using MSC.Editor.Lifecycle;
using NUnit.Framework;
using UnityEditor;

namespace MSC.Tests.EditMode.EditorLifecycle
{
    public sealed class PlayModeCompilationGuardTests
    {
        [Test]
        public void SafePreferencePolicy_RejectsLiveContinuationOnly()
        {
            Assert.That(PlayModeCompilationGuard.IsSafePreference(
                PlayModeCompilationGuard.RecompileAndContinuePlaying),
                Is.False);
            Assert.That(PlayModeCompilationGuard.IsSafePreference(
                PlayModeCompilationGuard.RecompileAfterFinishedPlaying),
                Is.True);
            Assert.That(PlayModeCompilationGuard.IsSafePreference(
                PlayModeCompilationGuard.StopPlayingAndRecompile),
                Is.True);
        }

        [Test]
        public void ResolveSafePreference_PreservesStricterStopPolicy()
        {
            Assert.That(PlayModeCompilationGuard.ResolveSafePreference(
                    PlayModeCompilationGuard.RecompileAndContinuePlaying),
                Is.EqualTo(PlayModeCompilationGuard
                    .RecompileAfterFinishedPlaying));
            Assert.That(PlayModeCompilationGuard.ResolveSafePreference(
                    PlayModeCompilationGuard.StopPlayingAndRecompile),
                Is.EqualTo(PlayModeCompilationGuard
                    .StopPlayingAndRecompile));
        }

        [Test]
        public void EditorPreference_IsSafeForProductionSessionLifecycle()
        {
            PlayModeCompilationGuard.EnsureSafePreference();
            int configured = EditorPrefs.GetInt(
                PlayModeCompilationGuard
                    .ScriptCompilationDuringPlayPreference,
                PlayModeCompilationGuard.RecompileAndContinuePlaying);

            Assert.That(
                PlayModeCompilationGuard.IsSafePreference(configured),
                Is.True,
                "The Editor must never continue the same production Play " +
                "session after recompiling scripts.");
        }
    }
}
