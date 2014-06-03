using System.Collections.Generic;
using System.Linq;
using Amazon.DataPipeline.Model;
using D2MPMaster.Database;
using D2MPMaster.Model;
using MongoDB.Bson;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;
using Query = MongoDB.Driver.Builders.Query;

namespace D2MPMaster.Client
{
    public class ClientManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Dictionary<string, ModClient> Clients = new Dictionary<string, ModClient>(); 
        Dictionary<string, ModClient> ClientUID = new Dictionary<string, ModClient>();
 
        public void OnOpen(string ID, WebSocketContext Context)
        {
            var client = new ModClient(Context);
            Clients[ID] = client;
            log.Debug(string.Format("Mod client connected #{1}: {0}.", ID, Context.Host));
        }

        public void OnClose(string ID, WebSocketContext Context, CloseEventArgs e)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Clients[ID].OnClose(e, ID);
        }

        public void OnMessage(string ID, WebSocketContext Context, MessageEventArgs e)
        {
            var client = Clients[ID];
            client.HandleMessage(e.Data, Context, ID);
        }

        public void OnError(string ID, WebSocketContext Context, ErrorEventArgs e)
        {
            log.Error(e.Message);
        }

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
                return;
            }
            modClient.UID = user.Id;
            ClientUID.Add(user.Id, modClient);
            Mongo.Clients.Remove(Query.EQ("_id", user.Id));
            Mongo.Clients.Insert(new ClientRecord()
                                 {
                                     Id = user.Id,
                                     status = 0
                                 });
        }

        public void DeregisterClient(ModClient modClient)
        {
            ClientUID.Remove(modClient.UID);
            Mongo.Clients.Remove(Query.EQ("_id", modClient.UID));
        }
    }

    class ClientService : WebSocketService
    {
        protected override void OnClose(CloseEventArgs e)
        {
            Program.Client.OnClose(ID, Context, e);
            base.OnClose(e);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Program.Client.OnError(ID, Context, e);
            base.OnError(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Program.Client.OnMessage(ID, Context, e);
            base.OnMessage(e);
        }

        protected override void OnOpen()
        {
            Program.Client.OnOpen(ID, Context);
            base.OnOpen();
        }

    }
}
