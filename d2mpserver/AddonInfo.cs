using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace d2mpserver
{
    public static class AddonInfo
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public static string DetectVersion(string dir)
        {
            var modName = Path.GetFileName(dir);
            var infoPath = Path.Combine(dir, "addoninfo.txt");
            string versionFile = "";
            if (File.Exists(infoPath))
            {
                versionFile = File.ReadAllText(infoPath);
            }
            var match = Regex.Match(versionFile, @"(addonversion)(\s+)(\d+\.)?(\d+\.)?(\d+\.)?(\*|\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string version = match.Groups.Cast<Group>().ToList().Skip(3).Aggregate("", (current, part) => current + part.Value);
                log.Debug(modName + "=" + version);
                return modName + "=" + version;
            }
            else
            {
                log.Error("No version info for addon: " + modName);
                return modName + "=?";
            }
        }
    }
}
