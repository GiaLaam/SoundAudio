using Microsoft.AspNetCore.Mvc;
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
    }
}
