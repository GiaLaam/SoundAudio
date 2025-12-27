using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace MyWebApp.Api.Hubs
{
    [Authorize(AuthenticationSchemes = "Cookies,Bearer")]
    public class MusicPlaybackHub : Hub
    {
        // Đổi từ Dictionary<userId, session> sang Dictionary<connectionId, session>
        // để hỗ trợ nhiều thiết bị cho cùng 1 user
        private static readonly Dictionary<string, PlaybackSession> ConnectionSessions = new();
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
                    // Lưu session theo connectionId (cho phép nhiều thiết bị)
                    ConnectionSessions[Context.ConnectionId] = new PlaybackSession
                    {
                        UserId = userId,
                        ConnectionId = Context.ConnectionId,
                        DeviceInfo = GetDeviceInfo(),
                        ConnectedAt = DateTime.UtcNow
                    };
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                Console.WriteLine($"[MusicPlaybackHub] User {userId} connected from {GetDeviceInfo()} (ConnectionId: {Context.ConnectionId})");
                
                // Log tổng số sessions
                lock (LockObject)
                {
                    Console.WriteLine($"[MusicPlaybackHub] Total active sessions: {ConnectionSessions.Count}");
                }
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
                    // Xóa session theo connectionId
                    ConnectionSessions.Remove(Context.ConnectionId);
                }

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
                
                Console.WriteLine($"[MusicPlaybackHub] User {userId} disconnected (ConnectionId: {Context.ConnectionId})");
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
                if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session))
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
            Console.WriteLine($"   Current ConnectionId: {Context.ConnectionId}");
            
            // Log các connections khác trong group
            lock (LockObject)
            {
                var otherConnections = ConnectionSessions.Values
                    .Where(s => s.UserId == userId && s.ConnectionId != Context.ConnectionId)
                    .ToList();
                Console.WriteLine($"   Other connections in group: {otherConnections.Count}");
                foreach (var conn in otherConnections)
                {
                    Console.WriteLine($"      - {conn.ConnectionId} ({conn.DeviceName ?? conn.DeviceInfo})");
                }
            }

            // Gửi event StopPlayback (cho mobile compatibility)
            Console.WriteLine($"   Sending StopPlayback to group user_{userId} except {Context.ConnectionId}");
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("StopPlayback", Context.ConnectionId);

            // Gửi event PausePlayback (cho web compatibility)
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("PausePlayback", new
                {
                    reason = "Playing on another device",
                    device = deviceInfo,
                    deviceName = deviceInfo,
                    songId = songId,
                    songName = songName
                });

            Console.WriteLine($"   ✅ Sent StopPlayback + PausePlayback to other devices");

            // Update session và reset các thiết bị khác
            lock (LockObject)
            {
                // Reset LastPlaybackTime của các thiết bị khác của cùng user
                foreach (var kvp in ConnectionSessions)
                {
                    if (kvp.Value.UserId == userId && kvp.Value.ConnectionId != Context.ConnectionId)
                    {
                        kvp.Value.LastPlaybackTime = null;
                    }
                }
                
                // Update thiết bị hiện tại
                if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session))
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
            Console.WriteLine($"   Current ConnectionId: {Context.ConnectionId}");
            
            // Log các connections khác trong group
            lock (LockObject)
            {
                var otherConnections = ConnectionSessions.Values
                    .Where(s => s.UserId == userId && s.ConnectionId != Context.ConnectionId)
                    .ToList();
                Console.WriteLine($"   Other connections in group: {otherConnections.Count}");
                foreach (var conn in otherConnections)
                {
                    Console.WriteLine($"      - {conn.ConnectionId} ({conn.DeviceName ?? conn.DeviceInfo})");
                }
            }

            // Gửi lệnh StopPlayback đến TẤT CẢ kết nối khác của cùng user
            Console.WriteLine($"   Sending StopPlayback to group user_{userId} except {Context.ConnectionId}");
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("StopPlayback", deviceId);

            // Cũng gửi event PausePlayback (cho web compatibility) với tên thiết bị thực
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("PausePlayback", new
                {
                    reason = "Playing on another device",
                    device = displayName,
                    deviceName = displayName,
                    songId = "",
                    songName = "",
                    sourceDeviceId = deviceId
                });

            Console.WriteLine($"   ✅ Sent StopPlayback event to other devices");

            // Update session với tên thiết bị và reset các thiết bị khác
            lock (LockObject)
            {
                // Reset LastPlaybackTime của các thiết bị khác của cùng user
                foreach (var kvp in ConnectionSessions)
                {
                    if (kvp.Value.UserId == userId && kvp.Value.ConnectionId != Context.ConnectionId)
                    {
                        kvp.Value.LastPlaybackTime = null;
                    }
                }
                
                // Update thiết bị hiện tại
                if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session))
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

            // Kiểm tra xem có thiết bị nào khác đang phát không
            lock (LockObject)
            {
                foreach (var kvp in ConnectionSessions)
                {
                    if (kvp.Value.UserId == userId && 
                        kvp.Value.ConnectionId != Context.ConnectionId &&
                        kvp.Value.LastPlaybackTime.HasValue &&
                        (DateTime.UtcNow - kvp.Value.LastPlaybackTime.Value).TotalMinutes < 5)
                    {
                        // Có thiết bị khác đang phát
                        Clients.Caller.SendAsync("PlaybackDenied", new
                        {
                            reason = "Playback is active on another device",
                            activeDevice = kvp.Value.DeviceName ?? kvp.Value.DeviceInfo,
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
                // Thông báo cho tất cả thiết bị khác của user này
                foreach (var kvp in ConnectionSessions)
                {
                    if (kvp.Value.UserId == userId && kvp.Value.ConnectionId != Context.ConnectionId)
                    {
                        Clients.Client(kvp.Value.ConnectionId)
                            .SendAsync("SessionTakenOver", new
                            {
                                newDevice = GetDeviceInfo(),
                                message = "Playback taken over by another device"
                            });
                    }
                }

                // Update session hiện tại
                if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session))
                {
                    session.LastPlaybackTime = DateTime.UtcNow;
                }
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
            // Check if device has custom name registered
            lock (LockObject)
            {
                if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session) 
                    && !string.IsNullOrEmpty(session.DeviceName))
                {
                    return session.DeviceName;
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

        /// <summary>
        /// Lấy danh sách tất cả thiết bị đang kết nối của user hiện tại
        /// </summary>
        public async Task<List<object>> GetConnectedDevices()
        {
            var userId = GetUserId();
            var devices = new List<object>();

            Console.WriteLine($"[MusicPlaybackHub] GetConnectedDevices called");
            Console.WriteLine($"   Current UserId: {userId}");
            Console.WriteLine($"   Current ConnectionId: {Context.ConnectionId}");

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"   ⚠️ UserId is null or empty!");
                return devices;
            }

            lock (LockObject)
            {
                Console.WriteLine($"   Total sessions in memory: {ConnectionSessions.Count}");
                
                // Log tất cả sessions để debug
                foreach (var kvp in ConnectionSessions)
                {
                    Console.WriteLine($"   Session: ConnId={kvp.Key}, UserId={kvp.Value.UserId}, Device={kvp.Value.DeviceInfo}");
                }
                
                // Lấy tất cả sessions của user này
                foreach (var kvp in ConnectionSessions)
                {
                    if (kvp.Value.UserId == userId)
                    {
                        var isActive = kvp.Value.LastPlaybackTime.HasValue && 
                                       (DateTime.UtcNow - kvp.Value.LastPlaybackTime.Value).TotalMinutes < 5;
                        
                        Console.WriteLine($"   Device: {kvp.Value.DeviceName ?? kvp.Value.DeviceInfo}");
                        Console.WriteLine($"      LastPlaybackTime: {kvp.Value.LastPlaybackTime}");
                        Console.WriteLine($"      isActive: {isActive}");
                        
                        devices.Add(new
                        {
                            deviceId = kvp.Value.DeviceId ?? kvp.Value.ConnectionId,
                            deviceName = kvp.Value.DeviceName ?? kvp.Value.DeviceInfo,
                            connectionId = kvp.Value.ConnectionId,
                            isActive = isActive,
                            isCurrentDevice = kvp.Value.ConnectionId == Context.ConnectionId,
                            currentSong = new
                            {
                                songId = kvp.Value.CurrentSongId,
                                songName = kvp.Value.CurrentSongName
                            }
                        });
                    }
                }
            }

            Console.WriteLine($"   ✅ Found {devices.Count} devices for user {userId}");
            
            return await Task.FromResult(devices);
        }

        /// <summary>
        /// Đồng bộ vị trí phát nhạc đến các thiết bị khác
        /// </summary>
        public async Task SyncPlaybackPosition(string songId, int positionMs, bool isPlaying)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId)) return;

            // Broadcast đến tất cả thiết bị khác của user này
            await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
                .SendAsync("PlaybackPositionSync", new
                {
                    songId = songId,
                    positionMs = positionMs,
                    isPlaying = isPlaying
                });
        }

        /// <summary>
        /// Chuyển phát nhạc sang thiết bị khác
        /// </summary>
        public async Task TransferPlayback(string targetDeviceId, string songId, int positionMs, bool isPlaying, string? songName = null, string? imageUrl = null, string? artistName = null)
        {
            var userId = GetUserId();

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"⚠️ [MusicPlaybackHub] TransferPlayback: User not authenticated");
                return;
            }

            Console.WriteLine($"🔄 [MusicPlaybackHub] TransferPlayback requested");
            Console.WriteLine($"   User: {userId}");
            Console.WriteLine($"   Target Device: {targetDeviceId}");
            Console.WriteLine($"   Song: {songId}");
            Console.WriteLine($"   Position: {positionMs}ms");
            Console.WriteLine($"   IsPlaying: {isPlaying}");
            Console.WriteLine($"   SongName: {songName}");
            Console.WriteLine($"   ImageUrl: {imageUrl}");
            Console.WriteLine($"   ArtistName: {artistName}");

            string? targetConnectionId = null;

            lock (LockObject)
            {
                // Tìm connectionId của thiết bị đích
                foreach (var kvp in ConnectionSessions)
                {
                    if (kvp.Value.UserId == userId && 
                        (kvp.Value.DeviceId == targetDeviceId || kvp.Value.ConnectionId == targetDeviceId))
                    {
                        targetConnectionId = kvp.Value.ConnectionId;
                        break;
                    }
                }
            }

            if (targetConnectionId == null)
            {
                Console.WriteLine($"❌ [MusicPlaybackHub] Target device not found: {targetDeviceId}");
                await Clients.Caller.SendAsync("TransferPlaybackResult", new
                {
                    success = false,
                    message = "Target device not found"
                });
                return;
            }

            // Gửi lệnh dừng phát cho tất cả thiết bị khác (bao gồm thiết bị hiện tại)
            await Clients.GroupExcept($"user_{userId}", targetConnectionId)
                .SendAsync("StopPlayback", targetDeviceId);

            // Gửi lệnh phát nhạc đến thiết bị đích
            await Clients.Client(targetConnectionId).SendAsync("StartPlaybackRemote", new
            {
                songId = songId,
                positionMs = positionMs,
                isPlaying = isPlaying,
                sourceDevice = GetDeviceInfo(),
                songName = songName ?? "",
                imageUrl = imageUrl ?? "",
                artistName = artistName ?? ""
            });

            Console.WriteLine($"✅ [MusicPlaybackHub] Playback transferred to {targetDeviceId}");

            await Clients.Caller.SendAsync("TransferPlaybackResult", new
            {
                success = true,
                message = "Playback transferred successfully",
                targetDevice = targetDeviceId
            });
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
