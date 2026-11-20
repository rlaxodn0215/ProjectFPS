#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GSPAWN
{
    [ExecuteInEditMode]
    public class PhysicsSimulationObjectMono : MonoBehaviour
    {
        public bool                 is2DObject;
        public bool                 hasRigidBody;
        public bool                 isKinematic;

        public Rigidbody2D          rigidBody2D;
        public Rigidbody            rigidBody3D;
        public Collider2D           addedCollider2D;
        public Collider             addedCollider3D;

        [SerializeField] private RigidbodyType2D        _original2DBodyType;
        [SerializeField] private float                  _original2DGravityScale;
        [SerializeField] private bool                   _original2DSimulated;
        [SerializeField] private Vector2                _original2DVelocity;
        [SerializeField] private float                  _original2DAngularVelocity;
        [SerializeField] private bool                   _original3DUseGravity;
        [SerializeField] private bool                   _original3DDetectCollisions;
        [SerializeField] private Vector3                _original3DVelocity;
        [SerializeField] private Vector3                _original3DAngularVelocity;
        [SerializeField] private bool                   _hasEnteredSimulation;
        [SerializeField] private bool                   _cleanupFinished;
        [SerializeField] private bool                   _canRecordSettledTransformUndo;
        [SerializeField] private int                    _creationUndoGroup;
        [SerializeField] private Vector3                _spawnLocalPosition;
        [SerializeField] private Quaternion             _spawnLocalRotation;
        [SerializeField] private Vector3                _spawnLocalScale;

        [System.NonSerialized] private Collider[]        _simColliders3D;
        [System.NonSerialized] private ContactPoint2D[]  _contactPoints2D = new ContactPoint2D[32];
        [System.NonSerialized] private RaycastHit[]       _supportHits3D = new RaycastHit[32];

        public int creationUndoGroup { get { return _creationUndoGroup; } }
        public bool hasSettledTransformChange
        {
            get
            {
                return _canRecordSettledTransformUndo &&
                       (transform.localPosition != _spawnLocalPosition ||
                        transform.localRotation != _spawnLocalRotation ||
                        transform.localScale != _spawnLocalScale);
            }
        }

        // Record the final physics pose only once the asynchronous simulation has finished.
        // Creation Undo records the initial drop position, not the pose reached on later frames.
        public bool recordSettledTransformUndo()
        {
            if (!_canRecordSettledTransformUndo || transform == null) return false;

            Vector3 finalPosition       = transform.localPosition;
            Quaternion finalRotation    = transform.localRotation;
            Vector3 finalScale          = transform.localScale;
            if (finalPosition == _spawnLocalPosition && finalRotation == _spawnLocalRotation && finalScale == _spawnLocalScale)
                return false;

            try
            {
                // Undo.RecordObject needs the BEFORE state at the time of this call.
                // Physics changed the actual transform outside normal Undo-tracked editor actions.
                transform.localPosition = _spawnLocalPosition;
                transform.localRotation = _spawnLocalRotation;
                transform.localScale    = _spawnLocalScale;
                Undo.RecordObject(transform, "Physics Spawn - Settle");
            }
            finally
            {
                transform.localPosition = finalPosition;
                transform.localRotation = finalRotation;
                transform.localScale    = finalScale;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
            return true;
        }

        public void onExitSimulation()
        {
            if (_cleanupFinished) return;
            _cleanupFinished = true;

            // Older Undo histories can restore helpers which predate _hasEnteredSimulation.
            bool hasSavedState = _hasEnteredSimulation;
            _hasEnteredSimulation = false;

            if (is2DObject)
            {
                if (addedCollider2D != null) DestroyImmediate(addedCollider2D);
                if (rigidBody2D != null)
                {
                    if (!hasRigidBody) DestroyImmediate(rigidBody2D);
                    else if (hasSavedState)
                    {
                        // Restore motion before reverting to a kinematic/static or disabled body.
                        #if UNITY_6000_0_OR_NEWER
                        rigidBody2D.linearVelocity   = _original2DVelocity;
                        #else
                        rigidBody2D.velocity         = _original2DVelocity;
                        #endif
                        rigidBody2D.angularVelocity  = _original2DAngularVelocity;
                        rigidBody2D.gravityScale     = _original2DGravityScale;
                        rigidBody2D.bodyType         = _original2DBodyType;
                        rigidBody2D.simulated        = _original2DSimulated;
                    }
                }
            }
            else
            {
                if (addedCollider3D != null) DestroyImmediate(addedCollider3D);
                if (rigidBody3D != null)
                {
                    if (!hasRigidBody) DestroyImmediate(rigidBody3D);
                    else if (hasSavedState)
                    {
                        // Assign velocity while the body is still dynamic; kinematic bodies reject it.
                        #if UNITY_6000_0_OR_NEWER
                        rigidBody3D.linearVelocity    = _original3DVelocity;
                        #else
                        rigidBody3D.velocity          = _original3DVelocity;
                        #endif
                        rigidBody3D.angularVelocity   = _original3DAngularVelocity;
                        rigidBody3D.useGravity        = _original3DUseGravity;
                        rigidBody3D.detectCollisions  = _original3DDetectCollisions;
                        rigidBody3D.isKinematic       = isKinematic;
                    }
                    else rigidBody3D.isKinematic = isKinematic;
                }
            }

            // Simulation motion changes prefab instances outside the normal editor transform tools.
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                if (hasRigidBody)
                {
                    if (is2DObject && rigidBody2D != null)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(rigidBody2D);
                    else if (!is2DObject && rigidBody3D != null)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(rigidBody3D);
                }
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        public void onEnterSimulation()
        {
            _cleanupFinished                  = false;
            _canRecordSettledTransformUndo     = UndoEx.enabled;
            _creationUndoGroup                 = Undo.GetCurrentGroup();
            _spawnLocalPosition                = transform.localPosition;
            _spawnLocalRotation                = transform.localRotation;
            _spawnLocalScale                   = transform.localScale;
            is2DObject = gameObject.hierarchyHasSprite(false, false);
            if (is2DObject) enter2DSimulation();
            else enter3DSimulation();
            _hasEnteredSimulation = true;
            if (!is2DObject) _simColliders3D = GetComponentsInChildren<Collider>(true);
        }

        private void enter2DSimulation()
        {
            rigidBody2D     = gameObject.getRigidBody2D();
            hasRigidBody    = rigidBody2D != null;

            if (hasRigidBody)
            {
                _original2DBodyType          = rigidBody2D.bodyType;
                _original2DGravityScale      = rigidBody2D.gravityScale;
                _original2DSimulated         = rigidBody2D.simulated;
                #if UNITY_6000_0_OR_NEWER
                _original2DVelocity          = rigidBody2D.linearVelocity;
                #else
                _original2DVelocity          = rigidBody2D.velocity;
                #endif
                _original2DAngularVelocity   = rigidBody2D.angularVelocity;
            }
            else rigidBody2D = gameObject.AddComponent<Rigidbody2D>();

            // Temporarily allow already-authored kinematic/static/disabled bodies to fall.
            rigidBody2D.bodyType     = RigidbodyType2D.Dynamic;
            rigidBody2D.simulated    = true;
            rigidBody2D.gravityScale = 1.0f;
            #if UNITY_6000_0_OR_NEWER
            rigidBody2D.linearVelocity = Vector2.zero;
            #else
            rigidBody2D.velocity = Vector2.zero;
            #endif
            rigidBody2D.angularVelocity = 0.0f;
            rigidBody2D.WakeUp();

            if (!hasUsable2DCollider())
            {
                AABB bounds = calcFallbackBounds();
                if (bounds.isValid)
                {
                    var collider = gameObject.AddComponent<BoxCollider2D>();
                    collider.offset = new Vector2(bounds.center.x, bounds.center.y);
                    collider.size = new Vector2(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.y));
                    addedCollider2D = collider;
                }
                else Debug.LogWarning("Physics Spawn: No usable 2D collider or visible bounds for " + gameObject.name, gameObject);
            }
        }

        private void enter3DSimulation()
        {
            rigidBody3D     = gameObject.getRigidBody();
            hasRigidBody    = rigidBody3D != null;
            isKinematic     = hasRigidBody && rigidBody3D.isKinematic;

            if (hasRigidBody)
            {
                _original3DUseGravity         = rigidBody3D.useGravity;
                _original3DDetectCollisions   = rigidBody3D.detectCollisions;
                #if UNITY_6000_0_OR_NEWER
                _original3DVelocity           = rigidBody3D.linearVelocity;
                #else
                _original3DVelocity           = rigidBody3D.velocity;
                #endif
                _original3DAngularVelocity    = rigidBody3D.angularVelocity;
            }
            else rigidBody3D = gameObject.AddComponent<Rigidbody>();

            rigidBody3D.isKinematic       = false;
            rigidBody3D.useGravity        = true;
            rigidBody3D.detectCollisions  = true;
            #if UNITY_6000_0_OR_NEWER
            rigidBody3D.linearVelocity    = Vector3.zero;
            #else
            rigidBody3D.velocity          = Vector3.zero;
            #endif
            rigidBody3D.angularVelocity   = Vector3.zero;
            rigidBody3D.WakeUp();

            if (!hasUsable3DCollider())
            {
                AABB bounds = calcFallbackBounds();
                if (bounds.isValid)
                {
                    var collider = gameObject.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = new Vector3(Mathf.Max(0.01f, bounds.size.x), Mathf.Max(0.01f, bounds.size.y), Mathf.Max(0.01f, bounds.size.z));
                    addedCollider3D = collider;
                }
                else Debug.LogWarning("Physics Spawn: No usable 3D collider or visible bounds for " + gameObject.name, gameObject);
            }
        }

        // This check deliberately fails closed. Objects that are nearly motionless in
        // midair must never be declared settled. Unusual collider shapes may therefore
        // use the full Simulation Time rather than completing early.
        public bool hasSurfaceSupport()
        {
            if (is2DObject) return has2DSurfaceSupport();
            return has3DSurfaceSupport();
        }

        private bool has2DSurfaceSupport()
        {
            if (rigidBody2D == null) return false;
            if (_contactPoints2D == null) _contactPoints2D = new ContactPoint2D[32];

            int numContacts = rigidBody2D.GetContacts(_contactPoints2D);
            if (numContacts == 0) return false;

            Bounds bounds = new Bounds();
            bool haveBounds = false;
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy ||
                    collider.isTrigger || collider.attachedRigidbody != rigidBody2D) continue;
                if (!haveBounds) { bounds = collider.bounds; haveBounds = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!haveBounds) return false;

            for (int i = 0; i < numContacts; ++i)
            {
                var contact = _contactPoints2D[i];
                // Accept either normal orientation (contacts are reported relative to
                // the collider ordering). The contact must be near the lowest surface.
                if (Mathf.Abs(contact.normal.y) >= 0.35f &&
                    contact.point.y <= bounds.min.y + 0.04f) return true;
            }
            return false;
        }

        private bool has3DSurfaceSupport()
        {
            if (rigidBody3D == null || _simColliders3D == null) return false;
            if (_supportHits3D == null) _supportHits3D = new RaycastHit[32];

            Bounds bounds = new Bounds();
            bool haveBounds = false;
            foreach (var collider in _simColliders3D)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    collider.isTrigger || collider.attachedRigidbody != rigidBody3D) continue;
                if (!haveBounds) { bounds = collider.bounds; haveBounds = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!haveBounds) return false;

            const float rayStartAboveBottom = 0.12f;
            const float rayDistance = 0.24f;
            float y = bounds.min.y + rayStartAboveBottom;
            float x = bounds.center.x;
            float z = bounds.center.z;
            float dx = bounds.extents.x * 0.65f;
            float dz = bounds.extents.z * 0.65f;

            return hasSupportRayHit(new Vector3(x, y, z), bounds.min.y, rayDistance) ||
                   hasSupportRayHit(new Vector3(x - dx, y, z - dz), bounds.min.y, rayDistance) ||
                   hasSupportRayHit(new Vector3(x + dx, y, z - dz), bounds.min.y, rayDistance) ||
                   hasSupportRayHit(new Vector3(x - dx, y, z + dz), bounds.min.y, rayDistance) ||
                   hasSupportRayHit(new Vector3(x + dx, y, z + dz), bounds.min.y, rayDistance);
        }

        private bool hasSupportRayHit(Vector3 origin, float bottomY, float distance)
        {
            int hits = Physics.RaycastNonAlloc(origin, Vector3.down, _supportHits3D, distance,
                                               ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; ++i)
            {
                var hit = _supportHits3D[i];
                if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.normal.y >= 0.35f && Mathf.Abs(hit.point.y - bottomY) <= 0.04f)
                    return true;
            }
            return false;
        }

        private bool hasUsable2DCollider()
        {
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
                if (collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger && collider.attachedRigidbody == rigidBody2D)
                    return true;
            return false;
        }

        private bool hasUsable3DCollider()
        {
            foreach (var collider in GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger || collider.attachedRigidbody != rigidBody3D)
                    continue;

                // A non-convex MeshCollider cannot collide as part of a dynamic Rigidbody.
                var meshCollider = collider as MeshCollider;
                if (meshCollider == null || meshCollider.convex) return true;
            }
            return false;
        }

        private AABB calcFallbackBounds()
        {
            var config = ObjectBounds.QueryConfig.defaultConfig;
            config.objectTypes = is2DObject ? GameObjectType.Sprite : GameObjectType.Mesh;
            return ObjectBounds.calcHierarchyModelAABB(gameObject, config);
        }
    }
}
#endif
