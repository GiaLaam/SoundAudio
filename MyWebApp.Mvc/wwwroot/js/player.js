// Audio Player Controller
class AudioPlayer {
    constructor() {
        console.log('AudioPlayer constructor called');
        
        this.audio = document.getElementById('audioElement');
        this.player = document.getElementById('audioPlayer');
        this.playBtn = document.getElementById('playBtn');
        this.prevBtn = document.getElementById('prevBtn');
        this.nextBtn = document.getElementById('nextBtn');
        this.progressSlider = document.querySelector('.progress-slider');
        this.progressFill = document.querySelector('.progress-fill');
        this.currentTimeEl = document.getElementById('currentTime');
        this.durationEl = document.getElementById('duration');
        this.volumeSlider = document.getElementById('volumeSlider');
        this.muteBtn = document.getElementById('muteBtn');
        this.songNameEl = document.getElementById('playerSongName');
        this.artistEl = document.getElementById('playerArtist');
        this.thumbnailEl = document.getElementById('playerThumbnail');
        
        console.log('Elements found:', {
            audio: !!this.audio,
            player: !!this.player,
            playBtn: !!this.playBtn,
            prevBtn: !!this.prevBtn,
            nextBtn: !!this.nextBtn,
            progressSlider: !!this.progressSlider,
            progressFill: !!this.progressFill,
            currentTimeEl: !!this.currentTimeEl,
            durationEl: !!this.durationEl,
            volumeSlider: !!this.volumeSlider,
            muteBtn: !!this.muteBtn,
            songNameEl: !!this.songNameEl,
            artistEl: !!this.artistEl,
            thumbnailEl: !!this.thumbnailEl
        });
        
        this.currentQueue = [];
        this.currentIndex = -1;
        this.isPlaying = false;
        
        this.initializeEventListeners();
    }
    
    initializeEventListeners() {
        // Play/Pause button
        this.playBtn.addEventListener('click', () => this.togglePlay());
        
        // Previous/Next buttons
        this.prevBtn.addEventListener('click', () => this.playPrevious());
        this.nextBtn.addEventListener('click', () => this.playNext());
        
        // Progress bar
        this.progressSlider.addEventListener('input', (e) => this.seek(e.target.value));
        
        // Volume control
        this.volumeSlider.addEventListener('input', (e) => this.setVolume(e.target.value));
        this.muteBtn.addEventListener('click', () => this.toggleMute());
        
        // Audio events
        this.audio.addEventListener('timeupdate', () => this.updateProgress());
        this.audio.addEventListener('loadedmetadata', () => this.updateDuration());
        this.audio.addEventListener('ended', () => this.playNext());
        this.audio.addEventListener('play', () => this.onPlay());
        this.audio.addEventListener('pause', () => this.onPause());
        
        // Set initial volume
        this.audio.volume = 0.7;
        this.volumeSlider.value = 70;
    }
    
    loadSong(songData) {
        console.log('Loading song:', songData);
        const apiBaseUrl = document.querySelector('[data-api-base]')?.getAttribute('data-api-base') || 'http://localhost:5289';
        console.log('API Base URL:', apiBaseUrl);
        
        // Set audio source
        const audioUrl = `${apiBaseUrl}/api/music/stream/${songData.id}`;
        console.log('Audio URL:', audioUrl);
        this.audio.src = audioUrl;
        
        // Update UI
        this.songNameEl.textContent = songData.name || 'Unknown Song';
        this.artistEl.textContent = songData.artist || 'Unknown Artist';
        
        if (songData.image) {
            this.thumbnailEl.src = `${apiBaseUrl}${songData.image}`;
        } else {
            this.thumbnailEl.src = '/images/default-album.png';
        }
        
        // Show player
        this.player.style.display = 'flex';
        console.log('Player displayed');
        
        // Auto play
        this.play();
    }
    
    play() {
        this.audio.play().catch(error => {
            console.error('Error playing audio:', error);
        });
    }
    
    pause() {
        this.audio.pause();
    }
    
    togglePlay() {
        if (this.isPlaying) {
            this.pause();
        } else {
            this.play();
        }
    }
    
    onPlay() {
        this.isPlaying = true;
        this.playBtn.innerHTML = '<i class="fas fa-pause"></i>';
    }
    
    onPause() {
        this.isPlaying = false;
        this.playBtn.innerHTML = '<i class="fas fa-play"></i>';
    }
    
    seek(value) {
        const time = (value / 100) * this.audio.duration;
        this.audio.currentTime = time;
    }
    
    updateProgress() {
        if (this.audio.duration) {
            const progress = (this.audio.currentTime / this.audio.duration) * 100;
            this.progressFill.style.width = `${progress}%`;
            this.progressSlider.value = progress;
            
            this.currentTimeEl.textContent = this.formatTime(this.audio.currentTime);
        }
    }
    
    updateDuration() {
        this.durationEl.textContent = this.formatTime(this.audio.duration);
    }
    
    setVolume(value) {
        this.audio.volume = value / 100;
        this.updateVolumeIcon(value);
    }
    
    toggleMute() {
        if (this.audio.volume > 0) {
            this.audio.dataset.prevVolume = this.audio.volume;
            this.audio.volume = 0;
            this.volumeSlider.value = 0;
            this.updateVolumeIcon(0);
        } else {
            const prevVolume = parseFloat(this.audio.dataset.prevVolume) || 0.7;
            this.audio.volume = prevVolume;
            this.volumeSlider.value = prevVolume * 100;
            this.updateVolumeIcon(prevVolume * 100);
        }
    }
    
    updateVolumeIcon(volume) {
        let icon = 'fa-volume-up';
        if (volume == 0) {
            icon = 'fa-volume-mute';
        } else if (volume < 50) {
            icon = 'fa-volume-down';
        }
        this.muteBtn.innerHTML = `<i class="fas ${icon}"></i>`;
    }
    
    setQueue(songs, startIndex = 0) {
        this.currentQueue = songs;
        this.currentIndex = startIndex;
        if (songs.length > 0) {
            this.loadSong(songs[startIndex]);
        }
    }
    
    playNext() {
        if (this.currentQueue.length === 0) return;
        
        this.currentIndex = (this.currentIndex + 1) % this.currentQueue.length;
        this.loadSong(this.currentQueue[this.currentIndex]);
    }
    
    playPrevious() {
        if (this.currentQueue.length === 0) return;
        
        if (this.audio.currentTime > 3) {
            this.audio.currentTime = 0;
        } else {
            this.currentIndex = this.currentIndex - 1;
            if (this.currentIndex < 0) {
                this.currentIndex = this.currentQueue.length - 1;
            }
            this.loadSong(this.currentQueue[this.currentIndex]);
        }
    }
    
    formatTime(seconds) {
        if (isNaN(seconds)) return '0:00';
        
        const mins = Math.floor(seconds / 60);
        const secs = Math.floor(seconds % 60);
        return `${mins}:${secs.toString().padStart(2, '0')}`;
    }
}

// Initialize player when DOM is ready
let player;
document.addEventListener('DOMContentLoaded', function() {
    console.log('DOM Content Loaded - Initializing player...');
    player = new AudioPlayer();
    console.log('Player initialized:', player);
    
    // Wire up all play buttons
    wirePlayButtons();
});

function wirePlayButtons() {
    console.log('Wiring play buttons...');
    
    // Play buttons on albums/songs
    const playButtons = document.querySelectorAll('.play-btn');
    console.log('Found play buttons:', playButtons.length);
    
    playButtons.forEach(btn => {
        btn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            console.log('Play button clicked');
            const songElement = this.closest('[data-song-id]');
            console.log('Song element:', songElement);
            
            if (songElement) {
                const songData = {
                    id: songElement.getAttribute('data-song-id'),
                    name: songElement.getAttribute('data-song-name'),
                    artist: songElement.getAttribute('data-song-artist'),
                    image: songElement.getAttribute('data-song-image')
                };
                
                console.log('Playing song:', songData);
                player.setQueue([songData], 0);
            } else {
                console.error('No song element found');
            }
        });
    });
    
    // List items (click entire row to play)
    const listItems = document.querySelectorAll('.list-item[data-song-id]');
    console.log('Found list items:', listItems.length);
    
    listItems.forEach(item => {
        item.addEventListener('click', function() {
            console.log('List item clicked');
            const songData = {
                id: this.getAttribute('data-song-id'),
                name: this.getAttribute('data-song-name'),
                artist: this.getAttribute('data-song-artist'),
                image: this.getAttribute('data-song-image')
            };
            
            console.log('Playing song from list:', songData);
            player.setQueue([songData], 0);
        });
    });
}

// Global function to play a song (can be called from anywhere)
window.playSong = function(songId, songName, artist, image) {
    if (!player) {
        console.error('Player not initialized');
        return;
    }
    
    player.setQueue([{
        id: songId,
        name: songName,
        artist: artist,
        image: image
    }], 0);
};

// Global function to play a list of songs
window.playQueue = function(songs, startIndex = 0) {
    if (!player) {
        console.error('Player not initialized');
        return;
    }
    
    player.setQueue(songs, startIndex);
};
