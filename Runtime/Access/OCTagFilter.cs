#nullable enable

using System;
using System.Collections.Generic;

namespace OperatorConsole
{
    /// <summary>Decides which Exposed Entries and Commands a Runtime Panel shows, from the Tags each names.</summary>
    public static class OCTagFilter
    {
        /// <summary>Resolves Tag names, as declared in <paramref name="config"/>, into <paramref name="ids"/> without duplicates. False, with <paramref name="error"/> set and <paramref name="ids"/> untouched, when <paramref name="names"/> is empty or names a Tag the Config doesn't declare.</summary>
        public static bool TryResolveNames(OCConfig config, IReadOnlyList<string> names, List<string> ids, out string error)
        {
            if (names.Count == 0)
            {
                error = "no Tags given; call ShowAllTags to show every Exposed Entry.";
                return false;
            }

            var resolved = new List<string>(names.Count);
            foreach (var name in names)
            {
                if (!config.TryGetTagByName(name, out var tag))
                {
                    error = $"Tag '{name}' is not declared in the Config.";
                    return false;
                }
                if (!resolved.Contains(tag.Id)) resolved.Add(tag.Id);
            }

            ids.Clear();
            ids.AddRange(resolved);
            error = string.Empty;
            return true;
        }

        public static bool SameSet(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++)
            {
                var found = false;
                for (var j = 0; j < b.Count && !found; j++) found = string.Equals(a[i], b[j], StringComparison.Ordinal);
                if (!found) return false;
            }
            return true;
        }

        /// <summary>True when <paramref name="entry"/> is Exposed and either side names no Tag, or both share at least one.</summary>
        public static bool Shows(OCEntryDefinition entry, IReadOnlyList<string> panelTagIds)
        {
            if (!entry.Exposed) return false;

            var entryTagIds = entry.TagIds;
            if (panelTagIds.Count == 0 || entryTagIds.Count == 0) return true;

            foreach (var id in entryTagIds)
            {
                for (var i = 0; i < panelTagIds.Count; i++)
                    if (string.Equals(panelTagIds[i], id, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>True when either side names no Tag, or both share at least one. The Command names Tags by name, resolved through <paramref name="config"/>; an unknown name matches nothing.</summary>
        public static bool Shows(OCRegisteredCommand command, IReadOnlyList<string> panelTagIds, OCConfig config)
        {
            var names = command.TagNames;
            if (panelTagIds.Count == 0 || names.Count == 0) return true;

            foreach (var name in names)
            {
                if (!config.TryGetTagByName(name, out var tag)) continue;
                for (var i = 0; i < panelTagIds.Count; i++)
                    if (string.Equals(panelTagIds[i], tag.Id, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
