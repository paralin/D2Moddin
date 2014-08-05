using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudWatch.Model;
using Amazon.DataPipeline.Model;
using Query = MongoDB.Driver.Builders.Query;

namespace D2MPMaster.Model
{
    /// <summary>
    /// A user stored in the database, auth through Meteor.
    /// </summary>
    public class User
    {
        public int __v { get; set; }
        public string Id { get; set; }
        public string[] authItems { get; set; }
        public Profile profile { get; set; }
        public SteamService steam { get; set; }

        public void CheckAndInit()
        {
            if (profile.metrics == null)
            {
                profile.metrics = new Dictionary<string, ModMetric>();
                var matches = Database.Mongo.Results.FindAs<MatchData>(Query.And(Query.EQ("ranked", true), Query.EQ("steamids", steam.steamid)));
                foreach (var match in matches)
                {
                    if (!profile.metrics.ContainsKey(match.mod))
                    {
                        profile.metrics[match.mod] = new ModMetric();
                    }
                    var goodguys = match.teams[0].players.Any(m => m.steam_id == steam.steamid);
                    if ((goodguys && match.good_guys_win) || (!goodguys && !match.good_guys_win))
                        profile.metrics[match.mod].wins++;
                    else
                    {
                        profile.metrics[match.mod].losses++;
                    }
                }
            }
            if (!profile.metrics.ContainsKey("reflex"))
            {
                profile.metrics["reflex"] = new ModMetric();
            }
            Database.Mongo.Users.Save(this);
        }
    }

    public class SteamService
    {
        public string steamid { get; set; }
        public int communityvisibilitystate { get; set; }
        public int profilestate { get; set; }
        public string personaname { get; set; }
        public long lastlogoff { get; set; }
        public int commentpermission { get; set; }
        public string profileurl { get; set; }
        public string avatar { get; set; }
        public string avatarmedium { get; set; }
        public string avatarfull { get; set; }
        public int personastate { get; set; }
        public string realname { get; set; }
        public string primaryclanid { get; set; }
        public long timecreated { get; set; }
        public int personastateflags { get; set; }
        public string gameextrainfo { get; set; }
        public string gameid { get; set; }
        public string loccountrycode { get; set; }
        public string locstatecode { get; set; }
        public int loccityid { get; set; }
    }


    public class LoginToken
    {
        public DateTime when { get; set; }
        public string hashedToken { get; set; }
    }

    public class Profile
    {
        public string name { get; set; }
        /// <summary>
        /// Null on default, if they want a special icon
        /// </summary>
        public string playerIcon { get; set; }

        /// <summary>
        /// Contribution description (probably null)
        /// </summary>
        public string contribDesc { get; set; }

        public Dictionary<string, int> mmr { get; set; }
        public DateTime PreventMMUntil { get; set; }
        public Dictionary<string, ModMetric> metrics { get; set; } 
    }

    public class ModMetric
    {
        public int wins { get; set; }
        public int losses { get; set; }
    }
}
