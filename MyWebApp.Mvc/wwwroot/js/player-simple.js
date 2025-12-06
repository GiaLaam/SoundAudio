// Simple Audio Player - Debug Version
console.log('=== PLAYER.JS LOADED ===');

document.addEventListener('DOMContentLoaded', function() {
    console.log('=== DOM READY ===');
    
    // Check if all required elements exist
    const elements = {
        audio: document.getElementById('audioElement'),
        player: document.getElementById('audioPlayer'),
        playBtn: document.getElementById('playBtn'),
        prevBtn: document.getElementById('prevBtn'),
        nextBtn: document.getElementById('nextBtn'),
        progressSlider: document.getElementById('progressSlider'),
        currentTime: document.getElementById('currentTime'),
        duration: document.getElementById('duration'),
        volumeSlider: document.getElementById('volumeSlider'),
        muteBtn: document.getElementById('muteBtn'),
        songName: document.getElementById('playerSongName'),
        artist: document.getElementById('playerArtist'),
        thumbnail: document.getElementById('playerThumbnail'),
        progressFill: document.querySelector('.progress-fill')
    };
    
    console.log('Elements check:', elements);
    
    // Simple play function
    window.playSong = function(songId, songName, artist, image) {
        console.log('=== PLAY SONG CALLED ===');
        console.log('Song ID:', songId);
        console.log('Song Name:', songName);
        
        if (!elements.audio || !elements.player) {
            console.error('Audio elements not found!');
            return;
        }
        
        const apiBaseUrl = document.body.getAttribute('data-api-base') || 'http://localhost:5289';
        const audioUrl = `${apiBaseUrl}/api/music/stream/${songId}`;
        
        console.log('Audio URL:', audioUrl);
        
        // Notify session manager about playback start
        if (window.sessionManager && window.sessionManager.isConnected) {
            window.sessionManager.notifyPlaybackStart(songId, songName);
        }
        
        // Set audio source
        elements.audio.src = audioUrl;
        
        // Update UI
        elements.songName.textContent = songName || 'Unknown Song';
        elements.artist.textContent = artist || 'Unknown Artist';
        
        if (image && elements.thumbnail) {
            elements.thumbnail.src = image.startsWith('http') ? image : apiBaseUrl + image;
        }
        
        // Show player
        elements.player.style.display = 'flex';
        console.log('Player displayed');
        
        // Play
        elements.audio.play()
            .then(() => console.log('Playing...'))
            .catch(err => console.error('Play error:', err));
    };
    
    // Wire up play buttons
    console.log('Wiring up buttons...');
    
    // Play button click
    if (elements.playBtn) {
        elements.playBtn.addEventListener('click', function() {
            console.log('Play button clicked');
            if (elements.audio.paused) {
                elements.audio.play();
            } else {
                elements.audio.pause();
            }
        });
    }
    
    // Audio events
    if (elements.audio) {
        elements.audio.addEventListener('play', function() {
            console.log('Audio playing');
            if (elements.playBtn) {
                elements.playBtn.innerHTML = '<i class="fas fa-pause"></i>';
            }
        });
        
        elements.audio.addEventListener('pause', function() {
            console.log('Audio paused');
            if (elements.playBtn) {
                elements.playBtn.innerHTML = '<i class="fas fa-play"></i>';
            }
        });
        
        elements.audio.addEventListener('error', function(e) {
            console.error('Audio error:', e);
            console.error('Audio error details:', elements.audio.error);
        });
        
        elements.audio.addEventListener('timeupdate', function() {
            if (elements.progressFill && elements.audio.duration) {
                const progress = (elements.audio.currentTime / elements.audio.duration) * 100;
                elements.progressFill.style.width = progress + '%';
                
                if (elements.currentTime) {
                    elements.currentTime.textContent = formatTime(elements.audio.currentTime);
                }
            }
        });
        
        elements.audio.addEventListener('loadedmetadata', function() {
            if (elements.duration) {
                elements.duration.textContent = formatTime(elements.audio.duration);
            }
        });
    }
    
    // Progress slider
    if (elements.progressSlider) {
        elements.progressSlider.addEventListener('input', function(e) {
            const time = (e.target.value / 100) * elements.audio.duration;
            elements.audio.currentTime = time;
        });
    }
    
    // Volume
    if (elements.volumeSlider) {
        elements.volumeSlider.addEventListener('input', function(e) {
            elements.audio.volume = e.target.value / 100;
        });
    }
    
    // Mute button
    if (elements.muteBtn) {
        elements.muteBtn.addEventListener('click', function() {
            if (elements.audio.volume > 0) {
                elements.audio.dataset.prevVolume = elements.audio.volume;
                elements.audio.volume = 0;
                this.innerHTML = '<i class="fas fa-volume-mute"></i>';
            } else {
                elements.audio.volume = parseFloat(elements.audio.dataset.prevVolume) || 0.7;
                this.innerHTML = '<i class="fas fa-volume-up"></i>';
            }
        });
    }
    
    // Wire song cards and list items
    document.querySelectorAll('[data-song-id]').forEach(function(element) {
        const isButton = element.classList.contains('play-btn');
        const isCard = element.classList.contains('card');
        const isListItem = element.classList.contains('list-item');
        
        if (isButton) {
            // Play button inside a card
            element.addEventListener('click', function(e) {
                e.stopPropagation();
                e.preventDefault();
                
                const parent = this.closest('[data-song-id]');
                if (parent) {
                    playSong(
                        parent.getAttribute('data-song-id'),
                        parent.getAttribute('data-song-name'),
                        parent.getAttribute('data-song-artist'),
                        parent.getAttribute('data-song-image')
                    );
                }
            });
        } else if (isListItem) {
            // Clickable list item
            element.addEventListener('click', function() {
                playSong(
                    this.getAttribute('data-song-id'),
                    this.getAttribute('data-song-name'),
                    this.getAttribute('data-song-artist'),
                    this.getAttribute('data-song-image')
                );
            });
        } else {
            // Card with play button inside
            const playBtn = element.querySelector('.play-btn');
            if (playBtn) {
                playBtn.addEventListener('click', function(e) {
                    e.stopPropagation();
                    e.preventDefault();
                    
                    playSong(
                        element.getAttribute('data-song-id'),
                        element.getAttribute('data-song-name'),
                        element.getAttribute('data-song-artist'),
                        element.getAttribute('data-song-image')
                    );
                });
            }
        }
    });
    
    console.log('Total songs found:', document.querySelectorAll('[data-song-id]').length);
    console.log('=== INITIALIZATION COMPLETE ===');
    
    // Setup session manager callbacks
    if (window.sessionManager) {
        window.sessionManager.onPause((data) => {
            console.log('[Player] Pause requested from session manager:', data);
            if (elements.audio && !elements.audio.paused) {
                elements.audio.pause();
            }
        });
        
        window.sessionManager.onSessionReplaced((data) => {
            console.log('[Player] Session replaced:', data);
            if (elements.audio && !elements.audio.paused) {
                elements.audio.pause();
            }
        });
    }
});

function formatTime(seconds) {
    if (isNaN(seconds)) return '0:00';
    const mins = Math.floor(seconds / 60);
    const secs = Math.floor(seconds % 60);
    return mins + ':' + (secs < 10 ? '0' : '') + secs;
}
