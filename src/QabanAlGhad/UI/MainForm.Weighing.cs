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
  void StartReading(){
   try{settings=ReadSettings();
    primary.Configure(settings.Endpoint,token.Text,settings.Company,settings.Freshness);returns.Configure(settings.ReturnsEndpoint,returnsToken.Text,settings.Company,settings.Freshness);
    running=true;received=-1;frames=new Frames();nextReadingLog=-1;lastFrameAt=-1;nextInvalidEvent=0;nextOpen=0;fields.Enabled=false;start.Enabled=false;stop.Enabled=true;save.Enabled=false;weight.Text="kg";httpState.Text="الإرسال: بانتظار قراءة حديثة";WriteLog("بدء القراءة "+settings.Port+" / "+settings.Baud+" / "+settings.DataBits+" / "+settings.Parity+" / StopBits.One");
   }catch(Exception ex){MessageBox.Show(ex.Message);}
  }
  void StopReading(){running=false;received=-1;primary.Stop();returns.Stop();CloseSerial();fields.Enabled=true;start.Enabled=true;stop.Enabled=false;save.Enabled=true;serialState.Text="الميزان: متوقف";httpState.Text="الإرسال: متوقف";weight.Text="kg";}
  void CloseSerial(){if(serial!=null){try{serial.Close();serial.Dispose();}catch{ }serial=null;}}
  void Tick(object sender,EventArgs e){
   if(!running)return;
   long now=clock.ElapsedMilliseconds;
   if(serial==null && now>=nextOpen){try{serial=new SerialPort(settings.Port,settings.Baud,(Parity)Enum.Parse(typeof(Parity),settings.Parity),settings.DataBits,StopBits.One);serial.Handshake=Handshake.None;serial.DtrEnable=false;serial.RtsEnable=false;serial.Open();frames=new Frames();WriteLog("تم فتح منفذ الميزان.");serialState.Text="الميزان: متصل؛ بانتظار إطار كامل";}catch(Exception ex){CloseSerial();received=-1;nextOpen=now+3000;serialState.Text="الميزان: تعذر فتح المنفذ";WriteLog("فشل فتح المنفذ: "+ex.GetType().Name+". تأكدي من المنفذ وإغلاق البرنامج القديم.");}}
   if(serial!=null){try{string part=serial.ReadExisting();if(part.Length>0){decimal w;string raw;if(frames.Push(part,out w,out raw)){RememberReading(now,raw,true,w);latest=w;received=now;sampledAtUtc=DateTime.UtcNow;weight.Text=w.ToString(CultureInfo.InvariantCulture)+" kg";serialState.Text="الميزان: قراءة حديثة";}else if(raw.Length>0){RememberReading(now,raw,false,0);received=-1;weight.Text="kg";serialState.Text="الميزان: إطار غير مدعوم أو غير مستقر";if(now>=nextInvalidEvent){nextInvalidEvent=now+3000;WriteLog("إطار: "+Regex.Replace(raw,@"[^\x20-\x7e]","?").Substring(0,Math.Min(120,raw.Length)));}}}}catch(MissingMethodException ex){StopReading();serialState.Text="الميزان: تعذر معالجة القراءة";WriteLog("خطأ توافق البرنامج: "+ex.Message);}catch(Exception ex){WriteLog("خطأ أثناء القراءة: "+ex.GetType().Name+": "+ex.Message);CloseSerial();received=-1;nextOpen=now+3000;}}
   bool recent=ReadingPolicy.IsFresh(now,received,settings.Freshness);
   age.Text=received<0?"لا توجد قراءة صالحة":("عمر القراءة: "+((now-received)/1000.0).ToString("0.0")+" ثانية");
   if(!recent){weight.Text="kg";if(received>=0)serialState.Text="الميزان: توقفت القراءات";if(!primary.Busy)httpState.Text="الإرسال: متوقف حتى تصل قراءة حديثة";}
   FlushReadingLog(now);
   primary.Pump(now,true,received,sampledAtUtc,latest);
   returns.Pump(now,true,received,sampledAtUtc,latest);
  }
 }
}
