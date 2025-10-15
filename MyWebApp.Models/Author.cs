using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MyWebApp.Models
{
    public class Author
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        [BsonElement("name")]
        public string Name { get; set; }

        [BsonElement("bio")]
        public string Bio { get; set; }

        [BsonElement("photoUrl")]
        public string PhotoUrl { get; set; }

        [BsonElement("PlaylistIds")]
        public List<string> PlaylistIds { get; set; } = new List<string>();
    }
}