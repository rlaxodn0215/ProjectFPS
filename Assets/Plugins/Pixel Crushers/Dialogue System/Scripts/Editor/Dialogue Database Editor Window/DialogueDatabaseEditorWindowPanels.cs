// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window manages the
    /// panel shown for each toolbar category.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        private Dictionary<Category, DatabaseEditorPanel> panels = new Dictionary<Category, DatabaseEditorPanel>();
        private DatabaseEditorPanel watchesPanel = null;

        private void CreatePanels(VisualElement host)
        {
            panels.Clear();
            panels.Add(Category.Database, new DatabasePanel());
            panels.Add(Category.Actors, new ActorsPanel());
            panels.Add(Category.ItemsQuests, new ItemsQuestsPanel());
            panels.Add(Category.Locations, new LocationsPanel());
            panels.Add(Category.Variables, new VariablesPanel());
            panels.Add(Category.Conversations, new ConversationsPanel());
            panels.Add(Category.Template, new TemplatePanel());
            watchesPanel = new WatchesPanel();
            foreach (var panel in AllPanels())
            {
                panel.window = this;
                host.Add(panel);
            }
        }

        private IEnumerable<DatabaseEditorPanel> AllPanels()
        {
            foreach (var panel in panels.Values)
            {
                yield return panel;
            }
            if (watchesPanel != null) yield return watchesPanel;
        }

        /// <summary>
        /// Clears per-database panel state, such as selections and filters, when the window
        /// switches to a different database.
        /// </summary>
        private void ResetPanelStates()
        {
            actorsPanelState = new ActorsPanelState();
            locationsPanelState = new AssetPanelState();
            itemsPanelState = new ItemsPanelState();
            variablesPanelState = new AssetPanelState();
            conversationsPanelState = new ConversationsPanelState();
        }

        private DatabaseEditorPanel GetPanel(Category category)
        {
            if (category == Category.Template && EditorApplication.isPlaying) return watchesPanel;
            return panels.TryGetValue(category, out var panel) ? panel : null;
        }

        /// <summary>
        /// Shows a category and selects an asset in it by ID. For conversations, also selects
        /// and frames entryID, if it's not -1.
        /// </summary>
        public void GoToAsset(Category category, int assetID, int entryID = -1)
        {
            if (database == null) return;
            CurrentCategory = category;
            switch (GetPanel(category))
            {
                case ConversationsPanel conversationsPanel:
                    conversationsPanel.SelectAndFrameEntry(assetID, entryID);
                    break;
                case AssetListPanel<Actor> actorsPanel:
                    actorsPanel.SelectAssetByID(assetID);
                    break;
                case AssetListPanel<Item> itemsPanel:
                    itemsPanel.SelectAssetByID(assetID);
                    break;
                case AssetListPanel<Location> locationsPanel:
                    locationsPanel.SelectAssetByID(assetID);
                    break;
                case AssetListPanel<Variable> variablesPanel:
                    variablesPanel.SelectAssetByID(assetID);
                    break;
            }
        }

        private void RefreshVisiblePanel()
        {
            if (contentHost == null) return;
            var activePanel = GetPanel(currentCategory);
            var needsDatabase = activePanel != null && activePanel.RequiresDatabase;
            var showPanel = activePanel != null && (database != null || !needsDatabase);
            foreach (var panel in AllPanels())
            {
                panel.style.display = (showPanel && panel == activePanel) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            noDatabaseHelpBox.style.display = (database == null && needsDatabase) ? DisplayStyle.Flex : DisplayStyle.None;
        }

    }

    /// <summary>
    /// Base class for the content panel of a toolbar category.
    /// </summary>
    public abstract class DatabaseEditorPanel : VisualElement
    {

        protected Label titleLabel;

        /// <summary>
        /// The window that owns this panel. Assigned when the window creates its panels.
        /// </summary>
        public DialogueDatabaseEditorWindow window { get; set; }

        protected DialogueDatabase database { get { return (window != null) ? window.Database : null; } }

        /// <summary>
        /// True if the panel can only be shown when a database is assigned.
        /// </summary>
        public virtual bool RequiresDatabase { get { return true; } }

        protected DatabaseEditorPanel(string title)
        {
            style.flexGrow = 1;
            style.paddingLeft = 8;
            style.paddingRight = 8;
            style.paddingTop = 8;
            style.paddingBottom = 8;
            titleLabel = new Label(title);
            titleLabel.style.fontSize = 16;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 6;
            Add(titleLabel);
        }

        /// <summary>
        /// Called when the window's database changes. serializedObject is null if no database.
        /// </summary>
        public virtual void OnDatabaseChanged(SerializedObject serializedObject) { }

        /// <summary>
        /// Called after an undo or redo, once the window's SerializedObject has been updated.
        /// </summary>
        public virtual void OnUndoRedo() { }

        /// <summary>
        /// Called after the editor switches between the Dark and Light themes, once the
        /// window's theme stylesheet has been swapped. Override to refresh colors set in code.
        /// </summary>
        public virtual void OnThemeChanged() { }

        protected void AddPlaceholderNote()
        {
            Add(new HelpBox("Not implemented yet.", UnityEngine.UIElements.HelpBoxMessageType.None));
        }

        protected static Label CreateTextAreaLabel(string text, string tooltip)
        {
            var label = new Label(text) { tooltip = tooltip };
            label.style.marginTop = 4;
            label.style.marginLeft = 3;
            return label;
        }

    }

}

#endif
