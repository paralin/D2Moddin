using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using D2MPMaster.Database;
using log4net.Config;
using NUnit.Framework;

namespace D2MPMaster.Tests
{
    [TestFixture]
    class Tests
    {
        [Test(Description = "Does the program launch correctly?")]
        public void ProgramStart()
        {
            var testThread = new Task(() => Program.Main(null));
            testThread.Start();
            Thread.Sleep(2000);
            Assert.NotNull(Program.Browser);
            Assert.NotNull(Program.Server);
            Assert.NotNull(Program.LobbyManager);
            Assert.NotNull(Program.LobbyManager.PublicLobbies);
            Assert.NotNull(Mongo.Database);
            Program.shutdown = true;
        }

        [Test(Description = "Try adding and removing lobbies, reset the collection. Should automatically update in browser.")]
        public void RandomOps()
        {
            var testThread = new Task(() => Program.Main(null));
            testThread.Start();
            Thread.Sleep(500);
            Assert.NotNull(Program.LobbyManager);
            int count = 60;
            while (count > 0)
            {
                count--;

                Thread.Sleep(1000);
            }
        }
    }
}
