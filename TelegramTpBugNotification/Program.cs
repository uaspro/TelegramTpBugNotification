using System.Threading;
using Microsoft.Extensions.Configuration;
using TelegramTpBugNotification.Managers;

namespace TelegramTpBugNotification
{
    public class Program
    {
        static void Main(string[] args)
        {
            var configurationBuilder = new ConfigurationBuilder()
               .AddEnvironmentVariables();

            var configuration = configurationBuilder.Build();
            new TelegramBotManager(configuration).StartBot();

            Thread.Sleep(int.MaxValue);
        }
    }
}
