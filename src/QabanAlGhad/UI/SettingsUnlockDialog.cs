using System;
using System.Drawing;
using System.Windows.Forms;
namespace WeighbridgeBridge {
 public sealed class SettingsUnlockDialog:Form {
  static int attempts;static DateTime blockedUntil;
  public SettingsUnlockDialog(){
   Text="الإعدادات";Size=new Size(360,215);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;BackColor=Color.FromArgb(246,248,250);
   var title=new Label{Text="كلمة المرور",Dock=DockStyle.Top,Height=42,TextAlign=ContentAlignment.MiddleCenter};
   var password=new TextBox{UseSystemPasswordChar=true,TextAlign=HorizontalAlignment.Center,Width=260,Top=48,Left=40,MaxLength=128};
   var error=new Label{Top=80,Left=15,Width=315,Height=35,ForeColor=Color.Firebrick,TextAlign=ContentAlignment.MiddleCenter};
   var enter=new Button{Text="دخول",Top=122,Left=175,Width=120,Height=32};var cancel=new Button{Text="إلغاء",Top=122,Left=40,Width=120,Height=32,DialogResult=DialogResult.Cancel};
   Controls.AddRange(new Control[]{title,password,error,enter,cancel});AcceptButton=enter;CancelButton=cancel;
   enter.Click+=(s,e)=>{if(DateTime.UtcNow<blockedUntil){error.Text="حاول بعد قليل";return;}if(SettingsGate.Verify(password.Text)){attempts=0;DialogResult=DialogResult.OK;Close();return;}password.Clear();error.Text="كلمة المرور غير صحيحة";if(++attempts>=5){attempts=0;blockedUntil=DateTime.UtcNow.AddSeconds(30);}password.Focus();};
   FormClosed+=(s,e)=>password.Clear();
  }
 }
}
