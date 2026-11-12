// Field names and namespace remain compatible with installed settings.json files.

using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WeighbridgeBridge
{
 public sealed class Settings {
  public string Port="COM6",Parity="None",Endpoint="https://app.circlessoft.com:9083/api/store_weight_scale",Company="1",ResponseMarker="";
  public string ReturnsEndpoint="https://qaban-113.pages.dev/api/scale/weight",ReturnsTokenProtected="",PrimaryTokenProtected="";
  public bool ReturnsEnabled=false;
  public int Baud=9600,DataBits=8,Interval=1000,Freshness=3000;
 }
 public static class SettingsStore {
  public static Settings Read(string path){
   var info=new FileInfo(path);
   if(info.Length>65536)throw new InvalidDataException("Settings too large.");
   var serializer=new JavaScriptSerializer{MaxJsonLength=65536};
   string json=File.ReadAllText(path);
   var fields=serializer.DeserializeObject(json) as Dictionary<string,object>;
   if(fields==null||!fields.ContainsKey("Port")||!fields.ContainsKey("Endpoint")||!fields.ContainsKey("Company"))
    throw new InvalidDataException("Incomplete settings.");
   var settings=serializer.Deserialize<Settings>(json);
   Validate(settings);
   return settings;
  }

  public static void Validate(Settings settings){
   if(!Regex.IsMatch(settings.Port??"",@"^COM[0-9]+$",RegexOptions.IgnoreCase))
    throw new InvalidDataException("أدخل منفذًا مثل COM6.");
   if(!Regex.IsMatch(settings.Company??"",@"^[0-9]+$"))
    throw new InvalidDataException("رقم الشركة يجب أن يكون أرقامًا.");
   Uri uri;
   if(!Uri.TryCreate(settings.Endpoint,UriKind.Absolute,out uri)||uri.Scheme!="https"||
      uri.UserInfo!=""||uri.Fragment!=""||uri.Query!="")
    throw new InvalidDataException("عنوان الإرسال يجب أن يكون HTTPS دون كلمة مرور أو معاملات إضافية.");
   Parity parity;
   if(settings.Baud<300||settings.Baud>115200||settings.DataBits<5||settings.DataBits>8||
      settings.Freshness<1000||settings.Freshness>30000||
      !Enum.TryParse(settings.Parity,out parity)||!Enum.IsDefined(typeof(Parity),parity)||parity.ToString()!=settings.Parity)
    throw new InvalidDataException("إعدادات منفذ الميزان غير صالحة.");
  }

  public static void Save(string path,Settings settings){
   Validate(settings);
   Directory.CreateDirectory(Path.GetDirectoryName(path));
   string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    var bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(settings));
    if(bytes.Length>65536)throw new InvalidDataException("Settings too large.");
    using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     output.Write(bytes,0,bytes.Length);
     output.Flush(true);
    }
    if(!File.Exists(path)){File.Move(temporary,path);return;}
    bool keepBackup;
    try{Read(path);keepBackup=true;}
    catch(ArgumentException){keepBackup=false;}
    catch(InvalidOperationException){keepBackup=false;}
    catch(InvalidDataException){keepBackup=false;}
    File.Replace(temporary,path,keepBackup?path+".bak":null);
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
