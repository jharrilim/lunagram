using Mond;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Lunagram
{
    public static class AppState
    {
        public static MondState         MondState { get; private set; }
        public static TelegramBotClient BotClient { get; private set; }

        public static void Configure(string botToken, string url)
        {
            MondState = new MondState()
            {
                Options =
                {
                    UseImplicitGlobals = true,
                    MakeRootDeclarationsGlobal = true
                }
            };
            BotClient = new TelegramBotClient(botToken);
            BotClient.SetWebhookAsync(url);
        }
    }
}
