using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using D2MPMaster.Lobbies;
using D2MPMaster.Properties;
using d2mpserver;
using Newtonsoft.Json.Linq;
using ServerCommon.Data;
using ServerCommon.Methods;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;

namespace D2MPMaster.Server
{
    public class ServerInstance
    {
        public string ID { get; set; }
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ObservableCollection<ServerAddon> Addons = new ObservableCollection<ServerAddon>();
        private bool _init;
        public Init InitData;
        public string Address;
        public WebSocket Socket;
        public int portCounter;
        public ConcurrentDictionary<int, GameInstance> Instances = new ConcurrentDictionary<int, GameInstance>(); 
        public bool Inited {
            get { return _init; }
            set
            {
                _init = value;
                if (value)
                {
                    Program.Server.RegisterServer(this);
                }
            }
        }

        public ServerInstance(WebSocketContext creationContext, string id)
        {
            ID = id;
            Socket = creationContext.WebSocket;
            Address = creationContext.UserEndPoint.ToString();
        }

        public void OnClose(object o, string id)
        {
            //Delete all lobbies
            foreach(var instance in Instances.Values)
            {
                Program.LobbyManager.OnServerShutdown(instance);
            }
            Instances = null;
            Inited = false;
        }

        public void HandleMessage(string data, WebSocketContext context, string ID)
        {
            try
            {
                var jdata = JObject.Parse(data);
                var id = jdata["msg"];
                if (id == null) return;
                var command = id.Value<string>();
                switch (command)
                {
                    case Init.Msg:
                    {
                        if (_init) return;
                        var msg = jdata.ToObject<Init>();
                        if (msg.password != Init.Password)
                        {
                            //Wrong password
                            Socket.Send("shutdown");
                            return;
                        }
                        if (msg.version != Init.Version)
                        {
                            Socket.Send("outOfDate|"+Program.S3.GenerateBundleURL("s"+Init.Version+".zip"));
                            return;
                        }
                        //Build server addon operation 
                        var add = (from addon in ServerAddons.Addons let exist = msg.addons.FirstOrDefault(m => m.name == addon.name && m.version == addon.version) where exist == null select addon.name + ">" + addon.version + ">" + Program.S3.GenerateBundleURL(addon.bundle).Replace('|', ' ')).ToList();
                        var del = (from addon in msg.addons let exist = ServerAddons.Addons.FirstOrDefault(m => m.name == addon.name) where exist == null select addon.name).ToList();
                        if ((add.Count + del.Count) == 0)
                        {
                            portCounter = msg.portRangeStart;
                            InitData = msg;
                            Inited = true;
                        }
                        else
                        {
                            Socket.Send("addonOps|"+string.Join(",", add)+"|"+string.Join(",", del));
                        }
                        break;
                    }
                    case OnServerLaunched.Msg:
                    {
                        var msg = jdata.ToObject<OnServerLaunched>();
                        if (Instances.ContainsKey(msg.id))
                        {
                            var instance = Instances[msg.id];
                            Program.LobbyManager.OnServerReady(instance);
                        }
                        break;
                    }
                    case OnServerShutdown.Msg:
                    {
                        var msg = jdata.ToObject<OnServerShutdown>();
                        if (Instances.ContainsKey(msg.id))
                        {
                            var instance = Instances[msg.id];
                            Program.LobbyManager.OnServerShutdown(instance);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Parsing server message.", ex);
            }
        }

        public GameInstance StartInstance(Lobby lobby)
        {
            Program.Server.IDCounter++;
            portCounter++;
            if (portCounter > InitData.portRangeEnd) portCounter = InitData.portRangeStart;
            var instance = new GameInstance()
                           {
                               ID = Program.Server.IDCounter,
                               lobby = lobby,
                               RconPass = Utils.RandomString(10),
                               Server = this,
                               state = GameState.Init,
                               port = portCounter
                           };
            var command = "launchServer|" + instance.ID + "|" + instance.port + "|" +
                          (lobby.devMode ? bool.TrueString : bool.FalseString) + "|" + Mods.Mods.ByID(lobby.mod).name +
                          "|" + instance.RconPass + "|" + string.Join("&", GenerateCommands(lobby));
            Socket.Send(command);
            Instances[instance.ID] = instance;
            return instance;
        }

        private string[] GenerateCommands(Lobby lobby)
        {
            var cmds = new List<string>
            {
                "d2lobby_gg_time " + (lobby.enableGG ? "5" : "-1"),
                "match_post_url \"" + Settings.Default.PostURL + "\"",
                "set_match_id \"" + lobby.id + "\""
            };
            cmds.AddRange(from plyr in lobby.radiant
                where plyr != null
                select string.Format("add_radiant_player \"{0}\" \"{1}\"", plyr.steam, Regex.Replace(plyr.name, "[^a-zA-Z0-9 -]", "")));
            cmds.AddRange(from plyr in lobby.dire
                where plyr != null
                select string.Format("add_dire_player \"{0}\" \"{1}\"", plyr.steam, Regex.Replace(plyr.name, "[^a-zA-Z0-9 -]", "")));
            return cmds.ToArray();
        }
    }
}
