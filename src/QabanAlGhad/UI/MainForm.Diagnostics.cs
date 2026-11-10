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
  void WriteBackgroundLog(string msg){try{Directory.CreateDirectory(dataDir);string f=Path.Combine(dataDir,"background.log");if(File.Exists(f)&&new FileInfo(f).Length>2*1024*1024)File.Delete(f);File.AppendAllText(f,DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+msg+Environment.NewLine,Encoding.UTF8);}catch{ }}
  void WriteLog(string msg){string line=DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+msg;if(log.TextLength>50000)log.Text=log.Text.Substring(log.TextLength-25000);log.AppendText(line+Environment.NewLine);try{Directory.CreateDirectory(dataDir);string f=Path.Combine(dataDir,"bridge.log");if(File.Exists(f)&&new FileInfo(f).Length>2*1024*1024){string old=f+".1";if(File.Exists(old))File.Delete(old);File.Move(f,old);}File.AppendAllText(f,line+Environment.NewLine,Encoding.UTF8);}catch{ }}

  void SaveReply(ApiReply reply,string name){try{Directory.CreateDirectory(dataDir);File.WriteAllText(Path.Combine(dataDir,name),"HTTP "+reply.StatusCode+Environment.NewLine+(reply.Body??""),Encoding.UTF8);}catch{ }}
 }
}
