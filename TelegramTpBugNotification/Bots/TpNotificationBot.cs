using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramTpBugNotification.Db.Models;
using TelegramTpBugNotification.Db.Mongo;
using TelegramTpBugNotification.Db.SQL;
using TelegramTpBugNotification.Scheduled;
using User = TelegramTpBugNotification.Db.Models.User;

namespace TelegramTpBugNotification.Bots
{
    public class TpNotificationBot
    {
        private const int TimeoutSeconds = 10;

        private readonly IConfigurationRoot _configuration;
        private readonly TelegramBotClient _botClient;

        private readonly SqlDbContext _sqlDbContext;
        private readonly MongoDbContext _mongoDbContext;

        public TpNotificationBot(IConfigurationRoot configuration, TelegramBotClient botClient)
        {
            _configuration = configuration;
            _botClient = botClient;

            _sqlDbContext = new SqlDbContext(configuration);
            _mongoDbContext = new MongoDbContext(configuration);

            SubscribeBotClientEvents();

            BugsChecker.Start(_botClient, _sqlDbContext, _mongoDbContext);
        }

        private void SubscribeBotClientEvents()
        {
            _botClient.OnMessage += BotClientOnOnMessage;
            _botClient.OnCallbackQuery += BotClientOnOnCallbackQuery;
        }

        private async void BotClientOnOnMessage(object sender, MessageEventArgs e)
        {
            var now = DateTime.UtcNow;
            if (e.Message.Text == null || (now - e.Message.Date).TotalSeconds > TimeoutSeconds)
            {
                return;
            }

            e.Message.Text = e.Message.Text.Trim();

            try
            {
                switch (e.Message.Text)
                {
                    case @"/start":
                        await SendStartMessage(e);

                        break;
                    case @"/all_bugs":
                        var userOpenBugs = await _mongoDbContext.GetUserOpenBugs(e.Message.From.Id);
                        await SendBugsMessage(e.Message.Chat, userOpenBugs);

                        break;
                    case @"/unsubscribe":
                        await _mongoDbContext.UnsubscribeUser(e.Message.From.Id);

                        break;
                    default:
                        await RegisterUser(e);

                        break;
                }
            }
            catch (Exception)
            {
                // ignored, for now
            }
        }

        private async void BotClientOnOnCallbackQuery(object sender, CallbackQueryEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.CallbackQuery.Data))
            {
                return;
            }

            try
            {
                var callbackCommandData = e.CallbackQuery.Data.Split("|");
                switch (callbackCommandData[0])
                {
                    case @"/notification_bugs":
                        if (callbackCommandData.Length < 2 || !Guid.TryParse(
                            callbackCommandData[1], out var notificationId))
                        {
                            return;
                        }

                        var notification = await _mongoDbContext.GetNotificationById(notificationId);
                        var notificationBugs = await _mongoDbContext.GetUserBugsByIds(
                            e.CallbackQuery.From.Id, notification.BugIds);

                        await SendBugsMessage(e.CallbackQuery.Message.Chat, notificationBugs);

                        break;
                }
            }
            catch (Exception)
            {
                // ignored, for now
            }

            await _botClient.AnswerCallbackQueryAsync(
                callbackQueryId: e.CallbackQuery.Id);
        }

        private async Task SendStartMessage(MessageEventArgs e)
        {
            await _botClient.SendTextMessageAsync(
                chatId: e.Message.Chat,
                text: "Скинь мені свій логін в TP для того, щоб почати трекати свої баги");
        }

        private async Task RegisterUser(MessageEventArgs e)
        {
            var tpUserExists = _sqlDbContext.CheckTpUserExists(e.Message.Text);
            if (tpUserExists)
            {
                await _mongoDbContext.InsertOrUpdateUser(new User
                {
                    ChatId = e.Message.Chat.Id,
                    TelegramUserId = e.Message.From.Id,
                    TpUserLogin = e.Message.Text
                });

                await _botClient.SendTextMessageAsync(
                    chatId: e.Message.Chat,
                    text: $"Вітаю, {e.Message.Text}, ти успішно підписався на сповіщення про нові баги, після синхронізації даних з TP, ти отримаєш сповіщення.");

                return;
            }

            await _botClient.SendTextMessageAsync(
                chatId: e.Message.Chat,
                text: $"Користувача {e.Message.Text} у TP не існує, спробуй ще раз");

            await SendStartMessage(e);
        }

        private async Task SendBugsMessage(Chat chatId, IList<Bug> bugs)
        {
            await _botClient.SendTextMessageAsync(
                chatId: chatId,
                text: $"🐞 Твої баги 🐞");

            foreach (var bug in bugs)
            {
                await _botClient.SendTextMessageAsync(
                    chatId: chatId,
                    text: $"#{bug.Id} <b>{bug.Name}</b>\nСтан: <b>{bug.State}</b>",
                    replyMarkup: new InlineKeyboardMarkup(
                        InlineKeyboardButton.WithUrl("Переглянути у TP", bug.Url)),
                    parseMode: ParseMode.Html);
            }
        }
    }
}
