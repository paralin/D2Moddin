using System.Dynamic;
using D2MPMaster.LiveData;
using d2mpserver;

namespace D2MPMaster.Lobbies
{
    /// <summary>
    /// A lobby instance.
    /// </summary>
    public class Lobby
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool hasPassword { get; set; }
        public string password { get; set; }
        public string[] banned { get; set; }
        public string creator { get; set; }
        /// <summary>
        /// Creator UserID not SteamID
        /// </summary>
        public string creatorid { get; set; }
        public Player[] radiant { get; set; }
        public Player[] dire { get; set; }
        /// <summary>
        /// Mod ID not name (before would be "reflex" or "fof")
        /// </summary>
        public string mod { get; set; }
        public string serverIP { get; set; }
        public bool isPublic { get; set; }
        public bool requiresFullLobby { get; set; }
        public bool devMode { get; set; }
        public bool enableGG { get; set; }
        public GameState state { get; set; }
        public int region { get; set; }
        public bool deleted { get; set; }
        public int status { get; set; }
    }
}
