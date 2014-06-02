using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.Remoting.Contexts;
using D2MPMaster.Browser;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using WebSocketSharp.Server;
using DiffGenerator = D2MPMaster.LiveData.DiffGenerator;

namespace D2MPMaster
{
    class BrowserManager : WebSocketService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, BrowserClient> Clients = new Dictionary<string, BrowserClient>();

        public BrowserManager()
        {
            Program.Browser = this;
            Program.LobbyManager.PublicLobbies.CollectionChanged += TransmitLobbiesChange;
        }

        private void TransmitLobbiesChange(object s, NotifyCollectionChangedEventArgs e)
        {
            var updates = new JArray();
            foreach (var lobby in e.NewItems)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        updates.Add(DiffGenerator.Add(lobby, "publicLobbies"));
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        updates.Add(DiffGenerator.Remove(lobby, "publicLobbies"));
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        updates.Add(DiffGenerator.RemoveAll("publicLobbies"));
                        break;
                }
            }
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = updates;
            var msg = upd.ToString(Formatting.None);
            Sessions.Broadcast(msg);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var client = Clients[ID];
            client.HandleMessage(e.Data, Context);
            base.OnMessage(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Clients.Remove(ID);
            Sessions.CloseSession(ID);
            base.OnClose(e);
        }

        protected override void OnOpen()
        {
            var client = new BrowserClient(Context.WebSocket);
            Clients[ID] = client;
            log.Debug(string.Format("Client connected #{1}: {0}.", ID, Context.Host));
            log.Debug(Context.Headers);
            base.OnOpen();
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Log.Error(e.Message);
            base.OnError(e);
        }
    }
}
