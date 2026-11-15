#nullable enable

using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace OperatorConsole.Editor
{
    /// <summary>The Manager's Settings tab: the Save File and the Config's Tags.</summary>
    internal sealed class OCSettingsTab : VisualElement
    {
        private readonly OCManagerContext _context;
        private readonly TextField _path;
        private readonly VisualElement _tags = new();
        private readonly Button _addTag;
        private int _shownTags = -1;

        public OCSettingsTab(OCManagerContext context)
        {
            _context = context;
            AddToClassList("oc-tab");
            var scroll = new ScrollView();
            scroll.AddToClassList("oc-scroll");
            Add(scroll);

            var so = context.So;
            var saveCard = Section(scroll, "Save File", null);
            saveCard.Add(Bound(so, "_autoSave", "AutoSave"));
            saveCard.Add(Bound(so, "_autoSaveDebounce", "AutoSave Debounce"));
            saveCard.Add(Bound(so, "_saveDirectory", "Save Directory"));
            saveCard.Add(Bound(so, "_fileName", "File Name"));
            saveCard.Add(Bound(so, "_logCapacity", "Log Capacity"));

            _path = new TextField("Resolved Path") { isReadOnly = true };
            _path.AddToClassList("oc-mono-field");
            saveCard.Add(_path);

            var buttons = new VisualElement();
            buttons.AddToClassList("oc-row");
            buttons.AddToClassList("oc-actions");
            buttons.Add(new Button(OpenFolder) { text = "Open Folder" });
            var delete = new Button(DeleteSaveFile) { text = "Delete Save File" };
            delete.AddToClassList("oc-button--danger");
            buttons.Add(delete);
            saveCard.Add(buttons);

            var tagCard = Section(scroll, "Tags", "Names that decide on which Runtime Panels an Exposed Entry or Command is shown. Each Runtime Panel starts with its Default Tags; code can change its Active Tags while the app runs.");
            tagCard.Add(_tags);
            _addTag = new Button(AddTag) { text = "+ Add Tag" };
            _addTag.AddToClassList("oc-align-start");
            tagCard.Add(_addTag);

            Refresh();
        }

        public void Refresh()
        {
            _path.SetValueWithoutNotify(OCPaths.ResolveSaveFilePath(_context.Config));
            var tagsProp = _context.So.FindProperty("_tags");
            if (tagsProp.arraySize != _shownTags) RebuildTags(tagsProp);
        }

        private void RebuildTags(SerializedProperty tagsProp)
        {
            _tags.Clear();
            _shownTags = tagsProp.arraySize;
            _addTag.SetEnabled(tagsProp.arraySize < OCTagEditing.MaxTags);
            if (tagsProp.arraySize == 0)
            {
                var none = new Label("No Tags: every Runtime Panel shows every Exposed Entry.");
                none.AddToClassList("oc-muted");
                _tags.Add(none);
            }

            for (var i = 0; i < tagsProp.arraySize; i++)
            {
                var index = i;
                var tagProp = tagsProp.GetArrayElementAtIndex(i);
                var row = new VisualElement();
                row.AddToClassList("oc-row");
                row.AddToClassList("oc-detail-row");
                var name = new TextField { isDelayed = true };
                name.BindProperty(tagProp.FindPropertyRelative("_name"));
                name.AddToClassList("oc-grow");
                var remove = new Button(() => TryRemoveTag(index)) { text = "✕", tooltip = "Remove Tag" };
                remove.AddToClassList("oc-icon-button");
                row.Add(name);
                row.Add(remove);
                _tags.Add(row);
            }
        }

        private void AddTag()
        {
            var tagsProp = _context.So.FindProperty("_tags");
            var index = tagsProp.arraySize;
            tagsProp.InsertArrayElementAtIndex(index);
            var added = tagsProp.GetArrayElementAtIndex(index);
            added.FindPropertyRelative("_id").stringValue = OCTag.NewId();
            added.FindPropertyRelative("_name").stringValue = $"Tag {index + 1}";
            _context.So.ApplyModifiedProperties();
            Refresh();
        }

        private void TryRemoveTag(int index)
        {
            var tagsProp = _context.So.FindProperty("_tags");
            var tagProp = tagsProp.GetArrayElementAtIndex(index);
            var id = tagProp.FindPropertyRelative("_id").stringValue;
            var name = tagProp.FindPropertyRelative("_name").stringValue;

            var users = OCTagEditing.FindUsers(_context.Config, id);
            if (users.Count > 0)
            {
                EditorUtility.DisplayDialog(
                    "Tag in use",
                    $"'{name}' can't be removed while it is still named by:\n\n- {string.Join("\n- ", users)}\n\nRemove it from these first.",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Tag", $"Remove Tag '{name}'?", "Remove", "Cancel")) return;
            tagsProp.DeleteArrayElementAtIndex(index);
            _context.So.ApplyModifiedProperties();
            Refresh();
        }

        private void OpenFolder()
        {
            var dir = Path.GetDirectoryName(OCPaths.ResolveSaveFilePath(_context.Config));
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }

        private void DeleteSaveFile()
        {
            var path = OCPaths.ResolveSaveFilePath(_context.Config);
            if (!EditorUtility.DisplayDialog("Delete Save File", $"Delete '{path}'? This cannot be undone.", "Delete", "Cancel")) return;
            if (File.Exists(path)) File.Delete(path);
            OC.Load();
        }

        private static PropertyField Bound(SerializedObject so, string path, string label)
        {
            var field = new PropertyField(so.FindProperty(path), label);
            field.BindProperty(so.FindProperty(path));
            return field;
        }

        internal static VisualElement Section(VisualElement parent, string title, string? description)
        {
            var heading = new Label(title);
            heading.AddToClassList("oc-section-title");
            parent.Add(heading);
            if (description != null)
            {
                var text = new Label(description);
                text.AddToClassList("oc-muted");
                text.AddToClassList("oc-wrap");
                text.AddToClassList("oc-section-description");
                parent.Add(text);
            }
            var card = new VisualElement();
            card.AddToClassList("oc-card");
            card.AddToClassList("oc-form");
            parent.Add(card);
            return card;
        }
    }
}
