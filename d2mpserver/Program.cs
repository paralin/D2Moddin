using System;
using d2mpserver.Properties;
using log4net.Config;

namespace d2mpserver
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static bool shutdown;
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            log.Info("D2MP server starting...");

            log.Debug("Server IP: "+Settings.Default.serverIP);
            log.Debug("Connect 'password': "+Settings.Default.connectPassword);

            ServerManager manager = new ServerManager();
            if (!manager.SetupEnvironment())
            {
                log.Fatal("Failed to setup the server, exiting.");
                return;
            }

            ServerConnection connection = new ServerConnection(manager);
            if(!connection.Connect())
            {
                log.Fatal("Can't connect to the server!");
                return;
            }

            connection.StartServerThread();

            shutdown = false;
            string line;
            while(!shutdown)
            {
              line = Console.ReadLine();
              var command = line.Split(' ');
              try{
                switch(command[0])
                {
                  case "exit":
                    shutdown = true;
                    log.Debug("Shutdown from console...");
                    break;
                  default:
                    log.Error("Unknown command: "+line);
                    break;
                }
              }
              catch(Exception ex){
                log.Error("Problem processing command: "+ex);
              }
            }
            connection.Shutdown();
            manager.ShutdownAllServers();
        }
    }
}
