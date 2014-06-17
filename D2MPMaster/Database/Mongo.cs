using System.Configuration;
using D2MPMaster.Properties;
using MongoDB.Bson;
using MongoDB.Driver;

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

        public static void Setup()
        {
            if (Client != null)
            {
                log.Error("Tried to create a second instance of Mongo.");
                return;
            }
            var connectString = Settings.Default.MongoURL;
            Client = new MongoClient(connectString);
            Server = Client.GetServer();
            Database = Server.GetDatabase(Settings.Default.MongoDB);
            Users = Database.GetCollection("users");
            Mods = Database.GetCollection("mods");
            Sessions = Database.GetCollection("sessions");
            Results = Database.GetCollection("matchResults");
        }
    }
}
