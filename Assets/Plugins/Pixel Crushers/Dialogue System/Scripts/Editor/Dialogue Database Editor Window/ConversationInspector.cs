// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Right-hand pane of the Conversations panel. Shows the conversation's properties when
    /// nothing is selected in the graph, or the selected entry, entries or link. All edits go
    /// through the database's SerializedObject. The Lua wizards and sequence editor are IMGUI,
    /// hosted in IMGUIContainers that write back through SerializedProperty.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public class ConversationInspector : VisualElement
    {

        private static readonly List<string> ConversationBuiltInFieldTitles = new List<string> { "Title", "Description", "Actor", "Conversant" };
        private static readonly List<string> EntryBuiltInFieldTitles = new List<string> { "Title", "Description", "Actor", "Conversant", "Menu Text", "Dialogue Text", "Sequence", "Response Menu Sequence" };
        private static readonly List<string> FalseConditionActions = new List<string> { "Block", "Passthrough" };

        // ConversationOverrideDisplaySettings fields shown in the Subtitle, Sequence, and Input Settings foldouts:
        private static readonly HashSet<string> OverrideSubtitleFieldNames = new HashSet<string>
        {
            "overrideSubtitleSettings", "showNPCSubtitlesDuringLine", "showNPCSubtitlesWithResponses", "showPCSubtitlesDuringLine",
            "skipPCSubtitleAfterResponseMenu", "subtitleCharsPerSecond", "minSubtitleSeconds", "continueButton"
        };
        private static readonly HashSet<string> OverrideSequenceFieldNames = new HashSet<string>
        {
            "overrideSequenceSettings", "defaultSequence", "defaultPlayerSequence", "defaultResponseMenuSequence"
        };
        private static readonly HashSet<string> OverrideInputFieldNames = new HashSet<string>
        {
            "overrideInputSettings", "alwaysForceResponseMenu", "includeInvalidEntries", "responseTimeout",
            "emTagForOldResponses", "emTagForInvalidResponses", "cancelSubtitle", "cancelConversation"
        };

        private readonly ConversationsPanel conversationsPanel;
        private TextField dialogueTextField;
        private TextField groupNameField;
        private Rect sequenceRect;
        private Rect responseMenuSequenceRect;
        private SequenceSyntaxState sequenceSyntaxState = SequenceSyntaxState.Unchecked;
        private SequenceSyntaxState responseMenuSequenceSyntaxState = SequenceSyntaxState.Unchecked;
        // Entry last shown by ShowEntry, to tell a rebuild from a new selection:
        private int shownConversationID = -1;
        private int shownEntryID = -1;
        // True after the user chooses to edit the START entry shown. Reset when the shown entry changes:
        private bool isStartEntryUnlocked = false;

        private SerializedObject serializedObject { get { return conversationsPanel.databaseSerializedObject; } }
        private DialogueDatabase database { get { return conversationsPanel.editedDatabase; } }
        private Template template { get { return conversationsPanel.editorWindow.Template; } }

        public ConversationInspector(ConversationsPanel panel)
        {
            this.conversationsPanel = panel;
        }

        public void ShowNothing()
        {
            OnShownEntryChanged(-1, -1);
            ClearContent();
        }

        private void ClearContent()
        {
            Clear();
            dialogueTextField = null;
            groupNameField = null;
        }

        /// <summary>
        /// Resets per-entry state when the inspector shows a different entry, or no entry.
        /// Rebuilds for the same entry keep it.
        /// </summary>
        private void OnShownEntryChanged(int conversationID, int entryID)
        {
            if (entryID != -1 && conversationID == shownConversationID && entryID == shownEntryID) return;
            shownConversationID = conversationID;
            shownEntryID = entryID;
            isStartEntryUnlocked = false;
            sequenceSyntaxState = SequenceSyntaxState.Unchecked;
            responseMenuSequenceSyntaxState = SequenceSyntaxState.Unchecked;
            LuaWizardPopup.CloseOpenPopup();
        }

        public void FocusDialogueText()
        {
            // The inspector rebuilds after the selection changes, so wait for that.
            schedule.Execute(() => { if (dialogueTextField != null) dialogueTextField.Focus(); }).StartingIn(50);
        }

        public void FocusGroupName()
        {
            schedule.Execute(() => { if (groupNameField != null) groupNameField.Focus(); }).StartingIn(50);
        }

        #region Conversation

        public void ShowConversation(SerializedProperty conversationProperty)
        {
            ShowNothing();
            var fieldsProperty = conversationProperty.FindPropertyRelative("fields");
            AddHeaderWithAIButton("Conversation", aiWindow.CreateAIBranchingConversationButton(ConversationPropertyUtility.GetConversationID(conversationProperty)));

            var idField = new IntegerField("ID") { tooltip = "Internal ID. Change at your own risk." };
            idField.BindProperty(conversationProperty.FindPropertyRelative("id"));
            idField.SetEnabled(false);
            Add(idField);

            var titleProperty = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.Title);
            if (titleProperty != null)
            {
                var titleValue = titleProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                var titleField = FieldPropertyUtility.CreateTextField("Title", titleValue, false);
                titleField.tooltip = "Conversation triggers reference conversations by this. Use forward slashes (/) to organize conversations into groups.";
                titleField.RegisterValueChangedCallback(evt =>
                {
                    // Disallow leading slashes, like the IMGUI editor.
                    var trimmed = evt.newValue.TrimStart('/');
                    if (trimmed != evt.newValue)
                    {
                        titleValue.stringValue = trimmed;
                        serializedObject.ApplyModifiedProperties();
                    }
                    conversationsPanel.OnConversationTitleChanged();
                });
                Add(titleField);
            }

            var descriptionProperty = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.Description);
            if (descriptionProperty != null) AddTextArea("Description", null, descriptionProperty);

            Add(CreateParticipantPopup(conversationProperty, "Actor", "Primary actor, usually the player."));
            Add(CreateParticipantPopup(conversationProperty, "Conversant", "Other actor, usually an NPC."));

            var overridesFoldout = new Foldout() { text = "Override Display Settings", value = false };
            overridesFoldout.style.marginTop = 4;
            AddOverrideSettingsFields(overridesFoldout, conversationProperty.FindPropertyRelative("overrideSettings"));
            Add(overridesFoldout);

            AddPrimaryFields(fieldsProperty, template.conversationPrimaryFieldTitles, ConversationBuiltInFieldTitles);

            var fieldList = new FieldListElement(fieldsProperty, database, () => template.conversationFields, conversationsPanel.conversationsState.fieldsFoldout);
            fieldList.expandedChanged += expanded => conversationsPanel.conversationsState.fieldsFoldout = expanded;
            fieldList.structureChanged += () => conversationsPanel.RequestGraphRebuild();
            Add(fieldList);
        }

        /// <summary>
        /// Adds a field for each ConversationOverrideDisplaySettings value, in declaration order.
        /// The subtitle, sequence, and input values, with their override toggles, go in their own foldouts,
        /// like the IMGUI editor's nested sections. Other values, including any added later,
        /// go directly in the parent.
        /// Like the IMGUI editor, unticking Use Overrides hides everything else, and unticking
        /// a group's override toggle hides the rest of that group's foldout.
        /// </summary>
        private static void AddOverrideSettingsFields(VisualElement parent, SerializedProperty overridesProperty)
        {
            if (overridesProperty == null) return;
            var topLevel = new OverrideGroup(parent, UseOverridesFieldName);
            var subtitleGroup = new OverrideGroup("Subtitle Settings", "overrideSubtitleSettings", OverrideSubtitleFieldNames);
            var sequenceGroup = new OverrideGroup("Sequence Settings", "overrideSequenceSettings", OverrideSequenceFieldNames);
            var inputGroup = new OverrideGroup("Input Settings", "overrideInputSettings", OverrideInputFieldNames);
            var groups = new OverrideGroup[] { subtitleGroup, sequenceGroup, inputGroup };
            var child = overridesProperty.Copy();
            var end = overridesProperty.GetEndProperty();
            if (!child.NextVisible(true)) return;
            while (!SerializedProperty.EqualContents(child, end))
            {
                var group = Array.Find(groups, g => g.fieldNames.Contains(child.name)) ?? topLevel;
                if (group != topLevel && group.container.parent == null)
                {
                    // Add each foldout where its first field appears, to keep declaration order.
                    parent.Add(group.container);
                    topLevel.dependents.Add(group.container);
                }
                var childProperty = child.Copy();
                var field = new PropertyField(childProperty);
                field.BindProperty(childProperty);
                group.container.Add(field);
                if (child.name == group.toggleName)
                {
                    group.toggleProperty = childProperty;
                    group.toggleField = field;
                }
                else
                {
                    group.dependents.Add(field);
                }
                if (!child.NextVisible(false)) break;
            }
            topLevel.TrackToggle();
            foreach (var group in groups)
            {
                group.TrackToggle();
            }
        }

        private const string UseOverridesFieldName = "useOverrides";

        /// <summary>
        /// Override settings fields controlled by one toggle, such as a foldout's
        /// Override Subtitle Settings toggle. The dependents are hidden while it's unticked.
        /// </summary>
        private class OverrideGroup
        {
            public readonly VisualElement container;
            public readonly string toggleName;
            public readonly HashSet<string> fieldNames;
            public readonly List<VisualElement> dependents = new List<VisualElement>();
            public SerializedProperty toggleProperty;
            public PropertyField toggleField;

            /// <summary>Top-level group, whose fields go directly in the parent.</summary>
            public OverrideGroup(VisualElement container, string toggleName)
            {
                this.container = container;
                this.toggleName = toggleName;
                fieldNames = new HashSet<string>();
            }

            /// <summary>Group whose fields go in a foldout.</summary>
            public OverrideGroup(string foldoutText, string toggleName, HashSet<string> fieldNames)
            {
                this.container = CreateOverrideGroupFoldout(foldoutText);
                this.toggleName = toggleName;
                this.fieldNames = fieldNames;
            }

            public void TrackToggle()
            {
                if (toggleProperty == null || toggleProperty.propertyType != SerializedPropertyType.Boolean) return;
                SetDependentsVisible(toggleProperty.boolValue);
                // TrackPropertyValue also catches undo and other changes made outside the toggle.
                toggleField.TrackPropertyValue(toggleProperty, property => SetDependentsVisible(property.boolValue));
            }

            private void SetDependentsVisible(bool visible)
            {
                foreach (var dependent in dependents)
                {
                    dependent.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private static Foldout CreateOverrideGroupFoldout(string text)
        {
            var foldout = new Foldout() { text = text, value = false };
            foldout.style.marginTop = 2;
            foldout.style.marginLeft = 3;
            return foldout;
        }

        /// <summary>
        /// Actor or Conversant popup for the conversation. Changing it also changes entries that
        /// used the old participant, and START's participant, like the IMGUI editor.
        /// </summary>
        private VisualElement CreateParticipantPopup(SerializedProperty conversationProperty, string fieldTitle, string tooltip)
        {
            var fieldsProperty = conversationProperty.FindPropertyRelative("fields");
            var ids = new List<int> { -1 };
            var names = new Dictionary<int, string> { { -1, "(None)" } };
            foreach (var actor in database.actors)
            {
                if (names.ContainsKey(actor.id)) continue;
                ids.Add(actor.id);
                names.Add(actor.id, string.Format("{0} [{1}]", actor.Name, actor.id));
            }
            int currentID;
            if (!int.TryParse(FieldPropertyUtility.LookupValue(fieldsProperty, fieldTitle), out currentID)) currentID = -1;
            if (!names.ContainsKey(currentID))
            {
                ids.Add(currentID);
                names.Add(currentID, string.Format("(Missing) [{0}]", currentID));
            }
            Func<int, string> format = id => names[id];
            // The int argument is an index into ids, not a default value, since T is int.
            var popup = new PopupField<int>(fieldTitle, ids, ids.IndexOf(currentID), format, format) { tooltip = tooltip };
            popup.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var oldID = evt.previousValue.ToString();
                var newID = evt.newValue.ToString();
                FieldPropertyUtility.SetFieldValue(fieldsProperty, fieldTitle, newID, FieldType.Actor, false);
                var entries = ConversationPropertyUtility.GetEntries(conversationProperty);
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var entryFields = entry.FindPropertyRelative("fields");
                    var isStart = ConversationPropertyUtility.GetEntryID(entry) == ConversationPropertyUtility.StartEntryID;
                    foreach (var participant in new string[] { "Actor", "Conversant" })
                    {
                        var participantProperty = FieldPropertyUtility.FindField(entryFields, participant);
                        if (participantProperty == null) continue;
                        var valueProperty = participantProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                        if (valueProperty.stringValue == oldID || (isStart && participant == fieldTitle)) valueProperty.stringValue = newID;
                    }
                }
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Change Conversation " + fieldTitle);
            });
            return popup;
        }

        #endregion

        #region Entry

        public void ShowEntry(SerializedProperty conversationProperty, SerializedProperty entryProperty)
        {
            var fieldsProperty = entryProperty.FindPropertyRelative("fields");
            var entryID = ConversationPropertyUtility.GetEntryID(entryProperty);
            var conversationID = ConversationPropertyUtility.GetConversationID(conversationProperty);
            var isStart = entryID == ConversationPropertyUtility.StartEntryID;
            var entryObject = conversationsPanel.GetEntryObject(entryID);
            OnShownEntryChanged(conversationID, entryID);
            ClearContent();

            Add(CreateHeader(isStart ? "START Entry" : "Dialogue Entry"));
            var firstFieldIndex = childCount;

            var idField = new IntegerField("ID") { tooltip = "Internal ID. Change at your own risk." };
            idField.BindProperty(entryProperty.FindPropertyRelative("id"));
            idField.SetEnabled(false);
            Add(idField);

            Add(CreateTextEditor(fieldsProperty, DialogueSystemFields.Title, "Title", "Optional title for your reference.", false));
            var descriptionProperty = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.Description);
            if (descriptionProperty != null) AddTextArea("Description", null, descriptionProperty);

            AddParticipants(fieldsProperty);

            // Group entries only organize their children, so they don't show text or sequences.
            var isGroupProperty = entryProperty.FindPropertyRelative("isGroup");
            var nonGroupFields = new VisualElement();
            var groupToggle = new UnityEngine.UIElements.Toggle("Group") { tooltip = "Tick to make this a group node, which only organizes its children." };
            groupToggle.RegisterValueChangedCallback(evt => nonGroupFields.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex);
            groupToggle.BindProperty(isGroupProperty);
            nonGroupFields.style.display = isGroupProperty.boolValue ? DisplayStyle.None : DisplayStyle.Flex;
            Add(groupToggle);
            Add(nonGroupFields);

            var state = conversationsPanel.conversationsState;
            var menuText = CreateTextEditor(fieldsProperty, "Menu Text", "Menu Text", "Text shown in the response menu. If blank, Dialogue Text is used.", false);
            nonGroupFields.Add(DialogueDatabaseEditorWindow.WithAIButton(menuText,
                aiWindow.CreateAIReviseTextButton(AIFieldTarget.ForEntry(conversationID, entryID, "Menu Text"))));
            AddLocalizedFieldsFoldout(nonGroupFields, fieldsProperty, conversationID, entryID, LocalizedFieldKind.MenuText, "Localized Menu Text", false,
                state.localizedMenuTextFoldout, expanded => state.localizedMenuTextFoldout = expanded);
            var dialogueText = CreateTextEditor(fieldsProperty, "Dialogue Text", "Dialogue Text", "Text shown as a subtitle.", true);
            dialogueTextField = dialogueText.Q<TextField>();
            DialogueDatabaseEditorWindow.AddAIButtonToLabelRow(dialogueText,
                aiWindow.CreateAIReviseTextButton(AIFieldTarget.ForEntry(conversationID, entryID, "Dialogue Text")));
            nonGroupFields.Add(dialogueText);
            AddLocalizedFieldsFoldout(nonGroupFields, fieldsProperty, conversationID, entryID, LocalizedFieldKind.DialogueText, "Localized Dialogue Text", true,
                state.localizedDialogueTextFoldout, expanded => state.localizedDialogueTextFoldout = expanded);

            var sequenceTarget = AIFieldTarget.ForEntry(conversationID, entryID, "Sequence");
            nonGroupFields.Add(CreateImguiFieldEditor(fieldsProperty, "Sequence", value =>
                SequenceEditorTools.DrawLayout(new GUIContent("Sequence", "Cutscene commands to play with this entry."), value, ref sequenceRect, ref sequenceSyntaxState, entryObject, null,
                false, (entry, field) => aiWindow.DrawAISequenceButton(sequenceTarget))));
            AddLocalizedFieldsFoldout(nonGroupFields, fieldsProperty, conversationID, entryID, LocalizedFieldKind.Sequence, "Localized Sequence", true,
                state.localizedSequenceFoldout, expanded => state.localizedSequenceFoldout = expanded);

            AddResponseMenuSequence(nonGroupFields, fieldsProperty, conversationID, entryID, entryObject);

            Add(CreateLuaEditor(entryProperty, conversationID, entryID, "conditionsString", true,
                "Conditions", "Optional Lua expression that must be true to use this entry."));
            Add(CreateFalseConditionActionPopup(entryProperty));

            Add(CreateLuaEditor(entryProperty, conversationID, entryID, "userScript", false,
                "Script", "Optional Lua code to run when this entry is used."));

            Add(new EntryEventsElement(conversationsPanel, entryProperty, conversationID, entryID));

            AddPrimaryFields(fieldsProperty, template.dialogueEntryPrimaryFieldTitles, EntryBuiltInFieldTitles);
            var linksStartIndex = childCount;
            AddLinks(entryProperty, conversationID, entryID);
            var linksEndIndex = childCount;

            var fieldList = new FieldListElement(fieldsProperty, database, () => template.dialogueEntryFields, conversationsPanel.conversationsState.entryFieldsFoldout);
            fieldList.expandedChanged += expanded => conversationsPanel.conversationsState.entryFieldsFoldout = expanded;
            fieldList.structureChanged += () => conversationsPanel.RequestGraphRebuild();
            Add(fieldList);

            if (isStart) LockStartEntryFields(firstFieldIndex, linksStartIndex, linksEndIndex);
        }

        /// <summary>
        /// Makes the START entry's fields, from firstFieldIndex on, read-only until the user
        /// clicks the "Edit anyway" button below a warning. The Links section, from
        /// linksStartIndex up to linksEndIndex, stays usable so the user can still go to
        /// START's children. The unlock lasts until the inspector shows a different entry.
        /// </summary>
        private void LockStartEntryFields(int firstFieldIndex, int linksStartIndex, int linksEndIndex)
        {
            var lockedElements = new List<VisualElement>();
            for (int i = firstFieldIndex; i < childCount; i++)
            {
                if (linksStartIndex <= i && i < linksEndIndex) continue;
                var element = ElementAt(i);
                element.SetEnabled(isStartEntryUnlocked);
                lockedElements.Add(element);
            }
            var warning = new HelpBox("This is the START entry. In most cases, you should leave this entry alone and begin your conversation with its child entries.",
                UnityEngine.UIElements.HelpBoxMessageType.Warning);
            Button editButton = null;
            editButton = new Button(() =>
            {
                isStartEntryUnlocked = true;
                foreach (var element in lockedElements)
                {
                    element.SetEnabled(true);
                }
                editButton.style.display = DisplayStyle.None;
            }) { text = "I understand. Edit anyway.", tooltip = "Make the START entry's fields editable." };
            editButton.style.alignSelf = Align.FlexStart;
            editButton.style.marginTop = 2;
            editButton.style.marginBottom = 4;
            editButton.style.display = isStartEntryUnlocked ? DisplayStyle.None : DisplayStyle.Flex;
            Insert(firstFieldIndex, warning);
            Insert(firstFieldIndex + 1, editButton);
        }

        private void AddParticipants(SerializedProperty fieldsProperty)
        {
            var actorProperty = FieldPropertyUtility.FindField(fieldsProperty, "Actor");
            var conversantProperty = FieldPropertyUtility.FindField(fieldsProperty, "Conversant");
            if (actorProperty != null) Add(FieldPropertyUtility.CreateValueEditor(actorProperty, database, "Actor"));
            if (conversantProperty != null) Add(FieldPropertyUtility.CreateValueEditor(conversantProperty, database, "Conversant"));
            if (actorProperty == null || conversantProperty == null) return;
            var swapButton = new Button(() =>
            {
                serializedObject.Update();
                var actorValue = actorProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                var conversantValue = conversantProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                var temp = actorValue.stringValue;
                actorValue.stringValue = conversantValue.stringValue;
                conversantValue.stringValue = temp;
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Swap Participants");
            }) { text = "Swap Actor and Conversant", tooltip = "Swap the speaker and listener." };
            swapButton.style.alignSelf = Align.FlexEnd;
            Add(swapButton);
        }

        private enum LocalizedFieldKind { MenuText, DialogueText, Sequence, ResponseMenuSequence }

        private const string LocalizedMenuTextPrefix = "Menu Text ";
        private const string LocalizedSequencePrefix = "Sequence ";
        private const string LocalizedResponseMenuSequencePrefix = "Response Menu Sequence ";

        /// <summary>
        /// Localization fields titled "Menu Text xx" localize Menu Text, "Sequence xx" localize
        /// Sequence, "Response Menu Sequence xx" localize Response Menu Sequence, and all others
        /// localize Dialogue Text.
        /// </summary>
        private static LocalizedFieldKind GetLocalizedFieldKind(string title)
        {
            if (title.StartsWith(LocalizedMenuTextPrefix) && title.Length > LocalizedMenuTextPrefix.Length) return LocalizedFieldKind.MenuText;
            if (title.StartsWith(LocalizedSequencePrefix) && title.Length > LocalizedSequencePrefix.Length) return LocalizedFieldKind.Sequence;
            if (title.StartsWith(LocalizedResponseMenuSequencePrefix) && title.Length > LocalizedResponseMenuSequencePrefix.Length) return LocalizedFieldKind.ResponseMenuSequence;
            return LocalizedFieldKind.DialogueText;
        }

        private static string GetLanguageCode(string title, LocalizedFieldKind kind)
        {
            switch (kind)
            {
                case LocalizedFieldKind.MenuText: return title.Substring(LocalizedMenuTextPrefix.Length);
                case LocalizedFieldKind.Sequence: return title.Substring(LocalizedSequencePrefix.Length);
                case LocalizedFieldKind.ResponseMenuSequence: return title.Substring(LocalizedResponseMenuSequencePrefix.Length);
                default: return title;
            }
        }

        /// <summary>
        /// Adds a foldout with the entry's localized versions of a primary field, if it has any.
        /// Each field is labeled with its language code.
        /// </summary>
        private void AddLocalizedFieldsFoldout(VisualElement parent, SerializedProperty fieldsProperty, int conversationID, int entryID,
            LocalizedFieldKind kind, string foldoutText, bool multiline, bool expanded, Action<bool> expandedChanged)
        {
            Foldout foldout = null;
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var fieldProperty = fieldsProperty.GetArrayElementAtIndex(i);
                var title = fieldProperty.FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
                if (string.IsNullOrEmpty(title) || FieldPropertyUtility.GetEffectiveTypeString(fieldProperty) != "CustomFieldType_Localization") continue;
                if (EntryBuiltInFieldTitles.Contains(title)) continue; // Primary fields such as Dialogue Text can also have Localization type.
                if (GetLocalizedFieldKind(title) != kind) continue;
                if (foldout == null)
                {
                    foldout = new Foldout() { text = foldoutText, value = expanded };
                    foldout.style.marginTop = 2;
                    foldout.RegisterValueChangedCallback(evt =>
                    {
                        if (evt.target == foldout) expandedChanged(evt.newValue);
                    });
                    parent.Add(foldout);
                }
                var textField = FieldPropertyUtility.CreateTextField(GetLanguageCode(title, kind), fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), multiline);
                textField.tooltip = title;
                var target = AIFieldTarget.ForEntry(conversationID, entryID, title);
                var isSequence = kind == LocalizedFieldKind.Sequence || kind == LocalizedFieldKind.ResponseMenuSequence;
                var aiButton = isSequence ? aiWindow.CreateAISequenceButton(target) : aiWindow.CreateAILocalizeTextButton(target);
                foldout.Add(DialogueDatabaseEditorWindow.WithAIButton(textField, aiButton));
            }
        }

        private void AddResponseMenuSequence(VisualElement parent, SerializedProperty fieldsProperty, int conversationID, int entryID, DialogueEntry entryObject)
        {
            const string FieldTitle = "Response Menu Sequence";
            var hasField = FieldPropertyUtility.FindField(fieldsProperty, FieldTitle) != null;
            var toggle = new UnityEngine.UIElements.Toggle("Response Menu Sequence") { value = hasField, tooltip = "Tick to play a sequence while the response menu is shown after this entry." };
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                if (evt.newValue) FieldPropertyUtility.SetFieldValue(fieldsProperty, FieldTitle, string.Empty, FieldType.Text);
                else FieldPropertyUtility.RemoveField(fieldsProperty, FieldTitle);
                Undo.SetCurrentGroupName(evt.newValue ? "Add Response Menu Sequence" : "Remove Response Menu Sequence");
                conversationsPanel.RequestGraphRebuild(); // Rebuilds this inspector too.
            });
            parent.Add(toggle);
            if (hasField)
            {
                var sequenceTarget = AIFieldTarget.ForEntry(conversationID, entryID, FieldTitle);
                parent.Add(CreateImguiFieldEditor(fieldsProperty, FieldTitle, value =>
                    SequenceEditorTools.DrawLayout(new GUIContent(" ", "Sequence to play while the response menu is shown."), value, ref responseMenuSequenceRect, ref responseMenuSequenceSyntaxState, entryObject, null,
                    false, (entry, field) => aiWindow.DrawAISequenceButton(sequenceTarget))));
            }
            var state = conversationsPanel.conversationsState;
            AddLocalizedFieldsFoldout(parent, fieldsProperty, conversationID, entryID, LocalizedFieldKind.ResponseMenuSequence, "Localized Response Menu Sequence", true,
                state.localizedResponseMenuSequenceFoldout, expanded => state.localizedResponseMenuSequenceFoldout = expanded);
        }

        private VisualElement CreateFalseConditionActionPopup(SerializedProperty entryProperty)
        {
            var property = entryProperty.FindPropertyRelative("falseConditionAction");
            var current = FalseConditionActions.Contains(property.stringValue) ? property.stringValue : FalseConditionActions[0];
            var popup = new PopupField<string>("False Condition Action", new List<string>(FalseConditionActions), current)
            {
                tooltip = "What to do if Conditions are false: Block stops here; Passthrough skips to this entry's children."
            };
            popup.RegisterValueChangedCallback(evt =>
            {
                property.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            popup.TrackPropertyValue(property, p => popup.SetValueWithoutNotify(FalseConditionActions.Contains(p.stringValue) ? p.stringValue : FalseConditionActions[0]));
            return popup;
        }

        #endregion

        #region Links

        private void AddLinks(SerializedProperty entryProperty, int conversationID, int entryID)
        {
            var linksProperty = entryProperty.FindPropertyRelative(ConversationPropertyUtility.LinksPropertyName);
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 8;
            var label = CreateHeader("Links To");
            label.style.flexGrow = 1;
            label.style.marginTop = 0;
            header.Add(label);
            var addButton = new Button() { text = "Add Link", tooltip = "Link to a new entry, an existing entry, or another conversation." };
            var isShiftDown = false; // A button's clicked callback doesn't report modifier keys.
            addButton.RegisterCallback<PointerDownEvent>(evt => isShiftDown = evt.shiftKey, TrickleDown.TrickleDown);
            addButton.clicked += () => ShowAddLinkMenu(addButton, entryProperty, conversationID, entryID, isShiftDown);
            header.Add(addButton);
            Add(header);

            if (linksProperty.arraySize == 0)
            {
                var none = new Label("No links. The conversation ends after this entry.");
                none.style.opacity = 0.6f;
                none.style.marginLeft = 3;
                Add(none);
            }
            for (int i = 0; i < linksProperty.arraySize; i++)
            {
                Add(CreateLinkRow(linksProperty, i, conversationID));
            }
        }

        private VisualElement CreateLinkRow(SerializedProperty linksProperty, int index, int conversationID)
        {
            var linkProperty = linksProperty.GetArrayElementAtIndex(index);
            var destinationConversationProperty = linkProperty.FindPropertyRelative("destinationConversationID");
            var destinationEntryProperty = linkProperty.FindPropertyRelative("destinationDialogueID");
            var container = new VisualElement();
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 2;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.AddToClassList("ds-indent-rule");

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            container.Add(row);

            var destinationConversationID = destinationConversationProperty.intValue;
            var destinationEntryID = destinationEntryProperty.intValue;
            if (destinationConversationID == conversationID)
            {
                var destination = conversationsPanel.GetEntryObject(destinationEntryID);
                var text = (destination != null)
                    ? string.Format("[{0}] {1}", destinationEntryID, ConversationsPanel.GetEntryText(destination))
                    : string.Format("[{0}] (missing entry)", destinationEntryID);
                var goButton = new Button(() => conversationsPanel.SelectEntry(destinationEntryID, true)) { text = text, tooltip = "Select this entry in the graph." };
                goButton.style.flexGrow = 1;
                goButton.style.flexShrink = 1;
                goButton.style.unityTextAlign = TextAnchor.MiddleLeft;
                goButton.style.overflow = Overflow.Hidden;
                goButton.style.textOverflow = TextOverflow.Ellipsis;
                row.Add(goButton);
            }
            else
            {
                var otherLabel = new Label("Another conversation:");
                otherLabel.style.flexGrow = 1;
                row.Add(otherLabel);
            }

            var upButton = CreateSmallButton("↑", "Move up", () => MoveLink(linksProperty, index, index - 1));
            upButton.SetEnabled(index > 0);
            row.Add(upButton);
            var downButton = CreateSmallButton("↓", "Move down", () => MoveLink(linksProperty, index, index + 1));
            downButton.SetEnabled(index < linksProperty.arraySize - 1);
            row.Add(downButton);
            row.Add(CreateSmallButton("-", "Delete link", () =>
            {
                serializedObject.Update();
                linksProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Delete Link");
                conversationsPanel.RequestGraphRebuild();
            }));

            if (destinationConversationID != conversationID)
            {
                AddOtherConversationPopups(container, destinationConversationProperty, destinationEntryProperty);
            }

            var priorityField = new EnumField("Priority", ConditionPriority.Normal) { tooltip = "Higher-priority links are evaluated first." };
            priorityField.BindProperty(linkProperty.FindPropertyRelative("priority"));
            if (destinationConversationID != conversationID)
            {
                // Priority shares its line with a Go button that follows the link.
                priorityField.style.flexGrow = 1;
                priorityField.style.flexShrink = 1;
                var priorityRow = new VisualElement();
                priorityRow.style.flexDirection = FlexDirection.Row;
                priorityRow.style.alignItems = Align.Center;
                priorityRow.Add(priorityField);
                var goButton = new Button(() =>
                {
                    // Read the destination now; changing the Entry popup doesn't rebuild this row.
                    serializedObject.Update();
                    conversationsPanel.SelectAndFrameEntry(destinationConversationProperty.intValue, destinationEntryProperty.intValue);
                })
                { text = "Go", tooltip = "Show the destination conversation and select this entry." };
                goButton.style.marginLeft = 4;
                goButton.style.flexShrink = 0;
                goButton.SetEnabled(LinkDestinationExists(destinationConversationProperty.intValue, destinationEntryProperty.intValue));
                goButton.TrackPropertyValue(destinationEntryProperty, p =>
                    goButton.SetEnabled(LinkDestinationExists(destinationConversationProperty.intValue, p.intValue)));
                priorityRow.Add(goButton);
                container.Add(priorityRow);
            }
            else
            {
                container.Add(priorityField);
            }
            return container;
        }

        private bool LinkDestinationExists(int destinationConversationID, int destinationEntryID)
        {
            if (database == null || destinationConversationID < 0) return false;
            var destinationConversation = database.conversations.Find(c => c != null && c.id == destinationConversationID);
            return destinationConversation != null && destinationConversation.dialogueEntries.Exists(e => e != null && e.id == destinationEntryID);
        }

        /// <summary>Conversation and entry popups for a link to another conversation.</summary>
        private void AddOtherConversationPopups(VisualElement container, SerializedProperty destinationConversationProperty, SerializedProperty destinationEntryProperty)
        {
            var conversationIDs = new List<int> { -1 };
            var conversationNames = new Dictionary<int, string> { { -1, "(Select Conversation)" } };
            foreach (var conversation in database.conversations)
            {
                if (conversationNames.ContainsKey(conversation.id)) continue;
                conversationIDs.Add(conversation.id);
                conversationNames.Add(conversation.id, string.Format("{0} [{1}]", conversation.Title, conversation.id));
            }
            var currentConversationID = destinationConversationProperty.intValue;
            if (!conversationNames.ContainsKey(currentConversationID)) currentConversationID = -1;
            Func<int, string> formatConversation = id => conversationNames[id];
            var conversationPopup = new PopupField<int>("Conversation", conversationIDs, conversationIDs.IndexOf(currentConversationID), formatConversation, formatConversation);
            conversationPopup.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                destinationConversationProperty.intValue = evt.newValue;
                destinationEntryProperty.intValue = (evt.newValue >= 0) ? ConversationPropertyUtility.StartEntryID : -1;
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Change Link Destination");
                conversationsPanel.RequestGraphRebuild();
            });
            container.Add(conversationPopup);

            var destinationConversation = database.conversations.Find(c => c.id == currentConversationID);
            if (destinationConversation == null) return;
            var entryIDs = new List<int>();
            var entryNames = new Dictionary<int, string>();
            foreach (var entry in destinationConversation.dialogueEntries)
            {
                if (entryNames.ContainsKey(entry.id)) continue;
                entryIDs.Add(entry.id);
                entryNames.Add(entry.id, string.Format("[{0}] {1}", entry.id, ConversationsPanel.GetEntryText(entry)));
            }
            var currentEntryID = destinationEntryProperty.intValue;
            if (!entryNames.ContainsKey(currentEntryID))
            {
                entryIDs.Insert(0, currentEntryID);
                entryNames.Add(currentEntryID, string.Format("[{0}] (missing entry)", currentEntryID));
            }
            Func<int, string> formatEntry = id => entryNames[id];
            var entryPopup = new PopupField<int>("Entry", entryIDs, entryIDs.IndexOf(currentEntryID), formatEntry, formatEntry);
            entryPopup.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                destinationEntryProperty.intValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Change Link Destination");
            });
            container.Add(entryPopup);
        }

        private void MoveLink(SerializedProperty linksProperty, int fromIndex, int toIndex)
        {
            serializedObject.Update();
            if (!(0 <= toIndex && toIndex < linksProperty.arraySize)) return;
            linksProperty.MoveArrayElement(fromIndex, toIndex);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Reorder Links");
            conversationsPanel.RequestGraphRebuild();
        }

        private void ShowAddLinkMenu(Button button, SerializedProperty entryProperty, int conversationID, int entryID, bool useSameActors)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("(New Entry)"), false, () => conversationsPanel.CreateChildEntry(entryID, useSameActors));
            menu.AddItem(new GUIContent("(Another Conversation)"), false, () => AddLink(entryProperty, -1, -1));
            menu.AddSeparator(string.Empty);
            var conversation = conversationsPanel.GetSelectedConversation();
            if (conversation != null)
            {
                foreach (var entry in conversation.dialogueEntries)
                {
                    if (entry.id == entryID) continue;
                    var text = ConversationsPanel.GetEntryText(entry);
                    if (text.Length > 40) text = text.Substring(0, 40) + "...";
                    var destinationID = entry.id;
                    // Menu paths treat "/" as a submenu separator, so replace it.
                    menu.AddItem(new GUIContent(string.Format("[{0}] {1}", entry.id, text.Replace('/', '∕'))), false,
                        () => AddLink(entryProperty, conversationID, destinationID));
                }
            }
            menu.DropDown(button.worldBound);
        }

        private void AddLink(SerializedProperty entryProperty, int destinationConversationID, int destinationEntryID)
        {
            serializedObject.Update();
            if (ConversationPropertyUtility.AddLink(entryProperty, destinationConversationID, destinationEntryID) == null) return;
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Link");
            conversationsPanel.RequestGraphRebuild();
        }

        #endregion

        #region Link and Multiple Selection

        public void ShowLink(SerializedProperty originEntryProperty, int linkIndex)
        {
            ShowNothing();
            var linkProperty = originEntryProperty.FindPropertyRelative(ConversationPropertyUtility.LinksPropertyName).GetArrayElementAtIndex(linkIndex);
            Add(CreateHeader("Link"));
            var originID = ConversationPropertyUtility.GetEntryID(originEntryProperty);
            var destinationID = linkProperty.FindPropertyRelative("destinationDialogueID").intValue;
            Add(new Label(string.Format("From [{0}] {1}", originID, ConversationsPanel.GetEntryText(conversationsPanel.GetEntryObject(originID)))));
            Add(new Label(string.Format("To [{0}] {1}", destinationID, ConversationsPanel.GetEntryText(conversationsPanel.GetEntryObject(destinationID)))));
            var priorityField = new EnumField("Priority", ConditionPriority.Normal) { tooltip = "Higher-priority links are evaluated first." };
            priorityField.BindProperty(linkProperty.FindPropertyRelative("priority"));
            priorityField.style.marginTop = 4;
            Add(priorityField);
            var deleteButton = new Button(() => conversationsPanel.DeleteGraphSelection()) { text = "Delete Link" };
            deleteButton.style.marginTop = 6;
            Add(deleteButton);
        }

        /// <summary>Shows a node group's name and color, like the IMGUI editor's DrawEntryGroupContents.</summary>
        public void ShowGroup(SerializedProperty groupProperty)
        {
            ShowNothing();
            Add(CreateHeader("Group"));
            groupNameField = FieldPropertyUtility.CreateTextField("Name", groupProperty.FindPropertyRelative("name"), false);
            Add(groupNameField);
            var colorField = new ColorField("Color");
            colorField.BindProperty(groupProperty.FindPropertyRelative("color"));
            Add(colorField);
            AddDeleteSelectionButton();
        }

        public void ShowMultiple(int entryCount, int linkCount)
        {
            ShowNothing();
            Add(CreateHeader("Selection"));
            AddSelectionCount(entryCount, linkCount);
            AddDeleteSelectionButton();
        }

        private void AddSelectionCount(int entryCount, int linkCount)
        {
            var parts = new List<string>();
            if (entryCount > 0) parts.Add(string.Format("{0} {1}", entryCount, entryCount == 1 ? "entry" : "entries"));
            if (linkCount > 0) parts.Add(string.Format("{0} {1}", linkCount, linkCount == 1 ? "link" : "links"));
            Add(new Label(string.Join(" and ", parts) + " selected."));
        }

        private void AddDeleteSelectionButton()
        {
            var deleteButton = new Button(() => conversationsPanel.DeleteGraphSelection()) { text = "Delete", tooltip = "Delete the selection. START is never deleted." };
            deleteButton.style.marginTop = 6;
            Add(deleteButton);
        }

        #endregion

        #region Multiple Entries

        // Entries shown by ShowEntries. Edits find them by ID, like SetEntryString.
        private int multipleEntriesConversationID = -1;
        private List<int> multipleEntryIDs = new List<int>();

        /// <summary>
        /// Shows a restricted set of fields for several entries, like the IMGUI editor's
        /// DrawMultinodeSelectionInspector. A change to a field applies to all of the entries.
        /// Popups and the Sequence field show MixedValue if the entries' values differ.
        /// </summary>
        public void ShowEntries(int conversationID, List<int> entryIDs, int linkCount)
        {
            ShowNothing();
            var entries = entryIDs.Select(id => conversationsPanel.GetEntryObject(id)).Where(entry => entry != null).ToList();
            if (entries.Count == 0) return;
            multipleEntriesConversationID = conversationID;
            multipleEntryIDs = entries.Select(entry => entry.id).ToList();
            var firstEntry = entries[0];
            var shownTitles = new HashSet<string>();

            Add(CreateHeader("Multiple Entries"));
            AddSelectionCount(entries.Count, linkCount);

            var idField = new TextField("ID") { value = FieldPropertyUtility.MixedValue, tooltip = "Internal ID. Change at your own risk." };
            idField.SetEnabled(false);
            Add(idField);

            Add(CreateEntriesFieldEditor(entries, new Field(DialogueSystemFields.Title, string.Empty, FieldType.Text), false, shownTitles));
            Add(CreateEntriesFieldEditor(entries, new Field("Actor", string.Empty, FieldType.Actor), false, shownTitles));
            Add(CreateEntriesFieldEditor(entries, new Field("Conversant", string.Empty, FieldType.Actor), false, shownTitles));
            var swapButton = new Button(SwapEntriesParticipants) { text = "Swap Actor and Conversant", tooltip = "Swap the speaker and listener of each entry." };
            swapButton.style.alignSelf = Align.FlexEnd;
            Add(swapButton);

            // Like the IMGUI editor, the first entry determines which of these fields to show.
            if (!firstEntry.isGroup)
            {
                Add(CreateEntriesSequenceEditor(entries, shownTitles));
                if (firstEntry.HasResponseMenuSequence())
                {
                    Add(CreateEntriesFieldEditor(entries, new Field("Response Menu Sequence", string.Empty, FieldType.Text), true, shownTitles));
                }
            }

            Add(CreateEntriesFalseConditionActionPopup(entries));
            Add(CreateEntriesLuaEditor(entries, "conditionsString", entry => entry.conditionsString, true,
                "Conditions", "Optional Lua expression that must be true to use these entries."));
            Add(CreateEntriesLuaEditor(entries, "userScript", entry => entry.userScript, false,
                "Script", "Optional Lua code to run when these entries are used."));

            AddEntriesOtherFields(entries, firstEntry, shownTitles);
            AddDeleteSelectionButton();
        }

        private static string GetCommonFieldValue(List<DialogueEntry> entries, string title)
        {
            var value = Field.LookupValue(entries[0].fields, title) ?? string.Empty;
            return entries.All(entry => (Field.LookupValue(entry.fields, title) ?? string.Empty) == value) ? value : FieldPropertyUtility.MixedValue;
        }

        private VisualElement CreateEntriesFieldEditor(List<DialogueEntry> entries, Field field, bool multiline, HashSet<string> shownTitles)
        {
            shownTitles.Add(field.title);
            return FieldPropertyUtility.CreateUnboundValueEditor(field, GetCommonFieldValue(entries, field.title), database, field.title, multiline,
                value => SetEntriesField(field, value));
        }

        private VisualElement CreateEntriesSequenceEditor(List<DialogueEntry> entries, HashSet<string> shownTitles)
        {
            var field = new Field(DialogueSystemFields.Sequence, string.Empty, FieldType.Text);
            shownTitles.Add(field.title);
            var value = GetCommonFieldValue(entries, field.title);
            var container = new IMGUIContainer(() =>
            {
                var newValue = SequenceEditorTools.DrawLayout(new GUIContent("Sequence", "Cutscene commands to play with these entries."),
                    value, ref sequenceRect, ref sequenceSyntaxState, null, null, true, (entryParam, fieldParam) => aiWindow.DrawAISequenceButton(null));
                if (newValue == value) return;
                value = newValue;
                SetEntriesField(field, newValue);
            });
            container.style.marginTop = 4;
            return container;
        }

        private VisualElement CreateEntriesFalseConditionActionPopup(List<DialogueEntry> entries)
        {
            Func<DialogueEntry, string> getAction = entry => FalseConditionActions.Contains(entry.falseConditionAction) ? entry.falseConditionAction : FalseConditionActions[0];
            var current = getAction(entries[0]);
            var choices = new List<string>(FalseConditionActions);
            if (entries.Any(entry => getAction(entry) != current))
            {
                current = FieldPropertyUtility.MixedValue;
                choices.Insert(0, current);
            }
            var popup = new PopupField<string>("False Condition Action", choices, current)
            {
                tooltip = "What to do if Conditions are false: Block stops here; Passthrough skips to these entries' children."
            };
            popup.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue == FieldPropertyUtility.MixedValue) return;
                ModifyEntries("Change False Condition Action", entryProperty => entryProperty.FindPropertyRelative("falseConditionAction").stringValue = evt.newValue);
            });
            return popup;
        }

        /// <summary>
        /// Lua editor that's blank if the entries' values differ, like the IMGUI editor, since
        /// MixedValue isn't valid Lua.
        /// </summary>
        private VisualElement CreateEntriesLuaEditor(List<DialogueEntry> entries, string propertyName, Func<DialogueEntry, string> getValue,
            bool isConditions, string label, string tooltip)
        {
            var firstValue = getValue(entries[0]);
            var textField = FieldPropertyUtility.CreateTextField(null, true);
            textField.SetValueWithoutNotify(entries.All(entry => getValue(entry) == firstValue) ? firstValue : string.Empty);
            textField.RegisterValueChangedCallback(evt =>
                ModifyEntries("Change " + label, entryProperty => entryProperty.FindPropertyRelative(propertyName).stringValue = evt.newValue));
            return CreateLuaEditor(textField, isConditions, label, tooltip, luaCode => textField.value = luaCode);
        }

        /// <summary>
        /// Foldout with the first entry's other fields. Fields shown above are left out, since
        /// unbound editors of the same field wouldn't update each other.
        /// </summary>
        private void AddEntriesOtherFields(List<DialogueEntry> entries, DialogueEntry firstEntry, HashSet<string> shownTitles)
        {
            var state = conversationsPanel.conversationsState;
            var foldout = new Foldout() { text = "Other Fields", value = state.entryFieldsFoldout };
            foldout.style.marginTop = 4;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) state.entryFieldsFoldout = evt.newValue;
            });
            foreach (var field in firstEntry.fields)
            {
                if (string.IsNullOrEmpty(field.title) || shownTitles.Contains(field.title)) continue;
                foldout.Add(CreateEntriesFieldEditor(entries, field, FieldPropertyUtility.IsTextAreaField(field.title), shownTitles));
            }
            Add(foldout);
        }

        private void SetEntriesField(Field field, string value)
        {
            ModifyEntries("Change Entries", entryProperty =>
                FieldPropertyUtility.SetFieldValue(entryProperty.FindPropertyRelative("fields"), field.title, value, field.type, false));
        }

        private void SwapEntriesParticipants()
        {
            ModifyEntries("Swap Participants", entryProperty =>
            {
                var fieldsProperty = entryProperty.FindPropertyRelative("fields");
                var actorProperty = FieldPropertyUtility.FindField(fieldsProperty, "Actor");
                var conversantProperty = FieldPropertyUtility.FindField(fieldsProperty, "Conversant");
                if (actorProperty == null || conversantProperty == null) return;
                var actorValue = actorProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                var conversantValue = conversantProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName);
                var temp = actorValue.stringValue;
                actorValue.stringValue = conversantValue.stringValue;
                conversantValue.stringValue = temp;
            });
            conversationsPanel.RequestGraphRebuild(); // Rebuilds this inspector too.
        }

        /// <summary>Applies a change to each of the entries shown by ShowEntries as one undo step.</summary>
        private void ModifyEntries(string undoName, Action<SerializedProperty> modify)
        {
            if (serializedObject == null || serializedObject.targetObject == null) return;
            serializedObject.Update();
            var conversationProperty = conversationsPanel.GetSelectedConversationProperty();
            if (conversationProperty == null || ConversationPropertyUtility.GetConversationID(conversationProperty) != multipleEntriesConversationID) return;
            foreach (var entryID in multipleEntryIDs)
            {
                var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, entryID);
                if (entryProperty != null) modify(entryProperty);
            }
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(undoName);
        }

        #endregion

        #region Helpers

        private static Label CreateHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 13;
            label.style.marginTop = 2;
            label.style.marginBottom = 4;
            label.style.marginLeft = 3;
            return label;
        }

        /// <summary>The window, for its AI addon hooks.</summary>
        private DialogueDatabaseEditorWindow aiWindow { get { return conversationsPanel.editorWindow; } }

        /// <summary>Adds a header, with an AI addon button at its right if the button isn't null.</summary>
        private void AddHeaderWithAIButton(string text, VisualElement aiButton)
        {
            var header = CreateHeader(text);
            if (aiButton == null)
            {
                Add(header);
                return;
            }
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            header.style.flexGrow = 1;
            aiButton.style.flexShrink = 0;
            row.Add(header);
            row.Add(aiButton);
            Add(row);
        }

        private static Button CreateSmallButton(string text, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.style.width = 22;
            button.style.flexShrink = 0;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            return button;
        }

        private void AddTextArea(string label, string tooltip, SerializedProperty fieldProperty)
        {
            var labelElement = new Label(label) { tooltip = tooltip };
            labelElement.style.marginLeft = 3;
            labelElement.style.marginTop = 4;
            Add(labelElement);
            Add(FieldPropertyUtility.CreateTextField(null, fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), true));
        }

        /// <summary>
        /// Text editor for a field that may not exist. The field is added on the first edit, so
        /// viewing an entry doesn't modify it. Multiline editors get their label above.
        /// </summary>
        private VisualElement CreateTextEditor(SerializedProperty fieldsProperty, string fieldTitle, string label, string tooltip, bool multiline)
        {
            var textField = FieldPropertyUtility.CreateTextField(multiline ? null : label, multiline);
            textField.tooltip = tooltip;
            var fieldProperty = FieldPropertyUtility.FindField(fieldsProperty, fieldTitle);
            if (fieldProperty != null)
            {
                textField.BindProperty(fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName));
            }
            else
            {
                EventCallback<ChangeEvent<string>> onFirstChange = null;
                onFirstChange = evt =>
                {
                    textField.UnregisterValueChangedCallback(onFirstChange);
                    serializedObject.Update();
                    var newFieldProperty = FieldPropertyUtility.AddField(fieldsProperty, fieldTitle, evt.newValue, FieldType.Text);
                    serializedObject.ApplyModifiedProperties();
                    textField.BindProperty(newFieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName));
                };
                textField.RegisterValueChangedCallback(onFirstChange);
            }
            if (!multiline) return textField;
            var container = new VisualElement();
            var labelElement = new Label(label) { tooltip = tooltip };
            labelElement.style.marginLeft = 3;
            labelElement.style.marginTop = 4;
            container.Add(labelElement);
            container.Add(textField);
            return container;
        }

        /// <summary>
        /// Multiline Lua text field for an entry's Conditions or Script, with a "..." button
        /// that opens the matching wizard in a LuaWizardPopup.
        /// </summary>
        private VisualElement CreateLuaEditor(SerializedProperty entryProperty, int conversationID, int entryID,
            string propertyName, bool isConditions, string label, string tooltip)
        {
            var textField = FieldPropertyUtility.CreateTextField(null, entryProperty.FindPropertyRelative(propertyName), true);
            return CreateLuaEditor(textField, isConditions, label, tooltip, luaCode => SetEntryString(conversationID, entryID, propertyName, luaCode));
        }

        private VisualElement CreateLuaEditor(TextField textField, bool isConditions, string label, string tooltip, Action<string> onApply)
        {
            var container = new VisualElement();
            container.style.marginTop = 4;
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            var labelElement = new Label(label) { tooltip = tooltip };
            labelElement.style.marginLeft = 3;
            labelElement.style.flexGrow = 1;
            header.Add(labelElement);
            textField.tooltip = tooltip;
            header.Add(CreateSmallButton("...", "Open Lua wizard.", () =>
            {
                // Open below the header, as wide as the inspector. The window's position is the
                // screen rect of its root element, which is offset in panel space by the tab bar.
                var editorWindow = conversationsPanel.editorWindow;
                var bound = header.worldBound;
                var offset = bound.position - editorWindow.rootVisualElement.worldBound.position;
                var screenRect = new Rect(editorWindow.position.position + offset, bound.size);
                Action<string, FieldType> addVariable = (name, type) => VariablePropertyUtility.AddVariable(serializedObject, database, template, name, type);
                if (isConditions) LuaWizardPopup.ShowConditions(screenRect, database, textField.value, onApply, addVariable);
                else LuaWizardPopup.ShowScript(screenRect, database, textField.value, onApply, addVariable);
            }));
            container.Add(header);
            container.Add(textField);
            return container;
        }

        /// <summary>
        /// Sets a string property of an entry by ID, so callers don't hold a SerializedProperty
        /// that an inspector rebuild may have disposed.
        /// </summary>
        private void SetEntryString(int conversationID, int entryID, string propertyName, string value)
        {
            if (serializedObject == null || serializedObject.targetObject == null) return;
            serializedObject.Update();
            var conversationProperty = conversationsPanel.GetSelectedConversationProperty();
            if (conversationProperty == null || ConversationPropertyUtility.GetConversationID(conversationProperty) != conversationID) return;
            var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, entryID);
            if (entryProperty == null) return;
            entryProperty.FindPropertyRelative(propertyName).stringValue = value;
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Hosts an IMGUI editor for a field's value. The field is added on the first edit if it doesn't exist.
        /// </summary>
        private IMGUIContainer CreateImguiFieldEditor(SerializedProperty fieldsProperty, string fieldTitle, Func<string, string> draw)
        {
            var container = new IMGUIContainer(() =>
            {
                if (serializedObject == null || serializedObject.targetObject == null) return;
                string value;
                try
                {
                    value = FieldPropertyUtility.LookupValue(fieldsProperty, fieldTitle) ?? string.Empty;
                }
                catch (Exception)
                {
                    return;
                }
                var newValue = draw(value);
                if (newValue != value) FieldPropertyUtility.SetFieldValue(fieldsProperty, fieldTitle, newValue, FieldType.Text);
            });
            container.style.marginTop = 4;
            return container;
        }

        private void AddPrimaryFields(SerializedProperty fieldsProperty, List<string> primaryFieldTitles, List<string> builtInTitles)
        {
            if (primaryFieldTitles == null) return;
            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var fieldProperty = fieldsProperty.GetArrayElementAtIndex(i);
                var title = fieldProperty.FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
                if (string.IsNullOrEmpty(title) || builtInTitles.Contains(title) || !primaryFieldTitles.Contains(title)) continue;
                var isTextArea = FieldPropertyUtility.GetEffectiveTypeString(fieldProperty) == "CustomFieldType_Text" && FieldPropertyUtility.IsTextAreaField(title);
                Add(FieldPropertyUtility.CreateValueEditor(fieldProperty, database, title, isTextArea));
            }
        }

        #endregion

    }

}

#endif
