using Microsoft.AspNetCore.Http;

namespace MyWebApp.Models
{
    public class UploadImageRequest
    {
        /// <summary>Ảnh cần upload (chấp nhận .jpg, .png, .webp, .gif)</summary>
        public IFormFile File { get; set; }
    }
}
