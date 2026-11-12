using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace WeighbridgeBridge
{
    public interface ISerialConnection : IDisposable
    {
        void Open();
        string ReadExisting();
    }

    sealed class ScaleConnection : ISerialConnection
    {
        readonly SerialPort port;
        public ScaleConnection(Settings settings)
        {
            port = new SerialPort(settings.Port, settings.Baud, (Parity)Enum.Parse(typeof(Parity), settings.Parity),
                                  settings.DataBits, StopBits.One);
        }
        public void Open()
        {
            port.Open();
        }
        public string ReadExisting()
        {
            return port.ReadExisting();
        }
        public void Dispose()
        {
            port.Dispose();
        }
    }

    public sealed class SerialSample
    {
        public long At;
        public DateTime Utc;
        public string Raw;
        public bool Valid;
        public decimal Weight;
    }

    public sealed class SerialSession
    {
        readonly object mailbox = new object();
        readonly Func<long> clock;
        readonly Func<Settings, ISerialConnection> factory;
        SerialSample incoming;
        string message;
        bool disconnected;
        CancellationTokenSource cancellation;
        public Task Completion { get; private set; }

        public SerialSession(Func<long> clock) : this(clock, settings => new ScaleConnection(settings)) {}
        public SerialSession(Func<long> clock, Func<Settings, ISerialConnection> factory)
        {
            this.clock = clock;
            this.factory = factory;
            Completion = Task.FromResult(0);
        }

        public void Start(Settings settings)
        {
            if (!Completion.IsCompleted)
                throw new InvalidOperationException("جارٍ إغلاق المنفذ. حاول بعد قليل.");
            lock (mailbox)
            {
                incoming = null;
                message = null;
                disconnected = false;
                cancellation = new CancellationTokenSource();
            }
            var active = cancellation;
            Completion = Task.Run(async () =>
                                  {
                                      try
                                      {
                                          await ReadLoop(settings, active.Token).ConfigureAwait(false);
                                      }
                                      catch (OperationCanceledException)
                                      {
                                      }
                                      finally
                                      {
                                          lock (mailbox)
                                          {
                                              if (cancellation == active)
                                                  cancellation = null;
                                              active.Dispose();
                                          }
                                      }
                                  });
        }

        public void Stop()
        {
            lock (mailbox)
            {
                if (cancellation != null)
                    cancellation.Cancel();
            }
        }

        public SerialSample Take(out string lastMessage, out bool lostConnection)
        {
            lock (mailbox)
            {
                var sample = incoming;
                lastMessage = message;
                lostConnection = disconnected;
                incoming = null;
                message = null;
                disconnected = false;
                return sample;
            }
        }

        void Report(string text, bool lostConnection)
        {
            lock (mailbox)
            {
                message = text;
                if (lostConnection)
                {
                    incoming = null;
                    disconnected = true;
                }
            }
        }
        async Task ReadLoop(Settings configuration, CancellationToken cancel)
        {
            int retry = 1000;
            while (!cancel.IsCancellationRequested)
            {
                ISerialConnection connection = null;
                try
                {
                    connection = factory(configuration);
                    connection.Open();
                    Report("تم فتح منفذ الميزان.", false);
                    var frames = new Frames(true);
                    long lastData = clock();
                    int silenceLimit = Math.Max(5000, configuration.Freshness * 2);
                    while (!cancel.IsCancellationRequested)
                    {
                        string part = connection.ReadExisting();
                        long now = clock();
                        if (part.Length > 0)
                        {
                            lastData = now;
                            decimal value;
                            string raw;
                            bool valid = frames.Push(part, out value, out raw);
                            if (raw.Length > 0)
                            {
                                if (valid)
                                    retry = 1000;
                                lock (mailbox)
                                {
                                    incoming = new SerialSample { At = now, Utc = DateTime.UtcNow, Raw = raw,
                                                                  Valid = valid, Weight = value };
                                }
                            }
                        }
                        else if (now - lastData >= silenceLimit)
                        {
                            Report("انقطاع بيانات الميزان. ستُعاد محاولة فتح المنفذ.", true);
                            break;
                        }
                        await Task.Delay(50, cancel).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Report("تعذر الاتصال بمنفذ الميزان: " + ex.GetType().Name, true);
                }
                finally
                {
                    if (connection != null)
                    {
                        try
                        {
                            connection.Dispose();
                        }
                        catch
                        {
                        }
                    }
                }
                await Task.Delay(retry, cancel).ConfigureAwait(false);
                retry = Math.Min(15000, retry * 2);
            }
        }
    }
}
