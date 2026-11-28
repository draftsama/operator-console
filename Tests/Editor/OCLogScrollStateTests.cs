#nullable enable

using NUnit.Framework;

namespace OperatorConsole.Tests
{
    [TestFixture]
    internal sealed class OCLogScrollStateTests
    {
        [Test]
        public void RefreshWhileHidden_WaitsForLayoutAndStillFollowsNewest()
        {
            var state = new OCLogScrollState();
            state.Observe(500f, 1000f, 1000f);

            state.Observe(0f, 0f, 0f);
            var refreshedWhileHidden = state.TryRefreshNow(0f);

            Assert.IsFalse(refreshedWhileHidden);
            Assert.IsFalse(state.TakePendingRefresh(0f));
            Assert.IsTrue(state.TakePendingRefresh(500f));
            Assert.IsFalse(state.TakePendingRefresh(500f));
            Assert.IsTrue(state.FollowsNewest);
        }

        [Test]
        public void ScrolledUpThenHidden_KeepsScrollPosition()
        {
            var state = new OCLogScrollState();
            state.Observe(500f, 200f, 1000f);

            state.Observe(0f, 0f, 0f);

            Assert.IsFalse(state.FollowsNewest);
            Assert.AreEqual(200f, state.Offset);
        }
    }
}
