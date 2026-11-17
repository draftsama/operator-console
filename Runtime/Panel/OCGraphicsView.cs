#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The Runtime Panel's Graphics tab: Quality Level and the Graphics Settings, applied live, with a live frame rate and Reset.</summary>
    internal sealed class OCGraphicsView : VisualElement, IOCLayoutAware
    {
        private const string ResetMessage = "Reset graphics to how the app started?\nSaved graphics settings are removed.";
        private const string NoUrpMessage = "This app does not render with URP, so image and shadow settings are not available.";
        private const float InputInset = 6f;
        private const float SwitchWidth = 52f;
        private const float FpsInterval = 0.25f;

        private readonly OCGraphics _graphics;
        private readonly OCPerformance _performance;
        private readonly Action<string, string, Action> _requestConfirm;
        private readonly ScrollView _scroll = new(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
        private readonly Label _now = new();
        private readonly Label _note = new(NoUrpMessage);
        private readonly DropdownField _quality = new();
        private readonly Label _custom = new("CUSTOM");
        private readonly Label _count = new();
        private readonly Button _reset;
        private readonly List<SettingRow> _settings = new();
        private readonly List<OCHeaderRow> _headers = new();
        private readonly Dictionary<string, OCHeaderRow> _headerByName = new();
        private readonly List<VisualElement> _rows = new();
        private readonly List<Label> _labels = new();
        private readonly List<Button> _segmentButtons = new();
        private readonly List<string> _qualityNames = new();

        private bool _dirty = true;
        private float _nextFps;

        public OCGraphicsView(OCGraphics graphics, OCPerformance performance, Action<string, string, Action> requestConfirm)
        {
            _graphics = graphics;
            _performance = performance;
            _requestConfirm = requestConfirm;
            AddToClassList("ocp-graphics");
            _scroll.AddToClassList("ocp-scroll");
            Add(_scroll);
            var content = _scroll.contentContainer;

            _now.AddToClassList("ocp-graphics__now");
            _note.AddToClassList("ocp-graphics__note");
            content.Add(_now);
            content.Add(_note);

            _custom.AddToClassList("ocp-graphics__custom");
            var qualityField = new VisualElement();
            qualityField.AddToClassList("ocp-graphics__inline");
            qualityField.Add(Grow(_quality));
            qualityField.Add(_custom);
            content.Add(Row("Quality Level", qualityField, out _));
            _quality.RegisterValueChangedCallback(_ => OnQualityPicked());

            foreach (var info in OCGraphicsCatalog.All)
            {
                if (!_headerByName.ContainsKey(info.Header))
                {
                    var header = new OCHeaderRow(info.Header);
                    _headerByName.Add(info.Header, header);
                    _headers.Add(header);
                    content.Add(header);
                }
                var setting = new SettingRow(info, this);
                _settings.Add(setting);
                content.Add(setting.Root);
            }

            var buttons = new VisualElement();
            buttons.AddToClassList("ocp-graphics__buttons");
            _count.AddToClassList("ocp-graphics__count");
            _reset = new Button(() => _requestConfirm(ResetMessage, "Reset", _graphics.Reset)) { text = "Reset to start-up graphics" };
            _reset.AddToClassList("ocp-button");
            _reset.AddToClassList("ocp-button--danger");
            buttons.Add(_reset);
            buttons.Add(_count);
            content.Add(buttons);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _graphics.Changed -= MarkDirty;
                _graphics.Changed += MarkDirty;
                MarkDirty();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _graphics.Changed -= MarkDirty);
        }

        public void Reload()
        {
            _nextFps = 0f;
            MarkDirty();
            Refresh();
        }

        public void Refresh()
        {
            if (_dirty)
            {
                _dirty = false;
                Sync();
            }

            var now = Time.unscaledTime;
            if (now < _nextFps) return;
            _nextFps = now + FpsInterval;
            ShowFrameRate();
        }

        public void ApplyLayout(OCPanelLayout layout)
        {
            _scroll.verticalScroller.style.width = layout.ScrollbarWidth;
            _scroll.mouseWheelScrollSize = layout.ScrollSensitivity;
            var inputHeight = layout.RowHeight - InputInset;
            foreach (var row in _rows)
            {
                row.style.minHeight = layout.RowHeight;
                row.style.height = layout.RowHeight;
                row.style.fontSize = layout.FontSize;
                row.style.marginBottom = layout.RowSpacing;
                row.Query(className: "unity-base-popup-field__input").ForEach(input => input.style.height = inputHeight);
            }
            foreach (var label in _labels) label.style.width = layout.LabelWidth;
            foreach (var header in _headers) header.ApplyLayout(layout);
            foreach (var button in _segmentButtons)
            {
                button.style.fontSize = layout.FontSize;
                button.style.height = inputHeight;
            }
            _now.style.fontSize = layout.FontSize;
            _note.style.fontSize = layout.CommandDescriptionFontSize;
            _custom.style.fontSize = layout.CommandDescriptionFontSize;
            _count.style.fontSize = layout.CommandDescriptionFontSize;
            _reset.style.fontSize = layout.FontSize;
            _reset.style.height = layout.RowHeight + InputInset;
        }

        private void MarkDirty() => _dirty = true;

        private void Sync()
        {
            _qualityNames.Clear();
            foreach (var name in _graphics.QualityLevelNames) _qualityNames.Add(name);
            _quality.choices = _qualityNames;
            var level = _graphics.QualityLevel;
            _quality.SetValueWithoutNotify(level >= 0 && level < _qualityNames.Count ? _qualityNames[level] : string.Empty);
            _quality.SetEnabled(_qualityNames.Count > 1);

            var fineTuned = _graphics.FineTunedCount;
            SetShown(_custom, fineTuned > 0);
            var levelName = level >= 0 && level < _qualityNames.Count ? _qualityNames[level] : "the preset";
            _count.text = fineTuned == 0
                ? "Following " + levelName
                : fineTuned + (fineTuned == 1 ? " setting fine-tuned on " : " settings fine-tuned on ") + levelName;

            var anyUrpHidden = false;
            foreach (var header in _headers) SetShown(header, false);
            foreach (var setting in _settings)
            {
                var supported = _graphics.Supports(setting.Info.Setting);
                if (!supported && setting.Info.NeedsUrp) anyUrpHidden = true;
                SetShown(setting.Root, supported);
                if (!supported) continue;
                SetShown(_headerByName[setting.Info.Header], true);
                setting.Show(_graphics.Get(setting.Info.Setting), _graphics.IsFineTuned(setting.Info.Setting));
            }
            SetShown(_note, anyUrpHidden);
        }

        private void ShowFrameRate()
        {
            if (!_performance.IsMeasuring)
            {
                SetShown(_now, false);
                return;
            }
            SetShown(_now, true);
            var fps = _performance.CurrentFps;
            var target = Application.targetFrameRate;
            var slow = target > 0 && fps < target * 0.95f;
            _now.text = "Now  " + Mathf.RoundToInt(fps).ToString(CultureInfo.InvariantCulture) + " fps  ·  "
                        + _performance.LatestFrameMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms"
                        + (target > 0 ? (slow ? "  ·  below " : "  ·  holding ") + target.ToString(CultureInfo.InvariantCulture) + " fps" : string.Empty);
            _now.EnableInClassList("ocp-graphics__now--slow", slow);
        }

        private void OnQualityPicked()
        {
            var picked = _quality.index;
            var current = _graphics.QualityLevel;
            _quality.SetValueWithoutNotify(current >= 0 && current < _qualityNames.Count ? _qualityNames[current] : string.Empty);
            if (picked < 0 || picked == current) return;

            var fineTuned = _graphics.FineTunedCount;
            if (fineTuned == 0)
            {
                _graphics.SetQualityLevel(picked);
                return;
            }
            var message = "Switch Quality Level to " + _qualityNames[picked] + "?\nThe " + fineTuned
                          + (fineTuned == 1 ? " fine-tuned setting goes" : " fine-tuned settings go") + " back to the preset.";
            _requestConfirm(message, "Switch", () => _graphics.SetQualityLevel(picked));
        }

        private VisualElement Row(string label, VisualElement field, out VisualElement mark)
        {
            var row = new VisualElement();
            row.AddToClassList("ocp-row");
            mark = new VisualElement();
            mark.AddToClassList("ocp-graphics__mark");
            var text = new Label(label);
            text.AddToClassList("ocp-label");
            text.AddToClassList("ocp-label--ellipsis");
            var holder = new VisualElement();
            holder.AddToClassList("ocp-field");
            holder.Add(field);
            row.Add(mark);
            row.Add(text);
            row.Add(holder);
            _labels.Add(text);
            _rows.Add(row);
            return row;
        }

        private static TField Grow<TField>(TField field) where TField : VisualElement
        {
            field.AddToClassList("ocp-grow");
            return field;
        }

        private static void SetShown(VisualElement element, bool shown) =>
            element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

        private sealed class SettingRow
        {
            private readonly OCGraphicsView _view;
            private readonly VisualElement _mark;
            private readonly DropdownField? _choice;
            private readonly List<string>? _choiceNames;
            private readonly Button[]? _segments;
            private readonly Slider? _slider;
            private readonly Label? _value;
            private readonly Toggle? _toggle;

            public SettingRow(OCGraphicsSettingInfo info, OCGraphicsView view)
            {
                Info = info;
                _view = view;
                VisualElement field;
                switch (info.Kind)
                {
                    case OCGraphicsKind.Choice:
                        _choiceNames = new List<string>();
                        foreach (var option in info.Options) _choiceNames.Add(option.Text);
                        _choice = new DropdownField { choices = _choiceNames };
                        _choice.RegisterValueChangedCallback(_ => OnChoicePicked());
                        field = Grow(_choice);
                        break;
                    case OCGraphicsKind.Segments:
                        field = new VisualElement();
                        field.AddToClassList("ocp-graphics__segments");
                        _segments = new Button[info.Options.Count];
                        for (var i = 0; i < _segments.Length; i++)
                        {
                            var value = info.Options[i].Value;
                            var button = new Button(() => Commit(value)) { text = info.Options[i].Text };
                            button.AddToClassList("ocp-button");
                            _segments[i] = button;
                            view._segmentButtons.Add(button);
                            field.Add(button);
                        }
                        break;
                    case OCGraphicsKind.Slider:
                        field = new VisualElement();
                        field.AddToClassList("ocp-graphics__inline");
                        _slider = new Slider(info.Min, info.Max);
                        _slider.RegisterValueChangedCallback(e => Commit(e.newValue));
                        _value = new Label();
                        _value.AddToClassList("ocp-graphics__value");
                        field.Add(Grow(_slider));
                        field.Add(_value);
                        break;
                    default:
                        _toggle = new Toggle();
                        _toggle.AddToClassList("ocp-switch");
                        var knob = new VisualElement();
                        knob.AddToClassList("ocp-switch-knob");
                        var checkmark = _toggle.Q(className: Toggle.checkmarkUssClassName);
                        checkmark.Add(knob);
                        // The default theme's checked-checkmark image rule outranks any USS selector here.
                        checkmark.style.backgroundImage = StyleKeyword.None;
                        var input = _toggle.Q(className: Toggle.inputUssClassName);
                        input.style.flexBasis = StyleKeyword.Auto;
                        input.style.width = SwitchWidth;
                        input.style.flexShrink = 0f;
                        _toggle.RegisterValueChangedCallback(e => Commit(e.newValue ? 1f : 0f));
                        field = _toggle;
                        break;
                }
                Root = view.Row(info.Label, field, out _mark);
            }

            public OCGraphicsSettingInfo Info { get; }
            public VisualElement Root { get; }

            public void Show(float value, bool fineTuned)
            {
                _mark.EnableInClassList("ocp-graphics__mark--on", fineTuned);
                if (_choice != null) _choice.SetValueWithoutNotify(Info.OptionText(value));
                if (_segments != null)
                {
                    var whole = Mathf.RoundToInt(value);
                    for (var i = 0; i < _segments.Length; i++)
                    {
                        var selected = Info.Options[i].Value == whole;
                        _segments[i].EnableInClassList("ocp-button--primary", selected);
                        _segments[i].EnableInClassList("ocp-button--ghost", !selected);
                    }
                }
                if (_slider != null)
                {
                    _slider.SetValueWithoutNotify(value);
                    _value!.text = SliderText(value);
                }
                _toggle?.SetValueWithoutNotify(value != 0f);
            }

            private string SliderText(float value) => Info.Setting switch
            {
                OCGraphicsSetting.RenderScale    => Mathf.RoundToInt(value * 100f).ToString(CultureInfo.InvariantCulture) + "%",
                OCGraphicsSetting.ShadowDistance => value.ToString("0", CultureInfo.InvariantCulture) + " m",
                _                                 => value.ToString("0.00", CultureInfo.InvariantCulture)
            };

            private void OnChoicePicked()
            {
                var index = _choice!.index;
                if (index >= 0 && index < Info.Options.Count) Commit(Info.Options[index].Value);
            }

            private void Commit(float value) => _view._graphics.Set(Info.Setting, value);
        }
    }
}
