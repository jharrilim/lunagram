using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            var message = update.Message;
            switch (message.Type)
            {
                case MessageType.Text:
                    var result = AppState.MondState.Run(update.Message.Text);
                    await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, result.Serialize());
                    break;

                default:
                    await AppState.BotClient.SendTextMessageAsync(message.Chat.Id, "Sorry, I did not understand that.");
                    break;
            }

            return Ok();
        }
    }
}