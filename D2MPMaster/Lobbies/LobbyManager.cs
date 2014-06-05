using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using D2MPMaster.Browser;
using D2MPMaster.Client;
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

namespace D2MPMaster.Lobbies
{
    /// <summary>
    /// Manages the lifecycle of lobbies and the list.
    /// </summary>
    [XSocketMetadata("LobbyManager", Constants.GenericTextBufferSize, PluginRange.Internal)]
    public class LobbyManager : XSocketController
    {
        private static readonly ClientController ClientsController = new ClientController();
        private static readonly BrowserController Browsers = new BrowserController();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Lobbies that should be visible in the list.
        /// </summary>
        public static ConcurrentObservableCollection<Lobby> PublicLobbies = new ConcurrentObservableCollection<Lobby>(new List<Lobby>());

        /// <summary>
        /// Lobbies that should have full updates sent out JUST to users in them.
        /// </summary>
        public static ConcurrentObservableCollection<Lobby> PlayingLobbies = new ConcurrentObservableCollection<Lobby>(new List<Lobby>());

        public static List<Lobby> LobbyQueue = new List<Lobby>();

        public LobbyManager()
        {
            PublicLobbies.CollectionChanged += TransmitLobbiesChange;
        }

        public void TransmitLobbiesChange(object s, NotifyCollectionChangedEventArgs e)
        {
            var updates = new JArray();
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                updates.Add(DiffGenerator.RemoveAll("publicLobbies"));
            }
            else
            {
                if (e.NewItems != null)
                    foreach (var lobby in e.NewItems)
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Add:
                                updates.Add(lobby.Add("publicLobbies"));
                                break;
                        }
                    }
                if (e.OldItems != null)
                    foreach (var lobby in e.OldItems)
                    {
                        switch (e.Action)
                        {
                            case NotifyCollectionChangedAction.Remove:
                                updates.Add(lobby.Remove("publicLobbies"));
                                break;
                        }
                    }
            }
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = updates;
            var msg = upd.ToString(Formatting.None);
            //Browsers.SendTo(m => m.lobby == null, new TextArgs(msg, "lobby"));
            Browsers.AsyncSendToAll(new TextArgs(msg, "lobby"), ar => { });
        }

        /// <summary>
        /// See if a user is already in a lobby.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public static PlayerLocation FindPlayer(User user)
        {
            PlayerLocation plyr = null;
            string steamID = user.services.steam.steamid;
            foreach (var lobby in PlayingLobbies)
            {
                if (lobby.radiant.Any(player => player.steam == steamID))
                {
                    plyr = new PlayerLocation()
                           {
                               lobby = lobby,
                               goodguys = true
                           };
                }
                if (lobby.dire.Any(player => player.steam == steamID))
                {
                    plyr = new PlayerLocation()
                    {
                        lobby = lobby,
                        goodguys = false
                    };
                }
                if (plyr != null) break;
            }
            return plyr;
        }

        /// <summary>
        /// Exit the lobby queue.
        /// </summary>
        /// <param name="lobby"></param>
        public static void CancelQueue(Lobby lobby)
        {
            if (LobbyQueue.Contains(lobby))
            {
                LobbyQueue.Remove(lobby);
                lobby.status = LobbyStatus.Start;
                PublicLobbies.Add(lobby);
                TransmitLobbyUpdate(lobby, new []{"status"});
                CalculateQueue();
            }
        }

        public static void CalculateQueue()
        {

            foreach (var lobby in LobbyQueue.ToArray())
            {
                var server = ServerManager.FindForLobby(lobby);
                if (server == null) continue;
                GameInstance instance = server.StartInstance(lobby);
                lobby.status = LobbyStatus.Configure;
                TransmitLobbyUpdate(lobby, new []{"status"});
                SendLaunchDota(lobby);
                LobbyQueue.Remove(lobby);
            }
        }

        /// <summary>
        /// Enter the lobby queue.
        /// </summary>
        /// <param name="lobby"></param>
        public static void StartQueue(Lobby lobby)
        {
            if (!LobbyQueue.Contains(lobby))
            {
                LobbyQueue.Add(lobby);
                lobby.status = LobbyStatus.Queue;
                PublicLobbies.Remove(lobby);
                TransmitLobbyUpdate(lobby, new[]{"status"});
                CalculateQueue();
            }
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
                var updates = new JArray {lobby.Update("lobby", fields)};
                upd = new JObject();
                upd["msg"] = "colupd";
                upd["ops"] = updates;
                var msg = upd.ToString(Formatting.None);
                Browsers.AsyncSendTo(m => m.user != null, new TextArgs(msg, "lobby"), ar => { });
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
            Browsers.AsyncSendTo(m=>m.user!=null&&m.lobby!=null&&m.lobby.id==lob.id, BrowserController.ClearLobby(),
                req => { });
            PublicLobbies.Remove(lob);
            PlayingLobbies.Remove(lob);
        }

        public static void LeaveLobby(BrowserController controller)
        {
            if (controller.lobby == null || controller.user == null) return;
            var lob = controller.lobby;
            controller.lobby = null;
            controller.Send(BrowserController.ClearLobby());
            if (lob.status > LobbyStatus.Queue) return;
            //Find the player
            var team = RemoveFromTeam(lob, controller.user.services.steam.steamid);
            lob.status = LobbyStatus.Start;
            if(team != null)
                TransmitLobbyUpdate(lob, new[] { team, "status" });
            if ((lob.TeamCount(lob.dire) == 0 && lob.TeamCount(lob.radiant) == 0) || lob.creatorid == controller.user.Id)
            {
                CloseLobby(lob);
            }
        }

        public static void JoinLobby(Lobby lobby, User user, BrowserController controller)
        {
            if (lobby==null || user == null) return;
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
                ClientsController.AsyncSendTo(m=>m.SteamID==controller.user.services.steam.steamid, ClientController.SetMod(mod),
                    req => { });
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
                            deleted = false,
                            dire = new Player[5],
                            radiant = new Player[5],
                            devMode = false,
                            enableGG = true,
                            hasPassword = false,
                            id = Utils.RandomString(17),
                            mod = mod.Id,
                            region=0,
                            name = name,
                            isPublic = true,
                            password = "",
                            state = GameState.Init,
                            requiresFullLobby =
                                !(user.authItems != null &&
                                 (user.authItems.Contains("developer") || user.authItems.Contains("admin") ||
                                  user.authItems.Contains("moderator"))),
                            serverIP = ""
                        };
            lob.radiant[0] = Player.FromUser(user);
            PublicLobbies.Add(lob);
            PlayingLobbies.Add(lob);
            Browsers.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, BrowserController.LobbySnapshot(lob),
                req => { });
            ClientsController.AsyncSendTo(m => m.SteamID == user.services.steam.steamid, ClientController.SetMod(mod),
                req => { });
			log.InfoFormat("Lobby created, User: #{0}, Name: #{1}", user.profile.name, name);
            return lob;
        }

        public static void ChatMessage(Lobby lobby, string msg, string name)
        {
            string cmsg = name + ": " + msg;
            if (msg.Length == 0) return;
            if (msg.Length > 140) msg = msg.Substring(0, 140);
            Browsers.AsyncSendTo(m=>m.lobby!=null&&m.lobby.id==lobby.id, BrowserController.ChatMessage(cmsg), req => { });
        }

        public static void BanFromLobby(Lobby lobby, string steam)
        {
            var client =
                Browsers.Find(m => m.user != null && m.user.services.steam.steamid == steam && m.lobby.id == lobby.id);
            var browserClients = client as BrowserController[] ?? client.ToArray();
            if (!browserClients.Any()) return;
            if (!lobby.banned.Contains(steam))
            {
                var arr = lobby.banned;
                Array.Resize(ref arr, lobby.banned.Length+1);
                arr[arr.Length-1] = steam;
                lobby.banned = arr;
                TransmitLobbyUpdate(lobby, new []{"banned"});
            }
            LeaveLobby(browserClients.First());
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
            if (PlayingLobbies.Contains(instance.lobby))
            {
				log.Info("Lobby finished "+instance.lobby.id);
                CloseLobby(instance.lobby);
                CalculateQueue();
            }
        }

        public static void OnServerReady(GameInstance instance)
        {
            if (PlayingLobbies.Contains(instance.lobby))
			{
                var lobby = instance.lobby;
                lobby.serverIP = instance.Server.Address.Split(':')[0]+":"+instance.port;
                lobby.status = LobbyStatus.Play;
                TransmitLobbyUpdate(lobby, new []{"serverIP", "status"});
                SendConnectDota(lobby);
				log.Info("Server ready "+instance.lobby.id+" "+instance.lobby.serverIP);
            }
        }

        private static void SendLaunchDota(Lobby lobby)
        {
            foreach (var plyr in lobby.radiant)
            {
                if (plyr == null) continue;
                ClientsController.AsyncSendTo(c=>c.SteamID!=null&&c.SteamID==plyr.steam, ClientController.LaunchDota(),
                    req => { });
            }
            foreach (var plyr in lobby.dire)
            {
                if (plyr == null) continue;
                ClientsController.AsyncSendTo(c => c.SteamID != null && c.SteamID == plyr.steam, ClientController.LaunchDota(),
                    req => { });
            }
        }

        private static void SendConnectDota(Lobby lobby)
        {
            foreach (var plyr in lobby.radiant)
            {
                if (plyr == null) continue;
                ClientsController.AsyncSendTo(c => c.SteamID != null && c.SteamID == plyr.steam, ClientController.ConnectDota(lobby.serverIP),
                    req => { });
            }
            foreach (var plyr in lobby.dire)
            {
                if (plyr == null) continue;
                ClientsController.AsyncSendTo(c => c.SteamID != null && c.SteamID == plyr.steam, ClientController.ConnectDota(lobby.serverIP),
                    req => { });
            }
        }

        public static void LaunchAndConnect(Lobby lobby, string steamid)
        {
            ClientsController.AsyncSendTo(m => m.SteamID == steamid, ClientController.LaunchDota(), req => { });
            ClientsController.AsyncSendTo(m => m.SteamID == steamid, ClientController.ConnectDota(lobby.serverIP),
                req => { });
        }
    }
}
