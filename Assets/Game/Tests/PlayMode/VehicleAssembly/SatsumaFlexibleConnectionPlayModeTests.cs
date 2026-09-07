using System.Collections;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed partial class SatsumaEngineFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator FlexibleEndsUseRealLateUpdateAndRestoreOriginalOnDisableAndDetach()
        {
            var flexible = vehicle.GetComponent<SatsumaFlexibleConnectionPresenter>();
            var binding = flexible.Bindings[0]; var original = binding.Filter.sharedMesh;
            binding.Owner.RuntimeState.SetInstalled("presentation-only-owner", false);
            binding.Neighbour.RuntimeState.SetInstalled("presentation-only-neighbour", false);
            // This lifecycle fixture deliberately has no assembled graph. A
            // temporary explicit anchor supplies the reviewed millimetre motion;
            // separate native/assembled EditMode tests prove the real endpoints.
            var anchor = new GameObject("TEST ONLY flexible end anchor").transform;
            anchor.SetParent(binding.Neighbour.transform, false);
            anchor.position = binding.Filter.transform.TransformPoint(binding.EndCenter) + Vector3.right * .01f;
            flexible.Configure(new[]{new SatsumaFlexibleConnectionBinding(binding.Owner,binding.Neighbour,binding.Filter,
                anchor,binding.EndCenter,Vector3.zero,binding.RigidRadius,binding.FadeRadius)});
            yield return Frames(3);
            Assert.That(binding.Filter.sharedMesh,Is.Not.SameAs(original));
            Vector3[] first = binding.Filter.sharedMesh.vertices;
            Time.timeScale=0; yield return Frames(3);
            Assert.That(binding.Filter.sharedMesh.vertices,Is.EqualTo(first));
            flexible.enabled=false;
            Assert.That(binding.Filter.sharedMesh,Is.SameAs(original));
            flexible.enabled=true; yield return Frames(2);
            Assert.That(binding.Filter.sharedMesh,Is.Not.SameAs(original));
            Time.timeScale=1; anchor.position+=Vector3.up*.002f; yield return Frames(2);
            Assert.That(binding.Filter.sharedMesh.vertices,Is.Not.EqualTo(first));
            binding.Neighbour.RuntimeState.SetLoose(binding.Neighbour.transform.position,binding.Neighbour.transform.rotation);
            yield return Frames(2);
            Assert.That(binding.Filter.sharedMesh,Is.SameAs(original));
            Assert.That(simulation.FixedTickCount,Is.Zero);
        }
    }
}
