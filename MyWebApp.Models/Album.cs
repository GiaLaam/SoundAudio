using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MyWebApp.Models
{
    public class Album
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("Name")]
        public string Name { get; set; } = null!;

        [BsonElement("ImageUrl")]
        public string? ImageUrl { get; set; }

        [BsonElement("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
