using System;
using MSC.Core.Lifecycle;
using MSC.Interaction.Carrying;
using MSC.Items;
using MSC.Player;
using MSC.Presentation.Fluid;
using UnityEngine;

namespace MSC.Needs
{
    /// <summary>
    /// Lightweight, interruptible first-person presentation for 09C actions.
    /// Gameplay state is already authoritative when this presenter receives an
    /// event; no action depends on an animation frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonLifeActionPresenter : MonoBehaviour,
        IUiVisibilityGate,
        IPlayerSubtitleSource
    {
        private enum PresentationKind
        {
            None = 0,
            Drink = 1,
            Eat = 2,
            Smoke = 3,
            Urinate = 4,
            Sleep = 5,
        }

        private Camera playerCamera;
        private Transform playerRoot;
        private FirstPersonMotor playerMotor;
        private PlayerInputRouter playerInput;
        private CharacterController characterController;
        private PhysicalCarryController carryController;
        private ItemWorldRuntime items;
        private PlayerNeedsRuntime needs;
        private Transform viewmodelRoot;
        private Transform leftArm;
        private Transform rightArm;
        private Transform heldProp;
        private ProceduralFluidStreamPresenter urineStream;
        private Material skinMaterial;
        private Material propMaterial;
        private FirstPersonLifeActionViewmodelBinding phase1Viewmodel;
        private bool phase1ViewmodelActive;
        private PresentationKind activeKind;
        private float presentationStartedAt;
        private float presentationDuration;
        private float messageUntil;
        private string message = string.Empty;
        private readonly GUIContent messageContent = new GUIContent();
        private Vector3 cameraRestPosition;
        private Quaternion cameraRestRotation;
        private Vector3 queuedSleepCameraPosition;
        private Quaternion queuedSleepPlayerRotation;
        private Vector3 wakePlayerPosition;
        private Quaternion wakePlayerRotation;
        private bool pendingSleepPose;
        private bool sleepPoseActive;
        private bool restoreInputAfterSleep;
        private bool restoreMotorAfterSleep;
        private bool restoreControllerAfterSleep;
        private bool drinkReadyActive;
        private float drinkReadyTransitionStartedAt;
        private bool continuousDrinkActive;
        private string continuousDrinkStableId = string.Empty;
        private Font uiFont;
        private GUIStyle messageStyle;
        private bool uiSuppressed;
        private bool contextHudPresenterActive;
        private bool initialized;

        private static readonly Vector3 LeftFallbackRestPosition =
            new Vector3(-0.24f, -0.34f, 0.5f);

        private static readonly Vector3 RightFallbackRestPosition =
            new Vector3(0.24f, -0.34f, 0.5f);

        public bool IsPresenting =>
            activeKind != PresentationKind.None ||
            drinkReadyActive;

        public bool IsUiSuppressed => uiSuppressed;
        public ProceduralFluidStreamPresenter UrineStream => urineStream;

        public string ActiveSubtitle =>
            initialized && isActiveAndEnabled &&
            Time.unscaledTime <= messageUntil &&
            !string.IsNullOrWhiteSpace(message)
                ? message
                : string.Empty;

        public void SetUiSuppressed(bool suppressed)
        {
            uiSuppressed = suppressed;
        }

        public void SetContextHudPresenterActive(bool active)
        {
            contextHudPresenterActive = active;
        }

        public static Rect CalculateSubtitleFrameRect(
            float screenWidth,
            float screenHeight,
            float textWidth,
            float textHeight)
        {
            const float horizontalPadding = 16f;
            const float verticalPadding = 8f;
            const float screenMargin = 24f;
            float maximumWidth = Mathf.Max(
                180f,
                Mathf.Min(860f, screenWidth - screenMargin * 2f));
            float frameWidth = Mathf.Clamp(
                textWidth + horizontalPadding * 2f,
                180f,
                maximumWidth);
            float frameHeight = Mathf.Max(
                38f,
                textHeight + verticalPadding * 2f);
            return new Rect(
                (screenWidth - frameWidth) * 0.5f,
                Mathf.Max(screenMargin, screenHeight - screenMargin - frameHeight),
                frameWidth,
                frameHeight);
        }

        public void Initialize(
            Transform configuredPlayerRoot,
            FirstPersonMotor configuredPlayerMotor,
            PlayerInputRouter configuredPlayerInput,
            Camera configuredCamera,
            ItemWorldRuntime configuredItems,
            PlayerNeedsRuntime configuredNeeds)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "First-person life-action presenter is already initialized.");
            }

            playerRoot = configuredPlayerRoot ??
                throw new ArgumentNullException(nameof(configuredPlayerRoot));
            playerMotor = configuredPlayerMotor ??
                throw new ArgumentNullException(nameof(configuredPlayerMotor));
            playerInput = configuredPlayerInput ??
                throw new ArgumentNullException(nameof(configuredPlayerInput));
            characterController =
                playerRoot.GetComponent<CharacterController>() ??
                throw new InvalidOperationException(
                    "First-person life-action presenter requires the player's " +
                    "CharacterController.");
            carryController =
                playerRoot.GetComponentInChildren<PhysicalCarryController>(true) ??
                throw new InvalidOperationException(
                    "First-person life-action presenter requires the player's " +
                    "PhysicalCarryController.");
            playerCamera = configuredCamera ??
                throw new ArgumentNullException(nameof(configuredCamera));
            items = configuredItems;
            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            cameraRestPosition = playerCamera.transform.localPosition;
            cameraRestRotation = playerCamera.transform.localRotation;
            BuildViewmodel();
            carryController.HeldUseReadyChanged +=
                HandleHeldUseReadyChanged;
            if (items != null)
            {
                items.ActionCompleted += HandleItemAction;
                items.HeldUseStateChanged += HandleHeldUseStateChanged;
            }

            needs.LifeActionStarted += HandleLifeActionStarted;
            needs.LifeActionCompleted += HandleLifeAction;
            playerInput.WaveRequested += HandleWaveRequested;
            playerInput.MiddleFingerRequested +=
                HandleMiddleFingerRequested;
            initialized = true;
            playerRoot.GetComponent<CrossdotPresenter>()?.BindSubtitleSource(this);
        }

        public void QueueSleepPose(
            Vector3 cameraWorldPosition,
            Quaternion playerWorldRotation)
        {
            if (!initialized)
            {
                throw new InvalidOperationException(
                    "First-person life-action presenter is not initialized.");
            }

            queuedSleepCameraPosition = cameraWorldPosition;
            queuedSleepPlayerRotation = playerWorldRotation.normalized;
            pendingSleepPose = true;
        }

        public void CancelQueuedSleepPose()
        {
            pendingSleepPose = false;
        }

        public bool TryPlayGesture(FirstPersonLifeActionVisual visual)
        {
            if (!initialized ||
                activeKind != PresentationKind.None ||
                drinkReadyActive ||
                continuousDrinkActive ||
                phase1Viewmodel == null)
            {
                return false;
            }

            switch (visual)
            {
                case FirstPersonLifeActionVisual.Hello:
                case FirstPersonLifeActionVisual.MiddleFinger:
                case FirstPersonLifeActionVisual.Push:
                case FirstPersonLifeActionVisual.Fist:
                    // The primitive arms are a development-only fallback.  A
                    // stale active flag leaves their capsule end at the camera
                    // origin and covers Game View with a skin-coloured blob,
                    // even though the licensed hand itself is posed correctly.
                    HidePrimitiveFallback();
                    phase1ViewmodelActive =
                        phase1Viewmodel.Play(visual);
                    if (phase1ViewmodelActive)
                    {
                        HidePrimitiveFallback();
                    }

                    return phase1ViewmodelActive;

                default:
                    return false;
            }
        }

        private void HandleWaveRequested()
        {
            TryPlayGesture(FirstPersonLifeActionVisual.Hello);
        }

        private void HandleMiddleFingerRequested()
        {
            TryPlayGesture(FirstPersonLifeActionVisual.MiddleFinger);
        }

        private void HandleLifeActionStarted(PlayerLifeActionStarted action)
        {
            if (action.Kind == PlayerLifeActionKind.Urinate)
            {
                Begin(
                    PresentationKind.Urinate,
                    action.RealTimeDuration,
                    action.Message);
            }
        }

        private void HandleItemAction(ItemActionCompleted action)
        {
            ItemDefinitionRecord actionDefinition = null;
            bool isFood = items?.Definitions != null &&
                items.Definitions.TryGet(
                    action.DefinitionId,
                    out actionDefinition) &&
                actionDefinition.Food.Edible;
            if (action.Action == ItemActionKind.ConsumptionStarted && isFood)
            {
                if (actionDefinition.Food.ConsumptionPresentation ==
                    ItemConsumptionPresentation.Drink)
                {
                    Begin(
                        PresentationKind.Drink,
                        actionDefinition.Food.ConsumptionDurationSeconds,
                        "Вы пьёте.");
                    return;
                }

                // Ordinary food remains animation-independent. The physical
                // item stays authoritative until the timed action completes.
                ShowMessage("Вы едите.", 1.35f);
                return;
            }

            if (action.Action != ItemActionKind.Used)
            {
                return;
            }

            if (isFood)
            {
                ShowMessage("Еда съедена.", 1.35f);
                return;
            }

            PresentationKind kind = ResolveItemPresentation(action);
            if (kind == PresentationKind.None)
            {
                // A generic tool-use event is feedback, not evidence that the
                // player ate the tool. Keep the message but do not start the
                // food viewmodel for jacks and other non-consumable items.
                ShowMessage("Предмет использован.", 1.35f);
                return;
            }

            if (kind == PresentationKind.Drink &&
                continuousDrinkActive &&
                string.Equals(
                    continuousDrinkStableId,
                    action.StableId.Value,
                    StringComparison.Ordinal))
            {
                return;
            }

            string label = kind switch
            {
                PresentationKind.Drink => "Вы пьёте.",
                PresentationKind.Eat => "Вы едите.",
                PresentationKind.Smoke => "Вы курите.",
                _ => "Предмет использован.",
            };
            Begin(kind, kind == PresentationKind.Smoke ? 2.5f : 1.35f, label);
        }

        private void HandleHeldUseStateChanged(ItemHeldUseStateChanged state)
        {
            if (state.Phase == ItemHeldUsePhase.Started)
            {
                if (carryController == null ||
                    !string.Equals(
                        carryController.HeldStableId,
                        state.StableId.Value,
                        StringComparison.Ordinal) ||
                    !IsDrinkDefinition(state.DefinitionId))
                {
                    return;
                }

                continuousDrinkActive = true;
                continuousDrinkStableId = state.StableId.Value;
                Begin(PresentationKind.Drink, 0.24f, "Вы пьёте.");
                return;
            }

            if (!continuousDrinkActive ||
                !string.Equals(
                    continuousDrinkStableId,
                    state.StableId.Value,
                    StringComparison.Ordinal))
            {
                return;
            }

            continuousDrinkActive = false;
            continuousDrinkStableId = string.Empty;
            if (activeKind == PresentationKind.Drink)
            {
                EndPresentation();
            }
        }

        private void HandleHeldUseReadyChanged(bool isReady)
        {
            drinkReadyActive = isReady;
            if (isReady)
            {
                drinkReadyTransitionStartedAt = Time.unscaledTime;
                if (activeKind == PresentationKind.None)
                {
                    EnterDrinkReadyPresentation(immediate: false);
                }

                return;
            }

            if (activeKind == PresentationKind.Drink)
            {
                EndPresentation();
                return;
            }

            if (phase1ViewmodelActive &&
                phase1Viewmodel != null &&
                phase1Viewmodel.ActiveVisual ==
                    FirstPersonLifeActionVisual.Drink)
            {
                phase1Viewmodel.Stop();
                phase1ViewmodelActive = false;
            }

            SetViewmodelVisible(false);
            carryController?.ClearHeldPresentationPose();
        }

        private void HandleLifeAction(PlayerLifeActionCompleted action)
        {
            if (!action.Succeeded)
            {
                ShowMessage(action.Message, 1.8f);
                return;
            }

            switch (action.Kind)
            {
                case PlayerLifeActionKind.Sleep:
                    Begin(PresentationKind.Sleep, 2.2f, action.Message);
                    break;
                case PlayerLifeActionKind.Urinate:
                    if (activeKind == PresentationKind.Urinate)
                    {
                        EndPresentation();
                    }

                    ShowMessage(action.Message, 1.8f);
                    break;
            }
        }

        private PresentationKind ResolveItemPresentation(
            ItemActionCompleted action)
        {
            if (items?.Definitions != null &&
                items.Definitions.TryGet(
                    action.DefinitionId,
                    out ItemDefinitionRecord definition))
            {
                if (string.Equals(
                        definition.FeatureId,
                        "P1.ITEM.111",
                        StringComparison.Ordinal))
                {
                    return PresentationKind.Smoke;
                }

                if (definition.Food.ConsumptionPresentation ==
                    ItemConsumptionPresentation.Drink)
                {
                    return PresentationKind.Drink;
                }

                if (definition.ContentMeasure == ItemContentMeasure.Litres ||
                    action.UseEffects.Thirst < -0.0001f ||
                    action.UseEffects.Intoxication > 0.0001f)
                {
                    return PresentationKind.Drink;
                }

                if (definition.PrimaryAction == ItemPrimaryAction.Consume)
                {
                    return PresentationKind.Eat;
                }
            }

            return PresentationKind.None;
        }

        private bool IsDrinkDefinition(string definitionId)
        {
            return items?.Definitions != null &&
                   items.Definitions.TryGet(
                       definitionId,
                       out ItemDefinitionRecord definition) &&
                   definition.PrimaryAction == ItemPrimaryAction.Consume &&
                   (definition.ContentMeasure == ItemContentMeasure.Litres ||
                    definition.Food.ConsumptionPresentation ==
                        ItemConsumptionPresentation.Drink);
        }

        private void Begin(
            PresentationKind kind,
            float duration,
            string label)
        {
            if (sleepPoseActive)
            {
                ExitSleepPose();
            }

            RestoreCamera();
            bool transitionFromDrinkReady =
                kind == PresentationKind.Drink &&
                drinkReadyActive &&
                phase1ViewmodelActive &&
                phase1Viewmodel != null &&
                phase1Viewmodel.ActiveVisual ==
                    FirstPersonLifeActionVisual.Drink;
            if (!transitionFromDrinkReady)
            {
                phase1Viewmodel?.Stop();
            }

            phase1ViewmodelActive = TryBeginPhase1Viewmodel(kind);
            heldProp?.gameObject.SetActive(false);
            activeKind = kind;
            presentationStartedAt = Time.unscaledTime;
            presentationDuration = Mathf.Max(0.1f, duration);
            ShowMessage(label, duration);
            SetViewmodelVisible(
                kind == PresentationKind.Eat ||
                (!phase1ViewmodelActive &&
                 (kind == PresentationKind.Drink ||
                  kind == PresentationKind.Smoke)));
            urineStream.SetFlowing(
                kind == PresentationKind.Urinate,
                clearExisting: kind != PresentationKind.Urinate);
            if (kind == PresentationKind.Urinate)
            {
                urineStream.SetIntensity(
                    Mathf.Lerp(
                        0.18f,
                        1f,
                        needs.Snapshot.NormalizedUrine));
            }
            ConfigureProp(kind);
            if (kind == PresentationKind.Sleep)
            {
                EnterQueuedSleepPose();
            }
            else
            {
                pendingSleepPose = false;
            }
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (activeKind == PresentationKind.None)
            {
                if (phase1Viewmodel != null &&
                    phase1Viewmodel.ActiveVisual !=
                        FirstPersonLifeActionVisual.None)
                {
                    HidePrimitiveFallback();
                }

                if (drinkReadyActive)
                {
                    float readyNormalized = Mathf.Clamp01(
                        (Time.unscaledTime -
                         drinkReadyTransitionStartedAt) / 0.18f);
                    ApplyDrinkReadyCarryPose(
                        Mathf.SmoothStep(0f, 1f, readyNormalized));
                }

                return;
            }

            float elapsed = Time.unscaledTime - presentationStartedAt;
            if (continuousDrinkActive &&
                activeKind == PresentationKind.Drink)
            {
                float entry = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / presentationDuration));
                AnimateViewmodel(entry, entry);
                return;
            }

            float normalized = Mathf.Clamp01(elapsed / presentationDuration);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            AnimateViewmodel(normalized, envelope);
            if (normalized >= 1f)
            {
                EndPresentation();
            }
        }

        private void AnimateViewmodel(float normalized, float envelope)
        {
            switch (activeKind)
            {
                case PresentationKind.Drink:
                    if (!phase1ViewmodelActive)
                    {
                        leftArm.localPosition = Vector3.Lerp(
                            LeftFallbackRestPosition,
                            new Vector3(-0.11f, -0.04f, 0.31f),
                            envelope);
                        rightArm.localPosition = Vector3.Lerp(
                            RightFallbackRestPosition,
                            new Vector3(0.1f, -0.02f, 0.3f),
                            envelope);
                    }

                    if (phase1ViewmodelActive &&
                        phase1Viewmodel?.DrinkGripAnchor != null)
                    {
                        carryController?.SetHeldPresentationAnchor(
                            phase1Viewmodel.DrinkGripAnchor,
                            1f);
                    }
                    else
                    {
                        carryController?.SetHeldPresentationPose(
                            new Vector3(0.03f, 0.19f, -0.93f),
                            Quaternion.Euler(72f, 0f, -10f),
                            envelope);
                    }

                    break;
                case PresentationKind.Eat:
                    leftArm.localPosition = Vector3.Lerp(
                        LeftFallbackRestPosition,
                        new Vector3(-0.08f, -0.02f, 0.3f),
                        envelope);
                    rightArm.localPosition = Vector3.Lerp(
                        RightFallbackRestPosition,
                        new Vector3(0.08f, -0.02f, 0.3f),
                        envelope);
                    heldProp.localPosition =
                        new Vector3(0f, -0.03f + envelope * 0.08f, 0.28f);
                    break;
                case PresentationKind.Smoke:
                    if (phase1ViewmodelActive &&
                        phase1Viewmodel?.CigaretteGripAnchor != null)
                    {
                        heldProp.gameObject.SetActive(true);
                        heldProp.SetParent(
                            phase1Viewmodel.CigaretteGripAnchor,
                            worldPositionStays: false);
                        heldProp.SetLocalPositionAndRotation(
                            new Vector3(0f, 0f, -0.028f),
                            Quaternion.Euler(90f, 0f, 0f));
                    }

                    if (!phase1ViewmodelActive)
                    {
                        rightArm.localPosition = Vector3.Lerp(
                            RightFallbackRestPosition,
                            new Vector3(0.09f, -0.02f, 0.28f),
                            envelope);
                        leftArm.localPosition =
                            new Vector3(-0.27f, -0.38f, 0.53f);
                        heldProp.localPosition = Vector3.Lerp(
                            new Vector3(0.2f, -0.25f, 0.5f),
                            new Vector3(0.035f, 0.015f, 0.2f),
                            envelope);
                        heldProp.localRotation =
                            Quaternion.Euler(0f, 0f, 90f);
                    }

                    break;
                case PresentationKind.Urinate:
                    urineStream.SetIntensity(
                        Mathf.Lerp(
                            0.12f,
                            1f,
                            needs.Snapshot.NormalizedUrine));
                    break;
                case PresentationKind.Sleep:
                    float tilt = Mathf.SmoothStep(0f, 1f, envelope);
                    playerCamera.transform.localPosition =
                        cameraRestPosition + new Vector3(0f, -0.32f * tilt, 0f);
                    playerCamera.transform.localRotation =
                        cameraRestRotation *
                        Quaternion.Euler(0f, 0f, 70f * tilt);
                    break;
            }
        }

        private void EndPresentation()
        {
            bool wasSleeping = activeKind == PresentationKind.Sleep;
            bool returnToDrinkReady =
                activeKind == PresentationKind.Drink &&
                drinkReadyActive &&
                carryController != null &&
                carryController.IsHeldUseReady;
            activeKind = PresentationKind.None;
            bool retainedPhase1DrinkReady =
                returnToDrinkReady &&
                phase1Viewmodel != null &&
                phase1Viewmodel.ShowDrinkReady(smooth: true);
            phase1ViewmodelActive = retainedPhase1DrinkReady;
            SetViewmodelVisible(false);
            heldProp?.gameObject.SetActive(false);
            if (!retainedPhase1DrinkReady)
            {
                phase1Viewmodel?.Stop();
                carryController?.ClearHeldPresentationPose();
            }

            urineStream.SetFlowing(false);
            RestoreCamera();
            if (wasSleeping)
            {
                ExitSleepPose();
            }

            if (returnToDrinkReady)
            {
                if (retainedPhase1DrinkReady)
                {
                    drinkReadyTransitionStartedAt =
                        Time.unscaledTime - 0.18f;
                    ApplyDrinkReadyCarryPose(1f);
                }
                else
                {
                    EnterDrinkReadyPresentation(immediate: true);
                }
            }
        }

        private void EnterDrinkReadyPresentation(bool immediate)
        {
            if (!drinkReadyActive ||
                carryController == null ||
                !carryController.IsHeldUseReady)
            {
                return;
            }

            phase1ViewmodelActive =
                phase1Viewmodel != null &&
                phase1Viewmodel.ShowDrinkReady(smooth: !immediate);
            SetViewmodelVisible(!phase1ViewmodelActive);
            heldProp?.gameObject.SetActive(false);
            if (!phase1ViewmodelActive)
            {
                leftArm.localPosition =
                    new Vector3(-0.11f, -0.04f, 0.31f);
                rightArm.localPosition =
                    new Vector3(0.1f, -0.02f, 0.3f);
            }

            if (immediate)
            {
                drinkReadyTransitionStartedAt =
                    Time.unscaledTime - 0.18f;
            }

            ApplyDrinkReadyCarryPose(immediate ? 1f : 0f);
        }

        private void ApplyDrinkReadyCarryPose(float weight)
        {
            if (phase1ViewmodelActive &&
                phase1Viewmodel?.DrinkGripAnchor != null)
            {
                carryController?.SetHeldPresentationAnchor(
                    phase1Viewmodel.DrinkGripAnchor,
                    weight);
                return;
            }

            carryController?.SetHeldPresentationPose(
                new Vector3(0.03f, 0.19f, -0.93f),
                Quaternion.Euler(72f, 0f, -10f),
                weight);
        }

        private void BuildViewmodel()
        {
            skinMaterial = BuildMaterial(
                "MSC 09C Viewmodel Skin",
                new Color(0.64f, 0.38f, 0.25f, 1f));
            propMaterial = BuildMaterial(
                "MSC 09C Viewmodel Prop",
                new Color(0.25f, 0.12f, 0.04f, 1f));
            var root = new GameObject("09C Life Action Viewmodel")
            {
                hideFlags = HideFlags.DontSave,
            };
            viewmodelRoot = root.transform;
            viewmodelRoot.SetParent(playerCamera.transform, false);
            if (!FirstPersonLifeActionViewmodelBinding.TryInstantiateLocalPhase1(
                    playerCamera.transform,
                    out phase1Viewmodel))
            {
                Debug.LogError(
                    "Required Phase 1 player viewmodel is unavailable or " +
                    "incomplete. Primitive action arms remain enabled only as " +
                    "an explicit Editor/development fallback; production " +
                    "builds are blocked by the legacy-presentation build guard.",
                    this);
            }
            leftArm = CreatePrimitive(
                "Left Action Arm",
                PrimitiveType.Capsule,
                skinMaterial,
                viewmodelRoot,
                new Vector3(0.11f, 0.3f, 0.11f));
            rightArm = CreatePrimitive(
                "Right Action Arm",
                PrimitiveType.Capsule,
                skinMaterial,
                viewmodelRoot,
                new Vector3(0.11f, 0.3f, 0.11f));
            heldProp = CreatePrimitive(
                "Action Prop",
                PrimitiveType.Cylinder,
                propMaterial,
                viewmodelRoot,
                new Vector3(0.04f, 0.13f, 0.04f));
            leftArm.localRotation = Quaternion.Euler(70f, 0f, -12f);
            rightArm.localRotation = Quaternion.Euler(70f, 0f, 12f);
            leftArm.localPosition = LeftFallbackRestPosition;
            rightArm.localPosition = RightFallbackRestPosition;

            var stream = new GameObject("Urine Stream")
            {
                hideFlags = HideFlags.DontSave,
            };
            stream.transform.SetParent(viewmodelRoot, false);
            urineStream =
                stream.AddComponent<ProceduralFluidStreamPresenter>();
            urineStream.Configure(
                FluidStreamProfile.Urine,
                new Vector3(0f, -0.5f, 0.6f),
                new Vector3(0f, -0.28f, 1f),
                ~0);
            viewmodelRoot.gameObject.SetActive(true);
            SetViewmodelVisible(false);
            heldProp.gameObject.SetActive(false);
            urineStream.SetFlowing(false, clearExisting: true);
        }

        private void ConfigureProp(PresentationKind kind)
        {
            heldProp.gameObject.SetActive(
                kind == PresentationKind.Eat ||
                kind == PresentationKind.Smoke);
            heldProp.localScale = kind switch
            {
                // Unity's primitive cylinder is two metres long before scale.
                // This produces an 8 x 90 mm cigarette, not a comedy baton.
                PresentationKind.Smoke => new Vector3(0.008f, 0.045f, 0.008f),
                _ => new Vector3(0.08f, 0.06f, 0.08f),
            };
            SetMaterialColor(
                propMaterial,
                kind == PresentationKind.Smoke
                    ? new Color(0.91f, 0.88f, 0.80f, 1f)
                    : new Color(0.25f, 0.12f, 0.04f, 1f));
            if (kind != PresentationKind.Smoke || !phase1ViewmodelActive)
            {
                heldProp.SetParent(viewmodelRoot, worldPositionStays: false);
            }
        }

        private void SetViewmodelVisible(bool visible)
        {
            leftArm?.gameObject.SetActive(visible);
            rightArm?.gameObject.SetActive(visible);
        }

        private void HidePrimitiveFallback()
        {
            SetViewmodelVisible(false);
            heldProp?.gameObject.SetActive(false);
            if (heldProp != null && viewmodelRoot != null &&
                heldProp.parent != viewmodelRoot)
            {
                heldProp.SetParent(viewmodelRoot, worldPositionStays: false);
            }
        }

        /// <summary>
        /// Bounded shared feedback surface for explicitly composed Phase 1
        /// interactions that do not own a dedicated HUD presenter yet.
        /// </summary>
        public void ShowStatusMessage(
            string value,
            float duration = 1.8f)
        {
            if (initialized)
            {
                ShowMessage(value, duration);
            }
        }

        private void ShowMessage(string value, float duration)
        {
            message = value ?? string.Empty;
            messageUntil = Time.unscaledTime + Mathf.Max(0.1f, duration);
        }

        private void OnGUI()
        {
            if (!initialized || uiSuppressed ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (activeKind == PresentationKind.Sleep)
            {
                float normalized = Mathf.Clamp01(
                    (Time.unscaledTime - presentationStartedAt) /
                    presentationDuration);
                float alpha = Mathf.Sin(normalized * Mathf.PI) * 0.96f;
                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.DrawTexture(
                    new Rect(0f, 0f, Screen.width, Screen.height),
                    Texture2D.whiteTexture);
                GUI.color = previous;
            }

            if (contextHudPresenterActive)
            {
                return;
            }

            string activeSubtitle = ActiveSubtitle;
            if (string.IsNullOrEmpty(activeSubtitle))
            {
                return;
            }

            if (uiFont == null)
            {
                uiFont = Resources.Load<Font>("Fonts/HelveticaNeueRoman");
            }

            messageStyle ??= new GUIStyle(GUI.skin.label)
            {
                font = uiFont != null ? uiFont : GUI.skin.font,
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                wordWrap = true,
                clipping = TextClipping.Clip,
                normal = { textColor = Color.white },
            };
            messageContent.text = activeSubtitle;
            float maximumTextWidth = Mathf.Max(
                148f,
                Mathf.Min(828f, Screen.width * 0.7f));
            Vector2 naturalSize = messageStyle.CalcSize(messageContent);
            float textWidth = Mathf.Min(maximumTextWidth, naturalSize.x);
            float textHeight = messageStyle.CalcHeight(
                messageContent,
                textWidth);
            Rect frame = CalculateSubtitleFrameRect(
                Screen.width,
                Screen.height,
                textWidth,
                textHeight);
            Color subtitlePreviousColor = GUI.color;
            GUI.color = new Color(0.012f, 0.01f, 0.008f, 0.72f);
            GUI.DrawTexture(
                frame,
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                alphaBlend: true);
            GUI.color = subtitlePreviousColor;
            Rect textRect = frame;
            textRect.x += 16f;
            textRect.width -= 32f;
            textRect.y += 8f;
            textRect.height -= 16f;
            GUI.Label(textRect, messageContent, messageStyle);
        }

        private void RestoreCamera()
        {
            if (playerCamera == null)
            {
                return;
            }

            playerCamera.transform.localPosition = cameraRestPosition;
            playerCamera.transform.localRotation = cameraRestRotation;
        }

        private void EnterQueuedSleepPose()
        {
            if (!pendingSleepPose ||
                playerRoot == null ||
                playerCamera == null)
            {
                pendingSleepPose = false;
                return;
            }

            pendingSleepPose = false;
            wakePlayerPosition = playerRoot.position;
            wakePlayerRotation = playerRoot.rotation;
            restoreInputAfterSleep =
                playerInput != null && playerInput.IsGameplayInputEnabled;
            restoreMotorAfterSleep =
                playerMotor != null && playerMotor.enabled;
            restoreControllerAfterSleep =
                characterController != null && characterController.enabled;

            if (restoreInputAfterSleep)
            {
                playerInput.SetGameplayInputEnabled(false);
            }

            if (playerMotor != null)
            {
                playerMotor.ResetInputIntent();
                playerMotor.enabled = false;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            Vector3 rootToCamera =
                playerRoot.InverseTransformPoint(playerCamera.transform.position);
            playerRoot.SetPositionAndRotation(
                queuedSleepCameraPosition -
                queuedSleepPlayerRotation * rootToCamera,
                queuedSleepPlayerRotation);
            Physics.SyncTransforms();
            sleepPoseActive = true;
        }

        private void ExitSleepPose()
        {
            if (!sleepPoseActive)
            {
                return;
            }

            if (characterController != null)
            {
                characterController.enabled = false;
            }

            playerRoot.SetPositionAndRotation(
                wakePlayerPosition,
                wakePlayerRotation);
            Physics.SyncTransforms();

            if (characterController != null)
            {
                characterController.enabled = restoreControllerAfterSleep;
            }

            if (playerMotor != null)
            {
                playerMotor.ResetInputIntent();
                playerMotor.enabled = restoreMotorAfterSleep;
            }

            if (playerInput != null && restoreInputAfterSleep)
            {
                playerInput.SetGameplayInputEnabled(true);
            }

            sleepPoseActive = false;
        }

        private void OnDisable()
        {
            if (!initialized)
            {
                return;
            }

            activeKind = PresentationKind.None;
            phase1Viewmodel?.Stop();
            phase1ViewmodelActive = false;
            drinkReadyActive = false;
            continuousDrinkActive = false;
            continuousDrinkStableId = string.Empty;
            carryController?.ClearHeldPresentationPose();
            SetViewmodelVisible(false);
            if (heldProp != null)
            {
                heldProp.gameObject.SetActive(false);
            }

            if (urineStream != null)
            {
                urineStream.SetFlowing(false, clearExisting: true);
            }

            RestoreCamera();
            ExitSleepPose();
        }

        private void OnDestroy()
        {
            playerRoot?.GetComponent<CrossdotPresenter>()
                ?.UnbindSubtitleSource(this);
            if (playerInput != null)
            {
                playerInput.WaveRequested -= HandleWaveRequested;
                playerInput.MiddleFingerRequested -=
                    HandleMiddleFingerRequested;
            }

            if (items != null)
            {
                items.ActionCompleted -= HandleItemAction;
                items.HeldUseStateChanged -= HandleHeldUseStateChanged;
            }

            if (needs != null)
            {
                needs.LifeActionStarted -= HandleLifeActionStarted;
                needs.LifeActionCompleted -= HandleLifeAction;
            }

            if (carryController != null)
            {
                carryController.HeldUseReadyChanged -=
                    HandleHeldUseReadyChanged;
            }

            RestoreCamera();
            ExitSleepPose();
            carryController?.ClearHeldPresentationPose();
            if (phase1Viewmodel != null)
            {
                DestroyOwned(phase1Viewmodel.gameObject);
                phase1Viewmodel = null;
            }

            DestroyOwned(skinMaterial);
            DestroyOwned(propMaterial);
        }

        private static Transform CreatePrimitive(
            string objectName,
            PrimitiveType type,
            Material material,
            Transform parent,
            Vector3 scale)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = objectName;
            instance.hideFlags = HideFlags.DontSave;
            instance.transform.SetParent(parent, false);
            instance.transform.localScale = scale;
            if (instance.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
                Destroy(collider);
            }

            if (instance.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return instance.transform;
        }

        private bool TryBeginPhase1Viewmodel(PresentationKind kind)
        {
            if (phase1Viewmodel == null)
            {
                return false;
            }

            FirstPersonLifeActionVisual visual = kind switch
            {
                PresentationKind.Drink =>
                    FirstPersonLifeActionVisual.Drink,
                PresentationKind.Smoke =>
                    FirstPersonLifeActionVisual.Smoke,
                _ => FirstPersonLifeActionVisual.None,
            };
            return visual != FirstPersonLifeActionVisual.None &&
                   phase1Viewmodel.Play(visual);
        }

        private static Material BuildMaterial(string name, Color color)
        {
            Shader shader =
                Shader.Find("HDRP/Unlit") ??
                Shader.Find("HDRP/Lit") ??
                Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.DontSave,
                enableInstancing = true,
                color = color,
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_UnlitColor"))
            {
                material.SetColor("_UnlitColor", color);
            }

            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_UnlitColor"))
            {
                material.SetColor("_UnlitColor", color);
            }
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value != null)
            {
                Destroy(value);
            }
        }
    }
}
