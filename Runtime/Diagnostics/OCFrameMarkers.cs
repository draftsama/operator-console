#nullable enable

using Unity.Profiling;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>Reads, per <see cref="OCFrameCategory"/>, the time of Unity's built-in Profiler markers for the last frame. Release builds strip the markers.</summary>
    internal sealed class OCFrameMarkers
    {
        private const float NanosecondsPerMillisecond = 1_000_000f;

        private static readonly (OCFrameCategory Category, string ProfilerCategory, string Marker)[] Markers =
        {
            (OCFrameCategory.Scripts, "PlayerLoop", "Update.ScriptRunBehaviourUpdate"),
            (OCFrameCategory.Scripts, "PlayerLoop", "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
            (OCFrameCategory.Scripts, "PlayerLoop", "FixedUpdate.ScriptRunBehaviourFixedUpdate"),
            (OCFrameCategory.Physics, "PlayerLoop", "FixedUpdate.PhysicsFixedUpdate"),
            (OCFrameCategory.Physics, "PlayerLoop", "FixedUpdate.Physics2DFixedUpdate"),
            (OCFrameCategory.Animation, "PlayerLoop", "Update.DirectorUpdate"),
            (OCFrameCategory.Animation, "PlayerLoop", "PreLateUpdate.DirectorUpdateAnimationBegin"),
            (OCFrameCategory.Animation, "PlayerLoop", "PreLateUpdate.DirectorUpdateAnimationEnd"),
            (OCFrameCategory.Rendering, "PlayerLoop", "PostLateUpdate.UpdateAllRenderers"),
            (OCFrameCategory.Rendering, "PlayerLoop", "PostLateUpdate.FinishFrameRendering"),
            (OCFrameCategory.UI, "PlayerLoop", "PostLateUpdate.PlayerUpdateCanvases"),
            (OCFrameCategory.UI, "PlayerLoop", "PreLateUpdate.UIElementsUpdatePanels"),
            (OCFrameCategory.UI, "PlayerLoop", "PostLateUpdate.UIElementsRepaintPanels"),
            (OCFrameCategory.Loading, "PlayerLoop", "Loading.UpdatePreloading"),
            (OCFrameCategory.Loading, "Render", "Shader.CreateGPUProgram"),
            (OCFrameCategory.GC, "GC", "GC.Collect"),
            (OCFrameCategory.WaitGpu, "Render", "Gfx.WaitForPresentOnGfxThread"),
        };

        private static string[]? s_names;

        private readonly ProfilerRecorder[] _recorders = new ProfilerRecorder[Markers.Length];
        private bool _running;

        /// <summary>True in the Editor and Development builds, where the markers exist.</summary>
        public static bool Available => Debug.isDebugBuild;

        /// <summary>The marker names that make up <paramref name="category"/>, comma separated.</summary>
        public static string Names(OCFrameCategory category)
        {
            if (s_names == null)
            {
                s_names = new string[OCSpike.CategoryCount];
                foreach (var (c, _, marker) in Markers)
                    s_names[(int)c] = s_names[(int)c] == null ? marker : s_names[(int)c] + ", " + marker;
            }
            return s_names[(int)category] ?? string.Empty;
        }

        public void Start()
        {
            if (_running || !Available) return;
            _running = true;
            for (var i = 0; i < Markers.Length; i++)
                _recorders[i] = ProfilerRecorder.StartNew(new ProfilerCategory(Markers[i].ProfilerCategory), Markers[i].Marker);
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            for (var i = 0; i < _recorders.Length; i++) _recorders[i].Dispose();
        }

        /// <summary>Writes each category's time into <paramref name="times"/>; false when the markers can't be read.</summary>
        public bool Read(float[] times)
        {
            System.Array.Clear(times, 0, times.Length);
            if (!_running) return false;
            var any = false;
            for (var i = 0; i < Markers.Length; i++)
            {
                if (!_recorders[i].Valid) continue;
                any = true;
                times[(int)Markers[i].Category] += _recorders[i].LastValue / NanosecondsPerMillisecond;
            }
            return any;
        }
    }
}
