// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Builds one-line rich text previews of database strings for result lists, such as
    /// global search and spell check results, with part of the string highlighted.
    /// </summary>
    public static class RichTextPreview
    {

        public const int ContextLength = 40;
        public const int MaxLength = 200;

        /// <summary>
        /// One line of value around the highlighted part, with the highlighted part in the
        /// theme's highlight color. If index is negative, shows the start of the value.
        /// </summary>
        public static string Build(string value, int index, int length)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (index < 0 || index + length > value.Length) return Escape(Flatten(value, 0, Mathf.Min(value.Length, MaxLength)));
            var start = Mathf.Max(0, index - ContextLength);
            var end = Mathf.Min(value.Length, index + length + MaxLength);
            var color = DialogueDatabaseEditorTheme.SearchHighlightHex;
            return (start > 0 ? "…" : string.Empty) +
                Escape(Flatten(value, start, index - start)) +
                "<b><color=" + color + ">" + Escape(Flatten(value, index, length)) + "</color></b>" +
                Escape(Flatten(value, index + length, end - (index + length)));
        }

        /// <summary>Replaces line breaks and tabs with spaces.</summary>
        public static string Flatten(string value, int start, int length)
        {
            return value.Substring(start, length).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        }

        /// <summary>Shows s literally in a rich text label.</summary>
        public static string Escape(string s)
        {
            return string.IsNullOrEmpty(s) ? s : "<noparse>" + s.Replace("</noparse>", "</no​parse>") + "</noparse>";
        }

    }

}

#endif
