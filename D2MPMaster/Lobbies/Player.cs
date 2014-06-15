using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D2MPMaster.Model;

namespace D2MPMaster.Lobbies
{
    public class Player
    {
        /// <summary>
        /// Human readable name
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// SteamID
        /// </summary>
        public string steam { get; set; }

        /// <summary>
        /// Avatar img URL (full)
        /// </summary>
        public string avatar { get; set; }

        public static Player FromUser(User user)
        {
            return new Player
                   {
                       avatar = user.steam.avatarfull,
                       name = user.profile.name,
                       steam = user.steam.steamid
                   };
        }
    }

    public class PlayerLocation
    {
        public Lobby lobby;
        public bool goodguys;
    }
}
