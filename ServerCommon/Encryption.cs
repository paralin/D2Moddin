// 
// Encryption.cs
// Created by ilian000 on 2014-06-25
// Licenced under the Apache License, Version 2.0
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ServerCommon
{
    public class RSAEncryption
    {
        private RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
        public string rsaPath;

        /// <summary>
        /// Constructs Encryption object: reads private key from file or creates a new one if it does not exist
        /// </summary>
        /// <param name="privKeyPath">Path of the private key to read or create</param>
        public RSAEncryption(string privKeyPath)
        {
            rsaPath = privKeyPath;
            if (File.Exists(rsaPath))
            {
                ReadKey();
            }
            else
            {
                AssignNewKey();
            }
        }
        /// <summary>
        /// Constructs Encryption object: reads public key from given XML string
        /// </summary>
        /// <param name="publicKeyXml">XML string containing public key</param>
        /// <param name="onlyPublic">Overload constructor parameter</param>
        public RSAEncryption(string publicKeyXml, bool onlyPublic)
        {
            ReadKey(publicKeyXml);
        }
        /// <summary>
        /// Creates new private and public key and saves it
        /// </summary>
        private void AssignNewKey()
        {
            rsa = new RSACryptoServiceProvider(2048);

            string publicPrivateKeyXML = rsa.ToXmlString(true);
            string publicOnlyKeyXML = rsa.ToXmlString(false);
            File.WriteAllText(rsaPath, publicPrivateKeyXML);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(rsaPath), "PublicKey.xml"), publicOnlyKeyXML);
        }
        /// <summary>
        /// Read private key xml from path to decrypt data
        /// </summary>
        private void ReadKey()
        {
            rsa.FromXmlString(File.ReadAllText(rsaPath));
        }
        /// <summary>
        /// Read public key xml from given string to encrypt data for later
        /// </summary>
        /// <param name="pubXml">Xml containing the public key</param>
        private void ReadKey(string pubXml)
        {
            rsa.FromXmlString(pubXml);
        }
        /// <summary>
        /// Encrypts data using the public key using Base64
        /// </summary>
        /// <param name="data">The data to encrypt</param>
        /// <returns>A base64 encoded string containing the encrypted data</returns>
        public string encrypt(string data)
        {
            return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(data), true));
        }
        /// <summary>
        /// Decrypts data using the private key
        /// </summary>
        /// <param name="encryptedData">A base64 encoded string containing the encrypted data</param>
        /// <returns>The decrypted data</returns>
        public string decrypt(string encryptedData)
        {
            if (!rsa.PublicOnly)
            {
                return Encoding.UTF8.GetString(rsa.Decrypt(Convert.FromBase64String(encryptedData), true));
            }
            return null;
        }
        /// <summary>
        /// Get the public key in XML format
        /// </summary>
        /// <returns>Public key in XML format</returns>
        public string getPublicKey()
        {
            return rsa.ToXmlString(false);
        }
    }
    public class AESEncryption
    {
        private static const byte[] IV = new Byte[0];
        public static EncryptModel encryptString(string data, RSAEncryption rsa)
        {
            RijndaelManaged Crypto = new RijndaelManaged();
            Crypto.IV = IV;
            var encryptor = Crypto.CreateEncryptor(Crypto.Key, Crypto.IV);
            var msEncrypt = new MemoryStream();
            using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
            using (var swEncrypt = new StreamWriter(csEncrypt))
            {
                swEncrypt.Write(data);
            }

            return new EncryptModel() { cipherData = Convert.ToBase64String(msEncrypt.ToArray()), encryptedSymKey = rsa.encrypt(Convert.ToBase64String(Crypto.Key)) };
        }
        public static string decryptString(EncryptModel m, RSAEncryption rsa)
        {
            string data;
            RijndaelManaged Crypto = new RijndaelManaged();
            Crypto.Key = Convert.FromBase64String(rsa.decrypt(m.encryptedSymKey));
            var decryptor = Crypto.CreateDecryptor(Crypto.Key, Crypto.IV);
            using (var msDecrypt = new MemoryStream(Convert.FromBase64String(m.cipherData)))
            {
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (var srDecrypt = new StreamReader(csDecrypt))
                    {
                        data = srDecrypt.ReadToEnd();
                    }
                }
            }
            return data;
        }
    }
    public class Encryption
    {
        private RSAEncryption rsa;
        public Encryption(string privKeyPath)
        {
            rsa = new RSAEncryption(privKeyPath);
        }
        public Encryption(string publicKeyXml, bool onlyPublic)
        {
            rsa = new RSAEncryption(publicKeyXml, onlyPublic);
        }
        public EncryptModel encrypt(string data)
        {
            return AESEncryption.encryptString(data, rsa);
        }
        public string decrypt(EncryptModel m){
            return AESEncryption.decryptString(m, rsa);
        }
        public string getPublicKey()
        {
            return rsa.getPublicKey();
        }
    }
    public class EncryptModel
    {
        /// <summary>
        /// The AES-ciphered data encoded in Base64
        /// </summary>
        public string cipherData { get; set; }
        /// <summary>
        /// The RSA-encrypted symetric key encoded in Base64 to encrypt and decrypt the cipherData
        /// The key itself is also encoded in Base64
        /// </summary>
        public string encryptedSymKey { get; set; }
    }
}
