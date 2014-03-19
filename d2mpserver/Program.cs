using System;
using d2mpserver.Properties;
using log4net.Config;

namespace d2mpserver
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            log.Info("D2MP server starting...");

            log.Debug("Server IP: "+Settings.Default.serverIP);
            log.Debug("Connect 'password': "+Settings.Default.connectPassword);

            ServerManager manager = new ServerManager();
            if (!manager.LocateServerEXE())
            {
                log.Fatal("Can't find scrds.exe");
                return;
            }

            ServerConnection connection = new ServerConnection(manager);
            if(!connection.Connect())
            {
                log.Fatal("Can't connect to the server!");
                return;
            }

            connection.StartServerThread();

            bool shutdown = false;
            bool controlling = false;
            Server controlled = null;
            string line;
            while(!shutdown)
            {
              line = Console.ReadLine();
              if(controlling){
                if(line == "stopcontrolling" || controlled.shutdown){
                  log.Debug("Stopped controlling server.");
                  controlled = null;
                  controlling = false;
                  if(line == "stopcontrolling")
                  {
                    continue;
                  }
                }
                else{
                  controlled.ToSTDIN(line);
                  continue;
                }
              }

              var command = line.Split(' ');
              try{
                switch(command[0])
                {
                  case "exit":
                    shutdown = true;
                    log.Debug("Shutdown from console...");
                    break;
                  case "control":
                    int id = int.Parse(command[1]);
                    var serv = manager.GetServer(id);
                    if(serv == null){
                      log.Error("Server ID not known: "+id);
                      break;
                    }
                    controlled = serv;
                    controlling = true;
                    log.Debug("Console started controlling: "+id);
                    break;
                  default:
                    log.Error("Unknown command: "+line);
                    break;
                }
              }
              catch(Exception ex){
                log.Error("Problem processing command: "+ex);
              }
            }
            connection.Shutdown();
            manager.ShutdownAllServers();
        }
    }
}
