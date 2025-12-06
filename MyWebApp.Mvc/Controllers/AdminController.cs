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

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.TotalSongs = (await _musicApiService.GetAllAsync())?.Count ?? 0;
            ViewBag.TotalAlbums = (await _albumApiService.GetAllAsync())?.Count ?? 0;
            ViewBag.TotalUsers = 0; // TODO: Implement user count
            ViewBag.TotalPlaylists = 0; // TODO: Implement playlist count
            return View();
        }

        public async Task<IActionResult> ManageMusic()
        {
            var music = await _musicApiService.GetAllAsync();
            return View(music);
        }

        public async Task<IActionResult> ManageAlbum()
        {
            var albums = await _albumApiService.GetAllAsync();
            return View(albums);
        }

        [HttpPost]
        public async Task<IActionResult> AddMusic(string nameSong, IFormFile musicFile, IFormFile? imageFile)
        {
            if (musicFile == null || musicFile.Length == 0)
            {
                ViewBag.Error = "File nhạc không được để trống.";
                return RedirectToAction("ManageMusic");
            }

            if (string.IsNullOrWhiteSpace(nameSong))
            {
                ViewBag.Error = "Tên bài hát không được để trống.";
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
                    imageFileName);

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
        public async Task<IActionResult> DeleteMusic(string id)
        {
            var result = await _musicApiService.DeleteAsync(id);
            if (result)
            {
                return Ok();
            }
            return BadRequest();
        }
    }
}
