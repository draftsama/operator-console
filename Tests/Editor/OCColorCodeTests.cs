#nullable enable

using NUnit.Framework;
using UnityEngine;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCColorCodeTests
    {
        [TestCase("#E84034", 232, 64, 52, 255)]
        [TestCase("e84034", 232, 64, 52, 255)]
        [TestCase("  #e84034  ", 232, 64, 52, 255)]
        [TestCase("#F00", 255, 0, 0, 255)]
        [TestCase("#F008", 255, 0, 0, 136)]
        [TestCase("#14285099", 20, 40, 80, 153)]
        public void TryParse_AcceptsHexCodes(string text, int r, int g, int b, int a)
        {
            Assert.That(OCColorCode.TryParse(text, out var color), Is.True);
            Assert.That((Color32)color, Is.EqualTo(new Color32((byte)r, (byte)g, (byte)b, (byte)a)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("#")]
        [TestCase("red")]
        [TestCase("#12345")]
        [TestCase("#GGGGGG")]
        [TestCase("#123456789")]
        public void TryParse_RejectsAnythingElse(string? text)
        {
            Assert.That(OCColorCode.TryParse(text, out _), Is.False);
        }

        [Test]
        public void Format_DropsAlphaOnlyWhenOpaque()
        {
            Assert.That(OCColorCode.Format(new Color32(232, 64, 52, 255)), Is.EqualTo("#E84034"));
            Assert.That(OCColorCode.Format(new Color32(20, 40, 80, 153)), Is.EqualTo("#14285099"));
        }
    }
}
