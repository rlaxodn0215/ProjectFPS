#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace GSPAWN
{
    public struct PhysicsSimulationConfig
    {
        public bool                                     simulate2D;
        public float                                    simulationTime;
        public float                                    simulationStep;
        public float                                    outOfBoundsYCoord;
        public float                                    lowMotionLinearSpeed;
        public float                                    lowMotionAngularSpeed;
        public float                                    lowMotionDuration;
        public bool                                     stopWhenSettled;

        public static readonly PhysicsSimulationConfig  defaultConfig = new PhysicsSimulationConfig()
        {
            simulate2D              = false,
            simulationTime          = 10.0f,
            simulationStep          = 0.01f,
            outOfBoundsYCoord       = -10.0f,
            lowMotionLinearSpeed    = 0.05f,
            lowMotionAngularSpeed   = 2.0f,
            lowMotionDuration       = 0.5f,
            stopWhenSettled         = false,
        };
    }

    public class PhysicsSimulation : Singleton<PhysicsSimulation>
    {
        private List<PhysicsSimulationObjectMono>       _simObjects             = new List<PhysicsSimulationObjectMono>();
        private Dictionary<PhysicsSimulationObjectMono, float> _lowMotionTimes = new Dictionary<PhysicsSimulationObjectMono, float>();
        private Dictionary<PhysicsSimulationObjectMono, float> _supportedLowMotionTimes = new Dictionary<PhysicsSimulationObjectMono, float>();
        private const float                              minSupportDuration = 0.2f;
        private PhysicsSimulationConfig                 _config;
        private float                                   _simulationTime;
        private bool                                    _isRunning;
        private bool                                    _performingInstantSimulation;
        private bool                                    _hasLastResult;
        private bool                                    _lastCompleted;
        private bool                                    _lastSettledEarly;
        private int                                     _supportedLowMotionCount;
        private int                                     _lowMotionCount;
        private int                                     _lastObjectCount;
        private int                                     _lastLowMotionCount;
        private int                                     _outOfBoundsCount;
        private int                                     _lastOutOfBoundsCount;
        private float                                   _lastProgress;

        public bool         isRunning           { get { return _isRunning; } }
        public float        simulationTime      { get { return _simulationTime; } }
        public int          activeObjectCount   { get { return _isRunning ? _simObjects.Count : 0; } }
        public int          lowMotionCount      { get { return _isRunning ? _lowMotionCount : 0; } }
        public int          settledObjectCount  { get { return _isRunning ? _supportedLowMotionCount : 0; } }
        public float        progress            { get { return _isRunning ? Mathf.Clamp01(_simulationTime / _config.simulationTime) : (_hasLastResult ? _lastProgress : 0.0f); } }
        public string       statusText
        {
            get
            {
                if (_isRunning)
                {
                    return "Simulating " + _simulationTime.ToString("F1") + " / " + _config.simulationTime.ToString("F1") + " s  |  " +
                           (_simObjects.Count - _lowMotionCount) + " moving, " + _lowMotionCount + " low-motion" +
                           (_config.stopWhenSettled ? "  |  " + _supportedLowMotionCount + " supported/settled" : "") +
                           (_outOfBoundsCount > 0 ? "  |  " + _outOfBoundsCount + " out of bounds" : "");
                }
                if (!_hasLastResult) return "Simulation idle.";
                return (_lastSettledEarly ? "Settled early" : (_lastCompleted ? "Completed" : (_lastObjectCount == 0 && _lastOutOfBoundsCount > 0 ? "Ended (out of bounds)" : "Stopped early"))) +
                       " after " + _simulationTime.ToString("F1") + " s: " + _lastLowMotionCount + " low-motion, " +
                       (_lastObjectCount - _lastLowMotionCount) + " still moving" +
                       (_lastOutOfBoundsCount > 0 ? ", " + _lastOutOfBoundsCount + " removed out of bounds" : "") + ".";
            }
        }

        public void performInstantSimulation(PhysicsSimulationConfig config)
        {
            // Instant placement already records the completed transform in the creation frame.
            // Do not add a redundant asynchronous settle Undo operation for that workflow.
            _performingInstantSimulation = true;
            try
            {
                start(config);
                while (_isRunning)
                    update();
            }
            finally { _performingInstantSimulation = false; }
        }

        public void start(PhysicsSimulationConfig config)
        {
            _config             = config;
            _simulationTime     = 0.0f;
            _isRunning          = _simObjects.Count != 0;
            if (_isRunning)
            {
                _hasLastResult = false;
                // New drops restart the simulated-time budget for the whole batch.
                // Existing bodies continue moving; their motion observations remain valid.
            }

            if (float.IsNaN(_config.simulationStep) || float.IsInfinity(_config.simulationStep) || _config.simulationStep <= 0.0f)
                _config.simulationStep = PhysicsSimulationConfig.defaultConfig.simulationStep;
            if (float.IsNaN(_config.simulationTime) || float.IsInfinity(_config.simulationTime) || _config.simulationTime <= 0.0f)
                _config.simulationTime = PhysicsSimulationConfig.defaultConfig.simulationTime;
            if (float.IsNaN(_config.lowMotionLinearSpeed) || float.IsInfinity(_config.lowMotionLinearSpeed) || _config.lowMotionLinearSpeed <= 0.0f)
                _config.lowMotionLinearSpeed = PhysicsSimulationConfig.defaultConfig.lowMotionLinearSpeed;
            if (float.IsNaN(_config.lowMotionAngularSpeed) || float.IsInfinity(_config.lowMotionAngularSpeed) || _config.lowMotionAngularSpeed <= 0.0f)
                _config.lowMotionAngularSpeed = PhysicsSimulationConfig.defaultConfig.lowMotionAngularSpeed;
            if (float.IsNaN(_config.lowMotionDuration) || float.IsInfinity(_config.lowMotionDuration) || _config.lowMotionDuration <= 0.0f)
                _config.lowMotionDuration = PhysicsSimulationConfig.defaultConfig.lowMotionDuration;
        }

        public void stop(bool commitSettledTransforms = true, bool completed = false, bool settledEarly = false)
        {
            // Must also clean up if an Undo destroyed the last simulation object.
            if (!_isRunning && _simObjects.Count == 0) return;
            _isRunning = false;
            bool recordUndo = commitSettledTransforms && !_performingInstantSimulation;
            _lastObjectCount = 0;
            _lastLowMotionCount = 0;
            foreach (var simObject in _simObjects)
            {
                if (simObject == null) continue;
                ++_lastObjectCount;
                float lowMotionTime;
                if (_lowMotionTimes.TryGetValue(simObject, out lowMotionTime) && lowMotionTime >= _config.lowMotionDuration)
                    ++_lastLowMotionCount;
            }
            _lastCompleted = completed;
            _lastSettledEarly = settledEarly;
            _lastOutOfBoundsCount = _outOfBoundsCount;
            _lastProgress = settledEarly ? 1.0f : Mathf.Clamp01(_simulationTime / _config.simulationTime);
            _hasLastResult = commitSettledTransforms;

            // Only report unfinished motion on normal completion. Stopping manually intentionally
            // preserves the poses reached so far, even if bodies have not become stationary.
            if (completed && !_performingInstantSimulation && _lastObjectCount > _lastLowMotionCount)
                Debug.LogWarning("Physics Spawn: " + (_lastObjectCount - _lastLowMotionCount) + " of " + _lastObjectCount +
                                 " objects were still moving when Simulation Time expired. Increase Simulation Time or inspect their collisions.");
            bool commitGroupInitialized = false;
            bool hasRecordedTransforms = false;

            try
            {
                foreach (var simObject in _simObjects)
                {
                    if (simObject == null) continue;

                    // Restore/remove temporary physics components BEFORE saving the final pose.
                    simObject.onExitSimulation();
                    if (recordUndo && simObject.hasSettledTransformChange)
                    {
                        if (!commitGroupInitialized)
                        {
                            // If the original creation group is still current, add its final pose
                            // to the same Undo step. Otherwise use a fresh group so we never
                            // accidentally merge other editor actions performed during simulation.
                            if (Undo.GetCurrentGroup() != simObject.creationUndoGroup)
                            {
                                Undo.IncrementCurrentGroup();
                                Undo.SetCurrentGroupName("Physics Spawn - Settle");
                            }
                            commitGroupInitialized = true;
                        }
                        hasRecordedTransforms |= simObject.recordSettledTransformUndo();
                    }
                    Object.DestroyImmediate(simObject);
                }
                if (hasRecordedTransforms) Undo.FlushUndoRecordObjects();
            }
            finally
            {
                _simObjects.Clear();
                _lowMotionTimes.Clear();
                _supportedLowMotionTimes.Clear();
                _lowMotionCount = 0;
                _supportedLowMotionCount = 0;
                _outOfBoundsCount = 0;
            }
        }

        public void addObject(GameObject gameObject)
        {
            if (gameObject == null) return;

            // These components are temporary simulation implementation details, not Undo operations.
            var simObject = gameObject.AddComponent<PhysicsSimulationObjectMono>();
            try
            {
                simObject.onEnterSimulation();
                _simObjects.Add(simObject);
                _lowMotionTimes[simObject] = 0.0f;
                _supportedLowMotionTimes[simObject] = 0.0f;
                _lowMotionCount = 0;
                _supportedLowMotionCount = 0;
            }
            catch
            {
                simObject.onExitSimulation();
                Object.DestroyImmediate(simObject);
                throw;
            }
        }

        public void update()
        {
            if (!_isRunning) return;
            if (_simObjects.Count == 0) { stop(); return; }

            float step = Mathf.Min(_config.simulationStep, _config.simulationTime - _simulationTime);
            if (step <= 0.0f) { stop(); return; }

            try
            {
                #if UNITY_2022_2_OR_NEWER
                var oldSimulationMode = Physics.simulationMode;
                try
                {
                    Physics.simulationMode = SimulationMode.Script;
                    Physics.SyncTransforms();
                    Physics.Simulate(step);
                }
                finally { Physics.simulationMode = oldSimulationMode; }
                #else
                bool oldAutoSimulation = Physics.autoSimulation;
                try
                {
                    Physics.autoSimulation = false;
                    Physics.SyncTransforms();
                    Physics.Simulate(step);
                }
                finally { Physics.autoSimulation = oldAutoSimulation; }
                #endif

                if (_config.simulate2D)
                {
                    var oldSimulationMode2D = Physics2D.simulationMode;
                    try
                    {
                        Physics2D.simulationMode = SimulationMode2D.Script;
                        Physics2D.SyncTransforms();
                        Physics2D.Simulate(step);
                    }
                    finally { Physics2D.simulationMode = oldSimulationMode2D; }
                }
            }
            catch
            {
                stop();
                throw;
            }

            _simulationTime += step;

            for (int i = 0; i < _simObjects.Count;)
            {
                var simObject = _simObjects[i];

                // Undo or object deletion may invalidate the helper before simulation finishes.
                if (simObject == null)
                {
                    _lowMotionTimes.Remove(simObject);
                    _supportedLowMotionTimes.Remove(simObject);
                    _simObjects.RemoveAt(i);
                    continue;
                }

                if (simObject.transform.position.y <= _config.outOfBoundsYCoord)
                {
                    _lowMotionTimes.Remove(simObject);
                    _supportedLowMotionTimes.Remove(simObject);
                    ++_outOfBoundsCount;
                    GameObject.DestroyImmediate(simObject.gameObject);
                    _simObjects.RemoveAt(i);
                    continue;
                }

                // This is a diagnostic only; NEVER remove bodies for having low velocity.
                // Contacts from later drops must still be able to wake and displace them.
                float linearSpeed = 0.0f;
                float angularSpeedDegrees = 0.0f;
                if (simObject.is2DObject && simObject.rigidBody2D != null)
                {
                    #if UNITY_6000_0_OR_NEWER
                    linearSpeed = simObject.rigidBody2D.linearVelocity.magnitude;
                    #else
                    linearSpeed = simObject.rigidBody2D.velocity.magnitude;
                    #endif
                    angularSpeedDegrees = Mathf.Abs(simObject.rigidBody2D.angularVelocity);
                }
                else if (!simObject.is2DObject && simObject.rigidBody3D != null)
                {
                    #if UNITY_6000_0_OR_NEWER
                    linearSpeed = simObject.rigidBody3D.linearVelocity.magnitude;
                    #else
                    linearSpeed = simObject.rigidBody3D.velocity.magnitude;
                    #endif
                    // Rigidbody.angularVelocity is in radians/second (unlike Rigidbody2D).
                    angularSpeedDegrees = simObject.rigidBody3D.angularVelocity.magnitude * Mathf.Rad2Deg;
                }

                float lowMotionTime;
                _lowMotionTimes.TryGetValue(simObject, out lowMotionTime);
                if (linearSpeed <= _config.lowMotionLinearSpeed && angularSpeedDegrees <= _config.lowMotionAngularSpeed)
                    lowMotionTime += step;
                else lowMotionTime = 0.0f;
                _lowMotionTimes[simObject] = lowMotionTime;

                // Speed alone is not proof of settlement: confirm support over several
                // consecutive steps. A doubtful object runs to the normal time limit.
                if (_config.stopWhenSettled)
                {
                    float supportedTime = 0.0f;
                    _supportedLowMotionTimes.TryGetValue(simObject, out supportedTime);
                    if (lowMotionTime >= _config.lowMotionDuration && simObject.hasSurfaceSupport())
                        supportedTime += step;
                    else supportedTime = 0.0f;
                    _supportedLowMotionTimes[simObject] = supportedTime;
                }
                ++i;
            }

            _lowMotionCount = 0;
            _supportedLowMotionCount = 0;
            foreach (var simObject in _simObjects)
            {
                if (simObject == null) continue;
                float lowMotionTime;
                if (_lowMotionTimes.TryGetValue(simObject, out lowMotionTime) &&
                    lowMotionTime >= _config.lowMotionDuration) ++_lowMotionCount;
                float supportedTime;
                if (_config.stopWhenSettled &&
                    _supportedLowMotionTimes.TryGetValue(simObject, out supportedTime) &&
                    supportedTime >= minSupportDuration) ++_supportedLowMotionCount;
            }

            if (_simulationTime >= _config.simulationTime) stop(true, true);
            else if (_simObjects.Count == 0) stop(true, false);
            else if (_config.stopWhenSettled && _supportedLowMotionCount == _simObjects.Count)
                stop(true, true, true);
        }
    }
}
#endif
