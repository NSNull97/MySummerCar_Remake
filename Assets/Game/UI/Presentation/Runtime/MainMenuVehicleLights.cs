using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.UI.Presentation
{
    /// <summary>Menu-only lamps; no battery, vehicle switches or gameplay state.</summary>
    internal sealed class MainMenuVehicleLights
    {
        private const float HeadlightCandela = 60000f;
        private const float RearLampCandela = 1500f;
        private readonly MainMenuVehicleModel model;
        private readonly Light[] lights;
        private readonly MaterialPropertyBlock emission = new MaterialPropertyBlock();
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int AlbedoAffectEmissiveId = Shader.PropertyToID("_AlbedoAffectEmissive");

        public IReadOnlyList<Light> Lights => lights;
        public float NightFactor { get; private set; }

        public MainMenuVehicleLights(MainMenuVehicleModel model, Transform lightParent, int layer, uint lightMask)
        {
            this.model = model;
            if (model.LampBindings.Count != 4)
                throw new InvalidOperationException("Rebuild the menu Satsuma preview to bind its four lamps.");
            lights = new Light[model.LampBindings.Count];
            for (int index = 0; index < lights.Length; index++)
            {
                MainMenuVehicleLampBinding binding = model.LampBindings[index];
                var owner = new GameObject("Menu lamp " + binding.LampId) { layer = layer };
                // The static car retains its mesh-only hierarchy; the stage owns lights.
                owner.transform.SetParent(lightParent, false);
                owner.transform.position = model.transform.TransformPoint(binding.LocalPosition);
                owner.transform.rotation = Quaternion.LookRotation(
                    model.transform.TransformDirection(binding.LocalDirection), model.transform.up);
                Light light = owner.AddComponent<Light>();
                lights[index] = light;
                light.type = binding.IsHeadlight ? LightType.Spot : LightType.Point;
                if (binding.IsHeadlight)
                {
                    light.spotAngle = 70f;
                    light.innerSpotAngle = 45f;
                    light.enableSpotReflector = true;
                }
                light.range = binding.IsHeadlight ? 30f : 4f;
                light.cullingMask = 1 << layer;
                light.shadows = LightShadows.Soft;
                HDAdditionalLightData hd = owner.AddComponent<HDAdditionalLightData>();
                light.lightUnit = LightUnit.Candela;
                light.useColorTemperature = false;
                light.color = binding.IsHeadlight ? Color.white : Color.red;
                light.enabled = false;
                light.shapeRadius = binding.IsHeadlight ? 0.06f : 0.025f;
                hd.interactsWithSky = false;
                hd.SetShadowResolutionOverride(true);
                hd.SetShadowResolution(binding.IsHeadlight ? 512 : 256);
                hd.SetLightLayer((UnityEngine.Rendering.HighDefinition.RenderingLayerMask)lightMask,
                    (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)lightMask);
            }
        }

        public void Apply(MainMenuLightingState state)
        {
            // Match dusk/dawn smoothly; daylight is exactly zero emission and no lights.
            NightFactor = Mathf.Clamp01(state.Twilight + state.Night);
            for (int index = 0; index < lights.Length; index++)
            {
                MainMenuVehicleLampBinding binding = model.LampBindings[index];
                lights[index].intensity = (binding.IsHeadlight ? HeadlightCandela : RearLampCandela) * NightFactor;
                lights[index].enabled = NightFactor > 0f;
                emission.Clear();
                binding.LensRenderer.GetPropertyBlock(emission, binding.MaterialSlot);
                Vector4 radiance = binding.IsHeadlight ? new Vector4(8000f, 8000f, 8000f, 1f) :
                    new Vector4(2500f, 0f, 0f, 1f);
                emission.SetVector(EmissiveColorId, radiance * NightFactor);
                emission.SetFloat(AlbedoAffectEmissiveId, 1f);
                binding.LensRenderer.SetPropertyBlock(emission, binding.MaterialSlot);
            }
        }
    }
}
