// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AlexaBotDemo.Infrastructure;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AlexaBotDemo.Bots
{
    public class AlexaBot : ActivityHandler
    {
        private readonly BotStateAccessors _accessors;
        private readonly IAdapterIntegration _botAdapter;
        private readonly IConfiguration _configuration;
        private readonly BotConversation _conversation;
        private readonly ILogger<AlexaBot> _logger;
        private readonly ObjectLogger _objectLogger;

        public AlexaBot(
            ObjectLogger objectLogger,
            BotConversation conversation,
            IAdapterIntegration botAdapter,
            IConfiguration configuration,
            BotStateAccessors accessors,
            ILogger<AlexaBot> logger)
        {
            _objectLogger = objectLogger;
            _conversation = conversation;
            _botAdapter = botAdapter;
            _configuration = configuration;
            _accessors = accessors;
            _logger = logger;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _objectLogger.LogObjectAsync(turnContext.Activity, turnContext.Activity.Id);

            await base.OnTurnAsync(turnContext, cancellationToken);

            await _accessors.SaveChangesAsync(turnContext);
        }

        protected override async Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            await EchoBackToMonitorBot(turnContext);

            switch (turnContext.Activity.Name)
            {
                case "LaunchRequest":
                    await HandleLaunchRequestAsync(turnContext, cancellationToken);
                    return;

                case "StopIntent":
                    await turnContext.SendActivityAsync(MessageFactory.Text("Terminando la sesión", inputHint: InputHints.IgnoringInput));
                    return;
            }

            await turnContext.SendActivityAsync(
                $"Event received. Channel: {turnContext.Activity.ChannelId}, Name: {turnContext.Activity.Name}");
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            await EchoBackToMonitorBot(turnContext);

            var message = turnContext.Activity.Text.ToLower();
            var alexaConversation = await _accessors.AlexaConversation.GetAsync(turnContext, () => new AlexaConversation());

            if (message == "adiós")
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Adiós {alexaConversation.UserName}!"), cancellationToken);

                alexaConversation.UserName = null;
                await _accessors.AlexaConversation.SetAsync(turnContext, alexaConversation);

                return;
            }

            var replyMessage = string.Empty;

            _logger.LogInformation(@"----- Retrieved alexaConversation ({@AlexaConversation})", alexaConversation);

            if (alexaConversation.UserName is null)
            {
                alexaConversation.UserName = message;
                await _accessors.AlexaConversation.SetAsync(turnContext, alexaConversation);

                replyMessage = $"Gracias {message}, ahora sí voy a repetir lo próximo que digas.";
            }
            else
            {
                replyMessage = $"{alexaConversation.UserName}, entendí, {message}";
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(replyMessage, inputHint: InputHints.ExpectingInput), cancellationToken);
        }

        private async Task EchoBackToMonitorBot(ITurnContext<IEventActivity> turnContext)
        {
            if (_conversation.Reference == null) return;

            var botAppId = string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) ? "*" : _configuration["MicrosoftAppId"];

            var eventValue = JsonConvert.SerializeObject(turnContext.Activity.Value, Formatting.Indented);

            await _botAdapter.ContinueConversationAsync(botAppId, _conversation.Reference, async (context, token) =>
            {
                await context.SendActivityAsync($"Event received:\n```\n{eventValue}\n```");
            });
        }

        private async Task EchoBackToMonitorBot(ITurnContext<IMessageActivity> turnContext)
        {
            if (_conversation.Reference == null) return;

            var botAppId = string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) ? "*" : _configuration["MicrosoftAppId"];

            await _botAdapter.ContinueConversationAsync(botAppId, _conversation.Reference, async (context, token) =>
            {
                await context.SendActivityAsync($"Message received ({turnContext.Activity.Locale}):\n**{turnContext.Activity.Text}**");
            });
        }

        private async Task HandleLaunchRequestAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            var alexaConversation = await _accessors.AlexaConversation.GetAsync(turnContext, () => new AlexaConversation());

            var greetingMessage = string.IsNullOrEmpty(alexaConversation.UserName)
                ? $"Hola, voy a repetir todo lo que digas, para empezar, por favor, dime tu nombre"
                : $@"Hola {alexaConversation.UserName}, seguimos con el mismo juego, dime cualquier cosa para repetirla";

            await turnContext.SendActivityAsync(MessageFactory.Text(greetingMessage, inputHint: InputHints.ExpectingInput));
        }
    }
}