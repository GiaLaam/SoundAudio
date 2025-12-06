using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MyWebApp.Models
{
    public class MusicFile
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("NameSong")]
        public string NameSong { get; set; } = null!;

        [BsonElement("FileName")]
        public string FileName { get; set; } = null!;

        [BsonElement("FilePath")]
        public string FilePath { get; set; } = null!;

        [BsonElement("Duration")]
        public string? Duration { get; set; }

        [BsonElement("AuthorId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AuthorId { get; set; }

        [BsonElement("AlbumId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? AlbumId { get; set; }

        [BsonElement("UploadeAt")]
        public DateTime UploadeAt { get; set; }

        [BsonElement("ImageUrl")]
        public string? ImageUrl { get; set; }

        [BsonElement("ImageGridFsId")]
        public ObjectId? ImageGridFsId { get; set; }

        public ObjectId GridFSFileId { get; set; }
    }
}