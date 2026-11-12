using System;
using System.IO;
using System.Text;
using System.Drawing;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace WeighbridgeBridge
{
    public sealed partial class MainForm
    {
        long nextReadingLog = -1, nextInvalidEvent, lastFrameAt = -1, lastLoggedFrameAt = -1;
        DateTime? lastReadingLocal;
        readonly Label logTimes = new Label();
        static string DisplayTime(DateTime? time)
        {
            return time.HasValue ? time.Value.ToString("HH:mm:ss") : "—";
        }
        readonly BackgroundLogWriter logWriter = new BackgroundLogWriter();
        string lastSerialIssue;
        string lastFrame = "";
        bool lastFrameValid;
        decimal lastFrameWeight;
        DateTime lastFrameUtc;
        void RememberReading(long now, string raw, bool valid, decimal value)
        {
            RememberSample(now, DateTime.UtcNow, raw, valid, value);
        }
        void RememberSample(long now, DateTime utc, string raw, bool valid, decimal value)
        {
            lastFrameUtc = utc;
            lastFrameAt = now;
            lastFrame = Regex.Replace(raw ?? "", @"[^\x20-\x7e]", "?");
            if (lastFrame.Length > 160)
                lastFrame = lastFrame.Substring(0, 160);
            lastFrameValid = valid;
            lastFrameWeight = value;
        }
        void FlushReadingLog(long now)
        {
            if (lastFrameAt <= lastLoggedFrameAt || now < nextReadingLog ||
                !ReadingPolicy.IsFresh(now, lastFrameAt, settings.Freshness))
                return;
            nextReadingLog = now + 1000;
            lastLoggedFrameAt = lastFrameAt;
            string time = lastFrameUtc.ToLocalTime().ToString("HH:mm:ss"),
                   value = lastFrameValid ? lastFrameWeight.ToString(CultureInfo.InvariantCulture)
                                          : ReadingDescription.Rejection(lastFrame);
            AppendGrid(readingGrid, new object[] { time, value, lastFrame }, 1200, false);
            AppendFile("readings.log",
                       lastFrameUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") + " | " + value + " | " + lastFrame);
        }
        void AppendGrid(DataGridView grid, object[] values, int limit, bool error)
        {
            bool follow = followLogs &&
                          (grid.Rows.Count == 0 || grid.FirstDisplayedScrollingRowIndex < 0 ||
                           grid.FirstDisplayedScrollingRowIndex + grid.DisplayedRowCount(false) >= grid.Rows.Count - 1);
            if (grid.Rows.Count >= limit)
                grid.Rows.RemoveAt(0);
            int index = grid.Rows.Add(values);
            grid.Rows[index].Tag = error;
            ApplyLogFilter(grid, grid.Rows[index]);
            if (error)
                grid.Rows[index].DefaultCellStyle.ForeColor = Color.Firebrick;
            if (follow && grid.Visible && grid.Rows[index].Visible)
                grid.FirstDisplayedScrollingRowIndex = index;
        }
        void AppendFile(string name, string text)
        {
            QueueLog(() =>
                     {
                         Directory.CreateDirectory(dataDir);
                         string file = Path.Combine(dataDir, name);
                         if (File.Exists(file) && new FileInfo(file).Length > 2 * 1024 * 1024)
                         {
                             string old = file + ".1";
                             if (File.Exists(old))
                                 File.Delete(old);
                             File.Move(file, old);
                         }
                         File.AppendAllText(file, text + Environment.NewLine, Encoding.UTF8);
                     });
        }
        void QueueLog(Action write)
        {
            logWriter.Enqueue(write);
        }
        void ApplyLogFilter(DataGridView grid, DataGridViewRow row)
        {
            string query = logSearch.Text.Trim();
            bool matches = query.Length == 0;
            foreach (DataGridViewCell cell in row.Cells)
                if (Convert.ToString(cell.Value).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    matches = true;
            bool visible =
                matches && (grid != eventGrid || logFilter.SelectedIndex != 1 || object.Equals(row.Tag, true));
            if (!visible && grid.CurrentRow == row)
                grid.CurrentCell = null;
            row.Visible = visible;
        }
        void FilterLogs()
        {
            foreach (var grid in new[] { readingGrid, eventGrid })
            {
                grid.CurrentCell = null;
                foreach (DataGridViewRow row in grid.Rows)
                    ApplyLogFilter(grid, row);
            }
        }
        async Task ShowDiagnostics(Button button)
        {
            button.Enabled = false;
            try
            {
                string[] available = await Task.Run(() => SerialPort.GetPortNames());
                if (IsDisposed)
                    return;
                Array.Sort(available, StringComparer.OrdinalIgnoreCase);
                string ports = available.Length == 0 ? "—" : string.Join(", ", available);
                string state = running ? operationIndicator.Text : "متوقف";
                string reading = DisplayTime(lastReadingLocal);
                string sent = DisplayTime(primary.LastWriteLocal);
                string details =
                    "قبان الغد " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) +
                    Environment.NewLine + "المنفذ المختار: " + SafePort(port.Text) + Environment.NewLine +
                    "المنافذ المتاحة: " + ports + Environment.NewLine + "الاتصال: " + baud.Value + " / " +
                    bits.Value + " / " + UiTheme.ParityName(parity.Text) + " / 1" + Environment.NewLine +
                    "الحالة: " + state + Environment.NewLine + "آخر قراءة: " + reading + Environment.NewLine +
                    "آخر إرسال: " + sent + Environment.NewLine + "آخر عطل منفذ: " + (lastSerialIssue ?? "—") +
                    Environment.NewLine + "آخر عطل إرسال: " + (primary.LastTransportIssue ?? "—") +
                    Environment.NewLine + "القناة الخلفية: " +
                    (returns.ConfigurationError ?? returns.LastResponseIssue ?? returns.LastTransportIssue ??
                     (returns.Ready ? "مهيأة" : "متوقفة")) +
                    Environment.NewLine + "أخطاء حفظ السجل: " + logWriter.Errors +
                    " | أحداث لم تُحفظ: " + logWriter.Dropped + Environment.NewLine +
                    "هذا تشخيص للحالة المحلية؛ لا يرسل طلب اختبار إلى الخادم.";
                using (var dialog = new Form())
                {
                    UiTheme.Dialog(dialog, "تشخيص الاتصال", new Size(600, 430));
                    var content =
                        new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                                               Padding = new Padding(20, 16, 20, 16), BackColor = Color.White };
                    content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    content.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                    content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
                    content.Controls.Add(UiTheme.Label("تشخيص الاتصال", UiTheme.Heading), 0, 0);
                    var card = new InputSurface { Dock = DockStyle.Fill, Padding = new Padding(10),
                                                  Margin = new Padding(0, 8, 0, 8) };
                    var body = new TextBox { Multiline = true,
                                             ReadOnly = true,
                                             Dock = DockStyle.Fill,
                                             ScrollBars = ScrollBars.Vertical,
                                             Text = details,
                                             RightToLeft = RightToLeft.Yes,
                                             Font = UiTheme.Body,
                                             BorderStyle = BorderStyle.None,
                                             BackColor = Color.White,
                                             ForeColor = UiTheme.Navy,
                                             AccessibleName = "تقرير تشخيص الاتصال" };
                    if (lastFrame.Length > 0)
                        body.AppendText(Environment.NewLine + "آخر إطار: " + lastFrame);
                    card.Controls.Add(body);
                    content.Controls.Add(card, 0, 1);
                    var actions =
                        new FlowLayoutPanel { Dock = DockStyle.Fill,        FlowDirection = FlowDirection.RightToLeft,
                                              RightToLeft = RightToLeft.No, WrapContents = false,
                                              BackColor = Color.White,      Padding = new Padding(0),
                                              Margin = new Padding(0) };
                    var copy = new ActionButton();
                    UiTheme.Button(copy, "نسخ التقرير", true);
                    copy.Size = new Size(124, 36);
                    copy.Margin = new Padding(0, 6, 8, 0);
                    copy.Click += (sender, e) =>
                    {
                        try
                        {
                            Clipboard.SetText(details);
                            copy.Text = "تم النسخ";
                        }
                        catch (ExternalException)
                        {
                            copy.Text = "تعذر النسخ";
                        }
                    };
                    var export = new ActionButton();
                    UiTheme.Button(export, "حفظ التقرير", false);
                    export.Size = new Size(124, 36);
                    export.Margin = new Padding(0, 6, 8, 0);
                    export.Click += (sender, e) =>
                    {
                        using (var destination =
                                   new SaveFileDialog { Title = "حفظ تقرير التشخيص", Filter = "ملف نصي (*.txt)|*.txt",
                                                        FileName = "QabanAlGhad-diagnostics.txt" })
                        {
                            if (destination.ShowDialog(dialog) != DialogResult.OK)
                                return;
                            try
                            {
                                File.WriteAllText(destination.FileName, details, Encoding.UTF8);
                                export.Text = "تم الحفظ";
                            }
                            catch (Exception)
                            {
                                AppMessage.Show(dialog, "تعذر حفظ التقرير.");
                            }
                        }
                    };
                    actions.Controls.Add(copy);
                    actions.Controls.Add(export);
                    var close = new ActionButton();
                    UiTheme.Button(close, "إغلاق", false);
                    close.Size = new Size(88, 36);
                    close.Margin = new Padding(0, 6, 8, 0);
                    close.DialogResult = DialogResult.Cancel;
                    actions.Controls.Add(close);
                    dialog.CancelButton = close;
                    content.Controls.Add(actions, 0, 2);
                    dialog.Controls.Add(content);
                    dialog.ShowDialog(this);
                }
            }
            catch (Exception)
            {
                if (!IsDisposed)
                    AppMessage.Show("تعذر فحص المنافذ.");
            }
            finally
            {
                if (!IsDisposed)
                    button.Enabled = true;
            }
        }
        static string SafePort(string value)
        {
            return Regex.IsMatch(value ?? "", @"^COM[0-9]+$", RegexOptions.IgnoreCase) ? value : "—";
        }
        void WriteBackgroundLog(string msg)
        {
            AppendFile("background.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg);
        }
        void WriteLog(string msg)
        {
            bool error = msg.Contains("فشل") || msg.Contains("خطأ") || msg.Contains("انقطاع") || msg.Contains("تعذر") ||
                         msg.Contains("HTTP") || msg.Contains("Exception") || msg.Contains("Timeout") ||
                         msg.Contains("Failure");
            AppendGrid(eventGrid, new object[] { DateTime.Now.ToString("HH:mm:ss"), error ? "عطل" : "حدث", msg }, 600,
                       error);
            AppendFile("bridge.log", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + msg);
        }
        void SaveReply(ApiReply reply, string name)
        {
            AppendFile(name,
                       "HTTP " + reply.StatusCode + (reply.DiagnosticCode == null ? "" : " | " + reply.DiagnosticCode));
        }
    }
}
