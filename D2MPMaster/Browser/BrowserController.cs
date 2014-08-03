using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using D2MPMaster.Browser.Methods;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using D2MPMaster.Lobbies;
using D2MPMaster.Matchmaking;
using D2MPMaster.Model;
using D2MPMaster.Friends;
using MongoDB.Driver.Builders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.Common.Socket.Event.Interface;
using XSockets.Core.XSocket;
using XSockets.Core.XSocket.Helpers;
using InstallMod = D2MPMaster.Browser.Methods.InstallMod;


namespace D2MPMaster.Browser
{
    public class BrowserController : XSocketController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ClientController ClientsController = new ClientController();

        private object ConcurrentLock = new object();

        public string ID;

        public BrowserController()
        {
            this.OnClose += OnClosed;
            ID = Utils.RandomString(10);
        }

        #region Variables
        //Chat flood prevention
        private string lastMsg = "";
        private DateTime lastMsgTime = DateTime.UtcNow;

        public bool isDuplicate = false;

        private Matchmake _match;
        public Matchmake matchmake
        {
            get { return _match; }
            set
            {
                _match = value;
                if (value != null)
                {
                    this.AsyncSend(ClearPublicLobbies(),
                        req => { });
                    this.AsyncSend(MatchmakeSnapshot(value), req => { });
                }
                else
                {
                    this.AsyncSend(PublicLobbySnapshot(),
                        req => { });
                    this.AsyncSend(ClearMatchmakeR(),
                        req => { });
                }
            }
        }

        public Mod[] QueuedWithMods = null;

        //User and lobby
        public User user = null;
        private Lobby _lobby = null;
        public Lobby lobby
        {
            get { return _lobby; }
            set
            {
                _lobby = value;
                if (value != null)
                {
                    this.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, ClearPublicLobbies(),
                        req => { });
                    FriendManager.updateStatus(this.user.steam.steamid, FriendStatus.InLobby, Mods.Mods.ByID(value.mod).fullname);
                }
                else
                {
                    this.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, PublicLobbySnapshot(),
                        req => { });
                    this.AsyncSendTo(m => m.user != null && m.user.Id == user.Id, ClearLobbyR(),
                        req => { });
                    FriendManager.updateStatus(this.user.steam.steamid, FriendStatus.Online);
                }
            }
        }

        public List<Friend> friendlist = null;

        #endregion

        #region Helpers
        public static void CheckLobby(BrowserController controller)
        {
            //if (controller.lobby != null && controller.lobby.deleted) controller.lobby = null;
        }

        public void RespondError(JObject req, string msg)
        {
            JObject resp = new JObject();
            resp["msg"] = "error";
            resp["reason"] = msg;
            resp["req"] = req["id"];
            Send(resp.ToString(Formatting.None));
        }

        /// <summary>
        /// Reload the user object from the database
        /// </summary>
        public void RefreshUser()
        {
            if (user == null) return;
            user = Mongo.Users.FindOneByIdAs<User>(user.Id);
        }

        /// <summary>
        /// Save any user changes in the DB
        /// </summary>
        public void SaveUser()
        {
            if (user == null) return;
            Mongo.Users.Save(user);
        }

        /// <summary>
        /// Has the user completed the test procedure?
        /// </summary>
        /// <param name="isTested">If set to <c>true</c> is tested.</param>
        public static void SetTested(User user, bool isTested)
        {
            if (user == null) return;
            if (isTested)
            {
                if (!user.authItems.Contains("tested"))
                {
                    var arr = new string[user.authItems.Length + 1];
                    int i = 0;
                    foreach (var auth in user.authItems)
                    {
                        arr[i] = auth;
                        i++;
                    }
                    arr[user.authItems.Length] = "tested";
                    user.authItems = arr;
                    Mongo.Users.Save(user);
                }
            }
            else
            {
                if (user.authItems.Contains("tested"))
                {
                    user.authItems = user.authItems.Where(m => m != "tested").ToArray();
                    Mongo.Users.Save(user);
                }
            }
        }

        #endregion

        #region Message Handling
        public override void OnMessage(ITextArgs args)
        {
            try
            {
                var jdata = JObject.Parse(args.data);
                var id = jdata["id"];
                if (id == null) return;
                Task.Factory.StartNew(() =>
                                      {
                                          lock (ConcurrentLock)
                                          {
                                              var command = id.Value<string>();
                                              switch (command)
                                              {
                                                  #region Authentication
                                                  case "deauth":
                                                      user = null;
                                                      this.SendJson("{\"status\": false}", "auth");
                                                      break;
                                                  case "auth":
                                                      try
                                                      {
                                                          //Parse the UID
                                                          var uid = jdata["uid"].Value<string>();
                                                          if (user != null && uid == user.Id)
                                                          {
                                                              this.SendJson("{\"msg\": \"auth\", \"status\": true}",
                                                                  "auth");
                                                              return;
                                                          }
                                                          //Parse the resume key
                                                          var key = jdata["key"].Value<string>();
                                                          //Find it in the database
                                                          var usr = Mongo.Users.FindOneAs<User>(Query.EQ("_id", uid));
                                                          if (usr != null)
                                                          {
                                                              var session =
                                                                  Mongo.Sessions.FindOneAs<Session>(Query.EQ("_id", key));
                                                              if (session == null || session.expires < DateTime.UtcNow)
                                                              {
                                                                  user = null;
                                                                  this.SendJson("{\"status\": false}", "auth");
                                                                  break;
                                                              }
                                                              if (usr.authItems != null &&
                                                                  usr.authItems.Contains("banned"))
                                                              {
                                                                  log.Debug(string.Format("User is banned {0}",
                                                                      usr.profile.name));
                                                                  RespondError(jdata,
                                                                      "You are banned from the lobby server.");
                                                                  this.SendJson(
                                                                      "{\"msg\": \"auth\", \"status\": false}",
                                                                      "auth");
                                                                  return;
                                                              }
                                                              user = usr;
                                                              var otherBrowsers =
                                                                  this.Find(m => m != this && m.user != null && m.user.Id == usr.Id);
                                                              var browser = otherBrowsers.FirstOrDefault();
                                                              if (browser != null)
                                                              {
                                                                  if (browser.lobby != null)
                                                                  {
                                                                      lobby = browser.lobby; this.AsyncSend(LobbySnapshot(lobby),
                                              cb => { });
                                                                  }
                                                                  browser.isDuplicate = true;
                                                                  browser.AsyncSend(AlreadyConnected(), cb => Task.Factory.StartNew
                                                                      (() =>
                                                                       {
                                                                           Thread.Sleep(1000);
                                                                           browser.Close();
                                                                       }));
                                                              }
                                                              this.SendJson("{\"msg\": \"auth\", \"status\": true}",
                                                                  "auth");
                                                              this.Send(PublicLobbySnapshot());
                                                              SendManagerStatus();
                                                              FriendManager.buildList(this);
                                                              FriendManager.updateStatus(this.user.steam.steamid, FriendStatus.Online);
                                                          }
                                                          else
                                                          {
                                                              user = null;
                                                              this.SendJson("{\"msg\": \"auth\", \"status\": false}",
                                                                  "auth");
                                                          }
                                                      }
                                                      catch (Exception ex)
                                                      {
                                                          RespondError(jdata,
                                                              "Issue authenticating you, we're working on it: " +
                                                              ex.Message);
                                                          log.Error("CANNOT AUTHENTICATE PLAYER", ex);
                                                      }
                                                      break;

                                                  #endregion
                                                  case "createlobby":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in!");
                                                              return;
                                                          }
                                                          //Check if they are in a lobby
                                                          //CheckLobby();
                                                          if (lobby != null)
                                                          {
                                                              RespondError(jdata, "You are already in a lobby.");
                                                              return; //omfg
                                                          }
                                                          if (matchmake != null)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are already in a matchmaking queue.");
                                                              return;
                                                          }
#if !DEV
                                                          if(!user.authItems.Contains("tested")){
                                                              var obj = new JObject();
                                                              obj["msg"] = "testneeded";
                                                              Send(obj.ToString(Formatting.None));
                                                              return;
                                                          }
#endif

													      if(user.authItems.Contains("createban")){
										                      RespondError(jdata, "You are banned from creating lobbies.");
													  	      return;
													      }
									                  
                                                          //Parse the create lobby request
                                                          var req = jdata["req"].ToObject<CreateLobby>();
                                                          if (req.name == null)
                                                          {
                                                              req.name = user.profile.name + "'s Lobby";
                                                          }
                                                          if (req.mod == null)
                                                          {
                                                              RespondError(jdata, "You did not specify a mod.");
                                                              return;
                                                          }
                                                          //Find the mod
                                                          var mod = Mods.Mods.ByID(req.mod);
                                                          if (mod == null || !mod.isPublic)
                                                          {
                                                              RespondError(jdata,
                                                                  "Can't find the mod, you probably don't have access.");
                                                              return;
                                                          }
                                                          if (!mod.playable)
                                                          {
                                                              RespondError(jdata,
                                                                  "That mod is not playable right now.");
                                                              return;
                                                          }
                                                          //Find the client
                                                          var clients = ClientsController.Find(m => m.UID == user.Id);
                                                          if (
                                                              !clients.Any(
                                                                  m =>
                                                                      m.Mods.Any(
                                                                          c =>
                                                                              c.name == mod.name && c.version == mod.version)))
                                                          {
                                                              var obj = new JObject();
                                                              obj["msg"] = "modneeded";
                                                              obj["name"] = mod.name;
                                                              Send(obj.ToString(Formatting.None));
                                                              return;
                                                          }

                                                          lobby = LobbyManager.CreateLobby(user, mod, req.name);
                                                          break;
                                                      }
                                                  case "stopmatchmake":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in!");
                                                              return;
                                                          }
                                                          if (matchmake == null)
                                                          {
                                                              RespondError(jdata, "You are not matchmaking.");
                                                              return;
                                                          }
                                                          MatchmakeManager.LeaveMatchmake(this);
                                                          break;
                                                      }
                                                  case "matchmake":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in!");
                                                              return;
                                                          }
                                                          if (lobby != null)
                                                          {
                                                              RespondError(jdata, "You are already in a lobby.");
                                                              return;
                                                          }
                                                          if (matchmake != null)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are already in a matchmaking queue.");
                                                              return;
                                                          }
                                                          if (user.profile.PreventMMUntil > DateTime.UtcNow)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are prevented from matchmaking until " +
                                                                  Utils.TimeFromNow(user.profile.PreventMMUntil, true) + ".");
                                                              return;
                                                          }
                                                          // Parse the Matchmake request
                                                          var req = jdata["req"].ToObject<doMatchmake>();
                                                          if (req.mods == null)
                                                          {
                                                              RespondError(jdata, "You did not specify any mods.");
                                                              return;
                                                          }
                                                          var client =
                                                              ClientsController.Find(m => m.UID == user.Id)
                                                                  .FirstOrDefault();
                                                          var mods = new List<Mod>();
                                                          foreach (var mod in req.mods.Select(Mods.Mods.ByID))
                                                          {
                                                              if (mod == null)
                                                              {
                                                                  RespondError(jdata,
                                                                      "Can't find one of the mods.");
                                                                  continue;
                                                              }
                                                              if (!mod.ranked)
                                                              {
                                                                  RespondError(jdata,
                                                                      mod.fullname +
                                                                      " is not available for ranked matchmaking.");
                                                                  continue;
                                                              }
                                                              mods.Add(mod);

                                                              // TODO: In the future, when mod downloading is browser independent, we can send an array of mods to install
                                                              if (client == null || !client.Mods.Any(c => c.name == mod.name && c.version == mod.version))
                                                              {
                                                                  var obj = new JObject();
                                                                  obj["msg"] = "modneeded";
                                                                  obj["name"] = mod.name;
                                                                  Send(obj.ToString(Formatting.None));
                                                                  return;
                                                              }
                                                          }
                                                          if (mods.Count == 0)
                                                          {
                                                              RespondError(jdata,
                                                                  "You didn't specifiy any mods, or all of the mods are not ranked.");
                                                              return;
                                                          }
                                                          QueuedWithMods = mods.ToArray();
                                                          matchmake = MatchmakeManager.CreateMatchmake(user, QueuedWithMods);
                                                          break;
                                                      }
                                                  case "switchteam":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in.");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby.");
                                                              return;
                                                          }
                                                          //Parse the switch team request
                                                          var req = jdata["req"].ToObject<SwitchTeam>();
                                                          var goodguys = req.team == "radiant";
                                                          if ((goodguys && lobby.TeamCount(lobby.radiant) >= 5) ||
                                                              (!goodguys && lobby.TeamCount(lobby.dire) >= 5))
                                                          {
                                                              RespondError(jdata, "That team is full.");
                                                              return;
                                                          }
                                                          LobbyManager.RemoveFromTeam(lobby, user.steam.steamid);
                                                          lobby.AddPlayer(goodguys ? lobby.radiant : lobby.dire,
                                                              Player.FromUser(user, lobby.creatorid == user.Id));
                                                          LobbyManager.TransmitLobbyUpdate(lobby, new[] { "radiant", "dire" });
                                                          break;
                                                      }
                                                  case "leavelobby":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in.");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby.");
                                                              return;
                                                          }
                                                          LobbyManager.LeaveLobby(this);
                                                          break;
                                                      }
                                                  case "chatmsg":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in (can't chat).");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby (can't chat).");
                                                              return;
                                                          }
                                                          var req = jdata["req"].ToObject<ChatMessage>();
                                                          var msg = req.message;
                                                          if (msg == null) return;
                                                          msg = Regex.Replace(msg, "^[\\w \\.\"'[]\\{\\}\\(\\)]+", "");
                                                          if (msg == lastMsg)
                                                          {
                                                              RespondError(jdata,
                                                                  "You cannot send the same message twice in a row.");
                                                              return;
                                                          }
                                                          var now = DateTime.UtcNow;
                                                          TimeSpan span = now - lastMsgTime;
                                                          if (span.TotalSeconds < 2)
                                                          {
                                                              RespondError(jdata,
                                                                  "You must wait 2 seconds between each message.");
                                                              return;
                                                          }
                                                          lastMsg = msg;
                                                          lastMsgTime = now;
                                                          LobbyManager.ChatMessage(lobby, msg, user.profile.name);
                                                          break;
                                                      }
                                                  case "kickplayer":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in (can't kick).");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby (can't kick).");
                                                              return;
                                                          }
                                                          if (lobby.creatorid != user.Id)
                                                          {
                                                              RespondError(jdata, "You are not the lobby host.");
                                                              return;
                                                          }
                                                          var req = jdata["req"].ToObject<KickPlayer>();
                                                          if (!LobbyManager.BanFromLobby(lobby, req.steam))
                                                          {
                                                              RespondError(jdata, "You are not allowed to kick admins.");
                                                              return;
                                                          }
                                                          break;
                                                      }
                                                  case "setname":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in (can't set name).");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby (can't set name).");
                                                              return;
                                                          }
                                                          if (lobby.creatorid != user.Id)
                                                          {
                                                              RespondError(jdata, "You are not the lobby host.");
                                                              return;
                                                          }

                                                          var req = jdata["req"].ToObject<SetName>();
                                                          var err = req.Validate();
                                                          if (err != null)
                                                          {
                                                              RespondError(jdata, err);
                                                              return;
                                                          }
                                                          LobbyManager.SetTitle(lobby, req.name);
                                                          break;
                                                      }
                                                  case "setpassword":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in (can't set name).");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby (can't set name).");
                                                              return;
                                                          }
                                                          if (lobby.creatorid != user.Id)
                                                          {
                                                              RespondError(jdata, "You are not the lobby host.");
                                                              return;
                                                          }

                                                          var req = jdata["req"].ToObject<SetPassword>();
                                                          LobbyManager.SetPassword(lobby, req.password);
                                                          break;
                                                      }
                                                  case "setregion":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are not logged in (can't set region).");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are not in a lobby (can't set region).");
                                                              return;
                                                          }
                                                          if (lobby.creatorid != user.Id)
                                                          {
                                                              RespondError(jdata, "You are not the lobby host.");
                                                              return;
                                                          }
                                                          var req = jdata["req"].ToObject<SetRegion>();
                                                          LobbyManager.SetRegion(lobby, req.region);
                                                          break;
                                                      }
                                                  case "startqueue":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in yet.");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby.");
                                                              return;
                                                          }
                                                          if (lobby.creatorid != user.Id)
                                                          {
                                                              RespondError(jdata, "You are not the lobby host.");
                                                              return;
                                                          }
                                                          if (lobby.status != LobbyStatus.Start)
                                                          {
                                                              RespondError(jdata, "You are already queuing/playing.");
                                                              return;
                                                          }
                                                          if (lobby.requiresFullLobby &&
                                                              (lobby.TeamCount(lobby.dire) + lobby.TeamCount(lobby.radiant) <
                                                               10))
                                                          {
                                                              RespondError(jdata, "Your lobby must be full to start.");
                                                              return;
                                                          }
                                                          LobbyManager.StartQueue(lobby);
                                                          return;
                                                      }
                                                  case "stopqueue":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in yet.");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby.");
                                                              return;
                                                          }
                                                          if (lobby.creatorid != user.Id)
                                                          {
                                                              RespondError(jdata, "You are not the lobby host.");
                                                              return;
                                                          }
                                                          if (lobby.status != LobbyStatus.Queue)
                                                          {
                                                              RespondError(jdata, "You are not queueing.");
                                                              return;
                                                          }
                                                          LobbyManager.CancelQueue(lobby);
                                                          return;
                                                      }
                                                  case "joinlobby":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in yet.");
                                                              return;
                                                          }
                                                          if (lobby != null)
                                                          {
                                                              RespondError(jdata, "You are already in a lobby.");
                                                              return;
                                                          }
                                                          if (matchmake != null)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are already in a matchmaking queue.");
                                                              return;
                                                          }
#if !DEV
                                                          if(!user.authItems.Contains("tested")){
                                                              var obj = new JObject();
                                                              obj["msg"] = "testneeded";
                                                              Send(obj.ToString(Formatting.None));
                                                              return;
                                                          }
#endif
                                                          var req = jdata["req"].ToObject<JoinLobby>();
                                                          Lobby lob = null;
                                                          //Find lobby
                                                          lock (LobbyManager.PublicLobbies)
                                                          {
                                                              lob =
                                                                  LobbyManager.PublicLobbies.FirstOrDefault(
                                                                      m => m.id == req.LobbyID);
                                                          }
                                                          if (lob == null)
                                                          {
                                                              RespondError(jdata, "Can't find that lobby.");
                                                              return;
                                                          }
                                                          if (lob.TeamCount(lob.dire) >= 5 &&
                                                              lob.TeamCount(lob.radiant) >= 5)
                                                          {
                                                              RespondError(jdata, "That lobby is full.");
                                                              return;
                                                          }
                                                          if (lob.banned.Contains(user.steam.steamid))
                                                          {
                                                              RespondError(jdata, "You are banned from that lobby.");
                                                              return;
                                                          }
                                                          //Find the mod
                                                          var mod = Mods.Mods.ByID(lob.mod);
                                                          if (mod == null)
                                                          {
                                                              RespondError(jdata,
                                                                  "Can't find the mod, you probably don't have access.");
                                                              return;
                                                          }
                                                          //Find the client
                                                          var clients = ClientsController.Find(m => m.UID == user.Id);
                                                          if (
                                                              !clients.Any(
                                                                  m =>
                                                                      m.Mods.Any(
                                                                          c =>
                                                                              c.name == mod.name && c.version == mod.version)))
                                                          {
                                                              var obj = new JObject();
                                                              obj["msg"] = "modneeded";
                                                              obj["name"] = mod.name;
                                                              Send(obj.ToString(Formatting.None));
                                                              return;
                                                          }

                                                          LobbyManager.JoinLobby(lob, user, this);
                                                          break;
                                                      }
                                                  case "invitefriend":
                                                  {
                                                      if (user == null)
                                                      {
                                                          RespondError(jdata, "You are not logged in.");
                                                          return;
                                                      }
                                                      if (lobby == null)
                                                      {
                                                          RespondError(jdata, "You are not in a lobby to invite your friend to.");
                                                          return;
                                                      }
                                                      var req = jdata["req"].ToObject<InviteFriend>();
                                                      FriendManager.InviteFriend(this, req.steamid);
                                                      break;
                                                  }
                                                  case "joinpasswordlobby":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in yet.");
                                                              return;
                                                          }
                                                          if (lobby != null)
                                                          {
                                                              RespondError(jdata, "You are already in a lobby.");
                                                              return;
                                                          }
                                                          if (matchmake != null)
                                                          {
                                                              RespondError(jdata,
                                                                  "You are already in a matchmaking queue.");
                                                              return;
                                                          }
                                                          var req = jdata["req"].ToObject<JoinPasswordLobby>();
                                                          //Find lobby
                                                          var lob =
                                                              LobbyManager.PlayingLobbies.FirstOrDefault(
                                                                  m => m.hasPassword && m.password == req.password);
                                                          if (lob == null)
                                                          {
                                                              RespondError(jdata,
                                                                  "Can't find any lobbies with that password.");
                                                              return;
                                                          }
                                                          if (lob.TeamCount(lob.dire) >= 5 &&
                                                              lob.TeamCount(lob.radiant) >= 5)
                                                          {
                                                              RespondError(jdata, "That lobby is full.");
                                                              return;
                                                          }
                                                          if (lob.banned.Contains(user.steam.steamid))
                                                          {
                                                              RespondError(jdata, "You are banned from that lobby.");
                                                              return;
                                                          }
                                                          //Find the mod
                                                          var mod = Mods.Mods.ByID(lob.mod);
                                                          if (mod == null)
                                                          {
                                                              RespondError(jdata,
                                                                  "Can't find the mod, you probably don't have access.");
                                                              return;
                                                          }
                                                          //Find the client
                                                          var clients = ClientsController.Find(m => m.UID == user.Id);
                                                          if (
                                                              !clients.Any(
                                                                  m =>
                                                                      m.Mods.Any(
                                                                          c =>
                                                                              c.name == mod.name && c.version == mod.version)))
                                                          {
                                                              var obj = new JObject();
                                                              obj["msg"] = "modneeded";
                                                              obj["name"] = mod.name;
                                                              Send(obj.ToString(Formatting.None));
                                                              return;
                                                          }

                                                          LobbyManager.JoinLobby(lob, user, this);
                                                          break;
                                                      }
                                                  case "joinfriendlobby":
                                                  {
                                                      if (user == null)
                                                      {
                                                          RespondError(jdata, "You are not logged in yet.");
                                                          return;
                                                      }
                                                      if (lobby != null)
                                                      {
                                                          RespondError(jdata, "You are already in a lobby.");
                                                          return;
                                                      }
#if !DEV
                                                      if (!user.authItems.Contains("tested"))
                                                      {
                                                          var obj = new JObject();
                                                          obj["msg"] = "testneeded";
                                                          Send(obj.ToString(Formatting.None));
                                                          return;
                                                      }
#endif
                                                      var req = jdata["req"].ToObject<JoinFriendLobby>();
                                                      //Find lobby
                                                      var lob =
                                                          LobbyManager.PlayingLobbies.FirstOrDefault(
                                                              m => m.getPlayers().Where(p=>p.steam == req.steamid).Any());
                                                      if (lob == null)
                                                      {
                                                          RespondError(jdata,
                                                              "Player is not in a lobby.");
                                                          return;
                                                      }
                                                      if (lob.TeamCount(lob.dire) >= 5 &&
                                                          lob.TeamCount(lob.radiant) >= 5)
                                                      {
                                                          RespondError(jdata, "That lobby is full.");
                                                          return;
                                                      }
                                                      if (lob.banned.Contains(user.steam.steamid))
                                                      {
                                                          RespondError(jdata, "You are banned from that lobby.");
                                                          return;
                                                      }
                                                      //Find the mod
                                                      var mod = Mods.Mods.ByID(lob.mod);
                                                      if (mod == null)
                                                      {
                                                          RespondError(jdata,
                                                              "Can't find the mod, you probably don't have access.");
                                                          return;
                                                      }
                                                      //Find the client
                                                      var clients = ClientsController.Find(m => m.UID == user.Id);
                                                      if (
                                                          !clients.Any(
                                                              m =>
                                                                  m.Mods.Any(
                                                                      c =>
                                                                          c.name == mod.name && c.version == mod.version)))
                                                      {
                                                          var obj = new JObject();
                                                          obj["msg"] = "modneeded";
                                                          obj["name"] = mod.name;
                                                          Send(obj.ToString(Formatting.None));
                                                          return;
                                                      }
                                                      LobbyManager.JoinLobby(lob, user, this, req.steamid);
                                                      break;
                                                  }
                                                  case "installmod":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in.");
                                                              return;
                                                          }
                                                          var clients = ClientsController.Find(m => m.UID == user.Id);
                                                          var clientControllers = clients as ClientController[] ??
                                                                                  clients.ToArray();
                                                          if (!clientControllers.Any())
                                                          {
                                                              //Error message
                                                              RespondError(jdata, "Your client has not been started yet.");
                                                              return;
                                                          }
                                                          var req = jdata["req"].ToObject<InstallMod>();
                                                          var mod = Mods.Mods.ByName(req.mod);
                                                          if (mod == null || !mod.playable)
                                                          {
                                                              this.AsyncSend(InstallResponse("Can't find that mod, or it's not playable.",
                                                                      true),
                                                                  rf => { });
                                                              return;
                                                          }
                                                          if (
                                                              clientControllers.FirstOrDefault()
                                                                  .Mods.Any(
                                                                      m => m.name == mod.name && m.version == mod.version))
                                                          {
                                                              this.AsyncSend(InstallResponse("The mod has already been installed.",
                                                                      true),
                                                                  rf => { });
                                                              return;
                                                          }

                                                          ClientsController.InstallMod(user.Id, mod);
                                                          break;
                                                      }
                                                  case "connectgame":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in yet.");
                                                              return;
                                                          }
                                                          if (lobby == null)
                                                          {
                                                              RespondError(jdata, "You are not in a lobby.");
                                                              return;
                                                          }
                                                          if (lobby.status != LobbyStatus.Play)
                                                          {
                                                              RespondError(jdata, "Your lobby isn't ready to play yet.");
                                                              return;
                                                          }
                                                          LobbyManager.LaunchAndConnect(lobby, user.steam.steamid);
                                                          break;
                                                      }
                                                  case "clearpubliclobbies":
                                                      {
                                                          if (user == null || !user.authItems.Contains("admin"))
                                                          {
                                                              RespondError(jdata, "You are not allowed to do this.");
                                                              return;
                                                          }
                                                          LobbyManager.ClearPendingLobbies();
                                                          break;
                                                      }
                                                  case "startLoadTest":
                                                      {
                                                          if (user == null)
                                                          {
                                                              RespondError(jdata, "You are not logged in yet.");
                                                              return;
                                                          }
                                                          if (lobby != null)
                                                          {
                                                              RespondError(jdata, "You are in a lobby already.");
                                                              return;
                                                          }
                                                          var mod = Mods.Mods.ByName("checker");
                                                          var clients = ClientsController.Find(m => m.UID == user.Id);
                                                          var clientControllers = clients as ClientController[] ??
                                                                                  clients.ToArray();
                                                          if (!clientControllers.Any())
                                                          {
                                                              //Error message
                                                              RespondError(jdata, "Your client has not been started yet.");
                                                              return;
                                                          }
                                                          if (!clientControllers.First().Mods.Any(m => m.name == mod.name && m.version == mod.version))
                                                          {
                                                              RespondError(jdata,
                                                                  "You need to click the green Install Mod button first.");
                                                              return;
                                                          }
                                                          /*if (user.authItems.Contains("tested"))
                                                          {
                                                              RespondError(jdata, "You have already completed the test.");
                                                              return;
                                                          }*/
                                                          lobby = LobbyManager.StartPlayerTest(user);
                                                          break;
                                                      }
                                                  default:
                                                      log.Debug(string.Format("Unknown command: {0}...",
                                                          command.Substring(0, 10)));
                                                      return;
                                              }
                                          }
                                      });
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
            } //Handle all malformed JSON / no ID field / other troll data
        }
        #endregion

        public void Send(string msg)
        {
            this.SendJson(msg, "lobby");
        }

        /// <summary>
        /// Detect manager status and send it out w/o active reference.
        /// </summary>
        public void SendManagerStatus()
        {
            SendManagerStatus(user != null && ClientsController.Find(m => m.Inited && m.SteamID == user.steam.steamid).Any());
        }

        public void SendManagerStatus(bool isConnected)
        {
            this.Send(ManagerStatus(isConnected));
        }

        public void OnClosed(object sender, OnClientDisconnectArgs e)
        {
            if (!isDuplicate)
            {
                LobbyManager.ForceLeaveLobby(this);
                MatchmakeManager.LeaveMatchmake(this);
                if(this.user != null)
                    FriendManager.updateStatus(this.user.steam.steamid, FriendStatus.Offline);
            }
        }

        public static ITextArgs ClearLobbyR()
        {
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray { DiffGenerator.RemoveAll("lobbies") };
            var msg = upd.ToString(Formatting.None);
            return new TextArgs(msg, "lobby");
        }

        public static ITextArgs AlreadyConnected()
        {
            var upd = new JObject();
            upd["msg"] = "alreadyconn";
            var msg = upd.ToString(Formatting.None);
            return new TextArgs(msg, "duplicate");
        }

        public static ITextArgs ManagerStatus(bool isConnected)
        {
            var upd = new JObject();
            upd["msg"] = "status";
            upd["status"] = isConnected;
            var msg = upd.ToString(Formatting.None);
            return new TextArgs(msg, "manager");
        }

        public static ITextArgs LobbySnapshot(Lobby lobby1)
        {
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray { DiffGenerator.RemoveAll("lobbies"), lobby1.Add("lobbies") };
            return new TextArgs(upd.ToString(Formatting.None), "lobby");
        }

        public static ITextArgs PublicLobbySnapshot()
        {
            var upd = new JObject();
            var ops = new JArray { DiffGenerator.RemoveAll("publicLobbies") };
            try
            {
                Lobby[] snap;
                lock (LobbyManager.PublicLobbies)
                    snap = LobbyManager.PublicLobbies.ToArray();
                foreach (var lobby in snap)
                {
                    if (lobby != null)
                        ops.Add(lobby.Add("publicLobbies"));
                }
            }
            catch (Exception ex)
            {
                log.Error("Problem creating lobby snapshot: ", ex);
            }
            upd["msg"] = "colupd";
            upd["ops"] = ops;
            return new TextArgs(upd.ToString(Formatting.None), "lobby");
        }
        public static ITextArgs MatchmakeSnapshot(Matchmake mm1)
        {
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray { DiffGenerator.RemoveAll("matchmake"), mm1.Add("matchmake") };
            return new TextArgs(upd.ToString(Formatting.None), "lobby");
        }

        public static ITextArgs ClearMatchmakeR()
        {
            var upd = new JObject();
            upd["msg"] = "colupd";
            upd["ops"] = new JArray {DiffGenerator.RemoveAll("matchmake")};
            var msg = upd.ToString(Formatting.None);
            return new TextArgs(msg, "lobby");
        }

        public static ITextArgs FriendsSnapshot(List<Friend> l)
        {
            var upd = new JObject();
            var ops = new JArray { DiffGenerator.RemoveAll("friends") };
            try
            {
                Friend[] snap;
                snap = l.ToArray();
                foreach (var friend in snap)
                {
                    if (friend != null)
                        ops.Add(friend.Add("friends"));
                }
            }
            catch (Exception ex)
            {
                log.Error("Problem creating friend snapshot: ", ex);
            }
            upd["msg"] = "colupd";
            upd["ops"] = ops;
            return new TextArgs(upd.ToString(Formatting.None), "friend");
        }

        public static ITextArgs inviteFriend(Lobby l, string sourceSteamid)
        {
            var cmd = new JObject();
            cmd["msg"] = "invite";
            cmd["source"] = sourceSteamid;
            cmd["mod"] = Mods.Mods.ByID(l.mod).fullname;
            return new TextArgs(cmd.ToString(Formatting.None), "invite");
        }

        public static ITextArgs ClearPublicLobbies()
        {
            var upd = new JObject();
            var ops = new JArray { DiffGenerator.RemoveAll("publicLobbies") };
            upd["msg"] = "colupd";
            upd["ops"] = ops;
            return new TextArgs(upd.ToString(Formatting.None), "lobby");
        }

        public static ITextArgs ChatMessage(string cmsg)
        {
            var cmd = new JObject();
            cmd["msg"] = "chat";
            cmd["message"] = cmsg;
            var data = cmd.ToString(Formatting.None);
            return new TextArgs(data, "lobby");
        }

        public static ITextArgs InstallResponse(string message, bool success)
        {
            var upd = new JObject();
            upd["msg"] = "installres";
            upd["success"] = success;
            upd["message"] = message;
            return new TextArgs(upd.ToString(Formatting.None), "lobby");
        }

        public static ITextArgs UpdateMods()
        {
            var upd = new JObject();
            upd["msg"] = "updatemods";
            return new TextArgs(upd.ToString(Formatting.None), "updatemods");
        }
    }
}
