using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace PixelCrushers.DialogueSystem
{

    /// <summary>
    /// Utility class to choose a dialogue entry ID using a dropdown menu.
    /// Optionally call DoLayoutFilter() or DrawFilter() to show a filter field
    /// that narrows the dropdown by entry Title, Dialogue Text, Menu Text, or ID.
    /// </summary>
    public class DialogueEntryPicker
    {
        public bool isValid { get { return conversation != null; } }

        /// <summary>
        /// Title of the conversation this picker lists entries from.
        /// </summary>
        public string conversationTitle { get; private set; }

        /// <summary>
        /// Filter text. If non-empty, only entries matching it (plus the current selection) are listed.
        /// </summary>
        public string filter
        {
            get { return m_filter; }
            set
            {
                var newValue = value ?? string.Empty;
                if (newValue == m_filter) return;
                m_filter = newValue;
                isDirty = true;
            }
        }

        private static readonly GUIContent FilterLabel = new GUIContent("Filter", "Filter entries by Title, Dialogue Text, Menu Text, or ID.");

        private Conversation conversation = null;
        private string m_filter = string.Empty;
        private bool isDirty = true;
        private int builtForID = int.MinValue;
        private string[] entryTexts = new string[0];
        private Dictionary<int, int> idToIndex = new Dictionary<int, int>();
        private Dictionary<int, int> indexToId = new Dictionary<int, int>();

        public DialogueEntryPicker(string conversationTitle)
        {
            this.conversationTitle = conversationTitle;
            var db = EditorTools.FindInitialDatabase();
            if (db == null) return;
            conversation = db.GetConversation(conversationTitle);
        }

        /// <summary>
        /// Returns true if the entry's Title, Dialogue Text, or Menu Text contains the filter
        /// (case-insensitive), or if the filter is exactly the entry's ID.
        /// </summary>
        public static bool MatchesFilter(DialogueEntry entry, string filter)
        {
            if (entry == null) return false;
            if (string.IsNullOrEmpty(filter)) return true;
            return string.Equals(entry.id.ToString(), filter) ||
                Contains(entry.Title, filter) ||
                Contains(entry.DialogueText, filter) ||
                Contains(entry.MenuText, filter);
        }

        private static bool Contains(string s, string filter)
        {
            return !string.IsNullOrEmpty(s) && s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RebuildIfNeeded(int currentID)
        {
            if (isDirty || (!string.IsNullOrEmpty(m_filter) && currentID != builtForID))
            {
                Rebuild(currentID);
            }
        }

        private void Rebuild(int currentID)
        {
            isDirty = false;
            builtForID = currentID;
            var list = new List<string>();
            idToIndex = new Dictionary<int, int>();
            indexToId = new Dictionary<int, int>();
            if (conversation != null)
            {
                var hasFilter = !string.IsNullOrEmpty(m_filter);
                for (int i = 0; i < conversation.dialogueEntries.Count; i++)
                {
                    var entry = conversation.dialogueEntries[i];
                    if (entry == null || idToIndex.ContainsKey(entry.id)) continue;
                    if (hasFilter && entry.id != currentID && !MatchesFilter(entry, m_filter)) continue;
                    var index = list.Count;
                    list.Add("[" + entry.id + "] " + ((entry.id == 0) ? "<START>" : entry.subtitleText).Replace("/", "∕"));
                    idToIndex.Add(entry.id, index);
                    indexToId.Add(index, entry.id);
                }
            }
            entryTexts = list.ToArray();
        }

        private int GetID(int index)
        {
            return indexToId.ContainsKey(index) ? indexToId[index] : -1;
        }

        private int GetIndex(int id)
        {
            return idToIndex.ContainsKey(id) ? idToIndex[id] : -1;
        }

        public int Draw(Rect rect, string label, int id)
        {
            RebuildIfNeeded(id);
            return GetID(EditorGUI.Popup(rect, label, GetIndex(id), entryTexts));
        }

        public int DoLayout(string label, int id)
        {
            RebuildIfNeeded(id);
            return GetID(EditorGUILayout.Popup(label, GetIndex(id), entryTexts));
        }

        /// <summary>
        /// Draws the filter text field in the given rect.
        /// </summary>
        public void DrawFilter(Rect rect)
        {
            filter = EditorGUI.TextField(rect, FilterLabel, filter);
        }

        /// <summary>
        /// Draws the filter text field using GUILayout.
        /// </summary>
        public void DoLayoutFilter()
        {
            filter = EditorGUILayout.TextField(FilterLabel, filter);
        }
    }
}
