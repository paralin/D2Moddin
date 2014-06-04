using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using D2MPMaster.Lobbies;
using D2MPMaster.Properties;
using D2MPMaster.Server;
using d2mpserver;
using Fleck;

namespace D2MPMaster
{
    class ServerManager : ISocketHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Dictionary<string, ServerInstance> Servers = new Dictionary<string, ServerInstance>();
        public Dictionary<string, ServerInstance> ActiveServers = new Dictionary<string, ServerInstance>();
        public int IDCounter = (new Random().Next(0, 1000));
        private WebSocketServer server;

        public ServerManager()
        {
        }

        public void OnMessage(string ID, IWebSocketConnection socket, string message)
        {
            var client = Servers[ID];
            var handleTask = new Task(() => client.HandleMessage(message, socket, ID));
            handleTask.Start();
        }

        public void OnClose(string ID, IWebSocketConnection socket)
        {
            log.Debug(string.Format("Client disconnect #{0}", ID));
            Servers[ID].OnClose(socket, ID);
        }

        public void OnOpen(string ID, IWebSocketConnection socket)
        {
            var client = new ServerInstance(socket, ID);
            Servers[ID] = client;
            log.Debug(string.Format("Client connected #{1}: {0}.", ID, socket.ConnectionInfo.ClientIpAddress));
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
}
