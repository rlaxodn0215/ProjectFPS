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
    /// This part of the UI Toolkit dialogue database editor window holds the Database
    /// panel's state so it survives domain reloads.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        [Serializable]
        public class DatabasePanelState
        {
            public bool emphasisSettingsFoldout = false;
            public List<int> expandedEmphasisSettings = new List<int>();
            public string searchText = string.Empty;
            public string replaceText = string.Empty;
            public bool searchUseRegex = false;
            public bool searchMatchCase = true;
            public bool searchSpecificConversation = false;
            public int searchConversationID = -1;
            public bool statsActorsFoldout = true;
            public bool statsConversationsFoldout = false;
            public string statsActorsSortColumn = "words";
            public bool statsActorsSortDescending = true;
            public string statsConversationsSortColumn = "words";
            public bool statsConversationsSortDescending = true;
            public bool issueChecksInitialized = false;
            public List<IssueCheck> enabledIssueChecks = new List<IssueCheck>();
            public bool issueEntrytagFormatSet = false;
            public EntrytagFormat issueEntrytagFormat = EntrytagFormat.ActorName_ConversationID_EntryID;
            public bool showIssueErrors = true;
            public bool showIssueWarnings = true;
            public bool showIssueInfo = true;
            public string issueFilter = string.Empty;
            public string issuesSortColumn = "severity";
            public bool issuesSortDescending = false;
            public DialogueDatabase mergeSource = null;
            public bool mergeProperties = true;
            public bool mergeEmphases = true;
            public bool mergeActors = true;
            public bool mergeItems = true;
            public bool mergeLocations = true;
            public bool mergeVariables = true;
            public bool mergeConversations = true;
            public DatabaseMerger.ConflictingIDRule mergeConflictingIDRule = DatabaseMerger.ConflictingIDRule.ReplaceConflictingIDs;
            public DatabasePanel.ExportFormat exportFormat = DatabasePanel.ExportFormat.ChatMapperXML;
            public string chatMapperExportPath = string.Empty;
            public string jsonExportPath = string.Empty;
            public string csvExportPath = string.Empty;
            public string voiceoverExportPath = string.Empty;
            public string languageTextExportPath = string.Empty;
            public string screenplayExportPath = string.Empty;
            public string proofreadingExportPath = string.Empty;
            public bool exportActors = true;
            public bool exportItems = true;
            public bool exportLocations = true;
            public bool exportVariables = true;
            public bool exportConversations = true;
            public bool exportCanvasRect = true;
            public bool exportConversationsAfterEntries = false;
            public bool exportConversationTitleSeparateColumn = false;
            public bool exportSpecificActor = false;
            public int exportActorID = -1;
            public bool exportOmitHiddenLines = false;
            public DialogueEntrySorter.SortMode exportSortMode = DialogueEntrySorter.SortMode.DoNotSort;
            public string exportVoiceoverInfoFieldName = string.Empty;
            public bool exportEntrytagFormatSet = false;
            public EntrytagFormat exportEntrytagFormat = EntrytagFormat.ActorName_ConversationID_EntryID;
            public EncodingType exportEncodingType = EncodingType.UTF8;
            public bool localizationLanguagesLoaded = false;
            public DialogueDatabaseLocalizationImporter.LocalizationLanguages localizationLanguages = new DialogueDatabaseLocalizationImporter.LocalizationLanguages();
            public bool localizationUseConversationTitle = false;
            public bool localizationUseKeyField = false;
            public string localizationKeyField = "Articy Id";
            public bool localizationAssignKeyValues = false;
            public bool localizationCreateNewFields = false;
            public bool localizationIncludeBlankEntries = true;
            public DialogueEntrySortMethod localizationSortMethod = DialogueEntrySortMethod.No;
        }

        [SerializeField]
        private DatabasePanelState databasePanelState = new DatabasePanelState();
        public DatabasePanelState DatabaseState { get { return databasePanelState; } }

        private float timeForNextAutoBackup = 0;

        /// <summary>
        /// Starts timing the next auto backup from now.
        /// </summary>
        public void RestartAutoBackupTimer()
        {
            timeForNextAutoBackup = Time.realtimeSinceStartup + DialogueDatabaseEditorSettings.instance.autoBackupFrequency;
        }

        private void Update()
        {
            if (EditorApplication.isPlaying) return;
            var settings = DialogueDatabaseEditorSettings.instance;
            if (settings.autoBackupFrequency > 0.001f && Time.realtimeSinceStartup > timeForNextAutoBackup)
            {
                RestartAutoBackupTimer();
                if (focusedWindow == this) DialogueDatabaseAutoBackup.MakeBackup(database, settings.autoBackupFolder);
            }
        }

    }

    /// <summary>
    /// Database category. A vertical toolbar on the left selects which section is shown
    /// on the right.
    /// </summary>
    public partial class DatabasePanel : DatabaseEditorPanel
    {

        public enum Section { Properties, GlobalReplace, Merge, Export, Localization, Stats, CheckIssues, SpellCheck, AIAddon, EditorSettings }

        private static readonly string[] SectionLabels =
        {
            "Database Properties",
            "Global Search & Replace",
            "Merge Database",
            "Export Database",
            "Localization Export/Import",
            "Database Stats",
            "Check For Issues",
            "Spell Check",
            "AI Addon",
            "Editor Settings"
        };

        private static readonly string[] SectionTooltips =
        {
            "Database properties.",
            "Globally search & replace text across entire database.",
            "Options to merge another database into this one.",
            "Options to export the database to Chat Mapper XML format.",
            "Options to export and import localization files.",
            "Stats on word count and asset count.",
            "Check for issues in the database.",
            "Check spelling across the database with the JSpell Checker API.",
            "Generate, translate, and voice content with an AI addon.",
            "Editor settings."
        };

        // Per-project user setting that remembers the section after the window is closed.
        private const string SectionConfigKey = "PixelCrushers.DialogueSystem.DialogueDatabaseEditorWindow.DatabaseSection";

        private const float SectionToolbarWidth = 170;
        private const float SectionButtonHeight = 22;
        private const float ContentPadding = 8;
        private const float RebaselineButtonWidth = 128;
        private const float EditorSettingsLabelWidth = 150;
        private const float BrowseButtonWidth = 22;

        private Section currentSection = Section.Properties;
        private Dictionary<Section, ToolbarToggle> sectionToggles = new Dictionary<Section, ToolbarToggle>();
        private Dictionary<Section, VisualElement> sections = new Dictionary<Section, VisualElement>();

        private SerializedObject serializedObject = null;
        private VisualElement propertiesSection = null;
        private VisualElement aiAddonSection = null;
        private IntegerField emphasisSizeField = null;
        private VisualElement emphasisList = null;
        private int shownEmphasisCount = 0;

        private DialogueDatabaseEditorWindow.DatabasePanelState databaseState { get { return window.DatabaseState; } }

        public DatabasePanel() : base("Database")
        {
            style.flexDirection = FlexDirection.Row;
            style.paddingLeft = 0;
            style.paddingRight = 0;
            style.paddingTop = 0;
            style.paddingBottom = 0;

            if (System.Enum.TryParse(EditorUserSettings.GetConfigValue(SectionConfigKey), out Section savedSection) &&
                IsSectionAvailable(savedSection))
            {
                currentSection = savedSection;
            }

            Add(CreateSectionToolbar());
            Add(CreateContentPane());
            SelectSection(currentSection);
        }

        private VisualElement CreateSectionToolbar()
        {
            var sectionToolbar = new VisualElement();
            sectionToolbar.style.width = SectionToolbarWidth;
            sectionToolbar.style.flexShrink = 0;
            sectionToolbar.AddToClassList("ds-separator-right");
            for (int i = 0; i < SectionLabels.Length; i++)
            {
                var section = (Section)i;
                if (!IsSectionAvailable(section)) continue;
                var toggle = new ToolbarToggle() { text = SectionLabels[i], tooltip = SectionTooltips[i] };
                toggle.style.height = SectionButtonHeight;
                toggle.style.marginLeft = 0;
                toggle.style.marginRight = 0;
                toggle.style.unityTextAlign = TextAnchor.MiddleLeft;
                toggle.RegisterValueChangedCallback(evt => OnSectionToggleChanged(section, evt.newValue));
                sectionToggles.Add(section, toggle);
                sectionToolbar.Add(toggle);
            }
            return sectionToolbar;
        }

        private VisualElement CreateContentPane()
        {
            var contentPane = new VisualElement();
            contentPane.style.flexGrow = 1;
            contentPane.style.flexShrink = 1;
            contentPane.style.paddingLeft = ContentPadding;
            contentPane.style.paddingRight = ContentPadding;
            contentPane.style.paddingTop = ContentPadding;
            contentPane.style.paddingBottom = ContentPadding;

            Remove(titleLabel);
            contentPane.Add(titleLabel);

            for (int i = 0; i < SectionLabels.Length; i++)
            {
                var section = (Section)i;
                if (!IsSectionAvailable(section)) continue;
                var sectionContent = CreateSection(section);
                sections.Add(section, sectionContent);
                contentPane.Add(sectionContent);
            }
            return contentPane;
        }

        /// <summary>
        /// Creates the content of a section. Sections that aren't implemented yet show a placeholder.
        /// </summary>
        private VisualElement CreateSection(Section section)
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.style.flexGrow = 1;
            switch (section)
            {
                case Section.Properties:
                    propertiesSection = scrollView; // Filled when the database is assigned.
                    break;
                case Section.GlobalReplace:
                    AddGlobalReplaceSection(scrollView);
                    FillScrollViewViewport(scrollView);
                    break;
                case Section.CheckIssues:
                    AddCheckIssuesSection(scrollView);
                    FillScrollViewViewport(scrollView);
                    break;
                case Section.Merge:
                    AddMergeSection(scrollView);
                    break;
                case Section.Export:
                    AddExportSection(scrollView);
                    break;
                case Section.Localization:
                    AddLocalizationSection(scrollView);
                    break;
                case Section.Stats:
                    AddStatsSection(scrollView);
                    break;
                case Section.SpellCheck:
                    AddSpellCheckSection(scrollView);
                    break;
                case Section.AIAddon:
                    aiAddonSection = scrollView; // Filled when the window is assigned.
                    break;
                case Section.EditorSettings:
                    AddEditorSettings(scrollView);
                    break;
                default:
                    scrollView.Add(new HelpBox("Not implemented yet.", UnityEngine.UIElements.HelpBoxMessageType.None));
                    break;
            }
            return scrollView;
        }

        /// <summary>
        /// Makes the scroll view's content at least as tall as its visible area, so a results
        /// list with flexGrow fills the rest of the section when there's room. When there
        /// isn't, the list keeps its minimum height and the section scrolls.
        /// </summary>
        private static void FillScrollViewViewport(ScrollView scrollView)
        {
            scrollView.contentViewport.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                scrollView.contentContainer.style.minHeight = evt.newRect.height;
            });
        }

        public override void OnDatabaseChanged(SerializedObject serializedObject)
        {
            this.serializedObject = serializedObject;
            RebuildPropertiesSection();
            if (aiAddonSection != null && aiAddonSection.childCount == 0 && window != null) window.AddAIAddonSection(aiAddonSection);
            RefreshGlobalReplaceFields();
            ClearSearchResults();
            RebuildConversationPopup();
            RefreshMergeFields();
            RefreshExportFields();
            RefreshLocalizationFields();
            ClearStats();
            RefreshIssueCheckFields();
            ClearIssues();
            if (spellCheckView != null) spellCheckView.ClearResults();
        }

        public override void OnThemeChanged()
        {
            // Search result previews highlight matches with a rich text color for the theme.
            if (resultsListView != null) resultsListView.RefreshItems();
            if (spellCheckView != null) spellCheckView.RefreshTheme();
        }

        public override void OnUndoRedo()
        {
            RebuildConversationPopup();
            // Results may be out of date. ApplyReplacements refreshes them itself.
            if (searchResults.Count > 0 && !isApplyingReplacements) RunSearch(false);
            RefreshMergePreview();
            MarkStatsOutOfDate();
            MarkIssuesOutOfDate();
            if (spellCheckView != null) spellCheckView.MarkOutOfDate();
            // Bound fields update themselves. Only the emphasis list's length needs handling.
            if (serializedObject == null || emphasisList == null) return;
            if (serializedObject.FindProperty("emphasisSettings").arraySize != shownEmphasisCount) RebuildEmphasisList();
        }

        #region Database Properties

        private void RebuildPropertiesSection()
        {
            propertiesSection.Clear();
            emphasisSizeField = null;
            emphasisList = null;
            if (serializedObject == null) return;

            propertiesSection.Add(FieldPropertyUtility.CreateTextField("Author", serializedObject.FindProperty("author"), false));

            var versionField = FieldPropertyUtility.CreateTextField("Version", serializedObject.FindProperty("version"), false);
            versionField.tooltip = "By default, this is the version of the Chat Mapper data model, but you can use it for your own purposes";
            propertiesSection.Add(versionField);

            propertiesSection.Add(CreateTextAreaLabel("Description", null));
            propertiesSection.Add(FieldPropertyUtility.CreateTextField(null, serializedObject.FindProperty("description"), true));

            const string GlobalUserScriptTooltip = "Optional Lua code to run when this database is loaded at runtime.";
            propertiesSection.Add(CreateTextAreaLabel("Global User Script", GlobalUserScriptTooltip));
            var globalUserScriptField = FieldPropertyUtility.CreateTextField(null, serializedObject.FindProperty("globalUserScript"), true);
            globalUserScriptField.tooltip = GlobalUserScriptTooltip;
            propertiesSection.Add(globalUserScriptField);

            propertiesSection.Add(CreateEmphasisSettingsFoldout());
            propertiesSection.Add(CreateBaseIDRow());
        }

        private VisualElement CreateEmphasisSettingsFoldout()
        {
            var foldout = new Foldout()
            {
                text = "Emphasis Settings",
                tooltip = "Settings to use for [em#] tags in dialogue text.",
                value = databaseState.emphasisSettingsFoldout
            };
            foldout.RegisterValueChangedCallback(evt =>
            {
                // Child foldouts and toggles bubble their change events up to here.
                if (evt.target == foldout) databaseState.emphasisSettingsFoldout = evt.newValue;
            });

            emphasisSizeField = new IntegerField("Size") { isDelayed = true };
            emphasisSizeField.RegisterValueChangedCallback(evt => SetEmphasisSettingsSize(evt.newValue));
            foldout.Add(emphasisSizeField);

            emphasisList = new VisualElement();
            foldout.Add(emphasisList);
            RebuildEmphasisList();
            return foldout;
        }

        private void RebuildEmphasisList()
        {
            var emphasisSettingsProperty = serializedObject.FindProperty("emphasisSettings");
            shownEmphasisCount = emphasisSettingsProperty.arraySize;
            emphasisSizeField.SetValueWithoutNotify(shownEmphasisCount);
            emphasisList.Clear();
            for (int i = 0; i < shownEmphasisCount; i++)
            {
                emphasisList.Add(CreateEmphasisSettingFoldout(i, emphasisSettingsProperty.GetArrayElementAtIndex(i)));
            }
        }

        private VisualElement CreateEmphasisSettingFoldout(int index, SerializedProperty settingProperty)
        {
            var foldout = new Foldout()
            {
                text = string.Format("Emphasis {0}", index + 1),
                value = databaseState.expandedEmphasisSettings.Contains(index)
            };
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout) return;
                databaseState.expandedEmphasisSettings.Remove(index);
                if (evt.newValue) databaseState.expandedEmphasisSettings.Add(index);
            });

            var colorField = new ColorField("Color");
            colorField.BindProperty(settingProperty.FindPropertyRelative("color"));
            foldout.Add(colorField);
            foldout.Add(CreateBoundToggle("Bold", settingProperty.FindPropertyRelative("bold")));
            foldout.Add(CreateBoundToggle("Italic", settingProperty.FindPropertyRelative("italic")));
            foldout.Add(CreateBoundToggle("Underline", settingProperty.FindPropertyRelative("underline")));
            return foldout;
        }

        private static UnityEngine.UIElements.Toggle CreateBoundToggle(string label, SerializedProperty property)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label);
            toggle.BindProperty(property);
            return toggle;
        }

        private void SetEmphasisSettingsSize(int newSize)
        {
            newSize = Mathf.Max(DialogueDatabase.NumEmphasisSettings, newSize);
            emphasisSizeField.SetValueWithoutNotify(newSize);
            serializedObject.Update();
            var emphasisSettingsProperty = serializedObject.FindProperty("emphasisSettings");
            var oldSize = emphasisSettingsProperty.arraySize;
            if (newSize == oldSize) return;
            emphasisSettingsProperty.arraySize = newSize;
            for (int i = oldSize; i < newSize; i++)
            {
                // Unity copies the last element into new elements, so reset them.
                var settingProperty = emphasisSettingsProperty.GetArrayElementAtIndex(i);
                settingProperty.FindPropertyRelative("color").colorValue = Color.white;
                settingProperty.FindPropertyRelative("bold").boolValue = false;
                settingProperty.FindPropertyRelative("italic").boolValue = false;
                settingProperty.FindPropertyRelative("underline").boolValue = false;
            }
            serializedObject.ApplyModifiedProperties();
            RebuildEmphasisList();
        }

        private VisualElement CreateBaseIDRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 4;

            var baseIDField = new IntegerField("Base ID")
            {
                tooltip = "Assign internal IDs to actors, variables, conversations, etc., starting from this base value. Useful when working with multiple databases."
            };
            baseIDField.style.flexGrow = 1;
            baseIDField.style.flexShrink = 1;
            baseIDField.BindProperty(serializedObject.FindProperty("baseID"));
            row.Add(baseIDField);

            var rebaselineButton = new Button(ConfirmRebaselineAllIDs)
            {
                text = "Rebaseline IDs",
                tooltip = "Reassign new IDs to all content in this database counting up from the database's Base ID. (Will not change IDs of content synced from other databases.)"
            };
            rebaselineButton.style.width = RebaselineButtonWidth;
            rebaselineButton.style.flexShrink = 0;
            row.Add(rebaselineButton);
            return row;
        }

        private void ConfirmRebaselineAllIDs()
        {
            if (database == null || !DialogueDatabaseIDRebaseliner.Confirm()) return;
            serializedObject.ApplyModifiedProperties();
            try
            {
                DialogueDatabaseIDRebaseliner.Rebaseline(database);
            }
            finally
            {
                window.RefreshAfterDirectDatabaseChange();
                if (DialogueEditorWindow.instance != null) DialogueEditorWindow.instance.Reset();
            }
        }

        #endregion

        #region Global Search & Replace

        private const float SearchButtonWidth = 120;
        private const float ResultRowHeight = 20;
        private const float ResultCheckboxWidth = 18;
        private const float ResultFieldColumnWidth = 130;
        private const float SearchResultsMinHeight = 160;

        private TextField searchTextField = null;
        private TextField replaceTextField = null;
        private UnityEngine.UIElements.Toggle regexToggle = null;
        private UnityEngine.UIElements.Toggle matchCaseToggle = null;
        private UnityEngine.UIElements.Toggle specificConversationToggle = null;
        private VisualElement conversationPopupHost = null;
        private PopupField<int> conversationPopup = null;
        private HelpBox searchErrorBox = null;
        private Button replaceCheckedButton = null;
        private Button replaceAllButton = null;
        private Button searchButton = null;
        private Label searchStatusLabel = null;
        private UnityEngine.UIElements.Toggle checkAllToggle = null;
        private ListView resultsListView = null;
        private Foldout customResultsFoldout = null;
        private Label customResultsLabel = null;

        private List<GlobalSearchMatch> searchResults = new List<GlobalSearchMatch>();
        private GlobalSearchOptions resultsOptions = null; // Options of the search that produced searchResults.
        private GlobalSearchMatcher resultsMatcher = null; // Matcher for resultsOptions, used to highlight previews.
        private bool isApplyingReplacements = false;

        private void AddGlobalReplaceSection(VisualElement parent)
        {
            var searchHeader = new VisualElement();
            searchHeader.style.flexDirection = FlexDirection.Row;
            searchHeader.style.alignItems = Align.Center;
            var searchLabel = CreateTextAreaLabel("Search For", "Text to search for in field values, Conditions, Script, Description and Global User Script.");
            searchLabel.style.marginTop = 0;
            searchLabel.style.flexGrow = 1;
            searchHeader.Add(searchLabel);
            matchCaseToggle = CreateSearchOptionToggle("Match Case", "Match uppercase and lowercase exactly.", (state, value) => state.searchMatchCase = value);
            searchHeader.Add(matchCaseToggle);
            regexToggle = CreateSearchOptionToggle("Regex", "Search for a .NET regular expression. Replace With can use substitutions such as $1.", (state, value) => state.searchUseRegex = value);
            searchHeader.Add(regexToggle);
            parent.Add(searchHeader);

            searchTextField = CreateSearchTextArea(value => { if (window != null) databaseState.searchText = value; });
            parent.Add(searchTextField);

            parent.Add(CreateTextAreaLabel("Replace With", "Text to replace matches with."));
            replaceTextField = CreateSearchTextArea(value => { if (window != null) databaseState.replaceText = value; });
            parent.Add(replaceTextField);

            var conversationRow = new VisualElement();
            conversationRow.style.flexDirection = FlexDirection.Row;
            conversationRow.style.marginTop = 4;
            specificConversationToggle = new UnityEngine.UIElements.Toggle("Specific Conversation")
            {
                tooltip = "Search only one conversation, not the whole database."
            };
            specificConversationToggle.RegisterValueChangedCallback(evt =>
            {
                if (window != null) databaseState.searchSpecificConversation = evt.newValue;
                RebuildConversationPopup();
                RefreshSearchButtons();
            });
            conversationRow.Add(specificConversationToggle);
            conversationPopupHost = new VisualElement();
            conversationPopupHost.style.flexGrow = 1;
            conversationPopupHost.style.flexShrink = 1;
            conversationRow.Add(conversationPopupHost);
            parent.Add(conversationRow);

            searchErrorBox = new HelpBox(string.Empty, UnityEngine.UIElements.HelpBoxMessageType.Error);
            searchErrorBox.style.display = DisplayStyle.None;
            parent.Add(searchErrorBox);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 4;
            searchButton = CreateSearchButton("Search", "List all matches below.", () => RunSearch(true));
            buttonRow.Add(searchButton);
            replaceCheckedButton = CreateSearchButton("Replace Checked", "Replace the checked matches in the list.", ReplaceChecked);
            buttonRow.Add(replaceCheckedButton);
            replaceAllButton = CreateSearchButton("Replace All", "Search, then replace every match.", ReplaceAll);
            buttonRow.Add(replaceAllButton);
            parent.Add(buttonRow);

            var resultsHeader = new VisualElement();
            resultsHeader.style.flexDirection = FlexDirection.Row;
            resultsHeader.style.alignItems = Align.Center;
            resultsHeader.style.marginTop = 6;
            checkAllToggle = new UnityEngine.UIElements.Toggle() { tooltip = "Check or uncheck all matches." };
            checkAllToggle.style.width = ResultCheckboxWidth;
            checkAllToggle.style.marginLeft = 3;
            checkAllToggle.RegisterValueChangedCallback(evt => SetAllResultsIncluded(evt.newValue));
            resultsHeader.Add(checkAllToggle);
            searchStatusLabel = new Label();
            searchStatusLabel.style.flexGrow = 1;
            resultsHeader.Add(searchStatusLabel);
            parent.Add(resultsHeader);

            resultsListView = new ListView(searchResults, ResultRowHeight, MakeResultRow, BindResultRow)
            {
                selectionType = SelectionType.Single,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                tooltip = "Double-click a match to show it."
            };
            resultsListView.style.flexGrow = 1;
            resultsListView.style.minHeight = SearchResultsMinHeight;
            resultsListView.style.marginTop = 2;
            resultsListView.AddToClassList("ds-bordered");
            resultsListView.itemsChosen += OnResultsChosen;
            parent.Add(resultsListView);

            customResultsFoldout = new Foldout() { text = "Custom Search Results", value = true };
            customResultsFoldout.style.display = DisplayStyle.None;
            customResultsLabel = new Label();
            customResultsLabel.style.whiteSpace = WhiteSpace.Normal;
            customResultsFoldout.Add(customResultsLabel);
            parent.Add(customResultsFoldout);

            RefreshSearchStatus();
        }

        private UnityEngine.UIElements.Toggle CreateSearchOptionToggle(string label, string tooltip, Action<DialogueDatabaseEditorWindow.DatabasePanelState, bool> setState)
        {
            var toggle = new UnityEngine.UIElements.Toggle() { text = label, tooltip = tooltip };
            toggle.style.marginLeft = 8;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (window != null) setState(databaseState, evt.newValue);
                HideSearchError();
            });
            return toggle;
        }

        private TextField CreateSearchTextArea(Action<string> setState)
        {
            var textField = new TextField() { multiline = true };
            textField.style.whiteSpace = WhiteSpace.Normal;
            textField.RegisterValueChangedCallback(evt =>
            {
                setState(evt.newValue);
                HideSearchError();
                RefreshSearchButtons();
            });
            return textField;
        }

        private static Button CreateSearchButton(string text, string tooltip, Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.style.width = SearchButtonWidth;
            return button;
        }

        /// <summary>
        /// Shows the saved search options in the fields. Called when the database is assigned,
        /// since the window, which holds the state, isn't assigned yet when the panel is built.
        /// </summary>
        private void RefreshGlobalReplaceFields()
        {
            if (window == null || searchTextField == null) return;
            searchTextField.SetValueWithoutNotify(databaseState.searchText);
            replaceTextField.SetValueWithoutNotify(databaseState.replaceText);
            regexToggle.SetValueWithoutNotify(databaseState.searchUseRegex);
            matchCaseToggle.SetValueWithoutNotify(databaseState.searchMatchCase);
            specificConversationToggle.SetValueWithoutNotify(databaseState.searchSpecificConversation);
            HideSearchError();
        }

        /// <summary>
        /// Rebuilds the conversation popup's choices, since PopupField can't change them in
        /// place on Unity 2022. Shown only if Specific Conversation is ticked.
        /// </summary>
        private void RebuildConversationPopup()
        {
            if (conversationPopupHost == null) return;
            conversationPopupHost.Clear();
            conversationPopup = null;
            AddConversationPopup();
            RefreshSearchButtons();
        }

        private void AddConversationPopup()
        {
            if (window == null || database == null || !databaseState.searchSpecificConversation) return;
            var ids = new List<int>();
            var titles = new Dictionary<int, string>();
            foreach (var conversation in database.conversations)
            {
                if (conversation == null || titles.ContainsKey(conversation.id)) continue;
                ids.Add(conversation.id);
                titles.Add(conversation.id, conversation.Title);
            }
            if (ids.Count == 0)
            {
                databaseState.searchConversationID = -1;
                conversationPopupHost.Add(new Label("(No conversations)"));
                return;
            }
            var index = ids.IndexOf(databaseState.searchConversationID);
            if (index < 0)
            {
                index = 0;
                databaseState.searchConversationID = ids[0];
            }
            Func<int, string> format = id => titles.TryGetValue(id, out var title) ? title : string.Empty;
            conversationPopup = new PopupField<int>(ids, index, format, format);
            conversationPopup.style.flexGrow = 1;
            conversationPopup.RegisterValueChangedCallback(evt => databaseState.searchConversationID = evt.newValue);
            conversationPopupHost.Add(conversationPopup);
        }

        private GlobalSearchOptions GetCurrentSearchOptions()
        {
            return new GlobalSearchOptions()
            {
                searchText = databaseState.searchText,
                replaceText = databaseState.replaceText,
                useRegex = databaseState.searchUseRegex,
                matchCase = databaseState.searchMatchCase,
                conversationID = databaseState.searchSpecificConversation ? databaseState.searchConversationID : -1
            };
        }

        private bool IsSearchReady()
        {
            if (window == null || database == null || string.IsNullOrEmpty(databaseState.searchText)) return false;
            return !databaseState.searchSpecificConversation || conversationPopup != null;
        }

        /// <summary>
        /// Lists the matches for the current options. If isUserSearch is true, also runs the
        /// customGlobalSearch hook. Otherwise it's a refresh after a replace or undo, so it
        /// reuses the options of the search that produced the list.
        /// </summary>
        private void RunSearch(bool isUserSearch)
        {
            if (!IsSearchReady() && isUserSearch) return;
            var options = isUserSearch ? GetCurrentSearchOptions() : resultsOptions;
            if (options == null || database == null) return;
            string error;
            var matcher = GlobalSearchMatcher.Create(options, out error);
            if (matcher == null)
            {
                ShowSearchError(error);
                ClearSearchResults();
                return;
            }
            HideSearchError();
            var result = GlobalSearchReplace.Find(database, matcher, options.conversationID);
            if (result.error != null) ShowSearchError(result.error);
            SetSearchResults(result.matches, options, result.canceled);
            if (isUserSearch) RunCustomGlobalSearch(options);
        }

        private void RunCustomGlobalSearch(GlobalSearchOptions options)
        {
            var custom = string.Empty;
            DialogueEditorWindow.InvokeCustomGlobalSearch(database, GetConversationTitle(options.conversationID), options.searchText, ref custom);
            custom = (custom ?? string.Empty).Trim();
            customResultsLabel.text = custom;
            customResultsFoldout.style.display = string.IsNullOrEmpty(custom) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private string GetConversationTitle(int conversationID)
        {
            if (conversationID == -1 || database == null) return string.Empty;
            var conversation = database.GetConversation(conversationID);
            return (conversation != null) ? conversation.Title : string.Empty;
        }

        private void ReplaceChecked()
        {
            if (resultsOptions == null || searchResults.Count == 0) return;
            var checkedMatches = searchResults.FindAll(match => match.include);
            if (checkedMatches.Count == 0) return;
            // Search with the options that found the list, but use the current replacement text.
            var options = CopySearchOptions(resultsOptions);
            options.replaceText = databaseState.replaceText;
            ApplyReplacements(checkedMatches, options);
        }

        private void ReplaceAll()
        {
            if (!IsSearchReady()) return;
            var options = GetCurrentSearchOptions();
            string error;
            var matcher = GlobalSearchMatcher.Create(options, out error);
            if (matcher == null)
            {
                ShowSearchError(error);
                return;
            }
            var result = GlobalSearchReplace.Find(database, matcher, options.conversationID);
            if (result.error != null)
            {
                ShowSearchError(result.error);
                return;
            }
            if (result.canceled) return;
            var scope = (options.conversationID == -1) ? "entire database" : "conversation '" + GetConversationTitle(options.conversationID) + "'";
            if (!EditorUtility.DisplayDialog("Replace All",
                string.Format("Replace all instances of '{0}' with '{1}' in {2}? ({3} matching values.)", options.searchText, options.replaceText, scope, result.matches.Count),
                "Replace All", "Cancel")) return;
            ApplyReplacements(result.matches, options, true);
        }

        /// <summary>
        /// Replaces the matches, refreshes the window, and lists what still matches.
        /// If runCustomHook is true, also runs the customGlobalSearchAndReplace hook, which
        /// works on the whole search scope, so it's only run by Replace All.
        /// </summary>
        private void ApplyReplacements(List<GlobalSearchMatch> matches, GlobalSearchOptions options, bool runCustomHook = false)
        {
            string error;
            var matcher = GlobalSearchMatcher.Create(options, out error);
            if (matcher == null || serializedObject == null)
            {
                if (error != null) ShowSearchError(error);
                return;
            }
            int count;
            try
            {
                count = GlobalSearchReplace.Apply(serializedObject, matches, matcher);
            }
            catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
            {
                ShowSearchError("The regular expression took too long to run. Try a simpler expression.");
                return;
            }
            if (runCustomHook && DialogueEditorWindow.HasCustomGlobalSearchAndReplace)
            {
                // The hook changes the database directly. Record it so the same undo step reverts it.
                Undo.RecordObject(database, GlobalSearchReplace.UndoName);
                DialogueEditorWindow.InvokeCustomGlobalSearchAndReplace(database, GetConversationTitle(options.conversationID), options.searchText, options.replaceText);
                EditorUtility.SetDirty(database);
            }
            resultsOptions = options;
            isApplyingReplacements = true;
            try
            {
                if (window != null) window.RefreshAllPanels();
            }
            finally
            {
                isApplyingReplacements = false;
            }
            RunSearch(false);
            searchStatusLabel.text = string.Format("Replaced {0} value{1}. {2}", count, (count == 1) ? string.Empty : "s", GetMatchCountText());
        }

        private static GlobalSearchOptions CopySearchOptions(GlobalSearchOptions options)
        {
            return new GlobalSearchOptions()
            {
                searchText = options.searchText,
                replaceText = options.replaceText,
                useRegex = options.useRegex,
                matchCase = options.matchCase,
                conversationID = options.conversationID
            };
        }

        private void SetSearchResults(List<GlobalSearchMatch> matches, GlobalSearchOptions options, bool canceled)
        {
            // Keep unchecked matches unchecked when the list is refreshed.
            var uncheckedPaths = new HashSet<string>();
            foreach (var match in searchResults)
            {
                if (!match.include) uncheckedPaths.Add(match.propertyPath);
            }
            searchResults.Clear();
            searchResults.AddRange(matches);
            foreach (var match in searchResults)
            {
                if (uncheckedPaths.Contains(match.propertyPath)) match.include = false;
            }
            resultsOptions = options;
            string error;
            resultsMatcher = GlobalSearchMatcher.Create(options, out error);
            resultsListView.ClearSelection();
            resultsListView.RefreshItems();
            RefreshSearchStatus();
            if (canceled) searchStatusLabel.text += " (Search canceled.)";
        }

        private void ClearSearchResults()
        {
            searchResults.Clear();
            resultsOptions = null;
            resultsMatcher = null;
            if (resultsListView == null) return;
            resultsListView.ClearSelection();
            resultsListView.RefreshItems();
            customResultsFoldout.style.display = DisplayStyle.None;
            RefreshSearchStatus();
        }

        private void SetAllResultsIncluded(bool include)
        {
            foreach (var match in searchResults)
            {
                match.include = include;
            }
            resultsListView.RefreshItems();
            RefreshSearchStatus();
        }

        private string GetMatchCountText()
        {
            if (resultsOptions == null) return string.Empty;
            if (searchResults.Count == 0) return "No matches.";
            var checkedCount = searchResults.FindAll(match => match.include).Count;
            return string.Format("{0} matching value{1}, {2} checked.", searchResults.Count, (searchResults.Count == 1) ? string.Empty : "s", checkedCount);
        }

        private void RefreshSearchStatus()
        {
            if (searchStatusLabel == null) return;
            searchStatusLabel.text = GetMatchCountText();
            var anyChecked = searchResults.Exists(match => match.include);
            checkAllToggle.SetValueWithoutNotify(anyChecked);
            checkAllToggle.SetEnabled(searchResults.Count > 0);
            RefreshSearchButtons();
        }

        private void RefreshSearchButtons()
        {
            if (searchButton == null) return;
            var ready = IsSearchReady();
            searchButton.SetEnabled(ready);
            replaceAllButton.SetEnabled(ready);
            replaceCheckedButton.SetEnabled(resultsOptions != null && searchResults.Exists(match => match.include));
        }

        private void ShowSearchError(string message)
        {
            searchErrorBox.text = message;
            searchErrorBox.style.display = DisplayStyle.Flex;
        }

        private void HideSearchError()
        {
            if (searchErrorBox != null) searchErrorBox.style.display = DisplayStyle.None;
        }

        private VisualElement MakeResultRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = ResultRowHeight;

            var toggle = new UnityEngine.UIElements.Toggle() { name = "include" };
            toggle.style.width = ResultCheckboxWidth;
            toggle.style.flexShrink = 0;
            toggle.RegisterValueChangedCallback(evt =>
            {
                var match = toggle.userData as GlobalSearchMatch;
                if (match == null) return;
                match.include = evt.newValue;
                RefreshSearchStatus();
            });
            row.Add(toggle);

            var ownerLabel = new Label() { name = "owner" };
            ownerLabel.style.width = Length.Percent(30);
            ownerLabel.style.flexShrink = 0;
            ownerLabel.style.overflow = Overflow.Hidden;
            ownerLabel.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(ownerLabel);

            var fieldLabel = new Label() { name = "field" };
            fieldLabel.style.width = ResultFieldColumnWidth;
            fieldLabel.style.flexShrink = 0;
            fieldLabel.style.overflow = Overflow.Hidden;
            fieldLabel.style.textOverflow = TextOverflow.Ellipsis;
            fieldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(fieldLabel);

            var previewLabel = new Label() { name = "preview", enableRichText = true };
            previewLabel.style.flexGrow = 1;
            previewLabel.style.flexShrink = 1;
            previewLabel.style.overflow = Overflow.Hidden;
            previewLabel.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(previewLabel);
            return row;
        }

        private void BindResultRow(VisualElement row, int index)
        {
            if (index < 0 || index >= searchResults.Count) return;
            var match = searchResults[index];
            var toggle = row.Q<UnityEngine.UIElements.Toggle>("include");
            toggle.userData = match;
            toggle.SetValueWithoutNotify(match.include);
            var ownerLabel = row.Q<Label>("owner");
            ownerLabel.text = match.ownerLabel;
            ownerLabel.tooltip = match.ownerLabel;
            var fieldLabel = row.Q<Label>("field");
            fieldLabel.text = match.fieldLabel;
            fieldLabel.tooltip = match.fieldLabel;
            var previewLabel = row.Q<Label>("preview");
            previewLabel.text = GetPreviewText(match.value);
            previewLabel.tooltip = match.value;
        }

        /// <summary>
        /// One line of the matching value around its first match, with the match highlighted.
        /// </summary>
        private string GetPreviewText(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int index, length;
            if (resultsMatcher == null || !resultsMatcher.FindFirst(value, out index, out length)) return RichTextPreview.Build(value, -1, 0);
            return RichTextPreview.Build(value, index, length);
        }

        private void OnResultsChosen(IEnumerable<object> items)
        {
            foreach (var item in items)
            {
                var match = item as GlobalSearchMatch;
                if (match != null) GoToMatch(match);
                return;
            }
        }

        private void GoToMatch(GlobalSearchMatch match)
        {
            GoTo(match.kind, match.assetID, match.entryID);
        }

        /// <summary>
        /// Shows an asset or dialogue entry, identified the way GlobalSearchMatch identifies it.
        /// </summary>
        private void GoTo(GlobalSearchMatch.Kind kind, int assetID, int entryID)
        {
            if (window == null) return;
            switch (kind)
            {
                case GlobalSearchMatch.Kind.Database:
                    SelectSection(Section.Properties);
                    break;
                case GlobalSearchMatch.Kind.Actor:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Actors, assetID);
                    break;
                case GlobalSearchMatch.Kind.Item:
                case GlobalSearchMatch.Kind.Quest:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.ItemsQuests, assetID);
                    break;
                case GlobalSearchMatch.Kind.Location:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Locations, assetID);
                    break;
                case GlobalSearchMatch.Kind.Variable:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Variables, assetID);
                    break;
                case GlobalSearchMatch.Kind.Conversation:
                case GlobalSearchMatch.Kind.Entry:
                    window.GoToAsset(DialogueDatabaseEditorWindow.Category.Conversations, assetID, entryID);
                    break;
            }
        }

        #endregion

        #region Database Stats

        private const float StatsLabelWidth = 150;
        private const float StatsNumberWidth = 80;
        private const float StatsTableHeight = 240;
        private const float StatsRowHeight = 20;
        private const float StatsNameColumnWidth = 200;

        private VisualElement statsSection = null;
        private VisualElement statsContent = null;
        private Label statsStatusLabel = null;
        private Button updateStatsButton = null;
        private Button copyStatsButton = null;
        private DatabaseStats currentStats = null;
        private bool isStatsComputeScheduled = false;

        private void AddStatsSection(VisualElement parent)
        {
            statsSection = parent;
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            updateStatsButton = new Button(ComputeStats) { text = "Update Stats", tooltip = "Count the database's assets and words again." };
            updateStatsButton.style.width = SearchButtonWidth;
            toolbar.Add(updateStatsButton);
            copyStatsButton = new Button(CopyStatsAsCsv) { text = "Copy as CSV", tooltip = "Copy all stats to the clipboard as CSV, for a spreadsheet." };
            copyStatsButton.style.width = SearchButtonWidth;
            toolbar.Add(copyStatsButton);
            statsStatusLabel = new Label();
            statsStatusLabel.style.marginLeft = 6;
            toolbar.Add(statsStatusLabel);
            parent.Add(toolbar);

            statsContent = new VisualElement();
            parent.Add(statsContent);

            // Compute when the section first becomes visible for a database, whether it's
            // shown by selecting the section or the Database category.
            parent.RegisterCallback<GeometryChangedEvent>(evt => ScheduleStatsIfVisible());
            RefreshStatsButtons();
        }

        private void ScheduleStatsIfVisible()
        {
            if (currentStats != null || isStatsComputeScheduled || database == null) return;
            if (currentSection != Section.Stats || statsSection.layout.width <= 0) return;
            isStatsComputeScheduled = true;
            statsSection.schedule.Execute(() =>
            {
                isStatsComputeScheduled = false;
                if (currentStats == null && database != null && currentSection == Section.Stats) ComputeStats();
            });
        }

        private void ClearStats()
        {
            currentStats = null;
            if (statsContent == null) return;
            statsContent.Clear();
            statsStatusLabel.text = string.Empty;
            RefreshStatsButtons();
            ScheduleStatsIfVisible();
        }

        private void MarkStatsOutOfDate()
        {
            if (currentStats == null || statsStatusLabel == null) return;
            statsStatusLabel.text = string.Format("Updated {0:t} (may be out of date)", currentStats.computedAt);
        }

        private void RefreshStatsButtons()
        {
            if (updateStatsButton == null) return;
            updateStatsButton.SetEnabled(database != null);
            copyStatsButton.SetEnabled(currentStats != null);
        }

        private void ComputeStats()
        {
            if (database == null) return;
            currentStats = DatabaseStatsCalculator.Compute(database);
            statsStatusLabel.text = string.Format("Updated {0:t}{1}", currentStats.computedAt, currentStats.canceled ? " (canceled; counts are partial)" : string.Empty);
            RebuildStatsContent();
            RefreshStatsButtons();
        }

        private void CopyStatsAsCsv()
        {
            if (currentStats == null) return;
            EditorGUIUtility.systemCopyBuffer = DatabaseStatsCalculator.ToCsv(currentStats);
            statsStatusLabel.text = string.Format("Updated {0:t}. Copied to clipboard.", currentStats.computedAt);
        }

        private void RebuildStatsContent()
        {
            statsContent.Clear();
            var stats = currentStats;
            if (stats == null) return;

            statsContent.Add(CreateStatsHeading("Asset Count"));
            AddCountRow("Actors", stats.numActors);
            AddCountRow("Items", stats.numItems);
            AddCountRow("Quests", stats.numQuests);
            AddCountRow("Locations", stats.numLocations);
            AddCountRow("Variables", stats.numVariables);
            AddCountRow("Conversations", stats.numConversations);
            AddCountRow("Dialogue Entries", stats.numEntries, "Entries in all conversations, not counting START entries.");
            AddCountRow("Entries Non-Blank", stats.numEntriesNonBlank, "Entries with Dialogue Text or Menu Text.");
            AddCountRow("Scene Events", stats.numSceneEvents);

            var wordHeading = CreateStatsHeading("Word Count");
            wordHeading.tooltip = "Words in player-facing text: quest and item Display Name, Description, Success/Failure Description and entries, " +
                "and dialogue entry Dialogue Text and Menu Text, plus localized versions. Markup tags aren't counted. " +
                "Text without spaces between words, such as Chinese or Japanese, counts as one word per run.";
            statsContent.Add(wordHeading);
            var headerCells = new List<string> { "Total" };
            headerCells.AddRange(stats.languages);
            statsContent.Add(CreateStatsGridRow(string.Empty, headerCells, true));
            statsContent.Add(CreateWordCountRow("Quests", stats.quests, false));
            statsContent.Add(CreateWordCountRow("Items", stats.items, false));
            statsContent.Add(CreateWordCountRow("Conversations", stats.conversations, false));
            statsContent.Add(CreateWordCountRow("Total", stats.total, true));

            statsContent.Add(CreateStatsTableFoldout("Actors", databaseState.statsActorsFoldout, value => databaseState.statsActorsFoldout = value,
                CreateActorStatsTable(stats)));
            statsContent.Add(CreateStatsTableFoldout("Conversations", databaseState.statsConversationsFoldout, value => databaseState.statsConversationsFoldout = value,
                CreateConversationStatsTable(stats)));
        }

        private static Label CreateStatsHeading(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 2;
            return label;
        }

        private void AddCountRow(string label, int count, string tooltip = null)
        {
            var row = CreateStatsGridRow(label, new List<string> { count.ToString("N0") }, false);
            row.tooltip = tooltip;
            statsContent.Add(row);
        }

        private VisualElement CreateWordCountRow(string label, WordCounts words, bool bold)
        {
            var cells = new List<string> { words.Total.ToString("N0") };
            foreach (var language in currentStats.languages)
            {
                cells.Add(words.Get(language).ToString("N0"));
            }
            return CreateStatsGridRow(label, cells, bold);
        }

        /// <summary>A label followed by right-aligned number cells.</summary>
        private static VisualElement CreateStatsGridRow(string label, List<string> cells, bool bold)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var labelElement = new Label(label);
            labelElement.style.width = StatsLabelWidth;
            labelElement.style.marginLeft = 3;
            row.Add(labelElement);
            foreach (var cell in cells)
            {
                var cellLabel = new Label(cell);
                cellLabel.style.width = StatsNumberWidth;
                cellLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                cellLabel.style.overflow = Overflow.Hidden;
                cellLabel.style.textOverflow = TextOverflow.Ellipsis;
                cellLabel.tooltip = cell;
                row.Add(cellLabel);
            }
            if (bold)
            {
                foreach (var child in row.Children())
                {
                    child.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
            }
            return row;
        }

        private static Foldout CreateStatsTableFoldout(string text, bool expanded, Action<bool> expandedChanged, VisualElement table)
        {
            var foldout = new Foldout() { text = text, value = expanded };
            foldout.style.marginTop = 8;
            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target == foldout) expandedChanged(evt.newValue);
            });
            foldout.Add(table);
            return foldout;
        }

        /// <summary>A column of a stats table.</summary>
        private class StatsColumn<T>
        {
            public string name;
            public string title;
            public float width;
            public bool isNumber;
            public Func<T, string> getText;
            public Func<T, IComparable> getSortKey;
            /// <summary>Optional custom cell. If null, the cell is a Label showing getText.</summary>
            public Func<VisualElement> makeCell;
            public Action<VisualElement, T> bindCell;
            /// <summary>Optional cell tooltip. If null, text cells show their text.</summary>
            public Func<T, string> getTooltip;
        }

        private MultiColumnListView CreateActorStatsTable(DatabaseStats stats)
        {
            var columns = new List<StatsColumn<ActorStatsRow>>
            {
                new StatsColumn<ActorStatsRow> { name = "actor", title = "Actor", width = StatsNameColumnWidth, getText = row => row.label, getSortKey = row => row.label },
                NumberColumn<ActorStatsRow>("conversations", "Conversations", row => row.conversationIDs.Count),
                NumberColumn<ActorStatsRow>("entries", "Entries", row => row.entries),
                NumberColumn<ActorStatsRow>("words", "Words", row => row.words.Total)
            };
            AddLanguageColumns(columns, stats, row => row.words);
            return CreateSortableTable(stats.actors, columns, "words",
                () => databaseState.statsActorsSortColumn, () => databaseState.statsActorsSortDescending,
                (column, descending) => { databaseState.statsActorsSortColumn = column; databaseState.statsActorsSortDescending = descending; },
                row => { if (row.exists) window.GoToAsset(DialogueDatabaseEditorWindow.Category.Actors, row.actorID); },
                "Words each actor speaks. Conversations counts conversations the actor speaks or is spoken to in. Double-click to show the actor.", out _);
        }

        private MultiColumnListView CreateConversationStatsTable(DatabaseStats stats)
        {
            var columns = new List<StatsColumn<ConversationStatsRow>>
            {
                new StatsColumn<ConversationStatsRow> { name = "title", title = "Conversation", width = StatsNameColumnWidth, getText = row => row.title, getSortKey = row => row.title ?? string.Empty },
                NumberColumn<ConversationStatsRow>("entries", "Entries", row => row.entries),
                NumberColumn<ConversationStatsRow>("words", "Words", row => row.words.Total)
            };
            AddLanguageColumns(columns, stats, row => row.words);
            return CreateSortableTable(stats.conversationRows, columns, "words",
                () => databaseState.statsConversationsSortColumn, () => databaseState.statsConversationsSortDescending,
                (column, descending) => { databaseState.statsConversationsSortColumn = column; databaseState.statsConversationsSortDescending = descending; },
                row => window.GoToAsset(DialogueDatabaseEditorWindow.Category.Conversations, row.conversationID),
                "Entries and words in each conversation. Double-click to show the conversation.", out _);
        }

        private static StatsColumn<T> NumberColumn<T>(string name, string title, Func<T, int> getValue)
        {
            return new StatsColumn<T>
            {
                name = name,
                title = title,
                width = StatsNumberWidth + 10,
                isNumber = true,
                getText = row => getValue(row).ToString("N0"),
                getSortKey = row => getValue(row)
            };
        }

        private static void AddLanguageColumns<T>(List<StatsColumn<T>> columns, DatabaseStats stats, Func<T, WordCounts> getWords)
        {
            // Only worth showing per-language columns if there's more than the default language.
            if (stats.languages.Count <= 1) return;
            foreach (var language in stats.languages)
            {
                var lang = language;
                columns.Add(NumberColumn<T>("lang:" + lang, lang, row => getWords(row).Get(lang)));
            }
        }

        /// <summary>
        /// Creates a sortable, fixed-height table of rows. Sorting is done here on
        /// columnSortingChanged, which works on Unity 2022, rather than with the newer built-in
        /// sorting modes. Call resort after changing the rows to sort and show them again.
        /// </summary>
        private MultiColumnListView CreateSortableTable<T>(List<T> rows, List<StatsColumn<T>> columns, string defaultSortColumn,
            Func<string> getSortColumn, Func<bool> getSortDescending, Action<string, bool> setSort, Action<T> chosen, string tooltip,
            out Action resort)
        {
            var listView = new MultiColumnListView()
            {
                itemsSource = rows,
                fixedItemHeight = StatsRowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
#if UNITY_6000_0_OR_NEWER
                sortingMode = ColumnSortingMode.Default,
#else
                sortingEnabled = true,
#endif
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                tooltip = tooltip
            };
            listView.style.height = StatsTableHeight;
            foreach (var statsColumn in columns)
            {
                var c = statsColumn;
                listView.columns.Add(new Column()
                {
                    name = c.name,
                    title = c.title,
                    width = c.width,
                    sortable = true,
                    makeCell = (c.makeCell != null) ? c.makeCell : () =>
                    {
                        var label = new Label();
                        label.style.unityTextAlign = c.isNumber ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                        label.style.paddingLeft = 3;
                        label.style.paddingRight = 3;
                        label.style.overflow = Overflow.Hidden;
                        label.style.textOverflow = TextOverflow.Ellipsis;
                        return label;
                    },
                    bindCell = (element, index) =>
                    {
                        if (index < 0 || index >= rows.Count) return;
                        var row = rows[index];
                        if (c.bindCell != null)
                        {
                            c.bindCell(element, row);
                        }
                        else
                        {
                            var label = (Label)element;
                            label.text = c.getText(row);
                            label.tooltip = (c.getTooltip != null) ? c.getTooltip(row) : (c.isNumber ? null : label.text);
                        }
                        if (c.bindCell != null && c.getTooltip != null) element.tooltip = c.getTooltip(row);
                    }
                });
            }

            Action applySort = () =>
            {
                var sortColumn = columns.Find(col => col.name == getSortColumn()) ?? columns.Find(col => col.name == defaultSortColumn);
                if (sortColumn == null) return;
                var descending = getSortDescending();
                // Stable sort, so equal values keep name order.
                var sorted = new List<T>(descending ? System.Linq.Enumerable.OrderByDescending(rows, sortColumn.getSortKey) : System.Linq.Enumerable.OrderBy(rows, sortColumn.getSortKey));
                rows.Clear();
                rows.AddRange(sorted);
                listView.RefreshItems();
            };

            listView.columnSortingChanged += () =>
            {
                foreach (var description in listView.sortedColumns)
                {
                    setSort(description.columnName, description.direction == SortDirection.Descending);
                    break;
                }
                applySort();
            };
            listView.itemsChosen += items =>
            {
                foreach (var item in items)
                {
                    if (item is T row) chosen(row);
                    return;
                }
            };

            // Show the saved sort on the header and apply it.
            var savedColumn = columns.Exists(col => col.name == getSortColumn()) ? getSortColumn() : defaultSortColumn;
            listView.sortColumnDescriptions.Add(new SortColumnDescription(savedColumn, getSortDescending() ? SortDirection.Descending : SortDirection.Ascending));
            applySort();
            resort = applySort;
            return listView;
        }

#endregion

        #region Check For Issues

        private const float IssueCheckToggleWidth = 200;
        private const float IssueSeverityColumnWidth = 28;
        private const float IssueCheckColumnWidth = 150;
        private const float IssueLocationColumnWidth = 220;
        private const float IssueMessageColumnWidth = 420;

        private Dictionary<IssueCheck, UnityEngine.UIElements.Toggle> issueCheckToggles = new Dictionary<IssueCheck, UnityEngine.UIElements.Toggle>();
        private EnumField entrytagFormatField = null;
        private const float IssuesListMinHeight = 160;

        private Button runChecksButton = null;
        private Button copyIssuesButton = null;
        private Label issuesStatusLabel = null;
        private ToolbarToggle showErrorsToggle = null;
        private ToolbarToggle showWarningsToggle = null;
        private ToolbarToggle showInfoToggle = null;
        private ToolbarSearchField issueFilterField = null;
        private MultiColumnListView issuesListView = null;
        private Action resortIssues = null;
        private DatabaseIssueChecker.Result issuesResult = null;
        private List<DatabaseIssue> visibleIssues = new List<DatabaseIssue>();
        private bool areIssuesOutOfDate = false;

        private void AddCheckIssuesSection(VisualElement parent)
        {
            var togglesRow = new VisualElement();
            togglesRow.style.flexDirection = FlexDirection.Row;
            togglesRow.style.flexWrap = Wrap.Wrap;
            for (int i = 0; i < DatabaseIssueChecker.CheckNames.Length; i++)
            {
                var check = (IssueCheck)i;
                var toggle = new UnityEngine.UIElements.Toggle() { text = DatabaseIssueChecker.CheckNames[i], tooltip = DatabaseIssueChecker.CheckTooltips[i] };
                toggle.style.width = IssueCheckToggleWidth;
                if (check == IssueCheck.LuaSyntax && !DatabaseIssueChecker.IsLuaSyntaxCheckAvailable)
                {
                    toggle.SetEnabled(false);
                    toggle.tooltip = "Not available when using a Lua implementation other than the built-in Lua interpreter (USE_NLUA or OVERRIDE_LUA).";
                }
                toggle.RegisterValueChangedCallback(evt => SetIssueCheckEnabled(check, evt.newValue));
                issueCheckToggles.Add(check, toggle);
                togglesRow.Add(toggle);
            }
            parent.Add(togglesRow);

            entrytagFormatField = new EnumField("Entrytag Format", EntrytagFormat.ActorName_ConversationID_EntryID)
            {
                tooltip = "Format of the entrytags that audio clip names must match. Defaults to the scene's Dialogue Manager setting."
            };
            entrytagFormatField.RegisterValueChangedCallback(evt =>
            {
                if (window == null || !(evt.newValue is EntrytagFormat)) return;
                databaseState.issueEntrytagFormat = (EntrytagFormat)evt.newValue;
                databaseState.issueEntrytagFormatSet = true;
            });
            parent.Add(entrytagFormatField);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.alignItems = Align.Center;
            buttonRow.style.marginTop = 4;
            var allButton = new Button(() => SetAllIssueChecksEnabled(true)) { text = "All", tooltip = "Tick all checks." };
            buttonRow.Add(allButton);
            var noneButton = new Button(() => SetAllIssueChecksEnabled(false)) { text = "None", tooltip = "Untick all checks." };
            buttonRow.Add(noneButton);
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            buttonRow.Add(spacer);
            runChecksButton = new Button(RunIssueChecks) { text = "Run Checks", tooltip = "Run the ticked checks and list the issues below." };
            runChecksButton.style.width = SearchButtonWidth;
            buttonRow.Add(runChecksButton);
            copyIssuesButton = new Button(CopyIssuesAsText) { text = "Copy as Text", tooltip = "Copy all issues to the clipboard as text, grouped by check." };
            copyIssuesButton.style.width = SearchButtonWidth;
            buttonRow.Add(copyIssuesButton);
            parent.Add(buttonRow);

            issuesStatusLabel = new Label();
            issuesStatusLabel.style.marginTop = 4;
            issuesStatusLabel.style.marginLeft = 3;
            parent.Add(issuesStatusLabel);

            var filterBar = new UnityEditor.UIElements.Toolbar();
            filterBar.style.marginTop = 4;
            showErrorsToggle = CreateSeverityFilterToggle("Show errors.", (state, value) => state.showIssueErrors = value);
            filterBar.Add(showErrorsToggle);
            showWarningsToggle = CreateSeverityFilterToggle("Show warnings.", (state, value) => state.showIssueWarnings = value);
            filterBar.Add(showWarningsToggle);
            showInfoToggle = CreateSeverityFilterToggle("Show info.", (state, value) => state.showIssueInfo = value);
            filterBar.Add(showInfoToggle);
            var filterSpacer = new ToolbarSpacer();
            filterSpacer.style.flexGrow = 1;
            filterBar.Add(filterSpacer);
            issueFilterField = new ToolbarSearchField() { tooltip = "Show only issues whose check, location, message or details contain this text." };
            issueFilterField.RegisterValueChangedCallback(evt =>
            {
                if (window != null) databaseState.issueFilter = evt.newValue;
                RefreshVisibleIssues();
            });
            filterBar.Add(issueFilterField);
            parent.Add(filterBar);

            issuesListView = CreateIssuesTable();
            issuesListView.style.height = StyleKeyword.Auto;
            issuesListView.style.flexGrow = 1;
            issuesListView.style.minHeight = IssuesListMinHeight;
            parent.Add(issuesListView);

            RefreshIssueButtons();
        }

        private ToolbarToggle CreateSeverityFilterToggle(string tooltip, Action<DialogueDatabaseEditorWindow.DatabasePanelState, bool> setState)
        {
            var toggle = new ToolbarToggle() { tooltip = tooltip, value = true };
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (window != null) setState(databaseState, evt.newValue);
                RefreshVisibleIssues();
            });
            return toggle;
        }

        private MultiColumnListView CreateIssuesTable()
        {
            var columns = new List<StatsColumn<DatabaseIssue>>
            {
                new StatsColumn<DatabaseIssue>
                {
                    name = "severity", title = string.Empty, width = IssueSeverityColumnWidth,
                    getSortKey = issue => (int)issue.severity,
                    makeCell = () =>
                    {
                        var icon = new Image() { scaleMode = ScaleMode.ScaleToFit };
                        icon.style.width = 16;
                        icon.style.height = 16;
                        icon.style.alignSelf = Align.Center;
                        icon.style.marginLeft = 4;
                        return icon;
                    },
                    bindCell = (element, issue) => ((Image)element).image = GetSeverityIcon(issue.severity),
                    getTooltip = issue => issue.severity.ToString()
                },
                new StatsColumn<DatabaseIssue>
                {
                    name = "check", title = "Check", width = IssueCheckColumnWidth,
                    getText = issue => DatabaseIssueChecker.CheckNames[(int)issue.check], getSortKey = issue => (int)issue.check
                },
                new StatsColumn<DatabaseIssue>
                {
                    name = "location", title = "Location", width = IssueLocationColumnWidth,
                    getText = issue => issue.location, getSortKey = issue => issue.location ?? string.Empty
                },
                new StatsColumn<DatabaseIssue>
                {
                    name = "message", title = "Message", width = IssueMessageColumnWidth,
                    getText = issue => issue.message, getSortKey = issue => issue.message ?? string.Empty,
                    getTooltip = issue => string.IsNullOrEmpty(issue.detail) ? issue.message : issue.message + "\n\n" + issue.detail
                }
            };
            // Created in the panel's constructor, before the window (which holds the state) is
            // assigned, so the sort callbacks must handle a null window. RefreshIssueCheckFields
            // applies the saved sort once the window is assigned.
            return CreateSortableTable(visibleIssues, columns, "severity",
                () => (window != null) ? databaseState.issuesSortColumn : "severity",
                () => (window != null) && databaseState.issuesSortDescending,
                (column, descending) =>
                {
                    if (window == null) return;
                    databaseState.issuesSortColumn = column;
                    databaseState.issuesSortDescending = descending;
                },
                issue => GoTo(issue.kind, issue.assetID, issue.entryID),
                "Double-click an issue to show it. Hover over the message for details.", out resortIssues);
        }

        private static Texture GetSeverityIcon(IssueSeverity severity)
        {
            var iconName = (severity == IssueSeverity.Error) ? "console.erroricon.sml"
                : (severity == IssueSeverity.Warning) ? "console.warnicon.sml" : "console.infoicon.sml";
            var content = EditorGUIUtility.IconContent(iconName);
            return (content != null) ? content.image : null;
        }

        /// <summary>
        /// Shows the saved check options. Called when the database is assigned, since the
        /// window, which holds the state, isn't assigned yet when the panel is built.
        /// </summary>
        private void RefreshIssueCheckFields()
        {
            if (window == null || runChecksButton == null) return;
            var state = databaseState;
            if (!state.issueChecksInitialized)
            {
                state.issueChecksInitialized = true;
                state.enabledIssueChecks = new List<IssueCheck>(DatabaseIssueChecker.DefaultChecks);
            }
            if (!state.issueEntrytagFormatSet)
            {
                var dialogueManager = PixelCrushers.GameObjectUtility.FindFirstObjectByType<DialogueSystemController>();
                if (dialogueManager != null) state.issueEntrytagFormat = dialogueManager.displaySettings.cameraSettings.entrytagFormat;
            }
            foreach (var kvp in issueCheckToggles)
            {
                kvp.Value.SetValueWithoutNotify(state.enabledIssueChecks.Contains(kvp.Key));
            }
            entrytagFormatField.SetValueWithoutNotify(state.issueEntrytagFormat);
            showErrorsToggle.SetValueWithoutNotify(state.showIssueErrors);
            showWarningsToggle.SetValueWithoutNotify(state.showIssueWarnings);
            showInfoToggle.SetValueWithoutNotify(state.showIssueInfo);
            issueFilterField.SetValueWithoutNotify(state.issueFilter);
            ShowSavedIssuesSort();
            RefreshEntrytagFormatVisibility();
            RefreshIssueButtons();
        }

        /// <summary>Shows the saved sort on the issues table's header and sorts by it.</summary>
        private void ShowSavedIssuesSort()
        {
            if (issuesListView == null || window == null) return;
            var column = databaseState.issuesSortColumn;
            if (string.IsNullOrEmpty(column) || issuesListView.columns[column] == null) column = "severity";
            var direction = databaseState.issuesSortDescending ? SortDirection.Descending : SortDirection.Ascending;
            var current = issuesListView.sortColumnDescriptions;
            if (current.Count == 1 && current[0].columnName == column && current[0].direction == direction) return;
            current.Clear();
            current.Add(new SortColumnDescription(column, direction));
            if (resortIssues != null) resortIssues();
        }

        private void SetIssueCheckEnabled(IssueCheck check, bool enabled)
        {
            if (window == null) return;
            var checks = databaseState.enabledIssueChecks;
            checks.Remove(check);
            if (enabled) checks.Add(check);
            RefreshEntrytagFormatVisibility();
            RefreshIssueButtons();
        }

        private void SetAllIssueChecksEnabled(bool enabled)
        {
            foreach (var kvp in issueCheckToggles)
            {
                if (!kvp.Value.enabledSelf) continue;
                kvp.Value.value = enabled; // Calls SetIssueCheckEnabled.
            }
        }

        private void RefreshEntrytagFormatVisibility()
        {
            if (entrytagFormatField == null || window == null) return;
            var show = databaseState.enabledIssueChecks.Contains(IssueCheck.EntrytagAudio);
            entrytagFormatField.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshIssueButtons()
        {
            if (runChecksButton == null) return;
            var anyCheck = window != null && databaseState.enabledIssueChecks.Exists(check =>
                check != IssueCheck.LuaSyntax || DatabaseIssueChecker.IsLuaSyntaxCheckAvailable);
            runChecksButton.SetEnabled(database != null && anyCheck);
            copyIssuesButton.SetEnabled(issuesResult != null);
        }

        private void RunIssueChecks()
        {
            if (database == null || window == null) return;
            if (serializedObject != null) serializedObject.ApplyModifiedProperties();
            var options = new IssueCheckOptions() { entrytagFormat = databaseState.issueEntrytagFormat };
            foreach (var check in databaseState.enabledIssueChecks) options.checks.Add(check);
            issuesResult = DatabaseIssueChecker.Run(database, options);
            areIssuesOutOfDate = false;
            RefreshVisibleIssues();
            RefreshIssueButtons();
        }

        private void ClearIssues()
        {
            issuesResult = null;
            areIssuesOutOfDate = false;
            RefreshVisibleIssues();
            RefreshIssueButtons();
        }

        private void MarkIssuesOutOfDate()
        {
            if (issuesResult == null) return;
            areIssuesOutOfDate = true;
            RefreshIssuesStatus();
        }

        private void CopyIssuesAsText()
        {
            if (issuesResult == null) return;
            EditorGUIUtility.systemCopyBuffer = DatabaseIssueChecker.ToText(database, issuesResult);
            issuesStatusLabel.text += " Copied to clipboard.";
        }

        /// <summary>Applies the severity and text filters, then sorts and shows the issues.</summary>
        private void RefreshVisibleIssues()
        {
            if (issuesListView == null) return;
            visibleIssues.Clear();
            if (issuesResult != null && window != null)
            {
                var state = databaseState;
                var filter = state.issueFilter;
                foreach (var issue in issuesResult.issues)
                {
                    if (issue.severity == IssueSeverity.Error && !state.showIssueErrors) continue;
                    if (issue.severity == IssueSeverity.Warning && !state.showIssueWarnings) continue;
                    if (issue.severity == IssueSeverity.Info && !state.showIssueInfo) continue;
                    if (!string.IsNullOrEmpty(filter) && !IssueContains(issue, filter)) continue;
                    visibleIssues.Add(issue);
                }
            }
            issuesListView.ClearSelection();
            if (resortIssues != null) resortIssues(); else issuesListView.RefreshItems();
            RefreshIssuesStatus();
        }

        private static bool IssueContains(DatabaseIssue issue, string filter)
        {
            return Contains(DatabaseIssueChecker.CheckNames[(int)issue.check], filter) || Contains(issue.location, filter) ||
                Contains(issue.message, filter) || Contains(issue.detail, filter);
        }

        private static bool Contains(string s, string filter)
        {
            return !string.IsNullOrEmpty(s) && s.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RefreshIssuesStatus()
        {
            if (issuesStatusLabel == null) return;
            int errors = 0, warnings = 0, infos = 0;
            if (issuesResult != null)
            {
                foreach (var issue in issuesResult.issues)
                {
                    if (issue.severity == IssueSeverity.Error) errors++;
                    else if (issue.severity == IssueSeverity.Warning) warnings++;
                    else infos++;
                }
            }
            showErrorsToggle.text = string.Format("Errors ({0})", errors);
            showWarningsToggle.text = string.Format("Warnings ({0})", warnings);
            showInfoToggle.text = string.Format("Info ({0})", infos);
            if (issuesResult == null)
            {
                issuesStatusLabel.text = "Tick the checks to run, then click Run Checks.";
                return;
            }
            var text = string.Format("{0} Checked {1:t}.", DatabaseIssueChecker.GetSummary(issuesResult.issues), issuesResult.checkedAt);
            if (visibleIssues.Count < issuesResult.issues.Count) text += string.Format(" Showing {0}.", visibleIssues.Count);
            if (issuesResult.canceled) text += " (Canceled; results are partial.)";
            if (areIssuesOutOfDate) text += " (May be out of date.)";
            issuesStatusLabel.text = text;
        }

        #endregion

        #region Editor Settings

        private void AddEditorSettings(VisualElement parent)
        {
            var settings = DialogueDatabaseEditorSettings.instance;

            var frequencyField = new FloatField("Auto Backup Frequency")
            {
                tooltip = "Seconds between auto backups. Set to zero for no auto backups.",
                isDelayed = true,
                value = settings.autoBackupFrequency
            };
            frequencyField.labelElement.style.minWidth = EditorSettingsLabelWidth;
            frequencyField.RegisterValueChangedCallback(evt =>
            {
                settings.autoBackupFrequency = evt.newValue;
                settings.Save();
                window.RestartAutoBackupTimer();
            });
            parent.Add(frequencyField);

            var folderRow = new VisualElement();
            folderRow.style.flexDirection = FlexDirection.Row;
            var folderField = new TextField("Auto Backup Folder")
            {
                tooltip = "Location to write auto backups.",
                value = settings.autoBackupFolder
            };
            folderField.style.flexGrow = 1;
            folderField.style.flexShrink = 1;
            folderField.labelElement.style.minWidth = EditorSettingsLabelWidth;
            folderField.RegisterValueChangedCallback(evt =>
            {
                settings.autoBackupFolder = evt.newValue;
                settings.Save();
            });
            folderRow.Add(folderField);
            var browseButton = new Button(() => BrowseForAutoBackupFolder(folderField)) { text = "...", tooltip = "Choose a folder." };
            browseButton.style.width = BrowseButtonWidth;
            browseButton.style.flexShrink = 0;
            folderRow.Add(browseButton);
            parent.Add(folderRow);

            var syncToggle = new UnityEngine.UIElements.Toggle("Sync On Open")
            {
                tooltip = "If any database sections are configured to sync content from another database, automatically sync when opening database.",
                value = settings.syncOnOpen
            };
            syncToggle.labelElement.style.minWidth = EditorSettingsLabelWidth;
            syncToggle.RegisterValueChangedCallback(evt =>
            {
                settings.syncOnOpen = evt.newValue;
                settings.Save();
            });
            parent.Add(syncToggle);
        }

        private static void BrowseForAutoBackupFolder(TextField folderField)
        {
            var newFolder = EditorUtility.OpenFolderPanel("Auto Backup Folder", folderField.value, string.Empty);
            if (string.IsNullOrEmpty(newFolder)) return;
            if (!newFolder.Contains(Application.dataPath))
            {
                EditorUtility.DisplayDialog("Auto Backup Folder", "Destination for backups must be in your Assets folder or a subfolder.", "OK");
                return;
            }
            folderField.value = "Assets" + newFolder.Replace(Application.dataPath, string.Empty);
        }

        #endregion

        private void OnSectionToggleChanged(Section section, bool isOn)
        {
            if (isOn)
            {
                SelectSection(section);
            }
            else if (section == currentSection)
            {
                // Clicking the active section keeps it selected, like the category toolbar.
                sectionToggles[section].SetValueWithoutNotify(true);
            }
        }

        /// <summary>The AI Addon section is only available if an AI addon is installed.</summary>
        private static bool IsSectionAvailable(Section section)
        {
            return section != Section.AIAddon || DialogueDatabaseEditorWindow.HasAIAddon;
        }

        private void SelectSection(Section section)
        {
            if (!IsSectionAvailable(section)) section = Section.Properties;
            currentSection = section;
            SaveSectionSelection(section);
            titleLabel.text = SectionLabels[(int)section];
            foreach (var kvp in sectionToggles)
            {
                kvp.Value.SetValueWithoutNotify(kvp.Key == section);
            }
            foreach (var kvp in sections)
            {
                kvp.Value.style.display = (kvp.Key == section) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (section == Section.GlobalReplace) RebuildConversationPopup(); // Conversations may have changed.
            if (section == Section.Merge) RefreshMergePreview(); // Either database may have changed.
            if (section == Section.Export) // Actors may have changed.
            {
                RebuildExportActorPopup();
                RefreshExportVisibility();
            }
            if (section == Section.Localization) RefreshLocalizationVisibility(); // Items may have changed.
        }

        private static void SaveSectionSelection(Section section)
        {
            var value = section.ToString();
            if (EditorUserSettings.GetConfigValue(SectionConfigKey) != value)
            {
                EditorUserSettings.SetConfigValue(SectionConfigKey, value);
            }
        }

    }

}

#endif
