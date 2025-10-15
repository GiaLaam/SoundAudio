using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MyWebApp.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = null!;

        [BsonElement("Name")]
        public string Name { get; set; } = null!;

        [BsonElement("Email")]
        public string Email { get; set; } = null!;

        [BsonElement("Password")]
        public string Password { get; set; } = null!;
    }
}