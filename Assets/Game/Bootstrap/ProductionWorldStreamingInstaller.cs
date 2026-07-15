using System;
using System.Collections;
using MSC.World.Streaming;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Explicit bootstrap composition for the bounded 05B.1 production-world session.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ProductionWorldStreamingInstaller : MonoBehaviour
    {
        [SerializeField] private GameCompositionRoot compositionRoot;
        [SerializeField] private ProductionWorldStreamingService worldStreaming;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Vector3 playerSpawnPosition;
        [SerializeField] private Vector3 playerSpawnEulerAngles;

        private GameObject spawnedPlayer;

        public GameCompositionRoot CompositionRoot => compositionRoot;
        public ProductionWorldStreamingService WorldStreaming => worldStreaming;
        public GameObject PlayerPrefab => playerPrefab;
        public Vector3 PlayerSpawnPosition => playerSpawnPosition;
        public Quaternion PlayerSpawnRotation => Quaternion.Euler(playerSpawnEulerAngles);
        public GameObject SpawnedPlayer => spawnedPlayer;
        public bool IsReady { get; private set; }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            GameCompositionRoot configuredRoot,
            ProductionWorldStreamingService configuredStreaming,
            GameObject configuredPlayerPrefab,
            Vector3 spawnPosition,
            Quaternion spawnRotation)
        {
            compositionRoot = configuredRoot;
            worldStreaming = configuredStreaming;
            playerPrefab = configuredPlayerPrefab;
            playerSpawnPosition = spawnPosition;
            playerSpawnEulerAngles = spawnRotation.eulerAngles;
        }
#endif

        private void Awake()
        {
            ValidateConfiguration();

            spawnedPlayer = Instantiate(
                playerPrefab,
                playerSpawnPosition,
                PlayerSpawnRotation,
                compositionRoot.transform);
            worldStreaming.BindFocus(spawnedPlayer.transform);
            compositionRoot.Initialize(GameServiceBindings.CreateWorldStreamingPartial(worldStreaming));
        }

        private IEnumerator Start()
        {
            yield return worldStreaming.RefreshNow();
            IsReady = worldStreaming.HasFocus && !worldStreaming.IsStreaming;
        }

        private void ValidateConfiguration()
        {
            if (compositionRoot == null)
            {
                throw new InvalidOperationException("Production streaming installer has no composition root.");
            }

            if (worldStreaming == null)
            {
                throw new InvalidOperationException("Production streaming installer has no world streaming service.");
            }

            if (playerPrefab == null)
            {
                throw new InvalidOperationException("Production streaming installer has no player prefab.");
            }

            if (compositionRoot.IsInitialized)
            {
                throw new InvalidOperationException("Production streaming installer cannot reuse an initialized composition root.");
            }

            if (worldStreaming.gameObject != compositionRoot.gameObject || gameObject != compositionRoot.gameObject)
            {
                throw new InvalidOperationException(
                    "Composition root, production streaming service, and installer must share the process-lifetime bootstrap object.");
            }
        }
    }
}
