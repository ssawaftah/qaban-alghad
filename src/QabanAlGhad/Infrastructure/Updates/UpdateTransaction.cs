using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Web.Script.Serialization;

namespace WeighbridgeBridge
{
    public sealed class UpdateJournal
    {
        public string Stage, Token, State;
    }

    // Compiled into both the application and the standalone updater.
    public static class UpdateTransaction
    {
        public static readonly string[] RuntimeFiles = { "QabanAlGhad.exe", "QabanAlGhad.exe.config",
                                                         "QabanAlGhad.Updater.exe", "QabanAlGhad.Updater.exe.config" };
        static readonly HashSet<string> PackageFiles = new HashSet<string>(
            new[] { "QabanAlGhad.exe", "QabanAlGhad.exe.config", "QabanAlGhad.Updater.exe",
                    "QabanAlGhad.Updater.exe.config", "QabanAlGhad.ico", "Start.cmd", "README.md", "SHA256.json" },
            StringComparer.OrdinalIgnoreCase);
        public static string JournalPath(string app)
        {
            return Path.Combine(app, ".qaban-update.json");
        }

        public static void PlainDirectory(string path)
        {
            for (var dir = new DirectoryInfo(Path.GetFullPath(path)); dir != null; dir = dir.Parent)
                if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked update directories are not supported.");
        }

        public static string NewStage(string app)
        {
            PlainDirectory(app);
            string stage = Path.Combine(app, ".qaban-update", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            return stage;
        }

        public static void Extract(string zip, string stage, string version, string expectedHash)
        {
            PlainDirectory(stage);
            if (ReleaseClient.Hash(zip) != expectedHash)
                throw new InvalidDataException("Package hash mismatch.");
            string dest = Path.Combine(stage, "new");
            Directory.CreateDirectory(dest);
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string prefix = "QabanAlGhad-" + version + "/";
            using (var archive = ZipFile.OpenRead(zip))
            {
                if (archive.Entries.Count > 20)
                    throw new InvalidDataException("Too many ZIP entries.");
                long total = 0;
                foreach (var entry in archive.Entries)
                {
                    string name = entry.FullName.Replace('\\', '/');
                    if (name == prefix && entry.Length == 0)
                        continue;
                    if (!name.StartsWith(prefix, StringComparison.Ordinal))
                        throw new InvalidDataException("Invalid package root.");
                    name = name.Substring(prefix.Length);
                    var attributes = typeof(ZipArchiveEntry).GetProperty("ExternalAttributes");
                    int external = attributes == null ? 0 : Convert.ToInt32(attributes.GetValue(entry, null));
                    int unixType = (external >> 16) & 0xF000;
                    if (!PackageFiles.Contains(name) || name.IndexOf('/') >= 0 || !found.Add(name) ||
                        unixType == 0xA000 || (external & (int)FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Unsafe ZIP entry.");
                    total += entry.Length;
                    if (entry.Length < 0 || total > ReleaseClient.MaximumPackageBytes)
                        throw new InvalidDataException("Expanded package too large.");
                    entry.ExtractToFile(Path.Combine(dest, name), false);
                }
            }
            foreach (string file in RuntimeFiles)
                if (!found.Contains(file))
                    throw new InvalidDataException("Incomplete runtime package.");
            if (AssemblyName.GetAssemblyName(Path.Combine(dest, "QabanAlGhad.exe")).Version.ToString(3) != version)
                throw new InvalidDataException("Package version mismatch.");
        }

        static void Validate(string app, UpdateJournal journal)
        {
            PlainDirectory(app);
            string parent = Path.Combine(Path.GetFullPath(app), ".qaban-update") + Path.DirectorySeparatorChar;
            string stage = Path.GetFullPath(journal.Stage);
            if (!stage.StartsWith(parent, StringComparison.OrdinalIgnoreCase) ||
                Path.GetDirectoryName(stage + Path.DirectorySeparatorChar) != stage ||
                Path.GetDirectoryName(stage) != parent.TrimEnd(Path.DirectorySeparatorChar) || journal.Token == null ||
                journal.Token.Length != 32)
                throw new InvalidDataException("Invalid recovery journal.");
            PlainDirectory(stage);
        }

        public static UpdateJournal Read(string app)
        {
            string file = JournalPath(app);
            if (!File.Exists(file))
                return null;
            if (new FileInfo(file).Length > 4096)
                throw new InvalidDataException("Oversized update journal.");
            var journal = new JavaScriptSerializer().Deserialize<UpdateJournal>(File.ReadAllText(file));
            if (journal == null)
                throw new InvalidDataException("Invalid update journal.");
            Validate(app, journal);
            return journal;
        }

        public static void Write(string app, UpdateJournal journal)
        {
            Validate(app, journal);
            string file = JournalPath(app), temp = file + ".tmp";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(journal));
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(file))
                File.Replace(temp, file, null);
            else
                File.Move(temp, file);
        }

        public static UpdateJournal Prepare(string app, string stage)
        {
            var journal = new UpdateJournal { Stage = stage, Token = Guid.NewGuid().ToString("N"), State = "prepared" };
            Validate(app, journal);
            if (File.Exists(JournalPath(app)))
                throw new IOException("An unfinished update must be recovered first.");
            string backup = Path.Combine(stage, "old");
            Directory.CreateDirectory(backup);
            foreach (string file in RuntimeFiles)
            {
                string source = Path.Combine(app, file);
                if (!File.Exists(source))
                    throw new FileNotFoundException("Missing installed runtime.", file);
                if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked runtime file.");
                File.Copy(source, Path.Combine(backup, file), false);
            }
            Write(app, journal);
            return journal;
        }

        static void AtomicCopy(string source, string target)
        {
            string temp = target + ".update-next";
            if ((File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) ||
                (File.Exists(temp) && (File.GetAttributes(temp) & FileAttributes.ReparsePoint) != 0))
                throw new IOException("Linked runtime target.");
            using (var input = File.OpenRead(source)) using (
                var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(true);
            }
            File.Replace(temp, target, null);
        }

        public static void Apply(string app, UpdateJournal journal)
        {
            Validate(app, journal);
            journal.State = "copying";
            Write(app, journal);
            foreach (string file in RuntimeFiles)
                AtomicCopy(Path.Combine(journal.Stage, "new", file), Path.Combine(app, file));
            journal.State = "launched";
            Write(app, journal);
        }

        public static void Restore(string app, UpdateJournal journal)
        {
            Validate(app, journal);
            // Backups are complete before any installed file is changed.
            foreach (string file in RuntimeFiles)
                if (!File.Exists(Path.Combine(journal.Stage, "old", file)))
                    throw new IOException("Incomplete recovery backup.");
            foreach (string file in RuntimeFiles)
                AtomicCopy(Path.Combine(journal.Stage, "old", file), Path.Combine(app, file));
            File.Delete(JournalPath(app));
        }

        public static bool Startup(string app, string[] args)
        {
            var journal = Read(app);
            if (journal == null)
                return false;
            bool updated = args.Length == 2 && args[0] == "--updated" && args[1] == journal.Token;
            if (updated && journal.State == "launched")
                return true;
            // A power loss or interrupted installer: never continue with a mixed installation.
            // The running EXE cannot replace itself on Windows; recovery is delegated to the staged runner.
            string runner = Path.Combine(journal.Stage, "runner.exe");
            if (!File.Exists(runner))
                throw new IOException("Recovery runner missing. Use the saved previous package.");
            Process.Start(new ProcessStartInfo(runner, "--recover \"" + app + "\" " +
                                                           Process.GetCurrentProcess().Id) { UseShellExecute = false });
            return false;
        }

        public static void Ready(string app)
        {
            var journal = Read(app);
            if (journal == null || journal.State != "launched")
                return;
            string receipt = Path.Combine(journal.Stage, "ready");
            string temporaryReceipt = receipt + ".tmp";
            File.WriteAllText(temporaryReceipt, journal.Token);
            File.Move(temporaryReceipt, receipt);
        }
    }
}
