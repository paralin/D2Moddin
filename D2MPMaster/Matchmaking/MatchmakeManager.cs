// 
// MatchmakeManager.cs
// Created by ilian000 on 2014-06-19
// Licenced under the Apache License, Version 2.0
//

using System.Configuration;
using D2MPMaster.Browser;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
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
        struct KFactor
        {
            public int MinMmr { get; set; }
            public int MaxMmr { get; set; }
            public int Factor { get; set; }
        }

        /// <summary>
        /// Base MMR for new players
        /// </summary>
        private const int BaseMmr = 1500;

        /// <summary>
        /// Minimum MMR archievable by a player
        /// </summary>
        private const int MmrFloor = 100;

        /// <summary>
        /// Maximum MMR archievable by a player
        /// </summary>
        private const int MmrRoof = 5000;

        /// <summary>
        /// Factors to calculate MMR after match
        /// </summary>
        private static readonly KFactor[] KFactors = new[]
        {
            new KFactor(){ MinMmr = MmrFloor, MaxMmr = 2099, Factor = 32 },
            new KFactor(){ MinMmr = 2100, MaxMmr = 3399, Factor = 24 },
            new KFactor(){ MinMmr = 3400, MaxMmr = MmrRoof, Factor = 16 }
        };

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
        public static Thread teammatchmakeThread;

        public MatchmakeManager()
        {
            if (!Registered)
            {
                Registered = true;
                matchmakeThread = new Thread(matchmakeT);
                teammatchmakeThread = new Thread(TeamMatchmakeT);
                matchmakeThread.Start();
                teammatchmakeThread.Start();
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

        private static void TeamMatchmakeT()
        {
            while (!shutdown)
            {
                Thread.Sleep(500);
                doTeamMatchmake();
            }
        }

        private static void doMatchmake()
        {
            foreach (var match in inMatchmaking.ToArray())
            {
                //if the match was moved to the other queue, simply ignore it
                if (inTeamMatchmaking.Contains(match))
                    continue;

                // Find match with a similar rating (search margin increases every try), enough free slots and a common mod
                var matchFound = inMatchmaking.FirstOrDefault(x => match.IsMatch(x));
                if (matchFound != null)
                {
                    // Merge everything to the new match
                    matchFound.MergeMatches(match);
                    // update the browsers with the new match
                    foreach (var browser in Browsers.Find(b => b.user != null && b.matchmake != null && b.matchmake.Id == match.Id))
                    {
                        browser.matchmake = matchFound;
                    }

                    log.InfoFormat("Matchmake merged from {0} players to {1} players after {2} tries. New rating: {3}",
                        match.Users.Count(), matchFound.Users.Count(), match.TryCount, matchFound.Ratings);

                    //emove the old match, we dont need it
                    lock (inMatchmaking)
                    {
                        inMatchmaking.Remove(match);
                    }

                    //if we are crowded
                    if (matchFound.Users.Length == 5)
                    {
                        //reset the tries and dont ignore it
                        matchFound.TryCount = 1;
                        matchFound.Ignore = false;

                        //move to team MM
                        lock (inMatchmaking)
                        {
                            inMatchmaking.Remove(matchFound);
                        }
                        lock (inTeamMatchmaking)
                        {
                            inTeamMatchmaking.Add(matchFound);
                        }
                    }
                }
                else
                {
                    //no match found, open the possibilities
                    match.TryCount++;
                }
            }
        }

        private static void doTeamMatchmake()
        {
            Random rnd = new Random();

            foreach (var match in inTeamMatchmaking.ToArray())
            {
                //dont process ignored match
                if (match.Ignore)
                    continue;

                // Find match with a similar rating (search margin increases every try) and a common mod
                var matchFound = inMatchmaking.FirstOrDefault(x => match.IsMatch(x, true));
                if (matchFound != null)
                {
                    //get available mods by rating
                    var mods = match.GetMatchedMods(matchFound);
                    //create a lobby with one of the mods
                    var lobby = LobbyManager.CreateMatchedLobby(match, matchFound, mods[rnd.Next(0, mods.Length)]);
                    //remove the matchmake from the browsers and set the lobby
                    foreach (var browser in Browsers.Find(b => b.user != null && b.matchmake != null && (b.matchmake.Id == match.Id || b.matchmake.Id == matchFound.Id)))
                    {
                        browser.lobby = lobby;
                        browser.matchmake = null;
                    }

                    //set this flag so it wont try to find during the array loop
                    matchFound.Ignore = true;

                    //remove the matches from the queue
                    lock (inTeamMatchmaking)
                    {
                        inTeamMatchmaking.Remove(match);
                        inTeamMatchmaking.Remove(matchFound);
                    }
                }
                else
                {
                    match.TryCount++;
                }
            }
        }

        public static Matchmake CreateMatchmake(User user, List<Mod> mods)
        {
            //loop through each mod
            foreach (var mod in mods)
            {
                //if the user does not have a MMR for it
                if (!user.profile.mmr.ContainsKey(mod.Id))
                {
                    //Assign base
                    user.profile.mmr.Add(mod.Id, BaseMmr);
                    //save it
                    Mongo.Users.Save(user);
                }
            }

            var matchmake = new Matchmake()
            {
                Id = Utils.RandomString(17),
                Users = new User[5],
                Mods = mods.Select(x => x.Id).ToArray(),
                Ratings = user.profile.mmr.Where(x => mods.Any(y => x.Key == y.Id)).ToDictionary(x => x.Key, x => x.Value),
                TryCount = 1
            };

            matchmake.Users[0] = user;

            log.InfoFormat("Matchmaking created User: #{0}", user.profile.name);

            //add to the queue
            lock (inMatchmaking)
            {
                inMatchmaking.Add(matchmake);
            }

            return matchmake;
        }

        public static void LeaveMatchmake(BrowserController controller)
        {
            if (controller.matchmake == null || controller.user == null)
                return;

            var mm = controller.matchmake;
            controller.matchmake = null;

            //remove the user from the MM
            for (int i = 0; i < mm.Users.Length; i++)
            {
                var user = mm.Users[i];
                if (user != null && user.steam == controller.user.steam)
                {
                    mm.Users[i] = null;
                    mm.UpdateRating();
                }
            }

            //if no users are left on it
            if (mm.Users.Length == 0)
            {
                //check the queue
                if (inMatchmaking.Contains(mm))
                {
                    lock (inMatchmaking)
                    {
                        //and remove it
                        inMatchmaking.Remove(mm);
                    }
                }
            }
            //if the match is in team with less than 5 players
            else if (inTeamMatchmaking.Contains(mm))
            {
                //set ignore, no mistakes allowed
                mm.Ignore = true;

                //remove from here
                lock (inTeamMatchmaking)
                {
                    inTeamMatchmaking.Remove(mm);
                }

                //add to here
                lock (inMatchmaking)
                {
                    inMatchmaking.Add(mm);
                }
            }
        }

        public static void CalculateAfterMatch(Model.MatchData pMatchData)
        {
            //get the users and their MMR
            List<User> radiantPlayers = pMatchData.teams[0].players.Select(player => Mongo.Users.FindOneAs<User>(Query.EQ("steam.steamid", player.steam_id))).ToList();
            List<User> direPlayers = pMatchData.teams[1].players.Select(player => Mongo.Users.FindOneAs<User>(Query.EQ("steam.steamid", player.steam_id))).ToList();

            //avg the MMR
            double radiantAvg = radiantPlayers.Average(a => a.profile.mmr[pMatchData.mod]);
            double direAvg = direPlayers.Average(a => a.profile.mmr[pMatchData.mod]);

            //calculate probability to win
            double qa = Math.Pow(10, (radiantAvg / 400.0));
            double qb = Math.Pow(10, (direAvg / 400.0));
            double radiantWinProb = qa / (qa + qb);
            double direWinProb = qb / (qa + qb);

            //get factors for increment or decrement
            KFactor radiantFactor = KFactors.First(a => radiantAvg >= a.MinMmr && radiantAvg <= a.MaxMmr);
            KFactor direFactor = KFactors.First(a => direAvg >= a.MinMmr && direAvg <= a.MaxMmr);

            //calculate the increments and decrements based on win only
            int incRadiant = 0;
            int incDire = 0;
            if (pMatchData.good_guys_win)
            {
                incRadiant = (int)Math.Round(radiantFactor.Factor * (1.0 - radiantWinProb));
                incDire = (int)Math.Round(direFactor.Factor * -direWinProb);
            }
            else
            {
                incRadiant = (int)Math.Round(radiantFactor.Factor * -radiantWinProb);
                incDire = (int)Math.Round(direFactor.Factor * (1.0 - direWinProb));
            }

            //increment results
            radiantPlayers.ForEach(player => player.profile.mmr[pMatchData.mod] += incRadiant);
            direPlayers.ForEach(player => player.profile.mmr[pMatchData.mod] += incDire);

            //todo: add individual increment and/or decrement based on gameplay

            //check roof, floor and save
            foreach (var player in radiantPlayers.Union(direPlayers))
            {
                if (player.profile.mmr[pMatchData.mod] > MmrRoof)
                    player.profile.mmr[pMatchData.mod] = MmrRoof;

                if (player.profile.mmr[pMatchData.mod] < MmrFloor)
                    player.profile.mmr[pMatchData.mod] = MmrFloor;

                Mongo.Users.Save(player);
            }
        }
    }
}
