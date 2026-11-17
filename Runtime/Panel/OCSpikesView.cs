#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace OperatorConsole
{
    /// <summary>The Performance tab's Spikes section: frames over the Spike threshold, newest first, with the picked Spike's breakdown beside the list.</summary>
    internal sealed class OCSpikesView : VisualElement
    {
        private const float SmallScale = 0.75f;
        private const float RowScale = 0.85f;
        private const float BigScale = 1.6f;
        private const float MinBarMs = 50f;
        private const double LogWindowSeconds = 1.0;
        private const int NearbyFrames = 4;
        private const int VisibleRows = 9;
        private const float SevereBudgets = 4f;
        private const float RowPadding = 10f;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly OCSpikeCause[] Causes = (OCSpikeCause[])Enum.GetValues(typeof(OCSpikeCause));

        private readonly OCPerformance _performance;
        private readonly OCLog _log;
        private readonly Label _title = new("SPIKES");
        private readonly VisualElement _recDot = new();
        private readonly Label _recText = new();
        private readonly Button _record;
        private readonly Button _clear;
        private readonly Button _save;
        private readonly Label _status = new();
        private readonly VisualElement _filters = new();
        private readonly VisualElement _listHead = new();
        private readonly ScrollView _list = new(ScrollViewMode.Vertical)
        {
            horizontalScrollerVisibility = ScrollerVisibility.Hidden,
            verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
        };
        private readonly VisualElement _detail = new();
        private readonly HashSet<OCSpikeCause> _hidden = new();
        private readonly Dictionary<OCSpike, VisualElement> _rows = new();
        private readonly List<OCLogEntry> _logEntries = new();
        private readonly int[] _causeCounts = new int[Causes.Length];

        private OCPanelLayout? _layout;
        private OCSpike? _selected;
        private int _shownVersion = -1;
        private bool _shownRecording;
        private float _shownThreshold = -1f;

        public OCSpikesView(OCPerformance performance, OCLog log)
        {
            _performance = performance;
            _log = log;
            AddToClassList("ocp-spikes");

            var head = new VisualElement();
            head.AddToClassList("ocp-spikes__head");
            _title.AddToClassList("ocp-header");
            _title.AddToClassList("ocp-perf__section-title");
            head.Add(_title);
            _recDot.AddToClassList("ocp-spikes__dot");
            head.Add(_recDot);
            _recText.AddToClassList("ocp-spikes__rec");
            head.Add(_recText);
            var grow = new VisualElement();
            grow.AddToClassList("ocp-grow");
            head.Add(grow);
            _record = SmallButton("Stop", ToggleRecording);
            _clear = SmallButton("Clear", () => _performance.Spikes.Clear());
            _save = SmallButton("Save to file", Save);
            head.Add(_record);
            head.Add(_clear);
            head.Add(_save);
            Add(head);

            _status.AddToClassList("ocp-spikes__status");
            _status.style.display = DisplayStyle.None;
            Add(_status);

            _filters.AddToClassList("ocp-spikes__filters");
            Add(_filters);

            var body = new VisualElement();
            body.AddToClassList("ocp-spikes__body");
            var list = new VisualElement();
            list.AddToClassList("ocp-spikes__list");
            _listHead.AddToClassList("ocp-spike-row");
            _listHead.AddToClassList("ocp-spike-row--head");
            list.Add(_listHead);
            _list.AddToClassList("ocp-spikes__scroll");
            list.Add(_list);
            body.Add(list);
            _detail.AddToClassList("ocp-spikes__detail");
            body.Add(_detail);
            Add(body);
        }

        /// <summary>Time.frameCount of the picked Spike, or -1 when none is picked.</summary>
        public event Action<int>? SelectedFrameChanged;

        public void ApplyLayout(OCPanelLayout layout)
        {
            _layout = layout;
            _performance.Spikes.ThresholdMs = layout.FrameBudgetMs * 2f;
            _title.style.fontSize = layout.FontSize * 0.7f;
            _recText.style.fontSize = layout.FontSize * SmallScale;
            _status.style.fontSize = layout.FontSize * SmallScale;
            foreach (var button in new[] { _record, _clear, _save })
            {
                button.style.fontSize = layout.FontSize * RowScale;
                button.style.height = layout.RowHeight;
            }
            _list.style.height = layout.RowHeight * VisibleRows;
            _list.verticalScroller.style.width = layout.ScrollbarWidth;
            _listHead.style.paddingRight = RowPadding + layout.ScrollbarWidth;
            _list.mouseWheelScrollSize = layout.ScrollSensitivity;
            RebuildHead();
            _shownVersion = -1;
            Refresh();
        }

        /// <summary>Redraws when Spikes were added or cleared; cheap when nothing changed.</summary>
        public void Refresh()
        {
            if (_layout == null) return;
            var spikes = _performance.Spikes;
            if (spikes.IsRecording != _shownRecording || !Mathf.Approximately(spikes.ThresholdMs, _shownThreshold)) UpdateRecording();
            if (spikes.Version == _shownVersion) return;
            _shownVersion = spikes.Version;
            if (_selected != null && !Contains(spikes.Spikes, _selected)) _selected = null;
            if (_selected == null && spikes.Spikes.Count > 0) _selected = spikes.Spikes[0];
            RebuildFilters();
            RebuildList();
            RebuildDetail();
            SelectedFrameChanged?.Invoke(_selected?.Frame ?? -1);
        }

        /// <summary>Picks the Spike closest to <paramref name="frame"/>, if one is within a few frames.</summary>
        public void SelectNear(int frame)
        {
            OCSpike? best = null;
            var bestDistance = NearbyFrames + 1;
            foreach (var spike in _performance.Spikes.Spikes)
            {
                var distance = Math.Abs(spike.Frame - frame);
                if (distance >= bestDistance) continue;
                best = spike;
                bestDistance = distance;
            }
            if (best != null) Select(best);
        }

        private void Select(OCSpike spike)
        {
            if (_selected != null && _rows.TryGetValue(_selected, out var old)) old.RemoveFromClassList("ocp-spike-row--selected");
            _selected = spike;
            if (_rows.TryGetValue(spike, out var row)) row.AddToClassList("ocp-spike-row--selected");
            RebuildDetail();
            SelectedFrameChanged?.Invoke(spike.Frame);
        }

        private void ToggleRecording()
        {
            _performance.Spikes.IsRecording = !_performance.Spikes.IsRecording;
            UpdateRecording();
        }

        private void UpdateRecording()
        {
            var spikes = _performance.Spikes;
            _shownRecording = spikes.IsRecording;
            _shownThreshold = spikes.ThresholdMs;
            _record.text = spikes.IsRecording ? "Stop" : "Record";
            _recText.text = (spikes.IsRecording ? "Recording" : "Stopped") + " · over " + spikes.ThresholdMs.ToString("0.0", Invariant) + " ms";
            _recDot.EnableInClassList("ocp-spikes__dot--off", !spikes.IsRecording);
        }

        private void RebuildHead()
        {
            _listHead.Clear();
            _listHead.Add(Cell("TIME", "ocp-spike-row__time", SmallScale));
            _listHead.Add(Cell("MS", "ocp-spike-row__ms", SmallScale));
            _listHead.Add(Cell("CAUSE", "ocp-spike-row__cause", SmallScale));
            _listHead.Add(Cell("SCENE", "ocp-spike-row__scene", SmallScale));
            _listHead.Add(Cell("FRAME", "ocp-spike-row__frame", SmallScale));
        }

        private void RebuildFilters()
        {
            _filters.Clear();
            Array.Clear(_causeCounts, 0, _causeCounts.Length);
            foreach (var spike in _performance.Spikes.Spikes) _causeCounts[(int)spike.Cause]++;
            foreach (var cause in Causes)
            {
                var count = _causeCounts[(int)cause];
                if (count == 0) continue;
                var chip = new Button(() =>
                {
                    if (!_hidden.Remove(cause)) _hidden.Add(cause);
                    RebuildFilters();
                    RebuildList();
                });
                chip.AddToClassList("ocp-spike-chip");
                chip.EnableInClassList("ocp-spike-chip--off", _hidden.Contains(cause));
                chip.Add(Swatch(cause));
                chip.Add(Cell(OCSpikeStyle.Name(cause), null, SmallScale));
                chip.Add(Cell(count.ToString(Invariant), "ocp-spike-chip__count", SmallScale));
                chip.style.height = _layout!.RowHeight * 0.75f;
                _filters.Add(chip);
            }
        }

        private void RebuildList()
        {
            _list.Clear();
            _rows.Clear();
            var spikes = _performance.Spikes.Spikes;
            var shown = 0;
            var severe = _layout!.FrameBudgetMs * SevereBudgets;
            foreach (var spike in spikes)
            {
                if (_hidden.Contains(spike.Cause)) continue;
                shown++;
                var row = new VisualElement();
                row.AddToClassList("ocp-spike-row");
                row.EnableInClassList("ocp-spike-row--selected", spike == _selected);
                row.style.height = _layout.RowHeight * 0.8f;
                row.Add(Cell(spike.Time.ToString("HH:mm:ss.fff", Invariant), "ocp-spike-row__time", SmallScale));
                var ms = Cell(spike.FrameMs.ToString("0.0", Invariant), "ocp-spike-row__ms", RowScale);
                ms.AddToClassList(spike.FrameMs > severe ? "ocp-spike__ms--severe" : "ocp-spike__ms--over");
                row.Add(ms);
                var cause = new VisualElement();
                cause.AddToClassList("ocp-spike-row__cause");
                cause.Add(Swatch(spike.Cause));
                cause.Add(Cell(OCSpikeStyle.Name(spike.Cause), null, RowScale));
                row.Add(cause);
                row.Add(Cell(spike.Scene, "ocp-spike-row__scene", SmallScale));
                row.Add(Cell("#" + spike.Frame.ToString(Invariant), "ocp-spike-row__frame", SmallScale));
                row.RegisterCallback<ClickEvent>(_ => Select(spike));
                _rows[spike] = row;
                _list.Add(row);
            }
            if (shown > 0) return;
            var empty = Cell(spikes.Count > 0
                ? "No Spikes match the causes picked above."
                : "No Spikes yet. Frames over " + _performance.Spikes.ThresholdMs.ToString("0.0", Invariant) + " ms show up here.", "ocp-spikes__empty", RowScale);
            _list.Add(empty);
        }

        private void RebuildDetail()
        {
            _detail.Clear();
            var spike = _selected;
            if (spike == null)
            {
                _detail.Add(Cell("Pick a Spike to see where its time went.", "ocp-spikes__empty", RowScale));
                return;
            }
            var budget = _layout!.FrameBudgetMs;

            var head = new VisualElement();
            head.AddToClassList("ocp-spike-detail__head");
            var ms = Cell(spike.FrameMs.ToString("0.0", Invariant) + " ms", "ocp-spike-detail__ms", BigScale);
            head.Add(ms);
            var cause = new VisualElement();
            cause.AddToClassList("ocp-spike-row__cause");
            cause.Add(Swatch(spike.Cause));
            cause.Add(Cell(OCSpikeStyle.Name(spike.Cause), null, RowScale));
            head.Add(cause);
            _detail.Add(head);
            _detail.Add(Cell(spike.Time.ToString("HH:mm:ss.fff", Invariant) + " · frame #" + spike.Frame.ToString(Invariant) + " · " + spike.Scene + " · "
                             + (spike.FrameMs / budget).ToString("0.0", Invariant) + "× budget", "ocp-spike-detail__sub", SmallScale));

            if (spike.HasBreakdown) AddBreakdown(spike, budget);
            else AddCpuGpu(spike, budget);

            _detail.Add(Caption("THIS FRAME"));
            var grid = new VisualElement();
            grid.AddToClassList("ocp-spike-detail__grid");
            grid.Add(Value("CPU main thread", Ms(spike.CpuMs), false));
            grid.Add(Value("GPU", Ms(spike.GpuMs), false));
            grid.Add(Value("GC alloc", Bytes(spike.GcAllocatedBytes), spike.GcAllocatedBytes > 256 * 1024));
            grid.Add(Value("GC collection", spike.GcCollected ? "Yes" : "No", spike.GcCollected));
            grid.Add(Value("Memory change", (spike.MemoryChangeBytes >= 0 ? "+" : "-") + Bytes(Math.Abs(spike.MemoryChangeBytes)), spike.MemoryChangeBytes > 20L * 1024 * 1024));
            grid.Add(Value("Batches / SetPass", Count(spike.Batches) + " / " + Count(spike.SetPassCalls), false));
            _detail.Add(grid);

            AddLog(spike);

            var actions = new VisualElement();
            actions.AddToClassList("ocp-spike-detail__actions");
            var copy = SmallButton("Copy Spike", () =>
            {
                GUIUtility.systemCopyBuffer = Describe(spike);
                ShowStatus("Copied the Spike at " + spike.Time.ToString("HH:mm:ss.fff", Invariant) + ".");
            });
            copy.style.fontSize = _layout.FontSize * RowScale;
            copy.style.height = _layout.RowHeight;
            actions.Add(copy);
            _detail.Add(actions);
        }

        private void AddBreakdown(OCSpike spike, float budget)
        {
            _detail.Add(Caption("WHERE THE TIME WENT"));
            var sum = 0f;
            for (var i = 0; i < OCSpike.CategoryCount; i++) sum += spike.CategoryTimes[i];
            var other = Mathf.Max(0f, spike.FrameMs - sum);
            var scale = Mathf.Max(MinBarMs, Mathf.Max(spike.FrameMs, sum));

            var bar = Bar();
            for (var i = 0; i < OCSpike.CategoryCount; i++) Segment(bar, spike.CategoryTimes[i], scale, OCSpikeStyle.Color((OCSpikeCause)i));
            Segment(bar, other, scale, OCSpikeStyle.Color(OCSpikeCause.Other));
            BudgetLine(bar, budget, scale);
            _detail.Add(bar);

            var order = new List<int>();
            for (var i = 0; i < OCSpike.CategoryCount; i++)
                if (spike.CategoryTimes[i] >= 0.05f) order.Add(i);
            order.Sort((a, b) => spike.CategoryTimes[b].CompareTo(spike.CategoryTimes[a]));
            var legend = new VisualElement();
            legend.AddToClassList("ocp-spike-detail__legend");
            for (var n = 0; n < order.Count; n++) legend.Add(LegendRow((OCSpikeCause)order[n], spike.CategoryTimes[order[n]], n == 0));
            if (other >= 0.05f) legend.Add(LegendRow(OCSpikeCause.Other, other, false));
            _detail.Add(legend);

            var top = order.Count > 0 ? (OCFrameCategory)order[0] : (OCFrameCategory?)null;
            var note = top.HasValue ? "Biggest slice: " + OCFrameMarkers.Names(top.Value) + "." : "No marked phase ran long.";
            note += spike.MarkName != null
                ? "\nYour OC.Mark block " + spike.MarkName + " took " + spike.MarkMs.ToString("0.0", Invariant) + " ms."
                : "\nNo OC.Mark block ran in this frame.";
            _detail.Add(Cell(note, "ocp-spike-detail__note", SmallScale));
        }

        private void AddCpuGpu(OCSpike spike, float budget)
        {
            _detail.Add(Caption("CPU VS GPU"));
            var cpu = float.IsNaN(spike.CpuMs) ? 0f : spike.CpuMs;
            var gpu = float.IsNaN(spike.GpuMs) ? 0f : spike.GpuMs;
            var scale = Mathf.Max(MinBarMs, Mathf.Max(spike.FrameMs, Mathf.Max(cpu, gpu)));
            var bar = Bar();
            Segment(bar, cpu, scale, OCSpikeStyle.Color(OCSpikeCause.Cpu));
            Segment(bar, Mathf.Max(0f, spike.FrameMs - cpu), scale, OCSpikeStyle.Color(OCSpikeCause.Gpu));
            BudgetLine(bar, budget, scale);
            _detail.Add(bar);
            var legend = new VisualElement();
            legend.AddToClassList("ocp-spike-detail__legend");
            legend.Add(LegendRow(OCSpikeCause.Cpu, spike.CpuMs, false, "CPU main thread"));
            legend.Add(LegendRow(OCSpikeCause.Gpu, spike.GpuMs, false));
            _detail.Add(legend);
            var note = "The split into Scripts, Physics, UI, Loading and the rest needs a Development Build, because Unity strips its markers from Release builds.";
            note += spike.MarkName != null
                ? " Your OC.Mark block " + spike.MarkName + " took " + spike.MarkMs.ToString("0.0", Invariant) + " ms."
                : " Blocks wrapped in OC.Mark are still timed.";
            _detail.Add(Cell(note, "ocp-spike-detail__locked", SmallScale));
        }

        private void AddLog(OCSpike spike)
        {
            _detail.Add(Caption("LOG ±1 S"));
            var box = new VisualElement();
            box.AddToClassList("ocp-spike-detail__log");
            if (!_log.IsCapturing)
            {
                box.Add(Cell("Show the Log tab on a Panel Definition to keep log lines here.", "ocp-spike-detail__log-line", SmallScale));
                _detail.Add(box);
                return;
            }
            _log.CopyTo(_logEntries);
            var from = spike.Time.AddSeconds(-LogWindowSeconds);
            var to = spike.Time.AddSeconds(LogWindowSeconds);
            var markerShown = false;
            var lines = 0;
            foreach (var entry in _logEntries)
            {
                if (entry.Time < from) continue;
                if (entry.Time > to) break;
                if (!markerShown && entry.Time > spike.Time)
                {
                    box.Add(SpikeLine(spike));
                    markerShown = true;
                }
                var line = Cell(entry.TimeText + "  " + entry.Preview(1), "ocp-spike-detail__log-line", SmallScale);
                if (entry.Type == OCLogType.Warning) line.AddToClassList("ocp-spike-detail__log-line--warning");
                if (entry.Type == OCLogType.Error) line.AddToClassList("ocp-spike-detail__log-line--error");
                box.Add(line);
                lines++;
            }
            if (!markerShown) box.Add(SpikeLine(spike));
            if (lines == 0) box.Add(Cell("No log lines in this window.", "ocp-spike-detail__log-line", SmallScale));
            _logEntries.Clear();
            _detail.Add(box);
        }

        private Label SpikeLine(OCSpike spike)
        {
            var line = Cell(spike.Time.ToString("HH:mm:ss.fff", Invariant) + "  <- spike frame", "ocp-spike-detail__log-line", SmallScale);
            line.AddToClassList("ocp-spike-detail__log-line--spike");
            return line;
        }

        private void Save()
        {
            var spikes = _performance.Spikes.Spikes;
            if (spikes.Count == 0)
            {
                ShowStatus("No Spikes to save.");
                return;
            }
            var path = Path.Combine(Application.persistentDataPath, "Spikes", "spikes-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", Invariant) + ".csv");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Csv(spikes));
                ShowStatus("Saved " + spikes.Count.ToString(Invariant) + " Spikes to " + path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                ShowStatus("Couldn't save the Spikes: " + e.Message);
            }
        }

        private void ShowStatus(string text)
        {
            _status.text = text;
            _status.style.display = DisplayStyle.Flex;
        }

        internal static string Csv(IReadOnlyList<OCSpike> spikes)
        {
            var csv = new StringBuilder();
            csv.Append("time,frame,frame_ms,cpu_ms,gpu_ms,cause,scene,gc_alloc_bytes,gc_collected,memory_change_bytes,batches,setpass_calls");
            for (var i = 0; i < OCSpike.CategoryCount; i++) csv.Append(',').Append(((OCFrameCategory)i).ToString().ToLowerInvariant()).Append("_ms");
            csv.Append(",mark,mark_ms\n");
            for (var n = spikes.Count - 1; n >= 0; n--)
            {
                var s = spikes[n];
                csv.Append(s.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", Invariant)).Append(',')
                    .Append(s.Frame.ToString(Invariant)).Append(',')
                    .Append(Number(s.FrameMs)).Append(',').Append(Number(s.CpuMs)).Append(',').Append(Number(s.GpuMs)).Append(',')
                    .Append(s.Cause).Append(',').Append(Quote(s.Scene)).Append(',')
                    .Append(s.GcAllocatedBytes.ToString(Invariant)).Append(',').Append(s.GcCollected ? "1" : "0").Append(',')
                    .Append(s.MemoryChangeBytes.ToString(Invariant)).Append(',')
                    .Append(s.Batches.ToString(Invariant)).Append(',').Append(s.SetPassCalls.ToString(Invariant));
                for (var i = 0; i < OCSpike.CategoryCount; i++) csv.Append(',').Append(s.HasBreakdown ? Number(s.CategoryTimes[i]) : string.Empty);
                csv.Append(',').Append(Quote(s.MarkName ?? string.Empty)).Append(',').Append(s.MarkName != null ? Number(s.MarkMs) : string.Empty).Append('\n');
            }
            return csv.ToString();
        }

        private static string Describe(OCSpike s)
        {
            var text = new StringBuilder();
            text.Append("Spike ").Append(s.FrameMs.ToString("0.0", Invariant)).Append(" ms, ").Append(OCSpikeStyle.Name(s.Cause))
                .Append(", ").Append(s.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", Invariant)).Append(", frame #").Append(s.Frame.ToString(Invariant))
                .Append(", scene ").Append(s.Scene).Append('\n');
            text.Append("CPU ").Append(Ms(s.CpuMs)).Append(", GPU ").Append(Ms(s.GpuMs)).Append(", GC alloc ").Append(Bytes(s.GcAllocatedBytes))
                .Append(s.GcCollected ? ", GC ran" : string.Empty).Append('\n');
            if (s.HasBreakdown)
                for (var i = 0; i < OCSpike.CategoryCount; i++)
                    if (s.CategoryTimes[i] >= 0.05f) text.Append("  ").Append(OCSpikeStyle.Name((OCSpikeCause)i)).Append(' ').Append(Ms(s.CategoryTimes[i])).Append('\n');
            if (s.MarkName != null) text.Append("OC.Mark ").Append(s.MarkName).Append(' ').Append(Ms(s.MarkMs)).Append('\n');
            return text.ToString();
        }

        private static bool Contains(IReadOnlyList<OCSpike> spikes, OCSpike spike)
        {
            for (var i = 0; i < spikes.Count; i++)
                if (spikes[i] == spike) return true;
            return false;
        }

        private static string Number(float value) => float.IsNaN(value) ? string.Empty : value.ToString("0.###", Invariant);

        private static string Quote(string value) => value.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";

        private static string Ms(float ms) => float.IsNaN(ms) ? "n/a" : ms.ToString("0.0", Invariant) + " ms";

        private static string Bytes(long bytes)
        {
            if (bytes < 0) return "n/a";
            if (bytes < 1024) return bytes.ToString(Invariant) + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("0.0", Invariant) + " KB";
            return (bytes / (1024f * 1024f)).ToString("0.0", Invariant) + " MB";
        }

        private static string Count(long value) => value < 0 ? "n/a" : value.ToString(Invariant);

        private Label Cell(string text, string? className, float scale)
        {
            var label = new Label(text);
            if (className != null) label.AddToClassList(className);
            if (_layout != null) label.style.fontSize = _layout.FontSize * scale;
            return label;
        }

        private Label Caption(string text)
        {
            var label = Cell(text, "ocp-spike-detail__caption", 0.65f);
            return label;
        }

        private VisualElement Value(string name, string value, bool high)
        {
            var cell = new VisualElement();
            cell.AddToClassList("ocp-spike-detail__value");
            cell.Add(Cell(name, "ocp-metric__name", SmallScale));
            var label = Cell(value, "ocp-spike-detail__value-text", RowScale);
            label.EnableInClassList("ocp-metric__value--over", high);
            cell.Add(label);
            return cell;
        }

        private VisualElement LegendRow(OCSpikeCause cause, float ms, bool top, string? name = null)
        {
            var row = new VisualElement();
            row.AddToClassList("ocp-spike-detail__legend-row");
            row.Add(Swatch(cause));
            row.Add(Cell(name ?? OCSpikeStyle.Name(cause), null, SmallScale));
            var grow = new VisualElement();
            grow.AddToClassList("ocp-grow");
            row.Add(grow);
            var value = Cell(Ms(ms), "ocp-spike-detail__legend-value", SmallScale);
            value.EnableInClassList("ocp-spike-detail__legend-value--top", top);
            row.Add(value);
            return row;
        }

        private static VisualElement Swatch(OCSpikeCause cause)
        {
            var swatch = new VisualElement();
            swatch.AddToClassList("ocp-perf__swatch");
            swatch.style.backgroundColor = OCSpikeStyle.Color(cause);
            return swatch;
        }

        private static VisualElement Bar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("ocp-spike-detail__bar");
            return bar;
        }

        private static void Segment(VisualElement bar, float ms, float scale, Color color)
        {
            if (ms <= 0f) return;
            var segment = new VisualElement();
            segment.AddToClassList("ocp-spike-detail__segment");
            segment.style.width = Length.Percent(Mathf.Min(100f, ms / scale * 100f));
            segment.style.backgroundColor = color;
            bar.Add(segment);
        }

        private static void BudgetLine(VisualElement bar, float budget, float scale)
        {
            var line = new VisualElement();
            line.AddToClassList("ocp-spike-detail__budget");
            line.style.left = Length.Percent(Mathf.Min(100f, budget / scale * 100f));
            bar.Add(line);
        }

        private static Button SmallButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("ocp-button");
            return button;
        }
    }

    /// <summary>Display names and colours of Spike causes, shared by the list, the filters and the breakdown bar.</summary>
    internal static class OCSpikeStyle
    {
        public static string Name(OCSpikeCause cause) => cause switch
        {
            OCSpikeCause.Scripts => "Scripts",
            OCSpikeCause.Physics => "Physics",
            OCSpikeCause.Animation => "Animation",
            OCSpikeCause.Rendering => "Rendering",
            OCSpikeCause.UI => "UI",
            OCSpikeCause.Loading => "Loading",
            OCSpikeCause.GC => "GC",
            OCSpikeCause.WaitGpu => "Wait GPU",
            OCSpikeCause.Gpu => "GPU",
            OCSpikeCause.Cpu => "CPU",
            _ => "Other"
        };

        public static Color Color(OCSpikeCause cause) => cause switch
        {
            OCSpikeCause.Scripts => Rgb(63, 123, 224),
            OCSpikeCause.Physics => Rgb(64, 176, 168),
            OCSpikeCause.Animation => Rgb(150, 112, 224),
            OCSpikeCause.Rendering => Rgb(224, 179, 64),
            OCSpikeCause.UI => Rgb(214, 104, 168),
            OCSpikeCause.Loading => Rgb(232, 134, 58),
            OCSpikeCause.GC => Rgb(227, 87, 77),
            OCSpikeCause.WaitGpu => Rgb(159, 194, 255),
            OCSpikeCause.Gpu => Rgb(159, 194, 255),
            _ => Rgb(110, 118, 132)
        };

        private static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    }
}
