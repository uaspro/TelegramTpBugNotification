using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TelegramTpBugNotification.Db.Models
{
    public class Notification
    {
        [BsonId]
        public ObjectId ObjectId { get; set; }

        public Guid Id { get; set; }

        public int TelegramUserId { get; set; }

        public int[] BugIds { get; set; }

        public bool IsSent { get; set; }
    }
}
