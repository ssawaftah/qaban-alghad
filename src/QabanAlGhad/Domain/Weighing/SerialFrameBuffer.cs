using System;

namespace WeighbridgeBridge
{
 public sealed class Frames {
  string pending="";
  public bool Push(string part, out decimal weight, out string last) {
   weight=0;last="";bool valid=false; pending+=part;
   if(pending.Length>16384) {pending="";return false;}
   int k;
   while((k=pending.IndexOfAny(new char[]{'\r','\n'}))>=0) {
    string frame=pending.Substring(0,k);pending=pending.Substring(k+1);
    if(frame.Trim().Length==0) continue;
    last=frame;decimal w;
    // The latest complete frame is authoritative. An invalid newer frame
    // must invalidate any earlier valid one from the same read.
    valid=Protocol.Parse(frame,out w);if(valid) weight=w;
   }
   return valid;
  }
 }
}
