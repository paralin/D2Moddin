// 
// MatchmakeManager.cs
// Created by ilian000 on 2014-06-19
// Licenced under the Apache License, Version 2.0
//

using D2MPMaster.Browser;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XSockets.Core.Common.Globals;
using XSockets.Core.Common.Socket;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using XSockets.Plugin.Framework;

namespace D2MPMaster.Matchmaking
{
    [XSocketMetadata("MatchmakeManager", Constants.GenericTextBufferSize, PluginRange.Internal)]
    public class MatchmakeManager : XSocketController, IDisposable
    {

        /// <summary>
        ///  Margin increases by this number every time doMatchmake executes.
        /// </summary>
        private const int ratingMargin = 10;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly BrowserController Browsers = new BrowserController();

        /// <summary>
        ///  List of groups of players to be filled with 5 slots to form a team.
        /// </summary>
        public static List<Matchmake> inMatchmaking = new List<Matchmake>();

        /// <summary>
        /// List of teams to be faced with another team.
        /// </summary>
        public static List<Matchmake> inTeamMatchmaking = new List<Matchmake>();


        public static volatile bool Registered = false;
        public static volatile bool shutdown = false;

        public static Thread matchmakeThread;

        public MatchmakeManager()
        {
            if (!Registered)
            {
                Registered = true;
                matchmakeThread = new Thread(matchmakeT);
                matchmakeThread.Start();
            }
        }

        public void Dispose()
        {
            shutdown = true;
            Registered = false;
        }

        private static void matchmakeT()
        {
            while (!shutdown)
            {
                Thread.Sleep(500);
                doMatchmake();
            }
        }

        private static void doMatchmake()
        {
            foreach (var m in inMatchmaking.ToArray())
            {
                bool matched = false;
                foreach (KeyValuePair<string, int> modMmr in m.rating)
                {
                    // Find match with a similar rating (search margin increases every try), enough free slots and a common mod
                    var match = inMatchmaking.Where(x => x.rating.Any(
                        y => y.Key == modMmr.Key && Math.Abs(y.Value - modMmr.Value) < m.matchTries * ratingMargin
                        ) && 5 - x.users.Count() >= m.users.Count() && x.mods.Intersect(m.mods).Any()).FirstOrDefault();
                    if (match != null)
                    {
                        // Merge users to one matchmake
                        match.MoveUsers(m.users, match.users);
                        foreach (var browser in Browsers.Find(b => b.user != null && b.matchmake != null && b.matchmake.id == m.id))
                        {
                            browser.matchmake = match;
                        }
                        m.users = null;
                        match.mods = match.mods.Intersect(m.mods).ToArray();
                        calculateMmr(match);
                        inMatchmaking.Remove(m);
                        matched = true;
                        log.InfoFormat("Matchmake merged from {0} players to {1} players after {2} tries. New rating: {3}", m.users.Count(), match.users.Count(), m.matchTries, match.rating);
                        if (match.users.Count() == 5)
                        {
                            match.matchTries = 1;
                            inTeamMatchmaking.Add(match);
                            inMatchmaking.Remove(match);
                        }
                    }
                }
                if (!matched)
                {
                    m.matchTries++;
                }

            }
            foreach (var m in inTeamMatchmaking.ToArray())
            {
                bool matched = false;
                foreach (KeyValuePair<string, int> modMmr in m.rating)
                {
                    var match = inMatchmaking.Where(x => x.rating.Any(
                   y => y.Key == modMmr.Key && Math.Abs(y.Value - modMmr.Value) < m.matchTries * ratingMargin
                   ) && x.mods.Intersect(m.mods).Any()).FirstOrDefault();
                    if (match != null)
                    {
                        Random rnd = new Random();
                        var mods = match.mods.Intersect(m.mods).ToArray();
                        var lobby = LobbyManager.CreateMatchedLobby(m, match, mods[rnd.Next(0, mods.Length)]);
                        foreach (var browser in Browsers.Find(b => b.user != null && b.matchmake != null && (b.matchmake.id == m.id || b.matchmake.id == match.id)))
                        {
                            browser.lobby = lobby;
                            browser.matchmake = null;
                        }
                        inTeamMatchmaking.Remove(m);
                        inTeamMatchmaking.Remove(match);
                        matched = true;
                    }
                }
                if (!matched)
                {
                    m.matchTries++;
                }               
            }
        }

        public static Matchmake CreateMatchmake(User user, List<Mod> mods)
        {
            var matchmake = new Matchmake()
            {
                id = Utils.RandomString(17),
                users = new User[5],
                mods = mods.Select(x => x.Id).ToArray(),
                rating = user.profile.mmr.Where(x => mods.Any(y => x.Key == y.Id)).ToDictionary(x => x.Key, x=> x.Value),
                matchTries = 1
            };
            matchmake.users[0] = user;
            inMatchmaking.Add(matchmake);
            log.InfoFormat("Matchmaking created User: #{0}", user.profile.name);
            return matchmake;
        }

        public static void LeaveMatchmake(BrowserController controller)
        {
            if (controller.matchmake == null || controller.user == null) return;
            var mm = controller.matchmake;
            controller.matchmake = null;
            if (mm.users.Count() == 0 && inMatchmaking.Contains(mm))
            {
                inMatchmaking.Remove(mm);
            }
            else if (inTeamMatchmaking.Contains(mm))
            {
                inTeamMatchmaking.Remove(mm);
                for (int i = 0; i < mm.users.Length; i++)
                {
                    var user = mm.users[i];
                    if (user != null && user.steam == controller.user.steam)
                    {
                        mm.users[i] = null;
                    }
                }
                inMatchmaking.Add(mm);
            }
        }

        private static void calculateMmr(Matchmake m)
        {
            foreach (KeyValuePair<string, int> modMmr in m.rating)
            {
                m.rating[modMmr.Key] = Convert.ToInt32(m.users.Select(x => x.profile.mmr.Where(y=> y.Key == modMmr.Key).Select(y=> y.Value).ToArray().Average()));
            }
        }
    }
}
