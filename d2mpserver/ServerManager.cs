using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
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
        public static string workingdir;
        public static string steamCmdPath;
        public static string gameRoot;
        public static string addonsPath;
        public static string ourPath;
        private static volatile bool shutdown;
        Dictionary<int, Server> servers = new Dictionary<int, Server>();

        public Server LaunchServer(int id, int port, bool dev, string mod, string rconPass)
        {
            log.Info("Launching server, ID: " + id + " on port " + port + (dev ? " in devmode." : "."));
            var serv = Server.Create(id, port, dev, mod, rconPass);
            serv.OnShutdown += (sender, args) => servers.Remove(serv.id);
            servers.Add(id, serv);
            return serv;
        }

        public Server GetServer(int id)
        {
            if (!servers.ContainsKey(id))
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
            foreach (var server in servers)
            {
                server.Value.Shutdown();
            }
            servers.Clear();
        }

        public void Shutdown()
        {
          ShutdownAllServers();
          if(activeSteamCMD != null){
            activeSteamCMD.Kill();
          }
          shutdown = true;
        }

        private SteamCMD activeSteamCMD = null;
        public bool SetupEnvironment()
        {
            try
            {
                using (var client = new WebClient())
                {
                    log.Info("Setting up working directory...");
                    ourPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    workingdir = Settings.Default.workingDir.Replace("{{exeloc}}",
                        ourPath);
                    if (!Directory.Exists(workingdir))
                    {
                        Directory.CreateDirectory(workingdir);
                        Directory.CreateDirectory(Path.Combine(workingdir, "steam"));
                        Directory.CreateDirectory(Path.Combine(workingdir, "game"));
                    }

                    log.Debug("Working directory: " + workingdir);

                    log.Debug("Searching for SteamCMD...");
                    steamCmdPath = Path.Combine(workingdir, "steam/steamcmd.exe");
                    if (!File.Exists(steamCmdPath))
                    {
                        log.Debug("Downloading SteamCMD....");
                        client.DownloadFile(Settings.Default.steamcmd, steamCmdPath);
                    }
                    log.Debug("SteamCMD path: " + steamCmdPath);

                    log.Debug("Launching SteamCMD to update Dota (570)...");
                    activeSteamCMD = SteamCMD.LaunchSteamCMD("+app_update 570");
                    activeSteamCMD.WaitForExitSync();
                    log.Debug("SteamCMD finished! Continuing...");
                    activeSteamCMD = null;
                    if(shutdown) { log.Debug("Environment setup canceled!"); return false;}

                    log.Debug("Finding dota.exe (Dota 2 root)...");
                    var files = Directory.GetFiles(Path.Combine(workingdir, "game"), "dota.exe",
                        SearchOption.AllDirectories);
                    if (files.Length < 1)
                    {
                        log.Fatal("Could not find dota.exe in the game dirs! Make sure SteamCMD worked properly.");
                        return false;
                    }

                    gameRoot = Path.GetDirectoryName(files[0]);
                    log.Debug("Found Dota root: " + gameRoot);

                    log.Debug("Patching gameinfo.txt...");
                    PatchGameInfo(Path.Combine(gameRoot, "dota/gameinfo.txt"));

                    log.Debug("Searching for srcds.exe");
                    exePath = Path.Combine(gameRoot, "srcds.exe");
                    if (!File.Exists(exePath))
                    {
                        log.Debug("Downloading srcds.exe...");
                        client.DownloadFile(Settings.Default.srcds, exePath);
                    }
                    log.Debug("SRCDS path: " + exePath);

                    addonsPath = Path.Combine(gameRoot, "dota/addons/");
                    if (!Directory.Exists(addonsPath))
                    {
                        log.Fatal("Addons dir doesn't exist: " + addonsPath);
                        return false;
                    }

                    File.Copy(Path.Combine(ourPath, "metamod.vdf"), Path.Combine(addonsPath, "metamod.vdf"), true);

                    log.Info("Setup complete, continuing server startup...");
                }
            }
            catch (Exception ex)
            {
                log.Fatal("Failed to setup the environment! " + ex);
                return false;
            }
            return true;
        }

        private void PatchGameInfo(string combine)
        {
            File.Copy(Path.Combine(ourPath, "gameinfo.txt"), combine, true);
        }

        public string GetAddonVersions()
        {
            return String.Join(",", Directory.GetDirectories(addonsPath).Select(AddonInfo.DetectVersion).ToArray());
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

        public void StartThread()
        {
            ThreadPool.QueueUserWorkItem(ServerThread);
        }

        private void ServerThread(object state)
        {
            Thread.Sleep(5000);
            if (OnReady != null)
                OnReady(this, EventArgs.Empty);
            while (!serverProc.HasExited)
            {
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
            //info.CreateNoWindow = true;
            if (dev)
            {
                info.Arguments += " " + Settings.Default.devArgs;
            }
            info.Arguments += " -port " + port;
            info.Arguments += " +rcon_password " + rconPass;
            info.UseShellExecute = false;
            //info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
            info.WorkingDirectory = ServerManager.workingdir;
            info.EnvironmentVariables.Add("LD_LIBRARY_PATH", info.WorkingDirectory + ":" + info.WorkingDirectory + "/bin");
            log.Debug(info.FileName + " " + info.Arguments);
            Server serv = new Server(serverProc, id, port, dev);
            serverProc.Start();
            serv.StartThread();
            serv.mod = mod;
            log.Debug("server ID: " + id + " spawned, process ID " + serverProc.Id);
            return serv;
        }

        public void Shutdown()
        {
            log.Debug("shutting down scrds id: " + id);
            shutdown = true;
            serverProc.StandardInput.WriteLine("exit");
        }
    }
}
