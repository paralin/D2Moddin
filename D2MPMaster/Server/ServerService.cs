using System.Linq;
using System.Runtime.ExceptionServices;
using D2MPMaster.Lobbies;
using D2MPMaster.Server;
using d2mpserver;
using MongoDB.Driver.Linq;
using XSockets.Core.XSocket.Helpers;

namespace D2MPMaster
{
    public static class ServerService
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static readonly ServerController Servers = new ServerController();
        public static ServerController FindForLobby(Lobby lobby)
        {
            //Params
            ServerRegion region = lobby.region;
            if (region != ServerRegion.UNKNOWN)
            {
                var regionServers = Servers.Find(m => m.Inited && m.InitData.regions.Contains((ServerCommon.ServerRegion)region)).OrderBy(m=>m.Instances.Count);
                if (!regionServers.Any()) lobby.region = ServerRegion.UNKNOWN;
                else return regionServers.FirstOrDefault(m=>m.Instances.Count < m.InitData.serverCount);
            }

            return Servers.Find(m => m.Inited && m.Instances.Count < m.InitData.serverCount).OrderBy(m=>m.Instances.Count).FirstOrDefault();
        }
    }
}
