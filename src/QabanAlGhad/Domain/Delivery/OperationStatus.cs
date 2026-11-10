namespace WeighbridgeBridge {
 public static class OperationStatus {
  public static bool Active(bool running,long now,long received,int freshness,long written){
   return running&&ReadingPolicy.IsFresh(now,received,freshness)&&ReadingPolicy.IsFresh(now,written,3000);
  }
 }
}
