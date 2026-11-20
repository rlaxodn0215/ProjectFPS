#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.Collections;
using System.Collections.Generic;

namespace GSPAWN
{
    #region Public Enumerations
    //-----------------------------------------------------------------------------
    // Name: SelectionBridgePairAxis (Public Enum)
    // Desc: Identifies the GSpawn grid axes processed when pairing Bridge endpoints.
    //-----------------------------------------------------------------------------
    public enum SelectionBridgePairAxis
    {
        Both = 0,          // Process both grid-plane axes in one Bridge command
        GridRight,         // Pair anchors along the GSpawn grid right axis
        GridForward        // Pair anchors along the GSpawn grid forward axis
    }

    //-----------------------------------------------------------------------------
    // Name: SelectionBridgePrefabAxis (Public Enum)
    // Desc: Identifies the prefab-local bounds axis used as the Bridge repetition axis.
    //-----------------------------------------------------------------------------
    public enum SelectionBridgePrefabAxis
    {
        Automatic = 0,     // Use the longest prefab-local axis perpendicular to Prefab Up
        Longest,           // Use the longest prefab-local axis perpendicular to Prefab Up
        Shortest,          // Use the shortest prefab-local axis perpendicular to Prefab Up
        X,
        Y,
        Z
    }

    //-----------------------------------------------------------------------------
    // Name: SelectionBridgeVerticalAlignment (Public Enum)
    // Desc: Identifies how generated Bridge prefab bounds are aligned vertically to
    //       the selected anchor pair.
    //-----------------------------------------------------------------------------
    public enum SelectionBridgeVerticalAlignment
    {
        Bottom = 0,        // Align generated prefab bottoms with the selected anchor bottoms
        Center,            // Align generated prefab centers with the selected anchor centers
        Top                // Align generated prefab tops with the selected anchor tops
    }

    //-----------------------------------------------------------------------------
    // Name: SelectionBridgePrefabUpAxis (Public Enum)
    // Desc: Identifies the prefab-local axis aligned with GSpawn grid up during Bridge
    //       placement.
    //-----------------------------------------------------------------------------
    public enum SelectionBridgePrefabUpAxis
    {
        Automatic = 0,     // Prefer prefab-local Y unless it is also the repetition axis
        X,
        Y,
        Z
    }
    #endregion

    #region Public Classes
    //-----------------------------------------------------------------------------
    // Name: SelectionBridge (Public Class)
    // Desc: Detects selected-anchor arm endpoints along the GSpawn grid axes and fills
    //       compatible endpoint gaps with instances randomly chosen from a prefab pool.
    //-----------------------------------------------------------------------------
    public class SelectionBridge
    {
        #region Private Structures

        //-----------------------------------------------------------------------------
        struct BridgeAnchor
        {
            #region Public Fields
            public GameObject  gameObject;      // Selected hierarchy root
            public OBB        obb;             // Anchor hierarchy world OBB
            public float       right;           // OBB center coordinate along grid right
            public float       up;              // OBB center coordinate along grid up
            public float       forward;         // OBB center coordinate along grid forward
            public int         selectionIndex;  // Tie-breaker within the selected objects
            #endregion
        }


        //-----------------------------------------------------------------------------
        struct BridgeEndpoint
        {
            #region Public Fields
            public int      anchorIndex;        // Parent anchor index
            public bool     positive;           // True for the positive axis terminal
            public float    axis;               // Terminal coordinate along the bridge axis
            public float    perpendicular;      // Occupied terminal-slice center
            public float    up;                 // Prefab-height-adjusted terminal center
            public float    bottomUp;           // Geometry-derived terminal bottom along grid up
            public float    topUp;              // Geometry-derived terminal top along grid up
            public float    turnSide;           // Signed offset from terminal center toward anchor body
            #endregion
        }


        //-----------------------------------------------------------------------------
        struct BridgePairCandidate
        {
            #region Public Fields
            public int      endpointIndex0;     // Positive endpoint index
            public int      endpointIndex1;     // Negative endpoint index
            public float    distance;           // Free endpoint distance along the axis
            #endregion
        }


        //-----------------------------------------------------------------------------
        struct BridgePrefab
        {
            #region Public Fields
            public PluginPrefab       prefab;               // Source prefab entry
            public Vector3        scale;                // Prefab asset root scale
            public Quaternion     rotation;             // Positive-axis Bridge root rotation
            public Quaternion     oppositeRotation;     // Negative-axis Bridge root rotation
            public Vector3        rootToCenter;         // Positive-axis root-to-model-center offset
            public Vector3        oppositeRootToCenter; // Negative-axis root-to-model-center offset
            public float          length;               // Projected OBB size along the bridge direction
            public float          perpendicularSize;    // Projected in-plane cross-section size
            public float          upSize;               // Projected size along grid up
            #endregion
        }
        #endregion

        #region Private Classes

        //-----------------------------------------------------------------------------
        class BridgeAnchorComparer : IComparer<BridgeAnchor>
        {
            #region Public Functions
            //-----------------------------------------------------------------------------
            // Name: Compare() (Public Function)
            // Desc: Compares two Bridge anchors.
            // Parm: lhs - First anchor.
            //       rhs - Second anchor.
            // Rtrn: Standard comparison result.
            //-----------------------------------------------------------------------------
            public int Compare(BridgeAnchor lhs, BridgeAnchor rhs)
            {
                int result = lhs.right.CompareTo(rhs.right);
                if (result != 0) return result;

                result = lhs.forward.CompareTo(rhs.forward);
                if (result != 0) return result;

                result = lhs.up.CompareTo(rhs.up);
                if (result != 0) return result;

                return lhs.selectionIndex.CompareTo(rhs.selectionIndex);
            }
            #endregion
        }


        //-----------------------------------------------------------------------------
        class BridgePairCandidateComparer : IComparer<BridgePairCandidate>
        {
            #region Public Functions
            //-----------------------------------------------------------------------------
            // Name: Compare() (Public Function)
            // Desc: Compares two Bridge pair candidates.
            // Parm: lhs - First candidate.
            //       rhs - Second candidate.
            // Rtrn: Standard comparison result.
            //-----------------------------------------------------------------------------
            public int Compare(BridgePairCandidate lhs, BridgePairCandidate rhs)
            {
                int result = lhs.distance.CompareTo(rhs.distance);
                if (result != 0) return result;

                result = lhs.endpointIndex0.CompareTo(rhs.endpointIndex0);
                if (result != 0) return result;

                return lhs.endpointIndex1.CompareTo(rhs.endpointIndex1);
            }
            #endregion
        }
        #endregion

        #region Private Static Readonly Fields
        static readonly float sMinAxisLength       = 0.001f;    // Smallest useful gap/prefab length
        static readonly int   sMaxPiecesPerBridge  = 10000;     // Defensive per-pair instance limit
        static readonly int   sMinSliceVertexCount = 3;         // Smallest useful terminal geometry slice
        #endregion

        #region Private Fields
        ObjectBounds.QueryConfig                mBoundsQueryConfig       = ObjectBounds.QueryConfig.defaultConfig; // Shared hierarchy-bounds query
        List<BridgeAnchor>               mAnchorBuffer            = new List<BridgeAnchor>(32);                           // Valid selected anchors
        List<BridgeEndpoint>             mEndpointBuffer          = new List<BridgeEndpoint>(64);                           // Geometry-derived arm endpoints
        List<BridgePairCandidate>        mPairCandidateBuffer     = new List<BridgePairCandidate>(64);                           // Compatible endpoint pairs
        List<BridgePrefab>               mPrefabBuffer            = new List<BridgePrefab>(32);                           // Valid Prefab Library variations
        List<BridgePrefab>               mFittingPrefabBuffer     = new List<BridgePrefab>(32);                           // Variations which fit the current gap
        List<BridgePrefab>               mPieceBuffer             = new List<BridgePrefab>(256);                          // Random variation sequence for one pair
        List<GameObject>                 mHierarchyObjectBuffer   = new List<GameObject>(128);                          // Current anchor hierarchy objects
        List<Vector3>                    mVertexBuffer            = new List<Vector3>(4096);                         // Current anchor world-space vertices
        HashSet<int>                     mPairedEndpointSet       = new HashSet<int>();                           // Endpoints consumed by accepted pairs
        HashSet<PluginPrefab>                mPrefabSet               = new HashSet<PluginPrefab>();                           // Prefab variation deduplication set
        List<GameObject>                 mSpawnedObjectBuffer     = new List<GameObject>(256);                          // Complete Bridge spawn batch
        BridgeAnchorComparer             mAnchorComparer          = new BridgeAnchorComparer();        // Reused deterministic anchor comparer
        BridgePairCandidateComparer      mPairCandidateComparer   = new BridgePairCandidateComparer(); // Reused nearest-pair comparer
        #endregion

        #region Public Functions

        //-----------------------------------------------------------------------------
        public int Execute(IReadOnlyList<GameObject> selectedObjects, IReadOnlyList<PluginPrefab> prefabs,
            SelectionBridgePairAxis pairAxis, SelectionBridgePrefabAxis prefabAxis,
            SelectionBridgePrefabUpAxis prefabUpAxis, SelectionBridgeVerticalAlignment verticalAlignment,
            bool reversePrefabAxis, bool invertPrefabUpAxis, float alignmentTolerance)
        {
            // Validate the operation before clearing/creating any scene content
            if (selectedObjects == null || selectedObjects.Count < 2 || prefabs == null || prefabs.Count == 0)
                return 0;

            // Cache the normalized GSpawn grid basis used by both pairing and placement
            PluginGrid grid        = PluginScene.instance.grid;
            Vector3 gridRight      = grid.right.normalized;
            Vector3 gridUp         = grid.up.normalized;
            Vector3 gridForward    = grid.look.normalized;
            if (gridRight.sqrMagnitude < 0.5f || gridUp.sqrMagnitude < 0.5f || gridForward.sqrMagnitude < 0.5f)
                return 0;

            // Collect and deterministically order valid hierarchy-bounds anchors
            CollectAnchors(selectedObjects, gridRight, gridUp, gridForward);
            if (mAnchorBuffer.Count < 2)
                return 0;

            mAnchorBuffer.Sort(mAnchorComparer);

            // Process the configured grid axes independently. Endpoint consumption is
            // scoped per axis, allowing one corner anchor to contribute both of its arms.
            float resolvedTolerance = Mathf.Max(sMinAxisLength, alignmentTolerance);
            mSpawnedObjectBuffer.Clear();
            // Isolate this invocation so one Ctrl+Z removes the entire Bridge batch.
            int undoGroup = -1;
            if (UndoEx.enabled)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("GSpawn Bridge");
            }
            if (pairAxis == SelectionBridgePairAxis.Both || pairAxis == SelectionBridgePairAxis.GridRight)
                ExecuteAxis(true, prefabs, prefabAxis, prefabUpAxis, verticalAlignment,
                    reversePrefabAxis, invertPrefabUpAxis, resolvedTolerance, gridRight, gridForward, gridUp);
            if (pairAxis == SelectionBridgePairAxis.Both || pairAxis == SelectionBridgePairAxis.GridForward)
                ExecuteAxis(false, prefabs, prefabAxis, prefabUpAxis, verticalAlignment,
                    reversePrefabAxis, invertPrefabUpAxis, resolvedTolerance, gridForward, gridRight, gridUp);

            // The prefab API already registers Undo and routes each instance to its
            // configured object group. Register their final transforms with the scene tree.
            int totalSpawnedCount = mSpawnedObjectBuffer.Count;
            if (totalSpawnedCount != 0)
            {
                ObjectEvents.onObjectsSpawned(mSpawnedObjectBuffer);
                SceneView.RepaintAll();
            }
            if (UndoEx.enabled) Undo.CollapseUndoOperations(undoGroup);

            mPairedEndpointSet.Clear();
            mEndpointBuffer.Clear();
            mPairCandidateBuffer.Clear();
            mPrefabBuffer.Clear();
            mFittingPrefabBuffer.Clear();
            mPieceBuffer.Clear();
            mHierarchyObjectBuffer.Clear();
            mVertexBuffer.Clear();
            mSpawnedObjectBuffer.Clear();
            return totalSpawnedCount;
        }

        #endregion

        #region Private Functions

        //-----------------------------------------------------------------------------
        int ExecuteAxis(bool pairAlongRight, IReadOnlyList<PluginPrefab> prefabs,
            SelectionBridgePrefabAxis prefabAxis, SelectionBridgePrefabUpAxis prefabUpAxis,
            SelectionBridgeVerticalAlignment verticalAlignment, bool reversePrefabAxis,
            bool invertPrefabUpAxis, float alignmentTolerance, Vector3 bridgeDirection,
            Vector3 perpendicularAxis, Vector3 gridUp)
        {
            // Resolve every selected prefab independently for this bridge direction.
            // Bounds, scale, automatic axis and center compensation can vary by entry.
            CollectPrefabs(prefabs, prefabAxis, prefabUpAxis, reversePrefabAxis,
                invertPrefabUpAxis, bridgeDirection, perpendicularAxis, gridUp);
            if (mPrefabBuffer.Count == 0)
                return 0;

            // Detect narrow occupied terminal slices instead of using hierarchy centers.
            // A broad terminal slice is a non-arm side and is deliberately excluded.
            CollectEndpoints(pairAlongRight, alignmentTolerance, bridgeDirection,
                perpendicularAxis, gridUp);
            if (mEndpointBuffer.Count < 2)
                return 0;

            BuildPairCandidates(alignmentTolerance);
            mPairCandidateBuffer.Sort(mPairCandidateComparer);
            mPairedEndpointSet.Clear();

            int firstSpawnedIndex = mSpawnedObjectBuffer.Count;
            int pairCandidateCount = mPairCandidateBuffer.Count;
            for (int i = 0; i < pairCandidateCount; ++i)
            {
                BridgePairCandidate pair = mPairCandidateBuffer[i];
                if (mPairedEndpointSet.Contains(pair.endpointIndex0) ||
                    mPairedEndpointSet.Contains(pair.endpointIndex1))
                    continue;

                // A failed prefab fit does not consume either endpoint, allowing the
                // next-nearest compatible opposing arm to be considered.
                if (SpawnBridgePair(pair, verticalAlignment, alignmentTolerance, bridgeDirection,
                    perpendicularAxis, gridUp) == 0)
                    continue;

                mPairedEndpointSet.Add(pair.endpointIndex0);
                mPairedEndpointSet.Add(pair.endpointIndex1);
            }

            mPairedEndpointSet.Clear();
            return mSpawnedObjectBuffer.Count - firstSpawnedIndex;
        }


        //-----------------------------------------------------------------------------
        void CollectPrefabs(IReadOnlyList<PluginPrefab> prefabs,
            SelectionBridgePrefabAxis prefabAxis, SelectionBridgePrefabUpAxis prefabUpAxis,
            bool reversePrefabAxis, bool invertPrefabUpAxis, Vector3 bridgeDirection,
            Vector3 perpendicularAxis, Vector3 gridUp)
        {
            mPrefabBuffer.Clear();
            mPrefabSet.Clear();

            int prefabCount = prefabs.Count;
            for (int i = 0; i < prefabCount; ++i)
            {
                PluginPrefab prefab = prefabs[i];
                if (prefab == null || prefab.prefabAsset == null || !mPrefabSet.Add(prefab))
                    continue;

                AABB prefabModelAABB = ObjectBounds.calcHierarchyModelAABB(prefab.prefabAsset, mBoundsQueryConfig);
                if (!prefabModelAABB.isValid)
                    continue;

                Vector3 prefabScale       = prefab.prefabAsset.transform.lossyScale;
                Vector3 scaledPrefabSize  = Vector3.Scale(prefabModelAABB.size, prefabScale.abs());

                // Resolve a complete prefab orientation frame. Automatic treats prefab-local
                // Y as up first, then chooses the longest remaining axis for repetition. This
                // prevents tall wall pieces from rotating their height into a horizontal gap.
                int prefabUpAxisIndex = ResolvePrefabUpAxisIndex(prefabUpAxis, scaledPrefabSize, -1);
                int prefabAxisIndex   = ResolvePrefabAxisIndex(prefabAxis, scaledPrefabSize, prefabUpAxisIndex);
                if (prefabUpAxisIndex == prefabAxisIndex)
                    prefabUpAxisIndex = ResolvePrefabUpAxisIndex(
                        SelectionBridgePrefabUpAxis.Automatic, scaledPrefabSize, prefabAxisIndex);

                Vector3 localBridgeAxis = GetLocalAxis(prefabAxisIndex);
                Vector3 localUpAxis     = GetLocalAxis(prefabUpAxisIndex);
                if (reversePrefabAxis)  localBridgeAxis = -localBridgeAxis;
                if (invertPrefabUpAxis) localUpAxis     = -localUpAxis;

                // Cache both signed Bridge orientations. Individual endpoint pairs choose
                // between them when their corner geometry exposes a clear continuation side.
                Quaternion finalRotation = BuildPrefabRotation(localBridgeAxis, localUpAxis,
                    bridgeDirection, gridUp);
                Quaternion oppositeRotation = BuildPrefabRotation(localBridgeAxis, localUpAxis,
                    -bridgeDirection, gridUp);
                OBB prefabOBB = new OBB(Vector3.zero, scaledPrefabSize, finalRotation);
                float pieceLength = CalculateSizeAlongAxis(prefabOBB, bridgeDirection);
                float perpendicularSize = CalculateSizeAlongAxis(prefabOBB, perpendicularAxis);
                float upSize            = CalculateSizeAlongAxis(prefabOBB, gridUp);
                if (pieceLength < sMinAxisLength || perpendicularSize < sMinAxisLength ||
                    upSize < sMinAxisLength)
                    continue;

                Vector3 scaledPrefabCenter = Vector3.Scale(prefabModelAABB.center, prefabScale);
                BridgePrefab bridgePrefab          = new BridgePrefab();
                bridgePrefab.prefab                = prefab;
                bridgePrefab.scale                 = prefabScale;
                bridgePrefab.rotation              = finalRotation;
                bridgePrefab.oppositeRotation      = oppositeRotation;
                bridgePrefab.rootToCenter          = finalRotation * scaledPrefabCenter;
                bridgePrefab.oppositeRootToCenter  = oppositeRotation * scaledPrefabCenter;
                bridgePrefab.length                = pieceLength;
                bridgePrefab.perpendicularSize     = perpendicularSize;
                bridgePrefab.upSize                = upSize;
                mPrefabBuffer.Add(bridgePrefab);
            }

            mPrefabSet.Clear();
        }


        //-----------------------------------------------------------------------------
        void CollectAnchors(IReadOnlyList<GameObject> selectedObjects, Vector3 gridRight,
            Vector3 gridUp, Vector3 gridForward)
        {
            mAnchorBuffer.Clear();

            int selectedCount = selectedObjects.Count;
            for (int i = 0; i < selectedCount; ++i)
            {
                GameObject gameObject = selectedObjects[i];
                if (gameObject == null || !gameObject.scene.IsValid())
                    continue;

                OBB obb = ObjectBounds.calcHierarchyWorldOBB(gameObject, mBoundsQueryConfig);
                if (!obb.isValid)
                    continue;

                BridgeAnchor anchor = new BridgeAnchor();
                anchor.gameObject  = gameObject;
                anchor.obb         = obb;
                anchor.right       = Vector3.Dot(obb.center, gridRight);
                anchor.up          = Vector3.Dot(obb.center, gridUp);
                anchor.forward     = Vector3.Dot(obb.center, gridForward);
                anchor.selectionIndex = i;
                mAnchorBuffer.Add(anchor);
            }
        }


        //-----------------------------------------------------------------------------
        void CollectEndpoints(bool pairAlongRight, float alignmentTolerance,
            Vector3 bridgeDirection, Vector3 perpendicularAxis, Vector3 gridUp)
        {
            mEndpointBuffer.Clear();

            int anchorCount = mAnchorBuffer.Count;
            for (int i = 0; i < anchorCount; ++i)
            {
                BridgeAnchor anchor = mAnchorBuffer[i];
                if (!CollectAnchorVertices(anchor.gameObject))
                {
                    AddOBBFallbackEndpoints(i, pairAlongRight, bridgeDirection, gridUp);
                    continue;
                }

                float minAxis = float.MaxValue;
                float maxAxis = float.MinValue;
                int vertexCount = mVertexBuffer.Count;
                for (int vertexIndex = 0; vertexIndex < vertexCount; ++vertexIndex)
                {
                    float coordinate = Vector3.Dot(mVertexBuffer[vertexIndex], bridgeDirection);
                    minAxis = Mathf.Min(minAxis, coordinate);
                    maxAxis = Mathf.Max(maxAxis, coordinate);
                }

                float axisSpan = maxAxis - minAxis;
                if (axisSpan < sMinAxisLength)
                    continue;

                float sliceDepth = CalculateTerminalSliceDepth(axisSpan, alignmentTolerance);
                TryAddGeometryEndpoint(i, false, minAxis, maxAxis, sliceDepth,
                    alignmentTolerance, bridgeDirection, perpendicularAxis, gridUp);
                TryAddGeometryEndpoint(i, true, minAxis, maxAxis, sliceDepth,
                    alignmentTolerance, bridgeDirection, perpendicularAxis, gridUp);
            }

            mHierarchyObjectBuffer.Clear();
            mVertexBuffer.Clear();
        }


        //-----------------------------------------------------------------------------
        bool CollectAnchorVertices(GameObject root)
        {
            mVertexBuffer.Clear();
            root.getAllChildrenAndSelf(false, false, mHierarchyObjectBuffer);

            for (int i = 0; i < mHierarchyObjectBuffer.Count; ++i)
            {
                GameObject hierarchyObject = mHierarchyObjectBuffer[i];
                Mesh mesh = hierarchyObject.getMesh();
                if (mesh == null) continue;

                // Use true geometry even when the imported Mesh has Read/Write disabled.
                // Mesh.vertices rejects those assets, but Unity's Editor-only MeshUtility
                // can read their vertex data without altering import settings.
                Transform meshTransform = hierarchyObject.transform;
                if (mesh.isReadable)
                {
                    Vector3[] vertices = mesh.vertices;
                    for (int v = 0; v < vertices.Length; ++v)
                        mVertexBuffer.Add(meshTransform.TransformPoint(vertices[v]));
                }
                else
                {
                    try
                    {
                        using (Mesh.MeshDataArray meshData = MeshUtility.AcquireReadOnlyMeshData(mesh))
                        {
                            int count = meshData[0].vertexCount;
                            if (count == 0) continue;

                            using (NativeArray<Vector3> vertices = new NativeArray<Vector3>(count, Allocator.Temp))
                            {
                                meshData[0].GetVertices(vertices);
                                for (int v = 0; v < count; ++v)
                                    mVertexBuffer.Add(meshTransform.TransformPoint(vertices[v]));
                            }
                        }
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogWarning("GSpawn Bridge: Could not read mesh vertices for '" +
                            hierarchyObject.name + "': " + exception.Message);
                    }
                }
            }

            // Meshes with no available vertex data use OBB endpoint fallback.
            return mVertexBuffer.Count >= sMinSliceVertexCount;
        }


        //-----------------------------------------------------------------------------
        bool TryAddGeometryEndpoint(int anchorIndex, bool positive, float minAxis,
            float maxAxis, float sliceDepth, float alignmentTolerance,
            Vector3 bridgeDirection, Vector3 perpendicularAxis, Vector3 gridUp)
        {
            float sliceBoundary = positive ? maxAxis - sliceDepth : minAxis + sliceDepth;
            float minPerpendicular = float.MaxValue;
            float maxPerpendicular = float.MinValue;
            float minUp = float.MaxValue;
            float maxUp = float.MinValue;
            int sliceVertexCount = 0;

            int vertexCount = mVertexBuffer.Count;
            for (int i = 0; i < vertexCount; ++i)
            {
                Vector3 vertex = mVertexBuffer[i];
                float axisCoordinate = Vector3.Dot(vertex, bridgeDirection);
                if ((positive && axisCoordinate < sliceBoundary) ||
                    (!positive && axisCoordinate > sliceBoundary))
                    continue;

                float perpendicular = Vector3.Dot(vertex, perpendicularAxis);
                minPerpendicular = Mathf.Min(minPerpendicular, perpendicular);
                maxPerpendicular = Mathf.Max(maxPerpendicular, perpendicular);
                float up = Vector3.Dot(vertex, gridUp);
                minUp = Mathf.Min(minUp, up);
                maxUp = Mathf.Max(maxUp, up);
                ++sliceVertexCount;
            }

            if (sliceVertexCount < sMinSliceVertexCount)
                return false;

            float perpendicularSize = maxPerpendicular - minPerpendicular;
            if (!TryFindTerminalPrefab(perpendicularSize, alignmentTolerance,
                out BridgePrefab terminalPrefab))
                return false;

            BridgeEndpoint endpoint = new BridgeEndpoint();
            endpoint.anchorIndex    = anchorIndex;
            endpoint.positive       = positive;
            endpoint.axis           = positive ? maxAxis : minAxis;
            endpoint.perpendicular  = (minPerpendicular + maxPerpendicular) * 0.5f;
            endpoint.up             = minUp + terminalPrefab.upSize * 0.5f;
            endpoint.bottomUp       = minUp;
            endpoint.topUp          = maxUp;

            // A corner arm normally terminates away from the hierarchy bounds center.
            // Cache that signed side so a matched pair can infer the common side toward
            // which both anchors continue/bend without inspecting prefab semantics.
            float anchorPerpendicular = Vector3.Dot(
                mAnchorBuffer[anchorIndex].obb.center, perpendicularAxis);
            endpoint.turnSide = anchorPerpendicular - endpoint.perpendicular;
            mEndpointBuffer.Add(endpoint);
            return true;
        }


        //-----------------------------------------------------------------------------
        bool TryFindTerminalPrefab(float perpendicularSize, float alignmentTolerance,
            out BridgePrefab terminalPrefab)
        {
            terminalPrefab = new BridgePrefab();
            float bestScore = float.MaxValue;

            int prefabCount = mPrefabBuffer.Count;
            for (int i = 0; i < prefabCount; ++i)
            {
                BridgePrefab bridgePrefab = mPrefabBuffer[i];
                float tolerance = Mathf.Max(alignmentTolerance, bridgePrefab.perpendicularSize * 0.05f);
                float minSize = Mathf.Max(sMinAxisLength,
                    bridgePrefab.perpendicularSize * 0.15f - tolerance);
                float maxSize = bridgePrefab.perpendicularSize * 2.75f + tolerance;
                if (perpendicularSize < minSize || perpendicularSize > maxSize)
                    continue;

                float score = Mathf.Abs(perpendicularSize - bridgePrefab.perpendicularSize) /
                    bridgePrefab.perpendicularSize;
                if (score < bestScore)
                {
                    bestScore = score;
                    terminalPrefab = bridgePrefab;
                }
            }

            return bestScore != float.MaxValue;
        }


        //-----------------------------------------------------------------------------
        void AddOBBFallbackEndpoints(int anchorIndex, bool pairAlongRight,
            Vector3 bridgeDirection, Vector3 gridUp)
        {
            BridgeAnchor anchor = mAnchorBuffer[anchorIndex];
            float centerAxis = pairAlongRight ? anchor.right : anchor.forward;
            float perpendicular = pairAlongRight ? anchor.forward : anchor.right;
            float axisExtent = CalculateExtentAlongAxis(anchor.obb, bridgeDirection);
            float upExtent = CalculateExtentAlongAxis(anchor.obb, gridUp);
            float up = anchor.up - upExtent + GetReferencePrefabUpSize() * 0.5f;

            BridgeEndpoint endpoint = new BridgeEndpoint();
            endpoint.anchorIndex   = anchorIndex;
            endpoint.positive      = false;
            endpoint.axis          = centerAxis - axisExtent;
            endpoint.perpendicular = perpendicular;
            endpoint.up            = up;
            endpoint.bottomUp      = anchor.up - upExtent;
            endpoint.topUp         = anchor.up + upExtent;
            endpoint.turnSide      = 0.0f;
            mEndpointBuffer.Add(endpoint);

            endpoint.positive = true;
            endpoint.axis     = centerAxis + axisExtent;
            mEndpointBuffer.Add(endpoint);
        }


        //-----------------------------------------------------------------------------
        float CalculateTerminalSliceDepth(float axisSpan, float alignmentTolerance)
        {
            float minPrefabLength = mPrefabBuffer[0].length;
            int prefabCount = mPrefabBuffer.Count;
            for (int i = 1; i < prefabCount; ++i)
                minPrefabLength = Mathf.Min(minPrefabLength, mPrefabBuffer[i].length);

            float desiredDepth = Mathf.Max(alignmentTolerance * 2.0f, minPrefabLength * 0.1f);
            return Mathf.Min(desiredDepth, Mathf.Max(sMinAxisLength, axisSpan * 0.25f));
        }


        //-----------------------------------------------------------------------------
        float GetReferencePrefabUpSize()
        {
            float sizeSum = 0.0f;
            int prefabCount = mPrefabBuffer.Count;
            for (int i = 0; i < prefabCount; ++i)
                sizeSum += mPrefabBuffer[i].upSize;
            return sizeSum / prefabCount;
        }


        //-----------------------------------------------------------------------------
        void BuildPairCandidates(float alignmentTolerance)
        {
            mPairCandidateBuffer.Clear();

            int endpointCount = mEndpointBuffer.Count;
            for (int i = 0; i < endpointCount; ++i)
            {
                BridgeEndpoint positiveEndpoint = mEndpointBuffer[i];
                if (!positiveEndpoint.positive)
                    continue;

                for (int j = 0; j < endpointCount; ++j)
                {
                    BridgeEndpoint negativeEndpoint = mEndpointBuffer[j];
                    if (negativeEndpoint.positive ||
                        negativeEndpoint.anchorIndex == positiveEndpoint.anchorIndex)
                        continue;

                    float distance = negativeEndpoint.axis - positiveEndpoint.axis;
                    if (distance < sMinAxisLength ||
                        Mathf.Abs(negativeEndpoint.perpendicular - positiveEndpoint.perpendicular) > alignmentTolerance ||
                        Mathf.Abs(negativeEndpoint.up - positiveEndpoint.up) > alignmentTolerance)
                        continue;

                    BridgePairCandidate pair = new BridgePairCandidate();
                    pair.endpointIndex0 = i;
                    pair.endpointIndex1 = j;
                    pair.distance       = distance;
                    mPairCandidateBuffer.Add(pair);
                }
            }
        }


        //-----------------------------------------------------------------------------
        int SpawnBridgePair(BridgePairCandidate pair,
            SelectionBridgeVerticalAlignment verticalAlignment, float alignmentTolerance,
            Vector3 bridgeDirection, Vector3 perpendicularAxis, Vector3 gridUp)
        {
            BridgeEndpoint startEndpoint = mEndpointBuffer[pair.endpointIndex0];
            BridgeEndpoint endEndpoint   = mEndpointBuffer[pair.endpointIndex1];
            float startCoordinate = startEndpoint.axis;
            float endCoordinate   = endEndpoint.axis;
            float gapLength       = endCoordinate - startCoordinate;

            // Resolve pair-specific facing only when both anchor terminals provide the
            // same clear continuation-side signal. Ambiguous pairs preserve the original
            // deterministic positive-axis orientation.
            Vector3 orientationDirection = ResolvePairOrientationDirection(startEndpoint,
                endEndpoint, alignmentTolerance, bridgeDirection, perpendicularAxis, gridUp);
            bool useOppositeOrientation = Vector3.Dot(orientationDirection, bridgeDirection) < 0.0f;

            // Build an independent random variation sequence for this pair. Prefabs too
            // large to fit this particular gap are excluded without discarding the rest
            // of the Prefab Library selection.
            if (!BuildPieceSequence(gapLength, out float pieceLengthSum))
                return 0;

            int pieceCount = mPieceBuffer.Count;

            // Average tolerated endpoint noise without making the generated row diagonal
            // to the active grid axes. Vertical placement is resolved separately from the
            // terminal slice used for pairing so upper arm geometry cannot make a full
            // bridge prefab float above the selected anchors.
            float        perpendicular   = (startEndpoint.perpendicular + endEndpoint.perpendicular) * 0.5f;
            // Align to the actual connecting arms, not the whole corner hierarchies.
            // Whole-corner bounds can contain taller/shorter perpendicular geometry.
            float        anchorBottomUp  = (startEndpoint.bottomUp + endEndpoint.bottomUp) * 0.5f;
            float        anchorTopUp     = (startEndpoint.topUp + endEndpoint.topUp) * 0.5f;
            float        anchorCenterUp  = (anchorBottomUp + anchorTopUp) * 0.5f;
            Vector3 startBoundary = bridgeDirection * startCoordinate +
                                    perpendicularAxis * perpendicular;
            Vector3 endBoundary   = bridgeDirection * endCoordinate +
                                    perpendicularAxis * perpendicular;

            // One piece is centered. Multi-piece rows keep both ends flush and distribute
            // the indivisible remainder uniformly as internal gap/overlap, even when the
            // randomly chosen prefab variations have different lengths.
            float pieceSpacing = pieceCount > 1 ?
                (gapLength - pieceLengthSum) / (pieceCount - 1) : 0.0f;
            float distanceFromStart = 0.0f;
            int firstSpawnedIndex = mSpawnedObjectBuffer.Count;
            for (int i = 0; i < pieceCount; ++i)
            {
                BridgePrefab bridgePrefab = mPieceBuffer[i];
                Vector3 center;
                if (pieceCount == 1)
                    center = (startBoundary + endBoundary) * 0.5f;
                else
                {
                    center = startBoundary + bridgeDirection *
                        (distanceFromStart + bridgePrefab.length * 0.5f);
                    distanceFromStart += bridgePrefab.length + pieceSpacing;
                }

                // Align each variation independently because selected prefab entries may
                // have different projected heights.
                float centerUp = CalculateBridgeCenterUp(verticalAlignment, bridgePrefab.upSize,
                    anchorBottomUp, anchorCenterUp, anchorTopUp);
                center += gridUp * centerUp;

                Quaternion prefabRotation = useOppositeOrientation ?
                    bridgePrefab.oppositeRotation : bridgePrefab.rotation;
                Vector3 rootToCenter = useOppositeOrientation ?
                    bridgePrefab.oppositeRootToCenter : bridgePrefab.rootToCenter;
                Vector3 rootPosition = center - rootToCenter;
                // Keep the prefab link, route through the configured object group,
                // and record final transform changes for reliable Undo/Redo.
                GameObject spawnedObject = bridgePrefab.prefab.spawn();
                if (spawnedObject == null) continue;
                UndoEx.recordTransform(spawnedObject.transform);
                spawnedObject.transform.position = rootPosition;
                spawnedObject.transform.rotation = prefabRotation;
                // Object groups may be scaled. Preserve the intended world-space
                // dimensions, matching GSpawn's other transform operations.
                spawnedObject.transform.setWorldScale(bridgePrefab.scale);
                if (!spawnedObject.activeSelf)
                {
                    UndoEx.recordGameObject(spawnedObject);
                    spawnedObject.SetActive(true);
                }
                mSpawnedObjectBuffer.Add(spawnedObject);
            }

            return mSpawnedObjectBuffer.Count - firstSpawnedIndex;
        }


        //-----------------------------------------------------------------------------
        Vector3 ResolvePairOrientationDirection(BridgeEndpoint startEndpoint,
            BridgeEndpoint endEndpoint, float alignmentTolerance, Vector3 bridgeDirection,
            Vector3 perpendicularAxis, Vector3 gridUp)
        {
            // Require two clear same-side hints. One-sided/contradictory geometry is not
            // enough evidence to change the historical deterministic orientation.
            float sideTolerance = Mathf.Max(sMinAxisLength, alignmentTolerance);
            float startSide     = startEndpoint.turnSide;
            float endSide       = endEndpoint.turnSide;
            if (Mathf.Abs(startSide) <= sideTolerance || Mathf.Abs(endSide) <= sideTolerance ||
                startSide * endSide <= 0.0f)
                return bridgeDirection;

            // Choose the travel sign whose right-handed Bridge frame points its lateral
            // side toward the common anchor-continuation side. 'Reverse prefab axis' can
            // still globally flip which authored prefab face occupies that side.
            Vector3 preferredSide = perpendicularAxis * Mathf.Sign(startSide + endSide);
            Vector3 positiveSide  = Vector3.Cross(gridUp, bridgeDirection);
            if (positiveSide.sqrMagnitude < 0.5f)
                return bridgeDirection;

            positiveSide.Normalize();
            return Vector3.Dot(positiveSide, preferredSide) >= 0.0f ?
                bridgeDirection : -bridgeDirection;
        }


        //-----------------------------------------------------------------------------
        float CalculateBridgeCenterUp(SelectionBridgeVerticalAlignment verticalAlignment,
            float prefabUpSize, float anchorBottomUp, float anchorCenterUp, float anchorTopUp)
        {
            if (verticalAlignment == SelectionBridgeVerticalAlignment.Center)
                return anchorCenterUp;
            if (verticalAlignment == SelectionBridgeVerticalAlignment.Top)
                return anchorTopUp - prefabUpSize * 0.5f;

            return anchorBottomUp + prefabUpSize * 0.5f;
        }


        //-----------------------------------------------------------------------------
        bool BuildPieceSequence(float gapLength, out float pieceLengthSum)
        {
            pieceLengthSum = 0.0f;
            mFittingPrefabBuffer.Clear();
            mPieceBuffer.Clear();
            if (gapLength < sMinAxisLength)
                return false;

            // A prefab must fit between the detected endpoints on its own. Allow only
            // the same tiny floating-point tolerance used by the original single-prefab
            // bridge implementation.
            float maxFittingLength = 0.0f;
            int prefabCount = mPrefabBuffer.Count;
            for (int i = 0; i < prefabCount; ++i)
            {
                BridgePrefab bridgePrefab = mPrefabBuffer[i];
                float fitTolerance = Mathf.Max(1e-4f, bridgePrefab.length * 1e-4f);
                if (bridgePrefab.length > gapLength + fitTolerance)
                    continue;

                mFittingPrefabBuffer.Add(bridgePrefab);
                maxFittingLength = Mathf.Max(maxFittingLength, bridgePrefab.length);
            }

            int fittingCount = mFittingPrefabBuffer.Count;
            if (fittingCount == 0 ||
                gapLength / maxFittingLength > sMaxPiecesPerBridge + 0.5f)
                return false;

            // Choose independently for every piece, matching multi-prefab replacement.
            // When the next piece crosses the far boundary, keep whichever total is
            // closest; ties keep the new piece to preserve round-half-up behavior.
            while (mPieceBuffer.Count < sMaxPiecesPerBridge)
            {
                BridgePrefab bridgePrefab = fittingCount == 1 ? mFittingPrefabBuffer[0] :
                    mFittingPrefabBuffer[UnityEngine.Random.Range(0, fittingCount)];
                float newLengthSum = pieceLengthSum + bridgePrefab.length;

                if (mPieceBuffer.Count != 0 && newLengthSum >= gapLength)
                {
                    float previousDifference = Mathf.Abs(gapLength - pieceLengthSum);
                    float newDifference      = Mathf.Abs(gapLength - newLengthSum);
                    if (newDifference <= previousDifference)
                    {
                        mPieceBuffer.Add(bridgePrefab);
                        pieceLengthSum = newLengthSum;
                    }
                    break;
                }

                mPieceBuffer.Add(bridgePrefab);
                pieceLengthSum = newLengthSum;
                if (pieceLengthSum >= gapLength)
                    break;
            }

            // A defensive-limit hit before reaching the far side is not a valid bridge.
            if (mPieceBuffer.Count == sMaxPiecesPerBridge && pieceLengthSum < gapLength)
            {
                mPieceBuffer.Clear();
                pieceLengthSum = 0.0f;
                return false;
            }

            return mPieceBuffer.Count != 0;
        }


        //-----------------------------------------------------------------------------
        int ResolvePrefabAxisIndex(SelectionBridgePrefabAxis prefabAxis, Vector3 scaledPrefabSize,
            int excludedAxisIndex)
        {
            if (prefabAxis == SelectionBridgePrefabAxis.X) return 0;
            if (prefabAxis == SelectionBridgePrefabAxis.Y) return 1;
            if (prefabAxis == SelectionBridgePrefabAxis.Z) return 2;

            bool useShortest = prefabAxis == SelectionBridgePrefabAxis.Shortest;
            int bestAxisIndex = -1;
            float bestSize = useShortest ? float.MaxValue : float.MinValue;
            for (int axisIndex = 0; axisIndex < 3; ++axisIndex)
            {
                if (axisIndex == excludedAxisIndex)
                    continue;

                float axisSize = GetAxisSize(scaledPrefabSize, axisIndex);
                if ((!useShortest && axisSize > bestSize) ||
                    (useShortest && axisSize < bestSize))
                {
                    bestSize = axisSize;
                    bestAxisIndex = axisIndex;
                }
            }

            return bestAxisIndex >= 0 ? bestAxisIndex : 0;
        }


        //-----------------------------------------------------------------------------
        int ResolvePrefabUpAxisIndex(SelectionBridgePrefabUpAxis prefabUpAxis,
            Vector3 scaledPrefabSize, int excludedAxisIndex)
        {
            if (prefabUpAxis == SelectionBridgePrefabUpAxis.X) return 0;
            if (prefabUpAxis == SelectionBridgePrefabUpAxis.Y) return 1;
            if (prefabUpAxis == SelectionBridgePrefabUpAxis.Z) return 2;

            if (excludedAxisIndex != 1)
                return 1;

            return scaledPrefabSize.x >= scaledPrefabSize.z ? 0 : 2;
        }


        //-----------------------------------------------------------------------------
        Quaternion BuildPrefabRotation(Vector3 localBridgeAxis, Vector3 localUpAxis,
            Vector3 bridgeDirection, Vector3 gridUp)
        {
            Quaternion localFrame = Quaternion.LookRotation(localBridgeAxis, localUpAxis);
            Quaternion worldFrame = Quaternion.LookRotation(bridgeDirection, gridUp);
            return worldFrame * Quaternion.Inverse(localFrame);
        }


        //-----------------------------------------------------------------------------
        float GetAxisSize(Vector3 size, int axisIndex)
        {
            if (axisIndex == 0) return size.x;
            if (axisIndex == 1) return size.y;
            return size.z;
        }


        //-----------------------------------------------------------------------------
        Vector3 GetLocalAxis(int axisIndex)
        {
            if (axisIndex == 0) return Vector3.right;
            if (axisIndex == 1) return Vector3.up;
            return Vector3.forward;
        }


        //-----------------------------------------------------------------------------
        float CalculateExtentAlongAxis(OBB box, Vector3 axis)
        {
            return Mathf.Abs(Vector3.Dot(box.right, axis)) * box.extents.x +
                   Mathf.Abs(Vector3.Dot(box.up, axis)) * box.extents.y +
                   Mathf.Abs(Vector3.Dot(box.look, axis)) * box.extents.z;
        }


        //-----------------------------------------------------------------------------
        float CalculateSizeAlongAxis(OBB box, Vector3 axis)
        {
            return CalculateExtentAlongAxis(box, axis) * 2.0f;
        }
        #endregion
    }
    #endregion
}
#endif
