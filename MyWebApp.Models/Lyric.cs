using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MyWebApp.Models
{
    public class Lyric
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonRepresentation(BsonType.ObjectId)]
        [BsonElement("MusicIds")]
        public ObjectId MusicIds { get; set; }

        [BsonElement("Content")]
        public string Content { get; set; } = null!;
    }
}
