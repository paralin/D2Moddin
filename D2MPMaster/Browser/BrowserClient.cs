using System;
using System.Linq;
using D2MPMaster.Database;
using D2MPMaster.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
/*
 * {
 *   id: "method|subscribe",
 *   
 * }
 */
using WebSocketSharp.Net.WebSockets;


namespace D2MPMaster.Browser
{
    class BrowserClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
#region Auth

        private bool authed = false;
        private User user;

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
                        //Parse the resume key
                        var key = jdata["key"]["hashedToken"].Value<string>();
                        //Parse the UID
                        var uid = jdata["uid"].Value<string>();
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
                    case "lobsub":
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
