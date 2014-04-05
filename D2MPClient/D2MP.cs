using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Win32;
using WebSocketSharp;

namespace d2mp
{
    public class D2MP
    {
        private static string server = "ws://d2modd.in:3005/";
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static WebSocket ws;
        private static string addonsDir;
        public static bool shutDown = false;
        public static bool doUninstall = false;
        private static string[] modNames = null;

        static void DeleteOurselves()
        {
            var currpath = Assembly.GetExecutingAssembly().Location;
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C timeout 3 & Del " + currpath;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            Process.Start(info);
        }

        static void UnzipFromStream(Stream zipStream, string outFolder)
        {

            ZipInputStream zipInputStream = new ZipInputStream(zipStream);
            ZipEntry zipEntry = zipInputStream.GetNextEntry();
            while (zipEntry != null)
            {
                String entryFileName = zipEntry.Name;
                log.Debug(" --> " + entryFileName);
                // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                // Optionally match entrynames against a selection list here to skip as desired.
                // The unpacked length is available in the zipEntry.Size property.

                byte[] buffer = new byte[4096];     // 4K is optimum

                // Manipulate the output filename here as desired.
                String fullZipToPath = Path.Combine(outFolder, entryFileName);
                string directoryName = Path.GetDirectoryName(fullZipToPath);
                if (directoryName.Length > 0)
                {
                    Directory.CreateDirectory(directoryName);
                    Thread.Sleep(30);
                }

                if (Path.GetFileName(fullZipToPath) != String.Empty)
                {
                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipInputStream, streamWriter, buffer);
                    }
                }
                zipEntry = zipInputStream.GetNextEntry();
            }
        }

        static void UninstallD2MP()
        {
            //Delete all files 
            var d2mpexecutable = Path.GetFileName(Assembly.GetExecutingAssembly().Location).ToLower();
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
            DeleteOurselves();
        }

        public static void main()
        {
            log.Debug("D2MP starting...");

            var iconThread = new Thread(delegate()
            {
                using (var icon = new ProcessIcon())
                {
                    icon.Display();
                    Application.Run();
                }
            });

            iconThread.SetApartmentState(ApartmentState.STA);
            iconThread.Start();

            try
            {
                var steam = new SteamFinder();
                var steamDir = steam.FindSteam(true);
                var dotaDir = steam.FindDota(true);
                if (steamDir == null || dotaDir == null)
                {
                    log.Fatal("Steam/dota was not found!");
                    return;
                }
                else
                {
                    log.Debug("Steam found: " + steamDir);
                    log.Debug("Dota found: " + dotaDir);
                }

                addonsDir = dotaDir;
                if (!Directory.Exists(addonsDir))
                {
                    log.Fatal("Addons dir: " + addonsDir + " does not exist.");
                    return;
                }

                
                {
                    var dirs = Directory.GetDirectories(addonsDir);
                    modNames = new string[dirs.Length];
                    int i = 0;
                    foreach (var dir in dirs)
                    {
                        var modName = Path.GetFileName(dir);
                        log.Debug("Found mod: " + modName + " detecting version...");
                        var infoPath = Path.Combine(addonsDir, modName + "/addoninfo.txt");
                        string versionFile = "";
                        if (File.Exists(infoPath))
                        {
                            versionFile = File.ReadAllText(infoPath);
                        }
                        var match = Regex.Match(versionFile, @"(addonversion)(\s+)(\d+\.)?(\d+\.)?(\d+\.)?(\*|\d+)",
                            RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string version = match.Groups.Cast<Group>()
                                .ToList()
                                .Skip(3)
                                .Aggregate("", (current, part) => current + part.Value);
                            log.Debug(modName + "=" + version);
                            modNames[i] = modName + "=" + version;
                        }
                        else
                        {
                            log.Error("Can't find version info for mod: " + modName + ", not including");
                            modNames[i] = modName + "=?";
                        }
                        i++;
                    }
                }

                //Detect user
                var config = File.ReadAllText(Path.Combine(steamDir, @"config\config.vdf"));
                var matches = Regex.Matches(config, "\"\\d{17}\"");
                string steamid;
                List<string> steamids = new List<string>();
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        steamid = match.Value.Substring(1).Substring(0, match.Value.Length - 2);
                        log.Debug("Steam ID detected: " + steamid);
                        steamids.Add(steamid);
                    }
                }
                else
                {
                    log.Fatal("Could not detect steam ID.");
                    return;
                }

                shutDown = false;
                int tryCount = 0;
                while (tryCount < 10 && !shutDown)
                {
                    using (ws = new WebSocket(server))
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

                                            if (e.Data == "close")
                                            {
                                                log.Debug("Shutting down due to server request.");
                                                shutDown = true;
                                                return;
                                            }

                                            if (e.Data == "uninstall")
                                            {
                                                log.Debug("Uninstalling due to server request...");
                                                UninstallD2MP();
                                                shutDown = true;
                                                return;
                                            }

                                            var msgParts = e.Data.Split(':');
                                            switch (msgParts[0])
                                            {
                                                case "installmod":
                                                    ThreadPool.QueueUserWorkItem(InstallMod, msgParts);
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
                            ws.Send("init:" + String.Join(",", steamids.ToArray(), 0, steamids.Count) + ":" + ver +
                                    ":" + String.Join(",", modNames));
                        }
                        catch (Exception ex)
                        {
                            log.Debug("Can't detect ID from version.txt, : " + ex);
                            return;
                        }

                        tryCount = 0;
                        while (ws.IsAlive && !shutDown)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                log.Fatal("Overall error in the program: " + ex);
            }

            if (doUninstall)
            {
                UninstallD2MP();
            }

            Application.Exit();
        }

        private static void InstallMod(object state)
        {
            var msgParts = (string[])state;
            var modp = msgParts[1].Split('=');
            var modname = modp[0];
            var url = "http:" + msgParts[3];
            log.Info("Server requested that we install mod " + modname + " from download " + url);

            //delete if already exists
            var targetDir = Path.Combine(addonsDir, modname);
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);
            //Make the dir again
            Directory.CreateDirectory(targetDir);
            //Stream the ZIP to the folder
            WebClient client = new WebClient();
            UnzipFromStream(client.OpenRead(url), targetDir);
            log.Info("Mod installed!");
            ws.Send("installedMod:" + modname);
            for(int i=0; i<modNames.Length; i++)
            {
                string[] parts = modNames[i].Split('=');
                if (parts[0] == modname)
                {
                    modNames[i] = msgParts[1];
                    return;
                }
            }
            var newArr = new string[modNames.Length + 1];
            for (int i = 0; i < modNames.Length; i++)
            {
                newArr[i] = modNames[i];
            }
            newArr[modNames.Length] = msgParts[1];
            modNames = newArr;
        }

        public static void Restart()
        {
            Process proc = new Process();
            proc.StartInfo.FileName = Assembly.GetExecutingAssembly().Location;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
            shutDown = true;
        }

        public static void ShowModList()
        {
            string message = "You currently have the following detected mods installed:\n\n"+String.Join(", ", modNames);
            MessageBox.Show(message, "Installed Mods");
        }
    }

    public class SteamFinder
    {
        private string cachedLocation = "";
        private string cachedDotaLocation = "";
        private static string[] knownLocations = new string[] { @"C:\Steam\", @"C:\Program Files (x86)\Steam\", @"C:\Program Files\Steam\" };

        public SteamFinder()
        {
        }

        bool ContainsSteam(string dir)
        {
            return Directory.Exists(dir) && File.Exists(Path.Combine(dir, "Steam.exe"));
        }

        public string FindSteam(bool delCache)
        {
            if (delCache) cachedLocation = "";
            if (delCache || cachedLocation == "")
            {
                foreach (var loc in knownLocations)
                {
                    if (ContainsSteam(loc))
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
            }
            else
            {
                return cachedLocation;
            }
        }

        public string FindDota(bool delCache)
        {
            if (!delCache && cachedDotaLocation != null) return cachedDotaLocation;
            var steamDir = FindSteam(false);
            if (steamDir != null)
            {
                var dir = Path.Combine(steamDir, "steamapps/common/dota 2 beta/dota/addons/");
                if (Directory.Exists(dir))
                {
                    cachedDotaLocation = dir;
                    return dir;
                }
            }

            //Get from registry
            RegistryKey regKey = Registry.LocalMachine;
            regKey =
                regKey.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 570");

            if (regKey != null)
            {
                cachedDotaLocation = regKey.GetValue("InstallLocation").ToString();
                return cachedDotaLocation;
            }

            return null;
        }
    }
}
