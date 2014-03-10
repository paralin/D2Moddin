using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;
using WebSocketSharp;

namespace d2mp
{
    public class D2MP
    {
        private static string server = "ws://d2modd.in:3005/";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void DeleteOurselves()
        {
            var currpath = Assembly.GetExecutingAssembly().Location;
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C choice /C Y /N /D Y /T 1 & Del " + currpath;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            Process.Start(info);
        }

        static void UninstallD2MP()
        {
            //Delete all files 
            var d2mpexecutable = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
            string[] filePaths = Directory.GetFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            foreach (string filePath in filePaths)
            {
                var name = new FileInfo(filePath).Name;
                name = name.ToLower();
                if (name != d2mpexecutable)
                {
                    File.Delete(filePath);
                }
            }
        }
        
        public static void main()
        {
            log.Debug("D2MP starting...");

            try
            {
                var steam = new SteamFinder();
                var steamDir = steam.FindSteam(true);
                if (steamDir == null)
                {
                    log.Fatal("Steam was not found!");
                    return;
                }
                else
                {
                    log.Debug("Steam found: " + steamDir);
                }

                //Detect user
                var config = File.ReadAllText(Path.Combine(steamDir, @"config\config.vdf"));
                var matches = Regex.Match(config, "\"\\d{17}\"");
                string steamid;
                if (matches.Success)
                {
                    steamid = matches.Value.Substring(1).Substring(0, matches.Value.Length - 2);
                    log.Debug("Steam ID detected: " + steamid);
                }
                else
                {
                    log.Fatal("Could not detect steam ID.");
                    return;
                }

                bool shutDown = false;
                int tryCount = 0;
                while(tryCount < 10 && !shutDown){
                    using (var ws = new WebSocket(server))
                    {
                        ws.OnMessage += (sender, e) =>
                            {
                                log.Debug("server: " + e.Data);
                                if (e.Data == "invalidid")
                                {
                                    log.Debug("Invalid ID!");
                                    shutDown = true;
                                    return;
                                }
                                else if (e.Data == "uninstall")
                                {
                                    log.Debug("Uninstalling due to server request...");
                                    UninstallD2MP();
                                    DeleteOurselves();
                                    shutDown = true;
                                    return;
                                }

                                var msgParts = e.Data.Split(':');
                                switch (msgParts[0])
                                {
                                    case "installmod":
                                        var modname = msgParts[1];
                                        log.Info("Server requested that we install mod " + modname);
                                        break;
                                    default:
                                        log.Error("Command not recognized: " + msgParts[0]);
                                        break;
                                }
                            };

                        ws.OnOpen += (sender, e) => log.Debug("Connected");
                        ws.OnClose += (sender, args) => log.Debug("Disconnected");
                        ws.Connect();
                        tryCount++;
                        if (!ws.IsAlive)
                        {
                            log.Debug("Can't connect to server, tries: " + tryCount);
                            tryCount++;
                            continue;
                        }

                        try
                        {
                            var ver =
                                File.ReadAllText(
                                    Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                                                 "version.txt"));
                            log.Debug("sending version: " + ver);
                            ws.Send("init:" + steamid + ":" + ver);
                        }
                        catch (Exception ex)
                        {
                            log.Debug("Can't detect ID from version.txt, : " + ex);
                            return;
                        }

                        tryCount = 0;
                        while(ws.IsAlive && !shutDown)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    Thread.Sleep(1000);
                }
            }catch(Exception ex)
            {
                log.Fatal("Overall error in the program: "+ex);
            }
        }
    }

    public class SteamFinder
    {
        private string cachedLocation;
        private static string[] knownLocations = new string[] { @"C:\Steam\", @"C:\Program Files (x86)\Steam\", @"C:\Program Files\Steam\" };

        public SteamFinder()
        {
            cachedLocation = "";
        }

        bool ContainsSteam(string dir)
        {
            return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Steam.exe"));
        }

        public string FindSteam(bool delCache)
        {
            if (delCache) cachedLocation = "";
            if(delCache || cachedLocation == "")
            {
                foreach(var loc in knownLocations)
                {
                    if(ContainsSteam(loc))
                    {
                        cachedLocation = loc;
                        return loc;
                    }
                }

                //Get from registry?
                RegistryKey regKey = Registry.CurrentUser;
                regKey = regKey.OpenSubKey(@"Software\Valve\Steam");

                if (regKey != null)
                {
                    cachedLocation = regKey.GetValue("SteamPath").ToString();
                    return cachedLocation;
                }

                //Search using file search? Eh... Return null.
                return null;
            }else
            {
                return cachedLocation;
            }
        }
    }
}
