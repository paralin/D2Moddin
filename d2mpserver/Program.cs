using System;
using System.Threading;
using System.Runtime.InteropServices;
using d2mpserver.Properties;
using log4net.Config;

namespace d2mpserver
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static volatile bool shutdown = false;
        [DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
        {
            ShutdownAll();
            shutdown = true;
            return true;
        }

        public static void ShutdownAll()
        {
            if (connection != null)
                connection.Shutdown();
            if (manager != null)
            {
                manager.Shutdown();
            }
            connection = null;
            manager = null;
        }

        static ServerConnection connection;
        static ServerManager manager;
        static void Main(string[] args)
        {
            SetConsoleCtrlHandler(ConsoleCtrlCheck, true);
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
            XmlConfigurator.Configure();
            log.Info("D2MP server starting...");

            log.Debug("Server IP: " + Settings.Default.serverIP);
            log.Debug("Connect 'password': " + Settings.Default.connectPassword);

            manager = new ServerManager();
            if (!manager.SetupEnvironment())
            {
                log.Fatal("Failed to setup the server, exiting.");
                return;
            }

            connection = new ServerConnection(manager);
            if (!connection.Connect())
            {
                log.Fatal("Can't connect to the server!");
                return;
            }

            connection.StartServerThread();

            while (!shutdown)
            {
                Thread.Sleep(100);
            }
            ShutdownAll();
        }
    }
}
