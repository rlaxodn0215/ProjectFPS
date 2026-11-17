// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Drop-down window that hosts a Conditions or Script wizard under a "..." button.
    /// Shows a preview of the generated Lua. Apply adds any new variables, passes the Lua
    /// to the caller, and closes. Revert or clicking outside closes without changes.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class LuaWizardPopup : EditorWindow
    {

        private const float Width = 520;
        private const float MinHeight = 120;
        private const float MaxHeight = 600;

        private static LuaWizardPopup current;

        [NonSerialized] private Rect activatorScreenRect;
        [NonSerialized] private LuaWizardElement wizard;
        [NonSerialized] private Action<string> onApply;
        [NonSerialized] private string undoName;
        [NonSerialized] private VisualElement content;
        [NonSerialized] private TextField preview;
        [NonSerialized] private Button applyButton;

        /// <summary>
        /// Opens the Conditions wizard under activatorScreenRect.
        /// </summary>
        /// <param name="onApply">Receives the new Lua code when the user clicks Apply.</param>
        /// <param name="addVariable">Adds a "(New)" variable when the user clicks Apply.</param>
        public static void ShowConditions(Rect activatorScreenRect, DialogueDatabase database, string luaCode,
            Action<string> onApply, Action<string, FieldType> addVariable)
        {
            Open(activatorScreenRect, new LuaConditionWizardElement(database, luaCode, addVariable), onApply, "Conditions Wizard");
        }

        /// <summary>
        /// Opens the Script wizard under activatorScreenRect.
        /// </summary>
        /// <param name="onApply">Receives the new Lua code when the user clicks Apply.</param>
        /// <param name="addVariable">Adds a "(New)" variable when the user clicks Apply.</param>
        public static void ShowScript(Rect activatorScreenRect, DialogueDatabase database, string luaCode,
            Action<string> onApply, Action<string, FieldType> addVariable)
        {
            Open(activatorScreenRect, new LuaScriptWizardElement(database, luaCode, addVariable), onApply, "Script Wizard");
        }

        public static void CloseOpenPopup()
        {
            if (current != null) current.Close();
            current = null;
        }

        private static void Open(Rect activatorScreenRect, LuaWizardElement wizard, Action<string> onApply, string undoName)
        {
            CloseOpenPopup();
            var popup = CreateInstance<LuaWizardPopup>();
            popup.activatorScreenRect = activatorScreenRect;
            popup.wizard = wizard;
            popup.onApply = onApply;
            popup.undoName = undoName;
            popup.titleContent = new GUIContent(undoName);
            current = popup;
            popup.ShowAsDropDown(activatorScreenRect, new Vector2(Mathf.Max(Width, activatorScreenRect.width), MinHeight));
            popup.Build();
        }

        private void OnDestroy()
        {
            if (current == this) current = null;
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            DialogueDatabaseEditorTheme.TrackThemeChanges(root, null);
            root.AddToClassList("ds-popup-root");
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) Close();
            });

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);

            // Everything goes in one container so its height can size the window.
            content = new VisualElement();
            content.style.paddingLeft = content.style.paddingRight = 6;
            content.style.paddingTop = content.style.paddingBottom = 6;
            scrollView.Add(content);

            content.Add(wizard);

            var previewLabel = new Label("Result");
            previewLabel.style.marginTop = 6;
            previewLabel.style.marginLeft = 3;
            content.Add(previewLabel);
            preview = new TextField() { multiline = true, isReadOnly = true };
            preview.style.whiteSpace = WhiteSpace.Normal;
            preview.style.minHeight = 36;
            content.Add(preview);

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 4;
            buttons.Add(new Button(Close) { text = "Revert", tooltip = "Close without changing the field." });
            applyButton = new Button(Apply) { text = "Apply", tooltip = "Set the field to the result." };
            buttons.Add(applyButton);
            content.Add(buttons);

            wizard.changed += RefreshPreview;
            RefreshPreview();
            content.RegisterCallback<GeometryChangedEvent>(evt => FitHeightToContent());
        }

        private void RefreshPreview()
        {
            if (wizard == null) return;
            preview.value = wizard.BuildLua();
            applyButton.SetEnabled(wizard.ItemCount > 0);
        }

        /// <summary>
        /// Resizes the window to its content, below the activator or above it if there's no
        /// room below. A drop-down window's position getter returns (0,0) for its location,
        /// so the location is computed from the activator rect instead of read back.
        /// </summary>
        private void FitHeightToContent()
        {
            if (float.IsNaN(content.layout.height)) return;
            var height = Mathf.Clamp(Mathf.Ceil(content.layout.height) + 2, MinHeight, MaxHeight);
            var width = position.width;
            if (Mathf.Abs(position.height - height) < 1) return;
            var desktop = UnityEditorInternal.InternalEditorUtility.GetBoundsOfDesktopAtPoint(activatorScreenRect.center);
            var x = Mathf.Clamp(activatorScreenRect.x, desktop.x, Mathf.Max(desktop.x, desktop.xMax - width));
            var y = activatorScreenRect.yMax;
            if (y + height > desktop.yMax && activatorScreenRect.y - height >= desktop.y) y = activatorScreenRect.y - height;
            minSize = new Vector2(width, height);
            maxSize = new Vector2(width, height);
            position = new Rect(x, y, width, height);
        }

        private void Apply()
        {
            if (wizard == null) return;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            wizard.AddNewVariables();
            var luaCode = wizard.BuildLua();
            var callback = onApply;
            Close();
            if (callback != null) callback(luaCode);
            Undo.SetCurrentGroupName(undoName);
            Undo.CollapseUndoOperations(undoGroup);
        }

    }

}

#endif
