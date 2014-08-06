// <copyright file="Program.cs">
// Copyright (c) 2014 All Right Reserved
//
// This source is subject to the License.
// Please see the License.txt file for more information.
// All other rights reserved.
//
// THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
// KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// </copyright>
// <author>Christian Stewart</author>
// <email>kidovate@gmail.com</email>
// <date>2014-05-10</date>
// <summary>Core D2Moddin manager launcher.</summary>
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace D2MPClientInstaller
{
    static class Program
    {
        private const bool doLog = true;
        private const string logFile = "d2mpinstaller.log";
        private static string ourDir;
        private static string installdir;
        static void Log(string text)
        {
            if (doLog) File.AppendAllText(Path.Combine(ourDir, logFile), text + "\n");
        }

        static void DeleteOurselves(string path)
        {
            var info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "/C timeout 2 & Del \"" + path + "\"";

            if (doLog)
                info.Arguments += " & Del \"" + Path.Combine(Path.GetDirectoryName(path), logFile) + "\"";

            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            Process.Start(info);
        }

        static void RunOurselves()
        {
            var proc = new Process();
            proc.StartInfo.FileName = Assembly.GetExecutingAssembly().Location;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
        }

        static void LaunchD2MP(string path)
        {
            var info = new ProcessStartInfo(path);
            info.WorkingDirectory = Path.GetDirectoryName(path);
            Log(string.Format("Starting on '{0}'", path));
            Process.Start(info);
        }

        static void ShutdownD2MP()
        {
            var exepath = Path.Combine(installdir, "d2mp.exe");
            var pidpath = Path.Combine(installdir, "d2mp.pid");
            if (Process.GetProcessesByName("d2mp").Length != 0)
            {
                if (File.Exists(pidpath))
                {
                    File.Delete(pidpath);
                    int wait = 0;
                    while (wait < 30 && Process.GetProcessesByName("d2mp").Length != 0)
                    {
                        Thread.Sleep(1000);
                        wait++;
                    }
                }
                var remaining = Process.GetProcessesByName("d2mp");
                foreach (var remain in remaining)
                {
                    remain.Kill();
                    remain.WaitForExit();
                }
            }
        }

        static void UninstallD2MP(string installdir)
        {
            ShutdownD2MP();
            //Delete all files 
            string[] filePaths = Directory.GetFiles(installdir);
            foreach (string filePath in filePaths)
            {
                File.Delete(filePath);
            }
        }

        static void ShowError(string message, string url = "")
        {
            using (var frm = new tryAgainForm(message))
            {
                if (!string.IsNullOrEmpty(url))
                {
                    frm.DownloadManuallyClick += (sender, args) => Process.Start(url);
                }
                else
                {
                    frm.DisableDownload();
                }

                if (frm.ShowDialog() == DialogResult.Retry)
                {
                    RunOurselves();
                }

                Environment.Exit(1);
            }
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ourDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Application.EnableVisualStyles();

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                Log(string.Format("Unhandled exception: {0}", args.ExceptionObject));
                ShowError(string.Format("Unhandled exception.\n{0}\nSee log for more details.", (args.ExceptionObject as Exception).Message));
            };

            Log("Finding install directories...");
            installdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "D2MP");
            var verpath = Path.Combine(installdir, "version.txt");
            Log("Temporary dir: " + installdir);
            Log("Verpath: " + verpath);
            if (!Directory.Exists(installdir))
                Directory.CreateDirectory(installdir);
            string infos;
            Log("Checking for client version...");
            using (WebClient client = new WebClient())
            {
                try
                {
                    infos = client.DownloadString("http://net1.d2modd.in/clientver");
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                    ShowError("Failed to download the latest client version information.\nCheck your internet connection!\nSomething is blocking us.");
                    return;//will exit after the error message anyway
                }
            }

            Log("Client info: \n" + infos);
            var info = infos.Split('|');
            Log("Version string: " + String.Join(",", info));
            var versplit = info[0].Split(':');
            if (versplit[0] == "version" && versplit[1] != "disabled")
            {
                //check for existing installed file
                if (!File.Exists(verpath) || File.ReadAllText(verpath) != versplit[1])
                {
                    Log("Uninstalling old version..");
                    try
                    {
                        UninstallD2MP(installdir);
                    }
                    catch (Exception ex)
                    {
                        Log("Problem uninstalling: " + ex);
                    }
                    Log("Downloading/unzipping new version...");
                    try
                    {
                        var dlPath = Path.Combine(installdir, "archive.zip");
                        using (WebClient client = new WebClient())
                        {
                            client.DownloadFile(info[1], dlPath);
                        }
                        d2mp.UnZip.unzipFromStream(File.OpenRead(dlPath), installdir);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString());
                        ShowError("Problem downloading new D2Moddin launcher:\n" + ex.Message, info[1]);
                        return;//will exit after the error message anyway
                    }
                }
            }
            else
            {
                try
                {
                    UninstallD2MP(installdir);
                }
                catch (Exception ex)
                {
                    Log("Problem uninstalling D2MP: " + ex);
                }
                return;
            }

            Log("Launching D2MP...");
            ShutdownD2MP();
            LaunchD2MP(Path.Combine(installdir, "d2mp.exe"));
            //delete ourselves
            Log("Deleting ourselves...");
            try
            {
                DeleteOurselves(Assembly.GetExecutingAssembly().Location);
            }
            catch (Exception ex)
            {
                Log("Problem deleting ourselves: " + ex);
            }
        }
    }
}
