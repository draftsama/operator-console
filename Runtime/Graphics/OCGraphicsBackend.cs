#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if OC_URP
using UnityEngine.Rendering.Universal;
#endif

namespace OperatorConsole
{
    /// <summary>Reads and writes Graphics through QualitySettings and, with URP installed, a runtime copy of the active URP asset so the project's asset is never changed.</summary>
    internal sealed class OCGraphicsBackend : IOCGraphicsBackend
    {
        private readonly List<string> _names = new();

#if OC_URP
        private UniversalRenderPipelineAsset? _original;
        private UniversalRenderPipelineAsset? _copy;
        private bool _copyOnQualityLevel;
#endif

        public IReadOnlyList<string> QualityLevelNames
        {
            get
            {
                _names.Clear();
                _names.AddRange(QualitySettings.names);
                return _names;
            }
        }

        public int QualityLevel => QualitySettings.GetQualityLevel();

        public void SetQualityLevel(int level) => QualitySettings.SetQualityLevel(level, true);

        public bool Supports(OCGraphicsSetting setting)
        {
            if (!OCGraphicsCatalog.Info(setting).NeedsUrp) return true;
#if OC_URP
            return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
            return false;
#endif
        }

        public float Read(OCGraphicsSetting setting)
        {
            switch (setting)
            {
                case OCGraphicsSetting.VSyncCount: return QualitySettings.vSyncCount;
                case OCGraphicsSetting.TargetFrameRate: return Application.targetFrameRate;
                case OCGraphicsSetting.TextureQuality: return QualitySettings.globalTextureMipmapLimit;
                case OCGraphicsSetting.AnisotropicFiltering: return (int)QualitySettings.anisotropicFiltering;
                case OCGraphicsSetting.LodBias: return QualitySettings.lodBias;
            }
#if OC_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                switch (setting)
                {
                    case OCGraphicsSetting.RenderScale: return urp.renderScale;
                    case OCGraphicsSetting.UpscalingFilter: return (int)urp.upscalingFilter;
                    case OCGraphicsSetting.MsaaSampleCount: return urp.msaaSampleCount;
                    case OCGraphicsSetting.Hdr: return urp.supportsHDR ? 1f : 0f;
                    case OCGraphicsSetting.ShadowDistance: return urp.shadowDistance;
                    case OCGraphicsSetting.ShadowResolution: return urp.mainLightShadowmapResolution;
                    case OCGraphicsSetting.ShadowCascadeCount: return urp.shadowCascadeCount;
                }
            }
#endif
            return 0f;
        }

        public void Write(OCGraphicsSetting setting, float value)
        {
            var whole = Mathf.RoundToInt(value);
            switch (setting)
            {
                case OCGraphicsSetting.VSyncCount: QualitySettings.vSyncCount = whole; return;
                case OCGraphicsSetting.TargetFrameRate: Application.targetFrameRate = whole; return;
                case OCGraphicsSetting.TextureQuality: QualitySettings.globalTextureMipmapLimit = whole; return;
                case OCGraphicsSetting.AnisotropicFiltering: QualitySettings.anisotropicFiltering = (AnisotropicFiltering)whole; return;
                case OCGraphicsSetting.LodBias: QualitySettings.lodBias = value; return;
            }
#if OC_URP
            var urp = EnsureCopy();
            if (urp == null) return;
            switch (setting)
            {
                case OCGraphicsSetting.RenderScale: urp.renderScale = value; break;
                case OCGraphicsSetting.UpscalingFilter: urp.upscalingFilter = (UpscalingFilterSelection)whole; break;
                case OCGraphicsSetting.MsaaSampleCount: urp.msaaSampleCount = whole; break;
                case OCGraphicsSetting.Hdr: urp.supportsHDR = whole != 0; break;
                case OCGraphicsSetting.ShadowDistance: urp.shadowDistance = value; break;
                case OCGraphicsSetting.ShadowResolution: urp.mainLightShadowmapResolution = whole; break;
                case OCGraphicsSetting.ShadowCascadeCount: urp.shadowCascadeCount = whole; break;
            }
#endif
        }

        public void ReleaseCopies()
        {
#if OC_URP
            if (_copy == null) return;
            if (_copyOnQualityLevel)
            {
                if (QualitySettings.renderPipeline == _copy) QualitySettings.renderPipeline = _original;
            }
            else if (GraphicsSettings.defaultRenderPipeline == _copy)
            {
                GraphicsSettings.defaultRenderPipeline = _original;
            }
            Object.Destroy(_copy);
            _copy = null;
            _original = null;
#endif
        }

#if OC_URP
        private UniversalRenderPipelineAsset? EnsureCopy()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset current) return null;
            if (current == _copy) return _copy;
            ReleaseCopies();
            _original = current;
            _copy = Object.Instantiate(current);
            _copy.name = current.name + " (Operator Console)";
            _copy.hideFlags = HideFlags.DontSave;
            _copyOnQualityLevel = QualitySettings.renderPipeline != null;
            if (_copyOnQualityLevel) QualitySettings.renderPipeline = _copy;
            else GraphicsSettings.defaultRenderPipeline = _copy;
            return _copy;
        }
#endif
    }
}
