using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace D2MPMaster.Model
{
    /// <summary>
    /// A user stored in the database, auth through Meteor.
    /// </summary>
    public class User
    {
        public string Id { get; set; }
        public DateTime createdAt { get; set; }
        public Profile profile { get; set; }
        public Services services { get; set; }
        public Status status { get; set; }
        public string[] authItems { get; set; }
    }

    public class Status
    {
        public DateTime lastLogin { get; set; }
        public bool online { get; set; }
    }

    public class Services
    {
        public ResumeService resume { get; set; }
        public SteamService steam { get; set; }
    }

    public class SteamService
    {
        [BsonElement("id")]
        public string steamid { get; set; }
        public string username { get; set; }
        public Avatar avatar { get; set; }
    }

    public class Avatar
    {
        public string small { get; set; }
        public string medium { get; set; }
        public string full { get; set; }
    }

    public class ResumeService
    {
        public LoginToken[] loginTokens;
    }

    public class LoginToken
    {
        public DateTime when { get; set; }
        public string hashedToken { get; set; }
    }

    public class Profile
    {
        public string name { get; set; }
    }
}
