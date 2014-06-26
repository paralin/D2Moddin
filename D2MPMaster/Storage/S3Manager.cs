using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using D2MPMaster.Model;
using D2MPMaster.Properties;

namespace D2MPMaster.Storage
{
    public class S3Manager
    {
        public AmazonS3Client Client;
        public S3Manager()
        {
            Client = new AmazonS3Client(Settings.Default.AWSKey, Settings.Default.AWSSecret, RegionEndpoint.USEast1);
        }

        public string GenerateBundleURL(string bundle)
        {
            return Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                Expires = DateTime.Now + TimeSpan.FromMinutes(15),
                BucketName = Settings.Default.Bucket,
                Key = bundle
            });
        }
        public string GenerateModURL(Mod mod)
        {
            if (mod.staticClientBundle != null)
                return mod.staticClientBundle;
            return GenerateBundleURL(mod.bundle);
        }
    }
}
