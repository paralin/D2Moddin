using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace d2mpserver
{
    [Serializable]
    public enum ServerRegion : int
    {
        UNKNOWN=0,
        NA=1,
        EU=2,
        AUS=3,
        CN=4
    }
}
