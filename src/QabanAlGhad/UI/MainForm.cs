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
 public sealed partial class MainForm:Form {
  TextBox port=new TextBox(),endpoint=new TextBox(),company=new TextBox(),marker=new TextBox(),token=new TextBox(),returnsEndpoint=new TextBox(),returnsToken=new TextBox();
  NumericUpDown baud=new NumericUpDown(),bits=new NumericUpDown(),interval=new NumericUpDown(),fresh=new NumericUpDown();
  ComboBox parity=new ComboBox();
  Button start=new Button(),stop=new Button(),save=new Button();
  Label weight=new Label();
  TableLayoutPanel fields=new TableLayoutPanel();
  decimal latest;long received=-1;bool running;DateTime sampledAtUtc;
  readonly Stopwatch clock=Stopwatch.StartNew();
  readonly System.Windows.Forms.Timer poll=new System.Windows.Forms.Timer();
  readonly string dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WeighbridgeBridge");
  Settings settings;
  readonly OutputChannel primary,returns;
  public MainForm() {
   primary=new OutputChannel(OutputKind.Primary,"الأساسي",()=>clock.ElapsedMilliseconds,WriteLog,SaveReply);
   returns=new OutputChannel(OutputKind.Returns,"القناة الثانية",()=>clock.ElapsedMilliseconds,WriteBackgroundLog,SaveReply);
   BuildInterface();
   settings=LoadSettings();
   try{if(!settingsNeedRepair&&Bootstrap.Import(Application.StartupPath,SettingsPath,settings))WriteBackgroundLog("تم تجهيز الربط.");}catch(Exception ex){WriteBackgroundLog("تعذر استيراد الربط: "+ex.GetType().Name);}
   Apply(settings);
   BindUpdates();
   start.Click+=(s,e)=>StartReading();stop.Click+=(s,e)=>StopReading();
   save.Click+=(s,e)=>{try{var cfg=ReadSettings();SettingsStore.Save(SettingsPath,cfg);settingsNeedRepair=false;settings=cfg;WriteLog("تم حفظ الإعدادات؛ رموز الربط مشفرة لحساب Windows الحالي.");}catch(Exception ex){MessageBox.Show(ex.Message);}};
   poll.Interval=100;poll.Tick+=(s,e)=>{Tick(s,e);RefreshInterface();};poll.Start();FormClosing+=(s,e)=>{StopReading();poll.Stop();};
  }
 }
}
