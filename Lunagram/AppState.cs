using Mond;
using Mond.Libraries;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Telegram.Bot;

namespace Lunagram
{
    public static class AppState
    {
        private const int maxOutputChars = 2048;
        private static StringWriter output;
        private static StringBuilder outputBuffer;

        public static MondState         MondState { get; private set; }
        public static TelegramBotClient BotClient { get; private set; }
        public static Random            Rng       { get; private set; }

        static AppState() => Rng = new Random();

        public static void Configure(string botToken, string url)
        {
            ConfigureMond();
            ConfigureTelegramBot(botToken, url);
        }

        private static void ConfigureTelegramBot(string token, string url)
        {
            BotClient = new TelegramBotClient(token);
            BotClient.SetWebhookAsync(url);
        }


        private static void ConfigureMond()
        {
            MondState = new MondState()
            {
                Options = new MondCompilerOptions
                {
                    DebugInfo = MondDebugInfoLevel.StackTrace,
                    UseImplicitGlobals = true,
                    MakeRootDeclarationsGlobal = true
                },
                Libraries =
                {
                    new ConsoleOutputLibraries()
                }
            };
            
            outputBuffer = new StringBuilder(maxOutputChars);
            output = new StringWriter(outputBuffer);
            MondState.Libraries.Configure(libs =>
            {
                var cout = libs.Get<ConsoleOutputLibrary>();
                cout.Out = output;
            });

            MondState.EnsureLibrariesLoaded();
        }

        public static string ExecuteMond(string source)
        {
            outputBuffer.Clear();
            try
            {
                MondValue result = MondState.Run(source, "Halbmondbox");

                if (result != MondValue.Undefined)
                {
                    Console.WriteLine("Result: ");
                    Console.WriteLine(result.Serialize());
                    output.WriteLine();

                    if (result["moveNext"])
                    {
                        output.WriteLine("sequence (15 max):");
                        foreach (var i in result.Enumerate(MondState).Take(15))
                        {
                            output.WriteLine(i.Serialize());
                        }
                    }
                    else
                    {
                        output.WriteLine(result.Serialize());
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                output.WriteLine(e.Message);
            }
            return outputBuffer.ToString();
        }
    }
}
