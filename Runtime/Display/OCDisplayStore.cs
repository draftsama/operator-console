#nullable enable

namespace OperatorConsole
{
    /// <summary>Reads and writes the Display Settings as Overrides under the package's fixed keys.</summary>
    internal static class OCDisplayStore
    {
        public const string KeyPrefix = "oc.display.";
        public const string ModeKey = KeyPrefix + "mode";
        public const string MonitorKey = KeyPrefix + "monitor";
        public const string WidthKey = KeyPrefix + "width";
        public const string HeightKey = KeyPrefix + "height";
        public const string XKey = KeyPrefix + "x";
        public const string YKey = KeyPrefix + "y";

        private static readonly string[] AllKeys = { ModeKey, MonitorKey, WidthKey, HeightKey, XKey, YKey };

        public static bool TryRead(OCStore store, out OCDisplaySettings settings)
        {
            if (!store.HasOverride(ModeKey))
            {
                settings = default;
                return false;
            }

            var mode = (OCDisplayMode)store.Get(ModeKey, (int)OCDisplayMode.Windowed);
            settings = new OCDisplaySettings(
                mode,
                store.Get(MonitorKey, 0),
                store.Get(WidthKey, OCDisplaySettings.MinWidth),
                store.Get(HeightKey, OCDisplaySettings.MinHeight),
                store.Get(XKey, 0),
                store.Get(YKey, 0)).Normalized();
            return true;
        }

        public static void Write(OCStore store, OCDisplaySettings settings)
        {
            var n = settings.Normalized();
            store.Set(ModeKey, (int)n.Mode);
            store.Set(MonitorKey, n.Monitor);
            store.Set(WidthKey, n.Width);
            store.Set(HeightKey, n.Height);
            store.Set(XKey, n.X);
            store.Set(YKey, n.Y);
        }

        public static void Clear(OCStore store)
        {
            foreach (var key in AllKeys) store.Reset(key);
        }
    }
}
