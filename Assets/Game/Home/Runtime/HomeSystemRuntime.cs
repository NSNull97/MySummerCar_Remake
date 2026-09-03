using System;
using MSC.Core.Time;
using MSC.Needs;
using UnityEngine;

namespace MSC.Home
{
    public delegate bool HomeTryStartUrination(out string failure);

    /// <summary>
    /// Project-owned authority for the first bounded 09C-H1 domestic slice.
    /// Presentation objects invoke explicit action IDs through
    /// <see cref="HomeInteractionTarget"/> and never drive state themselves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeSystemRuntime : MonoBehaviour
    {
        private HomeProvisionalTuning tuning =
            HomeProvisionalTuning.Defaults;
        private IGameTimeService gameTime;
        private IDisposable gameTimeSubscription;
        private Func<bool> needsReadyProvider;
        private IPlayerNeedsEffectSink needsEffectSink;
        private HomeTryStartUrination tryStartUrination;
        private Func<Vector3> playerPositionProvider;
        private Transform showerCleaningOrigin;
        private ulong revision;
        private float showerCleaningAccumulator;
        private bool kitchenTapOpen;
        private bool showerSwitchOn = true;
        private bool showerValveOpen;
        private bool electricSaunaPowerOn;
        private float electricSaunaHeatKnobDegrees =
            HomeSaunaControlRange.MinimumHeatDegrees;
        private float electricSaunaTimerKnobDegrees =
            HomeSaunaControlRange.MinimumTimerDegrees;
        private float electricSaunaTimerSecondsRemaining;
        private float saunaTemperatureCelsius = 20f;
        private float saunaSteamNormalized;
        private bool fridgePowered = true;
        private bool fridgeDoorOpen;
        private bool stovePowered;
        private bool televisionPowered;
        private bool fireplaceLit;
        private bool mangalLit;
        private bool electricityAvailable = true;
        private bool initialized;

        public event Action<HomeStateSnapshot> StateChanged;
        public event Action<HomeActionCompleted> ActionCompleted;

        public bool IsInitialized => initialized;

        public bool ElectricSaunaHeaterActive =>
            electricSaunaPowerOn &&
            electricSaunaTimerSecondsRemaining > 0f;

        public bool ElectricityAvailable => electricityAvailable;

        public bool FridgeCoolingActive =>
            fridgePowered && electricityAvailable && !fridgeDoorOpen;

        public bool StoveHeatingActive =>
            stovePowered && electricityAvailable;

        public bool MangalHeatingActive => mangalLit;

        public HomeStateSnapshot Snapshot =>
            new HomeStateSnapshot(
                revision,
                kitchenTapOpen,
                showerSwitchOn,
                showerValveOpen,
                electricSaunaPowerOn,
                electricSaunaHeatKnobDegrees,
                electricSaunaTimerKnobDegrees,
                electricSaunaTimerSecondsRemaining,
                saunaTemperatureCelsius,
                saunaSteamNormalized,
                fridgePowered,
                fridgeDoorOpen,
                stovePowered,
                televisionPowered,
                fireplaceLit,
                mangalLit);

        public void SetElectricityAvailable(bool available)
        {
            if (electricityAvailable == available)
            {
                return;
            }

            electricityAvailable = available;
            PublishStateChanged();
        }

        public void SetFridgeDoorOpen(bool open)
        {
            if (fridgeDoorOpen == open)
            {
                return;
            }

            fridgeDoorOpen = open;
            PublishStateChanged();
        }

        /// <summary>
        /// Ordinary composition-root entry point.
        /// </summary>
        public void Initialize(
            IGameTimeService configuredGameTime,
            PlayerNeedsRuntime configuredNeeds,
            Transform configuredPlayer)
        {
            if (configuredNeeds == null)
            {
                throw new ArgumentNullException(nameof(configuredNeeds));
            }

            if (configuredPlayer == null)
            {
                throw new ArgumentNullException(nameof(configuredPlayer));
            }

            Initialize(
                configuredGameTime,
                () => configuredNeeds.IsInitialized,
                configuredNeeds,
                configuredNeeds.TryUrinate,
                () => configuredPlayer.position);
        }

        /// <summary>
        /// Delegate-based seam for deterministic tests and future adapters.
        /// </summary>
        public void Initialize(
            IGameTimeService configuredGameTime,
            Func<bool> configuredNeedsReadyProvider,
            IPlayerNeedsEffectSink configuredNeedsEffectSink,
            HomeTryStartUrination configuredTryStartUrination,
            Func<Vector3> configuredPlayerPositionProvider)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Home system runtime is already initialized.");
            }

            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            needsReadyProvider = configuredNeedsReadyProvider ??
                throw new ArgumentNullException(
                    nameof(configuredNeedsReadyProvider));
            needsEffectSink = configuredNeedsEffectSink ??
                throw new ArgumentNullException(
                    nameof(configuredNeedsEffectSink));
            tryStartUrination = configuredTryStartUrination ??
                throw new ArgumentNullException(
                    nameof(configuredTryStartUrination));
            playerPositionProvider = configuredPlayerPositionProvider ??
                throw new ArgumentNullException(
                    nameof(configuredPlayerPositionProvider));

            if (!tuning.TryValidate(out string tuningFailure))
            {
                throw new InvalidOperationException(tuningFailure);
            }

            saunaTemperatureCelsius =
                tuning.SaunaAmbientTemperatureCelsius;
            initialized = true;
            gameTimeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
        }

        public void ConfigureShowerCleaningOrigin(
            Transform configuredOrigin)
        {
            showerCleaningOrigin = configuredOrigin ??
                throw new ArgumentNullException(nameof(configuredOrigin));
        }

        public void ConfigureProvisionalTuning(
            in HomeProvisionalTuning configuredTuning)
        {
            if (!configuredTuning.TryValidate(out string failure))
            {
                throw new ArgumentException(
                    failure,
                    nameof(configuredTuning));
            }

            tuning = configuredTuning;
            if (!initialized)
            {
                saunaTemperatureCelsius =
                    tuning.SaunaAmbientTemperatureCelsius;
            }
        }

        private void Update()
        {
            if (initialized)
            {
                AdvanceShowerCleaning(Time.deltaTime);
            }
        }

        /// <summary>
        /// Advances provisional sauna behavior in authoritative game seconds.
        /// Public so tests can advance the same deterministic simulation.
        /// </summary>
        public void AdvanceSimulation(float elapsedGameSeconds)
        {
            if (!initialized ||
                !float.IsFinite(elapsedGameSeconds) ||
                elapsedGameSeconds <= 0f)
            {
                return;
            }

            bool stateChanged = AdvanceSauna(elapsedGameSeconds);
            if (stateChanged)
            {
                PublishStateChanged();
            }
        }

        private void OnDestroy()
        {
            gameTimeSubscription?.Dispose();
            gameTimeSubscription = null;
        }

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            if (gameTimeEvent.Kind != GameTimeEventKind.Advanced)
            {
                return;
            }

            double elapsedGameSeconds =
                gameTimeEvent.Current.ElapsedGameSeconds -
                gameTimeEvent.Previous.ElapsedGameSeconds;
            if (elapsedGameSeconds <= 0d)
            {
                return;
            }

            AdvanceSimulation((float)Math.Min(elapsedGameSeconds, float.MaxValue));
        }

        public bool CanPerformAction(
            string stableActionId,
            HomeActionKind action)
        {
            if (!initialized ||
                !HomeActionIds.Matches(stableActionId, action))
            {
                return false;
            }

            return action switch
            {
                HomeActionKind.KitchenTapOpen => !kitchenTapOpen,
                HomeActionKind.KitchenTapConsume =>
                    kitchenTapOpen && NeedsAreReady(),
                HomeActionKind.KitchenTapClose => kitchenTapOpen,
                HomeActionKind.KitchenTapToggle => true,
                HomeActionKind.ElectricSaunaPowerToggle => false,
                HomeActionKind.ElectricSaunaTimerCycle => false,
                HomeActionKind.ElectricSaunaSteam =>
                    ElectricSaunaHeaterActive &&
                    saunaTemperatureCelsius >=
                        tuning.SaunaSteamMinimumTemperatureCelsius,
                HomeActionKind.ToiletUse => NeedsAreReady(),
                HomeActionKind.SinkWash => NeedsAreReady(),
                _ => true,
            };
        }

        public string GetInteractionPrompt(HomeActionKind action) =>
            action switch
            {
                HomeActionKind.KitchenTapOpen => "Открыть кран",
                HomeActionKind.KitchenTapConsume => "Пить воду",
                HomeActionKind.KitchenTapClose => "Закрыть кран",
                HomeActionKind.KitchenTapToggle => kitchenTapOpen
                    ? "Закрыть кран"
                    : "Открыть кран",
                HomeActionKind.ShowerSwitchToggle => showerSwitchOn
                    ? "Переключить воду на кран"
                    : "Переключить воду на душ",
                HomeActionKind.ShowerValveToggle => showerValveOpen
                    ? "Закрыть вентиль"
                    : "Открыть вентиль",
                HomeActionKind.ElectricSaunaPowerToggle =>
                    electricSaunaPowerOn
                        ? "Выключить электропечь"
                        : "Включить электропечь",
                HomeActionKind.ElectricSaunaTimerCycle =>
                    "Переключить таймер сауны",
                HomeActionKind.ElectricSaunaSteam => "Поддать пару",
                HomeActionKind.ToiletUse => "Использовать туалет",
                HomeActionKind.SinkWash => "Умыться",
                HomeActionKind.FridgePowerToggle => fridgePowered
                    ? "Выключить холодильник"
                    : "Включить холодильник",
                HomeActionKind.StovePowerToggle => stovePowered
                    ? "Выключить плиту"
                    : "Включить плиту",
                HomeActionKind.TelevisionPowerToggle => televisionPowered
                    ? "Выключить телевизор"
                    : "Включить телевизор",
                HomeActionKind.FireplaceFireToggle => fireplaceLit
                    ? "Погасить камин"
                    : "Разжечь камин",
                HomeActionKind.MangalFireToggle => mangalLit
                    ? "Погасить мангал"
                    : "Разжечь мангал",
                _ => string.Empty,
            };

        public bool TryPerformAction(
            string stableActionId,
            HomeActionKind action,
            out string failure)
        {
            if (!initialized)
            {
                failure = "Home system runtime is not initialized.";
                PublishAction(stableActionId, action, false, failure);
                return false;
            }

            if (!HomeActionIds.Matches(stableActionId, action))
            {
                failure =
                    "Home action ID does not match its configured action.";
                PublishAction(stableActionId, action, false, failure);
                return false;
            }

            bool stateChanged = false;
            bool succeeded;
            string message;
            switch (action)
            {
                case HomeActionKind.KitchenTapOpen:
                    succeeded = TrySetKitchenTap(
                        open: true,
                        out stateChanged,
                        out failure);
                    message = succeeded ? "Кран открыт." : failure;
                    break;
                case HomeActionKind.KitchenTapConsume:
                    succeeded = TryConsumeKitchenTapWater(out failure);
                    message = succeeded ? "Вы выпили воды." : failure;
                    break;
                case HomeActionKind.KitchenTapClose:
                    succeeded = TrySetKitchenTap(
                        open: false,
                        out stateChanged,
                        out failure);
                    message = succeeded ? "Кран закрыт." : failure;
                    break;
                case HomeActionKind.KitchenTapToggle:
                    succeeded = TrySetKitchenTap(
                        open: !kitchenTapOpen,
                        out stateChanged,
                        out failure);
                    message = succeeded
                        ? kitchenTapOpen
                            ? "Кран открыт."
                            : "Кран закрыт."
                        : failure;
                    break;
                case HomeActionKind.ShowerSwitchToggle:
                    showerSwitchOn = !showerSwitchOn;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = showerSwitchOn
                        ? "Вода направлена в душ."
                        : "Вода направлена в кран.";
                    break;
                case HomeActionKind.ShowerValveToggle:
                    showerValveOpen = !showerValveOpen;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = showerValveOpen
                        ? "Вентиль душа открыт."
                        : "Вентиль душа закрыт.";
                    break;
                case HomeActionKind.ElectricSaunaPowerToggle:
                    electricSaunaPowerOn = !electricSaunaPowerOn;
                    electricSaunaHeatKnobDegrees =
                        electricSaunaPowerOn
                            ? HomeSaunaControlRange.MaximumHeatDegrees
                            : HomeSaunaControlRange.MinimumHeatDegrees;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = electricSaunaPowerOn
                        ? "Электрическая сауна включена."
                        : "Электрическая сауна выключена.";
                    break;
                case HomeActionKind.ElectricSaunaTimerCycle:
                    CycleElectricSaunaTimer();
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = electricSaunaTimerSecondsRemaining > 0f
                        ? "Таймер сауны установлен."
                        : "Таймер сауны выключен.";
                    break;
                case HomeActionKind.ElectricSaunaSteam:
                    succeeded = TryCreateSaunaSteam(
                        out stateChanged,
                        out failure);
                    message = succeeded ? "На камни подана вода." : failure;
                    break;
                case HomeActionKind.ToiletUse:
                    succeeded = TryUseToilet(out failure);
                    message = succeeded
                        ? "Использование туалета начато."
                        : failure;
                    break;
                case HomeActionKind.SinkWash:
                    succeeded = TryWashAtSink(out failure);
                    message = succeeded ? "Вы умылись." : failure;
                    break;
                case HomeActionKind.FridgePowerToggle:
                    fridgePowered = !fridgePowered;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = fridgePowered
                        ? "Холодильник включён."
                        : "Холодильник выключен.";
                    break;
                case HomeActionKind.StovePowerToggle:
                    stovePowered = !stovePowered;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = stovePowered
                        ? "Плита включена."
                        : "Плита выключена.";
                    break;
                case HomeActionKind.TelevisionPowerToggle:
                    televisionPowered = !televisionPowered;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = televisionPowered
                        ? "Телевизор включён."
                        : "Телевизор выключен.";
                    break;
                case HomeActionKind.MangalFireToggle:
                    mangalLit = !mangalLit;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = mangalLit
                        ? "Мангал разожжён."
                        : "Мангал погашен.";
                    break;
                case HomeActionKind.FireplaceFireToggle:
                    fireplaceLit = !fireplaceLit;
                    stateChanged = true;
                    succeeded = true;
                    failure = string.Empty;
                    message = fireplaceLit
                        ? "Камин разожжён."
                        : "Камин погашен.";
                    break;
                default:
                    succeeded = false;
                    failure = $"Unsupported home action '{action}'.";
                    message = failure;
                    break;
            }

            if (stateChanged)
            {
                PublishStateChanged();
            }

            PublishAction(stableActionId, action, succeeded, message);
            return succeeded;
        }

        public bool CanAdjustAction(
            string stableActionId,
            HomeActionKind action)
        {
            return initialized &&
                   HomeActionIds.Matches(stableActionId, action) &&
                   (action == HomeActionKind.ElectricSaunaPowerToggle ||
                    action == HomeActionKind.ElectricSaunaTimerCycle);
        }

        public string GetAdjustmentPrompt(HomeActionKind action)
        {
            return action switch
            {
                HomeActionKind.ElectricSaunaPowerToggle =>
                    $"Нагрев сауны: {electricSaunaHeatKnobDegrees:0}° " +
                    "(колесо мыши)",
                HomeActionKind.ElectricSaunaTimerCycle =>
                    $"Таймер сауны: " +
                    $"{electricSaunaTimerSecondsRemaining / 60f:0.0} мин " +
                    "(колесо мыши)",
                _ => string.Empty,
            };
        }

        public bool TryAdjustAction(
            string stableActionId,
            HomeActionKind action,
            float scrollNotches,
            out string failure)
        {
            if (!CanAdjustAction(stableActionId, action))
            {
                failure = "Домашняя регулировка недоступна.";
                return false;
            }

            if (!float.IsFinite(scrollNotches) ||
                Mathf.Abs(scrollNotches) < 0.001f)
            {
                failure = "Величина регулировки недействительна.";
                return false;
            }

            switch (action)
            {
                case HomeActionKind.ElectricSaunaPowerToggle:
                    electricSaunaHeatKnobDegrees =
                        HomeSaunaControlRange.ClampHeat(
                            electricSaunaHeatKnobDegrees +
                            scrollNotches *
                            HomeSaunaControlRange.HeatStepDegrees);
                    electricSaunaPowerOn =
                        electricSaunaHeatKnobDegrees >
                        HomeSaunaControlRange.MinimumHeatDegrees +
                        0.001f;
                    break;

                case HomeActionKind.ElectricSaunaTimerCycle:
                    electricSaunaTimerKnobDegrees =
                        HomeSaunaControlRange.ClampTimer(
                            electricSaunaTimerKnobDegrees +
                            scrollNotches *
                            HomeSaunaControlRange.TimerStepDegrees);
                    electricSaunaTimerSecondsRemaining =
                        electricSaunaTimerKnobDegrees *
                        HomeSaunaControlRange.TimerSecondsPerDegree;
                    break;

                default:
                    failure = "Домашняя регулировка не поддерживается.";
                    return false;
            }

            PublishStateChanged();
            failure = string.Empty;
            PublishAction(
                stableActionId,
                action,
                true,
                GetAdjustmentPrompt(action));
            return true;
        }

        public HomeStateDto CaptureDto() =>
            HomeStateDto.Create(Snapshot);

        public bool TryRestoreDto(
            HomeStateDto dto,
            out string failure)
        {
            if (!initialized)
            {
                failure = "Home system runtime is not initialized.";
                return false;
            }

            if (dto == null)
            {
                failure = "Home state is missing.";
                return false;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            if (dto.electricSaunaTimerSecondsRemaining >
                tuning.SaunaTimerMaximumSeconds)
            {
                failure =
                    "Home sauna timer exceeds the configured maximum.";
                return false;
            }

            kitchenTapOpen = dto.kitchenTapOpen;
            showerSwitchOn = dto.showerSwitchOn;
            showerValveOpen = dto.showerValveOpen;
            electricSaunaPowerOn = dto.electricSaunaPowerOn;
            electricSaunaHeatKnobDegrees =
                dto.electricSaunaHeatKnobDegrees;
            electricSaunaTimerKnobDegrees =
                dto.electricSaunaTimerKnobDegrees;
            electricSaunaTimerSecondsRemaining =
                dto.electricSaunaTimerSecondsRemaining;
            saunaTemperatureCelsius = dto.saunaTemperatureCelsius;
            saunaSteamNormalized = dto.saunaSteamNormalized;
            fridgePowered = dto.fridgePowered;
            fridgeDoorOpen = dto.fridgeDoorOpen;
            stovePowered = dto.stovePowered;
            televisionPowered = dto.televisionPowered;
            fireplaceLit = dto.fireplaceLit;
            mangalLit = dto.mangalLit;
            showerCleaningAccumulator = 0f;
            PublishStateChanged();
            failure = string.Empty;
            return true;
        }

        private bool TrySetKitchenTap(
            bool open,
            out bool stateChanged,
            out string failure)
        {
            if (kitchenTapOpen == open)
            {
                stateChanged = false;
                failure = open
                    ? "Кран уже открыт."
                    : "Кран уже закрыт.";
                return false;
            }

            kitchenTapOpen = open;
            stateChanged = true;
            failure = string.Empty;
            return true;
        }

        private bool TryConsumeKitchenTapWater(out string failure)
        {
            if (!kitchenTapOpen)
            {
                failure = "Сначала откройте кран.";
                return false;
            }

            if (!NeedsAreReady())
            {
                failure = "Потребности игрока не готовы.";
                return false;
            }

            var delayedEffect = new PlayerNeedsEffectDelta(
                thirst: -tuning.KitchenTapThirstRelief,
                urine: tuning.KitchenTapUrineIncrease);
            return needsEffectSink.TryQueueDelayedEffect(
                delayedEffect,
                out failure);
        }

        private bool TryUseToilet(out string failure)
        {
            if (!NeedsAreReady())
            {
                failure = "Потребности игрока не готовы.";
                return false;
            }

            return tryStartUrination(out failure);
        }

        private bool TryWashAtSink(out string failure)
        {
            if (!NeedsAreReady())
            {
                failure = "Потребности игрока не готовы.";
                return false;
            }

            var effect = new PlayerNeedsEffectDelta(
                dirtiness: -tuning.SinkDirtinessRelief);
            return needsEffectSink.TryApplyEffectImmediately(
                effect,
                out failure);
        }

        private bool TryCreateSaunaSteam(
            out bool stateChanged,
            out string failure)
        {
            if (!ElectricSaunaHeaterActive)
            {
                stateChanged = false;
                failure = "Печь сауны не нагревается.";
                return false;
            }

            if (saunaTemperatureCelsius <
                tuning.SaunaSteamMinimumTemperatureCelsius)
            {
                stateChanged = false;
                failure = "Камни сауны ещё недостаточно нагреты.";
                return false;
            }

            saunaSteamNormalized = Mathf.Clamp01(
                saunaSteamNormalized + tuning.SaunaSteamImpulse);
            saunaTemperatureCelsius = Mathf.Max(
                tuning.SaunaAmbientTemperatureCelsius,
                saunaTemperatureCelsius -
                tuning.SaunaSteamHeatLossDegrees);
            stateChanged = true;
            failure = string.Empty;
            return true;
        }

        private void CycleElectricSaunaTimer()
        {
            electricSaunaTimerKnobDegrees =
                HomeSaunaControlRange.ClampTimer(
                    electricSaunaTimerKnobDegrees +
                    HomeSaunaControlRange.TimerStepDegrees);
            electricSaunaTimerSecondsRemaining =
                electricSaunaTimerKnobDegrees *
                HomeSaunaControlRange.TimerSecondsPerDegree;
        }

        private bool AdvanceSauna(float elapsedGameSeconds)
        {
            bool changed = false;
            if (ElectricSaunaHeaterActive)
            {
                float previousTimer =
                    electricSaunaTimerSecondsRemaining;
                electricSaunaTimerSecondsRemaining = Mathf.Max(
                    0f,
                    electricSaunaTimerSecondsRemaining -
                    elapsedGameSeconds);
                electricSaunaTimerKnobDegrees =
                    electricSaunaTimerSecondsRemaining > 0f
                        ? HomeSaunaControlRange.ClampTimer(
                            electricSaunaTimerSecondsRemaining /
                            HomeSaunaControlRange.TimerSecondsPerDegree)
                        : HomeSaunaControlRange.MinimumTimerDegrees;
                changed |= !Mathf.Approximately(
                    previousTimer,
                    electricSaunaTimerSecondsRemaining);

                float previousTemperature = saunaTemperatureCelsius;
                saunaTemperatureCelsius = Mathf.MoveTowards(
                    saunaTemperatureCelsius,
                    GetElectricSaunaTargetTemperature(),
                    tuning.SaunaHeatingDegreesPerSecond *
                    elapsedGameSeconds);
                changed |= !Mathf.Approximately(
                    previousTemperature,
                    saunaTemperatureCelsius);

                if (electricSaunaTimerSecondsRemaining <= 0f)
                {
                    changed = true;
                }
            }
            else
            {
                float previousTemperature = saunaTemperatureCelsius;
                saunaTemperatureCelsius = Mathf.MoveTowards(
                    saunaTemperatureCelsius,
                    tuning.SaunaAmbientTemperatureCelsius,
                    tuning.SaunaCoolingDegreesPerSecond *
                    elapsedGameSeconds);
                changed |= !Mathf.Approximately(
                    previousTemperature,
                    saunaTemperatureCelsius);
            }

            float previousSteam = saunaSteamNormalized;
            saunaSteamNormalized = Mathf.MoveTowards(
                saunaSteamNormalized,
                0f,
                tuning.SaunaSteamDecayPerSecond * elapsedGameSeconds);
            changed |= !Mathf.Approximately(
                previousSteam,
                saunaSteamNormalized);
            return changed;
        }

        private float GetElectricSaunaTargetTemperature()
        {
            float maximumHeat = electricSaunaHeatKnobDegrees /
                                HomeSaunaControlRange.HeatDivisor;
            float normalized = Mathf.InverseLerp(0f, 0.5f, maximumHeat);
            return Mathf.Lerp(
                tuning.SaunaAmbientTemperatureCelsius,
                tuning.SaunaMaximumTemperatureCelsius,
                normalized);
        }

        private void AdvanceShowerCleaning(float elapsedRealSeconds)
        {
            if (!showerSwitchOn ||
                !showerValveOpen ||
                showerCleaningOrigin == null ||
                !NeedsAreReady())
            {
                showerCleaningAccumulator = 0f;
                return;
            }

            Vector3 playerPosition = playerPositionProvider();
            Vector3 showerPosition = showerCleaningOrigin.position;
            float radius = tuning.ShowerCleaningRadiusMeters;
            if ((playerPosition - showerPosition).sqrMagnitude >
                radius * radius)
            {
                showerCleaningAccumulator = 0f;
                return;
            }

            showerCleaningAccumulator += elapsedRealSeconds;
            if (showerCleaningAccumulator <
                tuning.ShowerNeedWriteIntervalSeconds)
            {
                return;
            }

            float cleaningSeconds = showerCleaningAccumulator;
            showerCleaningAccumulator = 0f;
            float relief =
                tuning.ShowerDirtinessReliefPerSecond * cleaningSeconds;
            var effect = new PlayerNeedsEffectDelta(
                dirtiness: -relief);
            needsEffectSink.TryApplyEffectImmediately(
                effect,
                out _);
        }

        private bool NeedsAreReady() =>
            needsReadyProvider != null && needsReadyProvider();

        private void PublishStateChanged()
        {
            revision++;
            StateChanged?.Invoke(Snapshot);
        }

        private void PublishAction(
            string stableActionId,
            HomeActionKind action,
            bool succeeded,
            string message)
        {
            ActionCompleted?.Invoke(new HomeActionCompleted(
                stableActionId,
                action,
                succeeded,
                message,
                Snapshot));
        }
    }
}
