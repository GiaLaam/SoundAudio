using MongoDB.Driver;
using MyWebApp.Models;

namespace MyWebApp.Services
{
    public class AlbumService
    {
        private readonly IMongoCollection<Album> _collection;

        public AlbumService(IMongoDatabase db)
        {
            _collection = db.GetCollection<Album>("Albums");
        }

        public async Task<List<Album>> GetAllAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        public async Task<Album?> GetByIdAsync(string id)
        {
            return await _collection.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(Album album)
        {
            await _collection.InsertOneAsync(album);
        }

        public async Task UpdateAsync(string id, Album album)
        {
            await _collection.ReplaceOneAsync(a => a.Id == id, album);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var result = await _collection.DeleteOneAsync(a => a.Id == id);
            return result.DeletedCount > 0;
        }
    }
}
