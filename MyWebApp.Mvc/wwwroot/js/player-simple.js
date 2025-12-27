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
        progressFill: document.querySelector('.progress-fill'),
        // Lyrics elements
        lyricsBtn: document.getElementById('lyricsBtn'),
        lyricsPanel: document.getElementById('lyricsPanel'),
        lyricsOverlay: document.getElementById('lyricsOverlay'),
        closeLyricsBtn: document.getElementById('closeLyricsBtn'),
        lyricsContent: document.getElementById('lyricsContent'),
        lyricsThumbnail: document.getElementById('lyricsThumbnail'),
        lyricsSongName: document.getElementById('lyricsSongName'),
        lyricsArtist: document.getElementById('lyricsArtist'),
        devicesBtn: document.getElementById('devicesBtn')
    };
    
    console.log('Elements check:', elements);
    
    // Current song data for lyrics
    let currentSongData = {
        id: null,
        name: null,
        artist: null,
        image: null
    };
    
    // Playlist management for next/prev
    window.currentPlaylist = [];
    window.currentPlaylistIndex = -1;
    
    // Set playlist for next/prev functionality
    window.setPlaylist = function(songs, startIndex = 0) {
        window.currentPlaylist = songs || [];
        window.currentPlaylistIndex = startIndex;
        console.log(`[Player] Playlist set with ${songs.length} songs, starting at index ${startIndex}`);
    };
    
    // Update playlist index when playing a song
    window.updatePlaylistIndex = function(songId) {
        if (window.currentPlaylist.length > 0) {
            const idx = window.currentPlaylist.findIndex(s => s.id === songId || s.id === parseInt(songId));
            if (idx !== -1) {
                window.currentPlaylistIndex = idx;
                console.log(`[Player] Updated playlist index to ${idx}`);
            }
        }
    };
    
    // Simple play function
    window.playSong = async function(songId, songName, artist, image) {
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

        // Lưu songId hiện tại để transfer playback
        window.currentPlayingSongId = songId;
        
        // Nếu chưa có playlist hoặc bài hát không có trong playlist, fetch từ API
        let foundInPlaylist = false;
        if (window.currentPlaylist && window.currentPlaylist.length > 0) {
            const idx = window.currentPlaylist.findIndex(s => s.id === songId || s.id === parseInt(songId) || s.id == songId);
            if (idx !== -1) {
                foundInPlaylist = true;
                window.currentPlaylistIndex = idx;
            }
        }
        
        if (!foundInPlaylist) {
            try {
                console.log('[Player] Song not in playlist, fetching all songs...');
                const response = await fetch(`${apiBaseUrl}/api/music`);
                if (response.ok) {
                    const songs = await response.json();
                    const idx = songs.findIndex(s => s.id === songId || s.id === parseInt(songId) || s.id == songId);
                    if (idx !== -1) {
                        window.setPlaylist(songs, idx);
                    } else {
                        window.setPlaylist(songs, 0);
                    }
                }
            } catch (error) {
                console.error('[Player] Error fetching playlist:', error);
            }
        }
        
        // Notify session manager about playback start
        if (window.sessionManager && window.sessionManager.isConnected) {
            window.sessionManager.notifyPlaybackStart(songId, songName);
        }
        
        // Set audio source
        elements.audio.src = audioUrl;
        
        // Update UI
        elements.songName.textContent = songName || 'Unknown Song';
        elements.artist.textContent = artist || 'Unknown Artist';
        
        const fullImageUrl = image ? (image.startsWith('http') ? image : apiBaseUrl + image) : '/images/logo.png';
        if (elements.thumbnail) {
            elements.thumbnail.src = fullImageUrl;
        }
        
        // Update current song data for lyrics
        currentSongData = {
            id: songId,
            name: songName || 'Unknown Song',
            artist: artist || 'Unknown Artist',
            image: fullImageUrl
        };
        
        // Update lyrics panel song info
        if (elements.lyricsSongName) elements.lyricsSongName.textContent = currentSongData.name;
        if (elements.lyricsArtist) elements.lyricsArtist.textContent = currentSongData.artist;
        if (elements.lyricsThumbnail) elements.lyricsThumbnail.src = currentSongData.image;
        
        // Fetch lyrics for new song
        fetchLyrics(songId);
        
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
                // Seek đến remote position nếu có
                if (window.remotePosition && window.remotePosition > 0) {
                    console.log('[Player] Seeking to remote position:', window.remotePosition / 1000, 's');
                    elements.audio.currentTime = window.remotePosition / 1000;
                    window.remotePosition = null; // Reset
                }
                
                // Thông báo server trước khi play để dừng các thiết bị khác
                if (window.sessionManager && window.sessionManager.isConnected && window.currentPlayingSongId) {
                    window.sessionManager.notifyPlaybackStart(window.currentPlayingSongId, elements.songName?.textContent || '');
                }
                elements.audio.play();
            } else {
                elements.audio.pause();
            }
        });
    }
    
    // Next button click
    if (elements.nextBtn) {
        elements.nextBtn.addEventListener('click', function() {
            console.log('Next button clicked');
            if (window.currentPlaylist.length > 0 && window.currentPlaylistIndex < window.currentPlaylist.length - 1) {
                window.currentPlaylistIndex++;
                const nextSong = window.currentPlaylist[window.currentPlaylistIndex];
                window.playSong(nextSong.id, nextSong.name, nextSong.artist || '', nextSong.imageUrl || nextSong.image || '');
            } else {
                console.log('No next song available');
            }
        });
    }
    
    // Prev button click
    if (elements.prevBtn) {
        elements.prevBtn.addEventListener('click', function() {
            console.log('Prev button clicked');
            if (window.currentPlaylist.length > 0 && window.currentPlaylistIndex > 0) {
                window.currentPlaylistIndex--;
                const prevSong = window.currentPlaylist[window.currentPlaylistIndex];
                window.playSong(prevSong.id, prevSong.name, prevSong.artist || '', prevSong.imageUrl || prevSong.image || '');
            } else {
                console.log('No previous song available');
            }
        });
    }
    
    // Sync timer for position broadcast
    let syncTimer = null;
    
    function startSyncTimer() {
        stopSyncTimer();
        syncTimer = setInterval(() => {
            if (elements.audio && !elements.audio.paused && window.sessionManager && window.sessionManager.isConnected) {
                const songId = window.currentPlayingSongId || '';
                const positionMs = Math.floor(elements.audio.currentTime * 1000);
                window.sessionManager.syncPlaybackPosition(songId, positionMs, true);
            }
        }, 2000);
    }
    
    function stopSyncTimer() {
        if (syncTimer) {
            clearInterval(syncTimer);
            syncTimer = null;
        }
    }
    
    // Audio events
    if (elements.audio) {
        elements.audio.addEventListener('play', function() {
            console.log('Audio playing');
            startSyncTimer();
            if (elements.playBtn) {
                elements.playBtn.innerHTML = '<i class="fas fa-pause"></i>';
            }
        });
        
        elements.audio.addEventListener('pause', function() {
            console.log('Audio paused');
            stopSyncTimer();
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
    
    // ==================== LYRICS FUNCTIONALITY ====================
    
    let lyricsData = []; // Array of {time: seconds, text: string}
    let currentLyricIndex = -1;
    
    // Toggle lyrics panel
    function toggleLyricsPanel() {
        if (!elements.lyricsPanel || !elements.lyricsOverlay) return;
        
        const isOpen = elements.lyricsPanel.classList.contains('show');
        
        if (isOpen) {
            closeLyricsPanel();
        } else {
            openLyricsPanel();
        }
    }
    
    function openLyricsPanel() {
        if (!elements.lyricsPanel || !elements.lyricsOverlay) return;
        
        elements.lyricsPanel.classList.add('show');
        elements.lyricsOverlay.classList.add('show');
        if (elements.lyricsBtn) elements.lyricsBtn.classList.add('active');
    }
    
    function closeLyricsPanel() {
        if (!elements.lyricsPanel || !elements.lyricsOverlay) return;
        
        elements.lyricsPanel.classList.remove('show');
        elements.lyricsOverlay.classList.remove('show');
        if (elements.lyricsBtn) elements.lyricsBtn.classList.remove('active');
    }
    
    // Parse LRC format to array of {time, text}
    function parseLRC(lrcContent) {
        const lines = lrcContent.split('\n');
        const result = [];
        
        // Regex to match LRC timestamps: [mm:ss.xx] or [mm:ss:xx]
        const timeRegex = /\[(\d{2}):(\d{2})[.:](\d{2,3})\]/g;
        
        lines.forEach(line => {
            const matches = [...line.matchAll(timeRegex)];
            if (matches.length > 0) {
                // Get the text after all timestamps
                let text = line.replace(timeRegex, '').trim();
                
                // Skip empty lines or metadata lines
                if (!text || text.startsWith('[')) return;
                
                // Create entry for each timestamp
                matches.forEach(match => {
                    const minutes = parseInt(match[1]);
                    const seconds = parseInt(match[2]);
                    const ms = parseInt(match[3].padEnd(3, '0'));
                    const time = minutes * 60 + seconds + ms / 1000;
                    
                    result.push({ time, text });
                });
            }
        });
        
        // Sort by time
        result.sort((a, b) => a.time - b.time);
        
        return result;
    }
    
    // Render lyrics lines
    function renderLyrics(lyrics) {
        if (!elements.lyricsContent) return;
        
        if (lyrics.length === 0) {
            showNoLyrics();
            return;
        }
        
        const html = lyrics.map((item, index) => 
            `<div class="lyrics-line" data-index="${index}" data-time="${item.time}">${item.text}</div>`
        ).join('');
        
        elements.lyricsContent.innerHTML = `<div class="lyrics-text">${html}</div>`;
        
        // Add click handlers to seek to that time
        elements.lyricsContent.querySelectorAll('.lyrics-line').forEach(line => {
            line.addEventListener('click', function() {
                const time = parseFloat(this.dataset.time);
                if (elements.audio && !isNaN(time)) {
                    elements.audio.currentTime = time;
                }
            });
        });
    }
    
    // Update active lyric based on current time
    function updateActiveLyric(currentTime) {
        if (lyricsData.length === 0) return;
        
        // Find current lyric index
        let newIndex = -1;
        for (let i = lyricsData.length - 1; i >= 0; i--) {
            if (currentTime >= lyricsData[i].time - 0.1) {
                newIndex = i;
                break;
            }
        }
        
        if (newIndex !== currentLyricIndex) {
            currentLyricIndex = newIndex;
            
            // Update classes
            const lines = elements.lyricsContent.querySelectorAll('.lyrics-line');
            lines.forEach((line, index) => {
                line.classList.remove('active', 'passed');
                if (index === currentLyricIndex) {
                    line.classList.add('active');
                    // Scroll to active line
                    scrollToActiveLyric(line);
                } else if (index < currentLyricIndex) {
                    line.classList.add('passed');
                }
            });
        }
    }
    
    // Smooth scroll to active lyric
    function scrollToActiveLyric(element) {
        if (!element || !elements.lyricsPanel.classList.contains('show')) return;
        
        const container = elements.lyricsContent.parentElement; // .lyrics-panel-content
        const containerHeight = container.clientHeight;
        const elementTop = element.offsetTop;
        const elementHeight = element.clientHeight;
        
        // Center the active line
        const scrollTo = elementTop - (containerHeight / 2) + (elementHeight / 2);
        
        container.scrollTo({
            top: scrollTo,
            behavior: 'smooth'
        });
    }
    
    // Fetch lyrics from API
    function fetchLyrics(songId) {
        if (!elements.lyricsContent) return;
        
        // Reset
        lyricsData = [];
        currentLyricIndex = -1;
        
        // Show loading
        elements.lyricsContent.innerHTML = `
            <div class="lyrics-loading">
                <i class="fas fa-spinner"></i>
                <p>Đang tải lời bài hát...</p>
            </div>
        `;
        
        const apiBaseUrl = document.body.getAttribute('data-api-base') || 'http://localhost:5289';
        
        fetch(`${apiBaseUrl}/api/lyric/by-song/${songId}`)
            .then(response => {
                if (!response.ok) throw new Error('Not found');
                return response.json();
            })
            .then(data => {
                if (data && data.content) {
                    // Parse LRC format
                    lyricsData = parseLRC(data.content);
                    
                    if (lyricsData.length > 0) {
                        renderLyrics(lyricsData);
                    } else {
                        // No timestamps, show as plain text
                        let plainText = data.content
                            .replace(/\[.*?\]/g, '') // Remove all brackets
                            .split('\n')
                            .filter(line => line.trim())
                            .map(line => `<div class="lyrics-line">${line.trim()}</div>`)
                            .join('');
                        elements.lyricsContent.innerHTML = `<div class="lyrics-text">${plainText}</div>`;
                    }
                } else {
                    showNoLyrics();
                }
            })
            .catch(error => {
                console.log('Lyrics fetch error:', error);
                showNoLyrics();
            });
    }
    
    function showNoLyrics() {
        if (!elements.lyricsContent) return;
        lyricsData = [];
        
        elements.lyricsContent.innerHTML = `
            <div class="lyrics-not-found">
                <i class="fas fa-file-alt"></i>
                <p>Không tìm thấy lời bài hát</p>
            </div>
        `;
    }
    
    // Sync lyrics with audio timeupdate
    if (elements.audio) {
        elements.audio.addEventListener('timeupdate', function() {
            if (lyricsData.length > 0) {
                updateActiveLyric(this.currentTime);
            }
        });
        
        // Reset on song end
        elements.audio.addEventListener('ended', function() {
            currentLyricIndex = -1;
            const lines = elements.lyricsContent.querySelectorAll('.lyrics-line');
            lines.forEach(line => line.classList.remove('active', 'passed'));
        });
    }
    
    // Wire up lyrics button
    if (elements.lyricsBtn) {
        elements.lyricsBtn.addEventListener('click', toggleLyricsPanel);
    }
    
    // Wire up close button
    if (elements.closeLyricsBtn) {
        elements.closeLyricsBtn.addEventListener('click', closeLyricsPanel);
    }
    
    // Wire up overlay click to close
    if (elements.lyricsOverlay) {
        elements.lyricsOverlay.addEventListener('click', closeLyricsPanel);
    }
    
    // Close on Escape key
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape' && elements.lyricsPanel && elements.lyricsPanel.classList.contains('show')) {
            closeLyricsPanel();
        }
    });
    
    // Wire up devices button
    if (elements.devicesBtn) {
        elements.devicesBtn.addEventListener('click', function() {
            if (window.sessionManager) {
                window.sessionManager.showDevicesDialog();
            } else {
                console.log('[Player] Session manager not available');
            }
        });
    }
    
    // Export functions for external use
    window.openLyricsPanel = openLyricsPanel;
    window.closeLyricsPanel = closeLyricsPanel;
    window.toggleLyricsPanel = toggleLyricsPanel;
    
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
