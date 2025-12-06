using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyWebApp.Models;
using MyWebApp.Services;
using MongoDB.Bson;

namespace MyWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly MusicService _musicService;
        private readonly LyricService _lyricService;

        public AdminApiController(MusicService musicService, LyricService lyricService)
        {
            _musicService = musicService;
            _lyricService = lyricService;
        }

        /// <summary>
        /// Lấy danh sách toàn bộ bài hát.
        /// </summary>
        [HttpGet("songs")]
        [ProducesResponseType(typeof(IEnumerable<MusicFile>), 200)]
        public async Task<IActionResult> GetAllSongs()
        {
            var songs = await _musicService.GetAllAsync();
            return Ok(songs);
        }

        /// <summary>
        /// Xóa bài hát theo ID.
        /// </summary>
        [HttpDelete("delete/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteSong(string id)
        {
            var ok = await _musicService.DeleteAsync(id);
            return ok ? Ok(new { success = true }) : NotFound(new { success = false });
        }

        /// <summary>
        /// Thêm bài hát mới (upload file nhạc, ảnh, lời .lrc)
        /// </summary>
        [HttpPost("add")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Add([FromForm] UploadMusicRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Vui lòng chọn tệp nhạc.");

            var fileName = Path.GetFileNameWithoutExtension(request.File.FileName)
                .Replace(" ", "").ToLower() + ".mp3";
            var gridFsId = await _musicService.UploadToMongoDBAsync(request.File, fileName);

            var musicFile = new MusicFile
            {
                NameSong = request.NameSong,
                FileName = fileName,
                FilePath = $"/api/music/{fileName}",
                UploadeAt = DateTime.Now,
                GridFSFileId = gridFsId
            };

            if (request.ImageFile != null)
            {
                var ext = Path.GetExtension(request.ImageFile.FileName).ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                {
                    var imageName = Path.GetFileNameWithoutExtension(request.ImageFile.FileName)
                        .Replace(" ", "").ToLower() + ext;
                    var imageBytes = await ReadBytesAsync(request.ImageFile);
                    var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                    musicFile.ImageGridFsId = imageId;
                    musicFile.ImageUrl = $"/api/images/{imageName}";
                }
            }

            await _musicService.CreateAsync(musicFile);

            if (request.LrcFile != null && request.LrcFile.FileName.EndsWith(".lrc"))
            {
                using var reader = new StreamReader(request.LrcFile.OpenReadStream());
                var content = await reader.ReadToEndAsync();
                var lyric = new Lyric
                {
                    MusicIds = MongoDB.Bson.ObjectId.Parse(musicFile.Id),
                    Content = content
                };
                await _lyricService.CreateAsync(lyric);
            }

            return Ok(new { success = true, message = "Đã thêm bài hát thành công!" });
        }


        /// <summary>
        /// Xem chi tiết bài hát (kèm lời nếu có)
        /// </summary>
        [HttpGet("song/{id}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetSong(string id)
        {
            var song = await _musicService.GetByAsync(id);
            if (song == null) return NotFound();

            var lyric = await _lyricService.GetByMusicIdAsync(song.Id);
            return Ok(new { song, lyric = lyric?.Content });
        }

        /// <summary>
        /// Cập nhật tên hoặc ảnh bài hát.
        /// </summary>
        [HttpPut("update/{id}")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> UpdateSong(string id, [FromForm] UpdateMusicRequest request)
        {
            var song = await _musicService.GetByAsync(id);
            if (song == null)
                return NotFound();

            song.NameSong = request.NameSong;

            if (request.ImageFile != null)
            {
                var ext = Path.GetExtension(request.ImageFile.FileName).ToLower();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                {
                    var imageName = Path.GetFileNameWithoutExtension(request.ImageFile.FileName)
                        .Replace(" ", "").ToLower() + ext;
                    var imageBytes = await ReadBytesAsync(request.ImageFile);
                    var imageId = await _musicService.UploadImageAsync(imageBytes, imageName);
                    song.ImageGridFsId = imageId;
                    song.ImageUrl = $"/api/images/{imageName}";
                }
            }

            await _musicService.UpdateAsync(song.Id, song);
            return Ok(new { success = true });
        }

        private async Task<byte[]> ReadBytesAsync(IFormFile file)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            return ms.ToArray();
        }
    }
}
