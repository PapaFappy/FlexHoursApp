using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FlexHours
{
    public static class ChromeRefreshLink
    {
        public sealed class Registration
        {
            public string name { get; set; }
            public string[] allowed_origins { get; set; }
        }
        public static string FromManifest(string json, DateTime month)
        {
            if (json == null || json.Length > 64000) throw new InvalidDataException("Invalid Chrome helper registration.");
            var registration = new JavaScriptSerializer().Deserialize<Registration>(json);
            if (registration == null || registration.name != "com.flexhours.chrome_sync" || registration.allowed_origins == null
                || registration.allowed_origins.Length != 1 || !Regex.IsMatch(registration.allowed_origins[0] ?? "", @"^chrome-extension://[a-p]{32}/$"))
                throw new InvalidDataException("The existing Chrome extension registration could not be identified.");
            return registration.allowed_origins[0] + "popup.html#refresh=" + month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }
    }
}
