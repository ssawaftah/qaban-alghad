using System;

namespace WeighbridgeBridge
{
    public static class ReadingPolicy
    {
        public static bool IsFresh(long now, long received, int freshness)
        {
            return received >= 0 && now >= received && now - received <= freshness;
        }
        public static int Deadline(long now, long received, int freshness)
        {
            return (int)Math.Max(1, Math.Min(8000, freshness - (now - received)));
        }
    }
}
