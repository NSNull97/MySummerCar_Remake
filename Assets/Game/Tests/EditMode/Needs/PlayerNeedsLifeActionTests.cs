using System.Reflection;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Items;
using MSC.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Needs.Tests.EditMode
{
    public sealed class PlayerNeedsLifeActionTests
    {
        private GameObject player;
        private GameObject cameraPivot;
        private GameTimeService gameTime;
        private PlayerNeedsRuntime needs;
        private FirstPersonMotor motor;

        [SetUp]
        public void SetUp()
        {
            gameTime = new GameTimeService(new GameTimeConfig(
                "time.needs-tests.v1",
                new GameDate(1995, 8, 1),
                12d * 60d * 60d,
                86_400d,
                1d,
                0.25d,
                0.875d,
                GameTimeTuningClassification.RemakeDesignTarget));

            player = new GameObject("NeedsPlayerFixture");
            CharacterController controller =
                player.AddComponent<CharacterController>();
            cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(player.transform, false);
            motor = player.AddComponent<FirstPersonMotor>();
            motor.Configure(controller, cameraPivot.transform);
            needs = player.AddComponent<PlayerNeedsRuntime>();
            needs.Initialize(
                gameTime,
                motor,
                configuredItems: null,
                new ClockAdvanceFixture(gameTime));
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null)
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Urination_DrainsOverTimeThenAddsDirtinessAndPublishesResult()
        {
            Restore(urine: 68f, dirtiness: 12f);
            PlayerLifeActionStarted started = default;
            PlayerLifeActionCompleted observed = default;
            needs.LifeActionStarted += action => started = action;
            needs.LifeActionCompleted += action => observed = action;

            bool result = needs.TryUrinate(out string failure);

            Assert.That(result, Is.True, failure);
            Assert.That(needs.IsUrinating, Is.True);
            Assert.That(needs.Snapshot.Urine, Is.EqualTo(68f).Within(0.001f));
            Assert.That(started.Kind, Is.EqualTo(PlayerLifeActionKind.Urinate));
            Assert.That(started.RealTimeDuration, Is.GreaterThan(1f));

            needs.AdvanceActiveLifeAction(1f);

            Assert.That(needs.Snapshot.Urine, Is.LessThan(68f));
            Assert.That(needs.Snapshot.Urine, Is.GreaterThan(0f));

            needs.AdvanceActiveLifeAction(30f);

            Assert.That(needs.IsUrinating, Is.False);
            Assert.That(needs.Snapshot.Urine, Is.Zero);
            Assert.That(needs.Snapshot.Dirtiness, Is.EqualTo(14f).Within(0.001f));
            Assert.That(observed.Kind, Is.EqualTo(PlayerLifeActionKind.Urinate));
            Assert.That(observed.Succeeded, Is.True);
        }

        [Test]
        public void AlcoholClearance_ProducesRecoverableHangover()
        {
            Restore(intoxication: 8f);

            gameTime.AdvanceWhileRetainingPause(60d * 60d);

            Assert.That(needs.Snapshot.Intoxication, Is.LessThan(8f));
            Assert.That(needs.Snapshot.Hangover, Is.GreaterThan(0f));
        }

        [Test]
        public void DeveloperNeedSetter_ValidatesAndPublishesKnownNeed()
        {
            PlayerNeedsSnapshot observed = default;
            needs.StateChanged += snapshot => observed = snapshot;

            bool result = needs.TrySetNeed(
                "fatigue",
                100f,
                out string failure);

            Assert.That(result, Is.True, failure);
            Assert.That(needs.Snapshot.Fatigue, Is.EqualTo(100f));
            Assert.That(observed.Fatigue, Is.EqualTo(100f));
            Assert.That(
                needs.TrySetNeed("unknown", 5f, out _),
                Is.False);
        }

        [Test]
        public void WeightState_SynchronizesThePlayerPhysicalMass()
        {
            Assert.That(motor.BodyMassKilograms, Is.EqualTo(83f));

            Assert.That(
                needs.TrySetNeed(
                    "weight",
                    96f,
                    out string setFailure),
                Is.True,
                setFailure);
            Assert.That(motor.BodyMassKilograms, Is.EqualTo(96f));

            Restore(weightKilograms: 74.5f);
            Assert.That(motor.BodyMassKilograms, Is.EqualTo(74.5f));
        }

        [Test]
        public void DeveloperResetAll_ClearsNeedsPendingEffectsAndWeight()
        {
            var configured = new PlayerNeedsSaveDto
            {
                thirst = 14f,
                hunger = 23f,
                stress = 31f,
                urine = 44f,
                fatigue = 58f,
                dirtiness = 62f,
                weightKilograms = 91f,
                intoxication = 17f,
                hangover = 8f,
                pendingHungerEffect = -10f,
                pendingThirstEffect = -12f,
                pendingWeightEffect = 1.5f,
                pendingIntoxicationEffect = 9f,
                pendingUrineEffect = 11f,
                pendingFatigueEffect = -7f,
                pendingDirtinessEffect = 4f,
            };
            Assert.That(
                needs.TryRestoreDto(configured, out string restoreFailure),
                Is.True,
                restoreFailure);
            Assert.That(
                needs.DevTrySetProgressionDisabled(
                    true,
                    out string disableFailure),
                Is.True,
                disableFailure);

            bool result = needs.DevTryResetAll(out string failure);

            Assert.That(result, Is.True, failure);
            PlayerNeedsSnapshot snapshot = needs.Snapshot;
            Assert.That(snapshot.Thirst, Is.Zero);
            Assert.That(snapshot.Hunger, Is.Zero);
            Assert.That(snapshot.Stress, Is.Zero);
            Assert.That(snapshot.Urine, Is.Zero);
            Assert.That(snapshot.Fatigue, Is.Zero);
            Assert.That(snapshot.Dirtiness, Is.Zero);
            Assert.That(snapshot.Intoxication, Is.Zero);
            Assert.That(snapshot.Hangover, Is.Zero);
            Assert.That(snapshot.WeightKilograms, Is.EqualTo(83f));
            Assert.That(snapshot.PendingHungerEffect, Is.Zero);
            Assert.That(snapshot.PendingThirstEffect, Is.Zero);
            Assert.That(snapshot.PendingWeightEffect, Is.Zero);
            Assert.That(snapshot.PendingIntoxicationEffect, Is.Zero);
            Assert.That(snapshot.PendingUrineEffect, Is.Zero);
            Assert.That(snapshot.PendingFatigueEffect, Is.Zero);
            Assert.That(snapshot.PendingDirtinessEffect, Is.Zero);
            Assert.That(needs.DevProgressionDisabled, Is.True);
        }

        [Test]
        public void DeveloperDisable_FreezesProgressionAndDoesNotCatchUp()
        {
            Assert.That(
                needs.TrySetNeed("thirst", 10f, out string setFailure),
                Is.True,
                setFailure);
            Assert.That(
                needs.DevTrySetProgressionDisabled(
                    true,
                    out string disableFailure),
                Is.True,
                disableFailure);

            gameTime.AdvanceWhileRetainingPause(2d * 60d * 60d);
            Assert.That(
                needs.TryApplyEffectImmediately(
                    new PlayerNeedsEffectDelta(thirst: 20f),
                    out string effectFailure),
                Is.True,
                effectFailure);
            Assert.That(needs.Snapshot.Thirst, Is.EqualTo(10f));

            Assert.That(
                needs.DevTrySetProgressionDisabled(
                    false,
                    out string enableFailure),
                Is.True,
                enableFailure);
            gameTime.AdvanceWhileRetainingPause(60d * 60d);

            Assert.That(needs.Snapshot.Thirst, Is.EqualTo(14f).Within(0.001f));
        }

        [Test]
        public void FatigueBlinkResponse_IsSoftBoundedAndReturnsFullyOpen()
        {
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(-0.01f),
                Is.Zero);
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(0f),
                Is.Zero);
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(0.683f),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(0.2f),
                Is.LessThan(FatigueBlinkResponse.EvaluateClosure(0.5f)));
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(0.5f),
                Is.LessThan(FatigueBlinkResponse.EvaluateClosure(0.683f)));
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(0.8f),
                Is.GreaterThan(FatigueBlinkResponse.EvaluateClosure(1f)));
            Assert.That(
                FatigueBlinkResponse.EvaluateClosure(
                    FatigueBlinkResponse.BlinkDurationSeconds),
                Is.Zero);

            for (int sample = 0; sample <= 100; sample++)
            {
                float elapsed =
                    FatigueBlinkResponse.BlinkDurationSeconds *
                    sample / 100f;
                Assert.That(
                    FatigueBlinkResponse.EvaluateClosure(elapsed),
                    Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void FatigueBlinkIntervals_AreDeterministicAndDonorBounded()
        {
            float first = FatigueBlinkResponse.GetIntervalSeconds(7);
            Assert.That(
                FatigueBlinkResponse.GetIntervalSeconds(7),
                Is.EqualTo(first));

            bool observedVariation = false;
            for (uint sequence = 0; sequence < 32; sequence++)
            {
                float interval =
                    FatigueBlinkResponse.GetIntervalSeconds(sequence);
                Assert.That(
                    interval,
                    Is.InRange(
                        FatigueBlinkResponse.MinimumIntervalSeconds,
                        FatigueBlinkResponse.MaximumIntervalSeconds));
                observedVariation |= Mathf.Abs(interval - first) > 0.01f;
            }

            Assert.That(observedVariation, Is.True);
        }

        [Test]
        public void Sleep_UsesSharedClockAdvanceAndRelievesFatigueAndStress()
        {
            Restore(stress: 50f, fatigue: 90f, dirtiness: 5f);
            PlayerLifeActionCompleted observed = default;
            needs.LifeActionCompleted += action => observed = action;

            bool result = needs.TrySleep(out string failure);

            Assert.That(result, Is.True, failure);
            Assert.That(
                gameTime.CurrentGameTimeSeconds,
                Is.EqualTo(8d * 60d * 60d).Within(0.001d));
            Assert.That(needs.Snapshot.Fatigue, Is.LessThan(90f));
            Assert.That(needs.Snapshot.Stress, Is.LessThan(50f));
            Assert.That(observed.Kind, Is.EqualTo(PlayerLifeActionKind.Sleep));
            Assert.That(observed.Succeeded, Is.True);
            Assert.That(
                observed.AdvancedGameSeconds,
                Is.EqualTo(8d * 60d * 60d).Within(0.001d));
        }

        [Test]
        public void SaveDto_RoundTripsHangoverAndPendingLifeEffects()
        {
            var source = new PlayerNeedsSaveDto
            {
                thirst = 10f,
                hunger = 20f,
                stress = 30f,
                urine = 40f,
                fatigue = 50f,
                dirtiness = 60f,
                weightKilograms = 82.5f,
                intoxication = 7f,
                hangover = 8f,
                pendingHungerEffect = -3f,
                pendingThirstEffect = -4f,
                pendingWeightEffect = 0.25f,
                pendingIntoxicationEffect = 5f,
                pendingUrineEffect = 6f,
                pendingFatigueEffect = -7f,
                pendingDirtinessEffect = 8f,
            };

            string json = JsonUtility.ToJson(source);
            PlayerNeedsSaveDto restored =
                JsonUtility.FromJson<PlayerNeedsSaveDto>(json);

            Assert.That(restored.TryValidate(out string failure), Is.True, failure);
            Assert.That(restored.hangover, Is.EqualTo(8f));
            Assert.That(restored.pendingUrineEffect, Is.EqualTo(6f));
            Assert.That(restored.pendingFatigueEffect, Is.EqualTo(-7f));
            Assert.That(restored.pendingDirtinessEffect, Is.EqualTo(8f));
        }

        [Test]
        public void SubtitleFrame_IsBottomCenteredAndScreenBounded()
        {
            Rect frame = FirstPersonLifeActionPresenter.CalculateSubtitleFrameRect(
                1920f,
                1080f,
                420f,
                24f);

            Assert.That(frame.center.x, Is.EqualTo(960f).Within(0.001f));
            Assert.That(frame.yMax, Is.EqualTo(1056f).Within(0.001f));
            Assert.That(frame.xMin, Is.GreaterThanOrEqualTo(24f));
            Assert.That(frame.xMax, Is.LessThanOrEqualTo(1896f));
            Assert.That(frame.height, Is.GreaterThanOrEqualTo(38f));
        }

        [Test]
        public void OrdinaryFoodEventsNeverStartFirstPersonArmPresentation()
        {
            const string catalogPath =
                "Assets/Game/Items/Content/Definitions/" +
                "Phase1ItemDefinitionCatalog.asset";
            ItemDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    catalogPath);
            var itemRuntimeObject = new GameObject("Food presenter item runtime");
            var presenterObject = new GameObject("Food presenter test");

            try
            {
                ItemWorldRuntime itemRuntime =
                    itemRuntimeObject.AddComponent<ItemWorldRuntime>();
                typeof(ItemWorldRuntime).GetField(
                        "definitions",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(itemRuntime, catalog);
                FirstPersonLifeActionPresenter presenter =
                    presenterObject.AddComponent<FirstPersonLifeActionPresenter>();
                typeof(FirstPersonLifeActionPresenter).GetField(
                        "items",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(presenter, itemRuntime);
                MethodInfo handle = typeof(FirstPersonLifeActionPresenter)
                    .GetMethod(
                        "HandleItemAction",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handle, Is.Not.Null);
                StableEntityId stableId =
                    ItemStableIdUtility.CreateDeterministic(
                        "test.food-presenter.no-arms");

                handle.Invoke(
                    presenter,
                    new object[]
                    {
                        new ItemActionCompleted(
                            ItemActionKind.ConsumptionStarted,
                            stableId,
                            "item.macaroni-box",
                            100f),
                    });
                Assert.That(presenter.IsPresenting, Is.False);

                handle.Invoke(
                    presenter,
                    new object[]
                    {
                        new ItemActionCompleted(
                            ItemActionKind.Used,
                            stableId,
                            "item.macaroni-box",
                            0f),
                    });
                Assert.That(presenter.IsPresenting, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
                Object.DestroyImmediate(itemRuntimeObject);
            }
        }

        [Test]
        public void NonConsumableToolStateChangesNeverStartFoodPresentation()
        {
            const string catalogPath =
                "Assets/Game/Items/Content/Definitions/" +
                "Phase1ItemDefinitionCatalog.asset";
            ItemDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    catalogPath);
            var itemRuntimeObject = new GameObject("Tool presenter item runtime");
            var presenterObject = new GameObject("Tool presenter test");

            try
            {
                ItemWorldRuntime itemRuntime =
                    itemRuntimeObject.AddComponent<ItemWorldRuntime>();
                typeof(ItemWorldRuntime).GetField(
                        "definitions",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(itemRuntime, catalog);
                FirstPersonLifeActionPresenter presenter =
                    presenterObject.AddComponent<FirstPersonLifeActionPresenter>();
                typeof(FirstPersonLifeActionPresenter).GetField(
                        "items",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(presenter, itemRuntime);
                MethodInfo handle = typeof(FirstPersonLifeActionPresenter)
                    .GetMethod(
                        "HandleItemAction",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo resolve = typeof(FirstPersonLifeActionPresenter)
                    .GetMethod(
                        "ResolveItemPresentation",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(handle, Is.Not.Null);
                Assert.That(resolve, Is.Not.Null);
                var action = new ItemActionCompleted(
                    ItemActionKind.Used,
                    ItemStableIdUtility.CreateDeterministic(
                        "test.tool-presenter.floor-jack"),
                    "item.floor-jack",
                    0f);

                Assert.That(
                    resolve.Invoke(presenter, new object[] { action })
                        ?.ToString(),
                    Is.EqualTo("None"));
                handle.Invoke(presenter, new object[] { action });
                Assert.That(
                    presenter.IsPresenting,
                    Is.False,
                    "Persisting jack lift-height must not restart the food viewmodel.");
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
                Object.DestroyImmediate(itemRuntimeObject);
            }
        }

        [Test]
        public void AuthoredDrinkFoodsResolveExistingDrinkViewmodel()
        {
            const string catalogPath =
                "Assets/Game/Items/Content/Definitions/" +
                "Phase1ItemDefinitionCatalog.asset";
            ItemDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    catalogPath);
            var itemRuntimeObject = new GameObject("Drink food item runtime");
            var presenterObject = new GameObject("Drink food presenter test");

            try
            {
                ItemWorldRuntime itemRuntime =
                    itemRuntimeObject.AddComponent<ItemWorldRuntime>();
                typeof(ItemWorldRuntime).GetField(
                        "definitions",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(itemRuntime, catalog);
                FirstPersonLifeActionPresenter presenter =
                    presenterObject.AddComponent<FirstPersonLifeActionPresenter>();
                typeof(FirstPersonLifeActionPresenter).GetField(
                        "items",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(presenter, itemRuntime);
                MethodInfo resolve = typeof(FirstPersonLifeActionPresenter)
                    .GetMethod(
                        "ResolveItemPresentation",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(resolve, Is.Not.Null);

                foreach (string definitionId in new[]
                         {
                             "item.milk",
                             "item.juice-concentrate",
                             "item.buttermilk",
                             "item.orange-juice",
                             "item.mustard",
                             "item.ketchup",
                         })
                {
                    object presentation = resolve.Invoke(
                        presenter,
                        new object[]
                        {
                            new ItemActionCompleted(
                                ItemActionKind.Used,
                                ItemStableIdUtility.CreateDeterministic(
                                    $"test.drink-food.{definitionId}"),
                                definitionId,
                                0f),
                        });
                    Assert.That(
                        presentation?.ToString(),
                        Is.EqualTo("Drink"),
                        definitionId);
                }
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
                Object.DestroyImmediate(itemRuntimeObject);
            }
        }

        private void Restore(
            float stress = 0f,
            float urine = 0f,
            float fatigue = 0f,
            float dirtiness = 0f,
            float intoxication = 0f,
            float hangover = 0f,
            float weightKilograms = 83f)
        {
            var dto = PlayerNeedsSaveDto.Fresh();
            dto.stress = stress;
            dto.urine = urine;
            dto.fatigue = fatigue;
            dto.dirtiness = dirtiness;
            dto.intoxication = intoxication;
            dto.hangover = hangover;
            dto.weightKilograms = weightKilograms;
            Assert.That(needs.TryRestoreDto(dto, out string failure), Is.True, failure);
        }

        private sealed class ClockAdvanceFixture : IGameTimeAdvanceService
        {
            private readonly GameTimeService service;

            public ClockAdvanceFixture(GameTimeService configuredService)
            {
                service = configuredService;
            }

            public bool TryAdvanceGameSeconds(
                double gameSeconds,
                out string failure)
            {
                service.AdvanceWhileRetainingPause(gameSeconds);
                failure = string.Empty;
                return true;
            }
        }
    }
}
