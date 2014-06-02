using System;
using System.Linq;
using System.Net.WebSockets;
using System.Runtime.Remoting.Contexts;
using D2MPMaster.Browser.Methods;
using D2MPMaster.Database;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public BrowserClient(WebSocketSharp.WebSocket socket)
        {
            this.socket = socket;
        }

#region Variables
        private WebSocket socket;
        private bool authed = false;
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
        public void HandleMessage(string data, WebSocketContext context)
        {
            try
            {
                var jdata = JObject.Parse(data);
                var id = jdata["id"];
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
                        var tokens = usr.services.resume.loginTokens;
                        bool tokenfound = tokens.Any(token => token.hashedToken == key);
                        if (tokenfound && usr.status.online)
                        {
                            log.Debug(string.Format("Authentication {0} -> {1} ", uid, key));
                            authed = true;
                            user = usr;
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
                        //Parse the create lobby request
                        var req = jdata["req"].ToObject<CreateLobby>();
                        //Check if they are in a lobby
                        CheckLobby();
                        if(lobby != null)
                            RespondError(jdata, "You are already in a lobby.");
                        lobby = LobbyManager.CreateLobby(user, req);
                        break;
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
    }
}
