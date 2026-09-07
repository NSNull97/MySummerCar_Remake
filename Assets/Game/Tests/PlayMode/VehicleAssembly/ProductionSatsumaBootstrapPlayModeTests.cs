using System.Collections;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.Bootstrap;
using MSC.LegacyImport;
using MSC.Presentation.Fluid;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed class ProductionSatsumaBootstrapPlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private GameObject spawnedSatsuma;
        private bool bootstrapLoadStarted;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // This fixture opens a complete, persistent Bootstrap session.
            // Its menu preview must not survive into unrelated physics tests.
            // Destroy the fixture composition even if load failed before the
            // Satsuma reference below could be assigned.
            GameCompositionRoot composition = bootstrapLoadStarted ? GameCompositionRoot.ActiveRoot : null;
            if (composition != null &&
                composition.GetComponent<ProductionWorldStreamingInstaller>() != null)
            {
                composition.gameObject.SetActive(false);
                Object.Destroy(composition.gameObject);
            }
            if (spawnedSatsuma != null)
            {
                Object.Destroy(spawnedSatsuma);
                spawnedSatsuma = null;
                yield return null;
            }
            yield return null;
            yield return null;
            bootstrapLoadStarted = false;
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesExactlyOnePersistentSatsumaBeforeGameplay()
        {
            Assert.That(SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
                "Bootstrap includes the HDRP menu preview. Run this composition test with a graphics device, without -nographics.");
            bootstrapLoadStarted = true;
            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return load;
            yield return null;

            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(installer, Is.Not.Null);

            LegacySatsumaBaselineMetadata[] satsumas =
                Object.FindObjectsByType<LegacySatsumaBaselineMetadata>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            Assert.That(satsumas, Has.Length.EqualTo(1));
            spawnedSatsuma = satsumas[0].gameObject;
            var driving = installer.SpawnedPlayer.GetComponent<SatsumaDrivingSessionController>();
            Assert.That(driving, Is.Not.Null, "Production composition must bind the interior driver session.");
            Assert.That(driving.Station, Is.SameAs(spawnedSatsuma.GetComponent<SatsumaDriverStation>()));
            Assert.That(driving.IsDriving, Is.False, "A fresh menu session does not board automatically.");
            Assert.That(spawnedSatsuma.GetComponent<VehicleInputRouter>().IsDriverSessionManaged, Is.True);
            Assert.That(spawnedSatsuma.GetComponent<VehicleInputRouter>().IsDriverSessionActive, Is.False);
            Assert.That(spawnedSatsuma.GetComponent<SatsumaCockpitSteeringPresenter>(), Is.Not.Null);
            Assert.That(
                satsumas[0].VehicleContentId,
                Is.EqualTo("vehicle.satsuma"));
            Assert.That(satsumas[0].LoosePartCount, Is.EqualTo(125));
            Assert.That(satsumas[0].ActiveLoosePartCount, Is.EqualTo(120));
            Assert.That(
                satsumas[0].GetComponent<VehiclePersistenceBinding>(),
                Is.Not.Null);
            Assert.That(
                satsumas[0].GetComponent<VehiclePaintStateController>(),
                Is.Not.Null);
            Assert.That(
                satsumas[0].GetComponent<Rigidbody>().isKinematic,
                Is.True,
                "Before world preparation the persistent car is guarded against " +
                "falling through its still-unloaded support cell.");
            Assert.That(installer.IsGameplayPrepared, Is.False);
            Assert.That(installer.IsGameplayActive, Is.False);
            Assert.That(
                satsumas[0].GetComponent<Rigidbody>().useGravity,
                Is.True);
            VehicleAssemblyController assembly =
                satsumas[0].GetComponent<VehicleAssemblyController>();
            Assert.That(assembly, Is.Not.Null);
            Assert.That(assembly.Parts, Has.Length.EqualTo(126));
            var host = satsumas[0].GetComponent<VehicleSimulationHost>();
            var operating = satsumas[0].GetComponent<SatsumaEngineOperatingSource>();
            Assert.That(operating, Is.Not.Null);
            Assert.That(operating.ValidateBindings(out string operatingFailure), Is.True, operatingFailure);
            Assert.That(host.SatsumaOperatingSourceComponent, Is.SameAs(operating));
            Assert.That(host.State.SatsumaOperating, Is.Not.Null);
            var instruments = satsumas[0].GetComponent<SatsumaInstrumentPresenter>();
            Assert.That(instruments, Is.Not.Null);
            Assert.That(instruments.HasGameTimeBinding, Is.True, "The actual Bootstrap must provide authoritative game time.");
            Assert.That(instruments.Needles, Has.Length.EqualTo(11));
            Assert.That(satsumas[0].GetComponent<SatsumaFlexibleConnectionPresenter>().Bindings, Has.Length.EqualTo(3));
            Assert.That(satsumas[0].GetComponent<SatsumaEngineEnvironmentBridge>(), Is.Not.Null);
            Assert.That(operating.HasEnvironmentBinding, Is.EqualTo(installer.Environment.CurrentOutputs.IsValid),
                "The main menu must not fabricate an environment sample before world preparation.");
            Assert.That(assembly.Parts.Count(part => part.GetComponent<AssemblyMechanicalConditionState>() != null), Is.EqualTo(9));
            Assert.That(assembly.Parts.Sum(part => part.GetComponentsInChildren<AssemblyValveAdjustmentTarget>(true).Length), Is.EqualTo(8));
            Assert.That(assembly.Parts.Sum(part => part.GetComponentsInChildren<AssemblyServiceCapTarget>(true).Length), Is.EqualTo(5));
            var reservoirLevels = assembly.Parts.SelectMany(part => part.GetComponentsInChildren<ServiceReservoirLevelPresenter>(true)).ToArray();
            Assert.That(reservoirLevels, Has.Length.EqualTo(4), "Loose sibling parts must receive levels before their first installation.");
            Assert.That(reservoirLevels.All(level => level.IsConfigured && !level.IsVisible), Is.True);
            var mechanicalMotion = satsumas[0].GetComponent<SatsumaEngineMechanicalMotion>();
            Assert.That(mechanicalMotion, Is.Not.Null);
            Assert.That(mechanicalMotion.Shafts, Has.Length.EqualTo(9));
            Assert.That(mechanicalMotion.Reciprocating, Has.Length.EqualTo(32));
            Assert.That(
                assembly.MountPoints,
                Has.Length.EqualTo(124),
                "The reviewed 117 stock sockets plus seven purchased-consumable sockets must survive production composition.");
            Assert.That(assembly.Tools, Has.Length.EqualTo(12));
            Assert.That(
                assembly.MountPoints.Sum(mount =>
                    mount.Definition.Fasteners.Length),
                Is.EqualTo(294));
            Assert.That(
                Resources.FindObjectsOfTypeAll<
                        AssemblyFastenerInteractionTarget>()
                    .Count(target => target.Controller == assembly),
                Is.EqualTo(294),
                "Loose installed-part owners are deliberately detached from " +
                "the chassis hierarchy at runtime; count fasteners by their " +
                "assembly authority, not Transform ancestry.");
            AssemblyFastenerInteractionTarget[] doorBolts = Resources
                .FindObjectsOfTypeAll<AssemblyFastenerInteractionTarget>()
                .Where(target => target.Controller == assembly &&
                    (target.MountId == "mount.satsuma.door-left" ||
                     target.MountId == "mount.satsuma.door-right")).ToArray();
            Assert.That(doorBolts, Has.Length.EqualTo(8));
            foreach (AssemblyFastenerInteractionTarget bolt in doorBolts)
            {
                Assert.That(bolt.InstalledOnlyExternalPresentationRenderer, Is.Not.Null);
                Assert.That(bolt.InstalledOnlyExternalPresentationRenderer.enabled, Is.False,
                    "A new game's loose doors must not show their installed-only bolts.");
            }
            SatsumaFuelLineConnection fuelLine = satsumas[0].GetComponent<SatsumaFuelLineConnection>();
            Assert.That(fuelLine, Is.Not.Null);
            Assert.That(fuelLine.Assembly, Is.SameAs(assembly));
            Assert.That(fuelLine.Tank, Is.SameAs(assembly.Parts.Single(part =>
                part.Definition.DefinitionId == SatsumaFuelLineConnection.TankPartId)));
            Assert.That(satsumas[0].GetComponent<VehiclePersistenceBinding>().FuelLineConnection,
                Is.SameAs(fuelLine));
            Assert.That(fuelLine.Stage, Is.Zero);
            Assert.That(fuelLine.IsBolted, Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<SatsumaFuelLineFastenerInteractionTarget>()
                .Count(target => target.Connection == fuelLine), Is.EqualTo(1));
            Assert.That(
                assembly.MountPoints.Count(mount =>
                    mount.GetComponent<AssemblyOwnedMountAuthoring>() != null),
                Is.EqualTo(59));
            string[] consumableMountIds =
            {
                "mount.satsuma.cylinder-head.spark-plug-1",
                "mount.satsuma.cylinder-head.spark-plug-2",
                "mount.satsuma.cylinder-head.spark-plug-3",
                "mount.satsuma.cylinder-head.spark-plug-4",
                "mount.satsuma.engine-block.alternator-belt",
                "mount.satsuma.headlight-left.light-bulb",
                "mount.satsuma.headlight-right.light-bulb",
            };
            Assert.That(assembly.MountPoints.Where(mount =>
                    consumableMountIds.Contains(mount.MountId) &&
                    mount.GetComponent<AssemblyOwnedMountAuthoring>() != null)
                .Select(mount => mount.MountId), Is.EquivalentTo(consumableMountIds));
            Assert.That(
                assembly.Parts.Count(part =>
                    part.GetComponent<AssemblySurfaceMountHandoffTarget>() != null),
                Is.EqualTo(126));
            Assert.That(
                satsumas[0].GetComponentsInChildren<
                    AssemblyHingeMountAuthoring>(true),
                Has.Length.EqualTo(4));
            Assert.That(
                assembly.Parts.Where(part => part.Definition.DefinitionId ==
                        "vehicle.satsuma.part.dashboard")
                    .SelectMany(part => part.GetComponentsInChildren<
                        AssemblyHoodReleaseInteractionTarget>(true)).ToArray(),
                Has.Length.EqualTo(1));
            VehicleAssemblyAudioPresenter assemblyAudio = satsumas[0]
                .GetComponent<VehicleAssemblyAudioPresenter>();
            Assert.That(
                assemblyAudio,
                Is.Not.Null,
                "Production Satsuma must not fall back to generic interaction audio.");
            Assert.That(assemblyAudio.IsBound, Is.True, assemblyAudio.LastFailure);
            UnityAudioEventLibrary assemblyAudioLibrary =
                Resources.Load<UnityAudioEventLibrary>(
                    VehicleAssemblyAudioPresenter
                        .FallbackOverrideResourcesPath);
            Assert.That(assemblyAudioLibrary, Is.Not.Null);
            Assert.That(
                assemblyAudioLibrary.TryResolve(
                    AudioProjectIds.Events.InteractionPartInstall,
                    out UnityAudioEventDefinition installAudio),
                Is.True);
            Assert.That(installAudio.Clip, Is.Not.Null);
            Assert.That(
                installAudio.Clip.name,
                Is.EqualTo("satsuma_part_install"),
                "Part installation must use donor assemble audio, not the old door-like placeholder.");

            SatsumaEngineFeedbackPresenter engineAudio = satsumas[0]
                .GetComponent<SatsumaEngineFeedbackPresenter>();
            Assert.That(engineAudio, Is.Not.Null);
            Assert.That(engineAudio.IsBound, Is.True, engineAudio.LastFailure);
            Assert.That(engineAudio.Simulation, Is.SameAs(satsumas[0].GetComponent<VehicleSimulationHost>()));
            Assert.That(engineAudio.Ignition, Is.SameAs(satsumas[0].GetComponent<SatsumaIgnitionController>()));
            Assert.That(engineAudio.EnginePart, Is.SameAs(assembly.Parts.Single(part =>
                part.Definition.DefinitionId == "vehicle.satsuma.part.engine-block")));
            Assert.That(engineAudio.ExhaustBinding, Is.SameAs(satsumas[0].GetComponent<SatsumaEngineExhaustBinding>()));
            Assert.That(engineAudio.ExhaustBinding.IsConfigured, Is.True);
            Assert.That(engineAudio.ExhaustBinding.Headers, Is.SameAs(assembly.Parts.Single(part =>
                part.Definition.DefinitionId == "vehicle.satsuma.part.headers")));
            Assert.That(engineAudio.ExhaustBinding.Pipe, Is.SameAs(assembly.Parts.Single(part =>
                part.Definition.DefinitionId == "vehicle.satsuma.part.exhaust-pipe")));
            Assert.That(engineAudio.ExhaustBinding.Muffler, Is.SameAs(assembly.Parts.Single(part =>
                part.Definition.DefinitionId == "vehicle.satsuma.part.exhaust-muffler")));
            Assert.That(new[] { engineAudio.EngineEmitter.StableId, engineAudio.KeyEmitter.StableId,
                    engineAudio.ExhaustBinding.Emitter.StableId }, Is.EquivalentTo(new[]
            {
                "audio.emitter.vehicle.satsuma.engine",
                "audio.emitter.vehicle.satsuma.ignition",
                "audio.emitter.vehicle.satsuma.exhaust",
            }));
            UnityAudioEventLibrary engineAudioLibrary = Resources.Load<UnityAudioEventLibrary>(
                SatsumaEngineFeedbackPresenter.FallbackResourcesPath);
            Assert.That(engineAudioLibrary, Is.Not.Null);
            AudioEventId[] engineEvents =
            {
                SatsumaEngineAudioIds.KeyInserted, SatsumaEngineAudioIds.KeyRemoved,
                SatsumaEngineAudioIds.StarterEngaged, SatsumaEngineAudioIds.StarterLoop,
                SatsumaEngineAudioIds.EngineCaught, SatsumaEngineAudioIds.EngineThrottleLoop,
                SatsumaEngineAudioIds.EngineCoastLoop, SatsumaEngineAudioIds.ExhaustLoop,
                SatsumaEngineAudioIds.BeltSqueal, SatsumaEngineAudioIds.Pinging,
                SatsumaEngineAudioIds.ValveTick, SatsumaEngineAudioIds.BearingKnock,
                SatsumaEngineAudioIds.IntakeSpit, SatsumaEngineAudioIds.ExhaustBackfire,
                SatsumaEngineAudioIds.FanStarted, SatsumaEngineAudioIds.FanLoop, SatsumaEngineAudioIds.FanStopped,
            };
            foreach (AudioEventId eventId in engineEvents)
            {
                Assert.That(engineAudioLibrary.TryResolve(eventId, out UnityAudioEventDefinition definition),
                    Is.True, eventId.Value);
                Assert.That(definition.Clip, Is.Not.Null, eventId.Value);
            }

            LegacySatsumaLoosePartsRoot looseRoot =
                satsumas[0].GetComponent<LegacySatsumaLoosePartsRoot>();
            Assert.That(looseRoot, Is.Not.Null);
            Assert.That(looseRoot.LoosePartsRoot, Is.Not.Null);
            Assert.That(
                looseRoot.LoosePartsRoot.parent,
                Is.EqualTo(satsumas[0].transform.parent),
                "Loose parts must become vehicle siblings and never follow the chassis.");

            // The startup guard is released by the real world-materialization
            // lifecycle, not by mutating the Rigidbody in this fixture.
            Assert.That(installer.TryBeginGameplayPreparation(out string preparationFailure),
                Is.True, preparationFailure);
            float preparationDeadline = Time.realtimeSinceStartup + 120f;
            while (!installer.IsGameplayPrepared && installer.IsGameplayPreparationRunning &&
                   Time.realtimeSinceStartup < preparationDeadline)
            {
                yield return null;
            }
            Assert.That(installer.IsGameplayPrepared, Is.True,
                installer.LastGameplayPreparationFailure);
            Assert.That(installer.TryActivateGameplay(out string activationFailure),
                Is.True, activationFailure);
            Assert.That(operating.HasEnvironmentBinding, Is.True);
            Assert.That(operating.AmbientCelsius,
                Is.EqualTo(installer.Environment.CurrentOutputs.Weather.TemperatureCelsius).Within(.0001f));
            Assert.That(satsumas[0].GetComponent<Rigidbody>().isKinematic, Is.False,
                "After static support is materialized the incomplete Satsuma must " +
                "again be a physical chassis, without a part-install workaround.");
            Assert.That(satsumas[0].GetComponent<Rigidbody>().detectCollisions, Is.True);
            Assert.That(satsumas[0].GetComponent<Rigidbody>().useGravity, Is.True);
        }
    }
}
