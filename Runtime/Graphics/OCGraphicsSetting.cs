#nullable enable

using System.Collections.Generic;

namespace OperatorConsole
{
    /// <summary>A Graphics Setting other than the Quality Level.</summary>
    public enum OCGraphicsSetting
    {
        VSyncCount,
        TargetFrameRate,
        TextureQuality,
        AnisotropicFiltering,
        LodBias,
        RenderScale,
        UpscalingFilter,
        MsaaSampleCount,
        Hdr,
        ShadowDistance,
        ShadowResolution,
        ShadowCascadeCount
    }

    internal enum OCGraphicsKind
    {
        Choice,
        Segments,
        Slider,
        Toggle
    }

    internal readonly struct OCGraphicsOption
    {
        public OCGraphicsOption(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public int Value { get; }
        public string Text { get; }
    }

    internal sealed class OCGraphicsSettingInfo
    {
        public OCGraphicsSettingInfo(OCGraphicsSetting setting, string key, string label, string header, OCGraphicsKind kind,
            bool needsUrp, bool followsQualityLevel, OCGraphicsOption[]? options = null, float min = 0f, float max = 0f, float step = 0f, string unit = "")
        {
            Setting = setting;
            Key = OCGraphicsCatalog.KeyPrefix + key;
            Label = label;
            Header = header;
            Kind = kind;
            NeedsUrp = needsUrp;
            FollowsQualityLevel = followsQualityLevel;
            Options = options ?? System.Array.Empty<OCGraphicsOption>();
            Min = min;
            Max = max;
            Step = step;
            Unit = unit;
        }

        public OCGraphicsSetting Setting { get; }
        public string Key { get; }
        public string Label { get; }
        public string Header { get; }
        public OCGraphicsKind Kind { get; }
        public bool NeedsUrp { get; }

        /// <summary>True when a Quality Level change resets this setting to the new preset. Frame-rate settings stay as they are.</summary>
        public bool FollowsQualityLevel { get; }

        public IReadOnlyList<OCGraphicsOption> Options { get; }
        public float Min { get; }
        public float Max { get; }
        public float Step { get; }
        public string Unit { get; }

        public bool IsInteger => Kind != OCGraphicsKind.Slider;

        public float Normalize(float value) => Kind switch
        {
            OCGraphicsKind.Slider => Snap(System.Math.Clamp(value, Min, Max)),
            OCGraphicsKind.Toggle => value != 0f ? 1f : 0f,
            _                      => (float)System.Math.Round(value)
        };

        public string OptionText(float value)
        {
            var v = (int)System.Math.Round(value);
            foreach (var option in Options)
                if (option.Value == v) return option.Text;
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private float Snap(float value) =>
            Step > 0f ? (float)System.Math.Round(Min + System.Math.Round((value - Min) / Step) * Step, 4) : value;
    }

    internal static class OCGraphicsCatalog
    {
        public const string KeyPrefix = "oc.graphics.";
        public const string QualityLevelKey = KeyPrefix + "qualityLevel";

        private const string FrameRate = "Frame rate";
        private const string Textures = "Textures & detail";
        private const string Image = "Image";
        private const string Shadows = "Shadows";

        public static readonly OCGraphicsSettingInfo[] All =
        {
            new(OCGraphicsSetting.VSyncCount, "vSyncCount", "VSync", FrameRate, OCGraphicsKind.Segments, false, false,
                new OCGraphicsOption[] { new(0, "Off"), new(1, "Every frame"), new(2, "Every 2nd") }),
            new(OCGraphicsSetting.TargetFrameRate, "targetFrameRate", "Target FPS", FrameRate, OCGraphicsKind.Choice, false, false,
                new OCGraphicsOption[] { new(30, "30"), new(60, "60"), new(120, "120"), new(144, "144"), new(-1, "Unlimited") }),
            new(OCGraphicsSetting.TextureQuality, "globalTextureMipmapLimit", "Texture Quality", Textures, OCGraphicsKind.Choice, false, true,
                new OCGraphicsOption[] { new(0, "Full"), new(1, "Half"), new(2, "Quarter"), new(3, "Eighth") }),
            new(OCGraphicsSetting.AnisotropicFiltering, "anisotropicFiltering", "Anisotropic", Textures, OCGraphicsKind.Choice, false, true,
                new OCGraphicsOption[] { new(0, "Off"), new(1, "Per texture"), new(2, "Forced on") }),
            new(OCGraphicsSetting.LodBias, "lodBias", "LOD Bias", Textures, OCGraphicsKind.Slider, false, true, min: 0.25f, max: 3f, step: 0.05f),
            new(OCGraphicsSetting.RenderScale, "renderScale", "Render Scale", Image, OCGraphicsKind.Slider, true, true, min: 0.5f, max: 2f, step: 0.05f, unit: "×"),
            new(OCGraphicsSetting.UpscalingFilter, "upscalingFilter", "Upscaling Filter", Image, OCGraphicsKind.Choice, true, true,
                new OCGraphicsOption[] { new(0, "Auto"), new(1, "Bilinear"), new(2, "Point"), new(3, "FSR 1.0"), new(4, "STP") }),
            new(OCGraphicsSetting.MsaaSampleCount, "msaaSampleCount", "MSAA", Image, OCGraphicsKind.Segments, true, true,
                new OCGraphicsOption[] { new(1, "Off"), new(2, "2x"), new(4, "4x"), new(8, "8x") }),
            new(OCGraphicsSetting.Hdr, "supportsHDR", "HDR", Image, OCGraphicsKind.Toggle, true, true),
            new(OCGraphicsSetting.ShadowDistance, "shadowDistance", "Shadow Distance", Shadows, OCGraphicsKind.Slider, true, true, min: 0f, max: 200f, step: 5f, unit: "m"),
            new(OCGraphicsSetting.ShadowResolution, "mainLightShadowmapResolution", "Shadow Resolution", Shadows, OCGraphicsKind.Choice, true, true,
                new OCGraphicsOption[] { new(256, "256"), new(512, "512"), new(1024, "1024"), new(2048, "2048"), new(4096, "4096") }),
            new(OCGraphicsSetting.ShadowCascadeCount, "shadowCascadeCount", "Shadow Cascades", Shadows, OCGraphicsKind.Segments, true, true,
                new OCGraphicsOption[] { new(1, "1"), new(2, "2"), new(3, "3"), new(4, "4") })
        };

        public static OCGraphicsSettingInfo Info(OCGraphicsSetting setting) => All[(int)setting];
    }
}
