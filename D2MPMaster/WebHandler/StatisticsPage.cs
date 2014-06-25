﻿using System;
using System.Linq;
using D2MPMaster.Lobbies;
using D2MPMaster.Server;
using d2mpserver;
using Nancy;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XSockets.Core.XSocket.Helpers;

namespace D2MPMaster.WebHandler
{
    public class StatisticsPage : NancyModule
    {
        public StatisticsPage()
        {
            Get["/stats/general"] = data => HandleStatsGeneral();
        }

        private string HandleStatsGeneral()
        {
            JObject data = new JObject();

            //Lobby statistics
            data["lobby_total"] = LobbyManager.LobbyID.Values.Count;
            data["lobby_wait"] = LobbyManager.LobbyID.Values.Count(m => m.status == LobbyStatus.Start);
            data["lobby_play"] = LobbyManager.LobbyID.Values.Count(m => m.status == LobbyStatus.Play);
            lock (LobbyManager.LobbyQueue)
                data["lobby_queue"] = LobbyManager.LobbyQueue.Count;

            JArray regions;
            data["regions"] = regions = new JArray();

            foreach (var region in (int[])Enum.GetValues(typeof(ServerRegion)))
            {
                var regionj = new JObject();
                regionj["name"] = Enum.GetName(typeof (ServerRegion), region);
                regionj["id"] = region;
                var serversL =
                    ServerService.Servers.Find(m => m.Inited && m.InitData.region == (ServerCommon.ServerRegion) region);
                var serverControllers = serversL as ServerController[] ?? serversL.ToArray();
                regionj["servercount"] = serverControllers.Count();
                regionj["playing"] = serverControllers.Sum(server => server.Instances.Count);
                regions.Add(regionj);
            }

            JArray servers;
            data["servers"] = servers = new JArray();

            foreach (var server in ServerService.Servers.Find(m=>m.Inited))
            {
                var serverj = new JObject();
                serverj["name"] = server.InitData.name;
                serverj["ip"] = server.Address;
                serverj["activeinstances"] = server.Instances.Count;
                serverj["maxinstances"] = server.InitData.serverCount;
                serverj["region"] = (int)server.InitData.region;
                servers.Add(serverj);
            }

            return data.ToString(Formatting.Indented);
        }
    }
}
                                                                                                              