using System;
using MSC.Audio;
using MSC.Core.Lifecycle;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Needs;
using MSC.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.UI.Presentation
{
    /// <summary>
    /// Requests a clean-session load. The composition root owns the pending-slot
    /// handoff and scene reload so UI never restores save participants after the
    /// world has already been revealed.
    /// </summary>
    public delegate bool SaveLoadRequestHandler(string slotId, out string failure);
    public delegate bool NewGameVehiclePaintHandler(
        int paletteIndex,
        Color bodyColor,
        out string failure);

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
            Shader uiBlurShader = null,
            ISaveService saveService = null,
            SaveLoadRequestHandler requestLoad = null,
            Func<string, SaveRequest> createSaveRequest = null,
            string preferredSaveSlotId = "manual-01",
            string initialSaveStatus = null,
            SaveReadStatus? initialSaveReadStatus = null,
            IPlayerNeedsService playerNeeds = null,
            IPlayerMoneyService playerMoney = null,
            Action openDeveloperTools = null,
            NewGameVehiclePaintHandler configureNewGameVehiclePaint = null)
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
            SaveService = saveService;
            RequestLoad = requestLoad;
            CreateSaveRequest = createSaveRequest;
            PreferredSaveSlotId = string.IsNullOrWhiteSpace(preferredSaveSlotId)
                ? "manual-01"
                : preferredSaveSlotId;
            InitialSaveStatus = initialSaveStatus ?? string.Empty;
            InitialSaveReadStatus = initialSaveReadStatus;
            PlayerNeeds = playerNeeds;
            PlayerMoney = playerMoney;
            OpenDeveloperTools = openDeveloperTools;
            ConfigureNewGameVehiclePaint = configureNewGameVehiclePaint;
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

        /// <summary>
        /// Optional native-save status and write boundary. Its absence keeps the
        /// accepted 08A no-storage state intact for isolated UI fixtures.
        /// </summary>
        public ISaveService SaveService { get; }

        /// <summary>
        /// Optional clean-session load request owned by the composition root.
        /// </summary>
        public SaveLoadRequestHandler RequestLoad { get; }

        /// <summary>
        /// Supplies authoritative metadata for a manual save request.
        /// </summary>
        public Func<string, SaveRequest> CreateSaveRequest { get; }

        /// <summary>
        /// Active slot after a load, or the project default manual slot for a
        /// fresh session. This is an ID, never a filesystem path.
        /// </summary>
        public string PreferredSaveSlotId { get; }

        /// <summary>
        /// A load failure produced by the composition root before UI binding.
        /// It is presentation-only status and never authoritative save state.
        /// </summary>
        public string InitialSaveStatus { get; }

        /// <summary>
        /// Read result produced by a clean-session startup load before UI
        /// binding. Recovery statuses are replayed as presentation feedback;
        /// they do not trigger another storage operation.
        /// </summary>
        public SaveReadStatus? InitialSaveReadStatus { get; }

        public IPlayerNeedsService PlayerNeeds { get; }

        public IPlayerMoneyService PlayerMoney { get; }

        /// <summary>
        /// Optional development-only action supplied by the composition root.
        /// Release capability filtering keeps its visual entry absent.
        /// </summary>
        public Action OpenDeveloperTools { get; }

        /// <summary>
        /// Optional fresh-session vehicle customization boundary. It is never
        /// invoked while loading an existing save.
        /// </summary>
        public NewGameVehiclePaintHandler ConfigureNewGameVehiclePaint { get; }
    }
}
