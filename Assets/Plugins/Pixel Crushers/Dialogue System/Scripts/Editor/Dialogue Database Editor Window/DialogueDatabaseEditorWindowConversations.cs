// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window holds the Conversations
    /// panel's state so it survives domain reloads.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [Serializable]
        public class ConversationViewState
        {
            public int conversationID;
            public Vector3 position;
            public Vector3 scale = Vector3.one;
        }

        [Serializable]
        public class ConversationsPanelState : AssetPanelState
        {
            // Graph pan/zoom per conversation. Kept here, not in the asset, so panning doesn't dirty the database.
            public List<ConversationViewState> views = new List<ConversationViewState>();
            public int selectionConversationID = -1;
            public List<int> selectedEntryIDs = new List<int>();
            public List<int> selectedGroupIndices = new List<int>();
            public bool entryFieldsFoldout = false;
            public bool entryEventsFoldout = false;
            public bool localizedMenuTextFoldout = false;
            public bool localizedDialogueTextFoldout = false;
            public bool localizedSequenceFoldout = false;
            public bool localizedResponseMenuSequenceFoldout = false;
            public bool showFullNodeText = false;
            public bool showNodeIDs = false;
            public bool showAllActorNames = false;
            public bool showActorPortraits = false;
            public bool showLinkOrderOnArrows = true;
            public string lastTemplateFilename = string.Empty;
            public bool trimWhitespaceAroundPipes = true;
            public bool alternateActorsAtPipes = false;
            public bool autoArrangeOnCreate = false;
            public bool addNewNodesToRight = false;
            public bool confirmNodeAndLinkDeletion = true;
            public bool isSearchBarOpen = false;
            public string searchText = string.Empty;
            public bool searchCaseSensitive = false;
        }

        [SerializeField]
        private ConversationsPanelState conversationsPanelState = new ConversationsPanelState();
        public ConversationsPanelState ConversationsState { get { return conversationsPanelState; } }

    }

    /// <summary>
    /// Conversations category: the conversation list on the left, a GraphView node editor in the
    /// middle, and an inspector for the selected conversation, entry or link on the right. Graph
    /// and inspector edits go through the window's SerializedObject, keyed by entry ID.
    /// </summary>
    public class ConversationsPanel : AssetListPanel<Conversation>
    {

        private const float InspectorWidth = 340;
        private const float ChildNodeGap = 40;

        private ConversationGraphView graphView;
        private VisualElement graphHost;
        private Label noConversationLabel;
        private VisualElement searchBar;
        private ToolbarSearchField searchField;
        private ToolbarToggle searchCaseToggle;
        private ToolbarButton searchNextButton;
        private ToolbarButton searchPreviousButton;
        private Label searchStatusLabel;
        private VisualElement participantsWatermark;
        private Label actorWatermarkLabel;
        private Label conversantWatermarkLabel;
        private ScrollView inspectorScrollView;
        private ConversationInspector inspector;
        private VisualElement graphTracker;

        private Dictionary<int, DialogueEntryNode> nodesByID = new Dictionary<int, DialogueEntryNode>();
        private List<EntryGroupElement> groupElements = new List<EntryGroupElement>();
        private Dictionary<int, Sprite> actorPortraits = new Dictionary<int, Sprite>();
        // What a group drag moves, with start positions. Decided when the drag starts, like the IMGUI editor.
        private Dictionary<DialogueEntryNode, Vector2> draggedNodes = new Dictionary<DialogueEntryNode, Vector2>();
        private Dictionary<EntryGroupElement, Rect> draggedGroups = new Dictionary<EntryGroupElement, Rect>();
        private int currentConversationID = -1;
        private bool isRebuildingGraph = false;
        private bool isRebuildScheduled = false;
        private string lastStructureSignature = null;
        private int pendingSelectEntryID = -1;
        private int pendingSelectGroupIndex = -1;

        // The active conversation's state at runtime, if this database is loaded, else null.
        private ConversationState runtimeState = null;
        private DialogueEntry runtimeEntry = null;
        private const long RuntimePollInterval = 100; // ms

        public ConversationsPanel() : base("Conversations") { }

        protected override string AssetTypeName { get { return "Conversation"; } }
        protected override string ListPropertyName { get { return "conversations"; } }
        protected override string NameFieldTitle { get { return DialogueSystemFields.Title; } }
        protected override DialogueDatabaseEditorWindow.AssetPanelState state { get { return window.ConversationsState; } }
        internal DialogueDatabaseEditorWindow.ConversationsPanelState conversationsState { get { return window.ConversationsState; } }

        protected override List<Conversation> GetAssets(DialogueDatabase database) { return database.conversations; }
        protected override List<Field> GetTemplateFields(Template template, Conversation conversation) { return template.conversationFields; }
        protected override List<string> GetPrimaryFieldTitles(Template template, Conversation conversation) { return template.conversationPrimaryFieldTitles; }

        // Conversations don't sync from other databases.
        protected override bool IsSyncEnabled { get { return false; } set { } }
        protected override DialogueDatabase SyncDatabase { get { return null; } set { } }
        protected override void SyncFromDatabase() { }

        /// <summary>
        /// Groups conversations by the part of the title before the last "/", like the IMGUI
        /// editor's conversation popup submenus.
        /// </summary>
        protected override string GetGroupName(Conversation conversation)
        {
            var title = (conversation != null) ? (conversation.Title ?? string.Empty) : string.Empty;
            var slashPos = title.LastIndexOf('/');
            return (slashPos > 0) ? title.Substring(0, slashPos) : string.Empty;
        }

        #region Internal API for ConversationInspector

        internal DialogueDatabaseEditorWindow editorWindow { get { return window; } }
        internal DialogueDatabase editedDatabase { get { return database; } }
        internal SerializedObject databaseSerializedObject { get { return serializedObject; } }

        internal SerializedProperty GetSelectedConversationProperty()
        {
            var listProperty = GetListProperty();
            return (listProperty != null && 0 <= state.selectedIndex && state.selectedIndex < listProperty.arraySize)
                ? listProperty.GetArrayElementAtIndex(state.selectedIndex) : null;
        }

        internal Conversation GetSelectedConversation()
        {
            return GetAssetObject(state.selectedIndex);
        }

        internal DialogueEntry GetEntryObject(int entryID)
        {
            var conversation = GetSelectedConversation();
            return (conversation != null) ? conversation.dialogueEntries.Find(entry => entry.id == entryID) : null;
        }

        /// <summary>The entries selected in the node graph.</summary>
        internal List<DialogueEntry> GetSelectedEntryObjects()
        {
            var result = new List<DialogueEntry>();
            foreach (var entryID in conversationsState.selectedEntryIDs)
            {
                var entry = GetEntryObject(entryID);
                if (entry != null) result.Add(entry);
            }
            return result;
        }

        internal void SelectEntry(int entryID, bool frame)
        {
            DialogueEntryNode node;
            if (!nodesByID.TryGetValue(entryID, out node)) return;
            graphView.ClearSelection();
            graphView.AddToSelection(node);
            if (frame) graphView.FrameNode(node);
        }

        internal void CreateChildEntry(int parentEntryID, bool useSameActors)
        {
            DialogueEntryNode parentNode;
            if (!nodesByID.TryGetValue(parentEntryID, out parentNode)) return;
            var rect = parentNode.GetPosition();
            var position = conversationsState.addNewNodesToRight ? new Vector2(rect.xMax + ChildNodeGap, rect.y) : new Vector2(rect.x, rect.yMax + ChildNodeGap);
            CreateEntry(position, parentNode, useSameActors);
        }

        /// <summary>Rebuilds the graph after a structural change made outside the graph, such as in the inspector's link list.</summary>
        internal void RequestGraphRebuild()
        {
            ScheduleGraphRebuild();
        }

        internal void DeleteGraphSelection()
        {
            graphView.DeleteSelection();
        }

        internal void OnConversationTitleChanged()
        {
            RefreshListRows(); // The title determines the list label and group.
        }

        #endregion

        #region Menu and Add

        protected override void PopulateMenu(DropdownMenu menu)
        {
            menu.AppendAction("Play...", a => PlayConversationFromEntry(ConversationPropertyUtility.StartEntryID),
                a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Frame START", a => FrameStart(), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Frame All", a => graphView.FrameAll(), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            AppendRuntimeMenuItems(menu);
            AppendConversationMenuItems(menu);
            AppendSplitPipesMenuItems(menu);
            menu.AppendAction("Arrange Nodes/Vertically", a => ArrangeNodes(true), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Arrange Nodes/Horizontally", a => ArrangeNodes(false), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            AppendHideFilteredMenuItem(menu);
            AppendShowMenuItems(menu);
            menu.AppendAction("Sort/By Title", a => SortAssets(conversation => conversation.Title, StringComparer.CurrentCulture));
            menu.AppendAction("Sort/By ID", a => SortAssets(conversation => conversation.id, Comparer<int>.Default));
            menu.AppendAction("Search/Search Bar", a => SetSearchBarOpen(!conversationsState.isSearchBarOpen),
                a => conversationsState.isSearchBarOpen ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            AppendSettingsMenuItems(menu);
        }

        /// <summary>Adds the Menu dropdown items that the node editor's context menus also have.</summary>
        private void AppendSharedContextMenuItems(DropdownMenu menu)
        {
            AppendRuntimeMenuItems(menu);
            AppendConversationMenuItems(menu);
            AppendSplitPipesMenuItems(menu);
            AppendShowMenuItems(menu);
            AppendSettingsMenuItems(menu);
        }

        private void AppendConversationMenuItems(DropdownMenu menu)
        {
            // Like clicking blank canvas: clearing the selection shows the conversation in the inspector.
            menu.AppendAction("Conversation Properties", a => graphView.ClearSelection(), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("New Conversation", a => AddNewConversation());
            menu.AppendAction("Duplicate Conversation", a => DuplicateConversation(), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Spell Check Conversation...", a => SpellCheckWindow.Open(window, currentConversationID, true),
                a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Templates/New From Template/Built-In/Quest Conversation", a => CreateConversationFromJson(BuiltInConversationTemplates.QuestConversationJson));
            menu.AppendAction("Templates/New From Template/From Template JSON...", a => CreateConversationFromTemplateFile());
            menu.AppendAction("Templates/Save Template JSON...", a => SaveConversationTemplate(), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void AppendSplitPipesMenuItems(DropdownMenu menu)
        {
            menu.AppendAction("Split Pipes Into Nodes/Process Conversation", a => SplitPipesIntoNodes(), a => (currentConversationID >= 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            menu.AppendAction("Split Pipes Into Nodes/Trim Whitespace Around Pipes",
                a => conversationsState.trimWhitespaceAroundPipes = !conversationsState.trimWhitespaceAroundPipes,
                a => GetPipeOptionStatus(conversationsState.trimWhitespaceAroundPipes));
            menu.AppendAction("Split Pipes Into Nodes/Alternate Actors At Pipes",
                a => conversationsState.alternateActorsAtPipes = !conversationsState.alternateActorsAtPipes,
                a => GetPipeOptionStatus(conversationsState.alternateActorsAtPipes));
        }

        private void AppendShowMenuItems(DropdownMenu menu)
        {
            AppendShowMenuItem(menu, "Show/Show Node IDs", () => conversationsState.showNodeIDs, value => conversationsState.showNodeIDs = value);
            AppendShowMenuItem(menu, "Show/Show All Actor Names", () => conversationsState.showAllActorNames, value => conversationsState.showAllActorNames = value);
            AppendShowMenuItem(menu, "Show/Show Actor Portraits", () => conversationsState.showActorPortraits, value => conversationsState.showActorPortraits = value);
            AppendShowMenuItem(menu, "Show/Show Link Order", () => conversationsState.showLinkOrderOnArrows, value => conversationsState.showLinkOrderOnArrows = value);
            AppendShowMenuItem(menu, "Show/Full Node Text", () => conversationsState.showFullNodeText, value => conversationsState.showFullNodeText = value);
        }

        private void AppendSettingsMenuItems(DropdownMenu menu)
        {
            menu.AppendAction("Settings/Auto Arrange After Adding Node",
                a => conversationsState.autoArrangeOnCreate = !conversationsState.autoArrangeOnCreate,
                a => conversationsState.autoArrangeOnCreate ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Settings/Add New Nodes to Right",
                a => conversationsState.addNewNodesToRight = !conversationsState.addNewNodesToRight,
                a => conversationsState.addNewNodesToRight ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            menu.AppendAction("Settings/Confirm Node and Link Deletion",
                a => conversationsState.confirmNodeAndLinkDeletion = !conversationsState.confirmNodeAndLinkDeletion,
                a => conversationsState.confirmNodeAndLinkDeletion ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        /// <summary>Adds a checkable menu item for a display option, which refreshes the nodes when toggled.</summary>
        private void AppendShowMenuItem(DropdownMenu menu, string path, Func<bool> getValue, Action<bool> setValue)
        {
            menu.AppendAction(path, a =>
            {
                setValue(!getValue());
                RefreshNodeContents();
            },
            a => getValue() ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        protected override void OnAddButtonClicked(Button button)
        {
            AddNewConversation();
        }

        protected override void AddListFooterButtons(VisualElement footer)
        {
            var aiButton = window.CreateAIGenerateConversationButton();
            if (aiButton != null) footer.Add(aiButton);
        }

        private void AddNewConversation()
        {
            var playerID = database.playerID;
            var npc = database.actors.Find(actor => !actor.IsPlayer);
            var conversantID = (npc != null) ? npc.id : 0;
            // If the database only has the player, give the conversation an NPC to talk to.
            var addNPC = database.actors.Count == 1 && database.actors[0].IsPlayer;
            AddNewAsset(window.Template.conversationFields, "Conversation", conversationProperty =>
            {
                // Same change as the conversation, so one undo removes both.
                if (addNPC) conversantID = AddNPCActor(conversationProperty.serializedObject);
                // Replace the whole element, since InsertArrayElementAtIndex copied the previous conversation.
                var id = conversationProperty.FindPropertyRelative("id").intValue;
                conversationProperty.boxedValue = ConversationPropertyUtility.CreateConversation(window.Template, id,
                    string.Format("New Conversation {0}", id), playerID, conversantID);
            });
        }

        /// <summary>
        /// Adds an actor named "NPC" from the template, with the next actor ID, and returns its ID.
        /// </summary>
        private int AddNPCActor(SerializedObject databaseObject)
        {
            int highestID = database.baseID - 1;
            database.actors.ForEach(actor => highestID = Mathf.Max(highestID, actor.id));
            var npcID = Mathf.Max(1, highestID + 1);
            var actorsProperty = databaseObject.FindProperty("actors");
            var index = actorsProperty.arraySize;
            actorsProperty.InsertArrayElementAtIndex(index);
            // Replace the whole element, since InsertArrayElementAtIndex copied the previous actor.
            actorsProperty.GetArrayElementAtIndex(index).boxedValue = window.Template.CreateActor(npcID, "NPC", false);
            return npcID;
        }

        /// <summary>
        /// Adds a copy of the selected conversation with the next ID and an unused "Copy" title,
        /// like the IMGUI editor. Links within the conversation point to the copy.
        /// </summary>
        private void DuplicateConversation()
        {
            var source = GetSelectedConversation();
            if (source == null) return;
            if (!EditorUtility.DisplayDialog(string.Format("Copy '{0}'?", source.Title), "Make a copy of this conversation?", "Copy", "Cancel")) return;
            var copyTitle = GetAvailableCopyTitle(source.Title);
            AddNewAsset(null, "Conversation", conversationProperty =>
            {
                var copy = new Conversation(source);
                ConversationPropertyUtility.SetConversationID(copy, conversationProperty.FindPropertyRelative("id").intValue);
                copy.Title = copyTitle;
                conversationProperty.boxedValue = copy;
            });
            Undo.SetCurrentGroupName("Duplicate Conversation");
        }

        private string GetAvailableCopyTitle(string title)
        {
            var copyTitle = title + " Copy";
            int count = 0;
            while (database.GetConversation(copyTitle) != null && count < 1000)
            {
                count++;
                copyTitle = title + " Copy " + count;
            }
            return copyTitle;
        }

        private string GetTemplateDirectory()
        {
            var filename = conversationsState.lastTemplateFilename;
            return string.IsNullOrEmpty(filename) ? Application.dataPath : Path.GetDirectoryName(filename);
        }

        private void SaveConversationTemplate()
        {
            var conversation = GetSelectedConversation();
            if (conversation == null) return;
            var filename = EditorUtility.SaveFilePanel("Save Conversation Template", GetTemplateDirectory(), "ConversationTemplate.json", "json");
            if (string.IsNullOrEmpty(filename)) return;
            conversationsState.lastTemplateFilename = filename;
            try
            {
                File.WriteAllText(filename, JsonUtility.ToJson(conversation));
                Debug.Log("Dialogue System: Saved conversation template " + filename);
            }
            catch (Exception e)
            {
                Debug.LogError("Dialogue System: Unable to save conversation template " + filename + ": " + e.Message);
            }
        }

        private void CreateConversationFromTemplateFile()
        {
            var filename = EditorUtility.OpenFilePanel("Select Conversation Template", GetTemplateDirectory(), "json");
            if (string.IsNullOrEmpty(filename)) return;
            conversationsState.lastTemplateFilename = filename;
            string json;
            try
            {
                json = File.ReadAllText(filename);
            }
            catch (Exception e)
            {
                Debug.LogError("Dialogue System: Unable to load conversation template " + filename + ": " + e.Message);
                return;
            }
            CreateConversationFromJson(json);
        }

        /// <summary>
        /// Adds a conversation from template JSON with the next ID and an unused numbered title,
        /// like the IMGUI editor. Links within the conversation point to the new ID.
        /// </summary>
        private void CreateConversationFromJson(string json)
        {
            if (serializedObject == null) return;

            // Parse first. An exception inside AddNewAsset would leave a half-added array element.
            Conversation conversation = null;
            try
            {
                conversation = JsonUtility.FromJson<Conversation>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Dialogue System: Unable to read conversation template: " + e.Message);
            }
            if (conversation == null || conversation.dialogueEntries == null || conversation.dialogueEntries.Count == 0)
            {
                EditorUtility.DisplayDialog("Template Error", "Unable to read a conversation from the template.", "OK");
                return;
            }

            var title = GetAvailableTemplateTitle(conversation.Title);
            AddNewAsset(null, "Conversation", conversationProperty =>
            {
                ConversationPropertyUtility.SetConversationID(conversation, conversationProperty.FindPropertyRelative("id").intValue);
                conversation.Title = title;
                conversationProperty.boxedValue = conversation;
            });
            Undo.SetCurrentGroupName("New Conversation From Template");
        }

        private string GetAvailableTemplateTitle(string title)
        {
            int count = 1;
            var numberedTitle = title + " " + count;
            while (database.GetConversation(numberedTitle) != null && count < 1000)
            {
                count++;
                numberedTitle = title + " " + count;
            }
            return numberedTitle;
        }

        /// <summary>
        /// Arranges the selected conversation's nodes as a balanced tree, then frames the result.
        /// If multiple nodes are selected, arranges only those, in place, like the IMGUI editor.
        /// </summary>
        private void ArrangeNodes(bool vertically)
        {
            var selectedIDs = new HashSet<int>(graphView.selection.OfType<DialogueEntryNode>().Select(node => node.entryID));
            var isSubset = selectedIDs.Count > 1;
            if (!ApplyArrangement(vertically, isSubset ? selectedIDs : null)) return;
            Undo.SetCurrentGroupName("Arrange Nodes");
            ScheduleGraphRebuild();
            if (!isSubset) graphView.schedule.Execute(() => graphView.FrameAll()).StartingIn(50); // After the rebuild's layout.
        }

        /// <summary>
        /// Arranges all of the conversation's nodes, if it's the selected conversation, such as
        /// after an AI addon adds entries.
        /// </summary>
        internal void ArrangeAllNodes(int conversationID, bool vertically)
        {
            var conversation = GetSelectedConversation();
            if (conversation == null || conversation.id != conversationID) return;
            if (!ApplyArrangement(vertically, null)) return;
            Undo.SetCurrentGroupName("Arrange Nodes");
            ScheduleGraphRebuild();
            graphView.schedule.Execute(() => graphView.FrameAll()).StartingIn(50); // After the rebuild's layout.
        }

        /// <summary>Moves the conversation's entries, or a subset of them, into a balanced tree.</summary>
        private bool ApplyArrangement(bool vertically, HashSet<int> subset)
        {
            var conversation = GetSelectedConversation();
            var conversationProperty = GetSelectedConversationProperty();
            if (conversation == null || conversationProperty == null) return false;
            var positions = ConversationNodeArranger.Arrange(conversation, vertically, GetNodeSize, subset);
            serializedObject.Update();
            foreach (var pair in positions)
            {
                var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, pair.Key);
                if (entryProperty != null) ConversationPropertyUtility.SetEntryPosition(entryProperty, pair.Value);
            }
            serializedObject.ApplyModifiedProperties();
            return true;
        }

        /// <summary>
        /// The node's size as shown, since Show Full Node Text can make it larger than the
        /// default size. Uses the default size if the node hasn't been laid out yet. Ignores
        /// the canvasRect's size, which the IMGUI editor lets users widen.
        /// </summary>
        private Vector2 GetNodeSize(DialogueEntry entry)
        {
            DialogueEntryNode node;
            if (nodesByID.TryGetValue(entry.id, out node) && node.layout.width > 0 && node.layout.height > 0) return node.layout.size;
            return new Vector2(DialogueEntry.CanvasRectWidth, DialogueEntry.CanvasRectHeight);
        }

        private DropdownMenuAction.Status GetPipeOptionStatus(bool isOn)
        {
            if (currentConversationID < 0) return DropdownMenuAction.Status.Disabled;
            return isOn ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }

        /// <summary>
        /// Splits the selected conversation's entries at pipe characters. This is a bulk
        /// operation, so it works on the conversation object under Undo.RecordObject.
        /// </summary>
        private void SplitPipesIntoNodes()
        {
            var conversation = GetSelectedConversation();
            if (conversation == null) return;
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(database, "Split Pipes");
            conversation.SplitPipesIntoEntries(true, conversationsState.trimWhitespaceAroundPipes, null, conversationsState.alternateActorsAtPipes);
            EditorUtility.SetDirty(database);
            serializedObject.Update();
            ScheduleGraphRebuild();
        }

        #endregion

        #region Details Pane: Graph and Inspector

        protected override VisualElement CreateDetailsPane()
        {
            var splitView = new TwoPaneSplitView(1, InspectorWidth, TwoPaneSplitViewOrientation.Horizontal);
            splitView.viewDataKey = "PixelCrushers.DialogueDatabaseEditorWindow.ConversationsPanel.InspectorSplit";
            splitView.style.flexGrow = 1;

            graphHost = new VisualElement();
            graphHost.style.flexGrow = 1;
            graphHost.Add(CreateSearchBar());
            graphView = new ConversationGraphView();
            graphView.graphViewChanged = OnGraphViewChanged;
            graphView.viewTransformChanged = view => SaveViewState();
            graphView.selectionChanged += OnGraphSelectionChanged;
            graphView.createNodeRequested = (position, parentNode) => CreateEntry(position, parentNode, graphView.isShiftDown);
            graphView.createChildRequested = node => CreateChildEntry(node.entryID, graphView.isShiftDown);
            graphView.nodeDoubleClicked = node => inspector.FocusDialogueText();
            graphView.RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);
            graphView.arrangeNodesRequested = ArrangeNodes;
            graphView.appendSharedMenuItems = AppendSharedContextMenuItems;
            graphView.appendCanvasMenuItems = PopulateMenu; // Blank-canvas right-click shows all Menu items.
            graphView.appendNodeMenuItems = AppendNodeContextMenuItems;
            graphView.createGroupRequested = CreateGroupFromSelection;
            graphView.renameGroupRequested = group =>
            {
                graphView.SelectGroupOnly(group);
                inspector.FocusGroupName();
            };
            graphView.groupDragStarted = OnGroupDragStarted;
            graphView.groupDragged = OnGroupDragged;
            graphView.groupDragFinished = OnGroupDragFinished;
            graphView.groupResizeFinished = OnGroupResizeFinished;
            graphHost.Add(graphView);
            noConversationLabel = new Label("Select a conversation.");
            noConversationLabel.style.position = Position.Absolute;
            noConversationLabel.style.left = 8;
            noConversationLabel.style.top = 6;
            noConversationLabel.style.opacity = 0.6f;
            graphHost.Add(noConversationLabel);
            graphHost.Add(CreateParticipantsWatermark());
            graphHost.schedule.Execute(PollRuntimeState).Every(RuntimePollInterval);
            splitView.Add(graphHost);

            inspectorScrollView = new ScrollView(ScrollViewMode.Vertical);
            inspectorScrollView.style.flexGrow = 1;
            inspectorScrollView.style.minWidth = 220;
            inspectorScrollView.contentContainer.style.paddingLeft = 6;
            inspectorScrollView.contentContainer.style.paddingRight = 6;
            inspectorScrollView.contentContainer.style.paddingTop = 4;
            inspectorScrollView.contentContainer.style.paddingBottom = 6;
            inspector = new ConversationInspector(this);
            inspector.RegisterCallback<KeyDownEvent>(OnInspectorKeyDown, TrickleDown.TrickleDown);
            inspectorScrollView.Add(inspector);
            splitView.Add(inspectorScrollView);
            return splitView;
        }

        protected override void RebuildDetailsContent(SerializedProperty conversationProperty)
        {
            RefreshSearchBar();
            if (conversationProperty == null)
            {
                currentConversationID = -1;
                graphView.suppressSelectionEvents = true;
                graphView.ClearGraph();
                graphView.suppressSelectionEvents = false;
                nodesByID.Clear();
                groupElements.Clear();
                graphView.style.display = DisplayStyle.None;
                noConversationLabel.style.display = DisplayStyle.Flex;
                participantsWatermark.style.display = DisplayStyle.None;
                inspector.ShowNothing();
                return;
            }
            graphView.style.display = DisplayStyle.Flex;
            noConversationLabel.style.display = DisplayStyle.None;
            participantsWatermark.style.display = DisplayStyle.Flex;

            var conversationID = ConversationPropertyUtility.GetConversationID(conversationProperty);
            var isNewConversation = conversationID != currentConversationID;
            if (isNewConversation && conversationsState.selectionConversationID != conversationID)
            {
                conversationsState.selectionConversationID = conversationID;
                conversationsState.selectedEntryIDs.Clear();
                conversationsState.selectedGroupIndices.Clear();
            }
            currentConversationID = conversationID;
            BuildGraph();
            if (isNewConversation) RestoreViewState();
            RebuildInspector();
        }

        private void BuildGraph()
        {
            isRebuildScheduled = false;
            var conversation = GetSelectedConversation();
            if (conversation == null) return;
            isRebuildingGraph = true;
            graphView.suppressSelectionEvents = true;
            // Clearing the graph removes the focused node, which would stop the keyboard shortcuts.
            var focusedElement = (graphView.focusController != null) ? graphView.focusController.focusedElement as VisualElement : null;
            var graphHadFocus = focusedElement != null && graphView.Contains(focusedElement);
            try
            {
                graphView.ClearGraph();
                nodesByID.Clear();
                actorPortraits.Clear();
                groupElements.Clear();
                for (int i = 0; i < conversation.entryGroups.Count; i++)
                {
                    var groupElement = new EntryGroupElement(i);
                    groupElement.SetPosition(conversation.entryGroups[i].rect);
                    groupElements.Add(groupElement);
                    graphView.AddElement(groupElement);
                }
                var listener = new DialogueEdgeConnectorListener(OnEdgeDroppedOutsidePort, OnExistingLinkDroppedOnPort);
                foreach (var entry in conversation.dialogueEntries)
                {
                    if (nodesByID.ContainsKey(entry.id)) continue; // Duplicate IDs: show the first one.
                    var node = new DialogueEntryNode(entry.id, listener);
                    node.SetPosition(new Rect(entry.canvasRect.position, Vector2.zero));
                    node.RegisterCallback<MouseDownEvent>(evt =>
                    {
                        if (evt.button == 0 && evt.clickCount == 2) graphView.nodeDoubleClicked?.Invoke(node);
                    });
                    nodesByID.Add(entry.id, node);
                    graphView.AddElement(node);
                }
                foreach (var entry in conversation.dialogueEntries)
                {
                    DialogueEntryNode originNode;
                    if (!nodesByID.TryGetValue(entry.id, out originNode)) continue;
                    foreach (var link in entry.outgoingLinks)
                    {
                        DialogueEntryNode destinationNode;
                        if (link.destinationConversationID != conversation.id || !nodesByID.TryGetValue(link.destinationDialogueID, out destinationNode)) continue;
                        var edge = originNode.outputPort.ConnectTo<DialogueEdge>(destinationNode.inputPort);
                        edge.SetLink(entry.id, link.destinationDialogueID);
                        graphView.AddElement(edge);
                        StyleEdge(edge, entry.id, link);
                    }
                }
                RefreshNodeContents();

                // Restore the selection by entry ID and group index.
                var selectedIDs = new List<int>(conversationsState.selectedEntryIDs);
                var selectedGroupIndices = new List<int>(conversationsState.selectedGroupIndices);
                if (pendingSelectEntryID >= 0 || pendingSelectGroupIndex >= 0)
                {
                    selectedIDs.Clear();
                    selectedGroupIndices.Clear();
                    if (pendingSelectEntryID >= 0) selectedIDs.Add(pendingSelectEntryID);
                    if (pendingSelectGroupIndex >= 0) selectedGroupIndices.Add(pendingSelectGroupIndex);
                }
                foreach (var id in selectedIDs)
                {
                    DialogueEntryNode node;
                    if (nodesByID.TryGetValue(id, out node)) graphView.AddToSelection(node);
                }
                foreach (var index in selectedGroupIndices)
                {
                    if (0 <= index && index < groupElements.Count) graphView.AddToSelection(groupElements[index]);
                }
                lastStructureSignature = GetStructureSignature(conversation);
            }
            finally
            {
                graphView.suppressSelectionEvents = false;
                isRebuildingGraph = false;
            }
            if (graphHadFocus) graphView.Focus();

            // Track changes made through the inspector or elsewhere, and refresh nodes in place.
            if (graphTracker != null) graphTracker.RemoveFromHierarchy();
            graphTracker = new VisualElement();
            graphHost.Add(graphTracker);
            graphTracker.TrackSerializedObjectValue(serializedObject, so => RefreshNodeContents());

            if (pendingSelectEntryID >= 0 || pendingSelectGroupIndex >= 0)
            {
                pendingSelectEntryID = -1;
                pendingSelectGroupIndex = -1;
                UpdateSelectionState();
            }
        }

        private const int PriorityEdgeWidth = 4;

        /// <summary>
        /// Non-Normal priority links are drawn thicker, like the IMGUI editor. DialogueEdge
        /// applies the width safely; setting EdgeControl.edgeWidth directly throws if the edge
        /// isn't in the graph yet, and Edge resets it on every update anyway. At runtime, the
        /// current entry's links are green if valid and red if not, like the IMGUI editor.
        /// </summary>
        private void StyleEdge(Edge edge, int originEntryID, Link link)
        {
            var isNormal = link.priority == ConditionPriority.Normal;
            edge.tooltip = isNormal ? null : "Priority: " + link.priority;
            var dialogueEdge = edge as DialogueEdge;
            if (dialogueEdge == null) return;
            dialogueEdge.widthOverride = isNormal ? 0 : PriorityEdgeWidth;
            dialogueEdge.colorOverride = IsRuntimeEntry(originEntryID) ? (IsValidRuntimeLink(link) ? Color.green : Color.red) : (Color?)null;
        }

        /// <summary>
        /// A string that changes when entries or links are added, removed or retargeted, used to
        /// detect structural changes made outside this panel.
        /// </summary>
        private static string GetStructureSignature(Conversation conversation)
        {
            var builder = new System.Text.StringBuilder();
            builder.Append(conversation.entryGroups.Count).Append('|');
            foreach (var entry in conversation.dialogueEntries)
            {
                builder.Append(entry.id).Append(':');
                foreach (var link in entry.outgoingLinks)
                {
                    builder.Append(link.destinationConversationID).Append('.').Append(link.destinationDialogueID).Append(',');
                }
                builder.Append(';');
            }
            return builder.ToString();
        }

        private void ScheduleGraphRebuild()
        {
            if (isRebuildScheduled) return;
            isRebuildScheduled = true;
            schedule.Execute(() =>
            {
                if (!isRebuildScheduled) return;
                BuildGraph();
                RebuildInspector();
            });
        }

        protected override void OnPollExternalChanges(SerializedProperty conversationProperty)
        {
            var conversation = GetSelectedConversation();
            if (conversation == null || isRebuildScheduled) return;
            if (GetStructureSignature(conversation) != lastStructureSignature) ScheduleGraphRebuild();
        }

        #endregion

        #region Runtime

        /// <summary>
        /// At runtime, tracks the active conversation's state, like the IMGUI editor's
        /// UpdateRuntimeConversationsTab. The conversation model creates a new state for each
        /// line, so the nodes and edges only refresh when the reference changes.
        /// </summary>
        private void PollRuntimeState()
        {
            var newState = (Application.isPlaying && DialogueManager.isConversationActive)
                ? DialogueManager.currentConversationState : null;
            if (newState == runtimeState) return;
            runtimeState = newState;
            var entry = (runtimeState != null && runtimeState.subtitle != null) ? runtimeState.subtitle.dialogueEntry : null;
            runtimeEntry = (entry != null && IsRuntimeConversationFromEditedDatabase(entry.conversationID)) ? entry : null;
            if (currentConversationID >= 0) RefreshNodeContents();
        }

        /// <summary>
        /// True if the Dialogue Manager has loaded the edited database and the active
        /// conversation came from it: same ID and Title in the edited database.
        /// </summary>
        private bool IsRuntimeConversationFromEditedDatabase(int conversationID)
        {
            if (!IsEditedDatabaseLoaded()) return false;
            var runtimeConversation = DialogueManager.masterDatabase.GetConversation(conversationID);
            var editedConversation = database.GetConversation(conversationID);
            return runtimeConversation != null && editedConversation != null &&
                runtimeConversation.Title == editedConversation.Title;
        }

        /// <summary>
        /// True if the Dialogue Manager has loaded the edited database. With the Dialogue
        /// Manager's Instantiate Database option (the default), loadedDatabases holds
        /// Instantiate() copies, which have no link to their asset, so a copy with the same
        /// name (ignoring Unity's "(Clone)" suffix) also counts.
        /// </summary>
        private bool IsEditedDatabaseLoaded()
        {
            if (database == null || !DialogueManager.hasInstance) return false;
            var databaseManager = DialogueManager.databaseManager;
            if (databaseManager == null) return false;
            foreach (var loadedDatabase in databaseManager.loadedDatabases)
            {
                if (loadedDatabase == null) continue;
                if (loadedDatabase == database) return true;
                var loadedName = loadedDatabase.name;
                if (loadedName.EndsWith("(Clone)")) loadedName = loadedName.Substring(0, loadedName.Length - "(Clone)".Length).TrimEnd();
                if (loadedName == database.name) return true;
            }
            return false;
        }

        /// <summary>True if the entry in the shown conversation is the current runtime entry.</summary>
        private bool IsRuntimeEntry(int entryID)
        {
            return runtimeEntry != null && runtimeEntry.conversationID == currentConversationID && runtimeEntry.id == entryID;
        }

        /// <summary>
        /// True if the current runtime state offers the link's destination as a response,
        /// meaning its Conditions are true or blank. Like the IMGUI editor, a valid link that a
        /// higher priority link overrides isn't offered, so it counts as invalid.
        /// </summary>
        private bool IsValidRuntimeLink(Link link)
        {
            return runtimeState != null &&
                (ContainsResponse(runtimeState.pcResponses, link) || ContainsResponse(runtimeState.npcResponses, link));
        }

        private static bool ContainsResponse(Response[] responses, Link link)
        {
            if (responses == null) return false;
            foreach (var response in responses)
            {
                var destination = (response != null) ? response.destinationEntry : null;
                if (destination != null && destination.conversationID == link.destinationConversationID &&
                    destination.id == link.destinationDialogueID)
                {
                    return true;
                }
            }
            return false;
        }

        private void AppendRuntimeMenuItems(DropdownMenu menu)
        {
            menu.AppendAction("Go to Current Runtime Entry", a => GoToCurrentRuntimeEntry(),
                a => (runtimeEntry != null) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        /// <summary>
        /// Shows the active conversation, then selects and frames its current entry, like the
        /// IMGUI editor's GotoCurrentRuntimeEntry.
        /// </summary>
        private void GoToCurrentRuntimeEntry()
        {
            PollRuntimeState(); // The last poll may be out of date.
            if (runtimeEntry == null) return;
            SelectAndFrameEntry(runtimeEntry.conversationID, runtimeEntry.id);
        }

        /// <summary>
        /// Like the IMGUI editor's PlayConversationFromEntry: if the Dialogue Manager is running a
        /// conversation from the edited database, jumps it to the entry in the shown conversation.
        /// Otherwise opens the Edit Mode Player window on the entry.
        /// </summary>
        private void PlayConversationFromEntry(int entryID)
        {
            if (database == null || currentConversationID < 0) return;
            if (serializedObject != null) serializedObject.ApplyModifiedProperties(); // So the player sees pending edits.
            if (Application.isPlaying && DialogueManager.isConversationActive &&
                IsRuntimeConversationFromEditedDatabase(currentConversationID))
            {
                var entry = DialogueManager.masterDatabase.GetDialogueEntry(currentConversationID, entryID);
                if (entry != null)
                {
                    DialogueManager.conversationController.GotoState(DialogueManager.conversationModel.GetState(entry));
                    return;
                }
            }
            EditModePlayerWindow.Open(database, currentConversationID, entryID);
        }

        /// <summary>
        /// Shows a conversation, then selects and frames one of its entries. If entryID is -1,
        /// only shows the conversation.
        /// </summary>
        public void SelectAndFrameEntry(int conversationID, int entryID)
        {
            if (database == null) return;
            if (conversationID != currentConversationID)
            {
                var index = database.conversations.FindIndex(conversation => conversation.id == conversationID);
                if (index < 0) return;
                pendingSelectEntryID = entryID;
                SelectAssetAtIndex(index);
                if (currentConversationID != conversationID)
                {
                    pendingSelectEntryID = -1;
                    return;
                }
            }
            else
            {
                SelectEntry(entryID, false);
            }
            // After the rebuild's layout and RestoreViewState's framing.
            graphView.schedule.Execute(() =>
            {
                DialogueEntryNode node;
                if (currentConversationID == conversationID && nodesByID.TryGetValue(entryID, out node)) graphView.FrameNode(node);
            }).StartingIn(50);
        }

        #endregion

        #region Node Content

        /// <summary>The default node colors (EditorTools.NodeColor_*) differ between themes.</summary>
        public override void OnThemeChanged()
        {
            RefreshNodeContents();
        }

        /// <summary>Updates node titles, colors, icons and badges from the data, without rebuilding.</summary>
        private void RefreshNodeContents()
        {
            var conversation = GetSelectedConversation();
            if (conversation == null || conversation.id != currentConversationID) return;
            RefreshParticipantsWatermark(conversation);
            for (int i = 0; i < groupElements.Count && i < conversation.entryGroups.Count; i++)
            {
                groupElements[i].SetContent(conversation.entryGroups[i].name, conversation.entryGroups[i].color);
            }
            var incoming = GetIncomingExternalLinks(conversation);
            var linked = GetLinkedEntries(conversation, incoming.Keys);
            foreach (var entry in conversation.dialogueEntries)
            {
                DialogueEntryNode node;
                if (!nodesByID.TryGetValue(entry.id, out node)) continue;
                var externalLinks = entry.outgoingLinks.Count(link => link.destinationConversationID != conversation.id);
                List<KeyValuePair<Conversation, DialogueEntry>> incomingSources;
                incoming.TryGetValue(entry.id, out incomingSources);
                var incomingCount = (incomingSources != null) ? incomingSources.Count : 0;
                var isOrphan = !linked.Contains(entry.id);
                var tooltipText = GetNodeTooltip(entry) +
                    (isOrphan ? "\n(No incoming links.)" : string.Empty) +
                    GetIncomingLinksTooltip(incomingSources) +
                    (IsRuntimeEntry(entry.id) ? "\n(Current runtime entry.)" : string.Empty);
                var text = entry.isGroup ? "{group} " + GetEntryText(entry) : GetEntryText(entry);
                // Like the IMGUI editor's BuildDialogueEntryNodeText.
                if (entry.outgoingLinks == null || entry.outgoingLinks.Count == 0) text += " [END]";
                var actorText = GetActorText(conversation, entry);
                if (conversationsState.showNodeIDs)
                {
                    // The ID starts the node's first line, like the IMGUI editor.
                    var idText = string.Format("[{0}] ", entry.id);
                    if (actorText != null) actorText = idText + actorText;
                    else text = idText + text;
                }
                node.SetPortrait(conversationsState.showActorPortraits ? GetActorPortrait(entry.ActorID) : null);
                node.SetContent(text, actorText, conversationsState.showFullNodeText, GetNodeColor(entry, isOrphan),
                    HasSequence(entry), !string.IsNullOrEmpty(entry.conditionsString), !string.IsNullOrEmpty(entry.userScript),
                    HasEvent(entry), externalLinks, incomingCount, tooltipText);
            }

            // Link priorities can change in the inspector.
            foreach (var edge in graphView.edges.ToList())
            {
                var origin = (edge.output != null) ? edge.output.node as DialogueEntryNode : null;
                var destination = (edge.input != null) ? edge.input.node as DialogueEntryNode : null;
                var originEntry = (origin != null) ? conversation.dialogueEntries.Find(e => e.id == origin.entryID) : null;
                var link = (originEntry != null && destination != null)
                    ? originEntry.outgoingLinks.Find(l => l.destinationConversationID == conversation.id && l.destinationDialogueID == destination.entryID) : null;
                if (link != null)
                {
                    StyleEdge(edge, originEntry.id, link);
                    var dialogueEdge = edge as DialogueEdge;
                    if (dialogueEdge != null) dialogueEdge.SetOrderText(GetLinkOrderText(originEntry, link));
                }
            }
        }

        /// <summary>The link's number among the entry's links, if it has more than one, like the IMGUI editor.</summary>
        private string GetLinkOrderText(DialogueEntry entry, Link link)
        {
            if (!conversationsState.showLinkOrderOnArrows || entry.outgoingLinks.Count <= 1) return null;
            return (entry.outgoingLinks.IndexOf(link) + 1).ToString();
        }

        /// <summary>
        /// Portraits are cached because getting one can create a sprite. BuildGraph clears the
        /// cache, so a changed portrait shows up on the next rebuild.
        /// </summary>
        private Sprite GetActorPortrait(int actorID)
        {
            Sprite portrait;
            if (!actorPortraits.TryGetValue(actorID, out portrait))
            {
                var actor = database.GetActor(actorID);
                portrait = (actor != null) ? actor.GetPortraitSprite(1) : null;
                actorPortraits.Add(actorID, portrait);
            }
            return portrait;
        }

        /// <summary>
        /// Large text in the canvas's lower right corner showing the conversation's participants,
        /// like the IMGUI editor's DrawParticipantsOnCanvas. It doesn't block graph clicks.
        /// </summary>
        private VisualElement CreateParticipantsWatermark()
        {
            participantsWatermark = new VisualElement();
            participantsWatermark.pickingMode = PickingMode.Ignore;
            participantsWatermark.style.position = Position.Absolute;
            participantsWatermark.style.right = 4;
            participantsWatermark.style.bottom = 2;
            participantsWatermark.style.alignItems = Align.FlexEnd;
            participantsWatermark.style.display = DisplayStyle.None;
            actorWatermarkLabel = CreateParticipantWatermarkLabel();
            conversantWatermarkLabel = CreateParticipantWatermarkLabel();
            participantsWatermark.Add(actorWatermarkLabel);
            participantsWatermark.Add(conversantWatermarkLabel);
            return participantsWatermark;
        }

        private static Label CreateParticipantWatermarkLabel()
        {
            var label = new Label();
            label.pickingMode = PickingMode.Ignore;
            label.enableRichText = false;
            label.style.fontSize = 20;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.AddToClassList("ds-watermark");
            return label;
        }

        private void RefreshParticipantsWatermark(Conversation conversation)
        {
            SetParticipantWatermark(actorWatermarkLabel, "Actor", conversation.ActorID);
            SetParticipantWatermark(conversantWatermarkLabel, "Conversant", conversation.ConversantID);
        }

        private void SetParticipantWatermark(Label label, string role, int actorID)
        {
            var actor = database.GetActor(actorID);
            label.text = string.Format("{0}: {1}", role, (actor != null) ? actor.Name : "unassigned");
            label.EnableInClassList("ds-watermark--missing", actor == null);
        }

        /// <summary>
        /// "Name:" if Show All Actor Names is set or the entry's actor is neither of the
        /// conversation's participants, like the IMGUI editor. Otherwise null.
        /// </summary>
        private string GetActorText(Conversation conversation, DialogueEntry entry)
        {
            var actorID = entry.ActorID;
            var isPrimary = actorID == conversation.ActorID || actorID == conversation.ConversantID;
            if (isPrimary && !conversationsState.showAllActorNames) return null;
            var actor = database.GetActor(actorID);
            return (actor != null) ? actor.Name + ":" : null;
        }

        /// <summary>Tooltip with the ID, speaker and full text, since the node shows only one line.</summary>
        private string GetNodeTooltip(DialogueEntry entry)
        {
            var actor = database.actors.Find(a => a.id == entry.ActorID);
            var actorName = (actor != null) ? actor.Name : "(no actor)";
            return string.Format("[{0}] {1}{2}: {3}", entry.id, entry.isGroup ? "{group} " : string.Empty, actorName, GetEntryText(entry));
        }

        /// <summary>
        /// The entry's display text: Menu Text, else Dialogue Text, else its Title in angle
        /// brackets, like the IMGUI editor's BuildDialogueEntryNodeText.
        /// </summary>
        internal static string GetEntryText(DialogueEntry entry)
        {
            if (entry == null) return string.Empty;
            var text = entry.MenuText;
            if (string.IsNullOrEmpty(text)) text = entry.DialogueText;
            if (string.IsNullOrEmpty(text)) text = string.IsNullOrEmpty(entry.Title) ? string.Empty : "<" + entry.Title + ">";
            return text.Replace("\n", " ");
        }

        private static bool HasSequence(DialogueEntry entry)
        {
            var sequence = entry.Sequence;
            return !string.IsNullOrEmpty(sequence) && !(entry.id == 0 && (sequence == "None()" || sequence == "Continue()"));
        }

        private static bool HasEvent(DialogueEntry entry)
        {
            return (entry.onExecute != null && entry.onExecute.GetPersistentEventCount() > 0) || !string.IsNullOrEmpty(entry.sceneEventGuid);
        }

        /// <summary>
        /// Node header color, following the IMGUI editor's GetNodeStyle: START orange, orphans
        /// red, then the actor's custom NodeColor, then blue for players and gray for NPCs.
        /// The current runtime entry is green, ahead of all of these. An orphan is an entry other
        /// than START with no incoming links from this or any other conversation. Its children
        /// aren't orphans, even though they're unreachable too.
        /// </summary>
        private Color GetNodeColor(DialogueEntry entry, bool isOrphan)
        {
            if (IsRuntimeEntry(entry.id)) return EditorTools.NodeColor_Green;
            if (entry.id == ConversationPropertyUtility.StartEntryID) return EditorTools.NodeColor_Orange;
            if (isOrphan) return EditorTools.NodeColor_Red;
            var actor = database.actors.Find(a => a.id == entry.ActorID);
            if (actor != null)
            {
                var nodeColor = actor.LookupValue("NodeColor");
                if (!string.IsNullOrEmpty(nodeColor))
                {
                    var color = EditorTools.NodeColorStringToColor(nodeColor);
                    color.a = 1;
                    if (color.r > 0 || color.g > 0 || color.b > 0) return color;
                }
                if (actor.IsPlayer) return EditorTools.NodeColor_Blue;
            }
            return EditorTools.NodeColor_Gray;
        }

        /// <summary>
        /// Links from other conversations into this one, by destination entry ID, with each
        /// link's source conversation and entry. Scans every link in the database, so it's always
        /// current; that's cheap compared to refreshing the nodes.
        /// </summary>
        private Dictionary<int, List<KeyValuePair<Conversation, DialogueEntry>>> GetIncomingExternalLinks(Conversation conversation)
        {
            var incoming = new Dictionary<int, List<KeyValuePair<Conversation, DialogueEntry>>>();
            if (database == null) return incoming;
            foreach (var otherConversation in database.conversations)
            {
                if (otherConversation == null || otherConversation.id == conversation.id) continue;
                foreach (var entry in otherConversation.dialogueEntries)
                {
                    if (entry == null || entry.outgoingLinks == null) continue;
                    foreach (var link in entry.outgoingLinks)
                    {
                        if (link == null || link.destinationConversationID != conversation.id) continue;
                        List<KeyValuePair<Conversation, DialogueEntry>> sources;
                        if (!incoming.TryGetValue(link.destinationDialogueID, out sources))
                        {
                            sources = new List<KeyValuePair<Conversation, DialogueEntry>>();
                            incoming.Add(link.destinationDialogueID, sources);
                        }
                        sources.Add(new KeyValuePair<Conversation, DialogueEntry>(otherConversation, entry));
                    }
                }
            }
            return incoming;
        }

        private const int MaxIncomingLinksInTooltip = 10;

        private static string GetIncomingLinksTooltip(List<KeyValuePair<Conversation, DialogueEntry>> sources)
        {
            if (sources == null || sources.Count == 0) return string.Empty;
            var builder = new System.Text.StringBuilder("\nLinked from other conversations:");
            for (int i = 0; i < sources.Count && i < MaxIncomingLinksInTooltip; i++)
            {
                builder.Append("\n  ").Append(GetIncomingLinkLabel(sources[i]));
            }
            if (sources.Count > MaxIncomingLinksInTooltip)
            {
                builder.Append("\n  …and ").Append(sources.Count - MaxIncomingLinksInTooltip).Append(" more");
            }
            return builder.ToString();
        }

        private static string GetIncomingLinkLabel(KeyValuePair<Conversation, DialogueEntry> source)
        {
            return string.Format("{0} [{1}] {2}", source.Key.Title, source.Value.id, GetEntryText(source.Value));
        }

        /// <summary>
        /// Node context menu items: Play From Here, and items that go to the entries in other
        /// conversations that link to this node.
        /// </summary>
        private void AppendNodeContextMenuItems(DialogueEntryNode node, DropdownMenu menu)
        {
            var conversation = GetSelectedConversation();
            if (conversation == null || conversation.id != currentConversationID) return;
            var playEntryID = node.entryID;
            menu.AppendAction("Play From Here...", a => PlayConversationFromEntry(playEntryID));
            List<KeyValuePair<Conversation, DialogueEntry>> sources;
            if (!GetIncomingExternalLinks(conversation).TryGetValue(node.entryID, out sources)) return;
            foreach (var source in sources)
            {
                var conversationID = source.Key.id;
                var entryID = source.Value.id;
                // Slashes would make submenus.
                var label = string.Format("{0} [{1}]", source.Key.Title, entryID).Replace('/', '∕');
                menu.AppendAction("Go To Incoming Link/" + label, a => SelectAndFrameEntry(conversationID, entryID));
            }
        }

        /// <summary>
        /// START, the entries in externallyLinked (entries that other conversations link to),
        /// and every entry that another entry in this conversation links to. A link from an
        /// entry to itself doesn't count.
        /// </summary>
        private static HashSet<int> GetLinkedEntries(Conversation conversation, IEnumerable<int> externallyLinked)
        {
            var linked = new HashSet<int>();
            linked.Add(ConversationPropertyUtility.StartEntryID);
            if (externallyLinked != null) linked.UnionWith(externallyLinked);
            foreach (var entry in conversation.dialogueEntries)
            {
                if (entry == null || entry.outgoingLinks == null) continue;
                foreach (var link in entry.outgoingLinks)
                {
                    if (link != null && link.destinationConversationID == conversation.id && link.destinationDialogueID != entry.id)
                    {
                        linked.Add(link.destinationDialogueID);
                    }
                }
            }
            return linked;
        }

        #endregion

        #region Graph Changes

        private GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (isRebuildingGraph || serializedObject == null) return change;
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null) return change;
            serializedObject.Update();
            string undoName = null;
            var needsRebuild = false;

            // GraphView deletes an existing edge at the end of a drag. Keep the link instead.
            // The drop callbacks handle a new destination, and the rebuild redraws the edge.
            if (change.elementsToRemove != null &&
                change.elementsToRemove.RemoveAll(element => element is DialogueEdge edge && edge.isBeingDragged) > 0)
            {
                ScheduleGraphRebuild();
            }

            if (change.elementsToRemove != null && change.elementsToRemove.Count > 0)
            {
                // Never delete START. GraphView also lists the edges of deleted nodes, so keep an
                // edge only if it was selected itself or one of its nodes is really being deleted.
                change.elementsToRemove.RemoveAll(element => element is DialogueEntryNode node && node.isStart);
                var removedEntryIDs = new HashSet<int>(change.elementsToRemove.OfType<DialogueEntryNode>().Select(node => node.entryID));
                if (!ConfirmDeletion(change.elementsToRemove, removedEntryIDs.Count))
                {
                    change.elementsToRemove.Clear();
                    return change;
                }
                foreach (var edge in change.elementsToRemove.OfType<Edge>().ToList())
                {
                    var origin = edge.output != null ? edge.output.node as DialogueEntryNode : null;
                    var destination = edge.input != null ? edge.input.node as DialogueEntryNode : null;
                    if (origin == null || destination == null) continue;
                    var endpointDeleted = removedEntryIDs.Contains(origin.entryID) || removedEntryIDs.Contains(destination.entryID);
                    if (!endpointDeleted && !edge.selected)
                    {
                        change.elementsToRemove.Remove(edge);
                        continue;
                    }
                    var originProperty = ConversationPropertyUtility.FindEntry(conversationProperty, origin.entryID);
                    if (originProperty != null) ConversationPropertyUtility.RemoveLink(originProperty, currentConversationID, destination.entryID);
                }
                var removedGroupIndices = change.elementsToRemove.OfType<EntryGroupElement>().Select(group => group.groupIndex).ToList();
                if (removedGroupIndices.Count > 0)
                {
                    ConversationPropertyUtility.DeleteEntryGroups(conversationProperty, removedGroupIndices);
                    conversationsState.selectedGroupIndices.Clear();
                    needsRebuild = true; // Group indices shifted.
                    undoName = "Delete Group";
                }
                if (removedEntryIDs.Count > 0)
                {
                    ConversationPropertyUtility.DeleteEntries(conversationProperty, removedEntryIDs);
                    conversationsState.selectedEntryIDs.RemoveAll(removedEntryIDs.Contains);
                    needsRebuild = true; // Array indices shifted.
                    undoName = "Delete Dialogue Entries";
                }
                else if (undoName == null)
                {
                    undoName = "Delete Links";
                }
            }

            if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
            {
                foreach (var edge in change.edgesToCreate.ToList())
                {
                    var origin = edge.output.node as DialogueEntryNode;
                    var destination = edge.input.node as DialogueEntryNode;
                    var originProperty = (origin != null) ? ConversationPropertyUtility.FindEntry(conversationProperty, origin.entryID) : null;
                    if (originProperty == null || destination == null ||
                        ConversationPropertyUtility.AddLink(originProperty, currentConversationID, destination.entryID) == null)
                    {
                        change.edgesToCreate.Remove(edge); // Duplicate link.
                    }
                }
                undoName = undoName ?? "Add Link";
            }

            if (change.movedElements != null && change.movedElements.Count > 0)
            {
                foreach (var node in change.movedElements.OfType<DialogueEntryNode>())
                {
                    var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, node.entryID);
                    if (entryProperty != null) ConversationPropertyUtility.SetEntryPosition(entryProperty, node.GetCanvasPosition());
                }
                undoName = undoName ?? "Move Dialogue Entries";
            }

            serializedObject.ApplyModifiedProperties();
            if (undoName != null) Undo.SetCurrentGroupName(undoName);
            var conversation = GetSelectedConversation();
            if (conversation != null) lastStructureSignature = GetStructureSignature(conversation);
            if (needsRebuild) ScheduleGraphRebuild();
            else schedule.Execute(RefreshNodeContents); // Orphan colors depend on links.
            return change;
        }

        /// <summary>
        /// Asks before deleting nodes or links if Confirm Node and Link Deletion is set, like the
        /// IMGUI editor. Node groups don't ask. Edges that GraphView lists only because of a
        /// deleted node don't add to the question.
        /// </summary>
        private bool ConfirmDeletion(List<GraphElement> elements, int entryCount)
        {
            if (!conversationsState.confirmNodeAndLinkDeletion) return true;
            string title;
            if (entryCount > 1) title = "Delete selected entries?";
            else if (entryCount == 1) title = "Delete selected entry?";
            else if (elements.OfType<Edge>().Any(edge => edge.selected)) title = "Delete selected link?";
            else return true;
            return EditorUtility.DisplayDialog(title, "You can restore it with Undo.", "Delete", "Cancel");
        }

        private void OnEdgeDroppedOutsidePort(Edge edge, Vector2 worldPosition)
        {
            var target = graphView.GetNodeAt(worldPosition);

            // An existing link dropped on a node moves there. Anywhere else, it stays as it was.
            var dialogueEdge = edge as DialogueEdge;
            if (dialogueEdge != null && dialogueEdge.isExistingLink)
            {
                if (target == null) return;
                if (dialogueEdge.isOriginEndDragged) MoveLink(dialogueEdge, target.entryID, dialogueEdge.destinationEntryID);
                else MoveLink(dialogueEdge, dialogueEdge.originEntryID, target.entryID);
                return;
            }

            // Dragging from an entry's output onto another node links to it. Dragging onto empty
            // canvas creates a linked child there. Dropping back on the origin does nothing.
            var origin = (edge.output != null) ? edge.output.node as DialogueEntryNode : null;
            if (origin == null) return;
            if (target == null)
            {
                CreateEntry(graphView.contentViewContainer.WorldToLocal(worldPosition), origin, graphView.isShiftDown);
            }
            else if (target != origin)
            {
                LinkEntries(origin.entryID, target.entryID);
            }
        }

        private void OnExistingLinkDroppedOnPort(DialogueEdge edge)
        {
            var origin = edge.output.node as DialogueEntryNode;
            var destination = edge.input.node as DialogueEntryNode;
            if (origin != null && destination != null) MoveLink(edge, origin.entryID, destination.entryID);
        }

        private void MoveLink(DialogueEdge edge, int newOriginID, int newDestinationID)
        {
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null || newOriginID == newDestinationID) return;
            if (newOriginID == edge.originEntryID && newDestinationID == edge.destinationEntryID) return;
            serializedObject.Update();
            if (!ConversationPropertyUtility.MoveLink(conversationProperty, edge.originEntryID, edge.destinationEntryID, newOriginID, newDestinationID)) return;
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Move Link");
        }

        private void LinkEntries(int originEntryID, int destinationEntryID)
        {
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null) return;
            serializedObject.Update();
            var originProperty = ConversationPropertyUtility.FindEntry(conversationProperty, originEntryID);
            if (originProperty == null ||
                ConversationPropertyUtility.AddLink(originProperty, currentConversationID, destinationEntryID) == null) return; // Duplicate link.
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add Link");
            ScheduleGraphRebuild();
        }

        /// <summary>
        /// Creates an entry linked from parentNode (or the single selected node, or START) and
        /// selects it. The new entry's actor and conversant are the parent's, swapped, like the
        /// IMGUI editor's LinkToNewEntry.
        /// </summary>
        /// <param name="useSameActors">Keep the parent's actor and conversant instead of swapping them.</param>
        private void CreateEntry(Vector2 position, DialogueEntryNode parentNode, bool useSameActors)
        {
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null) return;
            if (parentNode == null)
            {
                var selectedNodes = graphView.selection.OfType<DialogueEntryNode>().ToList();
                parentNode = (selectedNodes.Count == 1) ? selectedNodes[0] : null;
                if (parentNode == null) nodesByID.TryGetValue(ConversationPropertyUtility.StartEntryID, out parentNode);
            }
            var parentEntry = (parentNode != null) ? GetEntryObject(parentNode.entryID) : null;
            var conversation = GetSelectedConversation();
            int actorID, conversantID;
            if (parentEntry != null && useSameActors)
            {
                actorID = parentEntry.ActorID;
                conversantID = parentEntry.ConversantID;
            }
            else if (parentEntry != null)
            {
                actorID = parentEntry.ConversantID;
                conversantID = (parentEntry.ActorID == parentEntry.ConversantID) ? database.playerID : parentEntry.ActorID;
            }
            else
            {
                actorID = conversation.ConversantID;
                conversantID = conversation.ActorID;
            }

            serializedObject.Update();
            var rect = new Rect(position, new Vector2(DialogueEntry.CanvasRectWidth, DialogueEntry.CanvasRectHeight));
            var entryProperty = ConversationPropertyUtility.AddEntry(conversationProperty, window.Template, string.Empty, rect, actorID, conversantID);
            var newEntryID = ConversationPropertyUtility.GetEntryID(entryProperty);
            if (parentNode != null)
            {
                var parentProperty = ConversationPropertyUtility.FindEntry(conversationProperty, parentNode.entryID);
                if (parentProperty != null) ConversationPropertyUtility.AddLink(parentProperty, currentConversationID, newEntryID);
            }
            serializedObject.ApplyModifiedProperties();
            if (conversationsState.autoArrangeOnCreate) ApplyArrangement(!conversationsState.addNewNodesToRight, null);
            Undo.SetCurrentGroupName("Create Dialogue Entry");

            pendingSelectEntryID = newEntryID;
            ScheduleGraphRebuild();
        }

        #endregion

        #region Keyboard Shortcuts

        private const float HomeViewMargin = 20;

        /// <summary>
        /// Handles the node editor's keyboard shortcuts. They only apply while the graph has
        /// focus, so they don't interfere with typing in the inspector or search bar.
        /// </summary>
        private void OnGraphKeyDown(KeyDownEvent evt)
        {
            if (currentConversationID < 0) return;
            var isCommand = IsCommandKey(evt);
            var isEnter = evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter;
            var isHandled = true;
            if (evt.keyCode == KeyCode.Home) ScrollToLeftmostAndTopmostNodes();
            else if (evt.keyCode == KeyCode.PageUp) graphView.PanBy(new Vector2(0, graphView.layout.height / 2));
            else if (evt.keyCode == KeyCode.PageDown) graphView.PanBy(new Vector2(0, -graphView.layout.height / 2));
            else if (isEnter && evt.modifiers == EventModifiers.None && GetSelectedNodes().Count == 1) inspector.FocusDialogueText();
            else if (isCommand && evt.keyCode == KeyCode.C) CopySelectedEntryText();
            else if (isCommand && evt.keyCode == KeyCode.V) PasteDialogueText();
            else isHandled = RunEntryShortcut(evt);
            if (isHandled) evt.StopImmediatePropagation();
        }

        /// <summary>
        /// Handles shortcuts that work from the inspector's entry section as well as the graph.
        /// Focus moves to the graph, since the shortcut changes the selection and the inspector
        /// rebuilds, which would otherwise leave nothing focused to receive the next shortcut.
        /// </summary>
        private void OnInspectorKeyDown(KeyDownEvent evt)
        {
            if (currentConversationID < 0 || GetSelectedNodes().Count == 0 || !RunEntryShortcut(evt)) return;
            graphView.Focus();
            evt.StopImmediatePropagation();
        }

        private static bool IsCommandKey(KeyDownEvent evt)
        {
            return (evt.ctrlKey || evt.commandKey) && evt.altKey;
        }

        /// <summary>Runs the shortcut that acts on the selected entries, if the key is one.</summary>
        private bool RunEntryShortcut(KeyDownEvent evt)
        {
            if (!IsCommandKey(evt)) return false;
            if (evt.keyCode == KeyCode.D) DuplicateSelectedEntries();
            else if (evt.keyCode == KeyCode.N || evt.keyCode == KeyCode.A) CreateEntryFromShortcut(evt.shiftKey);
            else return false;
            return true;
        }

        private List<DialogueEntryNode> GetSelectedNodes()
        {
            return graphView.selection.OfType<DialogueEntryNode>().ToList();
        }

        private void ScrollToLeftmostAndTopmostNodes()
        {
            if (nodesByID.Count == 0) return;
            var viewWidth = (graphView.layout.width - 2 * HomeViewMargin) / graphView.contentViewContainer.resolvedStyle.scale.value.x;
            graphView.ScrollToTopLeft(GetHomePosition(nodesByID.Values.Select(GetNodeRect).ToList(), viewWidth), HomeViewMargin);
        }

        /// <summary>
        /// Returns the content position to put at the view's top left corner: the leftmost
        /// node's left edge and the topmost node's top edge. If the view is too narrow to show
        /// both nodes, it moves right just enough to keep the topmost node, which is usually START.
        /// </summary>
        internal static Vector2 GetHomePosition(List<Rect> nodeRects, float viewWidth)
        {
            var topmost = nodeRects.OrderBy(rect => rect.yMin).First();
            var left = nodeRects.Min(rect => rect.xMin);
            if (topmost.xMax - left > viewWidth) left = topmost.xMax - viewWidth;
            return new Vector2(left, topmost.yMin);
        }

        /// <summary>
        /// Duplicates the selected entries and selects the copies, like the IMGUI editor.
        /// START is left out, since a conversation has only one.
        /// </summary>
        private void DuplicateSelectedEntries()
        {
            var conversationProperty = GetSelectedConversationProperty();
            var sources = GetSelectedNodes().Where(node => !node.isStart).Select(node => GetEntryObject(node.entryID)).Where(entry => entry != null).ToList();
            if (conversationProperty == null || sources.Count == 0) return;
            serializedObject.Update();
            var newIDs = new List<int>();
            foreach (var source in sources)
            {
                var entryProperty = ConversationPropertyUtility.AddEntryCopy(conversationProperty, source);
                FieldPropertyUtility.ApplyTemplate(entryProperty.FindPropertyRelative("fields"), window.Template.dialogueEntryFields);
                newIDs.Add(ConversationPropertyUtility.GetEntryID(entryProperty));
            }
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Duplicate Dialogue Entry");
            conversationsState.selectedEntryIDs = newIDs;
            conversationsState.selectedGroupIndices.Clear();
            ScheduleGraphRebuild();
        }

        /// <summary>Creates a child of the selected node, or of START if nothing is selected.</summary>
        private void CreateEntryFromShortcut(bool useSameActors)
        {
            var selectedNode = GetSelectedNodes().FirstOrDefault();
            CreateChildEntry((selectedNode != null) ? selectedNode.entryID : ConversationPropertyUtility.StartEntryID, useSameActors);
        }

        private void CopySelectedEntryText()
        {
            var selectedNode = GetSelectedNodes().FirstOrDefault();
            var entry = (selectedNode != null) ? GetEntryObject(selectedNode.entryID) : null;
            if (entry == null) return;
            var text = string.IsNullOrEmpty(entry.DialogueText) ? entry.MenuText : entry.DialogueText;
            if (!string.IsNullOrEmpty(text)) EditorGUIUtility.systemCopyBuffer = text;
        }

        private void PasteDialogueText()
        {
            var text = EditorGUIUtility.systemCopyBuffer;
            var conversationProperty = GetSelectedConversationProperty();
            var selectedNodes = GetSelectedNodes();
            if (string.IsNullOrEmpty(text) || conversationProperty == null || selectedNodes.Count == 0) return;
            serializedObject.Update();
            foreach (var node in selectedNodes)
            {
                var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, node.entryID);
                if (entryProperty != null) FieldPropertyUtility.SetFieldValue(entryProperty.FindPropertyRelative("fields"), "Dialogue Text", text, FieldType.Text, false);
            }
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Paste Dialogue Text");
            RebuildInspector(); // The multi-node inspector's fields aren't bound.
        }

        #endregion

        #region Search Bar

        /// <summary>
        /// Bar above the graph that searches the current conversation's entries, like the IMGUI
        /// editor's search bar. Enter finds the next match, and Shift+Enter the previous one.
        /// </summary>
        private VisualElement CreateSearchBar()
        {
            searchBar = new UnityEditor.UIElements.Toolbar();
            searchBar.style.flexShrink = 0;
            searchBar.style.display = DisplayStyle.None;

            searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.style.flexShrink = 1;
            searchField.style.width = StyleKeyword.Auto;
            searchField.RegisterValueChangedCallback(evt =>
            {
                conversationsState.searchText = evt.newValue;
                UpdateSearchButtons();
            });
            searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                SearchEntries(evt.shiftKey ? -1 : 1);
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            searchBar.Add(searchField);

            searchStatusLabel = new Label("No matches");
            searchStatusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            searchStatusLabel.style.marginLeft = 4;
            searchStatusLabel.style.marginRight = 4;
            searchStatusLabel.style.display = DisplayStyle.None;
            searchBar.Add(searchStatusLabel);

            searchCaseToggle = new ToolbarToggle() { text = "Aa", tooltip = "Case-sensitive" };
            searchCaseToggle.RegisterValueChangedCallback(evt =>
            {
                conversationsState.searchCaseSensitive = evt.newValue;
                searchStatusLabel.style.display = DisplayStyle.None;
            });
            searchBar.Add(searchCaseToggle);

            searchNextButton = new ToolbarButton(() => SearchEntries(1)) { text = "↓", tooltip = "Find next (Enter)" };
            searchBar.Add(searchNextButton);
            searchPreviousButton = new ToolbarButton(() => SearchEntries(-1)) { text = "↑", tooltip = "Find previous (Shift+Enter)" };
            searchBar.Add(searchPreviousButton);
            searchBar.Add(new ToolbarButton(() => SetSearchBarOpen(false)) { text = "X", tooltip = "Close the search bar." });
            return searchBar;
        }

        private void SetSearchBarOpen(bool isOpen)
        {
            conversationsState.isSearchBarOpen = isOpen;
            RefreshSearchBar();
            if (isOpen) searchField.schedule.Execute(() => searchField.Q<TextField>()?.Focus()).StartingIn(50); // After the menu closes.
        }

        /// <summary>Makes the search bar match the panel's saved state.</summary>
        private void RefreshSearchBar()
        {
            searchBar.style.display = conversationsState.isSearchBarOpen ? DisplayStyle.Flex : DisplayStyle.None;
            searchField.SetValueWithoutNotify(conversationsState.searchText);
            searchCaseToggle.SetValueWithoutNotify(conversationsState.searchCaseSensitive);
            UpdateSearchButtons();
        }

        private void UpdateSearchButtons()
        {
            var hasText = !string.IsNullOrEmpty(conversationsState.searchText);
            searchNextButton.SetEnabled(hasText);
            searchPreviousButton.SetEnabled(hasText);
            searchStatusLabel.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Selects and frames the next entry after the selected node (or START) that contains
        /// the search text, wrapping around the conversation.
        /// </summary>
        private void SearchEntries(int direction)
        {
            var conversation = GetSelectedConversation();
            var searchText = conversationsState.searchText;
            if (conversation == null || string.IsNullOrEmpty(searchText)) return;
            var entries = conversation.dialogueEntries;
            var selectedNode = graphView.selection.OfType<DialogueEntryNode>().FirstOrDefault();
            var startID = (selectedNode != null) ? selectedNode.entryID : ConversationPropertyUtility.StartEntryID;
            var index = FindNextMatch(entries, entries.FindIndex(entry => entry.id == startID), direction, searchText, conversationsState.searchCaseSensitive);
            searchStatusLabel.style.display = (index >= 0) ? DisplayStyle.None : DisplayStyle.Flex;
            if (index >= 0) SelectEntry(entries[index].id, true);
        }

        /// <summary>
        /// Returns the index of the next entry containing the text, or -1 if none does. Checks
        /// every entry once, ending with the start index, so a lone match on the current entry
        /// still counts.
        /// </summary>
        internal static int FindNextMatch(List<DialogueEntry> entries, int startIndex, int direction, string text, bool caseSensitive)
        {
            var count = entries.Count;
            for (int i = 1; i <= count; i++)
            {
                var index = (((startIndex + direction * i) % count) + count) % count;
                if (EntryContainsText(entries[index], text, caseSensitive)) return index;
            }
            return -1;
        }

        /// <summary>True if the text is in the entry's Conditions, Script or any field's value.</summary>
        internal static bool EntryContainsText(DialogueEntry entry, string text, bool caseSensitive)
        {
            var compareOptions = caseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase;
            Func<string, bool> contains = s => !string.IsNullOrEmpty(s) && CultureInfo.InvariantCulture.CompareInfo.IndexOf(s, text, compareOptions) >= 0;
            return contains(entry.conditionsString) || contains(entry.userScript) || entry.fields.Any(field => contains(field.value));
        }

        #endregion

        #region Node Groups

        private const float GroupPadding = 20;

        private static Rect GetNodeRect(DialogueEntryNode node)
        {
            return new Rect(node.GetCanvasPosition(), node.layout.size);
        }

        private static bool ContainsRect(Rect outer, Rect inner)
        {
            return outer.Contains(inner.min) && outer.Contains(inner.max);
        }

        /// <summary>
        /// Adds a group enclosing the selected nodes and selects it. The IMGUI editor's canvas
        /// has no negative coordinates, so the group's corner is kept positive.
        /// </summary>
        private void CreateGroupFromSelection()
        {
            var conversationProperty = GetSelectedConversationProperty();
            var selectedNodes = graphView.selection.OfType<DialogueEntryNode>().ToList();
            if (conversationProperty == null || selectedNodes.Count == 0) return;
            var rect = GetNodeRect(selectedNodes[0]);
            foreach (var node in selectedNodes)
            {
                var nodeRect = GetNodeRect(node);
                rect.xMin = Mathf.Min(rect.xMin, nodeRect.xMin);
                rect.yMin = Mathf.Min(rect.yMin, nodeRect.yMin);
                rect.xMax = Mathf.Max(rect.xMax, nodeRect.xMax);
                rect.yMax = Mathf.Max(rect.yMax, nodeRect.yMax);
            }
            rect.xMin = Mathf.Max(1, rect.xMin - GroupPadding);
            rect.yMin = Mathf.Max(1, rect.yMin - GroupPadding - ConversationPropertyUtility.GroupHeadingHeight);
            rect.xMax += GroupPadding;
            rect.yMax += GroupPadding;

            serializedObject.Update();
            pendingSelectGroupIndex = ConversationPropertyUtility.AddEntryGroup(conversationProperty, "Group", rect);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Create Group");
            ScheduleGraphRebuild();
        }

        private void OnGroupDragStarted(EntryGroupElement group)
        {
            draggedNodes.Clear();
            draggedGroups.Clear();
            var groupRect = group.GetPosition();
            draggedGroups.Add(group, groupRect);
            foreach (var node in nodesByID.Values)
            {
                var nodeRect = GetNodeRect(node);
                if (ContainsRect(groupRect, nodeRect)) draggedNodes.Add(node, nodeRect.position);
            }
            foreach (var otherGroup in groupElements)
            {
                if (otherGroup != group && ContainsRect(groupRect, otherGroup.GetPosition())) draggedGroups.Add(otherGroup, otherGroup.GetPosition());
            }
        }

        private void OnGroupDragged(EntryGroupElement group, Vector2 offset)
        {
            foreach (var pair in draggedNodes)
            {
                pair.Key.SetPosition(new Rect(pair.Value + offset, Vector2.zero));
            }
            foreach (var pair in draggedGroups)
            {
                var rect = pair.Value;
                rect.position += offset;
                pair.Key.SetPosition(rect);
            }
        }

        private void OnGroupDragFinished(EntryGroupElement group)
        {
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null) return;
            serializedObject.Update();
            foreach (var node in draggedNodes.Keys)
            {
                var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, node.entryID);
                if (entryProperty != null) ConversationPropertyUtility.SetEntryPosition(entryProperty, node.GetCanvasPosition());
            }
            foreach (var element in draggedGroups.Keys)
            {
                ConversationPropertyUtility.SetEntryGroupRect(conversationProperty, element.groupIndex, element.GetPosition());
            }
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Move Group");
            draggedNodes.Clear();
            draggedGroups.Clear();
        }

        private void OnGroupResizeFinished(EntryGroupElement group)
        {
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null) return;
            serializedObject.Update();
            ConversationPropertyUtility.SetEntryGroupRect(conversationProperty, group.groupIndex, group.GetPosition());
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Resize Group");
        }

        #endregion

        #region Selection and View

        private void OnGraphSelectionChanged()
        {
            UpdateSelectionState();
            RebuildInspector();
        }

        private void UpdateSelectionState()
        {
            conversationsState.selectionConversationID = currentConversationID;
            conversationsState.selectedEntryIDs = graphView.selection.OfType<DialogueEntryNode>().Select(node => node.entryID).ToList();
            conversationsState.selectedGroupIndices = graphView.selection.OfType<EntryGroupElement>().Select(group => group.groupIndex).ToList();
        }

        private void RebuildInspector()
        {
            var conversationProperty = GetSelectedConversationProperty();
            if (conversationProperty == null)
            {
                inspector.ShowNothing();
                return;
            }
            var selectedNodes = graphView.selection.OfType<DialogueEntryNode>().ToList();
            var selectedEdges = graphView.selection.OfType<Edge>().ToList();
            var selectedGroup = graphView.selection.OfType<EntryGroupElement>().FirstOrDefault();
            if (selectedNodes.Count > 1)
            {
                inspector.ShowEntries(currentConversationID, selectedNodes.Select(node => node.entryID).ToList(), selectedEdges.Count);
            }
            else if (selectedNodes.Count == 0 && selectedEdges.Count == 0)
            {
                var groupProperty = (selectedGroup != null) ? ConversationPropertyUtility.GetEntryGroup(conversationProperty, selectedGroup.groupIndex) : null;
                if (groupProperty != null) inspector.ShowGroup(groupProperty);
                else inspector.ShowConversation(conversationProperty);
            }
            else if (selectedNodes.Count == 1 && selectedEdges.Count == 0)
            {
                var entryProperty = ConversationPropertyUtility.FindEntry(conversationProperty, selectedNodes[0].entryID);
                if (entryProperty != null) inspector.ShowEntry(conversationProperty, entryProperty);
                else inspector.ShowConversation(conversationProperty);
            }
            else if (selectedNodes.Count == 0 && selectedEdges.Count == 1)
            {
                var edge = selectedEdges[0];
                var origin = edge.output.node as DialogueEntryNode;
                var destination = edge.input.node as DialogueEntryNode;
                var originProperty = (origin != null) ? ConversationPropertyUtility.FindEntry(conversationProperty, origin.entryID) : null;
                var linkIndex = (originProperty != null && destination != null)
                    ? ConversationPropertyUtility.FindLinkIndex(originProperty, currentConversationID, destination.entryID) : -1;
                if (linkIndex >= 0) inspector.ShowLink(originProperty, linkIndex);
                else inspector.ShowConversation(conversationProperty);
            }
            else
            {
                inspector.ShowMultiple(selectedNodes.Count, selectedEdges.Count);
            }
        }

        private void SaveViewState()
        {
            if (currentConversationID < 0 || window == null) return;
            var view = conversationsState.views.Find(v => v.conversationID == currentConversationID);
            if (view == null)
            {
                view = new DialogueDatabaseEditorWindow.ConversationViewState() { conversationID = currentConversationID };
                conversationsState.views.Add(view);
            }
            // GraphView pans and zooms its content container, not itself. UpdateViewTransform sets
            // the container's inline translate and scale immediately; resolvedStyle lags a frame.
            var content = graphView.contentViewContainer;
            var translate = content.style.translate;
            var scale = content.style.scale;
            view.position = (translate.keyword == StyleKeyword.Undefined)
                ? new Vector3(translate.value.x.value, translate.value.y.value, translate.value.z)
                : content.resolvedStyle.translate;
            view.scale = (scale.keyword == StyleKeyword.Undefined) ? scale.value.value : content.resolvedStyle.scale.value;
        }

        private void RestoreViewState()
        {
            var view = conversationsState.views.Find(v => v.conversationID == currentConversationID);
            if (view != null)
            {
                graphView.UpdateViewTransform(view.position, view.scale);
            }
            else
            {
                // Wait for layout so the graph knows its size, then put START at the top center.
                graphView.UpdateViewTransform(Vector3.zero, Vector3.one);
                graphView.schedule.Execute(FrameStart).StartingIn(20);
            }
        }

        private void FrameStart()
        {
            DialogueEntryNode startNode;
            if (nodesByID.TryGetValue(ConversationPropertyUtility.StartEntryID, out startNode)) graphView.FrameNode(startNode);
        }

        #endregion

    }

}

#endif
