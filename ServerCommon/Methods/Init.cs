using ServerCommon.Data;

namespace ServerCommon.Methods
{
    public class Init
    {
        public const string Msg = "init";
        public string msg = Msg;
        public static string Password = "pN5hHHHLRDAoAapz6HiNegkZgfN4rs";
        public string password = Password;
        public int serverCount { get; set; }
        public ServerAddon[] addons { get; set; }
        public const string Version = "2.0.3";
        public string version = Version;
        public int portRangeStart { get; set; }
        public int portRangeEnd { get; set; }
        public ServerRegion region { get; set; }
        public string name { get; set; }
        public string publicIP { get; set; }
    }
}
