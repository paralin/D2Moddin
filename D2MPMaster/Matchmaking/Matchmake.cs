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
        public string id { get; set; }
        public User[] users { get; set; }
        // Mod ids
        public string[] mods { get; set; }
        public int rating { get; set; }
        public int matchTries { get; set; }

        public void MoveUsers(User[] source, User[] destination)
        {
            foreach (var u in source)
            {
                for (var i = 0; i < destination.Length; i++)
                {
                    if (destination[i] != null) continue;
                    destination[i] = u;
                    return;
                }
            }
        }
    }
}
