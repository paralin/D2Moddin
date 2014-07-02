using ClientCommon.Data;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace D2MPMaster.Model
{
    /// <summary>
    /// An addon stored in the database.
    /// </summary>
    public class Mod
    {
        public string Id { get; set; }
        public string name { get; set; }
        public string fullname { get; set; }
        public string version { get; set; }
        public string author { get; set; }
        public string authorimage { get; set; }
        public string thumbsmall { get; set; }
        public string thumbnail { get; set; }
        public string spreadimage { get; set; }
        public string website { get; set; }
        public string subtitle { get; set; }
        public string description { get; set; }
        public string[] features { get; set; }
        public bool playable { get; set; }
        //requirements
        public string spreadvideo { get; set; }
        /// <summary>
        /// Exclude client files from the bundle.
        /// </summary>
        public string[] exclude { get; set; }
        public string bundle { get; set; }
        public string fetch { get; set; }
        public string user { get; set; }
        public bool isPublic { get; set; }

        /// <summary>
        /// Use some other static hosting off of AWS
        /// </summary>
        public string staticClientBundle { get; set; }

        public ClientMod ToClientMod()
        {
            return new ClientMod()
                   {
                       name = name,
                       version = version
                   };
        }
    }
}
