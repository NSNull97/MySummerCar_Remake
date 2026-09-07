using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    [DefaultExecutionOrder(20000)]
    public sealed class SatsumaRoadAuditProbe : MonoBehaviour
    {
        public Action<Collision> CollisionObserved;
        public Action AfterLateUpdate;
        private void OnCollisionEnter(Collision collision) => CollisionObserved?.Invoke(collision);
        private void OnCollisionStay(Collision collision) => CollisionObserved?.Invoke(collision);
        private void LateUpdate() => AfterLateUpdate?.Invoke();
    }

    public sealed class SatsumaNativeDriverPlayModeTests
    {
        [UnityTest]
        public IEnumerator NativeRoadRenderAuditUsesPopulatedBootstrap()
        {
            if (!Application.isEditor) Assert.Ignore("Editor-only native render audit.");
            Type fixture = Type.GetType("MSC.Save.Integration.Tests.EditMode.CanonicalConsumableNativeSaveTests, MSC.Save.Integration.Tests.EditMode", true);
            var flow = (IEnumerator)fixture.GetMethod("RunNativeRoadRenderAudit", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            try { yield return flow; }
            finally { (flow as IDisposable)?.Dispose(); }
        }

        [UnityTest]
        public IEnumerator NativeRoadPhysicsAuditUsesActualRoadColliders()
        {
            if (!Application.isEditor) Assert.Ignore("Editor-only native road diagnostic.");
            Type fixture = Type.GetType("MSC.Save.Integration.Tests.EditMode.CanonicalConsumableNativeSaveTests, MSC.Save.Integration.Tests.EditMode", true);
            var flow = (IEnumerator)fixture.GetMethod("RunNativeRoadPhysicsAudit", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
            try { yield return flow; }
            finally { (flow as IDisposable)?.Dispose(); }
        }

        [UnityTest]
        public IEnumerator NativeDriverFirstDriveUsesRealInputIgnitionAdapterHydraulicsAndNwhContacts()
        {
            if(!Application.isEditor) Assert.Ignore("The opt-in native fixture uses Editor asset loading; run in Editor PlayMode.");
            // Reuse the canonical native/item/ownership fixture without adding
            // an Editor assembly dependency to a runtime PlayMode test assembly.
            Type fixture=Type.GetType("MSC.Save.Integration.Tests.EditMode.CanonicalConsumableNativeSaveTests, MSC.Save.Integration.Tests.EditMode",true);
            MethodInfo method=fixture.GetMethod("RunNativeDriverDriveFlow",BindingFlags.Public|BindingFlags.Static);
            Assert.That(method,Is.Not.Null);
            var flow=(IEnumerator)method.Invoke(null,null);
            try {yield return flow;}
            finally {(flow as IDisposable)?.Dispose();}
        }
    }
}
