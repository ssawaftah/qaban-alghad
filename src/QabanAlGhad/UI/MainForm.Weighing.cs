using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WeighbridgeBridge
{
    public sealed partial class MainForm
    {
        readonly SerialSession serialSession;
        string readingState = "بانتظار القراءة";

        void StartReading()
        {
            try
            {
                if (!ResolveUnsavedSettings())
                    return;
                if (settingsNeedRepair)
                    throw new InvalidOperationException("احفظ إعدادات صحيحة قبل التشغيل.");
                if (!serialSession.Completion.IsCompleted)
                    throw new InvalidOperationException("جارٍ إغلاق المنفذ. حاول بعد قليل.");
                if (primaryCredentialUnavailable)
                    throw new InvalidOperationException(
                        "تعذر فتح رمز النظام المحفوظ. أعد إدخاله في الإعدادات قبل التشغيل.");
                string primaryToken, secondToken;
                LocalSecrets.TryUnprotect(settings.PrimaryTokenProtected, out primaryToken);
                bool secondAvailable = LocalSecrets.TryUnprotect(settings.ReturnsTokenProtected, out secondToken);
                primary.Configure(settings.Endpoint, primaryToken, settings.Company, settings.Freshness);
                returns.Configure(settings.ReturnsEndpoint, secondToken, settings.Company, settings.Freshness);
                if (!secondAvailable)
                    WriteBackgroundLog("تعذر فتح رمز القناة الثانية؛ يلزم إعادة التهيئة للحساب الحالي.");
                serialSession.Start(settings);
                updateInstallFailed = false;
                running = true;
                received = -1;
                nextReadingLog = lastFrameAt = lastLoggedFrameAt = -1;
                nextInvalidEvent = 0;
                readingState = "بانتظار القراءة";
                lastReadingLocal = null;
                lastFrame = "";
                lastSerialIssue = null;
                fields.Enabled = false;
                start.Enabled = false;
                stop.Enabled = true;
                save.Enabled = false;
                weight.Text = "كغ";
                WriteLog("بدء القراءة " + settings.Port + " / " + settings.Baud + " / " + settings.DataBits + " / " +
                         settings.Parity + " / StopBits.One");
            }
            catch (Exception ex)
            {
                AppMessage.Show(this,
                                ex is InvalidOperationException
                                    ? ex.Message
                                    : "تعذر بدء قراءة الميزان. راجع الإعدادات وتشخيص الاتصال ثم أعد المحاولة.",
                                "بدء القراءة");
            }
        }

        void StopReading()
        {
            running = false;
            received = -1;
            primary.Stop();
            returns.Stop();
            serialSession.Stop();
            fields.Enabled = true;
            start.Enabled = serialSession.Completion.IsCompleted;
            stop.Enabled = false;
            save.Enabled = true;
            weight.Text = "كغ";
        }

        void Tick(object sender, EventArgs e)
        {
            if (!running)
                return;
            string message;
            bool disconnected;
            SerialSample sample = serialSession.Take(out message, out disconnected);
            long now = clock.ElapsedMilliseconds;
            if (disconnected)
            {
                received = -1;
                readingState = "انقطع المنفذ";
                lastSerialIssue = message;
            }
            if (message != null)
                WriteLog(message);
            if (sample != null)
            {
                RememberSample(sample.At, sample.Utc, sample.Raw, sample.Valid, sample.Weight);
                lastReadingLocal = sample.Utc.ToLocalTime();
                if (sample.Valid)
                {
                    latest = sample.Weight;
                    received = sample.At;
                    sampledAtUtc = sample.Utc;
                    readingState = "بانتظار الإرسال";
                }
                else
                {
                    received = -1;
                    readingState = ReadingDescription.Rejection(sample.Raw);
                    if (now >= nextInvalidEvent)
                    {
                        nextInvalidEvent = now + 3000;
                        WriteLog(readingState + ": " + lastFrame);
                    }
                }
            }
            bool recent = ReadingPolicy.IsFresh(now, received, settings.Freshness);
            if (!recent && received >= 0)
                readingState = "لا تصل قراءة";
            weight.Text = recent ? latest.ToString(CultureInfo.InvariantCulture) + " كغ" : "كغ";
            FlushReadingLog(now);
            primary.Pump(now, true, received, sampledAtUtc, latest);
            returns.Pump(now, true, received, sampledAtUtc, latest);
        }
    }
}
