// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// Events foldout of the dialogue entry inspector, like the IMGUI editor's DrawUnityEvents.
    /// Shows the entry's scene-independent OnExecute() event and its scene event, which lives
    /// on a DialogueSystemSceneEvents component in a scene and is found by the GUID in the
    /// entry's EventGuid field. The scene event section follows scene loads and hierarchy changes.
    /// </summary>
    public class EntryEventsElement : VisualElement
    {

        private readonly ConversationsPanel conversationsPanel;
        private readonly int conversationID;
        private readonly int entryID;
        private readonly VisualElement sceneEventContainer;

        // What the scene event section shows, so it only rebuilds when this changes.
        private string shownGuid = null;
        private DialogueSystemSceneEvents shownInstance = null;
        private int shownIndex = -1;
        private bool isShowingSceneEvent = false;

        private SerializedObject serializedObject { get { return conversationsPanel.databaseSerializedObject; } }

        public EntryEventsElement(ConversationsPanel panel, SerializedProperty entryProperty, int conversationID, int entryID)
        {
            this.conversationsPanel = panel;
            this.conversationID = conversationID;
            this.entryID = entryID;
            style.marginTop = 4;

            var state = conversationsPanel.conversationsState;
            var foldout = new Foldout() { text = "Events", value = state.entryEventsFoldout };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) state.entryEventsFoldout = evt.newValue;
            });
            Add(foldout);

            foldout.Add(CreateBoldLabel("Scene-Independent Event", "This UnityEvent cannot point to scene objects. It can point to assets and prefabs."));
            var onExecuteProperty = entryProperty.FindPropertyRelative("onExecute");
            var onExecuteField = new PropertyField(onExecuteProperty, "OnExecute()") { tooltip = "Events to invoke when this entry is used. They can only reference assets, not scene objects." };
            onExecuteField.BindProperty(onExecuteProperty);
            foldout.Add(onExecuteField);

            sceneEventContainer = new VisualElement();
            sceneEventContainer.style.marginTop = 4;
            foldout.Add(sceneEventContainer);
            RefreshSceneEvent(true);

            RegisterCallback<AttachToPanelEvent>(evt => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(evt => Unsubscribe());
        }

        #region Scene Changes

        private void Subscribe()
        {
            Unsubscribe();
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void Unsubscribe()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        }

        private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode) { RefreshSceneEvent(false); }
        private void OnSceneClosed(UnityEngine.SceneManagement.Scene scene) { RefreshSceneEvent(false); }
        private void OnHierarchyChanged() { RefreshSceneEvent(false); }

        #endregion

        #region Scene Event Section

        /// <summary>Rebuilds the scene event section if what it should show has changed, or if forced.</summary>
        private void RefreshSceneEvent(bool force)
        {
            var guid = GetSceneEventGuid();
            int index = -1;
            var instance = string.IsNullOrEmpty(guid) ? null : FindSceneEvents(guid, out index);
            if (!force && guid == shownGuid && instance == shownInstance && index == shownIndex) return;
            shownGuid = guid;
            shownInstance = instance;
            shownIndex = index;

            // Unbind the old scene event fields before removing them.
            if (isShowingSceneEvent) sceneEventContainer.Unbind();
            sceneEventContainer.Clear();
            isShowingSceneEvent = false;

            if (string.IsNullOrEmpty(guid))
            {
                sceneEventContainer.Add(new Button(AddSceneEvent)
                {
                    text = "Add Scene Event",
                    tooltip = "Add a UnityEvent that operates on GameObjects in the currently-open scene."
                });
            }
            else if (instance == null)
            {
                sceneEventContainer.Add(new HelpBox("Scene Event operates in another scene.", UnityEngine.UIElements.HelpBoxMessageType.Info));
                sceneEventContainer.Add(CreateDeleteButton());
            }
            else
            {
                var sceneEventsSerializedObject = new SerializedObject(instance);
                var listProperty = sceneEventsSerializedObject.FindProperty("dialogueEntrySceneEvents");
                if (listProperty == null || !(0 <= index && index < listProperty.arraySize)) return;
                var sceneEventProperty = listProperty.GetArrayElementAtIndex(index);
                sceneEventContainer.Add(CreateBoldLabel("Scene Event", "UnityEvent that can point to GameObjects in " + instance.gameObject.scene.name + "."));
                var guidField = new TextField("Guid") { value = guid, tooltip = "Identifies this entry's event on the Dialogue System Scene Events GameObject." };
                guidField.SetEnabled(false);
                sceneEventContainer.Add(guidField);
                var onExecuteProperty = sceneEventProperty.FindPropertyRelative("onExecute");
                var onExecuteField = new PropertyField(onExecuteProperty, "OnExecute()") { tooltip = "Events to invoke when this entry is used. They can reference objects in this scene." };
                onExecuteField.BindProperty(onExecuteProperty);
                sceneEventContainer.Add(onExecuteField);
                sceneEventContainer.Add(CreateDeleteButton());
                isShowingSceneEvent = true;
            }
        }

        private Button CreateDeleteButton()
        {
            return new Button(DeleteSceneEvent) { text = "Delete Scene Event", tooltip = "Remove this entry's scene event." };
        }

        private static Label CreateBoldLabel(string text, string tooltip)
        {
            var label = new Label(text) { tooltip = tooltip };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginLeft = 3;
            label.style.marginTop = 2;
            return label;
        }

        #endregion

        #region Add and Delete

        private SerializedProperty FindEntryProperty()
        {
            if (serializedObject == null || serializedObject.targetObject == null) return null;
            var conversationProperty = conversationsPanel.GetSelectedConversationProperty();
            if (conversationProperty == null || ConversationPropertyUtility.GetConversationID(conversationProperty) != conversationID) return null;
            return ConversationPropertyUtility.FindEntry(conversationProperty, entryID);
        }

        private string GetSceneEventGuid()
        {
            var entryProperty = FindEntryProperty();
            if (entryProperty == null) return null;
            return FieldPropertyUtility.LookupValue(entryProperty.FindPropertyRelative("fields"), DialogueSystemFields.SceneEventGuidFieldName);
        }

        private void AddSceneEvent()
        {
            serializedObject.Update();
            var entryProperty = FindEntryProperty();
            if (entryProperty == null) return;
            var group = Undo.GetCurrentGroup();
            var instance = GetOrCreateActiveSceneEvents();
            if (instance == null) return;
            Undo.RecordObject(instance, "Add Scene Event");
            string guid;
            if (DialogueSystemSceneEvents.AddNewDialogueEntrySceneEvent(out guid, instance) == -1) return;
            EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
            FieldPropertyUtility.SetFieldValue(entryProperty.FindPropertyRelative("fields"), DialogueSystemFields.SceneEventGuidFieldName, guid, FieldType.Text, false);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Scene Event");
            Undo.CollapseUndoOperations(group);
            conversationsPanel.RequestGraphRebuild(); // Updates the node's event indicator and rebuilds the inspector.
        }

        private void DeleteSceneEvent()
        {
            serializedObject.Update();
            var entryProperty = FindEntryProperty();
            if (entryProperty == null) return;
            var fieldsProperty = entryProperty.FindPropertyRelative("fields");
            var guid = FieldPropertyUtility.LookupValue(fieldsProperty, DialogueSystemFields.SceneEventGuidFieldName);
            var group = Undo.GetCurrentGroup();
            if (!string.IsNullOrEmpty(guid))
            {
                // Remove it from every loaded scene that has it, in case a scene was duplicated.
                foreach (var instance in GameObjectUtility.FindObjectsByType<DialogueSystemSceneEvents>())
                {
                    if (DialogueSystemSceneEvents.GetDialogueEntrySceneEventIndex(guid, instance) == -1) continue;
                    Undo.RecordObject(instance, "Delete Scene Event");
                    DialogueSystemSceneEvents.RemoveDialogueEntrySceneEvent(guid, instance);
                    EditorSceneManager.MarkSceneDirty(instance.gameObject.scene);
                }
            }
            FieldPropertyUtility.RemoveField(fieldsProperty, DialogueSystemFields.SceneEventGuidFieldName, false);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Delete Scene Event");
            Undo.CollapseUndoOperations(group);
            conversationsPanel.RequestGraphRebuild();
        }

        #endregion

        #region Static Helpers

        /// <summary>
        /// Returns the loaded DialogueSystemSceneEvents that has a scene event with this GUID, and
        /// the event's index, or null. Unlike DialogueSystemSceneEvents.GetDialogueEntrySceneEventIndex(guid),
        /// this works outside play mode and checks every loaded scene.
        /// </summary>
        public static DialogueSystemSceneEvents FindSceneEvents(string guid, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(guid)) return null;
            foreach (var instance in GameObjectUtility.FindObjectsByType<DialogueSystemSceneEvents>())
            {
                if (instance == null || instance.dialogueEntrySceneEvents == null) continue;
                index = DialogueSystemSceneEvents.GetDialogueEntrySceneEventIndex(guid, instance);
                if (index != -1) return instance;
            }
            index = -1;
            return null;
        }

        /// <summary>
        /// Returns the active scene's DialogueSystemSceneEvents, creating its GameObject with
        /// undo if the scene doesn't have one.
        /// </summary>
        public static DialogueSystemSceneEvents GetOrCreateActiveSceneEvents()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            foreach (var instance in GameObjectUtility.FindObjectsByType<DialogueSystemSceneEvents>())
            {
                if (instance != null && instance.gameObject.scene == activeScene) return instance;
            }
            var go = new GameObject("Dialogue System Scene Events");
            Undo.RegisterCreatedObjectUndo(go, "Add Scene Event");
            var component = Undo.AddComponent(go, TypeUtility.GetWrapperType(typeof(DialogueSystemSceneEvents))) as DialogueSystemSceneEvents;
            EditorSceneManager.MarkSceneDirty(activeScene);
            return component;
        }

        #endregion

    }

}

#endif
