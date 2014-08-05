using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2MPMaster.Model
{
    public class ResultFailure
    {
        public string name { get; set; }
        public bool hasPassword { get; set; }
        public string password { get; set; }
        public string creator { get; set; }
        /// <summary>
        /// Creator UserID not SteamID
        /// </summary>
        public string creatorid { get; set; }
        /// <summary>
        /// Mod ID not name (before would be "reflex" or "fof")
        /// </summary>
        public string mod { get; set; }
        public string serverIP { get; set; }
        public bool isPublic { get; set; }
        public bool enableGG { get; set; }
        public int region { get; set; }
    }
}
