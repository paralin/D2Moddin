using System.Linq;
using D2MPMaster.Lobbies;
using D2MPMaster.Server;
using d2mpserver;
using XSockets.Core.XSocket.Helpers;

namespace D2MPMaster
{
    public static class ServerManager
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ServerController Servers = new ServerController();
        public static ServerController FindForLobby(Lobby lobby)
        {
            //Params
            ServerRegion region = lobby.region;
            ServerController server = null;
            if (region == ServerRegion.UNKNOWN)
            {
                return Servers.Find(m=>m.Inited&&m.Instances.Count < m.InitData.serverCount).FirstOrDefault();
            }
            return Servers.Find(m => m.Inited&&m.Instances.Count < m.InitData.serverCount && (int)m.InitData.region == (int)region).FirstOrDefault();
        }
    }
}
