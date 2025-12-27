using Microsoft.AspNetCore.Http;

namespace MyWebApp.Models
{
    public class UploadMusicRequest
    {
        public string NameSong { get; set; } = string.Empty;

        public IFormFile File { get; set; } = default!;

        public IFormFile? ImageFile { get; set; }

        public IFormFile? LrcFile { get; set; }

        public string? AlbumId { get; set; }
    }
}
