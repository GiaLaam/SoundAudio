using Microsoft.AspNetCore.Http;

namespace MyWebApp.Models
{
    public class UpdateMusicRequest
    {
        public string NameSong { get; set; } = string.Empty;
        public IFormFile? ImageFile { get; set; }
    }
}
