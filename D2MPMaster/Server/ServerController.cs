﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using D2MPMaster.Lobbies;
using D2MPMaster.Properties;
using d2mpserver;
using Newtonsoft.Json.Linq;
using ServerCommon.Data;
using ServerCommon.Methods;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using System.Diagnostics;
using D2MPMaster.Database;
using MongoDB.Driver.Builders;
using D2MPMaster.Model;
using Newtonsoft.Json;

namespace D2MPMaster.Server
{
    public class ServerController : XSocketController
    {
        public string ID { get; set; }
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public ObservableCollection<ServerAddon> Addons = new ObservableCollection<ServerAddon>();
        public Init InitData;
        public string Address;
        public int portCounter;
        public int IDCounter;
        public ConcurrentDictionary<int, GameInstance> Instances = new ConcurrentDictionary<int, GameInstance>();
        public bool Inited { get; set; }
        public ServerCommon.Encryption encryptor;
        public string serverPubKey = null;

        private object MsgLock = new object();

        public ServerController()
        {
            ID = Utils.RandomString(10);
            this.OnOpen += OnClientConnect;
            this.OnClose += OnClosed;
        }

        private void OnClientConnect(object sender, OnClientConnectArgs e)
        {
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
            if (encryptor != null)
            {
                this.SendJson(JsonConvert.SerializeObject(encryptor.encrypt(msg)), "commands");
                return;
            }
            this.SendJson(msg, "commands");
        }

        public override void OnMessage(XSockets.Core.Common.Socket.Event.Interface.ITextArgs textArgs)
        {
            try
            {
                JObject jdata = new JObject();
                try
                {
                    jdata = JObject.Parse(Program.decryptor.decrypt(JObject.Parse(textArgs.data).ToObject<ServerCommon.EncryptModel>()));
                }
                catch (ArgumentNullException)
                {
                    log.Warn("Received an unencrypted message. Probabably received mastey key request.");
                    jdata = JObject.Parse(textArgs.data);
                }
                catch (FormatException ex)
                {
                    log.Fatal("Message didn't get decrypted, but is formatted in an EncryptModel. This should be reported to the devs.", ex);
                }
                var id = jdata["msg"];
                if (id == null) return;
                var command = id.Value<string>();
                Task.Factory.StartNew(() =>
                {
                    lock (MsgLock)
                    {
                        switch (command)
                        {
                            case MasterKeyRequest.Msg:
                                Send("serverPubKey|" + Program.decryptor.getPublicKey());
                                break;
                            case Init.Msg:
                                {
                                    if (Inited) return;
                                    var msg = jdata.ToObject<Init>();
                                    log.Info(String.Format("Server trying to connect: hwGuid: {0} ip: {1}", msg.hwGuid, msg.publicIP));
                                    var serverPubKey = Mongo.ServerKeys.FindOneAs<ServerKey>(Query.EQ("_id", msg.hwGuid));
                                    if (serverPubKey == null)
                                    {
                                        // Public key not in database
                                        Send("keyFail");
                                        return;
                                    }
                                    else
                                    {
                                        log.Info("Server got accepted!");
                                        Send("keySuccess");
                                    }
                                    encryptor = new ServerCommon.Encryption(serverPubKey.pubKey, true);
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
                                        portCounter = msg.portRangeStart;
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
            portCounter++;
            if (portCounter > InitData.portRangeEnd) portCounter = InitData.portRangeStart;
            var instance = new GameInstance()
                           {
                               ID = IDCounter,
                               lobby = lobby,
                               RconPass = lobby.id + "R",
                               Server = this,
                               state = GameState.Init,
                               port = portCounter
                           };
            var command = "launchServer|" + instance.ID + "|" + instance.port + "|" +
                          (lobby.devMode ? bool.TrueString : bool.FalseString) + "|" + Mods.Mods.ByID(lobby.mod).name +
                          "|" + instance.RconPass + "|" + string.Join("&", GenerateCommands(lobby));
            Send(command);
            Instances[instance.ID] = instance;
            return instance;
        }

        private string[] GenerateCommands(Lobby lobby)
        {
            var cmds = new List<string>
            {
                "d2lobby_gg_time " + (lobby.enableGG ? "5" : "-1"),
#if DEBUG
                "match_post_url \"http://127.0.0.1:8080/gdataapi/matchres\"",
#else
                "match_post_url \"http://" + Settings.Default.WebAddress + "/gdataapi/matchres\"",
#endif
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
