using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using System.Collections.Generic;
namespace WeighbridgeBridge
{
    public static class InterfaceTests
    {
        static int count;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("Interface: " + label);
            count++;
        }
        static object Field(MainForm form, string name)
        {
            return typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
        }
        static void Set(MainForm form, string name, object value)
        {
            typeof(MainForm).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(form, value);
        }
        static void Call(MainForm form, string name, params object[] args)
        {
            typeof(MainForm).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, args);
        }
        static IEnumerable<Control> All(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in All(child))
                    yield return nested;
            }
        }
        static void Capture(MainForm form, string name)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name));
            }
        }
        public static void Rules()
        {
            Check(OperationStatus.Active(true, 1000, 1000, 3000, 1000), "new reading and body write active");
            Check(!OperationStatus.Active(false, 1000, 1000, 3000, 1000), "stopped red");
            Check(!OperationStatus.Active(true, 1000, 1000, 3000, -1), "no write red");
            Check(!OperationStatus.Active(true, 7000, 7000, 3000, 1000), "old write red");
            Check(OperationStatus.Active(true, 4500, 4500, 3000, 1000), "no flicker while previous request times out");
            Check(!OperationStatus.Active(true, 5000, 1000, 3000, 5000), "old reading red");
            var feedback = new TransferFeedback();
            feedback.BodyWritten();
            feedback.Reply(500, false, false, true);
            Check(OperationStatus.Active(true, 1000, 1000, 3000, 1000),
                  "server reply does not determine local indicator");
            Check(!SettingsGate.Verify(null) && !SettingsGate.Verify("") &&
                      !SettingsGate.Verify("incorrect-test-password"),
                  "password rejects invalid values");
            Console.WriteLine("PASS: " + count + " local operation/password rules");
        }
        public static int Windows()
        {
            Exception failure = null;
            using (var form = new MainForm())
            {
                form.Shown += (sender, e) =>
                {
                    try
                    {
                        var start = (Button)Field(form, "start");
                        var weight = (Label)Field(form, "weight");
                        var status = (Button)Field(form, "operationIndicator");
                        var stop = (Button)Field(form, "stop");
                        var overviewNav = (Button)Field(form, "overviewNav");
                        var logsNav = (Button)Field(form, "logsNav");
                        var settingsNav = (Button)Field(form, "settingsNav");
                        Check(weight.PointToScreen(Point.Empty).Y + weight.Height < start.PointToScreen(Point.Empty).Y,
                              "weight above action row");
                        Check(start.PointToScreen(Point.Empty).X > stop.PointToScreen(Point.Empty).X &&
                                  stop.PointToScreen(Point.Empty).X > status.PointToScreen(Point.Empty).X,
                              "actions ordered right to left");
                        Check(start.Top == stop.Top && stop.Top == status.Top, "actions share one row");
                        Check(Math.Abs(start.Left - stop.Right - (stop.Left - status.Right)) <= 1, "equal action gaps");
                        Check(overviewNav.PointToScreen(Point.Empty).X > logsNav.PointToScreen(Point.Empty).X &&
                                  logsNav.PointToScreen(Point.Empty).X > settingsNav.PointToScreen(Point.Empty).X,
                              "tabs ordered weight logs settings");
                        Check(Math.Abs(overviewNav.Width - logsNav.Width) <= 2 &&
                                  Math.Abs(logsNav.Width - settingsNav.Width) <= 2,
                              "tabs equal width");
                        Check(overviewNav.BackColor.G > overviewNav.BackColor.R && overviewNav.ForeColor == Color.White,
                              "default tab active green");
                        Check(form.ClientSize.Width <= 700 && form.ClientSize.Height <= 340, "compact default window");
                        var updateButton = (Button)Field(form, "updateInfo");
                        var credit = (Label)Field(form, "versionCredit");
                        Check(updateButton.Visible && credit.Visible, "update header visible on overview");
                        Check(credit.Text ==
                                  typeof(MainForm).Assembly.GetName().Version.ToString(3) + " - Abdelrahman Sawaftah",
                              "credit follows application version");
                        var iconPosition = updateButton.PointToScreen(Point.Empty);
                        Check(iconPosition.X < settingsNav.PointToScreen(Point.Empty).X + settingsNav.Width / 2 &&
                                  iconPosition.Y < settingsNav.PointToScreen(Point.Empty).Y &&
                                  updateButton.Region != null && updateButton.Text == "!",
                              "circular update icon at top left");
                        Check(credit.PointToScreen(Point.Empty).Y >= start.PointToScreen(Point.Empty).Y + start.Height,
                              "version credit below actions");
                        bool informationOpened = false;
                        using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
                        {
                            timer.Tick += (s, eventArgs) =>
                            {
                                var information = Field(form, "updateWindow") as Form;
                                if (information == null || !information.Visible)
                                    return;
                                timer.Stop();
                                informationOpened = ((Button)Field(form, "checkUpdate")).Visible &&
                                                    information.Text == "معلومات الإصدار والتحديث";
                                information.Close();
                            };
                            timer.Start();
                            updateButton.PerformClick();
                            timer.Stop();
                        }
                        Check(informationOpened, "icon opens version and update dialog");
                        Size compactSize = form.ClientSize;
                        Check(weight.Text == "kg", "no dash or fabricated zero");
                        foreach (var control in All(form))
                            Check(!(control is CheckBox), "no send checkbox");
                        Check(status.BackColor.R > status.BackColor.G, "initial state red");
                        bool asked = false;
                        Exception gateFailure = null;
                        using (var timer = new System.Windows.Forms.Timer { Interval = 100 })
                        {
                            timer.Tick += (s, eventArgs) =>
                            {
                                foreach (Form open in Application.OpenForms)
                                {
                                    if (!(open is SettingsUnlockDialog))
                                        continue;
                                    timer.Stop();
                                    asked = true;
                                    try
                                    {
                                        foreach (var control in All(open))
                                            if (control is TextBox)
                                                Check(((TextBox)control).UseSystemPasswordChar,
                                                      "password input masked");
                                    }
                                    catch (Exception ex)
                                    {
                                        gateFailure = ex;
                                    }
                                    open.DialogResult = DialogResult.Cancel;
                                    open.Close();
                                    break;
                                }
                            };
                            timer.Start();
                            ((Button)Field(form, "settingsNav")).PerformClick();
                            timer.Stop();
                        }
                        if (gateFailure != null)
                            throw gateFailure;
                        Check(asked, "settings requires dialog");
                        Check(!((Panel)Field(form, "settingsPage")).Visible, "cancel leaves settings locked");
                        long now = ((Stopwatch)Field(form, "clock")).ElapsedMilliseconds;
                        Set(form, "running", true);
                        Set(form, "received", now);
                        typeof(OutputChannel)
                            .GetProperty("LastBodyWritten")
                            .GetSetMethod(true)
                            .Invoke(Field(form, "primary"), new object[] { now });
                        weight.Text = "-120 kg";
                        Call(form, "RefreshInterface");
                        Check(status.BackColor.G > status.BackColor.R, "new local write green");
                        Capture(form, "ui-overview.png");
                        Set(form, "running", false);
                        Call(form, "RefreshInterface");
                        Call(form, "ShowScreen", Field(form, "settingsPage"), Field(form, "settingsNav"));
                        foreach (var control in All((Control)Field(form, "settingsPage")))
                        {
                            var grid = control as TableLayoutPanel;
                            if (grid == null || grid.ColumnCount != 2)
                                continue;
                            foreach (Control label in grid.Controls)
                            {
                                if (!(label is Label))
                                    continue;
                                int row = grid.GetRow(label);
                                var input = grid.GetControlFromPosition(1, row);
                                if (input == null)
                                    continue;
                                Check(Math.Abs(label.PointToScreen(Point.Empty).Y + label.Height / 2 -
                                               input.PointToScreen(Point.Empty).Y - input.Height / 2) < 15,
                                      "setting labels align with inputs");
                            }
                        }
                        Check(form.ClientSize.Width > compactSize.Width && form.ClientSize.Height > compactSize.Height,
                              "settings expand for readability");
                        Check(updateButton.Visible && credit.Visible, "update header visible in settings");
                        Capture(form, "ui-settings.png");
                        Call(form, "ShowScreen", Field(form, "logsPage"), Field(form, "logsNav"));
                        Check(logsNav.ForeColor == Color.White && overviewNav.ForeColor != Color.White,
                              "selected tab highlight follows navigation");
                        Call(form, "WriteLog", "اختبار واجهة محلي دون أوزان إنتاجية");
                        Call(form, "RememberReading", now, "ST,GS,-120,kg", true, -120m);
                        Call(form, "FlushReadingLog", now);
                        Call(form, "FlushReadingLog", now + 100);
                        var readings = (DataGridView)Field(form, "readingGrid");
                        var events = (DataGridView)Field(form, "eventGrid");
                        Check(readings.Rows.Count == 1, "one reading per second");
                        Call(form, "FlushReadingLog", now + 1000);
                        Check(readings.Rows.Count == 1, "no old frame logged as a new arrival");
                        Call(form, "RememberReading", now + 1100, "ST,GS,-120,kg", true, -120m);
                        Call(form, "FlushReadingLog", now + 1100);
                        Check(readings.Rows.Count == 2, "unchanged new arrival logged");
                        Check(events.Rows.Count == 1, "separate events list");
                        Check(updateButton.Visible && credit.Visible, "update header visible in logs");
                        Capture(form, "ui-logs.png");
                        Call(form, "ShowScreen", Field(form, "overviewPage"), Field(form, "overviewNav"));
                        Check(form.ClientSize == compactSize, "return to compact weight view");
                        Console.WriteLine("PASS: Windows UI layout, settings lock, local status and split logs");
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        Set(form, "running", false);
                        form.Close();
                    }
                };
                Application.Run(form);
            }
            if (failure != null)
            {
                Console.WriteLine(failure);
                return 1;
            }
            return 0;
        }
    }
}
