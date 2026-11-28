#nullable enable

namespace OperatorConsole
{
    // UI Toolkit gives a hidden ListView a zero-height viewport and a 0/0 scroller. Refreshing or scrolling it
    // then binds rows for the wrong offset, so both wait until the list is laid out again.
    internal sealed class OCLogScrollState
    {
        private const float BottomTolerance = 2f;

        public bool FollowsNewest { get; private set; } = true;

        public float Offset { get; private set; }

        public bool HasPendingRefresh { get; private set; }

        public void Observe(float viewportHeight, float value, float highValue)
        {
            if (!HasLayout(viewportHeight)) return;
            FollowsNewest = value >= highValue - BottomTolerance;
            Offset = value;
        }

        public void FollowNewest() => FollowsNewest = true;

        public bool TryRefreshNow(float viewportHeight)
        {
            HasPendingRefresh = !HasLayout(viewportHeight);
            return !HasPendingRefresh;
        }

        public bool TakePendingRefresh(float viewportHeight)
        {
            if (!HasPendingRefresh || !HasLayout(viewportHeight)) return false;
            HasPendingRefresh = false;
            return true;
        }

        private static bool HasLayout(float viewportHeight) => viewportHeight > 0f;
    }
}
