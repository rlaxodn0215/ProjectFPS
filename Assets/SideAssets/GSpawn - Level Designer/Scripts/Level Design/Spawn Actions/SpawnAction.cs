#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GSPAWN
{
    [Serializable]
    public class SpawnActionPrefabVariation
    {
        [SerializeField] private GameObject _prefabAsset;
        [SerializeField] private float _weight = 1.0f;

        public GameObject prefabAsset { get { return _prefabAsset; } set { _prefabAsset = value; } }
        public float weight { get { return _weight; } set { _weight = validWeight(value); } }

        private static float validWeight(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 1.0f : Mathf.Clamp(value, 0.0001f, 1.0f);
        }

        public SpawnActionPrefabVariation(GameObject prefab, float weight = 1.0f)
        {
            _prefabAsset = prefab;
            _weight = validWeight(weight);
        }
    }

    [Serializable]
    public class SpawnActionOutput
    {
        [SerializeField] private GameObject _prefabAsset;
        [SerializeField] private Vector3 _contextLocalPosition;
        [SerializeField] private Quaternion _contextLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 _worldScale = Vector3.one;
        [SerializeField, Range(0.0f, 1.0f)] private float _spawnChance = 1.0f;
        [SerializeField] private bool _randomizeScale;
        [SerializeField] private float _minScale = 1.0f;
        [SerializeField] private float _maxScale = 1.0f;
        [SerializeField] private bool _randomizeRotation;
        [SerializeField] private Vector3 _minRotation;
        [SerializeField] private Vector3 _maxRotation;
        [SerializeField] private List<SpawnActionPrefabVariation> _variations = new List<SpawnActionPrefabVariation>();

        public GameObject prefabAsset { get { return _prefabAsset; } }
        public Vector3 contextLocalPosition { get { return _contextLocalPosition; } }
        public Quaternion contextLocalRotation { get { return _contextLocalRotation; } }
        public Vector3 worldScale { get { return _worldScale; } }
        public float spawnChance { get { return _spawnChance; } set { _spawnChance = Mathf.Clamp01(value); } }
        public bool randomizeScale { get { return _randomizeScale; } set { _randomizeScale = value; } }
        public float minScale
        {
            get { return _minScale; }
            set { _minScale = Mathf.Max(0.01f, value); _maxScale = Mathf.Max(_maxScale, _minScale); }
        }
        public float maxScale
        {
            get { return _maxScale; }
            set { _maxScale = Mathf.Max(0.01f, value); _minScale = Mathf.Min(_minScale, _maxScale); }
        }
        public bool randomizeRotation { get { return _randomizeRotation; } set { _randomizeRotation = value; } }
        public Vector3 minRotation
        {
            get { return _minRotation; }
            set { _minRotation = finiteVector(value); _maxRotation = Vector3.Max(_maxRotation, _minRotation); }
        }
        public Vector3 maxRotation
        {
            get { return _maxRotation; }
            set { _maxRotation = finiteVector(value); _minRotation = Vector3.Min(_minRotation, _maxRotation); }
        }
        public List<SpawnActionPrefabVariation> variations
        {
            get
            {
                if (_variations == null) _variations = new List<SpawnActionPrefabVariation>();
                return _variations;
            }
        }

        public SpawnActionOutput(GameObject prefabAsset, Transform context, Transform output)
        {
            _prefabAsset = prefabAsset;
            _contextLocalPosition = context.InverseTransformPoint(output.position);
            _contextLocalRotation = Quaternion.Inverse(context.rotation) * output.rotation;
            _worldScale = output.lossyScale;
            _variations.Add(new SpawnActionPrefabVariation(prefabAsset));
            _variationsInitialized = true;
        }

        public void ensureLegacyVariations()
        {
            if (_variations == null) _variations = new List<SpawnActionPrefabVariation>();
            if (_variations.Count == 0 && _prefabAsset != null && !_variationsInitialized)
                _variations.Add(new SpawnActionPrefabVariation(_prefabAsset));
            _variationsInitialized = true;
        }

        [SerializeField] private bool _variationsInitialized;

        public bool containsPrefab(GameObject prefab)
        {
            foreach (SpawnActionPrefabVariation variation in variations)
                if (variation != null && variation.prefabAsset == prefab) return true;
            return false;
        }

        public GameObject pickPrefab(float unitRandom)
        {
            ensureLegacyVariations();
            float total = 0.0f;
            foreach (SpawnActionPrefabVariation variation in variations)
                if (variation != null && variation.prefabAsset != null && variation.weight > 0.0f)
                    total += variation.weight;
            if (total <= 0.0f) return null;

            float target = Mathf.Clamp01(unitRandom) * total;
            float accumulated = 0.0f;
            GameObject last = null;
            foreach (SpawnActionPrefabVariation variation in variations)
            {
                if (variation == null || variation.prefabAsset == null || variation.weight <= 0.0f) continue;
                last = variation.prefabAsset;
                accumulated += variation.weight;
                if (target < accumulated) return last;
            }
            return last;
        }

        public float getScaleMultiplier(float unitRandom)
        {
            return _randomizeScale ? Mathf.Lerp(_minScale, _maxScale, Mathf.Clamp01(unitRandom)) : 1.0f;
        }

        public Quaternion getRotationOffset(Vector3 unitRandom, Quaternion gridRotation)
        {
            if (!_randomizeRotation) return Quaternion.identity;
            Vector3 euler = new Vector3(
                Mathf.Lerp(_minRotation.x, _maxRotation.x, unitRandom.x),
                Mathf.Lerp(_minRotation.y, _maxRotation.y, unitRandom.y),
                Mathf.Lerp(_minRotation.z, _maxRotation.z, unitRandom.z));
            return gridRotation * Quaternion.Euler(euler) * Quaternion.Inverse(gridRotation);
        }

        private static Vector3 finiteVector(Vector3 value)
        {
            for (int i = 0; i < 3; i++)
                if (float.IsNaN(value[i]) || float.IsInfinity(value[i])) value[i] = 0.0f;
            return value;
        }
    }

    public class SpawnAction : ScriptableObject
    {
        [SerializeField] private bool _avoidOutputOverlaps;
        [SerializeField] private bool _pinned;
        [SerializeField] private GameObject _contextPrefab;
        [SerializeField] private string _contextName = string.Empty;
        [SerializeField] private List<SpawnActionOutput> _outputs = new List<SpawnActionOutput>();

        public bool pinned { get { return _pinned; } set { _pinned = value; } }
        public bool avoidOutputOverlaps { get { return _avoidOutputOverlaps; } set { _avoidOutputOverlaps = value; } }
        public GameObject contextPrefab { get { return _contextPrefab; } }
        public string contextName { get { return _contextName; } }
        public List<SpawnActionOutput> outputs { get { return _outputs; } }
        public bool isRecorded { get { return _outputs != null && _outputs.Count != 0; } }

        public void record(GameObject contextPrefab, string contextName, List<SpawnActionOutput> outputs)
        {
            _contextPrefab = contextPrefab;
            _contextName = contextName;
            _outputs = new List<SpawnActionOutput>(outputs);
        }
    }

    // Serialized on the GSpawn scene component, never in a project asset. Unity can
    // therefore persist real scene-object references and restore them via Undo/Redo.
    [Serializable]
    public class SpawnActionApplication
    {
        [SerializeField] private SpawnAction _action;
        [SerializeField] private GameObject _context;
        [SerializeField] private List<GameObject> _generated = new List<GameObject>();

        public SpawnAction action { get { return _action; } }
        public GameObject context { get { return _context; } }
        public List<GameObject> generated { get { return _generated; } }

        public SpawnActionApplication(SpawnAction action, GameObject context, List<GameObject> generated)
        {
            _action = action;
            _context = context;
            _generated = new List<GameObject>(generated);
        }

        public void replaceGenerated(List<GameObject> generated)
        {
            _generated = new List<GameObject>(generated);
        }
    }
}
#endif
