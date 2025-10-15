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
        /// Lấy file nhạc theo tên file (ví dụ: songname.mp3).
        /// </summary>
        /// <param name="fileName">Tên file nhạc đã lưu trong MongoDB.</param>
        /// <returns>Stream nhạc dạng <c>audio/mpeg</c>.</returns>
        [HttpGet("{fileName}")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetMusicFile(string fileName)
        {
            Console.WriteLine($"[MusicApi] Yêu cầu lấy file nhạc: {fileName}");

            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest(new { success = false, message = "Thiếu tên file nhạc." });

            try
            {
                var fileBytes = await _musicService.DownloadFileBytesAsync(fileName);
                if (fileBytes == null)
                {
                    Console.WriteLine($"[MusicApi] Không tìm thấy file: {fileName}");
                    return NotFound(new { success = false, message = "Không tìm thấy file nhạc." });
                }

                // Xác định loại MIME theo đuôi file
                var contentType = GetMimeType(fileName);
                Console.WriteLine($"[MusicApi] Trả về file: {fileName} ({contentType}), kích thước {fileBytes.Length} bytes");

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicApi] Lỗi khi tải file {fileName}: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy file nhạc theo ID trong MongoDB.
        /// </summary>
        /// <param name="id">ID của file nhạc (ObjectId dạng chuỗi).</param>
        /// <returns>Stream nhạc.</returns>
        [HttpGet("byid/{id}")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetMusicById(string id)
        {
            Console.WriteLine($"[MusicApi] Yêu cầu lấy nhạc theo ID bài hát: {id}");

            // Tìm thông tin bài hát trong MongoDB
            var song = await _musicService.GetByAsync(id);
            if (song == null)
            {
                Console.WriteLine($"[MusicApi] Không tìm thấy bài hát có ID: {id}");
                return NotFound(new { message = "Không tìm thấy bài hát." });
            }

            if (song.GridFSFileId == null)
            {
                Console.WriteLine($"[MusicApi] Bài hát {song.NameSong} không có GridFSFileId.");
                return NotFound(new { message = "Bài hát không có tệp âm thanh trong GridFS." });
            }

            // Lấy stream từ GridFS bằng GridFSFileId
            var stream = await _musicService.GetMusicFileAsync(song.GridFSFileId.ToString());
            if (stream == null)
            {
                Console.WriteLine($"[MusicApi] Không có stream cho GridFS ID: {song.GridFSFileId}");
                return NotFound(new { message = "Không tìm thấy file nhạc trong GridFS." });
            }

            Console.WriteLine($"[MusicApi] Trả về nhạc: {song.NameSong} ({song.FileName})");
            return File(stream, "audio/mpeg", song.FileName);
        }

        /// <summary>
        /// Hàm phụ: xác định loại MIME dựa vào phần mở rộng file.
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
