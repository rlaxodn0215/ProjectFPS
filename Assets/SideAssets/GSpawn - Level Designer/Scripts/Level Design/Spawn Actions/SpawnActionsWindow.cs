#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GSPAWN
{
    public class SpawnActionsWindow : PluginWindow
    {
        private const float PreviewWidth = 100.0f;
        private const float PreviewHeight = 126.0f;
        private const float PreviewGridHeight = (PreviewHeight + 6.0f) * 2.0f + 8.0f;
        private const float SplitterWidth = 4.0f;
        private const float MinLibraryWidth = 300.0f;
        private const float MinOutputsWidth = 380.0f;
        private const float MinVariationsWidth = 270.0f;
        private const double ContextHighlightDuration = 2.0;
        private const double ContextHighlightFrequency = 8.0;

        [SerializeField] private SpawnAction _activeAction;
        [SerializeField] private float _libraryWidth = 320.0f;
        [SerializeField] private float _variationWidth = 320.0f;
        [SerializeField] private string _search = string.Empty;
        [SerializeField] private Vector2 _actionScroll;
        [SerializeField] private Vector2 _detailsScroll;
        [SerializeField] private Vector2 _variationDetailsScroll;
        [SerializeField] private Vector2 _outputScroll;
        [SerializeField] private Vector2 _variationScroll;
        [SerializeField] private string _status = string.Empty;
        [SerializeField] private List<int> _selectedOutputs = new List<int>();
        [SerializeField] private List<int> _selectedVariations = new List<int>();
        [NonSerialized] private bool _pickingContext;
        [NonSerialized] private GameObject _highlightedContext;
        [NonSerialized] private double _contextHighlightStartTime;
        private readonly ObjectOutline _contextOutline = new ObjectOutline();
        private readonly List<GameObject> _contextOutlineObjects = new List<GameObject>(1);
#if UNITY_2022_1_OR_NEWER
        private Renderer[] _contextRenderers = new Renderer[0];
#endif

        private List<SpawnAction> _actions = new List<SpawnAction>();
        private readonly List<GameObject> _selection = new List<GameObject>();
        private readonly List<GameObject> _recordingOutputs = new List<GameObject>();
        private IMGUIListView<SpawnAction> _list;
        [NonSerialized] private SearchField _actionSearchField;
        [NonSerialized] private GUIStyle _contextCaptionStyle;
        [NonSerialized] private GUIStyle _contextNameStyle;
        private readonly Dictionary<object, PrefabPreview> _prefabPreviews = new Dictionary<object, PrefabPreview>();
        private object _rotatingSlot;
        private GameObject _rotatingPrefab;
        private int _rotatingGrid;
        private int _variationGridControlId;
        private readonly object _copyPasteToken = new object();
        private readonly List<SpawnActionPrefabVariation> _copiedVariations = new List<SpawnActionPrefabVariation>();
        private readonly HashSet<SpawnActionPrefabVariation> _copiedVariationSources = new HashSet<SpawnActionPrefabVariation>();
        private SpawnAction _copySourceAction;
        private SpawnActionOutput _copySourceOutput;
        private SpawnAction _pasteVariationAction;
        private SpawnActionOutput _pasteVariationOutput;
        private GUIStyle _copySourceNameStyle;

        [MenuItem("Tools/GSpawn/Windows/Spawn Actions...", false, 50)]
        private static void open() { PluginWindow.show<SpawnActionsWindow>("Spawn Actions"); }

        protected override void onBuildUI() { }
        protected override void onEnabled()
        {
            minSize = new Vector2(MinLibraryWidth + MinOutputsWidth + MinVariationsWidth + SplitterWidth * 2.0f, 360.0f);
            setupList();
            reloadActions();
            SceneView.beforeSceneGui += onSceneGUI;
            SceneView.duringSceneGui += drawContextHighlight;
            Undo.undoRedoPerformed += onUndoRedo;
        }
        protected override void onDisabled()
        {
            if (UICopyPaste.isActive && UICopyPaste.initiatorObject == _copyPasteToken) UICopyPaste.cancel();
            SceneView.beforeSceneGui -= onSceneGUI;
            SceneView.duringSceneGui -= drawContextHighlight;
            Undo.undoRedoPerformed -= onUndoRedo;
            cancelContextPick();
            clearContextHighlight();
            releasePreviews();
        }
        private void onUndoRedo()
        {
            EditorApplication.delayCall += () =>
            {
                if (!this) return;
                reloadActions();
                Repaint();
            };
        }

        protected override void onProjectChanged()
        {
            releasePreviews();
            reloadActions();
            Repaint();
        }

        private void setupList()
        {
            if (_list != null) return;
            _list = new IMGUIListView<SpawnAction>();
            _list.rowHeight = 24.0f;
            _list.showBorder = false;
            _list.itemPrefixWidth = 21.0f;
            _list.itemSuffixWidth = 128.0f;
            _list.imageSize = 0.0f;
            _list.renameOnDoubleClick = true;
            _list.canRenameItem = action => action;
            _list.renameItem = (action, name) =>
            {
                if (!SpawnActionService.renameAction(action, name, out _status) &&
                    string.IsNullOrEmpty(_status)) _status = "Unable to rename action.";
                reloadActions();
            };
            _list.getItemData = (action, data) =>
            {
                data.label = action ? action.name : "Missing Action";
                data.tooltip = action && action.isRecorded ?
                    "Context: " + (!string.IsNullOrEmpty(action.contextName) ? action.contextName :
                        (action.contextPrefab ? action.contextPrefab.name : "N/A")) : "Context: N/A";
                data.image = null;
            };
            _list.drawItemPrefix = (action, rect, selected, hovered) =>
            {
                if (!action) return;
                Rect buttonRect = new Rect(rect.x + 3, rect.y + 4, 16, 16);
                Texture2D texture = action.pinned ? TexturePool.instance.pin : TexturePool.instance.pin_Disabled;
                if (GUI.Button(buttonRect, new GUIContent(string.Empty, "Pin this action to keep it at the top of the list and visible during searches; click again to unpin."), GUIStyle.none))
                {
                    Undo.RecordObject(action, "Pin Spawn Action");
                    action.pinned = !action.pinned;
                    EditorUtility.SetDirty(action);
                    EditorApplication.delayCall += () =>
                    {
                        if (this) { reloadActions(); Repaint(); }
                    };
                }
                if (texture) GUI.DrawTexture(buttonRect, texture, ScaleMode.ScaleToFit, true);
            };
            _list.drawItemSuffix = (action, rect, selected, hovered) =>
            {
                if (!action) return;
                using (new EditorGUI.DisabledScope(!action.isRecorded))
                {
                    Rect applyRect = new Rect(rect.x + 1, rect.y + 3, 51, 18);
                    Rect refreshRect = new Rect(rect.x + 54, rect.y + 3, 72, 18);
                    if (GUI.Button(applyRect, new GUIContent("Apply", "Apply this Spawn Action to selected matching context objects, replacing existing generated outputs."), EditorStyles.miniButton))
                    {
                        selectAction(action);
                        applyAction(action, false);
                    }
                    if (GUI.Button(refreshRect, new GUIContent("Refresh", "Regenerate this action only on selected contexts where it has already been applied."), EditorStyles.miniButton))
                    {
                        selectAction(action);
                        applyAction(action, true);
                    }
                }
            };
            _list.selectionChanged = selection =>
            {
                SpawnAction chosen = selection.Count == 1 ? selection[0] : null;
                if (_activeAction != chosen)
                {
                    _activeAction = chosen;
                    _selectedOutputs.Clear();
                    _selectedVariations.Clear();
                }
                Repaint();
            };
            _list.itemContextClicked = action =>
            {
                if (!action) return;
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Rename", "Rename this Spawn Action"), false, () =>
                {
                    selectAction(action);
                    _list.BeginRename(action);
                    Repaint();
                });
                menu.ShowAsContext();
            };
            _list.deleteSelected = deleteSelectedActions;
        }

        private void applyAction(SpawnAction action, bool regenerateOnly)
        {
            int changed = SpawnActionService.applyToSelection(action, regenerateOnly, out _status);
            if (changed == 0 || _status.Contains("No objects were generated"))
                ShowNotification(new GUIContent(_status));
        }

        private void reloadActions()
        {
            setupList();
            _actions = SpawnActionService.getActions();
            _actions.Sort((a, b) =>
            {
                int pinned = b.pinned.CompareTo(a.pinned);
                return pinned != 0 ? pinned : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
            });
            _list.SetItems(_actions);
            _list.model.FilterItems(a => a && (a.pinned || string.IsNullOrEmpty(_search) ||
                a.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0));
            if (_activeAction && _actions.Contains(_activeAction)) _list.SelectOnlyItem(_activeAction);
            else _activeAction = null;
        }

        protected override void onGUI()
        {
            if (!GSpawn.active)
            {
                EditorGUILayout.HelpBox("Initialize GSpawn to use Spawn Actions.", MessageType.Info);
                return;
            }
            setupList();
            _libraryWidth = Mathf.Clamp(_libraryWidth, MinLibraryWidth,
                Mathf.Max(MinLibraryWidth, position.width - MinOutputsWidth - MinVariationsWidth - SplitterWidth * 2.0f));
            _variationWidth = Mathf.Clamp(_variationWidth, MinVariationsWidth,
                Mathf.Max(MinVariationsWidth, position.width - _libraryWidth - MinOutputsWidth - SplitterWidth * 2.0f));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(_libraryWidth), GUILayout.ExpandHeight(true));
            drawLibrary();
            EditorGUILayout.EndVertical();
            drawSplitter(true);

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            _detailsScroll = EditorGUILayout.BeginScrollView(_detailsScroll);
            drawOutputsPane();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            drawSplitter(false);

            EditorGUILayout.BeginVertical(GUILayout.Width(_variationWidth), GUILayout.ExpandHeight(true));
            _variationDetailsScroll = EditorGUILayout.BeginScrollView(_variationDetailsScroll);
            drawVariationsPane();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void drawSplitter(bool librarySplitter)
        {
            Rect rect = GUILayoutUtility.GetRect(SplitterWidth, 1.0f,
                GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.center.x, rect.y, 1.0f, rect.height), IMGUIViewUtility.borderColor);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            GUI.Label(rect, new GUIContent(string.Empty, librarySplitter ?
                "Drag to resize the Spawn Actions list." : "Drag to resize the Prefab Variations pane."), GUIStyle.none);
            int id = GUIUtility.GetControlID(FocusType.Passive, rect);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = id;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
            {
                if (librarySplitter)
                    _libraryWidth = Mathf.Clamp(_libraryWidth + e.delta.x, MinLibraryWidth,
                        Mathf.Max(MinLibraryWidth, position.width - _variationWidth - MinOutputsWidth - SplitterWidth * 2.0f));
                else
                    _variationWidth = Mathf.Clamp(_variationWidth - e.delta.x, MinVariationsWidth,
                        Mathf.Max(MinVariationsWidth, position.width - _libraryWidth - MinOutputsWidth - SplitterWidth * 2.0f));
                Repaint();
                e.Use();
            }
            else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
            {
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private void drawLibrary()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Spawn Actions", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent("+", "Create a new empty Spawn Action"), EditorStyles.toolbarButton,
                GUILayout.Width(25)))
            {
                SpawnAction created = SpawnActionService.createAction("New Spawn Action", out _status);
                if (created)
                {
                    _search = string.Empty;
                    _activeAction = created;
                    reloadActions();
                    _list.BeginRename(created);
                }
            }
            EditorGUILayout.EndHorizontal();

            Rect searchArea = GUILayoutUtility.GetRect(_libraryWidth - 8.0f, 21.0f,
                GUILayout.Width(_libraryWidth - 8.0f), GUILayout.Height(21.0f));
            if (_actionSearchField == null) _actionSearchField = new SearchField();
            Rect searchRect = new Rect(searchArea.x + 3.0f, searchArea.y + 1.0f,
                searchArea.width, 19.0f);
            string search = _actionSearchField.OnGUI(searchRect, _search);
            GUI.Label(searchRect, new GUIContent(string.Empty,
                "Filter Spawn Actions by name. Pinned actions remain visible."), GUIStyle.none);
            if (_search != search)
            {
                _search = search;
                _list.model.FilterItems(a => a && (a.pinned || string.IsNullOrEmpty(_search) ||
                    a.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            _actionScroll = _list.OnGUI(_actionScroll, focusedWindow == this);
        }

        private void selectAction(SpawnAction action)
        {
            _activeAction = action;
            _list.SelectOnlyItem(action);
            _selectedOutputs.Clear();
            _selectedVariations.Clear();
        }

        private void deleteSelectedActions()
        {
            IReadOnlyList<SpawnAction> selected = _list.selection;
            if (selected.Count == 0) return;

            if (!SpawnActionService.deleteActions(selected, out _status)) return;

            _activeAction = null;
            _selectedOutputs.Clear();
            _selectedVariations.Clear();
            reloadActions();
        }

        private void drawOutputsPane()
        {
            if (_list.model.selectedItemCount != 1 || !_activeAction)
            {
                EditorGUILayout.HelpBox("Select one Spawn Action to edit its details.", MessageType.Info);
                return;
            }
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Selected Action", EditorStyles.boldLabel);
            SpawnActionService.getRecordingSelection(_selection);
            using (new EditorGUI.DisabledScope(_pickingContext || _selection.Count == 0))
            {
                if (GUILayout.Button(new GUIContent("Record From Selection",
                    "Record the currently selected scene prefab instances as outputs. Then click their context object in the Scene view."),
                    GUILayout.Height(25))) beginContextPick();
            }
            if (_pickingContext)
            {
                EditorGUILayout.HelpBox("Recording " + _recordingOutputs.Count + " output(s). Click one context object in the Scene view. Escape/right-click cancels.", MessageType.Info);
                if (GUILayout.Button(new GUIContent("Cancel Context Pick", "Cancel recording without changing the selected Spawn Action."))) cancelContextPick();
            }
            else
                EditorGUILayout.HelpBox("Select output prefab instances, press Record From Selection, then pick the context in the Scene view.", MessageType.None);

            if (_activeAction.isRecorded)
            {
                if (_contextCaptionStyle == null)
                {
                    _contextCaptionStyle = new GUIStyle(EditorStyles.boldLabel);
                    _contextCaptionStyle.normal.textColor = Color.white;
                    _contextNameStyle = new GUIStyle(EditorStyles.boldLabel);
                    _contextNameStyle.normal.textColor = new Color(0.35f, 0.85f, 0.45f, 1.0f);
                }
                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Context:", _contextCaptionStyle, GUILayout.ExpandWidth(false));
                GUILayout.Label(_activeAction.contextName, _contextNameStyle, GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space(4);
            bool avoid = EditorGUILayout.ToggleLeft(new GUIContent("Avoid Output Overlaps",
                "Prevent outputs generated for different contexts from intersecting. Outputs belonging to the same context may overlap."),
                _activeAction.avoidOutputOverlaps);
            if (avoid != _activeAction.avoidOutputOverlaps)
            {
                editAction("Change Spawn Action Overlap");
                _activeAction.avoidOutputOverlaps = avoid;
            }
            EditorGUILayout.Space(9);
            EditorGUILayout.LabelField("Outputs", EditorStyles.boldLabel);
            if (_activeAction.outputs.Count > 0)
            {
                drawPreviewGrid(_activeAction.outputs.Count, i => _activeAction.outputs[i],
                    i => _activeAction.outputs[i].prefabAsset,
                    i => _selectedOutputs.Contains(i), i => changeSelection(_selectedOutputs, i, _activeAction.outputs.Count, true),
                    ref _outputScroll, PreviewGridHeight, 1);
                if (_selectedOutputs.Count > 0) drawOutputSettings();
            }
        }

        private PrefabPreview getPrefabPreview(object slot, GameObject prefab)
        {
            if (slot == null || !prefab) return null;
            PrefabPreview preview;
            if (!_prefabPreviews.TryGetValue(slot, out preview))
            {
                if (Event.current.type != EventType.Repaint) return null;
                preview = new PrefabPreview();
                preview.setPrefab(prefab);
                _prefabPreviews.Add(slot, preview);
            }
            else if (preview.prefabAsset != prefab && Event.current.type == EventType.Repaint)
                preview.setPrefab(prefab);
            return preview.prefabAsset == prefab ? preview : null;
        }

        private void releasePreviews()
        {
            foreach (PrefabPreview preview in _prefabPreviews.Values)
                preview.releaseTexture();
            _prefabPreviews.Clear();
            _rotatingSlot = null;
            _rotatingPrefab = null;
            _rotatingGrid = 0;
        }

        private static void drawGridBorder(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color border = IMGUIViewUtility.borderColor;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1.0f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1.0f, rect.width, 1.0f), border);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1.0f, rect.height), border);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1.0f, rect.y, 1.0f, rect.height), border);
        }

        private static void drawTileBorder(Rect tile, bool selected, bool copySource)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color border = copySource ? UIValues.copySourceGridItemTextColor :
                (selected ? new Color(0.37f, 0.65f, 1.0f, 1.0f) : IMGUIViewUtility.borderColor);
            float thickness = selected || copySource ? 2.0f : 1.0f;
            EditorGUI.DrawRect(new Rect(tile.x, tile.y, tile.width, thickness), border);
            EditorGUI.DrawRect(new Rect(tile.x, tile.yMax - thickness, tile.width, thickness), border);
            EditorGUI.DrawRect(new Rect(tile.x, tile.y, thickness, tile.height), border);
            EditorGUI.DrawRect(new Rect(tile.xMax - thickness, tile.y, thickness, tile.height), border);
        }

        private void drawPrefabTile(Rect tile, object slot, GameObject prefab, bool selected, int grid,
            bool copySource)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(tile, copySource ? new Color(0.11f, 0.42f, 0.18f, 0.75f) :
                (selected ? new Color(0.24f, 0.47f, 0.74f, 0.5f) : new Color(0.15f, 0.15f, 0.15f, 0.45f)));
            PrefabPreview preview = getPrefabPreview(slot, prefab);
            if (preview != null && preview.texture)
                GUI.DrawTexture(new Rect(tile.x + 4, tile.y + 4, tile.width - 8, 94),
                    preview.texture, ScaleMode.ScaleToFit);
            if (_copySourceNameStyle == null)
            {
                _copySourceNameStyle = new GUIStyle(EditorStyles.miniLabel);
                _copySourceNameStyle.normal.textColor = UIValues.copySourceGridItemTextColor;
            }
            GUI.Label(new Rect(tile.x + 3, tile.y + 100, tile.width - 6, 21),
                prefab ? prefab.name : "Missing Prefab",
                copySource ? _copySourceNameStyle : EditorStyles.miniLabel);
            string tooltip = grid == 1 ?
                "Select an output slot to edit its spawn settings. Ctrl/Cmd-click or Shift-click to select multiple outputs. Middle-mouse drag rotates its preview." :
                "Select a prefab variation to edit its weight. Ctrl/Cmd-click or Shift-click selects multiple variations. Middle-mouse drag rotates its preview. Delete removes selected variations.";
            if (prefab) tooltip = prefab.name + "\n" + tooltip;
            if (copySource) tooltip += "\nCopy source";
            GUI.Label(tile, new GUIContent(string.Empty, tooltip), GUIStyle.none);
            drawTileBorder(tile, selected, copySource);
        }

        private static int getHoveredTile(Rect view, Vector2 scroll, Vector2 mousePosition,
            int count, int columns)
        {
            if (!view.Contains(mousePosition)) return -1;
            Vector2 local = mousePosition - view.position + scroll;
            const float spacing = 6.0f;
            int col = Mathf.FloorToInt((local.x - 4.0f) / (PreviewWidth + spacing));
            int row = Mathf.FloorToInt((local.y - 4.0f) / (PreviewHeight + spacing));
            if (col < 0 || col >= columns || row < 0) return -1;
            int index = row * columns + col;
            if (index >= count) return -1;
            Rect tile = new Rect(4.0f + col * (PreviewWidth + spacing),
                4.0f + row * (PreviewHeight + spacing), PreviewWidth, PreviewHeight);
            return tile.Contains(local) ? index : -1;
        }

        private void handlePreviewGridInput(Rect view, Vector2 scroll, int count, int columns,
            int grid, int controlId, Func<int, object> slotAt, Func<int, GameObject> prefabAt,
            Action<int> onSelected)
        {
            Event e = Event.current;

            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == controlId)
            {
                Action copy = grid == 2 && _selectedVariations.Count > 0 ?
                    (Action)(() => copySelectedVariations(new List<int>(_selectedVariations))) : null;
                Action paste = canPasteIntoSelectedOutput() ? (Action)pasteVariationsIntoSelectedOutput : null;
                if (IMGUIViewUtility.HandleCopyPasteShortcut(e, copy, null, paste)) return;
                if (e.keyCode == KeyCode.Escape && canPasteVariations())
                {
                    UICopyPaste.cancel();
                    e.Use();
                    Repaint();
                    return;
                }
            }

            if (_rotatingGrid == grid && GUIUtility.hotControl == controlId)
            {
                if (e.type == EventType.MouseDrag)
                {
                    PrefabPreview preview = getPrefabPreview(_rotatingSlot, _rotatingPrefab);
                    if (preview != null) preview.rotate(e.delta);
                    Repaint();
                    e.Use();
                    return;
                }
                if (e.type == EventType.MouseUp || e.type == EventType.Ignore)
                {
                    GUIUtility.hotControl = 0;
                    _rotatingSlot = null;
                    _rotatingPrefab = null;
                    _rotatingGrid = 0;
                    if (e.type == EventType.MouseUp) e.Use();
                    return;
                }
            }

            if (e.type != EventType.MouseDown && e.type != EventType.ContextClick) return;
            int index = getHoveredTile(view, scroll, e.mousePosition, count, columns);
            if (index < 0)
            {
                if (grid == 2 && view.Contains(e.mousePosition))
                {
                    if (e.type == EventType.ContextClick)
                    {
                        showPrefabContextMenu(new List<GameObject>(), grid, -1);
                        e.Use();
                    }
                    else if (e.button == 0 && !e.alt)
                    {
                        GUIUtility.keyboardControl = controlId;
                        _selectedVariations.Clear();
                        Repaint();
                        e.Use();
                    }
                }
                return;
            }

            if (e.type == EventType.ContextClick)
            {
                List<int> selectedIndices = grid == 1 ? _selectedOutputs : _selectedVariations;
                var prefabAssets = new List<GameObject>();
                if (selectedIndices.Contains(index))
                {
                    foreach (int selected in selectedIndices)
                    {
                        if (selected < 0 || selected >= count) continue;
                        GameObject prefabAsset = prefabAt(selected);
                        if (prefabAsset && !prefabAssets.Contains(prefabAsset)) prefabAssets.Add(prefabAsset);
                    }
                }
                else
                {
                    GameObject prefabAsset = prefabAt(index);
                    if (prefabAsset) prefabAssets.Add(prefabAsset);
                }
                showPrefabContextMenu(prefabAssets, grid, index);
                e.Use();
                return;
            }

            if (e.button == 2 && !e.alt && !e.shift && !e.control && !e.command && prefabAt(index))
            {
                GUIUtility.hotControl = controlId;
                _rotatingSlot = slotAt(index);
                _rotatingPrefab = prefabAt(index);
                _rotatingGrid = grid;
                e.Use();
            }
            else if (e.button == 0 && !e.alt)
            {
                GUIUtility.keyboardControl = controlId;
                onSelected(index);
                e.Use();
            }
        }

        private void showPrefabContextMenu(List<GameObject> prefabAssets, int grid, int clickedIndex)
        {
            PrefabLibProfile profile = PrefabLibProfileDb.instance.activeProfile;
            var available = new List<GameObject>();
            if (profile != null)
            {
                foreach (GameObject asset in prefabAssets)
                    if (asset && profile.getPrefab(asset) != null && !available.Contains(asset))
                        available.Add(asset);
            }
            var menu = new GenericMenu();
            if (grid == 2)
            {
                var indices = new List<int>();
                if (clickedIndex >= 0)
                {
                    if (_selectedVariations.Contains(clickedIndex)) indices.AddRange(_selectedVariations);
                    else indices.Add(clickedIndex);
                }
                if (indices.Count > 0)
                    menu.AddItem(new GUIContent("Copy Variations"), false,
                        () => copySelectedVariations(indices));
                else menu.AddDisabledItem(new GUIContent("Copy Variations"));

                if (canPasteIntoSelectedOutput())
                    menu.AddItem(new GUIContent("Paste Variations"), false, pasteVariationsIntoSelectedOutput);
                else menu.AddDisabledItem(new GUIContent("Paste Variations"));
                if (clickedIndex >= 0) menu.AddSeparator("");
            }
            if (clickedIndex >= 0)
            {
                if (available.Count > 0)
                {
                    string label = available.Count > 1 ? "Select in Prefab Manager (" + available.Count + ")" :
                        "Select in Prefab Manager";
                    menu.AddItem(new GUIContent(label), false, () => selectPrefabsInManager(available));
                }
                else menu.AddDisabledItem(new GUIContent("Select in Prefab Manager"));
            }
            menu.ShowAsContext();
        }

        private bool canPasteVariations()
        {
            return UICopyPaste.isActive && UICopyPaste.initiatorObject == _copyPasteToken &&
                UICopyPaste.copyPasteMode == CopyPasteMode.Copy && _copiedVariations.Count > 0;
        }

        private bool canPasteIntoSelectedOutput()
        {
            if (!canPasteVariations() || !_activeAction || _selectedOutputs.Count != 1) return false;
            int index = _selectedOutputs[0];
            return index >= 0 && index < _activeAction.outputs.Count &&
                _activeAction.outputs[index] != null &&
                (_activeAction != _copySourceAction || _activeAction.outputs[index] != _copySourceOutput);
        }

        private void copySelectedVariations(List<int> indices)
        {
            if (!_activeAction || _selectedOutputs.Count != 1) return;
            int outputIndex = _selectedOutputs[0];
            if (outputIndex < 0 || outputIndex >= _activeAction.outputs.Count) return;
            SpawnActionOutput output = _activeAction.outputs[outputIndex];
            if (output == null) return;
            output.ensureLegacyVariations();

            var copied = new List<SpawnActionPrefabVariation>();
            var sources = new HashSet<SpawnActionPrefabVariation>();
            foreach (int index in indices)
            {
                if (index < 0 || index >= output.variations.Count) continue;
                SpawnActionPrefabVariation variation = output.variations[index];
                if (variation != null && variation.prefabAsset && sources.Add(variation))
                    copied.Add(new SpawnActionPrefabVariation(variation.prefabAsset, variation.weight));
            }
            if (copied.Count == 0) return;

            UICopyPaste.begin(CopyPasteMode.Copy, _copyPasteToken, onPasteVariations, cancelCopyVariations);
            _copiedVariations.AddRange(copied);
            _copiedVariationSources.UnionWith(sources);
            _copySourceAction = _activeAction;
            _copySourceOutput = output;
            Repaint();
        }

        private void pasteVariationsIntoSelectedOutput()
        {
            if (!canPasteIntoSelectedOutput()) return;
            _pasteVariationAction = _activeAction;
            _pasteVariationOutput = _activeAction.outputs[_selectedOutputs[0]];
            UICopyPaste.paste();
        }

        private void onPasteVariations()
        {
            if (!_pasteVariationAction || _pasteVariationOutput == null ||
                _copiedVariations.Count == 0) return;

            _pasteVariationOutput.ensureLegacyVariations();
            bool recordedUndo = false;
            _selectedVariations.Clear();
            foreach (SpawnActionPrefabVariation variation in _copiedVariations)
            {
                if (!variation.prefabAsset || _pasteVariationOutput.containsPrefab(variation.prefabAsset))
                    continue;
                if (!recordedUndo)
                {
                    Undo.RecordObject(_pasteVariationAction, "Paste Prefab Variations");
                    EditorUtility.SetDirty(_pasteVariationAction);
                    recordedUndo = true;
                }
                _selectedVariations.Add(_pasteVariationOutput.variations.Count);
                _pasteVariationOutput.variations.Add(new SpawnActionPrefabVariation(variation.prefabAsset, variation.weight));
            }
            if (!recordedUndo)
                ShowNotification(new GUIContent("All copied variations already exist in this output."));
            Repaint();
        }

        private void cancelCopyVariations()
        {
            _copiedVariations.Clear();
            _copiedVariationSources.Clear();
            _copySourceAction = null;
            _copySourceOutput = null;
            _pasteVariationAction = null;
            _pasteVariationOutput = null;
            Repaint();
        }

        private static void selectPrefabsInManager(List<GameObject> prefabAssets)
        {
            if (prefabAssets.Count == 0) return;
            PluginPrefabManagerWindow window = PluginWindow.show<PluginPrefabManagerWindow>("Prefab Manager");
            focusPrefabsInManager(prefabAssets, window, 3);
        }

        private static void focusPrefabsInManager(List<GameObject> prefabAssets,
            PluginPrefabManagerWindow window, int attempts)
        {
            if (!window) return;
            EditorApplication.delayCall += () =>
            {
                if (!window) return;
                PluginPrefabManagerUI manager = PluginPrefabManagerUI.instance;
                PrefabLibProfileDbUI libraryUI = PrefabLibProfileDbUI.instance;
                if (manager == null || !manager.uiVisibleAndReady ||
                    libraryUI == null || !libraryUI.uiVisibleAndReady)
                {
                    if (attempts > 0)
                    {
                        window.Repaint();
                        focusPrefabsInManager(prefabAssets, window, attempts - 1);
                    }
                    return;
                }
                PrefabLibProfile profile = PrefabLibProfileDb.instance.activeProfile;
                if (profile == null) return;
                var prefabs = new List<PluginPrefab>();
                var libs = new List<PrefabLib>();
                foreach (GameObject asset in prefabAssets)
                {
                    if (!asset) continue;
                    PluginPrefab prefab = profile.getPrefab(asset);
                    if (prefab == null || prefabs.Contains(prefab)) continue;
                    PrefabLib lib = profile.findOwnerLibOfPrefab(prefab);
                    if (lib == null) continue;
                    prefabs.Add(prefab);
                    if (!libs.Contains(lib)) libs.Add(lib);
                }
                if (prefabs.Count == 0) return;

                libraryUI.selectPrefabLibsForBrowsing(libs);
                manager.setTargetLibs(libs);
                manager.selectAndScrollToPrefabs(prefabs);
                window.Focus();
            };
        }

        private Rect drawPreviewGrid(int count, Func<int, object> slotAt, Func<int, GameObject> prefabAt,
            Func<int, bool> isSelected, Action<int> onSelected, ref Vector2 scroll, float height, int grid)
        {
            Rect view = GUILayoutUtility.GetRect(1.0f, height, GUILayout.ExpandWidth(true));
            int controlId = GUIUtility.GetControlID(FocusType.Keyboard, view);
            if (grid == 2) _variationGridControlId = controlId;
            int columns = Mathf.Max(1, Mathf.FloorToInt((view.width - 12.0f) / (PreviewWidth + 6.0f)));
            int rows = (count + columns - 1) / columns;
            float contentHeight = Mathf.Max(view.height - 2.0f, rows * (PreviewHeight + 6.0f));
            Rect content = new Rect(0, 0, Mathf.Max(1.0f, view.width - 16.0f), contentHeight);

            handlePreviewGridInput(view, scroll, count, columns, grid, controlId, slotAt, prefabAt, onSelected);

            scroll = GUI.BeginScrollView(view, scroll, content);
            for (int i = 0; i < count; ++i)
            {
                int col = i % columns;
                int row = i / columns;
                Rect tile = new Rect(4 + col * (PreviewWidth + 6), 4 + row * (PreviewHeight + 6),
                    PreviewWidth, PreviewHeight);
                object slot = slotAt(i);
                bool copySource = canPasteVariations() && _activeAction == _copySourceAction &&
                    (grid == 1 ? ReferenceEquals(slot, _copySourceOutput) :
                    _copySourceOutput != null && _selectedOutputs.Count == 1 &&
                    _selectedOutputs[0] >= 0 && _selectedOutputs[0] < _activeAction.outputs.Count &&
                    ReferenceEquals(_activeAction.outputs[_selectedOutputs[0]], _copySourceOutput) &&
                    slot is SpawnActionPrefabVariation &&
                    _copiedVariationSources.Contains((SpawnActionPrefabVariation)slot));
                drawPrefabTile(tile, slot, prefabAt(i), isSelected(i), grid, copySource);
            }
            GUI.EndScrollView();
            drawGridBorder(view);
            return view;
        }

        private void changeSelection(List<int> selected, int index, int count, bool output)
        {
            Event e = Event.current;
            int anchor = selected.Count > 0 ? selected[selected.Count - 1] : index;
            if (e.shift)
            {
                selected.Clear();
                for (int i = Mathf.Min(index, anchor); i <= Mathf.Max(index, anchor); ++i) selected.Add(i);
            }
            else if (e.control || e.command)
            {
                if (!selected.Remove(index)) selected.Add(index);
            }
            else
            {
                selected.Clear();
                selected.Add(index);
            }
            if (output) _selectedVariations.Clear();
            Repaint();
        }

        private void editAction(string reason)
        {
            Undo.RecordObject(_activeAction, reason);
            EditorUtility.SetDirty(_activeAction);
        }

        private List<SpawnActionOutput> selectedOutputObjects()
        {
            var result = new List<SpawnActionOutput>();
            foreach (int index in _selectedOutputs)
                if (index >= 0 && index < _activeAction.outputs.Count && _activeAction.outputs[index] != null)
                    result.Add(_activeAction.outputs[index]);
            return result;
        }

        private void drawOutputSettings()
        {
            List<SpawnActionOutput> selected = selectedOutputObjects();
            if (selected.Count == 0) return;
            EditorGUILayout.Space(8);

            bool mixed = selected.Exists(o => !Mathf.Approximately(o.spawnChance, selected[0].spawnChance));
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            float chance = EditorGUILayout.Slider(new GUIContent("Spawn Chance", "Probability (0–1) that this output slot spawns when the action is applied or regenerated."), selected[0].spawnChance, 0.0f, 1.0f);
            if (EditorGUI.EndChangeCheck()) { editAction("Change Spawn Chance"); foreach (var o in selected) o.spawnChance = chance; }
            EditorGUI.showMixedValue = false;

            mixed = selected.Exists(o => o.randomizeScale != selected[0].randomizeScale);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            bool scale = EditorGUILayout.Toggle(new GUIContent("Random Uniform Scale", "Randomize the chosen prefab’s uniform scale multiplier within the minimum and maximum values."), selected[0].randomizeScale);
            if (EditorGUI.EndChangeCheck()) { editAction("Change Scale Randomization"); foreach (var o in selected) o.randomizeScale = scale; }
            EditorGUI.showMixedValue = false;
            using (new EditorGUI.DisabledScope(!selected.Exists(o => o.randomizeScale)))
            {
                drawFloatRange(selected, "Min Scale", o => o.minScale, (o, v) => o.minScale = v);
                drawFloatRange(selected, "Max Scale", o => o.maxScale, (o, v) => o.maxScale = v);
            }

            mixed = selected.Exists(o => o.randomizeRotation != selected[0].randomizeRotation);
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            bool rotate = EditorGUILayout.Toggle(new GUIContent("Random Rotation", "Apply random rotation offsets about the active grid axes, within the minimum and maximum Euler angles."), selected[0].randomizeRotation);
            if (EditorGUI.EndChangeCheck()) { editAction("Change Rotation Randomization"); foreach (var o in selected) o.randomizeRotation = rotate; }
            EditorGUI.showMixedValue = false;
            using (new EditorGUI.DisabledScope(!selected.Exists(o => o.randomizeRotation)))
            {
                drawVectorRange(selected, "Min Rotation", o => o.minRotation, (o, v) => o.minRotation = v);
                drawVectorRange(selected, "Max Rotation", o => o.maxRotation, (o, v) => o.maxRotation = v);
            }
        }

        private static string settingTooltip(string label)
        {
            switch (label)
            {
                case "Min Scale": return "Smallest uniform scale multiplier for the selected output slots.";
                case "Max Scale": return "Largest uniform scale multiplier for the selected output slots.";
                case "Min Rotation": return "Minimum random Euler rotation offsets (degrees) about the active grid axes.";
                case "Max Rotation": return "Maximum random Euler rotation offsets (degrees) about the active grid axes.";
                default: return string.Empty;
            }
        }

        private void drawFloatRange(List<SpawnActionOutput> outputs, string label,
            Func<SpawnActionOutput, float> getter, Action<SpawnActionOutput, float> setter)
        {
            EditorGUI.showMixedValue = outputs.Exists(o => !Mathf.Approximately(getter(o), getter(outputs[0])));
            EditorGUI.BeginChangeCheck();
            float v = EditorGUILayout.FloatField(new GUIContent(label, settingTooltip(label)), getter(outputs[0]));
            if (EditorGUI.EndChangeCheck()) { editAction("Change " + label); foreach (var o in outputs) setter(o, v); }
            EditorGUI.showMixedValue = false;
        }

        private void drawVectorRange(List<SpawnActionOutput> outputs, string label,
            Func<SpawnActionOutput, Vector3> getter, Action<SpawnActionOutput, Vector3> setter)
        {
            EditorGUI.showMixedValue = outputs.Exists(o => getter(o) != getter(outputs[0]));
            EditorGUI.BeginChangeCheck();
            Vector3 v = EditorGUILayout.Vector3Field(new GUIContent(label, settingTooltip(label)), getter(outputs[0]));
            if (EditorGUI.EndChangeCheck()) { editAction("Change " + label); foreach (var o in outputs) setter(o, v); }
            EditorGUI.showMixedValue = false;
        }

        private void drawVariationsPane()
        {
            EditorGUILayout.Space(3);
            EditorGUILayout.LabelField("Prefab Variations", EditorStyles.boldLabel);
            if (_list.model.selectedItemCount != 1 || !_activeAction) return;
            drawVariationControls(selectedOutputObjects());
        }

        private void drawVariationControls(List<SpawnActionOutput> outputs)
        {
            EditorGUILayout.Space(6);
            if (outputs.Count != 1)
            {
                EditorGUILayout.HelpBox("Select exactly one output preview to edit its prefab variations.", MessageType.Info);
                return;
            }
            SpawnActionOutput output = outputs[0];
            output.ensureLegacyVariations();
            var variations = output.variations;
            EditorGUILayout.LabelField("Drag prefab assets from Prefab Manager or the Project window into the grid.", EditorStyles.miniLabel);
            Rect dropRect = drawPreviewGrid(variations.Count,
                i => variations[i],
                i => variations[i] != null ? variations[i].prefabAsset : null,
                i => _selectedVariations.Contains(i),
                i => changeSelection(_selectedVariations, i, variations.Count, false),
                ref _variationScroll, PreviewGridHeight, 2);

            Event e = Event.current;
            if (dropRect.Contains(e.mousePosition) &&
                (e.type == EventType.DragUpdated || e.type == EventType.DragPerform))
            {
                var droppedPrefabs = new List<GameObject>();
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    GameObject go = obj as GameObject;
                    if (go && PrefabUtility.IsPartOfPrefabAsset(go) && !output.containsPrefab(go) &&
                        !droppedPrefabs.Contains(go)) droppedPrefabs.Add(go);
                }
                PluginPrefabManagerUI manager = PluginPrefabManagerUI.instance;
                bool prefabManagerDrag = PluginDragAndDrop.initiatedByPlugin && manager != null &&
                    manager.dragAndDropInitiatedByPrefabView();
                if (prefabManagerDrag)
                {
                    var selectedPrefabs = new List<GameObject>();
                    manager.getVisibleSelectedPrefabAssets(selectedPrefabs);
                    foreach (GameObject go in selectedPrefabs)
                    {
                        if (go && !output.containsPrefab(go) && !droppedPrefabs.Contains(go))
                            droppedPrefabs.Add(go);
                    }
                }
                if (droppedPrefabs.Count > 0)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (e.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        editAction("Add Prefab Variations");
                        foreach (GameObject prefab in droppedPrefabs)
                            variations.Add(new SpawnActionPrefabVariation(prefab));
                        if (prefabManagerDrag) PluginDragAndDrop.endDrag();
                    }
                    e.Use();
                }
            }
            if (_selectedVariations.Count > 0)
            {
                int first = _selectedVariations[0];
                if (first >= 0 && first < variations.Count)
                {
                    float firstWeight = variations[first].weight;
                    bool mixed = _selectedVariations.Exists(i => i >= 0 && i < variations.Count &&
                        !Mathf.Approximately(firstWeight, variations[i].weight));
                    EditorGUI.showMixedValue = mixed;
                    EditorGUI.BeginChangeCheck();
                    float weight = EditorGUILayout.FloatField(new GUIContent("Selection Weight", "Relative probability of choosing this prefab for the output slot. Higher weights make it more likely; zero excludes it."), firstWeight);
                    if (EditorGUI.EndChangeCheck())
                    {
                        editAction("Change Variation Weight");
                        foreach (int i in _selectedVariations)
                            if (i >= 0 && i < variations.Count) variations[i].weight = weight;
                    }
                    EditorGUI.showMixedValue = false;
                }
            }
            Event keyEvent = Event.current;
            if (_selectedVariations.Count > 0 && keyEvent.type == EventType.KeyDown &&
                GUIUtility.keyboardControl == _variationGridControlId &&
                (keyEvent.keyCode == KeyCode.Delete || keyEvent.keyCode == KeyCode.Backspace) &&
                !keyEvent.alt && !keyEvent.control && !keyEvent.command)
            {
                editAction("Remove Variations");
                _selectedVariations.Sort();
                for (int i = _selectedVariations.Count - 1; i >= 0; --i)
                {
                    int index = _selectedVariations[i];
                    if (index < 0 || index >= variations.Count) continue;
                    SpawnActionPrefabVariation variation = variations[index];
                    PrefabPreview preview;
                    if (variation != null && _prefabPreviews.TryGetValue(variation, out preview))
                    {
                        preview.releaseTexture();
                        _prefabPreviews.Remove(variation);
                    }
                    variations.RemoveAt(index);
                }
                _selectedVariations.Clear();
                keyEvent.Use();
                Repaint();
            }
            if (variations.Count == 0)
                EditorGUILayout.HelpBox("This output has no prefab variations.", MessageType.Warning);
        }

        private void beginContextPick()
        {
            if (!_activeAction) return;
            SpawnActionService.getRecordingSelection(_recordingOutputs);
            if (!SpawnActionService.validateRecordingOutputs(_recordingOutputs, out _status))
            {
                _recordingOutputs.Clear();
                return;
            }
            _status = string.Empty;
            clearContextHighlight();
            _pickingContext = true;
            SceneView.RepaintAll();
        }

        private void cancelContextPick()
        {
            _pickingContext = false;
            _recordingOutputs.Clear();
            SceneView.RepaintAll();
            Repaint();
        }

        private void showContextHighlight(GameObject context)
        {
            clearContextHighlight();
            if (!context) return;
            _highlightedContext = context;
            _contextHighlightStartTime = EditorApplication.timeSinceStartup;
            _contextOutlineObjects.Clear();
            _contextOutlineObjects.Add(context);
            _contextOutline.setGatherObjects(_contextOutlineObjects);
#if UNITY_2022_1_OR_NEWER
            Renderer[] renderers = context.GetComponentsInChildren<Renderer>(false);
            List<Renderer> visibleRenderers = new List<Renderer>(renderers.Length);
            foreach (Renderer renderer in renderers)
                if (renderer && renderer.enabled) visibleRenderers.Add(renderer);
            _contextRenderers = visibleRenderers.ToArray();
#endif
            EditorApplication.update += updateContextHighlight;
            SceneView.RepaintAll();
        }

        private void clearContextHighlight()
        {
            EditorApplication.update -= updateContextHighlight;
            _highlightedContext = null;
            _contextOutlineObjects.Clear();
            _contextOutline.setGatherObjects(_contextOutlineObjects);
#if UNITY_2022_1_OR_NEWER
            _contextRenderers = new Renderer[0];
#endif
        }

        private void updateContextHighlight()
        {
            if (!_highlightedContext ||
                EditorApplication.timeSinceStartup - _contextHighlightStartTime >= ContextHighlightDuration)
            {
                clearContextHighlight();
            }
            SceneView.RepaintAll();
        }

        private void drawContextHighlight(SceneView sceneView)
        {
            if (!_highlightedContext || Event.current.type != EventType.Repaint) return;
            double elapsed = EditorApplication.timeSinceStartup - _contextHighlightStartTime;
            if (elapsed >= ContextHighlightDuration) return;
            int phase = Mathf.FloorToInt((float)(elapsed * ContextHighlightFrequency * 2.0));
            if ((phase & 1) != 0) return;
            Color highlightColor = ObjectSpawnPrefs.instance.spawnActionContextHighlightColor;
#if UNITY_2022_1_OR_NEWER
            if (_contextRenderers.Length != 0)
                Handles.DrawOutline(_contextRenderers, highlightColor, 0.0f);
#else
            _contextOutline.drawHandles(highlightColor, highlightColor, 1.0f);
#endif
        }

        private void onSceneGUI(SceneView sceneView)
        {
            if (!_pickingContext) return;
            Event e = Event.current;
            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            if ((e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) ||
                (e.type == EventType.MouseDown && e.button == 1))
            {
                cancelContextPick();
                e.Use();
                return;
            }
            if (e.type != EventType.MouseDown || e.button != 0 || e.alt) return;
            GameObject context = HandleUtility.PickGameObject(e.mousePosition, true);
            e.Use();
            if (!context)
            {
                _status = "Pick a valid scene context, or press Escape.";
                Repaint();
                return;
            }
            SpawnAction recorded;
            if (!SpawnActionService.tryRecord(context, _activeAction.name, _recordingOutputs,
                _activeAction, out recorded, out _status))
            {
                Repaint();
                return;
            }
            cancelContextPick();
            showContextHighlight(context);
            _activeAction = recorded;
            _selectedOutputs.Clear();
            _selectedVariations.Clear();
            _status = "Recorded " + recorded.outputs.Count + " output(s).";
            Repaint();
        }
    }
}
#endif
