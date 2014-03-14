using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using d2mpserver.Properties;

namespace d2mpserver
{
    public class ServerConnection
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public WebSocket socket = null;
        public bool infoValid = true;
        private bool shutdown = false;
        private ServerManager manager;

        public ServerConnection(ServerManager manager)
        {
            this.manager = manager;
        }

        void RegisterCallbacks()
        {
            socket.OnClose += (sender, args) =>
                                    {
                                        log.Debug("Disconnected from the server");
                                        //Reconnect?
                                    };
            socket.OnError += (sender, args) =>
                                    {
                                        log.Error("Socket error: "+args.Message);
                                    };
            socket.OnMessage += (sender, args) =>
                                    {
                                        log.Debug("Server message: "+args.Data);
                                        ProcessMessage(args.Data);
                                    };
            socket.OnOpen += (sender, args) =>
                                    {
                                        log.Info("Connected to the server.");
                                        SendInit();
                                    };
        }

        private void ProcessMessage(string data)
        {
            string[] command = data.Split('|');
            switch(command[0])
            {
                case "launchServer":
                    {
                        int id = int.Parse(command[1]);
                        int port = int.Parse(command[2]);
                        bool dev = bool.Parse(command[3]);
                        manager.LaunchServer(id, port, dev).OnShutdown += (sender, args) =>
                                                                              {
                                                                                  socket.SendAsync("onShutdown|" + id,
                                                                                                   b => { });
                                                                                  log.Debug("server shut down on its own: "+id);
                                                                              };
                        break;
                    }
                case "shutdownServer":
                    {
                        int id = int.Parse(command[1]);
                        manager.ShutdownServer(id);
                    }
                    break;
                case "authFail":
                    {
                        log.Fatal("Server doesn't like our init info, shutting down...");
                        infoValid = false;
                    }
                    break;
            }
        }

        public void SendInit()
        {
            var msg = "init|" + Settings.Default.connectPassword + "|" + Settings.Default.serverCount;
            socket.Send(msg);
        }

        public bool Connect()
        {
            if(socket != null && socket.IsAlive)
            {
                log.Debug("Already connected.");
                return true;
            }

            log.Debug("attempting connection to "+Settings.Default.serverIP);
            socket = new WebSocket(Settings.Default.serverIP);
            RegisterCallbacks();
            socket.Connect();

            return socket.IsAlive;
        }

        public void Disconnect()
        {
            if (socket == null || !socket.IsAlive)
                return;

            socket.Close();
            socket = null;
        }

        public void Shutdown()
        {
            shutdown = true;
            log.Debug("Shutting down...");
        }

        public void StartServerThread()
        {
            ThreadPool.QueueUserWorkItem(ServerThread);
        }

        private void ServerThread(object state)
        {
            while(!shutdown)
            {
                Thread.Sleep(100);
            }
            manager.ShutdownAllServers();
        }
    }
}
