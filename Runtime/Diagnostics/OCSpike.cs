#nullable enable

using System;

namespace OperatorConsole
{
    /// <summary>What a frame spent its time on, read from Unity's built-in Profiler markers. Only the Editor and Development builds have them.</summary>
    public enum OCFrameCategory
    {
        Scripts,
        Physics,
        Animation,
        Rendering,
        UI,
        Loading,
        GC,
        WaitGpu
    }

    /// <summary>The likeliest reason a Spike's frame ran long. The first values match <see cref="OCFrameCategory"/>.</summary>
    public enum OCSpikeCause
    {
        Scripts,
        Physics,
        Animation,
        Rendering,
        UI,
        Loading,
        GC,
        WaitGpu,
        Gpu,
        Cpu,
        Other
    }

    /// <summary>One frame that ran longer than the Spike threshold. Times are in milliseconds, sizes in bytes; NaN or -1 when the build can't measure them.</summary>
    public sealed class OCSpike
    {
        public const int CategoryCount = 8;

        internal readonly float[] CategoryTimes = new float[CategoryCount];

        public DateTime Time { get; internal set; }

        public int Frame { get; internal set; }

        public float FrameMs { get; internal set; }

        public float CpuMs { get; internal set; } = float.NaN;

        public float GpuMs { get; internal set; } = float.NaN;

        public long GcAllocatedBytes { get; internal set; } = -1;

        /// <summary>True when a garbage collection ran in or right around the frame.</summary>
        public bool GcCollected { get; internal set; }

        public long MemoryChangeBytes { get; internal set; }

        public long Batches { get; internal set; } = -1;

        public long SetPassCalls { get; internal set; } = -1;

        public string Scene { get; internal set; } = string.Empty;

        /// <summary>True when a scene finished loading in or right around the frame.</summary>
        public bool SceneLoaded { get; internal set; }

        /// <summary>True when <see cref="CategoryMs"/> holds real times; false in Release builds.</summary>
        public bool HasBreakdown { get; internal set; }

        /// <summary>The slowest block wrapped in <c>OC.Mark</c> during the frame, or null.</summary>
        public string? MarkName { get; internal set; }

        public float MarkMs { get; internal set; }

        public OCSpikeCause Cause { get; internal set; }

        /// <summary>Time the frame spent in <paramref name="category"/>. Categories can overlap: a GC or a shader compile runs inside another phase.</summary>
        public float CategoryMs(OCFrameCategory category) => CategoryTimes[(int)category];
    }
}
