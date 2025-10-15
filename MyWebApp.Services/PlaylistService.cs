using MongoDB.Driver;
using MyWebApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyWebApp.Services
{
    public class PlaylistService
    {
        private readonly IMongoCollection<Playlist> _playlistsCollection;

        public PlaylistService(IMongoDatabase database)
        {
            _playlistsCollection = database.GetCollection<Playlist>("Playlists");
        }

        // ✅ Lấy tất cả playlist theo OwnerId và OwnerType (user hoặc author)
        public async Task<List<Playlist>> GetByOwnerAsync(string ownerId, string ownerType)
        {
            if (string.IsNullOrEmpty(ownerId) || string.IsNullOrEmpty(ownerType))
                return new List<Playlist>();

            return await _playlistsCollection
                .Find(p => p.OwnerId == ownerId && p.OwnerType == ownerType)
                .ToListAsync();
        }

        // ✅ Lấy playlist theo ID
        public async Task<Playlist?> GetByIdAsync(string id)
        {
            return await _playlistsCollection
                .Find(p => p.Id == id)
                .FirstOrDefaultAsync();
        }

        // ✅ Tạo playlist mới
        public async Task CreateAsync(Playlist playlist)
        {
            await _playlistsCollection.InsertOneAsync(playlist);
        }

        // ✅ Cập nhật toàn bộ playlist
        public async Task UpdateAsync(Playlist playlist)
        {
            await _playlistsCollection.ReplaceOneAsync(p => p.Id == playlist.Id, playlist);
        }

        // ✅ Xoá playlist theo ID
        public async Task DeleteAsync(string id)
        {
            var filter = Builders<Playlist>.Filter.Eq(p => p.Id, id);
            await _playlistsCollection.DeleteOneAsync(filter);
        }

        // ✅ Cập nhật tên playlist
        public async Task<bool> UpdateNameAsync(string id, string newName)
        {
            try
            {
                var filter = Builders<Playlist>.Filter.Eq(p => p.Id, id);
                var update = Builders<Playlist>.Update.Set(p => p.Name, newName);
                var result = await _playlistsCollection.UpdateOneAsync(filter, update);

                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Lỗi trong UpdateNameAsync: " + ex.Message);
                return false;
            }
        }

        // ✅ Xoá bài hát khỏi playlist
        public async Task<bool> RemoveSongFromPlaylistAsync(string playlistId, string songId)
        {
            var update = Builders<Playlist>.Update.Pull(p => p.MusicIds, songId);
            var result = await _playlistsCollection.UpdateOneAsync(p => p.Id == playlistId, update);
            return result.ModifiedCount > 0;
        }
    }
}
