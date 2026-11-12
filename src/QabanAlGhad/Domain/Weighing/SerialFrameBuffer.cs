using System.Text;

namespace WeighbridgeBridge {
 public sealed class Frames {
  const int MaximumFrameLength=16384;
  readonly StringBuilder pending=new StringBuilder();
  bool discard;

  public bool Push(string part,out decimal weight,out string last){
   weight=0;
   last="";
   bool valid=false;
   foreach(char ch in part){
    if(ch=='\r'||ch=='\n'){
     if(discard){discard=false;continue;}
     if(pending.Length==0)continue;
     string frame=pending.ToString();
     pending.Clear();
     if(string.IsNullOrWhiteSpace(frame))continue;
     last=frame;
     valid=Protocol.Parse(frame,out weight);
    }else if(!discard){
     if(pending.Length==MaximumFrameLength){
      pending.Clear();
      discard=true;
      last="[oversized serial frame]";
      weight=0;
      valid=false;
     }else pending.Append(ch);
    }
   }
   return valid;
  }
 }
}
