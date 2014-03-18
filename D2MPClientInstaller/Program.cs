using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;

namespace D2MPClientInstaller
{
    static class Program
    {
        private static bool doLog = false;
        static void Log(string text)
        {
            if(doLog)
                File.AppendAllText("d2mpinstaller.log", text+"\n");
        }

        static void DeleteOurselves(string path)
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe");
            info.Arguments = "ping 192.0.2.2 -n 1 -w 3000 > nul & Del " + path;
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
                // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                // Optionally match entrynames against a selection list here to skip as desired.
                // The unpacked length is available in the zipEntry.Size property.

                byte[] buffer = new byte[4096];     // 4K is optimum

                // Manipulate the output filename here as desired.
                String fullZipToPath = Path.Combine(outFolder, entryFileName);
                string directoryName = Path.GetDirectoryName(fullZipToPath);
                if (directoryName.Length > 0)
                    Directory.CreateDirectory(directoryName);

                // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                // of the file, but does not waste memory.
                // The "using" will close the stream even if an exception occurs.
                using (FileStream streamWriter = File.Create(fullZipToPath))
                {
                    StreamUtils.Copy(zipInputStream, streamWriter, buffer);
                }
                zipEntry = zipInputStream.GetNextEntry();
            }
        }

        static void LaunchD2MP(string path)
        {
            var info = new ProcessStartInfo(path);
            info.WorkingDirectory = Path.GetDirectoryName(path);
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
            Log("Finding install directories...");
            var installdir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "D2MP");
            var verpath = Path.Combine(installdir, "version.txt");
            Log("Temporary dir: " + installdir);
            Log("Verpath: "+verpath);
            if(!Directory.Exists(installdir))
                Directory.CreateDirectory(installdir);
            var currpath = Assembly.GetExecutingAssembly().Location;
            if(Path.GetDirectoryName(currpath) != installdir)
            {
                Log("Copying ourselves into the temporary directory...");
                var target = Path.Combine(installdir, "installer.exe");

                try
                {
                    File.Delete(target);
                }catch
                {
                    Log("Installer does not already exist in target dir.");
                }

                File.Copy(currpath, target);
                LaunchD2MP(target);
                Log("Deleting ourselves...");
                DeleteOurselves(currpath);
                return;
            }
            
            //We are in the install dir, download files
            WebClient client = new WebClient();
            var info = client.DownloadString("http://d2modd.in:3000/clientver").Split('|');
            Log("Version string: "+String.Join(",", info));
            var versplit = info[0].Split(':');
            var verstr = versplit[1];
            if(versplit[0] == "version" && versplit[1] != "disabled")
            {
                //check for existing installed file
                if(!File.Exists(verpath) || File.ReadAllText(verpath) != versplit[1])
                {
                    Log("Uninstalling old version..");
                    UninstallD2MP(installdir);
                    Log("Unzipping new version...");
                    UnzipFromStream(client.OpenRead(info[1]), installdir);
                }
            }else
            {
                UninstallD2MP(installdir);
            }

            var exepath = Path.Combine(installdir, "d2mp.exe");

            if(File.Exists(exepath) && Process.GetProcessesByName("d2mp").Length == 0)
            {
                LaunchD2MP(exepath);
            }

            //delete ourselves
            DeleteOurselves(currpath);
            return;
        }
    }
}
