using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CanonicalConsumableNativeSaveTests
    {
        [Test]
        public void ReadOnlyNativeNinePurchasesRestoreIntoCurrentEngineWithoutServicingOldSave()
        {
            WithReadOnlyNativeEngine(document =>
            {
                VehicleSaveRecordDto old = EngineVehicle(document);
                Assert.That(old.assembly.fasteners, Has.Length.EqualTo(302));
                Assert.That(old.simulation.hasSatsumaOperatingState, Is.False);
                Assert.That(old.assembly.parts.Any(part => part.hasMechanicalCondition || part.hasServiceCaps || part.hasValveAdjustment), Is.False);
                using var fixture = EngineFixture();
                RestoreEngineDocument(fixture, document);
                VehicleSaveRecordDto current = EngineVehicle(NewDocument(fixture.Registry.CaptureDomains()));
                AssertNativePurchases(fixture, document, current);
                AssertExistingServiceValues(old, current);
                Assert.That(current.assembly.fasteners, Has.Length.EqualTo(294));
                PartSaveDto[] health = current.assembly.parts.Where(part => part.hasMechanicalCondition).ToArray();
                Assert.That(health, Has.Length.EqualTo(9));
                Assert.That(health.All(part => part.mechanicalCondition.conditionPercent == 100f && !part.mechanicalCondition.broken), Is.True);
                Assert.That(current.assembly.parts.Where(part => part.hasServiceCaps).SelectMany(part => part.serviceCaps.angles),
                    Is.EquivalentTo(Enumerable.Repeat(359f, 5)));
                PartSaveDto rocker = current.assembly.parts.Single(part => part.hasValveAdjustment);
                Assert.That(rocker.valveAdjustment.intake, Is.EqualTo(Vector4.one * 7f));
                Assert.That(rocker.valveAdjustment.exhaust, Is.EqualTo(Vector4.one * 6f));
                Assert.That(current.simulation.hasSatsumaOperatingState, Is.True);
                Assert.That(current.simulation.satsumaOperatingState.brakeFrontLiters, Is.Zero);
                Assert.That(current.simulation.satsumaOperatingState.brakeRearLiters, Is.Zero);
                Assert.That(current.simulation.satsumaOperatingState.clutchLiters, Is.Zero);
                Assert.That(fixture.Deferred.Snapshot(), Is.Empty);
            });
        }

        [Test]
        public void CurrentLiveEngineExtensionsAndPurchasesSurviveUnavailableOwnerThenFreshPresentation()
        {
            WithReadOnlyNativeEngine(document =>
            {
                var codec = new SaveDocumentCodec();
                SaveDocument extended;
                using (var source = EngineFixture())
                {
                    RestoreEngineDocument(source, document);
                    extended = NewDocument(source.Registry.CaptureDomains());
                }
                VehicleSaveRecordDto modified = EngineVehicle(extended);
                int index = 0;
                foreach (PartSaveDto part in modified.assembly.parts)
                {
                    if (part.hasMechanicalCondition) part.mechanicalCondition.conditionPercent = 31f + index++ * 6f;
                    if (part.hasServiceCaps)
                        for (int cap = 0; cap < part.serviceCaps.angles.Length; cap++)
                            part.serviceCaps.angles[cap] = cap == 0 ? 1f : 227f;
                    if (part.hasValveAdjustment)
                    {
                        part.valveAdjustment.intake = new Vector4(6.1f, 6.4f, 7.3f, 7.6f);
                        part.valveAdjustment.exhaust = new Vector4(5.1f, 5.4f, 6.3f, 6.6f);
                    }
                }
                modified.simulation.satsumaOperatingState = new SatsumaOperatingSaveDto
                {
                    brakeFrontLiters = .81f, brakeRearLiters = .57f, clutchLiters = .32f,
                    oilContaminationPercent = 37f, oilPressureBar = 2.3f, coolantPressurePsi = 8f,
                    crankingSeconds = 1.25f, radiatorFanRunning = true,
                    hasOdometerState = true, odometerTenKilometerUnits = 12345, odometerPartialMeters = 6789.25,
                };
                EngineEnvelope(extended).PayloadJson = SaveParticipantJson.Serialize(new VehicleDomainSaveDto { vehicles = new[] { modified } });
                extended = codec.Deserialize(codec.Serialize(extended), requireCurrentVersion: true);
                using var deferred = EngineFixture(deferVehicle: true);
                RestoreEngineDocument(deferred, extended);
                Assert.That(deferred.Runtime.LoadedInstances, Is.Empty, "An unloaded purchase cell must not materialize aggregate-owned parts.");
                Assert.That(deferred.Deferred.Snapshot().Count(value => value.OwnerDomainId == ItemSaveParticipant.DomainId), Is.EqualTo(9));
                Assert.That(deferred.Deferred.Snapshot().Count(value => value.OwnerDomainId == VehicleSaveParticipant.DomainId), Is.EqualTo(1));
                SaveDocument unavailable = codec.Deserialize(codec.Serialize(NewDocument(deferred.Registry.CaptureDomains())), requireCurrentVersion: true);
                AssertEngineExtensions(modified, EngineVehicle(unavailable));
                using var restored = EngineFixture();
                RestoreEngineDocument(restored, unavailable);
                VehicleSaveRecordDto captured = EngineVehicle(NewDocument(restored.Registry.CaptureDomains()));
                AssertEngineExtensions(modified, captured);
                AssertExistingServiceValues(modified, captured);
                AssertNativePurchases(restored, document, captured);
                Assert.That(restored.Deferred.Snapshot(), Is.Empty);
                // The same unavailable aggregate can also be registered later, without
                // loading the purchase cell or inventing a second physical owner.
                deferred.Vehicles.RegisterHierarchy(deferred.Persistence.gameObject);
                Assert.That(deferred.Runtime.LoadedInstances.Count(), Is.EqualTo(9));
                Assert.That(deferred.Deferred.Snapshot(), Is.Empty);
                AssertEngineExtensions(modified, EngineVehicle(NewDocument(deferred.Registry.CaptureDomains())));
            });
        }

        private static Fixture EngineFixture(bool deferVehicle = false) => new("item.spark-plug",
            "mount.satsuma.cylinder-head.spark-plug-1", allEngineConsumables: true, deferVehicle: deferVehicle);

        private static void WithReadOnlyNativeEngine(Action<SaveDocument> assertion)
        {
            string[] args = Environment.GetCommandLineArgs();
            int argument = Array.IndexOf(args, "-liveEngineSavePath");
            if (argument < 0 || argument + 1 >= args.Length) Assert.Ignore("Opt-in read-only native integration requires -liveEngineSavePath.");
            string path = Path.GetFullPath(args[argument + 1]);
            byte[] original = File.ReadAllBytes(path);
            IItemPresentationProvider previousHub = ItemPresentationProviderHub.Current;
            try
            {
                SaveDocument full = new SaveDocumentCodec().Deserialize(System.Text.Encoding.UTF8.GetString(original), requireCurrentVersion: true);
                VehicleSaveRecordDto vehicle = EngineVehicle(full);
                var ids = new HashSet<string>(vehicle.assembly.dynamicParts.Select(value => value.part.stableEntityId), StringComparer.Ordinal);
                Assert.That(ids.Count, Is.EqualTo(9), "This explicitly selected native fixture must contain all nine purchased descriptors.");
                ItemDomainSaveDto items = SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    full.Domains.Single(value => value.DomainId == ItemSaveParticipant.DomainId).PayloadJson);
                WorldEntityDomainSaveDto world = SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                    full.Domains.Single(value => value.DomainId == WorldEntitySaveParticipant.DomainId).PayloadJson);
                items.instances = items.instances.Where(value => ids.Contains(value.state.stableEntityId)).ToArray();
                world.entities = world.entities.Where(value => ids.Contains(value.stableEntityId)).ToArray();
                Assert.That(items.instances, Has.Length.EqualTo(9));
                Assert.That(world.entities, Is.Empty, "The current native save must already use aggregate physical ownership.");
                // Verify the whole native document's integrity above. Restore only the
                // three in-scope domains into isolated real services, never user storage.
                assertion(NewDocument(new[]
                {
                    CopyEnvelope(full, VehicleSaveParticipant.DomainId, EngineEnvelope(full).PayloadJson),
                    CopyEnvelope(full, ItemSaveParticipant.DomainId, SaveParticipantJson.Serialize(items)),
                    CopyEnvelope(full, WorldEntitySaveParticipant.DomainId, SaveParticipantJson.Serialize(world)),
                }));
                using var sha = SHA256.Create();
                TestContext.WriteLine("LIVE_ENGINE_NATIVE_READ_ONLY sha256=" + BitConverter.ToString(sha.ComputeHash(original)).Replace("-", "") +
                    " domains=vehicle/items/world purchases=9 nativeWrites=false");
            }
            finally
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original), "The selected native save must remain byte-for-byte untouched.");
                Assert.That(ItemPresentationProviderHub.Current, Is.SameAs(previousHub));
            }
        }

        private static SaveDomainEnvelope CopyEnvelope(SaveDocument source, string id, string payload)
        {
            SaveDomainEnvelope envelope = source.Domains.Single(value => value.DomainId == id);
            return new SaveDomainEnvelope { DomainId = id, SchemaVersion = envelope.SchemaVersion, PayloadJson = payload };
        }
        private static SaveDomainEnvelope EngineEnvelope(SaveDocument document) =>
            document.Domains.Single(value => value.DomainId == VehicleSaveParticipant.DomainId);
        private static VehicleSaveRecordDto EngineVehicle(SaveDocument document) =>
            SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(EngineEnvelope(document).PayloadJson).vehicles.Single();
        private static void RestoreEngineDocument(Fixture fixture, SaveDocument document)
        {
            string before = new SaveDocumentCodec().Serialize(document);
            var unresolved = new UnresolvedContentReport();
            PreparedSaveRestore prepared = fixture.Registry.PrepareRestore(document, unresolved, fixture.Deferred);
            Assert.That(fixture.Runtime.LoadedInstances, Is.Empty, "Preflight must not materialize purchases.");
            fixture.Registry.ApplyRestore(prepared, unresolved, fixture.Deferred);
            Assert.That(new SaveDocumentCodec().Serialize(document), Is.EqualTo(before));
        }
        private static void AssertExistingServiceValues(VehicleSaveRecordDto expected, VehicleSaveRecordDto actual)
        {
            Assert.That(actual.simulation.oilLiters, Is.EqualTo(expected.simulation.oilLiters));
            Assert.That(actual.simulation.coolantLiters, Is.EqualTo(expected.simulation.coolantLiters));
            Assert.That(actual.simulation.fuelLiters, Is.EqualTo(expected.simulation.fuelLiters));
            Assert.That(actual.simulation.batteryVoltage, Is.EqualTo(expected.simulation.batteryVoltage));
        }
        private static void AssertEngineExtensions(VehicleSaveRecordDto expected, VehicleSaveRecordDto actual)
        {
            Assert.That(actual.simulation.hasSatsumaOperatingState, Is.True);
            Assert.That(JsonUtility.ToJson(actual.simulation.satsumaOperatingState), Is.EqualTo(JsonUtility.ToJson(expected.simulation.satsumaOperatingState)));
            foreach (PartSaveDto part in expected.assembly.parts)
            {
                PartSaveDto restored = actual.assembly.parts.Single(value => value.stableEntityId == part.stableEntityId);
                Assert.That(restored.hasMechanicalCondition, Is.EqualTo(part.hasMechanicalCondition));
                Assert.That(restored.hasServiceCaps, Is.EqualTo(part.hasServiceCaps));
                Assert.That(restored.hasValveAdjustment, Is.EqualTo(part.hasValveAdjustment));
                if (part.hasMechanicalCondition) Assert.That(JsonUtility.ToJson(restored.mechanicalCondition), Is.EqualTo(JsonUtility.ToJson(part.mechanicalCondition)));
                if (part.hasServiceCaps) Assert.That(JsonUtility.ToJson(restored.serviceCaps), Is.EqualTo(JsonUtility.ToJson(part.serviceCaps)));
                if (part.hasValveAdjustment) Assert.That(JsonUtility.ToJson(restored.valveAdjustment), Is.EqualTo(JsonUtility.ToJson(part.valveAdjustment)));
            }
        }
        private static void AssertNativePurchases(Fixture fixture, SaveDocument original, VehicleSaveRecordDto captured)
        {
            ItemDomainSaveDto items = SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                original.Domains.Single(value => value.DomainId == ItemSaveParticipant.DomainId).PayloadJson);
            Assert.That(fixture.Runtime.LoadedInstances.Count(), Is.EqualTo(9));
            Assert.That(captured.assembly.parts, Has.Length.EqualTo(126));
            Assert.That(captured.assembly.dynamicParts, Has.Length.EqualTo(9));
            foreach (DynamicAssemblyPartSaveDto descriptor in EngineVehicle(original).assembly.dynamicParts)
            {
                string id = descriptor.part.stableEntityId;
                Assert.That(fixture.Runtime.TryGetInstance(id, out WorldItemInstance item), Is.True);
                PartInstance part = item.GetComponent<PartInstance>();
                Assert.That(part, Is.Not.Null);
                Assert.That(fixture.Assembly.AllRuntimeParts.Count(value => value.StableId.Value == id), Is.EqualTo(1));
                Assert.That(part.IsInstalled, Is.EqualTo(descriptor.part.lifecycleState == PartLifecycleState.Installed));
                Assert.That(part.RuntimeState.InstalledMountId, Is.EqualTo(descriptor.part.installedMountId));
                ItemRuntimeSaveRecord expected = items.instances.Single(value => value.state.stableEntityId == id);
                Assert.That(item.ConditionPercent, Is.EqualTo(expected.state.condition));
                Assert.That(fixture.Runtime.TryGetSourceCellId(id, out string cell), Is.True);
                Assert.That(cell, Is.EqualTo(expected.sourceCellId));
                Assert.That(fixture.World.TryResolve(id, out _), Is.False);
                Assert.That(item.PresentationRoot, Is.Not.Null);
                Assert.That(item.GetComponentsInChildren<ItemProxyPresentation>(true), Is.Empty);
                Assert.That(item.PresentationRoot.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThan(0));
                if (descriptor.itemDefinitionId is "item.spark-plug" or "item.light-bulb")
                    AssertGeneratedPresentation(descriptor.itemDefinitionId, item.PresentationRoot);
            }
        }
    }
}
