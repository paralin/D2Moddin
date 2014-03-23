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
            return true;
        }

        public Server LaunchServer(int id, int port, bool dev, string mod, string rconPass)
        {
            log.Info("Launching server, ID: "+id+" on port "+port+(dev?" in devmode.":"."));
            var serv = Server.Create(id, port, dev, mod, rconPass);
            serv.OnShutdown += (sender,args)=>servers.Remove(serv.id);
            servers.Add(id, serv);
            return serv;
        }

        public Server GetServer(int id)
        {
          if(!servers.ContainsKey(id))
            return null;
          return servers[id];
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
        public int id;
        private int port;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool shutdown = false;
        public event ShutdownEventHandler OnShutdown;
        public event ShutdownEventHandler OnReady;
        private string mod = "";
       

        private Server(Process serverProc, int id, int port, bool dev)
        {
            this.id = id;
            this.serverProc = serverProc;
            this.port = port;
        }

        public void ToSTDIN(string command){
          serverProc.StandardInput.WriteLine(command);
        }

        void SendModCommands(StreamWriter stdin)
        {
            log.Debug(id+": sending mod init commands");
            stdin.WriteLine("update_addon_paths;");
            stdin.WriteLine("dota_local_custom_enable 1;");
            stdin.WriteLine("dota_local_custom_game "+mod+";");
            stdin.WriteLine("dota_local_custom_map "+mod);
            stdin.WriteLine("dota_force_gamemode 15;");
            stdin.WriteLine("dota_wait_for_players_to_load 1;");
            stdin.WriteLine("dota_wait_for_players_to_load_timeout 30;");
            stdin.WriteLine("map "+mod+";");
            log.Debug(id+": map "+mod+";");
            if(OnReady != null)
              OnReady(this, EventArgs.Empty);
        }

        public void StartThread(){
            ThreadPool.QueueUserWorkItem(ServerThread);
        }

        private void OutCallback(string line)
        {
          log.Debug(id+": "+line);
          if(line.Contains("Console initialized."))
            SendModCommands(serverProc.StandardInput);
          else if(line.Contains("Match signout")){
            serverProc.StandardInput.WriteLine("exit");
          }
        }

        private void ServerThread(object state)
        {
            while(!serverProc.HasExited){
              serverProc.StandardInput.WriteLine();
              Thread.Sleep(300);
            }
            if (OnShutdown != null)
                OnShutdown(this, EventArgs.Empty);
            shutdown = true;
        }

        public static Server Create(int id, int port, bool dev, string mod, string rconPass)
        {
            Process serverProc = new Process();
            ProcessStartInfo info = serverProc.StartInfo;
            info.FileName = ServerManager.exePath;
            info.Arguments = Settings.Default.args;
            info.CreateNoWindow = true;
            if(dev)
            {
                info.Arguments += " " + Settings.Default.devArgs;
            }
            info.Arguments += " -port " + port;
            info.Arguments += " +rcon_password "+rconPass;
            info.UseShellExecute = false;
            info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
            info.WorkingDirectory = Settings.Default.workingDir;
            info.EnvironmentVariables.Add("LD_LIBRARY_PATH", info.WorkingDirectory+":"+info.WorkingDirectory+"/bin");
            log.Debug(info.FileName+" "+info.Arguments);
            Server serv = new Server(serverProc, id, port, dev);
            serverProc.EnableRaisingEvents = true;
            serverProc.OutputDataReceived += (sender, args) => serv.OutCallback(args.Data);
            serverProc.ErrorDataReceived += (sender, args) => serv.OutCallback(args.Data);
            serverProc.Start();
            serverProc.BeginOutputReadLine();
            serverProc.BeginErrorReadLine();
            serv.StartThread();
            serv.mod = mod;
            log.Debug("server ID: "+id+" spawned, process ID "+serverProc.Id);
            return serv;
        }

        public void Shutdown()
        {
            log.Debug("shutting down scrds id: "+id);
            shutdown = true;
            serverProc.StandardInput.WriteLine("exit");
            Thread.Sleep(300);
            serverProc.Kill();
        }
    }
}
