using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D2MPMaster.Model
{
    public class Session
    {
        public string _id { get; set; }
        //JSON session data
        public string session { get; set; }
        public DateTime expires { get; set; }
    }
}
