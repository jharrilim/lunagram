using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Lunagram.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReplController : ControllerBase
    {
        private static List<string> rumiQuotes = new List<string>();

        [HttpPost]
        public async Task<IActionResult> Post(Update update)
        {

            if (update.Type == UpdateType.Message)
            {
                await OnMessageHandler(update.Message);
            }
            return Ok();
        }
        private static async Task<Message> OnMessageHandler(Message message)
        {
            Console.WriteLine("Received message from: {0}, {1}", message.Chat.Id, message.Chat.FirstName + " " + message.Chat.LastName);

            if (message.Type != MessageType.Text)
                return null;

            MessageEntity commandEntity = message?.Entities?.FirstOrDefault(e => e.Type == MessageEntityType.BotCommand);
            if (commandEntity == null)
                return null;

            string text = message.Text;
            string commandText = CleanupCommand(text.Substring(commandEntity.Offset, commandEntity.Length));
            string remainingText = text.Substring(commandEntity.Offset + commandEntity.Length);

            try
            {
                switch (commandText)
                {
                    case "help":
                    case "f1":
                        return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "NO HELP");
                    case "whats":
                        return await UrbanDictionaryTopResult(message, remainingText);
                    case "eval":
                        return await RunMondScript(message, remainingText);

                    case "genie":
                        return await Genie(message, remainingText);

                    case "randomfact":
                        return await RandomFact(message);

                    case "chucknorrisfact":
                        return await RandomChuckNorrisFact(message);

                    case "dadjoke":
                        return await RandomDadJoke(message);

                    case "rumiquote":
                        return await RandomRumiQuote(message);

                    case "inspiration":
                        return await InspirationalQuote(message);

                    case "js":
                        return await RunJavascript(message, remainingText);
                    default:
                        return null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "Oops! Something went wrong.");
            }
        }

        private static async Task<Message> UrbanDictionaryTopResult(Message message, string text)
        {
            using(HttpClient client = new HttpClient())
            {
                // url encode the search term
                string searchTerm = WebUtility.UrlEncode(text);

                HttpResponseMessage resp = await client.GetAsync(
                    "https://api.urbandictionary.com/v0/define?term=" + searchTerm
                );
                resp.EnsureSuccessStatusCode();
                string content = await resp.Content.ReadAsStringAsync();
                JObject jsonContent = JsonConvert.DeserializeObject<JObject>(content);
                JArray definitions = (JArray)jsonContent["list"];
                if (definitions.Count == 0)
                {
                    return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "No results found.");
                }
                JObject definition = (JObject)definitions[0];
                string word = (string)definition["word"];
                string definitionText = (string)definition["definition"];
                string exampleText = (string)definition["example"];
                string thumbsUp = (string)definition["thumbs_up"];
                string thumbsDown = (string)definition["thumbs_down"];
                string writtenOn = (string)definition["written_on"];
                DateTime writtenOnDate = DateTime.Parse(writtenOn);
                string year = writtenOnDate.Year.ToString();
                string exampleTextFormatted = exampleText.Length > 0 ? $"\n\n*Example:*\n{exampleText}" : "";

                string result = $"*{word}* ({year})\n\n{definitionText}{exampleTextFormatted}\n\n`{thumbsUp}` 👍   `{thumbsDown}` 👎";
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, result, ParseMode.Markdown);
            }
        }

        private static async Task<Message> RunJavascript(Message message, string source)
        {
            string result = Javascript.JavascriptRunner.Run(source);
            string resultEncoded = "<pre>" + WebUtility.HtmlEncode(result) + "</pre>";
            return await AppState.BotClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: resultEncoded,
                parseMode: ParseMode.Html,
                replyToMessageId: message.MessageId
            );
        }

        private static async Task<Message> InspirationalQuote(Message message)
        {
            using(HttpClient client = new HttpClient())
            {
                HttpResponseMessage resp = await client.GetAsync("https://inspirobot.me/api?generate=true");
                resp.EnsureSuccessStatusCode();
                string url = await resp.Content.ReadAsStringAsync();
                return await AppState.BotClient.SendPhotoAsync(
                    chatId: message.Chat.Id,
                    photo: url,
                    parseMode: ParseMode.Html,
                    replyToMessageId: message.MessageId
                );
            }
        }

        private static async Task<Message> RandomChuckNorrisFact(Message message)
        {
            using(HttpClient client = new HttpClient())
            {
                HttpResponseMessage resp = await client.GetAsync("https://api.chucknorris.io/jokes/random");
                resp.EnsureSuccessStatusCode();
                string content = await resp.Content.ReadAsStringAsync();
                JObject jsonContent = JsonConvert.DeserializeObject<JObject>(content);
                string resultEncoded = "<b>" + WebUtility.HtmlEncode(jsonContent["value"].Value<string>()) + "</b>";
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
            }
        }

        private static async Task<HtmlNode> GetWikipediaTextNode()
        {
            const string path = "https://en.wikipedia.org/wiki/Special:Random";
            const string summaryXpath = "//*[@id=\"mw-content-text\"]/div/table/following-sibling::p[1]";
            HtmlWeb web = new HtmlWeb()
            {
                CaptureRedirect = true
            };
            HtmlDocument doc = await web.LoadFromWebAsync(path);
            HtmlNode summaryNode = doc.DocumentNode.SelectSingleNode(summaryXpath);
            return summaryNode;
        }

        private static async Task<Message> RandomFact(Message message)
        {
            StringBuilder sb = new StringBuilder();
            HtmlNode summaryNode = await GetWikipediaTextNode();

            if (summaryNode == null)
                summaryNode = await GetWikipediaTextNode();  // Theres a small chance that the xpath fails so try again

            if (summaryNode == null)
            {
                string failMsg = "This time I couldn't find a fact. Go ahead and give it another shot.";
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, failMsg, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
            }

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
            return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, html, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
        }
        private static async Task LoadRumiQuotes()
        {
            var def = new { Quotes = new List<string>() };
            var txt = await System.IO.File.ReadAllTextAsync(@"rumi.json");
            var json = JsonConvert.DeserializeAnonymousType(txt, def);
            var rumis = json.Quotes;
            rumiQuotes = rumis;
        }

        private static async Task<Message> RandomRumiQuote(Message message)
        {
            if (rumiQuotes.Count == 0)
            {
                await LoadRumiQuotes();
            }
            string rumi = rumiQuotes[Convert.ToInt32(Math.Floor(AppState.Rng.NextDouble() * rumiQuotes.Count))];
            string quote = $"<i>{rumi}</i>";
            return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, quote, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
        }

        private static async Task<Message> Genie(Message message, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                string r = "<i>" + "You must write a question such as: Am I the father?" + "</i>";
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, r, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
            }
            string res = "";
            int rnd = Convert.ToInt32(Math.Ceiling(AppState.Rng.NextDouble() * 6));
            switch (rnd)
            {
                case 1: res = "Probably not.";
                    break;
                case 2: res = "Maybe you should ask your parents.";
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

        private static async Task<Message> RunMondScript(Message message, string text)
        {
            string result = "Timed out.";
            await Task.WhenAny
            (
                new Task(() => { result = AppState.ExecuteMond(text); }),
                Task.Delay(TimeSpan.FromSeconds(10))
            );
            result = AppState.ExecuteMond(text);

            if (string.IsNullOrWhiteSpace(result))
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "Got it.", replyToMessageId: message.MessageId, parseMode: ParseMode.Html);

            string resultEncoded = "<pre>" + WebUtility.HtmlEncode(result) + "</pre>";
            return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
        }

        private static async Task<Message> RandomDadJoke(Message message)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                HttpResponseMessage resp = await client.GetAsync("https://icanhazdadjoke.com/");
                resp.EnsureSuccessStatusCode();
                string content = await resp.Content.ReadAsStringAsync();
                JObject jsonContent = JsonConvert.DeserializeObject<JObject>(content);
                string resultEncoded = "<b>" + WebUtility.HtmlEncode(jsonContent["joke"].Value<string>()) + "</b>";
                return await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, resultEncoded, replyToMessageId: message.MessageId, parseMode: ParseMode.Html);
            }
        }

        private static string CleanupCommand(string command)
        {
            Regex CommandRegex = new Regex(@"[/]+([a-z]+)");
            return CommandRegex.Match(command).Groups[1].Value.ToLower();
        }
    }
}