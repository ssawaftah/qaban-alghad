// Field names and namespace remain compatible with installed settings.json files.

namespace WeighbridgeBridge
{
 public sealed class Settings {
  public string Port="COM6",Parity="None",Endpoint="https://app.circlessoft.com:9083/api/store_weight_scale",Company="1",ResponseMarker="";
  public string ReturnsEndpoint="https://qaban-113.pages.dev/api/scale/weight",ReturnsTokenProtected="",PrimaryTokenProtected="";
  public bool ReturnsEnabled=false;
  public int Baud=9600,DataBits=8,Interval=1000,Freshness=3000;
 }
}
