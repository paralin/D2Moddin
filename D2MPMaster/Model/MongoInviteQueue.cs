using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2MPMaster.Model
{
    public class MongoInviteQueue
    {
        public int Id;
        public string steam_id;
        public bool invited;
        public string invite_key;
        public DateTime date_invited;
        public int __v;
    }
}
