﻿using D2MPMaster.Lobbies;
using d2mpserver;

namespace D2MPMaster.Server
{
    public class GameInstance
    {
        public ServerInstance Server;
        public int ID;
        public string RconPass;
        public Lobby lobby;
        public GameState state;
        public int port;
    }
}