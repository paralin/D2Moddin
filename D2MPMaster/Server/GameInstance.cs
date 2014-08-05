using D2MPMaster.Lobbies;

namespace D2MPMaster.Server
{
    public class GameInstance
    {
        public ServerController Server;
        public int ID;
        public string RconPass;
        public Lobby lobby;
        public GameState state;
        public int port;
        public string map;
        public int totalPlayers;
    }
}
