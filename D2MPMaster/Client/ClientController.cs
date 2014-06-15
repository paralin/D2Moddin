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
using XSockets.Core.XSocket.Helpers;
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
        }

        void DeregisterClient(object se, OnClientDisconnectArgs e)
        {
            if (UID == null) return;
            var browsers = Browser.Find(m => m.user != null && m.user.Id == UID);
            foreach (var browser in browsers)
            {
                browser.SendManagerStatus(false);
            }
        }

        void RegisterClient()
        {
            //Figure out UID
            User user = null;
            foreach (var steamid in InitData.SteamIDs.Where(steamid => steamid.Length == 17))
            {
                SteamID = steamid;
                user = Mongo.Users.FindOneAs<User>(Query.EQ("steam.steamid", steamid));
                if (user != null) break;
            }

            if (user == null) return;
            UID = user.Id;

            Inited = true;

            //Find if the user is online
            var browsers = Browser.Find(e => e.user != null && e.user.Id == UID);
            foreach (var browser in browsers)
            {
                browser.SendManagerStatus(true);
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
                        log.Debug(SteamID+" -> installed " + msg.Mod.name + ".");
                        Mods.Add(msg.Mod);
                        Browser.AsyncSendTo(x=>x.user!=null&&x.user.steam.steamid==SteamID, BrowserController.InstallResponse("The mod has been installed.", true), rf => { });
                        break;
                    }
                    case OnDeletedMod.Msg:
                    {
                        var msg = jdata.ToObject<OnDeletedMod>();
                        log.Debug(SteamID + " -> removed " + msg.Mod.name + ".");
                        Mods.Remove(msg.Mod);

                        break;
                    }
                    case Init.Msg:
                    {
                        var msg = jdata.ToObject<Init>();
                        InitData = msg;
                        if (msg.Version != Version.ClientVersion)
                        {
                            this.SendJson(JObject.FromObject(new Shutdown()).ToString(Formatting.None), "commands");
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
                //log.Error("Parsing client message.", ex);
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
