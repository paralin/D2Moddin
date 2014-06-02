using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using D2MPMaster.Browser;
using D2MPMaster.Browser.Methods;
using D2MPMaster.Model;
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
        public ObservableCollection<Lobby> PublicLobbies = new ObservableCollection<Lobby>();

        /// <summary>
        /// Lobbies that should have full updates sent out JUST to users in them.
        /// </summary>
        public ObservableCollection<Lobby> PlayingLobbies = new ObservableCollection<Lobby>();

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

        public void TransmitLobbyUpdate(Lobby lobby, string[] fields)
        {
            Program.Browser.TransmitPublicLobbiesUpdate(new List<Lobby>(){lobby}, fields);
            foreach (var plyr in lobby.radiant.Where(plyr => plyr != null))
            {
                Program.Browser.TransmitLobbyUpdate(plyr.steam, lobby, fields);
            }
            foreach (var plyr in lobby.dire.Where(plyr => plyr != null))
            {
                Program.Browser.TransmitLobbyUpdate(plyr.steam, lobby, fields);
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
            foreach (var plyr in lob.radiant)
            {
                if (plyr == null) continue;
                var client = Program.Browser.UserClients[plyr.steam];
                if (client != null)
                {
                    client.lobby = null;
                    client.SendClearLobby(null);
                }
            }
            foreach (var plyr in lob.dire)
            {
                if (plyr == null) continue;
                var client = Program.Browser.UserClients[plyr.steam];
                if (client != null)
                {
                    client.lobby = null;
                    client.SendClearLobby(null);
                }
            }
            PublicLobbies.Remove(lob);
            PlayingLobbies.Remove(lob);
        }

        public void LeaveLobby(BrowserClient client)
        {
            if (client.lobby == null || client.user == null) return;
            var lob = client.lobby;
            if (lob == null || lob.status > 1) return;
            client.lobby = null;
            client.SendClearLobby(null);
            //Find the player
            var team = RemoveFromTeam(lob, client.user.services.steam.steamid);
            if(team != null)
                TransmitLobbyUpdate(lob, new[] { team });
            if ((lob.TeamCount(lob.dire) == 0 && lob.TeamCount(lob.radiant) == 0) || lob.creatorid == client.user.Id)
            {
                CloseLobby(lob);
            }
        }

        /// <summary>
        /// Create a new lobby.
        /// </summary>
        /// <param name="user">Creator user</param>
        /// <param name="req">Create request</param>
        /// <returns></returns>
        public static Lobby CreateLobby(User user, CreateLobby req)
        {
            //Filter lobby name to alphanumeric only
            string name = Regex.Replace(req.name, "^[\\w \\.\"'[]\\{\\}\\(\\)]+", "");
            //Constrain lobby name length to 40 characters
            if (name.Length > 40)
            {
                name = name.Substring(0, 40);
            }
            //Find the mod
            var mod = Mods.Mods.ByID(req.mod);
            if (mod == null)
            {
                log.Error("Can't find mod "+req.mod+".");
                return null;
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
                            mod = req.mod,
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
    }
}
