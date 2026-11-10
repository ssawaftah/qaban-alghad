using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WeighbridgeBridge
{
 public static class ResponseRules {
  public static bool IsHtml(string media,string body){return (media??"").IndexOf("html",StringComparison.OrdinalIgnoreCase)>=0||(body??"").TrimStart().StartsWith("<",StringComparison.Ordinal);}
  public static bool Matches(string body,string expected){
   if(string.IsNullOrWhiteSpace(expected))return false;
   string e=expected.Trim(),b=(body??"").Trim();
   if(e.StartsWith("{",StringComparison.Ordinal)){
    try{
     var serializer=new JavaScriptSerializer();
     var pattern=serializer.DeserializeObject(e) as Dictionary<string,object>;
     if(pattern==null||pattern.Count==0)return false;
     return ContainsPattern(serializer.DeserializeObject(b),pattern);
    }catch{return false;}
   }
   // Plain text acknowledgement: exact match, never a substring.
   return string.Equals(b,e,StringComparison.Ordinal);
  }
  public static bool KnownSuccess(string body){return Matches(body,"{\"success\":\"Weight scale updated successfully\"}");}
  public static bool ExplicitFailure(string body){
   try{var value=new JavaScriptSerializer().DeserializeObject(body) as Dictionary<string,object>;if(value==null)return false;
    object accepted;if(value.TryGetValue("accepted",out accepted)&&accepted is bool&&!(bool)accepted)return true;
    object success;if(value.TryGetValue("success",out success)&&success is bool&&!(bool)success)return true;
    object error;return value.TryGetValue("error",out error)&&error!=null&&!(error is bool&&!(bool)error)&&!string.IsNullOrWhiteSpace(Convert.ToString(error));
   }catch{return false;}
  }
  static bool ContainsPattern(object actual,object expected){
   var pattern=expected as Dictionary<string,object>;
   if(pattern!=null){
    var obj=actual as Dictionary<string,object>;if(obj==null)return false;
    foreach(var item in pattern){object value;if(!obj.TryGetValue(item.Key,out value)||!ContainsPattern(value,item.Value))return false;}
    return true;
   }
   var list=expected as object[];
   if(list!=null){var values=actual as object[];if(values==null||list.Length!=values.Length)return false;for(int i=0;i<list.Length;i++)if(!ContainsPattern(values[i],list[i]))return false;return true;}
   if(expected==null)return actual==null;
   if(actual==null)return false;
   // JSON numeric CLR representations can differ between int/long/decimal.
   if(IsNumber(actual)&&IsNumber(expected))return Convert.ToDecimal(actual)==Convert.ToDecimal(expected);
   return actual.GetType()==expected.GetType() && actual.Equals(expected);
  }
  static bool IsNumber(object value){return value is int||value is long||value is decimal||value is double||value is float;}
 }
}
