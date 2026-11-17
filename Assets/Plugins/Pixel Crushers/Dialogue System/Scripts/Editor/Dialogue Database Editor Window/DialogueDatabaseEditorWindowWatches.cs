// Copyright (c) Pixel Crushers. All rights reserved.

#if UNITY_2022_1_OR_NEWER

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace PixelCrushers.DialogueSystem.DialogueEditor
{

    /// <summary>
    /// This part of the UI Toolkit dialogue database editor window holds the Watches panel's
    /// state so watches survive domain reloads. It isn't reset when the database changes,
    /// since watches apply to the runtime master database, not to the edited asset.
    /// </summary>
    public partial class DialogueDatabaseEditorWindow
    {

        public enum WatchType { Expression, Variable, Quest }

        [Serializable]
        public class WatchData
        {
            public WatchType type;
            public string expression = string.Empty;
            public string variableName = string.Empty;
            public string questName = string.Empty;
            public int questEntryNumber = 0; // 0 is the quest's main state.

            public WatchData(WatchType type)
            {
                this.type = type;
            }
        }

        [Serializable]
        public class WatchesPanelState
        {
            public List<WatchData> watches = new List<WatchData>();
            public bool autoRefresh = false;
            public float refreshFrequency = 1f;
            public bool conversationsExpanded = false;
            public string luaCommand = string.Empty;
        }

        [SerializeField]
        private WatchesPanelState watchesPanelState = new WatchesPanelState();
        public WatchesPanelState WatchesState
        {
            get
            {
                if (watchesPanelState == null) watchesPanelState = new WatchesPanelState();
                return watchesPanelState;
            }
        }

    }

    /// <summary>
    /// Watches category, which replaces the Template category in play mode. Watches Lua
    /// expressions, runtime variables, and quest states, lists active conversations, and
    /// runs Lua code. Ported from the IMGUI editor's DialogueEditorWindowWatchSection.
    /// </summary>
    public class WatchesPanel : DatabaseEditorPanel
    {

        private const long TickIntervalMs = 100;
        private const float MinRefreshFrequency = 0.1f;
        private const float RefreshButtonWidth = 56;
        private const float DeleteButtonWidth = 22;

        private VisualElement unavailableHelpBox;
        private VisualElement body;
        private UnityEngine.UIElements.Toggle autoRefreshToggle;
        private FloatField frequencyField;
        private Button refreshAllButton;
        private ScrollView scrollView;
        private VisualElement watchRowsHost;
        private Foldout conversationsFoldout;
        private TextField luaCommandField;

        private List<WatchRow> rows = new List<WatchRow>();
        private string[] variableNames = null;
        private string[] questNames = null;
        private double nextRefreshTime = 0;
        private string conversationsSignature = null;

        public override bool RequiresDatabase { get { return false; } }

        private DialogueDatabaseEditorWindow.WatchesPanelState state { get { return window.WatchesState; } }

        private static bool IsRuntimeAvailable
        {
            get { return EditorApplication.isPlaying && DialogueManager.hasInstance && DialogueManager.masterDatabase != null; }
        }

        public WatchesPanel() : base("Watches")
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

            unavailableHelpBox = new HelpBox("Watches are available in play mode when the scene has a Dialogue Manager.",
                UnityEngine.UIElements.HelpBoxMessageType.Info);
            unavailableHelpBox.style.marginLeft = 8;
            unavailableHelpBox.style.marginRight = 8;
            unavailableHelpBox.style.marginTop = 8;
            Add(unavailableHelpBox);

            body = new VisualElement();
            body.style.flexGrow = 1;
            Add(body);

            body.Add(CreateGlobalControls());

            scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.contentContainer.style.paddingLeft = 8;
            scrollView.contentContainer.style.paddingRight = 8;
            scrollView.contentContainer.style.paddingTop = 4;
            scrollView.contentContainer.style.paddingBottom = 8;
            watchRowsHost = new VisualElement();
            scrollView.Add(watchRowsHost);
            conversationsFoldout = new Foldout() { text = "Active Conversations" };
            conversationsFoldout.style.marginTop = 6;
            conversationsFoldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != conversationsFoldout) return;
                state.conversationsExpanded = evt.newValue;
            });
            scrollView.Add(conversationsFoldout);
            body.Add(scrollView);

            body.Add(CreateRunCodeBar());

            schedule.Execute(Tick).Every(TickIntervalMs);
        }

        public override void OnDatabaseChanged(SerializedObject serializedObject)
        {
            // Called on entering and exiting play mode, among other times.
            ClearNameCaches();
            Rebuild();
        }

        private void Rebuild()
        {
            if (window == null) return;
            var available = IsRuntimeAvailable;
            unavailableHelpBox.style.display = available ? DisplayStyle.None : DisplayStyle.Flex;
            body.style.display = available ? DisplayStyle.Flex : DisplayStyle.None;
            autoRefreshToggle.SetValueWithoutNotify(state.autoRefresh);
            state.refreshFrequency = Mathf.Max(MinRefreshFrequency, state.refreshFrequency);
            frequencyField.SetValueWithoutNotify(state.refreshFrequency);
            conversationsFoldout.SetValueWithoutNotify(state.conversationsExpanded);
            luaCommandField.SetValueWithoutNotify(state.luaCommand ?? string.Empty);
            conversationsSignature = null;
            RebuildRows();
            if (available)
            {
                RefreshAllWatches();
                RefreshActiveConversations();
            }
        }

        #region Global Controls

        private VisualElement CreateGlobalControls()
        {
            var row = CreateRow();
            row.style.marginLeft = 8;
            row.style.marginRight = 8;
            row.style.marginTop = 4;

            autoRefreshToggle = new UnityEngine.UIElements.Toggle("Auto-Refresh") { tooltip = "Re-evaluate all watches periodically." };
            autoRefreshToggle.labelElement.style.minWidth = StyleKeyword.Auto;
            autoRefreshToggle.RegisterValueChangedCallback(evt =>
            {
                state.autoRefresh = evt.newValue;
                ResetRefreshTime();
            });
            row.Add(autoRefreshToggle);

            frequencyField = new FloatField() { tooltip = "Seconds between automatic refreshes." };
            frequencyField.style.width = 80;
            frequencyField.RegisterValueChangedCallback(evt =>
            {
                var frequency = Mathf.Max(MinRefreshFrequency, evt.newValue);
                if (frequency != evt.newValue) frequencyField.SetValueWithoutNotify(frequency);
                state.refreshFrequency = frequency;
                ResetRefreshTime();
            });
            row.Add(frequencyField);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            row.Add(spacer);

            refreshAllButton = new Button(RefreshAllWatches) { text = "Refresh All", tooltip = "Re-evaluate all watches now." };
            refreshAllButton.style.width = RefreshButtonWidth + DeleteButtonWidth + 6;
            row.Add(refreshAllButton);
            return row;
        }

        #endregion

        #region Menu

        private void PopulateMenu(DropdownMenu menu)
        {
            Func<DropdownMenuAction, DropdownMenuAction.Status> needsRuntime =
                a => IsRuntimeAvailable ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;
            menu.AppendAction("Add Watch", a => AddWatch(new DialogueDatabaseEditorWindow.WatchData(DialogueDatabaseEditorWindow.WatchType.Expression)), needsRuntime);
            menu.AppendAction("Add Runtime Variable", a => AddRuntimeVariableWatch(), needsRuntime);
            menu.AppendAction("Add All Runtime Variables", a => AddAllRuntimeVariableWatches(), needsRuntime);
            menu.AppendAction("Refresh Runtime Variable List", a => RefreshVariableList(), needsRuntime);
            menu.AppendAction("Add Quest", a => AddQuestWatch(), needsRuntime);
            menu.AppendAction("Add All Quests", a => AddAllQuestWatches(), needsRuntime);
            menu.AppendAction("Reset", a => ConfirmResetWatches(),
                a => (window != null && state.watches.Count > 0) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void AddWatch(DialogueDatabaseEditorWindow.WatchData watch)
        {
            state.watches.Add(watch);
            var row = CreateWatchRow(watch);
            rows.Add(row);
            watchRowsHost.Add(row);
            row.Evaluate();
            RefreshRefreshAllButton();
            scrollView.schedule.Execute(() => scrollView.ScrollTo(row));
        }

        private void AddRuntimeVariableWatch()
        {
            variableNames = null; // Pick up variables added at runtime.
            var names = VariableNames;
            var watch = new DialogueDatabaseEditorWindow.WatchData(DialogueDatabaseEditorWindow.WatchType.Variable);
            if (names.Length > 0) watch.variableName = names[0];
            AddWatch(watch);
        }

        private void AddAllRuntimeVariableWatches()
        {
            variableNames = null;
            foreach (var variableName in VariableNames)
            {
                if (state.watches.Exists(x => x.type == DialogueDatabaseEditorWindow.WatchType.Variable && x.variableName == variableName)) continue;
                AddWatch(new DialogueDatabaseEditorWindow.WatchData(DialogueDatabaseEditorWindow.WatchType.Variable) { variableName = variableName });
            }
        }

        private void RefreshVariableList()
        {
            variableNames = null;
            RebuildRows();
            RefreshAllWatches();
        }

        private void AddQuestWatch()
        {
            questNames = null;
            var names = QuestNames;
            var watch = new DialogueDatabaseEditorWindow.WatchData(DialogueDatabaseEditorWindow.WatchType.Quest);
            if (names.Length > 0) watch.questName = names[0];
            AddWatch(watch);
        }

        private void AddAllQuestWatches()
        {
            questNames = null;
            foreach (var questName in QuestNames)
            {
                if (state.watches.Exists(x => x.type == DialogueDatabaseEditorWindow.WatchType.Quest && x.questName == questName)) continue;
                AddWatch(new DialogueDatabaseEditorWindow.WatchData(DialogueDatabaseEditorWindow.WatchType.Quest) { questName = questName });
            }
        }

        private void ConfirmResetWatches()
        {
            if (!EditorUtility.DisplayDialog("Reset watches?", "This removes all watches.", "Reset", "Cancel")) return;
            state.watches.Clear();
            RebuildRows();
        }

        private void DeleteWatch(WatchRow row)
        {
            state.watches.Remove(row.watch);
            rows.Remove(row);
            row.RemoveFromHierarchy();
            RefreshRefreshAllButton();
        }

        #endregion

        #region Name Catalogs

        private void ClearNameCaches()
        {
            variableNames = null;
            questNames = null;
        }

        /// <summary>Names of the variables in Lua's Variable table, sorted.</summary>
        private string[] VariableNames
        {
            get
            {
                if (variableNames == null || variableNames.Length == 0)
                {
                    var names = new List<string>();
                    if (IsRuntimeAvailable)
                    {
                        var variableTable = Lua.Run("return Variable").asTable;
                        if (variableTable != null)
                        {
                            foreach (var key in variableTable.keys)
                            {
                                names.Add(key);
                            }
                        }
                    }
                    names.Sort();
                    variableNames = names.ToArray();
                }
                return variableNames;
            }
        }

        /// <summary>Names of all quests in any state, sorted.</summary>
        private string[] QuestNames
        {
            get
            {
                if (questNames == null || questNames.Length == 0)
                {
                    var names = new List<string>();
                    if (IsRuntimeAvailable)
                    {
                        names.AddRange(QuestLog.GetAllQuests(QuestState.Abandoned | QuestState.Active | QuestState.Failure |
                            QuestState.Success | QuestState.Grantable | QuestState.ReturnToNPC | QuestState.Unassigned));
                    }
                    names.Sort();
                    questNames = names.ToArray();
                }
                return questNames;
            }
        }

        #endregion

        #region Watch Rows

        private void RebuildRows()
        {
            watchRowsHost.Clear();
            rows.Clear();
            if (window != null && IsRuntimeAvailable)
            {
                foreach (var watch in state.watches)
                {
                    if (watch == null) continue;
                    var row = CreateWatchRow(watch);
                    rows.Add(row);
                    watchRowsHost.Add(row);
                }
            }
            RefreshRefreshAllButton();
        }

        private void RefreshRefreshAllButton()
        {
            if (refreshAllButton != null) refreshAllButton.SetEnabled(window != null && state.watches.Count > 0);
        }

        private WatchRow CreateWatchRow(DialogueDatabaseEditorWindow.WatchData watch)
        {
            switch (watch.type)
            {
                case DialogueDatabaseEditorWindow.WatchType.Variable:
                    return new VariableWatchRow(this, watch);
                case DialogueDatabaseEditorWindow.WatchType.Quest:
                    return new QuestWatchRow(this, watch);
                default:
                    return new ExpressionWatchRow(this, watch);
            }
        }

        private void RefreshAllWatches()
        {
            if (IsRuntimeAvailable)
            {
                foreach (var row in rows)
                {
                    row.Evaluate();
                }
            }
            ResetRefreshTime();
        }

        private void ResetRefreshTime()
        {
            var frequency = (window != null) ? Mathf.Max(MinRefreshFrequency, state.refreshFrequency) : 1f;
            nextRefreshTime = EditorApplication.timeSinceStartup + frequency;
        }

        /// <summary>
        /// True if the element or one of its children has keyboard focus, in which case
        /// periodic refreshes leave its value alone so they don't overwrite the user's typing.
        /// </summary>
        private bool HasFocus(VisualElement element)
        {
            var focused = (focusController != null) ? focusController.focusedElement as VisualElement : null;
            return focused != null && element != null && (focused == element || element.Contains(focused));
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static T Grow<T>(T element, float minWidth = 60) where T : VisualElement
        {
            element.style.flexGrow = 1;
            element.style.flexShrink = 1;
            element.style.flexBasis = 0;
            element.style.minWidth = minWidth;
            return element;
        }

        private static TextField CreateReadOnlyTextField(string value)
        {
            var field = new TextField() { value = value ?? string.Empty, isReadOnly = true };
            field.SetEnabled(false);
            return field;
        }

        /// <summary>
        /// Popup of names that keeps a saved name that's no longer in the list, shown as
        /// "(Missing)". Dotted names appear in submenus.
        /// </summary>
        private static PopupField<string> CreateNamePopup(string[] names, string current, Action<string> onChanged)
        {
            var choices = new List<string>(names);
            if (string.IsNullOrEmpty(current))
            {
                current = (choices.Count > 0) ? choices[0] : string.Empty;
            }
            if (!choices.Contains(current)) choices.Add(current);
            var nameSet = new HashSet<string>(names);
            Func<string, string> format = s => nameSet.Contains(s) || string.IsNullOrEmpty(s) ? s : "(Missing) " + s;
            Func<string, string> formatListItem = s => format(s).Replace('.', '/');
            var popup = new PopupField<string>(choices, current, format, formatListItem);
            popup.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
            return popup;
        }

        /// <summary>Base class for a row that shows one watch.</summary>
        private abstract class WatchRow : VisualElement
        {
            public readonly DialogueDatabaseEditorWindow.WatchData watch;
            protected readonly WatchesPanel owner;
            protected readonly VisualElement content;

            protected WatchRow(WatchesPanel panel, DialogueDatabaseEditorWindow.WatchData watch)
            {
                this.owner = panel;
                this.watch = watch;
                style.flexDirection = FlexDirection.Row;
                style.alignItems = Align.Center;
                style.marginBottom = 1;
                content = CreateRow();
                content.style.flexGrow = 1;
                content.style.flexShrink = 1;
                Add(content);
                var refreshButton = new Button(Evaluate) { text = "Refresh", tooltip = "Re-evaluate now." };
                refreshButton.style.width = RefreshButtonWidth;
                Add(refreshButton);
                var deleteButton = new Button(() => panel.DeleteWatch(this)) { text = "-", tooltip = "Delete this watch." };
                deleteButton.style.width = DeleteButtonWidth;
                Add(deleteButton);
            }

            /// <summary>Re-evaluates the watch and shows its current value.</summary>
            public abstract void Evaluate();
        }

        #endregion

        #region Expression Watches

        private class ExpressionWatchRow : WatchRow
        {
            private TextField valueField;

            public ExpressionWatchRow(WatchesPanel panel, DialogueDatabaseEditorWindow.WatchData watch) : base(panel, watch)
            {
                var expressionField = Grow(new TextField() { value = watch.expression ?? string.Empty, tooltip = "Lua expression. Press Enter or Refresh to evaluate." });
                expressionField.RegisterValueChangedCallback(evt =>
                {
                    watch.expression = evt.newValue;
                    valueField.SetValueWithoutNotify(string.Empty);
                });
                expressionField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) Evaluate();
                }, TrickleDown.TrickleDown);
                content.Add(expressionField);
                valueField = Grow(CreateReadOnlyTextField(string.Empty));
                content.Add(valueField);
            }

            public override void Evaluate()
            {
                var value = string.IsNullOrEmpty(watch.expression) ? string.Empty : Lua.Run("return " + watch.expression).asString;
                valueField.SetValueWithoutNotify(value);
            }
        }

        #endregion

        #region Variable Watches

        private class VariableWatchRow : WatchRow
        {
            private enum ValueKind { None, Actor, Location, Table, Bool, Number, Text }

            private VisualElement valueHost;
            private VisualElement valueEditor;
            private ValueKind valueKind = ValueKind.None;
            private Lua.Result value = Lua.noResult;
            private Variable variable = null;
            private Action updateValueEditor = null;

            public VariableWatchRow(WatchesPanel panel, DialogueDatabaseEditorWindow.WatchData watch) : base(panel, watch)
            {
                var names = panel.VariableNames;
                if (names.Length == 0 && string.IsNullOrEmpty(watch.variableName))
                {
                    content.Add(Grow(CreateReadOnlyTextField("(No variables)")));
                }
                else
                {
                    if (string.IsNullOrEmpty(watch.variableName)) watch.variableName = names[0];
                    var popup = Grow(CreateNamePopup(names, watch.variableName, newName =>
                    {
                        watch.variableName = newName;
                        variable = null;
                        Evaluate();
                    }));
                    popup.tooltip = "Variable to watch.";
                    content.Add(popup);
                }
                valueHost = Grow(new VisualElement());
                content.Add(valueHost);
            }

            public override void Evaluate()
            {
                var name = watch.variableName;
                value = string.IsNullOrEmpty(name) ? Lua.noResult : DialogueLua.GetVariable(name);
                if (variable == null || DialogueLua.StringToTableIndex(variable.Name) != name) variable = FindVariable(name);
                var kind = GetValueKind();
                if (kind != valueKind || valueEditor == null)
                {
                    valueKind = kind;
                    BuildValueEditor();
                }
                else if (!owner.HasFocus(valueEditor) && updateValueEditor != null)
                {
                    updateValueEditor();
                }
            }

            private static Variable FindVariable(string name)
            {
                var masterDatabase = DialogueManager.masterDatabase;
                if (masterDatabase == null || string.IsNullOrEmpty(name)) return null;
                return masterDatabase.variables.Find(x => string.Equals(DialogueLua.StringToTableIndex(x.Name), name));
            }

            private ValueKind GetValueKind()
            {
                if (string.IsNullOrEmpty(watch.variableName)) return ValueKind.None;
                if (variable != null && variable.Type == FieldType.Actor) return ValueKind.Actor;
                if (variable != null && variable.Type == FieldType.Location) return ValueKind.Location;
                if (value.isTable) return ValueKind.Table;
                if (value.isBool) return ValueKind.Bool;
                if (value.isNumber) return ValueKind.Number;
                return ValueKind.Text;
            }

            private void SetValue(object newValue)
            {
                DialogueLua.SetVariable(watch.variableName, newValue);
                Evaluate();
            }

            private void BuildValueEditor()
            {
                valueHost.Clear();
                updateValueEditor = null;
                var masterDatabase = DialogueManager.masterDatabase;
                switch (valueKind)
                {
                    case ValueKind.Actor:
                        valueEditor = CreateAssetPopup((masterDatabase != null) ? masterDatabase.actors : null);
                        break;
                    case ValueKind.Location:
                        valueEditor = CreateAssetPopup((masterDatabase != null) ? masterDatabase.locations : null);
                        break;
                    case ValueKind.Table:
                        valueEditor = CreateReadOnlyTextField("(table)");
                        break;
                    case ValueKind.Bool:
                        var boolPopup = new PopupField<string>(new List<string> { "True", "False" }, value.asBool ? 0 : 1);
                        boolPopup.RegisterValueChangedCallback(evt => SetValue(evt.newValue == "True"));
                        updateValueEditor = () => boolPopup.SetValueWithoutNotify(value.asBool ? "True" : "False");
                        valueEditor = boolPopup;
                        break;
                    case ValueKind.Number:
                        var floatField = new FloatField() { value = value.asFloat, isDelayed = true };
                        floatField.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
                        updateValueEditor = () => floatField.SetValueWithoutNotify(value.asFloat);
                        valueEditor = floatField;
                        break;
                    case ValueKind.Text:
                        var textField = new TextField() { value = value.asString, isDelayed = true };
                        textField.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
                        updateValueEditor = () => textField.SetValueWithoutNotify(value.asString);
                        valueEditor = textField;
                        break;
                    default:
                        valueEditor = CreateReadOnlyTextField(string.Empty);
                        break;
                }
                valueEditor.style.flexGrow = 1;
                valueHost.Add(valueEditor);
            }

            /// <summary>
            /// Popup of an asset's ID, for Actor and Location variables. Keeps an ID that isn't in
            /// the database visible as "(Missing)" instead of showing (None).
            /// </summary>
            private VisualElement CreateAssetPopup<T>(List<T> assets) where T : Asset
            {
                var ids = new List<int> { -1 };
                var names = new Dictionary<int, string> { { -1, "(None)" } };
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset == null || names.ContainsKey(asset.id)) continue;
                        ids.Add(asset.id);
                        names.Add(asset.id, asset.Name);
                    }
                }
                Func<int> currentID = () =>
                {
                    int id;
                    if (!int.TryParse(value.asString, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) return -1;
                    if (!names.ContainsKey(id))
                    {
                        ids.Add(id);
                        names.Add(id, string.Format("(Missing) [{0}]", id));
                    }
                    return id;
                };
                Func<int, string> format = id => names.TryGetValue(id, out var name) ? name : id.ToString();
                var initialID = currentID(); // May add a missing ID to ids.
                // The int argument is an index into ids, not a default value, since T is int.
                var popup = new PopupField<int>(ids, ids.IndexOf(initialID), format, format);
                popup.RegisterValueChangedCallback(evt => SetValue(evt.newValue));
                updateValueEditor = () => popup.SetValueWithoutNotify(currentID());
                return popup;
            }
        }

        #endregion

        #region Quest Watches

        private class QuestWatchRow : WatchRow
        {
            private VisualElement entryHost;
            private EnumField stateField;
            private QuestState questState = QuestState.Unassigned;

            public QuestWatchRow(WatchesPanel panel, DialogueDatabaseEditorWindow.WatchData watch) : base(panel, watch)
            {
                var names = panel.QuestNames;
                if (names.Length == 0 && string.IsNullOrEmpty(watch.questName))
                {
                    content.Add(Grow(CreateReadOnlyTextField("(No quests)")));
                }
                else
                {
                    if (string.IsNullOrEmpty(watch.questName)) watch.questName = names[0];
                    var popup = Grow(CreateNamePopup(names, watch.questName, newName =>
                    {
                        watch.questName = newName;
                        watch.questEntryNumber = 0;
                        BuildEntryPopup();
                        Evaluate();
                    }));
                    popup.tooltip = "Quest to watch.";
                    content.Add(popup);
                }
                entryHost = Grow(new VisualElement());
                entryHost.style.flexDirection = FlexDirection.Row;
                content.Add(entryHost);
                BuildEntryPopup();
                stateField = Grow(new EnumField(QuestState.Unassigned) { tooltip = "Current state. Change it to set the state." }, 80);
                stateField.RegisterValueChangedCallback(evt =>
                {
                    if (string.IsNullOrEmpty(watch.questName) || !(evt.newValue is QuestState newState)) return;
                    if (watch.questEntryNumber == 0)
                    {
                        QuestLog.SetQuestState(watch.questName, newState);
                    }
                    else
                    {
                        QuestLog.SetQuestEntryState(watch.questName, watch.questEntryNumber, newState);
                    }
                    DialogueManager.SendUpdateTracker();
                    Evaluate();
                });
                stateField.SetEnabled(!string.IsNullOrEmpty(watch.questName));
                content.Add(stateField);
            }

            /// <summary>Popup of the quest's main state and its entries.</summary>
            private void BuildEntryPopup()
            {
                entryHost.Clear();
                if (string.IsNullOrEmpty(watch.questName)) return;
                var count = QuestLog.GetQuestEntryCount(watch.questName);
                var texts = new string[count + 1];
                texts[0] = "(Main State)";
                for (int i = 1; i <= count; i++)
                {
                    texts[i] = QuestLog.GetQuestEntry(watch.questName, i);
                    if (string.IsNullOrEmpty(texts[i])) texts[i] = "Entry " + i;
                }
                var numbers = new List<int>();
                for (int i = 0; i <= count; i++) numbers.Add(i);
                if (watch.questEntryNumber < 0 || watch.questEntryNumber > count) watch.questEntryNumber = 0;
                Func<int, string> format = i => (0 <= i && i < texts.Length) ? texts[i] : string.Empty;
                // The int argument is an index into numbers, which here equals the entry number.
                var popup = new PopupField<int>(numbers, watch.questEntryNumber, format, format) { tooltip = "Watch the main state or an entry's state." };
                popup.style.flexGrow = 1;
                popup.RegisterValueChangedCallback(evt =>
                {
                    watch.questEntryNumber = evt.newValue;
                    Evaluate();
                });
                entryHost.Add(popup);
            }

            public override void Evaluate()
            {
                var questName = watch.questName;
                questState = string.IsNullOrEmpty(questName) ? QuestState.Unassigned
                    : (watch.questEntryNumber == 0) ? QuestLog.GetQuestState(questName)
                    : QuestLog.GetQuestEntryState(questName, watch.questEntryNumber);
                if (!owner.HasFocus(stateField)) stateField.SetValueWithoutNotify(questState);
            }
        }

        #endregion

        #region Active Conversations

        private void RefreshActiveConversations()
        {
            var records = (DialogueManager.hasInstance) ? DialogueManager.instance.activeConversations : null;
            var signature = GetConversationsSignature(records);
            if (signature == conversationsSignature) return;
            conversationsSignature = signature;
            conversationsFoldout.Clear();
            if (records == null || records.Count == 0)
            {
                var none = new Label("(None)");
                none.style.opacity = 0.6f;
                none.style.marginLeft = 3;
                conversationsFoldout.Add(none);
                return;
            }
            foreach (var record in records)
            {
                if (record == null) continue;
                var row = CreateRow();
                row.Add(Grow(CreateReadOnlyTextField(record.conversationTitle)));
                var viewRecord = record;
                var viewButton = new Button(() => ViewConversation(viewRecord)) { text = "View", tooltip = "Show the conversation's current entry." };
                viewButton.style.width = 100;
                row.Add(viewButton);
                conversationsFoldout.Add(row);
            }
        }

        private static string GetConversationsSignature(List<ActiveConversationRecord> records)
        {
            if (records == null) return string.Empty;
            var sb = new StringBuilder();
            sb.Append(records.Count);
            foreach (var record in records)
            {
                sb.Append('|');
                if (record != null) sb.Append(record.conversationTitle);
            }
            return sb.ToString();
        }

        private void ViewConversation(ActiveConversationRecord record)
        {
            var controller = (record != null) ? record.conversationController : null;
            var currentState = (controller != null) ? controller.currentState : null;
            var entry = (currentState != null && currentState.subtitle != null) ? currentState.subtitle.dialogueEntry : null;
            if (entry == null || window == null) return;
            window.GoToAsset(DialogueDatabaseEditorWindow.Category.Conversations, entry.conversationID, entry.id);
        }

        #endregion

        #region Run Code

        private VisualElement CreateRunCodeBar()
        {
            var bar = CreateRow();
            bar.style.borderTopWidth = 1;
            bar.style.borderTopColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            bar.style.paddingLeft = 8;
            bar.style.paddingRight = 8;
            bar.style.paddingTop = 3;
            bar.style.paddingBottom = 3;
            bar.style.flexShrink = 0;

            var label = new Label("Run Code:");
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.marginRight = 4;
            bar.Add(label);

            luaCommandField = Grow(new TextField() { tooltip = "Lua code to run. Press Enter or Run to run it." });
            luaCommandField.RegisterValueChangedCallback(evt =>
            {
                if (window != null) state.luaCommand = evt.newValue;
            });
            luaCommandField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                RunLuaCommand();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            bar.Add(luaCommandField);

            bar.Add(new Button(() => SetLuaCommand(string.Empty)) { text = "Clear", tooltip = "Clear the code." });
            var runButton = new Button(RunLuaCommand) { text = "Run", tooltip = "Run the code." };
            runButton.style.width = 48;
            bar.Add(runButton);
            return bar;
        }

        private void SetLuaCommand(string command)
        {
            luaCommandField.SetValueWithoutNotify(command);
            if (window != null) state.luaCommand = command;
        }

        private void RunLuaCommand()
        {
            var command = luaCommandField.value;
            if (string.IsNullOrEmpty(command) || !IsRuntimeAvailable) return;
            Debug.Log("Running: " + command);
            Lua.Run(command, true);
            SetLuaCommand(string.Empty);
            RefreshAllWatches();
        }

        #endregion

        #region Tick

        private void Tick()
        {
            if (window == null || !EditorApplication.isPlaying || resolvedStyle.display != DisplayStyle.Flex) return;
            var available = IsRuntimeAvailable;
            if (available != (body.style.display == DisplayStyle.Flex))
            {
                // The Dialogue Manager appeared or went away, such as on a scene change.
                ClearNameCaches();
                Rebuild();
                return;
            }
            if (!available) return;
            RefreshActiveConversations();
            if (state.autoRefresh && EditorApplication.timeSinceStartup >= nextRefreshTime)
            {
                RefreshAllWatches();
            }
        }

        #endregion

    }

}

#endif
