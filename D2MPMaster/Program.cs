
using System;
using System.Net;
using System.Threading;
using D2MPMaster.Browser;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.Lobbies;
using D2MPMaster.Properties;
using D2MPMaster.Server;
using D2MPMaster.Storage;
using log4net.Config;
using MongoDB.Driver.Linq;
using WebSocketSharp.Server;

namespace D2MPMaster
{
    class Program
    {
        private static string version = "1.0.0";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static BrowserManager Browser;
        public static ServerManager Server;
        public static LobbyManager LobbyManager;
        public static WebSocketServer SocketServer;
        public static ClientManager Client;
        public static S3Manager S3;
        public static volatile bool shutdown;

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            Console.Title = string.Format("[{0}] D2MP Master", version);
            log.Info("Master server starting v."+version+".");

            log.Info("Initializing database...");
            Mongo.Setup();

            log.Info("Caching mods...");
            Mods.Mods.Cache();
            log.Info(Mods.Mods.ModCache.Count+" mods cached.");

            log.Info("Generating server addon list...");
            ServerAddons.Init(Mods.Mods.ModCache.Values);

            log.Info("Initializing Amazon S3...");
            S3 = new S3Manager();

            LobbyManager = new LobbyManager();
            Browser = new BrowserManager();
            Client = new ClientManager();
            Server = new ServerManager();

            var wssv = SocketServer = new WebSocketServer(Settings.Default.URI);
            wssv.AddWebSocketService<BrowserService>("/browser");
            wssv.AddWebSocketService<ServerService>("/server");
            wssv.AddWebSocketService<ClientService>("/client");
            wssv.Start();

            log.Info("Server running!");

            Console.CancelKeyPress += delegate
                                      {
                                          shutdown = true;
                                      };
            while (!shutdown)
            {
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
