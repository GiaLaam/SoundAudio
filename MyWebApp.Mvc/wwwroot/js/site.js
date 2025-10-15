$(document).ready(function () {
    window.sound = null;
    window.playlist = [];
    window.currentIndex = -1;
    window.isPlaying = false;
    window.isRepeat = false;
    window.isShuffle = false;
    window.currentSrc = null;
    window.lastSeek = 0;
    let isProcessing = false;

    const waveformMap = {}; // Lưu WaveSurfer cho từng bài

    $('.song-card').each(function (index) {  
        const src = $(this).data('src');
        const container = $(this).find('.waveform')[0];
        if (!src || !container) return;

        const ws = WaveSurfer.create({
            container: container,
            waveColor: '#ccc',
            progressColor: '#1DB954',
            barWidth: 2,
            height: 50,
            responsive: true,
            interact: false, // ❌ không cho click
            cursorWidth: 1,
            backend: 'MediaElement', // Sử dụng backend MediaElement
            media: null // Ngăn WaveSurfer tự phát âm thanh
        });

        ws.load(src);

        ws.on('ready', function () {
            ws.setVolume(0); // Đảm bảo WaveSurfer không phát âm thanh
        });

        // Khi người dùng tua bằng sóng, cập nhật vị trí nhạc
        ws.on('seek', function (progress) {
            if (sound && sound.state() === 'loaded') {
                const duration = sound.duration();
                const seekTime = progress * duration;
                sound.seek(seekTime);
                window.lastSeek = seekTime;
                syncWaveform();
            }
        });

        waveformMap[src] = ws;
    });

    function syncWaveform() {
        if (sound && sound.state() === 'loaded' && waveformMap[currentSrc]) {
            const seek = sound.seek() || 0;
            const duration = sound.duration() || 1;
            waveformMap[currentSrc].seekTo(seek / duration);
        }
    }


    function debounce(func, delay) {
        let timeout;
        return function (...args) {
            const context = this;
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(context, args), delay);
        };
    }


    function loadPlaylist() {
        playlist = $('.song-card').map(function () {
            return $(this).data('src');
        }).get();
        console.log('Playlist loaded:', playlist);

        if (playlist.length === 0) {
            currentIndex = -1;
            currentSrc = null;
            isPlaying = false;
            if (sound) {
                sound.stop();
                sound.unload();
                sound = null;
            }
            $('#playPause').html('<i class="fas fa-play"></i>');
            return;
        }

        if (currentSrc && playlist.includes(currentSrc)) {
            currentIndex = playlist.indexOf(currentSrc);
            if (isPlaying && sound && sound.state() === 'loaded') {
                sound.seek(lastSeek);
                sound.play();
                updateActiveItem();
                updateSongRowActive();
            }
        } else {
            currentIndex = 0;
            currentSrc = playlist[0];
            if (sound) {
                sound.stop();
                sound.unload();
                sound = null;
            }
            isPlaying = false;
            $('#playPause').html('<i class="fas fa-play"></i>');
        }
    }

    const progressBar = document.getElementById('progressBar');
    if (progressBar) {
        // Xử lý sự kiện input để cập nhật ngay lập tức
        $(progressBar).off('input change').on('input', function () {
            if (sound && sound.state() === 'loaded') {
                const raw = this.value;
                const seek = Number(raw);
                if (!isNaN(seek)) {
                    sound.seek(seek);
                    window.lastSeek = seek;
                    $('#currentTime').text(formatTime(seek));
                    syncWaveform();
                    console.log('ProgressBar input: seek=', seek);
                } else {
                    console.warn('Invalid seek value:', raw);
                }
            }
        });
    } else {
        console.error('Element #progressBar not found');
    }

    window.playMusic = function (src, forcePlay = false, indexOverride = null) {
        if (!src) {
            console.error('Invalid source:', src);
            alert('Không thể phát bài hát. Vui lòng thử lại.');
            return;
        }

        console.log("🎵 playMusic called with src:", src, " | forcePlay:", forcePlay, " | currentSrc:", currentSrc, " | indexOverride:", indexOverride);

        if (sound && currentSrc === src && !forcePlay && sound.state() === 'loaded') {
            if (isPlaying) {
                lastSeek = sound.seek() || 0;
                sound.pause();
            } else {
                sound.seek(lastSeek);
                sound.play();
                syncWaveform();
            }
            return;
        }

        if (sound) {
            sound.stop();
            sound.unload();
            if (waveformMap[currentSrc]) waveformMap[currentSrc].stop();
            sound = null;
        }

        if (indexOverride !== null) {
            currentIndex = indexOverride;
        } else {
            const index = playlist.indexOf(src);
            if (index !== -1) {
                currentIndex = index;
            } else {
                console.warn('Source not in playlist:', src);
                if (playlist.length === 0) {
                    playlist.push(src);
                    currentIndex = 0;
                }
            }
        }

        if (currentSrc !== src) {
            lastSeek = 0;
        }

        currentSrc = src;

        sound = new Howl({
            src: [src],
            html5: true,
            onload: function () {
                const duration = formatTime(sound.duration());
                $(`.song-card[data-src="${src}"] .song-Duration`).text(duration);
                $('#progressBar').attr('max', sound.duration());
                const card = $(`.song-card[data-src="${src}"]`);
                const name = card.find('.card-title').text().trim();
                const img = card.find('img').attr('src');

                $('#current-song-title').text(name || 'Đang phát bài hát');
                $('#current-song-img').attr('src', img || '/images/default-music.jpg');

                $.ajax({
                    url: '/api/lyric/by-filepath',
                    method: 'GET',
                    data: { filePath: src },
                    success: function (res) {
                        if (res && res.content) {
                            $('#lyricModalLabel').text(`Lời bài hát: ${res.songName}`);
                            $('#lyricContent').html(`<pre>${res.content}</pre>`);
                        } else {
                            $('#lyricContent').html('<p>Không có lời bài hát.</p>');
                        }
                    },
                    error: function () {
                        $('#lyricContent').html('<p>Lỗi khi tải lời bài hát.</p>');
                    }
                });

                sound.seek(lastSeek);
                sound.play();
                updateActiveItem();
                updateSongRowActive();
                syncWaveform();
            },
            onplay: function () {
                isPlaying = true;
                $('#playPause').html('<i class="fas fa-pause"></i>');
                updateProgress();
                updateSongRowActive();
                syncWaveform();
            },
            onpause: function () {
                isPlaying = false;
                lastSeek = sound.seek() || 0;
                $('#playPause').html('<i class="fas fa-play"></i>');
                updateSongRowActive();
                syncWaveform();
            },
            onend: function () {
                isPlaying = false;
                $('#playPause').html('<i class="fas fa-play"></i>');
                lastSeek = 0;
                if (isRepeat) {
                    sound.seek(0);
                    sound.play();
                    syncWaveform();
                } else {
                    playNext();
                }
            },
            onstop: function () {
                isPlaying = false;
                $('#playPause').html('<i class="fas fa-play"></i>');
                updateSongRowActive();
                syncWaveform();
            },
            onloaderror: function (id, err) {
                console.error("Load error:", err);
                alert('Không thể tải bài hát!');
            },
            onplayerror: function (id, err) {
                console.error("Play error:", err);
                sound.once('unlock', function () {
                    sound.seek(lastSeek);
                    sound.play();
                    syncWaveform();
                });
            }
        });

        updateActiveItem();
        updateSongRowActive();
    };

    function playNext() {
        if (isProcessing) {
            console.log('⏭️ playNext skipped: already processing');
            return;
        }
        if (playlist.length === 0) {
            alert('Danh sách phát trống! Vui lòng thêm bài hát.');
            return;
        }

        isProcessing = true;
        console.log(`✅ playNext called | currentIndex: ${currentIndex} | playlist:`, playlist);

        if (isShuffle) {
            let newIndex;
            do {
                newIndex = Math.floor(Math.random() * playlist.length);
            } while (newIndex === currentIndex && playlist.length > 1);
            currentIndex = newIndex;
        } else {
            currentIndex = (currentIndex + 1) % playlist.length;
        }

        if (currentIndex >= 0 && currentIndex < playlist.length) {
            console.log("▶️ Next to:", playlist[currentIndex]);
            playMusic(playlist[currentIndex], true, currentIndex);
        } else {
            console.error('Invalid currentIndex:', currentIndex);
            alert('Không thể phát bài tiếp theo. Vui lòng thử lại.');
        }

        isProcessing = false;
    }

    function playPrevious() {
        if (isProcessing) {
            console.log('⏮️ playPrevious skipped: already processing');
            return;
        }
        if (playlist.length === 0) {
            alert('Danh sách phát trống! Vui lòng thêm bài hát.');
            return;
        }

        isProcessing = true;
        console.log(`✅ playPrevious called | currentIndex: ${currentIndex} | playlist:`, playlist);

        currentIndex = (currentIndex - 1 + playlist.length) % playlist.length;

        if (currentIndex >= 0 && currentIndex < playlist.length) {
            console.log("⏮️ Previous to:", playlist[currentIndex]);
            playMusic(playlist[currentIndex], true, currentIndex);
        } else {
            console.error('Invalid currentIndex:', currentIndex);
            alert('Không thể phát bài trước đó. Vui lòng thử lại.');
        }

        isProcessing = false;
    }

    function toggleShuffle() {
        isShuffle = !isShuffle;
    
        if (isShuffle) {
            console.log('🔀 Shuffle ON');
            $('#shuffle').addClass('active');
        } else {
            console.log('🔀 Shuffle OFF');
            $('#shuffle').removeClass('active');
        }
    }
    
    function toggleRepeat() {
        isRepeat = !isRepeat;
    
        if (isRepeat) {
            console.log('🔁 Repeat ON');
            $('#repeat').addClass('active');
        } else {
            console.log('🔁 Repeat OFF');
            $('#repeat').removeClass('active');
        }
    }
    $(document).off('click', '#playPause').on('click', '#playPause', function (e) {
        e.preventDefault();
        console.log('Play/Pause button clicked');
        if (playlist.length === 0) {
            alert('Danh sách phát trống!');
            return;
        }

        if (currentIndex === -1) {
            currentIndex = 0;
            currentSrc = playlist[currentIndex];
        }

        if (currentSrc) {
            playMusic(currentSrc);
        }
    });
    
    $(document).off('click', '#shuffle').on('click', '#shuffle', function (e) {
        e.preventDefault();
        console.log('Shuffle button clicked');
        toggleShuffle();
    });

    $(document).off('click', '#repeat').on('click', '#repeat', function (e) {
        e.preventDefault();
        console.log('Repeat button clicked');
        toggleRepeat();
    });

    $(document).off('click', '#next').on('click', '#next', function (e) {
        e.preventDefault();
        console.log('Next button clicked');
        playNext();
    });

    $(document).off('click', '#prev').on('click', '#prev', function (e) {
        e.preventDefault();
        console.log('Previous button clicked');
        playPrevious();
    });


    function updateActiveItem() {
        $('.music-item').removeClass('active');
        if (currentIndex >= 0 && playlist[currentIndex]) {
            $(`.music-item[data-src="${playlist[currentIndex]}"]`).addClass('active');
        }
    }

    function updateSongRowActive() {
        $('.song-card').removeClass('playing');
        if (currentSrc) {
            $(`.song-card[data-src="${currentSrc}"]`).addClass('playing');
        }
    }

    function updateProgress() {
        if (sound && sound.state() === 'loaded') {
            var seek = sound.seek() || 0;
            var duration = sound.duration() || 0;

            $('#progressBar').val(seek);
            $('#progressBar').attr('max', duration);
            $('#currentTime').text(formatTime(seek));
            $('#duration').text(formatTime(duration));

            syncWaveform();

            if (isPlaying) {
                requestAnimationFrame(updateProgress);
            }
        }
    }

    function formatTime(seconds) {
        if (!seconds || isNaN(seconds)) return '00:00';
        var minutes = Math.floor(seconds / 60);
        seconds = Math.floor(seconds % 60);
        return (minutes < 10 ? '0' : '') + minutes + ':' + (seconds < 10 ? '0' : '') + seconds;
    }

    window.loadExternalPlaylist = function (srcList) {
        // Chuyển đổi srcList thành mảng các filePath
        window.playlist = srcList.map(song => song.filePath);
        window.currentIndex = 0;

        // Kiểm tra xem bài hát hiện tại có trong playlist mới không
        if (window.currentSrc && window.playlist.includes(window.currentSrc)) {
            window.currentIndex = window.playlist.indexOf(window.currentSrc);
            if (window.isPlaying && window.sound && window.sound.state() === 'loaded') {
                window.sound.seek(window.lastSeek);
                window.sound.play();
                updateActiveItem();
                updateSongRowActive();
                return;
            }
        }

        // Nếu không có bài hát hiện tại hoặc bài hát không trong playlist, phát bài đầu tiên
        if (window.playlist.length > 0) {
            window.currentSrc = window.playlist[0];
            window.currentIndex = 0;
            playMusic(window.currentSrc, true);
            updateSongRowActive();
        }
    };

    // Khởi tạo playlist
    loadPlaylist();

    // Xử lý âm lượng
    let currentVolume = 1.0;

    $('#volume').on('input', function () {
        currentVolume = this.value / 100;
        if (sound) {
            sound.volume(this.value / 100);
        }
    });

    $('#mute').on('click', function () {
        if (sound) {
            const isMuted = sound.mute();
            sound.mute(!isMuted);
            $(this).html(!isMuted ? '<i class="fas fa-volume-mute"></i>' : '<i class="fas fa-volume-up"></i>');
        }
    });

    // Xử lý nhấp vào song-row
    $('.song-card').on('click', function (e) {
        if (!$(e.target).closest('.add-to-playlist').length) {
            var src = $(this).data('src');
            currentIndex = playlist.indexOf(src);
            playMusic(src, true);
            updateSongRowActive();
        }
    });

    // Xử lý thêm vào playlist
    $('.add-to-playlist').on('click', function () {
        var songId = $(this).data('song-id');
        console.log('add-to-playlist clicked, songId:', songId); // Debug
        if (!songId) {
            console.error('Error: songId is undefined or empty');
            alert('Lỗi: Không tìm thấy ID bài hát. Vui lòng thử lại.');
            return;
        }

        $.ajax({
            url: '/Home/CheckLoginStatus',
            type: 'GET',
            success: function (response) {
                console.log('CheckLoginStatus response:', response); // Debug
                if (response.isLoggedIn && response.userId) {
                    $('#songId').val(songId);
                    console.log('Set #songId value:', $('#songId').val()); // Debug
                    $('#addToPlaylistModal').modal('show');
                } else {
                    alert('Vui lòng đăng nhập để thêm bài hát vào danh sách phát!');
                }
            },
            error: function (xhr, status, error) {
                console.error('CheckLoginStatus error:', xhr, status, error);
                alert('Lỗi khi kiểm tra trạng thái đăng nhập. Vui lòng thử lại.');
            }
        });
    });

    $('#savePlaylistBtn').on('click', function () {
        var songId = $('#songId').val();
        var playlistId = $('#existingPlaylist').val();
        var newPlaylistName = $('#newPlaylistName').val();

        console.log('savePlaylistBtn clicked, songId:', songId, 'playlistId:', playlistId, 'newPlaylistName:', newPlaylistName); // Debug

        if (!songId) {
            console.error('Error: songId is empty in savePlaylistBtn');
            alert('Lỗi: ID bài hát không hợp lệ. Vui lòng thử lại.');
            return;
        }

        if (!playlistId && !newPlaylistName) {
            alert('Vui lòng chọn playlist hoặc nhập tên playlist mới!');
            return;
        }

        $.ajax({
            url: '/User/AddToPlaylist',
            type: 'POST',
            data: { songId: songId, playlistId: playlistId, newPlaylistName: newPlaylistName },
            success: function (response) {
                console.log('AddToPlaylist response:', response); // Debug
                if (response.success) {
                    alert(response.message);
                    $('#addToPlaylistModal').modal('hide');
                    location.reload();
                } else {
                    alert(response.message);
                }
            },
            error: function (xhr, status, error) {
                console.error('AddToPlaylist error:', xhr, status, error); // Debug
                alert('Đã xảy ra lỗi khi thêm bài hát vào playlist: ' + error);
            }
        });
    });

    // Xử lý phát playlist
    $('.btn-play').on('click', function () {
        const playlistId = $(this).closest('.playlist-card').data('id');

        $.ajax({
            url: '/Playlist/GetMusicFilesByPlaylistId',
            type: 'GET',
            data: { id: playlistId },
            success: function (res) {
                if (res.success && res.songs && res.songs.length > 0) {
                    window.loadExternalPlaylist(res.songs.map(song => ({ filePath: song.filePath, fileName: song.fileName })));
                } else {
                    alert(res.message || 'Playlist không có bài hát nào.');
                }
            },
            error: function () {
                alert('Lỗi khi tải bài hát của playlist.');
            }
        });
    });

    // Xử lý xem chi tiết playlist
    $('.btn-view').on('click', function () {
        const playlistId = $(this).closest('.playlist-card').data('id');
        window.location.href = '/Playlist/ChiTiet/' + playlistId;
    });

    // Xử lý sửa playlist
    $('.btn-edit').on('click', function () {
        const card = $(this).closest('.playlist-card');
        const id = card.data('id');
        const name = card.data('name');

        $('#editPlaylistId').val(id);
        $('#editPlaylistName').val(name);
        $('#editPlaylistModal').modal('show');
    });

    // Xử lý xoá bài hát khỏi playlist
    $('.remove-from-playlist').on('click', function (e) {
        e.stopPropagation(); // tránh trigger playMusic khi click icon

        const songId = $(this).data('song-id');
        const playlistId = $(this).data('playlist-id');

        if (!songId || !playlistId) {
            alert('Thiếu thông tin bài hát hoặc playlist!');
            return;
        }

        if (!confirm("Bạn có chắc muốn xoá bài hát này khỏi playlist?")) {
            return;
        }

        $.ajax({
            url: '/Playlist/RemoveSong',
            type: 'POST',
            data: { songId: songId, playlistId: playlistId },
            success: function (res) {
                if (res.success) {
                    alert('Đã xoá bài hát khỏi playlist!');
                    // Xoá dòng bài hát khỏi giao diện
                    $(`.song-card[data-song-id="${songId}"]`).remove();
                    loadPlaylist(); // Cập nhật lại danh sách phát
                } else {
                    alert(res.message || 'Xoá thất bại.');
                }
            },
            error: function () {
                alert('Lỗi kết nối server khi xoá bài hát.');
            }
        });
    });

    $('#saveEditBtn').on('click', function () {
        const id = $('#editPlaylistId').val();
        const newName = $('#editPlaylistName').val();

        if (!newName.trim()) {
            alert('Tên mới không được để trống!');
            return;
        }

        $.ajax({
            url: '/Playlist/UpdateName',
            type: 'POST',
            data: { id: id, newName: newName },
            success: function (res) {
                if (res.success) {
                    alert('Đã đổi tên thành công!');
                    location.reload();
                } else {
                    alert(res.message);
                }
            },
            error: function () {
                alert('Có lỗi xảy ra khi đổi tên playlist.');
            }
        });
    });

    $(document).on('click', '.btn-delete-playlist', function () {
        const playlistId = $(this).data('id');
    
        if (!playlistId) {
            alert('Không tìm thấy ID playlist!');
            return;
        }
    
        if (!confirm('Bạn có chắc chắn muốn xoá playlist này?')) {
            return;
        }
    
        $.ajax({
            url: '/Playlist/Delete',
            type: 'POST',
            data: { id: playlistId },
            success: function (res) {
                if (res.success) {
                    alert('Đã xoá playlist thành công!');
                    location.reload();
                } else {
                    alert(res.message || 'Không thể xoá playlist.');
                }
            },
            error: function () {
                alert('Lỗi khi kết nối server để xoá playlist.');
            }
        });
    });
    $(document).on('submit', '#profileForm', function (e) {
        e.preventDefault();
    
        const formData = $(this).serialize();
        console.log('🟢 Form data:', formData);
        $.ajax({
            url: '/User/UpdateProfile',
            type: 'POST',
            data: formData,
            success: function (res) {
                if (res.success) {
                    alert('Cập nhật thành công!');
                    location.reload();
                } else {
                    alert(res.message);
                }
            },
            error: function (xhr, status, error) {
                console.error('🔴 AJAX error:', status, error, xhr.responseText);
                alert('Có lỗi khi cập nhật thông tin: ' + (xhr.responseText || 'Unknown error'))
            }
        });
    });
});