using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using D2MPMaster.Browser;
using D2MPMaster.Browser.Methods;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.Model;
using D2MPMaster.Server;
using d2mpserver;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace D2MPMaster.Lobbies
{
    /// <summary>
    /// Manages the lifecycle of lobbies and the list.
    /// </summary>
    public class LobbyManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Lobbies that should be visible in the list.
        /// </summary>
        public ConcurrentObservableCollection<Lobby> PublicLobbies = new ConcurrentObservableCollection<Lobby>(new List<Lobby>());

        /// <summary>
        /// Lobbies that should have full updates sent out JUST to users in them.
        /// </summary>
        public ConcurrentObservableCollection<Lobby> PlayingLobbies = new ConcurrentObservableCollection<Lobby>(new List<Lobby>());

        public List<Lobby> LobbyQueue = new List<Lobby>();

        /// <summary>
        /// See if a user is already in a lobby.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public PlayerLocation FindPlayer(User user)
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
        public void CancelQueue(Lobby lobby)
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

        public void CalculateQueue()
        {

            foreach (var lobby in LobbyQueue.ToArray())
            {
                var server = Program.Server.FindForLobby(lobby);
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
        public void StartQueue(Lobby lobby)
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

        public void TransmitLobbyUpdate(Lobby lobby, string[] fields)
        {
            foreach (var plyr in lobby.radiant.Where(plyr => plyr != null))
            {
                Program.Browser.TransmitLobbyUpdate(plyr.steam, lobby, fields);
            }
            foreach (var plyr in lobby.dire.Where(plyr => plyr != null))
            {
                Program.Browser.TransmitLobbyUpdate(plyr.steam, lobby, fields);
            }
            if (lobby.status == 0 && lobby.isPublic)
            {
                Program.Browser.TransmitPublicLobbiesUpdate(new List<Lobby> { lobby }, fields);
            }
        }

        public string RemoveFromTeam(Lobby lob, string steamid)
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

        public void CloseLobby(Lobby lob)
        {
            foreach (var client in from plyr in lob.radiant where plyr != null where Program.Browser.UserClients.ContainsKey(plyr.steam) select Program.Browser.UserClients[plyr.steam] into client where client != null select client)
            {
                client.lobby = null;
                client.SendClearLobby(null);
            }
            foreach (var client in from plyr in lob.dire where plyr != null where Program.Browser.UserClients.ContainsKey(plyr.steam) select Program.Browser.UserClients[plyr.steam] into client where client != null select client)
            {
                client.lobby = null;
                client.SendClearLobby(null);
            }
            PublicLobbies.Remove(lob);
            PlayingLobbies.Remove(lob);
        }

        public void LeaveLobby(BrowserClient client)
        {
            if (client.lobby == null || client.user == null) return;
            var lob = client.lobby;
            client.lobby = null;
            client.SendClearLobby(null);
            if (lob.status > LobbyStatus.Queue) return;
            //Find the player
            var team = RemoveFromTeam(lob, client.user.services.steam.steamid);
            lob.status = LobbyStatus.Start;
            if(team != null)
                TransmitLobbyUpdate(lob, new[] { team, "status" });
            if ((lob.TeamCount(lob.dire) == 0 && lob.TeamCount(lob.radiant) == 0) || lob.creatorid == client.user.Id)
            {
                CloseLobby(lob);
            }
        }

        public void JoinLobby(Lobby lobby, User user, BrowserClient client)
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
            client.lobby = lobby;
            Program.Browser.TransmitLobbySnapshot(user.services.steam.steamid, lobby);
            TransmitLobbyUpdate(lobby, new []{"radiant", "dire"});
            var mod = Mods.Mods.ByID(lobby.mod);
            if (mod != null && Program.Client.ClientUID.ContainsKey(user.Id))
            {
                var mclient = Program.Client.ClientUID[user.Id];
                mclient.SetMod(mod);
            }
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
            Program.LobbyManager.PublicLobbies.Add(lob);
            Program.LobbyManager.PlayingLobbies.Add(lob);
            Program.Browser.TransmitLobbySnapshot(user.services.steam.steamid, lob);
            Program.Client.ClientUID[user.Id].SetMod(mod);
			log.InfoFormat("Lobby created, User: #{0}, Name: #{1}", user.profile.name, name);
            return lob;
        }

        public void ChatMessage(Lobby lobby, string msg, string name)
        {
            string cmsg = name + ": " + msg;
            if (msg.Length == 0) return;
            if (msg.Length > 140) msg = msg.Substring(0, 140);
            var cmd = new JObject();
            cmd["msg"] = "chat";
            cmd["message"] = cmsg;
            var data = cmd.ToString(Formatting.None);
            foreach (var client in lobby.dire.Where(e=>e!=null).Select(plyr => Program.Browser.UserClients[plyr.steam]).Where(client => client != null))
            {
                client.Send(data);
            }
            foreach (var client in lobby.radiant.Where(e => e != null).Select(plyr => Program.Browser.UserClients[plyr.steam]).Where(client => client != null))
            {
                client.Send(data);
            }
        }

        public void BanFromLobby(Lobby lobby, string steam)
        {
            var client = Program.Browser.UserClients[steam];
            if (client == null) return;
            if (!lobby.banned.Contains(steam))
            {
                var arr = lobby.banned;
                Array.Resize(ref arr, lobby.banned.Length+1);
                arr[arr.Length-1] = steam;
                lobby.banned = arr;
                TransmitLobbyUpdate(lobby, new []{"banned"});
            }
            LeaveLobby(client);
        }

        public void TransmitPublicLobbySnapshot(BrowserClient client)
        {
            var msg = new JObject();
            msg["msg"] = "colupd";
            var ops = new JArray {DiffGenerator.RemoveAll("publicLobbies")};
            foreach (var lobby in PublicLobbies)
            {
                ops.Add(lobby.Add("publicLobbies"));
            }
            msg["ops"] = ops;
            client.Send(msg.ToString(Formatting.None));
        }

        public void SetTitle(Lobby lobby, string name)
        {
            lobby.name = name;
            TransmitLobbyUpdate(lobby, new[] { "name" });
        }

        public void SetRegion(Lobby lobby, ServerRegion region)
        {
            lobby.region = region;
            TransmitLobbyUpdate(lobby, new[] { "region" });
        }

        public void OnServerShutdown(GameInstance instance)
        {
            if (PlayingLobbies.Contains(instance.lobby))
            {
				log.Info("Lobby finished "+instance.lobby.id);
                CloseLobby(instance.lobby);
                CalculateQueue();
            }
        }

        public void OnServerReady(GameInstance instance)
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

        private void SendLaunchDota(Lobby lobby)
        {
            foreach (var client in lobby.radiant.Where(plyr => plyr != null).Select(plyr => Program.Client.ClientUID.Values.FirstOrDefault(m => m.SteamID == plyr.steam)).Where(client => client != null))
            {
                client.LaunchDota();
            }
            foreach (var client in lobby.dire.Where(plyr => plyr != null).Select(plyr => Program.Client.ClientUID.Values.FirstOrDefault(m => m.SteamID == plyr.steam)).Where(client => client != null))
            {
                client.LaunchDota();
            }
        }

        private void SendConnectDota(Lobby lobby)
        {
            foreach (var client in lobby.radiant.Where(plyr => plyr != null).Select(plyr => Program.Client.ClientUID.Values.FirstOrDefault(m => m.SteamID == plyr.steam)).Where(client => client != null))
            {
                client.ConnectDota(lobby.serverIP);
            }
            foreach (var client in lobby.dire.Where(plyr => plyr != null).Select(plyr => Program.Client.ClientUID.Values.FirstOrDefault(m => m.SteamID == plyr.steam)).Where(client => client != null))
            {
                client.ConnectDota(lobby.serverIP);
            }
        }

        public void LaunchAndConnect(Lobby lobby, string steamid)
        {
            var client = Program.Client.ClientUID.Values.FirstOrDefault(m => m.SteamID == steamid);
            if (client != null)
            {
                client.LaunchDota();
                client.ConnectDota(lobby.serverIP);
            }
        }
    }
}
