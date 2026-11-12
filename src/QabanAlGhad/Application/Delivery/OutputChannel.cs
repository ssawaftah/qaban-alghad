using System;
using System.Net;
using System.Globalization;
using System.Threading.Tasks;

namespace WeighbridgeBridge
{
    public enum OutputKind
    {
        Primary,
        Returns
    }
    public sealed class OutputChannel
    {
        public readonly TransferFeedback Feedback = new TransferFeedback();
        public readonly DeliverySchedule Schedule = new DeliverySchedule();
        public bool Busy { get; private set; }
        public long LastBodyWritten { get; private set; }
        public bool WriteFailed { get; private set; }
        public DateTime? LastWriteLocal { get; private set; }
        public string LastTransportIssue { get; private set; }
        long nextTransportLog, nextResponseLog;
        int suppressedFailures;
        public bool Ready { get; private set; }
        public string ConfigurationError { get; private set; }
        public string LastResponseIssue { get; private set; }
        public Task Pending { get; private set; }
        readonly OutputKind kind;
        readonly string name;
        readonly Func<long> clock;
        readonly Action<string> log;
        readonly Action<ApiReply, string> save;
        HttpWebRequest active;
        int generation;
        string endpoint, bearer, company;
        int freshness;
        public OutputChannel(OutputKind kind, string name, Func<long> clock, Action<string> log,
                             Action<ApiReply, string> save)
        {
            this.kind = kind;
            this.name = name;
            this.clock = clock;
            this.log = log;
            this.save = save;
            Pending = Task.FromResult(0);
            LastBodyWritten = -1;
        }
        public void Configure(string url, string token, string companyId, int freshnessMs, bool allowLoopback = false)
        {
            Stop();
            Feedback.Reset();
            nextTransportLog = 0;
            nextResponseLog = 0;
            suppressedFailures = 0;
            LastWriteLocal = null;
            LastTransportIssue = null;
            Schedule.Reset();
            Ready = false;
            ConfigurationError = null;
            LastResponseIssue = null;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || uri.UserInfo != "" || uri.Fragment != "" ||
                uri.Query != "" ||
                (uri.Scheme != "https" && !(allowLoopback && uri.Scheme == "http" && uri.Host == "127.0.0.1")))
            {
                ConfigurationFailed("عنوان غير صالح");
                return;
            }
            if (kind == OutputKind.Returns && string.IsNullOrWhiteSpace(token))
            {
                ConfigurationFailed("رمز API مطلوب");
                return;
            }
            endpoint = url;
            bearer = token;
            company = companyId;
            freshness = freshnessMs;
            Ready = true;
        }
        void ConfigurationFailed(string reason)
        {
            ConfigurationError = reason;
            if (log != null)
                log("[" + name + "] تعذر تهيئة القناة: " + reason);
        }
        public void Stop()
        {
            WriteFailed = false;
            LastBodyWritten = -1;
            generation++;
            Ready = false;
            if (active != null)
            {
                try
                {
                    active.Abort();
                }
                catch
                {
                }
            }
            active = null;
        }
        public void Pump(long now, bool enabled, long received, DateTime sampledAtUtc, decimal weight)
        {
            bool recent = ReadingPolicy.IsFresh(now, received, freshness);
            if (!Ready || !Schedule.CanSend(now, true, enabled, recent, Busy))
                return;
            Schedule.PeriodicStart(now, 1000);
            Pending = Send(weight, received, sampledAtUtc, generation);
        }
        public static string ReturnsPayload(decimal weight, DateTime sampledAtUtc)
        {
            return "{\"weightKg\":" + weight.ToString(CultureInfo.InvariantCulture) + ",\"sampledAt\":\"" +
                   sampledAtUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) + "\"}";
        }
        public static bool ReturnsAccepted(string body, decimal weight)
        {
            return ResponseRules.Matches(
                body, "{\"accepted\":true,\"weightKg\":" + weight.ToString(CultureInfo.InvariantCulture) + "}");
        }
        async Task Send(decimal weight, long received, DateTime sampledAtUtc, int gen)
        {
            Busy = true;
            Feedback.Begin();
            HttpWebRequest request = null;
            try
            {
                string payload = kind == OutputKind.Primary ? Protocol.Payload(company, weight)
                                                            : ReturnsPayload(weight, sampledAtUtc);
                bool sendOnly = kind == OutputKind.Primary;
                var reply = await ApiTransport.SendAsync(endpoint, payload, bearer,
                                                         ReadingPolicy.Deadline(clock(), received, freshness),
                                                         r =>
                                                         {
                                                             request = r;
                                                             active = r;
                                                         },
                                                         () =>
                                                         {
                                                             if (Ready && gen == generation)
                                                             {
                                                                 Feedback.BodyWritten(!sendOnly);
                                                                 LastBodyWritten = clock();
                                                                 LastWriteLocal = DateTime.Now;
                                                                 WriteFailed = false;
                                                                 LastTransportIssue = null;
                                                                 nextTransportLog = 0;
                                                                 suppressedFailures = 0;
                                                             }
                                                         },
                                                         sendOnly);
                if (!Ready || gen != generation)
                    return;
                if (sendOnly)
                {
                    Schedule.PeriodicSuccess();
                    return;
                }
                bool html = ResponseRules.IsHtml(reply.MediaType, reply.Body),
                     failure = ResponseRules.ExplicitFailure(reply.Body);
                bool accepted = ReturnsAccepted(reply.Body, weight);
                Feedback.Reply(reply.StatusCode, accepted, html, failure);
                LastResponseIssue = ResponseRules.ReturnsIssue(reply.StatusCode, reply.MediaType, reply.Body, weight);
                reply.DiagnosticCode = LastResponseIssue;
                if (Feedback.ServerError || !accepted)
                {
                    long now = clock();
                    Schedule.PeriodicFailure(now, reply.RetryAfterMilliseconds);
                    if (now >= nextResponseLog)
                    {
                        nextResponseLog = now + 30000;
                        if (save != null)
                            save(reply, Feedback.ServerError ? "last-error-returns.txt" : "response-returns.txt");
                        if (log != null)
                            log("[" + name + "] " + LastResponseIssue);
                    }
                }
                else
                {
                    Schedule.PeriodicSuccess();
                    nextResponseLog = 0;
                }
            }
            catch (WebException ex)
            {
                if (ex.Response != null)
                    ex.Response.Dispose();
                if (Ready && gen == generation)
                {
                    TransportFailed(DescribeTransportIssue(ex.Status));
                }
            }
            catch (Exception ex)
            {
                if (Ready && gen == generation)
                {
                    TransportFailed(ex.GetType().Name);
                }
            }
            finally
            {
                if (active == request)
                    active = null;
                Busy = false;
            }
        }
        static string DescribeTransportIssue(WebExceptionStatus status)
        {
            switch (status)
            {
            case WebExceptionStatus.Timeout:
                return "انتهت مهلة الاتصال أو الكتابة (Timeout)";
            case WebExceptionStatus.SecureChannelFailure:
                return "تعذر إنشاء اتصال TLS (SecureChannelFailure)";
            case WebExceptionStatus.TrustFailure:
                return "تعذر التحقق من شهادة الخادم (TrustFailure)";
            case WebExceptionStatus.NameResolutionFailure:
                return "تعذر العثور على عنوان الخادم (NameResolutionFailure)";
            default:
                return status.ToString();
            }
        }
        void TransportFailed(string issue)
        {
            Feedback.NoReply();
            WriteFailed = !Feedback.Written;
            LastTransportIssue = issue;
            long now = clock();
            if (WriteFailed)
                Schedule.TransportFailure(now);
            else
                Schedule.PeriodicFailure(now, 0);
            if (now < nextTransportLog)
            {
                suppressedFailures++;
                return;
            }
            if (log != null)
                log("[" + name + "] " + (WriteFailed ? "تعذر الإرسال: " : "تعذر قراءة الرد: ") + issue +
                    (suppressedFailures == 0 ? "" : " | تكرار " + suppressedFailures));
            suppressedFailures = 0;
            nextTransportLog = now + 30000;
        }
    }
}
