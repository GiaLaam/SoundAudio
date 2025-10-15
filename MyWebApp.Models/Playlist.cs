using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace MyWebApp.Models
{
    public class Playlist
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("OWnerId")]
        public string OwnerId { get; set; } = null!;

        [BsonElement("OwnerType")] // "user" hoặc "author"
        public string OwnerType { get; set; }

        [BsonElement("Name")]
        public string Name { get; set; } = null!;

        [BsonElement("MusicIds")]
        public List<string> MusicIds { get; set; } = new List<string>(); // Danh sách ID bài hát
    }
}