using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Mond;
using Newtonsoft.Json;
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

                case "genie":
                    await Genie(message, remainingText);
                    break;

                case "randomfact":
                    await RandomFact(message);
                    break;

                case "chucknorrisfact":
                    await RandomChuckNorrisFact(message);
                    break;
                default:
                    return false;
            }

            return true;
        }

        private static async Task<Message> RandomChuckNorrisFact(Message message)
        {
            using(HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage resp = await client.GetAsync("https://api.chucknorris.io/jokes/random");
                    resp.EnsureSuccessStatusCode();
                    string content = await resp.Content.ReadAsStringAsync();
                    dynamic jsonContent = JsonConvert.DeserializeObject<dynamic>(content);
                    string resultEncoded = "<b>" + WebUtility.HtmlEncode(jsonContent.value) + "</b>";
                    return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
                }
                catch (Exception)
                {
                    return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "Request failed. Please try again later.", replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
                }
            }
        }

        private static async Task<Message> RandomFact(Message message)
        {
            const string path = "https://en.wikipedia.org/wiki/Special:Random";
            const string summaryXpath = "//*[@id=\"mw-content-text\"]/div/table/following-sibling::p[1]";
            HtmlWeb web = new HtmlWeb()
            {
                CaptureRedirect = true
            };
            HtmlDocument doc = await web.LoadFromWebAsync(path);
            HtmlNode summaryNode = doc.DocumentNode.SelectSingleNode(summaryXpath);
            var sb = new StringBuilder();
            foreach (var node in summaryNode.DescendantsAndSelf())
            {
                if (!node.HasChildNodes)
                {
                    string text = node.InnerText;
                    if (!string.IsNullOrEmpty(text))
                    {
                        sb.Append(text.Trim());
                        sb.Append(' ');
                    }
                }
            }

            string html = $"{sb.ToString()}";
            if(web?.ResponseUri?.AbsoluteUri != null)
            {
                html = $"<i>Taken from: {web.ResponseUri.AbsoluteUri}</i>" + html;
            }
            return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, html, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);

        }

        private static async Task<Message> Genie(Message message, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                string r = "<i>" + "You must write a question such as: /genie Does Kiki love me?" + "</i>";
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, r, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
                
            }
            string res = "";
            int rnd = Convert.ToInt32(Math.Ceiling(AppState.Rng.NextDouble() * 6));
            switch (rnd)
            {
                case 1: res = "Probably not.";
                    break;
                case 2: res = "Seems alright.";
                    break;
                case 3: res = "Absolutely!";
                    break;
                case 4: res = "That is an absolute no.";
                    break;
                case 5: res = "Seems a little risky, think carefully.";
                    break;
                case 6: res = "I'm 50/50 on that one.";
                    break;
                default: res = "I am unsure.";
                    break;
            }
            string resultEncoded = "<i>" + WebUtility.HtmlEncode(res) + "</i>";
            return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
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