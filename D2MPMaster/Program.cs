using System;
using System.Collections.Generic;
using System.Threading;
using D2MPMaster.Database;
using D2MPMaster.MatchData;
using D2MPMaster.Properties;
using D2MPMaster.Server;
using D2MPMaster.Storage;
using log4net.Config;
using XSockets.Core.Common.Socket;
using XSockets.Core.Configuration;
using XSockets.Plugin.Framework;

namespace D2MPMaster
{
    class Program
    {
        private static string version = "1.0.2";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

			log.Info("Initializing match result server...");
			MatchDataServer matchDataServer = new MatchDataServer(Settings.Default.WebserverBind);

            log.Info("Initializing xsockets...");

            Console.CancelKeyPress += delegate
            {
                shutdown = true;
            };

            var settings = new List<ConfigurationSetting>();
            settings.Add(new ConfigurationSetting());
            using (var server = Composable.GetExport<IXSocketServerContainer>())
            {
                server.StartServers();
                log.Info("Server running!");
                while (!shutdown)
                {
                    Thread.Sleep(100);
                }
                server.StopServers();
                matchDataServer.Shutdown();
            }

            log.Info("Done, shutting down...");
			//wserver.Stop();
        }
    }
}
