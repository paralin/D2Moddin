using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using D2MPMaster.Properties;
using d2mpserver;
using Newtonsoft.Json.Linq;
using ServerCommon.Data;
using ServerCommon.Methods;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.Common.Socket.Event.Interface;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using System.Diagnostics;

namespace D2MPMaster.Server
{
    public class ServerController : XSocketController
    {
        public string ID { get; set; }
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ObservableCollection<ServerAddon> Addons = new ObservableCollection<ServerAddon>();
        public Init InitData;
        public string Address;
        public int IDCounter;
        private Timer mAckTimer = new Timer(30000);//30 seconds
        public ConcurrentDictionary<int, GameInstance> Instances = new ConcurrentDictionary<int, GameInstance>();
        public bool Inited { get; set; }
        private object ConcurrentLock = new object();

        private object MsgLock = new object();

        public ServerController()
        {
            ID = Utils.RandomString(10);
            this.OnOpen += OnClientConnect;
            this.OnClose += OnClosed;
            //force the connection closed when the server does not ACK
            mAckTimer.Elapsed += (sender, args) =>
            {
                log.Info("Server did not respond in time");
                mAckTimer.Stop();
                this.Close();
            };
        }

        private void OnClientConnect(object sender, OnClientConnectArgs e)
        {
            //stop the timer if the server ACK
            this.ProtocolInstance.OnPing += (s, args) => mAckTimer.Stop();//protocol instance is null until someone connects
        }

        public void OnClosed(object sender, OnClientDisconnectArgs onClientDisconnectArgs)
        {
            //Delete all lobbies
            if (Instances == null) return;
            foreach (var instance in Instances.Values)
            {
                LobbyManager.OnServerShutdown(instance);
            }
            Instances = null;
            Inited = false;
        }

        public void Send(string msg)
        {
            this.AsyncSend(new TextArgs(msg, "commands"), req => { });
            this.mAckTimer.Start();
        }

        public override void OnMessage(XSockets.Core.Common.Socket.Event.Interface.ITextArgs textArgs)
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
                                case Init.Msg:
                                {
                                    if (Inited) return;
                                    var msg = jdata.ToObject<Init>();
                                    if (msg.password != Init.Password)
                                    {
                                        //Wrong password
                                        Send("authFail");
                                        return;
                                    }
                                    if (msg.version != Init.Version)
                                    {
                                        Send("outOfDate|" + Program.S3.GenerateBundleURL("s" + Init.Version + ".zip"));
                                        return;
                                    }
                                    //Build server addon operation 
                                    var add = (from addon in ServerAddons.Addons
                                        let exist =
                                            msg.addons.FirstOrDefault(
                                                m => m.name == addon.name && m.version == addon.version)
                                        where exist == null
                                        select
                                            addon.name + ">" + addon.version + ">" +
                                            Program.S3.GenerateBundleURL(addon.bundle).Replace('|', ' ')).ToList();
                                    var del = (from addon in msg.addons
                                        let exist = ServerAddons.Addons.FirstOrDefault(m => m.name == addon.name)
                                        where exist == null
                                        select addon.name).ToList();
                                    if ((add.Count + del.Count) == 0)
                                    {
                                        InitData = msg;
                                        Address = InitData.publicIP;
                                        Inited = true;
                                    }
                                    else
                                    {
                                        Send("addonOps|" + string.Join(",", add) + "|" + string.Join(",", del));
                                    }
                                    break;
                                }
                                case OnServerLaunched.Msg:
                                {
                                    var msg = jdata.ToObject<OnServerLaunched>();
                                    if (Instances.ContainsKey(msg.id))
                                    {
                                        var instance = Instances[msg.id];
                                        instance.port = msg.port;
                                        LobbyManager.OnServerReady(instance);
                                    }
                                    break;
                                }
                                case OnServerShutdown.Msg:
                                {
                                    var msg = jdata.ToObject<OnServerShutdown>();
                                    if (Instances.ContainsKey(msg.id))
                                    {
                                        GameInstance instance;
                                        Instances.TryRemove(msg.id, out instance);
                                        LobbyManager.OnServerShutdown(instance);
                                    }
                                    break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error("Problem processing server message: ", ex);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error("Parsing server message.", ex);
            }
        }

        public GameInstance StartInstance(Lobby lobby)
        {
            IDCounter++;
            Mod mod = Mods.Mods.ByID(lobby.mod);
            var instance = new GameInstance()
                           {
                               ID = IDCounter,
                               lobby = lobby,
                               RconPass = lobby.id + "R",
                               Server = this,
                               state = GameState.Init,
                               map = (mod.mapOverride ?? mod.name)
                           };
            var command = "launchServer|" + instance.ID + "|" +
                          (lobby.devMode ? bool.TrueString : bool.FalseString) + "|" + Mods.Mods.ByID(lobby.mod).name +
                          "|" + instance.RconPass + "|" + string.Join("&", GenerateCommands(lobby)) + "|" + instance.map;
            Send(command);
            Instances[instance.ID] = instance;
            return instance;
        }

        private string[] GenerateCommands(Lobby lobby)
        {
            var cmds = new List<string>
                       {
                           "d2lobby_gg_time " + (lobby.enableGG ? "20" : "-1"),
#if DEBUG
                           "match_post_url \"http://127.0.0.1:8080/gdataapi/matchres\"",
#else
                "match_post_url \"http://" + Settings.Default.WebAddress + "/gdataapi/matchres\"",
#endif
                           "set_match_id \"" + lobby.id + "\"",
                           "d2l_disable_pause " + (lobby.disablePause ? "1" : "0")
                       };
            foreach (var plyr in lobby.radiant.Where(p => p != null))
            {
                var name = Regex.Replace(plyr.name, "[^a-zA-Z0-9 -]", "");
                if (string.IsNullOrWhiteSpace(name)) name = "Player";
                cmds.Add(string.Format("add_radiant_player \"{0}\" \"{1}\"", plyr.steam, name));
            }
            
            foreach (var plyr in lobby.dire.Where(p => p != null))
            {
                var name = Regex.Replace(plyr.name, "[^a-zA-Z0-9 -]", "");
                if (string.IsNullOrWhiteSpace(name)) name = "Player";
                cmds.Add(string.Format("add_dire_player \"{0}\" \"{1}\"", plyr.steam, name));
            }
            return cmds.ToArray();
        }
    }
}
