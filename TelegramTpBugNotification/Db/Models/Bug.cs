using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramTpBugNotification.Db.Models
{
    public class Bug
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }

        public int Id { get; set; }

        public string Name { get; set; }

        public BugState State { get; set; }

        public string Url { get; set; }

        public int? TelegramUserId { get; set; }

        public bool IsNotified { get; set; }

        public enum BugState
        {
            New = 0,
            InProgress = 1,
            Implemented = 2,
            Reviewed = 3,
            Verifying = 4,
            Done = 5
        }
    }
}
