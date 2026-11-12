using System;
using System.IO;
using System.IO.Ports;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Web.Script.Serialization;
using System.Collections.Generic;

namespace WeighbridgeBridge
{
    public sealed partial class MainForm : Form
    {
        TextBox endpoint = new TextBox(), company = new TextBox(), marker = new TextBox(), token = new TextBox(),
                returnsEndpoint = new TextBox(), returnsToken = new TextBox();
        NumericUpDown baud = new NumericUpDown(), bits = new NumericUpDown(), interval = new NumericUpDown(),
                      fresh = new NumericUpDown();
        ComboBox parity = new ComboBox(), port = new ComboBox();
        Button start = new ActionButton(), stop = new ActionButton(), save = new ActionButton();
        Label weight = new Label();
        TableLayoutPanel fields = new TableLayoutPanel();
        decimal latest;
        long received = -1;
        bool running;
        DateTime sampledAtUtc;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
        readonly string dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeighbridgeBridge");
        Settings settings;
        readonly OutputChannel primary, returns;
        bool closeApproved, closingInProgress;
        void CloseSafely(object sender, FormClosingEventArgs e)
        {
            if (closeApproved)
                return;
            e.Cancel = true;
            if (closingInProgress)
                return;
            if (!ResolveUnsavedSettings())
                return;
            closingInProgress = true;
            Enabled = false;
            // Queue shutdown after FormClosing returns, including when Close is called from Shown.
            BeginInvoke(new Action(async () => await CompleteClosing()));
        }
        async Task CompleteClosing()
        {
            updateCancellation.Cancel();
            StopReading();
            poll.Stop();
            try
            {
                var pending =
                    Task.WhenAll(primary.Pending, returns.Pending, serialSession.Completion, updatePreparation);
                if (await Task.WhenAny(pending, Task.Delay(3000)) == pending)
                    await pending;
                else
                    WriteBackgroundLog("انتهت مهلة إغلاق مهام الاتصال.");
            }
            catch (Exception)
            {
                WriteBackgroundLog("تعذر إكمال إغلاق مهام الاتصال.");
            }
            await logWriter.StopAsync(2000);
            closeApproved = true;
            Enabled = true;
            Close();
        }
        public MainForm() : this(null) {}
        public MainForm(string settingsDirectory)
        {
            if (settingsDirectory != null)
                dataDir = Path.GetFullPath(settingsDirectory);
            primary =
                new OutputChannel(OutputKind.Primary, "الأساسي", () => clock.ElapsedMilliseconds, WriteLog, SaveReply);
            returns = new OutputChannel(OutputKind.Returns, "القناة الثانية", () => clock.ElapsedMilliseconds,
                                        WriteBackgroundLog, SaveReply);
            serialSession = new SerialSession(() => clock.ElapsedMilliseconds);
            BuildInterface();
            BindSettingsChanges();
            settings = LoadSettings();
            try
            {
                if (!settingsNeedRepair && Bootstrap.Import(Application.StartupPath, SettingsPath, settings))
                    WriteBackgroundLog("تم تجهيز الربط.");
            }
            catch (Exception ex)
            {
                WriteBackgroundLog("تعذر استيراد الربط: " + ex.GetType().Name);
            }
            Apply(settings);
            BindUpdates();
            start.Click += (s, e) => StartReading();
            stop.Click += (s, e) => StopReading();
            save.Click += (s, e) => SaveSettings();
            poll.Interval = 100;
            poll.Tick += (s, e) =>
            {
                Tick(s, e);
                RefreshInterface();
            };
            poll.Start();
            FormClosing += CloseSafely;
            Disposed += (s, e) =>
            {
                poll.Dispose();
                fieldErrors.Dispose();
                helpTips.Dispose();
            };
        }
    }
}
