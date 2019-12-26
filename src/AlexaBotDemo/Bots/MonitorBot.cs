using AlexaBotDemo.Infrastructure;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AlexaBotDemo.Bots
{
    public class MonitorBot : ActivityHandler
    {
        private readonly ObjectLogger _objectLogger;
        private readonly BotConversation _conversation;
        private readonly IAdapterIntegration _botAdapter;
        private readonly BotStateAccessors _accessors;

        public MonitorBot(
            ObjectLogger objectLogger,
            BotConversation conversation,
            IAdapterIntegration botAdapter,
            BotStateAccessors accessors)
        {
            _objectLogger = objectLogger;
            _conversation = conversation;
            _botAdapter = botAdapter;
            _accessors = accessors;
        }
        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await _objectLogger.LogObjectAsync(turnContext.Activity, turnContext.Activity.Id);

            await base.OnTurnAsync(turnContext, cancellationToken);

            await _accessors.SaveChangesAsync(turnContext);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Hello world - From MonitorBor! (channel: {turnContext.Activity.ChannelId})"), cancellationToken);
                }
            }
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var message = turnContext.Activity.Text?.ToLower();

            switch (message ?? "")
            {
                case "monitor alexa":
                    _conversation.Reference = turnContext.Activity.GetConversationReference();
                    await turnContext.SendActivityAsync($@"Alexa monitor is on");

                    return;
            }

            await turnContext.SendActivityAsync($"Echo from MonitorBot: \"**{turnContext.Activity.Text}**\"");
        }

    }
}
