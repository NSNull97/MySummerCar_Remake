using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace MSC.Editor.Lifecycle
{
    /// <summary>
    /// Keeps project-owned runtime services coherent when another process edits
    /// C# while the Editor is in Play Mode. The production composition contains
    /// plain C# service references that Unity cannot restore across a live
    /// domain reload, so continuing the same Play session is never valid.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeCompilationGuard
    {
        public const string ScriptCompilationDuringPlayPreference =
            "ScriptCompilationDuringPlay";
        public const int RecompileAndContinuePlaying = 0;
        public const int RecompileAfterFinishedPlaying = 1;
        public const int StopPlayingAndRecompile = 2;

        private static bool reloadLockHeld;
        private static bool playStopRequested;

        static PlayModeCompilationGuard()
        {
            EnsureSafePreference();
            CompilationPipeline.compilationStarted -=
                HandleCompilationStarted;
            CompilationPipeline.compilationStarted +=
                HandleCompilationStarted;
            EditorApplication.playModeStateChanged -=
                HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged +=
                HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -=
                HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload +=
                HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
        }

        public static bool IsSafePreference(int preference)
        {
            return preference == RecompileAfterFinishedPlaying ||
                   preference == StopPlayingAndRecompile;
        }

        public static int ResolveSafePreference(int preference)
        {
            return IsSafePreference(preference)
                ? preference
                : RecompileAfterFinishedPlaying;
        }

        public static bool EnsureSafePreference()
        {
            int current = EditorPrefs.GetInt(
                ScriptCompilationDuringPlayPreference,
                RecompileAndContinuePlaying);
            int safe = ResolveSafePreference(current);
            if (safe == current)
            {
                return false;
            }

            EditorPrefs.SetInt(
                ScriptCompilationDuringPlayPreference,
                safe);
            Debug.Log(
                "[MSC] Script Changes While Playing was set to Recompile " +
                "After Finished Playing. Live domain reload cannot preserve " +
                "the production session's plain C# service bindings.");
            return true;
        }

        [MenuItem("Tools/MSC/Editor/Enforce Safe Play Mode Compilation")]
        private static void EnforceFromMenu()
        {
            bool changed = EnsureSafePreference();
            Debug.Log(changed
                ? "[MSC] Safe Play Mode compilation policy applied."
                : "[MSC] Play Mode compilation policy is already safe.");
        }

        private static void HandleCompilationStarted(object context)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode ||
                playStopRequested)
            {
                return;
            }

            // The preference normally defers compilation until Play Mode ends.
            // This fallback protects the session if an external tool or a later
            // preference change still starts compilation while Play is active.
            if (!reloadLockHeld)
            {
                EditorApplication.LockReloadAssemblies();
                reloadLockHeld = true;
            }

            playStopRequested = true;
            Debug.LogWarning(
                "[MSC] Script compilation started during Play Mode. Stopping " +
                "the session before assembly reload to preserve composition " +
                "root lifecycle integrity.");
            EditorApplication.isPlaying = false;
        }

        private static void HandlePlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            playStopRequested = false;
            ReleaseReloadLock();
        }

        private static void HandleBeforeAssemblyReload()
        {
            ReleaseReloadLock();
        }

        private static void HandleEditorQuitting()
        {
            ReleaseReloadLock();
        }

        private static void ReleaseReloadLock()
        {
            if (!reloadLockHeld)
            {
                return;
            }

            reloadLockHeld = false;
            EditorApplication.UnlockReloadAssemblies();
        }
    }
}
