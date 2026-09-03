using System;
using MSC.Core.Time;
using MSC.Items;
using MSC.Player;
using UnityEngine;

namespace MSC.Needs
{
    public enum PlayerLifeActionKind
    {
        Sleep = 0,
        Urinate = 1,
    }

    public readonly struct PlayerLifeActionStarted
    {
        public PlayerLifeActionStarted(
            PlayerLifeActionKind kind,
            float realTimeDuration,
            string message)
        {
            Kind = kind;
            RealTimeDuration = Mathf.Max(0f, realTimeDuration);
            Message = message ?? string.Empty;
        }

        public PlayerLifeActionKind Kind { get; }
        public float RealTimeDuration { get; }
        public string Message { get; }
    }

    public readonly struct PlayerLifeActionCompleted
    {
        public PlayerLifeActionCompleted(
            PlayerLifeActionKind kind,
            bool succeeded,
            double advancedGameSeconds,
            string message)
        {
            Kind = kind;
            Succeeded = succeeded;
            AdvancedGameSeconds = advancedGameSeconds;
            Message = message ?? string.Empty;
        }

        public PlayerLifeActionKind Kind { get; }
        public bool Succeeded { get; }
        public double AdvancedGameSeconds { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Project-owned survival state. Donor FSMs are evidence only; all rates are
    /// explicit authoring values and remain calibration-pending until repeated
    /// frozen-version measurements are available.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerNeedsRuntime : MonoBehaviour,
        IPlayerNeedsService,
        IPlayerNeedsEffectSink
    {
        [Header("Provisional rates per game hour")]
        [SerializeField, Min(0f)] private float thirstPerGameHour = 4f;
        [SerializeField, Min(0f)] private float hungerPerGameHour = 3f;
        [SerializeField, Min(0f)] private float stressPerGameHour = 0.8f;
        [SerializeField, Min(0f)] private float urinePerGameHour = 2f;
        [SerializeField, Min(0f)] private float fatiguePerGameHour = 2.5f;
        [SerializeField, Min(0f)] private float dirtinessPerGameHour = 1.5f;

        [Header("Running modifiers per game hour")]
        [SerializeField, Min(0f)] private float runningThirstPerGameHour = 2f;
        [SerializeField, Min(0f)] private float runningFatiguePerGameHour = 2.5f;
        [SerializeField, Min(0f)] private float runningDirtinessPerGameHour = 1f;

        [Header("Delayed digestion and metabolism per game hour")]
        [SerializeField, Min(0.01f)]
        private float nutritionAbsorptionPointsPerGameHour = 120f;
        [SerializeField, Min(0.01f)]
        private float hydrationAbsorptionPointsPerGameHour = 180f;
        [SerializeField, Min(0.01f)]
        private float weightAbsorptionKilogramsPerGameHour = 2f;
        [SerializeField, Min(0.01f)]
        private float alcoholAbsorptionPointsPerGameHour = 240f;
        [SerializeField, Min(0f)]
        private float alcoholClearancePointsPerGameHour = 2f;
        [SerializeField, Min(0f)]
        private float hangoverRecoveryPointsPerGameHour = 0.25f;
        [SerializeField, Range(0f, 1f)]
        private float hangoverPerClearedAlcoholPoint = 0.8f;

        [Header("Basic life actions (calibration pending)")]
        [SerializeField, Range(1f, 16f)] private float sleepGameHours = 8f;
        [SerializeField, Range(0f, 100f)] private float sleepFatigueRelief = 75f;
        [SerializeField, Range(0f, 100f)] private float sleepStressRelief = 15f;
        [SerializeField, Range(0f, 100f)] private float minimumUrineToVoid = 2f;
        [SerializeField, Range(0f, 25f)] private float urinationDirtiness = 2f;
        [SerializeField, Range(1f, 12f)] private float minimumUrinationSeconds = 2.5f;
        [SerializeField, Range(1f, 12f)] private float maximumUrinationSeconds = 7.5f;

        private IGameTimeService gameTime;
        private IGameTimeAdvanceService gameTimeAdvance;
        private FirstPersonMotor motor;
        private ItemWorldRuntime items;
        private IDisposable timeSubscription;
        private ulong revision;
        private float thirst;
        private float hunger;
        private float stress;
        private float urine;
        private float fatigue;
        private float dirtiness;
        private float weightKilograms = 83f;
        private float intoxication;
        private float hangover;
        private float pendingHungerEffect;
        private float pendingThirstEffect;
        private float pendingWeightEffect;
        private float pendingIntoxicationEffect;
        private float pendingUrineEffect;
        private float pendingFatigueEffect;
        private float pendingDirtinessEffect;
        private double lastProcessedElapsedGameSeconds;
        private float urinationElapsedSeconds;
        private float urinationDurationSeconds;
        private float urinationDrainPointsPerSecond;
        private float urinationPublishAccumulator;
        private bool urinationActive;
        private bool initialized;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool developmentProgressionDisabled;
#endif

        public event Action<PlayerNeedsSnapshot> StateChanged;
        public event Action<PlayerLifeActionStarted> LifeActionStarted;
        public event Action<PlayerLifeActionCompleted> LifeActionCompleted;

        public PlayerNeedsSnapshot Snapshot =>
            new PlayerNeedsSnapshot(
                revision,
                thirst,
                hunger,
                stress,
                urine,
                fatigue,
                dirtiness,
                weightKilograms,
                intoxication,
                hangover,
                pendingHungerEffect,
                pendingThirstEffect,
                pendingWeightEffect,
                pendingIntoxicationEffect,
                pendingUrineEffect,
                pendingFatigueEffect,
                pendingDirtinessEffect);

        public bool IsInitialized => initialized;
        public bool IsUrinating => urinationActive;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DevProgressionDisabled =>
            developmentProgressionDisabled;
#endif
        public float UrinationProgress => !urinationActive ||
            urinationDurationSeconds <= 0.0001f
                ? 0f
                : Mathf.Clamp01(
                    urinationElapsedSeconds / urinationDurationSeconds);

        public void Initialize(
            IGameTimeService configuredGameTime,
            FirstPersonMotor configuredMotor,
            ItemWorldRuntime configuredItems,
            IGameTimeAdvanceService configuredTimeAdvance = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Player needs runtime is already initialized.");
            }

            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            gameTimeAdvance = configuredTimeAdvance;
            motor = configuredMotor ??
                throw new ArgumentNullException(nameof(configuredMotor));
            items = configuredItems;
            lastProcessedElapsedGameSeconds =
                gameTime.Snapshot.ElapsedGameSeconds;
            timeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
            if (items != null)
            {
                items.ActionCompleted += HandleItemAction;
            }

            SynchronizeMotorMass();
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || gameTime == null)
            {
                return;
            }

            if (IsDevelopmentProgressionDisabled)
            {
                // Prevent a delayed catch-up when the developer enables needs
                // again after the game clock has continued to advance.
                lastProcessedElapsedGameSeconds =
                    gameTime.Snapshot.ElapsedGameSeconds;
                return;
            }

            // The game-time event is the primary path. Polling the same
            // authoritative snapshot is an idempotent watchdog: it prevents
            // needs from silently stalling if component update order delays or
            // suppresses an observer notification, without advancing on pause.
            ProcessThrough(gameTime.Snapshot.ElapsedGameSeconds);
            AdvanceActiveLifeAction(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Advances real-time life actions independently from the game clock.
        /// Public for deterministic validation; ordinary runtime ownership stays
        /// in Update.
        /// </summary>
        public void AdvanceActiveLifeAction(float elapsedRealSeconds)
        {
            if (!initialized ||
                !urinationActive ||
                !float.IsFinite(elapsedRealSeconds) ||
                elapsedRealSeconds <= 0f)
            {
                return;
            }

            float step = Mathf.Min(
                elapsedRealSeconds,
                urinationDurationSeconds - urinationElapsedSeconds);
            urinationElapsedSeconds += step;
            urine = AddNeed(
                urine,
                -urinationDrainPointsPerSecond * step);
            urinationPublishAccumulator += step;
            if (urinationPublishAccumulator >= 0.1f)
            {
                urinationPublishAccumulator = 0f;
                PublishChanged();
            }

            if (urinationElapsedSeconds + 0.0001f <
                urinationDurationSeconds)
            {
                return;
            }

            urinationActive = false;
            urinationElapsedSeconds = 0f;
            urinationDurationSeconds = 0f;
            urinationDrainPointsPerSecond = 0f;
            urinationPublishAccumulator = 0f;
            dirtiness = AddNeed(dirtiness, urinationDirtiness);
            PublishChanged();
            PublishLifeAction(
                PlayerLifeActionKind.Urinate,
                true,
                0d,
                "Мочевой пузырь опустошён.");
        }

        public PlayerNeedsSaveDto CaptureDto() =>
            PlayerNeedsSaveDto.Create(Snapshot);

        public bool TryRestoreDto(
            PlayerNeedsSaveDto dto,
            out string failure)
        {
            if (!initialized)
            {
                failure = "Player needs runtime is not initialized.";
                return false;
            }

            if (dto == null)
            {
                failure = "Player needs state is missing.";
                return false;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            thirst = dto.thirst;
            hunger = dto.hunger;
            stress = dto.stress;
            urine = dto.urine;
            fatigue = dto.fatigue;
            dirtiness = dto.dirtiness;
            weightKilograms = dto.weightKilograms;
            intoxication = dto.intoxication;
            hangover = dto.hangover;
            pendingHungerEffect = dto.pendingHungerEffect;
            pendingThirstEffect = dto.pendingThirstEffect;
            pendingWeightEffect = dto.pendingWeightEffect;
            pendingIntoxicationEffect =
                dto.pendingIntoxicationEffect;
            pendingUrineEffect = dto.pendingUrineEffect;
            pendingFatigueEffect = dto.pendingFatigueEffect;
            pendingDirtinessEffect = dto.pendingDirtinessEffect;
            lastProcessedElapsedGameSeconds =
                gameTime.Snapshot.ElapsedGameSeconds;
            CancelTransientLifeActions();
            PublishChanged();
            failure = string.Empty;
            return true;
        }

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            if (IsDevelopmentProgressionDisabled)
            {
                lastProcessedElapsedGameSeconds =
                    gameTimeEvent.Current.ElapsedGameSeconds;
                return;
            }

            if (gameTimeEvent.Kind == GameTimeEventKind.StateRestored)
            {
                lastProcessedElapsedGameSeconds =
                    gameTimeEvent.Current.ElapsedGameSeconds;
                return;
            }

            if (gameTimeEvent.Kind == GameTimeEventKind.Advanced)
            {
                ProcessThrough(gameTimeEvent.Current.ElapsedGameSeconds);
            }
        }

        private void ProcessThrough(double elapsedGameSeconds)
        {
            if (!double.IsFinite(elapsedGameSeconds))
            {
                return;
            }

            double deltaSeconds =
                elapsedGameSeconds - lastProcessedElapsedGameSeconds;
            if (deltaSeconds <= 0d)
            {
                if (deltaSeconds < 0d)
                {
                    lastProcessedElapsedGameSeconds = elapsedGameSeconds;
                }

                return;
            }

            lastProcessedElapsedGameSeconds = elapsedGameSeconds;
            float gameHours = (float)(deltaSeconds / 3600d);
            bool running = motor != null && motor.LocomotionState.Running;
            const float MaximumSimulationStepHours = 0.25f;
            while (gameHours > 0.000001f)
            {
                float stepHours = Mathf.Min(
                    gameHours,
                    MaximumSimulationStepHours);
                ProcessGameHours(stepHours, running);
                gameHours -= stepHours;
            }

            PublishChanged();
        }

        private void ProcessGameHours(float gameHours, bool running)
        {
            thirst = AddNeed(
                thirst,
                (thirstPerGameHour +
                 (running ? runningThirstPerGameHour : 0f)) * gameHours);
            hunger = AddNeed(hunger, hungerPerGameHour * gameHours);
            stress = AddNeed(stress, stressPerGameHour * gameHours);
            urine = AddNeed(urine, urinePerGameHour * gameHours);
            fatigue = AddNeed(
                fatigue,
                (fatiguePerGameHour +
                 (running ? runningFatiguePerGameHour : 0f)) * gameHours);
            dirtiness = AddNeed(
                dirtiness,
                (dirtinessPerGameHour +
                 (running ? runningDirtinessPerGameHour : 0f)) * gameHours);
            ApplyPendingNeed(
                ref hunger,
                ref pendingHungerEffect,
                nutritionAbsorptionPointsPerGameHour * gameHours);
            ApplyPendingNeed(
                ref thirst,
                ref pendingThirstEffect,
                hydrationAbsorptionPointsPerGameHour * gameHours);
            ApplyPendingWeight(
                weightAbsorptionKilogramsPerGameHour * gameHours);
            ApplyPendingNeed(
                ref intoxication,
                ref pendingIntoxicationEffect,
                alcoholAbsorptionPointsPerGameHour * gameHours);
            float intoxicationBeforeClearance = intoxication;
            intoxication = AddNeed(
                intoxication,
                -alcoholClearancePointsPerGameHour * gameHours);
            float clearedAlcohol =
                Mathf.Max(0f, intoxicationBeforeClearance - intoxication);
            hangover = AddNeed(
                hangover,
                clearedAlcohol * hangoverPerClearedAlcoholPoint);
            if (intoxication <= 0.1f &&
                pendingIntoxicationEffect <= 0.1f)
            {
                hangover = AddNeed(
                    hangover,
                    -hangoverRecoveryPointsPerGameHour * gameHours);
            }

            if (hangover > 0f &&
                intoxication <= 0.1f &&
                pendingIntoxicationEffect <= 0.1f)
            {
                stress = AddNeed(stress, hangover * 0.015f * gameHours);
                fatigue = AddNeed(fatigue, hangover * 0.02f * gameHours);
            }

            ApplyPendingNeed(
                ref urine,
                ref pendingUrineEffect,
                hydrationAbsorptionPointsPerGameHour * gameHours);
            ApplyPendingNeed(
                ref fatigue,
                ref pendingFatigueEffect,
                nutritionAbsorptionPointsPerGameHour * gameHours);
            ApplyPendingNeed(
                ref dirtiness,
                ref pendingDirtinessEffect,
                nutritionAbsorptionPointsPerGameHour * gameHours);
        }

        /// <summary>
        /// Applies immediate changes and queues metabolism-backed changes as one
        /// atomic operation, publishing at most one state notification.
        /// Delayed stress is unsupported because the current persisted state has
        /// no pending-stress channel; pass it in immediateDelta instead.
        /// </summary>
        public bool TryApplyEffects(
            in PlayerNeedsEffectDelta immediateDelta,
            in PlayerNeedsEffectDelta delayedDelta,
            out string failure)
        {
            if (!initialized)
            {
                failure = "Player needs runtime is not initialized.";
                return false;
            }

            if (!immediateDelta.IsFinite || !delayedDelta.IsFinite)
            {
                failure = "Player needs effect delta must contain finite values.";
                return false;
            }

            if (Mathf.Abs(delayedDelta.Stress) > 0.0001f)
            {
                failure =
                    "Delayed stress is unsupported; pass stress in the immediate delta.";
                return false;
            }

            if (IsDevelopmentProgressionDisabled)
            {
                failure = string.Empty;
                return true;
            }

            if (!immediateDelta.HasAnyEffect &&
                !delayedDelta.HasAnyEffect)
            {
                failure = string.Empty;
                return true;
            }

            ApplyImmediateEffect(immediateDelta);
            QueueDelayedEffect(delayedDelta);
            PublishChanged();
            failure = string.Empty;
            return true;
        }

        public bool TryApplyEffectImmediately(
            in PlayerNeedsEffectDelta delta,
            out string failure)
        {
            PlayerNeedsEffectDelta delayedDelta = default;
            return TryApplyEffects(delta, delayedDelta, out failure);
        }

        public bool TryQueueDelayedEffect(
            in PlayerNeedsEffectDelta delta,
            out string failure)
        {
            PlayerNeedsEffectDelta immediateDelta = default;
            return TryApplyEffects(immediateDelta, delta, out failure);
        }

        private void HandleItemAction(ItemActionCompleted action)
        {
            if (action.Action != ItemActionKind.Used ||
                !action.UseEffects.HasAnyEffect)
            {
                return;
            }

            PlayerNeedsEffectDelta immediateDelta =
                new PlayerNeedsEffectDelta(
                    stress: action.UseEffects.Stress);
            PlayerNeedsEffectDelta delayedDelta =
                new PlayerNeedsEffectDelta(
                    thirst: action.UseEffects.Thirst,
                    hunger: action.UseEffects.Hunger,
                    urine: action.UseEffects.Urine,
                    fatigue: action.UseEffects.Fatigue,
                    dirtiness: action.UseEffects.Dirtiness,
                    weightKilograms: action.UseEffects.Weight,
                    intoxication: action.UseEffects.Intoxication);
            TryApplyEffects(
                immediateDelta,
                delayedDelta,
                out _);
        }

        public bool TrySleep(out string failure)
        {
            if (urinationActive)
            {
                failure = "Нельзя лечь спать во время мочеиспускания.";
                PublishLifeAction(
                    PlayerLifeActionKind.Sleep,
                    false,
                    0d,
                    failure);
                return false;
            }

            if (!initialized || gameTimeAdvance == null)
            {
                failure =
                    "Сон недоступен: согласованный пропуск игрового времени не настроен.";
                PublishLifeAction(
                    PlayerLifeActionKind.Sleep,
                    false,
                    0d,
                    failure);
                return false;
            }

            double advanceSeconds = sleepGameHours * 3600d;
            if (!gameTimeAdvance.TryAdvanceGameSeconds(
                    advanceSeconds,
                    out failure))
            {
                PublishLifeAction(
                    PlayerLifeActionKind.Sleep,
                    false,
                    0d,
                    failure);
                return false;
            }

            if (!IsDevelopmentProgressionDisabled)
            {
                fatigue = AddNeed(fatigue, -sleepFatigueRelief);
                stress = AddNeed(stress, -sleepStressRelief);
                dirtiness = AddNeed(dirtiness, 1f);
                PublishChanged();
            }
            failure = string.Empty;
            PublishLifeAction(
                PlayerLifeActionKind.Sleep,
                true,
                advanceSeconds,
                "Вы поспали.");
            return true;
        }

        public bool TryUrinate(out string failure)
        {
            if (!initialized)
            {
                failure = "Потребности игрока не инициализированы.";
                return false;
            }

            if (IsDevelopmentProgressionDisabled)
            {
                failure = "Потребности отключены в dev-меню.";
                return false;
            }

            if (urine < minimumUrineToVoid)
            {
                failure = "Сейчас не хочется.";
                PublishLifeAction(
                    PlayerLifeActionKind.Urinate,
                    false,
                    0d,
                    failure);
                return false;
            }

            if (urinationActive)
            {
                failure = "Мочеиспускание уже начато.";
                return false;
            }

            float fullness = Mathf.InverseLerp(
                minimumUrineToVoid,
                100f,
                urine);
            urinationDurationSeconds = Mathf.Lerp(
                minimumUrinationSeconds,
                maximumUrinationSeconds,
                fullness);
            urinationElapsedSeconds = 0f;
            urinationDrainPointsPerSecond =
                urine / urinationDurationSeconds;
            urinationPublishAccumulator = 0f;
            urinationActive = true;
            failure = string.Empty;
            LifeActionStarted?.Invoke(new PlayerLifeActionStarted(
                PlayerLifeActionKind.Urinate,
                urinationDurationSeconds,
                "Вы мочитесь."));
            return true;
        }

        public bool TrySetNeed(
            string needId,
            float value,
            out string failure)
        {
            if (!initialized)
            {
                failure = "Потребности игрока не инициализированы.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(needId) ||
                !float.IsFinite(value))
            {
                failure = "Имя потребности или значение некорректно.";
                return false;
            }

            string normalized = needId.Trim().ToLowerInvariant();
            if (normalized == "weight" ||
                normalized == "weightkg" ||
                normalized == "weightkilograms")
            {
                if (value < 1f || value > 500f)
                {
                    failure = "Вес должен быть в диапазоне 1–500 кг.";
                    return false;
                }

                weightKilograms = value;
                PublishChanged();
                failure = string.Empty;
                return true;
            }

            if (value < 0f || value > 100f)
            {
                failure = "Потребность должна быть в диапазоне 0–100.";
                return false;
            }

            switch (normalized)
            {
                case "thirst":
                    thirst = value;
                    break;
                case "hunger":
                    hunger = value;
                    break;
                case "stress":
                    stress = value;
                    break;
                case "urine":
                case "bladder":
                    urine = value;
                    break;
                case "fatigue":
                    fatigue = value;
                    break;
                case "dirtiness":
                case "dirt":
                    dirtiness = value;
                    break;
                case "intoxication":
                case "drunk":
                case "alcohol":
                    intoxication = value;
                    break;
                case "hangover":
                    hangover = value;
                    break;
                default:
                    failure = $"Неизвестная потребность '{needId}'.";
                    return false;
            }

            PublishChanged();
            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Transient developer override. It is excluded from save data and
        /// suppresses passive progression plus gameplay effects while explicit
        /// developer setters remain usable.
        /// </summary>
        public bool DevTrySetProgressionDisabled(
            bool disabled,
            out string failure)
        {
            if (!initialized || gameTime == null)
            {
                failure = "Потребности игрока не инициализированы.";
                return false;
            }

            developmentProgressionDisabled = disabled;
            lastProcessedElapsedGameSeconds =
                gameTime.Snapshot.ElapsedGameSeconds;
            if (disabled)
            {
                CancelTransientLifeActions();
            }

            failure = string.Empty;
            return true;
        }

        /// <summary>
        /// Restores the project-owned fresh-game state and clears pending
        /// metabolism without changing the transient disabled toggle.
        /// </summary>
        public bool DevTryResetAll(out string failure)
        {
            return TryRestoreDto(PlayerNeedsSaveDto.Fresh(), out failure);
        }
#endif

        private void PublishLifeAction(
            PlayerLifeActionKind kind,
            bool succeeded,
            double advancedGameSeconds,
            string message)
        {
            LifeActionCompleted?.Invoke(new PlayerLifeActionCompleted(
                kind,
                succeeded,
                advancedGameSeconds,
                message));
        }

        private void PublishChanged()
        {
            SynchronizeMotorMass();
            revision++;
            StateChanged?.Invoke(Snapshot);
        }

        private void SynchronizeMotorMass()
        {
            if (motor != null)
            {
                motor.TrySetBodyMassKilograms(weightKilograms);
            }
        }

        private void OnDestroy()
        {
            timeSubscription?.Dispose();
            timeSubscription = null;
            if (items != null)
            {
                items.ActionCompleted -= HandleItemAction;
            }
        }

        private bool IsDevelopmentProgressionDisabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return developmentProgressionDisabled;
#else
                return false;
#endif
            }
        }

        private static float AddNeed(float current, float delta) =>
            Mathf.Clamp(current + delta, 0f, 100f);

        private void CancelTransientLifeActions()
        {
            urinationActive = false;
            urinationElapsedSeconds = 0f;
            urinationDurationSeconds = 0f;
            urinationDrainPointsPerSecond = 0f;
            urinationPublishAccumulator = 0f;
        }

        private static float AddPendingEffect(float current, float delta) =>
            Mathf.Clamp(current + delta, -1000f, 1000f);

        private void ApplyImmediateEffect(
            in PlayerNeedsEffectDelta delta)
        {
            thirst = AddNeed(thirst, delta.Thirst);
            hunger = AddNeed(hunger, delta.Hunger);
            stress = AddNeed(stress, delta.Stress);
            urine = AddNeed(urine, delta.Urine);
            fatigue = AddNeed(fatigue, delta.Fatigue);
            dirtiness = AddNeed(dirtiness, delta.Dirtiness);
            weightKilograms = Mathf.Clamp(
                weightKilograms + delta.WeightKilograms,
                1f,
                500f);
            intoxication = AddNeed(
                intoxication,
                delta.Intoxication);
        }

        private void QueueDelayedEffect(
            in PlayerNeedsEffectDelta delta)
        {
            pendingThirstEffect = AddPendingEffect(
                pendingThirstEffect,
                delta.Thirst);
            pendingHungerEffect = AddPendingEffect(
                pendingHungerEffect,
                delta.Hunger);
            pendingUrineEffect = AddPendingEffect(
                pendingUrineEffect,
                delta.Urine);
            pendingFatigueEffect = AddPendingEffect(
                pendingFatigueEffect,
                delta.Fatigue);
            pendingDirtinessEffect = AddPendingEffect(
                pendingDirtinessEffect,
                delta.Dirtiness);
            pendingWeightEffect = AddPendingEffect(
                pendingWeightEffect,
                delta.WeightKilograms);
            pendingIntoxicationEffect = AddPendingEffect(
                pendingIntoxicationEffect,
                delta.Intoxication);
        }

        private static void ApplyPendingNeed(
            ref float current,
            ref float pending,
            float maximumAppliedMagnitude)
        {
            float previous = pending;
            pending = Mathf.MoveTowards(
                pending,
                0f,
                Mathf.Max(0f, maximumAppliedMagnitude));
            current = AddNeed(current, previous - pending);
        }

        private void ApplyPendingWeight(float maximumAppliedMagnitude)
        {
            float previous = pendingWeightEffect;
            pendingWeightEffect = Mathf.MoveTowards(
                pendingWeightEffect,
                0f,
                Mathf.Max(0f, maximumAppliedMagnitude));
            weightKilograms = Mathf.Clamp(
                weightKilograms + previous - pendingWeightEffect,
                1f,
                500f);
        }
    }
}
