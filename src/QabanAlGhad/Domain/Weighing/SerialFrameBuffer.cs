using System;
using System.Text;

namespace WeighbridgeBridge
{
    public sealed class Frames
    {
        const int MaximumFrameLength = 16384;
        readonly StringBuilder pending = new StringBuilder();
        bool discard;
        bool syncing;

        public Frames() : this(false) {}
        public Frames(bool synchronizeOnOpen)
        {
            syncing = synchronizeOnOpen;
        }

        public bool Push(string part, out decimal weight, out string last)
        {
            weight = 0;
            last = "";
            bool valid = false;
            foreach (char ch in part)
            {
                if (ch == '\r' || ch == '\n')
                {
                    if (discard)
                    {
                        discard = false;
                        continue;
                    }
                    if (pending.Length == 0)
                        continue;
                    string frame = pending.ToString();
                    pending.Clear();
                    if (string.IsNullOrWhiteSpace(frame))
                        continue;
                    if (syncing)
                    {
                        syncing = false;
                        // The port can open in the middle of a continuously transmitted frame.
                        string start = frame.TrimStart(' ', '\x02');
                        if (!start.StartsWith("ST", StringComparison.OrdinalIgnoreCase) &&
                            !start.StartsWith("US", StringComparison.OrdinalIgnoreCase) &&
                            !start.StartsWith("1,", StringComparison.Ordinal))
                            continue;
                    }
                    last = frame;
                    valid = Protocol.Parse(frame, out weight);
                }
                else if (!discard)
                {
                    if (pending.Length == MaximumFrameLength)
                    {
                        pending.Clear();
                        discard = true;
                        syncing = false;
                        last = "[oversized serial frame]";
                        weight = 0;
                        valid = false;
                    }
                    else
                        pending.Append(ch);
                }
            }
            return valid;
        }
    }
}
