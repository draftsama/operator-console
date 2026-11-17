#nullable enable

using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>One Runtime Panel row for an Exposed Entry: an optional icon, a label with an optional description, then the widget's controls in <see cref="Field"/>.</summary>
    internal abstract class OCWidgetRow : VisualElement, IOCLayoutAware
    {
        private const float InputInset = 6f;
        private const float IconScale = 1.4f;
        private const float DescriptionScale = 0.8f;

        private static readonly Color LabelColor = new(238f / 255f, 240f / 255f, 244f / 255f);

        private readonly VisualElement _labelBox;
        private readonly VisualElement _icon;
        private readonly Label _label;
        private readonly Label _description;
        private float _iconSlot;
        private float _iconHeight;
        private bool _hasIcon;

        protected OCWidgetRow()
        {
            AddToClassList("ocp-row");

            _labelBox = new VisualElement();
            _labelBox.AddToClassList("ocp-label-box");
            Add(_labelBox);

            _icon = new VisualElement();
            _icon.AddToClassList("ocp-icon");
            _labelBox.Add(_icon);

            var text = new VisualElement();
            text.AddToClassList("ocp-label-text");
            _labelBox.Add(text);

            _label = new Label();
            _label.AddToClassList("ocp-label");
            text.Add(_label);

            _description = new Label();
            _description.AddToClassList("ocp-description");
            text.Add(_description);

            Field = new VisualElement();
            Field.AddToClassList("ocp-field");
            Add(Field);

            SetDescription(string.Empty);
            SetIcon(null, true, 0f);
        }

        /// <summary>The part of the row right of the label, where the widget and its Entry Action buttons go.</summary>
        public VisualElement Field { get; }

        public bool HasIcon => _hasIcon;

        /// <summary>Shows <paramref name="entry"/> and commits edits to it through <paramref name="store"/>.</summary>
        public abstract void Setup(OCEntryDefinition entry, OCStore store);

        /// <summary>Icon height for <paramref name="layout"/>: the Entry's own height, or 1.4× the font size when it has none.</summary>
        public float IconHeightFor(OCPanelLayout layout) => _iconHeight > 0f ? _iconHeight : layout.FontSize * IconScale;

        /// <summary>Gives the icon a column <paramref name="width"/> wide, kept as an empty gap when this row has no icon, so labels line up; 0 for no column.</summary>
        public void ReserveIconSlot(float width)
        {
            _iconSlot = width;
            UpdateIconDisplay();
        }

        protected void SetLabel(string text)
        {
            _label.text = text;
            _label.tooltip = text;
        }

        protected void SetDescription(string text)
        {
            var has = !string.IsNullOrWhiteSpace(text);
            _description.text = has ? text : string.Empty;
            _description.style.display = has ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList("ocp-row--described", has);
        }

        protected void SetIcon(Texture2D? icon, bool tint, float height)
        {
            _hasIcon = icon != null;
            _iconHeight = Mathf.Max(0f, height);
            _icon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
            _icon.style.unityBackgroundImageTintColor = tint ? LabelColor : Color.white;
            UpdateIconDisplay();
        }

        private void UpdateIconDisplay()
        {
            var has = _hasIcon;
            _icon.style.display = has || _iconSlot > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            _icon.style.visibility = has ? Visibility.Visible : Visibility.Hidden;
        }

        public virtual void ApplyLayout(OCPanelLayout layout)
        {
            var wrap = layout.LabelOverflow == OCLabelOverflow.Wrap;
            var described = ClassListContains("ocp-row--described");
            style.minHeight = layout.RowHeight;
            style.height = wrap || described ? StyleKeyword.Auto : layout.RowHeight;
            style.fontSize = layout.FontSize;

            _labelBox.style.width = layout.LabelWidth;
            _label.EnableInClassList("ocp-label--wrap", wrap);
            _label.EnableInClassList("ocp-label--ellipsis", !wrap);
            _description.style.fontSize = layout.FontSize * DescriptionScale;

            var iconHeight = IconHeightFor(layout);
            _icon.style.height = _hasIcon ? iconHeight : 0f;
            _icon.style.width = Mathf.Max(_iconSlot, _hasIcon ? iconHeight : 0f);

            var inputHeight = layout.RowHeight - InputInset;
            this.Query(className: "unity-base-text-field__input").ForEach(input => input.style.height = inputHeight);
            this.Query(className: "unity-base-popup-field__input").ForEach(input => input.style.height = inputHeight);
        }

        protected static TField Grow<TField>(TField field) where TField : VisualElement
        {
            field.AddToClassList("ocp-grow");
            return field;
        }

        protected static Label Axis(string text)
        {
            var label = new Label(text);
            label.AddToClassList("ocp-axis");
            return label;
        }
    }
}
