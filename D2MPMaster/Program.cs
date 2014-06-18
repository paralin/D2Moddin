using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.MatchData;
using D2MPMaster.Properties;
using D2MPMaster.Server;
using D2MPMaster.Storage;
using log4net.Config;
using Nancy.Hosting.Self;
using XSockets.Core.Common.Socket;
using XSockets.Core.Configuration;
using XSockets.Plugin.Framework;

namespace D2MPMaster
{
    class Program
    {
        private static string version = "1.2.0";
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
            Uri[] urilist = null;
            {
                IList<Uri> uris = new List<Uri>() { new Uri("http://127.0.0.1:" + Settings.Default.WebserverBind + "/"), new Uri("http://localhost:"+Settings.Default.WebserverBind+"/")};
                var u = new UdpClient("8.8.8.8", 1);
                IPAddress localAddr = ((IPEndPoint) u.Client.LocalEndPoint).Address;
                uris.Add(new Uri("http://" + localAddr + ":" + Settings.Default.WebserverBind + "/"));
                urilist = uris.ToArray();
            }
            using (var nancyServer = new NancyHost(urilist))
            {
                nancyServer.Start();
                log.Info("Initializing xsockets...");

                Console.CancelKeyPress += delegate
                                          {
                                              shutdown = true;
                                          };

                using (var server = Composable.GetExport<IXSocketServerContainer>())
                {
                    server.StartServers();
                    log.Info("Server running!");
                    while (!shutdown)
                    {
                        Thread.Sleep(100);
                    }
                    server.StopServers();
                }
                nancyServer.Stop();
            }

            log.Info("Done, shutting down...");
			//wserver.Stop();
        }
    }
}
