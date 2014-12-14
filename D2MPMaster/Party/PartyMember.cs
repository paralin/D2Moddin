// 
// PartyMember.cs
// Created by ilian000 on 2014-08-06
// Licenced under the Apache License, Version 2.0
//
      
using D2MPMaster.Model;
namespace D2MPMaster.Party
{
    public class PartyMember
    {
        /// <summary>
        /// Steam id of member
        /// </summary>
        public string id { get; set; }
        /// <summary>
        /// Profile name
        /// </summary>
        public string name { get; set; }
        /// <summary>
        /// Avatar image url
        /// </summary>
        public string avatar { get; set; }
        /// <summary>
        /// Current status of friend
        /// </summary>
        public static PartyMember FromUser(User u)
        {
            return new PartyMember
            {
                id = u.steam.steamid,
                name = u.profile.name,
                avatar = u.steam.avatar
            };
        }
    }
}
