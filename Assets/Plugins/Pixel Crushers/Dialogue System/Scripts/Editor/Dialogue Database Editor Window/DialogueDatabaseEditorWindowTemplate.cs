// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window holds the Template
    /// panel's state so it survives domain reloads. It isn't reset when the database changes.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [Serializable]
        public class TemplatePanelState
        {
            public List<string> expandedCategories = new List<string>();
            public string exportPath = string.Empty;
        }

        [SerializeField]
        private TemplatePanelState templatePanelState = new TemplatePanelState();
        public TemplatePanelState TemplateState { get { return templatePanelState; } }

    }

    /// <summary>
    /// Template category. Edits the fields that new assets get, which fields show in each
    /// asset type's main section (the Main column), and the outline line colors. Works without
    /// a database, in which case it edits the default template in EditorPrefs.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class TemplatePanel : DatabaseEditorPanel
    {

        private class Category
        {
            public string label;
            public string fieldsProperty;
            public string primaryTitlesProperty;
            public Category(string label, string fieldsProperty, string primaryTitlesProperty)
            {
                this.label = label;
                this.fieldsProperty = fieldsProperty;
                this.primaryTitlesProperty = primaryTitlesProperty;
            }
        }

        private static readonly Category[] Categories =
        {
            new Category("Actors", "actorFields", "actorPrimaryFieldTitles"),
            new Category("Items", "itemFields", "itemPrimaryFieldTitles"),
            new Category("Quests", "questFields", "questPrimaryFieldTitles"),
            new Category("Locations", "locationFields", "locationPrimaryFieldTitles"),
            new Category("Variables", "variableFields", "variablePrimaryFieldTitles"),
            new Category("Conversations", "conversationFields", "conversationPrimaryFieldTitles"),
            new Category("Dialogue Entries", "dialogueEntryFields", "dialogueEntryPrimaryFieldTitles"),
        };

        private const string UndoHint = "You can undo this with Edit > Undo.";

        private ScrollView scrollView;

        public override bool RequiresDatabase { get { return false; } }

        private DialogueDatabaseEditorWindow.TemplatePanelState templateState { get { return window.TemplateState; } }

        public TemplatePanel() : base("Templates")
        {
            style.paddingLeft = 0;
            style.paddingRight = 0;
            style.paddingTop = 0;
            style.paddingBottom = 0;
            Remove(titleLabel);

            var header = new UnityEditor.UIElements.Toolbar();
            titleLabel.style.fontSize = StyleKeyword.Null;
            titleLabel.style.marginBottom = 0;
            titleLabel.style.marginLeft = 4;
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.style.flexGrow = 1;
            header.Add(titleLabel);
            var menu = new ToolbarMenu() { text = "Menu" };
            PopulateMenu(menu.menu);
            header.Add(menu);
            Add(header);

            scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.contentContainer.style.paddingLeft = 8;
            scrollView.contentContainer.style.paddingRight = 8;
            scrollView.contentContainer.style.paddingTop = 4;
            scrollView.contentContainer.style.paddingBottom = 8;
            Add(scrollView);
        }

        public override void OnDatabaseChanged(SerializedObject serializedObject)
        {
            Rebuild();
        }

        public override void OnUndoRedo()
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (window == null) return;
            var scrollOffset = scrollView.scrollOffset;
            scrollView.Clear();
            var templateSerializedObject = window.TemplateSerializedObject;
            templateSerializedObject.Update();

            if (database == null)
            {
                scrollView.Add(new HelpBox("No database is selected. Changes apply to the default template for new databases, which is saved in your editor preferences.",
                    UnityEngine.UIElements.HelpBoxMessageType.Info));
            }

            foreach (var category in Categories)
            {
                var fieldList = new FieldListElement(
                    templateSerializedObject.FindProperty("template." + category.fieldsProperty),
                    database, null, templateState.expandedCategories.Contains(category.label),
                    category.label, templateSerializedObject.FindProperty("template." + category.primaryTitlesProperty));
                var label = category.label;
                fieldList.expandedChanged += expanded =>
                {
                    templateState.expandedCategories.Remove(label);
                    if (expanded) templateState.expandedCategories.Add(label);
                };
                fieldList.beforeDeleteField = title => ConfirmDeleteTemplateField(label, title);
                scrollView.Add(fieldList);
                window.AddAITemplateButtons(scrollView, label, ModifyTemplateAndAssets);
            }

            var colorsLabel = new Label("Line Colors");
            colorsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            colorsLabel.style.marginTop = 10;
            colorsLabel.style.marginLeft = 3;
            scrollView.Add(colorsLabel);
            scrollView.Add(CreateColorField("NPC Lines", "Color for NPC lines in Outline mode.", templateSerializedObject.FindProperty("template.npcLineColor")));
            scrollView.Add(CreateColorField("PC Lines", "Color for PC lines in Outline mode.", templateSerializedObject.FindProperty("template.pcLineColor")));
            scrollView.Add(CreateColorField("Repeat Lines", "Color for repeated lines in Outline mode.", templateSerializedObject.FindProperty("template.repeatLineColor")));

            // Save whenever the template changes.
            var tracker = new VisualElement();
            scrollView.Add(tracker);
            tracker.TrackSerializedObjectValue(templateSerializedObject, so => window.SaveTemplateIfChanged());

            scrollView.schedule.Execute(() => scrollView.scrollOffset = scrollOffset);
        }

        private static ColorField CreateColorField(string label, string tooltip, SerializedProperty property)
        {
            var colorField = new ColorField(label) { tooltip = tooltip };
            colorField.BindProperty(property);
            return colorField;
        }

        #region Menu

        private void PopulateMenu(DropdownMenu menu)
        {
            Func<DropdownMenuAction, DropdownMenuAction.Status> needsDatabase =
                a => (database != null) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            menu.AppendAction("Export XML...", a => ExportTemplate());
            menu.AppendAction("Import XML...", a => ImportTemplate());
            menu.AppendAction("Update Template From Assets", a => ConfirmUpdateTemplateFromAssets(), needsDatabase);
            menu.AppendAction("Apply Template To Assets", a => ConfirmApplyTemplateToAssets(), needsDatabase);
            menu.AppendAction("Sync Assets and Template", a => ConfirmSyncAssetsAndTemplate(), needsDatabase);
            menu.AppendAction("Remove Empty Fields", a => ConfirmRemoveEmptyFields());
            menu.AppendAction("Reset", a => ConfirmResetTemplate());
        }

        /// <summary>
        /// Runs a bulk change to the template and/or the database's assets as one undoable step.
        /// Bulk operations work on the objects directly (recorded with Undo.RecordObject), then
        /// update the SerializedObjects and save the template.
        /// </summary>
        private void ModifyTemplateAndAssets(string undoName, Action<DialogueDatabaseTemplateHolder, DialogueDatabase> modify)
        {
            var templateSerializedObject = window.TemplateSerializedObject;
            templateSerializedObject.ApplyModifiedProperties();
            Undo.RecordObject(window.TemplateHolder, undoName);
            var databaseSerializedObject = window.SerializedObject;
            if (database != null)
            {
                if (databaseSerializedObject != null) databaseSerializedObject.ApplyModifiedProperties();
                Undo.RecordObject(database, undoName);
            }
            modify(window.TemplateHolder, database);
            if (database != null)
            {
                EditorUtility.SetDirty(database);
                if (databaseSerializedObject != null) databaseSerializedObject.Update();
            }
            templateSerializedObject.Update();
            window.SaveTemplateIfChanged();
            Rebuild();
        }

        private void ExportTemplate()
        {
            var path = EditorUtility.SaveFilePanel("Export Template to XML", EditorWindowTools.GetDirectoryName(templateState.exportPath), templateState.exportPath, "xml");
            if (string.IsNullOrEmpty(path)) return;
            templateState.exportPath = path;
            File.WriteAllText(path, TemplateTools.ToXml(window.Template), System.Text.Encoding.Unicode);
        }

        private void ImportTemplate()
        {
            var path = EditorUtility.OpenFilePanel("Import Template from XML", EditorWindowTools.GetDirectoryName(templateState.exportPath), "xml");
            if (string.IsNullOrEmpty(path)) return;
            Template newTemplate = null;
            try
            {
                newTemplate = TemplateTools.FromXml(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("{0}: Unable to import template from {1}: {2}", DialogueDebug.Prefix, path, e.Message));
            }
            if (newTemplate == null)
            {
                EditorUtility.DisplayDialog("Import Error", "Unable to import template data from the XML file.", "OK");
                return;
            }
            ModifyTemplateAndAssets("Import Template", (holder, db) => holder.template = newTemplate);
        }

        private void ConfirmResetTemplate()
        {
            if (!EditorUtility.DisplayDialog("Reset template?", "This replaces the template with the default template. " + UndoHint, "Reset", "Cancel")) return;
            ModifyTemplateAndAssets("Reset Template", (holder, db) => holder.template = Template.FromDefault());
        }

        private void ConfirmUpdateTemplateFromAssets()
        {
            if (!EditorUtility.DisplayDialog("Update template from assets?",
                "This will update the template with any custom fields you've added to any assets in your dialogue database. " + UndoHint, "Update", "Cancel")) return;
            ModifyTemplateAndAssets("Update Template From Assets", (holder, db) => UpdateTemplateFromAssets(holder.template, db));
            Debug.Log(string.Format("{0}: Dialogue Editor template now contains all fields listed in any dialogue database asset.", DialogueDebug.Prefix));
        }

        private void ConfirmApplyTemplateToAssets()
        {
            if (!EditorUtility.DisplayDialog("Apply template to assets?",
                "This will add any missing template fields to all assets. " + UndoHint, "Apply", "Cancel")) return;
            ModifyTemplateAndAssets("Apply Template To Assets", (holder, db) => ApplyTemplateToAssets(holder.template, db));
            Debug.Log(string.Format("{0}: All assets now have all fields listed in the template.", DialogueDebug.Prefix));
        }

        private void ConfirmSyncAssetsAndTemplate()
        {
            if (!EditorUtility.DisplayDialog("Sync template and assets?",
                "This will update the template with any custom fields you've added to assets in your dialogue database, and then apply the updated template to all assets, putting their fields in template order. " + UndoHint, "Sync", "Cancel")) return;
            ModifyTemplateAndAssets("Sync Assets and Template", (holder, db) => SyncAssetsAndTemplate(holder.template, db));
            Debug.Log(string.Format("{0}: Dialogue Editor template now contains all fields listed in any dialogue database asset, and those assets now have all fields listed in the template.", DialogueDebug.Prefix));
        }

        private void ConfirmRemoveEmptyFields()
        {
            var scope = (database != null) ? "the template and all assets" : "the template";
            if (!EditorUtility.DisplayDialog("Remove empty fields?", string.Format("This will remove fields whose titles are blank from {0}. {1}", scope, UndoHint), "Remove", "Cancel")) return;
            ModifyTemplateAndAssets("Remove Empty Fields", (holder, db) => RemoveEmptyFields(holder.template, db));
            Debug.Log(string.Format("{0}: Removed empty fields from {1}.", DialogueDebug.Prefix, scope));
        }

        /// <summary>
        /// Asks whether to also remove a deleted template field from the database's assets of
        /// that category, like the IMGUI editor. Returns false if the user cancels.
        /// </summary>
        private bool ConfirmDeleteTemplateField(string categoryLabel, string fieldTitle)
        {
            if (database == null) return true;
            var result = EditorUtility.DisplayDialogComplex(string.Format("Delete '{0}'", fieldTitle),
                string.Format("Delete {0} from all assets in the database, or only from the template?", categoryLabel), "Remove All", "Template Only", "Cancel");
            if (result == 2) return false;
            if (result == 0)
            {
                // Recorded in the same undo group as the template field deletion that follows.
                var databaseSerializedObject = window.SerializedObject;
                if (databaseSerializedObject != null) databaseSerializedObject.ApplyModifiedProperties();
                Undo.RecordObject(database, "Remove Field From Assets");
                ScrubFieldFromCategory(database, categoryLabel, fieldTitle);
                EditorUtility.SetDirty(database);
                if (databaseSerializedObject != null) databaseSerializedObject.Update();
            }
            return true;
        }

        #endregion

        #region Template Operations

        // Ported from DialogueEditorWindowUpdateTemplateSection and DialogueEditorWindowTemplateSection.

        private static bool MatchesItemFilter(List<Field> fields, bool checkIsItem, bool requireIsItem)
        {
            if (!checkIsItem) return true;
            var isItemField = Field.Lookup(fields, DialogueSystemFields.IsItem);
            var isItem = isItemField != null && Tools.StringToBool(isItemField.value);
            return requireIsItem == isItem;
        }

        private static void AddMissingFieldsToTemplate(List<Field> assetFields, List<Field> templateFields, bool checkIsItem = false, bool requireIsItem = false)
        {
            if (assetFields == null || !MatchesItemFilter(assetFields, checkIsItem, requireIsItem)) return;
            foreach (var field in assetFields)
            {
                if (!Field.FieldExists(templateFields, field.title))
                {
                    templateFields.Add(new Field(field.title, string.Empty, field.type));
                }
            }
        }

        /// <summary>
        /// Replaces an asset's fields with one field per template field, in template order,
        /// keeping existing values and adding missing fields from the template.
        /// </summary>
        private static void EnforceTemplateOnFields(List<Field> fields, List<Field> templateFields, bool checkIsItem = false, bool requireIsItem = false)
        {
            if (fields == null || !MatchesItemFilter(fields, checkIsItem, requireIsItem)) return;
            var newFields = new List<Field>();
            foreach (var templateField in templateFields)
            {
                if (string.IsNullOrEmpty(templateField.title)) continue;
                newFields.Add(Field.Lookup(fields, templateField.title) ?? new Field(templateField));
            }
            fields.Clear();
            fields.AddRange(newFields);
        }

        /// <summary>
        /// Adds missing template fields and fixes field types, like DialogueEditorWindow.ApplyTemplate.
        /// </summary>
        private static void ApplyTemplate(List<Field> fields, List<Field> templateFields)
        {
            if (fields == null || templateFields == null) return;
            foreach (var templateField in templateFields)
            {
                if (string.IsNullOrEmpty(templateField.title)) continue;
                var field = Field.Lookup(fields, templateField.title);
                if (field != null)
                {
                    // Variables' Initial Value should never be applied from template.
                    if (field.type != templateField.type && !string.Equals(field.title, "Initial Value"))
                    {
                        field.type = templateField.type;
                        field.typeString = string.Empty;
                    }
                }
                else
                {
                    fields.Add(new Field(templateField));
                }
            }
        }

        private static void UpdateTemplateFromAssets(Template template, DialogueDatabase database)
        {
            database.actors.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, template.actorFields));
            database.items.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, template.itemFields, true, true));
            database.items.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, template.questFields, true, false));
            database.locations.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, template.locationFields));
            database.variables.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, template.variableFields));
            database.conversations.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, template.conversationFields));
            foreach (var conversation in database.conversations)
            {
                conversation.dialogueEntries.ForEach(entry => AddMissingFieldsToTemplate(entry.fields, template.dialogueEntryFields));
            }
        }

        private static void ApplyTemplateToAssets(Template template, DialogueDatabase database)
        {
            database.actors.ForEach(asset => ApplyTemplate(asset.fields, template.actorFields));
            database.items.ForEach(item => ApplyTemplate(item.fields, item.IsItem ? template.itemFields : template.questFields));
            database.locations.ForEach(asset => ApplyTemplate(asset.fields, template.locationFields));
            database.variables.ForEach(asset => ApplyTemplate(asset.fields, template.variableFields));
            database.conversations.ForEach(asset => ApplyTemplate(asset.fields, template.conversationFields));
            foreach (var conversation in database.conversations)
            {
                conversation.dialogueEntries.ForEach(entry => ApplyTemplate(entry.fields, template.dialogueEntryFields));
            }
        }

        internal static void SyncAssetsAndTemplate(Template template, DialogueDatabase database)
        {
            NormalizeAssets(database.actors, template.actorFields);
            NormalizeAssets(database.items, template.itemFields, true, true);
            NormalizeAssets(database.items, template.questFields, true, false);
            NormalizeAssets(database.locations, template.locationFields);
            NormalizeAssets(database.variables, template.variableFields);
            NormalizeAssets(database.conversations, template.conversationFields);
            foreach (var conversation in database.conversations)
            {
                conversation.dialogueEntries.ForEach(entry => AddMissingFieldsToTemplate(entry.fields, template.dialogueEntryFields));
            }
            foreach (var conversation in database.conversations)
            {
                conversation.dialogueEntries.ForEach(entry => EnforceTemplateOnFields(entry.fields, template.dialogueEntryFields));
            }
        }

        private static void NormalizeAssets<T>(List<T> assets, List<Field> templateFields, bool checkIsItem = false, bool requireIsItem = false) where T : Asset
        {
            assets.ForEach(asset => AddMissingFieldsToTemplate(asset.fields, templateFields, checkIsItem, requireIsItem));
            assets.ForEach(asset => EnforceTemplateOnFields(asset.fields, templateFields, checkIsItem, requireIsItem));
        }

        private static void RemoveEmptyFields(Template template, DialogueDatabase database)
        {
            foreach (var fields in new List<Field>[] { template.actorFields, template.itemFields, template.questFields, template.locationFields,
                template.variableFields, template.conversationFields, template.dialogueEntryFields })
            {
                RemoveEmptyFields(fields);
            }
            if (database == null) return;
            database.actors.ForEach(asset => RemoveEmptyFields(asset.fields));
            database.items.ForEach(asset => RemoveEmptyFields(asset.fields));
            database.locations.ForEach(asset => RemoveEmptyFields(asset.fields));
            database.variables.ForEach(asset => RemoveEmptyFields(asset.fields));
            database.conversations.ForEach(asset => RemoveEmptyFields(asset.fields));
            foreach (var conversation in database.conversations)
            {
                conversation.dialogueEntries.ForEach(entry => RemoveEmptyFields(entry.fields));
            }
        }

        private static void RemoveEmptyFields(List<Field> fields)
        {
            if (fields != null) fields.RemoveAll(field => string.IsNullOrEmpty(field.title));
        }

        private static void ScrubFieldFromCategory(DialogueDatabase database, string categoryLabel, string fieldTitle)
        {
            switch (categoryLabel)
            {
                case "Actors":
                    database.actors.ForEach(asset => ScrubField(asset.fields, fieldTitle));
                    break;
                case "Items":
                    database.items.ForEach(item => { if (item.IsItem) ScrubField(item.fields, fieldTitle); });
                    break;
                case "Quests":
                    database.items.ForEach(item => { if (!item.IsItem) ScrubField(item.fields, fieldTitle); });
                    break;
                case "Locations":
                    database.locations.ForEach(asset => ScrubField(asset.fields, fieldTitle));
                    break;
                case "Variables":
                    database.variables.ForEach(asset => ScrubField(asset.fields, fieldTitle));
                    break;
                case "Conversations":
                    database.conversations.ForEach(asset => ScrubField(asset.fields, fieldTitle));
                    break;
                case "Dialogue Entries":
                    foreach (var conversation in database.conversations)
                    {
                        conversation.dialogueEntries.ForEach(entry => ScrubField(entry.fields, fieldTitle));
                    }
                    break;
            }
        }

        private static void ScrubField(List<Field> fields, string fieldTitle)
        {
            if (fields == null) return;
            var field = fields.Find(f => string.Equals(f.title, fieldTitle));
            if (field != null) fields.Remove(field);
        }

        #endregion

    }

}

#endif
