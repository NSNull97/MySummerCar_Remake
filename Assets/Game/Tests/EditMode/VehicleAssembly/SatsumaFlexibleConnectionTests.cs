using System;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaFlexibleConnectionTests
    {
        [Test]
        public void ReviewedInstalledEndsCloseAndFollowVibrationWithoutMovingPhysicalOrFarEnds()
        {
            using var f = new Fixture(physicalScene: true);
            var presenter = f.Root.GetComponent<SatsumaFlexibleConnectionPresenter>();
            Assert.That(presenter, Is.Not.Null); Assert.That(presenter.Bindings, Has.Length.EqualTo(3));
            var originals = presenter.Bindings.Select(b => b.Filter.sharedMesh).ToArray();
            var vertices = originals.Select(m => m.vertices).ToArray();
            string save = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            var vibration = f.Root.GetComponent<SatsumaEngineVisualVibration>();
            for (int frame = 0; frame < 2; frame++)
            {
                if (frame > 0) vibration.ApplyFrame(VehicleEngineStatus.Running, 2800f, .5f, .016f);
                presenter.RefreshOutputs(); Assert.That(presenter.OutOfRangeConnections, Is.Zero);
                for (int i = 0; i < originals.Length; i++)
                {
                    var b = presenter.Bindings[i];
                    Assert.That(b.Filter.sharedMesh, Is.Not.SameAs(originals[i]));
                    Vector3 delta = b.Filter.transform.InverseTransformPoint(b.Target.TransformPoint(b.TargetPoint)) - b.EndCenter;
                    Assert.That(delta.magnitude, Is.InRange(.005f, .025f));
                    var actual = b.Filter.sharedMesh.vertices;
                    int fixedCount = 0, movingCount = 0;
                    for (int v = 0; v < actual.Length; v++)
                    {
                        float distance = Vector3.Distance(vertices[i][v], b.EndCenter);
                        if (distance <= b.RigidRadius)
                        { Assert.That(Vector3.Distance(actual[v], vertices[i][v] + delta), Is.LessThan(1e-6f)); movingCount++; }
                        if (distance >= b.FadeRadius)
                        { Assert.That(actual[v], Is.EqualTo(vertices[i][v])); fixedCount++; }
                    }
                    Assert.That(movingCount, Is.GreaterThan(10)); Assert.That(fixedCount, Is.GreaterThan(10));
                    Assert.That(originals[i].vertices, Is.EqualTo(vertices[i]));
                }
                Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(save));
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) presenter.RefreshOutputs();
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            vibration.ResetPresentation(); presenter.ResetPresentation();
            for (int i = 0; i < originals.Length; i++) Assert.That(presenter.Bindings[i].Filter.sharedMesh, Is.SameAs(originals[i]));
        }

        [Test]
        public void DetachedNeighbourAndRemovedEngineNeverLeaveStretchedHoseOrOwnedMeshBehind()
        {
            using var f = new Fixture(physicalScene: true);
            var presenter = f.Root.GetComponent<SatsumaFlexibleConnectionPresenter>();
            var b = presenter.Bindings[0]; Mesh original = b.Filter.sharedMesh;
            presenter.RefreshOutputs(); Assert.That(b.Filter.sharedMesh, Is.Not.SameAs(original));
            Assert.That(f.Assembly.TryBreakInstalledPart(b.Neighbour).Succeeded, Is.True);
            presenter.RefreshOutputs(); Assert.That(b.Filter.sharedMesh, Is.SameAs(original));
            var upper = presenter.Bindings[1]; Mesh originalUpper = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Meshes/7001391f8a7e9fe4e90aadc1d015fb85.asset");
            upper.Target.position += Vector3.right;
            presenter.RefreshOutputs(); Assert.That(presenter.OutOfRangeConnections, Is.GreaterThan(0));
            Assert.That(upper.Filter.sharedMesh, Is.SameAs(originalUpper));
        }
    }
}
