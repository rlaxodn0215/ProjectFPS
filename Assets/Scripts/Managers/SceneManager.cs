using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace ProjectFPS
{
    /// <summary>Component for single-scene asynchronous transitions.</summary>
    public sealed class SceneManager : MonoBehaviour
    {
        public bool IsLoading { get; private set; }
        public float LoadingProgress { get; private set; }

        public event Action LoadStarted;
        public event Action<float> LoadProgressChanged;
        public event Action LoadCompleted;

        private void Awake()
        {
            // Keep tracking the load after the source scene is unloaded.
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Loads an enabled build scene by name or full asset path.</summary>
        public void LoadScene(string sceneName)
        {
            if (IsLoading) return;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("SceneManager: A scene name or path is required.", this);
                return;
            }

            string target = sceneName.Replace('\\', '/');
            bool isPath = target.Contains("/");
            int match = -1;
            for (int i = 0; i < UnitySceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                bool matches = isPath
                    ? string.Equals(path, target, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(Path.GetFileNameWithoutExtension(path),
                        Path.GetFileNameWithoutExtension(target), StringComparison.OrdinalIgnoreCase);
                if (!matches) continue;
                if (match >= 0)
                {
                    Debug.LogWarning($"SceneManager: Scene name '{sceneName}' is ambiguous. Use a full path or build index.", this);
                    return;
                }
                match = i;
            }

            if (match < 0)
            {
                Debug.LogWarning($"SceneManager: Scene '{sceneName}' is not in the enabled build scenes.", this);
                return;
            }
            LoadScene(match);
        }

        public void LoadScene(int buildIndex)
        {
            if (IsLoading) return;
            if (buildIndex < 0 || buildIndex >= UnitySceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning($"SceneManager: Invalid scene build index {buildIndex}.", this);
                return;
            }

            IsLoading = true;
            LoadingProgress = 0f;
            AsyncOperation operation;
            try
            {
                operation = UnitySceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
                if (operation == null)
                    throw new InvalidOperationException("Unity did not create a scene loading operation.");
                operation.allowSceneActivation = true;
            }
            catch (Exception exception)
            {
                IsLoading = false;
                Debug.LogException(exception, this);
                return;
            }

            InvokeSafely(LoadStarted);
            InvokeSafely(LoadProgressChanged, 0f);
            StartCoroutine(TrackLoading(operation));
        }

        public void ReloadCurrentScene() => LoadScene(UnitySceneManager.GetActiveScene().buildIndex);

        public void LoadNextScene()
        {
            if (IsLoading) return;
            int currentIndex = UnitySceneManager.GetActiveScene().buildIndex;
            if (currentIndex < 0 || currentIndex + 1 >= UnitySceneManager.sceneCountInBuildSettings)
            {
                Debug.LogWarning("SceneManager: The active scene has no next enabled build scene.", this);
                return;
            }
            LoadScene(currentIndex + 1);
        }

        private IEnumerator TrackLoading(AsyncOperation operation)
        {
            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                if (progress != LoadingProgress)
                {
                    LoadingProgress = progress;
                    InvokeSafely(LoadProgressChanged, progress);
                }
                yield return null;
            }

            LoadingProgress = 1f;
            InvokeSafely(LoadProgressChanged, 1f);
            IsLoading = false;
            InvokeSafely(LoadCompleted);
        }

        private void InvokeSafely(Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList())
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void InvokeSafely(Action<float> handlers, float progress)
        {
            if (handlers == null) return;
            foreach (Action<float> handler in handlers.GetInvocationList())
            {
                try { handler(progress); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
