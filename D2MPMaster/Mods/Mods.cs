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
        public static Mod ByName(string name)
        {
            return Mongo.Mods.FindOneAs<Mod>(Query.EQ("name", name));
        }

        public static Mod ByID(string id)
        {
            return Mongo.Mods.FindOneAs<Mod>(Query.EQ("_id", id));
        }
    }
}
