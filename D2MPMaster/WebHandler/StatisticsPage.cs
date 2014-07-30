using System;
using System.Linq;
using D2MPMaster.Browser;
using D2MPMaster.Database;
using D2MPMaster.Lobbies;
using D2MPMaster.Server;
using d2mpserver;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceStack.Text;
using XSockets.Core.XSocket.Helpers;

namespace D2MPMaster.WebHandler
{
    public class StatisticsPage : NancyModule
    {
        private const string secret = "3J6EB7QIWUsCyk4MKBSe8y";
        private static readonly BrowserController Browsers = new BrowserController();
        private int lastMonth = 0;
        private DateTime lastupdated = DateTime.UtcNow;
        public StatisticsPage()
        {
            Get["/stats/general"] = data => HandleStatsGeneral();
            Get["/stats/"+secret+"/lobbies"] = data => HandleStatsLobbies();
            Get["/stats/"+secret+"/servers"] = data => HandleStatsServers();
            Get["/stats/mods"] = data => HandleStatsMods();
            Get["/stats/players"] = data => HandleStatsPlayers();
        }

        private string HandleStatsPlayers()
        {
            JObject data = new JObject();

            var browsers = Browsers.Find(m => m.user != null);
            data["online"] = browsers.Count();
            if(lastMonth == 0 || (DateTime.UtcNow-lastupdated).TotalHours>1)
                data["lastmonth"] = lastMonth = (int)Mongo.Users.Count(Query.GT("steam.lastlogoff", DateTime.Now.AddDays(-30).ToUnixTime()));
            else
            {
                data["lastmonth"] = lastMonth;
            }
            data["playing"] = browsers.Count(m=>m.lobby != null);

            return data.ToString(Formatting.Indented);
        }

        private string HandleStatsServers()
        {
            JObject data = new JObject();

            JArray servers;
            data["servers"] =servers= new JArray();

            foreach (var server in ServerService.Servers.Find(m=>m.Inited))
            {
                JObject serverj = new JObject();
                serverj["name"] = server.InitData.name;
                serverj["ip"] = server.Address;
                serverj["activeinstances"] = server.Instances.Count;
                serverj["maxinstances"] = server.InitData.serverCount;
                serverj["region"] = JArray.FromObject(server.InitData.regions);
                JArray instances = new JArray();
                serverj["instances"] = instances;
                foreach (var instance in server.Instances.Values)
                {
                    JObject jinstance = new JObject();
                    jinstance["id"] = instance.ID;
                    jinstance["lobby"] = JObject.FromObject(instance.lobby);
                    instances.Add(jinstance);
                }
                servers.Add(serverj);
            }

            return data.ToString(Formatting.Indented);
        }

        private string HandleStatsMods()
        {
            JObject data = new JObject();

            JArray mods;
            data["mods"] = mods = new JArray();

            foreach (var mod in Mods.Mods.ModCache.Values)
            {
                var modj = new JObject();
                modj["name"] = mod.name;
                modj["version"] = mod.version;
                modj["lobbies"] = LobbyManager.LobbyID.Values.Count(m => m.mod == mod.Id);
                mods.Add(modj);
            }

            return data.ToString(Formatting.Indented);
        }

        private string HandleStatsLobbies()
        {
            JObject data = new JObject();

            JArray lobbies;
            data["lobbies"] = lobbies = new JArray();

            lock (LobbyManager.PublicLobbies)
            {
                foreach (var lob in LobbyManager.LobbyID.Values)
                {
                    lobbies.Add(JObject.FromObject(lob));
                }
            }

            return data.ToString(Formatting.Indented);
        }

        private string HandleStatsGeneral()
        {
            JObject data = new JObject();

            lock (LobbyManager.PublicLobbies)
            {
                //Lobby statistics
                data["lobby_total"] = LobbyManager.LobbyID.Values.Count;
                data["lobby_wait"] = LobbyManager.LobbyID.Values.Count(m => m.status == LobbyStatus.Start);
                data["lobby_play"] = LobbyManager.LobbyID.Values.Count(m => m.status == LobbyStatus.Play);
                lock (LobbyManager.LobbyQueue)
                    data["lobby_queue"] = LobbyManager.LobbyQueue.Count;

                JArray regions;
                data["regions"] = regions = new JArray();

                foreach (var region in (int[]) Enum.GetValues(typeof (ServerRegion)))
                {
                    var regionj = new JObject();
                    regionj["name"] = Enum.GetName(typeof (ServerRegion), region);
                    regionj["id"] = region;
                    var serversL =
                        ServerService.Servers.Find(
                            m => m.Inited && m.InitData.regions.Contains((ServerCommon.ServerRegion) region));
                    var serverControllers = serversL as ServerController[] ?? serversL.ToArray();
                    regionj["servercount"] = serverControllers.Count();
                    regionj["playing"] = serverControllers.Sum(server => server.Instances.Count);
                    regions.Add(regionj);
                }

                JArray servers;
                data["servers"] = servers = new JArray();

                foreach (var server in ServerService.Servers.Find(m => m.Inited))
                {
                    var serverj = new JObject();
                    serverj["name"] = server.InitData.name;
                    serverj["ip"] = server.Address;
                    serverj["activeinstances"] = server.Instances.Count;
                    serverj["maxinstances"] = server.InitData.serverCount;
                    serverj["region"] = JArray.FromObject(server.InitData.regions);
                    servers.Add(serverj);
                }

                return data.ToString(Formatting.Indented);
            }
        }
    }
}
                                                                                                              