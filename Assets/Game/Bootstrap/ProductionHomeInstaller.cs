using System;
using MSC.Core.Time;
using MSC.Home;
using MSC.Interaction.Query;
using MSC.LegacyImport;
using MSC.Needs;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Explicit project-owned composition for the bounded 09C-H1 domestic
    /// slice. Frozen donor coordinates are inputs only; runtime dispatch never
    /// searches donor names or hierarchy paths.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionHomeInstaller : MonoBehaviour
    {
        private const string WeightScaleGaugeStableId =
            "066254c4582fa4c1e44162588d101bb7";

        private static readonly Vector3 weightScaleDetectionCenterWorld =
            new Vector3(165.967f, 1.269f, -1029.393f);

        private HomeSystemRuntime homeRuntime;
        private ProductionWorldStreamingService streaming;
        private HomeWeightScalePresenter weightScalePresenter;
        private Transform boundWeightScaleGauge;
        private Action<string> feedbackSink;
        private bool initialized;

        public HomeSystemRuntime HomeRuntime => homeRuntime;

        public HomeSystemRuntime Initialize(
            Transform compositionRoot,
            IGameTimeService gameTime,
            PlayerNeedsRuntime needs,
            Transform player,
            Action<string> configuredFeedbackSink)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Production home installer is already initialized.");
            }

            if (compositionRoot == null)
            {
                throw new ArgumentNullException(nameof(compositionRoot));
            }

            homeRuntime = GetComponent<HomeSystemRuntime>() ??
                gameObject.AddComponent<HomeSystemRuntime>();
            homeRuntime.Initialize(gameTime, needs, player);

            streaming =
                GetComponent<ProductionWorldStreamingService>() ??
                throw new InvalidOperationException(
                    "Production home doors require the composition-root world streaming service.");
            ProductionWorldDoorInstaller doorInstaller =
                GetComponent<ProductionWorldDoorInstaller>() ??
                gameObject.AddComponent<ProductionWorldDoorInstaller>();
            doorInstaller.Initialize(streaming);

            var targetRoot = new GameObject(
                "09C H1 Domestic Interaction Targets");
            targetRoot.transform.SetParent(compositionRoot, false);

            weightScalePresenter =
                targetRoot.AddComponent<HomeWeightScalePresenter>();
            weightScalePresenter.Initialize(
                needs,
                player,
                weightScaleDetectionCenterWorld);
            streaming.OwnedSceneLoaded += HandleOwnedSceneLoaded;
            streaming.OwnedSceneWillUnload += HandleOwnedSceneWillUnload;
            BindWeightScaleFromCurrentlyLoadedScenes();

            CreateFixtureTarget(
                targetRoot.transform,
                "Kitchen Tap Handle",
                new Vector3(161.89757f, 2.16918f, -1033.2045f),
                0.1f,
                HomeActionKind.KitchenTapToggle);
            CreateFixtureTarget(
                targetRoot.transform,
                "Kitchen Tap Water",
                new Vector3(161.89757f, 1.96f, -1033.2045f),
                0.09f,
                HomeActionKind.KitchenTapConsume);
            CreateFixtureTarget(
                targetRoot.transform,
                "Shower Outlet Selector",
                new Vector3(156.1865f, 2.0268998f, -1040.0011f),
                0.085f,
                HomeActionKind.ShowerSwitchToggle);
            CreateFixtureTarget(
                targetRoot.transform,
                "Shower Water Valve",
                new Vector3(156.047f, 2.0269997f, -1040.185f),
                0.085f,
                HomeActionKind.ShowerValveToggle);
            CreateRangeTarget(
                targetRoot.transform,
                "Electric Sauna Power",
                new Vector3(156.57181f, 1.4279811f, -1041.3208f),
                0.04f,
                HomeActionKind.ElectricSaunaPowerToggle);
            CreateRangeTarget(
                targetRoot.transform,
                "Electric Sauna Timer",
                new Vector3(156.6553f, 1.4279786f, -1041.3208f),
                0.04f,
                HomeActionKind.ElectricSaunaTimerCycle);
            CreateFixtureTarget(
                targetRoot.transform,
                "Electric Sauna Stones",
                new Vector3(156.71051f, 1.9741786f, -1041.1685f),
                0.12f,
                HomeActionKind.ElectricSaunaSteam);
            CreateFixtureTarget(
                targetRoot.transform,
                "Toilet Bowl",
                new Vector3(161.782f, 1.469999f, -1028.463f),
                0.16f,
                HomeActionKind.ToiletUse);
            CreateFixtureTarget(
                targetRoot.transform,
                "Bathroom Sink Tap",
                new Vector3(162.634f, 2.1209993f, -1029.384f),
                0.1f,
                HomeActionKind.SinkWash);
            CreateFixtureTarget(
                targetRoot.transform,
                "Kitchen Stove Control",
                new Vector3(161.4409f, 1.8618996f, -1034.5164f),
                0.09f,
                HomeActionKind.StovePowerToggle);
            CreateFixtureTarget(
                targetRoot.transform,
                "Living Room Television Switch",
                new Vector3(165.09071f, 1.5640922f, -1034.8029f),
                0.1f,
                HomeActionKind.TelevisionPowerToggle);
            CreateFixtureTarget(
                targetRoot.transform,
                "Living Room Fireplace",
                new Vector3(162.56157f, 1.7171801f, -1032.5475f),
                0.12f,
                HomeActionKind.FireplaceFireToggle);

            var showerOrigin = new GameObject("Shower Cleaning Volume Origin");
            showerOrigin.transform.SetParent(targetRoot.transform, false);
            showerOrigin.transform.position =
                new Vector3(156.14f, 1.25f, -1040.1f);
            homeRuntime.ConfigureShowerCleaningOrigin(
                showerOrigin.transform);

            // The donor WaterTap transforms describe the centre of their
            // capsule stream volumes, not the physical outlet. Start each
            // project-owned effect at the upper capsule endpoint so water
            // leaves the shower head / tap instead of appearing in mid-air.
            Transform showerHeadAnchor = CreateFluidAnchor(
                targetRoot.transform,
                "Shower Head Water Anchor",
                new Vector3(156.22597f, 3.24148f, -1040.1257f),
                new Vector3(0.182978f, -0.983117f, -0.000001f));
            Transform showerFaucetAnchor = CreateFluidAnchor(
                targetRoot.transform,
                "Shower Faucet Water Anchor",
                new Vector3(156.2394f, 1.9508996f, -1040.0048f),
                Vector3.down);
            HomeWaterPresentation waterPresentation =
                targetRoot.AddComponent<HomeWaterPresentation>();
            waterPresentation.Initialize(
                homeRuntime,
                showerHeadAnchor,
                showerFaucetAnchor,
                ~0);

            feedbackSink = configuredFeedbackSink;
            homeRuntime.ActionCompleted += HandleHomeActionCompleted;
            initialized = true;
            return homeRuntime;
        }

        private void CreateFixtureTarget(
            Transform targetRoot,
            string displayName,
            Vector3 worldPosition,
            float radius,
            HomeActionKind action)
        {
            var targetObject = new GameObject("09C H1 " + displayName);
            targetObject.transform.SetParent(targetRoot, false);
            targetObject.transform.position = worldPosition;

            SphereCollider collider =
                targetObject.AddComponent<SphereCollider>();
            collider.radius = Mathf.Max(0.025f, radius);
            collider.isTrigger = false;

            HomeInteractionTarget target =
                targetObject.AddComponent<HomeInteractionTarget>();
            target.Configure(
                HomeActionIds.For(action),
                homeRuntime.GetInteractionPrompt(action),
                action,
                homeRuntime);
            InteractionTargetHost host =
                targetObject.AddComponent<InteractionTargetHost>();
            host.Configure(target);
        }

        private static Transform CreateFluidAnchor(
            Transform parent,
            string displayName,
            Vector3 worldPosition,
            Vector3 worldDirection)
        {
            var anchorObject = new GameObject(displayName);
            Transform anchor = anchorObject.transform;
            anchor.SetParent(parent, false);
            anchor.position = worldPosition;
            anchor.rotation = Quaternion.LookRotation(
                worldDirection.normalized,
                Mathf.Abs(Vector3.Dot(
                    worldDirection.normalized,
                    Vector3.up)) > 0.98f
                        ? Vector3.forward
                        : Vector3.up);
            return anchor;
        }

        private void CreateRangeTarget(
            Transform targetRoot,
            string displayName,
            Vector3 worldPosition,
            float radius,
            HomeActionKind action)
        {
            var targetObject = new GameObject("09C H1 " + displayName);
            targetObject.transform.SetParent(targetRoot, false);
            targetObject.transform.position = worldPosition;

            SphereCollider collider =
                targetObject.AddComponent<SphereCollider>();
            collider.radius = Mathf.Max(0.025f, radius);
            collider.isTrigger = false;

            HomeRangeInteractionTarget target =
                targetObject.AddComponent<HomeRangeInteractionTarget>();
            target.Configure(
                HomeActionIds.For(action),
                action,
                homeRuntime,
                Vector3.up);
            InteractionTargetHost host =
                targetObject.AddComponent<InteractionTargetHost>();
            host.Configure(target);
        }

        private void HandleHomeActionCompleted(HomeActionCompleted action)
        {
            if (!string.IsNullOrWhiteSpace(action.Message))
            {
                feedbackSink?.Invoke(action.Message);
            }
        }

        private void BindWeightScaleFromCurrentlyLoadedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded && TryBindWeightScaleGauge(scene))
                {
                    return;
                }
            }
        }

        private void HandleOwnedSceneLoaded(Scene scene)
        {
            TryBindWeightScaleGauge(scene);
        }

        private void HandleOwnedSceneWillUnload(Scene scene)
        {
            if (boundWeightScaleGauge == null ||
                boundWeightScaleGauge.gameObject.scene != scene)
            {
                return;
            }

            weightScalePresenter?.UnbindGauge(boundWeightScaleGauge);
            boundWeightScaleGauge = null;
        }

        private bool TryBindWeightScaleGauge(Scene scene)
        {
            if (boundWeightScaleGauge != null ||
                weightScalePresenter == null ||
                !scene.IsValid() ||
                !scene.isLoaded)
            {
                return false;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DonorWorldBaselineEntityMetadata[] entities =
                    roots[rootIndex]
                        .GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(
                            includeInactive: true);
                for (int entityIndex = 0;
                     entityIndex < entities.Length;
                     entityIndex++)
                {
                    DonorWorldBaselineEntityMetadata entity =
                        entities[entityIndex];
                    if (!string.Equals(
                            entity.StableId,
                            WeightScaleGaugeStableId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    boundWeightScaleGauge = entity.transform;
                    weightScalePresenter.BindGauge(boundWeightScaleGauge);
                    return true;
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (homeRuntime != null)
            {
                homeRuntime.ActionCompleted -= HandleHomeActionCompleted;
            }

            if (streaming != null)
            {
                streaming.OwnedSceneLoaded -= HandleOwnedSceneLoaded;
                streaming.OwnedSceneWillUnload -= HandleOwnedSceneWillUnload;
            }
        }
    }
}
