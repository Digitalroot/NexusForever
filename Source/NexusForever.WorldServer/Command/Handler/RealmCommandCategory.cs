﻿using NexusForever.Shared;
using NexusForever.Shared.Network;
using NexusForever.WorldServer.Command.Context;
using NexusForever.WorldServer.Game.RBAC.Static;
using NexusForever.WorldServer.Game.Social;
using NexusForever.WorldServer.Game.Social.Static;
using NexusForever.WorldServer.Network;
using NLog;
using System;
using System.Threading.Tasks;
using System.Timers;
using Timer = System.Timers.Timer;

namespace NexusForever.WorldServer.Command.Handler
{
    [Command(Permission.Realm, "A collection of commands to manage the realm.", "realm")]
    public class RealmCommandCategory : CommandCategory
    {
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        [Command(Permission.RealmMOTD, "Set the realm's message of the day and announce to the realm.", "motd")]
        public void HandleRealmMotd(ICommandContext context,
            [Parameter("New message of the day for the realm.")]
            string message)
        {
            WorldServer.RealmMotd = message;
            Parallel.ForEach(NetworkManager<WorldSession>.Instance.GetSessions(), session =>
            {
                SocialManager.Instance.SendMessage(session, WorldServer.RealmMotd, "MOTD", ChatChannel.Realm);
            });
        }

        #region Shutdown Command

        [Command(Permission.Realm, "Start a realm shutdown", "shutdown")]
        public void RealmShutdown(ICommandContext context,
            [Parameter("Second until shutdown. (300 = 5 mins)")]
            uint seconds)
        {
            log.Info($"Realm is shutting down in {seconds} seconds.");
            shutdownDateTime = DateTime.Now.AddSeconds(seconds);
            Timer timer = new Timer(2000);
            timer.Elapsed += OnTimedEvent;
            timer.Start();
        }

        private static DateTime shutdownDateTime;

        private static void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            Timer timer = (Timer) source;
            if (DateTime.Now >= shutdownDateTime)
            {
                timer.Stop();
                Shutdown();
            }
            else
            {
                HandleShutdownTick(timer);
            }
        }

        private static void HandleShutdownTick(Timer timer)
        {
            TimeSpan timeSpan = shutdownDateTime - DateTime.Now;
            if (timeSpan.Minutes > 1 && !timer.Interval.Equals(60000))
            {
                HandleShutdownTimeGreaterThenOneMin(timer, timeSpan.Minutes);
            }
            else if (timeSpan.Minutes <= 1 && !timer.Interval.Equals(10000))
            {
                HandleShutdownTimeLessThenOneMin(timer, timeSpan.Seconds);
            }
            else if (timeSpan.Minutes > 0)
            {
                WarnUsers($"{timeSpan.Minutes} minutes.");
            }
            else
            {
                WarnUsers($"{timeSpan.Seconds.RoundOff()} seconds.");
            }
        }

        private static void HandleShutdownTimeGreaterThenOneMin(Timer timer, int timeSpanMinutes)
        {
            timer.Interval = 60000;
            WarnUsers($"{timeSpanMinutes} minutes.");
        }

        private static void HandleShutdownTimeLessThenOneMin(Timer timer, int timeSpanSeconds)
        {
            timer.Interval = 10000;
            WarnUsers($"{timeSpanSeconds.RoundOff()} seconds.");
        }

        private static void WarnUsers(string message)
        {
            message = $"Realm is shutting down in {message}.";
            log.Info(message);
            Parallel.ForEach(NetworkManager<WorldSession>.Instance.GetSessions(), session =>
            {
                SocialManager.Instance.SendMessage(session, message);
            });
        }

        private static void Shutdown()
        {
            log.Info("Realm is shutting down now.");
            // Gracefully disconnect all users.
            try
            {
                Parallel.ForEach(NetworkManager<WorldSession>.Instance.GetSessions(), session =>
                {
                    session.Disconnect();
                });
            }
            catch (InvalidOperationException exception)
            {
                if (!exception.Message.Equals("Collection was modified after the enumerator was instantiated."))
                {
                    log.Error(exception);
                }
            }
            finally
            {
                log.Info("All users have been disconnected.");
                Timer timer = new Timer(15000);
                timer.Elapsed += OnShutdown;
                timer.Start();
            }
        }

        private static void OnShutdown(object source, ElapsedEventArgs e)
        {
            log.Info("Realm Shutdown.");
            Timer timer = (Timer)source;
            timer?.Stop();
            Environment.Exit(0);
        }

        #endregion
    }
}
