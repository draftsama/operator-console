#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace OperatorConsole
{
    /// <summary>The single asset holding every Entry Definition plus save behaviour.</summary>
    [CreateAssetMenu(menuName = "Operator Console/Config")]
    public sealed class OCConfig : ScriptableObject
    {
        [SerializeField] private bool _autoSave = true;
        [SerializeField, Min(0f)] private float _autoSaveDebounce = 1f;
        [SerializeField] private string _saveDirectory = string.Empty;
        [SerializeField] private string _fileName = "save.json";
        [SerializeField] private List<OCEntryDefinition> _entries = new();
        [SerializeField, FormerlySerializedAs("_audiences")] private List<OCTag> _tags = new();
        [SerializeField] private List<OCPanelDefinition> _panels = new();
        [SerializeField, Min(1), Tooltip("Log Entries kept for the Runtime Panel's Log tab; the oldest is dropped when full.")]
        private int _logCapacity = OCLog.DefaultCapacity;

        public IReadOnlyList<OCTag> Tags => _tags;

        /// <summary>The Panel Definitions the app creates its Runtime Panels from.</summary>
        public IReadOnlyList<OCPanelDefinition> Panels => _panels;

        public bool TryGetTag(string id, out OCTag tag)
        {
            foreach (var a in _tags)
            {
                if (a.Id != id) continue;
                tag = a;
                return true;
            }
            tag = null!;
            return false;
        }

        public bool TryGetTagByName(string name, out OCTag tag)
        {
            foreach (var a in _tags)
            {
                if (!string.Equals(a.Name, name, System.StringComparison.Ordinal)) continue;
                tag = a;
                return true;
            }
            tag = null!;
            return false;
        }

        public bool AutoSave => _autoSave;
        public float AutoSaveDebounce => _autoSaveDebounce;
        public string SaveDirectory => _saveDirectory;
        public string FileName => _fileName;
        public int LogCapacity => Mathf.Max(1, _logCapacity);
        public IReadOnlyList<OCEntryDefinition> Entries => _entries;

        public bool TryGetEntry(string key, out OCEntryDefinition entry)
        {
            foreach (var e in _entries)
            {
                if (e.Key != key) continue;
                entry = e;
                return true;
            }
            entry = null!;
            return false;
        }

        public void SetEntry(OCEntryDefinition entry)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Key != entry.Key) continue;
                _entries[i] = entry;
                return;
            }
            _entries.Add(entry);
        }

        public bool RemoveEntry(string key) => _entries.RemoveAll(e => e.Key == key) > 0;

        internal void AddEntryForTests(OCEntryDefinition entry) => _entries.Add(entry);

        internal void AddTagForTests(OCTag tag) => _tags.Add(tag);

        internal void AddPanelForTests(OCPanelDefinition definition) => _panels.Add(definition);

        internal void SetForTests(bool autoSave, float debounce, string saveDirectory, string fileName)
        {
            _autoSave = autoSave;
            _autoSaveDebounce = debounce;
            _saveDirectory = saveDirectory;
            _fileName = fileName;
        }
    }
}
