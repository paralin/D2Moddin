using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using D2MPMaster.Browser;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace D2MPMaster
{
    class BrowserManager : WebSocketService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, BrowserClient> Clients = new Dictionary<string, BrowserClient>();
        
        protected override void OnMessage(MessageEventArgs e)
        {
            //log.Debug(string.Format("Client message #{0}: #{1}", ID, e.Data));
            var client = Clients[ID];
            client.HandleMessage(e.Data, Context);
            base.OnMessage(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Clients.Remove(ID);
            base.OnClose(e);
        }

        protected override void OnOpen()
        {
            var client = new BrowserClient();
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
