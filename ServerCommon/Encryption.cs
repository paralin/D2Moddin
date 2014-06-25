using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ServerCommon
{
    public class Encryption
    {
        private RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        public string rsaPath;// = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), Properties.Settings.Default.keyName);

        public Encryption(string path)
        {
            rsaPath = path;
            if (File.Exists(rsaPath))
            {
                ReadKey();
            }
            else
            {
                AssignNewKey();
            }
        }

        private void AssignNewKey()
        {
            rsa = new RSACryptoServiceProvider(2048);
            
            string publicPrivateKeyXML = rsa.ToXmlString(true);
            //string publicOnlyKeyXML = rsa.ToXmlString(false);
            File.WriteAllText(rsaPath, publicPrivateKeyXML);
        }

        public void ReadKey()
        {
            rsa.FromXmlString(File.ReadAllText(rsaPath));
        }
    }
}
