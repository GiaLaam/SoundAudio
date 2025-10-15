using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MyWebApp.Models;

namespace MyWebApp.Api.Controllers
{
    /// <summary>
    /// API lấy và upload hình ảnh được lưu trong MongoDB GridFS.
    /// </summary>
    [ApiController]
    [Route("api/images")]
    public class ImagesApiController : ControllerBase
    {
        private readonly GridFSBucket _gridFs;

        public ImagesApiController(IMongoDatabase mongoDatabase)
        {
            _gridFs = new GridFSBucket(mongoDatabase);
        }

        /// <summary>
        /// Lấy hình ảnh theo tên file (đã lưu trong MongoDB GridFS).
        /// </summary>
        /// <param name="fileName">Tên file ảnh (ví dụ: song.jpg)</param>
        [HttpGet("{fileName}")]
        [ProducesResponseType(typeof(FileStreamResult), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return BadRequest("Thiếu tên file ảnh.");

            try
            {
                var stream = await _gridFs.OpenDownloadStreamByNameAsync(fileName);

                var ext = Path.GetExtension(fileName).ToLower();
                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".jpeg" => "image/jpeg",
                    ".jpg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    _ => "application/octet-stream"
                };

                return File(stream, contentType);
            }
            catch (GridFSFileNotFoundException)
            {
                return NotFound(new { success = false, message = "Ảnh không tồn tại." });
            }
        }

        /// <summary>
        /// Upload ảnh mới lên MongoDB GridFS.
        /// </summary>
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> UploadImage([FromForm] UploadImageRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("Vui lòng chọn một ảnh để tải lên.");

            try
            {
                var fileName = Path.GetFileName(request.File.FileName).Replace(" ", "").ToLower();
                using var stream = request.File.OpenReadStream();
                await _gridFs.UploadFromStreamAsync(fileName, stream);

                return Ok(new
                {
                    success = true,
                    fileName,
                    url = $"/api/images/{fileName}"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi upload ảnh: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xóa ảnh trong MongoDB GridFS theo tên file.
        /// </summary>
        [HttpDelete("{fileName}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> DeleteImage(string fileName)
        {
            try
            {
                var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, fileName);
                using var cursor = await _gridFs.FindAsync(filter);
                var fileInfo = await cursor.FirstOrDefaultAsync();

                if (fileInfo == null)
                    return NotFound(new { success = false, message = "Ảnh không tồn tại." });

                await _gridFs.DeleteAsync(fileInfo.Id);

                return Ok(new { success = true, message = "Đã xóa ảnh thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Lỗi khi xóa ảnh: {ex.Message}" });
            }
        }
    }
}
