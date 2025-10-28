using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            return File(fileBytes, contentType, fileName);
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

            return File(stream, "audio/mpeg", song.FileName);
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
    }
}
