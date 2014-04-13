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
        public static string version = "1.1.1";
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

            //Make log dir
            var logDir = Path.Combine(rootDir, "oldlogs");
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            var logDirV = Path.Combine(logDir, ServerUpdater.version);
            if (!Directory.Exists(logDirV))
            {
                Directory.CreateDirectory(logDirV);
            }

            //Move all log files
            List<string> commands = Directory.EnumerateFiles(rootDir, "*.log").Select(logFile => string.Format("move /Y \"{0}\" \"{1}\"", logFile, Path.Combine(logDirV, Path.GetFileName(logFile)))).ToList();

            //Delete everything from our local folder
            commands.Add("del /F /Q \""+Path.Combine(rootDir, "*.*")+"\"");

            //Copy in the update
            commands.Add("copy /B /Y "+Path.Combine(updateDir, "*.*")+" "+rootDir);

            //Start d2mpserver
            commands.Add("start /d \""+rootDir+"\" "+Path.GetFileName(Assembly.GetExecutingAssembly().Location));

            //Delete the update dir
            commands.Add("rmdir \""+updateDir+"\" /s /q");
            
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C timeout 2 & " + string.Join(" & ", commands)+" & exit";
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
