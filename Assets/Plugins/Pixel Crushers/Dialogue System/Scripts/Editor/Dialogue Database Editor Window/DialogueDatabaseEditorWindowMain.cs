// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// UI Toolkit dialogue database editor window. This is the main part, which handles
    /// the window lifecycle and binds the window to the database as a SerializedObject.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow : EditorWindow
    {

        [SerializeField]
        private DialogueDatabase database = null;
        public DialogueDatabase Database { get { return database; } }

        private SerializedObject serializedObject = null;
        public SerializedObject SerializedObject { get { return serializedObject; } }

        // The template lives in a hidden holder object so the Template tab can edit it through
        // a SerializedObject. It's saved to the database's templateJson and to EditorPrefs.
        [SerializeField]
        private DialogueDatabaseTemplateHolder templateHolder = null;
        private SerializedObject templateSerializedObject = null;
        private bool isTemplateLoaded = false;
        private string lastSavedTemplateJson = null;

        /// <summary>
        /// The database's template, or the editor's saved template if the database doesn't have one.
        /// </summary>
        public Template Template
        {
            get
            {
                if (!isTemplateLoaded) LoadTemplate();
                return TemplateHolder.template;
            }
        }

        public DialogueDatabaseTemplateHolder TemplateHolder
        {
            get
            {
                if (templateHolder == null)
                {
                    templateHolder = CreateInstance<DialogueDatabaseTemplateHolder>();
                    templateSerializedObject = null;
                }
                // Not HideAndDontSave, which includes NotEditable and makes the bound fields read-only.
                // Set every time, since the holder survives domain reloads.
                const HideFlags HolderHideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                if (templateHolder.hideFlags != HolderHideFlags)
                {
                    templateHolder.hideFlags = HolderHideFlags;
                    templateSerializedObject = null;
                }
                return templateHolder;
            }
        }

        /// <summary>
        /// SerializedObject for editing the template. Call SaveTemplateIfChanged after applying changes.
        /// </summary>
        public SerializedObject TemplateSerializedObject
        {
            get
            {
                if (!isTemplateLoaded) LoadTemplate();
                if (templateSerializedObject == null || templateSerializedObject.targetObject == null)
                {
                    templateSerializedObject = new SerializedObject(TemplateHolder);
                }
                return templateSerializedObject;
            }
        }

        private VisualElement contentHost = null;

        /// <summary>True once CreateGUI has built the panels, so they can be navigated.</summary>
        internal bool IsGUICreated { get { return contentHost != null; } }
        private VisualElement noDatabaseHelpBox = null; // Help box and Create New Database button.

        private const float MinWidth = 720f;
        private const float MinHeight = 240f;

        // Per-project user setting that remembers the database after the window is closed.
        private const string DatabaseGUIDConfigKey = "PixelCrushers.DialogueSystem.DialogueDatabaseEditorWindow.DatabaseGUID";

        private void OnEnable()
        {
            minSize = new Vector2(MinWidth, MinHeight);
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            RestartAutoBackupTimer();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }

        private void OnDestroy()
        {
            if (templateHolder != null) DestroyImmediate(templateHolder);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            DialogueDatabaseEditorTheme.TrackThemeChanges(root, OnThemeChanged);
            root.Add(CreateToolbar());

            noDatabaseHelpBox = CreateNoDatabaseHelp();
            root.Add(noDatabaseHelpBox);

            // Panels handle their own scrolling so they can use split views that fill the window.
            contentHost = new VisualElement();
            contentHost.style.flexGrow = 1;
            root.Add(contentHost);
            CreatePanels(contentHost);

            if (database == null)
            {
                database = LoadSavedDatabase();
                SyncDatabaseIfSyncOnOpen();
            }
            SetDatabase((database != null) ? database : Selection.activeObject as DialogueDatabase);
            RefreshPlayModeState();
        }

        /// <summary>Lets panels refresh colors they set in code after the editor theme changes.</summary>
        private void OnThemeChanged()
        {
            foreach (var panel in AllPanels())
            {
                panel.OnThemeChanged();
            }
        }

        private VisualElement CreateNoDatabaseHelp()
        {
            var container = new VisualElement();
            container.style.marginTop = 8;
            container.style.marginLeft = 8;
            container.style.marginRight = 8;
            container.Add(new HelpBox("Select a dialogue database asset, assign one in the toolbar, or create a new one.", UnityEngine.UIElements.HelpBoxMessageType.Info));
            var createButton = new Button(CreateNewDatabase)
            {
                text = "Create New Database",
                tooltip = "Create a new dialogue database asset and edit it."
            };
            createButton.style.alignSelf = Align.FlexStart;
            createButton.style.marginTop = 4;
            container.Add(createButton);
            return container;
        }

        /// <summary>
        /// Asks where to save a new dialogue database, creates it like the Assets > Create menu
        /// item does, and edits it.
        /// </summary>
        private void CreateNewDatabase()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Dialogue Database", "Dialogue Database", "asset",
                "Choose where to save the new dialogue database.", GetSelectedProjectFolder());
            if (string.IsNullOrEmpty(path)) return;
            var newDatabase = DialogueSystemMenuItems.CreateDialogueDatabaseInstance();
            AssetDatabase.CreateAsset(newDatabase, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(newDatabase);
            SetDatabase(newDatabase);
        }

        /// <summary>The folder selected in the Project window, or the folder of the selected asset, or Assets.</summary>
        private static string GetSelectedProjectFolder()
        {
            var path = (Selection.activeObject != null) ? AssetDatabase.GetAssetPath(Selection.activeObject) : null;
            if (string.IsNullOrEmpty(path)) return "Assets";
            if (AssetDatabase.IsValidFolder(path)) return path;
            var folder = System.IO.Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(folder) ? "Assets" : folder.Replace('\\', '/');
        }

        private static DialogueDatabase LoadSavedDatabase()
        {
            var guid = EditorUserSettings.GetConfigValue(DatabaseGUIDConfigKey);
            if (string.IsNullOrEmpty(guid)) return null;
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<DialogueDatabase>(assetPath);
        }

        private static void SaveDatabaseSelection(DialogueDatabase database)
        {
            string guid = null;
            if (database != null)
            {
                // Keep the saved database if this one isn't an asset, such as one created at runtime.
                var assetPath = AssetDatabase.GetAssetPath(database);
                if (string.IsNullOrEmpty(assetPath)) return;
                guid = AssetDatabase.AssetPathToGUID(assetPath);
            }
            if (EditorUserSettings.GetConfigValue(DatabaseGUIDConfigKey) != guid)
            {
                EditorUserSettings.SetConfigValue(DatabaseGUIDConfigKey, guid);
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is DialogueDatabase selectedDatabase && selectedDatabase != database)
            {
                SetDatabase(selectedDatabase);
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                if (contentHost == null) return; // GUI not created yet.
                RefreshPlayModeState();
                SetDatabase(database); // Rebuild SerializedObject across play mode transition.
            }
        }

        private void OnUndoRedoPerformed()
        {
            // Undo may have restored the template holder; save it so the database matches.
            if (templateSerializedObject != null && templateSerializedObject.targetObject != null) templateSerializedObject.Update();
            if (serializedObject != null && serializedObject.targetObject != null) serializedObject.Update();
            SaveTemplateIfChanged();
            foreach (var panel in AllPanels())
            {
                panel.OnUndoRedo();
            }
        }

        /// <summary>
        /// Refreshes every panel, keeping selections, after a change to many values that bound
        /// fields alone don't show, such as asset names in lists and text on graph nodes.
        /// Uses the same path as undo/redo.
        /// </summary>
        public void RefreshAllPanels()
        {
            OnUndoRedoPerformed();
        }

        /// <summary>
        /// Sets the database that the window edits and binds the UI to it.
        /// </summary>
        public void SetDatabase(DialogueDatabase newDatabase)
        {
            var isDifferentDatabase = newDatabase != database;
            if (isDifferentDatabase) ResetPanelStates();
            database = newDatabase;
            if (isDifferentDatabase) SyncDatabaseIfSyncOnOpen();
            SaveDatabaseSelection(database);
            isTemplateLoaded = false;
            if (contentHost == null) return; // CreateGUI will bind when it runs.

            rootVisualElement.Unbind();
            serializedObject = (database != null) ? new SerializedObject(database) : null;
            if (serializedObject != null) rootVisualElement.Bind(serializedObject);

            if (databaseField != null) databaseField.SetValueWithoutNotify(database);
            foreach (var panel in AllPanels())
            {
                panel.OnDatabaseChanged(serializedObject);
            }
            RefreshVisiblePanel();
        }

        private void SyncDatabaseIfSyncOnOpen()
        {
            if (DialogueDatabaseEditorSettings.instance.syncOnOpen && database != null) database.SyncAll();
        }

        /// <summary>
        /// Rebinds the UI and clears per-database panel state after the database was changed
        /// directly, such as by rebaselining IDs, since selections may no longer be valid.
        /// </summary>
        public void RefreshAfterDirectDatabaseChange()
        {
            ResetPanelStates();
            SetDatabase(database);
        }

        private void LoadTemplate()
        {
            Template template = null;
            if (database != null && !string.IsNullOrEmpty(database.templateJson))
            {
                try
                {
                    template = JsonUtility.FromJson<Template>(database.templateJson);
                }
                catch (System.ArgumentException)
                {
                    template = null;
                }
            }
            if (template == null) template = TemplateTools.LoadFromEditorPrefs();
            TemplateHolder.template = template;
            isTemplateLoaded = true;
            lastSavedTemplateJson = JsonUtility.ToJson(template);
            if (templateSerializedObject != null && templateSerializedObject.targetObject != null) templateSerializedObject.Update();
        }

        /// <summary>
        /// Saves the template to the database's templateJson and to EditorPrefs, like the IMGUI
        /// editor, but only if it changed since it was last loaded or saved. The database write
        /// isn't recorded for undo, because undoing the template edit itself saves again.
        /// </summary>
        public void SaveTemplateIfChanged()
        {
            if (!isTemplateLoaded || templateHolder == null) return;
            var json = JsonUtility.ToJson(templateHolder.template);
            if (json == lastSavedTemplateJson) return;
            lastSavedTemplateJson = json;
            TemplateTools.SaveToEditorPrefs(templateHolder.template);
            if (database != null && serializedObject != null && serializedObject.targetObject != null)
            {
                serializedObject.Update();
                serializedObject.FindProperty("templateJson").stringValue = json;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

    }

}

#endif
