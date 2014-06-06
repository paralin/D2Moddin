using System;
using System.Collections.ObjectModel;
using System.Linq;
using Amazon.DataPipeline.Model;
using ClientCommon.Data;
using ClientCommon.Methods;
using D2MPMaster.Browser;
using D2MPMaster.Database;
using D2MPMaster.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.Common.Socket.Event.Interface;
using XSockets.Core.XSocket;
using Query = MongoDB.Driver.Builders.Query;
using Version = ClientCommon.Version;
using XSocketHelper = XSockets.Core.XSocket.Helpers.XSocketHelper;

namespace D2MPMaster.Client
{
    public class ClientController : XSocketController
    {
        private static readonly BrowserController Browser = new BrowserController();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ObservableCollection<ClientMod> Mods = new ObservableCollection<ClientMod>();
        public Init InitData;
        public string UID;
        public string SteamID;
        public bool Inited { get; set; }

        public ClientController()
        {
            this.OnClose += DeregisterClient;
            this.OnOpen += (sender, args) =>
                           {
                               log.Debug("Client connected.");
                           };
        }

        void DeregisterClient(object se, OnClientDisconnectArgs e)
        {
            if (UID == null) return;
            try
            {
                Mongo.Clients.Remove(Query.EQ("_id", UID));
            }
            catch (Exception ex)
            {
            }
        }

        void RegisterClient()
        {
            //Figure out UID
            User user = null;
            foreach (var steamid in InitData.SteamIDs.Where(steamid => steamid.Length == 17))
            {
                SteamID = steamid;
                user = Mongo.Users.FindOneAs<User>(Query.EQ("services.steam.id", steamid));
                if (user != null) break;
            }

            if (user == null) return;
            UID = user.Id;

            var exist = Mongo.Clients.FindOneAs<ClientRecord>(Query.EQ("_id", UID));
            if (exist == null)
            {
                Mongo.Clients.Insert(new ClientRecord()
                {
                    Id = UID,
                    status = 0
                });
            }
        }

        public static ITextArgs InstallMod(Mod mod)
        {
            var msg = JObject.FromObject(new InstallMod() {Mod = mod.ToClientMod(), url = Program.S3.GenerateModURL(mod)}).ToString(Formatting.None);
            return new TextArgs(msg, "commands");
        }

        public static ITextArgs LaunchDota()
        {
            return new TextArgs(JObject.FromObject(new LaunchDota()).ToString(Formatting.None), "commands");
        }

        public static ITextArgs SetMod(Mod mod)
        {
            var msg = JObject.FromObject(new SetMod() { Mod = mod.ToClientMod() }).ToString(Formatting.None);
            return new TextArgs(msg, "commands");
        }

        public override void OnMessage(ITextArgs textArgs)
        {
            try
            {
                var jdata = JObject.Parse(textArgs.data);
                var id = jdata["msg"];
                if (id == null) return;
                var command = id.Value<string>();
                switch (command)
                {
                    case OnInstalledMod.Msg:
                    {
                        var msg = jdata.ToObject<OnInstalledMod>();
                        log.Debug("Client installed " + msg.Mod.name + ".");
                        Mods.Add(msg.Mod);
                        XSocketHelper.AsyncSendTo(Browser, x=>x.user!=null&&x.user.services.steam.steamid==SteamID, BrowserController.InstallResponse("The mod has been installed.", true),
                            rf => { });
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
                            XSocketHelper.SendJson(this, JObject.FromObject(new Shutdown()).ToString(Formatting.None), "commands");
                            return;
                        }
                        foreach (var mod in msg.Mods.Where(mod => mod.name != null && mod.version != null)) Mods.Add(mod);
                        //Insert the client into the DB
                        RegisterClient();
                        break;
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error("Parsing client message.", ex);
            }
        }

        public static ITextArgs ConnectDota(string serverIp)
        {
            return new TextArgs(JObject.FromObject(new ConnectDota() { ip = serverIp }).ToString(Formatting.None), "commands");
        }

		public static ITextArgs Shutdown()
		{
            return new TextArgs(JObject.FromObject(new ClientCommon.Methods.Shutdown()).ToString(), "commands");
		}
    }
}
