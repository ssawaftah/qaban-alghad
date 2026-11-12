using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace WeighbridgeBridge
{
 public sealed partial class MainForm
 {
  static void ConfigureNumber(NumericUpDown n,int min,int max){n.Minimum=min;n.Maximum=max;n.Width=100;}
  static FlowLayoutPanel Row(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};foreach(var c in controls)p.Controls.Add(c);return p;}
  void AddField(string text,Control c){int r=fields.RowCount++;fields.RowStyles.Add(new RowStyle(SizeType.Absolute,34));fields.Controls.Add(new Label{Text=text,AutoSize=true,Anchor=AnchorStyles.Right},0,r);c.Dock=DockStyle.Fill;fields.Controls.Add(c,1,r);}
  bool settingsNeedRepair;
  string SettingsPath {get{return Path.Combine(dataDir,"settings.json");}}
  Settings LoadSettings(){
   string path=SettingsPath;
   bool missing=false;
   try{return SettingsStore.Read(path);}
   catch(FileNotFoundException){missing=true;}
   catch(DirectoryNotFoundException){missing=true;}
   catch(Exception){}
   try{
    var recovered=SettingsStore.Read(path+".bak");
    WriteLog("تم استعادة آخر إعدادات صالحة.");
    return recovered;
   }catch(FileNotFoundException){if(missing)return new Settings();}
   catch(DirectoryNotFoundException){if(missing)return new Settings();}
   catch(Exception){}
   settingsNeedRepair=true;
   WriteLog("تعذر قراءة الإعدادات والنسخة الاحتياطية. احفظ إعدادات صحيحة قبل التشغيل.");
   return new Settings{Endpoint=""};
  }
  void Apply(Settings c){port.Text=c.Port;endpoint.Text=c.Endpoint;company.Text=c.Company;marker.Text=c.ResponseMarker;baud.Value=Clamp(c.Baud,300,115200);bits.Value=Clamp(c.DataBits,5,8);interval.Value=1000;fresh.Value=Clamp(c.Freshness,1000,30000);returnsEndpoint.Text=c.ReturnsEndpoint;returnsToken.Text=LocalSecrets.Unprotect(c.ReturnsTokenProtected);token.Text=LocalSecrets.Unprotect(c.PrimaryTokenProtected);parity.SelectedItem=c.Parity;if(parity.SelectedIndex<0)parity.SelectedIndex=0;}
  static int Clamp(int v,int min,int max){return Math.Max(min,Math.Min(max,v));}
  Settings ReadSettings(){
   var c=new Settings{Port=port.Text.Trim(),Baud=(int)baud.Value,DataBits=(int)bits.Value,Parity=parity.Text,Endpoint=endpoint.Text.Trim(),Company=company.Text.Trim(),Interval=(int)interval.Value,Freshness=(int)fresh.Value,ResponseMarker=marker.Text,ReturnsEndpoint=returnsEndpoint.Text.Trim(),ReturnsEnabled=true,ReturnsTokenProtected=LocalSecrets.Protect(returnsToken.Text),PrimaryTokenProtected=LocalSecrets.Protect(token.Text)};
   SettingsStore.Validate(c);
   return c;
  }
 }
}
