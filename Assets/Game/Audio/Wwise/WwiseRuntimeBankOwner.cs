using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Audio.Composition;
using UnityEngine;

namespace MSC.Audio.Wwise
{
    /// <summary>
    /// Session-scoped owner for the bounded production bank set and official
    /// default listener. Missing banks leave Wwise unready so the router keeps
    /// the project-owned Unity fallback active.
    /// </summary>
    [DefaultExecutionOrder(-24000)]
    [DisallowMultipleComponent]
    public sealed class WwiseRuntimeBankOwner : MonoBehaviour, IAudioRuntimeOwner
    {
        private const int MaximumInitializationFrames = 300;

        [SerializeField] private WwiseAudioBackend backend;
        [SerializeField] private string[] bankNames =
        {
            "Init",
            "MSC_Vehicle",
            "MSC_Weather",
            "MSC_World",
            "MSC_Interaction",
            "MSC_UI",
        };

        private readonly List<LoadedBank> loadedBanks = new List<LoadedBank>();
        private Coroutine loadingCoroutine;

        public bool IsReady { get; private set; }
        public string LastFailure { get; private set; } = string.Empty;

        public bool BindListener(Transform listenerTransform, out string failure)
        {
            if (listenerTransform == null)
            {
                failure = "A listener Transform is required for Wwise.";
                return false;
            }

            GameObject listenerObject = listenerTransform.gameObject;
            if (listenerObject.GetComponent<AkGameObj>() == null)
            {
                listenerObject.AddComponent<AkGameObj>();
            }

            AkAudioListener listener = listenerObject.GetComponent<AkAudioListener>();
            if (listener == null)
            {
                listener = listenerObject.AddComponent<AkAudioListener>();
            }

            listener.SetIsDefaultListener(true);
            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            WwiseAudioBackend configuredBackend,
            params string[] configuredBanks)
        {
            backend = configuredBackend;
            bankNames = configuredBanks ?? Array.Empty<string>();
        }
#endif

        private void OnEnable()
        {
            loadingCoroutine = StartCoroutine(LoadBanksWhenInitialized());
        }

        private void OnDisable()
        {
            if (loadingCoroutine != null)
            {
                StopCoroutine(loadingCoroutine);
                loadingCoroutine = null;
            }

            UnloadOwnedBanks();
            IsReady = false;
        }

        private IEnumerator LoadBanksWhenInitialized()
        {
            IsReady = false;
            LastFailure = string.Empty;
            if (backend == null)
            {
                LastFailure = "WwiseRuntimeBankOwner has no WwiseAudioBackend.";
                yield break;
            }

            for (int frame = 0;
                 frame < MaximumInitializationFrames &&
                 !AkUnitySoundEngine.IsInitialized();
                 frame++)
            {
                yield return null;
            }

            if (!AkUnitySoundEngine.IsInitialized())
            {
                LastFailure = "The official Wwise SoundEngine did not initialize before the bank deadline.";
                yield break;
            }

            string[] configuredBanks = bankNames ?? Array.Empty<string>();
            for (int index = 0; index < configuredBanks.Length; index++)
            {
                string bankName = NormalizeBankName(configuredBanks[index]);
                if (bankName.Length == 0)
                {
                    LastFailure = $"Wwise bank entry {index} is empty.";
                    yield break;
                }

                AKRESULT result = AkUnitySoundEngine.LoadBank(bankName, out uint bankId);
                bool owned = result == AKRESULT.AK_Success;
                if (!owned && result != AKRESULT.AK_BankAlreadyLoaded)
                {
                    LastFailure = $"Failed to load Wwise SoundBank '{bankName}': {result}.";
                    yield break;
                }

                loadedBanks.Add(new LoadedBank(bankName, bankId, owned));
                backend.ReportBankLoaded(bankName);
            }

            IsReady = true;
            loadingCoroutine = null;
        }

        private void UnloadOwnedBanks()
        {
            for (int index = loadedBanks.Count - 1; index >= 0; index--)
            {
                LoadedBank bank = loadedBanks[index];
                if (bank.Owned && AkUnitySoundEngine.IsInitialized())
                {
                    AkUnitySoundEngine.UnloadBank(bank.BankId, IntPtr.Zero);
                }

                backend?.ReportBankUnloaded(bank.Name);
            }

            loadedBanks.Clear();
        }

        private static string NormalizeBankName(string bankName)
        {
            string normalized = bankName?.Trim() ?? string.Empty;
            return normalized.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(0, normalized.Length - 4)
                : normalized;
        }

        private readonly struct LoadedBank
        {
            public LoadedBank(string name, uint bankId, bool owned)
            {
                Name = name;
                BankId = bankId;
                Owned = owned;
            }

            public string Name { get; }
            public uint BankId { get; }
            public bool Owned { get; }
        }
    }
}
