using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;
using System;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace MyWebApp.Controllers
{
    public class MusicController : Controller
    {
        private readonly MusicService _musicService;
        private readonly LyricService _lyricService;

        public MusicController(MusicService musicService, LyricService lyricService)
        {
            _musicService = musicService;
            _lyricService = lyricService;
        }

        // Hiển thị danh sách nhạc
        public async Task<IActionResult> Index()
        {
            await _musicService.SyncMusicFilesWithGridFS();
            var songs = await _musicService.GetAllAsync();
            return View(songs.Select(s => s.FileName).ToList());
        }

        // Upload nhạc + lời bài hát + ảnh
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, IFormFile? lrcFile, IFormFile? imageFile)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Vui lòng chọn một tệp nhạc.";
                return RedirectToAction("Index");
            }

            if (!file.FileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Chỉ hỗ trợ tệp MP3.";
                return RedirectToAction("Index");
            }

            try
            {
                var sanitizedFileName = Path.GetFileNameWithoutExtension(file.FileName)
                    .Replace(" ", "").ToLower() + ".mp3";

                Console.WriteLine($"Uploading file: {sanitizedFileName}");

                // Lưu file nhạc
                var gridFsId = await _musicService.UploadToMongoDBAsync(file, sanitizedFileName);

                // Tạo đối tượng music
                var musicFile = new MusicFile
                {
                    FileName = sanitizedFileName,
                    FilePath = $"/api/music/{sanitizedFileName}",
                    GridFSFileId = gridFsId
                };

                // Nếu có ảnh
                if (imageFile != null && imageFile.Length > 0)
                {
                    var ext = Path.GetExtension(imageFile.FileName).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                    {
                        var imageFileName = Path.GetFileNameWithoutExtension(imageFile.FileName)
                            .Replace(" ", "").ToLower() + ext;

                        var imageBytes = await ReadBytesAsync(imageFile);
                        var imageId = await _musicService.UploadImageAsync(imageBytes, imageFileName);

                        // Lưu ID GridFS vào musicFile
                        musicFile.ImageGridFsId = imageId;
                        musicFile.ImageUrl = $"/api/images/{imageFileName}"; // nếu bạn có endpoint hiển thị
                    }
                }

                await _musicService.CreateAsync(musicFile);

                // Nếu có file LRC
                if (lrcFile != null && lrcFile.Length > 0 && lrcFile.FileName.EndsWith(".lrc"))
                {
                    using var reader = new StreamReader(lrcFile.OpenReadStream());
                    var lrcContent = await reader.ReadToEndAsync();

                    var lyric = new Lyric
                    {
                        MusicIds = ObjectId.Parse(musicFile.Id),
                        Content = lrcContent
                    };
                    await _lyricService.CreateAsync(lyric);
                }

                TempData["Message"] = $"Đã thêm bài hát '{sanitizedFileName}' thành công.";
            }
            catch (MongoException mex)
            {
                Console.WriteLine($"MongoDB upload error: {mex.Message}");
                TempData["Error"] = $"Lỗi MongoDB: {mex.Message}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General upload error: {ex.Message}");
                TempData["Error"] = $"Lỗi hệ thống: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Đọc byte[] từ file
        private async Task<byte[]> ReadBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }

    }
}
