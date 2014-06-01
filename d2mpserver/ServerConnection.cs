using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Runtime.Remoting.Channels;
using System.Threading;
using WebSocketSharp;
using d2mpserver.Properties;

namespace d2mpserver
{
    public class ServerConnection
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public WebSocket socket = null;
        public bool infoValid = true;
        private bool shutdown = false;
        private bool reconnecting = false;
        private ServerManager manager;

        public ServerConnection(ServerManager manager)
        {
            this.manager = manager;
        }

        void RegisterCallbacks()
        {
            socket.OnClose += (sender, args) =>
            {
                log.Debug("Disconnected from the server");
                reconnecting = true;
            };
            socket.OnError += (sender, args) =>
            {
                log.Error("Socket error: " + args.Message);
            };
            socket.OnMessage += (sender, args) =>
            {
                log.Debug("Server message: " + args.Data);
                ProcessMessage(args.Data);
            };
            socket.OnOpen += (sender, args) =>
            {
                log.Info("Connected to the server.");
                SendInit();
            };
        }

        private void PerformAddonOps(object state)
        {
            string[] command = (string[])state;

            if (command[1] != "")
            {
                log.Debug("Installing addons: " + command[1]);
                string[] addons = command[1].Split(',');
                foreach (var addon in addons)
                {
                    InstallAddon(addon);
                }
            }
            if (command[2] != "")
            {
                string[] deletions = command[2].Split(',');
                log.Debug("Deleting addons: " + command[2]);
                foreach (var deletion in deletions)
                {
                    DeleteAddon(deletion);
                }
            }
            SendInit();
        }

        private void ProcessMessage(string data)
        {
            string[] command = data.Split('|');
            switch (command[0])
            {
                case "shutdown":
                    {
                        Program.ShutdownAll();
                    }
                    break;
                case "restart":
                    {
                        ServerUpdater.RestartD2MP();
                        Program.ShutdownAll();
                    }
                    break;
                case "reinit":
                    {
                        SendInit();
                    }
                    break;
                case "addonOps":
                    {
                        ThreadPool.QueueUserWorkItem(PerformAddonOps, command);
                    }
                    break;
                case "launchServer":
                    {
                        int id = int.Parse(command[1]);
                        int port = int.Parse(command[2]);
                        bool dev = bool.Parse(command[3]);
                        string mod = command[4];
                        string rconPass = command[5];
                        string[] commands = command[6].Split('&');
                        var serv = manager.LaunchServer(id, port, dev, mod, rconPass, commands);
                        serv.OnReady += (sender, args) =>
                        {
                            socket.SendAsync("serverLaunched|" + id, b => { });
                            log.Debug("server finished launching " + id);
                        };
                        serv.OnShutdown += (sender, args) =>
                        {
                            socket.SendAsync("onShutdown|" + id,
                            b => { });
                            log.Debug("server shut down on its own: " + id);
                        };
                        break;
                    }
                case "setMaxLobbies":
                    {
                        int max = int.Parse(command[1]);
                        Settings.Default["serverCount"] = max;
                        var configPath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoaming).FilePath;
                        Settings.Default.Save();
                        log.Info("Server set the max lobby count to "+max);
                        log.Debug("Saved config: "+configPath);
                        break;
                    }
                case "setServerRegion":
                    {
                        Settings.Default["serverRegion"] = (ServerRegion)int.Parse(command[1]);
                        Settings.Default.Save();
                        log.Debug("The server region was set to "+command[1]);
                        break;
                    };
                case "setServerName":
                    {
                        Settings.Default["serverName"] = command[1];
                        Settings.Default.Save();
                        log.Debug("Server set our name to "+command[1]);
                        break;
                    }
                case "shutdownServer":
                    {
                        int id = int.Parse(command[1]);
                        manager.ShutdownServer(id);
                    }
                    break;
                case "authFail":
                    log.Debug("Auth password: " + Settings.Default.connectPassword + " is invalid.");
                    log.Fatal("Server doesn't like our init info (we're probably out of date), shutting down...");
                    infoValid = false;
                    Environment.Exit(0);
                    break;
                case "outOfDate":
                    log.Info("Server is out of date (current version is " + ServerUpdater.version + "), updating...");
                    if (!Settings.Default.disableUpdate)
                    {
                        var url = command[1];
                        log.Debug("Downloading update from " + url + "...");
                        ServerUpdater.UpdateFromURL(url);
                        break;
                    }
                    else
                    {
                        log.Fatal("Server out of date but auto updating disabled. Exiting...");
                        Program.shutdown = true;
                    }
                    break;
            }
        }

        private void InstallAddon(string addon)
        {
            log.Debug("Attempting to install " + addon);
            var parts = addon.Split('=');
            //get path to addon
            log.Debug("Clearing addon directory...");
            var path = Path.Combine(ServerManager.addonsPath, parts[0]);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            Directory.CreateDirectory(path);
            log.Debug("Downloading the addon to memory...");
            try
            {
                using (var client = new WebClient())
                {
                    using (var mem = new MemoryStream(client.DownloadData(parts[2].Replace('+', '='))))
                    {
                        log.Debug("Downloaded addon, length " + mem.Length + " bytes.");
                        mem.Position = 0;
                        try
                        {
                            Utils.UnzipFromStream(mem, ServerManager.addonsPath);
                        }
                        catch (Exception ex)
                        {
                            log.Error("Failed to unzip the mod properly!");
                            log.Error(ex.ToString());
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to download the addon to the memory!");
                log.Error(ex);
                return;
            }
            log.Debug("Addon downloaded and unzipped: " + parts[0]);
            var mapspath = Path.Combine(path, "maps");
            if (Directory.Exists(mapspath))
            {
                log.Debug("Maps directory exists, copying maps...");
                var gmapspath = Path.Combine(ServerManager.gameRoot, "dota/maps/");
                try
                {
                    foreach (var file in Directory.GetFiles(mapspath))
                        File.Copy(file, Path.Combine(gmapspath, Path.GetFileName(file)), true);
                }
                catch (Exception ex)
                {
                    log.Error("Can't copy the maps over, probably SRCDS is running.");
                }
            }
        }

        private void DeleteAddon(string addon)
        {
            log.Debug("Attempting to delete " + addon);
            //get path to addon
            var path = Path.Combine(ServerManager.addonsPath, addon);
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
            else
            {
                log.Error("Addon doesn't exist?");
            }
        }

        public void SendInit()
        {
            var msg = "init|" + Settings.Default.connectPassword + "|" + Settings.Default.serverCount + "|" + GetAddonVersionsString() + "|" + ServerUpdater.version + "|" + Settings.Default.portRangeStart + "-" + Settings.Default.portRangeEnd+"|"+(int)Settings.Default.serverRegion+"|"+Settings.Default.serverName.Replace('|', ' ')+"|"+(Settings.Default.publicAddr);
            socket.Send(msg);
        }

        private string GetAddonVersionsString()
        {
            return manager.GetAddonVersions();
        }

        public bool Connect()
        {
            if (socket != null && socket.IsAlive)
            {
                log.Debug("Already connected.");
                return true;
            }
            log.Debug("attempting connection to " + Settings.Default.serverIP);
            socket = new WebSocket(Settings.Default.serverIP);
            RegisterCallbacks();
            try
            {
                socket.Connect();
            }
            catch (Exception ex)
            {
                log.Error("Problem connecting: " + ex);
                return false;
            }

            return socket.IsAlive;
        }

        public void Disconnect()
        {
            if (socket == null || !socket.IsAlive)
                return;

            socket.Close();
            socket = null;
        }

        public void Shutdown()
        {
            shutdown = true;
            log.Debug("Shutting down...");
        }

        public void StartServerThread()
        {
            ThreadPool.QueueUserWorkItem(ServerThread);
        }

        private void ServerThread(object state)
        {
            while (!shutdown)
            {
                if (reconnecting)
                {
                    log.Debug("Attempting reconnection...");
                    if (!Connect())
                    {
                        log.Debug("... failed, will try again in 10 seconds ...");
                        //manager.ShutdownAllServers();
                        Thread.Sleep(9900);
                    }
                    else
                    {
                        reconnecting = false;
                    }
                }
                Thread.Sleep(100);
            }
            manager.ShutdownAllServers();
        }
    }
}
