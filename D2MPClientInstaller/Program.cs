using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Threading;

namespace D2MPClientInstaller
{
    static class Program
    {
        static void DeleteOurselves(string path)
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C choice /C Y /N /D Y /T 1 & Del " + path;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            Process.Start(info);
        }

        static void UninstallD2MP(string installdir)
        {
            Process[] proc = Process.GetProcessesByName("d2mp");
            if (proc.Length != 0)
            {
                foreach(var process in proc)
                {
                    process.Kill();
                }
            }
            Thread.Sleep(2000);
            //Delete all files 
            string[] filePaths = Directory.GetFiles(installdir);
            foreach (string filePath in filePaths)
            {
                var name = new FileInfo(filePath).Name;
                name = name.ToLower();
                if (name != "installer.exe")
                {
                    File.Delete(filePath);
                }
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            var installdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "D2MP");
            var verpath = Path.Combine(installdir, "version.txt");
            string pakpath = Path.Combine(installdir, "pak.zip");
            if(!Directory.Exists(installdir))
                Directory.CreateDirectory(installdir);
            var currpath = Assembly.GetExecutingAssembly().Location;
            if(Path.GetDirectoryName(currpath) != installdir)
            {
                var target = Path.Combine(installdir, "installer.exe");
                File.Delete(target);
                File.Copy(currpath, target);
                Process.Start(target);
                DeleteOurselves(currpath);
                return;
            }
            
            //We are in the install dir, download files
            WebClient client = new WebClient();
            var info = client.DownloadString("http://d2mp.herokuapp.com/clientver").Split('|');
            var versplit = info[0].Split(':');
            var verstr = versplit[1];
            if(versplit[0] == "version" && versplit[1] != "disabled")
            {
                //check for existing installed file
                if(!File.Exists(verpath) || File.ReadAllText(verpath) != versplit[1])
                {
                    UninstallD2MP(installdir);
                    client.DownloadFile(info[1], pakpath);
                    ZipFile.ExtractToDirectory(pakpath, installdir);
                    File.Delete(pakpath);
                    Process.Start(Path.Combine(installdir, "d2mp.exe"));
                }
            }else
            {
                UninstallD2MP(installdir);
            }
            //delete ourselves
            DeleteOurselves(currpath);
            return;
        }
    }
}
