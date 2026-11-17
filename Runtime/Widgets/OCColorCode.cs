#nullable enable

using System;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>Reads and writes the hex colour codes an Operator types: #RGB, #RGBA, #RRGGBB or #RRGGBBAA, with the # optional.</summary>
    internal static class OCColorCode
    {
        public static bool TryParse(string? text, out Color color)
        {
            color = default;
            if (text == null) return false;
            var trimmed = text.Trim();
            if (trimmed.Length == 0) return false;
            if (trimmed[0] != '#') trimmed = "#" + trimmed;

            for (var i = 1; i < trimmed.Length; i++)
                if (!Uri.IsHexDigit(trimmed[i])) return false;

            var digits = trimmed.Length - 1;
            if (digits is not (3 or 4 or 6 or 8)) return false;
            return ColorUtility.TryParseHtmlString(trimmed, out color);
        }

        /// <summary>#RRGGBB when opaque, otherwise #RRGGBBAA.</summary>
        public static string Format(Color color) => "#" + (color.a >= 1f
            ? ColorUtility.ToHtmlStringRGB(color)
            : ColorUtility.ToHtmlStringRGBA(color));
    }
}
