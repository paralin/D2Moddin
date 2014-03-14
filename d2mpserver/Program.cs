using System;
using d2mpserver.Properties;
using log4net.Config;

namespace d2mpserver
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            log.Info("D2MP server starting...");

            log.Debug("Server IP: "+Settings.Default.serverIP);
            log.Debug("Connect 'password': "+Settings.Default.connectPassword);

            ServerManager manager = new ServerManager();
            if (!manager.LocateServerEXE())
            {
                log.Fatal("Can't find scrds.exe");
                return;
            }

            ServerConnection connection = new ServerConnection(manager);
            if(!connection.Connect())
            {
                log.Fatal("Can't connect to the server!");
                return;
            }

            connection.StartServerThread();

            Console.ReadLine();
            connection.Shutdown();
        }
    }
}
