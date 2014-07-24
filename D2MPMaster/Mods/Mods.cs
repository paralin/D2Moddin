using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using D2MPMaster.Browser;
using D2MPMaster.Client;
using D2MPMaster.Database;
using D2MPMaster.Lobbies;
using D2MPMaster.Model;
using D2MPMaster.Server;
using KellermanSoftware.CompareNetObjects;
using MongoDB.Driver.Builders;
using ServiceStack.Text;
using XSockets.Core.Common.Socket.Event.Arguments;
using XSockets.Core.XSocket.Helpers;

namespace D2MPMaster.Mods
{
    /// <summary>
    /// Helpers for querying the database
    /// </summary>
    public static class Mods
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static Dictionary<string, Mod> ModCache = new Dictionary<string, Mod>(); 
        private static ClientController Clients = new ClientController();
        private static ServerController Servers = new ServerController();
        private static BrowserController Browser = new BrowserController();

        private static Timer UpdateTimer;

        public static Mod ByName(string name)
        {
            return Mongo.Mods.FindOneAs<Mod>(Query.EQ("name", name));
        }

        public static Mod ByID(string id)
        {
            Mod mod = null;
            if (ModCache.ContainsKey(id))
                mod = ModCache[id];
            return mod;
        }

        public static void InitCache()
        {
            var mods = Mongo.Mods.FindAllAs<Mod>();
            ModCache.Clear();
            foreach (var mod in mods)
            {
                ModCache[mod.Id] = mod;
            }
        }

        public static void CheckForUpdates(object state, ElapsedEventArgs elapsedEventArgs)
        {
             
            var mods = Mongo.Mods.FindAllAs<Mod>();
            var updatedMods = new HashSet<Mod>();
            log.Info("Checking for updates to mods...");
            var logic = new CompareLogic(){Config = new ComparisonConfig(){Caching = false, MaxDifferences = 100}};
            var modIds = new HashSet<string>();
            bool cosmeticsChanged = false;
            foreach (var mod in mods)
            {
                modIds.Add(mod.Id);
                if (!ModCache.ContainsKey(mod.Id))
                {
                    ModCache.Add(mod.Id, mod);
                    updatedMods.Add(mod);
                    log.InfoFormat("Mod [{0}] added to database.", mod.fullname);
                    cosmeticsChanged = true;
                    continue;
                }
                var omod = ModCache[mod.Id];
                var diff = logic.Compare(omod, mod);
                if (diff.AreEqual) continue;
                log.InfoFormat("Mod [{0}] updated!", mod.fullname);
                foreach (var difference in diff.Differences)
                {
                    log.Info(difference.PropertyName+": "+difference.Object1Value+" => "+difference.Object2Value);
                }
                if(mod.version!=omod.version||mod.isPublic != omod.isPublic || mod.playable != omod.playable) {
                    updatedMods.Add(mod);
                }
                ModCache[mod.Id] = mod;
                cosmeticsChanged = true;
            }
            foreach (var mod in ModCache.Where(mod => !modIds.Contains(mod.Key)).ToArray())
            {
                updatedMods.Add(mod.Value);
                ModCache.Remove(mod.Value.Id);
                cosmeticsChanged = true;
                log.InfoFormat("Mod [{0}] deleted!", mod.Value.fullname);
            }
            if (cosmeticsChanged) Browser.AsyncSendToAll(BrowserController.UpdateMods(), resp => { });
            if (updatedMods.Count == 0) return;
            log.InfoFormat("[{0}] mods updated, re-initing all clients and servers.", updatedMods.Count);
            ServerAddons.Init(ModCache.Values);
            foreach (var mod in updatedMods)
            {
                LobbyManager.CloseAll(mod);
            }
            Clients.SendToAll(ClientController.UpdateMods());
            foreach(var server in Servers.Find(m=>m.Inited))
            {
                server.Inited = false;
                server.Send("updateMods|" + string.Join(",", updatedMods.Select(m => m.name)));
            }
        }

        public static void StartUpdateTimer()
        {
            UpdateTimer = new Timer(30000);
            UpdateTimer.Elapsed += CheckForUpdates;
            UpdateTimer.Start();
        }

        public static void StopUpdateTimer()
        {
            UpdateTimer.Stop();
            UpdateTimer.Close();
        }
    }
}
