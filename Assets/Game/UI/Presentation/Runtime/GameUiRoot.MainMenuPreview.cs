using System;
using MSC.UI.Runtime.Routing;
using UnityEngine;

namespace MSC.UI.Presentation
{
    public sealed partial class GameUiRoot
    {
        private MainMenuVehiclePreview menuVehiclePreview;
        private MainMenuOrbitDrag menuOrbitDrag;

        public bool HasLiveMenuVehicle => menuVehiclePreview != null;

        private void InitializeMenuVehiclePreview()
        {
            if (dependencies.MenuVehiclePrefab == null) return;
            if (dependencies.MenuEnvironmentPrefab == null)
                throw new InvalidOperationException("The menu vehicle needs its explicit 3D environment prefab.");

            var previewObject = new GameObject("Main Menu Vehicle Preview");
            previewObject.transform.SetParent(transform, false);
            menuVehiclePreview = previewObject.AddComponent<MainMenuVehiclePreview>();
            menuVehiclePreview.FrameReady += OnMenuVehicleFrameReady;
            menuVehiclePreview.Initialize(dependencies.MenuVehiclePrefab,
                dependencies.MenuEnvironmentPrefab, dependencies.MenuLocalTimeProvider);
            menuVehiclePreview.SetPaint(CarColours[selectedCarColourIndex]);
            menuOrbitDrag = menuBackdropImage.gameObject.AddComponent<MainMenuOrbitDrag>();
            menuOrbitDrag.Initialize(OrbitMenuPreview, ResetMenuPreviewOrbit, CanOrbitMenuPreview);
            RefreshMenuOrbitInteraction();
        }

        private bool CanOrbitMenuPreview() => !sessionEnded && menuVehiclePreview != null &&
            currentRoute == UiRouteId.MainMenu && mainMenuModal == null;

        private void OrbitMenuPreview(Vector2 delta)
        {
            if (CanOrbitMenuPreview()) menuVehiclePreview.Orbit(delta);
        }

        private void ResetMenuPreviewOrbit()
        {
            if (CanOrbitMenuPreview()) menuVehiclePreview.ResetOrbit();
        }

        private void RefreshMenuOrbitInteraction()
        {
            bool available = CanOrbitMenuPreview();
            if (menuOrbitDrag != null) menuOrbitDrag.enabled = available;
            if (menuBackdropImage != null) menuBackdropImage.raycastTarget = available;
        }

        private void BuildMenuOrbitHint(Transform parent)
        {
            if (!HasLiveMenuVehicle) return;
            UnityEngine.UI.Text hint = factory.Text("MenuOrbitHint", parent,
                textCatalog.Get("ui.main.orbit_hint"), 462f, 857f, 532f, 28f,
                13, MainMenuStyle.SecondaryText, TextAnchor.MiddleCenter);
            AnchorMainMenuBlock(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(-108f, 56f));
        }

        private void OnMenuVehicleFrameReady()
        {
            if (menuVehiclePreview == null || menuBackdropImage == null) return;
            Texture source = menuVehiclePreview.OutputTexture;
            menuBackdropImage.texture = source;
            menuBackdropImage.enabled = activeBackdropMode == UiBackdropMode.MenuStatic;
            EnsureMenuBlurredTexture(source.width, source.height);
            ApplyMenuBackdropAspect();
            // The rendered vehicle and home yard feed every panel's existing blur.
            // Refresh only when the preview changes, never from idle UI Update.
            ApplyGaussianBlur(source, menuBlurredTexture);
            RefreshGlassSurfaces();
        }

        private void DisposeMenuVehiclePreview()
        {
            if (menuOrbitDrag != null) menuOrbitDrag.enabled = false;
            if (menuBackdropImage != null) menuBackdropImage.raycastTarget = false;
            if (menuVehiclePreview == null) return;
            menuVehiclePreview.FrameReady -= OnMenuVehicleFrameReady;
            menuVehiclePreview.SetVisible(false);
            Destroy(menuVehiclePreview.gameObject);
            menuVehiclePreview = null;
        }
    }
}
