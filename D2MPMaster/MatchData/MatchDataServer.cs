using System;
using Anna;
using Anna.Request;

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
        }

        private void HandleMatchRes(RequestContext ctx)
        {
            log.Debug("=== Match Data ===");
            log.Debug(ctx.Request.GetBody().Subscribe(req => log.Debug(req)));
        }

        public void Shutdown()
        {
            server.Dispose();
        }
    }
}
