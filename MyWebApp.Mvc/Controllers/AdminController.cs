using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;
using System.Diagnostics;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace MyWebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly MusicService _musicService;
        private readonly LyricService _lyricService;


        public AdminController(MusicService musicService, LyricService lyricService)
        {
            _musicService = musicService;
            _lyricService = lyricService;
        }

        public async Task<IActionResult> Dashboard()
        {
            ViewBag.Role = "Admin";
            var musicFiles = await _musicService.GetAllAsync();
            return View(musicFiles);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSong(string id)
        {
            var ok = await _musicService.DeleteAsync(id);
            return View();
        }

        [HttpGet]
        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Add(string nameSong, IFormFile file, IFormFile? imageFile, IFormFile? lrcFile)
        {
            if (file == null || file.Length == 0)
            {
                ViewBag.Error = "Vui lòng chọn tệp nhạc.";
                return View();
            }

            try
            {
                var fileName = Path.GetFileNameWithoutExtension(file.FileName).Replace(" ", "").ToLower() + ".mp3";
                var gridFsId = await _musicService.UploadToMongoDBAsync(file, fileName);

                var musicFile = new MusicFile
                {
                    NameSong = nameSong,
                    FileName = fileName,
                    FilePath = $"/api/music/{fileName}",
                    UploadeAt = DateTime.Now,
                    GridFSFileId = gridFsId
                };

                if (imageFile != null && imageFile.Length > 0)
                {
                    var ext = Path.GetExtension(imageFile.FileName).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        var imageName = Path.GetFileNameWithoutExtension(imageFile.FileName).Replace(" ", "").ToLower() + ext;
                        var imageBytes = await ReadBytesAsync(imageFile);
                        var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                        musicFile.ImageGridFsId = imageId;
                        musicFile.ImageUrl = $"/api/images/{imageName}";
                    }
                }

                await _musicService.CreateAsync(musicFile);

                // ✅ nếu có lời thì lưu vào DB
                if (lrcFile != null && lrcFile.Length > 0 && lrcFile.FileName.EndsWith(".lrc"))
                {
                    using var reader = new StreamReader(lrcFile.OpenReadStream());
                    var content = await reader.ReadToEndAsync();

                    var lyric = new Lyric
                    {
                        MusicIds = ObjectId.Parse(musicFile.Id),
                        Content = content
                    };

                    await _lyricService.CreateAsync(lyric); // ✅ lưu vào MongoDB
                }

                ViewBag.Message = "Đã thêm bài hát thành công!";
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Lỗi khi tải lên: {ex.Message}";
            }

            return View();
        }


        [HttpGet("Admin/ChiTiet/{id}")]
        public async Task<IActionResult> ChiTiet(string id)
        {
            var song = await _musicService.GetByAsync(id);
            if (song == null)
            {
                return NotFound();
            }

            // Lấy lời bài hát nếu có
            var lyric = await _lyricService.GetByMusicIdAsync(song.Id);
            ViewBag.Lyric = lyric?.Content ?? "Chưa có lời bài hát.";

            return View(song);
        }

        [HttpPost("Admin/UpdateSong")]
        public async Task<IActionResult> UpdateSong(string id, string NameSong, IFormFile? ImageFile)
        {
            var song = await _musicService.GetByAsync(id);
            if (song == null) return NotFound();

            song.NameSong = NameSong;

            if (ImageFile != null && ImageFile.Length > 0)
            {
                var ext = Path.GetExtension(ImageFile.FileName).ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                {
                    var imageName = Path.GetFileNameWithoutExtension(ImageFile.FileName).Replace(" ", "").ToLower() + ext;
                    byte[] imageBytes;
                    using (var ms = new MemoryStream())
                    {
                        await ImageFile.CopyToAsync(ms);
                        imageBytes = ms.ToArray();
                    }
                    var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                    song.ImageGridFsId = imageId;
                    song.ImageUrl = $"/api/images/{imageName}";
                }
            }

            await _musicService.UpdateAsync(song.Id, song); // Cần thêm phương thức Update trong MusicService
            return RedirectToAction("ChiTiet", new { id = song.Id });
        }

        private async Task<byte[]> ReadBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

    }
}
