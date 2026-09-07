using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Cover = MSC.Vehicle.Assembly.SatsumaRockerCoverFastenerMigration;
using Carb = MSC.Vehicle.Assembly.SatsumaCarburetorFastenerMigration;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class CanonicalHeadlightFastenerSaveTests
    {
        private static readonly string[] BodySlugs =
            { "exhaust-pipe", "exhaust-muffler", "fuel-tank", "seat-driver", "seat-passenger", "seat-rear" };

        internal static bool IsHeadlight(string mountId) =>
            mountId == "mount.satsuma.headlight-left" || mountId == "mount.satsuma.headlight-right";
        private static bool IsBodyAddition(string mountId) => BodySlugs.Any(slug => mountId == "mount.satsuma." + slug);
        private static bool IsSocket(string mountId) => mountId.Contains("spark-plug-") ||
            mountId.EndsWith(".alternator-belt", StringComparison.Ordinal) || mountId.EndsWith(".light-bulb", StringComparison.Ordinal);
        private static bool IsLower(string mountId) => mountId == "mount.satsuma.strut-fl" || mountId == "mount.satsuma.strut-fr";
        private static bool IsExterior(string mountId) => new[]
            { "bumper-front", "bumper-rear", "grille", "fender-left", "fender-right", "hood" }
            .Any(slug => mountId == "mount.satsuma." + slug);

        [TestCase(273, 1, false)] [TestCase(273, 2, false)] [TestCase(273, 3, false)]
        [TestCase(294, 3, false)] [TestCase(298, 3, false)]
        [TestCase(298, 3, true)] [TestCase(302, 3, true)]
        public void ExactRevisionsKeepExistingStagesAndOnlyAddLooseHeadlightBolts(int count, int schema, bool sockets)
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = fixture.Source(count, sockets);
            bool alreadyHasHeadlights = count == 302 || count == 298 && !sockets;
            source.schemaVersion = schema;
            if (schema == 1) source.fastenerGroups = Array.Empty<FastenerGroupSaveDto>();
            SetRightHeadlightLoose(source);
            FastenerSaveDto retained = source.fasteners.First(value => value.mountId == "test.mount.filler-0");
            retained.stage = 3;
            if (schema >= 2) source.fastenerGroups.Single(value => value.mountId == retained.mountId).isBolted = true;
            if (count >= 294)
            {
                foreach (FastenerSaveDto bolt in source.fasteners.Where(value => IsBodyAddition(value.mountId))) bolt.stage = 3;
                foreach (FastenerGroupSaveDto group in source.fastenerGroups.Where(value => IsBodyAddition(value.mountId))) group.isBolted = true;
            }
            if (alreadyHasHeadlights)
            {
                // Between OFF=0 and ON=2, a valid saved true latch must survive.
                source.fasteners.First(value => value.mountId == "mount.satsuma.headlight-left").stage = 1;
                source.fastenerGroups.Single(value => value.mountId == "mount.satsuma.headlight-left").isBolted = true;
            }
            string input = JsonUtility.ToJson(source);
            string checkpoint = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
            MountPointRuntime sameMount = fixture.Assembly.Graph.Mounts[0];
            AssemblyOperationResult valid = fixture.Assembly.ValidateSaveDataForRestore(source);
            Assert.That(valid.Succeeded, Is.True, valid.Message);
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(checkpoint));
            AssemblyOperationResult result = fixture.Assembly.RestoreSaveData(source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
            Assert.That(saved.parts.Select(value => value.stableEntityId), Is.EquivalentTo(source.parts.Select(value => value.stableEntityId)));
            Assert.That(saved.schemaVersion, Is.EqualTo(3));
            Assert.That(saved.mounts, Has.Length.EqualTo(124));
            Assert.That(saved.fastenerGroups, Has.Length.EqualTo(124));
            Assert.That(saved.fasteners, Has.Length.EqualTo(302));
            foreach (FastenerSaveDto previous in source.fasteners)
                Assert.That(JsonUtility.ToJson(saved.fasteners.Single(value => value.mountId == previous.mountId &&
                    value.fastenerDefinitionId == previous.fastenerDefinitionId)), Is.EqualTo(JsonUtility.ToJson(previous)));
            foreach (FastenerGroupSaveDto previous in source.fastenerGroups)
                Assert.That(saved.fastenerGroups.Single(value => value.mountId == previous.mountId).isBolted, Is.EqualTo(previous.isBolted));
            if (!alreadyHasHeadlights) AssertNewHeadlights(saved);
            if (count == 273)
            {
                Assert.That(saved.fasteners.Where(value => IsBodyAddition(value.mountId)).All(value => value.stage == 0), Is.True);
                Assert.That(saved.fasteners.Where(value => IsSocket(value.mountId)).All(value => !value.inserted && !value.seated && value.stage == 0), Is.True);
                Assert.That(saved.mounts.Where(value => IsSocket(value.mountId)).All(value => string.IsNullOrEmpty(value.installedPartStableEntityId)), Is.True);
            }
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(input));
            Assert.That(fixture.Assembly.Graph.Mounts[0], Is.SameAs(sameMount));
            string current = JsonUtility.ToJson(saved);
            Assert.That(fixture.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(current));
        }

        [TestCase(280)] [TestCase(274)] [TestCase(252)]
        [TestCase(260)] [TestCase(245)] [TestCase(253)]
        public void HeadlightsComposeWithRetirementsAndHistoricalStrutAndExteriorAdditions(int count)
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = fixture.Source(count);
            string original = JsonUtility.ToJson(source);
            AssemblyOperationResult result = fixture.Assembly.RestoreSaveData(source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            VehicleAssemblySaveData current = fixture.Assembly.CaptureSaveData();
            Assert.That(current.fasteners, Has.Length.EqualTo(302));
            AssertNewHeadlights(current);
            Assert.That(current.fasteners.Where(value => IsBodyAddition(value.mountId)).All(value => value.stage == 0), Is.True);
            if (count is 252 or 245)
                Assert.That(current.fasteners.Where(value => IsLower(value.mountId)).All(value => value.stage == 0), Is.True);
            if (count is 252 or 260 or 245 or 253)
                Assert.That(current.fasteners.Where(value => IsExterior(value.mountId)).All(value => value.stage == 8), Is.True);
            Assert.That(current.fasteners.Any(value => Cover.RetiredIds.Contains(value.fastenerDefinitionId) || value.fastenerDefinitionId == Carb.RetiredId), Is.False);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
        }

        [TestCase("partial-1")] [TestCase("partial-2")] [TestCase("partial-3")]
        [TestCase("naked-four")] [TestCase("duplicate-head")] [TestCase("unknown-head")]
        [TestCase("wrong-old-id")] [TestCase("old-bolted-group")]
        public void MalformedHeadlightRevisionRejectsWithoutChangingSourceOrRuntime(string corruption)
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = fixture.Source(corruption == "naked-four" ? 273 :
                corruption == "duplicate-head" || corruption == "unknown-head" ? 302 : 298);
            FastenerSaveDto[] heads = fixture.Assembly.CaptureSaveData().fasteners.Where(value => IsHeadlight(value.mountId)).ToArray();
            if (corruption.StartsWith("partial-", StringComparison.Ordinal))
                source.fasteners = source.fasteners.Concat(heads.Take(int.Parse(corruption.Substring(8)))).ToArray();
            else if (corruption == "naked-four") source.fasteners = source.fasteners.Concat(heads).ToArray();
            else if (corruption == "duplicate-head")
                source.fasteners.First(value => IsHeadlight(value.mountId)).fastenerDefinitionId = "fastener.satsuma.headlight-left.boltpm-2";
            else if (corruption == "unknown-head") source.fasteners.First(value => IsHeadlight(value.mountId)).fastenerDefinitionId = "unknown.headlight.bolt";
            else if (corruption == "wrong-old-id") source.fasteners.First(value => value.mountId == "test.mount.filler-0").fastenerDefinitionId = "unknown.old.bolt";
            else source.fastenerGroups.Single(value => value.mountId == "mount.satsuma.headlight-left").isBolted = true;
            string input = JsonUtility.ToJson(source);
            string checkpoint = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
            MountPointRuntime[] sameMounts = fixture.Assembly.Graph.Mounts.ToArray();
            Assert.That(fixture.Assembly.ValidateSaveDataForRestore(source).Succeeded, Is.False);
            Assert.That(fixture.Assembly.RestoreSaveData(source).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(input));
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(checkpoint));
            Assert.That(fixture.Assembly.Graph.Mounts, Is.EqualTo(sameMounts));
        }

        internal static void AssertNewHeadlights(VehicleAssemblySaveData data)
        {
            FastenerSaveDto[] heads = data.fasteners.Where(value => IsHeadlight(value.mountId)).ToArray();
            Assert.That(heads, Has.Length.EqualTo(4));
            foreach (FastenerSaveDto bolt in heads)
            {
                bool occupied = !string.IsNullOrEmpty(data.mounts.Single(value => value.mountId == bolt.mountId).installedPartStableEntityId);
                Assert.That(bolt.inserted, Is.EqualTo(occupied));
                Assert.That(bolt.seated, Is.EqualTo(occupied));
                Assert.That(bolt.stage, Is.Zero);
            }
            Assert.That(data.fastenerGroups.Where(value => IsHeadlight(value.mountId)).Select(value => value.isBolted), Is.EqualTo(new[] { false, false }));
        }

        [TestCase("wrong-tool")] [TestCase("wrong-size")] [TestCase("wrong-max-stage")]
        [TestCase("wrong-group-on")] [TestCase("duplicate-group")]
        public void UnreviewedHeadlightTargetContractRejectsBeforeChangingSourceOrRuntime(string corruption)
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = fixture.Source(298);
            Assert.That(fixture.Assembly.Graph.TryGetMount("mount.satsuma.headlight-left", out MountPointRuntime mount), Is.True);
            FastenerDefinition bolt = mount.Fasteners[0].Definition;
            if (corruption == "wrong-group-on" || corruption == "duplicate-group")
            {
                string[] ids = mount.Fasteners.Select(value => value.Definition.DefinitionId).ToArray();
                if (corruption == "duplicate-group") ids[1] = ids[0];
                mount.FastenerGroup.Definition.Configure(ids, 16, corruption == "wrong-group-on" ? 1 : 2, 0);
            }
            else
            {
                FastenerSize size = corruption == "wrong-size" ? FastenerSize.Millimeter8 : bolt.Size;
                bolt.Configure(bolt.DefinitionId, bolt.DisplayName, size, corruption == "wrong-max-stage" ? 9 : 8,
                    bolt.TighteningDirection, bolt.InsertedOnInstall, bolt.RequiredForRemoval,
                    ToolCompatibilityRule.Create(corruption == "wrong-tool" ? "WrongTool" : "Wrench", size));
            }
            string input = JsonUtility.ToJson(source);
            string checkpoint = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
            Assert.That(fixture.Assembly.ValidateSaveDataForRestore(source).Succeeded, Is.False);
            Assert.That(fixture.Assembly.RestoreSaveData(source).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(input));
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(checkpoint));
            Assert.That(fixture.Assembly.Graph.TryGetMount(mount.MountId, out MountPointRuntime sameMount), Is.True);
            Assert.That(sameMount, Is.SameAs(mount));
        }

        private static void SetRightHeadlightLoose(VehicleAssemblySaveData source)
        {
            MountSaveDto mount = source.mounts.Single(value => value.mountId == "mount.satsuma.headlight-right");
            PartSaveDto part = source.parts.Single(value => value.stableEntityId == mount.installedPartStableEntityId);
            mount.installedPartStableEntityId = string.Empty;
            part.installedMountId = string.Empty;
            part.lifecycleState = PartLifecycleState.Loose;
            foreach (FastenerSaveDto bolt in source.fasteners.Where(value => value.mountId == mount.mountId))
            { bolt.inserted = false; bolt.seated = false; bolt.stage = 0; }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Headlight content migration fixture");
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            private readonly List<PartInstance> parts = new List<PartInstance>();
            private readonly List<MountPointAuthoring> mounts = new List<MountPointAuthoring>();
            public VehicleAssemblyController Assembly { get; }

            public Fixture()
            {
                Part("test.body", true, string.Empty);
                AddMount(Cover.MountId, "test.cover", Cover.CanonicalIds, FastenerSize.Millimeter7, 2);
                AddMount(Carb.MountId, "vehicle.satsuma.part.carburetor", Carb.CanonicalIds, FastenerSize.Millimeter8, 8);
                foreach (string side in new[] { "fl", "fr" })
                    AddMount("mount.satsuma.strut-" + side, "test.strut." + side,
                        Enumerable.Range(1, 4).Select(value => "fastener.satsuma.strut-" + side + ".lower-" + value).ToArray());
                foreach (string panel in new[] { "bumper-front", "bumper-rear", "grille", "fender-left", "fender-right", "hood" })
                {
                    int count = panel.StartsWith("fender", StringComparison.Ordinal) ? 5 : panel == "hood" ? 4 : 2;
                    AddMount("mount.satsuma." + panel, "test.panel." + panel,
                        Enumerable.Range(1, count).Select(value => "fastener.satsuma." + panel + ".boltpm-" + value).ToArray());
                }
                int[] counts = { 3, 1, 7, 4, 4, 2 };
                int[] thresholds = { 6, 2, 12, 7, 7, 6 };
                FastenerSize[] sizes = { FastenerSize.Millimeter7, FastenerSize.Millimeter7, FastenerSize.Millimeter11,
                    FastenerSize.Millimeter9, FastenerSize.Millimeter9, FastenerSize.Millimeter9 };
                for (int index = 0; index < BodySlugs.Length; index++)
                {
                    string slug = BodySlugs[index];
                    AddMount("mount.satsuma." + slug, "vehicle.satsuma.part." + slug,
                        Enumerable.Range(1, counts[index]).Select(value => "fastener.satsuma." + slug + ".boltpm-" + value).ToArray(), sizes[index], thresholds[index]);
                }
                foreach (string side in new[] { "left", "right" })
                    AddMount("mount.satsuma.headlight-" + side, "vehicle.satsuma.part.headlight-" + side,
                        Enumerable.Range(1, 2).Select(value => "fastener.satsuma.headlight-" + side + ".boltpm-" + value).ToArray(), FastenerSize.Millimeter7, 2);
                for (int index = 0; index < 99; index++)
                    AddMount("test.mount.filler-" + index, "test.filler-" + index,
                        index == 0 ? Enumerable.Range(0, 235).Select(value => "test.fastener.filler-" + value).ToArray() : Array.Empty<string>());
                while (parts.Count < 126) Part("test.loose-" + parts.Count, false, string.Empty);
                for (int index = 1; index <= 4; index++)
                    AddMount(SatsumaConsumableAssemblyRules.SparkPlugMountId(index), "vehicle.satsuma.part.spark-plug",
                        new[] { SatsumaConsumableAssemblyRules.SparkPlugFastenerId(index) }, FastenerSize.None,
                        installPart: false, tool: SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchType);
                foreach (string id in new[] { "mount.satsuma.engine-block.alternator-belt", "mount.satsuma.headlight-left.light-bulb", "mount.satsuma.headlight-right.light-bulb" })
                    AddMount(id, "test.consumable", Array.Empty<string>(), installPart: false);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts.ToArray(), mounts.ToArray(), Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform, true);
                foreach (MountPointRuntime mount in Assembly.Graph.Mounts)
                {
                    foreach (FastenerInstance bolt in mount.Fasteners) Assert.That(bolt.TryRestore(mount.IsOccupied, mount.IsOccupied, 0), Is.True);
                    Assert.That(mount.FastenerGroup.TryRestoreLatch(false, mount.IsOccupied), Is.True);
                }
                Assert.That(Assembly.Parts, Has.Length.EqualTo(126));
                Assert.That(Assembly.Graph.Mounts, Has.Length.EqualTo(124));
                Assert.That(Assembly.CaptureSaveData().fasteners, Has.Length.EqualTo(302));
            }

            public VehicleAssemblySaveData Source(int count, bool sockets = true)
            {
                VehicleAssemblySaveData source = Assembly.CaptureSaveData();
                var bolts = source.fasteners.ToList();
                if (count < 302 && !(count == 298 && !sockets)) bolts.RemoveAll(value => IsHeadlight(value.mountId));
                if (count <= 280)
                {
                    bolts.RemoveAll(value => IsBodyAddition(value.mountId) || IsSocket(value.mountId));
                    source.mounts = source.mounts.Where(value => !IsSocket(value.mountId)).ToArray();
                    source.fastenerGroups = source.fastenerGroups.Where(value => !IsSocket(value.mountId)).ToArray();
                }
                else if (!sockets)
                {
                    bolts.RemoveAll(value => IsSocket(value.mountId));
                    source.mounts = source.mounts.Where(value => !IsSocket(value.mountId)).ToArray();
                    source.fastenerGroups = source.fastenerGroups.Where(value => !IsSocket(value.mountId)).ToArray();
                }
                if (count is 280 or 252 or 260)
                    foreach (string id in Cover.RetiredIds) bolts.Add(new FastenerSaveDto { mountId = Cover.MountId, fastenerDefinitionId = id, inserted = true, seated = true });
                if (count is 280 or 274 or 252 or 260)
                    bolts.Add(new FastenerSaveDto { mountId = Carb.MountId, fastenerDefinitionId = Carb.RetiredId, inserted = true, seated = true });
                if (count is 252 or 245) bolts.RemoveAll(value => IsLower(value.mountId));
                if (count is 252 or 260 or 245 or 253) bolts.RemoveAll(value => IsExterior(value.mountId));
                source.fasteners = bolts.ToArray();
                Assert.That(source.fasteners, Has.Length.EqualTo(count), "The fixture must construct the exact historical identity set.");
                return source;
            }

            private void AddMount(string id, string partId, string[] boltIds, FastenerSize size = FastenerSize.Millimeter8,
                int threshold = 1, bool installPart = true, string tool = "Wrench")
            {
                if (installPart) Part(partId, false, id);
                FastenerDefinition[] definitions = boltIds.Select(boltId =>
                {
                    FastenerDefinition bolt = Asset<FastenerDefinition>();
                    bolt.Configure(boltId, "bolt", size, 8, FastenerDirection.ClockwiseToTighten, true, true, ToolCompatibilityRule.Create(tool, size));
                    return bolt;
                }).ToArray();
                MountPointDefinition definition = Asset<MountPointDefinition>();
                definition.Configure(id, id, "test.socket", "test.body", new[] { partId }, new MountConstraint(.1f, 30, 1, 0), 0, definitions);
                var group = new FastenerGroupDefinition();
                group.Configure(boltIds, boltIds.Length * 8, boltIds.Length == 0 ? 0 : threshold, 0);
                definition.ConfigureFastenerGroup(group);
                var go = new GameObject(id); go.transform.SetParent(root.transform, false);
                MountPointAuthoring mount = go.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, go.transform, 0);
                mounts.Add(mount);
            }

            private void Part(string id, bool isRoot, string mountId)
            {
                var go = new GameObject(id); go.transform.SetParent(root.transform, false);
                PartDefinition definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, 1, null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                StableEntityIdAuthoring identity = go.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                PartInstance part = go.AddComponent<PartInstance>();
                part.Configure(definition, identity, go.AddComponent<Rigidbody>(), null, isRoot, mountId);
                parts.Add(part);
            }

            private T Asset<T>() where T : ScriptableObject
            { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }

            public void Dispose()
            { Object.DestroyImmediate(root); foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset); }
        }
    }

    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        [Test]
        public void Native18WithTwoBoughtBulbsMigrates298To302WithoutRewiringOrReplacingOwners()
        {
            IItemPresentationProvider previousHub = ItemPresentationProviderHub.Current;
            var codec = new SaveDocumentCodec();
            string json;
            string electricalJson;
            string dynamicJson;
            string itemsJson;
            string leftHousingId;
            string rightHousingId;
            string[] mounts = { "mount.satsuma.headlight-left.light-bulb", "mount.satsuma.headlight-right.light-bulb" };
            StableEntityId[] ids = mounts.Select(value => ItemStableIdUtility.CreateDeterministic("test.headlight-native." + value)).ToArray();
            var originals = new WorldItemInstance[2];
            using (var source = new Fixture("item.light-bulb", mounts[0]))
            {
                PartInstance leftHousing = source.Assembly.Parts.Single(value => value.Definition.DefinitionId == "vehicle.satsuma.part.headlight-left");
                PartInstance rightHousing = source.Assembly.Parts.Single(value => value.Definition.DefinitionId == "vehicle.satsuma.part.headlight-right");
                leftHousingId = leftHousing.StableId.Value;
                rightHousingId = rightHousing.StableId.Value;
                Assert.That(leftHousing.IsInstalled, Is.False);
                Assert.That(rightHousing.IsInstalled, Is.False);
                MountPointAuthoring housingMount = source.Assembly.MountPoints.Single(value => value.MountId == "mount.satsuma.headlight-left");
                leftHousing.transform.SetPositionAndRotation(housingMount.Pose.position, housingMount.Pose.rotation);
                leftHousing.Body.position = housingMount.Pose.position;
                leftHousing.Body.rotation = housingMount.Pose.rotation;
                AssemblyOperationResult installedHousing = source.Assembly.TryInstall(leftHousing, housingMount);
                Assert.That(installedHousing.Succeeded, Is.True, installedHousing.Message);
                AssertHeadlightHousingOwnership(source, leftHousingId, rightHousingId);
                for (int index = 0; index < ids.Length; index++)
                {
                    MountPointAuthoring mount = source.Assembly.MountPoints.Single(value => value.MountId == mounts[index]);
                    WorldItemInstance item = source.Runtime.SpawnDynamic("item.light-bulb", ids[index], mount.Pose.position, mount.Pose.rotation, source.Scene, PurchaseCell);
                    originals[index] = item;
                    ItemInstanceState state = item.CaptureState(); state.condition = 41f + index * 17f; item.ApplyState(state);
                    PartInstance part = item.GetComponent<PartInstance>();
                    part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                    part.Body.position = mount.Pose.position; part.Body.rotation = mount.Pose.rotation;
                    AssemblyOperationResult installed = source.Assembly.TryInstall(part, mount);
                    Assert.That(installed.Succeeded, Is.True, installed.Message);
                }
                SatsumaElectricalSystem electrical = source.Assembly.GetComponent<SatsumaElectricalSystem>();
                Assert.That(electrical, Is.Not.Null);
                Assert.That(electrical.TryRestore(new SatsumaElectricalSaveDto
                {
                    installedConnectionIds = new[] { "BatteryHarness", "GroundBattery", "FrontLightsHarness", "HeadlightLeft" },
                    batteryPlusStage = 3, batteryMinusStage = 5,
                }, out string failure), Is.True, failure);
                SaveDocument document = NewDocument(source.Registry.CaptureDomains());
                Assert.That(document.Header.DocumentVersion, Is.EqualTo(18));
                SaveDomainEnvelope envelope = document.Domains.Single(value => value.DomainId == VehicleSaveParticipant.DomainId);
                VehicleDomainSaveDto vehicles = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(envelope.PayloadJson);
                VehicleSaveRecordDto vehicle = vehicles.vehicles.Single();
                Assert.That(vehicle.assembly.schemaVersion, Is.EqualTo(3));
                Assert.That(vehicle.assembly.fasteners, Has.Length.EqualTo(294));
                // Reconstruct the exact pre-valve 302 roster before removing the
                // historical headlight packet. Four missing bolts is not a 298
                // fixture when eight valve pseudo-bolts have since been retired.
                FastenerSaveDto rocker = vehicle.assembly.fasteners.First(value => value.mountId == SatsumaRockerShaftFastenerMigration.MountId);
                vehicle.assembly.fasteners = vehicle.assembly.fasteners.Concat(SatsumaRockerShaftFastenerMigration.RetiredIds.Select(id => new FastenerSaveDto
                {
                    mountId = SatsumaRockerShaftFastenerMigration.MountId, fastenerDefinitionId = id,
                    inserted = rocker.inserted, seated = rocker.seated, stage = 0,
                })).ToArray();
                Assert.That(vehicle.assembly.fasteners, Has.Length.EqualTo(302));
                vehicle.assembly.fasteners = vehicle.assembly.fasteners.Where(value => !CanonicalHeadlightFastenerSaveTests.IsHeadlight(value.mountId)).ToArray();
                Assert.That(vehicle.assembly.fasteners, Has.Length.EqualTo(298));
                Assert.That(vehicle.assembly.mounts, Has.Length.EqualTo(124));
                electricalJson = JsonUtility.ToJson(vehicle.electrical);
                dynamicJson = JsonUtility.ToJson(new VehicleAssemblySaveData { dynamicParts = vehicle.assembly.dynamicParts });
                itemsJson = document.Domains.Single(value => value.DomainId == ItemSaveParticipant.DomainId).PayloadJson;
                envelope.PayloadJson = SaveParticipantJson.Serialize(vehicles);
                json = codec.Serialize(document);
            }
            Assert.That(originals.All(value => value == null), Is.True);
            using (var restored = new Fixture("item.light-bulb", mounts[0]))
            {
                Assert.That(restored.Runtime.LoadedInstances, Is.Empty);
                SaveDocument document = codec.Deserialize(json, requireCurrentVersion: true);
                string untouched = codec.Serialize(document);
                var deferred = new DeferredStableEntityStore();
                var unresolved = new UnresolvedContentReport();
                PreparedSaveRestore prepared = restored.Registry.PrepareRestore(document, unresolved, deferred);
                Assert.That(restored.Runtime.LoadedInstances, Is.Empty);
                restored.Registry.ApplyRestore(prepared, unresolved, deferred);
                AssertHeadlightHousingOwnership(restored, leftHousingId, rightHousingId);
                Assert.That(codec.Serialize(document), Is.EqualTo(untouched));
                SaveDocument captured = NewDocument(restored.Registry.CaptureDomains());
                VehicleSaveRecordDto vehicle = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(
                    captured.Domains.Single(value => value.DomainId == VehicleSaveParticipant.DomainId).PayloadJson).vehicles.Single();
                Assert.That(captured.Header.DocumentVersion, Is.EqualTo(18));
                Assert.That(vehicle.assembly.schemaVersion, Is.EqualTo(3));
                Assert.That(vehicle.assembly.parts, Has.Length.EqualTo(126));
                Assert.That(vehicle.assembly.mounts, Has.Length.EqualTo(124));
                Assert.That(vehicle.assembly.fasteners, Has.Length.EqualTo(294));
                CanonicalHeadlightFastenerSaveTests.AssertNewHeadlights(vehicle.assembly);
                Assert.That(JsonUtility.ToJson(vehicle.electrical), Is.EqualTo(electricalJson));
                Assert.That(vehicle.electrical.installedConnectionIds, Does.Not.Contain("HeadlightRight"));
                Assert.That(JsonUtility.ToJson(new VehicleAssemblySaveData { dynamicParts = vehicle.assembly.dynamicParts }), Is.EqualTo(dynamicJson));
                Assert.That(captured.Domains.Single(value => value.DomainId == ItemSaveParticipant.DomainId).PayloadJson, Is.EqualTo(itemsJson));
                WorldEntityDomainSaveDto world = SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                    captured.Domains.Single(value => value.DomainId == WorldEntitySaveParticipant.DomainId).PayloadJson);
                for (int index = 0; index < ids.Length; index++)
                {
                    Assert.That(restored.Runtime.TryGetInstance(ids[index].Value, out WorldItemInstance item), Is.True);
                    Assert.That(restored.Runtime.TryGetSourceCellId(ids[index].Value, out string cell), Is.True);
                    Assert.That(cell, Is.EqualTo(PurchaseCell));
                    Assert.That(item.GetComponent<PartInstance>().StableId, Is.EqualTo(ids[index]));
                    Assert.That(restored.Assembly.Graph.TryGetMount(mounts[index], out MountPointRuntime mount), Is.True);
                    Assert.That(mount.InstalledPart, Is.SameAs(item.GetComponent<PartInstance>()));
                    Assert.That(world.entities.Any(value => value.stableEntityId == ids[index].Value), Is.False);
                    AssertGeneratedPresentation("item.light-bulb", item.PresentationRoot);
                }
                Assert.That(deferred.Snapshot(), Is.Empty);
            }
            Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
        }

        private static void AssertHeadlightHousingOwnership(Fixture fixture, string leftId, string rightId)
        {
            Assert.That(fixture.Assembly.Graph.TryGetPartByStableId(leftId, out PartInstance left), Is.True);
            Assert.That(fixture.Assembly.Graph.TryGetPartByStableId(rightId, out PartInstance right), Is.True);
            Assert.That(fixture.Assembly.Graph.TryGetMount("mount.satsuma.headlight-left", out MountPointRuntime leftMount), Is.True);
            Assert.That(fixture.Assembly.Graph.TryGetMount("mount.satsuma.headlight-right", out MountPointRuntime rightMount), Is.True);
            Assert.That(leftMount.InstalledPart, Is.SameAs(left));
            Assert.That(left.IsInstalled, Is.True);
            Assert.That(left.RuntimeState.InstalledMountId, Is.EqualTo(leftMount.MountId));
            Assert.That(rightMount.IsOccupied, Is.False);
            Assert.That(right.IsInstalled, Is.False);
            Assert.That(right.RuntimeState.InstalledMountId, Is.Empty);
            VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
            Assert.That(saved.parts.Single(value => value.stableEntityId == leftId).lifecycleState, Is.EqualTo(PartLifecycleState.Installed));
            Assert.That(saved.parts.Single(value => value.stableEntityId == rightId).lifecycleState, Is.EqualTo(PartLifecycleState.Loose));
            Assert.That(saved.mounts.Single(value => value.mountId == leftMount.MountId).installedPartStableEntityId, Is.EqualTo(leftId));
            Assert.That(saved.mounts.Single(value => value.mountId == rightMount.MountId).installedPartStableEntityId, Is.Empty);
        }
    }
}
