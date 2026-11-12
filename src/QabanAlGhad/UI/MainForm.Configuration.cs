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
    public sealed partial class MainForm
    {
        static void ConfigureNumber(NumericUpDown n, int min, int max)
        {
            n.Minimum = min;
            n.Maximum = max;
            n.Width = 100;
        }
        static FlowLayoutPanel Row(params Control[] controls)
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
            foreach (var c in controls)
                p.Controls.Add(c);
            return p;
        }
        void AddField(string text, Control c)
        {
            int r = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            fields.Controls.Add(new Label { Text = text, AutoSize = true, Anchor = AnchorStyles.Right }, 0, r);
            c.Dock = DockStyle.Fill;
            fields.Controls.Add(c, 1, r);
        }
        bool settingsNeedRepair, settingsDirty, applyingSettings, primaryCredentialUnavailable;
        readonly ErrorProvider fieldErrors = new ErrorProvider();
        readonly ToolTip helpTips = new ToolTip();

        void BindSettingsChanges()
        {
            fieldErrors.ContainerControl = this;
            foreach (Control input in new Control[] { port, endpoint, company, token, baud, bits, parity, fresh })
            {
                input.TextChanged += (sender, e) =>
                {
                    if (!applyingSettings)
                    {
                        settingsDirty = true;
                        save.Text = "حفظ وتطبيق *";
                        fieldErrors.SetError((Control)sender, "");
                        SetFieldError((Control)sender, false);
                    }
                };
            }
        }
        static void SetFieldError(Control input, bool failed)
        {
            var host = input.Parent as InputSurface;
            if (host != null)
            {
                host.HasError = failed;
                host.Invalidate();
            }
        }
        bool ResolveUnsavedSettings()
        {
            if (!settingsDirty)
                return true;
            var answer = AppMessage.Show(this, "لديك تغييرات لم تُحفظ بعد. هل تريد حفظها وتطبيقها قبل المتابعة؟",
                                         "الإعدادات", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Cancel)
                return false;
            if (answer == DialogResult.Yes)
                return SaveSettings();
            Apply(settings);
            return true;
        }
        bool SaveSettings()
        {
            try
            {
                fieldErrors.Clear();
                foreach (Control input in new Control[] { port, endpoint, company, token, baud, bits, parity, fresh })
                    SetFieldError(input, false);
                var cfg = ReadSettings();
                SettingsStore.Save(SettingsPath, cfg);
                settingsNeedRepair = false;
                settings = cfg;
                Apply(cfg);
                WriteLog("تم حفظ الإعدادات وتطبيقها؛ رموز الربط مشفرة لحساب Windows الحالي.");
                return true;
            }
            catch (Exception ex)
            {
                AppMessage.Show(this,
                                ex is InvalidDataException
                                    ? ex.Message
                                    : "تعذر حفظ الإعدادات. تحقق من صلاحية الكتابة ثم أعد المحاولة.",
                                "تعذر حفظ الإعدادات");
                return false;
            }
        }
        void RefreshPorts()
        {
            string selected = port.Text;
            bool wasApplying = applyingSettings;
            applyingSettings = true;
            try
            {
                var names = SerialPort.GetPortNames();
                Array.Sort(names, StringComparer.OrdinalIgnoreCase);
                port.Items.Clear();
                port.Items.AddRange(names);
                port.Text = selected;
            }
            catch (Exception)
            {
                helpTips.SetToolTip(port, "تعذر تعداد المنافذ. يمكن إدخال اسم المنفذ يدويًا.");
            }
            finally
            {
                applyingSettings = wasApplying;
            }
        }
        string SettingsPath
        {
            get {
                return Path.Combine(dataDir, "settings.json");
            }
        }
        Settings LoadSettings()
        {
            string path = SettingsPath;
            bool missing = false;
            try
            {
                return SettingsStore.Read(path);
            }
            catch (FileNotFoundException)
            {
                missing = true;
            }
            catch (DirectoryNotFoundException)
            {
                missing = true;
            }
            catch (Exception)
            {
            }
            try
            {
                var recovered = SettingsStore.Read(path + ".bak");
                WriteLog("تم استعادة آخر إعدادات صالحة.");
                return recovered;
            }
            catch (FileNotFoundException)
            {
                if (missing)
                    return new Settings();
            }
            catch (DirectoryNotFoundException)
            {
                if (missing)
                    return new Settings();
            }
            catch (Exception)
            {
            }
            settingsNeedRepair = true;
            WriteLog("تعذر قراءة الإعدادات والنسخة الاحتياطية. احفظ إعدادات صحيحة قبل التشغيل.");
            return new Settings { Endpoint = "" };
        }
        void Apply(Settings c)
        {
            applyingSettings = true;
            try
            {
                port.Text = c.Port;
                endpoint.Text = c.Endpoint;
                company.Text = c.Company;
                marker.Text = c.ResponseMarker;
                baud.Value = Clamp(c.Baud, 300, 115200);
                bits.Value = Clamp(c.DataBits, 5, 8);
                interval.Value = 1000;
                fresh.Value = Clamp(c.Freshness, 1000, 30000);
                returnsEndpoint.Text = c.ReturnsEndpoint;
                string plain;
                bool secondAvailable = LocalSecrets.TryUnprotect(c.ReturnsTokenProtected, out plain);
                returnsToken.Text = plain;
                primaryCredentialUnavailable = !LocalSecrets.TryUnprotect(c.PrimaryTokenProtected, out plain);
                token.Text = plain;
                SetFieldError(token, primaryCredentialUnavailable);
                fieldErrors.SetError(token,
                                     primaryCredentialUnavailable
                                         ? "تعذر فتح الرمز المحفوظ. أعد إدخاله؛ ستُحفظ قيمته الأصلية حتى استبداله."
                                         : "");
                if (!secondAvailable)
                    WriteBackgroundLog("تعذر فتح رمز القناة الثانية. تم الاحتفاظ بالقيمة المشفرة الأصلية.");
                parity.SelectedItem = c.Parity;
                if (parity.SelectedIndex < 0)
                    parity.SelectedIndex = 0;
                settingsDirty = false;
                save.Text = "حفظ وتطبيق";
            }
            finally
            {
                applyingSettings = false;
            }
        }
        static int Clamp(int v, int min, int max)
        {
            return Math.Max(min, Math.Min(max, v));
        }
        Settings ReadSettings()
        {
            var c = new Settings { Port = port.Text.Trim(),
                                   Baud = (int)baud.Value,
                                   DataBits = (int)bits.Value,
                                   Parity = parity.Text,
                                   Endpoint = endpoint.Text.Trim(),
                                   Company = company.Text.Trim(),
                                   Interval = (int)interval.Value,
                                   Freshness = (int)fresh.Value,
                                   ResponseMarker = marker.Text,
                                   ReturnsEndpoint = returnsEndpoint.Text.Trim(),
                                   ReturnsEnabled = true,
                                   ReturnsTokenProtected = LocalSecrets.UpdateProtected(
                                       settings == null ? "" : settings.ReturnsTokenProtected, returnsToken.Text),
                                   PrimaryTokenProtected = LocalSecrets.UpdateProtected(
                                       settings == null ? "" : settings.PrimaryTokenProtected, token.Text) };
            try
            {
                SettingsStore.Validate(c);
            }
            catch (InvalidDataException ex)
            {
                Control target = !Regex.IsMatch(c.Port, @"^COM[0-9]+$", RegexOptions.IgnoreCase) ? (Control)port
                                 : !Regex.IsMatch(c.Company, @"^[0-9]+$")                        ? company
                                 : ex.Message.Contains("HTTPS")                                  ? endpoint
                                                                                                 : (Control)parity;
                fieldErrors.SetError(target, ex.Message);
                SetFieldError(target, true);
                target.Focus();
                throw;
            }
            return c;
        }
    }
}
