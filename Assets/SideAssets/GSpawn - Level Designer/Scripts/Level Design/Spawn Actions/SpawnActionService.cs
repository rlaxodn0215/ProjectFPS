#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GSPAWN
{
    // Editor-only authoring/application layer. No coupling to PrefabDecorRuleDb.
    public static class SpawnActionService
    {
        private static readonly List<GameObject> _selection = new List<GameObject>();
        private static readonly List<GameObject> _spawned = new List<GameObject>();
        private static readonly List<GameObject> _toRemove = new List<GameObject>();
        private static readonly HashSet<GameObject> _seen = new HashSet<GameObject>();

        public static string folderPath { get { return PluginFolders.data + "/Spawn Actions"; } }

        private static string dbAssetPath { get { return folderPath + "/SpawnActionDb.asset"; } }

        private static SpawnActionDb loadDb(bool createIfMissing)
        {
            SpawnActionDb db = AssetDatabase.LoadAssetAtPath<SpawnActionDb>(dbAssetPath);
            if (db || !createIfMissing) return db;
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                if (!AssetDatabase.IsValidFolder(PluginFolders.data)) return null;
                AssetDatabase.CreateFolder(PluginFolders.data, "Spawn Actions");
            }
            db = ScriptableObject.CreateInstance<SpawnActionDb>();
            db.name = "SpawnActionDb";
            AssetDatabase.CreateAsset(db, dbAssetPath);
            AssetDatabase.SaveAssets();
            return db;
        }

        public static List<SpawnAction> getActions()
        {
            var result = new List<SpawnAction>();
            var seen = new HashSet<SpawnAction>();
            SpawnActionDb db = loadDb(false);
            if (db)
            {
                foreach (SpawnAction action in db.actions)
                {
                    if (action && AssetDatabase.Contains(action) && seen.Add(action))
                        result.Add(action);
                }
            }
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                string[] ids = AssetDatabase.FindAssets("t:SpawnAction", new[] { folderPath });
                foreach (string id in ids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(id);
                    if (path == dbAssetPath || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                    SpawnAction legacy = AssetDatabase.LoadMainAssetAtPath(path) as SpawnAction;
                    if (legacy && seen.Add(legacy)) result.Add(legacy);
                }
            }
            result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public static bool deleteActions(IReadOnlyList<SpawnAction> selected, out string error)
        {
            error = string.Empty;
            if (selected == null || selected.Count == 0) return false;

            SpawnActionDb db = loadDb(false);
            var dbActions = new List<SpawnAction>();
            var legacyPaths = new List<string>();
            var unique = new HashSet<SpawnAction>();
            foreach (SpawnAction action in selected)
            {
                if (!action || !unique.Add(action)) continue;
                string path = AssetDatabase.GetAssetPath(action);
                if (db && path == dbAssetPath && db.actions.Contains(action) && AssetDatabase.IsSubAsset(action))
                {
                    dbActions.Add(action);
                }
                else if (path.StartsWith(folderPath + "/", StringComparison.Ordinal) &&
                    path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) &&
                    AssetDatabase.LoadMainAssetAtPath(path) == action)
                {
                    legacyPaths.Add(path);
                }
                else
                {
                    error = "Cannot delete a Spawn Action outside its GSpawn library.";
                    return false;
                }
            }
            if (dbActions.Count == 0 && legacyPaths.Count == 0) return false;

            if (dbActions.Count != 0)
            {
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Delete Spawn Actions");
                Undo.RecordObject(db, "Delete Spawn Actions");
                foreach (SpawnAction action in dbActions) db.removeAction(action);
                foreach (SpawnAction action in dbActions) UndoEx.destroyObjectImmediate(action);
                EditorUtility.SetDirty(db);
                Undo.CollapseUndoOperations(group);
            }

            foreach (string path in legacyPaths)
            {
                if (!AssetDatabase.DeleteAsset(path))
                    error = "Failed to delete legacy Spawn Action asset: " + path;
            }
            AssetDatabase.SaveAssets();
            return string.IsNullOrEmpty(error);
        }

        // GSpawn's scene selection is separate from Unity's native Selection. Respect
        // whichever selection the user is actively editing; don't combine stale sets.
        public static void getSelection(List<GameObject> result)
        {
            result.Clear();
            if (GSpawn.active == null) return;

            if (GSpawn.active.levelDesignToolId == LevelDesignToolId.ObjectSelection)
            {
                ObjectSelection.instance.getSelectedObjects(result);
                if (result.Count != 0) return;
            }

            result.AddRange(Selection.gameObjects);
            result.RemoveAll(go => !go || go.GetComponent<GSpawn>() != null);
        }

        // GSpawn and Unity maintain independent selections. For recording, use the
        // complete valid output set from one source, never merge their stale entries.
        public static void getRecordingSelection(List<GameObject> result)
        {
            result.Clear();
            if (GSpawn.active == null) return;

            var gspawnSelection = new List<GameObject>();
            ObjectSelection.instance.getSelectedObjects(gspawnSelection);
            var unitySelection = new List<GameObject>(Selection.gameObjects);

            int gspawnCount = countRecordingOutputs(gspawnSelection);
            int unityCount = countRecordingOutputs(unitySelection);
            bool useGSpawn = gspawnCount > unityCount ||
                (gspawnCount == unityCount &&
                 GSpawn.active.levelDesignToolId == LevelDesignToolId.ObjectSelection);

            List<GameObject> source = useGSpawn ? gspawnSelection : unitySelection;
            var seen = new HashSet<GameObject>();
            foreach (GameObject raw in source)
            {
                GameObject root = resolveRoot(raw);
                if (root && seen.Add(root)) result.Add(root);
            }
        }

        private static int countRecordingOutputs(IReadOnlyList<GameObject> objects)
        {
            var seen = new HashSet<GameObject>();
            if (objects == null || GSpawn.active == null) return 0;
            foreach (GameObject raw in objects)
            {
                GameObject root = resolveRoot(raw);
                if (!root || !seen.Add(root)) continue;
                if (root.scene != GSpawn.active.gameObject.scene || !sourcePrefab(root))
                    return 0;
            }
            return seen.Count;
        }

        private static GameObject resolveRoot(GameObject go)
        {
            if (!go || EditorUtility.IsPersistent(go) || !go.scene.IsValid()) return null;
            GameObject nearest = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            return nearest ? nearest : go;
        }

        private static GameObject sourcePrefab(GameObject go)
        {
            if (!go || !PrefabUtility.IsPartOfPrefabInstance(go)) return null;
            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            return root ? PrefabUtility.GetCorrespondingObjectFromSource(root) : null;
        }

        public static bool matches(SpawnAction action, GameObject target)
        {
            if (action == null || !target) return false;
            GameObject targetPrefab = sourcePrefab(target);
            if (action.contextPrefab && targetPrefab == action.contextPrefab) return true;

            // RLD matches the prefab identity first, then uses canonical names and
            // semantic name families to support related prefab variations.
            string recordedName = action.contextPrefab ? action.contextPrefab.name : action.contextName;
            string targetName = targetPrefab ? targetPrefab.name : target.name;
            if (SpawnActionContextMatcher.matchesNames(recordedName, targetName)) return true;
            if (!string.Equals(targetName, target.name, StringComparison.Ordinal) &&
                SpawnActionContextMatcher.matchesNames(recordedName, target.name)) return true;

            // Older GSpawn recordings stored the scene object's name instead of the
            // prefab's name. Preserve their original matching evidence.
            if (!string.Equals(recordedName, action.contextName, StringComparison.Ordinal))
            {
                if (SpawnActionContextMatcher.matchesNames(action.contextName, targetName)) return true;
                if (!string.Equals(targetName, target.name, StringComparison.Ordinal) &&
                    SpawnActionContextMatcher.matchesNames(action.contextName, target.name)) return true;
            }
            return false;
        }

        private static GameObject resolveMatchingContext(SpawnAction action, GameObject raw)
        {
            GameObject root = resolveRoot(raw);
            if (!root) return null;
            if (action.contextPrefab && sourcePrefab(root) == action.contextPrefab) return root;

            if (action.contextPrefab)
            {
                // Legacy recordings sometimes picked a nested root. If its exact
                // prefab identity exists uniquely below the selected root, use it.
                GameObject found = null;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject candidate = child.gameObject;
                    if (candidate == root || PrefabUtility.GetNearestPrefabInstanceRoot(candidate) != candidate ||
                        sourcePrefab(candidate) != action.contextPrefab) continue;
                    if (found) return null;
                    found = candidate;
                }
                if (found) return found;
            }
            return matches(action, root) ? root : null;
        }

        public static bool validateRecordingOutputs(IReadOnlyList<GameObject> selectedObjects, out string error)
        {
            error = string.Empty;
            if (GSpawn.active == null)
            { error = "Initialize GSpawn first."; return false; }
            var seen = new HashSet<GameObject>();
            int count = 0;
            if (selectedObjects != null)
            {
                foreach (GameObject raw in selectedObjects)
                {
                    GameObject selected = resolveRoot(raw);
                    if (!selected || !seen.Add(selected)) continue;
                    if (selected.scene != GSpawn.active.gameObject.scene)
                    { error = "All recorded outputs must be in the active GSpawn scene."; return false; }
                    if (!sourcePrefab(selected))
                    { error = "Every output must be a prefab instance root: " + selected.name; return false; }
                    ++count;
                }
            }
            if (count == 0)
            { error = "Select one or more prefab instances to record as outputs."; return false; }
            return true;
        }

        public static bool tryRecord(GameObject rawContext, string actionName,
            out SpawnAction action, out string error)
        {
            var recordingSelection = new List<GameObject>();
            getRecordingSelection(recordingSelection);
            return tryRecord(rawContext, actionName, recordingSelection, out action, out error);
        }

        public static bool tryRecord(GameObject rawContext, string actionName,
            IReadOnlyList<GameObject> selectedOutputs, out SpawnAction action, out string error)
        {
            return tryRecord(rawContext, actionName, selectedOutputs, null, out action, out error);
        }

        public static bool tryRecord(GameObject rawContext, string actionName,
            IReadOnlyList<GameObject> selectedOutputs, SpawnAction existingAction,
            out SpawnAction action, out string error)
        {
            action = null;
            error = string.Empty;
            if (GSpawn.active == null) { error = "Initialize GSpawn first."; return false; }
            GameObject context = resolveRoot(rawContext);
            if (!context) { error = "Choose a scene context object (not a prefab asset)."; return false; }
            if (context.scene != GSpawn.active.gameObject.scene)
            {
                error = "The context and active GSpawn instance must belong to the same scene.";
                return false;
            }

            if (!validateRecordingOutputs(selectedOutputs, out error)) return false;
            _seen.Clear();
            var outputs = new List<SpawnActionOutput>();
            foreach (GameObject raw in selectedOutputs)
            {
                GameObject selected = resolveRoot(raw);
                if (!selected || !_seen.Add(selected)) continue;
                if (selected == context)
                { error = "The context cannot be one of the selected outputs."; return false; }
                if (selected.scene != context.scene)
                { error = "All recorded objects must belong to the context's scene."; return false; }
                if (selected.transform.IsChildOf(context.transform) || context.transform.IsChildOf(selected.transform))
                { error = "Outputs cannot be parents or children of the context."; return false; }

                GameObject prefab = sourcePrefab(selected);
                if (!prefab)
                { error = "Every output must be a prefab instance root: " + selected.name; return false; }

                outputs.Add(new SpawnActionOutput(prefab, context.transform, selected.transform));
            }
            if (outputs.Count == 0)
            { error = "Select one or more prefab outputs, excluding the context."; return false; }

            if (existingAction != null)
            {
                Undo.RecordObject(existingAction, "Record Spawn Action");
                existingAction.record(sourcePrefab(context),
                    SpawnActionContextMatcher.getDisplayName(sourcePrefab(context) ? sourcePrefab(context).name : context.name), outputs);
                EditorUtility.SetDirty(existingAction);
                action = existingAction;
            }
            else
            {
                action = createAction(actionName, out error);
                if (action == null) return false;
                Undo.RecordObject(action, "Record Spawn Action");
                action.record(sourcePrefab(context),
                    SpawnActionContextMatcher.getDisplayName(sourcePrefab(context) ? sourcePrefab(context).name : context.name), outputs);
                EditorUtility.SetDirty(action);
            }
            AssetDatabase.SaveAssets();
            return true;
        }


        public static SpawnAction createAction(string name, out string error)
        {
            error = string.Empty;
            SpawnActionDb db = loadDb(true);
            if (!db)
            {
                error = "Could not create the Spawn Actions database.";
                return null;
            }

            string cleanName = string.IsNullOrWhiteSpace(name) ? "Spawn Action" : name.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) cleanName = cleanName.Replace(c, '_');
            foreach (char c in new[] { '<', '>', ':', '"', '/', (char)92, '|', '?', '*' }) cleanName = cleanName.Replace(c, '_');
            var names = new List<string>();
            foreach (SpawnAction action in getActions()) names.Add(action.name);
            cleanName = UniqueNameGen.generate(cleanName, names);

            SpawnAction created = ScriptableObject.CreateInstance<SpawnAction>();
            created.name = cleanName;
            AssetDbEx.addObjectToAsset(created, db);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Create Spawn Action");
            Undo.RecordObject(db, "Create Spawn Action");
            db.addAction(created);
            UndoEx.registerCreatedObject(created, "Create Spawn Action");
            EditorUtility.SetDirty(db);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            return created;
        }

        public static bool renameAction(SpawnAction action, string name, out string error)
        {
            error = string.Empty;
            if (!action || string.IsNullOrWhiteSpace(name)) return false;
            string assetPath = AssetDatabase.GetAssetPath(action);
            if (string.IsNullOrEmpty(assetPath)) return false;
            string cleanName = name.Trim();
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) cleanName = cleanName.Replace(c, '_');
            foreach (char c in new[] { '<', '>', ':', '"', '/', (char)92, '|', '?', '*' }) cleanName = cleanName.Replace(c, '_');

            if (AssetDatabase.IsSubAsset(action))
            {
                if (assetPath != dbAssetPath) return false;
                Undo.RecordObject(action, "Rename Spawn Action");
                action.name = cleanName;
                EditorUtility.SetDirty(action);
            }
            else
            {
                error = AssetDatabase.RenameAsset(assetPath, cleanName);
                if (!string.IsNullOrEmpty(error)) return false;
            }
            AssetDatabase.SaveAssets();
            return true;
        }

        private struct SpawnRequest
        {
            public GameObject context;
            public GameObject prefab;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 worldScale;
            public OBB obb;
        }

        private struct OverlapReservation
        {
            public GameObject context;
            public OBB obb;
            public GameObject source;
            public Matrix4x4 rootWorldMatrix;
        }

        private static void collectMatchingTargets(SpawnAction action, bool regenerateOnly,
            IEnumerable<GameObject> selection, List<SpawnActionApplication> applications,
            List<GameObject> targets, ref int otherSceneCount,
            ref int unmatchedCount, ref int missingApplications)
        {
            foreach (GameObject raw in selection)
            {
                GameObject root = resolveRoot(raw);
                if (!root || root.GetComponent<GSpawn>() != null) continue;
                if (root.scene != GSpawn.active.gameObject.scene)
                { ++otherSceneCount; continue; }
                GameObject target = resolveMatchingContext(action, root);
                if (!target)
                { ++unmatchedCount; continue; }
                if (!_seen.Add(target)) continue;
                if (regenerateOnly && findApplication(applications, action, target) == null)
                { ++missingApplications; continue; }
                targets.Add(target);
            }
        }

        private static readonly System.Random _variationRandom = new System.Random();
        private static readonly List<SpawnRequest> _requests = new List<SpawnRequest>();
        private static readonly List<OverlapReservation> _reservations = new List<OverlapReservation>();
        private static readonly Dictionary<GameObject, AABB> _modelBounds = new Dictionary<GameObject, AABB>();

        private static float nextRandom01() { return (float)_variationRandom.NextDouble(); }

        public static int applyToSelection(SpawnAction action, bool regenerateOnly, out string status)
        {
            status = string.Empty;
            if (!action || !action.isRecorded || GSpawn.active == null)
            { status = "Choose a recorded Spawn Action and initialize GSpawn."; return 0; }

            getSelection(_selection);
            _seen.Clear();
            var targets = new List<GameObject>();
            List<SpawnActionApplication> applications = GSpawn.active.spawnActionApplications;
            if (applications == null)
            { status = "The active GSpawn scene data is unavailable."; return 0; }

            int otherSceneCount = 0;
            int unmatchedCount = 0;
            int missingApplications = 0;
            collectMatchingTargets(action, regenerateOnly, _selection, applications, targets,
                ref otherSceneCount, ref unmatchedCount, ref missingApplications);

            // GSpawn's custom selection and Unity's native selection are independent. In
            // particular, the selected context may belong to the inactive selection source.
            if (targets.Count == 0)
            {
                var selectedInGSpawn = new List<GameObject>();
                ObjectSelection.instance.getSelectedObjects(selectedInGSpawn);
                collectMatchingTargets(action, regenerateOnly, selectedInGSpawn, applications, targets,
                    ref otherSceneCount, ref unmatchedCount, ref missingApplications);
            }
            if (targets.Count == 0)
            {
                var nativeSelection = new List<GameObject>(Selection.gameObjects);
                collectMatchingTargets(action, regenerateOnly, nativeSelection, applications, targets,
                    ref otherSceneCount, ref unmatchedCount, ref missingApplications);
            }
            if (targets.Count == 0)
            {
                if (unmatchedCount > 0)
                {
                    string name = action.contextPrefab ? action.contextPrefab.name : action.contextName;
                    status = "The selected object does not match the recorded context: " + name + ".";
                }
                else if (otherSceneCount > 0)
                    status = "Select context objects in the same scene as the active GSpawn instance.";
                else if (missingApplications > 0)
                    status = "Refresh requires a previous application of this action to the selected context.";
                else
                    status = "Select a scene context object matching this action before clicking Apply.";
                return 0;
            }

            _requests.Clear();
            _reservations.Clear();
            SpawnActionMeshOverlap.clearCache();
            _modelBounds.Clear();
            int skippedOverlap = 0;
            var targetSet = new HashSet<GameObject>(targets);
            Quaternion gridRotation = PluginScene.instance != null && PluginScene.instance.grid != null ?
                PluginScene.instance.grid.rotation : Quaternion.identity;

            if (action.avoidOutputOverlaps)
            {
                foreach (SpawnActionApplication app in applications)
                {
                    if (app == null || app.action != action || !app.context || targetSet.Contains(app.context)) continue;
                    foreach (GameObject generated in app.generated)
                    {
                        if (!generated) continue;
                        OBB bounds = ObjectBounds.calcHierarchyWorldOBB(generated, ObjectBounds.QueryConfig.defaultConfig);
                        if (bounds.isValid)
                            _reservations.Add(new OverlapReservation
                            {
                                context = app.context, obb = bounds, source = generated,
                                rootWorldMatrix = generated.transform.localToWorldMatrix
                            });
                    }
                }
            }

            foreach (GameObject target in targets)
            {
                foreach (SpawnActionOutput output in action.outputs)
                {
                    if (output == null) { status = "A recorded output is missing. No changes made."; return 0; }
                    if (output.spawnChance <= 0.0f || nextRandom01() >= output.spawnChance) continue;
                    GameObject prefab = output.pickPrefab(nextRandom01());
                    if (!prefab) continue;
                    if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
                    { status = "A variation is not a prefab asset: " + prefab.name + ". No changes made."; return 0; }

                    float scale = output.getScaleMultiplier(nextRandom01());
                    Quaternion offset = output.getRotationOffset(new Vector3(nextRandom01(), nextRandom01(), nextRandom01()), gridRotation);
                    SpawnRequest request = new SpawnRequest
                    {
                        context = target,
                        prefab = prefab,
                        position = target.transform.TransformPoint(output.contextLocalPosition),
                        rotation = offset * (target.transform.rotation * output.contextLocalRotation),
                        worldScale = output.worldScale * scale,
                        obb = OBB.getInvalid()
                    };

                    if (action.avoidOutputOverlaps)
                    {
                        if (tryPredictOBB(request, out OBB obb))
                        {
                            request.obb = obb;
                            bool blocked = false;
                            foreach (OverlapReservation existing in _reservations)
                            {
                                if (existing.context == target || !meaningfulOverlap(existing.obb, obb)) continue;
                                bool meshesIntersect;
                                if (!SpawnActionMeshOverlap.tryMeshOverlap(existing.source, existing.rootWorldMatrix,
                                    request.prefab, Matrix4x4.TRS(request.position, request.rotation, request.worldScale),
                                    out meshesIntersect) || meshesIntersect)
                                { blocked = true; break; }
                            }
                            if (blocked) { ++skippedOverlap; continue; }
                            _reservations.Add(new OverlapReservation
                            {
                                context = target, obb = obb, source = request.prefab,
                                rootWorldMatrix = Matrix4x4.TRS(request.position, request.rotation, request.worldScale)
                            });
                        }
                    }
                    _requests.Add(request);
                }
            }

            // A recorded application with no outputs needs no Undo if this pass will not spawn anything.
            // An unrecorded application still needs to be created even when nothing is spawned.
            var changedTargets = new List<GameObject>();
            foreach (GameObject target in targets)
            {
                SpawnActionApplication previous = findApplication(applications, action, target);
                bool hasPreviousOutputs = previous != null && previous.generated != null && previous.generated.Count != 0;
                bool hasNewOutputs = false;
                foreach (SpawnRequest request in _requests)
                {
                    if (request.context != target) continue;
                    hasNewOutputs = true;
                    break;
                }
                if (previous == null || hasPreviousOutputs || hasNewOutputs)
                    changedTargets.Add(target);
            }
            if (changedTargets.Count == 0)
            {
                status = "No Spawn Action outputs to refresh.";
                if (skippedOverlap != 0) status += " Skipped " + skippedOverlap + " overlapping output(s).";
                return 0;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(regenerateOnly ? "Regenerate Spawn Actions" : "Apply Spawn Actions");
            int changed = 0;
            int spawnedCount = 0;
            try
            {
                foreach (GameObject target in changedTargets)
                {
                    SpawnActionApplication old = findApplication(applications, action, target);
                    Undo.RecordObject(GSpawn.active, "Apply Spawn Actions");
                    _toRemove.Clear();
                    if (old != null)
                    {
                        foreach (GameObject go in old.generated)
                            if (go) _toRemove.Add(go);
                        foreach (GameObject go in _toRemove)
                        {
                            ObjectEvents.onObjectWillBeDestroyed(go);
                            Undo.DestroyObjectImmediate(go);
                        }
                    }

                    _spawned.Clear();
                    foreach (SpawnRequest request in _requests)
                    {
                        if (request.context != target) continue;
                        GameObject instance = spawnOutput(request);
                        if (instance) { _spawned.Add(instance); ++spawnedCount; }
                    }
                    if (old != null) old.replaceGenerated(_spawned);
                    else applications.Add(new SpawnActionApplication(action, target, _spawned));
                    ++changed;
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
                if (changed != 0)
                {
                    EditorUtility.SetDirty(GSpawn.active);
                    EditorSceneManager.MarkSceneDirty(GSpawn.active.gameObject.scene);
                    SceneView.RepaintAll();
                }
            }
            status = (regenerateOnly ? "Regenerated " : "Applied to ") + changed + " context(s).";
            if (skippedOverlap != 0) status += " Skipped " + skippedOverlap + " overlapping output(s).";
            if (changed != 0 && spawnedCount == 0)
                status += " No objects were generated. Check spawn chances, variations, and overlap settings.";
            return changed;
        }

        private static bool tryPredictOBB(SpawnRequest request, out OBB obb)
        {
            obb = OBB.getInvalid();
            if (!_modelBounds.TryGetValue(request.prefab, out AABB model))
            {
                model = ObjectBounds.calcHierarchyModelAABB(request.prefab, ObjectBounds.QueryConfig.defaultConfig);
                _modelBounds.Add(request.prefab, model);
            }
            if (!model.isValid) return false;
            Vector3 size = Vector3.Scale(model.size, new Vector3(
                Mathf.Abs(request.worldScale.x), Mathf.Abs(request.worldScale.y), Mathf.Abs(request.worldScale.z)));
            Vector3 center = request.position + request.rotation * Vector3.Scale(model.center, request.worldScale);
            obb = new OBB(center, size, request.rotation);
            return obb.isValid;
        }

        private static bool meaningfulOverlap(OBB first, OBB second)
        {
            if (!first.isValid || !second.isValid) return false;
            Vector3 a = first.size;
            Vector3 b = second.size;
            float referenceSize = Mathf.Max(1.0f, Mathf.Max(
                Mathf.Max(a.x, Mathf.Max(a.y, a.z)), Mathf.Max(b.x, Mathf.Max(b.y, b.z))));
            float tolerance = referenceSize * 1e-5f;
            Vector3[] axesA = { first.right, first.up, first.look };
            Vector3[] axesB = { second.right, second.up, second.look };
            foreach (Vector3 axis in axesA)
                if (!hasMeaningfulAxisOverlap(first, second, axis, tolerance)) return false;
            foreach (Vector3 axis in axesB)
                if (!hasMeaningfulAxisOverlap(first, second, axis, tolerance)) return false;
            foreach (Vector3 axisA in axesA)
                foreach (Vector3 axisB in axesB)
                    if (!hasMeaningfulAxisOverlap(first, second, Vector3.Cross(axisA, axisB), tolerance)) return false;
            return true;
        }

        private static bool hasMeaningfulAxisOverlap(OBB first, OBB second, Vector3 axis, float tolerance)
        {
            float lengthSq = axis.sqrMagnitude;
            if (lengthSq <= 1e-10f) return true;
            axis /= Mathf.Sqrt(lengthSq);
            Vector3 e1 = first.extents;
            Vector3 e2 = second.extents;
            float r1 = e1.x * Mathf.Abs(Vector3.Dot(first.right, axis)) +
                e1.y * Mathf.Abs(Vector3.Dot(first.up, axis)) + e1.z * Mathf.Abs(Vector3.Dot(first.look, axis));
            float r2 = e2.x * Mathf.Abs(Vector3.Dot(second.right, axis)) +
                e2.y * Mathf.Abs(Vector3.Dot(second.up, axis)) + e2.z * Mathf.Abs(Vector3.Dot(second.look, axis));
            return r1 + r2 - Mathf.Abs(Vector3.Dot(second.center - first.center, axis)) > tolerance;
        }

        public static int removeFromSelection(SpawnAction action, out string status)
        {
            status = string.Empty;
            if (GSpawn.active == null || action == null)
            { status = "Choose a Spawn Action."; return 0; }
            getSelection(_selection);
            _seen.Clear();
            var targets = new HashSet<GameObject>();
            foreach (GameObject raw in _selection)
            {
                GameObject target = resolveMatchingContext(action, raw);
                if (target && target.scene == GSpawn.active.gameObject.scene)
                    targets.Add(target);
            }
            var applications = GSpawn.active.spawnActionApplications;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Remove Spawn Action Outputs");
            int changed = 0;
            try
            {
                for (int i = applications.Count - 1; i >= 0; --i)
                {
                    SpawnActionApplication app = applications[i];
                    if (app == null || app.action != action || !targets.Contains(app.context)) continue;
                    Undo.RecordObject(GSpawn.active, "Remove Spawn Action Outputs");
                    foreach (GameObject go in app.generated)
                    {
                        if (!go) continue;
                        ObjectEvents.onObjectWillBeDestroyed(go);
                        Undo.DestroyObjectImmediate(go);
                    }
                    applications.RemoveAt(i);
                    ++changed;
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
                if (changed != 0)
                {
                    EditorUtility.SetDirty(GSpawn.active);
                    EditorSceneManager.MarkSceneDirty(GSpawn.active.gameObject.scene);
                    SceneView.RepaintAll();
                }
            }
            status = changed == 0 ? "No recorded applications on those contexts." : "Removed " + changed + " application(s).";
            return changed;
        }

        private static SpawnActionApplication findApplication(List<SpawnActionApplication> apps,
            SpawnAction action, GameObject context)
        {
            foreach (SpawnActionApplication app in apps)
                if (app != null && app.action == action && app.context == context) return app;
            return null;
        }

        private static GameObject spawnOutput(SpawnRequest request)
        {
            PluginPrefab pluginPrefab = PrefabLibProfileDb.instance.getPrefab(request.prefab);
            GameObject instance;
            if (pluginPrefab != null)
                instance = pluginPrefab.spawn();
            else
            {
                instance = PrefabUtility.InstantiatePrefab(request.prefab, request.context.scene) as GameObject;
                if (!instance) return null;
                Undo.RegisterCreatedObjectUndo(instance, "Spawn Action Output");
            }
            if (!instance) return null;
            Transform t = instance.transform;
            t.position = request.position;
            t.rotation = request.rotation;
            t.setWorldScale(request.worldScale);
            ObjectEvents.onObjectSpawned(instance);
            return instance;
        }
    }
}
#endif
