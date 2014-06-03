using System;
using System.Collections.Generic;
using System.Linq;
using D2MPMaster.Client;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Server;
using d2mpserver;
using MongoDB.Driver;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace D2MPMaster
{
    class ServerManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Dictionary<string, ServerInstance> Servers = new Dictionary<string, ServerInstance>();
        public Dictionary<string, ServerInstance> ActiveServers = new Dictionary<string, ServerInstance>();
        public int IDCounter = (new Random().Next(0, 1000));

        public ServerManager()
        {
        }
        
        public void OnOpen(string ID, WebSocketContext Context)
        {
            var server = new ServerInstance(Context, ID);
            Servers[ID] = server;
            log.Debug(string.Format("Server connected #{1}: {0}.", ID, Context.Host));
        }

        public void OnClose(string ID, WebSocketContext Context, CloseEventArgs e)
        {
            log.Debug(string.Format("Server disconnect #{0}", ID));
            Servers[ID].OnClose(e, ID);
            Servers.Remove(ID);
            ActiveServers.Remove(ID);
        }

        public void OnMessage(string ID, WebSocketContext Context, MessageEventArgs e)
        {
            var server = Servers[ID];
            server.HandleMessage(e.Data, Context, ID);
        }

        public void RegisterServer(ServerInstance serverInstance)
        {
            ActiveServers.Add(serverInstance.ID, serverInstance);
        }

        /// <summary>
        /// Finds an eligable server for a lobby.
        /// </summary>
        /// <param name="lobby"></param>
        /// <returns></returns>
        public ServerInstance FindForLobby(Lobby lobby)
        {
            //Params
            ServerRegion region = lobby.region;
            ServerInstance server = null;
            if (region == ServerRegion.UNKNOWN)
            {
                return ActiveServers.Values.FirstOrDefault(m => m.Instances.Count < m.InitData.serverCount);
            }
            return ActiveServers.Values.FirstOrDefault(
                m => m.Instances.Count < m.InitData.serverCount &&
                     (int) m.InitData.region == (int) region);
        }
    }
    class ServerService : WebSocketService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public ServerService()
        {
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            Program.Server.OnMessage(ID, Context, e);
            base.OnMessage(e);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            Program.Server.OnClose(ID, Context, e);
            base.OnClose(e);
        }

        protected override void OnOpen()
        {
            log.Debug(string.Format("Server connected {0}.", Context.Host));
            Program.Server.OnOpen(ID, Context);
            base.OnOpen();
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Log.Error(e.Message);
            base.OnError(e);
        }
    }
}
