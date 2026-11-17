// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Applies the Dialogue Database Editor Window's stylesheets for the editor's current
    /// Dark or Light theme, and reapplies them if the user switches themes.
    /// Colors that depend on the theme belong in the stylesheets (Editor Default Resources/
    /// Dialogue System/DialogueDatabaseEditorWindow*.uss). This class only holds the few
    /// values that code has to supply itself, such as colors inside rich text.
    /// </summary>
    public static class DialogueDatabaseEditorTheme
    {

        public const string RootClass = "ds-window-root";

        private const string SharedStyleSheetPath = "Dialogue System/DialogueDatabaseEditorWindow.uss";
        private const string DarkStyleSheetPath = "Dialogue System/DialogueDatabaseEditorWindowDark.uss";
        private const string LightStyleSheetPath = "Dialogue System/DialogueDatabaseEditorWindowLight.uss";

        private const long ThemeCheckInterval = 500; // Milliseconds.

        public static bool IsDark { get { return EditorGUIUtility.isProSkin; } }

        /// <summary>Rich text color for highlighted search matches.</summary>
        public static string SearchHighlightHex { get { return IsDark ? "#FFC94D" : "#A04000"; } }

        /// <summary>
        /// Adds the shared stylesheet and the current theme's stylesheet to a window's root,
        /// replacing the other theme's stylesheet if it's there.
        /// </summary>
        public static void Apply(VisualElement root)
        {
            if (root == null) return;
            root.AddToClassList(RootClass);
            FixUnityDefaultStyleSheet(root);
            AddStyleSheet(root, SharedStyleSheetPath);
            RemoveStyleSheet(root, IsDark ? LightStyleSheetPath : DarkStyleSheetPath);
            AddStyleSheet(root, IsDark ? DarkStyleSheetPath : LightStyleSheetPath);
        }

        /// <summary>
        /// Applies the stylesheets now and again whenever the editor theme changes, then calls
        /// onChanged so the caller can refresh colors it sets in code. Unity has no public
        /// event for theme changes, so this checks periodically while the root is in a panel.
        /// </summary>
        public static void TrackThemeChanges(VisualElement root, Action onChanged)
        {
            if (root == null) return;
            Apply(root);
            var wasDark = IsDark;
            root.schedule.Execute(() =>
            {
                if (IsDark == wasDark)
                {
                    // Also catch Unity leaving the other theme's default sheet on the root.
                    if (FindStyleSheet(root, IsDark ? "DefaultCommonLight" : "DefaultCommonDark") != null) FixUnityDefaultStyleSheet(root);
                    return;
                }
                wasDark = IsDark;
                Apply(root);
                onChanged?.Invoke();
            }).Every(ThemeCheckInterval);
        }

        /// <summary>
        /// Unity gives each editor window's root its default stylesheet for the current theme
        /// (DefaultCommonDark or DefaultCommonLight) and should swap it when the theme changes,
        /// but this window's root can keep the other theme's sheet, which makes text white in
        /// the Light theme. If the root has the wrong one, replace it with the matching sheet
        /// from an ancestor (the panel container has it), or drop it so the ancestor's applies.
        /// The sheets are rebuilt in their original order so the default sheet stays first.
        /// </summary>
        private static void FixUnityDefaultStyleSheet(VisualElement root)
        {
            var wrongPrefix = IsDark ? "DefaultCommonLight" : "DefaultCommonDark";
            var rightPrefix = IsDark ? "DefaultCommonDark" : "DefaultCommonLight";
            if (FindStyleSheet(root, wrongPrefix) == null) return;
            StyleSheet right = null;
            for (var ancestor = root.hierarchy.parent; ancestor != null && right == null; ancestor = ancestor.hierarchy.parent)
            {
                right = FindStyleSheet(ancestor, rightPrefix);
            }
            var sheets = new List<StyleSheet>();
            for (int i = 0; i < root.styleSheets.count; i++)
            {
                var sheet = root.styleSheets[i];
                if (sheet == null) continue;
                if (sheet.name.StartsWith(wrongPrefix))
                {
                    if (right != null && !sheets.Contains(right)) sheets.Add(right);
                }
                else if (!sheets.Contains(sheet))
                {
                    sheets.Add(sheet);
                }
            }
            root.styleSheets.Clear();
            foreach (var sheet in sheets)
            {
                root.styleSheets.Add(sheet);
            }
        }

        private static StyleSheet FindStyleSheet(VisualElement element, string namePrefix)
        {
            for (int i = 0; i < element.styleSheets.count; i++)
            {
                var sheet = element.styleSheets[i];
                if (sheet != null && sheet.name.StartsWith(namePrefix)) return sheet;
            }
            return null;
        }

        private static void AddStyleSheet(VisualElement root, string path)
        {
            var styleSheet = EditorGUIUtility.Load(path) as StyleSheet;
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);
        }

        private static void RemoveStyleSheet(VisualElement root, string path)
        {
            var styleSheet = EditorGUIUtility.Load(path) as StyleSheet;
            if (styleSheet != null && root.styleSheets.Contains(styleSheet)) root.styleSheets.Remove(styleSheet);
        }

    }

}

#endif
