using System;
using System.Collections.Generic;
using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Weather.System
{
    /// <summary>
    /// Reversible presentation facade used by the existing production owner.
    /// It guarantees that only one concrete weather backend is attached.
    /// </summary>
    [DefaultExecutionOrder(-30500)]
    [DisallowMultipleComponent]
    public sealed class GameWeatherSystem : MonoBehaviour,
        IEnvironmentPresentationAdapter,
        IEnvironmentPresentationCameraTarget,
        IEnvironmentPresentationSceneOwnershipGuard
    {
        private const string MissingBackendCode = "WEATHER-BACKEND-001";
        private const string DuplicateBackendCode = "WEATHER-BACKEND-002";
        private const string SwitchFailureCode = "WEATHER-BACKEND-003";

        [SerializeField] private WeatherBackendType selectedBackend =
            WeatherBackendType.EnviroLegacy;
        [SerializeField] private MonoBehaviour enviroLegacyBackendComponent;
        [SerializeField] private MonoBehaviour nativeHdrpBackendComponent;
        [SerializeField] private WeatherDebugController debugController;

        private readonly List<EnvironmentPresentationDiagnostic> diagnostics =
            new List<EnvironmentPresentationDiagnostic>(4);
        private IWeatherBackend enviroLegacyBackend;
        private IWeatherBackend nativeHdrpBackend;
        private IWeatherBackend activeBackend;
        private Camera presentationCamera;
        private EnvironmentPresentationStatus status =
            EnvironmentPresentationStatus.Detached;
        private EnvironmentPresentationFrame lastFrame;
        private bool hasLastFrame;

        public event Action<WeatherBackendType> BackendChanged;
        public event Action<Camera> PresentationCameraChanged;

        public WeatherBackendType SelectedBackend => selectedBackend;
        public IWeatherBackend ActiveBackend => activeBackend;
        public bool IsAttached => activeBackend != null && activeBackend.IsAttached;
        public EnvironmentPresentationCapabilities Capabilities =>
            activeBackend?.Capabilities ?? EnvironmentPresentationCapabilities.None;
        public EnvironmentPresentationStatus Status =>
            activeBackend?.Status ?? status;
        public IReadOnlyList<EnvironmentPresentationDiagnostic> Diagnostics =>
            activeBackend?.Diagnostics ?? diagnostics;
        public bool HasLastFrame => hasLastFrame;
        public EnvironmentPresentationFrame LastFrame => lastFrame;
        public Camera PresentationCamera => presentationCamera;

        private void Awake()
        {
            ResolveBackends();
        }

        private void OnDisable()
        {
            activeBackend?.Detach();
            activeBackend = null;
            status = EnvironmentPresentationStatus.Detached;
        }

        public EnvironmentPresentationStatus Attach()
        {
            ResolveBackends();
            IWeatherBackend requested = Resolve(selectedBackend);
            if (requested == null)
            {
                return Fault(
                    MissingBackendCode,
                    $"No valid {selectedBackend} backend is assigned.");
            }

            if (activeBackend != null && activeBackend != requested)
            {
                activeBackend.Detach();
            }

            EnsureInactiveDetached(requested);
            if (presentationCamera != null &&
                !requested.TrySetPresentationCamera(presentationCamera))
            {
                return Fault(
                    SwitchFailureCode,
                    $"{selectedBackend} rejected the presentation camera.");
            }

            status = requested.Attach();
            if (status.IsOperational)
            {
                activeBackend = requested;
                debugController?.RecordBackend(selectedBackend, status);
            }

            return status;
        }

        public EnvironmentPresentationStatus Present(
            in EnvironmentPresentationFrame frame)
        {
            if (activeBackend == null || !activeBackend.IsAttached)
            {
                EnvironmentPresentationStatus attachStatus = Attach();
                if (!attachStatus.IsOperational)
                {
                    return attachStatus;
                }
            }

            lastFrame = frame;
            hasLastFrame = true;
            status = activeBackend.Present(frame);
            debugController?.RecordFrame(frame, selectedBackend, status);
            return status;
        }

        public void Detach()
        {
            activeBackend?.Detach();
            activeBackend = null;
            status = EnvironmentPresentationStatus.Detached;
            debugController?.RecordBackend(selectedBackend, status);
        }

        public EnvironmentPresentationStatus SetBackend(
            WeatherBackendType backendType)
        {
            ResolveBackends();
            IWeatherBackend requested = Resolve(backendType);
            if (requested == null)
            {
                return Fault(
                    MissingBackendCode,
                    $"No valid {backendType} backend is assigned.");
            }

            if (activeBackend == requested && selectedBackend == backendType)
            {
                return activeBackend.Status;
            }

            IWeatherBackend previous = activeBackend;
            WeatherBackendType previousType = selectedBackend;
            previous?.Detach();
            activeBackend = null;
            EnsureInactiveDetached(requested);
            if (presentationCamera != null)
            {
                requested.TrySetPresentationCamera(presentationCamera);
            }

            EnvironmentPresentationStatus requestedStatus = requested.Attach();
            if (!requestedStatus.IsOperational)
            {
                requested.Detach();
                selectedBackend = previousType;
                if (previous != null)
                {
                    if (presentationCamera != null)
                    {
                        previous.TrySetPresentationCamera(presentationCamera);
                    }

                    EnvironmentPresentationStatus rollback = previous.Attach();
                    if (rollback.IsOperational)
                    {
                        activeBackend = previous;
                        if (hasLastFrame)
                        {
                            previous.Present(lastFrame);
                        }
                    }
                }

                return Fault(
                    SwitchFailureCode,
                    $"Switch to {backendType} failed; the previous backend was restored.");
            }

            selectedBackend = backendType;
            activeBackend = requested;
            status = requestedStatus;
            if (hasLastFrame)
            {
                status = requested.Present(lastFrame);
                if (!status.IsOperational)
                {
                    requested.Detach();
                    activeBackend = null;
                    selectedBackend = previousType;
                    if (previous != null)
                    {
                        if (presentationCamera != null)
                        {
                            previous.TrySetPresentationCamera(presentationCamera);
                        }

                        EnvironmentPresentationStatus rollback = previous.Attach();
                        if (rollback.IsOperational)
                        {
                            activeBackend = previous;
                            previous.Present(lastFrame);
                        }
                    }

                    return Fault(
                        SwitchFailureCode,
                        $"Switch to {backendType} rejected the last presentation frame; the previous backend was restored.");
                }
            }

            BackendChanged?.Invoke(selectedBackend);
            debugController?.RecordBackend(selectedBackend, status);
            return status;
        }

        public bool TrySetPresentationCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            presentationCamera = camera;
            PresentationCameraChanged?.Invoke(camera);
            if (activeBackend != null)
            {
                return activeBackend.TrySetPresentationCamera(camera);
            }

            ResolveBackends();
            IWeatherBackend requested = Resolve(selectedBackend);
            return requested == null || requested.TrySetPresentationCamera(camera);
        }

        public EnvironmentPresentationStatus RevalidateSceneOwnership()
        {
            ResolveBackends();
            if (enviroLegacyBackend != null && nativeHdrpBackend != null &&
                ReferenceEquals(enviroLegacyBackend, nativeHdrpBackend))
            {
                return Fault(
                    DuplicateBackendCode,
                    "Enviro and Native HDRP slots reference the same backend.");
            }

            if (activeBackend == null)
            {
                return Attach();
            }

            EnsureInactiveDetached(activeBackend);
            status = activeBackend.RevalidateSceneOwnership();
            debugController?.RecordBackend(selectedBackend, status);
            return status;
        }

        public void ConfigureForAuthoring(
            WeatherBackendType authoredSelection,
            MonoBehaviour authoredEnviroLegacyBackend,
            MonoBehaviour authoredNativeHdrpBackend,
            WeatherDebugController authoredDebugController = null)
        {
            selectedBackend = authoredSelection;
            enviroLegacyBackendComponent = authoredEnviroLegacyBackend;
            nativeHdrpBackendComponent = authoredNativeHdrpBackend;
            debugController = authoredDebugController;
            ResolveBackends();
        }

        private void ResolveBackends()
        {
            enviroLegacyBackend =
                enviroLegacyBackendComponent as IWeatherBackend;
            nativeHdrpBackend =
                nativeHdrpBackendComponent as IWeatherBackend;
        }

        private IWeatherBackend Resolve(WeatherBackendType type) =>
            type == WeatherBackendType.NativeHDRP
                ? nativeHdrpBackend
                : enviroLegacyBackend;

        private void EnsureInactiveDetached(IWeatherBackend requested)
        {
            if (enviroLegacyBackend != null &&
                enviroLegacyBackend != requested &&
                enviroLegacyBackend.IsAttached)
            {
                enviroLegacyBackend.Detach();
            }

            if (nativeHdrpBackend != null &&
                nativeHdrpBackend != requested &&
                nativeHdrpBackend.IsAttached)
            {
                nativeHdrpBackend.Detach();
            }
        }

        private EnvironmentPresentationStatus Fault(string code, string message)
        {
            diagnostics.Clear();
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Error,
                code,
                message));
            status = new EnvironmentPresentationStatus(
                EnvironmentPresentationState.Faulted,
                EnvironmentPresentationCapabilities.None,
                hasLastFrame ? lastFrame.Revision : 0UL,
                0,
                1);
            debugController?.RecordBackend(selectedBackend, status);
            return status;
        }
    }
}
