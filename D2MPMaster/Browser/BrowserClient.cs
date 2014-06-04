using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using D2MPMaster.Browser.Methods;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using d2mpserver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocket = WebSocketSharp.WebSocket;
/*
 * {
 *   id: "method|subscribe",
 *   
 * }
 */
using WebSocketContext = WebSocketSharp.Net.WebSockets.WebSocketContext;


namespace D2MPMaster.Browser
{
    public class BrowserClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ConcurrentDictionary<string, WebSocket> sockets = new ConcurrentDictionary<string, WebSocket>();
        public string baseSession;
        public WebSocket baseWebsocket;
		//public volatile bool proccommand = false;
        private string _id;
        private Thread SendQueue;
        public ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private object msgLock = new object();

        public BrowserClient(WebSocket socket, string sessionID)
        {
            sockets[sessionID] = socket;
            baseSession = sessionID;
            baseWebsocket = socket;
            SendQueue = new Thread(SendQueueProc);
            SendQueue.Start();
        }

        private void SendQueueProc()
        {
            while (sockets != null && sockets.Count > 0)
            {
                string msg = null;
                MessageQueue.TryDequeue(out msg);
                if (msg != null)
                {
                    foreach (var socket in sockets.Values)
                    {
                        socket.Send(msg);
                    }
                }
                Thread.Sleep(100);
            }
        }

        #region Variables
        private string lastMsg = "";
        private DateTime lastMsgTime = DateTime.UtcNow;
        private bool _authed;
        private bool authed
        {
            get { return _authed; }
            set
            {
                _authed = value;
                if (_authed)
                {
                    Program.Browser.RegisterUser(this, user);
                }
                else
                {
                    Program.Browser.DeregisterUser(this, user, _id);
                }
            }
        }
        public User user = null;
        public Lobby lobby = null;
        #endregion
        #region Helpers
        public void CheckLobby()
        {
            if (lobby != null && lobby.deleted) lobby = null;
        }

        public void RespondError(JObject req, string msg)
        {
            JObject resp = new JObject();
            resp["msg"] = "error";
            resp["reason"] = msg;
            resp["req"] = req["id"];
            Send(resp.ToString(Formatting.None));
        }

        #endregion

        #region Message Handling
        public void HandleMessage(string data, WebSocketContext context, string sessionID)
        {
            lock (msgLock)
            {
                try
                {
                    var jdata = JObject.Parse(data);
                    var id = jdata["id"];
                    _id = sessionID;
                    if (id == null) return;
                    var command = id.Value<string>();
                    switch (command)
                    {
                            #region Authentication

                        case "deauth":
                            authed = false;
                            user = null;
                            context.WebSocket.Send("{\"msg\": \"auth\", \"status\": false}");
                            break;
                        case "auth":
                            //Parse the UID
                            var uid = jdata["uid"].Value<string>();
                            if (authed && uid == user.Id)
                            {
                                context.WebSocket.Send("{\"msg\": \"auth\", \"status\": true}");
                                return;
                            }
                            //Parse the resume key
                            var key = jdata["key"]["hashedToken"].Value<string>();
                            //Find it in the database
                            var usr = Mongo.Users.FindOneAs<User>(Query.EQ("_id", uid));
                            bool tokenfound = false;
                            if (usr != null)
                            {
                                var tokens = usr.services.resume.loginTokens;
                                tokenfound = tokens.Any(token => token.hashedToken == key);
                            }
                            if (tokenfound && usr.status.online)
                            {
                                log.Debug(string.Format("Authentication {0} -> {1} ", uid, key));
                                user = usr;
                                authed = true;
                                context.WebSocket.Send("{\"msg\": \"auth\", \"status\": true}");
                            }
                            else
                            {
                                log.Debug(string.Format("Authentication failed, {0} token {1}", uid, key));
                                authed = false;
                                user = null;
                                context.WebSocket.Send("{\"msg\": \"auth\", \"status\": false}");
                            }
                            break;

                            #endregion

                        case "createlobby":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in!");
                                return;
                            }
                            //Check if they are in a lobby
                            CheckLobby();
                            if (lobby != null)
                            {
                                RespondError(jdata, "You are already in a lobby.");
                                return; //omfg
                            }
                            //Parse the create lobby request
                            var req = jdata["req"].ToObject<CreateLobby>();
                            if (req.name == null)
                            {
                                req.name = user.profile.name + "'s Lobby";
                            }
                            if (req.mod == null)
                            {
                                RespondError(jdata, "You did not specify a mod.");
                                return;
                            }
                            //Find the mod
                            var mod = Mongo.Mods.FindOneAs<Mod>(Query.EQ("_id", req.mod));
                            if (mod == null)
                            {
                                RespondError(jdata, "Can't find the mod, you probably don't have access.");
                                return;
                            }
                            //Find the client
                            ModClient client = null;
                            if (Program.Client.ClientUID.ContainsKey(user.Id))
                            {
                                client = Program.Client.ClientUID[user.Id];
                            }
                            if (client == null ||
                                client.Mods.FirstOrDefault(m => m.name == mod.name && m.version == mod.version) == null)
                            {
                                var obj = new JObject();
                                obj["msg"] = "modneeded";
                                obj["name"] = mod.name;
                                Send(obj.ToString(Formatting.None));
                                return;
                            }

                            lobby = LobbyManager.CreateLobby(user, mod, req.name);
                            break;
                        }
                        case "switchteam":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in.");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby.");
                                return;
                            }
                            //Parse the switch team request
                            var req = jdata["req"].ToObject<SwitchTeam>();
                            var goodguys = req.team == "radiant";
                            if ((goodguys && lobby.TeamCount(lobby.radiant) >= 5) ||
                                (!goodguys && lobby.TeamCount(lobby.dire) >= 5))
                            {
                                RespondError(jdata, "That team is full.");
                                return;
                            }
                            Program.LobbyManager.RemoveFromTeam(lobby, user.services.steam.steamid);
                            lobby.AddPlayer(goodguys ? lobby.radiant : lobby.dire, Player.FromUser(user));
                            Program.LobbyManager.TransmitLobbyUpdate(lobby, new[] {"radiant", "dire"});
                            if (lobby.status == 0 && lobby.isPublic)
                            {
                                Program.Browser.TransmitPublicLobbiesUpdate(new List<Lobby> {lobby},
                                    new[] {"radiant", "dire"});
                            }
                            break;
                        }
                        case "leavelobby":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in.");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby.");
                                return;
                            }
                            Program.LobbyManager.LeaveLobby(this);
                            break;
                        }
                        case "chatmsg":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in (can't chat).");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby (can't chat).");
                                return;
                            }
                            var req = jdata["req"].ToObject<ChatMessage>();
                            var msg = req.message;
                            if (msg == null) return;
                            msg = Regex.Replace(msg, "^[\\w \\.\"'[]\\{\\}\\(\\)]+", "");
                            if (msg == lastMsg)
                            {
                                RespondError(jdata, "You cannot send the same message twice in a row.");
                                return;
                            }
                            var now = DateTime.UtcNow;
                            TimeSpan span = now - lastMsgTime;
                            if (span.TotalSeconds < 2)
                            {
                                RespondError(jdata, "You must wait 2 seconds between each message.");
                                return;
                            }
                            lastMsg = msg;
                            lastMsgTime = now;
                            Program.LobbyManager.ChatMessage(lobby, msg, user.profile.name);
                            break;
                        }
                        case "kickplayer":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in (can't kick).");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby (can't kick).");
                                return;
                            }
                            if (lobby.creatorid != user.Id)
                            {
                                RespondError(jdata, "You are not the lobby host.");
                                return;
                            }
                            var req = jdata["req"].ToObject<KickPlayer>();
                            Program.LobbyManager.BanFromLobby(lobby, req.steam);
                            break;
                        }
                        case "setname":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in (can't set name).");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby (can't set name).");
                                return;
                            }
                            if (lobby.creatorid != user.Id)
                            {
                                RespondError(jdata, "You are not the lobby host.");
                                return;
                            }

                            var req = jdata["req"].ToObject<SetName>();
                            var err = req.Validate();
                            if (err != null)
                            {
                                RespondError(jdata, err);
                                return;
                            }
                            Program.LobbyManager.SetTitle(lobby, req.name);
                            break;
                        }
                        case "setregion":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in (can't set region).");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby (can't set region).");
                                return;
                            }
                            if (lobby.creatorid != user.Id)
                            {
                                RespondError(jdata, "You are not the lobby host.");
                                return;
                            }
                            var req = jdata["req"].ToObject<SetRegion>();
                            Program.LobbyManager.SetRegion(lobby, req.region);
                            break;
                        }
                        case "startqueue":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in yet.");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby.");
                                return;
                            }
                            if (lobby.creatorid != user.Id)
                            {
                                RespondError(jdata, "You are not the lobby host.");
                                return;
                            }
                            if (lobby.status != LobbyStatus.Start)
                            {
                                RespondError(jdata, "You are already queuing/playing.");
                                return;
                            }
                            if (lobby.requiresFullLobby &&
                                (lobby.TeamCount(lobby.dire) + lobby.TeamCount(lobby.radiant) < 10))
                            {
                                RespondError(jdata, "Your lobby must be full to start.");
                                return;
                            }
                            Program.LobbyManager.StartQueue(lobby);
                            return;
                        }
                        case "stopqueue":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in yet.");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby.");
                                return;
                            }
                            if (lobby.creatorid != user.Id)
                            {
                                RespondError(jdata, "You are not the lobby host.");
                                return;
                            }
                            if (lobby.status != LobbyStatus.Queue)
                            {
                                RespondError(jdata, "You are not queueing.");
                                return;
                            }
                            Program.LobbyManager.CancelQueue(lobby);
                            return;
                        }
                        case "joinlobby":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in yet.");
                                return;
                            }
                            if (lobby != null)
                            {
                                RespondError(jdata, "You are already in a lobby.");
                                return;
                            }
                            var req = jdata["req"].ToObject<JoinLobby>();
                            //Find lobby
                            var lob = Program.LobbyManager.PublicLobbies.FirstOrDefault(m => m.id == req.LobbyID);
                            if (lob == null)
                            {
                                RespondError(jdata, "Can't find that lobby.");
                                return;
                            }
                            if (lob.TeamCount(lob.dire) >= 5 && lob.TeamCount(lob.radiant) >= 5)
                            {
                                RespondError(jdata, "That lobby is full.");
                                return;
                            }
                            //Find the mod
                            var mod = Mongo.Mods.FindOneAs<Mod>(Query.EQ("_id", lob.mod));
                            if (mod == null)
                            {
                                RespondError(jdata, "Can't find the mod, you probably don't have access.");
                                return;
                            }
                            //Find the client
                            ModClient client = null;
                            if (Program.Client.ClientUID.ContainsKey(user.Id))
                            {
                                client = Program.Client.ClientUID[user.Id];
                            }
                            if (client == null ||
                                client.Mods.FirstOrDefault(m => m.name == mod.name && m.version == mod.version) == null)
                            {
                                var obj = new JObject();
                                obj["msg"] = "modneeded";
                                obj["name"] = mod.name;
                                Send(obj.ToString(Formatting.None));
                                return;
                            }

                            Program.LobbyManager.JoinLobby(lob, user, this);
                            break;
                        }
                        case "installmod":
                        {
                            if (user == null)
                            {
                                SendInstallRes(false, "You are not logged in yet.");
                                return;
                            }
                            var client = Program.Client.ClientUID[user.Id];
                            if (client == null)
                            {
                                SendInstallRes(false, "You have not launched the manager yet.");
                                return;
                            }
                            var req = jdata["req"].ToObject<InstallMod>();
                            var mod = Mongo.Mods.FindOneAs<Mod>(Query.EQ("name", req.mod));
                            if (mod == null)
                            {
                                SendInstallRes(false, "Can't find that mod in the database.");
                                return;
                            }
                            client.InstallMod(mod);
                            break;
                        }
                        case "connectgame":
                        {
                            if (user == null)
                            {
                                RespondError(jdata, "You are not logged in yet.");
                                return;
                            }
                            if (lobby == null)
                            {
                                RespondError(jdata, "You are not in a lobby.");
                                return;
                            }
                            if (lobby.status != LobbyStatus.Play)
                            {
                                RespondError(jdata, "Your lobby isn't ready to play yet.");
                                return;
                            }
                            Program.LobbyManager.LaunchAndConnect(lobby, user.services.steam.steamid);
                            break;
                        }
                        default:
                            log.Debug(string.Format("Unknown command: {0}...", command.Substring(0, 10)));
                            return;
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex.ToString());
                } //Handle all malformed JSON / no ID field / other troll data
            }
        }
        #endregion

        public void OnClose(CloseEventArgs closeEventArgs, string sessionID)
        {
            WebSocket value;
            sockets.TryRemove(sessionID, out value);
            if (sockets.Count == 0)
            {
                Program.Browser.DeregisterClient(this, baseSession);
                if (user != null && lobby != null)
                    Program.LobbyManager.LeaveLobby(this);
            }
        }

        public void RegisterSocket(WebSocket webSocket, string session)
        {
            sockets[session] = webSocket;
        }

        public void Obsolete()
        {
            sockets = null;
            baseSession = null;
            baseWebsocket = null;
        }

        public void SendClearLobby(WebSocket webSocket)
        {
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray { DiffGenerator.RemoveAll("lobbies") };
            var msg = upd.ToString(Formatting.None);
            if (webSocket != null)
                webSocket.Send(upd.ToString(Formatting.None));
            else
            {
                Send(msg);
            }
        }

        public void Send(string msg)
        {
            MessageQueue.Enqueue(msg);
        }

        public void SendInstallRes(bool worked, string msg)
        {
            var upd = new JObject();
            upd["msg"] = "installres";
            upd["success"] = worked;
            upd["message"] = msg;
            Send(upd.ToString(Formatting.None));
        }
    }
}
