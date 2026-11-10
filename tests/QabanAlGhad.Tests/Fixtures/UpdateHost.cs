using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
internal static class UpdateHost
{
    static int Main(string[] args)
    {
#if FAILED_HOST
        return 7;
#else
        if (args.Length == 2 && args[0] == "--updated")
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".qaban-update.json");
            var journal = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(file));
            if (Convert.ToString(journal["Token"]) != args[1]) return 9;
            File.WriteAllText(Path.Combine(Convert.ToString(journal["Stage"]), "ready"), args[1]);
        }
        return 0;
#endif
    }
}
