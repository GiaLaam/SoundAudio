using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/playlist")]
    [Authorize] // ✅ Đảm bảo chỉ người đã đăng nhập mới truy cập được
    public class PlaylistApiController : ControllerBase
    {
        private readonly PlaylistService _playlistService;
        private readonly MusicService _musicService;
        private readonly UserManager<ApplicationUser> _userManager;

        public PlaylistApiController(PlaylistService playlistService, MusicService musicService, UserManager<ApplicationUser> userManager)
        {
            _playlistService = playlistService;
            _musicService = musicService;
            _userManager = userManager;
        }

        // 1️⃣ Lấy tất cả playlist của user hiện tại
        [HttpGet("my-playlists")]
        public async Task<IActionResult> GetMyPlaylists()
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var playlists = await _playlistService.GetByOwnerAsync(userId, "user");
            return Ok(new { success = true, playlists });
        }

        // 2️⃣ Lấy chi tiết playlist + bài hát
        [HttpGet("{playlistId}")]
        public async Task<IActionResult> GetPlaylist(string playlistId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var playlist = await _playlistService.GetByIdAsync(playlistId);

            if (playlist == null || playlist.OwnerId != userId)
                return NotFound(new { success = false, message = "Playlist không tồn tại hoặc không thuộc về bạn." });

            var allSongs = await _musicService.GetAllAsync();
            var songs = allSongs.Where(m => playlist.MusicIds.Contains(m.Id)).ToList();

            return Ok(new { success = true, playlist, songs });
        }

        // 3️⃣ Tạo playlist mới
        [HttpPost("create")]
        public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var newPlaylist = new Playlist
            {
                OwnerId = userId,
                OwnerType = "user",
                Name = request.Name,
                MusicIds = request.SongId != null ? new List<string> { request.SongId } : new List<string>()
            };

            await _playlistService.CreateAsync(newPlaylist);
            return Ok(new { success = true, message = "Tạo playlist thành công", playlist = newPlaylist });
        }

        // 4️⃣ Thêm bài hát vào playlist
        [HttpPost("add-song")]
        public async Task<IActionResult> AddSongToPlaylist([FromBody] AddSongRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var playlist = await _playlistService.GetByIdAsync(request.PlaylistId);

            if (playlist == null || playlist.OwnerId != userId)
                return Unauthorized(new { success = false, message = "Playlist không tồn tại hoặc không thuộc về bạn." });

            if (!playlist.MusicIds.Contains(request.SongId))
            {
                playlist.MusicIds.Add(request.SongId);
                await _playlistService.UpdateAsync(playlist);
            }

            return Ok(new { success = true, message = "Đã thêm bài hát vào playlist" });
        }

        // 5️⃣ Xoá bài hát khỏi playlist
        [HttpDelete("{playlistId}/remove-song/{songId}")]
        public async Task<IActionResult> RemoveSong(string playlistId, string songId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var playlist = await _playlistService.GetByIdAsync(playlistId);

            if (playlist == null || playlist.OwnerId != userId)
                return Unauthorized(new { success = false, message = "Playlist không tồn tại hoặc không thuộc về bạn." });

            playlist.MusicIds.Remove(songId);
            await _playlistService.UpdateAsync(playlist);

            return Ok(new { success = true, message = "Đã xoá bài hát khỏi playlist" });
        }

        // 6️⃣ Đổi tên playlist
        [HttpPut("rename")]
        public async Task<IActionResult> RenamePlaylist([FromBody] RenamePlaylistRequest request)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var playlist = await _playlistService.GetByIdAsync(request.PlaylistId);

            if (playlist == null || playlist.OwnerId != userId)
                return Unauthorized(new { success = false, message = "Playlist không tồn tại hoặc không thuộc về bạn." });

            playlist.Name = request.NewName;
            await _playlistService.UpdateAsync(playlist);

            return Ok(new { success = true, message = "Đổi tên playlist thành công." });
        }

        // 7️⃣ Xoá playlist
        [HttpDelete("delete/{playlistId}")]
        public async Task<IActionResult> DeletePlaylist(string playlistId)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var playlist = await _playlistService.GetByIdAsync(playlistId);

            if (playlist == null || playlist.OwnerId != userId)
                return Unauthorized(new { success = false, message = "Playlist không tồn tại hoặc không thuộc về bạn." });

            await _playlistService.DeleteAsync(playlistId);
            return Ok(new { success = true, message = "Đã xoá playlist thành công." });
        }

        // DTO (Model nhận dữ liệu từ client)
        public class CreatePlaylistRequest
        {
            public string Name { get; set; } = string.Empty;
            public string? SongId { get; set; } // Cho phép tạo playlist và thêm bài hát cùng lúc
        }

        public class AddSongRequest
        {
            public string PlaylistId { get; set; } = string.Empty;
            public string SongId { get; set; } = string.Empty;
        }

        public class RenamePlaylistRequest
        {
            public string PlaylistId { get; set; } = string.Empty;
            public string NewName { get; set; } = string.Empty;
        }
    }
}
