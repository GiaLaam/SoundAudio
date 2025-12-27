using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyWebApp.Models;
using MyWebApp.Services;

namespace MyWebApp.Api.Controllers
{
    /// <summary>
    /// API quản lý và lấy lời bài hát (lyric) từ MongoDB.
    /// </summary>
    [ApiController]
    [Route("api/lyric")]
    public class LyricApiController : ControllerBase
    {
        private readonly LyricService _lyricService;
        private readonly MusicService _musicService;

        public LyricApiController(LyricService lyricService, MusicService musicService)
        {
            _lyricService = lyricService;
            _musicService = musicService;
        }

        /// <summary>
        /// Lấy lời bài hát theo đường dẫn file nhạc.
        /// </summary>
        /// <param name="filePath">Đường dẫn của file nhạc (ví dụ: /api/music/songname.mp3)</param>
        /// <returns>Nội dung lời bài hát và tên bài hát.</returns>
        [HttpGet("by-filepath")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetByFilePath([FromQuery] string filePath)
        {
            Console.WriteLine($"[LyricApi] Yêu cầu lấy lời theo filePath: {filePath}");

            if (string.IsNullOrWhiteSpace(filePath))
                return BadRequest(new { success = false, message = "Thiếu đường dẫn bài hát." });

            try
            {
                var song = await _musicService.GetByFilePathAsync(filePath);
                if (song == null)
                {
                    Console.WriteLine($"[LyricApi] Không tìm thấy bài hát: {filePath}");
                    return NotFound(new { success = false, message = "Không tìm thấy bài hát." });
                }

                var lyric = await _lyricService.GetByMusicIdAsync(song.Id);
                if (lyric == null)
                {
                    Console.WriteLine($"[LyricApi] Không có lời cho bài hát ID: {song.Id}");
                    return NotFound(new { success = false, message = "Không có lời bài hát." });
                }

                Console.WriteLine($"[LyricApi] Đã lấy lời thành công cho: {song.NameSong}");
                return Ok(new
                {
                    success = true,
                    songName = song.NameSong,
                    content = lyric.Content
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LyricApi] Lỗi lấy lời bài hát: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy lời bài hát theo ID bài hát trong MongoDB.
        /// </summary>
        /// <param name="songId">ID của bài hát (ObjectId dạng chuỗi).</param>
        [HttpGet("by-song/{songId}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> GetBySongId(string songId)
        {
            Console.WriteLine($"[LyricApi] Yêu cầu lấy lời theo songId: {songId}");
            try
            {
                var lyric = await _lyricService.GetByMusicIdAsync(songId);
                if (lyric == null)
                    return NotFound(new { success = false, message = "Không tìm thấy lời bài hát." });

                return Ok(new { success = true, songId, content = lyric.Content });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LyricApi] Lỗi khi lấy lời bài hát ID {songId}: {ex.Message}");
                return StatusCode(500, new { success = false, message = $"Lỗi hệ thống: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy tất cả lời bài hát (Admin).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin", AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        public async Task<IActionResult> GetAll()
        {
            var lyrics = await _lyricService.GetAllAsync();
            return Ok(new { success = true, data = lyrics });
        }

        /// <summary>
        /// Tạo lời bài hát mới (Admin).
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin", AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        public async Task<IActionResult> Create([FromBody] CreateLyricRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { success = false, message = "Nội dung lời bài hát không được để trống." });

            if (!MongoDB.Bson.ObjectId.TryParse(request.MusicId, out var musicObjectId))
                return BadRequest(new { success = false, message = "ID bài hát không hợp lệ." });

            var lyric = new Lyric
            {
                MusicIds = musicObjectId,
                Content = request.Content
            };

            await _lyricService.CreateAsync(lyric);
            return Ok(new { success = true, message = "Tạo lời bài hát thành công!", data = lyric });
        }

        /// <summary>
        /// Cập nhật lời bài hát (Admin).
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin", AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        public async Task<IActionResult> Update(string id, [FromBody] UpdateLyricRequest request)
        {
            var lyric = await _lyricService.GetByAsync(id);
            if (lyric == null)
                return NotFound(new { success = false, message = "Không tìm thấy lời bài hát." });

            if (!string.IsNullOrWhiteSpace(request.Content))
                lyric.Content = request.Content;

            await _lyricService.UpdateAsync(id, lyric);
            return Ok(new { success = true, message = "Cập nhật thành công!", data = lyric });
        }

        /// <summary>
        /// Xóa lời bài hát (Admin).
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin", AuthenticationSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{CookieAuthenticationDefaults.AuthenticationScheme}")]
        public async Task<IActionResult> Delete(string id)
        {
            var lyric = await _lyricService.GetByAsync(id);
            if (lyric == null)
                return NotFound(new { success = false, message = "Không tìm thấy lời bài hát." });

            await _lyricService.DeleteAsync(id);
            return Ok(new { success = true, message = "Xóa thành công!" });
        }

        // DTOs
        public class CreateLyricRequest
        {
            public string MusicId { get; set; } = string.Empty;
            public string Content { get; set; } = string.Empty;
        }

        public class UpdateLyricRequest
        {
            public string? Content { get; set; }
        }
    }
}
