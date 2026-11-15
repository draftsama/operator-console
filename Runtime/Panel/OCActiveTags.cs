#nullable enable

using System;
using UnityEngine;

namespace OperatorConsole
{
    /// <summary>Sets a Runtime Panel's Active Tags while this component is enabled, and returns them to the Default Tags when it is disabled, unless something else changed them since.</summary>
    [AddComponentMenu("Operator Console/Active Tags")]
    public sealed class OCActiveTags : MonoBehaviour
    {
        [SerializeField, Tooltip("Name of the Panel Definition whose Runtime Panel to change.")]
        private string _panelName = string.Empty;

        [SerializeField, Tooltip("Tag names, as declared in the Config. At least one.")]
        private string[] _tags = Array.Empty<string>();

        private OCRuntimePanel? _panel;
        private int _appliedVersion;
        private bool _started;

        public string PanelName => _panelName;

        // Runtime Panels are created after the first scene's OnEnable, so the first apply waits for Start.
        private void Start()
        {
            _started = true;
            Apply();
        }

        private void OnEnable()
        {
            if (_started) Apply();
        }

        private void OnDisable()
        {
            if (_panel != null && _panel.ActiveTagsVersion == _appliedVersion) _panel.ResetActiveTags();
            _panel = null;
        }

        private void Apply()
        {
            var panel = OC.Panels.Get(_panelName);
            if (panel == null || !panel.SetActiveTags(_tags)) return;
            _panel = panel;
            _appliedVersion = panel.ActiveTagsVersion;
        }
    }
}
