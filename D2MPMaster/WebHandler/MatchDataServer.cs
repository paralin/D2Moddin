using System;
using System.IO;
using System.Linq;
using D2MPMaster.Lobbies;
using Nancy;
using Nancy.ErrorHandling;
using Nancy.Responses;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ServiceStack.Text;

namespace D2MPMaster.MatchData
{
    public class MatchDataHandler : NancyModule
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public MatchDataHandler()
        {
            Post["/gdataapi/matchres"] = paramaters => HandleMatchRes(this.Request);
            Get["/clientver"] =
                paramaters => string.Format("version:{0}|http://cdn.germangang.com/d2moddin/{0}.zip",
                    ClientCommon.Version.ClientVersion);
        }

        private string HandleMatchRes(Request ctx)
        {
            StreamReader reader = new StreamReader(ctx.Body);
            string req = reader.ReadToEnd();
            try
            {
                var baseData = JObject.Parse(req);
                var status = baseData.Value<string>("status");
                var matchid = baseData.Value<string>("match_id");
                Lobby lob;
                if(!LobbyManager.LobbyID.TryGetValue(matchid, out lob)) return "doesntexist";
                if (status == "events")
                {
                    var events = baseData.Value<JArray>("events");
                    foreach (var eve in events)
                    {
                        LobbyManager.HandleEvent((GameEvents) eve.Value<int>("event_type"), eve, lob);
                    }
                }else if (status == "completed")
                {
                    var data = JsonConvert.DeserializeObject<Model.MatchData>(baseData.ToString());
                    data.ranked = lob.LobbyType == LobbyType.Matchmaking;
                    data.ConvertData();
                    data.steamids = data.teams[0].players.Select(x=>x.steam_id).Union(data.teams[1].players.Select(y=>y.steam_id)).ToArray();
                    data.date = DateTime.UtcNow.ToUnixTime();
					HandleMatchComplete(data, lob);
                }
                else if (status == "load_failed")
                {
                    HandleLoadFail(matchid, baseData.Value<JArray>("failed_players")); //parse the players property here for mroe reliable data
                }
                else
                {
                    log.Debug(req);
                }
                return "success";
            }
            catch{} //Ignore any JSON parser errors
            return "fail";
        }

        private static void HandleLoadFail(string matchid, JArray failedPlayers)
        {
            LobbyManager.OnLoadFail(matchid, failedPlayers);
        }

		private static void HandleMatchComplete(Model.MatchData toObject, Lobby lob)
        {
			var mod = Mods.Mods.ByID(lob.mod);
			if(mod != null)
				toObject.mod = mod.name;
			else
				toObject.mod = lob.mod;
            LobbyManager.OnMatchComplete(toObject);
        }
    }
    public class My404Hander : IStatusCodeHandler
    {
        public bool HandlesStatusCode(HttpStatusCode statusCode,
                                      NancyContext context)
        {
            return statusCode == HttpStatusCode.NotFound;
        }

        public void Handle(HttpStatusCode statusCode, NancyContext context)
        {
            context.Response = new TextResponse(statusCode, "Not found.");
        }
    }
}
