using System;
using System.Threading;
using D2MPMaster.Database;
using D2MPMaster.Lobbies;
using D2MPMaster.Properties;
using log4net.Config;
using WebSocketSharp.Server;

namespace D2MPMaster
{
    class Program
    {
        private static string version = "0.0.1";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static BrowserManager Browser;
        public static ServerManager Server;
        public static LobbyManager LobbyManager;
        public static WebSocketServer SocketServer;
        public static volatile bool shutdown;

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            Console.Title = string.Format("[{0}] D2MP Master", version);
            log.Info("Master server starting v."+version+".");

            log.Info("Initializing database...");
            Mongo.Setup();

            var wssv = SocketServer = new WebSocketServer(Settings.Default.URI);
            wssv.AddWebSocketService<BrowserManager>("/browser");
            wssv.AddWebSocketService<ServerManager>("/server");
            wssv.Start();

            LobbyManager = new LobbyManager();

            log.Info("Server running!");

            while (!shutdown)
            {
                if (Console.KeyAvailable) shutdown = true;
                Thread.Sleep(100);
            }

            wssv.Stop();
            
            log.Info("Done, shutting down...");

            Browser = null;
            Server = null;
            LobbyManager = null;
            SocketServer = null;
        }
    }
}
