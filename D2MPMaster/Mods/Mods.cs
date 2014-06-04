using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using D2MPMaster.Database;
using D2MPMaster.Model;
using MongoDB.Driver.Builders;

namespace D2MPMaster.Mods
{
    /// <summary>
    /// Helpers for querying the database
    /// </summary>
    public static class Mods
    {
        public static Dictionary<string, Mod> ModCache = new Dictionary<string, Mod>(); 
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

        public static void Cache()
        {
            var mods = Mongo.Mods.FindAllAs<Mod>();
            ModCache.Clear();
            foreach (var mod in mods)
            {
                ModCache[mod.Id] = mod;
            }
        }
    }
}
