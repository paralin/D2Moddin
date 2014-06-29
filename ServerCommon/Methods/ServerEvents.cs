namespace ServerCommon.Methods
{
    public class OnServerLaunched
    {
        public const string Msg = "onlaunched";
        public int id { get; set; }
        public string msg = Msg;
    }

    public class OnServerShutdown
    {
        public const string Msg = "onshutdown";
        public int id { get; set; }
        public string msg = Msg;
    }

    public class MasterKeyRequest
    {
        public const string Msg = "masterkeyrequest";
        public string msg = Msg;
    }
}
