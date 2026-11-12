using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace WeighbridgeBridge
{
 public sealed class DeliverySchedule {
  public long NextAttempt {get;private set;}
  public int Failures {get;private set;}
  bool hasConfirmed;decimal confirmed;
  public bool NeedsSend(decimal weight){return !hasConfirmed||weight!=confirmed;}
  public void Confirm(decimal weight){confirmed=weight;hasConfirmed=true;}
  public void Reset(){Failures=0;NextAttempt=0;hasConfirmed=false;}
  public bool CanSend(long now,bool running,bool enabled,bool fresh,bool busy){return running&&enabled&&fresh&&!busy&&now>=NextAttempt;}
  public void PeriodicStart(long now,int interval){NextAttempt=now+interval;}
  public void PeriodicSuccess(){Failures=0;}
  public int PeriodicFailure(long now,int serverDelay){Failures=Math.Min(Failures+1,20);int delay=Math.Min(300000,Math.Max(0,serverDelay));if(delay>0)NextAttempt=Math.Max(NextAttempt,now+delay);return delay;}
  public void TransportFailure(long now){
   Failures=Math.Min(Failures+1,20);
   int delay=Math.Min(15000,1000 << Math.Min(4,Failures-1));
   NextAttempt=Math.Max(NextAttempt,now+delay);
  }
  public void Success(long now,int interval){Failures=0;NextAttempt=now+interval;}
  public int Failed(long now,int serverDelay){Failures=Math.Min(Failures+1,20);int delay=Math.Max(Math.Min(30000,2000*(1<<Math.Min(4,Failures-1))),Math.Min(300000,Math.Max(0,serverDelay)));NextAttempt=now+delay;return delay;}
 }
}
