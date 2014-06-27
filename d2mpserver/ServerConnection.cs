using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServerCommon.Methods;
using d2mpserver.Properties;
using XSockets.Client40;
using XSockets.Client40.Common.Event.Arguments;

namespace d2mpserver
{
    public class ServerConnection
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public bool infoValid = true;
        private ServerManager manager;
        private XSocketClient client;
        private ServerCommon.Encryption decryptor;
        private ServerCommon.Encryption encryptor;

        public ServerConnection(ServerManager manager)
        {
            this.manager = manager;
            SetupClient();
            try
            {
                client.Open();
            }
            catch (Exception ex)
            {
                log.Error("Can't connect.");
                AttemptReconnect();
            }
        }

        private void SetupClient()
        {
            log.Info("Setting up keys...");
            decryptor = new ServerCommon.Encryption(Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Properties.Settings.Default.keyName));
            log.Info(String.Format("Key location: {0}", decryptor.getKeyPath()));
#if DEBUG
            client = new XSocketClient("ws://localhost:4502/ServerController", "*");
#else
            client = new XSocketClient(Settings.Default.serverIP, "*");
#endif
            client.OnClose += (sender, args) => log.Debug("Disconnected from the server");
            client.OnError += (sender, args) => log.Error("Socket error: " + args.data);
            client.OnPing += OnPing;

            client.Bind("commands", e =>
            {
                log.Debug("Server message: " + e.data);
                var m = JObject.Parse(e.data).ToObject<ServerCommon.EncryptModel>();
                try
                {
                    ProcessMessage(decryptor.decrypt(m));
                }
                catch (FormatException)
                {
                    log.Warn("Message didn't get decrypted. Maybe an error message from the server?");
                    ProcessMessage(e.data);
                }
            });
            client.OnOpen += (sender, args) =>
            {
                log.Info("Connected to the server.");
                SendInit();
            };
            client.OnClose += (sender, args) =>
            {
                log.Info("Disconnected from the server.");
                AttemptReconnect();
            };
        }

        void OnPing(object sender, BinaryArgs e)
        {
        }


        private void AttemptReconnect()
        {
            Thread.Sleep(5000);
            SetupClient();
            log.Info("Attempting reconnect...");
            try
            {
                client.Open();
            }
            catch (Exception ex)
            {
                AttemptReconnect();
            }
        }

        private string GetPublicIpAddress()
        {
            var request = (HttpWebRequest)WebRequest.Create("http://ifconfig.me");
            request.UserAgent = "curl"; // this simulate curl linux command
            string publicIPAddress;
            request.Method = "GET";
            try
            {
                using (WebResponse response = request.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        publicIPAddress = reader.ReadToEnd();
                        return publicIPAddress.Replace("\n", "");
                    }
                }
            }
            catch (WebException)
            {
                log.Info(request.RequestUri.ToString() + " is down. Trying mirror...");
            }
            try
            {
                request = (HttpWebRequest)HttpWebRequest.Create("http://checkip.dyndns.org");
                using (WebResponse response = request.GetResponse())
                {
                    string text = new StreamReader(response.GetResponseStream()).ReadToEnd();
                    text = text.Substring(text.IndexOf(":") + 2);
                    text = text.Substring(0, text.IndexOf("<"));
                    return text;
                }
            }
            catch (WebException)
            {
                log.Fatal("Cannot determine external ip. All mirrors are down.");
                Environment.Exit(1);
            }
            return null;
        }

        private void PerformAddonOps(object state)
        {
            var command = (string[])state;

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
                        serv.OnReady += (sender, args) => Send(JObject.FromObject(new OnServerLaunched() { id = id }).ToString(Formatting.None));
                        serv.OnShutdown += (sender, args) => Send(JObject.FromObject(new OnServerShutdown() {id = id}).ToString(Formatting.None));
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
                    log.Debug("Auth password is invalid.");
                    log.Fatal("Server doesn't like our init info (we're probably out of date), shutting down...");
                    infoValid = false;
                    Environment.Exit(0);
                    break;
                case "keyFail":
                    {
                        log.Debug("Your ip or hostname is not recognized by the server.");
                        infoValid = false;
                        Environment.Exit(0);
                        break;
                    }
                case "serverPubKey":
                    {
                        log.Debug("Received public server key");
                        encryptor = new ServerCommon.Encryption(command[1], true);
                        break;
                    }
                case "outOfDate":
                    log.Info("Server is out of date (current version is " + Init.Version + "), updating...");
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

        void Send(string message)
        {
            if (encryptor != null)
            {
                client.Send(new TextArgs(JsonConvert.SerializeObject(encryptor.encrypt(message)), "data"));
                return;
            }
            client.Send(new TextArgs(message, "data"));
        }

        private void InstallAddon(string addon)
        {
            log.Debug("Attempting to install " + addon);
            var parts = addon.Split('>');
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
            var data = new Init()
                       {
                           addons = manager.GetAddonVersions(),
                           region = (ServerCommon.ServerRegion)((int)Settings.Default.serverRegion),
                           name = Settings.Default.serverName,
                           portRangeStart = Settings.Default.portRangeStart,
                           portRangeEnd = Settings.Default.portRangeEnd,
                           serverCount = Settings.Default.serverCount,
                           publicIP = GetPublicIpAddress()
                       };
            var json = JObject.FromObject(data);
            Send(json.ToString(Formatting.None));
        }


        public void Shutdown()
        {
            log.Debug("Shutting down...");
            if(client.IsConnected)
                client.Close();
            manager.ShutdownAllServers();
        }
    }
}
