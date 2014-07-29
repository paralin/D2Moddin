using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;

namespace D2MPBundler
{
    class Program
    {
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            if (!Directory.Exists(sourceDirName)) return;
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
        static void Main(string[] args)
        {
            string path;
            if (args.Length == 1)
            {
                path = args[0];
                if(!Directory.Exists(path))
                    path = Path.GetDirectoryName(path);
            }
            else
            {
                var folderBrowserDialog1 = new FolderBrowserDialog();
                if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
                {
                    path = folderBrowserDialog1.SelectedPath;
                }
                else
                {
                    return;
                }
            }
            var appPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            Console.WriteLine(path);
            var infoJson = Path.Combine(path, "info.json");
            if (!File.Exists(infoJson))
            {
                Console.WriteLine("info.json not found!");
                Console.ReadLine();
                return;
            }
            var data = JObject.Parse(File.ReadAllText(infoJson));
            var exclude = new List<string>();
            if (data.Property("exclude") != null)
            {
                exclude.AddRange(data.Value<JArray>("exclude").Select(m=>m.Value<string>()));
            }
            var name = data.Value<string>("name");
            var modpath = Path.Combine(path, name);
            if(Directory.Exists(modpath)) Directory.Delete(modpath, true);
            var srvZip = Path.Combine(path, "serv_" + name + ".zip");
            var cliZip = Path.Combine(path,  name + ".zip");
            if(File.Exists(srvZip)) File.Delete(srvZip);
            if(File.Exists(cliZip)) File.Delete(cliZip);
            Directory.CreateDirectory(modpath);
            DirectoryCopy(Path.Combine(path, "itembuilds"), Path.Combine(modpath, "itembuilds"), true);
            DirectoryCopy(Path.Combine(path, "maps"), Path.Combine(modpath, "maps"), true);
            DirectoryCopy(Path.Combine(path, "materials"), Path.Combine(modpath, "materials"), true);
            DirectoryCopy(Path.Combine(path, "particles"), Path.Combine(modpath, "particles"), true);
            DirectoryCopy(Path.Combine(path, "resource"), Path.Combine(modpath, "resource"), true);
            DirectoryCopy(Path.Combine(path, "scripts"), Path.Combine(modpath, "scripts"), true);
            DirectoryCopy(Path.Combine(path, "sound"), Path.Combine(modpath, "sound"), true);
            var addonInfo = string.Format(@"""AddonInfo""
{{
        addontitle              ""{0}""
        addonversion            {1}
}}", data.Value<string>("fullname"), data.Value<string>("version"));
            File.WriteAllText(Path.Combine(modpath, "addoninfo.txt"), addonInfo);
            ZipFiles(modpath, srvZip, null);
            ZipFilesB(modpath, cliZip);
            Directory.Delete(modpath, true);
            var cdnPath = Path.Combine(appPath, "cdn");
            if(Directory.Exists(cdnPath)) File.Copy(cliZip, Path.Combine(cdnPath, name+".zip"), true);
            var awsPath = Path.Combine(appPath, "aws");
            if (Directory.Exists(awsPath)) File.Copy(srvZip, Path.Combine(awsPath, "serv_" + name + ".zip"), true);
        }
        public static void ZipFiles(string inputFolderPath, string outputPathAndFile, string password)
        {
            ArrayList ar = GenerateFileList(inputFolderPath); // generate file list
            int TrimLength = (Directory.GetParent(inputFolderPath)).ToString().Length;
            // find number of chars to remove     // from orginal file path
            TrimLength += 1; //remove '\'
            FileStream ostream;
            byte[] obuffer;
            string outPath = outputPathAndFile;
            ZipOutputStream oZipStream = new ZipOutputStream(File.Create(outPath)); // create zip stream
            if (password != null && password != String.Empty)
                oZipStream.Password = password;
            oZipStream.SetLevel(9); // maximum compression
            ZipEntry oZipEntry;
            foreach (string Fil in ar) // for each file, generate a zipentry
            {
                oZipEntry = new ZipEntry(Fil.Remove(0, TrimLength));
                oZipStream.PutNextEntry(oZipEntry);

                if (!Fil.EndsWith(@"/")) // if a file ends with '/' its a directory
                {
                    ostream = File.OpenRead(Fil);
                    obuffer = new byte[ostream.Length];
                    ostream.Read(obuffer, 0, obuffer.Length);
                    oZipStream.Write(obuffer, 0, obuffer.Length);
                    ostream.Close();
                }
            }
            oZipStream.Finish();
            oZipStream.Close();
        }
        public static void ZipFilesB(string inputFolderPath, string outputPathAndFile)
        {
            ArrayList ar = GenerateFileListB(inputFolderPath); // generate file list
            int TrimLength = inputFolderPath.Length;
            // find number of chars to remove     // from orginal file path
            TrimLength += 1; //remove '\'
            FileStream ostream;
            byte[] obuffer;
            string outPath = outputPathAndFile;
            ZipOutputStream oZipStream = new ZipOutputStream(File.Create(outPath)); // create zip stream
            oZipStream.SetLevel(9); // maximum compression
            ZipEntry oZipEntry;
            foreach (string Fil in ar) // for each file, generate a zipentry
            {
                oZipEntry = new ZipEntry(Fil.Remove(0, TrimLength));
                oZipStream.PutNextEntry(oZipEntry);

                if (!Fil.EndsWith(@"/")) // if a file ends with '/' its a directory
                {
                    ostream = File.OpenRead(Fil);
                    obuffer = new byte[ostream.Length];
                    ostream.Read(obuffer, 0, obuffer.Length);
                    oZipStream.Write(obuffer, 0, obuffer.Length);
                    ostream.Close();
                }
            }
            oZipStream.Finish();
            oZipStream.Close();
        }
        private static ArrayList GenerateFileList(string Dir)
        {
            ArrayList fils = new ArrayList();
            bool Empty = true;
            foreach (string file in Directory.GetFiles(Dir)) // add each file in directory
            {
                fils.Add(file);
                Empty = false;
            }

            if (Empty)
            {
                if (Directory.GetDirectories(Dir).Length == 0)
                    // if directory is completely empty, add it
                {
                    fils.Add(Dir + @"/");
                }
            }

            foreach (string dirs in Directory.GetDirectories(Dir)) // recursive
            {
                foreach (object obj in GenerateFileList(dirs))
                {
                    fils.Add(obj);
                }
            }
            return fils; // return file list
        }
        private static ArrayList GenerateFileListB(string Dir)
        {
            ArrayList fils = new ArrayList();
            bool Empty = true;
            foreach (string file in Directory.GetFiles(Dir)) // add each file in directory
            {
                fils.Add(file);
                Empty = false;
            }

            if (Empty)
            {
                if (Directory.GetDirectories(Dir).Length == 0)
                // if directory is completely empty, add it
                {
                    fils.Add(Dir + @"/");
                }
            }

            foreach (string dirs in Directory.GetDirectories(Dir)) // recursive
            {
                if (dirs.Contains("vscripts")) continue;
                foreach (object obj in GenerateFileListB(dirs))
                {
                    fils.Add(obj);
                }
            }
            return fils; // return file list
        }


        public static void UnZipFiles(string zipPathAndFile, string outputFolder, string password, bool deleteZipFile)
        {
            ZipInputStream s = new ZipInputStream(File.OpenRead(zipPathAndFile));
            if (password != null && password != String.Empty)
                s.Password = password;
            ZipEntry theEntry;
            string tmpEntry = String.Empty;
            while ((theEntry = s.GetNextEntry()) != null)
            {
                string directoryName = outputFolder;
                string fileName = Path.GetFileName(theEntry.Name);
                // create directory 
                if (directoryName != "")
                {
                    Directory.CreateDirectory(directoryName);
                }
                if (fileName != String.Empty)
                {
                    if (theEntry.Name.IndexOf(".ini") < 0)
                    {
                        string fullPath = directoryName + "\\" + theEntry.Name;
                        fullPath = fullPath.Replace("\\ ", "\\");
                        string fullDirPath = Path.GetDirectoryName(fullPath);
                        if (!Directory.Exists(fullDirPath)) Directory.CreateDirectory(fullDirPath);
                        FileStream streamWriter = File.Create(fullPath);
                        int size = 2048;
                        byte[] data = new byte[2048];
                        while (true)
                        {
                            size = s.Read(data, 0, data.Length);
                            if (size > 0)
                            {
                                streamWriter.Write(data, 0, size);
                            }
                            else
                            {
                                break;
                            }
                        }
                        streamWriter.Close();
                    }
                }
            }
            s.Close();
            if (deleteZipFile)
                File.Delete(zipPathAndFile);
        }
    }
}
