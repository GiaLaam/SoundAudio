using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Bson;
using MyWebApp.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace MyWebApp.Services
{
    public class MusicService
    {
        private readonly IMongoCollection<MusicFile> _collection;
        private readonly IMongoCollection<Lyric> _lyricCollection;
        private readonly GridFSBucket _gridFs;

        public MusicService(IMongoDatabase database)
        {
            _collection = database.GetCollection<MusicFile>("Songs");
            _lyricCollection = database.GetCollection<Lyric>("Lyrics");
            _gridFs = new GridFSBucket(database, new GridFSBucketOptions
            {
                BucketName = "fs" // rõ ràng
            });
        }

        // -----------------------------
        // 📦 CRUD metadata (Songs)
        // -----------------------------
        public async Task<List<MusicFile>> GetAllAsync() =>
            await _collection.Find(_ => true).ToListAsync();

        public async Task<MusicFile?> GetByFileNameAsync(string fileName) =>
            await _collection.Find(m => m.FileName == fileName).FirstOrDefaultAsync();

        public async Task<MusicFile?> GetByAsync(string id) =>
            await _collection.Find(m => m.Id == id).FirstOrDefaultAsync();

        public async Task CreateAsync(MusicFile musicFile) =>
            await _collection.InsertOneAsync(musicFile);

        public async Task UpdateAsync(string id, MusicFile updated) =>
            await _collection.ReplaceOneAsync(m => m.Id == id, updated);

        public async Task<bool> DeleteAsync(string id)
        {
            var song = await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
            if (song == null) return false;

            try
            {
                if (song.GridFSFileId != ObjectId.Empty)
                    await _gridFs.DeleteAsync(song.GridFSFileId);
            }
            catch (GridFSFileNotFoundException)
            {
                Console.WriteLine($"[MusicService] File GridFS không tồn tại khi xóa: {song.GridFSFileId}");
            }

            var result = await _collection.DeleteOneAsync(x => x.Id == id);
            return result.DeletedCount > 0;
        }

        // -----------------------------
        // 🎵 File operations
        // -----------------------------
        private async Task<GridFSFileInfo?> FindFileByNameAsync(string fileName)
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, fileName);
            var cursor = await _gridFs.FindAsync(filter);
            return await cursor.FirstOrDefaultAsync();
        }

        public async Task<Stream?> DownloadFileAsync(string fileName)
        {
            var fileInfo = await FindFileByNameAsync(fileName);
            if (fileInfo == null) return null;
            return await _gridFs.OpenDownloadStreamAsync(fileInfo.Id);
        }

        public async Task<byte[]?> DownloadFileBytesAsync(string fileName)
        {
            var fileInfo = await FindFileByNameAsync(fileName);
            if (fileInfo == null) return null;

            using var stream = await _gridFs.OpenDownloadStreamAsync(fileInfo.Id);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        public async Task<ObjectId> UploadToMongoDBAsync(IFormFile file, string fileName)
        {
            using var stream = file.OpenReadStream();
            return await _gridFs.UploadFromStreamAsync(fileName, stream);
        }

        public async Task<ObjectId> UploadImageAsync(byte[] imageBytes, string fileName)
        {
            return await _gridFs.UploadFromBytesAsync(fileName, imageBytes);
        }

        public async Task<byte[]?> DownloadImageAsync(string imageFileName)
        {
            var fileInfo = await FindFileByNameAsync(imageFileName);
            if (fileInfo == null) return null;

            using var stream = await _gridFs.OpenDownloadStreamAsync(fileInfo.Id);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }

        // Tìm bài hát theo FilePath (ví dụ "/api/music/khongthesay.mp3")
        public async Task<MusicFile?> GetByFilePathAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            try
            {
                return await _collection.Find(x => x.FilePath == filePath).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicService] Lỗi khi tìm theo FilePath '{filePath}': {ex.Message}");
                return null;
            }
        }

        public async Task<Stream?> GetMusicFileAsync(string gridFsId)
        {
            try
            {
                if (!ObjectId.TryParse(gridFsId, out var objectId))
                {
                    Console.WriteLine($"[MusicService] ❌ ID không hợp lệ: {gridFsId}");
                    return null;
                }

                Console.WriteLine($"[MusicService] 🔍 Đang tìm file GridFS với ID: {objectId}");

                var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Id, objectId);
                var fileInfo = await (await _gridFs.FindAsync(filter)).FirstOrDefaultAsync();

                if (fileInfo == null)
                {
                    Console.WriteLine($"[MusicService] ❌ Không tìm thấy file trong GridFS với ID: {objectId}");
                    return null;
                }

                Console.WriteLine($"[MusicService] ✅ Đã tìm thấy file: {fileInfo.Filename}");
                return await _gridFs.OpenDownloadStreamAsync(objectId);
            }
            catch (GridFSFileNotFoundException)
            {
                Console.WriteLine($"[MusicService] ⚠️ Không có stream cho GridFS ID: {gridFsId}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicService] 💥 Lỗi khi mở file GridFS {gridFsId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Đồng bộ / kiểm tra sự nhất quán giữa collection Songs và GridFS (fs.files).
        /// In ra thông tin về:
        ///  - songsMissingFile: các document trong Songs mà GridFSFileId không tồn tại trong fs.files
        ///  - filesWithoutSong: các file trong fs.files không được tham chiếu bởi bất kỳ document Songs nào
        /// (Không tự động xóa; chỉ log/report để bạn kiểm tra và xử lý thủ công).
        /// </summary>
        public async Task SyncMusicFilesWithGridFS()
        {
            try
            {
                Console.WriteLine("[MusicService] Bắt đầu kiểm tra đồng bộ Songs <-> GridFS...");

                // 1) Lấy tất cả songs
                var songs = await _collection.Find(_ => true).ToListAsync();

                // 2) Lấy tất cả file info từ GridFS (fs.files)
                var gridFsCursor = await _gridFs.FindAsync(FilterDefinition<GridFSFileInfo>.Empty);
                var fsFiles = await gridFsCursor.ToListAsync();

                // 3) Tạo set ID của fs files
                var fsIds = new HashSet<string>(fsFiles.Select(f => f.Id.ToString()));

                // 4) Kiểm tra songs có GridFSFileId tồn tại trong fs.files không
                var songsMissingFile = new List<MusicFile>();
                foreach (var s in songs)
                {
                    if (s.GridFSFileId == ObjectId.Empty)
                    {
                        songsMissingFile.Add(s);
                        continue;
                    }

                    if (!fsIds.Contains(s.GridFSFileId.ToString()))
                    {
                        songsMissingFile.Add(s);
                    }
                }

                // 5) Kiểm tra file trong fs.files có được tham chiếu trong Songs không
                var songGridFsIds = new HashSet<string>(
                    songs.Where(s => s.GridFSFileId != ObjectId.Empty)
                        .Select(s => s.GridFSFileId.ToString())
                );

                var filesWithoutSong = fsFiles.Where(f => !songGridFsIds.Contains(f.Id.ToString())).ToList();

                // 6) Log kết quả
                Console.WriteLine($"[MusicService] Tổng Songs: {songs.Count}");
                Console.WriteLine($"[MusicService] Tổng GridFS files: {fsFiles.Count}");
                Console.WriteLine($"[MusicService] Songs có GridFSFileId bị thiếu/không tồn tại: {songsMissingFile.Count}");
                foreach (var s in songsMissingFile.Take(50))
                {
                    Console.WriteLine($"  - SongId: {s.Id} | Name: {s.NameSong} | GridFSFileId: {s.GridFSFileId}");
                }
                if (songsMissingFile.Count > 50) Console.WriteLine("  ... (còn nữa)");

                Console.WriteLine($"[MusicService] Files trong fs.files không có document Songs tham chiếu: {filesWithoutSong.Count}");
                foreach (var f in filesWithoutSong.Take(50))
                {
                    Console.WriteLine($"  - FileId: {f.Id} | Filename: {f.Filename} | length: {f.Length}");
                }
                if (filesWithoutSong.Count > 50) Console.WriteLine("  ... (còn nữa)");

                Console.WriteLine("[MusicService] Kiểm tra hoàn tất. Gợi ý: nếu bạn muốn xóa files không dùng tới, cân nhắc backup trước.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MusicService] Lỗi khi sync Songs với GridFS: {ex.Message}");
            }
        }
    }
}
