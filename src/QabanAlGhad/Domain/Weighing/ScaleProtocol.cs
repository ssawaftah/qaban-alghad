using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WeighbridgeBridge
{
    public static class Protocol
    {
        public static bool Parse(string raw, out decimal weight)
        {
            weight = 0;
            string value = raw.Trim('\x02', '\x03', '\n', '\r', ' ');
            var match = Regex.Match(value, @"^(?:ST\s*,\s*(?<mode>GS|NT)\s*,|1\s*,\s*ST\s*,)\s*(?<value>.+)$",
                                    RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;
            value = match.Groups["value"].Value;
            value = Regex.Replace(value, @"\s*,?\s*kg\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (match.Groups["mode"].Value.Equals("GS", StringComparison.OrdinalIgnoreCase) &&
                value.StartsWith("1,", StringComparison.Ordinal))
                value = value.Substring(2).Trim();
            return Regex.IsMatch(value, @"^[+-]?\s*[0-9]+(?:\.[0-9]+)?$") &&
                   decimal.TryParse(value.Replace(" ", ""),
                                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                                    CultureInfo.InvariantCulture, out weight);
        }
        public static string Payload(string company, decimal weight)
        {
            return "{\"company_id\":\"" + company + "\",\"weight_scale\":\"" +
                   weight.ToString(CultureInfo.InvariantCulture) + "\"}";
        }
    }
}
