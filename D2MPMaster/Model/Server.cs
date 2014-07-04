// 
// Server.cs
// Created by ilian000 on 2014-06-25
// Licenced under the Apache License, Version 2.0
//
      
namespace D2MPMaster.Model
{
    public class Server
    {
        /// <summary>
        /// The hardware Guid of the server stored in the database <see cref="d2mpserver.HardwareGuid"/>
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// The public RSA key of the server stored in the database<see cref="ServerCommon.Encryption"/>
        /// </summary>
        public string pubKey { get; set; }
    }
}
