using System;
using Anna;
using Anna.Request;
using D2MPMaster.Lobbies;
using Newtonsoft.Json.Linq;

namespace D2MPMaster.MatchData
{
    public class MatchDataServer
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private HttpServer server;
        public MatchDataServer(string url)
        {
            this.server = new HttpServer(url);
            SetupServerBinds();
        }

        private void SetupServerBinds()
        {
            server.POST("/gdataapi/matchres")
                .Subscribe(HandleMatchRes);
            server.GET("/clientver")
                .Subscribe(
                    req => req.Respond(string.Format("version:{0}|https://s3-us-west-2.amazonaws.com/d2mpclient/{0}.zip",
                        ClientCommon.Version.ClientVersion)));
        }

        private void HandleMatchRes(RequestContext ctx)
        {
            ctx.Request.GetBody().Subscribe(req =>
                {
                    try
                    {
                        var baseData = JObject.Parse(req);
                        var status = baseData.Value<string>("status");
                        var matchid = baseData.Value<string>("match_id");
                        Lobby lob;
                        if(!LobbyManager.LobbyID.TryGetValue(matchid, out lob)) return;
                        if (status == "events")
                        {
                            
                        }else if (status == "completed")
                        {
                            HandleMatchComplete(baseData.ToObject<Model.MatchData>());
                        }
                        else if (status == "load_failed")
                        {
                            HandleLoadFail(matchid);
                        }
                    }
                    catch{} //Ignore any JSON parser errors
                });
        }

        private static void HandleLoadFail(string matchid)
        {
            LobbyManager.OnLoadFail(matchid);
        }

        private static void HandleMatchComplete(Model.MatchData toObject)
        {
            LobbyManager.OnMatchComplete(toObject);
        }

        public void Shutdown()
        {
            server.Dispose();
        }
    }
}
