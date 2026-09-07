using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaDoorFastenerVisibilityAuthoring;
using Builder = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaBaselineBuilder;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaDoorFastenerVisibilityAuthoringTests
    {
        [Test]
        public void ExactEightOldDoorRenderersAreBoundAndHiddenWithoutChangingOtherTargetsOrPoses()
        {
            using var f = new Fixture();
            f.MakeLegacy();
            AssemblyFastenerInteractionTarget[] all = f.Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true);
            var unchangedTargets = all.Except(f.Doors).ToDictionary(value => value, EditorJsonUtility.ToJson);
            var unchangedRenderers = f.Root.GetComponentsInChildren<Renderer>(true)
                .Except(f.Visuals).ToDictionary(value => value, EditorJsonUtility.ToJson);
            var poses = f.Visuals.Select(value => (value.transform.parent, value.transform.localPosition,
                value.transform.localRotation, value.transform.localScale, value.GetComponent<MeshFilter>().sharedMesh)).ToArray();
            string[] definitions = f.Assembly.MountPoints.Select(value => EditorJsonUtility.ToJson(value.Definition)).ToArray();
            Assert.That(Authoring.ApplyToInstance(f.Assembly), Is.EqualTo(8));
            Assert.That(Authoring.ApplyToInstance(f.Assembly), Is.Zero);
            Assert.That(f.Doors.Select(value => value.InstalledOnlyExternalPresentationRenderer), Is.EqualTo(f.Visuals));
            Assert.That(f.Visuals.Select(value => value.enabled), Is.All.False);
            Assert.That(f.Visuals.Select(value => (value.transform.parent, value.transform.localPosition,
                value.transform.localRotation, value.transform.localScale, value.GetComponent<MeshFilter>().sharedMesh)), Is.EqualTo(poses));
            foreach (var row in unchangedTargets) Assert.That(EditorJsonUtility.ToJson(row.Key), Is.EqualTo(row.Value), row.Key.FastenerDefinitionId);
            foreach (var row in unchangedRenderers) Assert.That(EditorJsonUtility.ToJson(row.Key), Is.EqualTo(row.Value), row.Key.name);
            Assert.That(f.Assembly.MountPoints.Select(value => EditorJsonUtility.ToJson(value.Definition)), Is.EqualTo(definitions));
        }

        [Test]
        public void ExactPartiallyBoundDoorCohortRepairsOnlyTheMissingBinding()
        {
            using var f = new Fixture();
            Authoring.ApplyToInstance(f.Assembly);
            f.Doors[5].ConfigureInstalledOnlyExternalPresentationRenderer(null);
            f.Visuals[5].enabled = true;
            Assert.That(Authoring.ApplyToInstance(f.Assembly), Is.EqualTo(1));
            Assert.That(Authoring.ApplyToInstance(f.Assembly), Is.Zero);
        }

        [Test]
        public void ForeignExternalRendererRejectsBothDoorGroupsBeforeMutation()
        {
            using var f = new Fixture();
            f.MakeLegacy();
            f.Doors[7].ConfigureInstalledOnlyExternalPresentationRenderer(f.Visuals[0]);
            string[] targets = f.Doors.Select(EditorJsonUtility.ToJson).ToArray();
            Assert.Throws<InvalidDataException>(() => Authoring.ApplyToInstance(f.Assembly));
            Assert.That(f.Doors.Select(EditorJsonUtility.ToJson), Is.EqualTo(targets));
            Assert.That(f.Visuals.Select(value => value.enabled), Is.All.True);
        }

        [Test]
        public void ForeignPresentationParentRejectsBeforeHidingAnyDoorBolt()
        {
            using var f = new Fixture();
            f.MakeLegacy();
            f.Visuals[7].transform.SetParent(f.Doors[7].transform, true);
            Assert.Throws<InvalidDataException>(() => Authoring.ApplyToInstance(f.Assembly));
            Assert.That(f.Doors.Select(value => value.InstalledOnlyExternalPresentationRenderer), Is.All.Null);
            Assert.That(f.Visuals.Select(value => value.enabled), Is.All.True);
        }

        [Test]
        public void DuplicateDoorTargetRejectsBeforeMutation()
        {
            using var f = new Fixture();
            f.MakeLegacy();
            UnityEngine.Object.Instantiate(f.Doors[7].gameObject, f.Doors[7].transform.parent);
            Assert.Throws<InvalidDataException>(() => Authoring.ApplyToInstance(f.Assembly));
            Assert.That(f.Doors.Select(value => value.InstalledOnlyExternalPresentationRenderer), Is.All.Null);
            Assert.That(f.Visuals.Select(value => value.enabled), Is.All.True);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly VehicleAssemblyController Assembly;
            public readonly AssemblyFastenerInteractionTarget[] Doors;
            public readonly Renderer[] Visuals;

            public Fixture()
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(Builder.RuntimePrefabPath) == null)
                    Assert.Ignore("Private canonical donor-derived vehicle has not been generated on this machine.");
                Root = PrefabUtility.LoadPrefabContents(Builder.RuntimePrefabPath);
                Assembly = Root.GetComponent<VehicleAssemblyController>();
                Doors = Assembly.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Where(value => Authoring.DoorMountIds.Contains(value.MountId))
                    .OrderBy(value => value.FastenerDefinitionId, StringComparer.Ordinal).ToArray();
                Assert.That(Doors, Has.Length.EqualTo(8));
                Visuals = Doors.Select(value => value.GetComponent<InteractionTargetHost>().OutlineRenderers.Single()).ToArray();
            }

            public void MakeLegacy()
            {
                for (int index = 0; index < Doors.Length; index++)
                {
                    Doors[index].ConfigureInstalledOnlyExternalPresentationRenderer(null);
                    Visuals[index].enabled = true;
                }
            }

            public void Dispose() => PrefabUtility.UnloadPrefabContents(Root);
        }
    }
}
