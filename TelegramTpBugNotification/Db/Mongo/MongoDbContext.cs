using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using TelegramTpBugNotification.Db.Models;

namespace TelegramTpBugNotification.Db.Mongo
{
    public class MongoDbContext
    {
        private const string MongoDbConnectingStringConfigKey = "TelegramBotMongoDbConnectionString";

        private const string MongoDbName = nameof(TelegramTpBugNotification);

        private const string UsersCollectionName = MongoDbName + "_users";
        private const string BugsCollectionName = MongoDbName + "_bugs";
        private const string NotificationsCollectionName = MongoDbName + "_notifications";

        private readonly MongoClient _mongoClient;

        private IMongoDatabase Database => _mongoClient.GetDatabase(MongoDbName);

        private IMongoCollection<Bug> BugsCollection => Database.GetCollection<Bug>(BugsCollectionName);
        private IMongoCollection<User> UsersCollection => Database.GetCollection<User>(UsersCollectionName);
        private IMongoCollection<Notification> NotificationsCollection => Database.GetCollection<Notification>(NotificationsCollectionName);

        public MongoDbContext(IConfigurationRoot configuration)
        {
            _mongoClient = new MongoClient(configuration.GetConnectionString(MongoDbConnectingStringConfigKey));
        }

        public async Task InsertOrUpdateUser(User user)
        {
            var dbUser = await UsersCollection
                              .Find(Builders<User>.Filter.Eq(nameof(User.TelegramUserId), user.TelegramUserId))
                              .FirstOrDefaultAsync();

            if (dbUser == null)
            {
                await UsersCollection.InsertOneAsync(user);
            }
            else
            {
                await UsersCollection.UpdateOneAsync(
                    Builders<User>.Filter.Eq(nameof(User.TelegramUserId), user.TelegramUserId),
                    Builders<User>.Update.Set(nameof(User.TpUserLogin), user.TpUserLogin));
            }
        }

        public async Task<IList<User>> GetUsers()
        {
            return await UsersCollection.Find(Builders<User>.Filter.Empty).ToListAsync();
        }

        public async Task InsertOrUpdateBugs(IList<Bug> bugs)
        {
            foreach (var bug in bugs)
            {
                var dbBug = await BugsCollection.Find(Builders<Bug>.Filter.Eq(nameof(Bug.Id), bug.Id))
                                                .FirstOrDefaultAsync();

                if (dbBug == null)
                {
                    await BugsCollection.InsertOneAsync(bug);
                }
                else
                {
                    bug.ObjectId = dbBug.ObjectId;
                    bug.IsNotified =
                        dbBug.State != bug.State && dbBug.State > Bug.BugState.InProgress &&
                        bug.State <= Bug.BugState.InProgress || dbBug.IsNotified;

                    await BugsCollection.ReplaceOneAsync(Builders<Bug>.Filter.Eq(nameof(Bug.Id), bug.Id), bug);
                }
            }
        }

        public async Task UpdateExistingBugsExeptNew(IList<Bug> newBugs)
        {
            await BugsCollection.UpdateManyAsync(
                Builders<Bug>.Filter.Nin(nameof(Bug.Id), newBugs.Select(b => b.Id)),
                Builders<Bug>.Update.Set(nameof(Bug.State), Bug.BugState.Done)
                                    .Set(nameof(Bug.IsNotified), true));
        }

        public async Task<IList<Bug>> GetUserOpenBugs(int telegramUserId)
        {
            return await BugsCollection.Find(
                                            Builders<Bug>.Filter.And(
                                                Builders<Bug>.Filter.Eq(
                                                    nameof(Bug.TelegramUserId),
                                                    telegramUserId),
                                                Builders<Bug>.Filter.Lte(
                                                    nameof(Bug.State), Bug.BugState.Done)))
                                       .Sort(Builders<Bug>.Sort.Ascending(nameof(Bug.State)))
                                       .ToListAsync();
        }

        public async Task<IList<Bug>> GetUserBugsByIds(int telegramUserId, int[] bugIds)
        {
            return await BugsCollection.Find(
                                            Builders<Bug>.Filter.And(
                                                Builders<Bug>.Filter.Eq(
                                                    nameof(Bug.TelegramUserId),
                                                    telegramUserId),
                                                Builders<Bug>.Filter.In(nameof(Bug.Id), bugIds)))
                                       .Sort(Builders<Bug>.Sort.Ascending(nameof(Bug.State)))
                                       .ToListAsync();
        }

        public async Task<IList<Bug>> GetUserNotNotifiedBugs(int telegramUserId)
        {
            return await BugsCollection.Find(
                                            Builders<Bug>.Filter.And(
                                                Builders<Bug>.Filter.Eq(nameof(Bug.TelegramUserId), telegramUserId),
                                                Builders<Bug>.Filter.Lte(
                                                    nameof(Bug.State),
                                                    Bug.BugState.InProgress),
                                                Builders<Bug>.Filter.Eq(nameof(Bug.IsNotified), false)))
                                       .Sort(Builders<Bug>.Sort.Ascending(nameof(Bug.State)))
                                       .ToListAsync();
        }

        public async Task InsertNotification(Notification notification)
        {
            await BugsCollection.UpdateManyAsync(
                Builders<Bug>.Filter.In(nameof(Bug.Id), notification.BugIds),
                Builders<Bug>.Update.Set(nameof(Bug.IsNotified), true));

            await NotificationsCollection.InsertOneAsync(notification);
        }

        public async Task<Notification> GetNotificationById(Guid notificationId)
        {
            return await NotificationsCollection
                        .Find(Builders<Notification>.Filter.Eq(nameof(Notification.IsSent), false))
                        .FirstOrDefaultAsync();
        }

        public async Task GetNotSentNotifications()
        {
            await NotificationsCollection.Find(Builders<Notification>.Filter.Eq(nameof(Notification.IsSent), false))
                                         .ToListAsync();
        }

        public async Task UpdateNotificationIsSent(Notification notification, bool isSent)
        {
            await NotificationsCollection.UpdateOneAsync(
                Builders<Notification>.Filter.Eq(nameof(Notification.Id), notification.Id),
                Builders<Notification>.Update.Set(nameof(Notification.IsSent), isSent));
        }
    }
}
