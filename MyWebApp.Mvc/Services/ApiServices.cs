using MyWebApp.Models;

namespace MyWebApp.Mvc.Services
{
    public class MusicApiService
    {
        private readonly ApiService _apiService;

        public MusicApiService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<List<MusicFile>> GetAllAsync()
        {
            var response = await _apiService.GetAsync<List<MusicFile>>("/api/music");
            return response ?? new List<MusicFile>();
        }

        public async Task<MusicFile?> GetByIdAsync(string id)
        {
            var response = await _apiService.GetAsync<MusicFile>($"/api/music/byid/{id}");
            return response;
        }

        public async Task<Stream?> GetMusicStreamAsync(string gridFsId)
        {
            // This needs special handling for streaming
            return null; // Implement stream handling if needed
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _apiService.DeleteAsync($"/api/music/{id}");
        }

        public async Task<MusicFile?> UploadAsync(string nameSong, Stream musicStream, string musicFileName, Stream? imageStream, string? imageFileName)
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(nameSong), "NameSong");
            content.Add(new StreamContent(musicStream), "File", musicFileName);
            
            if (imageStream != null && !string.IsNullOrEmpty(imageFileName))
            {
                content.Add(new StreamContent(imageStream), "ImageFile", imageFileName);
            }

            return await _apiService.PostMultipartAsync<MusicFile>("/api/music/upload", content);
        }
    }

    public class AlbumApiService
    {
        private readonly ApiService _apiService;

        public AlbumApiService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<List<Album>> GetAllAsync()
        {
            var response = await _apiService.GetAsync<List<Album>>("/api/AlbumApi");
            return response ?? new List<Album>();
        }

        public async Task<Album?> GetByIdAsync(string id)
        {
            var response = await _apiService.GetAsync<Album>($"/api/AlbumApi/{id}");
            return response;
        }

        public async Task<Album?> CreateAsync(string name, string? imageUrl = null)
        {
            var request = new { Name = name, ImageUrl = imageUrl };
            var response = await _apiService.PostAsync<object, Album>("/api/AlbumApi", request);
            return response;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _apiService.DeleteAsync($"/api/AlbumApi/{id}");
        }
    }

    public class PlaylistApiService
    {
        private readonly ApiService _apiService;

        public PlaylistApiService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<List<Playlist>> GetAllAsync()
        {
            var response = await _apiService.GetAsync<List<Playlist>>("/api/playlist/all");
            return response ?? new List<Playlist>();
        }

        public async Task<List<Playlist>> GetByOwnerAsync(string ownerId, string ownerType)
        {
            // This now calls the same endpoint as GetAllAsync since API uses current user from token
            var response = await _apiService.GetAsync<List<Playlist>>("/api/playlist/all");
            return response ?? new List<Playlist>();
        }

        public async Task<Playlist?> GetByIdAsync(string id)
        {
            var response = await _apiService.GetAsync<Playlist>($"/api/playlist/{id}");
            return response;
        }

        public async Task<Playlist?> CreateAsync(Playlist playlist)
        {
            var response = await _apiService.PostAsync<Playlist, Playlist>("/api/playlist/create", playlist);
            return response;
        }

        public async Task<Playlist?> UpdateAsync(Playlist playlist)
        {
            var response = await _apiService.PutAsync<Playlist, Playlist>($"/api/playlist/{playlist.Id}", playlist);
            return response;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _apiService.DeleteAsync($"/api/playlist/{id}");
        }
    }

    public class AuthorApiService
    {
        private readonly ApiService _apiService;

        public AuthorApiService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<List<Author>> GetAllAsync()
        {
            var response = await _apiService.GetAsync<List<Author>>("/api/author");
            return response ?? new List<Author>();
        }

        public async Task<Author?> GetByIdAsync(string id)
        {
            var response = await _apiService.GetAsync<Author>($"/api/author/{id}");
            return response;
        }

        public async Task<Author?> CreateAsync(Author author)
        {
            var response = await _apiService.PostAsync<Author, Author>("/api/author", author);
            return response;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            return await _apiService.DeleteAsync($"/api/author/{id}");
        }
    }
}
