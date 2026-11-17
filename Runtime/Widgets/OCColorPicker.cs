#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>
    /// The popup a Color row opens on the Runtime Panel: a saturation/value square, hue and alpha bars, a colour code
    /// field and the colours applied recently. Nothing is written until Apply; Cancel, Esc or a tap outside leave the Value alone.
    /// One picker lives in each panel card and is reused by every Color row in it.
    /// </summary>
    internal sealed class OCColorPicker : VisualElement
    {
        private const string CardClass = "ocp";
        private const string CodeHint = "#RGB, #RRGGBB or #RRGGBBAA";
        private const string CodeError = "Not a colour code. Keeping the last valid colour.";
        private const int SvSize = 64;
        private const int BarWidth = 128;
        private const int AlphaHeight = 8;
        private const int CheckerCell = 4;
        private const int MaxRecent = 8;
        private const float Gap = 8f;
        private const float Edge = 12f;

        private static readonly Color32 CheckerLight = new(136, 136, 136, 255);
        private static readonly Color32 CheckerDark = new(85, 85, 85, 255);

        private readonly VisualElement _card;
        private readonly VisualElement _scrim;
        private readonly Label _title;
        private readonly Label _key;
        private readonly VisualElement _sv;
        private readonly VisualElement _svKnob;
        private readonly VisualElement _hue;
        private readonly VisualElement _hueKnob;
        private readonly VisualElement _alpha;
        private readonly VisualElement _alphaKnob;
        private readonly VisualElement _before;
        private readonly VisualElement _after;
        private readonly TextField _code;
        private readonly Label _hint;
        private readonly VisualElement _recentRow;
        private readonly List<Color> _recent = new();
        private readonly Color32[] _svPixels = new Color32[SvSize * SvSize];
        private readonly Color32[] _alphaPixels = new Color32[BarWidth * AlphaHeight];

        private Texture2D? _svTexture;
        private Texture2D? _hueTexture;
        private Texture2D? _alphaTexture;
        private VisualElement? _anchor;
        private Action<Color>? _onApply;
        private Color _start;
        private float _h;
        private float _s;
        private float _v;
        private float _a = 1f;
        private float _svHue = -1f;
        private bool _writingCode;

        private OCColorPicker(VisualElement card)
        {
            _card = card;
            AddToClassList("ocp-picker");
            focusable = true;

            _scrim = new VisualElement();
            _scrim.AddToClassList("ocp-picker-scrim");
            _scrim.RegisterCallback<PointerDownEvent>(e =>
            {
                e.StopPropagation();
                Close();
            });

            var head = new VisualElement();
            head.AddToClassList("ocp-picker__head");
            _title = new Label();
            _title.AddToClassList("ocp-picker__title");
            _key = new Label();
            _key.AddToClassList("ocp-picker__key");
            head.Add(_title);
            head.Add(_key);
            Add(head);

            _sv = new VisualElement();
            _sv.AddToClassList("ocp-picker__sv");
            _svKnob = Knob(_sv);
            Add(_sv);
            Drag(_sv, p =>
            {
                _s = p.x;
                _v = 1f - p.y;
                Redraw(true);
            });

            _hue = new VisualElement();
            _hue.AddToClassList("ocp-picker__bar");
            _hueKnob = Knob(_hue);
            Add(_hue);
            Drag(_hue, p =>
            {
                _h = Mathf.Min(p.x, 0.9999f);
                Redraw(true);
            });

            _alpha = new VisualElement();
            _alpha.AddToClassList("ocp-picker__bar");
            _alphaKnob = Knob(_alpha);
            Add(_alpha);
            Drag(_alpha, p =>
            {
                _a = Mathf.Round(p.x * 100f) / 100f;
                Redraw(true);
            });

            var compare = new VisualElement();
            compare.AddToClassList("ocp-picker__compare");
            _before = CompareChip(compare, "before");
            _before.tooltip = "Back to the starting colour";
            _before.RegisterCallback<ClickEvent>(_ => SetColor(_start));
            _after = CompareChip(compare, "after");
            Add(compare);

            var codeRow = new VisualElement();
            codeRow.AddToClassList("ocp-picker__code-row");
            var codeLabel = new Label("Code");
            codeLabel.AddToClassList("ocp-picker__code-label");
            _code = new TextField { maxLength = 9 };
            _code.AddToClassList("ocp-grow");
            _code.RegisterValueChangedCallback(e => OnCodeTyped(e.newValue));
            _code.RegisterCallback<FocusOutEvent>(_ => WriteCode());
            _code.RegisterCallback<KeyDownEvent>(OnCodeKey, TrickleDown.TrickleDown);
            codeRow.Add(codeLabel);
            codeRow.Add(_code);
            Add(codeRow);

            _hint = new Label(CodeHint);
            _hint.AddToClassList("ocp-picker__hint");
            Add(_hint);

            _recentRow = new VisualElement();
            _recentRow.AddToClassList("ocp-picker__recent");
            Add(_recentRow);

            var actions = new VisualElement();
            actions.AddToClassList("ocp-picker__actions");
            var cancel = new Button(Close) { text = "Cancel" };
            cancel.AddToClassList("ocp-button");
            var apply = new Button(Apply) { text = "Apply" };
            apply.AddToClassList("ocp-button");
            apply.AddToClassList("ocp-button--primary");
            actions.Add(cancel);
            actions.Add(apply);
            Add(actions);

            RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.Escape) return;
                e.StopPropagation();
                Close();
            });
            RegisterCallback<GeometryChangedEvent>(_ => Place());
            RegisterCallback<DetachFromPanelEvent>(_ => DestroyTextures());

            SetShown(false);
            card.Add(_scrim);
            card.Add(this);
        }

        public bool IsOpen => style.display == DisplayStyle.Flex;

        /// <summary>The picker of the panel card holding <paramref name="row"/>, created on first use; null when the row is not in a card.</summary>
        public static OCColorPicker? For(VisualElement row)
        {
            var card = row.parent;
            while (card != null && !card.ClassListContains(CardClass)) card = card.parent;
            if (card == null) return null;
            return card.Q<OCColorPicker>() ?? new OCColorPicker(card);
        }

        /// <summary>Shows the picker next to <paramref name="anchor"/>, starting at <paramref name="start"/>; <paramref name="onApply"/> gets the colour on Apply only.</summary>
        public void Open(VisualElement anchor, string title, string key, Color start, float fontSize, Action<Color> onApply)
        {
            EnsureTextures();
            _anchor = anchor;
            _onApply = onApply;
            _start = start;
            _title.text = title;
            _key.text = key;
            _before.style.backgroundColor = start;
            ApplySize(fontSize);
            BuildRecent();
            SetColor(start);

            _scrim.BringToFront();
            BringToFront();
            SetShown(true);
            Place();
            _code.Focus();
        }

        public void Close()
        {
            if (!IsOpen) return;
            SetShown(false);
            _onApply = null;
            var anchor = _anchor;
            _anchor = null;
            anchor?.Focus();
        }

        private void Apply()
        {
            var color = Current();
            Remember(color);
            var onApply = _onApply;
            Close();
            onApply?.Invoke(color);
        }

        private void Remember(Color color)
        {
            var code = OCColorCode.Format(color);
            for (var i = _recent.Count - 1; i >= 0; i--)
                if (OCColorCode.Format(_recent[i]) == code) _recent.RemoveAt(i);
            _recent.Insert(0, color);
            if (_recent.Count > MaxRecent) _recent.RemoveRange(MaxRecent, _recent.Count - MaxRecent);
        }

        private void BuildRecent()
        {
            _recentRow.Clear();
            _recentRow.style.display = _recent.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var color in _recent)
            {
                var chip = new VisualElement { tooltip = OCColorCode.Format(color) };
                chip.AddToClassList("ocp-picker__recent-chip");
                chip.style.backgroundColor = color;
                chip.RegisterCallback<ClickEvent>(_ => SetColor(color));
                _recentRow.Add(chip);
            }
        }

        private void SetColor(Color color)
        {
            Color.RGBToHSV(color, out var h, out var s, out var v);
            if (s > 0f && v > 0f) _h = h;
            _s = s;
            _v = v;
            _a = color.a;
            Redraw(true);
        }

        private Color Current()
        {
            var color = Color.HSVToRGB(_h, _s, _v);
            color.a = _a;
            return color;
        }

        private void Redraw(bool writeCode)
        {
            if (!Mathf.Approximately(_svHue, _h)) FillSv();
            FillAlpha();

            Position(_svKnob, _s, 1f - _v);
            Position(_hueKnob, _h, 0.5f);
            Position(_alphaKnob, _a, 0.5f);
            _after.style.backgroundColor = Current();

            if (writeCode) WriteCode();
        }

        private void WriteCode()
        {
            _writingCode = true;
            _code.SetValueWithoutNotify(OCColorCode.Format(Current()));
            _writingCode = false;
            ShowCodeError(false);
        }

        private void OnCodeTyped(string text)
        {
            if (_writingCode) return;
            if (!OCColorCode.TryParse(text, out var color))
            {
                ShowCodeError(true);
                return;
            }

            Color.RGBToHSV(color, out var h, out var s, out var v);
            if (s > 0f && v > 0f) _h = h;
            _s = s;
            _v = v;
            _a = color.a;
            Redraw(false);
            ShowCodeError(false);
        }

        private void OnCodeKey(KeyDownEvent e)
        {
            if (e.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter)) return;
            e.StopPropagation();
            if (OCColorCode.TryParse(_code.value, out _)) Apply();
        }

        private void ShowCodeError(bool error)
        {
            _code.EnableInClassList("ocp-field--error", error);
            _hint.EnableInClassList("ocp-picker__hint--error", error);
            _hint.text = error ? CodeError : CodeHint;
        }

        private void SetShown(bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            style.display = display;
            _scrim.style.display = display;
        }

        private void ApplySize(float fontSize)
        {
            style.fontSize = fontSize;
            style.width = fontSize * 17f;
            _sv.style.height = fontSize * 9f;
            _hue.style.height = fontSize * 0.9f;
            _alpha.style.height = fontSize * 0.9f;
            foreach (var knob in new[] { _svKnob, _hueKnob, _alphaKnob })
            {
                knob.style.width = fontSize * 0.95f;
                knob.style.height = fontSize * 0.95f;
            }
        }

        private void Place()
        {
            if (_anchor == null || style.display == DisplayStyle.None) return;
            var cardWidth = _card.layout.width;
            var cardHeight = _card.layout.height;
            var width = layout.width;
            var height = layout.height;
            if (float.IsNaN(width) || float.IsNaN(height) || float.IsNaN(cardWidth)) return;

            var anchor = _card.WorldToLocal(_anchor.worldBound);
            var left = Mathf.Clamp(anchor.xMin, Edge, Mathf.Max(Edge, cardWidth - width - Edge));
            var top = anchor.yMax + Gap;
            if (top + height > cardHeight - Edge) top = anchor.yMin - Gap - height;
            top = Mathf.Clamp(top, Edge, Mathf.Max(Edge, cardHeight - height - Edge));

            style.left = left;
            style.top = top;
        }

        private void EnsureTextures()
        {
            if (_svTexture != null) return;

            _svTexture = NewTexture(SvSize, SvSize);
            _alphaTexture = NewTexture(BarWidth, AlphaHeight);
            _hueTexture = NewTexture(BarWidth, 1);
            var hue = new Color32[BarWidth];
            for (var x = 0; x < BarWidth; x++) hue[x] = Color.HSVToRGB(x / (BarWidth - 1f), 1f, 1f);
            _hueTexture.SetPixels32(hue);
            _hueTexture.Apply(false);

            _sv.style.backgroundImage = new StyleBackground(_svTexture);
            _hue.style.backgroundImage = new StyleBackground(_hueTexture);
            _alpha.style.backgroundImage = new StyleBackground(_alphaTexture);
            _svHue = -1f;
        }

        private void FillSv()
        {
            if (_svTexture == null) return;
            _svHue = _h;
            for (var y = 0; y < SvSize; y++)
            {
                var v = y / (SvSize - 1f);
                for (var x = 0; x < SvSize; x++)
                    _svPixels[y * SvSize + x] = Color.HSVToRGB(_h, x / (SvSize - 1f), v);
            }
            _svTexture.SetPixels32(_svPixels);
            _svTexture.Apply(false);
        }

        private void FillAlpha()
        {
            if (_alphaTexture == null) return;
            Color32 rgb = Color.HSVToRGB(_h, _s, _v);
            for (var y = 0; y < AlphaHeight; y++)
            for (var x = 0; x < BarWidth; x++)
            {
                var checker = ((x / CheckerCell) + (y / CheckerCell)) % 2 == 0 ? CheckerLight : CheckerDark;
                _alphaPixels[y * BarWidth + x] = Color32.Lerp(checker, rgb, x / (BarWidth - 1f));
            }
            _alphaTexture.SetPixels32(_alphaPixels);
            _alphaTexture.Apply(false);
        }

        private void DestroyTextures()
        {
            DestroyTexture(ref _svTexture);
            DestroyTexture(ref _hueTexture);
            DestroyTexture(ref _alphaTexture);
        }

        private static void DestroyTexture(ref Texture2D? texture)
        {
            if (texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        private static Texture2D NewTexture(int width, int height) => new(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        private static VisualElement Knob(VisualElement track)
        {
            var knob = new VisualElement { pickingMode = PickingMode.Ignore };
            knob.AddToClassList("ocp-picker__knob");
            track.Add(knob);
            return knob;
        }

        private static VisualElement CompareChip(VisualElement parent, string text)
        {
            var chip = new VisualElement();
            chip.AddToClassList("ocp-picker__chip");
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            chip.Add(label);
            parent.Add(chip);
            return chip;
        }

        private static void Position(VisualElement knob, float x, float y)
        {
            knob.style.left = Length.Percent(x * 100f);
            knob.style.top = Length.Percent(y * 100f);
        }

        private static void Drag(VisualElement track, Action<Vector2> onMove)
        {
            void Move(Vector3 local)
            {
                var size = track.layout.size;
                if (size.x <= 0f || size.y <= 0f) return;
                onMove(new Vector2(Mathf.Clamp01(local.x / size.x), Mathf.Clamp01(local.y / size.y)));
            }

            track.RegisterCallback<PointerDownEvent>(e =>
            {
                track.CapturePointer(e.pointerId);
                Move(e.localPosition);
                e.StopPropagation();
            });
            track.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (track.HasPointerCapture(e.pointerId)) Move(e.localPosition);
            });
            track.RegisterCallback<PointerUpEvent>(e =>
            {
                if (track.HasPointerCapture(e.pointerId)) track.ReleasePointer(e.pointerId);
            });
        }
    }
}
