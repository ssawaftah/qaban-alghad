using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeighbridgeBridge {
 public sealed partial class MainForm {
  sealed class SerialReading {
   public long At;
   public DateTime Utc;
   public string Raw;
   public bool Valid;
   public decimal Weight;
  }

  readonly object serialMailbox=new object();
  SerialReading incomingReading;
  string incomingSerialEvent;
  bool incomingDisconnect;
  Task serialWorker;
  CancellationTokenSource serialCancellation;
  string readingState="بانتظار القراءة";

  void StartReading(){
   try{
    if(settingsNeedRepair)throw new InvalidOperationException("احفظ إعدادات صحيحة قبل التشغيل.");
    if(serialWorker!=null&&!serialWorker.IsCompleted)
     throw new InvalidOperationException("جارٍ إغلاق المنفذ. حاول بعد قليل.");
    settings=ReadSettings();
    primary.Configure(settings.Endpoint,token.Text,settings.Company,settings.Freshness);
    returns.Configure(settings.ReturnsEndpoint,returnsToken.Text,settings.Company,settings.Freshness);
    lock(serialMailbox){
     incomingReading=null;
     incomingSerialEvent=null;
     incomingDisconnect=false;
    }
    running=true;
    received=-1;
    nextReadingLog=lastFrameAt=lastLoggedFrameAt=-1;
    nextInvalidEvent=0;
    readingState="بانتظار القراءة";
    lastReadingLocal=null;
    lastFrame="";
    lastSerialIssue=null;
    fields.Enabled=false;
    start.Enabled=false;
    stop.Enabled=true;
    save.Enabled=false;
    weight.Text="kg";
    var activeSettings=settings;
    serialCancellation=new CancellationTokenSource();
    var cancellation=serialCancellation;
    serialWorker=Task.Run(async ()=>{
     try{await ReadSerial(activeSettings,cancellation.Token);}
     catch(OperationCanceledException){}
     finally{lock(serialMailbox){if(serialCancellation==cancellation)serialCancellation=null;cancellation.Dispose();}}
    });
    WriteLog("بدء القراءة "+settings.Port+" / "+settings.Baud+" / "+settings.DataBits+" / "+settings.Parity+" / StopBits.One");
   }catch(Exception ex){MessageBox.Show(ex.Message);}
  }

  void StopReading(){
   running=false;
   received=-1;
   primary.Stop();
   returns.Stop();
   lock(serialMailbox){if(serialCancellation!=null)serialCancellation.Cancel();}
   fields.Enabled=true;
   start.Enabled=true;
   stop.Enabled=false;
   save.Enabled=true;
   weight.Text="kg";
  }

  void ReportSerial(string message,bool disconnected){
   lock(serialMailbox){
    incomingSerialEvent=message;
    if(disconnected){incomingReading=null;incomingDisconnect=true;}
   }
  }

  async Task ReadSerial(Settings configuration,CancellationToken cancel){
   int retry=1000;
   while(!cancel.IsCancellationRequested){
    SerialPort connection=null;
    try{
     connection=new SerialPort(configuration.Port,configuration.Baud,
       (Parity)Enum.Parse(typeof(Parity),configuration.Parity),configuration.DataBits,StopBits.One);
     connection.Open();
     ReportSerial("تم فتح منفذ الميزان.",false);
     var frames=new Frames(true);
     long lastData=clock.ElapsedMilliseconds;
     int silenceLimit=Math.Max(5000,configuration.Freshness*2);
     while(!cancel.IsCancellationRequested){
      string part=connection.ReadExisting();
      long now=clock.ElapsedMilliseconds;
      if(part.Length>0){
       lastData=now;
       decimal value;
       string raw;
       bool valid=frames.Push(part,out value,out raw);
       if(raw.Length>0){
        if(valid)retry=1000;
        lock(serialMailbox){
         incomingReading=new SerialReading{At=now,Utc=DateTime.UtcNow,Raw=raw,Valid=valid,Weight=value};
        }
       }
      }else if(now-lastData>=silenceLimit){
       ReportSerial("انقطاع بيانات الميزان. ستُعاد محاولة فتح المنفذ.",true);
       break;
      }
      await Task.Delay(50,cancel).ConfigureAwait(false);
     }
    }catch(OperationCanceledException){return;}
    catch(Exception ex){ReportSerial("تعذر الاتصال بمنفذ الميزان: "+ex.GetType().Name,true);}
    finally{if(connection!=null){try{connection.Dispose();}catch{}}}
    await Task.Delay(retry,cancel).ConfigureAwait(false);
    retry=Math.Min(15000,retry*2);
   }
  }

  void Tick(object sender,EventArgs e){
   if(!running)return;
   SerialReading sample;
   string message;
   bool disconnected;
   lock(serialMailbox){
    sample=incomingReading;
    message=incomingSerialEvent;
    disconnected=incomingDisconnect;
    incomingReading=null;
    incomingSerialEvent=null;
    incomingDisconnect=false;
   }
   long now=clock.ElapsedMilliseconds;
   if(disconnected){received=-1;readingState="انقطع المنفذ";lastSerialIssue=message;}
   if(message!=null)WriteLog(message);
   if(sample!=null){
    RememberReading(sample.At,sample.Raw,sample.Valid,sample.Weight);
    lastReadingLocal=sample.Utc.ToLocalTime();
    if(sample.Valid){
     latest=sample.Weight;
     received=sample.At;
     sampledAtUtc=sample.Utc;
     readingState="بانتظار الإرسال";
    }else{
     received=-1;
     readingState=sample.Raw.TrimStart(' ','\x02').StartsWith("US",StringComparison.OrdinalIgnoreCase)
      ?"قراءة غير مستقرة"
      :Regex.IsMatch(sample.Raw,@"(?:[0-9]|,)\s*(lb|lbs|oz|g|t)\s*$",RegexOptions.IgnoreCase)
       ?"وحدة غير مدعومة":"قراءة غير صالحة";
     if(now>=nextInvalidEvent){
      nextInvalidEvent=now+3000;
      WriteLog(readingState+": "+lastFrame);
     }
    }
   }
   bool recent=ReadingPolicy.IsFresh(now,received,settings.Freshness);
   if(!recent&&received>=0)readingState="لا تصل قراءة";
   weight.Text=recent?latest.ToString(CultureInfo.InvariantCulture)+" kg":"kg";
   FlushReadingLog(now);
   primary.Pump(now,true,received,sampledAtUtc,latest);
   returns.Pump(now,true,received,sampledAtUtc,latest);
  }
 }
}
