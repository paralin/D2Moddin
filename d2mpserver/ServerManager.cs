using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using d2mpserver.Properties;

namespace d2mpserver
{
    // When a server is shut down
    public delegate void ShutdownEventHandler(object sender, EventArgs e);

    public class ServerManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static string exePath;
        Dictionary<int, Server> servers = new Dictionary<int, Server>();

        public bool LocateServerEXE()
        {
            exePath = Settings.Default.exePath;
            return File.Exists(exePath);
        }

        public Server LaunchServer(int id, int port, bool dev)
        {
            log.Info("Launching server, ID: "+id+" on port "+port+(dev?" in devmode.":"."));
            var serv = Server.Create(id, port, dev);
            servers.Add(id, serv);
            return serv;
        }

        public void ShutdownServer(int id)
        {
            servers[id].Shutdown();
            servers.Remove(id);
        }

        public void ShutdownAllServers()
        {
            foreach(var server in servers)
            {
                server.Value.Shutdown();
            }
            servers.Clear();
        }
    }

    //Stores info on a server
    public class Server
    {
        private Process serverProc;
        private int id;
        private int port;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private bool shutdown = false;
        public event ShutdownEventHandler OnShutdown;
       

        private Server(Process serverProc, int id, int port, bool dev)
        {
            this.id = id;
            this.serverProc = serverProc;
            this.port = port;
            ThreadPool.QueueUserWorkItem(ServerThread);
        }

        private void ServerThread(object state)
        {
            serverProc.WaitForExit();
            if (OnShutdown != null)
                OnShutdown(this, EventArgs.Empty);
        }

        public static Server Create(int id, int port, bool dev)
        {
            ProcessStartInfo info = new ProcessStartInfo(ServerManager.exePath);
            info.Arguments = Settings.Default.args;
            if(dev)
            {
                info.Arguments += " " + Settings.Default.devArgs;
            }
            info.Arguments += " -port " + port;
            info.WorkingDirectory = Path.GetDirectoryName(ServerManager.exePath);
            Process serverProc = Process.Start(info);
            Server serv = new Server(serverProc, id, port, dev);
            log.Debug("server ID: "+id+" spawned");
            return serv;
        }

        public void Shutdown()
        {
            if (shutdown) return;
            log.Debug("shutting down scrds id: "+id+" by server request");
            serverProc.Kill();
        }
    }
}
