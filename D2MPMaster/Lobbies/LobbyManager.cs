using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Amazon.DataPipeline.Model;
using D2MPMaster.Browser;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.Model;
using D2MPMaster.Server;
using d2mpserver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XSockets.Core.Common.Globals;
using XSockets.Core.Common.Socket;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using PluginRange = XSockets.Plugin.Framework.PluginRange;
using Query = MongoDB.Driver.Builders.Query;

namespace D2MPMaster.Lobbies
{
    /// <summary>
    /// Manages the lifecycle of lobbies and the list.
    /// </summary>
    [XSocketMetadata("LobbyManager", Constants.GenericTextBufferSize, PluginRange.Internal)]
    public class LobbyManager : XSocketController, IDisposable
    {
        private static readonly ClientController ClientsController = new ClientController();
        private static readonly BrowserController Browsers = new BrowserController();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Lobbies that should be visible in the list.
        /// </summary>
        public static ObservableCollection<Lobby> PublicLobbies = new ObservableCollection<Lobby>();

        /// <summary>
        /// Lobbies that should have full updates sent out JUST to users in them.
        /// </summary>
        public static ObservableCollection<Lobby> PlayingLobbies = new ObservableCollection<Lobby>();

        //Test lobbies being batched together before being put into the main lobby queue
        public static List<Lobby> TestLobbyQueue = new List<Lobby>();

        public static ConcurrentDictionary<string,Lobby> LobbyID = new ConcurrentDictionary<string,Lobby>(); 

        public static volatile bool Registered = false;

        public static List<Lobby> LobbyQueue = new List<Lobby>();

        public static Queue<JObject> PublicLobbyUpdateQueue = new Queue<JObject>();
        public static Thread LobbyUpdateThread;
        public static Thread CalculateQueueThread;
        public static Thread IdleLobbyThread;
        public static Thread TestLobbyThread;

        public static volatile bool shutdown = false;

        public LobbyManager()
        {
            if (!Registered)
            {
                Registered = true;
                IdleLobbyThread = new Thread(IdleLobbyProc);
                IdleLobbyThread.Start();
                LobbyUpdateThread = new Thread(LobbyUpdateProc);
                LobbyUpdateThread.Start();
                CalculateQueueThread = new Thread(CalculateQueueT);
                CalculateQueueThread.Start();
                TestLobbyThread = new Thread(TestLobbyProc);
                TestLobbyThread.Start();
                PublicLobbies.CollectionChanged += TransmitLobbiesChange;
                PlayingLobbies.CollectionChanged += UpdateLobbyIDDict;
            }
        }

        private void UpdateLobbyIDDict(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                LobbyID.Clear();
                return;
            }
            if(e.NewItems != null)
            foreach (Lobby lobby in e.NewItems)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        LobbyID[lobby.id] = lobby;
                        break;
                }
            }
            if(e.OldItems != null)
            foreach (Lobby lobby in e.OldItems)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Remove:
                        Lobby res;
                        LobbyID.TryRemove(lobby.id, out res);
                        break;
                }
            }
        }

        public void Dispose()
        {
            shutdown = true;
            Registered = false;
        }

        public void TransmitLobbiesChange(object s, NotifyCollectionChangedEventArgs e)
        {
            if (shutdown) return;
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                PublicLobbyUpdateQueue.Enqueue(DiffGenerator.RemoveAll("publicLobbies"));
            }
            else
            {
                if (e.NewItems != null)
                    foreach (var lobby in e.NewItems)
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                PublicLobbyUpdateQueue.Enqueue(lobby.Add("publicLobbies"));
                                break;
                        }
                    }
                if (e.OldItems != null)
                    foreach (var lobby in e.OldItems)
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Remove:
                                PublicLobbyUpdateQueue.Enqueue(lobby.Remove("publicLobbies"));
                                break;
                        }
                    }
            }

        }

        public void TestLobbyProc()
        {
            while (!shutdown)
            {
                Thread.Sleep(30000);
                lock (TestLobbyQueue)
                {
                    lock (LobbyQueue)
                    {
                        foreach (var lobby in TestLobbyQueue)
                        {
                            LobbyQueue.Add(lobby);
                            lobby.status = LobbyStatus.Queue;
                            TransmitLobbyUpdate(lobby, new []{"status"});
                        }
                        TestLobbyQueue.Clear();
                    }
                }
            }
        }

        public void LobbyUpdateProc()
        {
            while (!shutdown)
            {
                Thread.Sleep(500);
                var upd = new JObject();
                var updates = new JArray();
                while (PublicLobbyUpdateQueue.Count > 0)
                {
                    var update = PublicLobbyUpdateQueue.Dequeue();
                    if (update == null) continue;
                    updates.Add(update);
                }
                if (updates.Count == 0) continue;
                upd["msg"] = "colupd";
                upd["ops"] = updates;
                var msg = upd.ToString(Formatting.None);
                Browsers.AsyncSendTo(m=>m.user!=null&&m.lobby==null,new TextArgs(msg, "publicLobbies"), ar => { });
            }
        }

        public void IdleLobbyProc()
        {
            while (!shutdown)
            {
                Thread.Sleep(20000);
                var lobbies =
                    LobbyID.Values.Where(
                        m =>
                            m.status == LobbyStatus.Start &&
                            !m.hasPassword &&
						m.IdleSince < DateTime.Now.Subtract(TimeSpan.FromMinutes(3)));
                foreach (var lobby in lobbies)
                {
                    CloseLobby(lobby); 
                    log.DebugFormat("Cleared lobby {0} for inactivity.", lobby.id);
                }
                //All playing lobbies with no server with an instance that has the lobby
                lobbies =
                    LobbyID.Values.Where(
                        m => m.status==LobbyStatus.Play&&!ServerService.Servers.Find(z => z.Instances.Any(f => f.Value.lobby.id == m.id)).Any());
                foreach (var lobby in lobbies)
                {
                    CloseLobby(lobby);
                    log.DebugFormat("Cleared orphan lobby {0}.", lobby.id);
                }
            }
        }

        public void CalculateQueueT()
        {
            while (!shutdown)
            {
                Thread.Sleep(500);
                CalculateQueue();
            }
        }

        /// <summary>
        /// See if a user is already in a lobby.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static PlayerLocation FindPlayer(User user)
        {
            return PlayingLobbies.Select(lobby => FindPlayerLocation(user, lobby)).FirstOrDefault(loc => loc != null);
        }

        public static PlayerLocation FindPlayerLocationU(string userid, Lobby lobby)
        {
            User user = Mongo.Users.FindOneAs<User>(Query.EQ("_id", userid));
            if (user == null) return null;
            return FindPlayerLocation(user, lobby);
        }

        public static PlayerLocation FindPlayerLocation(User user, Lobby lobby)
        {
            Player plyr;
            plyr = lobby.radiant.FirstOrDefault(player => player.steam == user.steam.steamid);
            if (plyr != null)
            {
                return new PlayerLocation()
                {
                    lobby = lobby,
                    goodguys = true,
                    player = plyr
                };
            }
            plyr = lobby.dire.FirstOrDefault(player => player.steam == user.steam.steamid);
            if (plyr != null)
            {
                return new PlayerLocation()
                {
                    lobby = lobby,
                    goodguys = true,
                    player = plyr
                };
            }
            return null;
        }

        /// <summary>
        /// Exit the lobby queue.
        /// </summary>
        /// <param name="lobby"></param>
        public static void CancelQueue(Lobby lobby)
        {
            if(lobby == null) return;
            lock (LobbyQueue)
            {
                if (!LobbyQueue.Contains(lobby)) return;
                LobbyQueue.Remove(lobby);
            }
            lobby.status = LobbyStatus.Start;
            lobby.IdleSince = DateTime.Now;
            if(lobby.isPublic)
                lock(PublicLobbies)
                    PublicLobbies.Add(lobby);
            TransmitLobbyUpdate(lobby, new []{"status"});
        }

        private static void CalculateQueue()
        {
            lock (LobbyQueue)
            {
                foreach (var lobby in LobbyQueue.ToArray())
                {
                    var server = ServerService.FindForLobby(lobby);
                    if (server == null) continue;
                    GameInstance instance = server.StartInstance(lobby);
                    lobby.status = LobbyStatus.Configure;
                    TransmitLobbyUpdate(lobby, new[] {"status"});
                    LobbyQueue.Remove(lobby);
                }
            }
        }

        /// <summary>
        /// Enter the lobby queue.
        /// </summary>
        /// <param name="lobby"></param>
        public static void StartQueue(Lobby lobby)
        {
            lock (LobbyQueue)
            {
                if (!LobbyQueue.Contains(lobby))
                {
                    lobby.status = LobbyStatus.Queue;
                    lock (PublicLobbies)
                        PublicLobbies.Remove(lobby);
                    TransmitLobbyUpdate(lobby, new[] {"status"});
                    SendLaunchDota(lobby);
                    LobbyQueue.Add(lobby);
                }
            }
        }

        public static void SetPassword(Lobby lobby, string password)
        {
            if(lobby == null) return;
            var hadPassword = lobby.hasPassword;
            if (password == null) password = "";

            if (password == "")
            {
                lobby.hasPassword = false;
                lobby.isPublic = true;
                lobby.password = "";
                lock(PublicLobbies)
                    if (!PublicLobbies.Contains(lobby)) PublicLobbies.Add(lobby);
            }
            else
            {
                lobby.hasPassword = true;
                lobby.isPublic = false;
                lobby.password = password;
                lock(PublicLobbies)
                    if (PublicLobbies.Contains(lobby)) PublicLobbies.Remove(lobby);
            }
			if(hadPassword != lobby.hasPassword)
                TransmitLobbyUpdate(lobby, new []{"isPublic","hasPassword"});
			lobby.IdleSince = DateTime.Now;
        }

        public static void TransmitLobbyUpdate(Lobby lobby, string[] fields)
        {
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {lobby.Update("lobbies", fields)};
            Browsers.AsyncSendTo(m=>m.lobby!=null&&m.lobby.id==lobby.id, new TextArgs(upd.ToString(Formatting.None), "lobby"),
                req => { });
            if (PublicLobbies.Contains(lobby))
            {
                PublicLobbyUpdateQueue.Enqueue(lobby.Update("publicLobbies", fields));
            }
        }

        public static string RemoveFromTeam(Lobby lob, string steamid)
        {
            for (int i = 0; i < lob.radiant.Length; i++)
            {
                var plyr = lob.radiant[i];
                if (plyr != null && plyr.steam == steamid)
                {
                    lob.radiant[i] = null;
                    return "radiant";
                }
            }
            for (int i = 0; i < lob.dire.Length; i++)
            {
                var plyr = lob.dire[i];
                if (plyr != null && plyr.steam == steamid)
                {
                    lob.dire[i] = null;
                    return "dire";
                }
            }
            return null;
        }

        public static void CloseLobby(Lobby lob)
        {
            foreach (var browser in Browsers.Find(m => m.user != null && m.lobby != null && m.lobby.id == lob.id))
            {
                browser.lobby = null;
            }
            lock (PublicLobbies)
                PublicLobbies.Remove(lob);
            lock(PlayingLobbies)
                PlayingLobbies.Remove(lob);
            lock (LobbyQueue)
                LobbyQueue.Remove(lob);
        }

        public static void LeaveLobby(BrowserController controller)
        {
            if (controller.lobby == null || controller.user == null) return;
            var lob = controller.lobby;
            controller.lobby = null;
            if (lob.status > LobbyStatus.Queue) return;
            //Find the player
            var team = RemoveFromTeam(lob, controller.user.steam.steamid);
            lob.status = LobbyStatus.Start;
            if(team != null)
                TransmitLobbyUpdate(lob, new[] { team, "status" });
            if ((lob.TeamCount(lob.dire) == 0 && lob.TeamCount(lob.radiant) == 0) || lob.creatorid == controller.user.Id)
            {
                CloseLobby(lob);
            }
        }

        public static void ForceLeaveLobby(BrowserController controller)
        {
            if (controller.lobby == null || controller.user == null) return;
            var lob = controller.lobby;
            controller.lobby = null;
            if (lob.LobbyType == LobbyType.PlayerTest)
            {
                RemoveFromTeam(lob, controller.user.steam.steamid);
                if (lob.TeamCount(lob.radiant) + lob.TeamCount(lob.dire) == 0)
                {
                    lock (TestLobbyQueue)
                    {
                        lock (PlayingLobbies)
                        {
                            TestLobbyQueue.Remove(lob);
                            PlayingLobbies.Remove(lob);
                            LobbyQueue.Remove(lob);
                            return;
                        }
                    }
                }
                else
                {
                    lock (LobbyQueue)
                    {
                        if (LobbyQueue.Contains(lob))
                        {
                            LobbyQueue.Remove(lob);
                            lock(TestLobbyQueue)
                                TestLobbyQueue.Add(lob);
                            lob.status = LobbyStatus.Start;
                            TransmitLobbyUpdate(lob, new []{"status"});
                        }
                    }
                }           
            }
            if (lob.status > LobbyStatus.Queue) return; //will be auto handled later
            //Find the player
            var team = RemoveFromTeam(lob, controller.user.steam.steamid);
            CancelQueue(lob);
            if (team != null)
                TransmitLobbyUpdate(lob, new[] { team });
            if ((lob.TeamCount(lob.dire) == 0 && lob.TeamCount(lob.radiant) == 0) || lob.creatorid == controller.user.Id)
            {
                CloseLobby(lob);
            }
        }

        public static void JoinLobby(Lobby lobby, User user, BrowserController controller)
        {
            if (lobby==null || user == null) return;
            foreach (var result in Browsers.Find(m => m.user != null && m.user.Id == user.Id && m.lobby!=null))
            {
                LeaveLobby(result);
            }
            var direCount = lobby.TeamCount(lobby.dire);
            var radCount = lobby.TeamCount(lobby.radiant);
            if (direCount >= 5 && radCount >= 5) return;
            if (direCount < radCount || direCount == radCount)
            {
                lobby.AddPlayer(lobby.dire, Player.FromUser(user));
            }
            else
            {
                lobby.AddPlayer(lobby.radiant, Player.FromUser(user));
            }
            controller.lobby = lobby;
            Browsers.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, BrowserController.LobbySnapshot(lobby),
                req => { });
            TransmitLobbyUpdate(lobby, new []{"radiant", "dire"});
            var mod = Mods.Mods.ByID(lobby.mod);
            if (mod != null)
                ClientsController.SetMod(controller.user.steam.steamid, mod);
            ClientsController.AsyncSendTo(m => m.SteamID == user.steam.steamid, ClientController.LaunchDota(), req => { });
        }

        /// <summary>
        /// Create a new lobby.
        /// </summary>
        /// <param name="user">User creating the lobby.</param>
        /// <param name="mod">Mod.</param>
        /// <param name="name">Name of the lobby.</param>
        /// <returns></returns>
        public static Lobby CreateLobby(User user, Mod mod, string name)
        {
            /*
            foreach (var result in Browsers.Find(m => m.user != null && m.user.Id == user.Id && m.lobby!=null))
            {
                LeaveLobby(result);
            }
            */

            //Filter lobby name to alphanumeric only
            name = Regex.Replace(name, "^[\\w \\.\"'[]\\{\\}\\(\\)]+", "");
            //Constrain lobby name length to 40 characters
            if (name.Length > 40)
            {
                name = name.Substring(0, 40);
            }

            var lob = new Lobby()
                        {
                            creator = user.profile.name,
                            creatorid = user.Id,
                            banned = new string[0],
                            dire = new Player[5],
                            IdleSince = DateTime.Now,
                            radiant = new Player[5],
                            devMode = false,
                            enableGG = true,
                            hasPassword = false,
                            id = Utils.RandomString(17),
                            mod = mod.Id,
							region=(int)ServerRegion.UNKNOWN,
                            name = name,
                            isPublic = true,
							password = string.Empty,
                            state = GameState.Init,
							LobbyType = LobbyType.Normal,
                            requiresFullLobby =
                                !(user.authItems != null &&
                                 (user.authItems.Contains("developer") || user.authItems.Contains("admin") ||
                                  user.authItems.Contains("moderator"))),
							serverIP = string.Empty
                        };
            lob.radiant[0] = Player.FromUser(user);
            lock(PublicLobbies)
                PublicLobbies.Add(lob);
            lock(PlayingLobbies)
                PlayingLobbies.Add(lob);
            Browsers.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, BrowserController.LobbySnapshot(lob),
                req => { });
            ClientsController.SetMod(user.steam.steamid, mod);
            ClientsController.AsyncSendTo(m => m.SteamID == user.steam.steamid, ClientController.LaunchDota(), req => { });
            log.InfoFormat("Lobby created, User: #{0}, Name: #{1}, Id: {2}", user.profile.name, name, user.Id);
            return lob;
        }

        /// <summary>
        /// Start a load test
        /// </summary>
        /// <param name="user">User creating the lobby.</param>
        /// <param name="mod">Mod.</param>
        /// <param name="name">Name of the lobby.</param>
        /// <returns></returns>
        public static Lobby CreateTestLobby(User user)
        {
            foreach (var result in Browsers.Find(m => m.user != null && m.user.Id == user.Id && m.lobby != null))
            {
                LeaveLobby(result);
            }
            var mod = Mods.Mods.ByName("checker");
            var lob = new Lobby()
            {
                creator = user.profile.name,
                creatorid = user.Id,
                banned = new string[0],
                dire = new Player[5],
                IdleSince = DateTime.Now,
                radiant = new Player[5],
                devMode = false,
                enableGG = true,
                hasPassword = false,
                id = Utils.RandomString(17),
                mod = mod.Id,
                region = (int)ServerRegion.UNKNOWN,
                name = "LOADTEST "+user.Id,
                isPublic = false,
                password = string.Empty,
                state = GameState.Init,
                LobbyType = LobbyType.PlayerTest,
                requiresFullLobby = false,
                status = LobbyStatus.Start,
                serverIP = string.Empty
            };
            lob.radiant[0] = Player.FromUser(user);
            lock (PlayingLobbies)
            {
                PlayingLobbies.Add(lob);
            }
            Browsers.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, BrowserController.LobbySnapshot(lob),
                req => { });
            ClientsController.SetMod(user.steam.steamid, mod);
            ClientsController.AsyncSendTo(m => m.SteamID == user.steam.steamid, ClientController.LaunchDota(), req => { });
            log.InfoFormat("Load test lobby created w/ user: #{0}", user.profile.name);
            return lob;
        }

        public static Lobby StartPlayerTest(User user)
        {
            //Find a lobby that isn't full
            lock(TestLobbyQueue){
                var lobby = TestLobbyQueue.FirstOrDefault(m=>m.LobbyType==LobbyType.PlayerTest&&m.TeamCount(m.radiant)+m.TeamCount(m.dire)<10);
                if(lobby != null){
                    var direCount = lobby.TeamCount(lobby.dire);
                    var radCount = lobby.TeamCount(lobby.radiant);
                    if (direCount < radCount || direCount == radCount)
                    {
                        lobby.AddPlayer(lobby.dire, Player.FromUser(user));
                    }
                    else
                    {
                        lobby.AddPlayer(lobby.radiant, Player.FromUser(user));
                    }
                    Browsers.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, BrowserController.LobbySnapshot(lobby),
                        req => { });
                    ClientsController.SetMod(user.steam.steamid, Mods.Mods.ByName("checker"));
                    ClientsController.AsyncSendTo(m => m.SteamID == user.steam.steamid, ClientController.LaunchDota(), req => { });
                    return lobby;
                }else{
                    lobby = CreateTestLobby(user);
                    TestLobbyQueue.Add(lobby);
                    return lobby;
                }
            }
        }

        public static void ChatMessage(Lobby lobby, string msg, string name)
        {
            string cmsg = name + ": " + msg;
            if (msg.Length == 0) return;
            if (msg.Length > 140) msg = msg.Substring(0, 140);
            Browsers.AsyncSendTo(m=>m.lobby!=null&&m.lobby.id==lobby.id, BrowserController.ChatMessage(cmsg), req => { });
        }

        public static bool BanFromLobby(Lobby lobby, string steam)
        {
            var client =
                Browsers.Find(m => m.user != null && m.user.steam.steamid == steam && m.lobby!=null&&m.lobby.id == lobby.id);
            var browserClients = client as BrowserController[] ?? client.ToArray();
            if (!browserClients.Any()) return;
            if (browserClients.First().user.authItems.Contains("admin"))
            {
                return false;
            }
            if (!lobby.banned.Contains(steam))
            {
                var arr = lobby.banned;
                Array.Resize(ref arr, lobby.banned.Length+1);
                arr[arr.Length-1] = steam;
                lobby.banned = arr;
                TransmitLobbyUpdate(lobby, new []{"banned"});
            }
            LeaveLobby(browserClients.First());
            return true;
        }

        public static void SetTitle(Lobby lobby, string name)
        {
            lobby.name = name;
            TransmitLobbyUpdate(lobby, new[] { "name" });
        }

        public static void SetRegion(Lobby lobby, ServerRegion region)
        {
            lobby.region = region;
            TransmitLobbyUpdate(lobby, new[] { "region" });
        }

        public static void OnServerShutdown(GameInstance instance)
        {
            log.Info("Server shutdown: " + instance.lobby.id);
            if (!LobbyID.Values.Contains(instance.lobby) || instance.lobby.status == LobbyStatus.Start) return;
            if (instance.lobby.LobbyType == LobbyType.PlayerTest)
            {
                log.Error("No match result info for test lobby, setting all to success.");
                foreach (var browser in instance.lobby.radiant.Where(player => player != null).Select(player => Browsers.Find(m => m.user != null && m.user.steam.steamid == player.steam).FirstOrDefault()).Where(browser => browser != null))
                {
                    browser.SetTested(true);
                }
                foreach (var browser in instance.lobby.dire.Where(player => player != null).Select(player => Browsers.Find(m => m.user != null && m.user.steam.steamid == player.steam).FirstOrDefault()).Where(browser => browser != null))
                {
                    browser.SetTested(true);
                }
            }else if (instance.lobby.LobbyType == LobbyType.Normal)
            {
                log.Error("No match result info for regular lobby, returning to wait.");
                ReturnToWait(instance.lobby);
            }else
                CloseLobby(instance.lobby);
        }

        public static void OnServerReady(GameInstance instance)
        {
            if (!PlayingLobbies.Contains(instance.lobby)) return;
            var lobby = instance.lobby;
            lobby.serverIP = instance.Server.Address.Split(':')[0]+":"+instance.port;
            lobby.status = LobbyStatus.Play;
            TransmitLobbyUpdate(lobby, new []{"status"});
            foreach (var player in lobby.radiant)
            {
                if (player == null) continue;
                player.failedConnect = false;
            }
            foreach (var player in lobby.dire)
            {
                if (player == null) continue;
                player.failedConnect = false;
            }
            SendLaunchDota(lobby);
            SendConnectDota(lobby);
            log.Info("Server ready "+instance.lobby.id+" "+instance.lobby.serverIP);
        }

        public static void ReturnToWait(Lobby lobby)
        {
            if (lobby==null||!LobbyID.Values.Contains(lobby)) return;
            lobby.serverIP = "";
            lobby.status = LobbyStatus.Start;
            lobby.IdleSince = DateTime.Now;
            //Check that all members are still connected
            var radiant = new List<Player>(5);
            var dire = new List<Player>(5);
            radiant.AddRange(from plyr in lobby.radiant
                where plyr != null
                let hasBrowser = Browsers.Find(m => m.user != null && m.lobby != null && m.user.steam.steamid == plyr.steam && m.lobby.id == lobby.id).Any()
                where hasBrowser
                select plyr);
            dire.AddRange(from plyr in lobby.dire
                where plyr != null
                let hasBrowser = Browsers.Find(m => m.user != null && m.lobby != null && m.user.steam.steamid == plyr.steam && m.lobby.id == lobby.id).Any()
                where hasBrowser
                select plyr);
            Player[] radiantt = new Player[5];
            Player[] diret = new Player[5];
            var i = 0;
            foreach (var player in radiant)
            {
                radiantt[i] = player;
                i++;
            }
            i = 0;
            foreach (var player in dire)
            {
                diret[i] = player;
                i++;
            }
            lobby.radiant = radiantt;
            lobby.dire = diret;
            if (FindPlayerLocationU(lobby.creatorid, lobby) == null)
            {
                CloseLobby(lobby);
            }
            else
            {
                TransmitLobbyUpdate(lobby, new[] {"status", "radiant", "dire"});
                lock(PublicLobbies)
                    if (lobby.isPublic) PublicLobbies.Add(lobby);
            }
        }

        private static void SendLaunchDota(Lobby lobby)
        {
            foreach (var plyr in lobby.radiant.Where(plyr => plyr != null))
            {
                ClientsController.AsyncSendTo(c=>c.SteamID!=null&&c.SteamID==plyr.steam, ClientController.LaunchDota(),
                    req => { });
            }
            foreach (var plyr in lobby.dire.Where(plyr => plyr != null))
            {
                ClientsController.AsyncSendTo(c => c.SteamID != null && c.SteamID == plyr.steam, ClientController.LaunchDota(),
                    req => { });
			}
        }

        private static void SendConnectDota(Lobby lobby)
        {
            foreach (var plyr in lobby.radiant.Where(plyr => plyr != null))
            {
                ClientsController.ConnectDota(plyr.steam, lobby.serverIP);
            }
            foreach (var plyr in lobby.dire.Where(plyr => plyr != null))
            {
                ClientsController.ConnectDota(plyr.steam, lobby.serverIP);
            }
        }

        public static void LaunchAndConnect(Lobby lobby, string steamid)
        {
            ClientsController.AsyncSendTo(m => m.SteamID == steamid, ClientController.LaunchDota(), req => { });
            ClientsController.ConnectDota(steamid, lobby.serverIP);
        }

        public static void OnMatchComplete(Model.MatchData toObject)
        {
            Lobby lob;
            if (!LobbyID.TryGetValue(toObject.match_id, out lob)) return;
            log.Debug("Match completed, "+lob.id);
            if(lob.LobbyType == LobbyType.Normal){
                CloseLobby(lob);
                try
                {
                    Mongo.Results.Insert(toObject);
                }
                catch (Exception ex)
                {
                    log.Error("Failed to store match result " + lob.id, ex);
                }
            }
            else if (lob.LobbyType == LobbyType.PlayerTest)
            {
                foreach (var browser in lob.radiant.Where(player => player != null).Select(player => Browsers.Find(m => m.user != null && m.user.steam.steamid == player.steam).FirstOrDefault()).Where(browser => browser != null))
                {
                    browser.SetTested(true);
                }
                foreach (var browser in lob.dire.Where(player => player != null).Select(player => Browsers.Find(m => m.user != null && m.user.steam.steamid == player.steam).FirstOrDefault()).Where(browser => browser != null))
                {
                    browser.SetTested(true);
                }
                CloseLobby(lob);
            }
        }

        public static void OnLoadFail(string matchid, JArray failedPlayers)
        {
            Lobby lob;
            if (!LobbyID.TryGetValue(matchid, out lob)) return;
            if (lob.status != LobbyStatus.Play) return;
            var failed = new List<string>(10);
            failed.AddRange(failedPlayers.Select(player => player.Value<int>().ToSteamID64()));
            foreach(var player in lob.radiant){
                if(player == null) continue;
                player.failedConnect = failed.Contains(player.steam);
            }
            foreach(var player in lob.dire){
                if(player == null) continue;
                player.failedConnect = failed.Contains(player.steam);
            }
            if(lob.LobbyType == LobbyType.Normal)
            {
                log.Debug(matchid + " failed to load, returning to waiting stage.");
                foreach (var steam in failed)
                {
                    var browser = Browsers.Find(m => m.user != null && m.user.steam.steamid == steam).FirstOrDefault();
                    if (browser != null)
                    {
                        browser.SetTested(false);
                        log.Debug(matchid + " -> marked " + steam + " as FAIL");
                    }
                }
                ReturnToWait(lob);
            }
            else if (lob.LobbyType == LobbyType.PlayerTest)
            {
                foreach (var player in lob.radiant)
                {
                    if (player == null) continue;
                    var browser = Browsers.Find(m => m.user != null && m.user.steam.steamid == player.steam).FirstOrDefault();
                    if (browser != null)
                    {
                        browser.SetTested(!player.failedConnect);
                    }
                }
                foreach (var player in lob.dire)
                {
                    if (player == null) continue;
                    var browser = Browsers.Find(m => m.user != null && m.user.steam.steamid == player.steam).FirstOrDefault();
                    if (browser != null)
                    {
                        browser.SetTested(!player.failedConnect);
                    }
                }
                CloseLobby(lob);
            }
        }

        public static void HandleEvent(GameEvents eventType, JToken data, Lobby lob)
        {
            switch (eventType)
            {
                case GameEvents.GameStateChange:
                {
                    var gameState = (GameState) data.Value<int>("new_state");
                    log.Debug(lob.id + " -> entered state " + Enum.GetName(typeof(GameState), gameState));
                    lob.state = gameState;
                    TransmitLobbyUpdate(lob, new []{"state"});
                    break;
                }
                case GameEvents.PlayerConnect:
                {
                    var plyr =
                        FindPlayerLocation(
                            new User() {steam = new SteamService() {steamid = data.Value<int>("player").ToSteamID64()}},
                            lob);
                    if (plyr != null)
                    {
                        log.Debug(lob.id + " -> player connected: " + plyr.player.name);
                    }
                    break;
                }
                case GameEvents.PlayerDisconnect:
                {
                    var plyr =
                        FindPlayerLocation(
                            new User()
                            {
                                steam =
                                    new SteamService()
                                    {
                                        steamid = data.Value<int>("player").ToSteamID64()
                                    }
                            }, lob);
                    if (plyr != null)
                    {
                        log.Debug(lob.id + " -> player disconnected: " + plyr.player.name);
                    }
                    break;
                }
            }
        }

        public static void ClearPendingLobbies()
        {
			log.Info("Clearing idle lobbies!");
			try{
    			var lobbies = LobbyID.Values.Where(m => m.status == LobbyStatus.Start);
                foreach (var lobby in lobbies)
                {
                    CloseLobby(lobby);
                }
                log.Info("Cleared "+lobbies.Count()+" lobbies.");
			}catch(Exception ex){
				log.Error("Failed to clear idle lobbies!", ex);
			}
        }
    }
}
