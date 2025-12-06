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
            
        } catch (error) {
            console.error('[PlaybackSession] Connection error:', error);
        }
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
