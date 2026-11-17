#nullable enable

using System;
using System.Collections.Generic;

namespace OperatorConsole
{
    /// <summary>Graphics: how the app renders on this machine. Every change takes effect at once and becomes an Override; Reset returns to how the app started.</summary>
    public sealed class OCGraphics
    {
        private readonly Func<OCStore> _store;
        private readonly IOCGraphicsBackend _backend;
        private int _startupLevel = -1;
        private readonly float[] _startupValues = new float[OCGraphicsCatalog.All.Length];
        private readonly bool[] _startupKnown = new bool[OCGraphicsCatalog.All.Length];

        internal OCGraphics(Func<OCStore> store, IOCGraphicsBackend backend)
        {
            _store = store;
            _backend = backend;
        }

        public IReadOnlyList<string> QualityLevelNames => _backend.QualityLevelNames;

        public int QualityLevel => _backend.QualityLevel;

        /// <summary>True when <paramref name="setting"/> can be changed in this app; URP-only settings are false without URP.</summary>
        public bool Supports(OCGraphicsSetting setting) => _backend.Supports(setting);

        public float Get(OCGraphicsSetting setting) => _backend.Read(setting);

        /// <summary>True when <paramref name="setting"/> has an Override.</summary>
        public bool IsFineTuned(OCGraphicsSetting setting) => _store().HasOverride(OCGraphicsCatalog.Info(setting).Key);

        /// <summary>How many settings that follow the Quality Level have an Override.</summary>
        public int FineTunedCount
        {
            get
            {
                var count = 0;
                foreach (var info in OCGraphicsCatalog.All)
                    if (info.FollowsQualityLevel && Supports(info.Setting) && IsFineTuned(info.Setting)) count++;
                return count;
            }
        }

        /// <summary>True while the Quality Level has been fine-tuned.</summary>
        public bool IsCustom => FineTunedCount > 0;

        public event Action? Changed;

        /// <summary>Applies <paramref name="value"/> and stores it as an Override. Slider values are clamped and snapped to their step.</summary>
        public void Set(OCGraphicsSetting setting, float value)
        {
            if (!Supports(setting)) return;
            CaptureStartup();
            var info = OCGraphicsCatalog.Info(setting);
            value = info.Normalize(value);
            _backend.Write(setting, value);
            Write(_store(), info, value);
            Changed?.Invoke();
        }

        /// <summary>Switches to <paramref name="level"/>. Overrides of settings that follow the Quality Level are removed; frame-rate settings keep their values.</summary>
        public void SetQualityLevel(int level)
        {
            var names = QualityLevelNames;
            if (names.Count == 0) return;
            level = Math.Clamp(level, 0, names.Count - 1);
            CaptureStartup();
            var store = _store();
            foreach (var info in OCGraphicsCatalog.All)
                if (info.FollowsQualityLevel) store.Reset(info.Key);
            store.Set(OCGraphicsCatalog.QualityLevelKey, level);
            SwitchLevelKeepingFrameRate(level);
            Changed?.Invoke();
        }

        /// <summary>Removes every Graphics Setting Override and restores Graphics as the app started.</summary>
        public void Reset()
        {
            var store = _store();
            store.Reset(OCGraphicsCatalog.QualityLevelKey);
            foreach (var info in OCGraphicsCatalog.All) store.Reset(info.Key);
            RestoreStartup();
            Changed?.Invoke();
        }

        internal void ApplyStoredAtStart()
        {
            CaptureStartup();
            var store = _store();
            if (store.HasOverride(OCGraphicsCatalog.QualityLevelKey))
            {
                var names = QualityLevelNames;
                if (names.Count > 0) SwitchLevelKeepingFrameRate(Math.Clamp(store.Get(OCGraphicsCatalog.QualityLevelKey, QualityLevel), 0, names.Count - 1));
            }
            foreach (var info in OCGraphicsCatalog.All)
                if (store.HasOverride(info.Key) && Supports(info.Setting)) _backend.Write(info.Setting, info.Normalize(Read(store, info)));
        }

        /// <summary>Puts Graphics back as the app started without touching any Override; used when leaving Play mode so project settings stay unchanged.</summary>
        internal void RestoreStartup()
        {
            if (_startupLevel < 0) return;
            _backend.ReleaseCopies();
            if (_backend.QualityLevel != _startupLevel) _backend.SetQualityLevel(_startupLevel);
            for (var i = 0; i < _startupValues.Length; i++)
            {
                var setting = OCGraphicsCatalog.All[i].Setting;
                if (_startupKnown[i] && Supports(setting) && _backend.Read(setting) != _startupValues[i]) _backend.Write(setting, _startupValues[i]);
            }
            _backend.ReleaseCopies();
        }

        private void CaptureStartup()
        {
            if (_startupLevel >= 0) return;
            _startupLevel = _backend.QualityLevel;
            for (var i = 0; i < _startupValues.Length; i++)
            {
                var setting = OCGraphicsCatalog.All[i].Setting;
                _startupKnown[i] = Supports(setting);
                if (_startupKnown[i]) _startupValues[i] = _backend.Read(setting);
            }
        }

        private void SwitchLevelKeepingFrameRate(int level)
        {
            var vSync = _backend.Read(OCGraphicsSetting.VSyncCount);
            var target = _backend.Read(OCGraphicsSetting.TargetFrameRate);
            _backend.ReleaseCopies();
            _backend.SetQualityLevel(level);
            _backend.Write(OCGraphicsSetting.VSyncCount, vSync);
            _backend.Write(OCGraphicsSetting.TargetFrameRate, target);
        }

        private static float Read(OCStore store, OCGraphicsSettingInfo info) => info.Kind switch
        {
            OCGraphicsKind.Slider => store.Get(info.Key, 0f),
            OCGraphicsKind.Toggle => store.Get(info.Key, false) ? 1f : 0f,
            _                      => store.Get(info.Key, 0)
        };

        private static void Write(OCStore store, OCGraphicsSettingInfo info, float value)
        {
            switch (info.Kind)
            {
                case OCGraphicsKind.Slider: store.Set(info.Key, value); break;
                case OCGraphicsKind.Toggle: store.Set(info.Key, value != 0f); break;
                default: store.Set(info.Key, (int)value); break;
            }
        }
    }
}
