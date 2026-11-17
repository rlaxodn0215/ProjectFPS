// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window holds the Variables
    /// panel's state so it survives domain reloads.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [SerializeField]
        private AssetPanelState variablesPanelState = new AssetPanelState();
        public AssetPanelState VariablesState { get { return variablesPanelState; } }

    }

    /// <summary>
    /// Variables category. A variable's type is the type of its Initial Value field.
    /// With Use Group Foldouts, the list groups variables by the part of the name before
    /// the first dot, such as "Quest" in "Quest.KillCount". Variables that share a name are
    /// highlighted, since Lua can only hold one of them.
    /// </summary>
    public class VariablesPanel : AssetListPanel<Variable>
    {

        private const double ConflictCheckInterval = 0.25; // seconds


        private HashSet<string> conflictedNames = new HashSet<string>();
        private double nextConflictCheckTime = 0;
        private HelpBox conflictHelpBox;

        public VariablesPanel() : base("Variables") { }

        protected override string AssetTypeName { get { return "Variable"; } }
        protected override string ListPropertyName { get { return "variables"; } }
        protected override DialogueDatabaseEditorWindow.AssetPanelState state { get { return window.VariablesState; } }
        protected override bool ShowIDField { get { return false; } } // Variables are referenced by name, like the IMGUI editor.

        protected override List<Variable> GetAssets(DialogueDatabase database) { return database.variables; }
        protected override List<Field> GetTemplateFields(Template template, Variable variable) { return template.variableFields; }
        protected override List<string> GetPrimaryFieldTitles(Template template, Variable variable) { return template.variablePrimaryFieldTitles; }

        protected override bool IsSyncEnabled
        {
            get { return database.syncInfo.syncVariables; }
            set { database.syncInfo.syncVariables = value; }
        }

        protected override DialogueDatabase SyncDatabase
        {
            get { return database.syncInfo.syncVariablesDatabase; }
            set { database.syncInfo.syncVariablesDatabase = value; }
        }

        protected override void SyncFromDatabase() { database.SyncVariables(); }

        private bool UseGroupFoldouts
        {
            get { return DialogueDatabaseEditorSettings.instance.useGroupFoldouts; }
            set
            {
                DialogueDatabaseEditorSettings.instance.useGroupFoldouts = value;
                DialogueDatabaseEditorSettings.instance.Save();
            }
        }

        protected override string GetGroupName(Variable variable)
        {
            if (!UseGroupFoldouts || variable == null) return null;
            var name = variable.Name ?? string.Empty;
            var dotPos = name.IndexOf('.');
            return (dotPos > 0) ? name.Substring(0, dotPos) : string.Empty;
        }

        #region Menu and Add

        protected override void PopulateMenu(DropdownMenu menu)
        {
            menu.AppendAction("New Variable", a => AddNewVariable("CustomFieldType_Text"));
            menu.AppendAction("Sort/By Name", a => SortAssets(variable => variable.Name, System.StringComparer.CurrentCulture));
            menu.AppendAction("Sort/By ID", a => SortAssets(variable => variable.id, Comparer<int>.Default));
            AppendHideFilteredMenuItem(menu);
            AppendSyncMenuItem(menu);
            menu.AppendAction("Use Group Foldouts", a =>
            {
                UseGroupFoldouts = !UseGroupFoldouts;
                RefreshListRows();
            }, a => UseGroupFoldouts ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Variable Viewer...", a => VariableViewWindow.OpenVariableViewWindow());
        }

        protected override void OnAddButtonClicked(Button button)
        {
            // Like the IMGUI editor, "+" asks for the new variable's type.
            var typeNames = CustomFieldTypeService.GetDialogueSystemTypes();
            var publicNames = CustomFieldTypeService.GetDialogueSystemPublicNames();
            var menu = new GenericMenu();
            for (int i = 0; i < publicNames.Length; i++)
            {
                var typeName = (i < typeNames.Length) ? typeNames[i] : "CustomFieldType_Text";
                menu.AddItem(new GUIContent(publicNames[i]), false, () => AddNewVariable(typeName));
            }
            menu.DropDown(button.worldBound);
        }

        /// <summary>
        /// Adds a variable whose Initial Value has the given custom field type. With group
        /// foldouts on, the new variable goes in the selected variable's group.
        /// </summary>
        private void AddNewVariable(string typeName)
        {
            var group = GetGroupName(GetAssetObject(state.selectedIndex));
            AddNewAsset(window.Template.variableFields, "Variable", variableProperty =>
            {
                var fieldsProperty = variableProperty.FindPropertyRelative("fields");
                foreach (var title in new string[] { DialogueSystemFields.Name, DialogueSystemFields.InitialValue, DialogueSystemFields.Description })
                {
                    if (FieldPropertyUtility.FindField(fieldsProperty, title) == null) FieldPropertyUtility.AddField(fieldsProperty, title, string.Empty, FieldType.Text);
                }
                if (!string.IsNullOrEmpty(group))
                {
                    var nameValue = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.Name).FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                    nameValue.stringValue = group + "." + nameValue.stringValue;
                }
                CustomFieldType customFieldType;
                if (CustomFieldTypeService.GetTypesDictionary().TryGetValue(typeName, out customFieldType))
                {
                    var initialValue = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.InitialValue);
                    initialValue.FindPropertyRelative(FieldPropertyUtility.TypePropertyName).intValue = (int)customFieldType.storeFieldAsType;
                    initialValue.FindPropertyRelative(FieldPropertyUtility.TypeStringPropertyName).stringValue = typeName;
                }
            });
            nextConflictCheckTime = 0;
        }

        #endregion

        #region Name Conflicts

        private void UpdateConflictedNames(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup < nextConflictCheckTime) return;
            nextConflictCheckTime = EditorApplication.timeSinceStartup + ConflictCheckInterval;
            conflictedNames.Clear();
            if (database == null) return;
            var names = new HashSet<string>();
            foreach (var variable in database.variables)
            {
                var name = variable.Name;
                if (!names.Add(name)) conflictedNames.Add(name);
            }
        }

        private bool IsConflicted(Variable variable)
        {
            UpdateConflictedNames(false);
            return variable != null && conflictedNames.Contains(variable.Name);
        }

        protected override void StyleListRow(VisualElement row, Label nameLabel, Variable variable)
        {
            var isConflicted = IsConflicted(variable);
            nameLabel.EnableInClassList("ds-conflict", isConflicted);
            nameLabel.tooltip = isConflicted ? "Another variable has the same name." : null;
        }

        protected override void OnNameChanged()
        {
            // Names determine conflicts and, with group foldouts, which group a variable is in.
            UpdateConflictedNames(true);
            if (conflictHelpBox != null)
            {
                conflictHelpBox.style.display = IsConflicted(GetAssetObject(state.selectedIndex)) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (UseGroupFoldouts) RefreshListRows(); else RefreshListItems();
        }

        #endregion

        #region Details

        protected override void AddAssetDetails(VisualElement parent, SerializedProperty variableProperty, SerializedProperty fieldsProperty)
        {
            conflictHelpBox = new HelpBox("Another variable has the same name. Lua can only hold one value per name, so rename one of them.",
                UnityEngine.UIElements.HelpBoxMessageType.Warning);
            UpdateConflictedNames(true);
            conflictHelpBox.style.display = IsConflicted(GetAssetObject(state.selectedIndex)) ? DisplayStyle.Flex : DisplayStyle.None;
            parent.Add(conflictHelpBox);

            AddInitialValueField(parent, fieldsProperty);
            AddPrimaryFields(parent, fieldsProperty, true, title => title == DialogueSystemFields.InitialValue);

            if (EditorApplication.isPlaying)
            {
                parent.Add(new HelpBox("Use the Watches tab or the Variable Viewer for runtime values.", UnityEngine.UIElements.HelpBoxMessageType.Info));
            }
        }

        /// <summary>
        /// Initial Value editor, which depends on the variable's type, plus a type popup.
        /// Changing the type clears the value and rebuilds the editor.
        /// </summary>
        private void AddInitialValueField(VisualElement parent, SerializedProperty fieldsProperty)
        {
            var initialValueProperty = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.InitialValue);
            if (initialValueProperty == null)
            {
                parent.Add(CreateOptionalTextField("Initial Value", "The variable's value when the game starts.", fieldsProperty, DialogueSystemFields.InitialValue, false));
                return;
            }
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var valueEditor = FieldPropertyUtility.CreateValueEditor(initialValueProperty, database, "Initial Value");
            valueEditor.tooltip = "The variable's value when the game starts.";
            valueEditor.style.flexGrow = 1;
            row.Add(valueEditor);
            var typePopup = FieldPropertyUtility.CreateTypePopup(initialValueProperty, DeferRebuildDetails);
            typePopup.tooltip = "The variable's type.";
            typePopup.style.width = 110;
            typePopup.style.flexShrink = 0;
            row.Add(typePopup);
            parent.Add(row);
        }

        #endregion

    }

}

#endif
