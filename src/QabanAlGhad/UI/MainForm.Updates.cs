using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed partial class MainForm
    {
        sealed class UpdateInfoButton : Button
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Canvas);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var circle = Rectangle.Inflate(ClientRectangle, -2, -2);
                var outline = Focused && ShowFocusCues ? Accent : Muted;
                using (var pen = new Pen(outline, 1.25f)) e.Graphics.DrawEllipse(pen, circle);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Ink,
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                          TextFormatFlags.NoPadding);
            }
        }

        readonly Button updateInfo = new UpdateInfoButton(), checkUpdate = new Button();
        Form updateWindow;
        Label updateVersion;
        ProgressBar updateProgress;
        Task updatePreparation = Task.FromResult(0);
        bool updateInstallFailed;
        Label versionCredit;
        static readonly Version CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        AvailableUpdate availableUpdate;
        bool updating, checkingUpdate;

        void BindUpdates()
        {
            updateInfo.Click += (s, e) => ShowUpdateInformation();
            Disposed += (s, e) =>
            {
                if (updateWindow != null)
                    updateWindow.Dispose();
            };
            checkUpdate.Click += async (s, e) =>
            {
                if (availableUpdate == null)
                    await CheckUpdate();
                else
                    await InstallUpdate();
            };
        }

        void ShowUpdateInformation()
        {
            if (updateWindow == null)
            {
                updateWindow = new Form { Text = "معلومات الإصدار والتحديث",
                                          ClientSize = new Size(390, 192),
                                          StartPosition = FormStartPosition.CenterParent,
                                          FormBorderStyle = FormBorderStyle.FixedDialog,
                                          AutoScaleMode = AutoScaleMode.Font,
                                          Font = Font,
                                          BackColor = Canvas,
                                          RightToLeft = RightToLeft.Yes,
                                          RightToLeftLayout = true,
                                          ShowInTaskbar = false,
                                          MaximizeBox = false,
                                          MinimizeBox = false };
                var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                                                     Padding = new Padding(12, 10, 12, 8) };
                content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                content.Controls.Add(CenterLabel("قبان الغد " + CurrentVersion.ToString(3), 12, Ink, Canvas), 0, 0);
                var author = CenterLabel("Abdelrahman Sawaftah", 9, Muted, Canvas);
                author.RightToLeft = RightToLeft.No;
                content.Controls.Add(author, 0, 1);
                updateVersion = CenterLabel("", 9, Ink, Canvas);
                content.Controls.Add(updateVersion, 0, 2);
                StyleButton(checkUpdate, "التحقق من التحديثات", false);
                checkUpdate.Size = new Size(190, 34);
                checkUpdate.Margin = new Padding(0);
                updateProgress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0,
                                                   Maximum = 100,         AccessibleName = "تقدم تنزيل التحديث",
                                                   Visible = false,       Margin = new Padding(12, 5, 12, 5) };
                content.Controls.Add(updateProgress, 0, 3);
                content.Controls.Add(Centered(checkUpdate), 0, 4);
                updateWindow.Controls.Add(content);
                updateWindow.AcceptButton = checkUpdate;
                updateWindow.FormClosing += (s, e) =>
                {
                    if (updating && e.CloseReason == CloseReason.UserClosing)
                        e.Cancel = true;
                };
            }
            updateWindow.ShowDialog(this);
        }

        void ShowUpdateButton(string caption, bool offerUpdate)
        {
            checkUpdate.Text = caption;
            checkUpdate.AccessibleName = caption;
            checkUpdate.BackColor = offerUpdate ? Accent : System.Drawing.Color.FromArgb(231, 237, 241);
            checkUpdate.ForeColor = offerUpdate ? System.Drawing.Color.White : Ink;
            checkUpdate.FlatAppearance.MouseOverBackColor =
                offerUpdate ? System.Drawing.Color.FromArgb(0, 105, 98) : System.Drawing.Color.FromArgb(219, 229, 235);
        }

        async Task CheckUpdate()
        {
            if (checkingUpdate || updating)
                return;
            checkingUpdate = true;
            checkUpdate.Enabled = false;
            ShowUpdateButton("جارٍ التحقق", false);
            try
            {
                availableUpdate = await ReleaseClient.Check(CurrentVersion, updateCancellation.Token);
                if (IsDisposed || updateCancellation.IsCancellationRequested)
                    return;
                updateVersion.Text = availableUpdate == null ? "" : "الإصدار المتاح " + availableUpdate.Version;
                ShowUpdateButton(availableUpdate == null ? "لا توجد تحديثات" : "يوجد تحديث", availableUpdate != null);
            }
            catch (Exception ex)
            {
                if (IsDisposed || updateCancellation.IsCancellationRequested)
                    return;
                availableUpdate = null;
                updateVersion.Text = UpdateError(ex);
                WriteBackgroundLog("Update check: " + ex.GetType().Name);
                ShowUpdateButton("تعذر التحقق", false);
            }
            finally
            {
                checkingUpdate = false;
                if (!IsDisposed)
                    checkUpdate.Enabled = true;
            }
        }

        static string UpdateError(Exception error)
        {
            if (error is UnauthorizedAccessException)
                return "تحقق من صلاحية الكتابة في مجلد البرنامج.";
            if (error is InvalidDataException)
                return "تعذر التحقق من سلامة حزمة التحديث.";
            if (error is System.Net.WebException)
                return "تعذر الاتصال بخدمة التحديث. تحقق من الشبكة ثم أعد المحاولة.";
            if (error is IOException)
                return "تعذر تجهيز ملفات التحديث؛ تحقق من المساحة والملفات المفتوحة.";
            return "تعذر إكمال العملية. راجع سجل التشخيص وأعد المحاولة.";
        }

        async Task InstallUpdate()
        {
            if (updating || checkingUpdate || availableUpdate == null)
                return;
            if (!ResolveUnsavedSettings())
                return;
            updating = true;
            checkUpdate.Enabled = false;
            string stage = null;
            bool stoppedForInstall = false;
            updateInstallFailed = false;
            try
            {
                var update = availableUpdate;
                stage = UpdateTransaction.NewStage(Application.StartupPath);
                string zip = Path.Combine(stage, "package.zip");
                ShowUpdateButton("جارٍ التنزيل", false);
                updateProgress.Visible = true;
                updateProgress.Value = 0;
                var progress = new Progress<int>(value =>
                                                 {
                                                     if (!IsDisposed && !updateProgress.IsDisposed)
                                                     {
                                                         updateProgress.Value = value;
                                                         updateVersion.Text = "تنزيل التحديث " + value + "%";
                                                     }
                                                 });
                await ReleaseClient.Fetch(update, zip, updateCancellation.Token, progress);
                // Extraction and hashing run away from the serial polling/UI thread.
                ShowUpdateButton("جارٍ التحقق والتجهيز", false);
                updatePreparation =
                    Task.Run(() => UpdateTransaction.Extract(zip, stage, update.Version, update.Sha256));
                await updatePreparation;
                if (updateCancellation.IsCancellationRequested)
                    return;
                if (MessageBox.Show(updateWindow,
                                    "التحديث " + update.Version +
                                        " جاهز. سيتوقف القبان والإرسال ويُعاد فتح البرنامج. تثبيت الآن؟",
                                    "قبان الغد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;
                StopReading();
                stoppedForInstall = true;
                var pending = Task.WhenAll(primary.Pending, returns.Pending, serialSession.Completion);
                if (await Task.WhenAny(pending, Task.Delay(10000)) != pending)
                    throw new IOException("Requests are still closing.");
                await pending;
                string runner = Path.Combine(stage, "runner.exe");
                File.Copy(Path.Combine(Application.StartupPath, "QabanAlGhad.Updater.exe"), runner, false);
                File.Copy(Path.Combine(Application.StartupPath, "QabanAlGhad.Updater.exe.config"), runner + ".config",
                          false);
                UpdateTransaction.Prepare(Application.StartupPath, stage);
                try
                {
                    Process.Start(
                        new ProcessStartInfo(runner, "--install \"" + Application.StartupPath + "\" " +
                                                         Process.GetCurrentProcess().Id) { UseShellExecute = false });
                }
                catch
                {
                    File.Delete(UpdateTransaction.JournalPath(Application.StartupPath));
                    throw;
                }
                updating = false;
                updateWindow.Close();
                Close();
            }
            catch (Exception ex)
            {
                if (!updateCancellation.IsCancellationRequested)
                {
                    WriteBackgroundLog("Update install: " + ex.GetType().Name);
                    updateInstallFailed = stoppedForInstall;
                    updateVersion.Text = UpdateError(ex);
                    MessageBox.Show(updateWindow,
                                    "لم يكتمل التحديث. " + UpdateError(ex) +
                                        (stoppedForInstall
                                             ? "\nالقبان متوقف. بعد معالجة السبب اضغط تشغيل لاستئناف القراءة والإرسال."
                                             : "\nلم تتغير حالة تشغيل القبان."),
                                    "قبان الغد");
                }
            }
            finally
            {
                updating = false;
                if (!IsDisposed)
                {
                    updateProgress.Visible = false;
                    ShowUpdateButton("يوجد تحديث", true);
                    checkUpdate.Enabled = true;
                }
                // Retain only stages that contain a recovery backup.
                if (stage != null && !Directory.Exists(Path.Combine(stage, "old")))
                    try
                    {
                        Directory.Delete(stage, true);
                    }
                    catch
                    {
                    }
            }
        }
    }
}
