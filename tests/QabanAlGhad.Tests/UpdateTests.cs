using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace WeighbridgeBridge
{
    public static class UpdateTests
    {
        static int count;
        static void Check(bool ok, string label)
        {
            if (!ok)
                throw new Exception("Update test failed: " + label);
            count++;
        }
        static void Reject(Action action, string label)
        {
            try
            {
                action();
            }
            catch
            {
                count++;
                return;
            }
            throw new Exception("Expected rejection: " + label);
        }
        static string Release(string version, string url = null, string digest = null, long size = 100)
        {
            return new JavaScriptSerializer().Serialize(
                new { draft = false, prerelease = false, tag_name = "v" + version,
                      assets =
                          new[] { new { name = "QabanAlGhad-" + version + ".zip",
                                        browser_download_url =
                                            url ?? "https://github.com/ssawaftah/qaban-alghad/releases/download/v" +
                                                       version + "/QabanAlGhad-" + version + ".zip",
                                        size = size, digest = digest ?? "sha256:" + new string('a', 64) } } });
        }
        static void MakeZip(string path, string entry)
        {
            using (var z = ZipFile.Open(path, ZipArchiveMode.Create)) using (
                var writer = new StreamWriter(z.CreateEntry(entry).Open())) writer.Write("test");
        }
        public static void Run()
        {
            count = 0;
            Check(ReleaseClient.Parse(Release("0.10.0"), new Version("0.9.2.0")).Version == "0.10.0",
                  "numeric version ordering");
            Check(ReleaseClient.Parse(Release("0.10.0"), new Version("0.10.0.0")) == null, "same version ignored");
            Check(ReleaseClient.Parse(Release("0.9.9"), new Version("0.10.0.0")) == null, "downgrade ignored");
            Reject(() =>
                       ReleaseClient.Parse(Release("0.11.0", "https://evil.example/update.zip"), new Version("0.10.0")),
                   "foreign source");
            Reject(() => ReleaseClient.Parse(Release("0.11.0", digest: "missing"), new Version("0.10.0")),
                   "missing publisher digest");
            Reject(() => ReleaseClient.Parse(Release("0.11.0", size: ReleaseClient.MaximumPackageBytes + 1),
                                             new Version("0.10.0")),
                   "package limit");
            Reject(() => ReleaseClient.Parse("{}", new Version("0.10.0")), "malformed metadata");
            string root = Path.Combine(Path.GetTempPath(), "qaban-update-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string stage = UpdateTransaction.NewStage(root), zip = Path.Combine(stage, "test.zip");
                MakeZip(zip, "QabanAlGhad-0.11.0/../../settings.json");
                Reject(() => UpdateTransaction.Extract(zip, stage, "0.11.0", ReleaseClient.Hash(zip)),
                       "path traversal");
                Reject(() => UpdateTransaction.Extract(zip, stage, "0.11.0", new string('0', 64)), "tampered ZIP");
                string second = UpdateTransaction.NewStage(root), other = Path.Combine(second, "test.zip");
                MakeZip(other, "QabanAlGhad-0.11.0/settings.json");
                Reject(() => UpdateTransaction.Extract(other, second, "0.11.0", ReleaseClient.Hash(other)),
                       "settings overwrite");
                string third = UpdateTransaction.NewStage(root), partial = Path.Combine(third, "test.zip");
                MakeZip(partial, "QabanAlGhad-0.11.0/README.md");
                Reject(() => UpdateTransaction.Extract(partial, third, "0.11.0", ReleaseClient.Hash(partial)),
                       "incomplete runtime");
                string readyStage = UpdateTransaction.NewStage(root);
                Directory.CreateDirectory(Path.Combine(readyStage, "new"));
                foreach (string name in UpdateTransaction.RuntimeFiles)
                {
                    File.WriteAllText(Path.Combine(root, name), "old:" + name);
                    File.WriteAllText(Path.Combine(readyStage, "new", name), "new:" + name);
                }
                File.WriteAllText(Path.Combine(root, "settings.json"), "private-settings");
                var journal = UpdateTransaction.Prepare(root, readyStage);
                Check(UpdateTransaction.Read(root).State == "prepared", "durable journal");
                UpdateTransaction.Apply(root, journal);
                Check(File.ReadAllText(Path.Combine(root, "QabanAlGhad.exe")).StartsWith("new:"), "install runtime");
                UpdateTransaction.Restore(root, journal);
                Check(File.ReadAllText(Path.Combine(root, "QabanAlGhad.exe")).StartsWith("old:"), "rollback runtime");
                Check(File.ReadAllText(Path.Combine(root, "settings.json")) == "private-settings",
                      "settings preserved");
                Check(!File.Exists(UpdateTransaction.JournalPath(root)), "recovery cleared");
                Reject(() => UpdateTransaction.Write(root, new UpdateJournal { Stage = Path.GetTempPath(),
                                                                               Token = new string('a', 32),
                                                                               State = "prepared" }),
                       "foreign recovery directory");
            }
            finally
            {
                Directory.Delete(root, true);
            }
            Console.WriteLine("PASS: " + count + " update metadata/integrity/extraction/recovery checks");
        }

        static void CloseTestApp(string root)
        {
            foreach (var child in Process.GetProcesses())
            {
                try
                {
                    if (!string.Equals(child.MainModule.FileName, Path.Combine(root, "QabanAlGhad.exe"),
                                       StringComparison.OrdinalIgnoreCase))
                        continue;
                    child.CloseMainWindow();
                    if (!child.WaitForExit(10000))
                    {
                        child.Kill();
                        child.WaitForExit();
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
                finally
                {
                    child.Dispose();
                }
            }
        }

        public static int OnlineLifecycle(string root)
        {
            try
            {
                Version previous = AssemblyName.GetAssemblyName(Path.Combine(root, "QabanAlGhad.exe")).Version;
                var cancellation = System.Threading.CancellationToken.None;
                var release = ReleaseClient.Check(previous, cancellation).GetAwaiter().GetResult();
                for (int attempt = 0; release == null && attempt < 5; attempt++)
                {
                    System.Threading.Thread.Sleep(3000);
                    release = ReleaseClient.Check(previous, cancellation).GetAwaiter().GetResult();
                }
                if (release == null)
                    throw new Exception("No newer public release available for the online upgrade test.");
                string stage = UpdateTransaction.NewStage(root), zip = Path.Combine(stage, "package.zip");
                ReleaseClient.Fetch(release, zip, cancellation).GetAwaiter().GetResult();
                UpdateTransaction.Extract(zip, stage, release.Version, release.Sha256);
                File.WriteAllText(Path.Combine(root, "settings.json"), "preserve-test-settings");
                string runner = Path.Combine(stage, "runner.exe");
                File.Copy(Path.Combine(root, "QabanAlGhad.Updater.exe"), runner);
                File.Copy(Path.Combine(root, "QabanAlGhad.Updater.exe.config"), runner + ".config");
                UpdateTransaction.Prepare(root, stage);
                using (var process = Process.Start(new ProcessStartInfo(
                           runner, "--install \"" + root + "\" 2147483647") { UseShellExecute = false }))
                {
                    if (!process.WaitForExit(45000))
                    {
                        process.Kill();
                        throw new Exception("Online update timed out.");
                    }
                    Check(process.ExitCode == 0, "public release installation succeeded");
                }
                Check(AssemblyName.GetAssemblyName(Path.Combine(root, "QabanAlGhad.exe")).Version.ToString(3) ==
                          release.Version,
                      "new published binary installed");
                Check(File.ReadAllText(Path.Combine(root, "settings.json")) == "preserve-test-settings",
                      "online install preserves settings");
                Check(!File.Exists(UpdateTransaction.JournalPath(root)), "online install health confirmed");
                Console.WriteLine("PASS: real public GitHub update " + previous + " -> " + release.Version +
                                  "; no serial port or production weights used");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }
            finally
            {
                CloseTestApp(root);
            }
        }

        public static int Lifecycle()
        {
            try
            {
                string output = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                foreach (int scenario in new[] { 0, 1, 2 })
                {
                    Console.WriteLine("Windows update scenario: " + scenario);
                    bool fail = scenario == 1;
                    string root =
                        Path.Combine(Path.GetTempPath(), "qaban-updater-lifecycle-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(root);
                    try
                    {
                        // Real Windows processes and files; no scale or production API is involved.
                        File.Copy(Path.Combine(output, "UpdateHealthHost.exe"), Path.Combine(root, "QabanAlGhad.exe"));
                        File.Copy(Path.Combine(output, "QabanAlGhad.Updater.exe"),
                                  Path.Combine(root, "QabanAlGhad.Updater.exe"));
                        File.Copy(Path.Combine(output, "QabanAlGhad.exe.config"),
                                  Path.Combine(root, "QabanAlGhad.exe.config"));
                        File.Copy(Path.Combine(output, "QabanAlGhad.Updater.exe.config"),
                                  Path.Combine(root, "QabanAlGhad.Updater.exe.config"));
                        File.WriteAllText(Path.Combine(root, "settings.json"), "private");
                        string stage = UpdateTransaction.NewStage(root), next = Path.Combine(stage, "new");
                        Directory.CreateDirectory(next);
                        foreach (string name in UpdateTransaction.RuntimeFiles)
                            File.Copy(Path.Combine(root, name), Path.Combine(next, name));
                        File.Copy(Path.Combine(output, fail            ? "UpdateFailedHost.exe"
                                                       : scenario == 2 ? "QabanAlGhad.exe"
                                                                       : "UpdateHealthHost.exe"),
                                  Path.Combine(next, "QabanAlGhad.exe"), true);
                        string runner = Path.Combine(stage, "runner.exe");
                        File.Copy(Path.Combine(root, "QabanAlGhad.Updater.exe"), runner);
                        File.Copy(Path.Combine(root, "QabanAlGhad.Updater.exe.config"), runner + ".config");
                        UpdateTransaction.Prepare(root, stage);
                        using (var process = Process.Start(new ProcessStartInfo(
                                   runner, "--install \"" + root + "\" 2147483647") { UseShellExecute = false }))
                        {
                            if (!process.WaitForExit(45000))
                            {
                                process.Kill();
                                process.WaitForExit();
                                throw new Exception("Updater did not finish, scenario " + scenario);
                            }
                            string diagnostic = Path.Combine(root, "update-error.log");
                            if (File.Exists(diagnostic))
                                Console.WriteLine(File.ReadAllText(diagnostic));
                            Check(process.ExitCode == (fail ? 1 : 0), "real updater exit");
                        }
                        Check(!File.Exists(UpdateTransaction.JournalPath(root)), "real updater journal complete");
                        Check(File.ReadAllText(Path.Combine(root, "settings.json")) == "private",
                              "real updater settings untouched");
                        Check(ReleaseClient.Hash(Path.Combine(root, "QabanAlGhad.exe")) ==
                                  ReleaseClient.Hash(
                                      Path.Combine(output, scenario == 2 ? "QabanAlGhad.exe" : "UpdateHealthHost.exe")),
                              "real updater active binary");
                        {
                            foreach (var child in Process.GetProcesses())
                            {
                                try
                                {
                                    if (!string.Equals(child.MainModule.FileName, Path.Combine(root, "QabanAlGhad.exe"),
                                                       StringComparison.OrdinalIgnoreCase))
                                        continue;
                                    child.CloseMainWindow();
                                    if (!child.WaitForExit(10000))
                                    {
                                        child.Kill();
                                        child.WaitForExit();
                                    }
                                }
                                catch (System.ComponentModel.Win32Exception)
                                {
                                }
                                catch (InvalidOperationException)
                                {
                                }
                                finally
                                {
                                    child.Dispose();
                                }
                            }
                        }
                    }
                    finally
                    {
                        CloseTestApp(root);
                        for (int attempt = 0;; attempt++)
                        {
                            try
                            {
                                Directory.Delete(root, true);
                                break;
                            }
                            catch (IOException)
                            {
                                if (attempt == 10)
                                    throw;
                                System.Threading.Thread.Sleep(200);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                if (attempt == 10)
                                    throw;
                                System.Threading.Thread.Sleep(200);
                            }
                        }
                    }
                }
                Console.WriteLine("PASS: Windows updater real-process install and rollback");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return 1;
            }
        }
    }
}
