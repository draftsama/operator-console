#nullable enable

using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCSpikeRecorderTests
    {
        [Test]
        public void Tick_RecordsFramesOverThresholdThenWaitsOutTheCooldown()
        {
            var recorder = new OCSpikeRecorder { ThresholdMs = 33.3f };

            Assert.That(recorder.Tick(20f), Is.False);
            Assert.That(recorder.Tick(40f), Is.True);
            for (var i = 0; i < OCSpikeRecorder.CooldownFrames; i++) Assert.That(recorder.Tick(80f), Is.False);
            Assert.That(recorder.Tick(80f), Is.True);

            recorder.IsRecording = false;
            for (var i = 0; i < OCSpikeRecorder.CooldownFrames; i++) recorder.Tick(10f);
            Assert.That(recorder.Tick(80f), Is.False);
        }

        [Test]
        public void Add_KeepsNewestFirstAndDropsTheOldestPastCapacity()
        {
            var recorder = new OCSpikeRecorder();
            for (var i = 0; i < OCSpikeRecorder.Capacity + 5; i++) recorder.Add(new OCSpike { Frame = i, FrameMs = 40f });

            Assert.That(recorder.Spikes.Count, Is.EqualTo(OCSpikeRecorder.Capacity));
            Assert.That(recorder.Spikes[0].Frame, Is.EqualTo(OCSpikeRecorder.Capacity + 4));
            Assert.That(recorder.Spikes[OCSpikeRecorder.Capacity - 1].Frame, Is.EqualTo(5));
            var version = recorder.Version;
            recorder.Clear();
            Assert.That(recorder.Spikes.Count, Is.Zero);
            Assert.That(recorder.Version, Is.Not.EqualTo(version));
        }

        [Test]
        public void Classify_WithBreakdown_NamesTheBiggestCategoryUnlessGcOrLoadingDominates()
        {
            var scripts = Breakdown(40f, (OCFrameCategory.Scripts, 24f), (OCFrameCategory.Rendering, 6f));
            var gc = Breakdown(40f, (OCFrameCategory.Scripts, 26f), (OCFrameCategory.GC, 12f));
            var loading = Breakdown(60f, (OCFrameCategory.Rendering, 30f), (OCFrameCategory.Loading, 20f));
            var other = Breakdown(40f, (OCFrameCategory.UI, 2f));

            Assert.That(OCSpikeRecorder.Classify(scripts), Is.EqualTo(OCSpikeCause.Scripts));
            Assert.That(OCSpikeRecorder.Classify(gc), Is.EqualTo(OCSpikeCause.GC));
            Assert.That(OCSpikeRecorder.Classify(loading), Is.EqualTo(OCSpikeCause.Loading));
            Assert.That(OCSpikeRecorder.Classify(other), Is.EqualTo(OCSpikeCause.Other));
        }

        [Test]
        public void Classify_WithoutBreakdown_FallsBackToGcLoadingGpuThenCpu()
        {
            Assert.That(OCSpikeRecorder.Classify(new OCSpike { FrameMs = 40f, GcCollected = true, SceneLoaded = true }), Is.EqualTo(OCSpikeCause.GC));
            Assert.That(OCSpikeRecorder.Classify(new OCSpike { FrameMs = 40f, SceneLoaded = true }), Is.EqualTo(OCSpikeCause.Loading));
            Assert.That(OCSpikeRecorder.Classify(new OCSpike { FrameMs = 40f, CpuMs = 10f, GpuMs = 35f }), Is.EqualTo(OCSpikeCause.Gpu));
            Assert.That(OCSpikeRecorder.Classify(new OCSpike { FrameMs = 40f, CpuMs = 35f }), Is.EqualTo(OCSpikeCause.Cpu));
        }

        [Test]
        public void Marks_ReportTheSlowestNameOfTheFrameJustEnded()
        {
            OCMarks.EndFrame();
            using (OC.Mark("Tests.Fast")) { }
            using (OC.Mark("Tests.Slow")) System.Threading.Thread.Sleep(5);

            OCMarks.EndFrame();
            Assert.That(OCMarks.LastFrameTopName, Is.EqualTo("Tests.Slow"));
            Assert.That(OCMarks.LastFrameTopMs, Is.GreaterThan(1f));

            OCMarks.EndFrame();
            Assert.That(OCMarks.LastFrameTopName, Is.Null);
        }

        private static OCSpike Breakdown(float frameMs, params (OCFrameCategory Category, float Ms)[] times)
        {
            var spike = new OCSpike { FrameMs = frameMs, HasBreakdown = true };
            foreach (var (category, ms) in times) spike.CategoryTimes[(int)category] = ms;
            return spike;
        }
    }
}
