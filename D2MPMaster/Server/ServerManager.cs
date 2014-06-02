using WebSocketSharp;
using WebSocketSharp.Server;

namespace D2MPMaster
{
    class ServerManager : WebSocketService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ServerManager()
        {
            Program.Server = this;
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            base.OnMessage(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
        }

        protected override void OnOpen()
        {
            log.Debug(string.Format("Client connected {0}.", Context.Host));
            base.OnOpen();
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Log.Error(e.Message);
            base.OnError(e);
        }
    }
}
