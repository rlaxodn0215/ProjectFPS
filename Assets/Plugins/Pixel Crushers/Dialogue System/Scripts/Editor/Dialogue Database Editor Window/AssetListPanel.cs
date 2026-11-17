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

    public partial class DialogueDatabaseEditorWindow
    {

        /// <summary>
        /// State shared by asset list panels, kept on the window so it survives domain reloads.
        /// </summary>
        [Serializable]
        public class AssetPanelState
        {
            public int selectedIndex = -1;
            public string filter = string.Empty;
            public bool hideFilteredOut = false;
            public bool fieldsFoldout = false;
            public List<string> expandedGroups = new List<string>(); // Group headers that are expanded (see GetGroupName).
        }

    }

    /// <summary>
    /// Base class for categories that edit one of the database's asset lists (actors,
    /// locations, etc.): a filterable, reorderable list on the left and the selected
    /// asset's properties on the right. Subclasses supply the asset-specific parts.
    /// All edits go through the window's SerializedObject, except syncing from another
    /// database, which uses DialogueDatabase's Sync methods under Undo.RecordObject.
    /// </summary>
    public abstract class AssetListPanel<T> : DatabaseEditorPanel where T : Asset
    {

        private const float ListRowHeight = 22; // Includes the row's 1px border and 1px margin on each side.
        private const float RowTypeColumnWidth = 48;
        private const long ExternalChangePollInterval = 500; // ms

        protected SerializedObject serializedObject;
        private List<int> visibleIndices = new List<int>();
        private HashSet<int> syncedIDs = new HashSet<int>();
        private int lastAssetCount = -1;
        private int lastFieldCount = -1;
        private int selectedAssetID = -1; // Restores selection by ID after undo/redo reorders the list.
        private bool isRefreshingList = false;

        // Rows in visibleIndices are array indices, or -(n + 1) for the header of groupNames[n].
        private List<string> groupNames = new List<string>();
        private const string UngroupedGroupLabel = "(Ungrouped)";

        private VisualElement syncRow;
        private ObjectField syncDatabaseField;
        private ToolbarSearchField searchField;
        private ListView listView;
        private Button removeButton;
        private ScrollView detailsScrollView;

        #region Subclass API

        /// <summary>Singular asset type name used in menus and messages, such as "Actor".</summary>
        protected abstract string AssetTypeName { get; }

        /// <summary>Plural asset type name used in messages, such as "Actors".</summary>
        protected virtual string AssetTypePluralName { get { return AssetTypeName + "s"; } }

        /// <summary>Name of the DialogueDatabase list field, such as "actors".</summary>
        protected abstract string ListPropertyName { get; }

        /// <summary>This panel's state, stored on the window.</summary>
        protected abstract DialogueDatabaseEditorWindow.AssetPanelState state { get; }

        protected abstract List<T> GetAssets(DialogueDatabase database);

        /// <summary>Template fields for an asset. Asset is null when adding a new one.</summary>
        protected abstract List<Field> GetTemplateFields(Template template, T asset);

        protected abstract List<string> GetPrimaryFieldTitles(Template template, T asset);

        /// <summary>
        /// Text for the list's optional Type column, such as "Quest". Return null to hide the column.
        /// </summary>
        protected virtual string GetRowTypeLabel(T asset) { return null; }

        /// <summary>Adds the Menu button's items. Subclasses can override to add or replace items.</summary>
        protected virtual void PopulateMenu(DropdownMenu menu)
        {
            menu.AppendAction("New " + AssetTypeName, a => AddNewAsset());
            menu.AppendAction("Sort/By Name", a => SortAssets(asset => asset.Name, StringComparer.CurrentCulture));
            menu.AppendAction("Sort/By ID", a => SortAssets(asset => asset.id, Comparer<int>.Default));
            AppendHideFilteredMenuItem(menu);
            AppendSyncMenuItem(menu);
        }

        /// <summary>
        /// Group for the list's collapsible group headers. Return null to show a flat list
        /// (the default), or an empty string for an asset that isn't in a group.
        /// </summary>
        protected virtual string GetGroupName(T asset) { return null; }

        /// <summary>
        /// Title of the field that holds the asset's name. Conversations use "Title".
        /// </summary>
        protected virtual string NameFieldTitle { get { return DialogueSystemFields.Name; } }

        /// <summary>Whether the details pane shows the read-only ID field.</summary>
        protected virtual bool ShowIDField { get { return true; } }

        /// <summary>
        /// Lets subclasses style a bound list row, such as highlighting a warning. The base
        /// resets the name label's color before calling this.
        /// </summary>
        protected virtual void StyleListRow(VisualElement row, Label nameLabel, T asset) { }

        /// <summary>Called when the Name field in the details pane changes.</summary>
        protected virtual void OnNameChanged() { }

        /// <summary>
        /// Adds buttons to the list's footer, before the "+" and "-" buttons. Called once, the
        /// first time the window assigns a database, so the window is available.
        /// </summary>
        protected virtual void AddListFooterButtons(VisualElement footer) { }

        private VisualElement listFooter = null;
        private bool addedListFooterButtons = false;

        /// <summary>
        /// The AI target for a field of the asset shown in the details pane, for AI addon
        /// buttons. Override for asset types that AI addons can edit.
        /// </summary>
        protected virtual AIFieldTarget? GetAIFieldTarget(string fieldTitle) { return null; }

        /// <summary>The AI addon's revise button for a field of the shown asset, or null.</summary>
        protected VisualElement CreateAIReviseTextButton(string fieldTitle)
        {
            var target = GetAIFieldTarget(fieldTitle);
            return (target.HasValue && window != null) ? window.CreateAIReviseTextButton(target.Value) : null;
        }

        /// <summary>The ID of the asset shown in the details pane, or -1.</summary>
        protected int SelectedAssetID { get { return selectedAssetID; } }

        /// <summary>Called by the list's "+" button. The default adds a new asset.</summary>
        protected virtual void OnAddButtonClicked(Button button)
        {
            AddNewAsset();
        }

        protected abstract bool IsSyncEnabled { get; set; }

        protected abstract DialogueDatabase SyncDatabase { get; set; }

        /// <summary>Copies assets from SyncDatabase, such as DialogueDatabase.SyncActors().</summary>
        protected abstract void SyncFromDatabase();

        /// <summary>Message asking whether to delete synced assets when sync is turned off.</summary>
        protected virtual string DisconnectSyncMessage
        {
            get { return string.Format("Also delete synced {0} from this database?", AssetTypePluralName.ToLower()); }
        }

        /// <summary>Whether a synced asset should be deleted when the user disconnects sync.</summary>
        protected virtual bool ShouldDeleteOnDisconnect(T asset) { return true; }

        /// <summary>
        /// Called when a new asset is added, after ID and fields are set. InsertArrayElementAtIndex
        /// duplicates the previous element, so reset any asset-specific properties here.
        /// </summary>
        protected virtual void ResetNewAssetProperties(SerializedProperty assetProperty) { }

        /// <summary>
        /// Adds the asset-specific part of the details pane, between Name and All Fields.
        /// The default shows the template's primary fields.
        /// </summary>
        protected virtual void AddAssetDetails(VisualElement parent, SerializedProperty assetProperty, SerializedProperty fieldsProperty)
        {
            AddPrimaryFields(parent, fieldsProperty, false);
        }

        #endregion

        protected AssetListPanel(string title) : base(title)
        {
            titleLabel.style.display = DisplayStyle.None;
            style.paddingLeft = 0;
            style.paddingRight = 0;
            style.paddingTop = 0;
            style.paddingBottom = 0;

            var splitView = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            splitView.viewDataKey = "PixelCrushers.DialogueDatabaseEditorWindow." + GetType().Name + ".SplitView";
            splitView.style.flexGrow = 1;
            Add(splitView);
            splitView.Add(CreateListPane());
            splitView.Add(CreateDetailsPane());

            schedule.Execute(PollForExternalChanges).Every(ExternalChangePollInterval);
        }

        #region List Pane

        private VisualElement CreateListPane()
        {
            var pane = new VisualElement();
            pane.style.minWidth = 160;
            pane.style.overflow = Overflow.Hidden;

            var toolbar = new UnityEditor.UIElements.Toolbar();
            searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1;
            searchField.style.width = StyleKeyword.Auto;
            searchField.RegisterValueChangedCallback(evt =>
            {
                state.filter = evt.newValue;
                RefreshList();
            });
            toolbar.Add(searchField);
            var menu = new ToolbarMenu() { text = "Menu" };
            PopulateMenu(menu.menu);
            toolbar.Add(menu);
            pane.Add(toolbar);

            syncRow = new VisualElement();
            syncRow.style.flexDirection = FlexDirection.Row;
            syncRow.style.marginTop = 2;
            syncDatabaseField = new ObjectField("Sync From") { objectType = typeof(DialogueDatabase), allowSceneObjects = false, tooltip = string.Format("Database to sync {0} from.", AssetTypePluralName.ToLower()) };
            // Let the field shrink to fit the list pane. Editor field labels have a large default min-width.
            syncDatabaseField.style.flexGrow = 1;
            syncDatabaseField.style.flexShrink = 1;
            syncDatabaseField.style.minWidth = 0;
            syncDatabaseField.labelElement.style.minWidth = 0;
            syncDatabaseField.labelElement.style.width = 64;
            syncDatabaseField.RegisterValueChangedCallback(evt => SetSyncDatabase(evt.newValue as DialogueDatabase));
            syncRow.Add(syncDatabaseField);
            var syncNowButton = new Button(SyncNow) { text = "Sync Now", tooltip = "Syncs from the database." };
            syncNowButton.style.flexShrink = 0;
            syncRow.Add(syncNowButton);
            pane.Add(syncRow);

            listView = new ListView();
            listView.style.flexGrow = 1;
            listView.fixedItemHeight = ListRowHeight;
            listView.selectionType = SelectionType.Single;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.showBorder = true;
            listView.itemsSource = visibleIndices;
            listView.makeItem = MakeListRow;
            listView.bindItem = BindListRow;
            listView.unbindItem = UnbindListRow;
            listView.itemIndexChanged += OnListReordered;
            listView.selectedIndicesChanged += OnListSelectionChanged;
            pane.Add(listView);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            listFooter = footer;
            var addButton = new Button() { text = "+", tooltip = string.Format("Add new {0}.", AssetTypeName.ToLower()) };
            addButton.clicked += () => OnAddButtonClicked(addButton);
            footer.Add(addButton);
            removeButton = new Button(DeleteSelectedAsset) { text = "-", tooltip = string.Format("Delete selected {0}.", AssetTypeName.ToLower()) };
            footer.Add(removeButton);
            pane.Add(footer);

            return pane;
        }

        private VisualElement MakeListRow()
        {
            var row = new VisualElement();
            row.style.flexGrow = 1;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Stretch; // Labels fill the row height so the Type column's divider spans it.
            row.style.paddingLeft = 4;
            row.style.paddingRight = 4;

            // Single-pixel border around each element, in the theme's border color. The 1px
            // margin keeps adjacent rows' borders (and the ListView's own border) from merging
            // into 2px lines.
            row.AddToClassList("ds-asset-row");

            // Optional Type column (see GetRowTypeLabel), hidden unless the subclass provides a label.
            var typeLabel = new Label() { name = "type" };
            typeLabel.style.width = RowTypeColumnWidth;
            typeLabel.style.flexShrink = 0;
            typeLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            typeLabel.AddToClassList("ds-asset-row__type");
            typeLabel.style.marginLeft = 0;
            typeLabel.style.marginRight = 0;
            typeLabel.style.paddingLeft = 0;
            typeLabel.style.paddingRight = 4;
            typeLabel.style.opacity = 0.6f;
            typeLabel.style.display = DisplayStyle.None;
            row.Add(typeLabel);

            // Group header text, shown instead of the type and name on group header rows.
            var groupLabel = new Label() { name = "group" };
            groupLabel.style.flexGrow = 1;
            groupLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            groupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            groupLabel.style.display = DisplayStyle.None;
            row.Add(groupLabel);

            var nameLabel = new Label() { name = "name" };
            nameLabel.style.flexGrow = 1;
            nameLabel.style.flexBasis = 0;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(nameLabel);
            return row;
        }

        private void BindListRow(VisualElement row, int index)
        {
            var listProperty = GetListProperty();
            if (listProperty == null || !(0 <= index && index < visibleIndices.Count)) return;
            var arrayIndex = visibleIndices[index];
            var groupLabel = row.Q<Label>("group");
            var typeLabel = row.Q<Label>("type");
            var nameLabel = row.Q<Label>("name");
            if (arrayIndex < 0)
            {
                var group = groupNames[-arrayIndex - 1];
                var isExpanded = state.expandedGroups.Contains(group);
                groupLabel.text = (isExpanded ? "\u25BC " : "\u25B6 ") + (string.IsNullOrEmpty(group) ? UngroupedGroupLabel : group);
                groupLabel.style.display = DisplayStyle.Flex;
                typeLabel.style.display = DisplayStyle.None;
                nameLabel.Unbind();
                nameLabel.style.display = DisplayStyle.None;
                row.style.opacity = 1f;
                return;
            }
            groupLabel.style.display = DisplayStyle.None;
            nameLabel.style.display = DisplayStyle.Flex;
            if (!(0 <= arrayIndex && arrayIndex < listProperty.arraySize)) return;
            var fieldsProperty = listProperty.GetArrayElementAtIndex(arrayIndex).FindPropertyRelative("fields");
            BindLabelToField(nameLabel, fieldsProperty, NameFieldTitle);
            var asset = GetAssetObject(arrayIndex);
            var typeText = GetRowTypeLabel(asset);
            typeLabel.text = typeText ?? string.Empty;
            typeLabel.style.display = (typeText != null) ? DisplayStyle.Flex : DisplayStyle.None;
            var isGrouped = groupNames.Count > 0;
            nameLabel.style.paddingLeft = (typeText != null) ? 4 : isGrouped ? 14 : 0; // Indent under group headers.
            var isDimmed = !EditorTools.IsAssetInFilter(asset, state.filter) || IsSynced(asset);
            row.style.opacity = isDimmed ? 0.4f : 1f;
            nameLabel.style.color = StyleKeyword.Null;
            StyleListRow(row, nameLabel, asset);
        }

        private static void BindLabelToField(Label label, SerializedProperty fieldsProperty, string fieldTitle)
        {
            var fieldProperty = FieldPropertyUtility.FindField(fieldsProperty, fieldTitle);
            if (fieldProperty != null)
            {
                label.BindProperty(fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName));
            }
            else
            {
                label.Unbind();
                label.text = string.Empty;
            }
        }

        private void UnbindListRow(VisualElement row, int index)
        {
            row.Q<Label>("name").Unbind();
        }

        private void OnListSelectionChanged(IEnumerable<int> selectedIndices)
        {
            if (isRefreshingList) return;
            var index = listView.selectedIndex;
            // An empty selection only comes from the list itself, such as when the selected
            // asset's group is collapsed (ListView may report it after RefreshList returns).
            // Keep the current asset selected in the details pane.
            if (index < 0) return;
            var arrayIndex = (0 <= index && index < visibleIndices.Count) ? visibleIndices[index] : -1;
            if (arrayIndex < -1 || (arrayIndex == -1 && index >= 0))
            {
                // Group header: toggle it and keep the current asset selected.
                var group = groupNames[-arrayIndex - 1];
                if (!state.expandedGroups.Remove(group)) state.expandedGroups.Add(group);
                RefreshList();
                return;
            }
            if (arrayIndex == state.selectedIndex) return;
            state.selectedIndex = arrayIndex;
            RebuildDetails();
        }

        private void OnListReordered(int fromIndex, int toIndex)
        {
            // Reordering is only enabled when visibleIndices covers the whole array, so list
            // indices are array indices.
            if (serializedObject == null) return;
            serializedObject.Update();
            GetListProperty().MoveArrayElement(fromIndex, toIndex);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(string.Format("Reorder {0}", AssetTypePluralName));
            state.selectedIndex = toIndex;
            RefreshList();
            RebuildDetails();
        }

        /// <summary>
        /// Rebuilds the list of visible assets from the filter and reselects the selected asset.
        /// </summary>
        private void RefreshList()
        {
            visibleIndices.Clear();
            groupNames.Clear();
            var listProperty = GetListProperty();
            var count = (listProperty != null) ? listProperty.arraySize : 0;
            var hideFiltered = state.hideFilteredOut && !string.IsNullOrEmpty(state.filter);
            var shownIndices = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (hideFiltered && !EditorTools.IsAssetInFilter(GetAssetObject(i), state.filter)) continue;
                shownIndices.Add(i);
            }

            // Group headers only appear if at least one asset is in a named group.
            var groupOf = new Dictionary<int, string>();
            var isGrouped = false;
            foreach (var i in shownIndices)
            {
                var group = GetGroupName(GetAssetObject(i));
                if (group == null) continue;
                groupOf[i] = group;
                if (group.Length > 0) isGrouped = true;
            }
            if (isGrouped)
            {
                foreach (var i in shownIndices)
                {
                    if (!groupNames.Contains(groupOf[i])) groupNames.Add(groupOf[i]);
                }
                for (int g = 0; g < groupNames.Count; g++)
                {
                    visibleIndices.Add(-(g + 1));
                    if (!state.expandedGroups.Contains(groupNames[g])) continue;
                    foreach (var i in shownIndices)
                    {
                        if (groupOf[i] == groupNames[g]) visibleIndices.Add(i);
                    }
                }
            }
            else
            {
                visibleIndices.AddRange(shownIndices);
            }
            lastAssetCount = count;
            listView.reorderable = !hideFiltered && !isGrouped;

            // RefreshItems drops selected indices that are now out of range and reports it as a
            // selection change, which would overwrite state.selectedIndex.
            isRefreshingList = true;
            try
            {
                listView.RefreshItems();
                // Guard -1 (no selection), which is also the encoding of the first group header.
                var listIndex = (state.selectedIndex >= 0) ? visibleIndices.IndexOf(state.selectedIndex) : -1;
                listView.SetSelectionWithoutNotify((listIndex >= 0) ? new int[] { listIndex } : new int[0]);
                if (listIndex >= 0) listView.ScrollToItem(listIndex);
            }
            finally
            {
                isRefreshingList = false;
            }
            removeButton.SetEnabled(0 <= state.selectedIndex && state.selectedIndex < count);
        }

        /// <summary>Rebinds the visible list rows without rebuilding the list.</summary>
        protected void RefreshListItems()
        {
            isRefreshingList = true;
            try
            {
                listView.RefreshItems();
            }
            finally
            {
                isRefreshingList = false;
            }
        }

        /// <summary>Rebuilds the visible list, such as after a grouping option changes.</summary>
        protected void RefreshListRows()
        {
            RefreshList();
        }

        #endregion

        #region Asset Operations

        protected SerializedProperty GetListProperty()
        {
            return (serializedObject != null && serializedObject.targetObject != null) ? serializedObject.FindProperty(ListPropertyName) : null;
        }

        // The asset objects are only read, for filtering, sorting, and sync checks. All edits go through serializedObject.
        protected T GetAssetObject(int arrayIndex)
        {
            if (database == null) return null;
            var assets = GetAssets(database);
            return (0 <= arrayIndex && arrayIndex < assets.Count) ? assets[arrayIndex] : null;
        }

        protected void AddNewAsset()
        {
            AddNewAsset(GetTemplateFields(window.Template, null), AssetTypeName);
        }

        /// <summary>
        /// Adds an asset with the next ID, fields copied from templateFields, and the name
        /// "New [typeLabel] [ID]", then selects it.
        /// </summary>
        /// <param name="initialize">Optional. Adjusts the new asset before it's applied, such as setting fields.</param>
        protected void AddNewAsset(List<Field> templateFields, string typeLabel, Action<SerializedProperty> initialize = null)
        {
            if (serializedObject == null) return;
            serializedObject.Update();
            var listProperty = GetListProperty();
            int highestID = database.baseID - 1;
            GetAssets(database).ForEach(asset => highestID = Mathf.Max(highestID, asset.id));
            var id = Mathf.Max(1, highestID + 1);

            var index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            var assetProperty = listProperty.GetArrayElementAtIndex(index);
            assetProperty.FindPropertyRelative("id").intValue = id;
            var fieldsProperty = assetProperty.FindPropertyRelative("fields");
            FieldPropertyUtility.AssignFields(fieldsProperty, window.Template.CreateFields(templateFields ?? new List<Field>()));
            var nameProperty = FieldPropertyUtility.FindField(fieldsProperty, NameFieldTitle);
            if (nameProperty == null) nameProperty = FieldPropertyUtility.AddField(fieldsProperty, NameFieldTitle, string.Empty, FieldType.Text);
            nameProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName).stringValue = string.Format("New {0} {1}", typeLabel, id);
            ResetNewAssetProperties(assetProperty);
            if (initialize != null) initialize(assetProperty);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Add " + typeLabel);

            SelectAssetAtIndex(index);
        }

        /// <summary>
        /// Selects the asset at an array index and shows it in the details pane. Expands its
        /// group and clears a filter that hides filtered-out assets, so the list shows it.
        /// </summary>
        protected void SelectAssetAtIndex(int index)
        {
            state.selectedIndex = index;
            var group = GetGroupName(GetAssetObject(index));
            if (group != null && !state.expandedGroups.Contains(group)) state.expandedGroups.Add(group);
            if (state.hideFilteredOut && !string.IsNullOrEmpty(state.filter)) searchField.value = string.Empty;
            RefreshList();
            RebuildDetails();
        }

        /// <summary>
        /// Selects the asset with an ID, such as a search result. Returns false if there's no such asset.
        /// </summary>
        public bool SelectAssetByID(int id)
        {
            if (database == null) return false;
            var index = GetAssets(database).FindIndex(asset => asset.id == id);
            if (index < 0) return false;
            SelectAssetAtIndex(index);
            return true;
        }

        private void DeleteSelectedAsset()
        {
            var asset = GetAssetObject(state.selectedIndex);
            if (asset == null || IsSynced(asset)) return;
            if (!EditorUtility.DisplayDialog(string.Format("Delete '{0}'?", EditorTools.GetAssetName(asset)),
                string.Format("Are you sure you want to delete this {0}?", AssetTypeName.ToLower()), "Delete", "Cancel")) return;
            serializedObject.Update();
            var listProperty = GetListProperty();
            listProperty.DeleteArrayElementAtIndex(state.selectedIndex);
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName("Delete " + AssetTypeName);
            state.selectedIndex = Mathf.Min(state.selectedIndex, listProperty.arraySize - 1);
            RefreshList();
            RebuildDetails();
        }

        protected void SortAssets<TKey>(Func<T, TKey> keySelector, IComparer<TKey> comparer)
        {
            if (serializedObject == null) return;
            serializedObject.Update();
            var listProperty = GetListProperty();
            var assets = GetAssets(database);
            var count = listProperty.arraySize;
            // Track selection by ID. ApplyModifiedProperties reuses asset objects by position,
            // so an object reference would point at a different asset after sorting.
            var selectedAsset = GetAssetObject(state.selectedIndex);
            var selectedID = (selectedAsset != null) ? selectedAsset.id : -1;
            var sortedOrder = Enumerable.Range(0, count).OrderBy(i => keySelector(assets[i]), comparer).ToList();

            // Apply the sorted order as a series of moves so the change goes through serializedObject.
            var currentOrder = Enumerable.Range(0, count).ToList();
            for (int targetIndex = 0; targetIndex < count; targetIndex++)
            {
                var originalIndex = sortedOrder[targetIndex];
                var currentIndex = currentOrder.IndexOf(originalIndex);
                if (currentIndex == targetIndex) continue;
                listProperty.MoveArrayElement(currentIndex, targetIndex);
                currentOrder.RemoveAt(currentIndex);
                currentOrder.Insert(targetIndex, originalIndex);
            }
            serializedObject.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(string.Format("Sort {0}", AssetTypePluralName));

            state.selectedIndex = (selectedID != -1) ? GetAssets(database).FindIndex(asset => asset.id == selectedID) : -1;
            RefreshList();
            RebuildDetails();
        }

        #endregion

        #region Sync

        // Syncing copies assets with DialogueDatabase's Sync methods, so these operations modify
        // the database object directly with Undo.RecordObject, then update serializedObject.

        private void RecordSyncedIDs()
        {
            syncedIDs.Clear();
            if (database != null && IsSyncEnabled && SyncDatabase != null)
            {
                GetAssets(SyncDatabase).ForEach(asset => syncedIDs.Add(asset.id));
            }
        }

        protected bool IsSynced(T asset)
        {
            return asset != null && syncedIDs.Contains(asset.id);
        }

        /// <summary>
        /// Adds a toggle that hides assets that don't match the search filter.
        /// </summary>
        protected void AppendHideFilteredMenuItem(DropdownMenu menu)
        {
            menu.AppendAction("Show/Hide Conversations Not In Filter", a =>
            {
                state.hideFilteredOut = !state.hideFilteredOut;
                RefreshList();
            },
            a => state.hideFilteredOut ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        protected void AppendSyncMenuItem(DropdownMenu menu)
        {
            menu.AppendAction("Sync From DB", a => ToggleSyncFromDatabase(),
                a => (database != null && IsSyncEnabled) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
        }

        private void ToggleSyncFromDatabase()
        {
            if (database == null) return;
            serializedObject.ApplyModifiedProperties();
            RecordSyncedIDs();
            Undo.RecordObject(database, string.Format("Toggle Sync {0}", AssetTypePluralName));
            IsSyncEnabled = !IsSyncEnabled;
            if (!IsSyncEnabled && SyncDatabase != null)
            {
                if (EditorUtility.DisplayDialog("Disconnect Synced DB", DisconnectSyncMessage, "Yes", "No"))
                {
                    GetAssets(database).RemoveAll(asset => syncedIDs.Contains(asset.id) && ShouldDeleteOnDisconnect(asset));
                }
            }
            OnSyncChanged();
        }

        private void SetSyncDatabase(DialogueDatabase newSyncDatabase)
        {
            if (database == null || newSyncDatabase == SyncDatabase) return;
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(database, string.Format("Change {0} Sync Database", AssetTypeName));
            SyncDatabase = newSyncDatabase;
            SyncFromDatabase();
            OnSyncChanged();
        }

        private void SyncNow()
        {
            if (database == null) return;
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(database, string.Format("Sync {0}", AssetTypePluralName));
            SyncFromDatabase();
            OnSyncChanged();
        }

        private void OnSyncChanged()
        {
            EditorUtility.SetDirty(database);
            serializedObject.Update();
            RefreshAll();
        }

        private void RefreshSyncRow()
        {
            var isSyncing = database != null && IsSyncEnabled;
            syncRow.style.display = isSyncing ? DisplayStyle.Flex : DisplayStyle.None;
            syncDatabaseField.SetValueWithoutNotify(isSyncing ? SyncDatabase : null);
        }

        #endregion

        #region Details Pane

        /// <summary>
        /// Creates the right-hand pane. The default is a scrolling form; subclasses can return
        /// their own element and fill it in RebuildDetailsContent.
        /// </summary>
        protected virtual VisualElement CreateDetailsPane()
        {
            detailsScrollView = new ScrollView(ScrollViewMode.Vertical);
            detailsScrollView.style.flexGrow = 1;
            detailsScrollView.contentContainer.style.paddingLeft = 8;
            detailsScrollView.contentContainer.style.paddingRight = 8;
            detailsScrollView.contentContainer.style.paddingTop = 6;
            detailsScrollView.contentContainer.style.paddingBottom = 6;
            return detailsScrollView;
        }

        protected void RebuildDetails()
        {
            lastFieldCount = -1;
            var selectedAsset = GetAssetObject(state.selectedIndex);
            selectedAssetID = (selectedAsset != null) ? selectedAsset.id : -1;
            var listProperty = GetListProperty();
            var assetProperty = (listProperty != null && 0 <= state.selectedIndex && state.selectedIndex < listProperty.arraySize)
                ? listProperty.GetArrayElementAtIndex(state.selectedIndex) : null;
            RebuildDetailsContent(assetProperty);
            if (assetProperty != null) lastFieldCount = assetProperty.FindPropertyRelative("fields").arraySize;
        }

        /// <summary>
        /// Fills the details pane for the selected asset, or for no selection if assetProperty
        /// is null. Subclasses that override CreateDetailsPane override this too.
        /// </summary>
        protected virtual void RebuildDetailsContent(SerializedProperty assetProperty)
        {
            var scrollOffset = detailsScrollView.scrollOffset;
            detailsScrollView.Clear();

            if (assetProperty == null)
            {
                var hint = new Label(string.Format("Select {0} {1}.", StartsWithVowel(AssetTypeName) ? "an" : "a", AssetTypeName.ToLower()));
                hint.style.opacity = 0.6f;
                detailsScrollView.Add(hint);
                return;
            }

            var fieldsProperty = assetProperty.FindPropertyRelative("fields");

            var asset = GetAssetObject(state.selectedIndex);
            var body = new VisualElement();
            if (IsSynced(asset))
            {
                detailsScrollView.Add(new HelpBox(string.Format("This {0} is synced from {1}. Edit it in that database.",
                    AssetTypeName.ToLower(), SyncDatabase.name), UnityEngine.UIElements.HelpBoxMessageType.Info));
                body.SetEnabled(false);
            }
            detailsScrollView.Add(body);

            if (ShowIDField)
            {
                var idField = new IntegerField("ID") { tooltip = "Internal ID. Change at your own risk." };
                idField.BindProperty(assetProperty.FindPropertyRelative("id"));
                idField.SetEnabled(false);
                body.Add(idField);
            }

            var nameProperty = FieldPropertyUtility.GetOrAddField(fieldsProperty, NameFieldTitle, string.Empty, FieldType.Text);
            var nameField = FieldPropertyUtility.CreateTextField(NameFieldTitle, nameProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), false);
            nameField.tooltip = string.Format("Name of this {0}.", AssetTypeName.ToLower());
            nameField.RegisterValueChangedCallback(evt => OnNameChanged());
            body.Add(nameField);

            AddAssetDetails(body, assetProperty, fieldsProperty);

            var fieldList = new FieldListElement(fieldsProperty, database, () => GetTemplateFields(window.Template, GetAssetObject(state.selectedIndex)), state.fieldsFoldout);
            fieldList.expandedChanged += expanded => state.fieldsFoldout = expanded;
            fieldList.structureChanged += RebuildDetails;
            body.Add(fieldList);

            detailsScrollView.schedule.Execute(() => detailsScrollView.scrollOffset = scrollOffset);
        }

        private static bool StartsWithVowel(string s)
        {
            return !string.IsNullOrEmpty(s) && "AEIOUaeiou".IndexOf(s[0]) >= 0;
        }

        /// <summary>
        /// Adds editors for the template's primary fields. Description is shown first as a
        /// text area if alwaysShowDescription is true or it's a primary field.
        /// </summary>
        /// <param name="excludeTitle">Optional filter for primary fields the caller draws itself.</param>
        protected void AddPrimaryFields(VisualElement parent, SerializedProperty fieldsProperty, bool alwaysShowDescription, Func<string, bool> excludeTitle = null)
        {
            var primaryFieldTitles = GetPrimaryFieldTitles(window.Template, GetAssetObject(state.selectedIndex)) ?? new List<string>();

            var descriptionProperty = FieldPropertyUtility.FindField(fieldsProperty, DialogueSystemFields.Description);
            if (descriptionProperty != null && (alwaysShowDescription || primaryFieldTitles.Contains(DialogueSystemFields.Description)) &&
                (excludeTitle == null || !excludeTitle(DialogueSystemFields.Description)))
            {
                parent.Add(CreateTextAreaLabel("Description", null));
                parent.Add(FieldPropertyUtility.CreateTextField(null, descriptionProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), true));
            }

            for (int i = 0; i < fieldsProperty.arraySize; i++)
            {
                var fieldProperty = fieldsProperty.GetArrayElementAtIndex(i);
                var title = fieldProperty.FindPropertyRelative(FieldPropertyUtility.TitlePropertyName).stringValue;
                if (string.IsNullOrEmpty(title) || title == DialogueSystemFields.Description || !primaryFieldTitles.Contains(title)) continue;
                if (excludeTitle != null && excludeTitle(title)) continue;
                var isTextArea = FieldPropertyUtility.GetEffectiveTypeString(fieldProperty) == "CustomFieldType_Text" && FieldPropertyUtility.IsTextAreaField(title);
                parent.Add(FieldPropertyUtility.CreateValueEditor(fieldProperty, database, title, isTextArea));
            }
        }

        /// <summary>
        /// Adds a "Use Display Name" toggle, and a Display Name field when it's ticked.
        /// </summary>
        protected void AddDisplayNameFields(VisualElement parent, SerializedProperty fieldsProperty)
        {
            const string DisplayNameFieldTitle = "Display Name";
            var displayNameProperty = FieldPropertyUtility.FindField(fieldsProperty, DisplayNameFieldTitle);
            var toggle = new UnityEngine.UIElements.Toggle("Use Display Name") { value = displayNameProperty != null, tooltip = "Tick to use a Display Name in UIs that's different from the Name." };
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                if (evt.newValue)
                {
                    var name = FieldPropertyUtility.LookupValue(fieldsProperty, DialogueSystemFields.Name);
                    FieldPropertyUtility.SetFieldValue(fieldsProperty, DisplayNameFieldTitle, name, FieldType.Text);
                }
                else
                {
                    FieldPropertyUtility.RemoveField(fieldsProperty, DisplayNameFieldTitle);
                }
                DeferRebuildDetails();
            });
            parent.Add(toggle);
            if (displayNameProperty != null)
            {
                var displayNameField = FieldPropertyUtility.CreateTextField("Display Name", displayNameProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName), false);
                displayNameField.tooltip = "The name to show in UIs.";
                parent.Add(DialogueDatabaseEditorWindow.WithAIButton(displayNameField, CreateAIReviseTextButton(DisplayNameFieldTitle)));
            }
        }

        // The CreateOptional* editors below show a field that may not exist yet. They add the
        // field on the first edit instead of when drawn, so viewing an asset doesn't modify it.

        /// <summary>
        /// Text editor for a field that may not exist. If multiline, returns a label above a text area.
        /// </summary>
        protected VisualElement CreateOptionalTextField(string label, string tooltip, SerializedProperty fieldsProperty, string title, bool multiline)
        {
            var textField = FieldPropertyUtility.CreateTextField(multiline ? null : label, multiline);
            textField.tooltip = tooltip;
            var fieldProperty = FieldPropertyUtility.FindField(fieldsProperty, title);
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
                    var newFieldProperty = FieldPropertyUtility.AddField(fieldsProperty, title, evt.newValue, FieldType.Text);
                    serializedObject.ApplyModifiedProperties();
                    AcknowledgeFieldCountChange(fieldsProperty); // Don't rebuild (and lose focus) while typing.
                    textField.BindProperty(newFieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName));
                    textField.RegisterCallback<FocusOutEvent>(e => DeferRebuildDetails()); // Show the new field in All Fields.
                };
                textField.RegisterValueChangedCallback(onFirstChange);
            }
            if (!multiline) return textField;
            var container = new VisualElement();
            container.Add(CreateTextAreaLabel(label, tooltip));
            container.Add(textField);
            return container;
        }

        /// <summary>
        /// Toggle for a Boolean field that may not exist. Unticked if missing.
        /// </summary>
        /// <param name="onChanged">Called after the value is written. If null, the details rebuild when the field is added.</param>
        protected UnityEngine.UIElements.Toggle CreateOptionalToggle(string label, string tooltip, SerializedProperty fieldsProperty, string title, Action<bool> onChanged = null)
        {
            var toggle = new UnityEngine.UIElements.Toggle(label) { tooltip = tooltip };
            toggle.SetValueWithoutNotify(FieldPropertyUtility.StringToBool(FieldPropertyUtility.LookupValue(fieldsProperty, title)));
            toggle.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var hadField = FieldPropertyUtility.FindField(fieldsProperty, title) != null;
                FieldPropertyUtility.SetFieldValue(fieldsProperty, title, FieldPropertyUtility.BoolToString(evt.newValue), FieldType.Boolean);
                if (onChanged != null) onChanged(evt.newValue);
                else if (!hadField) DeferRebuildDetails();
            });
            var fieldProperty = FieldPropertyUtility.FindField(fieldsProperty, title);
            if (fieldProperty != null)
            {
                toggle.TrackPropertyValue(fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName),
                    property => toggle.SetValueWithoutNotify(FieldPropertyUtility.StringToBool(property.stringValue)));
            }
            return toggle;
        }

        /// <summary>
        /// Quest state popup for a field that may not exist. Shows "unassigned" if missing.
        /// </summary>
        protected PopupField<string> CreateOptionalQuestStatePopup(string label, string tooltip, SerializedProperty fieldsProperty, string title)
        {
            const string DefaultState = "unassigned";
            var choices = FieldPropertyUtility.QuestStateStrings;
            Func<string, string> toChoice = value => (value == null) ? DefaultState
                : (string.IsNullOrEmpty(value) || !choices.Contains(value)) ? choices[0] : value;
            var popup = new PopupField<string>(label, new List<string>(choices), toChoice(FieldPropertyUtility.LookupValue(fieldsProperty, title))) { tooltip = tooltip };
            popup.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                var hadField = FieldPropertyUtility.FindField(fieldsProperty, title) != null;
                var value = (evt.newValue == choices[0]) ? string.Empty : evt.newValue;
                FieldPropertyUtility.SetFieldValue(fieldsProperty, title, value, FieldType.Text);
                if (!hadField) DeferRebuildDetails();
            });
            var fieldProperty = FieldPropertyUtility.FindField(fieldsProperty, title);
            if (fieldProperty != null)
            {
                popup.TrackPropertyValue(fieldProperty.FindPropertyRelative(FieldPropertyUtility.ValuePropertyName),
                    property => popup.SetValueWithoutNotify(toChoice(property.stringValue)));
            }
            return popup;
        }

        /// <summary>
        /// Records the current field count so the external-change poll doesn't rebuild the
        /// details for a field this panel just added.
        /// </summary>
        protected void AcknowledgeFieldCountChange(SerializedProperty fieldsProperty)
        {
            lastFieldCount = fieldsProperty.arraySize;
        }

        protected void DeferRebuildDetails()
        {
            // Defer so the rebuild doesn't remove the element whose event is being handled.
            schedule.Execute(() =>
            {
                RefreshList();
                RebuildDetails();
            });
        }

        #endregion

        #region Refresh

        public override void OnDatabaseChanged(SerializedObject serializedObject)
        {
            this.serializedObject = serializedObject;
            if (window == null) return;
            if (!addedListFooterButtons && listFooter != null)
            {
                addedListFooterButtons = true;
                var extraButtons = new VisualElement();
                extraButtons.style.flexDirection = FlexDirection.Row;
                AddListFooterButtons(extraButtons);
                listFooter.Insert(0, extraButtons);
            }
            searchField.SetValueWithoutNotify(state.filter);
            RefreshAll();
        }

        public override void OnUndoRedo()
        {
            if (database != null && selectedAssetID != -1)
            {
                var index = GetAssets(database).FindIndex(asset => asset.id == selectedAssetID);
                if (index != -1) state.selectedIndex = index;
            }
            RefreshAll();
        }

        private void RefreshAll()
        {
            var listProperty = GetListProperty();
            if (listProperty != null && state.selectedIndex >= listProperty.arraySize) state.selectedIndex = listProperty.arraySize - 1;
            RecordSyncedIDs();
            RefreshSyncRow();
            RefreshList();
            RebuildDetails();
        }

        /// <summary>
        /// Bound fields update themselves, but structural changes made elsewhere (such as
        /// in the IMGUI Dialogue Editor) need the list or details to be rebuilt.
        /// </summary>
        private void PollForExternalChanges()
        {
            if (serializedObject == null || serializedObject.targetObject == null || window == null) return;
            if (resolvedStyle.display == DisplayStyle.None) return;
            serializedObject.UpdateIfRequiredOrScript();
            var listProperty = GetListProperty();
            if (listProperty.arraySize != lastAssetCount)
            {
                RefreshAll();
                return;
            }
            if (0 <= state.selectedIndex && state.selectedIndex < listProperty.arraySize)
            {
                var assetProperty = listProperty.GetArrayElementAtIndex(state.selectedIndex);
                var fieldCount = assetProperty.FindPropertyRelative("fields").arraySize;
                if (fieldCount != lastFieldCount)
                {
                    RebuildDetails();
                    return;
                }
                OnPollExternalChanges(assetProperty);
            }
        }

        /// <summary>
        /// Called every poll interval with the selected asset, after the base checks, so
        /// subclasses can detect other structural changes made elsewhere.
        /// </summary>
        protected virtual void OnPollExternalChanges(SerializedProperty assetProperty) { }

        #endregion

    }

}

#endif
