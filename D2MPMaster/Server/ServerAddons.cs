using System.Collections.Generic;
using System.Collections.ObjectModel;
using D2MPMaster.Model;
using D2MPMaster.Properties;
using Newtonsoft.Json.Linq;
using ServerCommon.Data;

namespace D2MPMaster.Server
{
    public static class ServerAddons
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static ObservableCollection<ServerAddon> Addons = new ObservableCollection<ServerAddon>(); 
        public static void Init(Dictionary<string, Mod>.ValueCollection values)
        {
            var addons = JArray.Parse(Settings.Default.ServerAddons).ToObject<ServerAddon[]>();
            Addons.Clear();
            foreach (var addon in addons)
            {
                Addons.Add(addon);
            }
            foreach (var mod in values)
            {
                Addons.Add(new ServerAddon()
                           {
                               bundle = "serv_"+mod.bundle,
                               name = mod.name,
                               version = mod.version
                           });
            }
            log.Info("Loaded "+addons.Length+" server addons.");
        }
    }
}
