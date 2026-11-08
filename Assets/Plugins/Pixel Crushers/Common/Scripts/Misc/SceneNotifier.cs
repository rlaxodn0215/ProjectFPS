// Copyright (c) Pixel Crushers. All rights reserved.

using UnityEngine;

namespace PixelCrushers
{

    /// <summary>
    /// Service to notify subscribers when a scene is being unloaded.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class SceneNotifier
    {

        public delegate void UnloadSceneDelegate(int sceneIndex);

        /// <summary>
        /// Invoked by NotifyWillUnloadScene(sceneIndex), which should be called
        /// before unloading a scene.
        /// </summary>
        public static event UnloadSceneDelegate willUnloadScene = delegate { };

#if UNITY_2019_3_OR_NEWER && UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void InitStaticVariables()
        {
            willUnloadScene = delegate { };
        }
#endif

        /// <summary>
        /// Notifies all subscribers that the scene with the specified index will be unloaded.
        /// </summary>
        /// <param name="sceneIndex">Scene index in build settings.</param>
        public static void NotifyWillUnloadScene(int sceneIndex)
        {
            willUnloadScene(sceneIndex);
        }

    }

}