using System;
using System.Collections.Generic;
using System.Threading;
using D2MPMaster.Database;
using D2MPMaster.Lobbies;
using D2MPMaster.Server;
using D2MPMaster.Storage;
using log4net.Config;
using MongoDB.Driver.Linq;
using XSockets.Core.Common.Configuration;
using XSockets.Core.Common.Globals;
using XSockets.Core.Common.Socket;
using XSockets.Core.Configuration;
using XSockets.Plugin.Framework;

namespace D2MPMaster
{
    class Program
    {
        private static string version = "1.0.1";
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

            log.Info("Initializing xsockets...");

            Console.CancelKeyPress += delegate
            {
                shutdown = true;
            };

            var settings = new List<ConfigurationSetting>();
            settings.Add(new ConfigurationSetting());
            using (var server = Composable.GetExport<IXSocketServerContainer>())
            {
                server.StartServers(useLoopback:false);
                log.Info("Server running!");
                while (!shutdown)
                {
                    Thread.Sleep(100);
                }
                server.StopServers();
            }

            log.Info("Done, shutting down...");
        }
    }
}
