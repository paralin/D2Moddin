using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;

namespace d2mpserver
{
    public class ServerUpdater
    {
        public static string version = "1.0.3";
        private static string fromUrl;

        public static void UpdateFromURL(string url)
        {
            fromUrl = url;
            ThreadPool.QueueUserWorkItem(UpdateThread);
        }

        private static void UpdateThread(object state)
        {
            string rootDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string updateDir = Path.Combine(rootDir, "updatestaging");
            if (Directory.Exists(updateDir))
            {
                Directory.Delete(updateDir, true);
            }
            Directory.CreateDirectory(updateDir);
            using (var wc = new WebClient())
            {
                Utils.UnzipFromStream(wc.OpenRead(fromUrl), updateDir);
            }
            string command = "copy /B /Y "+Path.Combine(updateDir, "*.*")+" "+rootDir+" & start /d \""+rootDir+"\" "+Path.GetFileName(Assembly.GetExecutingAssembly().Location)+" & rmdir \""+updateDir+"\" /s /q & exit";
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C timeout 2 & " + command;
            info.UseShellExecute = false;
            Process.Start(info);
            Environment.Exit(0);
        }

        public static void RestartD2MP()
        {
            string command = "start /d \"" + Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\" " + Path.GetFileName(Assembly.GetExecutingAssembly().Location) + " & exit";
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C timeout 1 & " + command;
            info.UseShellExecute = false;
            Process.Start(info);
        }
    }
}
