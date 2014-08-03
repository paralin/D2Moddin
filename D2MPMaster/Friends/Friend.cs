// 
// Friend.cs
// Created by ilian000 on 2014-07-19
// Licenced under the Apache License, Version 2.0
//

namespace D2MPMaster.Friends
{
    public class Friend
    {
        /// <summary>
        /// Steam id of friend
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
        public FriendStatus status { get; set; }
        /// <summary>
        /// Modname of current lobby/game
        /// </summary>
        public string modname { get; set; }
    }
}
