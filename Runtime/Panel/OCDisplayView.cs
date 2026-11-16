#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The Runtime Panel's Display tab: Display Mode, monitor, size and position, with Apply (confirm or revert) and Reset.</summary>
    internal sealed class OCDisplayView : VisualElement, IOCLayoutAware
    {
        private const string ResetMessage = "Reset the display to how the app started?\nSaved display settings are removed.";
        private const string ResetButtonText = "Reset";
        private const float InputInset = 6f;

        private static readonly OCDisplayMode[] Modes = { OCDisplayMode.Windowed, OCDisplayMode.Borderless, OCDisplayMode.FullScreen };
        private static readonly string[] ModeNames = { "Windowed", "Borderless", "Full Screen" };

        private readonly OCDisplay _display;
        private readonly Action<string, string, Action> _requestConfirm;
        private readonly ScrollView _scroll = new(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden };
        private readonly VisualElement _controls = new();
        private readonly Label _now = new();
        private readonly Label _note = new("Display settings only work in a Windows build. The values below are read-only here.");
        private readonly Label _hint = new();
        private readonly Button[] _modeButtons = new Button[3];
        private readonly DropdownField _monitor = new();
        private readonly DropdownField _resolution = new();
        private readonly IntegerField _width = new() { isDelayed = true };
        private readonly IntegerField _height = new() { isDelayed = true };
        private readonly IntegerField _x = new() { isDelayed = true };
        private readonly IntegerField _y = new() { isDelayed = true };
        private readonly VisualElement _monitorRow;
        private readonly VisualElement _resolutionRow;
        private readonly VisualElement _widthRow;
        private readonly VisualElement _heightRow;
        private readonly VisualElement _xRow;
        private readonly VisualElement _yRow;
        private readonly Button _apply;
        private readonly Button _reset;
        private readonly VisualElement _overlay = new();
        private readonly Label _overlayTitle = new("Keep these display settings?");
        private readonly Label _overlayText = new("Reverting to the previous display in");
        private readonly Label _count = new();
        private readonly Button _keep;
        private readonly Button _revert;
        private readonly List<Label> _labels = new();
        private readonly List<VisualElement> _rows = new();
        private readonly List<string> _monitorNames = new();
        private readonly List<Vector2Int> _resolutions = new();
        private readonly List<string> _resolutionNames = new();

        private OCDisplayMode _mode;
        private int _monitorIndex;
        private int _w = OCDisplaySettings.MinWidth;
        private int _h = OCDisplaySettings.MinHeight;
        private int _px;
        private int _py;
        private bool _dirty = true;
        private int _shownSeconds = -1;

        public OCDisplayView(OCDisplay display, Action<string, string, Action> requestConfirm)
        {
            _display = display;
            _requestConfirm = requestConfirm;
            AddToClassList("ocp-display");
            _scroll.AddToClassList("ocp-scroll");
            Add(_scroll);
            var content = _scroll.contentContainer;

            _now.AddToClassList("ocp-display__now");
            _note.AddToClassList("ocp-display__note");
            _hint.AddToClassList("ocp-display__hint");
            content.Add(_now);
            content.Add(_note);
            content.Add(_controls);

            var modeField = new VisualElement();
            modeField.AddToClassList("ocp-display__modes");
            for (var i = 0; i < Modes.Length; i++)
            {
                var mode = Modes[i];
                var button = new Button(() => SelectMode(mode)) { text = ModeNames[i] };
                button.AddToClassList("ocp-button");
                _modeButtons[i] = button;
                modeField.Add(button);
            }
            AddRow("Display Mode", modeField);
            _monitorRow = AddRow("Monitor", Grow(_monitor));
            _resolutionRow = AddRow("Resolution", Grow(_resolution));
            _widthRow = AddRow("Width", Grow(_width), "px");
            _heightRow = AddRow("Height", Grow(_height), "px");
            _xRow = AddRow("Position X", Grow(_x), "px from the monitor's left");
            _yRow = AddRow("Position Y", Grow(_y), "px from the monitor's top");
            _controls.Add(_hint);

            var buttons = new VisualElement();
            buttons.AddToClassList("ocp-display__buttons");
            _apply = new Button(OnApplyClicked) { text = "Apply" };
            _apply.AddToClassList("ocp-button");
            _apply.AddToClassList("ocp-button--primary");
            _reset = new Button(() => _requestConfirm(ResetMessage, ResetButtonText, _display.Reset)) { text = "Reset to start-up display" };
            _reset.AddToClassList("ocp-button");
            _reset.AddToClassList("ocp-button--danger");
            buttons.Add(_apply);
            buttons.Add(_reset);
            _controls.Add(buttons);

            _overlay.AddToClassList("ocp-confirm");
            var box = new VisualElement();
            box.AddToClassList("ocp-confirm__box");
            _overlayTitle.AddToClassList("ocp-confirm__message");
            _overlayText.AddToClassList("ocp-confirm__message");
            _count.AddToClassList("ocp-display__count");
            var overlayButtons = new VisualElement();
            overlayButtons.AddToClassList("ocp-confirm__buttons");
            _revert = new Button(_display.Revert) { text = "Revert now" };
            _revert.AddToClassList("ocp-button");
            _revert.AddToClassList("ocp-button--ghost");
            _keep = new Button(_display.Confirm) { text = "Keep" };
            _keep.AddToClassList("ocp-button");
            _keep.AddToClassList("ocp-button--primary");
            overlayButtons.Add(_revert);
            overlayButtons.Add(_keep);
            box.Add(_overlayTitle);
            box.Add(_overlayText);
            box.Add(_count);
            box.Add(overlayButtons);
            _overlay.Add(box);
            Add(_overlay);

            _monitor.RegisterValueChangedCallback(_ => _monitorIndex = Mathf.Max(0, _monitor.index));
            _resolution.RegisterValueChangedCallback(_ => OnResolutionPicked());
            _width.RegisterValueChangedCallback(e => _w = Clamp(_width, e.newValue, OCDisplaySettings.MinWidth));
            _height.RegisterValueChangedCallback(e => _h = Clamp(_height, e.newValue, OCDisplaySettings.MinHeight));
            _x.RegisterValueChangedCallback(e => _px = e.newValue);
            _y.RegisterValueChangedCallback(e => _py = e.newValue);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                _display.Changed -= MarkDirty;
                _display.Changed += MarkDirty;
                MarkDirty();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => _display.Changed -= MarkDirty);
        }

        /// <summary>Loads the current Display into the fields, dropping edits not applied.</summary>
        public void Reload()
        {
            var current = _display.Current;
            _mode = current.Mode;
            _monitorIndex = current.Monitor;
            _w = current.Width;
            _h = current.Height;
            _px = current.X;
            _py = current.Y;
            OCDisplayDriver.GetMonitorNames(_monitorNames);
            _monitor.choices = _monitorNames;
            RebuildResolutions();
            ShowFields();
            MarkDirty();
        }

        public void Refresh()
        {
            if (_dirty)
            {
                _dirty = false;
                var supported = _display.IsSupported;
                var pending = _display.IsPending;
                _now.text = "Now: " + _display.Current;
                _note.style.display = supported ? DisplayStyle.None : DisplayStyle.Flex;
                _controls.SetEnabled(supported && !_display.IsBusy && !pending);
                _overlay.style.display = pending ? DisplayStyle.Flex : DisplayStyle.None;
                _shownSeconds = -1;
            }

            if (_display.IsPending)
            {
                var seconds = Mathf.CeilToInt(_display.SecondsLeft);
                if (seconds != _shownSeconds)
                {
                    _shownSeconds = seconds;
                    _count.text = seconds.ToString();
                }
            }
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
                row.Query(className: "unity-base-text-field__input").ForEach(input => input.style.height = inputHeight);
                row.Query(className: "unity-base-popup-field__input").ForEach(input => input.style.height = inputHeight);
            }
            foreach (var label in _labels) label.style.width = layout.LabelWidth;
            foreach (var button in _modeButtons)
            {
                button.style.fontSize = layout.FontSize;
                button.style.height = inputHeight;
            }
            foreach (var label in new[] { _now, _note, _hint, _overlayTitle, _overlayText })
                label.style.fontSize = label == _now || label == _overlayTitle ? layout.FontSize : layout.CommandDescriptionFontSize;
            _count.style.fontSize = layout.FontSize * 2f;
            foreach (var button in new[] { _apply, _reset, _keep, _revert })
            {
                button.style.fontSize = layout.FontSize;
                button.style.height = layout.RowHeight + InputInset;
            }
        }

        private void MarkDirty() => _dirty = true;

        private static int Clamp(IntegerField field, int value, int min)
        {
            var clamped = Math.Max(min, value);
            if (clamped != value) field.SetValueWithoutNotify(clamped);
            return clamped;
        }

        private void SelectMode(OCDisplayMode mode)
        {
            _mode = mode;
            RebuildResolutions();
            ShowFields();
        }

        private void OnResolutionPicked()
        {
            var index = _resolution.index;
            if (index < 0 || index >= _resolutions.Count) return;
            _w = _resolutions[index].x;
            _h = _resolutions[index].y;
            _width.SetValueWithoutNotify(_w);
            _height.SetValueWithoutNotify(_h);
        }

        private void OnApplyClicked() => _display.Try(new OCDisplaySettings(_mode, _monitorIndex, _w, _h, _px, _py));

        private void RebuildResolutions()
        {
            OCDisplayDriver.GetResolutions(_resolutions);
            var size = new Vector2Int(_w, _h);
            if (!_resolutions.Contains(size)) _resolutions.Insert(0, size);
            _resolutionNames.Clear();
            foreach (var r in _resolutions) _resolutionNames.Add($"{r.x} × {r.y}");
            _resolution.choices = _resolutionNames;
            _resolution.SetValueWithoutNotify(_resolutionNames[_resolutions.IndexOf(size)]);
        }

        private void ShowFields()
        {
            var windowed = _mode == OCDisplayMode.Windowed;
            var borderless = _mode == OCDisplayMode.Borderless;
            var fullScreen = _mode == OCDisplayMode.FullScreen;
            for (var i = 0; i < Modes.Length; i++)
            {
                _modeButtons[i].EnableInClassList("ocp-button--primary", Modes[i] == _mode);
                _modeButtons[i].EnableInClassList("ocp-button--ghost", Modes[i] != _mode);
            }
            SetShown(_monitorRow, !windowed);
            SetShown(_resolutionRow, fullScreen);
            SetShown(_widthRow, !fullScreen);
            SetShown(_heightRow, !fullScreen);
            SetShown(_xRow, borderless);
            SetShown(_yRow, borderless);
            _monitor.index = Mathf.Clamp(_monitorIndex, 0, Mathf.Max(0, _monitorNames.Count - 1));
            _width.SetValueWithoutNotify(_w);
            _height.SetValueWithoutNotify(_h);
            _x.SetValueWithoutNotify(_px);
            _y.SetValueWithoutNotify(_py);
            _hint.text = _mode switch
            {
                OCDisplayMode.Windowed   => "Windowed: set the size; Windows decides where the window sits.",
                OCDisplayMode.Borderless => "Borderless: set size and position; position counts from the chosen monitor's top-left.",
                _                         => "Full Screen: pick a resolution the monitor supports and which monitor to use."
            };
        }

        private VisualElement AddRow(string label, VisualElement field, string? suffix = null)
        {
            var row = new VisualElement();
            row.AddToClassList("ocp-row");
            var text = new Label(label);
            text.AddToClassList("ocp-label");
            text.AddToClassList("ocp-label--ellipsis");
            var holder = new VisualElement();
            holder.AddToClassList("ocp-field");
            holder.Add(field);
            if (suffix != null)
            {
                var unit = new Label(suffix);
                unit.AddToClassList("ocp-axis");
                holder.Add(unit);
            }
            row.Add(text);
            row.Add(holder);
            _controls.Add(row);
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
    }
}
