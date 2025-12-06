using Microsoft.AspNetCore.Mvc;
using MyWebApp.Mvc.Services;
using MyWebApp.Models;

namespace MyWebApp.Mvc.Controllers
{
    public class AlbumController : Controller
    {
        private readonly AlbumApiService _albumApiService;
        private readonly MusicApiService _musicApiService;

        public AlbumController(AlbumApiService albumApiService, MusicApiService musicApiService)
        {
            _albumApiService = albumApiService;
            _musicApiService = musicApiService;
        }

        // GET: Album
        public async Task<IActionResult> Index()
        {
            var albums = await _albumApiService.GetAllAsync();
            return View(albums);
        }

        // GET: Album/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var album = await _albumApiService.GetByIdAsync(id);
            if (album == null)
            {
                return NotFound();
            }

            // Lấy danh sách bài hát trong album
            var allMusic = await _musicApiService.GetAllAsync();
            var albumSongs = allMusic.Where(m => m.AlbumId == id).ToList();

            ViewBag.Songs = albumSongs;
            return View(album);
        }
    }
}
