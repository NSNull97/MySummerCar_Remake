using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Lifecycle;
using MSC.Services.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Project-owned physical book interaction layered over reviewed donor
    /// catalog pages. Input, camera ownership and selections remain independent
    /// from donor scene objects and PlayMaker state machines.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhysicalServiceCatalogController : MonoBehaviour,
        IServiceCatalogBookPresenter
    {
        private enum BookState
        {
            Closed,
            Opening,
            Open,
            Closing,
        }

        private readonly struct PageHotspot
        {
            public PageHotspot(float x, float y, int entryOffset)
                : this(x, y, entryOffset, float.NaN)
            {
            }

            public PageHotspot(
                float x,
                float y,
                int entryOffset,
                float variant)
            {
                NormalizedPosition = new Vector2(x, y);
                EntryOffset = entryOffset;
                Variant = variant;
            }

            public Vector2 NormalizedPosition { get; }
            public int EntryOffset { get; }
            public float Variant { get; }
        }

        private const float HomePageWidth = 0.21f;
        private const float HomePageHeight = 0.3f;
        private const float FleetariPageWidth = 0.21f;
        private const float FleetariPageHeight = 0.42f;
        private const float BookAnimationSeconds = 0.28f;
        private const float PageTurnSeconds = 0.24f;
        private const float PresentationRetrySeconds = 0.5f;
        private const int HomeProductPageCount = 8;
        private const int HomeSpreadCount = HomeProductPageCount / 2 + 1;
        private const int FleetariProductPageCount = 6;

        private static readonly int[] HomePageEntryCounts =
            { 5, 6, 5, 4, 11, 4, 3, 8 };

        // Donor Sheets/Magazine/Products local positions, in the exact order
        // of HomePartsMailOrderCatalog. The order form used one TextMesh per
        // selected product instead of one multiline overlay.
        private static readonly Vector2[] HomeOrderRowPositions =
        {
            new(0.1465f, -0.4156f),
            new(-0.0158f, -0.3217f),
            new(-0.0158f, -0.3079f),
            new(-0.0247f, -0.4370f),
            new(0.1450f, -0.3910f),
            new(-0.0219f, -0.1582f),
            new(0.1450f, -0.4017f),
            new(-0.0321f, -0.2712f),
            new(0.1450f, -0.3510f),
            new(-0.0214f, -0.2083f),
            new(0.1441f, -0.3001f),
            new(0.1400f, -0.2620f),
            new(-0.0185f, -0.4130f),
            new(-0.0121f, -0.3322f),
            new(0.1399f, -0.1857f),
            new(-0.0180f, -0.3439f),
            new(-0.0143f, -0.1832f),
            new(0.1400f, -0.1980f),
            new(-0.0207f, -0.2340f),
            new(0.1450f, -0.3250f),
            new(-0.0265f, -0.2836f),
            new(0.1447f, -0.2741f),
            new(-0.0143f, -0.3734f),
            new(-0.0199f, -0.2210f),
            new(0.1400f, -0.2350f),
            new(-0.0197f, -0.1956f),
            new(-0.0179f, -0.2460f),
            new(-0.0143f, -0.3877f),
            new(-0.0247f, -0.2582f),
            new(0.1400f, -0.2080f),
            new(-0.0259f, -0.2962f),
            new(0.1450f, -0.3780f),
            new(0.1439f, -0.1595f),
            new(0.1400f, -0.2480f),
            new(0.1426f, -0.4287f),
            new(0.1400f, -0.2220f),
            new(0.1490f, -0.3120f),
            new(-0.0157f, -0.1703f),
            new(0.1457f, -0.4403f),
            new(-0.0143f, -0.4005f),
            new(-0.0185f, -0.4264f),
            new(0.1450f, -0.3640f),
            new(-0.0179f, -0.3578f),
            new(0.1471f, -0.3372f),
            new(0.1399f, -0.1725f),
            new(0.1479f, -0.2864f),
        };

        private static readonly PageHotspot[][] HomeHotspots =
        {
            HomeDonorHotspots(
                (-0.293f, -0.205f),
                (-0.096f, -0.086f),
                (-0.090f, 0.222f),
                (-0.236f, 0.192f),
                (-0.158f, 0.066f)),
            HomeDonorHotspots(
                (-0.255f, 0.203f),
                (-0.085f, -0.032f),
                (-0.2697f, 0.0235f),
                (-0.300f, -0.195f),
                (-0.065f, 0.125f),
                (-0.2259f, -0.084f)),
            HomeDonorHotspots(
                (-0.3012f, 0.110f),
                (-0.0683f, -0.0009f),
                (-0.2029f, -0.1723f),
                (-0.063f, -0.169f),
                (-0.2106f, -0.0821f)),
            HomeDonorHotspots(
                (-0.184f, -0.194f),
                (-0.192f, 0.058f),
                (-0.268f, -0.076f),
                (-0.090f, 0.170f)),
            HomeDonorHotspots(
                (-0.2899f, 0.1583f),
                (-0.308f, -0.055f),
                (-0.294f, -0.082f),
                (-0.181f, -0.184f),
                (-0.155f, -0.212f),
                (-0.145f, -0.236f),
                (-0.265f, -0.108f),
                (-0.290f, 0.030f),
                (-0.335f, 0.084f),
                (-0.304f, 0.058f),
                (-0.109f, 0.190f)),
            HomeDonorHotspots(
                (-0.2269f, 0.1555f),
                (-0.1123f, 0.1308f),
                (-0.2862f, 0.0174f),
                (-0.1113f, -0.1535f)),
            HomeDonorHotspots(
                (-0.104f, 0.1342f),
                (-0.2473f, -0.0466f),
                (-0.0912f, -0.1504f)),
            HomeDonorHotspots(
                (-0.306f, -0.0165f),
                (-0.218f, -0.0164f),
                (-0.133f, -0.0221f),
                (-0.046f, -0.0204f),
                (-0.3087f, -0.197f),
                (-0.2191f, -0.197f),
                (-0.1329f, -0.197f),
                (-0.051f, -0.197f)),
        };

        private static readonly PageHotspot[][] FleetariHotspots =
        {
            VerticalCheckboxes(
                0.772f,
                0.871f, 0.795f, 0.718f, 0.640f, 0.562f,
                0.485f, 0.408f, 0.331f, 0.254f, 0.176f),
            new[]
            {
                new PageHotspot(0.772f, 0.730f, 0, 4.286f),
                new PageHotspot(0.772f, 0.655f, 0, 3.700f),
                new PageHotspot(0.772f, 0.582f, 0, 3.900f),
                new PageHotspot(0.772f, 0.510f, 0, 4.110f),
                new PageHotspot(0.772f, 0.438f, 0, 4.415f),
                new PageHotspot(0.772f, 0.368f, 0, 4.625f),
            },
            VerticalCheckboxes(
                0.772f,
                0.790f, 0.722f, 0.652f, 0.584f, 0.516f,
                0.448f, 0.380f, 0.312f, 0.244f, 0.176f),
            VerticalCheckboxes(0.772f, 0.838f, 0.742f, 0.338f, 0.252f),
            VerticalCheckboxes(0.772f, 0.814f, 0.733f, 0.649f),
            VerticalCheckboxes(0.772f, 0.866f, 0.787f, 0.713f, 0.637f),
        };

        private LegacyServiceCatalogKind kind;
        private IServiceCatalogBookSelection selection;
        private Camera playerCamera;
        private IGameplayInputGate inputGate;
        private GameObject closedVisual;
        private HomePartsCatalogPhysicalBinding homePhysicalBinding;
        private Material[] contentMaterials = Array.Empty<Material>();
        private Material orderMaterial;
        private Material fallbackPaper;
        private Material markerMaterial;
        private Material homeSelectionMarkerMaterial;
        private Font homeOrderFont;
        private Transform bookRoot;
        private Transform coverHinge;
        private Transform turningPageHinge;
        private Renderer productPageRenderer;
        private Renderer secondaryProductPageRenderer;
        private Renderer orderPageRenderer;
        private Renderer turningPageRenderer;
        private Renderer baseCoverRenderer;
        private Renderer movingCoverRenderer;
        private Collider productPageCollider;
        private Collider secondaryProductPageCollider;
        private Collider orderPageCollider;
        private GameObject pageSurfaces;
        private GameObject turningPage;
        private TextMesh orderText;
        private readonly List<TextMesh> homeOrderRows = new();
        private readonly List<GameObject> selectionMarkers = new();
        private readonly List<IUiVisibilityGate> uiGates = new();
        private readonly List<bool> uiGateStates = new();
        private BookState state;
        private float animationProgress;
        private int pageIndex;
        private int pendingPageIndex;
        private int pageTurnDirection;
        private bool pageTurning;
        private float pageTurnProgress;
        private bool inputWasEnabled;
        private Vector3 savedCameraPosition;
        private Quaternion savedCameraRotation;
        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible;
        private bool configured;
        private bool presentationBound;
        private float nextPresentationAttemptTime;
        private LegacyServiceCatalogPresentationProvider boundProvider;

        public bool IsOpen => state != BookState.Closed;
        public int PageIndex => pageIndex;
        public int PageCount => kind == LegacyServiceCatalogKind.HomeParts
            ? HomeSpreadCount
            : FleetariProductPageCount + 1;

        public void Configure(
            LegacyServiceCatalogKind configuredKind,
            IServiceCatalogBookSelection configuredSelection,
            Camera configuredCamera,
            IGameplayInputGate configuredInputGate,
            GameObject configuredClosedVisual,
            IEnumerable<Material> configuredContentMaterials,
            Material configuredOrderMaterial,
            HomePartsCatalogPhysicalBinding configuredPhysicalBinding = null)
        {
            kind = configuredKind;
            selection = configuredSelection ??
                throw new ArgumentNullException(nameof(configuredSelection));
            playerCamera = configuredCamera;
            inputGate = configuredInputGate;
            closedVisual = configuredClosedVisual;
            homePhysicalBinding = configuredPhysicalBinding;
            contentMaterials = (configuredContentMaterials ??
                    Enumerable.Empty<Material>())
                .Where(material => material != null)
                .ToArray();
            orderMaterial = configuredOrderMaterial;
            configured = true;
            BuildBook();
            ApplyPageImmediately();
            TryRefreshPresentation(true);
        }

        public bool TryOpen()
        {
            TryRefreshPresentation(true);
            if (!configured || selection.EntryCount == 0 ||
                playerCamera == null || inputGate == null ||
                state != BookState.Closed)
            {
                return false;
            }

            if (kind == LegacyServiceCatalogKind.HomeParts &&
                (homePhysicalBinding == null ||
                 !homePhysicalBinding.TryBeginReading()))
            {
                return false;
            }

            savedCameraPosition = playerCamera.transform.position;
            savedCameraRotation = playerCamera.transform.rotation;
            savedCursorLock = Cursor.lockState;
            savedCursorVisible = Cursor.visible;
            inputWasEnabled = inputGate.IsGameplayInputEnabled;
            homePhysicalBinding?.SynchronizePose();
            inputGate.SetGameplayInputEnabled(false);
            SetUiSuppressed(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            closedVisual?.SetActive(false);
            homePhysicalBinding?.SetCatalogVisible(false);
            bookRoot.gameObject.SetActive(true);
            pageSurfaces.SetActive(false);
            turningPage.SetActive(false);
            coverHinge.localRotation = Quaternion.identity;
            animationProgress = 0f;
            state = BookState.Opening;
            PinCamera();
            return true;
        }

        private void Update()
        {
            TryRefreshPresentation(false);
            if (state == BookState.Closed)
            {
                return;
            }

            float delta = Time.unscaledDeltaTime;
            if (state == BookState.Opening || state == BookState.Closing)
            {
                UpdateBookAnimation(delta);
                return;
            }

            if (pageTurning)
            {
                UpdatePageTurn(delta);
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                Mouse.current?.rightButton.wasPressedThisFrame == true)
            {
                BeginClose();
                return;
            }

            float scroll = Mouse.current?.scroll.ReadValue().y ?? 0f;
            if (!pageTurning &&
                (scroll > 0.01f ||
                 Keyboard.current?.aKey.wasPressedThisFrame == true ||
                 Keyboard.current?.leftArrowKey.wasPressedThisFrame == true))
            {
                BeginPageTurn(-1);
            }
            else if (!pageTurning &&
                     (scroll < -0.01f ||
                      Keyboard.current?.dKey.wasPressedThisFrame == true ||
                      Keyboard.current?.rightArrowKey.wasPressedThisFrame == true))
            {
                BeginPageTurn(1);
            }

            if (!pageTurning &&
                Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                TryHandleClick(Mouse.current.position.ReadValue());
            }

            if (!pageTurning && IsHomeOrderSpread &&
                Keyboard.current?.fKey.wasPressedThisFrame == true &&
                selection.SelectedCount > 0 && selection.TryConfirm())
            {
                BeginClose();
            }
        }

        private void LateUpdate()
        {
            if (state != BookState.Closed)
            {
                PinCamera();
            }
        }

        private void UpdateBookAnimation(float delta)
        {
            float direction = state == BookState.Opening ? 1f : -1f;
            animationProgress = Mathf.Clamp01(
                animationProgress + direction * delta / BookAnimationSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, animationProgress);
            coverHinge.localRotation = Quaternion.Euler(0f, 0f, 180f * eased);
            pageSurfaces.SetActive(animationProgress > 0.42f);

            if (state == BookState.Opening && animationProgress >= 1f)
            {
                state = BookState.Open;
                RefreshSelectionPresentation();
            }
            else if (state == BookState.Closing && animationProgress <= 0f)
            {
                CompleteClose();
            }
        }

        private void BeginClose()
        {
            if (state == BookState.Closed || state == BookState.Closing)
            {
                return;
            }

            pageTurning = false;
            turningPage.SetActive(false);
            animationProgress = 1f;
            state = BookState.Closing;
        }

        private void CompleteClose()
        {
            state = BookState.Closed;
            bookRoot.gameObject.SetActive(false);
            closedVisual?.SetActive(true);
            homePhysicalBinding?.SetCatalogVisible(true);
            homePhysicalBinding?.EndReading();
            if (playerCamera != null)
            {
                playerCamera.transform.SetPositionAndRotation(
                    savedCameraPosition,
                    savedCameraRotation);
            }

            if (inputGate != null)
            {
                inputGate.SetGameplayInputEnabled(inputWasEnabled);
            }

            SetUiSuppressed(false);
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }

        private void BeginPageTurn(int direction)
        {
            int target = pageIndex + direction;
            if (target < 0 || target >= PageCount)
            {
                return;
            }

            pageTurnDirection = direction;
            pendingPageIndex = target;
            pageTurnProgress = 0f;
            pageTurning = true;
            turningPage.SetActive(true);
            turningPageRenderer.sharedMaterial = MaterialForTurn(pageIndex);
            float width = PageWidth;
            turningPageHinge.localPosition = kind ==
                                             LegacyServiceCatalogKind.HomeParts
                ? Vector3.zero
                : new Vector3(-width * 0.5f, 0.012f, 0f);
            Transform sheet = turningPageRenderer.transform;
            bool homeForwardTurn =
                kind == LegacyServiceCatalogKind.HomeParts && direction > 0;
            bool sheetStartsOnRight = kind ==
                                      LegacyServiceCatalogKind.HomeParts
                ? homeForwardTurn
                : direction < 0;
            sheet.localPosition = sheetStartsOnRight
                ? new Vector3(width * 0.5f, 0f, 0f)
                : new Vector3(-width * 0.5f, 0f, 0f);
            turningPageHinge.localRotation = Quaternion.identity;
        }

        private void UpdatePageTurn(float delta)
        {
            pageTurnProgress = Mathf.Clamp01(
                pageTurnProgress + delta / PageTurnSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, pageTurnProgress);
            float turnAngle = kind == LegacyServiceCatalogKind.HomeParts
                ? (pageTurnDirection > 0 ? 180f : -180f)
                : (pageTurnDirection > 0 ? -180f : 180f);
            turningPageHinge.localRotation = Quaternion.Euler(
                0f,
                0f,
                turnAngle * eased);
            if (pageTurnProgress >= 0.5f && pageIndex != pendingPageIndex)
            {
                pageIndex = pendingPageIndex;
                ApplyPageImmediately();
            }

            if (pageTurnProgress >= 1f)
            {
                pageTurning = false;
                turningPage.SetActive(false);
                RefreshSelectionPresentation();
            }
        }

        private void TryHandleClick(Vector2 screenPosition)
        {
            Ray ray = playerCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 2f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (kind == LegacyServiceCatalogKind.HomeParts &&
                IsHomeOrderSpread && hit.collider == orderPageCollider)
            {
                Vector2 uv = OrientedHitUv(
                    hit,
                    orderPageCollider.transform);
                if (uv.x >= 0.58f && uv.y <= 0.24f)
                {
                    selection.TryConfirm();
                }

                return;
            }

            int productPage = ProductPageForCollider(hit.collider);
            if (productPage < 0)
            {
                return;
            }

            Vector2 pageUv = OrientedHitUv(hit, hit.collider.transform);
            if (kind == LegacyServiceCatalogKind.FleetariServices &&
                pageIndex == FleetariProductPageCount)
            {
                if (pageUv.x >= 0.60f && pageUv.y <= 0.24f)
                {
                    selection.TryConfirm();
                    RefreshSelectionPresentation();
                }

                return;
            }

            PageHotspot[] hotspots = HotspotsForProductPage(productPage);
            if (hotspots.Length == 0)
            {
                return;
            }

            int best = -1;
            float bestDistance = kind == LegacyServiceCatalogKind.HomeParts &&
                                 hotspots.Length > 8
                ? 0.09f * 0.09f
                : 0.12f * 0.12f;
            for (int index = 0; index < hotspots.Length; index++)
            {
                float distance = (hotspots[index].NormalizedPosition - pageUv)
                    .sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = index;
                    bestDistance = distance;
                }
            }

            if (best < 0)
            {
                return;
            }

            PageHotspot hotspot = hotspots[best];
            int entryIndex = EntryStartForProductPage(productPage) +
                             hotspot.EntryOffset;
            selection.TryActivateEntry(entryIndex, hotspot.Variant);
            RefreshSelectionPresentation();
        }

        private void ApplyPageImmediately()
        {
            if (productPageRenderer == null)
            {
                return;
            }

            if (kind == LegacyServiceCatalogKind.HomeParts)
            {
                bool orderSpread = IsHomeOrderSpread;
                int firstProductPage = pageIndex * 2;
                productPageRenderer.gameObject.SetActive(!orderSpread);
                secondaryProductPageRenderer.gameObject.SetActive(!orderSpread);
                orderPageRenderer.gameObject.SetActive(orderSpread);
                productPageRenderer.sharedMaterial = orderSpread
                    ? fallbackPaper
                    : MaterialForProductPage(firstProductPage);
                secondaryProductPageRenderer.sharedMaterial = orderSpread
                    ? fallbackPaper
                    : MaterialForProductPage(firstProductPage + 1);
                orderPageRenderer.sharedMaterial = orderMaterial ??
                                                   fallbackPaper;
            }
            else
            {
                productPageRenderer.sharedMaterial = MaterialForPage(pageIndex);
            }

            if (orderPageRenderer != null)
            {
                orderPageRenderer.gameObject.SetActive(
                    kind == LegacyServiceCatalogKind.HomeParts &&
                    IsHomeOrderSpread);
            }

            RefreshSelectionPresentation();
        }

        private void RefreshSelectionPresentation()
        {
            if (orderText != null)
            {
                orderText.gameObject.SetActive(
                    kind == LegacyServiceCatalogKind.FleetariServices &&
                    pageIndex == FleetariProductPageCount);
            }

            bool showHomeOrder = kind ==
                                 LegacyServiceCatalogKind.HomeParts &&
                                 IsHomeOrderSpread;
            for (int index = 0; index < homeOrderRows.Count; index++)
            {
                TextMesh row = homeOrderRows[index];
                row.gameObject.SetActive(showHomeOrder);
                row.text = showHomeOrder &&
                           index < selection.EntryCount &&
                           selection.IsEntrySelected(index)
                    ? selection.GetEntry(index).DisplayName
                    : string.Empty;
            }

            for (int index = 0; index < selectionMarkers.Count; index++)
            {
                DestroyObject(selectionMarkers[index]);
            }

            selectionMarkers.Clear();
            if (kind == LegacyServiceCatalogKind.HomeParts &&
                !IsHomeOrderSpread)
            {
                CreateSelectionMarkersForProductPage(
                    pageIndex * 2,
                    productPageRenderer);
                CreateSelectionMarkersForProductPage(
                    pageIndex * 2 + 1,
                    secondaryProductPageRenderer);
            }
            else if (kind == LegacyServiceCatalogKind.FleetariServices &&
                     pageIndex < ProductPageCount)
            {
                PageHotspot[] hotspots = CurrentHotspots;
                var markedEntries = new HashSet<int>();
                for (int index = 0; index < hotspots.Length; index++)
                {
                    int entryIndex = PageEntryStart + hotspots[index].EntryOffset;
                    if (!markedEntries.Add(entryIndex) ||
                        !selection.IsEntrySelected(entryIndex))
                    {
                        continue;
                    }

                    CreateSelectionMarker(hotspots[index].NormalizedPosition);
                }
            }

            if (orderText != null &&
                kind == LegacyServiceCatalogKind.FleetariServices)
            {
                orderText.text = BuildOrderText();
            }
        }

        private void CreateSelectionMarkersForProductPage(
            int productPage,
            Renderer pageRenderer)
        {
            PageHotspot[] hotspots = HotspotsForProductPage(productPage);
            var markedEntries = new HashSet<int>();
            int entryStart = EntryStartForProductPage(productPage);
            for (int index = 0; index < hotspots.Length; index++)
            {
                int entryIndex = entryStart + hotspots[index].EntryOffset;
                if (!markedEntries.Add(entryIndex) ||
                    !selection.IsEntrySelected(entryIndex))
                {
                    continue;
                }

                CreateSelectionMarker(
                    hotspots[index].NormalizedPosition,
                    pageRenderer);
            }
        }

        private string BuildOrderText()
        {
            var lines = new List<string>();
            for (int index = 0; index < selection.EntryCount; index++)
            {
                if (!selection.IsEntrySelected(index))
                {
                    continue;
                }

                ServiceCatalogBookEntry entry = selection.GetEntry(index);
                string name = entry.DisplayName.Length > 25
                    ? entry.DisplayName.Substring(0, 25)
                    : entry.DisplayName;
                lines.Add($"{name}  {entry.PriceMinorUnits / 100L} MK");
                if (lines.Count == 11)
                {
                    break;
                }
            }

            if (selection.SelectedCount > lines.Count)
            {
                lines.Add($"…ещё {selection.SelectedCount - lines.Count}");
            }

            return string.Join("\n", lines);
        }

        private void CreateSelectionMarker(
            Vector2 uv,
            Renderer targetRenderer = null)
        {
            targetRenderer ??= productPageRenderer;
            bool useDonorSelection =
                kind == LegacyServiceCatalogKind.HomeParts &&
                homeSelectionMarkerMaterial != null;
            var marker = GameObject.CreatePrimitive(
                useDonorSelection ? PrimitiveType.Quad : PrimitiveType.Cube);
            marker.name = "Catalog selected marker";
            marker.transform.SetParent(targetRenderer.transform, false);
            marker.transform.localPosition = new Vector3(
                (uv.x - 0.5f),
                0.58f,
                (uv.y - 0.5f));
            marker.transform.localScale = useDonorSelection
                ? new Vector3(0.1f, 0.1f, 0.1f)
                : new Vector3(0.075f, 0.075f, 0.012f);
            if (useDonorSelection)
            {
                marker.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }

            marker.GetComponent<Renderer>().sharedMaterial = useDonorSelection
                ? homeSelectionMarkerMaterial
                : markerMaterial;
            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyObject(collider);
            }

            selectionMarkers.Add(marker);
        }

        private void BuildBook()
        {
            CreateRuntimeMaterials();
            var root = new GameObject("Physical catalog reading view");
            bookRoot = root.transform;
            bookRoot.SetParent(transform, false);
            bookRoot.localPosition = new Vector3(0f, 0.008f, 0f);
            bookRoot.localRotation = kind == LegacyServiceCatalogKind.HomeParts
                ? Quaternion.identity
                : new Quaternion(0f, 0.89758277f, 0f, -0.44084606f);

            float width = PageWidth;
            float height = PageHeight;
            Material cover = kind == LegacyServiceCatalogKind.HomeParts
                ? LegacyServiceCatalogPresentationProvider.Current?.HomePartsCover
                : LegacyServiceCatalogPresentationProvider.Current
                    ?.FleetariServicesCover;
            cover ??= fallbackPaper;

            GameObject baseCover = CreateSurface(
                "Catalog back cover",
                bookRoot,
                kind == LegacyServiceCatalogKind.HomeParts
                    ? new Vector3(0f, 0f, 0f)
                    : Vector3.zero,
                kind == LegacyServiceCatalogKind.HomeParts ? width * 2f : width,
                height,
                cover,
                createCollider: false);
            baseCoverRenderer = baseCover.GetComponent<Renderer>();
            baseCover.transform.localPosition += Vector3.down * 0.003f;

            var hingeObject = new GameObject("Catalog cover hinge");
            coverHinge = hingeObject.transform;
            coverHinge.SetParent(bookRoot, false);
            GameObject movingCover = CreateSurface(
                "Catalog moving cover",
                coverHinge,
                new Vector3(width * 0.5f, 0.004f, 0f),
                width,
                height,
                cover,
                createCollider: false);
            movingCoverRenderer = movingCover.GetComponent<Renderer>();
            movingCover.transform.localScale += new Vector3(0f, 0.0004f, 0f);

            var pagesObject = new GameObject("Catalog page surfaces");
            pageSurfaces = pagesObject;
            pagesObject.transform.SetParent(bookRoot, false);
            Vector3 productPosition = kind == LegacyServiceCatalogKind.HomeParts
                ? new Vector3(-width * 0.5f, 0.006f, 0f)
                : new Vector3(0f, 0.006f, 0f);
            GameObject productPage = CreateSurface(
                "Catalog product page",
                pagesObject.transform,
                productPosition,
                width,
                height,
                fallbackPaper,
                createCollider: true);
            OrientReadingSurface(productPage.transform);
            productPageRenderer = productPage.GetComponent<Renderer>();
            productPageCollider = productPage.GetComponent<Collider>();

            if (kind == LegacyServiceCatalogKind.HomeParts)
            {
                GameObject secondaryProductPage = CreateSurface(
                    "Catalog secondary product page",
                    pagesObject.transform,
                    new Vector3(width * 0.5f, 0.006f, 0f),
                    width,
                    height,
                    fallbackPaper,
                    createCollider: true);
                OrientReadingSurface(secondaryProductPage.transform);
                secondaryProductPageRenderer = secondaryProductPage
                    .GetComponent<Renderer>();
                secondaryProductPageCollider = secondaryProductPage
                    .GetComponent<Collider>();

                GameObject orderPage = CreateSurface(
                    "Catalog order form",
                    pagesObject.transform,
                    new Vector3(width * 0.5f, 0.006f, 0f),
                    width,
                    height,
                    orderMaterial ?? fallbackPaper,
                    createCollider: true);
                OrientReadingSurface(orderPage.transform);
                orderPageRenderer = orderPage.GetComponent<Renderer>();
                orderPageCollider = orderPage.GetComponent<Collider>();
            }

            var turnHingeObject = new GameObject("Catalog turning page hinge");
            turningPageHinge = turnHingeObject.transform;
            turningPageHinge.SetParent(bookRoot, false);
            turningPageHinge.localPosition = new Vector3(0f, 0.012f, 0f);
            turningPage = CreateSurface(
                "Catalog turning page",
                turningPageHinge,
                new Vector3(-width * 0.5f, 0f, 0f),
                width,
                height,
                fallbackPaper,
                createCollider: false);
            OrientReadingSurface(turningPage.transform);
            turningPageRenderer = turningPage.GetComponent<Renderer>();

            if (kind == LegacyServiceCatalogKind.HomeParts)
            {
                CreateHomeOrderRows(pagesObject.transform, width);
            }
            else
            {
                CreateOrderText(pagesObject.transform, width, height);
            }
            pagesObject.SetActive(false);
            turningPage.SetActive(false);
            root.SetActive(false);
        }

        private static void OrientReadingSurface(Transform surface)
        {
            surface.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private bool TryRefreshPresentation(bool force)
        {
            if (!configured)
            {
                return false;
            }

            if (!force && Time.unscaledTime < nextPresentationAttemptTime)
            {
                return presentationBound;
            }

            nextPresentationAttemptTime =
                Time.unscaledTime + PresentationRetrySeconds;
            LegacyServiceCatalogPresentationProvider provider =
                LegacyServiceCatalogPresentationProvider.Current;
            if (provider == null)
            {
                presentationBound = false;
                return false;
            }

            if (boundProvider == provider && presentationBound)
            {
                return true;
            }

            Material cover = kind == LegacyServiceCatalogKind.HomeParts
                ? provider.HomePartsCover
                : provider.FleetariServicesCover;
            if (cover != null)
            {
                if (baseCoverRenderer != null)
                {
                    baseCoverRenderer.sharedMaterial = cover;
                }

                if (movingCoverRenderer != null)
                {
                    movingCoverRenderer.sharedMaterial = cover;
                }
            }

            if (closedVisual == null && provider.TryCreate(
                    kind,
                    transform,
                    out GameObject createdVisual))
            {
                closedVisual = createdVisual;
                if (kind == LegacyServiceCatalogKind.HomeParts)
                {
                    homePhysicalBinding?.SetFallbackVisual(createdVisual);
                }
                else
                {
                    createdVisual.SetActive(state == BookState.Closed);
                }
            }

            IReadOnlyList<Material> providerPages = kind ==
                                                     LegacyServiceCatalogKind.HomeParts
                ? provider.HomePartsPages
                : provider.FleetariServicePages;
            int expectedCount = kind == LegacyServiceCatalogKind.HomeParts
                ? HomeProductPageCount
                : FleetariProductPageCount;
            Material providerOrder = kind == LegacyServiceCatalogKind.HomeParts
                ? provider.HomePartsOrderPage
                : provider.FleetariOrderPage;
            if (!TryCopyMaterials(
                    providerPages,
                    expectedCount,
                    out Material[] refreshedPages) ||
                providerOrder == null)
            {
                boundProvider = provider;
                presentationBound = false;
                return false;
            }

            contentMaterials = refreshedPages;
            orderMaterial = providerOrder;
            if (kind == LegacyServiceCatalogKind.HomeParts)
            {
                homeSelectionMarkerMaterial = provider.HomePartsSelectionMark;
                homeOrderFont = provider.HomePartsOrderFont;
                ApplyHomeOrderFont();
            }

            boundProvider = provider;
            presentationBound = true;
            ApplyPageImmediately();
            return true;
        }

        private static bool TryCopyMaterials(
            IReadOnlyList<Material> source,
            int expectedCount,
            out Material[] result)
        {
            if (source == null || source.Count != expectedCount)
            {
                result = Array.Empty<Material>();
                return false;
            }

            result = new Material[expectedCount];
            for (int index = 0; index < expectedCount; index++)
            {
                Material material = source[index];
                if (material == null)
                {
                    result = Array.Empty<Material>();
                    return false;
                }

                result[index] = material;
            }

            return true;
        }

        private static GameObject CreateSurface(
            string objectName,
            Transform parent,
            Vector3 localPosition,
            float width,
            float height,
            Material material,
            bool createCollider)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = objectName;
            surface.transform.SetParent(parent, false);
            surface.transform.localPosition = localPosition;
            surface.transform.localScale = new Vector3(width, 0.002f, height);
            surface.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = surface.GetComponent<Collider>();
            if (!createCollider && collider != null)
            {
                DestroyObject(collider);
            }

            return surface;
        }

        private void CreateOrderText(Transform parent, float width, float height)
        {
            var textObject = new GameObject("Catalog handwritten order text");
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = kind ==
                                                 LegacyServiceCatalogKind.HomeParts
                ? new Vector3(width * 0.08f, 0.009f, height * 0.28f)
                : new Vector3(-width * 0.42f, 0.009f, height * 0.30f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            orderText = textObject.AddComponent<TextMesh>();
            orderText.anchor = TextAnchor.UpperLeft;
            orderText.alignment = TextAlignment.Left;
            orderText.characterSize = kind == LegacyServiceCatalogKind.HomeParts
                ? 0.0085f
                : 0.010f;
            orderText.fontSize = 40;
            orderText.color = new Color(0.06f, 0.10f, 0.32f, 1f);
            orderText.richText = false;
            orderText.gameObject.SetActive(
                pageIndex == FleetariProductPageCount);
        }

        private void CreateHomeOrderRows(Transform parent, float width)
        {
            int rowCount = Mathf.Min(
                selection.EntryCount,
                HomeOrderRowPositions.Length);
            for (int index = 0; index < rowCount; index++)
            {
                Vector2 donor = HomeOrderRowPositions[index];
                var rowObject = new GameObject(
                    $"Catalog order row {index + 1:00}");
                rowObject.transform.SetParent(parent, false);
                rowObject.transform.localPosition = new Vector3(
                    width * 0.5f +
                    (donor.x - 0.127f) * (width / 0.4f),
                    0.009f,
                    (donor.y + 0.24439735f) *
                    (HomePageHeight / 0.6f));
                rowObject.transform.localRotation =
                    Quaternion.Euler(90f, 0f, 0f);
                TextMesh row = rowObject.AddComponent<TextMesh>();
                row.anchor = TextAnchor.UpperLeft;
                row.alignment = TextAlignment.Left;
                row.characterSize = 0.0025f;
                row.fontSize = 0;
                row.color = new Color32(57, 64, 208, 255);
                row.richText = false;
                homeOrderRows.Add(row);
            }

            ApplyHomeOrderFont();
        }

        private void ApplyHomeOrderFont()
        {
            if (homeOrderFont == null)
            {
                return;
            }

            for (int index = 0; index < homeOrderRows.Count; index++)
            {
                TextMesh row = homeOrderRows[index];
                row.font = homeOrderFont;
                MeshRenderer renderer = row.GetComponent<MeshRenderer>();
                if (renderer != null && homeOrderFont.material != null)
                {
                    renderer.sharedMaterial = homeOrderFont.material;
                }
            }
        }

        private void CreateRuntimeMaterials()
        {
            Shader shader = Shader.Find("HDRP/Lit") ??
                            Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Standard");
            fallbackPaper = new Material(shader)
            {
                name = "Runtime catalog fallback paper",
            };
            markerMaterial = new Material(shader)
            {
                name = "Runtime catalog selection marker",
            };
            SetMaterialColor(fallbackPaper, new Color(0.79f, 0.76f, 0.66f));
            SetMaterialColor(markerMaterial, new Color(0.08f, 0.62f, 0.24f));
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }

        private Material MaterialForPage(int index)
        {
            if (index >= 0 && index < contentMaterials.Length)
            {
                return contentMaterials[index];
            }

            if (kind == LegacyServiceCatalogKind.FleetariServices &&
                index == FleetariProductPageCount)
            {
                return orderMaterial ?? fallbackPaper;
            }

            return fallbackPaper;
        }

        private Material MaterialForProductPage(int productPage)
        {
            return productPage >= 0 && productPage < contentMaterials.Length
                ? contentMaterials[productPage]
                : fallbackPaper;
        }

        private Material MaterialForTurn(int spread)
        {
            if (kind != LegacyServiceCatalogKind.HomeParts)
            {
                return MaterialForPage(spread);
            }

            if (spread >= HomeProductPageCount / 2)
            {
                return orderMaterial ?? fallbackPaper;
            }

            return MaterialForProductPage(spread * 2 + 1);
        }

        private int ProductPageCount =>
            kind == LegacyServiceCatalogKind.HomeParts
                ? HomeProductPageCount
                : FleetariProductPageCount;

        private PageHotspot[] CurrentHotspots
        {
            get
            {
                PageHotspot[][] source = kind ==
                                         LegacyServiceCatalogKind.HomeParts
                    ? HomeHotspots
                    : FleetariHotspots;
                return pageIndex >= 0 && pageIndex < source.Length
                    ? source[pageIndex]
                    : Array.Empty<PageHotspot>();
            }
        }

        private bool IsHomeOrderSpread =>
            kind == LegacyServiceCatalogKind.HomeParts &&
            pageIndex == HomeSpreadCount - 1;

        private int ProductPageForCollider(Collider collider)
        {
            if (kind != LegacyServiceCatalogKind.HomeParts)
            {
                return collider == productPageCollider ? pageIndex : -1;
            }

            if (IsHomeOrderSpread)
            {
                return -1;
            }

            if (collider == productPageCollider)
            {
                return pageIndex * 2;
            }

            return collider == secondaryProductPageCollider
                ? pageIndex * 2 + 1
                : -1;
        }

        private static PageHotspot[] HotspotsForProductPage(int productPage)
        {
            return productPage >= 0 && productPage < HomeHotspots.Length
                ? HomeHotspots[productPage]
                : Array.Empty<PageHotspot>();
        }

        private static int EntryStartForProductPage(int productPage)
        {
            int result = 0;
            for (int index = 0;
                 index < productPage && index < HomePageEntryCounts.Length;
                 index++)
            {
                result += HomePageEntryCounts[index];
            }

            return result;
        }

        private int PageEntryStart
        {
            get
            {
                int[] counts = kind == LegacyServiceCatalogKind.HomeParts
                    ? HomePageEntryCounts
                    : new[] { 10, 1, 10, 4, 3, 4 };
                int result = 0;
                for (int index = 0; index < pageIndex && index < counts.Length;
                     index++)
                {
                    result += counts[index];
                }

                return result;
            }
        }

        private float PageWidth => kind == LegacyServiceCatalogKind.HomeParts
            ? HomePageWidth
            : FleetariPageWidth;

        private float PageHeight => kind == LegacyServiceCatalogKind.HomeParts
            ? HomePageHeight
            : FleetariPageHeight;

        private void PinCamera()
        {
            Vector3 center = bookRoot.position;
            float spreadWidth = kind == LegacyServiceCatalogKind.HomeParts
                ? HomePageWidth * 2f
                : FleetariPageWidth;
            float verticalFovRadians = Mathf.Clamp(
                playerCamera.fieldOfView,
                1f,
                179f) * Mathf.Deg2Rad;
            float aspect = Mathf.Max(0.1f, playerCamera.aspect);
            float horizontalFovRadians = 2f * Mathf.Atan(
                Mathf.Tan(verticalFovRadians * 0.5f) * aspect);
            const float viewportFill = 0.78f;
            float heightDistance = PageHeight * 0.5f /
                                   (Mathf.Tan(verticalFovRadians * 0.5f) *
                                    viewportFill);
            float widthDistance = spreadWidth * 0.5f /
                                  (Mathf.Tan(horizontalFovRadians * 0.5f) *
                                   viewportFill);
            float distance = Mathf.Max(
                playerCamera.nearClipPlane + 0.04f,
                Mathf.Max(heightDistance, widthDistance));
            Vector3 position = center + Vector3.up * distance;
            Quaternion rotation = Quaternion.LookRotation(
                Vector3.down,
                bookRoot.forward);
            playerCamera.transform.SetPositionAndRotation(position, rotation);
        }

        private void SetUiSuppressed(bool suppressed)
        {
            if (suppressed)
            {
                uiGates.Clear();
                uiGateStates.Clear();
                if (playerCamera != null)
                {
                    MonoBehaviour[] behaviours = playerCamera.transform.root
                        .GetComponentsInChildren<MonoBehaviour>(true);
                    foreach (MonoBehaviour behaviour in behaviours)
                    {
                        if (behaviour is not IUiVisibilityGate gate)
                        {
                            continue;
                        }

                        uiGates.Add(gate);
                        uiGateStates.Add(gate.IsUiSuppressed);
                        gate.SetUiSuppressed(true);
                    }
                }

                return;
            }

            for (int index = 0; index < uiGates.Count; index++)
            {
                uiGates[index]?.SetUiSuppressed(uiGateStates[index]);
            }

            uiGates.Clear();
            uiGateStates.Clear();
        }

        private static Vector2 HitUv(RaycastHit hit, Transform surface)
        {
            Vector3 local = surface.InverseTransformPoint(hit.point);
            return new Vector2(local.x + 0.5f, local.z + 0.5f);
        }

        private static Vector2 OrientedHitUv(
            RaycastHit hit,
            Transform surface)
        {
            // Reading surfaces are rotated together with their collider. Local
            // coordinates therefore remain material-space coordinates and must
            // not be inverted a second time.
            return HitUv(hit, surface);
        }

        private static PageHotspot[] HomeDonorHotspots(
            params (float X, float Y)[] donorCoordinates)
        {
            var result = new PageHotspot[donorCoordinates.Length];
            for (int index = 0; index < result.Length; index++)
            {
                float u = (donorCoordinates[index].X + 0.4f) / 0.4f;
                float v = (donorCoordinates[index].Y + 0.3f) / 0.6f;
                result[index] = new PageHotspot(u, v, index);
            }

            return result;
        }

        private static PageHotspot[] VerticalCheckboxes(
            float x,
            params float[] y)
        {
            var result = new PageHotspot[y.Length];
            for (int index = 0; index < y.Length; index++)
            {
                result[index] = new PageHotspot(x, y[index], index);
            }

            return result;
        }

        private void OnDisable()
        {
            if (state != BookState.Closed)
            {
                animationProgress = 0f;
                CompleteClose();
            }
        }

        private void OnDestroy()
        {
            if (fallbackPaper != null)
            {
                DestroyObject(fallbackPaper);
            }

            if (markerMaterial != null)
            {
                DestroyObject(markerMaterial);
            }
        }

        private static void DestroyObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
