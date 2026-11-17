#nullable enable

using System.Collections.Generic;

namespace OperatorConsole
{
    /// <summary>Keeps the last <see cref="Capacity"/> Spikes, newest first, for the Performance tab. Main thread only.</summary>
    public sealed class OCSpikeRecorder
    {
        public const int Capacity = 100;

        /// <summary>Frames skipped after a Spike, so one slow load doesn't fill the list.</summary>
        public const int CooldownFrames = 3;

        private const float DominantShare = 0.25f;
        private const float MinShare = 0.1f;

        private readonly List<OCSpike> _spikes = new(Capacity);
        private int _cooldown;

        /// <summary>Frames longer than this, in milliseconds, are recorded.</summary>
        public float ThresholdMs { get; set; } = 33.3f;

        public bool IsRecording { get; set; } = true;

        /// <summary>Changes whenever a Spike is added or the list is cleared.</summary>
        public int Version { get; private set; }

        /// <summary>Newest first.</summary>
        public IReadOnlyList<OCSpike> Spikes => _spikes;

        public void Clear()
        {
            if (_spikes.Count == 0) return;
            _spikes.Clear();
            Version++;
        }

        /// <summary>Called once per frame; true when this frame should be recorded.</summary>
        internal bool Tick(float frameMs)
        {
            if (_cooldown > 0)
            {
                _cooldown--;
                return false;
            }
            if (!IsRecording || frameMs <= ThresholdMs) return false;
            _cooldown = CooldownFrames;
            return true;
        }

        internal void Add(OCSpike spike)
        {
            spike.Cause = Classify(spike);
            if (_spikes.Count == Capacity) _spikes.RemoveAt(Capacity - 1);
            _spikes.Insert(0, spike);
            Version++;
        }

        internal static OCSpikeCause Classify(OCSpike spike)
        {
            if (spike.HasBreakdown)
            {
                var frame = spike.FrameMs;
                if (spike.CategoryMs(OCFrameCategory.GC) >= frame * DominantShare) return OCSpikeCause.GC;
                if (spike.CategoryMs(OCFrameCategory.Loading) >= frame * DominantShare) return OCSpikeCause.Loading;
                var best = -1;
                var bestMs = 0f;
                for (var i = 0; i < OCSpike.CategoryCount; i++)
                {
                    if (spike.CategoryTimes[i] <= bestMs) continue;
                    best = i;
                    bestMs = spike.CategoryTimes[i];
                }
                return best >= 0 && bestMs >= frame * MinShare ? (OCSpikeCause)best : OCSpikeCause.Other;
            }
            if (spike.GcCollected) return OCSpikeCause.GC;
            if (spike.SceneLoaded) return OCSpikeCause.Loading;
            if (!float.IsNaN(spike.GpuMs) && !float.IsNaN(spike.CpuMs) && spike.GpuMs > spike.CpuMs) return OCSpikeCause.Gpu;
            return OCSpikeCause.Cpu;
        }
    }
}
