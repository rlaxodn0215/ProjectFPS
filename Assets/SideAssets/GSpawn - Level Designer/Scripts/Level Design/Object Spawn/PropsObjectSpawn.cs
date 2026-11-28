#if UNITY_EDITOR
using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEditor;

namespace GSPAWN
{
    public class PropsObjectSpawn : ObjectSpawnTool
    {
        [NonSerialized]
        private ObjectSpawnGuideSettings    _spawnGuideSettings;
        [NonSerialized]
        private ObjectSurfaceSnapSettings   _surfaceSnapSettings;
        [NonSerialized]
        private ObjectDragSpawnSettings     _dragSpawnSettings;
        [NonSerialized]
        private TerrainFlattenSettings      _terrainFlattenSettings;
        [SerializeField]
        private ObjectSurfaceSnapSession    _surfaceSnapSession;
        [SerializeField]
        private ObjectMirrorGizmo           _mirrorGizmo;
        [NonSerialized]
        private ObjectMirrorGizmoSettings   _mirrorGizmoSettings;
        [NonSerialized]
        private Vector3                     _lastSurfacePickPoint;
        [NonSerialized]
        private bool                        _dragStrokeActive;
        [NonSerialized]
        private int                         _dragStrokeUndoGroup        = -1;
        [NonSerialized]
        private HashSet<TerrainData>        _dragStrokeTerrainData      = new HashSet<TerrainData>();
        [NonSerialized]
        private List<OBB>                   _dragStrokeOBBs             = new List<OBB>();
        [NonSerialized]
        private List<OBB>                   _mirroredDragOBBBuffer      = new List<OBB>();
        [NonSerialized]
        private MirroredObjectList          _mirroredSpawnObjects       = new MirroredObjectList();
        [NonSerialized]
        private SceneRaycastFilter          _pickPrefabRaycastFilter    = new SceneRaycastFilter();
        [NonSerialized]
        private List<OBB>                   _obbBuffer                  = new List<OBB>();
        [NonSerialized]
        private List<Vector3>               _vector3Buffer              = new List<Vector3>();
        [NonSerialized]
        private Vector3[]                   _terrainPatchCorners        = new Vector3[4];
        [NonSerialized]
        private bool                        _randomGuideInitialized;
        [NonSerialized]
        private bool                        _randomPoolUnavailable;
        [NonSerialized]
        private RandomPrefabProfile         _previousRandomPrefabProfile;

        [SerializeField]
        private int                         _decorRuleIndex             = 0;
        [NonSerialized]
        private DecorRuleApplyResult        _prevDecorRuleApplyResult   = new DecorRuleApplyResult();
        [NonSerialized]
        private DecorRuleApplyResult        _decorRuleApplyResult       = new DecorRuleApplyResult();
        [NonSerialized]
        private GameObject                  _surfaceObjectPrefab        = null;
        [NonSerialized]
        private GameObject                  _prevSurfaceObjectPrefab    = null;
        [NonSerialized]
        private GameObject                  _prevDecoratedPrefab        = null;

        private ObjectSurfaceSnapSession    surfaceSnapSession
        {
            get
            {
                if (_surfaceSnapSession == null)
                {
                    _surfaceSnapSession = CreateInstance<ObjectSurfaceSnapSession>();
                    _surfaceSnapSession.sharedSettings = surfaceSnapSettings;
                }
                return _surfaceSnapSession;
            }
        }

        public ObjectSurfaceSnapSettings    surfaceSnapSettings
        {
            get
            {
                if (_surfaceSnapSettings == null) _surfaceSnapSettings = AssetDbEx.loadScriptableObject<ObjectSurfaceSnapSettings>(PluginFolders.settings, typeof(PropsObjectSpawn).Name + "_" + typeof(ObjectSurfaceSnapSettings).Name);
                return _surfaceSnapSettings;
            }
        }
        public ObjectDragSpawnSettings      dragSpawnSettings
        {
            get
            {
                if (_dragSpawnSettings == null) _dragSpawnSettings = AssetDbEx.loadScriptableObject<ObjectDragSpawnSettings>(PluginFolders.settings, typeof(PropsObjectSpawn).Name + "_" + typeof(ObjectDragSpawnSettings).Name);
                return _dragSpawnSettings;
            }
        }
        public TerrainFlattenSettings       terrainFlattenSettings
        {
            get
            {
                if (_terrainFlattenSettings == null) _terrainFlattenSettings = AssetDbEx.loadScriptableObject<TerrainFlattenSettings>(PluginFolders.settings, typeof(PropsObjectSpawn).Name + "_" + typeof(TerrainFlattenSettings).Name);
                return _terrainFlattenSettings;
            }
        }
        public override ObjectSpawnGuideSettings spawnGuideSettings
        {
            get
            {
                if (_spawnGuideSettings == null) _spawnGuideSettings = AssetDbEx.loadScriptableObject<ObjectSpawnGuideSettings>(PluginFolders.settings, typeof(PropsObjectSpawn).Name + "_" + typeof(ObjectSpawnGuideSettings).Name);
                return _spawnGuideSettings;
            }
        }
        public ObjectMirrorGizmoSettings    mirrorGizmoSettings
        {
            get
            {
                if (_mirrorGizmoSettings == null) _mirrorGizmoSettings = AssetDbEx.loadScriptableObject<ObjectMirrorGizmoSettings>(PluginFolders.settings, typeof(PropsObjectSpawn).Name + "_" + typeof(ObjectMirrorGizmoSettings).Name);
                return _mirrorGizmoSettings;
            }
        }
        public override ObjectSpawnToolId   spawnToolId             { get { return ObjectSpawnToolId.Props; } }
        public override bool                requiresSpawnGuide      { get { return true; } }
        public override ObjectMirrorGizmo   mirrorGizmo             { get { return _mirrorGizmo; } }

        public PropsObjectSpawn()
        {
        }

        public override void setSpawnGuidePrefab(PluginPrefab prefab)
        {
            if (_dragStrokeActive) endDragStroke();

            if (spawnGuideSettings.randomizePrefab)
            {
                // A Prefab Manager double-click only starts/restarts placement in random mode.
                // The clicked prefab must not replace the random profile's choice.
                selectRandomSpawnGuidePrefab(false);
            }
            else spawnGuide.usePrefab(prefab, surfaceSnapSession);
        }

        private bool isSpawnGuidePrefabInRandomProfile(RandomPrefabProfile profile)
        {
            if (profile == null || spawnGuide.sourcePrefab == null || spawnGuide.sourcePrefab.prefabAsset == null)
                return false;

            for (int i = 0; i < profile.numPrefabs; ++i)
            {
                RandomPrefab candidate = profile.getPrefab(i);
                if (candidate != null && candidate.used && candidate.probability > 0.0f &&
                    candidate.pluginPrefab == spawnGuide.sourcePrefab && candidate.prefabAsset != null)
                    return true;
            }

            return false;
        }

        private void selectRandomSpawnGuidePrefab(bool restartActiveSession, bool forceGuideRebuild = true)
        {
            RandomPrefabProfile profile = spawnGuideSettings.randomPrefabProfile;
            RandomPrefab randomPrefab = profile != null ? profile.pickPrefab() : null;
            PluginPrefab prefab = randomPrefab != null && randomPrefab.used && randomPrefab.probability > 0.0f &&
                                  randomPrefab.pluginPrefab != null && randomPrefab.prefabAsset != null
                                  ? randomPrefab.pluginPrefab : null;

            _previousRandomPrefabProfile = profile;
            _randomGuideInitialized = true;
            _randomPoolUnavailable = prefab == null;

            // An empty/unusable random pool must never silently fall back to a Manager prefab.
            // Keep the existing guide when the next random pick happens to be the same prefab.
            if (forceGuideRebuild || spawnGuide.sourcePrefab != prefab ||
                (prefab == null && spawnGuide.gameObject != null))
                spawnGuide.usePrefab(prefab, surfaceSnapSession, restartActiveSession);
        }

        private void syncRandomSpawnGuidePrefab()
        {
            if (!spawnGuideSettings.randomizePrefab)
            {
                _randomGuideInitialized = false;
                _randomPoolUnavailable = false;
                _previousRandomPrefabProfile = null;
                return;
            }

            RandomPrefabProfile profile = spawnGuideSettings.randomPrefabProfile;
            if (!_randomGuideInitialized || _previousRandomPrefabProfile != profile || _randomPoolUnavailable ||
                (spawnGuide.isPresentInScene && !isSpawnGuidePrefabInRandomProfile(profile)))
            {
                if (_dragStrokeActive) endDragStroke();
                selectRandomSpawnGuidePrefab(true);
            }
        }

        public override void onNoLongerActive()
        {
            endDragStroke();
            spawnGuide.destroyGuide();
            _randomGuideInitialized = false;
            _randomPoolUnavailable = false;
            _previousRandomPrefabProfile = null;
            enableSpawnGuidePrefabScroll = false;
        }

        public void executeSurfaceSnapSessionCommand(ObjectSurfaceSnapSessionCommand command)
        {
            surfaceSnapSession.executeCommand(command);
        }

        protected override void doOnSceneGUI()
        {
            // Resolve the initial guide from the random pool, even when Prefab Manager
            // has no selection. Also refresh it when the selected profile changes.
            syncRandomSpawnGuidePrefab();
            spawnGuide.onSceneGUI();
            _mirrorGizmo.onSceneGUI();

            Event e = Event.current;
            bool pickingPrefab = FixedShortcuts.enablePickSpawnGuidePrefabFromScene(e);
            if (pickingPrefab)
            {
                if (e.isLeftMouseButtonDownEvent())
                {
                    var prefabPickResult = PluginScene.instance.pickPrefab(PluginCamera.camera.getCursorRay(), _pickPrefabRaycastFilter, ObjectRaycastConfig.defaultConfig);
                    if (prefabPickResult != null)
                    {
                        setSpawnGuidePrefab(prefabPickResult.pickedPluginPrefab);
                        if (!spawnGuideSettings.randomizePrefab)
                            spawnGuide.setRotationAndScale(prefabPickResult.pickedObject.transform.rotation, prefabPickResult.pickedObject.transform.lossyScale);
                    }
                }
            }
            else
            {
                if (enableSpawnGuidePrefabScroll && e.isScrollWheel)
                {
                    PluginPrefab newPrefab = PluginPrefabManagerUI.instance.scrollVisiblePrefabSelection((int)e.getMouseScrollSign());
                    if (newPrefab != null)
                    {
                        setSpawnGuidePrefab(newPrefab);
                        e.disable();
                    }
                }
            }

            if (surfaceSnapSession.isActive)
            {
                if (spawnGuideSettings.applyDecorRules)
                {
                    if (FixedShortcuts.changeDecorRuleIndex(e))
                    {
                        if (e.getMouseScrollSign() > 0) ++_decorRuleIndex;
                        else --_decorRuleIndex;

                        e.disable();
                    }

                    if (surfaceSnapSession.surfaceObject != null)
                    {
                        _surfaceObjectPrefab = surfaceSnapSession.surfaceObject.getOutermostPrefabAsset();
                        GameObject decoratedPrefab = spawnGuide.sourcePrefab.prefabAsset;
                        if (_surfaceObjectPrefab != _prevSurfaceObjectPrefab || decoratedPrefab != _prevDecoratedPrefab)
                        {
                            var decorPrefab = PrefabDecorRuleDb.instance.getDecor(decoratedPrefab);
                            _decorRuleIndex = decorPrefab != null ? decorPrefab.getFavPropsSpawnRule(surfaceSnapSession.surfaceObject) : 0;
                            _prevSurfaceObjectPrefab = _surfaceObjectPrefab;
                            _prevDecoratedPrefab = decoratedPrefab;
                        }
                    }

                    PrefabDecorRuleDb.instance.applyDecorRule(spawnGuide.gameObject,
                                spawnGuide.sourcePrefab.prefabAsset, surfaceSnapSession.surfaceObject, _decorRuleIndex,
                                e.delta.magnitude != 0.0f, _decorRuleApplyResult);

                    _prevDecorRuleApplyResult.copy(_decorRuleApplyResult);
                    _decorRuleIndex = _decorRuleApplyResult.ruleIndex;
                }

                if (surfaceSnapSession.isSurfaceValid)
                {
                    if (e.isLeftMouseButtonDownEvent() && !e.alt && !pickingPrefab)
                    {
                        endDragStroke();
                        Vector3 surfacePoint = surfaceSnapSession.surfacePickPoint;
                        bool isTerrain = surfaceSnapSession.isSurfaceTerrain;
                        if (spawnDuringDragStroke(isTerrain))
                        {
                            _dragStrokeActive = true;
                            _lastSurfacePickPoint = surfacePoint;
                        }
                    }
                    else
                    if (_dragStrokeActive && e.isLeftMouseButtonDragEvent() && !e.alt && !pickingPrefab)
                    {
                        if ((_lastSurfacePickPoint - surfaceSnapSession.surfacePickPoint).magnitude >= dragSpawnSettings.minDragDistance)
                        {
                            OBB candidateOBB = spawnGuide.calcWorldOBB();
                            if (!dragSpawnSettings.useSafeDragDistance || !overlapsDragStroke(candidateOBB))
                            {
                                Vector3 surfacePoint = surfaceSnapSession.surfacePickPoint;
                                bool isTerrain = surfaceSnapSession.isSurfaceTerrain;
                                if (spawnDuringDragStroke(isTerrain))
                                    _lastSurfacePickPoint = surfacePoint;
                            }
                        }
                    }
                    else
                    if (FixedShortcuts.changeRadiusByScrollWheel(e))
                    {
                        if (terrainFlattenSettings.flattenTerrain)
                        {
                            e.disable();

                            int amount = (int)(e.getMouseScrollSign() * 0.5f);
                            if (amount == 0) amount = (int)(e.getMouseScrollSign());

                            terrainFlattenSettings.terrainQuadRadius -= amount;
                            EditorUtility.SetDirty(terrainFlattenSettings);
                        }
                    }

                    if (_mirrorGizmo.enabled)
                    {
                        _mirrorGizmo.mirrorOBB(spawnGuide.calcWorldOBB(), _obbBuffer);
                        _mirrorGizmo.drawMirroredOBBs(_obbBuffer);
                    }
                }
            }

            if (e.isLeftMouseButtonUpEvent() || e.type == EventType.MouseLeaveWindow ||
                (_dragStrokeActive && !surfaceSnapSession.isActive))
                endDragStroke();
        }

        protected override void draw()
        {
            if (surfaceSnapSession.isActive)
            {
                if (surfaceSnapSession.isSurfaceUnityTerrain && terrainFlattenSettings.flattenTerrain)
                {
                    Terrain terrain     = surfaceSnapSession.surfaceObject.getTerrain();
                    float terrainYPos   = terrain.transform.position.y;

                    spawnGuide.calcWorldOBB().calcCorners(_vector3Buffer, false);
                    AABB aabb           = new AABB(_vector3Buffer);
                    terrain.calcTerrainPatchCorners(terrainFlattenSettings.terrainQuadRadius, aabb, _terrainPatchCorners);

                    HandlesEx.saveColor();
                    Handles.color = ObjectSpawnPrefs.instance.propsSpawnTerrainFlattenAreaColor;

                    const int numSamplePoints   = 30;
                    float stepSize              = 1.0f / (float)numSamplePoints;
                    for (int lineIndex = 0; lineIndex < 4; ++lineIndex)
                    {
                        Vector3 p0      = _terrainPatchCorners[lineIndex];
                        Vector3 p1      = _terrainPatchCorners[(lineIndex + 1) % 4];
                        Vector3 dir     = (p1 - p0);
                        Vector3 prevPt  = terrain.projectPoint(terrainYPos, p0);
                        for (int i = 0; i < numSamplePoints; ++i)
                        {
                            Vector3 currentPt = terrain.projectPoint(terrainYPos, prevPt + dir * stepSize);
                            Handles.DrawLine(prevPt, currentPt);
                            prevPt = currentPt;
                        }
                    }

                    HandlesEx.restoreColor();
                }

                if (spawnGuideSettings.applyDecorRules)
                {
                    Handles.BeginGUI();
                    Vector3 labelPos            = HandlesEx.calcLabelPositionAboveOBB(spawnGuide.calcWorldOBB());
                    var labelStyle              = new GUIStyle(GUIStyleDb.instance.sceneViewInfoLabel);
                    labelStyle.normal.textColor = Color.green;
                    labelStyle.hover.textColor  = Color.green;
                    Handles.Label(labelPos, "Decor rules: " + _decorRuleApplyResult.rules.Count + "\nSelected rule: " + _decorRuleIndex, labelStyle);
                    Handles.EndGUI();
                }
            }
        }

        private bool spawnDuringDragStroke(bool lockTerrainSurface)
        {
            Vector3 surfaceNormal = surfaceSnapSession.surfacePickNormal;
            if (!surfaceSnapSettings.alignAxis) surfaceNormal = surfaceSnapSession.getNoAlignmentRotationAxis();

            if (_dragStrokeUndoGroup < 0 && UndoEx.enabled)
            {
                Undo.IncrementCurrentGroup();
                _dragStrokeUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Props Drag Spawn");
            }

            GameObject spawnedObject = spawn();
            if (spawnedObject == null)
            {
                if (!_dragStrokeActive) endDragStroke();
                return false;
            }

            surfaceSnapSession.addObjectHierarchyIgnoredAsSurface(spawnedObject);
            foreach (var mirroredObject in _mirroredSpawnObjects.objects)
                surfaceSnapSession.addObjectHierarchyIgnoredAsSurface(mirroredObject);

            if (lockTerrainSurface) surfaceSnapSession.setSurfaceLocked(true);

            if (dragSpawnSettings.useSafeDragDistance)
            {
                OBB spawnedOBB = ObjectBounds.calcHierarchyWorldOBB(spawnedObject, spawnGuide.worldOBBQConfig);
                if (spawnedOBB.isValid)
                {
                    _dragStrokeOBBs.Add(spawnedOBB);
                    if (_mirrorGizmo.enabled)
                    {
                        _mirrorGizmo.mirrorOBB(spawnedOBB, _mirroredDragOBBBuffer);
                        foreach (OBB mirroredOBB in _mirroredDragOBBBuffer)
                            if (mirroredOBB.isValid) _dragStrokeOBBs.Add(mirroredOBB);
                    }
                }
            }

            // Register the spawned instance as ignored before changing the guide.
            // Restarting the snap session earlier can pick the new instance as its surface.
            if (spawnGuideSettings.randomizePrefab)
                selectRandomSpawnGuidePrefab(true, false);

            spawnGuide.randomizeTransformIfNecessary(spawnGuideSettings.transformRandomizationSettings, surfaceNormal);
            return true;
        }

        private bool overlapsDragStroke(OBB candidateOBB)
        {
            if (!candidateOBB.isValid) return false;

            foreach (OBB spawnedOBB in _dragStrokeOBBs)
                if (spawnedOBB.intersectsOBB(candidateOBB)) return true;

            if (_mirrorGizmo.enabled)
            {
                _mirrorGizmo.mirrorOBB(candidateOBB, _mirroredDragOBBBuffer);
                foreach (OBB mirroredOBB in _mirroredDragOBBBuffer)
                {
                    foreach (OBB spawnedOBB in _dragStrokeOBBs)
                        if (spawnedOBB.intersectsOBB(mirroredOBB)) return true;
                }
            }

            return false;
        }

        private void endDragStroke()
        {
            _dragStrokeActive = false;
            _dragStrokeOBBs.Clear();
            _mirroredDragOBBBuffer.Clear();
            _mirroredSpawnObjects.objects.Clear();
            _dragStrokeTerrainData.Clear();

            if (_dragStrokeUndoGroup >= 0)
            {
                Undo.CollapseUndoOperations(_dragStrokeUndoGroup);
                _dragStrokeUndoGroup = -1;
            }

            if (_surfaceSnapSession != null)
            {
                _surfaceSnapSession.clearObjectsIgnoredAsSurface();
                _surfaceSnapSession.setSurfaceLocked(false);
            }
        }

        private GameObject spawn()
        {
            _mirroredSpawnObjects.objects.Clear();
            if (spawnGuide.sourcePrefab == null || spawnGuide.sourcePrefab.prefabAsset == null) return null;

            if (spawnGuideSettings.applyDecorRules)
            {
                var decorPrefab = PrefabDecorRuleDb.instance.getDecor(spawnGuide.sourcePrefab.prefabAsset);
                if (decorPrefab != null)
                    decorPrefab.setFavPropsSpawnRule(surfaceSnapSession.surfaceObject, _decorRuleIndex);
            }

            GameObject spawnedObject;
            if (surfaceSnapSession.isSurfaceUnityTerrain && terrainFlattenSettings.flattenTerrain)
            {
                Terrain terrain                     = surfaceSnapSession.surfaceObject.getTerrain();

                TerrainFlattenConfig flattenConfig  = new TerrainFlattenConfig();
                flattenConfig.terrainQuadRadius     = terrainFlattenSettings.terrainQuadRadius;
                flattenConfig.mode                  = terrainFlattenSettings.mode;
                flattenConfig.applyFalloff          = terrainFlattenSettings.applyFalloff;
                if (UndoEx.enabled && _dragStrokeTerrainData.Add(terrain.terrainData))
                    Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Props Drag Spawn");
                terrain.flattenAroundOBB(spawnGuide.calcWorldOBB(), flattenConfig);

                surfaceSnapSession.projectTargetsOnSurface();
                spawnedObject = spawnGuide.spawn();
            }
            else spawnedObject = spawnGuide.spawn();

            if (spawnedObject == null) return null;
            if (_mirrorGizmo.enabled) _mirrorGizmo.mirrorObject_NoDuplicateCommand(spawnedObject, spawnGuide.sourcePrefab.prefabAsset, _mirroredSpawnObjects);

            return spawnedObject;
        }

        protected override void onEnabled()
        {
            _pickPrefabRaycastFilter            = createDefaultPrefabPickRaycastFilter();
            surfaceSnapSession.sharedSettings   = surfaceSnapSettings;

            if (_mirrorGizmo == null)
            {
                _mirrorGizmo = ScriptableObject.CreateInstance<ObjectMirrorGizmo>();
                _mirrorGizmo.enabled = false;
            }

            _mirrorGizmo.sharedSettings = mirrorGizmoSettings;
        }

        protected override void onDisabled()
        {
            endDragStroke();
            _randomGuideInitialized = false;
            _randomPoolUnavailable = false;
            _previousRandomPrefabProfile = null;
        }

        protected override void onDestroy()
        {
            endDragStroke();
            ScriptableObjectEx.destroyImmediate(_surfaceSnapSession);
        }
    }
}
#endif