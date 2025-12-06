using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MyWebApp.Api.Hubs
{
    [Authorize(AuthenticationSchemes = "Cookies,Bearer")]
    public class MusicPlaybackHub : Hub
    {
        private static readonly Dictionary<string, PlaybackSession> UserSessions = new();
        private static readonly object LockObject = new();

        private string? GetUserId()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? Context.User?.FindFirst("sub")?.Value
                   ?? Context.User?.FindFirst(ClaimTypes.Name)?.Value
                   ?? Context.User?.Identity?.Name;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = GetUserId();

            Console.WriteLine($"[MusicPlaybackHub] Connection attempt - UserId: {userId}, IsAuthenticated: {Context.User?.Identity?.IsAuthenticated}");
            
            if (Context.User != null)
            {
                Console.WriteLine($"[MusicPlaybackHub] User claims:");
                foreach (var claim in Context.User.Claims)
                {
                    Console.WriteLine($"  - {claim.Type}: {claim.Value}");
                }
            }

            if (!string.IsNullOrEmpty(userId))
            {
                lock (LockObject)
                {
                    if (UserSessions.TryGetValue(userId, out var existingSession))
                    {
                        // Notify the old connection that it's being replaced
                        if (existingSession.ConnectionId != Context.ConnectionId)
                        {
                            Clients.Client(existingSession.ConnectionId)
                                .SendAsync("SessionReplaced", new
                                {
                                    newDevice = GetDeviceInfo(),
                                    message = "Playback has started on another device"
                                });
                        }
                    }

                    UserSessions[userId] = new PlaybackSession
                    {
                        UserId = userId,
                        ConnectionId = Context.ConnectionId,
                        DeviceInfo = GetDeviceInfo(),
                        ConnectedAt = DateTime.UtcNow
                    };
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                Console.WriteLine($"[MusicPlaybackHub] User {userId} connected from {GetDeviceInfo()}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = GetUserId();

            if (!string.IsNullOrEmpty(userId))
            {
                lock (LockObject)
                {
                    if (UserSessions.TryGetValue(userId, out var session) 
                        && session.ConnectionId == Context.ConnectionId)
                    {
                        UserSessions.Remove(userId);
                    }
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                Console.WriteLine($"[MusicPlaybackHub] User {userId} disconnected");
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task RegisterDevice(string deviceId, string deviceName, string deviceType)
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("RegisterDeviceResult", new
                {
                    success = false,
                    message = "User not authenticated"
                });
                return;
            }

            lock (LockObject)
            {
                if (UserSessions.TryGetValue(userId, out var session))
                {
                    session.DeviceId = deviceId;
                    session.DeviceName = deviceName;
                    session.DeviceInfo = deviceType;
                }
            }

            Console.WriteLine($"[MusicPlaybackHub] Device registered - User: {userId}, Device: {deviceName} ({deviceId}), Type: {deviceType}");

            await Clients.Caller.SendAsync("RegisterDeviceResult", new
            {
                success = true,
                message = "Device registered successfully",
                deviceId = deviceId,
                deviceName = deviceName
            });
        }

        public async Task StartPlayback(string songId, string songName)
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
                return;

            string deviceInfo = GetDeviceInfo();
            
            Console.WriteLine($"[MusicPlaybackHub] User {userId} started playing: {songName} on {deviceInfo}");

            // Gửi event StopPlayback (cho mobile compatibility)
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("StopPlayback", Context.ConnectionId);

            // Gửi event PausePlayback (cho web compatibility)
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("PausePlayback", new
                {
                    reason = "Playing on another device",
                    device = deviceInfo,
                    songId = songId,
                    songName = songName
                });

            Console.WriteLine($"   ✅ Sent StopPlayback + PausePlayback to other devices");

            // Update session
            lock (LockObject)
            {
                if (UserSessions.TryGetValue(userId, out var session))
                {
                    session.CurrentSongId = songId;
                    session.CurrentSongName = songName;
                    session.LastPlaybackTime = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Mobile/Web gọi method này khi bắt đầu phát nhạc
        /// Sẽ gửi lệnh StopPlayback đến TẤT CẢ thiết bị khác
        /// </summary>
        public async Task NotifyPlaybackStarted(string deviceId, string? deviceName = null)
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"⚠️ [PlaybackHub] NotifyPlaybackStarted called but no userId!");
                return;
            }

            // Sử dụng deviceName nếu được cung cấp, nếu không thì dùng GetDeviceInfo()
            string displayName = !string.IsNullOrEmpty(deviceName) ? deviceName : GetDeviceInfo();
            
            Console.WriteLine($"🎵 [PlaybackHub] Device {deviceId} (User: {userId}, Name: {displayName}) started playback");
            Console.WriteLine($"   Notifying other connections to stop");

            // Gửi lệnh StopPlayback đến TẤT CẢ kết nối khác của cùng user
            // Clients.Others = tất cả kết nối trừ kết nối hiện tại
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("StopPlayback", deviceId);

            // Cũng gửi event PausePlayback (cho web compatibility) với tên thiết bị thực
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("PausePlayback", new
                {
                    reason = "Playing on another device",
                    device = displayName,
                    songId = "",
                    songName = "",
                    sourceDeviceId = deviceId
                });

            Console.WriteLine($"   ✅ Sent StopPlayback event to other devices");

            // Update session với tên thiết bị
            lock (LockObject)
            {
                if (UserSessions.TryGetValue(userId, out var session))
                {
                    session.DeviceId = deviceId;
                    session.DeviceName = displayName;
                    session.LastPlaybackTime = DateTime.UtcNow;
                }
            }
        }

        public async Task RequestPlayback()
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
                return;

            lock (LockObject)
            {
                if (UserSessions.TryGetValue(userId, out var session))
                {
                    // Check if there's another active connection
                    if (session.ConnectionId != Context.ConnectionId)
                    {
                        // This is not the active device
                        Clients.Caller.SendAsync("PlaybackDenied", new
                        {
                            reason = "Playback is active on another device",
                            activeDevice = session.DeviceInfo,
                            canTakeover = true
                        });
                        return;
                    }
                }
            }

            await Clients.Caller.SendAsync("PlaybackAllowed");
        }

        public async Task TakeoverPlayback()
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
                return;

            lock (LockObject)
            {
                if (UserSessions.TryGetValue(userId, out var oldSession))
                {
                    // Notify old device
                    Clients.Client(oldSession.ConnectionId)
                        .SendAsync("SessionTakenOver", new
                        {
                            newDevice = GetDeviceInfo(),
                            message = "Playback taken over by another device"
                        });
                }

                // Update to new session
                UserSessions[userId] = new PlaybackSession
                {
                    UserId = userId,
                    ConnectionId = Context.ConnectionId,
                    DeviceInfo = GetDeviceInfo(),
                    ConnectedAt = DateTime.UtcNow
                };
            }

            await Clients.Caller.SendAsync("TakeoverSuccess", new
            {
                message = "You are now the active playback device"
            });
        }

        public async Task UpdatePlaybackState(string state, string? songId = null, double? position = null)
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
                return;

            // Broadcast to all other devices of this user
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("PlaybackStateChanged", new
                {
                    state = state,
                    songId = songId,
                    position = position,
                    device = GetDeviceInfo()
                });
        }

        private string GetDeviceInfo()
        {
            var userId = GetUserId();
            
            // Check if device has custom name registered
            if (!string.IsNullOrEmpty(userId))
            {
                lock (LockObject)
                {
                    if (UserSessions.TryGetValue(userId, out var session) 
                        && session.ConnectionId == Context.ConnectionId
                        && !string.IsNullOrEmpty(session.DeviceName))
                    {
                        return session.DeviceName;
                    }
                }
            }

            // Fallback to User-Agent detection
            var userAgent = Context.GetHttpContext()?.Request.Headers["User-Agent"].ToString() ?? "";
            
            if (userAgent.Contains("Dart") || userAgent.Contains("Flutter"))
                return "Mobile App";
            else if (userAgent.Contains("Mobile") || userAgent.Contains("Android") || userAgent.Contains("iPhone"))
                return "Mobile Browser";
            else if (userAgent.Contains("Electron"))
                return "Desktop App";
            else if (userAgent.Contains("Edg"))
                return "Edge Browser";
            else if (userAgent.Contains("Chrome"))
                return "Chrome Browser";
            else if (userAgent.Contains("Firefox"))
                return "Firefox Browser";
            else if (userAgent.Contains("Safari"))
                return "Safari Browser";
            else
                return "Web Browser";
        }
    }

    public class PlaybackSession
    {
        public string UserId { get; set; } = null!;
        public string ConnectionId { get; set; } = null!;
        public string DeviceInfo { get; set; } = null!;
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }
        public DateTime ConnectedAt { get; set; }
        public string? CurrentSongId { get; set; }
        public string? CurrentSongName { get; set; }
        public DateTime? LastPlaybackTime { get; set; }
    }
}
