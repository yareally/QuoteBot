using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Chronic;

using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;

namespace CustomStateBot
{
    [LuisModel("c413b2ef-382c-45bd-8ff0-f76d60e2a821", "6d0966209c6e4f6b835ce34492f3e6d9")]
    [Serializable]
    public class SimpleAlarmDialog : LuisDialog<object>
    {
        public const string DEFAULT_ALARM_WHAT = "default";

        public const string ENTITY_ALARM_TITLE = "builtin.alarm.title";

        public const string ENTITY_ALARM_START_TIME = "builtin.alarm.start_time";

        public const string ENTITY_ALARM_START_DATE = "builtin.alarm.start_date";

        private readonly Dictionary<string, Alarm> alarmByWhat = new Dictionary<string, Alarm>();

        private Alarm turnOff;

        public SimpleAlarmDialog() {}

        public SimpleAlarmDialog(ILuisService service) : base(service) {}

        public bool TryFindAlarm(LuisResult result, out Alarm alarm)
        {
            EntityRecommendation title;
            string what = result.TryFindEntity(ENTITY_ALARM_TITLE, out title) ? title.Entity : DEFAULT_ALARM_WHAT;
            return alarmByWhat.TryGetValue(what, out alarm);
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Sorry I did not understand: " + string.Join(", ", result.Intents.Select(i => i.Intent));
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.delete_alarm")]
        public async Task DeleteAlarm(IDialogContext context, LuisResult result)
        {
            if (TryFindAlarm(result, out Alarm alarm)) {
                alarmByWhat.Remove(alarm.What);
                await context.PostAsync($"alarm {alarm} deleted");
            }
            else {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.find_alarm")]
        public async Task FindAlarm(IDialogContext context, LuisResult result)
        {
            if (TryFindAlarm(result, out Alarm alarm)) await context.PostAsync($"found alarm {alarm}");
            else await context.PostAsync("did not find alarm");

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.set_alarm")]
        public async Task SetAlarm(IDialogContext context, LuisResult result)
        {
            if (!result.TryFindEntity(ENTITY_ALARM_TITLE, out EntityRecommendation title))
                title = new EntityRecommendation(ENTITY_ALARM_TITLE) { Entity = DEFAULT_ALARM_WHAT };

            if (!result.TryFindEntity(ENTITY_ALARM_START_DATE, out EntityRecommendation date))
                date = new EntityRecommendation(ENTITY_ALARM_START_DATE) { Entity = string.Empty };

            if (!result.TryFindEntity(ENTITY_ALARM_START_TIME, out EntityRecommendation time))
                time = new EntityRecommendation(ENTITY_ALARM_START_TIME) { Entity = string.Empty };

            var parser = new Parser();
            Span span = parser.Parse(date.Entity + " " + time.Entity);

            if (span != null) {
                DateTime? when = span.Start ?? span.End;
                var alarm = new Alarm { What = title.Entity, When = when.Value };
                alarmByWhat[alarm.What] = alarm;

                string reply = $"alarm {alarm} created";
                await context.PostAsync(reply);
            }
            else {
                await context.PostAsync("could not find time for alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.snooze")]
        public async Task AlarmSnooze(IDialogContext context, LuisResult result)
        {
            if (TryFindAlarm(result, out Alarm alarm)) {
                alarm.When = alarm.When.Add(TimeSpan.FromMinutes(7));
                await context.PostAsync($"alarm {alarm} snoozed!");
            }
            else {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.time_remaining")]
        public async Task TimeRemaining(IDialogContext context, LuisResult result)
        {
            if (TryFindAlarm(result, out Alarm alarm)) {
                DateTime now = DateTime.UtcNow;
                if (alarm.When > now) {
                    TimeSpan remaining = alarm.When.Subtract(DateTime.UtcNow);
                    await context.PostAsync($"There is {remaining} remaining for alarm {alarm}.");
                }
                else {
                    await context.PostAsync($"The alarm {alarm} expired already.");
                }
            }
            else {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.turn_off_alarm")]
        public async Task TurnOffAlarm(IDialogContext context, LuisResult result)
        {
            if (TryFindAlarm(result, out turnOff)) {
                PromptDialog.Confirm(
                    context,
                    AfterConfirming_TurnOffAlarm,
                    "Are you sure?",
                    promptStyle: PromptStyle.None);
            }
            else {
                await context.PostAsync("did not find alarm");
                context.Wait(MessageReceived);
            }
        }

        public async Task AfterConfirming_TurnOffAlarm(IDialogContext context, IAwaitable<bool> confirmation)
        {
            if (await confirmation) {
                alarmByWhat.Remove(turnOff.What);
                await context.PostAsync($"Ok, alarm {turnOff} disabled.");
            }
            else {
                await context.PostAsync("Ok! We haven't modified your alarms!");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.alarm_other")]
        public async Task AlarmOther(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("what ?");
            context.Wait(MessageReceived);
        }

        [Serializable]
        public sealed class Alarm : IEquatable<Alarm>
        {
            public DateTime When { get; set; }

            public string What { get; set; }

            public bool Equals(Alarm other) => other != null && When == other.When && What == other.What;

            public override string ToString() => $"[{What} at {When}]";

            public override bool Equals(object other) => Equals(other as Alarm);

            public override int GetHashCode() => What.GetHashCode();
        }
    }
}