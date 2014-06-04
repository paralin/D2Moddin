using System;
using System.Collections.ObjectModel;
using System.Linq;
using ClientCommon.Data;
using ClientCommon.Methods;
using D2MPMaster.Model;
using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Version = ClientCommon.Version;

namespace D2MPMaster.Client
{
    public class ModClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ObservableCollection<ClientMod> Mods = new ObservableCollection<ClientMod>();
        private bool _init;
        public Init InitData;
        public string UID;
        public string SteamID;

        public IWebSocketConnection Socket;
        public bool Inited {
            get { return _init; }
            set
            {
                _init = value;
                if (value)
                {
                    Program.Client.RegisterClient(this);
                }
            }
        }

        public ModClient(IWebSocketConnection sock, string id)
        {
            Socket = sock;
        }

        public void OnClose(object o, string id)
        {
            if (_init)
            {
                Program.Client.DeregisterClient(this);
            }
        }

        public void InstallMod(Mod mod)
        {
            var msg = JObject.FromObject(new InstallMod() {Mod = mod.ToClientMod(), url = Program.S3.GenerateModURL(mod)}).ToString(Formatting.None);
            Socket.Send(msg);
            log.Debug(UID+" -> InstallMod "+mod.name);
        }

        public void LaunchDota()
        {
            Socket.Send(JObject.FromObject(new LaunchDota()).ToString(Formatting.None));
        }

        public void SetMod(Mod mod)
        {
            var msg = JObject.FromObject(new SetMod() { Mod = mod.ToClientMod() }).ToString(Formatting.None);
            Socket.Send(msg);
        }

        public void HandleMessage(string data, IWebSocketConnection context, string ID)
        {
            try
            {
                var jdata = JObject.Parse(data);
                var id = jdata["msg"];
                if (id == null) return;
                var command = id.Value<string>();
                switch (command)
                {
                    case OnInstalledMod.Msg:
                    {
                        var msg = jdata.ToObject<OnInstalledMod>();
                        Mods.Add(msg.Mod);
                        log.Debug("Client installed " + msg.Mod.name + ".");
                        if(Program.Browser.UserClients.ContainsKey(SteamID)){
                            Program.Browser.UserClients[SteamID].SendInstallRes(true, "The mod has been installed.");
                        }
                        else
                        {
                            log.Debug("No browser to send the install result to! "+UID);
                        }
                        break;
                    }
                    case Init.Msg:
                    {
                        var msg = jdata.ToObject<Init>();
                        log.Debug("Client init with version "+msg.Version);
                        InitData = msg;
                        if (msg.Version != Version.ClientVersion)
                        {
                            log.Debug("Old version, sending shutdown message.");
                            context.Send(JObject.FromObject(new Shutdown()).ToString(Formatting.None));
                            return;
                        }
                        foreach (var mod in msg.Mods.Where(mod => mod.name != null && mod.version != null)) Mods.Add(mod);
                        Inited = true;
                        break;
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error("Parsing client message.", ex);
            }
        }

        public void ConnectDota(string serverIp)
        {
            Socket.Send(JObject.FromObject(new ConnectDota(){ip=serverIp}).ToString(Formatting.None));
        }

		public void Shutdown()
		{
			Socket.Send(JObject.FromObject(new ClientCommon.Methods.Shutdown()).ToString());
		}
    }
}
