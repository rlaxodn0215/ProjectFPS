// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Database panel's Merge Database section: merges another database's contents into
    /// this one with DatabaseMerger, showing a summary of what the merge will do first.
    /// </summary>
    public partial class DatabasePanel
    {

        private const float MergeLabelWidth = 150;
        private const float MergeIndent = 15;
        private const float MergeButtonWidth = 100;
        private const string MergeUndoName = "Merge Database";

        private const string ConflictRuleTooltip =
            "Replace Conflicting IDs: If the same ID exists in both databases, replace the original one with the new one.\n" +
            "Allow Conflicting IDs: Append assets even if IDs conflict.\n" +
            "Assign Unique IDs: Append and assign new IDs to assets from the source database. Assets with the same names as existing ones aren't added; references to them use the existing ones.";

        private ObjectField mergeSourceField = null;
        private UnityEngine.UIElements.Toggle mergePropertiesToggle = null;
        private UnityEngine.UIElements.Toggle mergeEmphasesToggle = null;
        private UnityEngine.UIElements.Toggle mergeActorsToggle = null;
        private UnityEngine.UIElements.Toggle mergeItemsToggle = null;
        private UnityEngine.UIElements.Toggle mergeLocationsToggle = null;
        private UnityEngine.UIElements.Toggle mergeVariablesToggle = null;
        private UnityEngine.UIElements.Toggle mergeConversationsToggle = null;
        private EnumField mergeRuleField = null;
        private HelpBox mergeErrorBox = null;
        private HelpBox mergeWarningBox = null;
        private VisualElement mergePreviewBox = null;
        private Label mergePreviewLabel = null;
        private Button mergeButton = null;

        // The panel is created before its window is assigned, so the fields are filled from
        // the window's state later, in RefreshMergeFields.
        private void AddMergeSection(VisualElement parent)
        {
            parent.Add(new HelpBox("Add the contents of another database to this database.", UnityEngine.UIElements.HelpBoxMessageType.None));

            mergeSourceField = new ObjectField("Source Database")
            {
                objectType = typeof(DialogueDatabase),
                allowSceneObjects = false,
                tooltip = "Database whose contents will be added to this database."
            };
            mergeSourceField.style.marginTop = 4;
            mergeSourceField.labelElement.style.minWidth = MergeLabelWidth;
            mergeSourceField.RegisterValueChangedCallback(evt =>
            {
                if (window == null) return;
                databaseState.mergeSource = evt.newValue as DialogueDatabase;
                RefreshMergePreview();
            });
            parent.Add(mergeSourceField);

            mergePropertiesToggle = AddMergeToggle(parent, "Merge DB Properties", "Fill in this database's blank Author, Version and Description, and append the source's Global User Script.",
                (state, value) => state.mergeProperties = value);
            mergeEmphasesToggle = AddMergeToggle(parent, "Merge [em#] Settings", "Replace this database's [em#] emphasis settings with the source's.",
                (state, value) => state.mergeEmphases = value);
            mergeEmphasesToggle.style.marginLeft = MergeIndent;
            mergeEmphasesToggle.labelElement.style.minWidth = MergeLabelWidth - MergeIndent;
            mergeActorsToggle = AddMergeToggle(parent, "Merge Actors", "Add the source's actors.", (state, value) => state.mergeActors = value);
            mergeItemsToggle = AddMergeToggle(parent, "Merge Items/Quests", "Add the source's items and quests.", (state, value) => state.mergeItems = value);
            mergeLocationsToggle = AddMergeToggle(parent, "Merge Locations", "Add the source's locations.", (state, value) => state.mergeLocations = value);
            mergeVariablesToggle = AddMergeToggle(parent, "Merge Variables", "Add the source's variables.", (state, value) => state.mergeVariables = value);
            mergeConversationsToggle = AddMergeToggle(parent, "Merge Conversations", "Add the source's conversations.", (state, value) => state.mergeConversations = value);

            mergeRuleField = new EnumField("If IDs Conflict", DatabaseMerger.ConflictingIDRule.ReplaceConflictingIDs) { tooltip = ConflictRuleTooltip };
            mergeRuleField.labelElement.style.minWidth = MergeLabelWidth;
            mergeRuleField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is DatabaseMerger.ConflictingIDRule)) return;
                databaseState.mergeConflictingIDRule = (DatabaseMerger.ConflictingIDRule)evt.newValue;
                RefreshMergePreview();
            });
            parent.Add(mergeRuleField);

            mergeErrorBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.Error);
            mergeErrorBox.style.marginTop = 6;
            parent.Add(mergeErrorBox);

            mergePreviewBox = new VisualElement();
            mergePreviewBox.AddToClassList("ds-box");
            mergePreviewBox.style.marginTop = 6;
            mergePreviewBox.style.paddingLeft = 6;
            mergePreviewBox.style.paddingRight = 6;
            mergePreviewBox.style.paddingTop = 4;
            mergePreviewBox.style.paddingBottom = 4;
            var previewHeading = new Label("Merge Preview");
            previewHeading.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewHeading.style.marginBottom = 2;
            mergePreviewBox.Add(previewHeading);
            mergePreviewLabel = new Label() { enableRichText = false };
            mergePreviewLabel.style.whiteSpace = WhiteSpace.Normal;
            mergePreviewBox.Add(mergePreviewLabel);
            parent.Add(mergePreviewBox);

            mergeWarningBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.Warning);
            mergeWarningBox.style.marginTop = 4;
            parent.Add(mergeWarningBox);

            mergeButton = new Button(ConfirmAndMerge)
            {
                text = "Merge...",
                tooltip = "Merge the source database into this database. You can undo the merge."
            };
            mergeButton.style.width = MergeButtonWidth;
            mergeButton.style.alignSelf = Align.FlexEnd;
            mergeButton.style.marginTop = 6;
            parent.Add(mergeButton);

            RefreshMergePreview();
        }

        private UnityEngine.UIElements.Toggle AddMergeToggle(VisualElement parent, string label, string tooltip,
            System.Action<DialogueDatabaseEditorWindow.DatabasePanelState, bool> setState)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label) { tooltip = tooltip, value = true };
            toggle.labelElement.style.minWidth = MergeLabelWidth;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (window == null) return;
                setState(databaseState, evt.newValue);
                RefreshMergePreview();
            });
            parent.Add(toggle);
            return toggle;
        }

        /// <summary>Shows the window's saved merge options in the fields.</summary>
        private void RefreshMergeFields()
        {
            if (window == null || mergeSourceField == null) return;
            var state = databaseState;
            mergeSourceField.SetValueWithoutNotify(state.mergeSource);
            mergePropertiesToggle.SetValueWithoutNotify(state.mergeProperties);
            mergeEmphasesToggle.SetValueWithoutNotify(state.mergeEmphases);
            mergeActorsToggle.SetValueWithoutNotify(state.mergeActors);
            mergeItemsToggle.SetValueWithoutNotify(state.mergeItems);
            mergeLocationsToggle.SetValueWithoutNotify(state.mergeLocations);
            mergeVariablesToggle.SetValueWithoutNotify(state.mergeVariables);
            mergeConversationsToggle.SetValueWithoutNotify(state.mergeConversations);
            mergeRuleField.SetValueWithoutNotify(state.mergeConflictingIDRule);
            RefreshMergePreview();
        }

        /// <summary>
        /// Updates the preview, warning and error from the current options and databases.
        /// Called when an option changes, when the section is shown, and after undo/redo.
        /// </summary>
        private void RefreshMergePreview()
        {
            if (mergeButton == null) return;
            var error = (window != null) ? GetMergeError() : "No database.";
            var summary = (error == null) ? GetMergeSummary() : null;
            if (mergeEmphasesToggle != null) mergeEmphasesToggle.style.display = (window != null && databaseState.mergeProperties) ? DisplayStyle.Flex : DisplayStyle.None;
            mergeErrorBox.text = error ?? string.Empty;
            mergeErrorBox.style.display = (error != null && window != null && databaseState.mergeSource != null) ? DisplayStyle.Flex : DisplayStyle.None;
            mergePreviewBox.style.display = (summary != null) ? DisplayStyle.Flex : DisplayStyle.None;
            mergePreviewLabel.text = (summary != null) ? summary.ToString() : string.Empty;
            var conflicts = (summary != null) ? summary.totalIDConflicts : 0;
            mergeWarningBox.text = string.Format("{0} added asset{1} will have the same ID as an existing asset, which may break conversations and quests.",
                conflicts, (conflicts == 1) ? string.Empty : "s");
            mergeWarningBox.style.display = (conflicts > 0) ? DisplayStyle.Flex : DisplayStyle.None;
            mergeButton.SetEnabled(error == null);
        }

        /// <summary>Returns why the merge can't run, or null if it can.</summary>
        private string GetMergeError()
        {
            var state = databaseState;
            if (database == null) return "No database.";
            if (state.mergeSource == null) return "Select a source database.";
            if (state.mergeSource == database) return "The source database is the database being edited. Select a different database.";
            if (!(state.mergeProperties || state.mergeActors || state.mergeItems || state.mergeLocations || state.mergeVariables || state.mergeConversations))
            {
                return "Select at least one part of the database to merge.";
            }
            return null;
        }

        private DatabaseMerger.MergeSummary GetMergeSummary()
        {
            var state = databaseState;
            return DatabaseMerger.GetMergeSummary(database, state.mergeSource, state.mergeConflictingIDRule,
                state.mergeProperties, state.mergeEmphases, state.mergeActors, state.mergeItems,
                state.mergeLocations, state.mergeVariables, state.mergeConversations);
        }

        private string GetMergeRuleWarning()
        {
            var sourceName = databaseState.mergeSource.name;
            switch (databaseState.mergeConflictingIDRule)
            {
                case DatabaseMerger.ConflictingIDRule.ReplaceConflictingIDs:
                    return string.Format("If assets with the same IDs exist in both databases, the versions in {0} will replace the versions in this database.", sourceName);
                case DatabaseMerger.ConflictingIDRule.AllowConflictingIDs:
                    return string.Format("If IDs in {0} already exist in this database, you'll get overlaps that may break conversations.", sourceName);
                case DatabaseMerger.ConflictingIDRule.AssignUniqueIDs:
                    return string.Format("Assets from {0} will get new IDs that aren't used in this database.", sourceName);
                default:
                    return string.Empty;
            }
        }

        private void ConfirmAndMerge()
        {
            if (window == null || GetMergeError() != null) return;
            var state = databaseState;
            var source = state.mergeSource;
            var summary = GetMergeSummary();
            var message = string.Format("Merge {0} into {1}?\n\n{2}\n\n{3}", source.name, database.name, summary, GetMergeRuleWarning());
            if (!EditorUtility.DisplayDialog("Merge Database", message, "Merge", "Cancel")) return;

            if (serializedObject != null) serializedObject.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(database, MergeUndoName);
            // Merge from a temporary copy, so nothing in this database can share objects with
            // the source asset, such as entries' OnExecute events.
            var sourceCopy = Object.Instantiate(source);
            try
            {
                DatabaseMerger.Merge(database, sourceCopy, state.mergeConflictingIDRule, state.mergeProperties, state.mergeEmphases,
                    state.mergeActors, state.mergeItems, state.mergeLocations, state.mergeVariables, state.mergeConversations);
            }
            finally
            {
                Object.DestroyImmediate(sourceCopy);
                EditorUtility.SetDirty(database);
                state.mergeSource = null;
                window.RefreshAfterDirectDatabaseChange();
                if (DialogueEditorWindow.instance != null) DialogueEditorWindow.instance.Reset();
            }
            Debug.Log(string.Format("{0}: Merged contents of {1} into {2}.", DialogueDebug.Prefix, source.name, database.name));
        }

    }

}

#endif
