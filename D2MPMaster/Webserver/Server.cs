using System;
using System.IO;
using System.Net;
using Griffin.Networking.Protocol.Http.Services.BodyDecoders;
using Griffin.WebServer;
using Griffin.WebServer.Modules;

namespace D2MPMaster.Webserver
{
    class WebServer
    {
        ModuleManager manager = new ModuleManager();

        ClientVerModule clientVer = new ClientVerModule();
        private HttpServer server;
        public WebServer()
        {
            manager.Add(new BodyDecodingModule(new UrlFormattedDecoder()));
            manager.Add(clientVer);
            server = new HttpServer(manager);
        }

        public void Start()
        {
            server.Start(IPAddress.Any, 80);
        }

        public void Stop()
        {
            server.Stop();
        }
    }

    internal class ClientVerModule : IWorkerModule
    {
        public void BeginRequest(IHttpContext context)
        {
         
        }

        public void EndRequest(IHttpContext context)
        {
            
        }

        public void HandleRequestAsync(IHttpContext context, Action<IAsyncModuleResult> callback)
        {
            // Since this module only supports sync
            callback(new AsyncModuleResult(context, HandleRequest(context)));
        }

        public ModuleResult HandleRequest(IHttpContext context)
        {
            context.Response.Body = new MemoryStream();
            var writer = new StreamWriter(context.Response.Body);
            writer.Write("version:{0}|https://s3-us-west-2.amazonaws.com/d2mpclient/{0}.zip", ClientCommon.Version.ClientVersion);
            context.Response.Body.Position = 0;
            return ModuleResult.Continue;
        }
    }
}
