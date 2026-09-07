using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class SatsumaCockpitSteeringTests
    {
        [Test]
        public void CanonicalSteeringFollowsRawInputWithoutMovingPartMountOrCollisionAndResetsOnDetach()
        {
            var root=PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                var motion=root.GetComponent<SatsumaCockpitSteeringPresenter>();
                var assembly=root.GetComponent<VehicleAssemblyController>();
                Assert.That(motion,Is.Not.Null);
                Assert.That(motion.Bindings,Has.Length.EqualTo(10));
                var originals=motion.Bindings.Select(b=>(b.Leaf.localPosition,b.Leaf.localRotation)).ToArray();
                var owners=motion.Bindings.Select(b=>b.Owner).Distinct().ToArray();
                foreach(var owner in owners) owner.RuntimeState.SetInstalled("test.steering.mount",false);
                var poses=owners.Select(p=>(p.transform.position,p.transform.rotation)).ToArray();
                root.transform.SetPositionAndRotation(new Vector3(8,6,-4),Quaternion.Euler(12,74,8));
                poses=owners.Select(p=>(p.transform.position,p.transform.rotation)).ToArray();
                foreach(float steer in new[]{-1f,-.5f,0f,.5f,1f,0f})
                {
                    motion.ApplyFrame(steer);
                    Assert.That(motion.AppliedAngleDegrees,Is.EqualTo(steer*450f));
                    for(int i=0;i<owners.Length;i++)
                    {
                        Assert.That(owners[i].transform.position,Is.EqualTo(poses[i].position));
                        Assert.That(owners[i].transform.rotation,Is.EqualTo(poses[i].rotation));
                    }
                    for(int i=0;i<motion.Bindings.Length;i++)
                    {
                        var leaf=motion.Bindings[i].Leaf;
                        Assert.That(SatsumaEngineVisualVibration.IsSafeVisualLeaf(leaf),Is.True);
                        Quaternion delta=Quaternion.AngleAxis(steer*450f,root.transform.TransformDirection(motion.LocalAxis));
                        Quaternion expected=delta*leaf.parent.rotation*originals[i].localRotation;
                        Assert.That(Quaternion.Angle(leaf.rotation,expected),Is.LessThan(.1f));
                    }
                }
                motion.ApplyFrame(.3f); motion.RestoreVisuals();
                for(int i=0;i<motion.Bindings.Length;i++)
                {
                    Assert.That(motion.Bindings[i].Leaf.localPosition,Is.EqualTo(originals[i].localPosition));
                    Assert.That(motion.Bindings[i].Leaf.localRotation,Is.EqualTo(originals[i].localRotation));
                }
                foreach(var owner in owners) owner.RuntimeState.SetLoose(Vector3.zero,Quaternion.identity);
                motion.ApplyFrame(1f);
                for(int i=0;i<motion.Bindings.Length;i++) Assert.That(motion.Bindings[i].Leaf.localRotation,Is.EqualTo(originals[i].localRotation));
                Assert.That(Phase1SatsumaCockpitSteeringAuthoring.ApplyToInstance(assembly),Is.Zero);
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
