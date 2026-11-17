#nullable enable

using System.Collections.Generic;

namespace OperatorConsole
{
    /// <summary>Where Graphics values are read from and written to: Unity's quality and pipeline settings in the app, a fake in tests.</summary>
    internal interface IOCGraphicsBackend
    {
        IReadOnlyList<string> QualityLevelNames { get; }
        int QualityLevel { get; }

        /// <summary>Switches the Quality Level, which resets every setting that follows it to the new preset.</summary>
        void SetQualityLevel(int level);

        bool Supports(OCGraphicsSetting setting);
        float Read(OCGraphicsSetting setting);
        void Write(OCGraphicsSetting setting, float value);

        /// <summary>Puts back the project's own pipeline assets, dropping the runtime copies that took the writes.</summary>
        void ReleaseCopies();
    }
}
