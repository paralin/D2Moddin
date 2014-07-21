// 
// Friend.cs
// Created by ilian000 on 2014-07-19
// Licenced under the Apache License, Version 2.0
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using d2mpserver;

namespace D2MPMaster.Friends
{
    public class Friend
    {
        public string id { get; set; }
        public string steamid { get; set; }
        public string avatar { get; set; }
        public FriendStatus status { get; set; }
    }
}
