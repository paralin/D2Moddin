// 
// Party.cs
// Created by ilian000 on 2014-08-05
// Licenced under the Apache License, Version 2.0
//

using D2MPMaster.Model;
using System.Collections.Generic;

namespace D2MPMaster.Party
{
    public class Party
    {
        public string id { get; set; }

        /// <summary>
        /// Creator UserID not SteamID
        /// </summary>
        public string creatorid { get; set; }
        /// <summary>
        /// Users participating in this party
        /// </summary>
        public List<PartyMember> users { get; set; }
        /// <summary>
        /// Steamids of users that are invited
        /// </summary>
        public List<string> invitedUsers { get; set; }
    }
}
