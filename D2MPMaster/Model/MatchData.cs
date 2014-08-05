using System.Collections.Generic;
using Amazon.DataPipeline.Model;
using D2MPMaster.Database;
using D2MPMaster.LiveData;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using Query = MongoDB.Driver.Builders.Query;

namespace D2MPMaster.Model
{
    public class MatchData
    {
        public ObjectId _id { get; set; }
		public string mod;
        public bool ranked;
        public bool automatic_surrender;
        public long date;
        public int duration;
        public int first_blood_time;
        public bool good_guys_win;
        public bool mass_disconnect;
        public string match_id;
        public int[] num_players;
        public string server_addr;
        public int server_version;
        public TeamRecord[] teams;
        public string[] steamids;

        public MatchData ConvertData()
        {
            foreach(var team in teams)
            {
                foreach (var player in team.players)
                {
                    player.ConvertData();
                }
            }
            return this;
        }
    }

    public class TeamRecord
    {
        public PlayerRecord[] players;
    }

    public class PlayerRecord
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public int assists;
        public int account_id;
        public string steam_id;
        public string user_id;
        public string avatar;
        public string name;
        public int claimed_denies;
        public int claimed_farm_gold;
        public int deaths;
        public int denies;
        public int gold;
        public int gold_per_min;
        public int hero_damage;
        public int hero_healing;
        public int hero_id;
        public int[] items;
        public int kills;
        public int last_hits;
        public int leaver_status;
        public int level;
        public int tower_damage;
        public int xp_per_minute;

        public User ConvertData()
        {
            //Detect steamid from accountid
            steam_id = account_id.ToSteamID64();
            var user = Mongo.Users.FindOneAs<User>(Query.EQ("steam.steamid", steam_id));
            if (user != null)
            {
                user_id = user.Id;
                avatar = user.steam.avatarfull;
                name = user.profile.name;
            }
            else
            {
                log.Error("Can't find user for steam ID: " + steam_id + " account ID: " + account_id);
            }
            return user;
        }
    }
}
/* Game Enclassult
{ "additional_msgs" : [  ],
  "automatic_surrender" : false,
  "barracks_status" : [ 63,
      55
    ],
  "cluster" : 0,
  "date" : 1403009933,
  "duration" : 1297,
  "fantasy_stats" : [  ],
  "first_blood_time" : 432,
  "game_balance" : 0.048237118870019913,
  "good_guys_win" : true,
  "mass_disconnect" : false,
  "match_id" : "64746e3d-c5d5-4b0a-8437-ecab3b746fe5",
  "num_players" : [ 1,
      1
    ],
  "picks_bans" : [  ],
  "player_strange_count_adjustments" : [  ],
  "server_addr" : "46.4.45.98:20001",
  "server_version" : 978,
  "status" : "completed",
  "teams" : [ { "players" : [ { "ability_upgrades" : [ { "ability" : 5412,
                    "time" : 135
                  }
                ],
              "account_id" : 76561198096341135,
              "additional_units_inventory" : [ { "items" : [ 181,
                        50,
                        182,
                        25,
                        25,
                        55
                      ],
                    "unit_name" : "spirit_bear"
                  } ],
              "assists" : 0,
              "claimed_denies" : 1,
              "claimed_farm_gold" : 6808,
              "claimed_misses" : 35,
              "deaths" : 3,
              "denies" : 10,
              "gold" : 2190,
              "gold_per_min" : 449,
              "gold_spent" : 7265,
              "hero_damage" : 8147,
              "hero_healing" : 0,
              "hero_id" : 80,
              "items" : [ 214,
                  36,
                  0,
                  46,
                  182,
                  0
                ],
              "kills" : 5,
              "last_hits" : 94,
              "leaver_status" : 0,
              "level" : 16,
              "misses" : 31,
              "party_id" : 0,
              "scaled_assists" : 0.0,
              "scaled_deaths" : 3.0,
              "scaled_kills" : 4.4995193481445313,
              "support_ability_value" : 0,
              "support_gold" : 0,
              "time_last_seen" : 0,
              "tower_damage" : 2874,
              "xp_per_minute" : 700
            } ] },
      { "players" : [ { "ability_upgrades" : [ { "ability" : 5392,
                    "time" : 142
                  },
                  { "ability" : 5393,
                    "time" : 266
                  },
                  { "ability" : 5392,
                    "time" : 315
                  },
                  { "ability" : 5393,
                    "time" : 394
                  },
                  { "ability" : 5392,
                    "time" : 473
                  },
                  { "ability" : 5394,
                    "time" : 524
                  },
                  { "ability" : 5392,
                    "time" : 609
                  },
                  { "ability" : 5393,
                    "time" : 747
                  },
                  { "ability" : 5393,
                    "time" : 876
                  },
                  { "ability" : 5391,
                    "time" : 1049
                  },
                  { "ability" : 5394,
                    "time" : 1120
                  },
                  { "ability" : 5391,
                    "time" : 1275
                  },
                  { "ability" : 5391,
                    "time" : 1426
                  }
                ],
              "account_id" : 76561198055761521,
              "additional_units_inventory" : [  ],
              "assists" : 0,
              "claimed_denies" : 3,
              "claimed_farm_gold" : 5342,
              "claimed_misses" : 25,
              "deaths" : 5,
              "denies" : 4,
              "gold" : 208,
              "gold_per_min" : 265,
              "gold_spent" : 4750,
              "hero_damage" : 2977,
              "hero_healing" : 0,
              "hero_id" : 76,
              "items" : [ 63,
                  41,
                  77,
                  19,
                  19,
                  0
                ],
              "kills" : 3,
              "last_hits" : 54,
              "leaver_status" : 0,
              "level" : 13,
              "misses" : 28,
              "party_id" : 0,
              "scaled_assists" : 0.0,
              "scaled_deaths" : 5.0,
              "scaled_kills" : 3.5004806518554687,
              "support_ability_value" : 2800,
              "support_gold" : 0,
              "time_last_seen" : 0,
              "tower_damage" : 206,
              "xp_per_minute" : 430
            } ] }
    ],
  "tower_status" : [ 2047,
      1991
    ]
}
*/