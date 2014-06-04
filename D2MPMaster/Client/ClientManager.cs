using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using D2MPMaster.Browser;
using D2MPMaster.Database;
using D2MPMaster.Model;
using D2MPMaster.Properties;
using Fleck;
using MongoDB.Bson;
using Query = MongoDB.Driver.Builders.Query;

namespace D2MPMaster.Client
{
    public class ClientManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Dictionary<string, ModClient> Clients = new Dictionary<string, ModClient>(); 
		public ConcurrentDictionary<string, ModClient> ClientUID = new ConcurrentDictionary<string, ModClient>();
        private WebSocketServer server;

        public ClientManager()
        {
            server = new WebSocketServer("ws://0.0.0.0:" + Settings.Default.BrowserPort);
            server.Start(socket =>
            {
                string ID = Utils.RandomString(10);
                socket.OnOpen = () => OnOpen(ID, socket);
                socket.OnClose = () => OnClose(ID, socket);
                socket.OnMessage = message => OnMessage(ID, socket, message);
            });
        }

        public void OnMessage(string ID, IWebSocketConnection socket, string message)
        {
            var client = Clients[ID];
            var handleTask = new Task(() => client.HandleMessage(message, socket, ID));
            handleTask.Start();
        }

        public void OnClose(string ID, IWebSocketConnection socket)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Clients[ID].OnClose(socket, ID);
        }

        public void OnOpen(string ID, IWebSocketConnection socket)
        {
            var client = new ModClient(socket, ID);
            Clients[ID] = client;
            log.Debug(string.Format("Client connected #{1}: {0}.", ID, socket.ConnectionInfo.ClientIpAddress));
        }

        #region client

        public void RegisterClient(ModClient modClient)
        {
            List<BsonValue> values = modClient.InitData.SteamIDs.Where(sid => sid.Length == 17).Select(sid => new BsonString(sid)).Cast<BsonValue>().ToList();
            foreach (var sid in modClient.InitData.SteamIDs)
            {
                log.Debug(sid);
            }
            var user = Mongo.Users.FindOneAs<User>(Query.In("services.steam.id", values));
            if (user == null)
            {
                if(values.Count > 0)
                    log.Debug("Can't find user for "+values[0]+".");
				modClient.Shutdown();
                return;
            }
            modClient.UID = user.Id;
            modClient.SteamID = user.services.steam.steamid;
			if(ClientUID.ContainsKey(user.Id))
			{
			    ModClient client;
                ClientUID.TryRemove(user.Id, out client);
			}
            ClientUID[user.Id] = modClient;
            Mongo.Clients.Remove(Query.EQ("_id", user.Id));
            Mongo.Clients.Insert(new ClientRecord()
                                 {
                                     Id = user.Id,
                                     status = 0
                                 });
        }

        public void DeregisterClient(ModClient modClient)
        {
            ModClient client;
            ClientUID.TryRemove(modClient.UID, out client);
            Mongo.Clients.Remove(Query.EQ("_id", modClient.UID));
        }
        #endregion
    }
}
