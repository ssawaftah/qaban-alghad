using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WeighbridgeBridge
{
    public sealed class BackgroundLogWriter
    {
        readonly Queue<Action> jobs = new Queue<Action>();
        readonly TaskCompletionSource<bool> drained = new TaskCompletionSource<bool>();
        bool working, stopping;
        int dropped, errors;
        public int Dropped
        {
            get {
                return Volatile.Read(ref dropped);
            }
        }
        public int Errors
        {
            get {
                return Volatile.Read(ref errors);
            }
        }

        public void Enqueue(Action write)
        {
            lock (jobs)
            {
                if (stopping || jobs.Count >= 256)
                {
                    Interlocked.Increment(ref dropped);
                    return;
                }
                jobs.Enqueue(write);
                if (working)
                    return;
                working = true;
                Task.Run((Action)Drain);
            }
        }

        void Drain()
        {
            while (true)
            {
                Action write;
                lock (jobs)
                {
                    if (jobs.Count == 0)
                    {
                        working = false;
                        if (stopping)
                            drained.TrySetResult(true);
                        return;
                    }
                    write = jobs.Dequeue();
                }
                try
                {
                    write();
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref errors);
                }
            }
        }

        public async Task<bool> StopAsync(int timeoutMilliseconds)
        {
            lock (jobs)
            {
                stopping = true;
                if (!working)
                    drained.TrySetResult(true);
            }
            return await Task.WhenAny(drained.Task, Task.Delay(timeoutMilliseconds)).ConfigureAwait(false) ==
                   drained.Task;
        }
    }
}
