#nullable enable

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>Bar graph of the kept frame times, newest on the right: main-thread time, rest of the frame, frames over budget, frames over the Spike threshold, and dashed budget and threshold lines. Clicking a bar reports its frame.</summary>
    internal sealed class OCFrameGraph : VisualElement
    {
        private const float MinTopMs = 33.3f;
        private const float TopPadding = 8f;
        private const float DashLength = 6f;
        private const float DashGap = 5f;
        private const float LineWidth = 1f;
        private const float SpikeHeadroom = 1.5f;

        private static readonly Color CpuColor = new(63f / 255f, 123f / 255f, 224f / 255f);
        private static readonly Color RestColor = new(159f / 255f, 194f / 255f, 1f, 0.45f);
        private static readonly Color OverColor = new(224f / 255f, 179f / 255f, 64f / 255f);
        private static readonly Color GridColor = new(1f, 1f, 1f, 0.06f);
        private static readonly Color BudgetColor = new(1f, 1f, 1f, 0.75f);
        private static readonly Color SpikeColor = new(227f / 255f, 87f / 255f, 77f / 255f);
        private static readonly Color HighlightColor = new(238f / 255f, 240f / 255f, 244f / 255f);

        private readonly OCFrameStats _stats;
        private readonly Label _topLabel = new();
        private readonly Label _budgetLabel = new();
        private float _budgetMs = 16.67f;
        private float _shownTop = -1f;
        private float _shownBudget = -1f;
        private float _spikeMs = 33.3f;
        private int _highlightFrame = -1;

        public OCFrameGraph(OCFrameStats stats)
        {
            _stats = stats;
            AddToClassList("ocp-graph");
            _topLabel.AddToClassList("ocp-graph__label");
            _budgetLabel.AddToClassList("ocp-graph__label");
            Add(_topLabel);
            Add(_budgetLabel);
            generateVisualContent += Draw;
            RegisterCallback<GeometryChangedEvent>(_ => PlaceLabels());
            RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        /// <summary>Time.frameCount of the bar the operator clicked.</summary>
        public event Action<int>? FrameClicked;

        public void SetSpikeThreshold(float spikeMs)
        {
            if (Mathf.Approximately(spikeMs, _spikeMs)) return;
            _spikeMs = spikeMs;
            PlaceLabels();
            MarkDirtyRepaint();
        }

        /// <summary>Draws the bar of <paramref name="frame"/> (a Time.frameCount) in white; -1 for none.</summary>
        public void SetHighlight(int frame)
        {
            if (frame == _highlightFrame) return;
            _highlightFrame = frame;
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            var count = _stats.Count;
            var width = contentRect.width;
            if (count == 0 || width <= 0f || _stats.LatestFrame < 0) return;
            var barWidth = width / OCFrameStats.Capacity;
            var index = count - Mathf.CeilToInt((width - evt.localPosition.x) / barWidth);
            if (index < 0 || index >= count) return;
            FrameClicked?.Invoke(_stats.LatestFrame - (count - 1 - index));
        }

        public void SetBudget(float budgetMs, float labelFontSize)
        {
            _budgetMs = budgetMs;
            _topLabel.style.fontSize = labelFontSize;
            _budgetLabel.style.fontSize = labelFontSize;
            PlaceLabels();
            MarkDirtyRepaint();
        }

        private float TopMs => Mathf.Max(Mathf.Max(_budgetMs * 2f, MinTopMs), _spikeMs * SpikeHeadroom);

        private float Y(float ms, float height)
        {
            var top = TopMs;
            return height - Mathf.Min(ms, top) / top * (height - TopPadding);
        }

        private void PlaceLabels()
        {
            var height = contentRect.height;
            if (float.IsNaN(height) || height <= 0f) return;
            var top = TopMs;
            if (!Mathf.Approximately(top, _shownTop))
            {
                _shownTop = top;
                _topLabel.text = top.ToString("0") + " ms";
            }
            if (!Mathf.Approximately(_budgetMs, _shownBudget))
            {
                _shownBudget = _budgetMs;
                _budgetLabel.text = _budgetMs.ToString("0.0") + " ms";
            }
            _topLabel.style.top = 2f;
            _budgetLabel.style.top = Y(_budgetMs, height) - 2f;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var width = rect.width;
            var height = rect.height;
            if (width <= 0f || height <= 0f) return;

            var count = _stats.Count;
            var dashes = Mathf.CeilToInt(width / (DashLength + DashGap));
            var quads = count * 2 + dashes * 2 + 2;
            var mesh = context.Allocate(quads * 4, quads * 6);
            var vertex = 0;

            var barWidth = width / OCFrameStats.Capacity;
            var firstFrame = _stats.LatestFrame - (count - 1);
            for (var i = 0; i < count; i++)
            {
                var frame = _stats.FrameAt(i);
                var cpu = _stats.CpuAt(i);
                var x = width - (count - i) * barWidth;
                var right = x + Mathf.Max(1f, barWidth - 0.5f);
                var frameY = Y(frame, height);

                var highlighted = _highlightFrame >= 0 && _stats.LatestFrame >= 0 && firstFrame + i == _highlightFrame;
                if (highlighted || frame > _budgetMs || float.IsNaN(cpu))
                {
                    var color = highlighted ? HighlightColor : frame > _spikeMs ? SpikeColor : frame > _budgetMs ? OverColor : CpuColor;
                    Quad(mesh, ref vertex, x, frameY, right, height, color);
                    Quad(mesh, ref vertex, x, height, right, height, CpuColor);
                    continue;
                }
                var cpuY = Y(Mathf.Min(cpu, frame), height);
                Quad(mesh, ref vertex, x, frameY, right, cpuY, RestColor);
                Quad(mesh, ref vertex, x, cpuY, right, height, CpuColor);
            }

            Quad(mesh, ref vertex, 0f, Y(_budgetMs * 0.5f, height), width, Y(_budgetMs * 0.5f, height) + LineWidth, GridColor);
            Quad(mesh, ref vertex, 0f, Y(_budgetMs * 1.5f, height), width, Y(_budgetMs * 1.5f, height) + LineWidth, GridColor);

            var budgetY = Y(_budgetMs, height);
            var spikeY = Y(_spikeMs, height);
            for (var d = 0; d < dashes; d++)
            {
                var x = d * (DashLength + DashGap);
                Quad(mesh, ref vertex, x, budgetY, Mathf.Min(x + DashLength, width), budgetY + LineWidth, BudgetColor);
                Quad(mesh, ref vertex, x, spikeY, Mathf.Min(x + DashLength, width), spikeY + LineWidth, SpikeColor);
            }
        }

        private static void Quad(MeshWriteData mesh, ref int vertex, float left, float top, float right, float bottom, Color color)
        {
            mesh.SetNextVertex(new Vertex { position = new Vector3(left, top, Vertex.nearZ), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(right, top, Vertex.nearZ), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(right, bottom, Vertex.nearZ), tint = color });
            mesh.SetNextVertex(new Vertex { position = new Vector3(left, bottom, Vertex.nearZ), tint = color });
            var b = (ushort)vertex;
            mesh.SetNextIndex(b);
            mesh.SetNextIndex((ushort)(b + 1));
            mesh.SetNextIndex((ushort)(b + 2));
            mesh.SetNextIndex(b);
            mesh.SetNextIndex((ushort)(b + 2));
            mesh.SetNextIndex((ushort)(b + 3));
            vertex += 4;
        }
    }
}
