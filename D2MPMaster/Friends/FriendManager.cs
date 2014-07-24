// 
// FriendManager.cs
// Created by ilian000 on 2014-07-19
// Licenced under the Apache License, Version 2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using D2MPMaster.Database;
using MongoDB.Driver.Builders;
using D2MPMaster.Model;
using d2mpserver;
using D2MPMaster.LiveData;
using D2MPMaster.Browser;
using System.Reflection;
using XSockets.Core.Common.Socket.Event.Arguments;
using D2MPMaster.Lobbies;


namespace D2MPMaster.Friends
{
    class FriendManager : XSocketController, IDisposable
    {
        private static readonly BrowserController Browsers = new BrowserController();
        private static List<Friend> listQueue = new List<Friend>();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static volatile bool Registered = false;
        public static volatile bool shutdown = false;

        public static void buildList(BrowserController controller)
        {
            List<Friend> list = new List<Friend>();
            string queryUrl = String.Format("http://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={0}&steamid={1}&relationship=friend", Properties.Settings.Default.SteamWebAPIKey, controller.user.steam.steamid);
            using (WebClient c = new WebClient())
            {
                c.DownloadStringAsync(new Uri(queryUrl));
                c.DownloadStringCompleted += (s, e) =>
                {
                    try
                    {
                        dynamic result = JObject.Parse(e.Result);
                        List<MongoDB.Driver.IMongoQuery> queries = new List<MongoDB.Driver.IMongoQuery>();
                        foreach (var friend in result.friendslist.friends)
                        {
                            queries.Add(Query.EQ("steam.steamid", (string)friend.steamid));
                        }
                        var users = Mongo.Users.FindAs<User>(Query.Or(queries));
                        foreach (var friend in result.friendslist.friends)
                        {
                            var usr = users.Where(x => x.steam.steamid == (string)friend.steamid).FirstOrDefault();
                            list.Add(new Friend() {
                                id = (string)friend.steamid,
                                status = usr == null ? FriendStatus.NotRegistered : getFriendStatus((string)friend.steamid),
                                avatar = usr == null? null : (string)usr.steam.avatar 
                            });
                        }
                        controller.friendlist = list;
                        controller.Send(BrowserController.FriendsSnapshot(list));
                    }
                    catch (Exception ex)
                    {
                        log.Error("Could get build list.", ex);
                    }
                };
            }
        }

        private static FriendStatus getFriendStatus(string steamid)
        {
            // TODO: Add idle status
            var browser = Browsers.Find(m => m.user != null && m.user.steam.steamid == steamid).FirstOrDefault();
            if (browser != null)
            {
                if (browser.lobby != null)
                {
                    if (browser.lobby.status > LobbyStatus.Queue)
                        return FriendStatus.InGame;
                    else
                        return FriendStatus.InLobby;
                }
                else
                    return FriendStatus.Online;
            }
            else
                return FriendStatus.Offline;
        }

        public static void updateStatus(string steamid, FriendStatus status, string modname = null)
        {
            foreach (var friendBrowser in Browsers.Find(m => m.user != null && m.friendlist != null && m.friendlist.Any(x => x.id == steamid)))
            {
                lock (friendBrowser.friendlist)
                {
                    var friend = friendBrowser.friendlist.Find(f => f.id == steamid);
                    if (friend.status != status || friend.modname != modname)
                    {
                        List<String> fields = new List<string>();
                        if (status != friend.status)
                        {
                            friend.status = status;
                            fields.Add("status");
                        }
                        if (modname != null && modname != friend.modname)
                        {
                            friend.modname = modname;
                            fields.Add("modname");
                        }
                        TransmitFriendUpdate(friend, fields.ToArray());
                    }
                }
            }
        }

        public static void TransmitFriendUpdate(Friend friend, string[] fields)
        {
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray { friend.Update("friends", fields) };
            Browsers.AsyncSendTo(m => m.friendlist.Any(x => x.id == friend.id), new TextArgs(upd.ToString(Formatting.None), "friend"),
                req => { });
        }

        public static void InviteFriend(BrowserController c, string steamid)
        {
            Browsers.AsyncSendTo(m => m.user != null && m.user.steam.steamid == steamid, BrowserController.inviteFriend(c.lobby, c.user.steam.steamid), req => { });
        }

        public void Dispose()
        {
        }
    }
}
