namespace MyWebApp.Api.Models
{
    public class UploadAlbumImageRequest
    {
        public IFormFile Image { get; set; } = null!;
    }

    public class UpdateAlbumWithImageRequest
    {
        public string? Name { get; set; }
        public IFormFile? Image { get; set; }
    }

    public class CreateAlbumWithImageRequest
    {
        public string Name { get; set; } = string.Empty;
        public IFormFile? Image { get; set; }
    }
}
