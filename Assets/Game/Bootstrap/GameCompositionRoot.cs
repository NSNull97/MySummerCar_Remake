using System;
using MSC.Core.Lifecycle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Process-lifetime owner for explicitly constructed services. This component is not a global service locator.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    [DisallowMultipleComponent]
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        private static GameCompositionRoot activeRoot;

        private GameServiceBindings serviceBindings;
        private string originScenePath = string.Empty;
        private int originSceneHandle = -1;
        private bool sessionEnding;
        private bool replacedPreviousSession;

        public static GameCompositionRoot ActiveRoot => activeRoot;

        public bool IsPrimaryRoot =>
            !Application.isPlaying || activeRoot == this;

        public bool IsInitialized => serviceBindings != null;

        public int BoundServiceCount => serviceBindings?.ServiceCount ?? 0;

        public bool HasCompleteBindings => serviceBindings != null && serviceBindings.IsComplete;

        public bool ReplacedPreviousSession => replacedPreviousSession;

        public void Initialize(GameServiceBindings bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("The game composition root has already been initialized.");
            }

            serviceBindings = bindings;
        }

        public void Shutdown()
        {
            serviceBindings = null;
        }

        private void Awake()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            replacedPreviousSession = false;

            if (activeRoot != null && activeRoot != this)
            {
                if (activeRoot.CanBeReplacedBy(gameObject.scene))
                {
                    // A Single-mode session transition makes the incoming root
                    // the only loaded playable composition. Release old static
                    // owners synchronously so the replacement hierarchy can
                    // complete Awake safely.
                    activeRoot.EndSessionAndDestroy();
                    replacedPreviousSession = true;
                }
                else
                {
                    // An additive Bootstrap is an invalid duplicate, not a new
                    // session. Keep the established owner and fail the duplicate
                    // hierarchy closed before its children start.
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                    return;
                }
            }

            originScenePath = gameObject.scene.path;
            originSceneHandle = gameObject.scene.handle;
            activeRoot = this;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            DontDestroyOnLoad(gameObject);
        }

        private bool CanBeReplacedBy(Scene candidateScene)
        {
            if (!candidateScene.IsValid() ||
                candidateScene.handle == originSceneHandle)
            {
                return false;
            }

            // During a Single-mode transition the old origin scene has already
            // been unloaded before the incoming root runs Awake. The incoming
            // scene may be a different playable composition, so scene identity
            // must not prevent a clean process-lifetime handoff. In an additive
            // load the old origin is still present and the candidate remains an
            // invalid duplicate.
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(index);
                if (loadedScene.handle == originSceneHandle &&
                    loadedScene.isLoaded)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDestroy()
        {
            if (activeRoot != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Shutdown();
            activeRoot = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (activeRoot != this || mode != LoadSceneMode.Single ||
                string.Equals(
                    scene.path,
                    originScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            // A Single-mode scene outside the Bootstrap is a menu/development
            // transition. Session-owned player/world/weather must not leak into it.
            EndSessionAndDestroy();
        }

        private void EndSessionAndDestroy()
        {
            if (sessionEnding)
            {
                return;
            }

            sessionEnding = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            MonoBehaviour[] behaviours =
                GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (!(behaviours[index] is IGameSessionLifetime lifetime))
                {
                    continue;
                }

                try
                {
                    lifetime.EndGameSession();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, behaviours[index]);
                }
            }

            Shutdown();
            if (activeRoot == this)
            {
                activeRoot = null;
            }

            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
