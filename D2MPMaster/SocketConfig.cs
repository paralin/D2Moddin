using System;
using XSockets.Core.Configuration;

namespace D2MPMaster
{
    public class SocketConfig : ConfigurationSetting
    {
        public SocketConfig() : base(new Uri("ws://ddp2.d2modd.in:4000"),new Uri("ws://127.0.0.1:4000"))
        {
        }
    }
}
