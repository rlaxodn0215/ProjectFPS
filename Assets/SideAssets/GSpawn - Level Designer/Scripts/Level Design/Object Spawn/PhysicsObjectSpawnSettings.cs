#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GSPAWN
{
    public class PhysicsObjectSpawnSettings : PluginSettings<PhysicsObjectSpawnSettings>
    {
        [SerializeField]
        private string  _randomPrefabProfileName    = defaultRandomPrefabProfileName;
        [SerializeField]
        private bool    _randomizeRotation          = defaultRanfomizeRotation;
        [SerializeField]
        private float   _simulationTime             = defaultSimulationTime;
        [SerializeField]
        private float   _simulationStep             = defaultSimulationStep;
        [SerializeField]
        private float   _lowMotionLinearSpeed       = defaultLowMotionLinearSpeed;
        [SerializeField]
        private float   _lowMotionAngularSpeed      = defaultLowMotionAngularSpeed;
        [SerializeField]
        private float   _lowMotionDuration          = defaultLowMotionDuration;
        [SerializeField]
        private bool    _stopWhenSettled             = defaultStopWhenSettled;
        [SerializeField]
        private float   _outOfBoundsYCoord          = defaultOutOfBoundsYCoord;
        [SerializeField]
        private float   _dropRadius                 = defaultDropRadius;
        [SerializeField]
        private float   _dropHeight                 = defaultDropHeight;
        [SerializeField]
        private float   _dropInterval               = defaultDropInterval;
        [SerializeField]
        private bool    _instant                    = defaultInstant;

        public RandomPrefabProfile                  randomPrefabProfile
        {
            get
            {
                var profile = RandomPrefabProfileDb.instance.findProfile(_randomPrefabProfileName);
                if (profile == null) profile = RandomPrefabProfileDb.instance.defaultProfile;
                return profile;
            }
        }
        public bool             randomizeRotation                   { get { return _randomizeRotation; } set { UndoEx.record(this); _randomizeRotation = value; EditorUtility.SetDirty(this); } }
        public float            simulationTime                      { get { return _simulationTime; } set { UndoEx.record(this); _simulationTime = Mathf.Max(0.1f, value); EditorUtility.SetDirty(this); } }
        public float            simulationStep                      { get { return _simulationStep; } set { UndoEx.record(this); _simulationStep = Mathf.Max(0.01f, value); EditorUtility.SetDirty(this); } }
        public float            lowMotionLinearSpeed                { get { return _lowMotionLinearSpeed; } set { UndoEx.record(this); _lowMotionLinearSpeed = Mathf.Max(0.001f, value); EditorUtility.SetDirty(this); } }
        public float            lowMotionAngularSpeed               { get { return _lowMotionAngularSpeed; } set { UndoEx.record(this); _lowMotionAngularSpeed = Mathf.Max(0.01f, value); EditorUtility.SetDirty(this); } }
        public float            lowMotionDuration                   { get { return _lowMotionDuration; } set { UndoEx.record(this); _lowMotionDuration = Mathf.Max(0.1f, value); EditorUtility.SetDirty(this); } }
        public bool             stopWhenSettled                      { get { return _stopWhenSettled; } set { UndoEx.record(this); _stopWhenSettled = value; EditorUtility.SetDirty(this); } }
        public float            outOfBoundsYCoord                   { get { return _outOfBoundsYCoord; } set { UndoEx.record(this); _outOfBoundsYCoord = value; EditorUtility.SetDirty(this); } }       
        public float            dropRadius                          { get { return _dropRadius; } set { UndoEx.record(this); _dropRadius = Mathf.Max(0.1f, value); EditorUtility.SetDirty(this); } }
        public float            dropHeight                          { get { return _dropHeight; } set { UndoEx.record(this); _dropHeight = Mathf.Max(0.1f, value); EditorUtility.SetDirty(this); } }
        public float            dropInterval                        { get { return _dropInterval; } set { UndoEx.record(this); _dropInterval = Mathf.Max(0.01f, value); EditorUtility.SetDirty(this); } }
        public bool             instant                             { get { return _instant; } set { UndoEx.record(this); _instant = value; EditorUtility.SetDirty(this); } }

        public static string    defaultRandomPrefabProfileName      { get { return RandomPrefabProfileDb.defaultProfileName; } }
        public static bool      defaultRanfomizeRotation            { get { return true; } }
        public static float     defaultSimulationTime               { get { return 10.0f; } }
        public static float     defaultSimulationStep               { get { return 0.01f; } }
        public static float     defaultLowMotionLinearSpeed         { get { return 0.05f; } }
        public static float     defaultLowMotionAngularSpeed        { get { return 2.0f; } }
        public static float     defaultLowMotionDuration            { get { return 0.5f; } }
        public static bool      defaultStopWhenSettled              { get { return false; } }
        public static float     defaultOutOfBoundsYCoord            { get { return -10.0f; } }
        public static float     defaultDropRadius                   { get { return 2.0f; } }
        public static float     defaultDropHeight                   { get { return 5.0f; } }
        public static float     defaultDropInterval                 { get { return 0.05f; } }
        public static bool      defaultInstant                      { get { return false; } }

        public void buildUI(VisualElement parent)
        {
            const float labelWidth = 140.0f;

            IMGUIContainer randomPrefabProfileContainer = UI.createIMGUIContainer(parent);
            randomPrefabProfileContainer.onGUIHandler = () =>
            {
                string newName = EditorUIEx.profileNameSelectionField<RandomPrefabProfileDb, RandomPrefabProfile>
                    (RandomPrefabProfileDb.instance, "Random prefab profile", labelWidth, _randomPrefabProfileName);
                if (newName != _randomPrefabProfileName)
                {
                    UndoEx.record(this);
                    _randomPrefabProfileName = newName;
                    EditorUtility.SetDirty(this);
                }
            };

            var randomizeRotationField = UI.createToggle("_randomizeRotation", serializedObject, "Randomize rotation", 
                "If checked, a random rotation will be applied to objects before they get dropped in the scene.", parent);
            randomizeRotationField.setChildLabelWidth(labelWidth);

            var simulationTimeField = UI.createFloatField("_simulationTime", serializedObject, "Simulation time", "The amount of time the simulation lasts in seconds. " + 
                "Note: The simulation time is always reset when new objects are spawned.", 0.1f, parent);
            simulationTimeField.setChildLabelWidth(labelWidth);

            var simulationStepField = UI.createFloatField("_simulationStep", serializedObject, "Simulation step", "The time used to advance the physics simulation.", 0.01f, parent);
            simulationStepField.setChildLabelWidth(labelWidth);

            var lowMotionLinearSpeedField = UI.createFloatField("_lowMotionLinearSpeed", serializedObject, "Low-motion speed",
                "Linear speed in meters/second below which an object may count as low-motion. Diagnostic only; objects remain simulated.", 0.001f, parent);
            lowMotionLinearSpeedField.setChildLabelWidth(labelWidth);

            var lowMotionAngularSpeedField = UI.createFloatField("_lowMotionAngularSpeed", serializedObject, "Low-motion rotation",
                "Angular speed in degrees/second below which an object may count as low-motion. Diagnostic only.", 0.01f, parent);
            lowMotionAngularSpeedField.setChildLabelWidth(labelWidth);

            var lowMotionDurationField = UI.createFloatField("_lowMotionDuration", serializedObject, "Low-motion duration",
                "Continuous simulated seconds below both speed thresholds required to count as low-motion. Also used by optional Stop when settled.", 0.1f, parent);
            lowMotionDurationField.setChildLabelWidth(labelWidth);

            var stopWhenSettledField = UI.createToggle("_stopWhenSettled", serializedObject, "Stop when settled",
                "Optionally finish before Simulation Time when every object has low linear/angular motion for the configured duration AND nearby supporting collision geometry. If support is uncertain, simulation continues to its time limit. New drops restart the budget.", parent);
            stopWhenSettledField.setChildLabelWidth(labelWidth);

            var outOfBoundsYCoordField = UI.createFloatField("_outOfBoundsYCoord", serializedObject, "Out of bounds Y", "During a simulation, " + 
                "the plugin will destroy any objects whose positions along the Y axis become <= to this value. Useful to avoid stray objects.", parent);
            outOfBoundsYCoordField.setChildLabelWidth(labelWidth);

            var dropRadiusField = UI.createFloatField("_dropRadius", serializedObject, "Drop radius", "The radius of the circle in which objects will be spawned.", 0.1f, parent);
            dropRadiusField.setChildLabelWidth(labelWidth);

            var dropHeightField = UI.createFloatField("_dropHeight", serializedObject, "Drop height", "The height from which the object will be dropped onto the surface.", 0.1f, parent);
            dropHeightField.setChildLabelWidth(labelWidth);

            var dropIntervalField = UI.createFloatField("_dropInterval", serializedObject, "Drop interval", "The amount of time that needs to pass between successive drops.", 0.01f, parent);
            dropIntervalField.setChildLabelWidth(labelWidth);

            var instantToggle = UI.createToggle("_instant", serializedObject, "Instant", "If checked, the simulation will be applied instantly for each object that is spawned.", parent);
            instantToggle.setChildLabelWidth(labelWidth);

            var progressBar = new ProgressBar();
            progressBar.lowValue = 0.0f;
            progressBar.highValue = 100.0f;
            progressBar.title = "Physics simulation";
            parent.Add(progressBar);

            var statusLabel = new Label("Simulation idle.");
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.tooltip = "Low-motion is based on speed and duration; it does not guarantee an object is supported by a surface. New drops restart the simulation timer.";
            parent.Add(statusLabel);
            statusLabel.schedule.Execute(() =>
            {
                var simulation = PhysicsSimulation.instance;
                progressBar.value = simulation.progress * 100.0f;
                statusLabel.text = simulation.statusText;
            }).Every(200);

            UI.createUseDefaultsButton(() => useDefaults(), parent);
        }

        public override void useDefaults()
        {
            _randomPrefabProfileName    = defaultRandomPrefabProfileName;
            simulationTime              = defaultSimulationTime;
            simulationStep              = defaultSimulationStep;
            lowMotionLinearSpeed        = defaultLowMotionLinearSpeed;
            lowMotionAngularSpeed       = defaultLowMotionAngularSpeed;
            lowMotionDuration           = defaultLowMotionDuration;
            stopWhenSettled             = defaultStopWhenSettled;
            outOfBoundsYCoord           = defaultOutOfBoundsYCoord;
            dropRadius                  = defaultDropRadius;
            dropHeight                  = defaultDropHeight;
            dropInterval                = defaultDropInterval;
            instant                     = defaultInstant;

            EditorUtility.SetDirty(this);
        }
    }
}
#endif