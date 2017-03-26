using System;
using System.Configuration;
using System.Reflection;
using System.Web;
using System.Web.Http;

using Autofac;

using Microsoft.Bot.Builder.Azure;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;

namespace CustomStateBot
{
    public class WebApiApplication : HttpApplication
    {
        protected void Application_Start()
        {
            var docDbServiceEndpoint = new Uri(ConfigurationManager.AppSettings["DocumentDbServiceEndpoint"]);
            string docDbEmulatorKey = ConfigurationManager.AppSettings["DocumentDbAuthKey"];

            var builder = new ContainerBuilder();

            builder.RegisterModule(new AzureModule(Assembly.GetExecutingAssembly()));

            var store = new DocumentDbBotDataStore(docDbServiceEndpoint, docDbEmulatorKey);
            builder.Register(c => store)
                .Keyed<IBotDataStore<BotData>>(AzureModule.Key_DataStore)
                .AsSelf()
                .SingleInstance();

            builder.Update(Conversation.Container);

            GlobalConfiguration.Configure(WebApiConfig.Register);
        }
    }
}