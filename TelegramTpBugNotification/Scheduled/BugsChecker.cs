using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramTpBugNotification.Db.Models;
using TelegramTpBugNotification.Db.Mongo;
using TelegramTpBugNotification.Db.SQL;

namespace TelegramTpBugNotification.Scheduled
{
    public class BugsChecker
    {
        private const int UpdateIntervalMinutes = 1;

        public static void Start(TelegramBotClient botClient, SqlDbContext sqlDbContext, MongoDbContext mongoDbContext)
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var dbUsers = await mongoDbContext.GetUsers();
                        foreach (var dbUser in dbUsers)
                        {
                            var tpUserBugs = sqlDbContext.GetTpUserOpenBugs(dbUser);
                            await mongoDbContext.InsertOrUpdateBugs(tpUserBugs);
                            await mongoDbContext.UpdateExistingBugsExeptNew(tpUserBugs);

                            var notNotifiedBugs = await mongoDbContext.GetUserNotNotifiedBugs(dbUser.TelegramUserId);
                            if (!notNotifiedBugs.Any())
                            {
                                continue;
                            }

                            var notification = new Notification
                            {
                                Id = Guid.NewGuid(),
                                TelegramUserId = dbUser.TelegramUserId,
                                BugIds = notNotifiedBugs.Select(b => b.Id).ToArray()
                            };

                            await mongoDbContext.InsertNotification(notification);

                            await botClient.SendTextMessageAsync(
                                chatId: dbUser.ChatId,
                                text: $"🐞 У тебе з'явились нові баги ({notification.BugIds.Length}) 🐞",
                                replyMarkup: new InlineKeyboardMarkup(
                                    InlineKeyboardButton.WithCallbackData(
                                        "Переглянути", $"/notification_bugs|{notification.Id}")));
                        }
                    }
                    catch (Exception)
                    {
                        // ignored, for now
                    }

                    Thread.Sleep(UpdateIntervalMinutes * 60 * 1000);
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}
