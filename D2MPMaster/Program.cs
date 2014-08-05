using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D2MPMaster.Browser;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.Properties;
using D2MPMaster.Server;
using D2MPMaster.Storage;
using log4net.Config;
using Nancy.Hosting.Self;
using XSockets.Core.Common.Socket;
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
            log.Debug(Settings.Default.MongoURL);
            Mongo.Setup();
            Task.Factory.StartNew(Mongo.UpdateOldMatchResults);

            log.Info("Caching mods...");
            Mods.Mods.InitCache();
            log.Info(Mods.Mods.ModCache.Count+" mods cached.");

            log.Info("Generating server addon list...");
            ServerAddons.Init(Mods.Mods.ModCache.Values);

            log.Info("Initializing Amazon S3...");
            S3 = new S3Manager();

			log.Info("Initializing match result server...");
            Uri[] urilist = null;
            {
				IList<Uri> uris = new List<Uri>();
#if DEBUG||DEV
                uris.Add(new Uri("http://127.0.0.1:" + Settings.Default.WebserverBind));
#else
                //uris.Add(new Uri("http://net1.d2modd.in:" + Settings.Default.WebserverBind));
                uris.Add(new Uri("http://"+Settings.Default.WebAddress+":"+Settings.Default.WebserverBind));
#endif
                urilist = uris.ToArray();
            }
			foreach(var uri in urilist){
				log.Debug(uri);
			}
			var config = new HostConfiguration();
            using (var nancyServer = new NancyHost(config,urilist))
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
                    Mods.Mods.StartUpdateTimer();
					Lobbies.LobbyManager.Start ();
                    log.Info("Server running!");
                    while (!shutdown)
                    {
                        Thread.Sleep(100);
                    }
                    BrowserController.cts.Cancel();
                    ClientController.cts.Cancel();
                    ServerController.cts.Cancel();
					Lobbies.LobbyManager.Stop ();
					Mods.Mods.StopUpdateTimer();
                    server.StopServers();
                }
                nancyServer.Stop();
            }

            log.Info("Done, shutting down...");
			//wserver.Stop();
        }
    }
}
