// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EmptyBot v4.6.2

using AlexaBotDemo.Adapters;
using AlexaBotDemo.Bots;
using AlexaBotDemo.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Integration;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace AlexaBotDemo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            // Bot adapters
            services.AddSingleton<IAdapterIntegration, BotAdapterWithErrorHandler>();
            services.AddSingleton<IBotFrameworkHttpAdapter, AlexaAdapterWithErrorHandler>();

            // Object logger
            services.AddSingleton(sp =>
            {
                var environment = sp.GetRequiredService<IHostingEnvironment>();
                var logFolder = Path.GetFullPath(Path.Combine(environment.ContentRootPath, $"../../object-logs/"));

                return new ObjectLogger(logFolder);
            });

            // Bot state
            services.AddSingleton<IStorage, MemoryStorage>();
            services.AddSingleton<UserState>();
            services.AddSingleton<BotStateAccessors>();
            // Conversation reference temporal store
            services.AddSingleton<BotConversation>();

            // Bots.
            services.AddTransient<AlexaBot>();
            services.AddTransient<MonitorBot>();

            // QnAMaker endpoint
            services.AddSingleton(new QnAMakerEndpoint
            {
                KnowledgeBaseId = Configuration.GetValue<string>("QnAKnowledgebaseId"),
                EndpointKey = Configuration.GetValue<string>("QnAAuthKey"),
                Host = Configuration.GetValue<string>("QnAEndpointHostName")
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseWebSockets();
            app.UseMvc();
        }
    }
}