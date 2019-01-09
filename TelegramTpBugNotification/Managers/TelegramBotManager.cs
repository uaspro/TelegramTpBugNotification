using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using TelegramTpBugNotification.Bots;

namespace TelegramTpBugNotification.Managers
{
    public class TelegramBotManager
    {
        private const string BotTokenConfigKey = "AppSettings:TelegramBotToken";

        private readonly IConfigurationRoot _configuration;
        private readonly TelegramBotClient _botClient;
        private readonly TpNotificationBot _bot;

        public TelegramBotManager(IConfigurationRoot configuration)
        {
            _configuration = configuration;
            _botClient = new TelegramBotClient(configuration[BotTokenConfigKey]);

            _bot = new TpNotificationBot(configuration, _botClient);
        }

        public void StartBot()
        {
            _botClient.StartReceiving();
        }
    }
}
