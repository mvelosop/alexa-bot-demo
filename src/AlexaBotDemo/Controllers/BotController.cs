// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EmptyBot v4.6.2

using AlexaBotDemo.Infrastructure;
using Bot.Builder.Community.Adapters.Alexa;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading.Tasks;

namespace AlexaBotDemo.Controllers
{
    [ApiController]
    public class BotController : ControllerBase
    {
        private readonly IBotFrameworkHttpAdapter _alexaAdapter;
        private readonly IBot _bot;
        private readonly IAdapterIntegration _botAdapter;
        private readonly ObjectLogger _objectLogger;

        public BotController(
            ObjectLogger objectLogger,
            IAdapterIntegration botAdapter,
            IBotFrameworkHttpAdapter alexaAdapter,
            IBot bot)
        {
            _objectLogger = objectLogger;
            _botAdapter = botAdapter;
            _alexaAdapter = alexaAdapter;
            _bot = bot;
        }

        [HttpPost("api/alexa")]
        public async Task AlexaPostAsync()
        {
            Request.EnableBuffering();

            using (var reader = new StreamReader(Request.Body))
            {
                var body = await reader.ReadToEndAsync();
                var bodyObject = JsonConvert.DeserializeObject<JObject>(body);
                var sessionId = bodyObject["session"]["sessionId"].Value<string>();

                _objectLogger.SetSessionId(sessionId);
                await _objectLogger.LogObjectAsync(body, HttpContext.TraceIdentifier);

                Request.Body.Position = 0;

                await _alexaAdapter.ProcessAsync(Request, Response, _bot);
            }
        }

        [HttpPost("api/messages")]
        public async Task<InvokeResponse> BotPostAsync([FromBody]Activity activity)
        {
            var authHeader = Request.Headers["Authorization"];

            return await _botAdapter.ProcessActivityAsync(authHeader, activity, _bot.OnTurnAsync, default);
        }
    }
}