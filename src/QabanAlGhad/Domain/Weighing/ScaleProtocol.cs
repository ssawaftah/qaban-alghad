using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace WeighbridgeBridge
{
 public static class Protocol {
  public static bool Parse(string raw, out decimal weight) {
   weight=0; string s=raw.Trim('\x02','\x03','\n','\r',' ');
   s=Regex.Replace(s,@"^ST\s*,\s*(GS|NT)\s*,",m=>"ST,"+m.Groups[1].Value.ToUpperInvariant()+",",RegexOptions.IgnoreCase);
   if(s.StartsWith("ST,GS,")) {
    s=s.Substring(6);
    if(s.StartsWith("1,") && s.IndexOf(',')>=0) s=s.Substring(2);
   } else if(s.StartsWith("1,ST,")) s=s.Substring(5).Split(new char[]{','})[0];
   else if(s.StartsWith("ST,NT,")) s=s.Substring(6).Split(new char[]{','})[0];
   else return false;
   s=Regex.Replace(s,@"\s*,?\s*kg\s*$","",RegexOptions.IgnoreCase).Trim();
   return Regex.IsMatch(s,@"^[+-]?\s*\d+(?:\.\d+)?$") &&
     decimal.TryParse(s.Replace(" ",""),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out weight);
  }
  public static string Payload(string company, decimal weight) {
   return "{\"company_id\":\""+company+"\",\"weight_scale\":\""+weight.ToString(CultureInfo.InvariantCulture)+"\"}";
  }
 }
}
