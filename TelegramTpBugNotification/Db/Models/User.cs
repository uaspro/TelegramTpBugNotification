using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramTpBugNotification.Db.Models
{
    public class User
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }

        public long ChatId { get; set; }

        public int TelegramUserId { get; set; }

        public string TpUserLogin { get; set; }
    }
}
