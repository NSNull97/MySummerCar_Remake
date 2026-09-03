using System;
using MSC.Audio;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Bootstrap
{
    [DisallowMultipleComponent]
    public sealed class ProductionSatsumaInstaller : MonoBehaviour
    {
        public const string ResourcePath =
            "Phase1Vehicles/Satsuma_Phase1_V1a";

        private GameObject spawnedVehicle;

        public GameObject SpawnedVehicle => spawnedVehicle;

        public bool TryApplyNewGamePaint(
            int paletteIndex,
            Color bodyColor,
            out string failure)
        {
            if (spawnedVehicle == null)
            {
                failure = "The Phase 1 Satsuma has not been initialized.";
                return false;
            }

            VehiclePaintStateController paint =
                spawnedVehicle.GetComponent<VehiclePaintStateController>();
            if (paint == null)
            {
                failure = "The generated Satsuma has no body-paint state.";
                return false;
            }

            return paint.TryApplyPlayerSelectedPaint(
                paletteIndex,
                bodyColor,
                out failure);
        }

        public GameObject Initialize(
            Transform compositionRoot,
            MonoBehaviour assemblyAudioBackend = null)
        {
            if (compositionRoot == null)
            {
                throw new ArgumentNullException(nameof(compositionRoot));
            }

            LegacySatsumaBaselineMetadata[] existing = compositionRoot
                .GetComponentsInChildren<LegacySatsumaBaselineMetadata>(true);
            if (existing.Length > 1)
            {
                throw new InvalidOperationException(
                    "More than one project-owned Satsuma baseline is loaded.");
            }

            if (existing.Length == 1)
            {
                if (!existing[0].TryValidate(out string existingFailure))
                {
                    throw new InvalidOperationException(existingFailure);
                }

                spawnedVehicle = existing[0].gameObject;
                ConfigureAssemblyAudio(assemblyAudioBackend);
                return spawnedVehicle;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcePath) ??
                throw new InvalidOperationException(
                    "The private Phase 1 Satsuma baseline has not been built. " +
                    "Run Tools/MSC Remake/Phase 1/Satsuma/Build V1d Baseline.");
            LegacySatsumaBaselineMetadata metadata =
                prefab.GetComponent<LegacySatsumaBaselineMetadata>() ??
                throw new InvalidOperationException(
                    "The generated Satsuma prefab has no provenance metadata.");
            if (!metadata.TryValidate(out string failure))
            {
                throw new InvalidOperationException(failure);
            }

            spawnedVehicle = Instantiate(
                prefab,
                metadata.DefaultWorldPosition,
                metadata.DefaultWorldRotation,
                compositionRoot);
            spawnedVehicle.name = "Phase 1 Satsuma";
            ConfigureAssemblyAudio(assemblyAudioBackend);
            return spawnedVehicle;
        }

        private void ConfigureAssemblyAudio(MonoBehaviour audioBackend)
        {
            if (audioBackend == null)
            {
                return;
            }

            if (!(audioBackend is IAudioBackend))
            {
                throw new InvalidOperationException(
                    "The Satsuma assembly audio component does not implement IAudioBackend.");
            }

            VehicleAssemblyController controller = spawnedVehicle
                .GetComponentInChildren<VehicleAssemblyController>(true) ??
                throw new InvalidOperationException(
                    "The generated Satsuma has no assembly controller for audio binding.");
            VehicleAssemblyAudioPresenter presenter =
                spawnedVehicle.GetComponent<VehicleAssemblyAudioPresenter>() ??
                spawnedVehicle.AddComponent<VehicleAssemblyAudioPresenter>();
            if (!presenter.Configure(controller, audioBackend))
            {
                throw new InvalidOperationException(
                    "The Satsuma assembly audio presenter failed: " +
                    presenter.LastFailure);
            }
        }
    }
}
