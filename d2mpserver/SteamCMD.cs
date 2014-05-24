using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using d2mpserver.Properties;

namespace d2mpserver
{
    /// <summary>
    /// Manages a running SteamCMD instance.
    /// </summary>
    public class SteamCMD
    {
        private Process proc;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        public SteamCMD(Process process)
        {
            proc = process;
        }

        public void Kill(){
          proc.Kill();
        }

        public void ToSTDIN(string command)
        {
            proc.StandardInput.WriteLine(command);
        }

        public void StartThread()
        {
            ThreadPool.QueueUserWorkItem(ServerThread);
        }

        private void OutCallback(string line)
        {
            log.Debug(line);
        }

        private void ServerThread(object state)
        {
            while (!proc.HasExited)
            {
                proc.StandardInput.Write("");
                Thread.Sleep(300);
            }
        }

        public void WaitForExitSync()
        {
            proc.WaitForExit();
        }

        public static SteamCMD LaunchSteamCMD(string launchargs)
        {
            Process serverProc = new Process();
            ProcessStartInfo info = serverProc.StartInfo;
            info.FileName = ServerManager.steamCmdPath;
            info.CreateNoWindow = true;
            info.Arguments += " +login " + Settings.Default.SteamCMDLogin + " " + Settings.Default.SteamCMDPass;
            info.Arguments += " +force_install_dir \"" + Path.Combine(ServerManager.workingdir, "game")+"\"";
            info.Arguments += " " + launchargs + " +quit";
            info.UseShellExecute = false;
            info.RedirectStandardInput = info.RedirectStandardOutput = info.RedirectStandardError = true;
            info.WorkingDirectory = ServerManager.workingdir;
            log.Debug(info.FileName + " " + info.Arguments);
            SteamCMD cmd = new SteamCMD(serverProc);
            serverProc.EnableRaisingEvents = true;
            serverProc.OutputDataReceived += (sender, args) => cmd.OutCallback(args.Data);
            serverProc.ErrorDataReceived += (sender, args) => cmd.OutCallback(args.Data);
            serverProc.Start();
            serverProc.BeginOutputReadLine();
            serverProc.BeginErrorReadLine();
            cmd.StartThread();
            log.Debug("SteamCMD spawned, process ID " + serverProc.Id);
            return cmd;
        }
    }
}
