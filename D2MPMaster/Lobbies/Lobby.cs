namespace D2MPMaster.Lobbies
{
    public class Lobby
    {
        public string id { get; set; }
        public string name { get; set; }
        public bool hasPassword { get; set; }
        public string password { get; set; }
        public string[] banned { get; set; }
        public string creator { get; set; }
        public string creatorid { get; set; }
        public Player[] radiant { get; set; }
        public Player[] dire { get; set; }
        //Using ID now not name
        public string mod { get; set; }

    }
}
