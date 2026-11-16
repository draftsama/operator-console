#nullable enable

using System;

namespace OperatorConsole
{
    public enum OCDisplayMode
    {
        Windowed,
        Borderless,
        FullScreen
    }

    /// <summary>One Display Setting set: how the app's window is presented. Fields a mode does not use are zero after <see cref="Normalized"/>.</summary>
    public readonly struct OCDisplaySettings : IEquatable<OCDisplaySettings>
    {
        public const int MinWidth = 320;
        public const int MinHeight = 240;

        public OCDisplaySettings(OCDisplayMode mode, int monitor, int width, int height, int x, int y)
        {
            Mode = mode;
            Monitor = monitor;
            Width = width;
            Height = height;
            X = x;
            Y = y;
        }

        public OCDisplayMode Mode { get; }

        /// <summary>Index of the monitor in the display layout. Unused by Windowed.</summary>
        public int Monitor { get; }

        public int Width { get; }
        public int Height { get; }

        /// <summary>Pixels from the chosen monitor's left edge. Used only by Borderless.</summary>
        public int X { get; }

        /// <summary>Pixels from the chosen monitor's top edge. Used only by Borderless.</summary>
        public int Y { get; }

        public OCDisplaySettings Normalized()
        {
            var width = Math.Max(MinWidth, Width);
            var height = Math.Max(MinHeight, Height);
            var monitor = Math.Max(0, Monitor);
            return Mode switch
            {
                OCDisplayMode.Windowed   => new OCDisplaySettings(Mode, 0, width, height, 0, 0),
                OCDisplayMode.Borderless => new OCDisplaySettings(Mode, monitor, width, height, X, Y),
                _                         => new OCDisplaySettings(Mode, monitor, width, height, 0, 0)
            };
        }

        public bool Equals(OCDisplaySettings other)
        {
            var a = Normalized();
            var b = other.Normalized();
            return a.Mode == b.Mode && a.Monitor == b.Monitor && a.Width == b.Width && a.Height == b.Height && a.X == b.X && a.Y == b.Y;
        }

        public override bool Equals(object? obj) => obj is OCDisplaySettings other && Equals(other);

        public override int GetHashCode()
        {
            var n = Normalized();
            return HashCode.Combine(n.Mode, n.Monitor, n.Width, n.Height, n.X, n.Y);
        }

        public override string ToString()
        {
            var n = Normalized();
            return n.Mode switch
            {
                OCDisplayMode.Windowed   => $"Windowed · {n.Width}×{n.Height}",
                OCDisplayMode.Borderless => $"Borderless · {n.Width}×{n.Height} · ({n.X}, {n.Y}) · monitor {n.Monitor + 1}",
                _                         => $"Full Screen · {n.Width}×{n.Height} · monitor {n.Monitor + 1}"
            };
        }
    }
}
