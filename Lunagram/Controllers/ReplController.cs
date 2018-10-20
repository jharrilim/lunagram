using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mond;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Lunagram.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReplController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Post(Update update)
        {

            if (update.Type == UpdateType.Message)
            {
                await OnMessageHandler(update.Message);
            }
            return Ok();
        }
        private static async Task<bool> OnMessageHandler(Message message)
        {
            Console.WriteLine("Received message from: {0}, {1}", message.Chat.Id, message.Chat.FirstName + " " + message.Chat.LastName);

            if (message.Type != MessageType.Text)
                return false;

            MessageEntity commandEntity = message?.Entities?.FirstOrDefault(e => e.Type == MessageEntityType.BotCommand);
            if (commandEntity == null)
                return false;

            string text = message.Text;
            string commandText = CleanupCommand(text.Substring(commandEntity.Offset, commandEntity.Length));
            string remainingText = text.Substring(commandEntity.Offset + commandEntity.Length);

            switch (commandText)
            {
                case "help":
                case "f1":
                    await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "NO HELP");
                    break;

                case "eval":
                    await RunMondScript(message, remainingText);
                    break;

                //case "method":
                //    await AddMondMethod(message, remainingText);
                //    break;

                //case "view":
                //    await ViewMondVariable(message, remainingText);
                //    break;

                default:
                    return false;
            }

            return true;
        }

        private static async Task RunMondScript(Message message, string text)
        {
            try
            {
                string result = "Timed out.";
                await Task.WhenAny
                (
                    new Task(() => { result = AppState.ExecuteMond(text); }),
                    Task.Delay(TimeSpan.FromSeconds(10))
                );
                result = AppState.ExecuteMond(text);
                if (string.IsNullOrWhiteSpace(result))
                {
                    return;
                }
                else
                {
                    string resultEncoded = "<pre>" + WebUtility.HtmlEncode(result) + "</pre>";
                    await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);

                }
            }
            catch (Exception e)
            {
                await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, e.Message);
            }            
        }

        private static string CleanupCommand(string command)
        {
            Regex CommandRegex = new Regex(@"[/]+([a-z]+)");
            return CommandRegex.Match(command).Groups[1].Value.ToLower();
        }
    }
}