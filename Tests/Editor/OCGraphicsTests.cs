#nullable enable

using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCGraphicsTests
    {
        private OCConfig? _config;
        private OCStore _store = null!;
        private FakeBackend _backend = null!;
        private OCGraphics _graphics = null!;

        [SetUp]
        public void SetUp()
        {
            _config = TestConfigs.Create();
            _store = new OCStore(_config, Path.Combine(_config.SaveDirectory, _config.FileName));
            _backend = new FakeBackend();
            _graphics = new OCGraphics(() => _store, _backend);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config == null) return;
            if (Directory.Exists(_config.SaveDirectory)) Directory.Delete(_config.SaveDirectory, true);
            Object.DestroyImmediate(_config);
            _config = null;
        }

        [Test]
        public void Set_ClampsAndSnapsSliderToItsStep()
        {
            _graphics.Set(OCGraphicsSetting.RenderScale, 0.83f);
            _graphics.Set(OCGraphicsSetting.ShadowDistance, 900f);

            Assert.That(_backend.Read(OCGraphicsSetting.RenderScale), Is.EqualTo(0.85f).Within(1e-4f));
            Assert.That(_backend.Read(OCGraphicsSetting.ShadowDistance), Is.EqualTo(200f));
            Assert.That(_store.Get(OCGraphicsCatalog.Info(OCGraphicsSetting.RenderScale).Key, 0f), Is.EqualTo(0.85f).Within(1e-4f));
        }

        [Test]
        public void Set_IgnoresSettingTheAppCannotChange()
        {
            _backend.Urp = false;

            _graphics.Set(OCGraphicsSetting.MsaaSampleCount, 4);

            Assert.That(_graphics.IsFineTuned(OCGraphicsSetting.MsaaSampleCount), Is.False);
        }

        [Test]
        public void FineTunedCount_LeavesOutFrameRateSettings()
        {
            _graphics.Set(OCGraphicsSetting.VSyncCount, 0);
            Assert.That(_graphics.IsCustom, Is.False);

            _graphics.Set(OCGraphicsSetting.LodBias, 1.5f);
            Assert.That(_graphics.FineTunedCount, Is.EqualTo(1));
        }

        [Test]
        public void SetQualityLevel_DropsFineTuningButKeepsFrameRate()
        {
            _graphics.Set(OCGraphicsSetting.VSyncCount, 2);
            _graphics.Set(OCGraphicsSetting.RenderScale, 0.6f);

            _graphics.SetQualityLevel(0);

            Assert.That(_graphics.IsFineTuned(OCGraphicsSetting.RenderScale), Is.False);
            Assert.That(_backend.Read(OCGraphicsSetting.RenderScale), Is.EqualTo(FakeBackend.Presets[0][OCGraphicsSetting.RenderScale]));
            Assert.That(_graphics.IsFineTuned(OCGraphicsSetting.VSyncCount), Is.True);
            Assert.That(_backend.Read(OCGraphicsSetting.VSyncCount), Is.EqualTo(2f));
            Assert.That(_store.Get(OCGraphicsCatalog.QualityLevelKey, -1), Is.EqualTo(0));
        }

        [Test]
        public void Reset_RemovesOverridesAndRestoresStartup()
        {
            _backend.Write(OCGraphicsSetting.TargetFrameRate, 60);
            _graphics.SetQualityLevel(0);
            _graphics.Set(OCGraphicsSetting.TargetFrameRate, -1);
            _graphics.Set(OCGraphicsSetting.ShadowDistance, 100f);

            _graphics.Reset();

            Assert.That(_backend.QualityLevel, Is.EqualTo(1));
            Assert.That(_backend.Read(OCGraphicsSetting.TargetFrameRate), Is.EqualTo(60f));
            Assert.That(_backend.Read(OCGraphicsSetting.ShadowDistance), Is.EqualTo(FakeBackend.Presets[1][OCGraphicsSetting.ShadowDistance]));
            Assert.That(_store.HasOverride(OCGraphicsCatalog.QualityLevelKey), Is.False);
            Assert.That(_graphics.IsFineTuned(OCGraphicsSetting.TargetFrameRate), Is.False);
        }

        [Test]
        public void ApplyStoredAtStart_AppliesLevelThenOverrides()
        {
            _store.Set(OCGraphicsCatalog.QualityLevelKey, 0);
            _store.Set(OCGraphicsCatalog.Info(OCGraphicsSetting.ShadowDistance).Key, 75f);
            _store.Set(OCGraphicsCatalog.Info(OCGraphicsSetting.Hdr).Key, true);

            _graphics.ApplyStoredAtStart();

            Assert.That(_backend.QualityLevel, Is.EqualTo(0));
            Assert.That(_backend.Read(OCGraphicsSetting.ShadowDistance), Is.EqualTo(75f));
            Assert.That(_backend.Read(OCGraphicsSetting.Hdr), Is.EqualTo(1f));
        }

        private sealed class FakeBackend : IOCGraphicsBackend
        {
            public static readonly Dictionary<OCGraphicsSetting, float>[] Presets =
            {
                new() { [OCGraphicsSetting.VSyncCount] = 0, [OCGraphicsSetting.RenderScale] = 0.8f, [OCGraphicsSetting.ShadowDistance] = 20f, [OCGraphicsSetting.Hdr] = 0f },
                new() { [OCGraphicsSetting.VSyncCount] = 0, [OCGraphicsSetting.RenderScale] = 1f, [OCGraphicsSetting.ShadowDistance] = 40f, [OCGraphicsSetting.Hdr] = 0f }
            };

            private readonly Dictionary<OCGraphicsSetting, float> _values = new();

            public FakeBackend() => SetQualityLevel(1);

            public bool Urp { get; set; } = true;

            public IReadOnlyList<string> QualityLevelNames { get; } = new[] { "Mobile", "PC" };

            public int QualityLevel { get; private set; }

            public void SetQualityLevel(int level)
            {
                QualityLevel = level;
                foreach (var pair in Presets[level]) _values[pair.Key] = pair.Value;
            }

            public bool Supports(OCGraphicsSetting setting) => Urp || !OCGraphicsCatalog.Info(setting).NeedsUrp;

            public float Read(OCGraphicsSetting setting) => _values.TryGetValue(setting, out var value) ? value : 0f;

            public void Write(OCGraphicsSetting setting, float value) => _values[setting] = value;

            public void ReleaseCopies() { }
        }
    }
}
