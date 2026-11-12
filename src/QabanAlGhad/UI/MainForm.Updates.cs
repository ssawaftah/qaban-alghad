using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed partial class MainForm
    {
        readonly Button checkUpdate = new Button();
        Label versionCredit;
        static readonly Version CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        AvailableUpdate availableUpdate;
        bool updating, checkingUpdate;

        void BindUpdates()
        {
            checkUpdate.Click += async (s, e) =>
            {
                if (availableUpdate == null) await CheckUpdate();
                else await InstallUpdate();
            };
            FormClosing += (s, e) => updateCancellation.Cancel();
        }

        void ShowUpdateButton(string caption, bool offerUpdate)
        {
            checkUpdate.Text = caption;
            checkUpdate.AccessibleName = caption;
            checkUpdate.BackColor = offerUpdate ? Accent : System.Drawing.Color.FromArgb(231, 237, 241);
            checkUpdate.ForeColor = offerUpdate ? System.Drawing.Color.White : Ink;
            checkUpdate.FlatAppearance.MouseOverBackColor = offerUpdate
                ? System.Drawing.Color.FromArgb(0, 105, 98)
                : System.Drawing.Color.FromArgb(219, 229, 235);
        }

        async Task CheckUpdate()
        {
            if (checkingUpdate || updating) return;
            checkingUpdate = true;
            checkUpdate.Enabled = false;
            ShowUpdateButton("جارٍ التحقق", false);
            try
            {
                availableUpdate = await ReleaseClient.Check(CurrentVersion, updateCancellation.Token);
                if (IsDisposed || updateCancellation.IsCancellationRequested) return;
                ShowUpdateButton(availableUpdate == null ? "لا توجد تحديثات" : "يوجد تحديث", availableUpdate != null);
            }
            catch (Exception ex)
            {
                if (IsDisposed || updateCancellation.IsCancellationRequested) return;
                availableUpdate = null;
                WriteBackgroundLog("Update check: " + ex.GetType().Name);
                ShowUpdateButton("تعذر التحقق", false);
            }
            finally
            {
                checkingUpdate = false;
                if (!IsDisposed) checkUpdate.Enabled = true;
            }
        }

        async Task InstallUpdate()
        {
            if (updating || checkingUpdate || availableUpdate == null) return;
            updating = true; checkUpdate.Enabled = false;
            string stage = null;
            try
            {
                var update = availableUpdate;
                stage = UpdateTransaction.NewStage(Application.StartupPath);
                string zip = Path.Combine(stage, "package.zip");
                ShowUpdateButton("جارٍ التنزيل", false);
                await ReleaseClient.Fetch(update, zip, updateCancellation.Token);
                // Extraction and hashing run away from the serial polling/UI thread.
                await Task.Run(() => UpdateTransaction.Extract(zip, stage, update.Version, update.Sha256));
                if (updateCancellation.IsCancellationRequested) return;
                if (MessageBox.Show("التحديث " + update.Version + " جاهز. سيتوقف القبان والإرسال ويُعاد فتح البرنامج. تثبيت الآن؟", "قبان الغد", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                StopReading();
                var pending = Task.WhenAll(primary.Pending, returns.Pending);
                if (await Task.WhenAny(pending, Task.Delay(10000)) != pending) throw new IOException("Requests are still closing.");
                await pending;
                string runner = Path.Combine(stage, "runner.exe");
                File.Copy(Path.Combine(Application.StartupPath, "QabanAlGhad.Updater.exe"), runner, false);
                File.Copy(Path.Combine(Application.StartupPath, "QabanAlGhad.Updater.exe.config"), runner + ".config", false);
                UpdateTransaction.Prepare(Application.StartupPath, stage);
                try
                {
                    Process.Start(new ProcessStartInfo(runner, "--install \"" + Application.StartupPath + "\" " + Process.GetCurrentProcess().Id) { UseShellExecute = false });
                }
                catch { File.Delete(UpdateTransaction.JournalPath(Application.StartupPath)); throw; }
                Close();
            }
            catch (Exception ex)
            {
                if (!updateCancellation.IsCancellationRequested)
                {
                    WriteBackgroundLog("Update install: " + ex.GetType().Name);
                    MessageBox.Show("لم يكتمل التحديث. النسخة الحالية وإعداداتك محفوظة.\n" + ex.Message, "قبان الغد");
                }
            }
            finally
            {
                updating = false;
                if (!IsDisposed) { ShowUpdateButton("يوجد تحديث", true); checkUpdate.Enabled = true; }
                // Retain only stages that contain a recovery backup.
                if (stage != null && !Directory.Exists(Path.Combine(stage, "old")))
                    try { Directory.Delete(stage, true); } catch { }
            }
        }
    }
}
