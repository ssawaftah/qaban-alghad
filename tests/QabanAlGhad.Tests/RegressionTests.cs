using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WeighbridgeBridge
{
    public static class RegressionTests
    {
        static int count;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new Exception("Regression: " + label);
            count++;
        }

        public static void Run()
        {
            count = 0;
            SettingsAndSecrets();
            Acknowledgements();
            Logging().GetAwaiter().GetResult();
            Serial().GetAwaiter().GetResult();
            Check(ReadingDescription.Rejection("1,US,12,kg") == "قراءة غير مستقرة", "channel unstable classified");
            Check(ReadingDescription.Rejection("ST,GS,12,lb") == "وحدة غير مدعومة", "unit rejection classified");
            Check(ReadingDescription.Rejection("garbage") == "قراءة غير صالحة", "malformed differs from unstable");
            Check(ReadingDescription.Rejection("[oversized serial frame]") == "إطار أطول من الحد المسموح",
                  "oversize classified");
            Console.WriteLine("PASS: " + count + " reliability regression checks");
        }

        static void SettingsAndSecrets()
        {
            string folder = Path.Combine(Path.GetTempPath(), "qaban-regression-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "settings.json");
                SettingsStore.Save(path, new Settings { Port = "COM42", Company = "7" });
                SettingsStore.Save(path, new Settings { Port = "COM43", Company = "8" });
                File.WriteAllText(path, "{\"Port\":\"COM6\",\"Company\":\"1\",\"Endpoint\":\"https://example.test/" +
                                            "weight\",\"Baud\":999999999999999999999}");
                bool rejected = false;
                try
                {
                    SettingsStore.Read(path);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }
                Check(rejected, "overflow is classified as corrupt settings");
                SettingsStore.Save(path, new Settings { Port = "COM44" });
                Check(SettingsStore.Read(path).Port == "COM44", "overflow corruption can be repaired");
                Check(SettingsStore.Read(path + ".bak").Port == "COM42", "repair keeps last good backup");
                string plain;
                Check(LocalSecrets.TryUnprotect("", out plain) && plain == "",
                      "absent secret differs from decryption failure");
                Check(!LocalSecrets.TryUnprotect("broken-protected-secret", out plain), "broken secret detected");
                Check(LocalSecrets.UpdateProtected("broken-protected-secret", "") == "broken-protected-secret",
                      "blank field preserves inaccessible ciphertext");
                string updated = LocalSecrets.UpdateProtected("broken-protected-secret", "replacement-test-only");
                Check(LocalSecrets.TryUnprotect(updated, out plain) && plain == "replacement-test-only",
                      "explicit replacement repairs credential");
                Check(LocalSecrets.UpdateProtected(updated, "") == "",
                      "readable credential can be deliberately cleared");
                SettingsStore.Save(path, new Settings { PrimaryTokenProtected = "broken-protected-secret" });
                Check(SettingsStore.Read(path).PrimaryTokenProtected == "broken-protected-secret",
                      "stored inaccessible ciphertext remains intact");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        static void Acknowledgements()
        {
            Check(ResponseRules.ReturnsIssue(200, "application/json", "{\"accepted\":true,\"weightKg\":12}", 120) ==
                      "وزن الإقرار لا يطابق العينة",
                  "wrong echo has specific diagnosis");
            Check(ResponseRules.ReturnsIssue(200, "application/json", "{\"accepted\":true}", 120) ==
                      "وزن الإقرار غير موجود أو غير رقمي",
                  "missing echo diagnosed");
            Check(ResponseRules.ReturnsIssue(200, "application/json", "{}", 120) == "الإقرار accepted:true غير موجود",
                  "missing acknowledgement diagnosed");
            Check(ResponseRules.ReturnsIssue(200, "application/json", "not-json", 120) == "استجابة JSON غير صالحة",
                  "invalid JSON diagnosed");
            Check(ResponseRules.ReturnsIssue(200, "text/html", "login", 120) == "استجابة HTML بدل إقرار JSON",
                  "HTML diagnosed");
            Check(ResponseRules.ReturnsIssue(200, "application/json", "{\"accepted\":true,\"weightKg\":120}", 120) ==
                      null,
                  "valid acknowledgement has no issue");
            var messages = new List<string>();
            var channel = new OutputChannel(OutputKind.Returns, "test", () => 0, messages.Add, null);
            channel.Configure("https://example.test/weight", "", "1", 3000);
            Check(!channel.Ready && messages.Count == 1 && messages[0].Contains("رمز API مطلوب"),
                  "configuration failure logged once");
            Check(!messages[0].Contains("example.test"), "configuration diagnostic excludes URL");
        }

        static async Task Logging()
        {
            var writer = new BackgroundLogWriter();
            var release = new TaskCompletionSource<bool>();
            int saved = 0;
            writer.Enqueue(() =>
                           {
                               release.Task.GetAwaiter().GetResult();
                               Interlocked.Increment(ref saved);
                           });
            writer.Enqueue(() => Interlocked.Increment(ref saved));
            Task<bool> closing = writer.StopAsync(2000);
            Check(!closing.IsCompleted, "shutdown waits for pending log work");
            writer.Enqueue(() => Interlocked.Increment(ref saved));
            Check(writer.Dropped == 1, "work after shutdown counted and refused");
            release.SetResult(true);
            Check(await closing && saved == 2, "pending logs drained in order");
            var broken = new BackgroundLogWriter();
            broken.Enqueue(() =>
                           { throw new IOException("test-only"); });
            broken.Enqueue(() => Interlocked.Increment(ref saved));
            Check(await broken.StopAsync(2000) && broken.Errors == 1 && saved == 3,
                  "failed write does not lose later events");
            var stalled = new BackgroundLogWriter();
            var unblock = new TaskCompletionSource<bool>();
            stalled.Enqueue(() => unblock.Task.GetAwaiter().GetResult());
            Check(!await stalled.StopAsync(30), "stalled storage has bounded shutdown");
            unblock.SetResult(true);
            Check(await stalled.StopAsync(2000), "stalled writer can finish after timeout");
        }

        sealed class FakePort : ISerialConnection
        {
            readonly Queue<string> parts = new Queue<string>();
            public bool Closed;
            public void Push(string value)
            {
                lock (parts) parts.Enqueue(value);
            }
            public void Open() {}
            public string ReadExisting()
            {
                lock (parts) return parts.Count == 0 ? "" : parts.Dequeue();
            }
            public void Dispose()
            {
                Closed = true;
            }
        }

        static async Task<SerialSample> Next(SerialSession session)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 2500)
            {
                string message;
                bool lost;
                var value = session.Take(out message, out lost);
                if (value != null)
                    return value;
                await Task.Delay(10);
            }
            throw new Exception("Serial fixture timed out");
        }

        static async Task Serial()
        {
            var timer = Stopwatch.StartNew();
            var fake = new FakePort();
            var session = new SerialSession(() => timer.ElapsedMilliseconds, settings => fake);
            fake.Push("g\rST,GS,-12");
            fake.Push("0kg\r");
            session.Start(new Settings());
            try
            {
                bool refused = false;
                try
                {
                    session.Start(new Settings());
                }
                catch (InvalidOperationException)
                {
                    refused = true;
                }
                Check(refused, "second serial worker refused");
                var first = await Next(session);
                Check(first.Valid && first.Weight == -120 && first.Utc.Kind == DateTimeKind.Utc,
                      "serial worker joins fragments and timestamps arrival");
                fake.Push("US,GS,-120kg\r");
                Check(!(await Next(session)).Valid, "unstable arrival invalidates previous sample");
                fake.Push("ST,GS,-120kg\r");
                var second = await Next(session);
                Check(second.Valid && second.Weight == -120 && second.At > first.At,
                      "same weight new arrival refreshes sample");
            }
            finally
            {
                session.Stop();
                await session.Completion;
            }
            Check(fake.Closed, "stop disposes connection before completing");
            session.Start(new Settings());
            session.Stop();
            await session.Completion;
            Check(session.Completion.IsCompleted, "session restarts after complete shutdown");
        }
    }
}
