using System;
using MSC.Core.Lifecycle;
using MSC.Economy;
using MSC.Needs;
using MSC.Player;
using UnityEngine;

namespace MSC.Audio.PlayerIntegration
{
    /// <summary>
    /// Binds project-owned player, needs and economy events to stable voice
    /// event IDs. Donor clips are an optional removable fallback supplement;
    /// donor controllers and hierarchy names never become runtime authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerVoiceReactionController : MonoBehaviour,
        IGameplayLocaleSettingsSink
    {
        private PlayerInputRouter input;
        private IPlayerNeedsService needs;
        private IPlayerNeedsEffectSink needsEffects;
        private IEconomyTransactionFeedbackSource economyFeedback;
        private IAudioBackend primaryAudio;
        private IAudioBackend fallbackAudio;
        private Action<string> subtitleFeedback;
        private PlayerVoiceReactionPolicy policy;
        private string localeId = string.Empty;
        private bool initialized;

        public event Action<PlayerVoiceReactionDecision> ReactionTriggered;

        public bool IsInitialized => initialized;
        public bool HasTriggeredReaction { get; private set; }
        public PlayerVoiceReactionDecision LastDecision { get; private set; }
        public string LastAudioFailure { get; private set; } = string.Empty;

        public string LocaleId => localeId;

        public void ApplyGameplayLocale(string configuredLocaleId)
        {
            localeId = InteractionUiTextCatalog.NormalizeLocaleId(
                configuredLocaleId);
        }

        public void Initialize(
            PlayerInputRouter configuredInput,
            IPlayerNeedsService configuredNeeds,
            IPlayerNeedsEffectSink configuredNeedsEffects,
            IEconomyTransactionFeedbackSource configuredEconomyFeedback,
            IAudioBackend configuredPrimaryAudio,
            IAudioBackend configuredFallbackAudio,
            Action<string> configuredSubtitleFeedback = null,
            uint presentationRandomSeed = 0u)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Player voice reactions are already initialized.");
            }

            input = configuredInput ??
                throw new ArgumentNullException(nameof(configuredInput));
            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            needsEffects = configuredNeedsEffects ??
                throw new ArgumentNullException(nameof(configuredNeedsEffects));
            economyFeedback = configuredEconomyFeedback ??
                throw new ArgumentNullException(nameof(configuredEconomyFeedback));
            primaryAudio = configuredPrimaryAudio;
            fallbackAudio = configuredFallbackAudio;
            subtitleFeedback = configuredSubtitleFeedback;
            policy = presentationRandomSeed == 0u
                ? new PlayerVoiceReactionPolicy()
                : new PlayerVoiceReactionPolicy(presentationRandomSeed);

            input.SwearRequested += HandleManualSwear;
            input.MiddleFingerRequested += HandleMiddleFinger;
            economyFeedback.TransactionRejected += HandleTransactionRejected;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || !input.IsGameplayInputEnabled)
            {
                return;
            }

            float stress = needs.Snapshot.Stress;
            policy.ObserveStress(stress);
            if (stress >= PlayerVoiceReactionPolicy.AutomaticStressThreshold)
            {
                RequestReaction(PlayerVoiceReactionKind.AutomaticHighStress);
            }
        }

        private void OnDestroy()
        {
            if (!initialized)
            {
                return;
            }

            input.SwearRequested -= HandleManualSwear;
            input.MiddleFingerRequested -= HandleMiddleFinger;
            economyFeedback.TransactionRejected -= HandleTransactionRejected;
            initialized = false;
        }

        public bool RequestReaction(PlayerVoiceReactionKind kind)
        {
            if (!initialized ||
                !policy.TryRequest(
                    kind,
                    Time.unscaledTimeAsDouble,
                    needs.Snapshot.Stress,
                    out PlayerVoiceReactionDecision decision))
            {
                return false;
            }

            if (decision.StressRelief > 0f)
            {
                var relief = new PlayerNeedsEffectDelta(
                    stress: -decision.StressRelief);
                if (!needsEffects.TryApplyEffectImmediately(
                        in relief,
                        out string failure))
                {
                    Debug.LogWarning(
                        "Player swear stress relief could not be applied: " +
                        failure,
                        this);
                }
            }

            PostVoice(decision.Kind, decision.VariantIndex);
            subtitleFeedback?.Invoke(
                PlayerVoiceReactionCatalog.GetSubtitle(
                    decision.Kind,
                    decision.VariantIndex,
                    ResolveSubtitleLanguage()));
            LastDecision = decision;
            HasTriggeredReaction = true;
            ReactionTriggered?.Invoke(decision);
            return true;
        }

        private SystemLanguage ResolveSubtitleLanguage()
        {
            string effectiveLocale = localeId;
            if (string.IsNullOrEmpty(effectiveLocale) &&
                TryGetComponent(out CrossdotPresenter contextualHud))
            {
                effectiveLocale = contextualHud.LocaleId;
            }

            if (string.IsNullOrEmpty(effectiveLocale))
            {
                return Application.systemLanguage;
            }

            return InteractionUiTextCatalog.IsRussianLocale(effectiveLocale)
                ? SystemLanguage.Russian
                : SystemLanguage.English;
        }

        private void HandleManualSwear() =>
            RequestReaction(PlayerVoiceReactionKind.ManualSwear);

        private void HandleMiddleFinger() =>
            RequestReaction(PlayerVoiceReactionKind.MiddleFinger);

        private void HandleTransactionRejected(EconomyTransactionRejected rejected)
        {
            if (rejected.Receipt.FailureReason ==
                EconomyTransactionFailureReason.InsufficientFunds)
            {
                RequestReaction(PlayerVoiceReactionKind.InsufficientFunds);
            }
        }

        private void PostVoice(
            PlayerVoiceReactionKind kind,
            int variantIndex)
        {
            AudioEventId eventId = kind == PlayerVoiceReactionKind.MiddleFinger
                ? AudioProjectIds.Events.GetPlayerMiddleFingerVariant(
                    variantIndex)
                : AudioProjectIds.Events.GetPlayerSwearVariant(variantIndex);
            var request = new AudioEventRequest(
                eventId,
                worldPosition: transform.position,
                allowMultiple: false);
            IAudioEventHandle handle = primaryAudio != null && primaryAudio.IsReady
                ? primaryAudio.PostEvent(in request)
                : AudioEventHandles.Invalid;
            if (!handle.IsValid &&
                fallbackAudio != null &&
                fallbackAudio.IsReady &&
                !ReferenceEquals(primaryAudio, fallbackAudio))
            {
                handle = fallbackAudio.PostEvent(in request);
            }

            LastAudioFailure = handle.IsValid
                ? string.Empty
                : fallbackAudio != null && fallbackAudio.IsReady
                    ? fallbackAudio.FailureReason
                    : primaryAudio?.FailureReason ??
                      "No player voice audio backend is ready.";
        }
    }

    public static class PlayerVoiceReactionCatalog
    {
        public const string FallbackResourcesPath =
            "Phase1PlayerVoice/Phase1PlayerVoiceAudioEventLibrary";

        private static readonly string[] EnglishSubtitles =
        {
            "Oh, fuck!",
            "Oh, Satan!",
            "What a fucking mess!",
            "Satan...",
            "What the hell?!",
            "God help me.",
            "Oh, fuck, again...",
            "Satan?",
            "God of thunder!",
            "This is definitely a fucking mess!",
            "Satan again.",
            "Oh hell, for real!",
            "Not Satan.",
            "God, help me already!",
            "Fuck.",
            "The mother of all fuckups!",
        };

        private static readonly string[] RussianSubtitles =
        {
            "Ох, блядь!",
            "Ох, сатана!",
            "Ну и ебанина!",
            "Сатана...",
            "Какого хрена?!",
            "Господи, помоги.",
            "Опять, блядь...",
            "Сатана?",
            "Бог грома!",
            "Ну точно полный пиздец!",
            "Опять сатана.",
            "Да ёбаный в рот!",
            "Только не сатана.",
            "Господи, да помоги уже!",
            "Блядь.",
            "Пиздец всему пиздецу!",
        };

        // Exact English strings stored in the locked donor's Finger subtitle
        // table component &107451. Russian values below are project-localized
        // fallbacks; the Finnish audio remains the authoritative presentation.
        private static readonly string[] MiddleFingerEnglishSubtitles =
        {
            "Smell pussy!",
            "Go to pussy, satan.",
            "Smell a shit!",
            "Go to pussy, please.",
            "You dork of the pussy.",
            "You, go home.",
            "Go to hell.",
            "I don't have energy to watch your face.",
            "You dick of a pussy.",
            "You dick of a pussy!",
            "You truly are a wart of a dick.",
        };

        private static readonly string[] MiddleFingerRussianSubtitles =
        {
            "Нюхай пизду!",
            "Иди в пизду, сатана.",
            "Нюхай говно!",
            "Иди в пизду, пожалуйста.",
            "Пиздюк ты ебаный.",
            "Ты — иди домой.",
            "Иди к чёрту.",
            "У меня сил нет смотреть на твою рожу.",
            "Ебаный ты хуй.",
            "Ебаный ты хуй!",
            "Да ты настоящая бородавка на хую.",
        };

        public static string GetSubtitle(
            PlayerVoiceReactionKind kind,
            int variantIndex,
            SystemLanguage language)
        {
            bool middleFinger = kind == PlayerVoiceReactionKind.MiddleFinger;
            int count = middleFinger
                ? AudioProjectIds.Events.PlayerMiddleFingerVariantCount
                : AudioProjectIds.Events.PlayerSwearVariantCount;
            if (variantIndex < 0 ||
                variantIndex >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(variantIndex));
            }

            if (middleFinger)
            {
                return language == SystemLanguage.Russian
                    ? MiddleFingerRussianSubtitles[variantIndex]
                    : MiddleFingerEnglishSubtitles[variantIndex];
            }

            return language == SystemLanguage.Russian
                ? RussianSubtitles[variantIndex]
                : EnglishSubtitles[variantIndex];
        }
    }
}
