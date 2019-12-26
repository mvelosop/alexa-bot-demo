// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AlexaBotDemo.Infrastructure;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
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
            ILogger<AlexaBot> logger,
            ObjectLogger objectLogger,
            IConfiguration configuration,
            IAdapterIntegration botAdapter,
            BotConversation conversation,
            BotStateAccessors accessors,
            QnAMakerEndpoint endpoint)
        {
            _logger = logger;
            _objectLogger = objectLogger;
            _configuration = configuration;

            // ** Bot adapter (to send proactive message)
            _botAdapter = botAdapter;

            // ** Bot state handling
            _conversation = conversation;
            _accessors = accessors;

            // ** QnA endpoint
            AlexaBotQnA = new QnAMaker(endpoint);
        }

        public QnAMaker AlexaBotQnA { get; private set; }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _objectLogger.LogObjectAsync(turnContext.Activity, turnContext.Activity.Id);

            await base.OnTurnAsync(turnContext, cancellationToken);

            // ** Save bot state changes
            await _accessors.SaveChangesAsync(turnContext);
        }

        protected override async Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            // ** Echo event information to monitor bot
            await EchoEventAsync(turnContext);

            switch (turnContext.Activity.Name)
            {
                case "LaunchRequest":
                    await HandleLaunchRequestAsync(turnContext, cancellationToken);
                    return;

                case "StopIntent":
                    await turnContext.SendActivityAsync(MessageFactory.Text("Terminando la sesión", inputHint: InputHints.IgnoringInput));
                    return;
            }

            // ** Speak back any other event
            await turnContext.SendActivityAsync(
                $"Event received: {turnContext.Activity.Name}");
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // ** Echo user message to monitor
            await EchoUserMessageAsync(turnContext);

            var message = turnContext.Activity.Text.ToLower();
            var alexaConversation = await _accessors.AlexaConversation.GetAsync(turnContext, () => new AlexaConversation());

            _logger.LogInformation(@"----- Retrieved alexaConversation ({@AlexaConversation})", alexaConversation);

            // ** Handle goodbye message
            if (message == "adiós")
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Adiós {alexaConversation.UserName}!"), cancellationToken);

                // Reset conversation for next interaction
                alexaConversation = new AlexaConversation();
                await _accessors.AlexaConversation.SetAsync(turnContext, alexaConversation);

                return;
            }

            var replyMessage = string.Empty;

            if (alexaConversation.UserName is null)
            {
                alexaConversation.UserName = message;

                replyMessage = $"Gracias {message}, ahora sí voy a repetir lo que digas.";
            }
            else if (alexaConversation.TurnControl < 0)
            {
                replyMessage = $"{alexaConversation.UserName}, dijiste {turnContext.Activity.Text}";
            }
            else if (alexaConversation.TurnControl == 0)
            {
                replyMessage = $"A ver {alexaConversation.UserName}, esto está un poco aburrido, mejor hazme preguntas.";
            }
            else
            {
                // ** Search QnA service
                replyMessage = await FindAnswerAsync(turnContext, cancellationToken);
            }

            alexaConversation.TurnControl++;

            await _accessors.AlexaConversation.SetAsync(turnContext, alexaConversation);

            await EchoBotMessageAsync(turnContext, replyMessage);

            // ** Speak back reply message
            await turnContext.SendActivityAsync(MessageFactory.Text(replyMessage, inputHint: InputHints.ExpectingInput), cancellationToken);
        }

        private async Task EchoBotMessageAsync(ITurnContext<IMessageActivity> turnContext, string message)
        {
            if (_conversation.Reference == null) return;

            var botAppId = string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) ? "*" : _configuration["MicrosoftAppId"];

            await _botAdapter.ContinueConversationAsync(botAppId, _conversation.Reference, async (context, token) =>
            {
                await context.SendActivityAsync($"Bot said:\n*{message}*");
            });
        }

        private async Task EchoEventAsync(ITurnContext<IEventActivity> turnContext)
        {
            // ** Nothing to do if no conversation reference
            if (_conversation.Reference == null) return;

            var eventValue = JsonConvert.SerializeObject(turnContext.Activity.Value, Formatting.Indented);
            var botAppId = string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) ? "*" : _configuration["MicrosoftAppId"];

            // ** Send proactive message
            await _botAdapter.ContinueConversationAsync(botAppId, _conversation.Reference, async (context, token) =>
            {
                await context.SendActivityAsync($"Event received:\n```\n{eventValue}\n```");
            });
        }

        private async Task EchoUserMessageAsync(ITurnContext<IMessageActivity> turnContext)
        {
            if (_conversation.Reference == null) return;

            var botAppId = string.IsNullOrEmpty(_configuration["MicrosoftAppId"]) ? "*" : _configuration["MicrosoftAppId"];

            await _botAdapter.ContinueConversationAsync(botAppId, _conversation.Reference, async (context, token) =>
            {
                await context.SendActivityAsync($"User said ({turnContext.Activity.Locale}):\n**{turnContext.Activity.Text}**");
            });
        }

        private async Task<string> FindAnswerAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var results = await AlexaBotQnA.GetAnswersAsync(turnContext);

            if (results.Length == 0)
            {
                var alexaConversation = await _accessors.AlexaConversation.GetAsync(turnContext, () => new AlexaConversation());

                return $"Perdona {alexaConversation.UserName}, pero no tengo idea, prueba preguntarme otra cosa.";
            }

            return results.First().Answer;
        }

        private async Task HandleLaunchRequestAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            // ** Set up welcome (back) message
            var alexaConversation = await _accessors.AlexaConversation.GetAsync(turnContext, () => new AlexaConversation());
            var game = alexaConversation.TurnControl >= 0
                ? "Ahora estamos jugando a que tú me haces preguntas."
                : "seguimos con el mismo juego, dime cualquier cosa para repetirla.";

            var greetingMessage = string.IsNullOrEmpty(alexaConversation.UserName)
                ? $"Hola, soy un demo de Alexa con Bot Framework y voy a repetir todo lo que digas, para empezar, por favor, dime tu nombre"
                : $@"Hola {alexaConversation.UserName}, {game}";

            await turnContext.SendActivityAsync(MessageFactory.Text(greetingMessage, inputHint: InputHints.ExpectingInput));
        }
    }
}