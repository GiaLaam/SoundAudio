using Microsoft.AspNetCore.Mvc;
using MyWebApp.Services;
using MyWebApp.Mvc.Services;

namespace MyWebApp.Controllers
{
    [Route("Playlist")]
    public class PlaylistController : Controller
    {
        private readonly PlaylistApiService _playlistApiService;
        private readonly MusicApiService _musicApiService;

        public PlaylistController(PlaylistApiService playlistApiService, MusicApiService musicApiService)
        {
            _playlistApiService = playlistApiService ?? throw new ArgumentNullException(nameof(playlistApiService));
            _musicApiService = musicApiService ?? throw new ArgumentNullException(nameof(musicApiService));
        }

        [HttpGet("GetMusicFilesByPlaylistId")]
        public async Task<IActionResult> GetMusicFilesByPlaylistId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Json(new { success = false, message = "ID không hợp lệ." });
            }

            try
            {
                var playlist = await _playlistApiService.GetByIdAsync(id);
                if (playlist == null || playlist.MusicIds == null || !playlist.MusicIds.Any())
                {
                    return Json(new { success = false, message = "Playlist không tồn tại hoặc trống." });
                }

                var allSongs = await _musicApiService.GetAllAsync();
                var songs = allSongs
                    .Where(m => playlist.MusicIds.Contains(m.Id))
                    .Select(m => new
                    {
                        m.Id,
                        m.FileName,
                        m.FilePath,
                        m.Duration
                    }).ToList();

                return Json(new { success = true, songs });
            }
            catch (Exception)
            {
                // Avoid logging the exception directly to prevent potential logging-related errors
                return Json(new { success = false, message = "Đã xảy ra lỗi khi lấy danh sách bài hát." });
            }
        }

        [HttpPost("UpdateName")]
        public async Task<IActionResult> UpdateName(string id, string newName)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrWhiteSpace(newName))
            {
                return Json(new { success = false, message = "ID hoặc tên mới không hợp lệ." });
            }

            try
            {
                var playlist = await _playlistApiService.GetByIdAsync(id);
                if (playlist == null)
                    return Json(new { success = false, message = "Playlist không tồn tại." });

                playlist.Name = newName.Trim();
                await _playlistApiService.UpdateAsync(playlist);
                return Json(new { success = true, message = "Đã đổi tên playlist thành công." });
            }
            catch (Exception)
            {
                // Avoid logging the exception directly
                return Json(new { success = false, message = "Đã xảy ra lỗi khi đổi tên playlist." });
            }
        }

        [HttpGet("ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(string id)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("DangNhap", "Home");
            }

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            try
            {
                var playlist = await _playlistApiService.GetByIdAsync(id);
                if (playlist == null)
                {
                    return NotFound();
                }

                var allSongs = await _musicApiService.GetAllAsync();
                var songs = (playlist.MusicIds != null)
                    ? allSongs.Where(m => playlist.MusicIds.Contains(m.Id)).ToList()
                    : new List<MyWebApp.Models.MusicFile>();

                ViewBag.PlaylistName = playlist.Name;
                ViewBag.PlaylistSongs = songs;
                ViewBag.PlaylistId = playlist.Id;
                ViewBag.ViewMode = "ChiTiet";
                ViewBag.IsLoggedIn = true;

                return View("~/Views/User/NguoiDung.cshtml", songs);
            }
            catch (Exception)
            {
                // Avoid logging the exception directly
                return StatusCode(500, new { message = "Lỗi máy chủ nội bộ" });
            }
        }

        [HttpPost("RemoveSong")]
        public async Task<IActionResult> RemoveSong(string playlistId, string songId)
        {
            if (string.IsNullOrEmpty(playlistId) || string.IsNullOrEmpty(songId))
                return Json(new { success = false, message = "Thiếu thông tin" });

            var playlist = await _playlistApiService.GetByIdAsync(playlistId);
            if (playlist == null)
                return Json(new { success = false, message = "Playlist không tồn tại" });

            playlist.MusicIds.Remove(songId);
            await _playlistApiService.UpdateAsync(playlist);
            
            return Json(new { success = true });
        }
        
        [HttpPost("Delete")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Json(new { success = false, message = "ID không hợp lệ." });
            }
            try
            {
                await _playlistApiService.DeleteAsync(id);
                return Json(new { success = true });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "Lỗi server khi xoá playlist." });
            }
        }
    }
}