using System.Collections;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.Bootstrap;
using MSC.LegacyImport;
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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (spawnedSatsuma != null)
            {
                Object.Destroy(spawnedSatsuma);
                spawnedSatsuma = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BootstrapCreatesExactlyOnePersistentSatsumaBeforeGameplay()
        {
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
                Is.False,
                "The incomplete Satsuma must remain a physical chassis; " +
                "missing assembly support is disabled per wheel instead.");
            Assert.That(
                satsumas[0].GetComponent<Rigidbody>().useGravity,
                Is.True);
            VehicleAssemblyController assembly =
                satsumas[0].GetComponent<VehicleAssemblyController>();
            Assert.That(assembly, Is.Not.Null);
            Assert.That(assembly.Parts, Has.Length.EqualTo(126));
            Assert.That(
                assembly.MountPoints,
                Has.Length.EqualTo(117),
                "The retired duplicate subframe FSM socket must not reappear in the production composition.");
            Assert.That(assembly.Tools, Has.Length.EqualTo(10));
            Assert.That(
                assembly.MountPoints.Sum(mount =>
                    mount.Definition.Fasteners.Length),
                Is.EqualTo(280));
            Assert.That(
                Resources.FindObjectsOfTypeAll<
                        AssemblyFastenerInteractionTarget>()
                    .Count(target => target.Controller == assembly),
                Is.EqualTo(280),
                "Loose installed-part owners are deliberately detached from " +
                "the chassis hierarchy at runtime; count fasteners by their " +
                "assembly authority, not Transform ancestry.");
            Assert.That(
                assembly.MountPoints.Count(mount =>
                    mount.GetComponent<AssemblyOwnedMountAuthoring>() != null),
                Is.EqualTo(52));
            Assert.That(
                assembly.Parts.Count(part =>
                    part.GetComponent<AssemblySurfaceMountHandoffTarget>() != null),
                Is.EqualTo(126));
            Assert.That(
                satsumas[0].GetComponentsInChildren<
                    AssemblyHingeMountAuthoring>(true),
                Has.Length.EqualTo(4));
            Assert.That(
                satsumas[0].GetComponentsInChildren<
                    AssemblyHoodReleaseInteractionTarget>(true),
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

            LegacySatsumaLoosePartsRoot looseRoot =
                satsumas[0].GetComponent<LegacySatsumaLoosePartsRoot>();
            Assert.That(looseRoot, Is.Not.Null);
            Assert.That(looseRoot.LoosePartsRoot, Is.Not.Null);
            Assert.That(
                looseRoot.LoosePartsRoot.parent,
                Is.EqualTo(satsumas[0].transform.parent),
                "Loose parts must become vehicle siblings and never follow the chassis.");
        }
    }
}
