using System;
using System.Text.RegularExpressions;

namespace WeighbridgeBridge
{
    public static class ReadingDescription
    {
        public static string Rejection(string raw)
        {
            raw = raw ?? "";
            if (raw.TrimStart(' ', '\x02').StartsWith("US", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(raw, @"^\s*1\s*,\s*US\s*,", RegexOptions.IgnoreCase))
                return "قراءة غير مستقرة";
            if (raw == "[oversized serial frame]")
                return "إطار أطول من الحد المسموح";
            if (Regex.IsMatch(raw.TrimEnd('\x03', '\r', '\n', ' '), @"(?:[0-9]|,)\s*(lb|lbs|oz|g|t)\s*$",
                              RegexOptions.IgnoreCase))
                return "وحدة غير مدعومة";
            return "قراءة غير صالحة";
        }
    }
}
