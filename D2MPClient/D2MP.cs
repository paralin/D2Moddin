using System;
using System.IO;
using WebSocketSharp;

namespace d2mp
{
    public class D2MP
    {
        private static string server = "ws://10.0.1.2:3005/";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static void main()
        {
            log.Debug("D2MP starting...");
            var client = new ServerClient();
            using (var ws = new WebSocket(server))
            {
                ws.OnMessage += (sender, e) => log.Debug("server: " + e.Data);
                ws.OnOpen += (sender, e) => log.Debug("connected");
                ws.OnClose += (sender, args) => log.Debug("Disconnected");
                ws.Connect();

                ws.Send("BALUS");
                while(ws.IsAlive)
                {
                }
            }
        }
    }
}
