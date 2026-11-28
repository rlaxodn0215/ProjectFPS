#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace GSPAWN
{
    public class ObjectDragSpawnSettings : PluginSettings<ObjectDragSpawnSettings>
    {
        [SerializeField]
        private float   _minDragDistance            = defaultMinDragDistance;
        [SerializeField]
        private bool    _useSafeDragDistance        = defaultUseSafeDragDistance;

        public float            minDragDistance                 { get { return _minDragDistance; } set { UndoEx.record(this); _minDragDistance = Mathf.Max(1e-4f, value); EditorUtility.SetDirty(this); } }
        public bool             useSafeDragDistance             { get { return _useSafeDragDistance; } set { UndoEx.record(this); _useSafeDragDistance = value; EditorUtility.SetDirty(this); } }

        public static float     defaultMinDragDistance          { get { return 1.0f; } }
        public static bool      defaultUseSafeDragDistance      { get { return true; } }

        public override void useDefaults()
        {
            minDragDistance         = defaultMinDragDistance;
            useSafeDragDistance     = defaultUseSafeDragDistance;

            EditorUtility.SetDirty(this);
        }

        public void buildUI(VisualElement parent)
        {
            const float labelWidth = 150.0f;

            var floatField  = UI.createFloatField("_minDragDistance", serializedObject, "~Min drag distance", "An approximate value that indicates the minimum drag distance that must be traversed by the mouse cursor before a new object is spawned.", 1e-4f, parent);
            floatField.setChildLabelWidth(labelWidth);
            
            var toggleField = UI.createToggle("_useSafeDragDistance", serializedObject, "Avoid drag overlaps", "When enabled, drag spawning uses oriented prefab bounds to avoid placing objects on top of others in the same drag stroke. Minimum drag distance is still respected.", parent);
            toggleField.setChildLabelWidth(labelWidth);

            UI.createUseDefaultsButton(() => useDefaults(), parent);
        }
    }
}
#endif