namespace D2MPMaster.Model
{
    public class ClientRecord
    {
        public string Id { get; set; }
        //fof=0.35.14
        public string[] installedMods { get; set; }
        public int status { get; set; }
        public string ip { get; set; }
        public string uid { get; set; }
    }
}
