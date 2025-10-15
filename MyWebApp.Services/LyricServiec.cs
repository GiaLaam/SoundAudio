using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Bson;
using MyWebApp.Models;

namespace MyWebApp.Services
{
    public class LyricService
    {
        private readonly IMongoCollection<Lyric> _lyrics;
        private readonly GridFSBucket _gridFS;

        public LyricService(IMongoDatabase database)
        {
            _lyrics = database.GetCollection<Lyric>("Lyrics");
            _gridFS = new GridFSBucket(database);
        }

        public async Task<Lyric?> GetByMusicIdAsync(string musicId)
        {
            if (!ObjectId.TryParse(musicId, out var objectId)) return null;

            var filter = Builders<Lyric>.Filter.Eq(l => l.MusicIds, objectId);
            return await _lyrics.Find(filter).FirstOrDefaultAsync();
        }



        public async Task<List<Lyric>> GetAllAsync()
        {
            return await _lyrics.Find(_ => true).ToListAsync();
        }

        public async Task<Lyric?> GetByAsync(string id)
        {
            return await _lyrics.Find(lyric => lyric.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(Lyric lyric)
        {
            await _lyrics.InsertOneAsync(lyric);
        }

        public async Task UpdateAsync(string id, Lyric lyric)
        {
            await _lyrics.ReplaceOneAsync(l => l.Id == id, lyric);
        }

        public async Task DeleteAsync(string id)
        {
            await _lyrics.DeleteOneAsync(l => l.Id == id);
        }
    }
}