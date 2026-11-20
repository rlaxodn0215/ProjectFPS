#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace GSPAWN
{
    public static class MeshCombiner
    {
        private class MaterialInstance
        {
            public Material                 material;
            public MeshRenderer             referenceRenderer;
            public Vector3Int               spatialCell;
            public List<MeshInstance>       meshInstances = new List<MeshInstance>();
        }

        // Efficient coarse lookup; the renderer settings are checked within each bucket.
        // In scenes with thousands of spatial cells this avoids O(N^2) group searches.
        private struct MaterialSpatialKey : IEquatable<MaterialSpatialKey>
        {
            public Material                  material;
            public Vector3Int                cell;

            public bool Equals(MaterialSpatialKey other)
            {
                return material == other.material && cell == other.cell;
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialSpatialKey && Equals((MaterialSpatialKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked { return ((material != null ? material.GetHashCode() : 0) * 397) ^ cell.GetHashCode(); }
            }
        }

        private class MeshInstance
        {
            public Mesh                     mesh;
            public int                      subMeshIndex;
            public Transform                transform;
            public MeshRenderer             renderer;
        }

        private class CombinedMeshData
        {
            public List<Vector3>            positions   = new List<Vector3>();
            public List<Vector3>            normals     = new List<Vector3>();
            public List<Vector4>            tangents    = new List<Vector4>();
            public List<Color>              colors      = new List<Color>();
            public List<Vector4>            uv1         = new List<Vector4>();
            public List<Vector4>            uv2         = new List<Vector4>();
            public List<Vector4>            uv3         = new List<Vector4>();
            public List<Vector4>            uv4         = new List<Vector4>();
            public List<Vector4>            uv5         = new List<Vector4>();
            public List<Vector4>            uv6         = new List<Vector4>();
            public List<Vector4>            uv7         = new List<Vector4>();
            public List<Vector4>            uv8         = new List<Vector4>();
            public List<int>                indices     = new List<int>();

            public bool                     hasTangents;
            public bool                     hasColors;
            public bool                     hasUV1;
            public bool                     hasUV2;
            public bool                     hasUV3;
            public bool                     hasUV4;
            public bool                     hasUV5;
            public bool                     hasUV6;
            public bool                     hasUV7;
            public bool                     hasUV8;

            public void reset()
            {
                positions.Clear();
                normals.Clear();
                tangents.Clear();
                colors.Clear();
                uv1.Clear();
                uv2.Clear();
                uv3.Clear();
                uv4.Clear();
                uv5.Clear();
                uv6.Clear();
                uv7.Clear();
                uv8.Clear();
                indices.Clear();

                hasTangents = false;
                hasColors = false;
                hasUV1 = false;
                hasUV2 = false;
                hasUV3 = false;
                hasUV4 = false;
                hasUV5 = false;
                hasUV6 = false;
                hasUV7 = false;
                hasUV8 = false;
            }
        }

        // Count exclusions per renderer, with an actionable explanation for an empty result.
        // A renderer is counted only once, at the first validation rule that excludes it.
        private sealed class CombineDiagnostics
        {
            private sealed class Exclusion
            {
                public string               reason;
                public string               remedy;
                public string               example;
                public int                  count;
                public bool                 expected;
            }

            public int                      inputObjects;
            public int                      renderersFound;
            public int                      candidateRenderers;
            public int                      supportedRenderers;
            private List<Exclusion>          _exclusions = new List<Exclusion>();

            public bool hasExclusions { get { return _exclusions.Count != 0; } }
            public bool hasUnexpectedExclusions { get { return _exclusions.Exists(e => !e.expected); } }

            public void skip(MeshRenderer renderer, string reason, string remedy, bool expected = false)
            {
                Exclusion exclusion = null;
                foreach (var item in _exclusions)
                {
                    if (item.reason == reason) { exclusion = item; break; }
                }
                if (exclusion == null)
                {
                    exclusion = new Exclusion();
                    exclusion.reason = reason;
                    exclusion.remedy = remedy;
                    exclusion.expected = expected;
                    _exclusions.Add(exclusion);
                }
                ++exclusion.count;
                if (exclusion.example == null && renderer != null)
                    exclusion.example = renderer.gameObject.name;
            }

            public void addPreviewExclusions(MeshCombinePreviewWindow.Report report)
            {
                foreach (var exclusion in _exclusions)
                {
                    report.exclusions.Add(new MeshCombinePreviewWindow.ExclusionInfo
                    {
                        reason      = exclusion.reason,
                        remedy      = exclusion.remedy,
                        example     = exclusion.example,
                        count       = exclusion.count,
                        expected    = exclusion.expected
                    });
                }
            }

            public string buildReport(bool nothingCombined, string detail, int maxReasons = int.MaxValue, bool preview = false)
            {
                var builder = new StringBuilder();
                if (preview)
                {
                    if (nothingCombined) builder.AppendLine("No eligible meshes found with the current settings.");
                    else builder.AppendLine("Some renderers are excluded by the current settings.");
                }
                else if (nothingCombined) builder.AppendLine("No meshes were combined.");
                else builder.AppendLine("Some source meshes were skipped.");
                builder.AppendLine();
                builder.AppendLine("Input objects: " + inputObjects);
                builder.AppendLine("MeshRenderers found: " + renderersFound);
                builder.AppendLine("Passed initial checks: " + candidateRenderers);
                builder.AppendLine("Supported mesh renderers: " + supportedRenderers);
                if (!string.IsNullOrEmpty(detail))
                {
                    builder.AppendLine();
                    builder.AppendLine(detail);
                }
                if (renderersFound == 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("No MeshRenderer components were found on the selected objects or their children. SkinnedMeshRenderers are not supported. Ensure the intended scene objects are selected.");
                }

                if (_exclusions.Count != 0)
                {
                    _exclusions.Sort((a, b) => b.count.CompareTo(a.count));
                    builder.AppendLine();
                    builder.AppendLine("Excluded renderers:");
                    int shown = 0;
                    foreach (var exclusion in _exclusions)
                    {
                        if (shown == maxReasons) break;
                        ++shown;
                        builder.AppendLine("- " + exclusion.count + " : " + exclusion.reason + (exclusion.expected ? " (filtered by settings)" : ""));
                        if (!string.IsNullOrEmpty(exclusion.example))
                            builder.AppendLine("  Example: " + exclusion.example);
                        if (!string.IsNullOrEmpty(exclusion.remedy))
                            builder.AppendLine("  " + exclusion.remedy);
                    }
                    if (shown < _exclusions.Count)
                        builder.AppendLine("Additional exclusion categories: " + (_exclusions.Count - shown) + ".");
                }
                return builder.ToString().TrimEnd();
            }

            public void showNoMeshes(string detail)
            {
                EditorUtility.DisplayDialog("Nothing Combined", buildReport(true, detail, 5), "OK");
            }

        }

        private static MeshCombineSettings    _settings;
        private static List<GameObject>       _parentsBuffer = new List<GameObject>();

        public static void combine(List<GameObject> sourceObjects, GameObject destinationParent, MeshCombineSettings settings)
        {
            if (sourceObjects == null || sourceObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("Missing Data", "No source objects available.", "Ok");
                return;
            }
            if (!validateSettings(destinationParent, settings)) return;

            _settings = settings;

            var validSources = new List<GameObject>();
            foreach (var source in sourceObjects)
                if (source != null && !validSources.Contains(source)) validSources.Add(source);
            GameObjectEx.getParents(validSources, _parentsBuffer);
            var meshObjects = new List<GameObject>();
            var diagnostics = new CombineDiagnostics();
            diagnostics.inputObjects = sourceObjects.Count;
            try
            {
                collectMeshObjects(_parentsBuffer, meshObjects, diagnostics);
                combineMeshObjects(meshObjects, destinationParent, diagnostics);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Mesh Combine Failed", "The mesh combine operation failed:\n" + ex.Message, "OK");
            }
            finally
            {
                PluginProgressDialog.end();
                _parentsBuffer.Clear();
            }
        }

        public static void combineChildren(GameObject sourceParent, GameObject destinationParent, MeshCombineSettings settings)
        {
            if (sourceParent == null)
            {
                EditorUtility.DisplayDialog("Missing Data", "You must specify a source parent whose child meshes must be combined.", "Ok");
                return;
            }
            if (!validateSettings(destinationParent, settings)) return;

            _settings = settings;

            var meshObjects = new List<GameObject>();
            var diagnostics = new CombineDiagnostics();
            diagnostics.inputObjects = 1;
            try
            {
                collectMeshObjects(sourceParent, meshObjects, diagnostics);
                combineMeshObjects(meshObjects, destinationParent, diagnostics);
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Mesh Combine Failed", "The mesh combine operation failed:\n" + ex.Message, "OK");
            }
            finally
            {
                PluginProgressDialog.end();
            }
        }

        // Stage C: a non-destructive preflight. This shares source collection, renderer
        // validation, material grouping and chunk-limit decisions with the real operation.
        public static void preview(List<GameObject> sourceObjects, GameObject destinationParent, MeshCombineSettings settings)
        {
            if (sourceObjects == null || sourceObjects.Count == 0)
            {
                showPreviewMessage("No source objects selected.", "Select one or more scene objects and use Preview selected.");
                return;
            }

            string settingsError = getSettingsError(destinationParent, settings);
            if (settingsError != null)
            {
                showPreviewMessage("Preview unavailable", settingsError);
                return;
            }

            _settings = settings;
            var validSources = new List<GameObject>();
            foreach (var source in sourceObjects)
                if (source != null && !validSources.Contains(source)) validSources.Add(source);
            GameObjectEx.getParents(validSources, _parentsBuffer);
            var meshObjects = new List<GameObject>();
            var diagnostics = new CombineDiagnostics();
            diagnostics.inputObjects = sourceObjects.Count;
            try
            {
                collectMeshObjects(_parentsBuffer, meshObjects, diagnostics);
                showPreview(meshObjects, diagnostics);
            }
            catch (Exception ex)
            {
                showPreviewMessage("Unable to analyze the selected meshes", ex.Message);
            }
            finally
            {
                PluginProgressDialog.end();
                _parentsBuffer.Clear();
            }
        }

        public static void previewChildren(GameObject sourceParent, GameObject destinationParent, MeshCombineSettings settings)
        {
            if (sourceParent == null)
            {
                showPreviewMessage("No source parent assigned.", "Choose a Source parent and use Preview children.");
                return;
            }

            string settingsError = getSettingsError(destinationParent, settings);
            if (settingsError != null)
            {
                showPreviewMessage("Preview unavailable", settingsError);
                return;
            }

            _settings = settings;
            var meshObjects = new List<GameObject>();
            var diagnostics = new CombineDiagnostics();
            diagnostics.inputObjects = 1;
            try
            {
                collectMeshObjects(sourceParent, meshObjects, diagnostics);
                showPreview(meshObjects, diagnostics);
            }
            catch (Exception ex)
            {
                showPreviewMessage("Unable to analyze the source meshes", ex.Message);
            }
            finally { PluginProgressDialog.end(); }
        }

        private static void showPreviewMessage(string message, string detail)
        {
            MeshCombinePreviewWindow.showReport(new MeshCombinePreviewWindow.Report
            {
                message = message,
                detail = detail
            });
        }

        private struct MeshEstimate
        {
            public int                  outputMeshes;
            public long                 outputVertices;
            public long                 triangles;
        }

        private static MeshEstimate estimateGroup(MaterialInstance materialInstance)
        {
            var estimate = new MeshEstimate();
            int maxVertices = getMaxNumberOfMeshVerts();
            int chunkVertices = 0;
            foreach (var instance in materialInstance.meshInstances)
            {
                var mesh = instance.mesh;
                int[] triangles = mesh.GetTriangles(instance.subMeshIndex);
                bool canReuse = mesh.normals.Length == mesh.vertexCount;
                var remap = new Dictionary<int, byte>();
                for (int tri = 0; tri + 2 < triangles.Length; tri += 3)
                {
                    int i0 = triangles[tri];
                    int i1 = triangles[tri + 1];
                    int i2 = triangles[tri + 2];
                    int fresh = countNewTriangleVertices(remap, canReuse, i0, i1, i2);
                    if (chunkVertices + fresh > maxVertices)
                    {
                        if (chunkVertices != 0)
                        {
                            ++estimate.outputMeshes;
                            estimate.outputVertices += chunkVertices;
                        }
                        chunkVertices = 0;
                        remap.Clear();
                    }

                    if (canReuse)
                    {
                        if (!remap.ContainsKey(i0)) { remap.Add(i0, 0); ++chunkVertices; }
                        if (!remap.ContainsKey(i1)) { remap.Add(i1, 0); ++chunkVertices; }
                        if (!remap.ContainsKey(i2)) { remap.Add(i2, 0); ++chunkVertices; }
                    }
                    else chunkVertices += 3;

                    ++estimate.triangles;
                }
            }
            if (chunkVertices != 0)
            {
                ++estimate.outputMeshes;
                estimate.outputVertices += chunkVertices;
            }
            return estimate;
        }

        private static void showPreview(List<GameObject> meshObjects, CombineDiagnostics diagnostics)
        {
            var groups = new List<MaterialInstance>();
            var acceptedRenderers = new HashSet<MeshRenderer>();
            if (meshObjects.Count != 0)
                collectMaterialInstances(meshObjects, groups, acceptedRenderers, diagnostics);

            var report = new MeshCombinePreviewWindow.Report();
            report.inputObjects = diagnostics.inputObjects;
            report.renderersFound = diagnostics.renderersFound;
            report.candidateRenderers = diagnostics.candidateRenderers;
            report.eligibleRenderers = diagnostics.supportedRenderers;
            report.materialGroups = groups.Count;
            report.indexFormat = _settings.combinedIndexFormat.ToString();
            report.spatialChunks = _settings.useSpatialChunks;
            report.spatialCellSize = _settings.spatialChunkSize;
            report.generateLightmapUVs = _settings.generateLightmapUVs;
            diagnostics.addPreviewExclusions(report);

            foreach (var renderer in acceptedRenderers)
            {
                if (renderer == null) continue;
                if (findLODIndex(renderer) >= 0) ++report.lodRenderers;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) report.inputVertices += filter.sharedMesh.vertexCount;
            }

            if (groups.Count != 0)
            {
                PluginProgressDialog.begin("Estimating Combined Meshes");
                try
                {
                    for (int i = 0; i < groups.Count; ++i)
                    {
                        MaterialInstance group = groups[i];
                        PluginProgressDialog.updateProgress(group.material.name, (i + 1) / (float)groups.Count);
                        MeshEstimate estimate = estimateGroup(group);
                        report.outputMeshes += estimate.outputMeshes;
                        report.outputVertices += estimate.outputVertices;
                        report.triangles += estimate.triangles;
                    }
                }
                finally { PluginProgressDialog.end(); }
            }

            MeshCombinePreviewWindow.showReport(report);
        }

        private static string safeAssetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "CombinedMesh";
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; ++i)
            {
                char c = chars[i];
                if (char.IsControl(c) || c == '/' || c == '\\' || c == ':' || c == '*' ||
                    c == '?' || c == '"' || c == '<' || c == '>' || c == '|') chars[i] = '_';
            }
            return new string(chars);
        }

        // A non-invertible destination/source transform cannot be baked into a combined mesh.
        // Check it before creating assets, instead of allowing NaN/Infinity vertices through.
        private static bool hasUsableTransform(Matrix4x4 matrix)
        {
            float determinant = matrix.determinant;
            if (float.IsNaN(determinant) || float.IsInfinity(determinant) ||
                Mathf.Abs(determinant) < 1e-20f) return false;

            for (int i = 0; i < 16; ++i)
                if (float.IsNaN(matrix[i]) || float.IsInfinity(matrix[i])) return false;
            return true;
        }

        public static bool isValidOutputFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.EndsWith("/", StringComparison.Ordinal) || path.IndexOf('\\') >= 0 ||
                path.IndexOf(':') >= 0 || path.IndexOf('\0') >= 0) return false;

            foreach (char c in path)
                if (char.IsControl(c)) return false;

            string[] components = path.Split('/');
            foreach (string component in components)
            {
                if (string.IsNullOrEmpty(component) || component == "." || component == ".." ||
                    component.Trim() != component || component.IndexOfAny(new char[] { '<', '>', '"', '|', '?', '*' }) >= 0)
                    return false;
            }

            string pluginDataFolder = PluginFolders.data;
            if (path.Equals(pluginDataFolder, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(pluginDataFolder + "/", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private static string getSettingsError(GameObject destinationParent, MeshCombineSettings settings)
        {
            if (destinationParent == null)
                return "Assign a Destination parent for the combined mesh objects.";
            if (settings == null || (!settings.combineStaticMeshes && !settings.combineDynamicMeshes))
                return "Enable Combine static meshes or Combine dynamic meshes.";
            if (!isValidOutputFolderPath(settings.combinedMeshFolder))
                return "Choose a valid folder inside Assets, outside GSpawn's Data folder. Do not use '.', '..', or trailing slashes.";
            if (!destinationParent.scene.IsValid())
                return "The Destination parent must be a scene object, not a prefab asset.";
            if (!hasUsableTransform(destinationParent.transform.localToWorldMatrix))
                return "The Destination parent has a non-invertible transform. Check its scale and parent transforms.";
            return null;
        }

        private static bool validateSettings(GameObject destinationParent, MeshCombineSettings settings)
        {
            string error = getSettingsError(destinationParent, settings);
            if (error == null) return true;
            EditorUtility.DisplayDialog("Invalid Mesh Combine Settings", error, "OK");
            return false;
        }

        private static void collectMeshObjects(List<GameObject> parentObjects, List<GameObject> meshObjects, CombineDiagnostics diagnostics)
        {
            meshObjects.Clear();
            var visited = new HashSet<MeshRenderer>();
            int count = parentObjects.Count;
            PluginProgressDialog.begin("Collecting Mesh Objects");
            try
            {
                for (int i = 0; i < count; ++i)
                {
                    GameObject parent = parentObjects[i];
                    if (parent == null) continue;
                    collectMeshObjectsInternal(parent, meshObjects, visited, diagnostics);
                    PluginProgressDialog.updateProgress(parent.name, (i + 1) / (float)count);
                }
            }
            finally { PluginProgressDialog.end(); }
        }

        private static void collectMeshObjects(GameObject sourceParent, List<GameObject> meshObjects, CombineDiagnostics diagnostics)
        {
            meshObjects.Clear();
            PluginProgressDialog.begin("Collecting Mesh Objects");
            try { collectMeshObjectsInternal(sourceParent, meshObjects, new HashSet<MeshRenderer>(), diagnostics); }
            finally { PluginProgressDialog.end(); }
        }

        private static void collectMeshObjectsInternal(GameObject sourceParent, List<GameObject> meshObjects,
            HashSet<MeshRenderer> visited, CombineDiagnostics diagnostics)
        {
            var renderers = sourceParent.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !visited.Add(renderer)) continue;
                ++diagnostics.renderersFound;
                if (canMeshObjectBeCombined(renderer, sourceParent, diagnostics))
                {
                    meshObjects.Add(renderer.gameObject);
                    ++diagnostics.candidateRenderers;
                }
            }
        }

        private static int findLODIndex(MeshRenderer renderer)
        {
            // LOD renderers are not necessarily direct children of the LODGroup GameObject.
            for (Transform transform = renderer.transform; transform != null; transform = transform.parent)
            {
                LODGroup group = transform.GetComponent<LODGroup>();
                if (group == null) continue;

                var lods = group.GetLODs();
                for (int lodIndex = 0; lodIndex < lods.Length; ++lodIndex)
                    foreach (var lodRenderer in lods[lodIndex].renderers)
                        if (lodRenderer == renderer) return lodIndex;
            }
            return -1;
        }

        private static bool canMeshObjectBeCombined(MeshRenderer renderer, GameObject sourceParent, CombineDiagnostics diagnostics)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                diagnostics.skip(renderer, "Inactive or disabled MeshRenderer", "Enable the source renderer and GameObject.", true);
                return false;
            }
            if (renderer.forceRenderingOff)
            {
                diagnostics.skip(renderer, "Rendering forced off", "Disable 'Force Rendering Off' if the renderer should participate.", true);
                return false;
            }
            if (EditorUtility.IsPersistent(renderer))
            {
                diagnostics.skip(renderer, "Prefab asset instead of scene instance", "Select scene objects rather than prefab assets.");
                return false;
            }

            GameObject gameObject = renderer.gameObject;
            if (!_settings.combineStaticMeshes && gameObject.isStatic)
            {
                diagnostics.skip(renderer, "Static meshes excluded", "Enable 'Combine static meshes'.", true);
                return false;
            }
            if (!_settings.combineDynamicMeshes && !gameObject.isStatic)
            {
                diagnostics.skip(renderer, "Dynamic meshes excluded", "Enable 'Combine dynamic meshes'.", true);
                return false;
            }

            // Do not exclude sources simply because they are children of the destination parent.
            // This is a valid workflow, especially with 'Combine selected'. Collection finishes
            // before outputs are created, so the newly created children cannot be re-collected.
            var filter = gameObject.getMeshFilter();
            if (filter == null || filter.sharedMesh == null)
            {
                diagnostics.skip(renderer, "Missing MeshFilter or source mesh", "Ensure each MeshRenderer has a MeshFilter with a valid mesh.");
                return false;
            }
            string sourceAssetPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!string.IsNullOrEmpty(sourceAssetPath) &&
                sourceAssetPath.StartsWith(_settings.combinedMeshFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.skip(renderer, "Previously generated combined mesh", "Select the original source meshes instead of previously generated outputs.");
                return false;
            }

            if (_settings.ignoreMultiLevelHierarchies)
            {
                bool nested = sourceParent != null ?
                    ((gameObject.transform.parent != null && gameObject.transform.parent.gameObject != sourceParent) ||
                        gameObject.transform.childCount != 0) :
                    (gameObject.transform.parent != null || gameObject.transform.childCount != 0);
                if (nested)
                {
                    diagnostics.skip(renderer, "Multi-level hierarchy excluded", "Disable 'Ignore multi-level hierarchies'.", true);
                    return false;
                }
            }

            if (!_settings.combineLODs && findLODIndex(renderer) >= 0)
            {
                diagnostics.skip(renderer, "LODGroup renderer excluded", "Enable 'Combine LODs' and choose the intended LOD index.", true);
                return false;
            }
            return true;
        }

        // World-space cells are optional. The cell comes from bounds center, not the pivot,
        // so displaced mesh pivots still get grouped near their visible geometry.
        private static Vector3Int getSpatialCell(MeshRenderer renderer)
        {
            if (!_settings.useSpatialChunks) return Vector3Int.zero;

            float size = _settings.spatialChunkSize;
            Vector3 center = renderer.bounds.center;
            return new Vector3Int(Mathf.FloorToInt(center.x / size),
                                  Mathf.FloorToInt(center.y / size),
                                  Mathf.FloorToInt(center.z / size));
        }

        private static bool rendererSettingsMatch(MeshRenderer a, MeshRenderer b)
        {
            return a.gameObject.layer == b.gameObject.layer &&
                   a.shadowCastingMode == b.shadowCastingMode &&
                   a.staticShadowCaster == b.staticShadowCaster &&
                   a.receiveShadows == b.receiveShadows &&
                   a.lightProbeUsage == b.lightProbeUsage &&
                   a.reflectionProbeUsage == b.reflectionProbeUsage &&
                   a.lightmapIndex == b.lightmapIndex &&
                   a.lightmapScaleOffset == b.lightmapScaleOffset &&
                   a.realtimeLightmapIndex == b.realtimeLightmapIndex &&
                   a.realtimeLightmapScaleOffset == b.realtimeLightmapScaleOffset &&
                   a.probeAnchor == b.probeAnchor &&
                   a.lightProbeProxyVolumeOverride == b.lightProbeProxyVolumeOverride &&
                   a.sortingLayerID == b.sortingLayerID &&
                   a.sortingOrder == b.sortingOrder &&
                   a.rendererPriority == b.rendererPriority &&
                   a.rayTracingMode == b.rayTracingMode &&
                   a.motionVectorGenerationMode == b.motionVectorGenerationMode &&
                   a.allowOcclusionWhenDynamic == b.allowOcclusionWhenDynamic &&
                   a.renderingLayerMask == b.renderingLayerMask &&
                   a.receiveGI == b.receiveGI &&
                   a.scaleInLightmap == b.scaleInLightmap &&
                   a.stitchLightmapSeams == b.stitchLightmapSeams;
        }

        private static void collectMaterialInstances(List<GameObject> meshObjects, List<MaterialInstance> materialInstances,
            HashSet<MeshRenderer> sourceRenderers, CombineDiagnostics diagnostics)
        {
            materialInstances.Clear();
            sourceRenderers.Clear();

            var buckets = new Dictionary<MaterialSpatialKey, List<MaterialInstance>>();
            PluginProgressDialog.begin("Collecting Material Instances");
            try
            {
                for (int i = 0; i < meshObjects.Count; ++i)
                {
                    GameObject meshObject = meshObjects[i];
                    PluginProgressDialog.updateProgress(meshObject.name, (i + 1) / (float)meshObjects.Count);

                    var filter = meshObject.getMeshFilter();
                    var renderer = meshObject.getMeshRenderer();
                    if (filter == null || renderer == null) continue;
                    if (renderer.additionalVertexStreams != null || renderer.enlightenVertexStream != null)
                    {
                        // MeshRenderer vertex streams override source mesh attributes and are not merged here.
                        diagnostics.skip(renderer, "Additional vertex streams", "Remove additional vertex-stream overrides or use their baked meshes.");
                        continue;
                    }

                    var propertyBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(propertyBlock);
                    if (!propertyBlock.isEmpty)
                    {
                        // Combining instances with per-renderer overrides would change their appearance.
                        diagnostics.skip(renderer, "MaterialPropertyBlock overrides", "Remove per-renderer material overrides before combining.");
                        continue;
                    }

                    if (_settings.combineLODs)
                    {
                        int lodIndex = findLODIndex(renderer);
                        if (lodIndex >= 0 && lodIndex != _settings.lodIndex)
                        {
                            diagnostics.skip(renderer, "Different LOD level", "Choose the matching LOD index in Mesh Combine settings.", true);
                            continue;
                        }
                    }

                    Mesh mesh = filter.sharedMesh;
                    if (!hasUsableTransform(renderer.transform.localToWorldMatrix))
                    {
                        diagnostics.skip(renderer, "Non-invertible source transform",
                            "Check the source object's scale and parent transforms (zero scale is unsupported).");
                        continue;
                    }
                    // Skipping unsupported renderers as a whole means we never hide only part of their geometry.
                    if (!mesh.isReadable)
                    {
                        diagnostics.skip(renderer, "Source mesh is not readable",
                            "Enable 'Read/Write' in the source model import settings. 'Combined meshes are readable' only affects outputs.");
                        continue;
                    }
                    if (mesh.vertexCount == 0 || mesh.subMeshCount == 0)
                    {
                        diagnostics.skip(renderer, "Empty source mesh", "Check the mesh asset for vertices and triangle submeshes.");
                        continue;
                    }
                    var materials = renderer.sharedMaterials;
                    if (materials.Length != mesh.subMeshCount)
                    {
                        // Unity draws the final submesh again for each excess material slot.
                        // Silently ignoring those slots would remove visible material layers.
                        diagnostics.skip(renderer,
                            materials.Length < mesh.subMeshCount ? "Too few material slots" : "Extra material slots",
                            materials.Length < mesh.subMeshCount ? "Assign one material for every mesh submesh." :
                            "Extra materials render the last submesh again. Match material slots to submeshes before combining.");
                        continue;
                    }

                    bool valid = true;
                    string invalidReason = null;
                    string invalidRemedy = null;
                    for (int submesh = 0; submesh < mesh.subMeshCount; ++submesh)
                    {
                        if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                        {
                            valid = false;
                            invalidReason = "Non-triangle submesh";
                            invalidRemedy = "Only triangle submeshes can be combined.";
                            break;
                        }
                        if (mesh.GetIndexCount(submesh) < 3)
                        {
                            valid = false;
                            invalidReason = "Submesh has no triangles";
                            invalidRemedy = "Check source mesh triangle indices.";
                            break;
                        }
                        if (materials[submesh] == null)
                        {
                            valid = false;
                            invalidReason = "Missing material";
                            invalidRemedy = "Assign a non-null material to every submesh.";
                            break;
                        }
                        propertyBlock.Clear();
                        renderer.GetPropertyBlock(propertyBlock, submesh);
                        if (!propertyBlock.isEmpty)
                        {
                            valid = false;
                            invalidReason = "Per-material property overrides";
                            invalidRemedy = "Remove per-material MaterialPropertyBlock overrides before combining.";
                            break;
                        }
                    }
                    if (valid && renderer.lightmapIndex >= 0 && !_settings.generateLightmapUVs)
                    {
                        var sourceUV2 = new List<Vector4>();
                        mesh.GetUVs(1, sourceUV2);
                        if (sourceUV2.Count != mesh.vertexCount)
                        {
                            valid = false;
                            invalidReason = "Lightmapped mesh without UV2";
                            invalidRemedy = "Provide UV2 lightmap coordinates or enable 'Generate lightmap UVs'.";
                        }
                    }
                    if (!valid)
                    {
                        diagnostics.skip(renderer, invalidReason ?? "Unsupported source mesh", invalidRemedy);
                        continue;
                    }

                    Vector3Int spatialCell = getSpatialCell(renderer);
                    // Grouping by material, compatible renderer settings, and optionally by
                    // world-space cell avoids giant meshes extending across an entire level.
                    for (int submesh = 0; submesh < mesh.subMeshCount; ++submesh)
                    {
                        Material material = materials[submesh];
                        var key = new MaterialSpatialKey { material = material, cell = spatialCell };
                        List<MaterialInstance> candidates;
                        if (!buckets.TryGetValue(key, out candidates))
                        {
                            candidates = new List<MaterialInstance>();
                            buckets.Add(key, candidates);
                        }

                        MaterialInstance entry = null;
                        foreach (var candidate in candidates)
                        {
                            if (rendererSettingsMatch(candidate.referenceRenderer, renderer))
                            {
                                entry = candidate;
                                break;
                            }
                        }
                        if (entry == null)
                        {
                            entry = new MaterialInstance();
                            entry.material = material;
                            entry.referenceRenderer = renderer;
                            entry.spatialCell = spatialCell;
                            materialInstances.Add(entry);
                            candidates.Add(entry);
                        }

                        var instance = new MeshInstance();
                        instance.mesh = mesh;
                        instance.subMeshIndex = submesh;
                        instance.transform = meshObject.transform;
                        instance.renderer = renderer;
                        entry.meshInstances.Add(instance);
                    }
                    sourceRenderers.Add(renderer);
                    ++diagnostics.supportedRenderers;
                }
            }
            finally { PluginProgressDialog.end(); }
        }

        private static void combineMeshObjects(List<GameObject> meshObjects, GameObject destinationParent, CombineDiagnostics diagnostics)
        {
            if (meshObjects.Count == 0)
            {
                diagnostics.showNoMeshes(null);
                return;
            }

            var materialInstances = new List<MaterialInstance>();
            var sourceRenderers = new HashSet<MeshRenderer>();
            collectMaterialInstances(meshObjects, materialInstances, sourceRenderers, diagnostics);
            if (materialInstances.Count == 0)
            {
                diagnostics.showNoMeshes(null);
                return;
            }

            // Build and save outputs before changing the source renderers or registering Undo.
            // On failure only objects/assets created by this run will be deleted.
            var createdObjects = new List<GameObject>();
            var createdMeshes = new List<Mesh>();
            var createdAssetPaths = new List<string>();
            bool committed = false;
            var previousRendererStates = new Dictionary<MeshRenderer, bool>();
            try
            {
                if (!AssetDatabase.IsValidFolder(_settings.combinedMeshFolder))
                    AssetDbEx.createAssetFolder(_settings.combinedMeshFolder);
                if (!AssetDatabase.IsValidFolder(_settings.combinedMeshFolder))
                    throw new InvalidOperationException("Unable to create combined mesh folder: " + _settings.combinedMeshFolder);

                for (int i = 0; i < materialInstances.Count; ++i)
                    combine(materialInstances[i], destinationParent, createdObjects, createdMeshes);

                if (createdMeshes.Count == 0)
                {
                    diagnostics.showNoMeshes("The accepted source meshes produced no triangle data.");
                    return;
                }

                PluginProgressDialog.begin("Saving Mesh Assets");
                try
                {
                    for (int i = 0; i < createdMeshes.Count; ++i)
                    {
                        Mesh mesh = createdMeshes[i];
                        PluginProgressDialog.updateProgress(mesh.name, (i + 1) / (float)createdMeshes.Count);
                        string path = AssetDatabase.GenerateUniqueAssetPath(_settings.combinedMeshFolder + "/" + mesh.name + ".asset");
                        // Track the path *before* CreateAsset so a partially created asset is cleaned up too.
                        createdAssetPaths.Add(path);
                        AssetDatabase.CreateAsset(mesh, path);
                        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) == null)
                            throw new InvalidOperationException("Failed to save combined mesh asset: " + path);
                    }
                    AssetDatabase.SaveAssets();
                }
                finally { PluginProgressDialog.end(); }

                // One Undo action for the complete scene operation. Mesh assets deliberately remain
                // in the project on Undo: Redo needs them to reconstruct the scene GameObjects.
                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Combine Meshes");
                foreach (var obj in createdObjects) Undo.RegisterCreatedObjectUndo(obj, "Combine Meshes");

                if (_settings.disableSourceRenderers)
                {
                    foreach (var renderer in sourceRenderers)
                    {
                        if (renderer == null) continue;
                        previousRendererStates.Add(renderer, renderer.enabled);
                        Undo.RecordObject(renderer, "Combine Meshes");
                        renderer.enabled = false;
                        if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                    }
                }
                Undo.CollapseUndoOperations(group);
                committed = true;
            }
            finally
            {
                PluginProgressDialog.end();
                if (!committed)
                {
                    // Restore anything changed if an exception occurred during the final Undo commit.
                    foreach (var pair in previousRendererStates)
                    {
                        if (pair.Key == null) continue;
                        pair.Key.enabled = pair.Value;
                        if (PrefabUtility.IsPartOfPrefabInstance(pair.Key))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);
                    }
                    foreach (var obj in createdObjects)
                        if (obj != null) UnityEngine.Object.DestroyImmediate(obj);
                    foreach (string path in createdAssetPaths)
                        if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                    foreach (var mesh in createdMeshes)
                        if (mesh != null && !AssetDatabase.Contains(mesh)) UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
        }

        private static int getMaxNumberOfMeshVerts()
        {
            if (_settings.combinedIndexFormat == MeshCombineIndexFormat.UInt16)
                return _settings.generateLightmapUVs ? 32000 : 65000;
            return _settings.generateLightmapUVs ? 4000000 : 8000000;
        }

        private static void combine(MaterialInstance materialInstance, GameObject destinationParent,
            List<GameObject> createdObjects, List<Mesh> createdMeshes)
        {
            var meshInstances = materialInstance.meshInstances;
            if (meshInstances.Count == 0) return;

            int maxVertices = getMaxNumberOfMeshVerts();
            var meshData = new CombinedMeshData();
            Matrix4x4 destinationWorldToLocal = destinationParent.transform.worldToLocalMatrix;

            PluginProgressDialog.begin("Combining Meshes for Material: " + materialInstance.material.name);
            try
            {
                for (int instanceIndex = 0; instanceIndex < meshInstances.Count; ++instanceIndex)
                {
                    MeshInstance instance = meshInstances[instanceIndex];
                    Mesh mesh = instance.mesh;
                    PluginProgressDialog.updateProgress("Mesh: " + mesh.name, (instanceIndex + 1) / (float)meshInstances.Count);

                    Matrix4x4 matrix = destinationWorldToLocal * instance.transform.localToWorldMatrix;
                    if (!hasUsableTransform(matrix))
                        throw new InvalidOperationException("A mesh source or its destination has a non-invertible transform: " + instance.transform.name);
                    Matrix4x4 inverseTranspose = matrix.inverse.transpose;
                    Vector3 column0 = matrix.MultiplyVector(Vector3.right);
                    Vector3 column1 = matrix.MultiplyVector(Vector3.up);
                    Vector3 column2 = matrix.MultiplyVector(Vector3.forward);
                    bool flipWinding = Vector3.Dot(Vector3.Cross(column0, column1), column2) < 0.0f;

                    int[] triangles = mesh.GetTriangles(instance.subMeshIndex);
                    Vector3[] positions = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    Vector4[] tangents = mesh.tangents;
                    Color[] colors = mesh.colors;
                    var uv1List = new List<Vector4>();
                    var uv2List = new List<Vector4>();
                    var uv3List = new List<Vector4>();
                    var uv4List = new List<Vector4>();
                    var uv5List = new List<Vector4>();
                    var uv6List = new List<Vector4>();
                    var uv7List = new List<Vector4>();
                    var uv8List = new List<Vector4>();
                    mesh.GetUVs(0, uv1List);
                    mesh.GetUVs(1, uv2List);
                    mesh.GetUVs(2, uv3List);
                    mesh.GetUVs(3, uv4List);
                    mesh.GetUVs(4, uv5List);
                    mesh.GetUVs(5, uv6List);
                    mesh.GetUVs(6, uv7List);
                    mesh.GetUVs(7, uv8List);
                    Vector4[] uv1 = uv1List.ToArray();
                    Vector4[] uv2 = uv2List.ToArray();
                    Vector4[] uv3 = uv3List.ToArray();
                    Vector4[] uv4 = uv4List.ToArray();
                    Vector4[] uv5 = uv5List.ToArray();
                    Vector4[] uv6 = uv6List.ToArray();
                    Vector4[] uv7 = uv7List.ToArray();
                    Vector4[] uv8 = uv8List.ToArray();
                    bool hasNormals = normals.Length == positions.Length;
                    bool hasTangents = tangents.Length == positions.Length;
                    bool hasColors = colors.Length == positions.Length;
                    bool hasUV1 = uv1.Length == positions.Length;
                    bool hasUV2 = uv2.Length == positions.Length && !_settings.generateLightmapUVs;
                    bool hasUV3 = uv3.Length == positions.Length;
                    bool hasUV4 = uv4.Length == positions.Length;
                    bool hasUV5 = uv5.Length == positions.Length;
                    bool hasUV6 = uv6.Length == positions.Length;
                    bool hasUV7 = uv7.Length == positions.Length;
                    bool hasUV8 = uv8.Length == positions.Length;

                    // A source vertex may be shared by many triangles. Keep one output vertex
                    // per source index, per source instance and output chunk. For meshes without
                    // normals we preserve the previous per-face flat-shading fallback instead.
                    var remap = new Dictionary<int, int>();
                    for (int tri = 0; tri + 2 < triangles.Length; tri += 3)
                    {
                        int i0 = triangles[tri];
                        int i1 = triangles[tri + 1];
                        int i2 = triangles[tri + 2];
                        int newVertices = countNewTriangleVertices(remap, hasNormals, i0, i1, i2);
                        if (meshData.positions.Count + newVertices > maxVertices)
                        {
                            createCombinedMeshObject(meshData, materialInstance, destinationParent, createdObjects, createdMeshes);
                            meshData.reset();
                            remap.Clear();
                        }

                        Vector3 p0 = matrix.MultiplyPoint3x4(positions[i0]);
                        Vector3 p1 = matrix.MultiplyPoint3x4(positions[i1]);
                        Vector3 p2 = matrix.MultiplyPoint3x4(positions[i2]);
                        Vector3 faceNormal = Vector3.Cross(p1 - p0, p2 - p0);
                        if (flipWinding) faceNormal = -faceNormal;
                        if (faceNormal.sqrMagnitude < 1e-12f) faceNormal = Vector3.up;
                        else faceNormal.Normalize();

                        int v0 = appendIndexedVertex(meshData, remap, hasNormals, i0, p0, faceNormal, inverseTranspose, matrix, flipWinding,
                            normals, hasNormals, tangents, hasTangents, colors, hasColors,
                            uv1, hasUV1, uv2, hasUV2, uv3, hasUV3, uv4, hasUV4,
                            uv5, hasUV5, uv6, hasUV6, uv7, hasUV7, uv8, hasUV8);
                        int v1 = appendIndexedVertex(meshData, remap, hasNormals, i1, p1, faceNormal, inverseTranspose, matrix, flipWinding,
                            normals, hasNormals, tangents, hasTangents, colors, hasColors,
                            uv1, hasUV1, uv2, hasUV2, uv3, hasUV3, uv4, hasUV4,
                            uv5, hasUV5, uv6, hasUV6, uv7, hasUV7, uv8, hasUV8);
                        int v2 = appendIndexedVertex(meshData, remap, hasNormals, i2, p2, faceNormal, inverseTranspose, matrix, flipWinding,
                            normals, hasNormals, tangents, hasTangents, colors, hasColors,
                            uv1, hasUV1, uv2, hasUV2, uv3, hasUV3, uv4, hasUV4,
                            uv5, hasUV5, uv6, hasUV6, uv7, hasUV7, uv8, hasUV8);

                        if (flipWinding)
                        {
                            meshData.indices.Add(v2);
                            meshData.indices.Add(v1);
                            meshData.indices.Add(v0);
                        }
                        else
                        {
                            meshData.indices.Add(v0);
                            meshData.indices.Add(v1);
                            meshData.indices.Add(v2);
                        }
                    }
                }
                // A chunk can end on the vertex limit; don't generate an empty trailing mesh.
                if (meshData.positions.Count != 0)
                    createCombinedMeshObject(meshData, materialInstance, destinationParent, createdObjects, createdMeshes);
            }
            finally { PluginProgressDialog.end(); }
        }

        // Shared by geometry output and preview estimation to prevent inconsistent
        // vertex-limit decisions near a chunk boundary.
        private static int countNewTriangleVertices<T>(IDictionary<int, T> remap, bool canReuse,
            int i0, int i1, int i2)
        {
            if (!canReuse) return 3;
            return (remap.ContainsKey(i0) ? 0 : 1) +
                   (i1 == i0 || remap.ContainsKey(i1) ? 0 : 1) +
                   (i2 == i0 || i2 == i1 || remap.ContainsKey(i2) ? 0 : 1);
        }

        private static int appendIndexedVertex(CombinedMeshData data, Dictionary<int, int> remap, bool canReuse,
            int index, Vector3 position, Vector3 faceNormal, Matrix4x4 inverseTranspose, Matrix4x4 matrix, bool flipped,
            Vector3[] normals, bool hasNormals, Vector4[] tangents, bool hasTangents,
            Color[] colors, bool hasColors, Vector4[] uv1, bool hasUV1,
            Vector4[] uv2, bool hasUV2, Vector4[] uv3, bool hasUV3, Vector4[] uv4, bool hasUV4,
            Vector4[] uv5, bool hasUV5, Vector4[] uv6, bool hasUV6, Vector4[] uv7, bool hasUV7, Vector4[] uv8, bool hasUV8)
        {
            int outputIndex;
            if (canReuse && remap.TryGetValue(index, out outputIndex)) return outputIndex;
            outputIndex = data.positions.Count;
            appendVertex(data, index, position, faceNormal, inverseTranspose, matrix, flipped,
                normals, hasNormals, tangents, hasTangents, colors, hasColors,
                uv1, hasUV1, uv2, hasUV2, uv3, hasUV3, uv4, hasUV4,
                uv5, hasUV5, uv6, hasUV6, uv7, hasUV7, uv8, hasUV8);
            if (canReuse) remap.Add(index, outputIndex);
            return outputIndex;
        }

        private static void appendVertex(CombinedMeshData data, int index, Vector3 position, Vector3 faceNormal,
            Matrix4x4 inverseTranspose, Matrix4x4 matrix, bool flipped,
            Vector3[] normals, bool hasNormals, Vector4[] tangents, bool hasTangents,
            Color[] colors, bool hasColors, Vector4[] uv1, bool hasUV1,
            Vector4[] uv2, bool hasUV2, Vector4[] uv3, bool hasUV3, Vector4[] uv4, bool hasUV4,
            Vector4[] uv5, bool hasUV5, Vector4[] uv6, bool hasUV6, Vector4[] uv7, bool hasUV7, Vector4[] uv8, bool hasUV8)
        {
            Vector3 normal = faceNormal;
            if (hasNormals)
            {
                normal = inverseTranspose.MultiplyVector(normals[index]);
                if (normal.sqrMagnitude > 1e-12f) normal.Normalize();
                else normal = faceNormal;
            }

            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-8f) tangent = Vector3.Cross(normal, Vector3.right);
            if (hasTangents)
            {
                Vector4 t = tangents[index];
                tangent = matrix.MultiplyVector(new Vector3(t.x, t.y, t.z));
                tangent -= normal * Vector3.Dot(tangent, normal);
            }
            if (tangent.sqrMagnitude < 1e-12f) tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude < 1e-12f) tangent = Vector3.right;
            tangent.Normalize();
            float handedness = hasTangents ? tangents[index].w : 1.0f;
            if (flipped) handedness = -handedness;

            data.positions.Add(position);
            data.normals.Add(normal);
            data.tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, handedness));
            data.colors.Add(hasColors ? colors[index] : Color.white);
            data.uv1.Add(hasUV1 ? uv1[index] : Vector4.zero);
            data.uv2.Add(hasUV2 ? uv2[index] : Vector4.zero);
            data.uv3.Add(hasUV3 ? uv3[index] : Vector4.zero);
            data.uv4.Add(hasUV4 ? uv4[index] : Vector4.zero);
            // Higher UV channels are uncommon; allocate them only when a source uses them.
            // Backfill earlier vertices when a UV channel first appears midway through a chunk.
            appendOptionalUV(data.uv5, data.hasUV5, hasUV5, uv5, index, data.positions.Count);
            appendOptionalUV(data.uv6, data.hasUV6, hasUV6, uv6, index, data.positions.Count);
            appendOptionalUV(data.uv7, data.hasUV7, hasUV7, uv7, index, data.positions.Count);
            appendOptionalUV(data.uv8, data.hasUV8, hasUV8, uv8, index, data.positions.Count);

            data.hasTangents |= hasTangents;
            data.hasColors |= hasColors;
            data.hasUV1 |= hasUV1;
            data.hasUV2 |= hasUV2;
            data.hasUV3 |= hasUV3;
            data.hasUV4 |= hasUV4;
            data.hasUV5 |= hasUV5;
            data.hasUV6 |= hasUV6;
            data.hasUV7 |= hasUV7;
            data.hasUV8 |= hasUV8;
        }

        private static void appendOptionalUV(List<Vector4> output, bool wasPresent, bool sourceHasUV,
            Vector4[] sourceUV, int sourceIndex, int currentVertexCount)
        {
            if (!wasPresent && !sourceHasUV) return;
            while (output.Count < currentVertexCount - 1) output.Add(Vector4.zero);
            output.Add(sourceHasUV ? sourceUV[sourceIndex] : Vector4.zero);
        }

        private static Vector3 calculatePivot(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extent = bounds.extents;
            switch (_settings.combinedMeshPivot)
            {
                case MeshCombinePivot.TopCenter:     return center + Vector3.up * extent.y;
                case MeshCombinePivot.BottomCenter:  return center - Vector3.up * extent.y;
                case MeshCombinePivot.LeftCenter:    return center - Vector3.right * extent.x;
                case MeshCombinePivot.RightCenter:   return center + Vector3.right * extent.x;
                case MeshCombinePivot.FrontCenter:   return center + Vector3.forward * extent.z;
                case MeshCombinePivot.BackCenter:    return center - Vector3.forward * extent.z;
                default:                             return center;
            }
        }

        private static void copyRendererSettings(MeshRenderer source, MeshRenderer destination)
        {
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.staticShadowCaster = source.staticShadowCaster;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.lightmapIndex = source.lightmapIndex;
            destination.lightmapScaleOffset = source.lightmapScaleOffset;
            destination.realtimeLightmapIndex = source.realtimeLightmapIndex;
            destination.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
            destination.probeAnchor = source.probeAnchor;
            destination.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
            destination.rendererPriority = source.rendererPriority;
            destination.rayTracingMode = source.rayTracingMode;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.renderingLayerMask = source.renderingLayerMask;
            destination.receiveGI = source.receiveGI;
            destination.scaleInLightmap = source.scaleInLightmap;
            destination.stitchLightmapSeams = source.stitchLightmapSeams;
        }

        private static void createCombinedMeshObject(CombinedMeshData data, MaterialInstance materialInstance,
            GameObject destinationParent, List<GameObject> createdObjects, List<Mesh> createdMeshes)
        {
            if (data.positions.Count == 0) return;
            Mesh combinedMesh = new Mesh();
            // Track both allocations immediately so failures during mesh construction can be rolled back.
            createdMeshes.Add(combinedMesh);
            string materialName = materialInstance.material.name;
            combinedMesh.name = safeAssetName(_settings.combinedMeshBaseName + materialName + "_" + (createdMeshes.Count - 1));
            combinedMesh.indexFormat = _settings.combinedIndexFormat == MeshCombineIndexFormat.UInt32 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            combinedMesh.SetVertices(data.positions);
            combinedMesh.SetNormals(data.normals);
            if (data.hasTangents) combinedMesh.SetTangents(data.tangents);
            if (data.hasColors) combinedMesh.SetColors(data.colors);
            if (data.hasUV1) combinedMesh.SetUVs(0, data.uv1);
            if (data.hasUV2) combinedMesh.SetUVs(1, data.uv2);
            if (data.hasUV3) combinedMesh.SetUVs(2, data.uv3);
            if (data.hasUV4) combinedMesh.SetUVs(3, data.uv4);
            if (data.hasUV5) combinedMesh.SetUVs(4, data.uv5);
            if (data.hasUV6) combinedMesh.SetUVs(5, data.uv6);
            if (data.hasUV7) combinedMesh.SetUVs(6, data.uv7);
            if (data.hasUV8) combinedMesh.SetUVs(7, data.uv8);
            combinedMesh.SetTriangles(data.indices, 0);
            combinedMesh.RecalculateBounds();

            // Vertices are already in the destination parent's local space. Pivot adjustment
            // must therefore also happen there, *before* UploadMeshData(true).
            Vector3 localPivot = calculatePivot(combinedMesh.bounds);
            var adjustedPositions = new List<Vector3>(data.positions.Count);
            for (int i = 0; i < data.positions.Count; ++i) adjustedPositions.Add(data.positions[i] - localPivot);
            combinedMesh.SetVertices(adjustedPositions);
            combinedMesh.RecalculateBounds();

            if (_settings.generateLightmapUVs) Unwrapping.GenerateSecondaryUVSet(combinedMesh);

            string baseName = _settings.combinedMeshObjectBaseName ?? string.Empty;
            GameObject combinedObject = new GameObject(baseName + materialName);
            createdObjects.Add(combinedObject);
            combinedObject.transform.SetParent(destinationParent.transform, false);
            combinedObject.transform.localPosition = localPivot;
            combinedObject.transform.localRotation = Quaternion.identity;
            combinedObject.transform.localScale = Vector3.one;
            combinedObject.isStatic = _settings.combineAsStatic;
            // Camera culling masks and layer-specific lights depend on the GameObject layer.
            combinedObject.layer = materialInstance.referenceRenderer.gameObject.layer;

            MeshFilter filter = combinedObject.AddComponent<MeshFilter>();
            filter.sharedMesh = combinedMesh;
            MeshRenderer renderer = combinedObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materialInstance.material;
            copyRendererSettings(materialInstance.referenceRenderer, renderer);
            if (_settings.generateLightmapUVs)
            {
                // The new UV2 atlas requires a fresh bake; don't reuse a source lightmap mapping.
                renderer.lightmapIndex = -1;
                renderer.lightmapScaleOffset = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
                renderer.realtimeLightmapIndex = -1;
            }

            combinedMesh.UploadMeshData(!_settings.combinedMeshesAreReadable);
        }
    }
}
#endif
