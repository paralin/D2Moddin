using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.WebSockets;
using System.Threading.Tasks;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using D2MPMaster.Properties;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace D2MPMaster.Browser
{
    class BrowserManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private ConcurrentDictionary<string, BrowserClient> Clients = new ConcurrentDictionary<string, BrowserClient>();
        public ConcurrentDictionary<string, BrowserClient> UserClients = new ConcurrentDictionary<string, BrowserClient>();
        private WebSocketServer server;

        private object transmitLock = new object();

        public BrowserManager()
        {
            server = new WebSocketServer("ws://0.0.0.0:"+Settings.Default.BrowserPort);
            server.Start(socket =>
                         {
                             string ID = Utils.RandomString(10);
                             socket.OnOpen = () => OnOpen(ID, socket);
                             socket.OnClose = () => OnClose(ID, socket);
                             socket.OnMessage = message => OnMessage(ID, socket, message);
                         });
            Program.LobbyManager.PublicLobbies.CollectionChanged += TransmitLobbiesChange;
        }

        public void Stop()
        {
            server.Dispose();
        }

        public void TransmitPublicLobbiesUpdate(List<Lobby> lobbies, string[] fields)
        {
            //lock (transmitLock)
            //{
            var updates = new JArray();
            foreach (var lobby in lobbies)
            {
                updates.Add(lobby.Update("publicLobbies", fields));
            }
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = updates;
            var msg = upd.ToString(Formatting.None);
            Broadcast(msg);
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
                var upd = new JObject();
                upd["msg"] = "colupd";
                upd["ops"] = updates;
                var msg = upd.ToString(Formatting.None);
                Broadcast(msg);
            }
        }

        public void Broadcast(string msg)
        {
            foreach (var client in Clients.Values)
            {
                client.Send(msg);
            }
        }

        public void TransmitLobbyUpdate(string steamid, Lobby lobby, string[] fields)
        {
            lock (transmitLock)
            {
                var client = UserClients[steamid];
                //Generate message
                var upd = new JObject();
                upd["msg"] = "colupd";
                upd["ops"] = new JArray {lobby.Update("lobbies", fields)};
                client.Send(upd.ToString(Formatting.None));
            }
        }

        public void TransmitLobbySnapshot(string steamid, Lobby lob)
        {
            var client = UserClients[steamid];
            //Generate message
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {DiffGenerator.RemoveAll("lobbies"), lob.Add("lobbies") };
            client.Send(upd.ToString(Formatting.None));
        }

        public void OnMessage(string ID, IWebSocketConnection socket, string message)
        {
            var client = Clients[ID];
            var handleTask = new Task(() => client.HandleMessage(message, socket, ID));
            handleTask.Start();
        }

        public void OnClose(string ID, IWebSocketConnection socket)
        {
            log.Debug(string.Format("Browser disconnect #{0}", ID));
            Clients[ID].OnClose(socket, ID);
        }

        public void OnOpen(string ID, IWebSocketConnection socket)
        {
            var client = new BrowserClient(socket, ID);
            Clients[ID] = client;
            Program.LobbyManager.TransmitPublicLobbySnapshot(client);
            log.Debug(string.Format("Browser connected #{1}: {0}.", ID, socket.ConnectionInfo.ClientIpAddress));
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
                if (client.lobby != null)
                {
                    TransmitLobbySnapshot(user.services.steam.steamid, client.lobby);
                }
            }
            else
            {
                UserClients[user.services.steam.steamid] = browserClient;
            }
        }

        public void DeregisterClient(BrowserClient browserClient, string baseSession)
        {
            try
            {
                Clients.Remove(baseSession);
                if (browserClient.user != null)
                {
                    UserClients.Remove(browserClient.user.services.steam.steamid);
                }
            }
            catch
            {
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
