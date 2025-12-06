using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Api.Controllers
{
    /// <summary>
    /// API phục vụ file nhạc từ MongoDB GridFS.
    /// </summary>
    [ApiController]
    [Route("api/music")]
    public class MusicApiController : ControllerBase
    {
        private readonly MusicService _musicService;

        public MusicApiController(MusicService musicService)
        {
            _musicService = musicService;
        }

        /// <summary>
        /// ✅ Lấy danh sách tất cả bài hát (dành cho app mobile).
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // 👈 Cho phép truy cập không cần đăng nhập
        [ProducesResponseType(200)]
        public async Task<IActionResult> GetAllSongs()
        {
            try
            {
                var songs = await _musicService.GetAllAsync();

                if (songs == null || !songs.Any())
                    return NotFound(new { message = "Không có bài hát nào trong cơ sở dữ liệu." });

                return Ok(songs.Select(s => new
                {
                    s.Id,
                    s.NameSong,
                    s.FileName,
                    s.FilePath,
                    s.ImageUrl,
                    s.AuthorId,
                    s.UploadeAt
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicApi] Lỗi lấy danh sách bài hát: {ex.Message}");
                return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        /// <summary>
        /// ✅ Lấy file nhạc theo tên file (ví dụ: songname.mp3).
        /// </summary>
        [HttpGet("{fileName}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMusicFile(string fileName)
        {
            Console.WriteLine($"[MusicApi] Yêu cầu lấy file nhạc: {fileName}");

            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest(new { message = "Thiếu tên file nhạc." });

            var fileBytes = await _musicService.DownloadFileBytesAsync(fileName);
            if (fileBytes == null)
                return NotFound(new { message = "Không tìm thấy file nhạc." });

            var contentType = GetMimeType(fileName);
            Console.WriteLine($"[MusicApi] Trả về file: {fileName} ({contentType})");

            return File(fileBytes, contentType, fileName, enableRangeProcessing: true);
        }

        /// <summary>
        /// ✅ Stream file nhạc theo ID bài hát (cho audio player).
        /// </summary>
        [HttpGet("stream/{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> StreamMusic(string id)
        {
            Console.WriteLine($"[MusicApi] Stream nhạc - ID bài hát: {id}");

            var song = await _musicService.GetByAsync(id);
            if (song == null)
                return NotFound(new { message = "Không tìm thấy bài hát." });

            if (song.GridFSFileId == null)
                return NotFound(new { message = "Bài hát không có file nhạc trong GridFS." });

            var stream = await _musicService.GetMusicFileAsync(song.GridFSFileId.ToString());
            if (stream == null)
                return NotFound(new { message = "Không tìm thấy file nhạc trong GridFS." });

            return File(stream, "audio/mpeg", song.FileName, enableRangeProcessing: true);
        }

        /// <summary>
        /// ✅ Lấy file nhạc theo ID trong MongoDB.
        /// </summary>
        [HttpGet("byid/{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetMusicById(string id)
        {
            Console.WriteLine($"[MusicApi] Yêu cầu lấy nhạc theo ID bài hát: {id}");

            var song = await _musicService.GetByAsync(id);
            if (song == null)
                return NotFound(new { message = "Không tìm thấy bài hát." });

            if (song.GridFSFileId == null)
                return NotFound(new { message = "Bài hát không có file nhạc trong GridFS." });

            var stream = await _musicService.GetMusicFileAsync(song.GridFSFileId.ToString());
            if (stream == null)
                return NotFound(new { message = "Không tìm thấy file nhạc trong GridFS." });

            return File(stream, "audio/mpeg", song.FileName, enableRangeProcessing: true);
        }

        /// <summary>
        /// Xác định loại MIME theo phần mở rộng.
        /// </summary>
        private string GetMimeType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                ".ogg" => "audio/ogg",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Upload bài hát mới.
        /// </summary>
        [HttpPost("upload")]
        [Authorize(Roles = "Admin")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UploadMusic([FromForm] UploadMusicRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { message = "File nhạc không được để trống." });

                if (string.IsNullOrWhiteSpace(request.NameSong))
                    return BadRequest(new { message = "Tên bài hát không được để trống." });

                var sanitizedFileName = Path.GetFileNameWithoutExtension(request.File.FileName)
                    .Replace(" ", "").ToLower() + ".mp3";

                // Upload file nhạc vào GridFS
                var gridFsId = await _musicService.UploadToMongoDBAsync(request.File, sanitizedFileName);

                // Tạo đối tượng MusicFile
                var music = new MyWebApp.Models.MusicFile
                {
                    NameSong = request.NameSong,
                    FileName = sanitizedFileName,
                    FilePath = $"/api/music/{sanitizedFileName}",
                    GridFSFileId = gridFsId,
                    UploadeAt = DateTime.UtcNow
                };

                // Upload ảnh nếu có
                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    var ext = Path.GetExtension(request.ImageFile.FileName).ToLower();
                    var imageFileName = Path.GetFileNameWithoutExtension(sanitizedFileName) + ext;
                    
                    using var ms = new MemoryStream();
                    await request.ImageFile.CopyToAsync(ms);
                    var imageId = await _musicService.UploadImageAsync(ms.ToArray(), imageFileName);
                    
                    music.ImageGridFsId = imageId;
                    music.ImageUrl = $"/api/images/{imageFileName}";
                }

                await _musicService.CreateAsync(music);

                return Ok(new { success = true, message = "Upload thành công!", data = music });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicApi] Lỗi upload: {ex.Message}");
                return StatusCode(500, new { message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy ảnh bài hát.
        /// </summary>
        [HttpGet("image/{fileName}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetImage(string fileName)
        {
            var imageBytes = await _musicService.DownloadImageAsync(fileName);
            if (imageBytes == null)
                return NotFound();

            var ext = Path.GetExtension(fileName).ToLower();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return File(imageBytes, contentType);
        }

        /// <summary>
        /// Xóa bài hát theo ID.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMusic(string id)
        {
            var result = await _musicService.DeleteAsync(id);
            if (!result)
                return NotFound(new { message = "Không tìm thấy bài hát." });

            return Ok(new { success = true, message = "Xóa thành công!" });
        }
    }
}
