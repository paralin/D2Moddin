using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using D2MPMaster.Browser.Methods;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
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
        public Dictionary<string, WebSocket> sockets = new Dictionary<string, WebSocket>();
        public string baseSession;
        public WebSocket baseWebsocket;
        private string _id;

        public BrowserClient(WebSocket socket, string sessionID)
        {
            this.socket = socket;
            sockets.Add(sessionID, socket);
            baseSession = sessionID;
            baseWebsocket = socket;
        }

#region Variables
        private WebSocket socket;
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
            socket.Send(resp.ToString(Formatting.None));
        }

#endregion

#region Message Handling
        public void HandleMessage(string data, WebSocketContext context, string sessionID)
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
                        //Check if they are in a lobby
                        CheckLobby();
                        if (lobby != null)
                            RespondError(jdata, "You are already in a lobby.");
                        lobby = LobbyManager.CreateLobby(user, req);
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
                        Program.LobbyManager.TransmitLobbyUpdate(lobby, new []{"radiant", "dire"});
                        if (lobby.status == 0 && lobby.isPublic)
                        {
                            Program.Browser.TransmitPublicLobbiesUpdate(new List<Lobby> { lobby }, new[] { "radiant", "dire" });
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
                        var req = jdata["req"].ToObject<SetRegion>();
                        Program.LobbyManager.SetRegion(lobby, req.region);
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
#endregion

        public void OnClose(CloseEventArgs closeEventArgs, string sessionID)
        {
            sockets.Remove(sessionID);
            if (sockets.Count == 0)
            {
                Program.Browser.DeregisterClient(this, baseSession);
                if(user != null && lobby != null)
                    Program.LobbyManager.LeaveLobby(this);
            }
        }

        public void RegisterSocket(WebSocket webSocket, string session)
        {
            sockets.Add(session, webSocket);
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
            upd["ops"] = new JArray {DiffGenerator.RemoveAll("lobbies")};
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
            foreach (var sock in sockets.Values)
            {
                if (sock == null) continue;
                sock.Send(msg);
            }
        }
    }
}
