using System.Collections.Generic;
using System.Collections.Specialized;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace D2MPMaster.Browser
{
    class BrowserManager : WebSocketService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, BrowserClient> Clients = new Dictionary<string, BrowserClient>();
        private Dictionary<string, BrowserClient> UserClients = new Dictionary<string, BrowserClient>(); 

        public BrowserManager()
        {
            Program.Browser = this;
            Program.LobbyManager.PublicLobbies.CollectionChanged += TransmitLobbiesChange;
        }

        public void TransmitPublicLobbiesUpdate(List<Lobby> lobbies, string[] fields)
        {
            var updates = new JArray();
            foreach (var lobby in lobbies)
            {
                lobby.Update("publicLobbies", fields);
            }
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = updates;
            var msg = upd.ToString(Formatting.None);
            Sessions.Broadcast(msg);
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
                if(e.NewItems != null)
                foreach (var lobby in e.NewItems)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            updates.Add(lobby.Add("publicLobbies"));
                            break;
                    }
                }
                if(e.OldItems != null)
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
            Sessions.Broadcast(msg);
        }

        public void TransmitLobbyUpdate(string steamid, Lobby lobby, string[] fields)
        {
            var client = UserClients[steamid];
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {lobby.Update("lobbies", fields)};
            client.Send(upd.ToString(Formatting.None));
        }

        public void TransmitLobbySnapshot(string steamid, Lobby lob)
        {
            var client = UserClients[steamid];
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {lob.Add("lobbies")};
            client.Send(upd.ToString(Formatting.None));
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var client = Clients[ID];
            client.HandleMessage(e.Data, Context, ID);
            base.OnMessage(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Clients[ID].OnClose(e, ID);
            Sessions.CloseSession(ID);
            base.OnClose(e);
        }

        protected override void OnOpen()
        {
            var client = new BrowserClient(Context.WebSocket, ID);
            Clients[ID] = client;
            log.Debug(string.Format("Client connected #{1}: {0}.", ID, Context.Host));
            base.OnOpen();
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Log.Error(e.Message);
            base.OnError(e);
        }

        /// <summary>
        /// When a user logged in, check to see if we can merge their BrowserClients
        /// </summary>
        /// <param name="browserClient"></param>
        /// <param name="user"></param>
        public void RegisterUser(BrowserClient browserClient, User user)
        {
            if (UserClients.ContainsKey(user.services.steam.steamid))
            {
                var client = UserClients[user.services.steam.steamid];
                client.RegisterSocket(browserClient.baseWebsocket, browserClient.baseSession);
                Clients[browserClient.baseSession] = client;
                browserClient.Obsolete();
            }
            else
            {
                UserClients[user.services.steam.steamid] = browserClient;
            }
        }

        public void DeregisterClient(BrowserClient browserClient, string baseSession)
        {
            Clients.Remove(baseSession);
            if (browserClient.user != null)
            {
                UserClients.Remove(browserClient.user.services.steam.steamid);
            }
        }

        public void DeregisterUser(BrowserClient browserClient, User user, string id)
        {
            if (user == null) return;
            //Delete all of their specific lobbies (so they are no longer in a lobby)
            browserClient.SendClearLobby(browserClient.sockets[id]);
            if (browserClient.sockets.Count > 1)
            {
                //Create a new BrowserClient to handle the new de-authed orphan
                var client = new BrowserClient(browserClient.sockets[id], id);
                browserClient.sockets.Remove(id);
                Clients[id] = client;
            }
            else
            {
                UserClients.Remove(browserClient.user.services.steam.steamid);
                Program.LobbyManager.LeaveLobby(browserClient);
            }
        }


    }
}
