using System.Configuration;
using System.Linq;
using Amazon.DataPipeline.Model;
using D2MPMaster.Properties;
using MongoDB.Bson;
using MongoDB.Driver;
using Query = MongoDB.Driver.Builders.Query;

namespace D2MPMaster.Database
{
    public class Mongo
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static MongoClient Client = null;
        public static MongoServer Server;
        public static MongoDatabase Database;

        public static MongoCollection Users;
        public static MongoCollection Mods;
        public static MongoCollection Sessions;
        public static MongoCollection Results;
        public static MongoCollection InviteQueue;
        public static MongoCollection InviteKeys;

        public static void Setup()
        {
            if (Client != null)
            {
                log.Error("Tried to create a second instance of Mongo.");
                return;
            }
#if DEBUG||DEV
            var connectString = Settings.Default.MongoDevURL;
#else
            var connectString = Settings.Default.MongoURL;
#endif
            Client = new MongoClient(connectString);
            Server = Client.GetServer();
#if DEBUG||DEV
            Database = Server.GetDatabase(Settings.Default.MongoDevDB);
#else
            Database = Server.GetDatabase(Settings.Default.MongoDB);
#endif
            Users = Database.GetCollection("users");
            Mods = Database.GetCollection("mods");
            Sessions = Database.GetCollection("sessions");
            Results = Database.GetCollection("matchResults");
            InviteQueue = Database.GetCollection("inviteQueue");
            InviteKeys = Database.GetCollection("inviteKeys");
        }

        public static void UpdateOldMatchResults()
        {
            var matches = Results.FindAs<Model.MatchData>(Query.NotExists("steamids"));
            matches.SetFlags(QueryFlags.NoCursorTimeout);
            var count = matches.Count();
            log.DebugFormat("Updating {0} old match results...", count);
            int i=0;

            foreach (var match in matches)
            {
                i++;
                if (i%20 == 0)
                    log.DebugFormat("Current: {0} of {1}", i, count);
                match.ranked = false;
                match.steamids =
                    match.teams[0].players.Select(x => x.steam_id)
                        .Union(match.teams[1].players.Select(y => y.steam_id))
                        .ToArray();
                foreach (var player in match.teams.SelectMany(team => team.players))
                {
                    player.ConvertData();
                }
                Results.Save(match);
            }
            log.InfoFormat("Updated [{0}] old matches.", count);
        }
    }
}