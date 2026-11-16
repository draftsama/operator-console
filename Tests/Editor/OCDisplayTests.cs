#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCDisplayTests
    {
        private OCConfig? _config;

        [TearDown]
        public void TearDown()
        {
            if (_config == null) return;
            if (Directory.Exists(_config.SaveDirectory)) Directory.Delete(_config.SaveDirectory, true);
            Object.DestroyImmediate(_config);
            _config = null;
        }

        private OCStore NewStore()
        {
            _config = TestConfigs.Create();
            return new OCStore(_config, Path.Combine(_config.SaveDirectory, _config.FileName));
        }

        [Test]
        public void Normalized_RaisesSizeToMinimum()
        {
            var n = new OCDisplaySettings(OCDisplayMode.Windowed, 0, 10, 10, 0, 0).Normalized();

            Assert.That(n.Width, Is.EqualTo(OCDisplaySettings.MinWidth));
            Assert.That(n.Height, Is.EqualTo(OCDisplaySettings.MinHeight));
        }

        [Test]
        public void Normalized_ZeroesFieldsTheModeDoesNotUse()
        {
            var windowed = new OCDisplaySettings(OCDisplayMode.Windowed, 2, 800, 600, 50, 60).Normalized();
            var fullScreen = new OCDisplaySettings(OCDisplayMode.FullScreen, 2, 800, 600, 50, 60).Normalized();
            var borderless = new OCDisplaySettings(OCDisplayMode.Borderless, 2, 800, 600, 50, 60).Normalized();

            Assert.That((windowed.Monitor, windowed.X, windowed.Y), Is.EqualTo((0, 0, 0)));
            Assert.That((fullScreen.Monitor, fullScreen.X, fullScreen.Y), Is.EqualTo((2, 0, 0)));
            Assert.That((borderless.Monitor, borderless.X, borderless.Y), Is.EqualTo((2, 50, 60)));
        }

        [Test]
        public void Equals_IgnoresFieldsTheModeDoesNotUse()
        {
            var a = new OCDisplaySettings(OCDisplayMode.Windowed, 0, 800, 600, 0, 0);
            var b = new OCDisplaySettings(OCDisplayMode.Windowed, 3, 800, 600, 10, 10);

            Assert.That(a, Is.EqualTo(b));
        }

        [Test]
        public void Store_WriteThenRead_RoundTrips()
        {
            var store = NewStore();
            var written = new OCDisplaySettings(OCDisplayMode.Borderless, 1, 1280, 720, 40, 30);

            OCDisplayStore.Write(store, written);

            Assert.That(OCDisplayStore.TryRead(store, out var read), Is.True);
            Assert.That(read, Is.EqualTo(written));
        }

        [Test]
        public void Store_WithoutOverrides_ReadsNothing()
        {
            Assert.That(OCDisplayStore.TryRead(NewStore(), out _), Is.False);
        }

        [Test]
        public void Store_Clear_RemovesOnlyDisplayOverrides()
        {
            var store = NewStore();
            store.Set("other", 1);
            OCDisplayStore.Write(store, new OCDisplaySettings(OCDisplayMode.FullScreen, 0, 1920, 1080, 0, 0));

            OCDisplayStore.Clear(store);

            Assert.That(OCDisplayStore.TryRead(store, out _), Is.False);
            Assert.That(store.HasOverride("other"), Is.True);
        }

        [Test]
        public void Store_DisplayKeys_DoNotWarnAboutMissingDefinition()
        {
            var store = NewStore();

            OCDisplayStore.Write(store, new OCDisplaySettings(OCDisplayMode.Windowed, 0, 800, 600, 0, 0));
            OCDisplayStore.TryRead(store, out _);

            LogAssert.NoUnexpectedReceived();
        }
    }
}
