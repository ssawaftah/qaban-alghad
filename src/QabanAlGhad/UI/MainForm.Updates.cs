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
        readonly Button checkUpdate = new Button(), installUpdate = new Button();
        readonly CancellationTokenSource updateCancellation = new CancellationTokenSource();
        AvailableUpdate availableUpdate;
        bool updating, checkingUpdate;

        void BindUpdates()
        {
            checkUpdate.Click += async (s, e) => await CheckUpdate(true);
            installUpdate.Click += async (s, e) => await InstallUpdate();
            Shown += async (s, e) => await CheckUpdate(false);
            FormClosing += (s, e) => updateCancellation.Cancel();
        }

        async Task CheckUpdate(bool manual)
        {
            if (checkingUpdate || updating) return;
            checkingUpdate = true; checkUpdate.Enabled = false;
            try
            {
                availableUpdate = await ReleaseClient.Check(Assembly.GetExecutingAssembly().GetName().Version, updateCancellation.Token);
                if (IsDisposed) return;
                installUpdate.Visible = availableUpdate != null;
                if (manual && availableUpdate == null) MessageBox.Show("البرنامج محدّث.", "قبان الغد");
            }
            catch (Exception ex)
            {
                if (updateCancellation.IsCancellationRequested) return;
                WriteBackgroundLog("Update check: " + ex.GetType().Name);
                if (manual) MessageBox.Show("تعذر التحقق من التحديثات. حاول لاحقًا.", "قبان الغد");
            }
            finally { checkingUpdate = false; if (!IsDisposed) checkUpdate.Enabled = true; }
        }

        async Task InstallUpdate()
        {
            if (updating || availableUpdate == null) return;
            updating = true; installUpdate.Enabled = false; checkUpdate.Enabled = false;
            string stage = null;
            try
            {
                var update = availableUpdate;
                stage = UpdateTransaction.NewStage(Application.StartupPath);
                string zip = Path.Combine(stage, "package.zip");
                installUpdate.Text = "جارٍ التنزيل";
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
                if (!IsDisposed) { installUpdate.Text = "تحديث البرنامج"; installUpdate.Enabled = true; checkUpdate.Enabled = true; }
                // Retain only stages that contain a recovery backup.
                if (stage != null && !Directory.Exists(Path.Combine(stage, "old")))
                    try { Directory.Delete(stage, true); } catch { }
            }
        }
    }
}
