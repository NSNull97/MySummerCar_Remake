using System;
using System.IO;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaNativeStartSmokeTests
    {
        [Test]
        public void TightenedCopyUsesAuthoredMaximumOnlyForOccupiedMountAndPreservesSource()
        {
            var root = new GameObject("Test-only native snapshot copying");
            var fastener = ScriptableObject.CreateInstance<FastenerDefinition>();
            var definition = ScriptableObject.CreateInstance<MountPointDefinition>();
            try
            {
                fastener.Configure("fastener.test", "Test", FastenerSize.Millimeter10, 3,
                    FastenerDirection.ClockwiseToTighten, true, true, new ToolCompatibilityRule());
                definition.Configure("definition.test", "Test", "test", "part.owner", Array.Empty<string>(),
                    new MountConstraint(1f, 180f, 1f, 0f), 0f, new[] { fastener });
                definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(new[] { fastener }));
                var occupied = root.AddComponent<MountPointAuthoring>();
                occupied.Configure(definition, "mount.occupied", root.transform, 0);
                var emptyObject = new GameObject("Empty test mount");
                emptyObject.transform.SetParent(root.transform, false);
                var empty = emptyObject.AddComponent<MountPointAuthoring>();
                empty.Configure(definition, "mount.empty", emptyObject.transform, 0);
                var source = new VehicleAssemblySaveData
                {
                    mounts = new[]
                    {
                        new MountSaveDto { mountId = occupied.MountId, installedPartStableEntityId = "test-part" },
                        new MountSaveDto { mountId = empty.MountId },
                    },
                    fasteners = new[]
                    {
                        new FastenerSaveDto { mountId = occupied.MountId, fastenerDefinitionId = fastener.DefinitionId, stage = 1 },
                        new FastenerSaveDto { mountId = empty.MountId, fastenerDefinitionId = fastener.DefinitionId },
                    },
                    fastenerGroups = new[]
                    {
                        new FastenerGroupSaveDto { mountId = occupied.MountId },
                        new FastenerGroupSaveDto { mountId = empty.MountId },
                    },
                };
                string before = JsonUtility.ToJson(source);
                var copy = Phase1SatsumaNativeStartSmoke.CreateTightenedCopy(source, new[] { occupied, empty });
                Assert.That(copy.fasteners[0].stage, Is.EqualTo(3));
                Assert.That(copy.fasteners[0].inserted && copy.fasteners[0].seated, Is.True);
                Assert.That(copy.fastenerGroups[0].isBolted, Is.True);
                Assert.That(copy.fasteners[1].stage, Is.Zero);
                Assert.That(copy.fasteners[1].inserted || copy.fasteners[1].seated || copy.fastenerGroups[1].isBolted, Is.False);
                Assert.That(copy.fasteners[0], Is.Not.SameAs(source.fasteners[0]));
                Assert.That(copy.mounts[0], Is.Not.SameAs(source.mounts[0]));
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(fastener);
            }
        }

        [Test]
        public void MissingVehicleDomainIsRejectedWithoutSynthesizingState()
        {
            Assert.Throws<InvalidDataException>(() => Phase1SatsumaNativeStartSmoke.ReadVehicleRecord("{\"Domains\":[]}"));
        }

        [Test]
        public void ExplicitNativeSnapshotSmokePreservesFileAndExercisesStartFailureBoundaries()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-engineSavePath");
            if (index < 0 || index + 1 >= args.Length) Assert.Ignore("Opt-in real native snapshot: pass -engineSavePath after the scoped night refresh.");
            var report = Phase1SatsumaNativeStartSmoke.RunFile(args[index + 1]);
            Assert.That(report.originalRestorePassed && report.sourceDtoUnchanged, Is.True);
            Assert.That(report.syntheticPlugs, Is.EqualTo(4));
            Assert.That(report.observedCranking && report.reachedRunning, Is.True, JsonUtility.ToJson(report));
            Assert.That(report.fuelPumpLossStalled, Is.True, JsonUtility.ToJson(report));
            Assert.That(report.emptyEngineCannotRun, Is.True, JsonUtility.ToJson(report));
        }
    }
}
