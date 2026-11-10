using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace WeighbridgeBridge {
 public sealed partial class MainForm {
  long nextReadingLog=-1,nextInvalidEvent,lastFrameAt=-1;
  string lastFrame="";bool lastFrameValid;decimal lastFrameWeight;
  void RememberReading(long now,string raw,bool valid,decimal value){lastFrameAt=now;lastFrame=Regex.Replace(raw??"",@"[^\x20-\x7e]","?");if(lastFrame.Length>160)lastFrame=lastFrame.Substring(0,160);lastFrameValid=valid;lastFrameWeight=value;}
  void FlushReadingLog(long now){
   if(now<nextReadingLog||!ReadingPolicy.IsFresh(now,lastFrameAt,settings.Freshness))return;
   nextReadingLog=now+1000;
   string time=DateTime.Now.ToString("HH:mm:ss"),value=lastFrameValid?lastFrameWeight.ToString(CultureInfo.InvariantCulture):"غير مستقرة";
   AppendGrid(readingGrid,new object[]{time,value,lastFrame},1200,false);
   AppendFile("readings.log",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" | "+value+" | "+lastFrame);
  }
  void AppendGrid(DataGridView grid,object[] values,int limit,bool error){
   bool follow=grid.Rows.Count==0||grid.FirstDisplayedScrollingRowIndex<0||grid.FirstDisplayedScrollingRowIndex+grid.DisplayedRowCount(false)>=grid.Rows.Count-1;
   if(grid.Rows.Count>=limit)grid.Rows.RemoveAt(0);
   int index=grid.Rows.Add(values);if(error)grid.Rows[index].DefaultCellStyle.ForeColor=Color.Firebrick;
   if(follow&&grid.Visible)grid.FirstDisplayedScrollingRowIndex=index;
  }
  void AppendFile(string name,string text){try{Directory.CreateDirectory(dataDir);string file=Path.Combine(dataDir,name);if(File.Exists(file)&&new FileInfo(file).Length>2*1024*1024){string old=file+".1";if(File.Exists(old))File.Delete(old);File.Move(file,old);}File.AppendAllText(file,text+Environment.NewLine,Encoding.UTF8);}catch{}}
  void WriteBackgroundLog(string msg){AppendFile("background.log",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+msg);}
  void WriteLog(string msg){
   bool error=msg.Contains("فشل")||msg.Contains("خطأ")||msg.Contains("انقطاع")||msg.Contains("تعذر")||msg.Contains("HTTP")||msg.Contains("Exception")||msg.Contains("Timeout")||msg.Contains("Failure");
   AppendGrid(eventGrid,new object[]{DateTime.Now.ToString("HH:mm:ss"),error?"عطل":"حدث",msg},600,error);
   AppendFile("bridge.log",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+msg);
  }
  void SaveReply(ApiReply reply,string name){
   try{Directory.CreateDirectory(dataDir);File.WriteAllText(Path.Combine(dataDir,name),"HTTP "+reply.StatusCode+Environment.NewLine+(reply.Body??""),Encoding.UTF8);}catch{}
  }
 }
}
