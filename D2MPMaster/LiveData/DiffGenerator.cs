using System;
using System.Linq;
using System.Security.Permissions;
using D2MPMaster.Lobbies;
using Newtonsoft.Json.Linq;

namespace D2MPMaster.LiveData
{
    public static class DiffGenerator
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Generate a DiffSync JSON operation ($set update).
        /// </summary>
        /// <param name="source">The object that is changing.</param>
        /// <param name="fields">The updated fields.</param>
        /// <returns></returns>
        public static JObject Update<T>(this T source, string collection, string[] fields)
        {
            var obj = new JObject();
            obj["_o"] = "update";
            obj["_c"] = collection;
            obj["_id"] = (string)source.GetType().GetProperty("id").GetValue(source, null);
            foreach (var field in fields)
            {
                try
                {
                    var prop = source.GetType().GetProperty(field);
                    if (prop == null) continue;
                    var attr = (ExcludeFieldAttribute[])prop.GetCustomAttributes(typeof(ExcludeFieldAttribute), false);
                    if (attr.Length > 0)
                    {
                        var attrib = attr[0];
                        if (attrib.Collections.Contains(collection)) continue;
                    }
                    var ival = prop.GetValue(source, null);
                    if (ival == null) continue;
                    var val = Convert.ChangeType(ival, prop.PropertyType);
                    obj[prop.Name] = JToken.FromObject(val);
                }
                catch (Exception ex)
                {
                    log.Error("Can't generate UPDATE for field "+field+"", ex);
                }
            }
            return obj;
        }

        public static JObject Add<T>(this T source, string collection)
        {
            var obj = new JObject();
            foreach (var field in source.GetType().GetProperties())
            {
                var attr = (ExcludeFieldAttribute[])field.GetCustomAttributes(typeof(ExcludeFieldAttribute), false);
                if (attr.Length > 0)
                {
                    var attrib = attr[0];
                    if (attrib.Collections.Contains(collection)) continue;
                }
                var ival = field.GetValue(source, null);
                if (ival == null) continue;
                var val = Convert.ChangeType(ival, field.PropertyType);
                obj[field.Name] = JToken.FromObject(val);
            }
            obj["_o"] = "insert";
            obj["_c"] = collection;
            obj["_id"] = obj["id"].Value<string>();
            obj.Remove("id");
            return obj;
        }

        public static JObject Remove<T>(this T source, string collection)
        {
            var obj = new JObject();
            obj["_o"] = "remove";
            obj["_c"] = collection;
            obj["_id"] = (string)source.GetType().GetProperty("id").GetValue(source, null);
            return obj;
        }

        public static JObject RemoveAll(string collection)
        {
            var obj = new JObject();
            obj["_o"] = "remove";
            obj["_c"] = collection;
            //This will make a empty delete specifier {}
            return obj;
        }
    }
}
