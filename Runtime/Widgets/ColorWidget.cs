#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    internal sealed class ColorWidget : OCWidget<Color>
    {
        private const float SwatchInset = 6f;

        private readonly VisualElement _swatch = new() { focusable = true, tooltip = "Open the colour picker" };
        private readonly TextField _hex = new() { isDelayed = true };
        private string _title = string.Empty;
        private float _fontSize = 20f;

        public ColorWidget()
        {
            _swatch.AddToClassList("ocp-swatch");
            _swatch.AddToClassList("ocp-swatch--button");
            _swatch.RegisterCallback<ClickEvent>(_ => OpenPicker());
            _swatch.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Space)) return;
                e.StopPropagation();
                OpenPicker();
            });
            _hex.textEdition.placeholder = "#RRGGBBAA";
            _hex.RegisterValueChangedCallback(e => Apply(e.newValue));
            Field.Add(_swatch);
            Field.Add(Grow(_hex));
        }

        protected override void OnSetup(OCEntryDefinition entry) => _title = entry.DisplayLabel;

        public override void ApplyLayout(OCPanelLayout layout)
        {
            base.ApplyLayout(layout);
            _fontSize = layout.FontSize;
            _swatch.style.width = layout.RowHeight - SwatchInset;
            _swatch.style.height = layout.RowHeight - SwatchInset;
        }

        protected override void Show(Color value)
        {
            _hex.SetValueWithoutNotify(OCColorCode.Format(value));
            _swatch.style.backgroundColor = value;
        }

        private void OpenPicker()
        {
            var picker = OCColorPicker.For(this);
            picker?.Open(_swatch, _title, Key, Store.Get(Key, Color.white), _fontSize, Commit);
        }

        private void Apply(string text)
        {
            if (OCColorCode.TryParse(text, out var color)) Commit(color);
            else Show(Store.Get(Key, Color.white));
        }
    }
}
