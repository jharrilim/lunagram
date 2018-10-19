using Mond;
using Mond.Libraries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;

namespace Lunagram
{
    public static class AppState
    {
        public static MondState         MondState { get; private set; }
        public static TelegramBotClient BotClient { get; private set; }

        private const int maxOutputChars = 2048;
        private static StringWriter output;
        private static StringBuilder outputBuffer;

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

            ResetMondOutput();

            MondState.Libraries.Configure(libs =>
            {
                var cout = libs.Get<ConsoleOutputLibrary>();
                cout.Out = output;
            });

            MondState.EnsureLibrariesLoaded();
        }

        private static void ResetMondOutput()
        {
            outputBuffer = new StringBuilder(maxOutputChars);
            output = new StringWriter(outputBuffer);
        }

        public static string ExecuteMond(string source)
        {
            ResetMondOutput();
            try
            {
                MondValue result = MondState.Run(source);

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
                Console.WriteLine(e);
                output.WriteLine(e.Message);
            }
            return outputBuffer.ToString();
        }
    }
}
