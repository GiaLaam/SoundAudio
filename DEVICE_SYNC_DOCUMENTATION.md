# Tài Liệu Chi Tiết: Tính Năng Đồng Bộ Thiết Bị Phát Nhạc

## Mục Lục
1. [Tổng Quan](#1-tổng-quan)
2. [Kiến Trúc Hệ Thống](#2-kiến-trúc-hệ-thống)
3. [Server - SignalR Hub](#3-server---signalr-hub)
4. [Web Client](#4-web-client)
5. [Mobile Client (Flutter)](#5-mobile-client-flutter)
6. [Flow Hoạt Động Chi Tiết](#6-flow-hoạt-động-chi-tiết)
7. [Các Vấn Đề Và Giải Pháp](#7-các-vấn-đề-và-giải-pháp)

---

## 1. Tổng Quan

### 1.1 Mô tả tính năng
Tính năng này cho phép đồng bộ phát nhạc giữa nhiều thiết bị (web và mobile) của cùng một user, tương tự Spotify Connect:
- **Chỉ một thiết bị phát tại một thời điểm**: Khi phát trên thiết bị A, thiết bị B tự động dừng
- **Chuyển thiết bị (Transfer Playback)**: Chuyển bài đang phát sang thiết bị khác, giữ nguyên vị trí
- **Đồng bộ vị trí thời gian thực**: Thanh tiến trình trên thiết bị không phát cũng chạy theo
- **Hiển thị danh sách thiết bị**: Xem tất cả thiết bị đang online và chọn để chuyển

### 1.2 Công nghệ sử dụng
- **Server**: ASP.NET Core với SignalR (WebSocket)
- **Web**: JavaScript với SignalR Client
- **Mobile**: Flutter với `signalr_netcore` package

---

## 2. Kiến Trúc Hệ Thống

```
┌─────────────────────────────────────────────────────────────────┐
│                        SignalR Hub (Server)                      │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                   MusicPlaybackHub                       │    │
│  │  - ConnectionSessions: Dictionary<connectionId, session> │    │
│  │  - Groups: user_{userId} (tất cả connections của 1 user) │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ WebSocket
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
   ┌─────────┐           ┌─────────┐           ┌─────────┐
   │  Web 1  │           │  Web 2  │           │ Mobile  │
   │ Chrome  │           │ Firefox │           │ Flutter │
   └─────────┘           └─────────┘           └─────────┘
        │                     │                     │
        └─────────────────────┴─────────────────────┘
                              │
                    Cùng 1 User (userId)
                    Group: user_123
```

### 2.2 Các Events chính

| Event | Hướng | Mô tả |
|-------|-------|-------|
| `StopPlayback` | Server → Client | Yêu cầu dừng phát |
| `StartPlaybackRemote` | Server → Client | Yêu cầu bắt đầu phát (transfer) |
| `PlaybackPositionSync` | Server → Client | Đồng bộ vị trí phát |
| `NotifyPlaybackStarted` | Client → Server | Thông báo đã bắt đầu phát |
| `TransferPlayback` | Client → Server | Yêu cầu chuyển sang thiết bị khác |
| `SyncPlaybackPosition` | Client → Server | Gửi vị trí phát hiện tại |
| `GetConnectedDevices` | Client → Server | Lấy danh sách thiết bị |

---

## 3. Server - SignalR Hub

### 3.1 Cấu trúc PlaybackSession

```csharp
// File: MusicPlaybackHub.cs

public class PlaybackSession
{
    public string UserId { get; set; } = null!;        // ID của user
    public string ConnectionId { get; set; } = null!;  // ID connection SignalR (unique mỗi lần kết nối)
    public string? DeviceId { get; set; }              // ID thiết bị (persistent)
    public string? DeviceName { get; set; }            // Tên thiết bị (VD: "iPhone 15 Pro")
    public string? DeviceInfo { get; set; }            // Thông tin thiết bị từ User-Agent
    public DateTime? LastPlaybackTime { get; set; }    // Thời điểm phát gần nhất (để xác định thiết bị active)
    public string? CurrentSongId { get; set; }         // ID bài đang phát
    public string? CurrentSongName { get; set; }       // Tên bài đang phát
}
```

**Giải thích:**
- `ConnectionId`: Mỗi lần client kết nối sẽ có ID khác nhau. Dùng làm key trong Dictionary.
- `DeviceId`: ID cố định của thiết bị, không đổi giữa các lần kết nối.
- `LastPlaybackTime`: Quan trọng để xác định thiết bị nào đang active. Thiết bị phát gần nhất trong 5 phút = active.

### 3.2 Lưu trữ Sessions

```csharp
// Static dictionary để lưu tất cả sessions
// Key = ConnectionId, Value = PlaybackSession
private static readonly Dictionary<string, PlaybackSession> ConnectionSessions = new();

// Lock object để thread-safe khi truy cập dictionary
private static readonly object LockObject = new();
```

**Tại sao dùng ConnectionId làm key thay vì UserId?**
- Một user có thể có nhiều thiết bị kết nối cùng lúc
- Mỗi thiết bị có ConnectionId riêng
- Cho phép quản lý từng connection độc lập

### 3.3 Kết nối và Đăng ký thiết bị

```csharp
public override async Task OnConnectedAsync()
{
    // Lấy userId từ JWT token (đã authenticate)
    var userId = GetUserId();
    
    if (!string.IsNullOrEmpty(userId))
    {
        // Thêm connection vào group của user
        // Group name = "user_{userId}"
        // Tất cả thiết bị của cùng user sẽ ở cùng group
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        
        // Tạo session mới cho connection này
        lock (LockObject)
        {
            ConnectionSessions[Context.ConnectionId] = new PlaybackSession
            {
                UserId = userId,
                ConnectionId = Context.ConnectionId,
                DeviceInfo = GetDeviceInfo()  // Lấy từ User-Agent header
            };
        }
    }
    
    await base.OnConnectedAsync();
}
```

**Giải thích:**
- `Groups.AddToGroupAsync`: SignalR Groups cho phép gửi message đến nhiều connections cùng lúc
- Mỗi user có 1 group riêng, tất cả thiết bị của user đó join vào group này
- Khi gửi `StopPlayback`, chỉ cần gửi đến group thay vì từng connection

```csharp
// Client gọi để đăng ký thông tin thiết bị chi tiết
public async Task RegisterDevice(string deviceId, string deviceName, string deviceType)
{
    var userId = GetUserId();
    if (string.IsNullOrEmpty(userId)) return;

    lock (LockObject)
    {
        if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session))
        {
            // Cập nhật thông tin thiết bị
            session.DeviceId = deviceId;      // VD: "a1b2c3d4-..."
            session.DeviceName = deviceName;  // VD: "iPhone 15 Pro"
            // deviceType có thể là "mobile", "web", "desktop"
        }
    }
}
```

### 3.4 Thông báo bắt đầu phát (Core Logic)

```csharp
public async Task NotifyPlaybackStarted(string deviceId, string? deviceName = null)
{
    var userId = GetUserId();
    if (string.IsNullOrEmpty(userId)) return;

    // Sử dụng deviceName nếu được cung cấp, nếu không thì dùng GetDeviceInfo()
    string displayName = !string.IsNullOrEmpty(deviceName) ? deviceName : GetDeviceInfo();

    // ===== BƯỚC 1: Gửi lệnh dừng đến TẤT CẢ thiết bị khác =====
    // GroupExcept: Gửi đến tất cả trong group TRỪ connection hiện tại
    await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
        .SendAsync("StopPlayback", deviceId);
    
    // Gửi thêm PausePlayback với thông tin chi tiết hơn (cho web)
    await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
        .SendAsync("PausePlayback", new
        {
            reason = "Playing on another device",
            device = displayName,
            deviceName = displayName,
            sourceDeviceId = deviceId
        });

    // ===== BƯỚC 2: Cập nhật trạng thái trong session =====
    lock (LockObject)
    {
        // Reset LastPlaybackTime của các thiết bị khác
        // Điều này đảm bảo chỉ có 1 thiết bị được đánh dấu "active"
        foreach (var kvp in ConnectionSessions)
        {
            if (kvp.Value.UserId == userId && kvp.Value.ConnectionId != Context.ConnectionId)
            {
                kvp.Value.LastPlaybackTime = null;  // Không còn active
            }
        }
        
        // Cập nhật thiết bị hiện tại là active
        if (ConnectionSessions.TryGetValue(Context.ConnectionId, out var session))
        {
            session.DeviceId = deviceId;
            session.DeviceName = displayName;
            session.LastPlaybackTime = DateTime.UtcNow;  // Đánh dấu active
        }
    }
}
```

**Flow hoạt động:**
```
Mobile nhấn Play
       │
       ▼
NotifyPlaybackStarted("mobile-123", "iPhone 15")
       │
       ├──► Clients.GroupExcept("user_123", mobileConnectionId)
       │         .SendAsync("StopPlayback", "mobile-123")
       │              │
       │              ▼
       │         Web nhận "StopPlayback" → audio.pause()
       │
       └──► Cập nhật session:
              - Mobile: LastPlaybackTime = now (active)
              - Web: LastPlaybackTime = null (inactive)
```

### 3.5 Lấy danh sách thiết bị

```csharp
public async Task<List<object>> GetConnectedDevices()
{
    var devices = new List<object>();
    var userId = GetUserId();
    
    if (string.IsNullOrEmpty(userId)) return devices;

    lock (LockObject)
    {
        foreach (var kvp in ConnectionSessions)
        {
            // Chỉ lấy sessions của user hiện tại
            if (kvp.Value.UserId == userId)
            {
                // Xác định thiết bị có đang active không
                // Active = có LastPlaybackTime trong vòng 5 phút
                var isActive = kvp.Value.LastPlaybackTime.HasValue && 
                               (DateTime.UtcNow - kvp.Value.LastPlaybackTime.Value).TotalMinutes < 5;
                
                devices.Add(new
                {
                    deviceId = kvp.Value.DeviceId ?? kvp.Value.ConnectionId,
                    deviceName = kvp.Value.DeviceName ?? kvp.Value.DeviceInfo,
                    connectionId = kvp.Value.ConnectionId,  // Quan trọng cho TransferPlayback
                    isActive = isActive,                     // Thiết bị đang phát
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
    
    return await Task.FromResult(devices);
}
```

**Kết quả trả về:**
```json
[
  {
    "deviceId": "iphone-abc123",
    "deviceName": "iPhone 15 Pro",
    "connectionId": "conn-xyz789",
    "isActive": true,
    "isCurrentDevice": false,
    "currentSong": { "songId": "1", "songName": "Song A" }
  },
  {
    "deviceId": "web-def456",
    "deviceName": "Chrome Browser",
    "connectionId": "conn-uvw456",
    "isActive": false,
    "isCurrentDevice": true,
    "currentSong": null
  }
]
```

### 3.6 Chuyển thiết bị phát (Transfer Playback)

```csharp
public async Task TransferPlayback(
    string targetDeviceId,   // ID thiết bị đích
    string songId,           // ID bài hát
    int positionMs,          // Vị trí hiện tại (milliseconds)
    bool isPlaying,          // Đang phát hay đang pause
    string? songName = null,
    string? imageUrl = null,
    string? artistName = null)
{
    var userId = GetUserId();
    if (string.IsNullOrEmpty(userId)) return;

    string? targetConnectionId = null;

    // ===== BƯỚC 1: Tìm connectionId của thiết bị đích =====
    lock (LockObject)
    {
        foreach (var kvp in ConnectionSessions)
        {
            // Match bằng DeviceId hoặc ConnectionId
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
        // Thiết bị không tìm thấy
        await Clients.Caller.SendAsync("TransferPlaybackResult", new
        {
            success = false,
            message = "Target device not found"
        });
        return;
    }

    // ===== BƯỚC 2: Gửi lệnh dừng đến tất cả thiết bị TRỪ thiết bị đích =====
    await Clients.GroupExcept($"user_{userId}", targetConnectionId)
        .SendAsync("StopPlayback", targetDeviceId);

    // ===== BƯỚC 3: Gửi lệnh phát đến thiết bị đích =====
    await Clients.Client(targetConnectionId).SendAsync("StartPlaybackRemote", new
    {
        songId = songId,
        positionMs = positionMs,
        isPlaying = isPlaying,
        sourceDevice = GetDeviceInfo(),  // Thiết bị nguồn
        songName = songName ?? "",
        imageUrl = imageUrl ?? "",
        artistName = artistName ?? ""
    });

    // Thông báo thành công
    await Clients.Caller.SendAsync("TransferPlaybackResult", new
    {
        success = true,
        message = "Playback transferred successfully"
    });
}
```

**Flow Transfer:**
```
Web (đang phát ở 1:30) nhấn Transfer → Mobile
       │
       ▼
TransferPlayback("mobile-conn-id", "song1", 90000, true, "Song Name", "/image.jpg")
       │
       ├──► Clients.GroupExcept(..., mobileConnId).SendAsync("StopPlayback")
       │         │
       │         ▼
       │    (Không có ai khác trong group)
       │
       ├──► Clients.Client(mobileConnId).SendAsync("StartPlaybackRemote", {
       │         songId: "song1",
       │         positionMs: 90000,
       │         isPlaying: true,
       │         songName: "Song Name",
       │         imageUrl: "/image.jpg"
       │    })
       │         │
       │         ▼
       │    Mobile nhận → Phát "song1" từ 1:30
       │
       └──► Clients.Caller.SendAsync("TransferPlaybackResult", { success: true })
                 │
                 ▼
            Web nhận → Hiện thông báo "Đã chuyển sang iPhone"
```

### 3.7 Đồng bộ vị trí phát

```csharp
public async Task SyncPlaybackPosition(string songId, int positionMs, bool isPlaying)
{
    var userId = GetUserId();
    if (string.IsNullOrEmpty(userId)) return;

    // Broadcast vị trí đến tất cả thiết bị khác của user
    await Clients.GroupExcept($"user_{userId}", Context.ConnectionId)
        .SendAsync("PlaybackPositionSync", new
        {
            songId = songId,
            positionMs = positionMs,
            isPlaying = isPlaying
        });
}
```

**Cách hoạt động:**
- Thiết bị đang phát gửi `SyncPlaybackPosition` mỗi 2 giây
- Server broadcast đến tất cả thiết bị khác
- Thiết bị khác nhận và cập nhật UI (thanh tiến trình)

---

## 4. Web Client

### 4.1 Khởi tạo SignalR Connection

```javascript
// File: playback-session.js

class PlaybackSessionManager {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.deviceId = this.getDeviceId();  // Lấy hoặc tạo device ID
    }

    // Tạo/lấy Device ID (lưu trong localStorage để persistent)
    getDeviceId() {
        let deviceId = localStorage.getItem('deviceId');
        if (!deviceId) {
            // Tạo UUID mới
            deviceId = 'web-' + crypto.randomUUID();
            localStorage.setItem('deviceId', deviceId);
        }
        return deviceId;
    }

    async connect(token) {
        // API base URL từ attribute của body
        const apiBaseUrl = document.body.getAttribute('data-api-base') || 'http://localhost:5289';
        
        // Tạo connection với JWT token
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl(`${apiBaseUrl}/hubs/playback`, {
                accessTokenFactory: () => token  // Token được gửi trong header Authorization
            })
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])  // Retry intervals
            .configureLogging(signalR.LogLevel.Information)
            .build();

        // ===== ĐĂNG KÝ CÁC EVENT LISTENERS =====
        this.setupEventListeners();

        // Kết nối
        await this.connection.start();
        this.isConnected = true;

        // Đăng ký thông tin thiết bị
        await this.registerDevice();
    }
}
```

### 4.2 Đăng ký Event Listeners

```javascript
setupEventListeners() {
    // ===== EVENT: StopPlayback =====
    // Nhận khi thiết bị khác bắt đầu phát
    this.connection.on('StopPlayback', (deviceId) => {
        console.log('[PlaybackSession] StopPlayback received from:', deviceId);
        
        // Dừng audio
        const audio = document.getElementById('audioElement');
        if (audio && !audio.paused) {
            audio.pause();
        }
        
        // Gọi callback nếu có
        if (this.onPauseCallback) {
            this.onPauseCallback({ 
                reason: 'Playing on another device',
                sourceDeviceId: deviceId 
            });
        }
    });

    // ===== EVENT: PausePlayback =====
    // Tương tự StopPlayback nhưng có thêm thông tin chi tiết
    this.connection.on('PausePlayback', (data) => {
        console.log('[PlaybackSession] PausePlayback:', data);
        
        const audio = document.getElementById('audioElement');
        if (audio && !audio.paused) {
            audio.pause();
        }
        
        // Hiện thông báo với tên thiết bị
        this.showDeviceSwitchNotification(data);
    });

    // ===== EVENT: StartPlaybackRemote =====
    // Nhận khi được transfer playback từ thiết bị khác
    this.connection.on('StartPlaybackRemote', async (data) => {
        console.log('[PlaybackSession] StartPlaybackRemote:', data);
        
        const songId = data.songId;
        const positionMs = data.positionMs || 0;
        const shouldPlay = data.isPlaying !== false;
        const songName = data.songName || '';
        const imageUrl = data.imageUrl || '';
        
        if (songId) {
            // Kiểm tra bài hát có trong playlist hiện tại không
            let foundInPlaylist = false;
            if (window.currentPlaylist && window.currentPlaylist.length > 0) {
                const idx = window.currentPlaylist.findIndex(s => s.id == songId);
                if (idx !== -1) {
                    foundInPlaylist = true;
                    window.currentPlaylistIndex = idx;
                }
            }
            
            // Nếu không có, fetch tất cả bài hát để có playlist cho next/prev
            if (!foundInPlaylist) {
                try {
                    const apiBaseUrl = document.body.getAttribute('data-api-base');
                    const response = await fetch(`${apiBaseUrl}/api/music`);
                    if (response.ok) {
                        const songs = await response.json();
                        const idx = songs.findIndex(s => s.id == songId);
                        if (idx !== -1 && window.setPlaylist) {
                            window.setPlaylist(songs, idx);
                        }
                    }
                } catch (error) {
                    console.error('Error fetching songs:', error);
                }
            }
            
            // Phát bài hát
            if (window.playSong) {
                window.playSong(songId, songName, '', imageUrl);
                
                // Seek đến vị trí sau khi audio load xong
                const audio = document.getElementById('audioElement');
                if (audio) {
                    audio.addEventListener('loadedmetadata', function onLoaded() {
                        audio.currentTime = positionMs / 1000;  // Convert ms to seconds
                        if (shouldPlay) {
                            audio.play();
                        }
                        audio.removeEventListener('loadedmetadata', onLoaded);
                    });
                }
            }
        }
    });

    // ===== EVENT: PlaybackPositionSync =====
    // Nhận vị trí phát từ thiết bị khác (mỗi 2 giây)
    this.connection.on('PlaybackPositionSync', (data) => {
        const audio = document.getElementById('audioElement');
        
        // Chỉ cập nhật UI nếu đang KHÔNG phát
        if (audio && audio.paused) {
            const positionMs = data.positionMs || 0;
            
            // Cập nhật thanh progress
            if (audio.duration && !isNaN(audio.duration)) {
                const progress = (positionMs / 1000 / audio.duration) * 100;
                const progressFill = document.querySelector('.progress-fill');
                if (progressFill) {
                    progressFill.style.width = progress + '%';
                }
                
                // Cập nhật thời gian hiển thị
                const currentTimeEl = document.getElementById('currentTime');
                if (currentTimeEl) {
                    const seconds = Math.floor(positionMs / 1000);
                    const mins = Math.floor(seconds / 60);
                    const secs = seconds % 60;
                    currentTimeEl.textContent = `${mins}:${secs.toString().padStart(2, '0')}`;
                }
            }
            
            // Lưu remote position để khi nhấn play sẽ seek đến đó
            window.remotePosition = positionMs;
        }
    });
}
```

### 4.3 Gửi thông báo phát nhạc

```javascript
// Gọi khi bắt đầu phát nhạc
async notifyPlaybackStart(songId, songName) {
    if (!this.isConnected) return;

    try {
        // Gọi Hub method StartPlayback
        await this.connection.invoke('StartPlayback', songId, songName);
        console.log('[PlaybackSession] Notified playback start');
    } catch (error) {
        console.error('[PlaybackSession] Error notifying playback:', error);
    }
}
```

### 4.4 Gửi đồng bộ vị trí

```javascript
// Gọi định kỳ mỗi 2 giây khi đang phát
async syncPlaybackPosition(songId, positionMs, isPlaying) {
    if (!this.isConnected) return;

    try {
        await this.connection.invoke('SyncPlaybackPosition', songId, positionMs, isPlaying);
    } catch (error) {
        // Ignore errors - sync không critical
    }
}
```

### 4.5 Timer gửi sync position

```javascript
// File: player-simple.js

// Biến lưu timer
let syncTimer = null;

// Bắt đầu gửi sync
function startSyncTimer() {
    stopSyncTimer();  // Dừng timer cũ nếu có
    
    syncTimer = setInterval(() => {
        // Kiểm tra điều kiện: đang phát và đã kết nối
        if (elements.audio && !elements.audio.paused && 
            window.sessionManager && window.sessionManager.isConnected) {
            
            const songId = window.currentPlayingSongId || '';
            const positionMs = Math.floor(elements.audio.currentTime * 1000);
            
            // Gửi sync
            window.sessionManager.syncPlaybackPosition(songId, positionMs, true);
        }
    }, 2000);  // Mỗi 2 giây
}

// Dừng timer
function stopSyncTimer() {
    if (syncTimer) {
        clearInterval(syncTimer);
        syncTimer = null;
    }
}

// Gắn vào audio events
elements.audio.addEventListener('play', function() {
    startSyncTimer();  // Bắt đầu sync khi phát
});

elements.audio.addEventListener('pause', function() {
    stopSyncTimer();  // Dừng sync khi pause
});
```

### 4.6 Xử lý nút Play với remote position

```javascript
elements.playBtn.addEventListener('click', function() {
    if (elements.audio.paused) {
        // ===== QUAN TRỌNG: Seek đến remote position nếu có =====
        if (window.remotePosition && window.remotePosition > 0) {
            console.log('[Player] Seeking to remote position:', window.remotePosition / 1000, 's');
            elements.audio.currentTime = window.remotePosition / 1000;
            window.remotePosition = null;  // Reset sau khi seek
        }
        
        // Thông báo server TRƯỚC khi phát
        if (window.sessionManager && window.sessionManager.isConnected && window.currentPlayingSongId) {
            window.sessionManager.notifyPlaybackStart(
                window.currentPlayingSongId, 
                elements.songName?.textContent || ''
            );
        }
        
        elements.audio.play();
    } else {
        elements.audio.pause();
    }
});
```

### 4.7 Transfer Playback

```javascript
async transferPlayback(targetDeviceId, songId, positionMs, isPlaying, songName, imageUrl, artistName) {
    if (!this.isConnected) return false;

    try {
        await this.connection.invoke(
            'TransferPlayback', 
            targetDeviceId, 
            songId, 
            positionMs, 
            isPlaying,
            songName || '',
            imageUrl || '',
            artistName || ''
        );
        return true;
    } catch (error) {
        console.error('Error transferring playback:', error);
        return false;
    }
}
```

---

## 5. Mobile Client (Flutter)

### 5.1 SignalR Service - Khởi tạo

```dart
// File: signalr_service.dart

class SignalRService {
  HubConnection? _hubConnection;
  String? _deviceId;
  String? _deviceName;
  bool _isConnected = false;
  
  // ===== STREAMS để broadcast events đến các listeners =====
  
  // Stream khi cần dừng phát (thiết bị khác bắt đầu phát)
  final StreamController<Map<String, dynamic>> _stopPlaybackController = 
      StreamController<Map<String, dynamic>>.broadcast();
  Stream<Map<String, dynamic>> get stopPlaybackStream => _stopPlaybackController.stream;
  
  // Stream khi nhận lệnh phát từ thiết bị khác (transfer)
  final StreamController<Map<String, dynamic>> _startPlaybackRemoteController = 
      StreamController<Map<String, dynamic>>.broadcast();
  Stream<Map<String, dynamic>> get startPlaybackRemoteStream => _startPlaybackRemoteController.stream;
  
  // Stream đồng bộ vị trí
  final StreamController<Map<String, dynamic>> _positionSyncController = 
      StreamController<Map<String, dynamic>>.broadcast();
  Stream<Map<String, dynamic>> get positionSyncStream => _positionSyncController.stream;

  // Khởi tạo service
  Future<void> initialize() async {
    // Lấy hoặc tạo Device ID (lưu trong SharedPreferences)
    final prefs = await SharedPreferences.getInstance();
    _deviceId = prefs.getString('device_id');
    if (_deviceId == null) {
      _deviceId = const Uuid().v4();  // Tạo UUID mới
      await prefs.setString('device_id', _deviceId!);
    }
    
    // Lấy tên thiết bị
    await _initDeviceInfo();
  }

  // Lấy thông tin thiết bị
  Future<void> _initDeviceInfo() async {
    final deviceInfo = DeviceInfoPlugin();
    
    if (Platform.isAndroid) {
      final androidInfo = await deviceInfo.androidInfo;
      _deviceId = androidInfo.id;
      _deviceName = '${androidInfo.manufacturer} ${androidInfo.model}';
      // VD: "Samsung Galaxy S21"
    } else if (Platform.isIOS) {
      final iosInfo = await deviceInfo.iosInfo;
      _deviceId = iosInfo.identifierForVendor ?? 'unknown-ios';
      _deviceName = _getReadableIOSDeviceName(iosInfo.utsname.machine);
      // Convert "iPhone16,1" → "iPhone 15 Pro"
    }
  }
}
```

### 5.2 Kết nối và Đăng ký Events

```dart
Future<void> connect(User user) async {
  if (_isConnected) return;

  try {
    // URL của SignalR Hub
    final serverUrl = 'https://your-api.com/hubs/playback';
    
    // Tạo connection với JWT token
    _hubConnection = HubConnectionBuilder()
      .withUrl(serverUrl, options: HttpConnectionOptions(
        accessTokenFactory: () async {
          return user.token;  // JWT token
        },
      ))
      .withAutomaticReconnect(retryDelays: [0, 2000, 5000, 10000, 30000])
      .build();

    // ===== ĐĂNG KÝ EVENT LISTENERS =====

    // EVENT: StopPlayback
    _hubConnection!.on('StopPlayback', (arguments) {
      print('📩 StopPlayback event received!');
      
      if (arguments != null && arguments.isNotEmpty) {
        final sendingDeviceId = arguments[0] as String;
        print('🛑 Received StopPlayback from device: $sendingDeviceId');
        
        // Thêm vào stream để AudioPlayerService xử lý
        _stopPlaybackController.add({
          'deviceId': sendingDeviceId,
          'deviceName': 'Another device',
        });
      }
    });

    // EVENT: PausePlayback (có thêm thông tin)
    _hubConnection!.on('PausePlayback', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        try {
          final data = arguments[0] as Map<String, dynamic>;
          final deviceName = data['deviceName'] ?? 'Another device';
          final songName = data['songName'] ?? '';
          
          _stopPlaybackController.add({
            'deviceId': data['sourceDeviceId'] ?? '',
            'deviceName': deviceName,
            'songName': songName,
          });
        } catch (e) {
          print('Error parsing PausePlayback: $e');
        }
      }
    });

    // EVENT: StartPlaybackRemote (transfer)
    _hubConnection!.on('StartPlaybackRemote', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        try {
          final data = arguments[0] as Map<String, dynamic>;
          print('🎵 Received StartPlaybackRemote:');
          print('   Song ID: ${data['songId']}');
          print('   Position: ${data['positionMs']}ms');
          
          // Thêm vào stream để AudioPlayerService xử lý
          _startPlaybackRemoteController.add(data);
        } catch (e) {
          print('Error parsing StartPlaybackRemote: $e');
        }
      }
    });

    // EVENT: PlaybackPositionSync
    _hubConnection!.on('PlaybackPositionSync', (arguments) {
      if (arguments != null && arguments.isNotEmpty) {
        try {
          final data = arguments[0] as Map<String, dynamic>;
          _positionSyncController.add(data);
        } catch (e) {
          print('Error parsing PlaybackPositionSync: $e');
        }
      }
    });

    // Bắt đầu connection
    await _hubConnection!.start();
    _isConnected = true;
    
    // Đăng ký thông tin thiết bị
    await _registerDevice();
    
  } catch (e) {
    print('Error connecting to SignalR: $e');
    _isConnected = false;
  }
}
```

### 5.3 Đăng ký thiết bị

```dart
Future<void> _registerDevice() async {
  if (_hubConnection == null || !_isConnected) return;

  try {
    await _hubConnection!.invoke('RegisterDevice', args: <Object>[
      _deviceId!,                    // Device ID
      _deviceName ?? 'Mobile App',   // Device Name
      Platform.isIOS ? 'ios' : 'android',  // Device Type
    ]);
    print('✅ Device registered: $_deviceName');
  } catch (e) {
    print('Error registering device: $e');
  }
}
```

### 5.4 Thông báo bắt đầu phát

```dart
Future<void> notifyPlaybackStarted({
  String? songId,
  String? songName,
  String? artistName,
  String? imageUrl,
}) async {
  if (_hubConnection == null || !_isConnected || _deviceId == null) {
    print('⚠️ SignalR not connected - cannot notify playback');
    return;
  }

  try {
    print('🎵 Notifying server about playback...');
    print('   Device ID: $_deviceId');
    print('   Device Name: $_deviceName');
    
    // Gọi Hub method với đúng signature: (deviceId, deviceName)
    await _hubConnection!.invoke('NotifyPlaybackStarted', args: <Object>[
      _deviceId!,
      _deviceName ?? 'Mobile App',
    ]);
    
    print('✅ NotifyPlaybackStarted called successfully');
  } catch (e) {
    print('❌ Error notifying playback: $e');
  }
}
```

### 5.5 Gửi đồng bộ vị trí

```dart
Future<void> syncPlaybackPosition(String songId, int positionMs, bool isPlaying) async {
  if (_hubConnection == null || !_isConnected) return;

  try {
    await _hubConnection!.invoke('SyncPlaybackPosition', args: <Object>[
      songId,
      positionMs,
      isPlaying,
    ]);
  } catch (e) {
    // Ignore errors - sync không critical
  }
}
```

### 5.6 Transfer Playback

```dart
Future<bool> transferPlayback(
  String targetDeviceId,
  String songId,
  Duration position,
  bool isPlaying, {
  String? songName,
  String? imageUrl,
  String? artistName,
}) async {
  if (_hubConnection == null || !_isConnected) {
    return false;
  }

  try {
    await _hubConnection!.invoke('TransferPlayback', args: <Object>[
      targetDeviceId,
      songId,
      position.inMilliseconds,
      isPlaying,
      songName ?? '',
      imageUrl ?? '',
      artistName ?? '',
    ]);
    
    return true;
  } catch (e) {
    print('Error transferring playback: $e');
    return false;
  }
}
```

### 5.7 AudioPlayerService - Xử lý Events

```dart
// File: audio_player_service.dart

class AudioPlayerService {
  final AudioPlayer _player = AudioPlayer();
  final SignalRService _signalR = SignalRService();
  
  // Stream cho UI lắng nghe remote position
  final BehaviorSubject<Duration?> remotePositionStream = BehaviorSubject.seeded(null);
  
  // Timer gửi sync
  Timer? _syncTimer;

  Future<void> init() async {
    await _signalR.initialize();
    
    // ===== XỬ LÝ STOP PLAYBACK =====
    _signalR.stopPlaybackStream.listen((data) {
      final deviceName = data['deviceName'] ?? 'Another device';
      
      print('🛑 Received stop command from: $deviceName');
      
      // Dừng phát
      pause();
      
      // Gửi thông báo để UI hiển thị
      _devicePlaybackNotificationController.add({
        'deviceName': deviceName,
        'message': 'Đang phát trên $deviceName',
      });
    });

    // ===== XỬ LÝ POSITION SYNC =====
    _signalR.positionSyncStream.listen((data) {
      final positionMs = data['positionMs'] as int? ?? 0;
      final isPlaying = data['isPlaying'] as bool? ?? false;
      
      // Chỉ cập nhật nếu KHÔNG đang phát
      if (!_player.playing) {
        remotePositionStream.add(Duration(milliseconds: positionMs));
      }
    });

    // ===== XỬ LÝ START PLAYBACK REMOTE (TRANSFER) =====
    _signalR.startPlaybackRemoteStream.listen((data) async {
      final songId = data['songId'] as String?;
      final positionMs = data['positionMs'] as int? ?? 0;
      final shouldPlay = data['isPlaying'] as bool? ?? true;
      final remoteImageUrl = data['imageUrl'] as String? ?? '';
      
      if (songId != null && songId.isNotEmpty) {
        try {
          Song? song;
          
          // Kiểm tra trong playlist hiện tại
          if (_songs.isNotEmpty) {
            final idx = _songs.indexWhere((s) => s.id == songId);
            if (idx != -1) {
              song = _songs[idx];
              // Seek đến bài đó trong playlist
              await _player.seek(Duration(milliseconds: positionMs), index: idx);
            }
          }
          
          // Nếu không có, fetch từ API
          if (song == null) {
            final allSongs = await ApiService.fetchSongs();
            final idx = allSongs.indexWhere((s) => s.id == songId);
            if (idx != -1) {
              song = allSongs[idx];
              // Set playlist để next/prev hoạt động
              await setPlaylist(allSongs, startIndex: idx);
              await _player.seek(Duration(milliseconds: positionMs));
            }
          }
          
          // Phát hoặc pause
          if (shouldPlay) {
            await _player.play();
            // Thông báo server
            await _signalR.notifyPlaybackStarted(songId: song?.id);
          } else {
            await _player.pause();
          }
          
        } catch (e) {
          print('Error starting playback from remote: $e');
        }
      }
    });

    // ===== BẮT ĐẦU/DỪNG SYNC TIMER KHI PLAYING STATE THAY ĐỔI =====
    _player.playingStream.listen((playing) {
      if (playing) {
        _startSyncTimer();
        remotePositionStream.add(null);  // Reset remote position
      } else {
        _stopSyncTimer();
      }
    });
  }

  // ===== SYNC TIMER =====
  void _startSyncTimer() {
    _stopSyncTimer();
    _syncTimer = Timer.periodic(const Duration(seconds: 2), (_) {
      final currentSong = currentSongStream.valueOrNull;
      if (currentSong != null && _player.playing) {
        _signalR.syncPlaybackPosition(
          currentSong.id ?? '',
          _player.position.inMilliseconds,
          true,
        );
      }
    });
  }

  void _stopSyncTimer() {
    _syncTimer?.cancel();
    _syncTimer = null;
  }
}
```

### 5.8 Xử lý nút Play với remote position

```dart
Future<void> play() async {
  try {
    // ===== QUAN TRỌNG: Seek đến remote position nếu có =====
    final remotePos = remotePositionStream.valueOrNull;
    if (remotePos != null && !_player.playing) {
      print('Seeking to remote position ${remotePos.inSeconds}s');
      await _player.seek(remotePos);
      remotePositionStream.add(null);  // Reset
    }
    
    // Thông báo server TRƯỚC khi phát
    final currentSong = currentSongStream.valueOrNull;
    if (currentSong != null) {
      await _signalR.notifyPlaybackStarted(
        songId: currentSong.id.toString(),
        songName: currentSong.name ?? 'Unknown',
      );
    }
    
    await _player.play();
  } catch (e) {
    print('Error playing: $e');
  }
}
```

### 5.9 UI - Hiển thị remote position

```dart
// File: player_screen.dart

// Stream kết hợp position local và remote
Stream<PositionData> get _positionDataStream =>
    Rx.combineLatest4<Duration, Duration?, bool, Duration?, PositionData>(
      widget.audioPlayer.positionStream,        // Position từ player
      widget.audioPlayer.durationStream,         // Duration
      widget.audioPlayer.playingStream,          // Đang phát?
      AudioPlayerService().remotePositionStream, // Remote position
      (position, duration, isPlaying, remotePosition) {
        // Nếu không đang phát VÀ có remote position → hiển thị remote
        Duration displayPosition = position;
        if (!isPlaying && remotePosition != null) {
          displayPosition = remotePosition;
        }
        
        return PositionData(
          position: displayPosition,
          duration: duration ?? Duration.zero,
          isPlaying: isPlaying,
        );
      },
    );
```

### 5.10 UI - Nút Play/Pause

```dart
// QUAN TRỌNG: Gọi AudioPlayerService thay vì audioPlayer trực tiếp
GestureDetector(
  onTap: () async {
    if (widget.audioPlayer.playing) {
      // Gọi service để có thể xử lý logic khác
      await AudioPlayerService().pause();
    } else {
      // Gọi service để seek remote position và notify server
      await AudioPlayerService().play();
    }
  },
  child: Container(
    // ... UI
  ),
),
```

---

## 6. Flow Hoạt Động Chi Tiết

### 6.1 Flow: Mobile phát nhạc, Web dừng

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MOBILE                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 1. User nhấn Play                                                            │
│    ↓                                                                         │
│ 2. AudioPlayerService.play() được gọi                                       │
│    ↓                                                                         │
│ 3. Gọi signalR.notifyPlaybackStarted(deviceId, deviceName)                  │
│    ↓                                                                         │
│ 4. Gửi đến Server: invoke('NotifyPlaybackStarted', 'mobile-123', 'iPhone')  │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SERVER                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 5. NotifyPlaybackStarted("mobile-123", "iPhone") được gọi                   │
│    ↓                                                                         │
│ 6. Clients.GroupExcept("user_123", mobileConnId)                            │
│         .SendAsync("StopPlayback", "mobile-123")                            │
│    ↓                                                                         │
│ 7. Cập nhật session:                                                         │
│    - Mobile: LastPlaybackTime = DateTime.UtcNow                             │
│    - Web: LastPlaybackTime = null                                           │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                               WEB                                            │
├─────────────────────────────────────────────────────────────────────────────┤
│ 8. connection.on('StopPlayback') được trigger                               │
│    ↓                                                                         │
│ 9. audio.pause()                                                             │
│    ↓                                                                         │
│ 10. Hiện thông báo "Đang phát trên iPhone"                                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 6.2 Flow: Transfer từ Web sang Mobile

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              WEB (đang phát ở 1:30)                         │
├─────────────────────────────────────────────────────────────────────────────┤
│ 1. User mở Device Dialog, chọn iPhone                                       │
│    ↓                                                                         │
│ 2. Lấy thông tin: songId, positionMs (90000), isPlaying (true)              │
│    ↓                                                                         │
│ 3. Gọi transferPlayback("mobile-conn-id", "song1", 90000, true, ...)       │
│    ↓                                                                         │
│ 4. Sau khi thành công: audio.pause()                                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SERVER                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 5. TransferPlayback(...) được gọi                                           │
│    ↓                                                                         │
│ 6. Tìm connectionId của mobile từ targetDeviceId                            │
│    ↓                                                                         │
│ 7. Clients.GroupExcept(..., mobileConnId).SendAsync("StopPlayback")         │
│    (Web nhận StopPlayback nhưng đã pause rồi nên không ảnh hưởng)          │
│    ↓                                                                         │
│ 8. Clients.Client(mobileConnId).SendAsync("StartPlaybackRemote", {          │
│        songId: "song1",                                                      │
│        positionMs: 90000,                                                    │
│        isPlaying: true,                                                      │
│        songName: "...",                                                      │
│        imageUrl: "..."                                                       │
│    })                                                                        │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MOBILE                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 9. startPlaybackRemoteStream nhận event                                     │
│    ↓                                                                         │
│ 10. Tìm bài hát trong playlist hoặc fetch từ API                            │
│    ↓                                                                         │
│ 11. setPlaylist(allSongs, startIndex: idx) - để next/prev hoạt động        │
│    ↓                                                                         │
│ 12. player.seek(Duration(milliseconds: 90000)) - seek đến 1:30             │
│    ↓                                                                         │
│ 13. player.play()                                                            │
│    ↓                                                                         │
│ 14. signalR.notifyPlaybackStarted() - thông báo đang phát                  │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 6.3 Flow: Đồng bộ vị trí

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         MOBILE (đang phát)                                   │
├─────────────────────────────────────────────────────────────────────────────┤
│ Mỗi 2 giây:                                                                  │
│ 1. _syncTimer trigger                                                        │
│    ↓                                                                         │
│ 2. signalR.syncPlaybackPosition(songId, positionMs, true)                   │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SERVER                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 3. SyncPlaybackPosition(songId, positionMs, isPlaying)                      │
│    ↓                                                                         │
│ 4. Clients.GroupExcept(..., mobileConnId)                                   │
│        .SendAsync("PlaybackPositionSync", { songId, positionMs, isPlaying })│
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          WEB (đang pause)                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│ 5. connection.on('PlaybackPositionSync') trigger                            │
│    ↓                                                                         │
│ 6. Kiểm tra: audio.paused === true                                          │
│    ↓                                                                         │
│ 7. Cập nhật UI:                                                              │
│    - progressFill.style.width = (positionMs / duration) * 100 + '%'         │
│    - currentTime.textContent = formatTime(positionMs)                       │
│    ↓                                                                         │
│ 8. Lưu: window.remotePosition = positionMs                                  │
│    (Để khi nhấn Play sẽ seek đến vị trí này)                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 6.4 Flow: Nhấn Play sau khi nhận sync

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          WEB (remotePosition = 90000)                        │
├─────────────────────────────────────────────────────────────────────────────┤
│ 1. User nhấn Play                                                            │
│    ↓                                                                         │
│ 2. Kiểm tra: window.remotePosition > 0 → true                               │
│    ↓                                                                         │
│ 3. audio.currentTime = 90000 / 1000 = 90 (giây) = 1:30                     │
│    ↓                                                                         │
│ 4. window.remotePosition = null (reset)                                     │
│    ↓                                                                         │
│ 5. sessionManager.notifyPlaybackStart(songId, songName)                     │
│    ↓                                                                         │
│ 6. audio.play() → Phát từ 1:30                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              SERVER                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 7. StartPlayback hoặc NotifyPlaybackStarted được gọi                        │
│    ↓                                                                         │
│ 8. Gửi StopPlayback đến Mobile                                              │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                              MOBILE                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│ 9. stopPlaybackStream nhận event                                            │
│    ↓                                                                         │
│ 10. player.pause()                                                           │
│    ↓                                                                         │
│ 11. Hiện snackbar "Đang phát trên Chrome"                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 7. Các Vấn Đề Và Giải Pháp

### 7.1 Vấn đề: Nhiều thiết bị cùng user

**Vấn đề:** Một user có thể có nhiều thiết bị (2 web tabs, 1 mobile). Cần quản lý tất cả.

**Giải pháp:** 
- Sử dụng `ConnectionId` làm key thay vì `UserId`
- Sử dụng SignalR Groups để gom tất cả connections của 1 user

```csharp
// Mỗi connection có ID riêng
ConnectionSessions[Context.ConnectionId] = new PlaybackSession { ... };

// Tất cả connections của user ở cùng group
await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
```

### 7.2 Vấn đề: Xác định thiết bị đang active

**Vấn đề:** Làm sao biết thiết bị nào đang phát nhạc?

**Giải pháp:** Sử dụng `LastPlaybackTime`

```csharp
// Khi phát: cập nhật thời gian
session.LastPlaybackTime = DateTime.UtcNow;

// Reset các thiết bị khác
foreach (var other in otherSessions) {
    other.LastPlaybackTime = null;
}

// Kiểm tra active: có thời gian trong 5 phút gần đây
var isActive = session.LastPlaybackTime.HasValue && 
               (DateTime.UtcNow - session.LastPlaybackTime.Value).TotalMinutes < 5;
```

### 7.3 Vấn đề: Next/Prev không hoạt động sau transfer

**Vấn đề:** Khi transfer, thiết bị đích chỉ nhận 1 bài, không có playlist.

**Giải pháp:** Fetch toàn bộ playlist khi nhận transfer

```dart
// Nếu bài không có trong playlist hiện tại
if (song == null) {
    // Fetch TẤT CẢ bài hát
    final allSongs = await ApiService.fetchSongs();
    
    // Set làm playlist với startIndex đúng
    await setPlaylist(allSongs, startIndex: idx);
}
```

### 7.4 Vấn đề: Ảnh không hiển thị sau transfer

**Vấn đề:** Khi transfer, `imageUrl` không được gửi kèm.

**Giải pháp:** Thêm `imageUrl` vào `TransferPlayback` và `StartPlaybackRemote`

```csharp
// Hub method nhận thêm tham số
public async Task TransferPlayback(..., string? imageUrl = null)

// Gửi kèm trong event
await Clients.Client(targetConnectionId).SendAsync("StartPlaybackRemote", new {
    songId = songId,
    imageUrl = imageUrl ?? "",
    ...
});
```

### 7.5 Vấn đề: Web không dừng khi Mobile phát

**Vấn đề:** Player screen gọi `audioPlayer.play()` trực tiếp, bypass `AudioPlayerService`.

**Giải pháp:** Luôn gọi qua `AudioPlayerService`

```dart
// SAI - bypass notify server
await widget.audioPlayer.play();

// ĐÚNG - có notify server
await AudioPlayerService().play();
```

### 7.6 Vấn đề: Thanh tiến trình không sync trên Mobile

**Vấn đề:** Mobile không cập nhật UI khi nhận `PlaybackPositionSync`.

**Giải pháp:** Thêm `remotePositionStream` vào combine stream của UI

```dart
Stream<PositionData> get _positionDataStream =>
    Rx.combineLatest4<Duration, Duration?, bool, Duration?, PositionData>(
      widget.audioPlayer.positionStream,
      widget.audioPlayer.durationStream,
      widget.audioPlayer.playingStream,
      AudioPlayerService().remotePositionStream,  // THÊM STREAM NÀY
      (position, duration, isPlaying, remotePosition) {
        // Hiển thị remote position khi không phát
        if (!isPlaying && remotePosition != null) {
          return PositionData(position: remotePosition, ...);
        }
        return PositionData(position: position, ...);
      },
    );
```

### 7.7 Vấn đề: Snackbar hiển thị nhiều lần

**Vấn đề:** Nhận nhiều events liên tiếp, snackbar hiển thị chồng chéo.

**Giải pháp:** Thêm cooldown

```dart
DateTime? _lastSnackbarTime;
final Duration _snackbarCooldown = const Duration(seconds: 3);

void _showSnackbar(String message) {
  final now = DateTime.now();
  
  // Kiểm tra cooldown
  if (_lastSnackbarTime != null && 
      now.difference(_lastSnackbarTime!) < _snackbarCooldown) {
    return;  // Bỏ qua
  }
  
  _lastSnackbarTime = now;
  
  // Ẩn snackbar cũ
  ScaffoldMessenger.of(context).hideCurrentSnackBar();
  
  // Hiện snackbar mới
  ScaffoldMessenger.of(context).showSnackBar(...);
}
```

---

## Tổng Kết

### Các điểm quan trọng cần nhớ:

1. **SignalR Groups**: Sử dụng để gửi message đến nhiều connections cùng lúc
2. **ConnectionId vs DeviceId**: 
   - ConnectionId thay đổi mỗi lần kết nối
   - DeviceId cố định, lưu trong localStorage/SharedPreferences
3. **Luôn thông báo server trước khi phát**: Để các thiết bị khác biết mà dừng
4. **Sync position định kỳ**: Mỗi 2 giây, gửi vị trí hiện tại
5. **Remote position**: Lưu lại để khi nhấn Play sẽ seek đến đó
6. **Gọi qua Service**: Luôn gọi play/pause qua AudioPlayerService, không trực tiếp

### Files quan trọng:

| File | Mô tả |
|------|-------|
| `MusicPlaybackHub.cs` | Server SignalR Hub - xử lý tất cả logic |
| `playback-session.js` | Web SignalR client - kết nối và xử lý events |
| `player-simple.js` | Web audio player - điều khiển phát nhạc |
| `signalr_service.dart` | Mobile SignalR client |
| `audio_player_service.dart` | Mobile audio player service |
| `player_screen.dart` | Mobile player UI |

---

*Tài liệu này được tạo để học tập và tham khảo. Cập nhật: 2024*
