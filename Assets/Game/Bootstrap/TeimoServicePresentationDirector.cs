using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Characters;
using MSC.Items;
using MSC.Items.Presentation;
using MSC.LegacyImport;
using MSC.NPC;
using MSC.Player;
using MSC.Presentation.Fluid;
using MSC.Services;
using MSC.Services.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Presentation-only Teimo service choreography. Transactions, schedules,
    /// items and saves remain authoritative in their owning runtimes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TeimoServicePresentationDirector : MonoBehaviour
    {
        public const string TeimoCharacterId =
            "character.fixture.stationary-service";
        public const string MealOfferId =
            "service.pub.sausage-and-fries";
        public const string PubLocationId =
            "service.location.teimo-pub";

        private const string CashRegisterAction =
            "action.character.teimo.cash-register";
        private const string AngryInAction =
            "action.character.teimo.angry-in";
        private const string AngryAction =
            "action.character.teimo.angry";
        private const string GiveDrinkAction =
            "action.character.teimo.give-drink";
        private const string BringFoodAction =
            "action.character.teimo.bring-food";
        private const string LeanTableOutAction =
            "action.character.teimo.lean-table-out";
        private const string LeanTableInAction =
            "action.character.teimo.lean-table-in";
        private const string MoveKitchenInAction =
            "action.character.teimo.move-kitchen-in";
        private const string MoveKitchenOutAction =
            "action.character.teimo.move-kitchen-out";
        private const string WalkHandOutAction =
            "action.character.teimo.walk-hand-out";
        private const string ServiceWalkAction =
            "action.character.teimo.service-walk";
        private const string CookAction =
            "action.character.teimo.cook";
        private const string Cook2Action =
            "action.character.teimo.cook2";
        private const string FridgeOpenAction =
            "action.character.teimo.fridge-open";
        private const string FridgeCloseAction =
            "action.character.teimo.fridge-close";
        private const string MicrowaveDoorAction =
            "action.character.teimo.microwave-door";
        private const string MealDefinitionId =
            "item.sausage-and-potatoes-meal";
        private const string VodkaEffectId =
            "effect.service.pub.vodka-shot.deferred";

        private const string FridgeDoorSourcePivotStableId =
            "dba046cb262c15b7563e3db01d958d8d";
        private const string MicrowaveDoorSourcePivotStableId =
            "e987cbf980f7f153ad92a0f7db863845";
        private const string MicrowaveFoodStableId =
            "30dcc88157750906751cee877504b5d6";

        private static readonly Vector3 FridgeDoorHingeWorldPosition =
            new(-1379.0602f, 6.772943f, 143.67981f);
        private static readonly Vector3 MicrowaveDoorHingeWorldPosition =
            new(-1377.9106f, 7.1120143f, 144.1311f);
        private const float CounterPropRevealSeconds = 2.3f;
        private const float CounterPropReleaseSeconds = 5.3f;
        private const float FoodCounterReleaseSeconds = 0.4f;
        private const float MicrowaveCookMinimumSeconds = 55f;
        private const float MicrowaveCookMaximumSeconds = 70f;
        // The Sound FSM starts when OPEN is sent. By Close fridge, five seconds
        // have elapsed; openclose (3.045669) + start (4.612063) + end
        // (3.279909) therefore leave 5.937641 seconds around CookTime.
        private const float MicrowaveSoundSequenceRemainderSeconds =
            5.937641f;

        // Exact donor Pivot values. The motion clips animate this root while a
        // separate looping body clip supplies the leg movement.
        private static readonly Vector3 DonorKitchenMotionStart =
            new(-6.5f, 0f, 0f);
        private static readonly Quaternion DonorShopWorldRotation =
            new(-1.7881393e-7f, 0.9598001f, -1.3411045e-7f, 0.2806844f);
        private static readonly Vector3 LeftHandPropPosition =
            new(-0.07702478f, -0.009046347f, 0.058048535f);
        private static readonly Quaternion LeftHandPropRotation =
            new(0.6986096f, 0.01243795f, 0.36545095f, -0.61500865f);
        private static readonly Vector3 RightHandMealPosition =
            new(-0.165f, -0.033f, -0.078f);
        private static readonly Quaternion RightHandMealRotation =
            new(-0.7457655f, -0.20686212f, 0.13369338f, 0.6190057f);
        private static readonly Vector3 BoxedMealInItemPivotPosition =
            new(0.0149f, -0.0625f, 0.0112f);
        private static readonly Quaternion BoxedMealInItemPivotRotation =
            new(-0.1372799f, 0.18210278f, 0.65559274f, -0.71985483f);

        private readonly Queue<ServicePresentationRequest> serviceRequests = new();
        private NpcWorldRuntime npcWorld;
        private ItemDefinitionCatalog itemDefinitions;
        private Transform aimOrigin;
        private PlayerInputRouter input;
        private ProceduralFluidStreamPresenter urineStream;
        private Action<string> status;
        private Coroutine serviceRoutine;
        private Coroutine angryRoutine;
        private bool angryPoseActive;
        private LegacyCharacterPresentationBinding overridePresentation;
        private GameObject activeServiceProp;
        private GameObject activeServiceVisual;
        private GameObject activeMicrowaveFood;
        private readonly List<Material> activeServiceMaterials = new();
        private bool positionOverrideActive;
        private Vector3 overrideWorldPosition;
        private Quaternion overrideWorldRotation;
        private Vector3 overrideReturnWorldPosition;
        private Quaternion overrideReturnWorldRotation;
        private bool activeRequestIsMeal;
        private int pendingMealCount;

        public bool IsPreparingMeal => activeRequestIsMeal || pendingMealCount > 0;
        public int PendingMealCount => pendingMealCount;
        public bool IsAngryPoseActive => angryPoseActive;

        public void Configure(
            NpcWorldRuntime configuredNpcWorld,
            ItemDefinitionCatalog configuredItemDefinitions,
            Transform configuredAimOrigin,
            PlayerInputRouter configuredInput,
            ProceduralFluidStreamPresenter configuredUrineStream,
            Action<string> configuredStatus = null)
        {
            UnsubscribeInput();
            npcWorld = configuredNpcWorld;
            itemDefinitions = configuredItemDefinitions;
            aimOrigin = configuredAimOrigin;
            input = configuredInput;
            urineStream = configuredUrineStream;
            status = configuredStatus;
            if (input != null)
            {
                input.MiddleFingerRequested += HandleProvocationRequested;
            }

            if (urineStream != null)
            {
                urineStream.Impacted += HandleUrineImpact;
            }
        }

        public void HandleServiceFeedback(ServiceInteractionFeedback feedback)
        {
            if (!feedback.Result.Succeeded)
            {
                return;
            }

            if (feedback.Kind == ServiceInteractionKind.StoreCheckout)
            {
                ClearAngryPose();
                TryPlay(
                    CashRegisterAction,
                    restartStateAnimationAfter: false);
                return;
            }

            if (feedback.Kind != ServiceInteractionKind.PubPurchase)
            {
                return;
            }

            // Pub presentation owns its queued handoff. Cigarettes are also
            // retrieved below the pub counter, never scanned at the store till.
            ClearAngryPose();
        }

        public void RequestMealPreparation(
            in ServiceHandoffRequest handoff,
            Action completion)
        {
            ClearAngryPose();
            pendingMealCount++;
            EnqueueServicePresentation(new ServicePresentationRequest(
                in handoff,
                ServicePresentationKind.Meal,
                completion));
        }

        public void RequestCounterHandoff(
            in ServiceHandoffRequest handoff,
            Action completion)
        {
            ClearAngryPose();
            EnqueueServicePresentation(new ServicePresentationRequest(
                in handoff,
                ServicePresentationKind.CounterHandoff,
                completion));
        }

        private void EnqueueServicePresentation(
            ServicePresentationRequest request)
        {
            serviceRequests.Enqueue(request);
            if (serviceRoutine == null)
            {
                serviceRoutine = StartCoroutine(ProcessServiceQueue());
            }
        }

        private IEnumerator ProcessServiceQueue()
        {
            while (serviceRequests.Count > 0)
            {
                ServicePresentationRequest request = serviceRequests.Dequeue();
                if (!TryGetTeimo(
                        out LegacyCharacterPresentationBinding teimo))
                {
                    serviceRequests.Enqueue(request);
                    status?.Invoke("Теймо ждёт загрузки бара, заказ сохранён");
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                activeRequestIsMeal = request.Kind == ServicePresentationKind.Meal;
                if (activeRequestIsMeal)
                {
                    pendingMealCount = Mathf.Max(0, pendingMealCount - 1);
                    yield return PrepareMeal(teimo, request);
                }
                else
                {
                    yield return PresentCounterHandoff(teimo, request);
                }

                request.CompleteOnce();
                activeRequestIsMeal = false;
            }

            serviceRoutine = null;
        }

        private IEnumerator PresentCounterHandoff(
            LegacyCharacterPresentationBinding teimo,
            ServicePresentationRequest request)
        {
            status?.Invoke("Теймо достаёт заказ из-под прилавка");
            BeginServiceProp(
                teimo,
                request,
                "hand_left",
                LeftHandPropPosition,
                LeftHandPropRotation);
            activeServiceProp?.SetActive(false);
            teimo.TryPlayAction(GiveDrinkAction, resumeStateAfter: false);
            float duration = GetActionDuration(teimo, GiveDrinkAction, 6f);
            yield return new WaitForSeconds(Mathf.Min(
                CounterPropRevealSeconds,
                duration));
            activeServiceProp?.SetActive(true);
            yield return new WaitForSeconds(Mathf.Max(
                0f,
                Mathf.Min(CounterPropReleaseSeconds, duration) -
                CounterPropRevealSeconds));
            EndServiceProp();
            request.CompleteOnce();
            yield return new WaitForSeconds(Mathf.Max(
                0f,
                duration - CounterPropReleaseSeconds));
            teimo.StopActionAndResumeState(restartStateAnimation: false);
            status?.Invoke("Теймо поставил заказ на прилавок");
        }

        private IEnumerator PrepareMeal(
            LegacyCharacterPresentationBinding teimo,
            ServicePresentationRequest request)
        {
            BeginPositionOverride(teimo);
            BindKitchenFixtures(
                out Transform fridgeDoor,
                out Transform microwaveDoor,
                out GameObject microwaveFood);
            Quaternion fridgeClosed = fridgeDoor != null
                ? fridgeDoor.localRotation
                : Quaternion.identity;
            Quaternion microwaveClosed = microwaveDoor != null
                ? microwaveDoor.localRotation
                : Quaternion.identity;
            BeginMealProp(teimo, request, microwaveFood);

            status?.Invoke("Теймо пошёл готовить картошку");
            yield return PlayAndWait(teimo, LeanTableOutAction, 0.5f);
            yield return PlayDonorMotion(
                teimo,
                MoveKitchenInAction,
                ServiceWalkAction,
                4f);

            teimo.TryPlayAction(CookAction, resumeStateAfter: false);
            yield return new WaitForSeconds(0.5f);
            StartCoroutine(PlayDonorDoorClip(
                teimo,
                FridgeOpenAction,
                fridgeDoor,
                fridgeClosed,
                0.5833333f));
            yield return new WaitForSeconds(1.5f);
            AttachMealPropToRightHand(teimo);
            yield return new WaitForSeconds(2.5f);
            activeServiceProp?.SetActive(false);
            StartCoroutine(PlayDonorDoorClip(
                teimo,
                MicrowaveDoorAction,
                microwaveDoor,
                microwaveClosed,
                2f));
            yield return new WaitForSeconds(1.5f);
            activeMicrowaveFood?.SetActive(true);
            yield return new WaitForSeconds(3.5f);
            StartCoroutine(PlayDonorDoorClip(
                teimo,
                FridgeCloseAction,
                fridgeDoor,
                fridgeClosed,
                0.5f));

            status?.Invoke("Картошка греется");
            yield return new WaitForSeconds(
                MicrowaveSoundSequenceRemainderSeconds +
                UnityEngine.Random.Range(
                    MicrowaveCookMinimumSeconds,
                    MicrowaveCookMaximumSeconds));

            teimo.TryPlayAction(Cook2Action, resumeStateAfter: false);
            yield return new WaitForSeconds(1.5f);
            StartCoroutine(PlayDonorDoorClip(
                teimo,
                MicrowaveDoorAction,
                microwaveDoor,
                microwaveClosed,
                2f));
            yield return new WaitForSeconds(1.5f);
            activeMicrowaveFood?.SetActive(false);
            AttachMealPropToRightHand(teimo);
            yield return new WaitForSeconds(1f);
            AttachMealPropToLeftHand(teimo);
            yield return new WaitForSeconds(1f);

            yield return PlayDonorMotion(
                teimo,
                MoveKitchenOutAction,
                WalkHandOutAction,
                4f);
            teimo.TryPlayAction(BringFoodAction, resumeStateAfter: false);
            float bringDuration = GetActionDuration(teimo, BringFoodAction, 1.85f);
            yield return new WaitForSeconds(Mathf.Min(
                FoodCounterReleaseSeconds,
                bringDuration));
            EndMealProp();
            request.CompleteOnce();
            yield return new WaitForSeconds(2f);
            yield return PlayAndWait(teimo, LeanTableInAction, 0.5f);
            teimo.StopActionAndResumeState(restartStateAnimation: false);

            EndPositionOverride(restoreReturnPose: true);
            status?.Invoke("Теймо принёс картошку");
        }

        private void BeginPositionOverride(
            LegacyCharacterPresentationBinding teimo)
        {
            overridePresentation = teimo;
            positionOverrideActive = true;
            overrideWorldPosition = teimo.transform.position;
            overrideWorldRotation = teimo.transform.rotation;
            overrideReturnWorldPosition = overrideWorldPosition;
            overrideReturnWorldRotation = overrideWorldRotation;
        }

        private void EndPositionOverride(bool restoreReturnPose)
        {
            if (restoreReturnPose && overridePresentation != null)
            {
                overridePresentation.transform.SetPositionAndRotation(
                    overrideReturnWorldPosition,
                    overrideReturnWorldRotation);
            }

            positionOverrideActive = false;
            overridePresentation = null;
        }

        private void BeginMealProp(
            LegacyCharacterPresentationBinding teimo,
            ServicePresentationRequest request,
            GameObject microwaveFood)
        {
            EndMealProp();
            activeMicrowaveFood = microwaveFood;
            activeMicrowaveFood?.SetActive(false);
            BeginServiceProp(
                teimo,
                request,
                "hand_right",
                RightHandMealPosition,
                RightHandMealRotation);
            activeServiceProp?.SetActive(false);
        }

        private void AttachMealPropToRightHand(
            LegacyCharacterPresentationBinding teimo)
        {
            Transform rightHand = FindDescendant(teimo?.transform, "hand_right");
            if (rightHand == null || activeServiceProp == null)
            {
                return;
            }

            activeMicrowaveFood?.SetActive(false);
            Transform prop = activeServiceProp.transform;
            prop.SetParent(rightHand, false);
            prop.localPosition = RightHandMealPosition;
            prop.localRotation = RightHandMealRotation;
            prop.localScale = Vector3.one;
            ResetServiceVisualPose();
            activeServiceProp.SetActive(true);
        }

        private void AttachMealPropToLeftHand(
            LegacyCharacterPresentationBinding teimo)
        {
            Transform leftHand = FindDescendant(teimo?.transform, "hand_left");
            if (leftHand == null || activeServiceProp == null)
            {
                return;
            }

            activeMicrowaveFood?.SetActive(false);
            Transform prop = activeServiceProp.transform;
            prop.SetParent(leftHand, false);
            prop.localPosition = LeftHandPropPosition;
            prop.localRotation = LeftHandPropRotation;
            prop.localScale = Vector3.one;
            if (activeServiceVisual != null)
            {
                activeServiceVisual.transform.localPosition =
                    BoxedMealInItemPivotPosition;
                activeServiceVisual.transform.localRotation =
                    BoxedMealInItemPivotRotation;
            }

            activeServiceProp.SetActive(true);
        }

        private void ResetServiceVisualPose()
        {
            if (activeServiceVisual == null)
            {
                return;
            }

            activeServiceVisual.transform.localPosition = Vector3.zero;
            activeServiceVisual.transform.localRotation = Quaternion.identity;
        }

        private void EndMealProp()
        {
            activeMicrowaveFood?.SetActive(false);
            activeMicrowaveFood = null;
            EndServiceProp();
        }

        private void BeginServiceProp(
            LegacyCharacterPresentationBinding teimo,
            ServicePresentationRequest request,
            string handName,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            EndServiceProp();
            Transform hand = FindDescendant(teimo?.transform, handName);
            if (hand == null)
            {
                status?.Invoke("У Теймо не найден сокет руки для выдачи заказа");
                return;
            }

            var holder = new GameObject("Temporary Teimo service prop");
            holder.transform.SetParent(hand, false);
            holder.transform.localPosition = localPosition;
            holder.transform.localRotation = localRotation;
            holder.transform.localScale = Vector3.one;

            string requestedDefinitionId = !string.IsNullOrEmpty(
                request.ItemDefinitionId)
                    ? request.ItemDefinitionId
                    : request.Kind == ServicePresentationKind.Meal
                        ? MealDefinitionId
                        : string.Empty;
            bool created = false;
            if (!string.IsNullOrEmpty(requestedDefinitionId) &&
                itemDefinitions != null &&
                itemDefinitions.TryGet(
                    requestedDefinitionId,
                    out ItemDefinitionRecord definition) &&
                ItemPresentationProviderHub.Current != null)
            {
                created = ItemPresentationProviderHub.Current.TryInstantiate(
                    definition,
                    holder.transform,
                    out activeServiceVisual);
            }
            else if (string.Equals(
                         request.EffectId,
                         VodkaEffectId,
                         StringComparison.Ordinal))
            {
                CreateVodkaShotPresentation(holder.transform);
                created = true;
            }

            if (!created)
            {
                DestroyPresentationObject(holder);
                status?.Invoke(
                    $"Не удалось показать товар '{requestedDefinitionId}' в руке Теймо");
                return;
            }

            activeServiceProp = holder;
            ResetServiceVisualPose();
        }

        private void EndServiceProp()
        {
            if (activeServiceProp != null)
            {
                activeServiceProp.SetActive(false);
                DestroyPresentationObject(activeServiceProp);
                activeServiceProp = null;
            }

            activeServiceVisual = null;

            for (int index = 0; index < activeServiceMaterials.Count; index++)
            {
                DestroyPresentationObject(activeServiceMaterials[index]);
            }

            activeServiceMaterials.Clear();
        }

        private void CreateVodkaShotPresentation(Transform parent)
        {
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glass.name = "Temporary vodka shot glass";
            glass.transform.SetParent(parent, false);
            glass.transform.localPosition = new Vector3(0f, 0f, -0.0455f);
            glass.transform.localRotation = Quaternion.identity;
            glass.transform.localScale = new Vector3(0.055f, 0.0285f, 0.06f);
            DisableAndDestroyCollider(glass);
            Material glassMaterial = CreateTransparentMaterial(
                "Temporary Teimo shot glass",
                new Color(0.78f, 0.9f, 0.96f, 0.3f));
            if (glassMaterial != null)
            {
                glass.GetComponent<Renderer>().sharedMaterial = glassMaterial;
            }

            GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            liquid.name = "Temporary vodka fill";
            liquid.transform.SetParent(parent, false);
            liquid.transform.localPosition = new Vector3(0f, -0.006f, -0.0455f);
            liquid.transform.localRotation = Quaternion.identity;
            liquid.transform.localScale = new Vector3(0.043f, 0.020f, 0.047f);
            DisableAndDestroyCollider(liquid);
            Material liquidMaterial = CreateTransparentMaterial(
                "Temporary Teimo vodka",
                new Color(0.86f, 0.92f, 0.96f, 0.58f));
            if (liquidMaterial != null)
            {
                liquid.GetComponent<Renderer>().sharedMaterial = liquidMaterial;
            }
        }

        private Material CreateTransparentMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = name,
                renderQueue = 3000,
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_SurfaceType"))
            {
                material.SetFloat("_SurfaceType", 1f);
            }

            if (material.HasProperty("_BlendMode"))
            {
                material.SetFloat("_BlendMode", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            activeServiceMaterials.Add(material);
            return material;
        }

        private static void DisableAndDestroyCollider(GameObject owner)
        {
            Collider collider = owner.GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            collider.enabled = false;
            DestroyPresentationObject(collider);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (string.Equals(
                        descendants[index].name,
                        name,
                        StringComparison.Ordinal))
                {
                    return descendants[index];
                }
            }

            return null;
        }

        private static void DestroyPresentationObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private IEnumerator PlayDonorMotion(
            LegacyCharacterPresentationBinding presentation,
            string motionActionId,
            string bodyActionId,
            float fallbackSeconds)
        {
            if (!presentation.TryGetActionClip(
                    motionActionId,
                    out AnimationClip motionClip))
            {
                yield return new WaitForSeconds(fallbackSeconds);
                yield break;
            }

            var sampler = new GameObject("Temporary Teimo donor motion sampler");
            sampler.hideFlags = HideFlags.HideAndDontSave;
            presentation.TryPlayAction(bodyActionId, resumeStateAfter: false);
            float duration = Mathf.Max(0.01f, motionClip.length);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                motionClip.SampleAnimation(sampler, elapsed);
                ApplyDonorMotionSample(sampler.transform);
                yield return null;
            }

            motionClip.SampleAnimation(sampler, duration);
            ApplyDonorMotionSample(sampler.transform);
            DestroyPresentationObject(sampler);
        }

        private void ApplyDonorMotionSample(Transform sample)
        {
            Vector3 donorDelta = sample.localPosition - DonorKitchenMotionStart;
            overrideWorldPosition = overrideReturnWorldPosition +
                DonorShopWorldRotation * donorDelta;
            Quaternion worldDelta =
                DonorShopWorldRotation *
                sample.localRotation *
                Quaternion.Inverse(DonorShopWorldRotation);
            overrideWorldRotation = worldDelta * overrideReturnWorldRotation;
        }

        private static IEnumerator PlayDonorDoorClip(
            LegacyCharacterPresentationBinding presentation,
            string actionId,
            Transform door,
            Quaternion authoredClosedRotation,
            float fallbackSeconds)
        {
            if (door == null ||
                !presentation.TryGetActionClip(actionId, out AnimationClip clip))
            {
                yield break;
            }

            var sampler = new GameObject("Temporary Teimo donor door sampler");
            sampler.hideFlags = HideFlags.HideAndDontSave;
            float duration = Mathf.Max(0.01f, clip.length > 0f
                ? clip.length
                : fallbackSeconds);
            float closedSampleTime = string.Equals(
                actionId,
                FridgeCloseAction,
                StringComparison.Ordinal)
                    ? duration
                    : 0f;
            clip.SampleAnimation(sampler, closedSampleTime);
            Quaternion donorClosed = sampler.transform.localRotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
                clip.SampleAnimation(sampler, elapsed);
                door.localRotation = authoredClosedRotation *
                    Quaternion.Inverse(donorClosed) *
                    sampler.transform.localRotation;
                yield return null;
            }

            DestroyPresentationObject(sampler);
        }

        private static float GetActionDuration(
            LegacyCharacterPresentationBinding presentation,
            string actionId,
            float fallbackSeconds) =>
            presentation.TryGetActionDuration(actionId, out float measured)
                ? measured
                : fallbackSeconds;

        private static IEnumerator PlayAndWait(
            LegacyCharacterPresentationBinding presentation,
            string actionId,
            float fallbackSeconds)
        {
            presentation.TryPlayAction(actionId, resumeStateAfter: false);
            float duration = presentation.TryGetActionDuration(
                actionId,
                out float measured)
                ? measured
                : fallbackSeconds;
            yield return new WaitForSeconds(Mathf.Max(0.05f, duration));
        }

        private void HandleProvocationRequested()
        {
            if (angryRoutine != null || angryPoseActive ||
                !IsPlayerAimingAtTeimo())
            {
                return;
            }

            angryRoutine = StartCoroutine(PlayAngryReaction());
        }

        private void HandleUrineImpact(GameObject other)
        {
            if (angryRoutine != null ||
                angryPoseActive ||
                other == null ||
                !TryGetTeimo(out LegacyCharacterPresentationBinding teimo))
            {
                return;
            }

            Transform hit = other.transform;
            if (hit != teimo.transform && !hit.IsChildOf(teimo.transform))
            {
                return;
            }

            angryRoutine = StartCoroutine(PlayAngryReaction());
        }

        private IEnumerator PlayAngryReaction()
        {
            if (!TryGetTeimo(out LegacyCharacterPresentationBinding teimo))
            {
                angryRoutine = null;
                yield break;
            }

            yield return PlayAndWait(teimo, AngryInAction, 0.5f);
            angryPoseActive = teimo.TryPlayAction(
                AngryAction,
                resumeStateAfter: false);
            angryRoutine = null;
        }

        private void ClearAngryPose()
        {
            if (angryRoutine != null)
            {
                StopCoroutine(angryRoutine);
                angryRoutine = null;
            }

            bool wasAngry = angryPoseActive;
            angryPoseActive = false;
            if (!TryGetTeimo(
                    out LegacyCharacterPresentationBinding teimo))
            {
                return;
            }

            bool animationIsAngry = string.Equals(
                    teimo.ActiveActionId,
                    AngryInAction,
                    StringComparison.Ordinal) ||
                string.Equals(
                    teimo.ActiveActionId,
                    AngryAction,
                    StringComparison.Ordinal);
            if (wasAngry || animationIsAngry)
            {
                teimo.StopActionAndResumeState();
            }
        }

        private bool IsPlayerAimingAtTeimo()
        {
            if (aimOrigin == null ||
                !TryGetTeimo(out LegacyCharacterPresentationBinding teimo))
            {
                return false;
            }

            Vector3 target = teimo.transform.position + Vector3.up * 0.95f;
            Vector3 delta = target - aimOrigin.position;
            float forwardDistance = Vector3.Dot(aimOrigin.forward, delta);
            if (forwardDistance < 0f || forwardDistance > 3f)
            {
                return false;
            }

            Vector3 closest = aimOrigin.position +
                aimOrigin.forward * forwardDistance;
            return Vector3.Distance(closest, target) <= 0.35f;
        }

        private bool TryPlay(
            string actionId,
            bool restartStateAnimationAfter = true) =>
            TryGetTeimo(out LegacyCharacterPresentationBinding teimo) &&
            teimo.TryPlayAction(
                actionId,
                resumeStateAfter: true,
                restartStateAnimationAfter: restartStateAnimationAfter);

        private bool TryGetTeimo(
            out LegacyCharacterPresentationBinding presentation)
        {
            presentation = null;
            return npcWorld != null &&
                   npcWorld.TryGetPresentation(
                       TeimoCharacterId,
                       out presentation);
        }

        private static void BindKitchenFixtures(
            out Transform fridgeDoor,
            out Transform microwaveDoor,
            out GameObject microwaveFood)
        {
            Transform fridgeDoorVisual = null;
            Transform microwaveDoorVisual = null;
            microwaveFood = null;
            GameObject microwaveFoodFallback = null;
            DonorWorldBaselineEntityMetadata[] entities =
                UnityEngine.Object.FindObjectsByType<
                    DonorWorldBaselineEntityMetadata>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < entities.Length; index++)
            {
                DonorWorldBaselineEntityMetadata entity = entities[index];
                if (string.Equals(
                        entity.SourceParentStableId,
                        FridgeDoorSourcePivotStableId,
                        StringComparison.Ordinal))
                {
                    fridgeDoorVisual = entity.transform;
                }
                else if (string.Equals(
                             entity.SourceParentStableId,
                             MicrowaveDoorSourcePivotStableId,
                             StringComparison.Ordinal))
                {
                    microwaveDoorVisual = entity.transform;
                }

                if (string.Equals(
                        entity.StableId,
                        MicrowaveFoodStableId,
                        StringComparison.Ordinal))
                {
                    microwaveFood = entity.gameObject;
                }
                else if (microwaveFood == null && string.Equals(
                             entity.SourceParentStableId,
                             MicrowaveFoodStableId,
                             StringComparison.Ordinal))
                {
                    microwaveFoodFallback = entity.gameObject;
                }
            }

            microwaveFood ??= microwaveFoodFallback;
            fridgeDoor = EnsureDoorPivot(
                fridgeDoorVisual,
                FridgeDoorSourcePivotStableId,
                FridgeDoorHingeWorldPosition,
                "Teimo Fridge Door Pivot");
            microwaveDoor = EnsureDoorPivot(
                microwaveDoorVisual,
                MicrowaveDoorSourcePivotStableId,
                MicrowaveDoorHingeWorldPosition,
                "Teimo Microwave Door Pivot");
        }

        private static Transform EnsureDoorPivot(
            Transform visual,
            string sourcePivotStableId,
            Vector3 hingeWorldPosition,
            string displayName)
        {
            if (visual == null)
            {
                return null;
            }

            TeimoKitchenDoorPivot existing =
                visual.GetComponentInParent<TeimoKitchenDoorPivot>();
            if (existing != null && string.Equals(
                    existing.SourcePivotStableId,
                    sourcePivotStableId,
                    StringComparison.Ordinal))
            {
                return existing.transform;
            }

            var pivotObject = new GameObject(displayName);
            Scene scene = visual.gameObject.scene;
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(pivotObject, scene);
            }

            Transform pivot = pivotObject.transform;
            pivot.SetParent(visual.parent, worldPositionStays: false);
            pivot.SetPositionAndRotation(hingeWorldPosition, visual.rotation);
            pivotObject.AddComponent<TeimoKitchenDoorPivot>().Configure(
                sourcePivotStableId);
            visual.SetParent(pivot, worldPositionStays: true);
            return pivot;
        }

        private void LateUpdate()
        {
            if (angryPoseActive &&
                (!TryGetTeimo(
                     out LegacyCharacterPresentationBinding teimo) ||
                 !string.Equals(
                     teimo.ActiveActionId,
                     AngryAction,
                     StringComparison.Ordinal)))
            {
                angryPoseActive = false;
            }

            if (!positionOverrideActive || overridePresentation == null)
            {
                return;
            }

            overridePresentation.transform.SetPositionAndRotation(
                overrideWorldPosition,
                overrideWorldRotation);
        }

        private void OnDestroy()
        {
            EndPositionOverride(restoreReturnPose: true);
            ClearAngryPose();
            EndMealProp();
            UnsubscribeInput();
        }

        private void UnsubscribeInput()
        {
            if (input != null)
            {
                input.MiddleFingerRequested -= HandleProvocationRequested;
                input = null;
            }

            if (urineStream != null)
            {
                urineStream.Impacted -= HandleUrineImpact;
                urineStream = null;
            }
        }

        private enum ServicePresentationKind
        {
            CounterHandoff = 0,
            Meal = 1,
        }

        private sealed class ServicePresentationRequest
        {
            private readonly Action completion;
            private bool completionInvoked;

            public ServicePresentationRequest(
                in ServiceHandoffRequest handoff,
                ServicePresentationKind kind,
                Action completion)
            {
                OperationId = handoff.OperationId;
                ItemDefinitionId = handoff.ItemDefinitionId;
                EffectId = handoff.EffectId;
                VariantIndex = handoff.VariantIndex;
                Quantity = handoff.Quantity;
                Kind = kind;
                this.completion = completion;
            }

            public string OperationId { get; }
            public string ItemDefinitionId { get; }
            public string EffectId { get; }
            public int VariantIndex { get; }
            public int Quantity { get; }
            public ServicePresentationKind Kind { get; }
            public void CompleteOnce()
            {
                if (completionInvoked)
                {
                    return;
                }

                completionInvoked = true;
                completion?.Invoke();
            }
        }
    }

    [DisallowMultipleComponent]
    internal sealed class TeimoKitchenDoorPivot : MonoBehaviour
    {
        [SerializeField] private string sourcePivotStableId = string.Empty;

        public string SourcePivotStableId => sourcePivotStableId;

        public void Configure(string configuredSourcePivotStableId) =>
            sourcePivotStableId = configuredSourcePivotStableId ?? string.Empty;
    }

    internal sealed class TeimoPreparedServiceHandoffBackend :
        IServiceHandoffBackend,
        IServiceHandoffPreflight
    {
        private readonly IServiceHandoffBackend inner;
        private readonly IServiceHandoffPreflight preflight;
        private readonly TeimoServicePresentationDirector director;
        private readonly HashSet<string> preparing = new(StringComparer.Ordinal);
        private readonly HashSet<string> ready = new(StringComparer.Ordinal);
        private ServiceRuntime runtime;

        public TeimoPreparedServiceHandoffBackend(
            IServiceHandoffBackend configuredInner,
            TeimoServicePresentationDirector configuredDirector)
        {
            inner = configuredInner ??
                throw new ArgumentNullException(nameof(configuredInner));
            preflight = configuredInner as IServiceHandoffPreflight ??
                throw new ArgumentException(
                    "Prepared handoff requires preflight support.",
                    nameof(configuredInner));
            director = configuredDirector ??
                throw new ArgumentNullException(nameof(configuredDirector));
        }

        public void BindRuntime(ServiceRuntime configuredRuntime) =>
            runtime = configuredRuntime ??
                throw new ArgumentNullException(nameof(configuredRuntime));

        public bool CanFulfill(
            in ServiceHandoffRequest request,
            out string failure) =>
            preflight.CanFulfill(in request, out failure);

        public bool TryFulfill(
            in ServiceHandoffRequest request,
            out string failure)
        {
            if (!string.Equals(
                    request.LocationId,
                    TeimoServicePresentationDirector.PubLocationId,
                    StringComparison.Ordinal))
            {
                return inner.TryFulfill(in request, out failure);
            }

            if (ready.Contains(request.OperationId))
            {
                bool fulfilled = inner.TryFulfill(in request, out failure);
                if (fulfilled)
                {
                    ready.Remove(request.OperationId);
                    preparing.Remove(request.OperationId);
                }

                return fulfilled;
            }

            if (preparing.Add(request.OperationId))
            {
                string operationId = request.OperationId;
                Action completion = () =>
                {
                    ready.Add(operationId);
                    preparing.Remove(operationId);
                    runtime?.RetryFulfillment(operationId);
                };
                if (string.Equals(
                        request.OfferId,
                        TeimoServicePresentationDirector.MealOfferId,
                        StringComparison.Ordinal))
                {
                    director.RequestMealPreparation(in request, completion);
                }
                else
                {
                    director.RequestCounterHandoff(in request, completion);
                }
            }

            failure = string.Equals(
                    request.OfferId,
                    TeimoServicePresentationDirector.MealOfferId,
                    StringComparison.Ordinal)
                ? "Teimo is preparing the paid meal."
                : "Teimo is retrieving the paid pub order.";
            return false;
        }
    }

    internal sealed class TeimoPubEffectHandoffBackend :
        IServiceEffectHandoffBackend,
        IServiceEffectHandoffPreflight
    {
        private const string VodkaEffectId =
            "effect.service.pub.vodka-shot.deferred";
        private readonly HashSet<string> applied = new(StringComparer.Ordinal);

        public bool CanApply(
            string effectId,
            int quantity,
            out string failure)
        {
            bool valid = quantity > 0 && string.Equals(
                effectId,
                VodkaEffectId,
                StringComparison.Ordinal);
            failure = valid
                ? string.Empty
                : $"Service effect '{effectId}' has no runtime handoff.";
            return valid;
        }

        public bool TryApply(
            string operationId,
            int lineIndex,
            string effectId,
            int quantity,
            out string failure)
        {
            if (!CanApply(effectId, quantity, out failure))
            {
                return false;
            }

            // The purchased donor shot is consumed during Teimo's handoff.
            // Intoxication tuning has no locked measurement yet, so this
            // boundary records idempotent fulfillment without fabricating a
            // needs delta.
            applied.Add($"{operationId}|{lineIndex}");
            failure = string.Empty;
            return true;
        }
    }
}
