// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// "All Fields" foldout that edits an asset's List<Field> through SerializedProperty.
    /// Each row has the field's title, value, and type, plus move and delete buttons.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class FieldListElement : VisualElement
    {

        private static List<Field> clipboardFields = null;

        private SerializedProperty fieldsProperty;
        private DialogueDatabase database;
        private Func<List<Field>> getTemplateFields;
        private SerializedProperty primaryFieldTitlesProperty;

        /// <summary>
        /// Optional. Called with a field's title before it's deleted. Return false to cancel.
        /// </summary>
        public Func<string, bool> beforeDeleteField;

        private Foldout foldout;
        private VisualElement headerButtons;
        private Button pasteButton;
        private HelpBox duplicatesHelpBox;
        private VisualElement rows;

        /// <summary>
        /// Invoked after fields are added, removed, moved, or change type. Owners that
        /// show fields elsewhere should rebuild.
        /// </summary>
        public event Action structureChanged;

        /// <summary>
        /// Invoked when the foldout is expanded or collapsed.
        /// </summary>
        public event Action<bool> expandedChanged;

        /// <param name="getTemplateFields">Template fields for the Template button. If null, the button is hidden.</param>
        /// <param name="title">Foldout text.</param>
        /// <param name="primaryFieldTitlesProperty">
        /// Optional List&lt;string&gt; of primary field titles. If set, each row gets a Main
        /// checkbox that adds or removes its title, as in the template editor.
        /// </param>
        public FieldListElement(SerializedProperty fieldsProperty, DialogueDatabase database, Func<List<Field>> getTemplateFields, bool expanded,
            string title = "All Fields", SerializedProperty primaryFieldTitlesProperty = null)
        {
            this.fieldsProperty = fieldsProperty;
            this.database = database;
            this.getTemplateFields = getTemplateFields;
            this.primaryFieldTitlesProperty = primaryFieldTitlesProperty;
            style.marginTop = 6;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            Add(header);

            foldout = new Foldout() { text = title, value = expanded };
            foldout.style.flexGrow = 1;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout) return;
                UpdateExpanded();
                if (expandedChanged != null) expandedChanged(evt.newValue);
            });
            header.Add(foldout);

            headerButtons = new VisualElement();
            headerButtons.style.flexDirection = FlexDirection.Row;
            if (getTemplateFields != null)
            {
                headerButtons.Add(new Button(ApplyTemplate) { text = "Template", tooltip = "Add any missing fields from the template." });
            }
            headerButtons.Add(new Button(CopyFields) { text = "Copy", tooltip = "Copy these fields to the clipboard." });
            pasteButton = new Button(PasteFields) { text = "Paste", tooltip = "Paste the clipboard into these fields." };
            headerButtons.Add(pasteButton);
            header.Add(headerButtons);
            header.Add(new Button(AddField) { text = "+", tooltip = "Add new field." });

            duplicatesHelpBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.Warning);
            Add(duplicatesHelpBox);

            rows = new VisualElement();
            rows.style.marginLeft = 12;
            Add(rows);

            UpdateExpanded();
        }

        public void Rebuild()
        {
            rows.Clear();
            fieldsProperty.serializedObject.Update();
            if (foldout.value)
            {
                rows.Add(CreateHeadingRow());
                var count = fieldsProperty.arraySize;
                for (int i = 0; i < count; i++)
                {
                    rows.Add(CreateFieldRow(i, count));
                }
            }
            UpdateDuplicatesWarning();
        }

        private void UpdateExpanded()
        {
            headerButtons.style.display = foldout.value ? DisplayStyle.Flex : DisplayStyle.None;
            pasteButton.SetEnabled(clipboardFields != null);
            Rebuild();
        }

        private VisualElement CreateHeadingRow()
        {
            // Mirror the field row's columns, with hidden buttons, so headings line up exactly.
            var row = CreateRow();
            row.Add(CreateHeadingLabel("Title"));
            row.Add(CreateHeadingLabel("Value"));
            var typeLabel = CreateHeadingLabel("Type");
            typeLabel.style.flexGrow = 0;
            typeLabel.style.flexShrink = 0;
            typeLabel.style.flexBasis = TypeWidth; // flex-basis overrides width in a row.
            typeLabel.style.width = TypeWidth;
            row.Add(typeLabel);
            if (primaryFieldTitlesProperty != null)
            {
                var mainLabel = CreateHeadingLabel("Main");
                mainLabel.style.flexGrow = 0;
                mainLabel.style.flexShrink = 0;
                mainLabel.style.flexBasis = MainWidth;
                mainLabel.style.width = MainWidth;
                mainLabel.tooltip = MainTooltip;
                row.Add(mainLabel);
            }
            foreach (var text in new string[] { "↑", "↓", "-" })
            {
                var placeholder = CreateRowButton(null, text);
                placeholder.style.visibility = Visibility.Hidden;
                row.Add(placeholder);
            }
            return row;
        }

        private const float TypeWidth = 110;
        private const float MainWidth = 36;
        private const string MainTooltip = "Show in the main editor section, not just in the All Fields foldout. Some built-in fields are always shown in the main section regardless of this checkbox.";
        private const float RowButtonWidth = 22;

        private static Label CreateHeadingLabel(string text)
        {
            var label = new Label(text);
            label.style.flexGrow = 1;
            label.style.flexBasis = 0;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginLeft = 3;
            label.style.marginRight = 3;
            label.style.paddingLeft = 2;
            return label;
        }

        private static Button CreateRowButton(Action clickEvent, string text, string tooltip = null)
        {
            var button = new Button(clickEvent) { text = text, tooltip = tooltip };
            button.style.width = RowButtonWidth;
            button.style.flexShrink = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            return button;
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 1;
            return row;
        }

        private VisualElement CreateFieldRow(int index, int count)
        {
            var fieldProperty = fieldsProperty.GetArrayElementAtIndex(index);
            var titleProperty = fieldProperty.FindPropertyRelative(FieldPropertyUtility.TitlePropertyName);
            var isTextArea = FieldPropertyUtility.IsTextAreaField(titleProperty.stringValue);

            var container = new VisualElement();
            var row = CreateRow();
            container.Add(row);

            var titleField = new TextField();
            titleField.style.flexGrow = 1;
            titleField.style.flexBasis = 0;
            titleField.BindProperty(titleProperty);
            titleField.RegisterValueChangedCallback(evt => UpdateDuplicatesWarning());
            row.Add(titleField);

            VisualElement valueEditor;
            if (isTextArea)
            {
                // Text area fields put the value on its own line below the title, like the IMGUI editor.
                valueEditor = new VisualElement();
                var textArea = FieldPropertyUtility.CreateValueEditor(fieldProperty, database, null, true);
                container.Add(textArea);
            }
            else
            {
                valueEditor = FieldPropertyUtility.CreateValueEditor(fieldProperty, database);
            }
            valueEditor.style.flexGrow = 1;
            valueEditor.style.flexBasis = 0;
            row.Add(valueEditor);

            var typePopup = FieldPropertyUtility.CreateTypePopup(fieldProperty, OnStructureChanged);
            typePopup.style.width = TypeWidth;
            typePopup.style.flexShrink = 0;
            row.Add(typePopup);

            if (primaryFieldTitlesProperty != null)
            {
                var mainToggle = new UnityEngine.UIElements.Toggle() { tooltip = MainTooltip };
                mainToggle.style.width = MainWidth;
                mainToggle.style.flexShrink = 0;
                mainToggle.style.justifyContent = Justify.Center;
                mainToggle.SetValueWithoutNotify(IndexOfPrimaryTitle(titleProperty.stringValue) >= 0);
                mainToggle.RegisterValueChangedCallback(evt => SetPrimary(titleProperty.stringValue, evt.newValue));
                row.Add(mainToggle);
            }

            var upButton = CreateRowButton(() => MoveField(index, index - 1), "↑", "Move up");
            upButton.SetEnabled(index > 0);
            row.Add(upButton);
            var downButton = CreateRowButton(() => MoveField(index, index + 1), "↓", "Move down");
            downButton.SetEnabled(index < count - 1);
            row.Add(downButton);
            row.Add(CreateRowButton(() => DeleteField(index), "-", "Delete field " + titleProperty.stringValue + "."));

            return container;
        }

        private void UpdateDuplicatesWarning()
        {
            var titles = new HashSet<string>();
            var duplicates = new List<string>();
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var title = fieldsProperty.GetArrayElementAtIndex(i).FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
                if (!titles.Add(title) && !duplicates.Contains(title)) duplicates.Add(title);
            }
            duplicatesHelpBox.text = (duplicates.Count > 0) ? "Duplicate Fields: " + string.Join(", ", duplicates) : string.Empty;
            duplicatesHelpBox.style.display = (duplicates.Count > 0) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnStructureChanged()
        {
            // Defer so the rebuild doesn't remove the element whose event is being handled.
            schedule.Execute(() =>
            {
                Rebuild();
                if (structureChanged != null) structureChanged();
            });
        }

        private int IndexOfPrimaryTitle(string title)
        {
            for (int i = 0; i < primaryFieldTitlesProperty.arraySize; i++)
            {
                if (primaryFieldTitlesProperty.GetArrayElementAtIndex(i).stringValue == title) return i;
            }
            return -1;
        }

        private void SetPrimary(string title, bool isPrimary)
        {
            primaryFieldTitlesProperty.serializedObject.Update();
            var index = IndexOfPrimaryTitle(title);
            if (isPrimary && index < 0)
            {
                var newIndex = primaryFieldTitlesProperty.arraySize;
                primaryFieldTitlesProperty.InsertArrayElementAtIndex(newIndex);
                primaryFieldTitlesProperty.GetArrayElementAtIndex(newIndex).stringValue = title;
            }
            else if (!isPrimary && index >= 0)
            {
                primaryFieldTitlesProperty.DeleteArrayElementAtIndex(index);
            }
            primaryFieldTitlesProperty.serializedObject.ApplyModifiedProperties();
        }

        private void AddField()
        {
            fieldsProperty.serializedObject.Update();
            FieldPropertyUtility.AddField(fieldsProperty, string.Empty, string.Empty, FieldType.Text);
            fieldsProperty.serializedObject.ApplyModifiedProperties();
            foldout.value = true;
            OnStructureChanged();
        }

        private void DeleteField(int index)
        {
            fieldsProperty.serializedObject.Update();
            if (!(0 <= index && index < fieldsProperty.arraySize)) return;
            var title = fieldsProperty.GetArrayElementAtIndex(index).FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
            if (string.Equals(title, "Is Item") &&
                !EditorUtility.DisplayDialog("Delete 'Is Item' field?", "'Is Item' is a special built-in field that specifies whether this entry is an item or a quest. Unless you're really sure what you're doing, you shouldn't delete it. Are you sure you want to delete it?", "OK", "Cancel"))
            {
                return;
            }
            if (beforeDeleteField != null && !beforeDeleteField(title)) return;
            fieldsProperty.serializedObject.Update(); // The hook may have changed things.
            if (!(0 <= index && index < fieldsProperty.arraySize)) return;
            fieldsProperty.DeleteArrayElementAtIndex(index);
            // In the template, also untick Main so the field stops showing in the inspectors'
            // main section, unless a duplicate field with the same title is still there.
            if (primaryFieldTitlesProperty != null && FieldPropertyUtility.FindField(fieldsProperty, title) == null)
            {
                var primaryIndex = IndexOfPrimaryTitle(title);
                if (primaryIndex >= 0) primaryFieldTitlesProperty.DeleteArrayElementAtIndex(primaryIndex);
            }
            fieldsProperty.serializedObject.ApplyModifiedProperties();
            OnStructureChanged();
        }

        private void MoveField(int fromIndex, int toIndex)
        {
            fieldsProperty.serializedObject.Update();
            if (!(0 <= toIndex && toIndex < fieldsProperty.arraySize)) return;
            fieldsProperty.MoveArrayElement(fromIndex, toIndex);
            fieldsProperty.serializedObject.ApplyModifiedProperties();
            OnStructureChanged();
        }

        private void ApplyTemplate()
        {
            fieldsProperty.serializedObject.Update();
            FieldPropertyUtility.ApplyTemplate(fieldsProperty, (getTemplateFields != null) ? getTemplateFields() : null);
            OnStructureChanged();
        }

        private void CopyFields()
        {
            fieldsProperty.serializedObject.Update();
            clipboardFields = FieldPropertyUtility.ToFieldList(fieldsProperty);
            pasteButton.SetEnabled(true);
        }

        private void PasteFields()
        {
            if (clipboardFields == null) return;
            fieldsProperty.serializedObject.Update();
            FieldPropertyUtility.PasteFields(fieldsProperty, clipboardFields);
            OnStructureChanged();
        }

    }

}

#endif
