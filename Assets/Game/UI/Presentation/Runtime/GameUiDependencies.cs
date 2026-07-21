using System;
using MSC.Audio;
using MSC.Core.Lifecycle;
using MSC.Core.Time;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.UI.Presentation
{
    public sealed class GameUiDependencies
    {
        public GameUiDependencies(
            Func<bool> isWorldReady,
            IGameTimeService gameTime,
            IAudioBackend audio,
            InputActionAsset playerActions,
            InputActionAsset vehicleActions,
            GameObject gameplayRoot,
            string settingsPath,
            bool startInMainMenu,
            Camera backdropCamera = null,
            Texture2D menuBackdropTexture = null,
            Texture2D menuLogoTexture = null,
            IGameplaySessionGate gameplaySessionGate = null,
            Shader uiBlurShader = null)
        {
            IsWorldReady = isWorldReady ?? throw new ArgumentNullException(nameof(isWorldReady));
            GameTime = gameTime;
            Audio = audio;
            PlayerActions = playerActions;
            VehicleActions = vehicleActions;
            GameplayRoot = gameplayRoot;
            SettingsPath = string.IsNullOrWhiteSpace(settingsPath)
                ? throw new ArgumentException("A settings path is required.", nameof(settingsPath))
                : settingsPath;
            StartInMainMenu = startInMainMenu;
            BackdropCamera = backdropCamera;
            MenuBackdropTexture = menuBackdropTexture;
            MenuLogoTexture = menuLogoTexture;
            UiBlurShader = uiBlurShader;
            GameplaySessionGate = gameplaySessionGate;
        }

        public Func<bool> IsWorldReady { get; }

        public IGameTimeService GameTime { get; }

        public IAudioBackend Audio { get; }

        public InputActionAsset PlayerActions { get; }

        public InputActionAsset VehicleActions { get; }

        public GameObject GameplayRoot { get; }

        public string SettingsPath { get; }

        public bool StartInMainMenu { get; }

        /// <summary>
        /// Explicit production camera used for a one-shot, low-resolution menu
        /// backdrop. Tests and headless compositions may omit it.
        /// </summary>
        public Camera BackdropCamera { get; }

        /// <summary>
        /// Project-owned static menu plate. It is never an approved-reference
        /// screenshot and may be omitted by headless test fixtures.
        /// </summary>
        public Texture2D MenuBackdropTexture { get; }

        /// <summary>
        /// Project-owned menu logo supplied explicitly by the composition root.
        /// Headless fixtures may omit it and use the procedural fallback.
        /// </summary>
        public Texture2D MenuLogoTexture { get; }

        /// <summary>
        /// Explicit project-owned separable Gaussian blur shader. Production
        /// authoring supplies it directly so player builds never depend on a
        /// string lookup or shader stripping heuristics.
        /// </summary>
        public Shader UiBlurShader { get; }

        /// <summary>
        /// Optional explicit boundary used by the production Bootstrap to keep
        /// a prepared world dormant behind the front end. Headless UI fixtures
        /// may omit it.
        /// </summary>
        public IGameplaySessionGate GameplaySessionGate { get; }
    }
}
