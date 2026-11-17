#nullable enable

using System;
using System.Diagnostics;

namespace OperatorConsole
{
    /// <summary>A block timed by <c>OC.Mark</c>. Dispose it, normally with <c>using</c>, to stop timing.</summary>
    public readonly struct OCMarkScope : IDisposable
    {
        private readonly int _slot;
        private readonly long _start;

        internal OCMarkScope(int slot, long start)
        {
            _slot = slot;
            _start = start;
        }

        public void Dispose() => OCMarks.End(_slot, _start);
    }

    /// <summary>Per-frame totals of the blocks timed with <c>OC.Mark</c>, by name. Uses a stopwatch, so it works in Release builds. Main thread only.</summary>
    internal static class OCMarks
    {
        public const int MaxNames = 32;

        private static readonly string?[] s_names = new string?[MaxNames];
        private static readonly long[] s_ticks = new long[MaxNames];
        private static int s_count;

        /// <summary>The slowest name of the frame that ended at the last <see cref="EndFrame"/>, or null.</summary>
        public static string? LastFrameTopName { get; private set; }

        public static float LastFrameTopMs { get; private set; }

        public static OCMarkScope Begin(string name) => new(Slot(name), Stopwatch.GetTimestamp());

        public static void End(int slot, long start)
        {
            if (slot >= 0) s_ticks[slot] += Stopwatch.GetTimestamp() - start;
        }

        /// <summary>Closes the current frame's totals and starts the next frame at zero.</summary>
        public static void EndFrame()
        {
            string? top = null;
            long topTicks = 0;
            for (var i = 0; i < s_count; i++)
            {
                if (s_ticks[i] > topTicks)
                {
                    topTicks = s_ticks[i];
                    top = s_names[i];
                }
                s_ticks[i] = 0;
            }
            LastFrameTopName = top;
            LastFrameTopMs = (float)(topTicks * 1000.0 / Stopwatch.Frequency);
        }

        private static int Slot(string name)
        {
            for (var i = 0; i < s_count; i++)
                if (ReferenceEquals(s_names[i], name) || string.Equals(s_names[i], name, StringComparison.Ordinal)) return i;
            if (s_count == MaxNames) return -1;
            s_names[s_count] = name;
            return s_count++;
        }
    }
}
