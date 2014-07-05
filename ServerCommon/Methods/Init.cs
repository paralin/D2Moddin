using ServerCommon.Data;

namespace ServerCommon.Methods
{
    public class Init
    {
        public const string Msg = "init";
        public string msg = Msg;
        public static string Password = "4go5zFnkoyxbX2wC97mOcqhuqWEQK20fs3RxeXLpzrrxJpuM6idT6zsAOhr7TokRfgLFwhsjmq1Cf8Suz1eK6DGsbHB0Tkf1AfSOPE24VPM8";
        public string password = Password;
        public int serverCount { get; set; }
        public ServerAddon[] addons { get; set; }
        public const string Version = "2.1.5";
        public string version = Version;
        public int portRangeStart { get; set; }
        public int portRangeEnd { get; set; }
        public ServerRegion region { get; set; }
        public string name { get; set; }
        public string publicIP { get; set; }
    }
}
