using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;

namespace CustomStateBot
{
    [Serializable]
    public class StateDialog : IDialog<object>
    {
        private const string HELP_MESSAGE =
            "\n " + "* To receive a new quote, type 'quote'. \n "
            + "\n " + "* To see this menu again, type 'help'. \n "
            + "* To find out more about the app, type 'about' \n ";

        private bool userWelcomed;


        public async Task StartAsync(IDialogContext context)
        {
            await context.PostAsync(
                $"I'm QuoteBot. I'm currently configured to send you quotes by Hillary Clinton. "
                + $"If you don't like Hillary, gtfo :p"
            );
            context.Wait(MessageReceivedAsync);
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            IMessageActivity message = await result;

            if (!context.UserData.TryGetValue(string.Format(ContextConstants.USER_NAME_KEY, message.From.Name), out string userName)) {
                context.UserData.SetValue(string.Format(ContextConstants.USER_NAME_KEY, message.From.Name), message.From.Name);
                userName = message.From.Name;
                //PromptDialog.Text(context, ResumeAfterPrompt, "Before get started, please tell me your name?");
            }

            if (!context.UserData.TryGetValue(string.Format(ContextConstants.USER_QUOTE_KEY, message.From.Name, "Hillary_Clinton"), out int quoteId)) {
                context.UserData.SetValue(string.Format(ContextConstants.USER_QUOTE_KEY, message.From.Name, "Hillary_Clinton"), quoteId);
            }

            if (!userWelcomed) {
                userWelcomed = true;
                await context.PostAsync($"Welcome {userName}! List of commands: {HELP_MESSAGE}");

                context.Wait(MessageReceivedAsync);
                return;
            }

            if (message.Text.Equals("quote", StringComparison.InvariantCultureIgnoreCase)) {
                XElement xmlTree;

                using (
                    StreamReader reader =
                        File.OpenText($"{AppDomain.CurrentDomain.BaseDirectory}/quotes/en/Hillary_Clinton.xml")) {
                    string fileText = await reader.ReadToEndAsync();
                    xmlTree = XElement.Parse(fileText);
                }
                List<XElement> elems = xmlTree.FirstNode.ElementsAfterSelf().ToList();
                
                string quote = elems[quoteId].Value;
                string refStr = elems[quoteId].Attribute(XName.Get("ref")).Value.Replace(". ", ".  \n  \n");
                string author = elems[quoteId].Attribute(XName.Get("author")).Value;

                await context.PostAsync($"{quote}\n\n\t- {author}");
                await context.PostAsync($"Source(s): {refStr}");
                quoteId = quoteId >= elems.Count ? 0 : quoteId + 1;
                context.UserData.SetValue(string.Format(ContextConstants.USER_QUOTE_KEY, message.From.Name, "Hillary_Clinton"), quoteId);
            }
            else if (message.Text.Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
                await context.PostAsync($"List of commands: {HELP_MESSAGE}");
            }
            else if (message.Text.Equals("about", StringComparison.InvariantCultureIgnoreCase)) {
                await context.PostAsync("For Jules, my favorite Hillary fan :)  \n\n ©2017 | CodingCreation LLC");
            }

            context.Wait(MessageReceivedAsync);
        }

        private async Task ResumeAfterPrompt(IDialogContext context, IAwaitable<string> result)
        {
            try {
                string userName = await result;
                userWelcomed = true;

                await context.PostAsync($"Welcome {userName}! {HELP_MESSAGE}");

                context.UserData.SetValue(ContextConstants.USER_NAME_KEY, userName);
            } catch (TooManyAttemptsException) {}

            context.Wait(MessageReceivedAsync);
        }
    }
}