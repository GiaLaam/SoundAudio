// Music Playback Session Manager với SignalR
class PlaybackSessionManager {
    constructor() {
        this.connection = null;
        this.isConnected = false;
        this.currentDevice = null;
        this.onPauseCallback = null;
        this.onSessionReplacedCallback = null;
    }

    async initialize() {
        const apiBaseUrl = document.body.getAttribute('data-api-base') || 'http://localhost:5289';
        
        // Check if user is authenticated
        const isAuthenticated = document.body.getAttribute('data-authenticated') === 'true';
        
        if (!isAuthenticated) {
            console.log('[PlaybackSession] User not authenticated, skipping SignalR connection');
            return;
        }

        try {
            // Get SignalR token from MVC
            const tokenResponse = await fetch('/api/session/signalr-token', {
                credentials: 'include'
            });
            
            if (!tokenResponse.ok) {
                console.error('[PlaybackSession] Failed to get SignalR token');
                return;
            }

            const tokenData = await tokenResponse.json();
            if (!tokenData.success) {
                console.error('[PlaybackSession] Invalid token response');
                return;
            }

            console.log('[PlaybackSession] Got token for user:', tokenData.userName);

            // Initialize SignalR connection with JWT token
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl(`${apiBaseUrl}/hubs/playback`, {
                    accessTokenFactory: () => tokenData.token
                })
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            // Setup event handlers
            this.setupEventHandlers();

            // Start connection
            await this.connection.start();
            this.isConnected = true;
            console.log('[PlaybackSession] Connected to SignalR hub');
            console.log('[PlaybackSession] UserId:', tokenData.userId);

            // Register device with server
            await this.registerDevice();
            
        } catch (error) {
            console.error('[PlaybackSession] Connection error:', error);
        }
    }

    async registerDevice() {
        if (!this.isConnected) return;

        try {
            const deviceId = this.getDeviceId();
            const deviceName = this.getDeviceName();
            
            console.log('[PlaybackSession] Registering device:', deviceName);
            
            await this.connection.invoke('RegisterDevice', deviceId, deviceName, 'Web');
            console.log('[PlaybackSession] Device registered successfully');
        } catch (error) {
            console.error('[PlaybackSession] Failed to register device:', error);
        }
    }

    getDeviceId() {
        let deviceId = localStorage.getItem('playback_device_id');
        if (!deviceId) {
            deviceId = 'web_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
            localStorage.setItem('playback_device_id', deviceId);
        }
        return deviceId;
    }

    getDeviceName() {
        const userAgent = navigator.userAgent;
        if (userAgent.includes('Edg')) return 'Microsoft Edge';
        if (userAgent.includes('Chrome')) return 'Google Chrome';
        if (userAgent.includes('Firefox')) return 'Mozilla Firefox';
        if (userAgent.includes('Safari')) return 'Safari';
        return 'Web Browser';
    }

    setupEventHandlers() {
        // When another device starts playing
        this.connection.on('PausePlayback', (data) => {
            console.log('[PlaybackSession] Pause requested from another device:', data);
            
            this.showDeviceSwitchNotification(data);
            
            if (this.onPauseCallback) {
                this.onPauseCallback(data);
            }
        });

        // StopPlayback event (for mobile compatibility)
        this.connection.on('StopPlayback', (deviceId) => {
            console.log('[PlaybackSession] Received StopPlayback from device:', deviceId);
            console.log('[PlaybackSession] Pausing playback');
            
            if (this.onPauseCallback) {
                this.onPauseCallback({ 
                    reason: 'Playing on another device',
                    sourceDeviceId: deviceId 
                });
            }
        });

        // When session is replaced by another connection
        this.connection.on('SessionReplaced', (data) => {
            console.log('[PlaybackSession] Session replaced:', data);
            
            this.showSessionReplacedDialog(data);
            
            if (this.onSessionReplacedCallback) {
                this.onSessionReplacedCallback(data);
            }
        });

        // When session is taken over
        this.connection.on('SessionTakenOver', (data) => {
            console.log('[PlaybackSession] Session taken over:', data);
            
            this.showSessionTakenOverNotification(data);
            
            if (this.onPauseCallback) {
                this.onPauseCallback({ reason: data.message });
            }
        });

        // Playback denied
        this.connection.on('PlaybackDenied', (data) => {
            console.log('[PlaybackSession] Playback denied:', data);
            
            if (data.canTakeover) {
                this.showTakeoverDialog(data);
            }
        });

        // Playback allowed
        this.connection.on('PlaybackAllowed', () => {
            console.log('[PlaybackSession] Playback allowed');
        });

        // Takeover success
        this.connection.on('TakeoverSuccess', (data) => {
            console.log('[PlaybackSession] Takeover success:', data);
            this.showSuccessNotification('Bạn đang phát nhạc trên thiết bị này');
        });

        // StartPlaybackRemote - khi được yêu cầu phát nhạc từ thiết bị khác
        this.connection.on('StartPlaybackRemote', async (data) => {
            console.log('[PlaybackSession] StartPlaybackRemote received:', data);
            
            const songId = data.songId;
            const positionMs = data.positionMs || 0;
            const shouldPlay = data.isPlaying !== false;
            const sourceDevice = data.sourceDevice || 'Another device';
            const songName = data.songName || '';
            const imageUrl = data.imageUrl || '';
            const artistName = data.artistName || '';
            
            if (songId) {
                console.log(`[PlaybackSession] Starting playback: songId=${songId}, position=${positionMs}ms`);
                console.log(`[PlaybackSession] Song info: name=${songName}, image=${imageUrl}`);
                
                // Kiểm tra xem bài hát có trong playlist hiện tại không
                let foundInPlaylist = false;
                if (window.currentPlaylist && window.currentPlaylist.length > 0) {
                    const idx = window.currentPlaylist.findIndex(s => s.id === songId || s.id === parseInt(songId));
                    if (idx !== -1) {
                        foundInPlaylist = true;
                        window.currentPlaylistIndex = idx;
                        console.log(`[PlaybackSession] Found song in current playlist at index ${idx}`);
                    }
                }
                
                // Nếu không có trong playlist, fetch tất cả bài hát từ API
                if (!foundInPlaylist) {
                    try {
                        const apiBaseUrl = document.body.getAttribute('data-api-base') || 'http://localhost:5289';
                        console.log('[PlaybackSession] Fetching all songs from API...');
                        const response = await fetch(`${apiBaseUrl}/api/music`);
                        if (response.ok) {
                            const songs = await response.json();
                            console.log(`[PlaybackSession] Fetched ${songs.length} songs`);
                            
                            // Tìm index của bài hát
                            const idx = songs.findIndex(s => s.id === songId || s.id === parseInt(songId));
                            if (idx !== -1 && window.setPlaylist) {
                                window.setPlaylist(songs, idx);
                                console.log(`[PlaybackSession] Set playlist with ${songs.length} songs, starting at index ${idx}`);
                            }
                        }
                    } catch (error) {
                        console.error('[PlaybackSession] Error fetching songs:', error);
                    }
                }
                
                // Gọi hàm play của player với thông tin bài hát
                if (window.playSong) {
                    window.playSong(songId, songName, artistName, imageUrl);
                    
                    // Seek đến vị trí sau khi load
                    const audio = document.getElementById('audioElement');
                    if (audio) {
                        audio.addEventListener('loadedmetadata', function onLoaded() {
                            audio.currentTime = positionMs / 1000;
                            if (shouldPlay) {
                                audio.play();
                            }
                            audio.removeEventListener('loadedmetadata', onLoaded);
                        });
                    }
                    
                    this.showSuccessNotification(`Đã chuyển từ ${sourceDevice}`);
                }
            }
        });

        // PlaybackPositionSync - đồng bộ vị trí từ thiết bị khác
        this.connection.on('PlaybackPositionSync', (data) => {
            const audio = document.getElementById('audioElement');
            // Chỉ cập nhật UI nếu đang không phát
            if (audio && audio.paused) {
                const positionMs = data.positionMs || 0;
                const isPlaying = data.isPlaying;
                
                // Cập nhật thanh progress (không seek audio)
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
                
                // Lưu remote position để khi resume có thể seek đến đó
                window.remotePosition = positionMs;
            }
        });

        // Reconnection handlers
        this.connection.onreconnecting(() => {
            console.log('[PlaybackSession] Reconnecting...');
            this.isConnected = false;
        });

        this.connection.onreconnected(() => {
            console.log('[PlaybackSession] Reconnected');
            this.isConnected = true;
        });

        this.connection.onclose(() => {
            console.log('[PlaybackSession] Connection closed');
            this.isConnected = false;
        });
    }

    async notifyPlaybackStart(songId, songName) {
        if (!this.isConnected) {
            console.log('[PlaybackSession] Not connected, skipping notification');
            return;
        }

        try {
            await this.connection.invoke('StartPlayback', songId, songName);
            console.log('[PlaybackSession] Notified playback start');
        } catch (error) {
            console.error('[PlaybackSession] Error notifying playback:', error);
        }
    }

    async requestPlayback() {
        if (!this.isConnected) {
            return true; // Allow if not connected
        }

        try {
            await this.connection.invoke('RequestPlayback');
            return true;
        } catch (error) {
            console.error('[PlaybackSession] Error requesting playback:', error);
            return false;
        }
    }

    async takeoverPlayback() {
        if (!this.isConnected) {
            return false;
        }

        try {
            await this.connection.invoke('TakeoverPlayback');
            return true;
        } catch (error) {
            console.error('[PlaybackSession] Error taking over playback:', error);
            return false;
        }
    }

    showDeviceSwitchNotification(data) {
        const notification = document.createElement('div');
        notification.className = 'playback-notification';
        notification.innerHTML = `
            <div class="notification-content">
                <i class="fas fa-mobile-alt"></i>
                <div class="notification-text">
                    <strong>Đang phát trên ${data.deviceName || data.device}</strong>
                    <p>${data.songName || 'một bài hát khác'}</p>
                </div>
            </div>
        `;
        
        document.body.appendChild(notification);
        
        setTimeout(() => {
            notification.classList.add('show');
        }, 100);
        
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 300);
        }, 4000);
    }

    showSessionReplacedDialog(data) {
        // Create modal
        const modal = document.createElement('div');
        modal.className = 'session-modal';
        modal.innerHTML = `
            <div class="session-modal-content">
                <i class="fas fa-exclamation-circle"></i>
                <h3>Phiên nghe nhạc đã chuyển</h3>
                <p>${data.message}</p>
                <p>Thiết bị mới: <strong>${data.newDevice}</strong></p>
                <button class="btn btn-primary" onclick="this.closest('.session-modal').remove()">
                    Đã hiểu
                </button>
            </div>
        `;
        
        document.body.appendChild(modal);
        setTimeout(() => modal.classList.add('show'), 100);
    }

    showSessionTakenOverNotification(data) {
        const notification = document.createElement('div');
        notification.className = 'playback-notification error';
        notification.innerHTML = `
            <div class="notification-content">
                <i class="fas fa-times-circle"></i>
                <div class="notification-text">
                    <strong>Phát nhạc trên thiết bị khác</strong>
                    <p>${data.message}</p>
                </div>
            </div>
        `;
        
        document.body.appendChild(notification);
        setTimeout(() => notification.classList.add('show'), 100);
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 300);
        }, 4000);
    }

    showTakeoverDialog(data) {
        const modal = document.createElement('div');
        modal.className = 'session-modal';
        modal.innerHTML = `
            <div class="session-modal-content">
                <i class="fas fa-music"></i>
                <h3>Đang phát nhạc trên thiết bị khác</h3>
                <p>Thiết bị hiện tại: <strong>${data.activeDevice}</strong></p>
                <p>Bạn có muốn chuyển sang phát nhạc ở đây không?</p>
                <div style="display: flex; gap: 12px; margin-top: 20px;">
                    <button class="btn btn-secondary" onclick="this.closest('.session-modal').remove()">
                        Hủy
                    </button>
                    <button class="btn btn-primary" onclick="window.sessionManager.handleTakeover(this)">
                        Chuyển sang đây
                    </button>
                </div>
            </div>
        `;
        
        document.body.appendChild(modal);
        setTimeout(() => modal.classList.add('show'), 100);
    }

    async handleTakeover(button) {
        button.disabled = true;
        button.textContent = 'Đang chuyển...';
        
        const success = await this.takeoverPlayback();
        
        if (success) {
            button.closest('.session-modal').remove();
        } else {
            button.disabled = false;
            button.textContent = 'Thử lại';
        }
    }

    showSuccessNotification(message) {
        const notification = document.createElement('div');
        notification.className = 'playback-notification success';
        notification.innerHTML = `
            <div class="notification-content">
                <i class="fas fa-check-circle"></i>
                <div class="notification-text">
                    <strong>${message}</strong>
                </div>
            </div>
        `;
        
        document.body.appendChild(notification);
        setTimeout(() => notification.classList.add('show'), 100);
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => notification.remove(), 300);
        }, 3000);
    }

    onPause(callback) {
        this.onPauseCallback = callback;
    }

    onSessionReplaced(callback) {
        this.onSessionReplacedCallback = callback;
    }

    // Lấy danh sách thiết bị đang kết nối
    async getConnectedDevices() {
        if (!this.isConnected) {
            console.log('[PlaybackSession] Not connected, cannot get devices');
            return [];
        }

        try {
            const devices = await this.connection.invoke('GetConnectedDevices');
            console.log('[PlaybackSession] Connected devices:', devices);
            return devices || [];
        } catch (error) {
            console.error('[PlaybackSession] Error getting devices:', error);
            return [];
        }
    }

    // Gửi đồng bộ vị trí phát đến các thiết bị khác
    async syncPlaybackPosition(songId, positionMs, isPlaying) {
        if (!this.isConnected) return;

        try {
            await this.connection.invoke('SyncPlaybackPosition', songId, positionMs, isPlaying);
        } catch (error) {
            // Ignore errors - sync is not critical
        }
    }

    // Chuyển phát nhạc sang thiết bị khác
    async transferPlayback(targetDeviceId, songId, positionMs, isPlaying, songName = '', imageUrl = '', artistName = '') {
        if (!this.isConnected) {
            console.log('[PlaybackSession] Not connected, cannot transfer playback');
            return false;
        }

        try {
            await this.connection.invoke('TransferPlayback', targetDeviceId, songId, positionMs, isPlaying, songName, imageUrl, artistName);
            console.log('[PlaybackSession] Playback transfer requested with song info:', { songName, imageUrl, artistName });
            return true;
        } catch (error) {
            console.error('[PlaybackSession] Error transferring playback:', error);
            return false;
        }
    }

    // Hiển thị dialog chọn thiết bị
    async showDevicesDialog() {
        console.log('[PlaybackSession] showDevicesDialog called');
        console.log('[PlaybackSession] isConnected:', this.isConnected);
        
        const devices = await this.getConnectedDevices();
        console.log('[PlaybackSession] Devices received:', devices);
        
        // Tạo modal
        const modal = document.createElement('div');
        modal.className = 'devices-modal';
        modal.innerHTML = `
            <div class="devices-modal-content">
                <div class="devices-header">
                    <i class="fas fa-devices" style="color: #1DB954; font-size: 24px;"></i>
                    <h3>Thiết bị phát nhạc</h3>
                    <button class="devices-close-btn" onclick="this.closest('.devices-modal').remove()">
                        <i class="fas fa-times"></i>
                    </button>
                </div>
                
                <div class="devices-list">
                    ${devices.length === 0 ? `
                        <div class="no-devices">
                            <i class="fas fa-mobile-alt"></i>
                            <p>Không tìm thấy thiết bị nào</p>
                            <small>Đăng nhập trên thiết bị khác để xem ở đây</small>
                        </div>
                    ` : devices.map(device => `
                        <div class="device-item ${device.isCurrentDevice ? 'current' : ''}" 
                             data-device-id="${device.deviceId}"
                             data-connection-id="${device.connectionId}">
                            <div class="device-icon">
                                <i class="fas ${this.getDeviceIcon(device.deviceName)}"></i>
                            </div>
                            <div class="device-info">
                                <div class="device-name">${device.deviceName}</div>
                                ${device.isCurrentDevice ? '<div class="device-status">Thiết bị này</div>' : ''}
                                ${device.isActive && !device.isCurrentDevice ? '<div class="device-status active">Đang phát</div>' : ''}
                                ${device.currentSong && device.currentSong.songName ? 
                                    `<div class="device-song">${device.currentSong.songName}</div>` : ''}
                            </div>
                            ${device.isActive ? '<div class="device-playing"><i class="fas fa-volume-up"></i></div>' : ''}
                        </div>
                    `).join('')}
                </div>
            </div>
        `;
        
        document.body.appendChild(modal);
        setTimeout(() => modal.classList.add('show'), 100);

        // Click vào thiết bị để chuyển phát nhạc
        modal.querySelectorAll('.device-item:not(.current)').forEach(item => {
            item.addEventListener('click', async () => {
                const deviceId = item.dataset.connectionId || item.dataset.deviceId;
                const audio = document.getElementById('audioElement');
                const songId = window.currentPlayingSongId || '';
                const positionMs = audio ? Math.floor(audio.currentTime * 1000) : 0;
                const isPlaying = audio ? !audio.paused : false;
                
                // Lấy thêm thông tin bài hát
                const songNameEl = document.querySelector('.song-name, .player-info h4, #playerSongName');
                const artistEl = document.querySelector('.artist-name, .player-info p, #playerArtist');
                const thumbnailEl = document.querySelector('.player-thumbnail img, #playerThumbnail');
                
                const songName = songNameEl?.textContent || '';
                const artistName = artistEl?.textContent || '';
                const imageUrl = thumbnailEl?.src || '';

                item.classList.add('loading');
                item.innerHTML += '<div class="loading-spinner"><i class="fas fa-spinner fa-spin"></i></div>';

                const success = await this.transferPlayback(deviceId, songId, positionMs, isPlaying, songName, imageUrl, artistName);
                
                if (success) {
                    // Dừng phát nhạc trên thiết bị này sau khi transfer
                    if (audio) {
                        audio.pause();
                    }
                    
                    modal.remove();
                    this.showSuccessNotification(`Đã chuyển sang ${item.querySelector('.device-name').textContent}`);
                } else {
                    item.classList.remove('loading');
                    item.querySelector('.loading-spinner')?.remove();
                }
            });
        });

        // Click overlay để đóng
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                modal.remove();
            }
        });
    }

    getDeviceIcon(deviceName) {
        const name = (deviceName || '').toLowerCase();
        if (name.includes('iphone') || name.includes('mobile') || name.includes('phone')) {
            return 'fa-mobile-alt';
        } else if (name.includes('ipad') || name.includes('tablet')) {
            return 'fa-tablet-alt';
        } else if (name.includes('mac') || name.includes('laptop') || name.includes('pc')) {
            return 'fa-laptop';
        } else if (name.includes('chrome') || name.includes('firefox') || name.includes('edge') || name.includes('safari') || name.includes('web')) {
            return 'fa-globe';
        }
        return 'fa-desktop';
    }
}

// Initialize globally
window.sessionManager = new PlaybackSessionManager();

// Initialize when authenticated
document.addEventListener('DOMContentLoaded', function() {
    // Check if user is logged in
    const isAuthenticated = document.body.hasAttribute('data-authenticated');
    
    if (isAuthenticated) {
        window.sessionManager.initialize().catch(err => {
            console.error('[PlaybackSession] Failed to initialize:', err);
        });
    }
});
