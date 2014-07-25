using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Timers;
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
using System.Collections.Generic;

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
        private Timer mAckTimer = new Timer(10000);//10 seconds

        public bool Inited { get; set; }

        private object ConcurrentLock = new object();

        public ClientController()
        {
            this.OnOpen += OnClientConnect;
            this.OnClose += DeregisterClient;
            this.OnError += OnClientError;
            //force the connection closed when the client does not ACK
            mAckTimer.Elapsed += (sender, args) =>
            {
                log.Info("Client did not respond in time");
                mAckTimer.Stop();
                this.Close();
            };
        }

        private void OnClientConnect(object sender, OnClientConnectArgs e)
        {
            //stop the timer if the client ACK
            this.ProtocolInstance.OnPing += (s, args) => mAckTimer.Stop();//protocol instance is null until someone connects
        }

        private void OnClientError(object sender, OnErrorArgs args)
        {
            log.Error(args.Message, args.Exception);
        }

        private void OnClientError(object sender, OnErrorArgs args)
        {
            log.Error(args.Message, args.Exception);
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
				var userb = Mongo.Users.FindOneAs<User>(Query.EQ("steam.steamid", steamid));
			    if (userb != null)
			    {
			        var browser = Browser.Find(m => m.user != null && m.user.Id == userb.Id);
					if (browser.Any()) {user = userb; break;};
			    }
			}

			if (user == null)
			{
                this.AsyncSend(NotifyMessage("Account link unsuccessful","Sign into the same account on the website and in Steam.", true) , ar => { });
				return;
			}
			SteamID = user.steam.steamid;
			UID = user.Id;

			Inited = true;

			//Find if the user is online
			var browsersn = Browser.Find(e => e.user != null && e.user.Id == UID);
			foreach (var browser in browsersn)
			{
				browser.SendManagerStatus(true);
                //set the current mod (helps after manager restart)
                if( browser.lobby != null )
			        SetMod(SteamID, D2MPMaster.Mods.Mods.ByID(browser.lobby.mod));
			}
		}

        public void InstallMod(string pUID, Mod mod)
        {
            //find the correct client
            ClientController client = this.Find(c => !string.IsNullOrEmpty(c.UID) && c.UID == pUID).FirstOrDefault();
            if (client != null)
            {
                var msg = JObject.FromObject(new InstallMod() { Mod = mod.ToClientMod(), url = Program.S3.GenerateModURL(mod) }).ToString(Formatting.None);
                client.AsyncSend(new TextArgs(msg, "commands"),
                    req => { });
                client.mAckTimer.Start();
            }
        }

        public static ITextArgs LaunchDota()
        {
            return new TextArgs(JObject.FromObject(new LaunchDota()).ToString(Formatting.None), "commands");
        }

        public static ITextArgs NotifyMessage(string title, string message)
        {
            return new TextArgs(JObject.FromObject(new NotifyMessage() { message = new Message() { title = title, message = message } }).ToString(Formatting.None), "commands");
        }

        public static ITextArgs NotifyMessage(string title, string message, bool shutdown)
        {
            return new TextArgs(JObject.FromObject(new NotifyMessage() { message = new Message() { title = title, message = message, shutdown = shutdown } }).ToString(Formatting.None), "commands");
        }

        public void SetMod(string pSteamId, Mod mod)
        {
            //find the correct client
            ClientController client = this.Find(c => !string.IsNullOrEmpty(c.SteamID) && c.SteamID == pSteamId).FirstOrDefault();
            if (client != null)
            {
                var msg = JObject.FromObject(new SetMod() { Mod = mod.ToClientMod() }).ToString(Formatting.None);
                client.AsyncSend(new TextArgs(msg, "commands"), req => { });
                client.mAckTimer.Start();
            }
        }

        public override void OnMessage(ITextArgs textArgs)
        {
            try
            {
                var jdata = JObject.Parse(textArgs.data);
                var id = jdata["msg"];
                if (id == null) return;
                var command = id.Value<string>();
                Task.Factory.StartNew(() =>
                {
                    lock (ConcurrentLock)
                    {
                        try
                        {
                            switch (command)
                            {
                                case OnInstalledMod.Msg:
                                {
                                    this.InstallMod(this.UID, mod);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            //log.Error("Parsing client message.", ex);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                
            }
        }

        public void ConnectDota(string pSteamId, string serverIp)
        {
            //find the correct client
            ClientController client = this.Find(c => !string.IsNullOrEmpty(c.SteamID) && c.SteamID == pSteamId).FirstOrDefault();
            if (client != null)
            {
                var msg = JObject.FromObject(new ConnectDota() { ip = serverIp }).ToString(Formatting.None);
                client.AsyncSend(new TextArgs(msg, "commands"), arg=>{});
                client.mAckTimer.Start();
            }
        }

        public static ITextArgs Shutdown(bool restart)
        {
            return new TextArgs(JObject.FromObject(new ClientCommon.Methods.Shutdown(){restart=restart}).ToString(), "commands");
        }

        public static ITextArgs UpdateMods()
        {
            return new TextArgs(JObject.FromObject(new ClientCommon.Methods.UpdateMods()).ToString(), "commands");
        }
        
        public static ITextArgs Uninstall()
        {
            return new TextArgs(JObject.FromObject(new ClientCommon.Methods.Uninstall()).ToString(), "commands");
        }
    }
}
