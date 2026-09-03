using System;
using MSC.Audio.InteractionIntegration;
using MSC.Audio.PlayerIntegration;
using MSC.Audio.WeatherIntegration;
using MSC.Interaction.Carrying;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Audio.Composition
{
    /// <summary>
    /// Project-owned production audio installer. It binds typed gameplay
    /// components to the router without referencing Wwise or Unity Audio types.
    /// </summary>
    [DefaultExecutionOrder(90)]
    [DisallowMultipleComponent]
    public sealed class ProductionAudioComposition : MonoBehaviour
    {
        [SerializeField] private AudioBackendRouter router;
        [SerializeField] private MonoBehaviour preferredBackendComponent;
        [SerializeField] private MonoBehaviour fallbackBackendComponent;
        [SerializeField] private MonoBehaviour runtimeOwnerComponent;
        [SerializeField] private AudioEmitterAuthoring ambienceEmitter;
        [SerializeField] private WeatherAudioPresenter weatherPresenter;
        [SerializeField] private WeatherExposureResolver weatherExposureResolver;

        public IAudioBackend Backend => router;
        public IAudioBackend FallbackBackend =>
            fallbackBackendComponent as IAudioBackend;
        public WeatherExposureResolver WeatherExposureResolver =>
            weatherExposureResolver;
        public bool IsInitialized { get; private set; }
        public string LastFailure { get; private set; } = string.Empty;

        public bool InitializeSession(
            GameObject spawnedPlayer,
            ProductionEnvironmentController environmentController)
        {
            IsInitialized = false;
            if (!TryValidate(out string failure))
            {
                return Fail(failure);
            }

            if (spawnedPlayer == null)
            {
                return Fail("Production audio requires the spawned player.");
            }

            if (environmentController == null)
            {
                return Fail("Production audio requires ProductionEnvironmentController.");
            }

            Camera playerCamera = spawnedPlayer.GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
            {
                return Fail("The spawned player has no camera for the audio listener.");
            }

            PhysicalCarryController carryController =
                spawnedPlayer.GetComponentInChildren<PhysicalCarryController>(true);
            if (carryController == null)
            {
                return Fail("The spawned player has no PhysicalCarryController for interaction audio.");
            }

            router.Configure(
                preferredBackendComponent,
                fallbackBackendComponent,
                scanLoadedScenes: true);

            // Trigger callbacks are delivered to the player collider owner,
            // not to the child camera. Keep spatial sampling on the camera but
            // host the environment-zone listener on the player root alongside
            // the CharacterController.
            AudioListenerContextPresenter listener =
                spawnedPlayer.GetComponent<AudioListenerContextPresenter>();
            if (listener == null)
            {
                listener = spawnedPlayer.AddComponent<AudioListenerContextPresenter>();
            }

            listener.Configure(router, playerCamera.transform);

            AudioEmitterAuthoring interactionEmitter =
                carryController.GetComponent<AudioEmitterAuthoring>();
            if (interactionEmitter == null)
            {
                interactionEmitter =
                    carryController.gameObject.AddComponent<AudioEmitterAuthoring>();
            }

            interactionEmitter.Configure(
                "audio.emitter.player.interaction",
                router,
                carryController.transform);

            InteractionAudioBridge interactionBridge =
                carryController.GetComponent<InteractionAudioBridge>();
            if (interactionBridge == null)
            {
                interactionBridge =
                    carryController.gameObject.AddComponent<InteractionAudioBridge>();
            }

            if (!interactionBridge.Configure(
                    carryController,
                    router,
                    interactionEmitter))
            {
                return Fail(interactionBridge.LastFailure);
            }

            PlayerFootstepAudioPresenter footstepPresenter =
                carryController.GetComponent<PlayerFootstepAudioPresenter>();
            if (footstepPresenter == null)
            {
                footstepPresenter =
                    carryController.gameObject.AddComponent<PlayerFootstepAudioPresenter>();
            }

            // Interaction and footsteps originate from the same player root.
            // Reuse its one registered emitter instead of creating a second
            // Wwise game object with a duplicate spatial identity.
            if (!footstepPresenter.Configure(
                    spawnedPlayer,
                    router,
                    interactionEmitter))
            {
                return Fail(footstepPresenter.LastFailure);
            }

            PlayerLeanImpactAudioPresenter leanImpactPresenter =
                carryController.GetComponent<PlayerLeanImpactAudioPresenter>();
            if (leanImpactPresenter == null)
            {
                leanImpactPresenter =
                    carryController.gameObject.AddComponent<
                        PlayerLeanImpactAudioPresenter>();
            }

            if (!leanImpactPresenter.Configure(
                    spawnedPlayer,
                    router,
                    interactionEmitter))
            {
                return Fail(leanImpactPresenter.LastFailure);
            }

            ambienceEmitter.Configure(
                "audio.emitter.environment.production",
                router,
                playerCamera.transform);
            if (weatherExposureResolver == null)
            {
                weatherExposureResolver =
                    FindFirstObjectByType<WeatherExposureResolver>();
            }

            weatherPresenter.Configure(
                environmentController,
                router,
                ambienceEmitter,
                listener,
                weatherExposureResolver);

            WorldAmbientAudioPresenter ambientPresenter =
                GetComponent<WorldAmbientAudioPresenter>();
            if (ambientPresenter == null)
            {
                ambientPresenter = gameObject.AddComponent<WorldAmbientAudioPresenter>();
            }

            if (!ambientPresenter.Configure(
                    router,
                    ambienceEmitter,
                    environmentController.GameTime,
                    environmentController))
            {
                return Fail("World ambient audio presenter could not be configured.");
            }

            if (runtimeOwnerComponent is IAudioRuntimeOwner runtimeOwner &&
                !runtimeOwner.BindListener(playerCamera.transform, out failure))
            {
                return Fail(failure);
            }

            LastFailure = string.Empty;
            IsInitialized = true;
            return true;
        }

        public bool TryLoadFallbackSupplementalEventLibrary(
            string resourcesPath,
            out string failure)
        {
            if (!(fallbackBackendComponent is IAudioSupplementalContentBackend
                    supplementalContent))
            {
                failure =
                    "Configured fallback audio backend cannot load supplemental event libraries.";
                return false;
            }

            return supplementalContent.TryLoadSupplementalEventLibrary(
                resourcesPath,
                out failure);
        }

        public bool TryLoadFallbackOverrideEventLibrary(
            string resourcesPath,
            out string failure)
        {
            if (!(fallbackBackendComponent is IAudioOverrideContentBackend
                    overrideContent))
            {
                failure =
                    "Configured fallback audio backend cannot load override event libraries.";
                return false;
            }

            return overrideContent.TryLoadOverrideEventLibrary(
                resourcesPath,
                out failure);
        }

        public bool TryValidate(out string failure)
        {
            if (router == null)
            {
                failure = "Production audio has no AudioBackendRouter.";
                return false;
            }

            if (preferredBackendComponent != null &&
                !(preferredBackendComponent is IAudioBackend))
            {
                failure = "Preferred audio component does not implement IAudioBackend.";
                return false;
            }

            if (!(fallbackBackendComponent is IAudioBackend))
            {
                failure = "Fallback audio component does not implement IAudioBackend.";
                return false;
            }

            if (runtimeOwnerComponent != null &&
                !(runtimeOwnerComponent is IAudioRuntimeOwner))
            {
                failure = "Audio runtime owner does not implement IAudioRuntimeOwner.";
                return false;
            }

            if (ambienceEmitter == null || weatherPresenter == null)
            {
                failure = "Production weather audio components are incomplete.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            AudioBackendRouter configuredRouter,
            MonoBehaviour configuredPreferredBackend,
            MonoBehaviour configuredFallbackBackend,
            MonoBehaviour configuredRuntimeOwner,
            AudioEmitterAuthoring configuredAmbienceEmitter,
            WeatherAudioPresenter configuredWeatherPresenter)
        {
            router = configuredRouter;
            preferredBackendComponent = configuredPreferredBackend;
            fallbackBackendComponent = configuredFallbackBackend;
            runtimeOwnerComponent = configuredRuntimeOwner;
            ambienceEmitter = configuredAmbienceEmitter;
            weatherPresenter = configuredWeatherPresenter;
        }

        public void ConfigureWeatherExposureForAuthoring(
            WeatherExposureResolver configuredWeatherExposureResolver)
        {
            weatherExposureResolver = configuredWeatherExposureResolver;
        }
#endif

        private bool Fail(string failure)
        {
            LastFailure = string.IsNullOrWhiteSpace(failure)
                ? "Production audio composition failed."
                : failure;
            Debug.LogError(LastFailure, this);
            return false;
        }
    }
}
