using System;
using System.Reflection;
using MSC.Traffic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Traffic
{
    public sealed class TrafficRuntimeLifecycleEditModeTests
    {
        [Test]
        public void InitializedFlag_IsExcludedFromUnityHotReloadState()
        {
            FieldInfo initialized = typeof(TrafficWorldRuntime).GetField(
                "initialized",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(initialized, Is.Not.Null);
            Assert.That(
                initialized.GetCustomAttribute<NonSerializedAttribute>(),
                Is.Not.Null,
                "A hot reload must not restore initialized=true after plain " +
                "C# session dependencies have been discarded.");
        }

        [Test]
        public void Update_WhenSessionDependenciesAreLost_FailsClosedOnce()
        {
            var runtimeObject = new GameObject("TrafficRuntimeLifecycleTest");
            try
            {
                TrafficWorldRuntime runtime =
                    runtimeObject.AddComponent<TrafficWorldRuntime>();
                FieldInfo initialized = typeof(TrafficWorldRuntime).GetField(
                    "initialized",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo update = typeof(TrafficWorldRuntime).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(initialized, Is.Not.Null);
                Assert.That(update, Is.Not.Null);
                initialized.SetValue(runtime, true);

                LogAssert.Expect(
                    LogType.Warning,
                    "Traffic runtime lost live session dependencies and was " +
                    "disabled. Restart Play Mode to rebuild the session.");
                Assert.DoesNotThrow(() => update.Invoke(runtime, null));
                Assert.That(runtime.IsInitialized, Is.False);
                Assert.That(runtime.enabled, Is.False);

                // A FixedUpdate in the same frame must now be a quiet no-op.
                MethodInfo fixedUpdate = typeof(TrafficWorldRuntime).GetMethod(
                    "FixedUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(fixedUpdate, Is.Not.Null);
                Assert.DoesNotThrow(() => fixedUpdate.Invoke(runtime, null));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(runtimeObject);
            }
        }
    }
}
