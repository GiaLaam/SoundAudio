using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Mvc.Services;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MyWebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly MusicApiService _musicApiService;
        private readonly AlbumApiService _albumApiService;

        public AdminController(MusicApiService musicApiService, AlbumApiService albumApiService)
        {
            _musicApiService = musicApiService;
            _albumApiService = albumApiService;
        }

        #region Dashboard
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalSongs = (await _musicApiService.GetAllAsync())?.Count ?? 0;
            ViewBag.TotalAlbums = (await _albumApiService.GetAllAsync())?.Count ?? 0;
            ViewBag.TotalUsers = 0;
            ViewBag.TotalPlaylists = 0;
            return View();
        }
        #endregion

        #region Music Management
        public async Task<IActionResult> ManageMusic()
        {
            var music = await _musicApiService.GetAllAsync();
            var albums = await _albumApiService.GetAllAsync();
            ViewBag.Albums = albums;
            return View(music);
        }

        public async Task<IActionResult> EditMusic(string id)
        {
            var music = await _musicApiService.GetByIdAsync(id);
            if (music == null)
            {
                TempData["Error"] = "Không tìm thấy bài hát.";
                return RedirectToAction("ManageMusic");
            }
            var albums = await _albumApiService.GetAllAsync();
            ViewBag.Albums = albums;
            return View(music);
        }

        [HttpPost]
        public async Task<IActionResult> AddMusic(string nameSong, IFormFile musicFile, IFormFile? imageFile, string? albumId)
        {
            // Debug: kiểm tra JWT token
            var token = HttpContext.Session.GetString("JwtToken");
            Console.WriteLine($"[AdminController] JWT Token exists: {!string.IsNullOrEmpty(token)}");
            Console.WriteLine($"[AdminController] Adding music: {nameSong}, AlbumId: {albumId}");

            if (musicFile == null || musicFile.Length == 0)
            {
                TempData["Error"] = "File nhạc không được để trống.";
                return RedirectToAction("ManageMusic");
            }

            if (string.IsNullOrWhiteSpace(nameSong))
            {
                TempData["Error"] = "Tên bài hát không được để trống.";
                return RedirectToAction("ManageMusic");
            }

            try
            {
                using var musicStream = musicFile.OpenReadStream();
                Stream? imageStream = null;
                string? imageFileName = null;

                if (imageFile != null && imageFile.Length > 0)
                {
                    imageStream = imageFile.OpenReadStream();
                    imageFileName = imageFile.FileName;
                }

                var result = await _musicApiService.UploadAsync(
                    nameSong, 
                    musicStream, 
                    musicFile.FileName, 
                    imageStream, 
                    imageFileName,
                    albumId);

                imageStream?.Dispose();

                if (result != null)
                {
                    TempData["Success"] = "Thêm bài hát thành công!";
                }
                else
                {
                    TempData["Error"] = "Thêm bài hát thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("ManageMusic");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMusic(string id, string nameSong, string? albumId, IFormFile? imageFile)
        {
            try
            {
                Stream? imageStream = null;
                string? imageFileName = null;

                if (imageFile != null && imageFile.Length > 0)
                {
                    imageStream = imageFile.OpenReadStream();
                    imageFileName = imageFile.FileName;
                }

                var result = await _musicApiService.UpdateAsync(id, nameSong, albumId, imageStream, imageFileName);
                imageStream?.Dispose();

                if (result)
                {
                    TempData["Success"] = "Cập nhật bài hát thành công!";
                }
                else
                {
                    TempData["Error"] = "Cập nhật bài hát thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("ManageMusic");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteMusic(string id)
        {
            var result = await _musicApiService.DeleteAsync(id);
            if (result)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false });
        }
        #endregion

        #region Album Management
        public async Task<IActionResult> ManageAlbum()
        {
            var albums = await _albumApiService.GetAllAsync();
            return View(albums);
        }

        public async Task<IActionResult> EditAlbum(string id)
        {
            var album = await _albumApiService.GetByIdAsync(id);
            if (album == null)
            {
                TempData["Error"] = "Không tìm thấy album.";
                return RedirectToAction("ManageAlbum");
            }
            return View(album);
        }

        [HttpPost]
        public async Task<IActionResult> AddAlbum(string name, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Tên album không được để trống.";
                return RedirectToAction("ManageAlbum");
            }

            try
            {
                Album? result;
                if (imageFile != null && imageFile.Length > 0)
                {
                    using var imageStream = imageFile.OpenReadStream();
                    result = await _albumApiService.CreateWithImageAsync(name, imageStream, imageFile.FileName);
                }
                else
                {
                    result = await _albumApiService.CreateAsync(name);
                }

                if (result != null)
                {
                    TempData["Success"] = "Thêm album thành công!";
                }
                else
                {
                    TempData["Error"] = "Thêm album thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("ManageAlbum");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAlbum(string id, string name, IFormFile? imageFile)
        {
            try
            {
                bool result;
                if (imageFile != null && imageFile.Length > 0)
                {
                    using var imageStream = imageFile.OpenReadStream();
                    result = await _albumApiService.UpdateWithImageAsync(id, name, imageStream, imageFile.FileName);
                }
                else
                {
                    result = await _albumApiService.UpdateAsync(id, name);
                }

                if (result)
                {
                    TempData["Success"] = "Cập nhật album thành công!";
                }
                else
                {
                    TempData["Error"] = "Cập nhật album thất bại.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi: {ex.Message}";
            }

            return RedirectToAction("ManageAlbum");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAlbum(string id)
        {
            var result = await _albumApiService.DeleteAsync(id);
            if (result)
            {
                return Ok(new { success = true });
            }
            return BadRequest(new { success = false });
        }
        #endregion
    }
}
