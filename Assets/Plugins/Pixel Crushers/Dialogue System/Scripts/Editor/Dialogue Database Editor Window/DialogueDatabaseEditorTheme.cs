// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// Unity applies its own default editor stylesheet (DefaultCommonDark or DefaultCommonLight)
    /// to the root. The one exception this class handles is GraphView: see KeepLightThemeOnAttach.
    /// That was a headache to track down because it only affected the ConversationsPanel's 
    /// GraphView in the Light theme.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
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
            RestoreLightEditorStyleSheet(root);
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
                if (IsDark == wasDark) return;
                wasDark = IsDark;
                Apply(root);
                onChanged?.Invoke();
            }).Every(ThemeCheckInterval);
        }

        /// <summary>
        /// When a GraphView is attached to a panel, Unity calls its internal
        /// UIElementsEditorUtility.ForceDarkStyleSheet, which replaces DefaultCommonLight with
        /// DefaultCommonDark on the window's root. In the Light theme that gives every control
        /// in the window Dark-theme styling. This swaps the Light sheet back each time the
        /// GraphView is attached. Call it from the GraphView's constructor so the callback runs
        /// after GraphView's own.
        /// </summary>
        public static void KeepLightThemeOnAttach(VisualElement graphView)
        {
            if (graphView == null) return;
            graphView.RegisterCallback<AttachToPanelEvent>(evt => RestoreLightEditorStyleSheet(graphView));
        }

        /// <summary>
        /// In the Light theme, replaces Unity's DefaultCommonDark sheet with DefaultCommonLight
        /// on the nearest of element and its ancestors that has it, keeping the sheets' order.
        /// It only ever swaps one for the other, so it never leaves the root without a default sheet.
        /// </summary>
        private static void RestoreLightEditorStyleSheet(VisualElement element)
        {
            if (IsDark || element == null) return;
            var dark = GetUnityCommonStyleSheet("GetCommonDarkStyleSheet");
            var light = GetUnityCommonStyleSheet("GetCommonLightStyleSheet");
            if (dark == null || light == null) return;
            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                if (!e.styleSheets.Contains(dark)) continue;
                var sheets = new List<StyleSheet>();
                for (int i = 0; i < e.styleSheets.count; i++)
                {
                    var sheet = (e.styleSheets[i] == dark) ? light : e.styleSheets[i];
                    if (sheet != null && !sheets.Contains(sheet)) sheets.Add(sheet);
                }
                e.styleSheets.Clear();
                foreach (var sheet in sheets)
                {
                    e.styleSheets.Add(sheet);
                }
                return;
            }
        }

        private static Type unityEditorUtilityType = null;
        private static readonly Dictionary<string, StyleSheet> unityCommonStyleSheets = new Dictionary<string, StyleSheet>();

        /// <summary>
        /// Gets Unity's DefaultCommonDark or DefaultCommonLight sheet from the internal
        /// UIElementsEditorUtility class, or null if a future Unity version removes it.
        /// </summary>
        private static StyleSheet GetUnityCommonStyleSheet(string methodName)
        {
            StyleSheet styleSheet;
            if (unityCommonStyleSheets.TryGetValue(methodName, out styleSheet) && styleSheet != null) return styleSheet;
            try
            {
                if (unityEditorUtilityType == null)
                {
                    unityEditorUtilityType = typeof(EditorWindow).Assembly.GetType("UnityEditor.UIElements.UIElementsEditorUtility");
                }
                var method = unityEditorUtilityType?.GetMethod(methodName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                styleSheet = method?.Invoke(null, null) as StyleSheet;
            }
            catch (Exception)
            {
                styleSheet = null;
            }
            unityCommonStyleSheets[methodName] = styleSheet;
            return styleSheet;
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
