// 
// Matchmake.cs
// Created by ilian000 on 2014-06-19
// Licenced under the Apache License, Version 2.0
//

using D2MPMaster.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2MPMaster.Matchmaking
{
    /// <summary>
    /// A matchmake instance. Will be merged with another matchmake instance with a similar matchmaking rating to a lobby
    /// </summary>
    public class Matchmake
    {
        /// <summary>
        ///  Margin increases by this number every time doMatchmake executes.
        /// </summary>
        private const int RatingMargin = 10;

        public string id { get; set; }
        public User[] Users { get; set; }
        // Mod ids
        public string[] Mods { get; set; }
        public Dictionary<string, int> Ratings { get; set; }
        public int TryCount { get; set; }
        public bool Ignore { get; set; }

        /// <summary>
        /// Merge pMatch into this
        /// </summary>
        public void MergeMatches(Matchmake pMatch)
        {
            this.Users = this.Users.Union(pMatch.Users).ToArray();
            this.Mods = this.GetMatchedMods(pMatch);

            this.UpdateRating();
        }
        
        /// <summary>
        /// Check if the two matches match the criteria
        /// </summary>
        /// <param name="pTeam">Is this a team MM?</param>
        public bool IsMatch(Matchmake pMatch, bool pTeam = false)
        {
            bool result = false;

            //not the same match
            if (this != pMatch &&
                (pTeam || this.Users.Length <= (5 - pMatch.Users.Length))) //there is room for everybody
            {
                result = this.GetMatchedMods(pMatch).Length > 0;
            }

            return result;
        }

        /// <summary>
        /// Get The matched mods
        /// </summary>
        public string[] GetMatchedMods(Matchmake pMatch)
        {
            //get intersection mods only
            return this.Mods.Intersect(pMatch.Mods)
                .Where(modName => Math.Abs(this.Ratings[modName] - pMatch.Ratings[modName]) < this.TryCount * RatingMargin)//and it has to fall in range
                .ToArray();
        }

        /// <summary>
        /// Update the current MM rating
        /// </summary>
        public void UpdateRating()
        {
            this.Ratings.Clear();
            foreach (var mod in this.Mods)
            {
                this.Ratings.Add(mod, (int)this.Users.Average(user => user.profile.mmr[mod]));
            }
        }
    }
}
