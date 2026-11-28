#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;

namespace GSPAWN
{
    public class XYZMeasureUI : VisualElement
    {
        FloatField  _xField;
        FloatField  _yField;
        FloatField  _zField;
        Button      _actionButton;

        public FloatField   xField          { get { return _xField; } }
        public FloatField   yField          { get { return _yField; } }
        public FloatField   zField          { get { return _zField; } }
        public Button       actionButton    { get { return _actionButton; } }

        public void init(VisualElement parent, Vector3 value, string actionName, Func<Vector3> measureAction)
        {
            style.flexDirection = FlexDirection.Row;
            parent.Add(this);
            {
                _actionButton = new Button();
                _actionButton.text = actionName;
                _actionButton.style.marginRight = -2.0f;
                Add(_actionButton);
                _actionButton.clicked += () =>
                {
                    Vector3 size = measureAction();
                    _xField.value = size.x;
                    _yField.value = size.y;
                    _zField.value = size.z;
                };

                _xField = new FloatField();
                _yField = new FloatField();
                _zField = new FloatField();

                _xField.style.width = 40.0f;
                _yField.style.width = 40.0f;
                _zField.style.width = 40.0f;

                _xField.setTextColor(DefaultSystemValues.xAxisColor);
                _yField.setTextColor(DefaultSystemValues.yAxisColor);
                _zField.setTextColor(DefaultSystemValues.zAxisColor);

                _xField.value = value.x;
                _yField.value = value.y;
                _zField.value = value.z;

                Add(_xField);
                Add(_yField);
                Add(_zField);
            }
        }
    }

    public class ObjectSelectionUI : PluginUI
    {
        private enum ViewId
        {
            Settings = 0,
            TransformTools,
            Misc
        }

        private ToolbarButton   _moveGimzoBtn;
        private ToolbarButton   _rotationGizmoBtn;
        private ToolbarButton   _scaleGizmoBtn;
        private ToolbarButton   _universalGizmoBtn;
        private ToolbarButton   _extrudeGizmoBtn;
        private ToolbarButton   _mirrorGizmoBtn;

        private ToolbarButton   _gizmoPivotBtn;
        private ToolbarButton   _gizmoTransformSpaceBtn;

        private ToolbarButton   _selectionRectBtn;
        private ToolbarButton   _selectionSegmentsBtn;
        private ToolbarButton   _selectionBoxBtn;

        private ToolbarButton   _settingsBtn;
        private ToolbarButton   _transformToolsBtn;
        private ToolbarButton   _miscBtn;

        private Button          _projectOnGridBtn;
        private Button          _projectOnObjectBtn;

        private XYZMeasureUI    _boundsMeasureUI;
        private XYZMeasureUI    _2BOverlapMeasureUI;
        [SerializeField]
        private Vector3         _boundsMeasureResult = Vector3.zero;
        [SerializeField]
        private Vector3         _2BOverlapMeasureResult = Vector3.zero;

        [SerializeField]
        private ViewId              _activeViewId       = ViewId.Settings;
        [SerializeField]
        private UISection           _transformSection;
        [SerializeField]
        private ObjectTransformUI   _transformUI;

        [SerializeField]
        private UISection   _selectionSettingsSection;
        [SerializeField]
        private UISection   _extrudeGizmoSettingsSection;
        [SerializeField]
        private UISection   _mirrorGizmoSettingsSection;
        [SerializeField]
        private UISection   _modularSnapSettingsSection;
        [SerializeField]
        private UISection   _surfaceSnapSettingsSection;
        [SerializeField]
        private UISection   _projectionSettingsSection;
        [SerializeField]
        private UISection   _vertexSnapSettingsSection;
        [SerializeField]
        private UISection   _boxSnapSettingsSection;
        [SerializeField]
        private UISection   _selectionGrowSettingsSection;
        [SerializeField]
        private UISection   _measureToolsSection;
        [SerializeField]
        private UISection   _bridgeSection;

        [SerializeField]
        private SelectionBridgePairAxis          _bridgePairAxis           = SelectionBridgePairAxis.Both;
        [SerializeField]
        private SelectionBridgePrefabAxis        _bridgePrefabAxis         = SelectionBridgePrefabAxis.Automatic;
        [SerializeField]
        private SelectionBridgePrefabUpAxis      _bridgePrefabUpAxis       = SelectionBridgePrefabUpAxis.Automatic;
        [SerializeField]
        private SelectionBridgeVerticalAlignment _bridgeVerticalAlignment = SelectionBridgeVerticalAlignment.Bottom;
        [SerializeField]
        private bool _bridgeReversePrefabAxis;
        [SerializeField]
        private bool _bridgeInvertPrefabUpAxis;
        [SerializeField]
        private float _bridgeAlignmentTolerance = 0.05f;

        [NonSerialized]
        private SelectionBridge     _bridge = new SelectionBridge();

        private UISection           transformSection                        { get { if (_transformSection == null) _transformSection = ScriptableObject.CreateInstance<UISection>(); return _transformSection; } }
        private ObjectTransformUI   transformUI                             { get { if (_transformUI == null) _transformUI = ScriptableObject.CreateInstance<ObjectTransformUI>(); return _transformUI; } }
        private UISection           selectionSettingsSection                { get { if (_selectionSettingsSection == null) _selectionSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _selectionSettingsSection; } }
        private UISection           extrudeGizmoSettingsSection             { get { if (_extrudeGizmoSettingsSection == null) _extrudeGizmoSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _extrudeGizmoSettingsSection; } }
        private UISection           mirrorGizmoSettingsSection              { get { if (_mirrorGizmoSettingsSection == null) _mirrorGizmoSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _mirrorGizmoSettingsSection; } }
        private UISection           modularSnapSettingsSection              { get { if (_modularSnapSettingsSection == null) _modularSnapSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _modularSnapSettingsSection; } }
        private UISection           surfaceSnapSettingsSection              { get { if (_surfaceSnapSettingsSection == null) _surfaceSnapSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _surfaceSnapSettingsSection; } }
        private UISection           projectionSettingsSection               { get { if (_projectionSettingsSection == null) _projectionSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _projectionSettingsSection; } }
        private UISection           vertexSnapSettingsSection               { get { if (_vertexSnapSettingsSection == null) _vertexSnapSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _vertexSnapSettingsSection; } }
        private UISection           boxSnapSettingsSection                  { get { if (_boxSnapSettingsSection == null) _boxSnapSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _boxSnapSettingsSection; } }
        private UISection           selectionGrowSettingsSection            { get { if (_selectionGrowSettingsSection == null) _selectionGrowSettingsSection = ScriptableObject.CreateInstance<UISection>(); return _selectionGrowSettingsSection; } }
        private UISection           measureToolsSection                     { get { if (_measureToolsSection == null) _measureToolsSection = ScriptableObject.CreateInstance<UISection>(); return _measureToolsSection; } }
        private UISection           bridgeSection                           { get { if (_bridgeSection == null) _bridgeSection = ScriptableObject.CreateInstance<UISection>(); return _bridgeSection; } }

        private static string       uiSectionRowSeparator_Transform         { get { return "A"; } }
        private static string       uiSectionRowSeparator_Settings          { get { return "B"; } }
        private static string       uiSectionRowSeparator_TransformTools    { get { return "C"; } }
        private static string       uiSectionRowSeparator_Misc              { get { return "D"; } }

        public ObjectTransformUI    objectTransformUI                       { get { return transformUI; } }

        public static ObjectSelectionUI instance                            { get { return GSpawn.active.objectSelectionUI; } }

        public void setObjectTransformUIEnabled(bool enabled)
        {
            if (ready)
            {
                transformSection.contentContainer.SetEnabled(enabled);
            }
        }

        public void refreshObjectTransformUI()
        {
            if (ready)
            {
                transformUI.refresh();
                transformSection.setTitle(getTransformUISectionTitle());
            }
        }

        protected override void onRefresh()
        {
            refreshGizmoSelectionButtons();
            refreshGizmoPivotButton();
            refreshGizmoTransformSpaceButton();
            refreshObjectSelectShapeButtons();
            refreshViewSelectionButtons();
            refreshTooltips();
            transformUI.refreshTooltips();
            updateVisibility();
        }

        protected override void onBuild()
        {
            var toolbarContainer                    = new VisualElement();
            toolbarContainer.style.flexDirection    = FlexDirection.Row;
            contentContainer.Add(toolbarContainer);

            Toolbar toolbar                         = UI.createStylizedToolbar(toolbarContainer);
            toolbar.style.height                    = UIValues.mediumToolbarButtonSize + 2.0f;
            toolbar.style.borderRightColor          = UIValues.toolbarBorderColor;
            toolbar.style.borderRightWidth          = 1.0f;
            toolbar.style.borderBottomWidth         = 1.0f;
            toolbar.style.borderBottomColor         = UIValues.toolbarBorderColor;

            _moveGimzoBtn                           = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.moveGizmo, "", toolbar);
            _moveGimzoBtn.clicked                   += () =>
            {
                ObjectSelectionGizmos.instance.setGizmoEnabled(ObjectSelectionGizmoId.Move, true, true, ObjectSelectionGizmoId.Mirror);
                SceneViewEx.focus();
            };

            _rotationGizmoBtn                       = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.rotationGizmo, "", toolbar);
            _rotationGizmoBtn.clicked               += () =>
            {
                ObjectSelectionGizmos.instance.setGizmoEnabled(ObjectSelectionGizmoId.Rotate, true, true, ObjectSelectionGizmoId.Mirror);
                SceneViewEx.focus();
            };

            _scaleGizmoBtn                          = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.scaleGizmo, "", toolbar);
            _scaleGizmoBtn.clicked                  += () =>
            {
                ObjectSelectionGizmos.instance.setGizmoEnabled(ObjectSelectionGizmoId.Scale, true, true, ObjectSelectionGizmoId.Mirror);
                SceneViewEx.focus();
            };

            _universalGizmoBtn                      = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.universalGizmo, "", toolbar);
            _universalGizmoBtn.clicked              += () =>
            {
                ObjectSelectionGizmos.instance.setGizmoEnabled(ObjectSelectionGizmoId.Universal, true, true, ObjectSelectionGizmoId.Mirror);
                SceneViewEx.focus();
            };

            _extrudeGizmoBtn                        = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.extrudeGizmo, "", toolbar);
            _extrudeGizmoBtn.clicked                += () =>
            {
                ObjectSelectionGizmos.instance.setGizmoEnabled(ObjectSelectionGizmoId.Extrude, true, true, ObjectSelectionGizmoId.Mirror);
                SceneViewEx.focus();
            };

            _mirrorGizmoBtn                         = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.mirrorGizmo, "", toolbar);
            _mirrorGizmoBtn.clicked                 += () =>
            {
                ObjectSelectionGizmos.instance.setGizmoEnabled(ObjectSelectionGizmoId.Mirror, !ObjectSelectionGizmos.instance.isGizmoEnabled(ObjectSelectionGizmoId.Mirror), false);
                SceneViewEx.focus();
            };

            refreshGizmoSelectionButtons();

            toolbar                                 = UI.createStylizedToolbar(toolbarContainer);
            toolbar.style.height                    = UIValues.mediumToolbarButtonSize + 2.0f;
            toolbar.style.flexGrow                  = 1.0f;
            toolbar.style.borderBottomWidth         = 1.0f;
            toolbar.style.borderBottomColor         = UIValues.toolbarBorderColor;

            _gizmoPivotBtn                          = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.gizmoCenterPivot, "", toolbar);
            _gizmoPivotBtn.clicked                  += () => 
            { 
                ObjectSelectionGizmos.instance.transformPivot = ObjectSelectionGizmos.instance.transformPivot == ObjectGizmoTransformPivot.Center ? ObjectGizmoTransformPivot.Mesh : ObjectGizmoTransformPivot.Center; 
                SceneViewEx.focus();
            };
            _gizmoTransformSpaceBtn                 = UI.createSmallToolSelectionToolbarButton(TexturePool.instance.gizmoLocalTransformSpace, "", toolbar);;
            _gizmoTransformSpaceBtn.clicked         += () => 
            { 
                ObjectSelectionGizmos.instance.transformSpace = ObjectSelectionGizmos.instance.transformSpace == ObjectGizmoTransformSpace.Global ? ObjectGizmoTransformSpace.Local : ObjectGizmoTransformSpace.Global; 
                SceneViewEx.focus();
            };

            toolbar                                 = UI.createToolSelectionToolbar(contentContainer);
            _selectionRectBtn                       = UI.createMediumToolSelectionToolbarButton(TexturePool.instance.selectionRect, "", toolbar);
            _selectionRectBtn.style.marginTop       = 1.0f;
            _selectionRectBtn.clicked               += () => 
            { 
                ObjectSelection.instance.selectionShapeType = ObjectSelectionShape.Type.Rect;
                SceneViewEx.focus();
            };

            _selectionSegmentsBtn                   = UI.createMediumToolSelectionToolbarButton(TexturePool.instance.selectionSegments, "", toolbar);
            _selectionSegmentsBtn.style.marginTop   = 1.0f;
            _selectionSegmentsBtn.clicked           += () => 
            { 
                ObjectSelection.instance.selectionShapeType = ObjectSelectionShape.Type.Segments;
                SceneViewEx.focus();
            };

            _selectionBoxBtn                        = UI.createMediumToolSelectionToolbarButton(TexturePool.instance.selectionBox, "", toolbar);
            _selectionBoxBtn.style.marginTop        = 1.0f;
            _selectionBoxBtn.clicked                += () => 
            { 
                ObjectSelection.instance.selectionShapeType = ObjectSelectionShape.Type.Box;
                SceneViewEx.focus();
            };

            refreshGizmoPivotButton();
            refreshGizmoTransformSpaceButton();
            refreshObjectSelectShapeButtons();

            toolbar                                 = UI.createToolSelectionToolbar(contentContainer);
            _settingsBtn                            = UI.createToolbarButton(TexturePool.instance.settings, UI.ButtonStyle.Push, UIValues.mediumToolbarButtonSize, toolbar);
            UI.useDefaultMargins(_settingsBtn);
            _settingsBtn.style.marginTop            = 1.0f;
            _settingsBtn.tooltip                    = "Settings";
            _settingsBtn.clicked                    += () =>
            {
                _activeViewId = ViewId.Settings;
                updateVisibility();
                refreshViewSelectionButtons();
                SceneViewEx.focus();
            };

            _transformToolsBtn                      = UI.createToolbarButton(TexturePool.instance.transform, UI.ButtonStyle.Push, UIValues.mediumToolbarButtonSize, toolbar);
            UI.useDefaultMargins(_transformToolsBtn);
            _transformToolsBtn.style.marginTop      = 1.0f;
            _transformToolsBtn.tooltip              = "Transform tools";
            _transformToolsBtn.clicked              += () =>
            {
                _activeViewId = ViewId.TransformTools;
                updateVisibility();
                refreshViewSelectionButtons();
                SceneViewEx.focus();
            };

            _miscBtn                                = UI.createToolbarButton(TexturePool.instance.misc, UI.ButtonStyle.Push, UIValues.mediumToolbarButtonSize, toolbar);
            UI.useDefaultMargins(_miscBtn);
            _miscBtn.tooltip                        = "Misc";
            _miscBtn.clicked                        += () =>
            {
                _activeViewId = ViewId.Misc;
                updateVisibility();
                refreshViewSelectionButtons();
                SceneViewEx.focus();
            };
       
            transformSection.build(getTransformUISectionTitle(), TexturePool.instance.transform, true, contentContainer);
            transformUI.build(ObjectSelection.instance.objectCollection, transformSection.contentContainer);
            transformUI.visibilityCondition = () => { return uiVisibleAndReady; };
      
            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_Settings);
            selectionSettingsSection.build("Selection", TexturePool.instance.settings, true, contentContainer);
            ObjectSelection.instance.settings.buildUI(selectionSettingsSection.contentContainer);            

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_Settings);
            extrudeGizmoSettingsSection.build("Extrude Gizmo", TexturePool.instance.extrudeGizmo, true, contentContainer);
            ObjectSelectionGizmos.instance.extrudeGizmoSettings.buildUI(extrudeGizmoSettingsSection.contentContainer);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_Settings);
            mirrorGizmoSettingsSection.build("Mirror Gizmo", TexturePool.instance.mirrorGizmo, true, contentContainer);
            UI.createPositionField(PluginGizmo.positionPropertyName, ObjectSelectionGizmos.instance.mirrorGizmoSerializedObject, "Mirror gizmo position", mirrorGizmoSettingsSection.contentContainer);
            UI.createRotationField(PluginGizmo.rotationPropertyName, ObjectSelectionGizmos.instance.mirrorGizmoSerializedObject, "Mirror gizmo rotation", mirrorGizmoSettingsSection.contentContainer);
            ObjectSelectionGizmos.instance.mirrorGizmoSettings.buildUI(mirrorGizmoSettingsSection.contentContainer);
            UI.createUISectionRowSeparator(mirrorGizmoSettingsSection.contentContainer);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_TransformTools);
            modularSnapSettingsSection.build("Modular Snap", TexturePool.instance.modularSnapSpawn, true, contentContainer);
            ObjectSelection.instance.modularSnapSettings.buildUI(modularSnapSettingsSection.contentContainer);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_TransformTools);
            surfaceSnapSettingsSection.build("Surface Snap", TexturePool.instance.objectSurfaceSnap, true, contentContainer);
            ObjectSelection.instance.surfaceSnapSettings.buildUI(surfaceSnapSettingsSection.contentContainer);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_TransformTools);
            projectionSettingsSection.build("Projection", TexturePool.instance.projection, true, contentContainer);
            ObjectSelection.instance.projectionSettings.buildUI(projectionSettingsSection.contentContainer);

            VisualElement buttonsContainer          = new VisualElement();
            buttonsContainer.style.flexDirection    = FlexDirection.Row;
            projectionSettingsSection.contentContainer.Add(buttonsContainer);

            _projectOnGridBtn                       = new Button();
            _projectOnGridBtn.text                  = "Project on grid";
            _projectOnGridBtn.style.width           = UIValues.useDefaultsButtonWidth;
            _projectOnGridBtn.clicked               += () => { ObjectSelection.instance.projectOnGrid(PluginScene.instance.grid); };
            buttonsContainer.Add(_projectOnGridBtn);

            _projectOnObjectBtn                     = new Button();
            _projectOnObjectBtn.text                = "Project on object";            
            _projectOnObjectBtn.style.width         = 110.0f;
            _projectOnObjectBtn.style.marginLeft    = UIValues.actionButtonLeftMargin;
            _projectOnObjectBtn.clicked             += () => { ObjectSelection.instance.beginTransformSession(ObjectTransformSessionType.Projection); };
            buttonsContainer.Add(_projectOnObjectBtn);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_TransformTools);
            vertexSnapSettingsSection.build("Vertex Snap", TexturePool.instance.vertexSnap, true, contentContainer);
            ObjectSelection.instance.vertexSnapSettings.buildUI(vertexSnapSettingsSection.contentContainer);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_TransformTools);
            boxSnapSettingsSection.build("Box Snap", TexturePool.instance.boxSnap, true, contentContainer);
            ObjectSelection.instance.boxSnapSettings.buildUI(boxSnapSettingsSection.contentContainer);

            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_Misc);
            selectionGrowSettingsSection.build("Selection Grow", TexturePool.instance.selectionGrow, true, contentContainer);
            ObjectSelection.instance.growSettings.buildUI(selectionGrowSettingsSection.contentContainer);

            createBridgeSection();
            createMeasureSection();

            var growContentContainer = selectionGrowSettingsSection.contentContainer;
            var growDefaultsButton = growContentContainer.ElementAt(growContentContainer.childCount - 1);
            var growButtonsContainer = new VisualElement();
            growButtonsContainer.style.flexDirection = FlexDirection.Row;
            growContentContainer.Add(growButtonsContainer);
            growButtonsContainer.Add(growDefaultsButton);

            var growBtn                 = new Button();
            growButtonsContainer.Add(growBtn);
            growBtn.text                = "Grow";
            growBtn.style.width         = UIValues.useDefaultsButtonWidth;
            growBtn.style.marginLeft    = UIValues.actionButtonLeftMargin;
            growBtn.tooltip             = "Grow selection" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.grow);
            growBtn.clicked             += () => 
            {
                ObjectSelection.instance.grow();
            };

            updateVisibility();
        }

        private void createBridgeSection()
        {
            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_Misc);
            bridgeSection.build("Bridge", TexturePool.instance.bridge, true, contentContainer);

            VisualElement parent = bridgeSection.contentContainer;

            UI.createEnumField(typeof(SelectionBridgePairAxis), nameof(_bridgePairAxis), serializedObject,
                "Pair axis", "Bridge along one or both axes of the active GSpawn grid.", parent);
            UI.createEnumField(typeof(SelectionBridgePrefabAxis), nameof(_bridgePrefabAxis), serializedObject,
                "Prefab axis", "Local axis repeated along each gap. Automatic chooses the longest axis perpendicular to Prefab Up.", parent);
            UI.createEnumField(typeof(SelectionBridgePrefabUpAxis), nameof(_bridgePrefabUpAxis), serializedObject,
                "Prefab up", "Local axis aligned to grid up. Automatic prefers local Y.", parent);
            // Normalize previously serialized Endpoint (value 3) to Bottom.
            // Keep the other enum values stable for existing GSpawn settings.
            serializedObject.Update();
            var verticalAlignmentProperty = serializedObject.FindProperty(nameof(_bridgeVerticalAlignment));
            if (verticalAlignmentProperty.intValue < (int)SelectionBridgeVerticalAlignment.Bottom ||
                verticalAlignmentProperty.intValue > (int)SelectionBridgeVerticalAlignment.Top)
            {
                verticalAlignmentProperty.intValue = (int)SelectionBridgeVerticalAlignment.Bottom;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            UI.createEnumField(typeof(SelectionBridgeVerticalAlignment), nameof(_bridgeVerticalAlignment), serializedObject,
                "Vertical alignment", "Align the bottom, center, or top of the bridge prefabs to the connecting geometry.", parent);
            UI.createToggle(nameof(_bridgeReversePrefabAxis), serializedObject,
                "Reverse prefab axis", "Reverse the prefab's local repetition direction.", parent);
            UI.createToggle(nameof(_bridgeInvertPrefabUpAxis), serializedObject,
                "Invert prefab up", "Flip the prefab's local up direction.", parent);
            UI.createFloatField(nameof(_bridgeAlignmentTolerance), serializedObject,
                "Alignment tolerance", "Maximum sideways or vertical offset allowed between endpoints (world units). Increase for imperfect modular geometry; decrease to avoid incorrect matches.", 0.001f, parent);

            var bridgeButtonsContainer = new VisualElement();
            bridgeButtonsContainer.style.flexDirection = FlexDirection.Row;
            parent.Add(bridgeButtonsContainer);
            UI.createUseDefaultsButton(useBridgeDefaults, bridgeButtonsContainer);

            var bridgeButton = new Button();
            bridgeButton.text = "Bridge Selected";
            bridgeButton.tooltip = "Fill gaps between GSpawn-selected scene anchors using the prefabs selected in the Prefab Manager.";
            bridgeButton.style.width = 125.0f;
            bridgeButton.style.marginLeft = UIValues.actionButtonLeftMargin;
            bridgeButton.clicked += executeBridge;
            bridgeButtonsContainer.Add(bridgeButton);
        }

        private void useBridgeDefaults()
        {
            serializedObject.Update();
            serializedObject.FindProperty(nameof(_bridgePairAxis)).enumValueIndex = (int)SelectionBridgePairAxis.Both;
            serializedObject.FindProperty(nameof(_bridgePrefabAxis)).enumValueIndex = (int)SelectionBridgePrefabAxis.Automatic;
            serializedObject.FindProperty(nameof(_bridgePrefabUpAxis)).enumValueIndex = (int)SelectionBridgePrefabUpAxis.Automatic;
            serializedObject.FindProperty(nameof(_bridgeVerticalAlignment)).enumValueIndex = (int)SelectionBridgeVerticalAlignment.Bottom;
            serializedObject.FindProperty(nameof(_bridgeReversePrefabAxis)).boolValue = false;
            serializedObject.FindProperty(nameof(_bridgeInvertPrefabUpAxis)).boolValue = false;
            serializedObject.FindProperty(nameof(_bridgeAlignmentTolerance)).floatValue = 0.05f;
            serializedObject.ApplyModifiedProperties();
        }

        private void executeBridge()
        {
            var anchors = new System.Collections.Generic.List<GameObject>();
            ObjectSelection.instance.getSelectedObjects(anchors);
            if (anchors.Count < 2)
            {
                EditorUtility.DisplayDialog("Bridge", "Select at least two scene objects with GSpawn's Object Selection tool.", "OK");
                return;
            }

            // Use the actual visible Prefab Manager selection. Scanning uiSelected
            // across the entire profile also picks stale selections from other libraries.
            var prefabs = new System.Collections.Generic.List<PluginPrefab>();
            PluginPrefabManagerUI.instance.getVisibleSelectedPrefabs(prefabs);
            prefabs.RemoveAll(prefab => prefab == null || prefab.prefabAsset == null);
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("Bridge", "Select at least one prefab in GSpawn's Prefab Manager first.", "OK");
                return;
            }

            if (_bridge == null) _bridge = new SelectionBridge();
            int count = _bridge.Execute(anchors, prefabs, _bridgePairAxis, _bridgePrefabAxis,
                _bridgePrefabUpAxis, _bridgeVerticalAlignment, _bridgeReversePrefabAxis,
                _bridgeInvertPrefabUpAxis, _bridgeAlignmentTolerance);
            if (count == 0)
                EditorUtility.DisplayDialog("Bridge", "No matching endpoint gaps were found. Check the selected anchors, prefab dimensions, grid axes, and alignment tolerance.", "OK");

            SceneViewEx.focus();
        }

        private void createMeasureSection()
        {
            UI.createUISectionRowSeparator(contentContainer, uiSectionRowSeparator_Misc);
            measureToolsSection.build("Measure Tools", TexturePool.instance.ruler, true, contentContainer);

            const float btnWidth = 90.0f;

            _boundsMeasureUI = new XYZMeasureUI();
            _boundsMeasureUI.init(measureToolsSection.contentContainer, _boundsMeasureResult, "Bounds", () => 
            { return ObjectSelection.instance.calcAABB().size; });
            _boundsMeasureUI.actionButton.style.width = btnWidth;
            _boundsMeasureUI.actionButton.tooltip = "Measure the object selection bounds.";

            _boundsMeasureUI.xField.RegisterValueChangedCallback((e) => { _boundsMeasureResult.x = e.newValue; EditorUtility.SetDirty(this); });
            _boundsMeasureUI.yField.RegisterValueChangedCallback((e) => { _boundsMeasureResult.y = e.newValue; EditorUtility.SetDirty(this); });
            _boundsMeasureUI.zField.RegisterValueChangedCallback((e) => { _boundsMeasureResult.z = e.newValue; EditorUtility.SetDirty(this); });

            _2BOverlapMeasureUI = new XYZMeasureUI();
            _2BOverlapMeasureUI.init(measureToolsSection.contentContainer, _2BOverlapMeasureResult, "2B. Overlap", () => 
            { return ObjectSelection.instance.calc2BOverlapSize(); });
            _2BOverlapMeasureUI.actionButton.style.width = btnWidth;
            _2BOverlapMeasureUI.actionButton.tooltip = "Measure the bounds overlap of 2 selected objects. Only 2 objects have to be selected for this to work.";

            _2BOverlapMeasureUI.xField.RegisterValueChangedCallback((e) => { _2BOverlapMeasureResult.x = e.newValue; EditorUtility.SetDirty(this); });
            _2BOverlapMeasureUI.yField.RegisterValueChangedCallback((e) => { _2BOverlapMeasureResult.y = e.newValue; EditorUtility.SetDirty(this); });
            _2BOverlapMeasureUI.zField.RegisterValueChangedCallback((e) => { _2BOverlapMeasureResult.z = e.newValue; EditorUtility.SetDirty(this); });
        }

        private void refreshTooltips()
        {
            _projectOnGridBtn.tooltip       = ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.projectOnGrid, "Project selected objects on the scene grid.");
            _projectOnObjectBtn.tooltip     = ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.projectOnObject, "Project selected objects on an object that you pick in the scene.");
        }

        private string getTransformUISectionTitle()
        {
            if (ObjectSelection.instance.numSelectedObjects == 1)
                return "Transform (" + ObjectSelection.instance.numSelectedObjects + ") - " + ObjectSelection.instance.getSelectedObject(0).name;
            else 
                return ObjectSelection.instance.numSelectedObjects != 0 ? "Transform (" + ObjectSelection.instance.numSelectedObjects + ")" : "Transform";
        }

        private void refreshGizmoSelectionButtons()
        {
            _moveGimzoBtn.tooltip = "Move" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.moveGizmo);
            _moveGimzoBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _rotationGizmoBtn.tooltip = "Rotate" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.rotationGizmo);
            _rotationGizmoBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _scaleGizmoBtn.tooltip = "Scale" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.scaleGizmo);
            _scaleGizmoBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _universalGizmoBtn.tooltip = "Move/Rotate/Scale" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.universalGizmo);
            _universalGizmoBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _extrudeGizmoBtn.tooltip = "Extrude" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.extrudeGizmo);
            _extrudeGizmoBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _mirrorGizmoBtn.tooltip = "Mirror" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(GlobalShortcutNames.mirrorGizmo_Toggle);
            _mirrorGizmoBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            var selectionGizmos = ObjectSelectionGizmos.instance;
            if (selectionGizmos.isGizmoEnabled(ObjectSelectionGizmoId.Move)) _moveGimzoBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            if (selectionGizmos.isGizmoEnabled(ObjectSelectionGizmoId.Rotate)) _rotationGizmoBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            if (selectionGizmos.isGizmoEnabled(ObjectSelectionGizmoId.Scale)) _scaleGizmoBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            if (selectionGizmos.isGizmoEnabled(ObjectSelectionGizmoId.Universal)) _universalGizmoBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            if (selectionGizmos.isGizmoEnabled(ObjectSelectionGizmoId.Extrude)) _extrudeGizmoBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            if (selectionGizmos.isGizmoEnabled(ObjectSelectionGizmoId.Mirror)) _mirrorGizmoBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
        }

        private void refreshGizmoPivotButton()
        {
            if (ObjectSelectionGizmos.instance.transformPivot == ObjectGizmoTransformPivot.Center)
            {
                _gizmoPivotBtn.tooltip = "Center Pivot\r\n\r\nThe gizmo's position in placed in the center of the object selection.";
                _gizmoPivotBtn.style.backgroundImage = TexturePool.instance.gizmoCenterPivot;
            }
            else
            {
                _gizmoPivotBtn.tooltip = "Mesh Pivot\r\n\r\nThe gizmo's position is defined by the object's mesh pivot.";
                _gizmoPivotBtn.style.backgroundImage = TexturePool.instance.gizmoMeshPivot;
            }
        }

        private void refreshGizmoTransformSpaceButton()
        {
            if (ObjectSelectionGizmos.instance.transformSpace == ObjectGizmoTransformSpace.Global)
            {
                _gizmoTransformSpaceBtn.tooltip = "Global\r\n\r\nThe gizmo axes are aligned to the global coordinate system.";
                _gizmoTransformSpaceBtn.style.backgroundImage = TexturePool.instance.gizmoGlobalTransformSpace;
            }
            else
            {
                _gizmoTransformSpaceBtn.tooltip = "Local\r\n\r\nThe gizmo inherits the rotation of the object.";
                _gizmoTransformSpaceBtn.style.backgroundImage = TexturePool.instance.gizmoLocalTransformSpace;
            }
        }

        private void refreshObjectSelectShapeButtons()
        {
            _selectionRectBtn.tooltip = "Selection Rectangle" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.selectionRect, "Multi-object selection is performed using a selection rectangle.");
            _selectionRectBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _selectionSegmentsBtn.tooltip = "Selection Segments" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.selectionSegments, "Multi-object selection is performed using a chain of segments.");
            _selectionSegmentsBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            _selectionBoxBtn.tooltip = "Selection Box" + ShortcutProfileDb.instance.activeProfile.getShortcutUITooltip(ObjectSelectionShortcutNames.selectionBox, "Multi-object selection is performed using a selection box.");
            _selectionBoxBtn.style.unityBackgroundImageTintColor = UIValues.inactiveButtonTintColor;

            if (ObjectSelection.instance.selectionShapeType == ObjectSelectionShape.Type.Rect) _selectionRectBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            else if (ObjectSelection.instance.selectionShapeType == ObjectSelectionShape.Type.Segments) _selectionSegmentsBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
            else if (ObjectSelection.instance.selectionShapeType == ObjectSelectionShape.Type.Box) _selectionBoxBtn.style.unityBackgroundImageTintColor = UIValues.activeButtonColor;
        }

        private void refreshViewSelectionButtons()
        {
            if (_activeViewId == ViewId.Settings)
            {
                _settingsBtn.style.backgroundColor          = UIValues.activeButtonColor;
                _transformToolsBtn.style.backgroundColor    = UIValues.inactiveButtonColor;
                _miscBtn.style.backgroundColor              = UIValues.inactiveButtonColor;
            }
            else
            if (_activeViewId == ViewId.TransformTools)
            {
                _settingsBtn.style.backgroundColor          = UIValues.inactiveButtonColor;
                _transformToolsBtn.style.backgroundColor    = UIValues.activeButtonColor;
                _miscBtn.style.backgroundColor              = UIValues.inactiveButtonColor;
            }
            else
            if (_activeViewId == ViewId.Misc)
            {
                _settingsBtn.style.backgroundColor          = UIValues.inactiveButtonColor;
                _transformToolsBtn.style.backgroundColor    = UIValues.inactiveButtonColor;
                _miscBtn.style.backgroundColor              = UIValues.activeButtonColor;
            }
        }

        private void updateVisibility()
        {
            bool visible = _activeViewId == ViewId.Settings;
            contentContainer.setChildrenDisplayVisible(uiSectionRowSeparator_Settings, visible);
            selectionSettingsSection.setVisible(visible);
            extrudeGizmoSettingsSection.setVisible(visible);
            mirrorGizmoSettingsSection.setVisible(visible);

            visible = _activeViewId == ViewId.TransformTools;
            contentContainer.setChildrenDisplayVisible(uiSectionRowSeparator_TransformTools, visible);
            modularSnapSettingsSection.setVisible(visible);
            surfaceSnapSettingsSection.setVisible(visible);
            projectionSettingsSection.setVisible(visible);
            vertexSnapSettingsSection.setVisible(visible);
            boxSnapSettingsSection.setVisible(visible);

            visible = _activeViewId == ViewId.Misc;
            contentContainer.setChildrenDisplayVisible(uiSectionRowSeparator_Misc, visible);
            selectionGrowSettingsSection.setVisible(visible);
            bridgeSection.setVisible(visible);
            measureToolsSection.setVisible(visible);
        }

        protected override void onDestroy()
        {
            ScriptableObjectEx.destroyImmediate(_transformSection);
            ScriptableObjectEx.destroyImmediate(_transformUI);
            ScriptableObjectEx.destroyImmediate(_selectionSettingsSection);
            ScriptableObjectEx.destroyImmediate(_extrudeGizmoSettingsSection);
            ScriptableObjectEx.destroyImmediate(_mirrorGizmoSettingsSection);
            ScriptableObjectEx.destroyImmediate(_modularSnapSettingsSection);
            ScriptableObjectEx.destroyImmediate(_surfaceSnapSettingsSection);
            ScriptableObjectEx.destroyImmediate(_projectionSettingsSection);
            ScriptableObjectEx.destroyImmediate(_vertexSnapSettingsSection);
            ScriptableObjectEx.destroyImmediate(_boxSnapSettingsSection);
            ScriptableObjectEx.destroyImmediate(_selectionGrowSettingsSection);
            ScriptableObjectEx.destroyImmediate(_measureToolsSection);
            ScriptableObjectEx.destroyImmediate(_bridgeSection);
        }
    }
}
#endif