using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WeighbridgeBridge;

internal static class UpdaterProgram
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length != 3) return 2;
        string identity;
        using (var hash = System.Security.Cryptography.SHA256.Create())
            identity = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(args[1]).ToLowerInvariant()))).Replace("-", "");
        bool first;
        using (var gate = new Mutex(true, "QabanAlGhad.Update." + identity, out first))
        {
            if (!first) return 3;
            return Run(args);
        }
    }

    static int Run(string[] args)
    {
        string app = null; UpdateJournal journal = null;
        try
        {
            if (args.Length != 3 || (args[0] != "--install" && args[0] != "--recover")) throw new ArgumentException("Invalid updater invocation.");
            app = Path.GetFullPath(args[1]);
            journal = UpdateTransaction.Read(app);
            if (journal == null) throw new IOException("Missing update journal.");
            string self = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(self), Path.Combine(journal.Stage, "runner.exe"), StringComparison.OrdinalIgnoreCase)) throw new IOException("Updater must run from staging.");
            int pid = int.Parse(args[2]);
            Process parent = null;
            try { parent = Process.GetProcessById(pid); } catch (ArgumentException) { }
            if (parent != null)
            {
                using (parent)
                {
                    if (!parent.HasExited && !string.Equals(parent.MainModule.FileName, Path.Combine(app, "QabanAlGhad.exe"), StringComparison.OrdinalIgnoreCase)) throw new IOException("Unexpected parent process.");
                    if (!parent.WaitForExit(30000)) throw new IOException("Close the weighbridge program before updating.");
                }
            }
            if (args[0] == "--recover")
            {
                UpdateTransaction.Restore(app, journal);
                Process.Start(Path.Combine(app, "QabanAlGhad.exe"));
                return 0;
            }
            if (journal.State != "prepared") throw new IOException("Installation is not prepared.");
            UpdateTransaction.Apply(app, journal);
            using (var child = Process.Start(new ProcessStartInfo(Path.Combine(app, "QabanAlGhad.exe"), "--updated " + journal.Token) { UseShellExecute = false, WorkingDirectory = app }))
            {
                string receipt = Path.Combine(journal.Stage, "ready");
                for (int i = 0; i < 300; i++)
                {
                    if (File.Exists(receipt) && File.ReadAllText(receipt) == journal.Token)
                    {
                        // Retain backups for manual rollback, but clear the pending transaction.
                        File.Delete(UpdateTransaction.JournalPath(app));
                        return 0;
                    }
                    if (child.HasExited) break;
                    Thread.Sleep(100);
                }
                if (!child.HasExited) { child.Kill(); child.WaitForExit(10000); }
            }
            UpdateTransaction.Restore(app, journal);
            Process.Start(Path.Combine(app, "QabanAlGhad.exe"));
            File.WriteAllText(Path.Combine(app, "update-status.log"), "Update failed startup; previous version restored.");
            return 1;
        }
        catch (Exception ex)
        {
            // If copying began, keep the journal/backups so next startup enters recovery.
            try { if (app != null) File.WriteAllText(Path.Combine(app, "update-error.log"), DateTime.UtcNow.ToString("o") + " " + ex); } catch { }
            try
            {
                if (journal != null && (journal.State == "copying" || journal.State == "launched"))
                {
                    UpdateTransaction.Restore(app, journal);
                    Process.Start(Path.Combine(app, "QabanAlGhad.exe"));
                }
                else if (journal != null && journal.State == "prepared") File.Delete(UpdateTransaction.JournalPath(app));
            }
            catch (Exception recoveryError) { try { File.AppendAllText(Path.Combine(app, "update-error.log"), Environment.NewLine + recoveryError); } catch { } }
            return 1;
        }
    }
}
