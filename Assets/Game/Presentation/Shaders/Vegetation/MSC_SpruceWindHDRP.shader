Shader "MSC/HDRP/Spruce Wind"
{
    Properties
    {
        [MainColor]_BaseColor("Base Color", Color) = (0.82, 0.82, 0.78, 1)
        [MainTexture]_BaseColorMap("Base Color Map", 2D) = "white" {}
        [Normal]_NormalMap("Normal Map", 2D) = "bump" {}
        _NormalScale("Normal Scale", Range(0, 2)) = 0.85
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.06
        _SpecularColor("Foliage Specular F0", Color) = (0.018, 0.018, 0.018, 1)
        [HideInInspector]_AlphaRemapMin("Alpha Remap Min", Float) = 0
        [HideInInspector]_AlphaRemapMax("Alpha Remap Max", Float) = 1
        [ToggleUI]_AlphaCutoffEnable("Alpha Cutoff Enable", Float) = 1
        _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.43

        [HideInInspector]_EmissiveColor("Emissive Color", Color) = (0, 0, 0, 0)
        [HideInInspector]_EmissionColor("Legacy GI Emission", Color) = (0, 0, 0, 0)
        [HideInInspector]_EmissiveExposureWeight("Emissive Exposure", Float) = 1
        [HideInInspector]_SurfaceType("Surface Type", Float) = 0
        [HideInInspector]_BlendMode("Blend Mode", Float) = 0
        [HideInInspector]_SrcBlend("Source Blend", Float) = 1
        [HideInInspector]_DstBlend("Destination Blend", Float) = 0
        [HideInInspector]_DstBlend2("Destination Blend 2", Float) = 0
        [HideInInspector]_AlphaSrcBlend("Alpha Source Blend", Float) = 1
        [HideInInspector]_AlphaDstBlend("Alpha Destination Blend", Float) = 0
        [HideInInspector]_ZWrite("Z Write", Float) = 1
        [HideInInspector]_ZTestDepthEqualForOpaque("Z Test", Float) = 4
        [HideInInspector]_CullMode("Cull Mode", Float) = 0
        [HideInInspector]_CullModeForward("Forward Cull Mode", Float) = 0
        [HideInInspector]_ZClip("Z Clip", Float) = 1
        [HideInInspector]_DoubleSidedEnable("Double Sided", Float) = 1
        [HideInInspector]_DoubleSidedConstants("Double Sided Constants", Vector) = (-1, -1, -1, 0)
        [HideInInspector]_StencilRef("Stencil Ref", Float) = 0
        [HideInInspector]_StencilWriteMask("Stencil Write Mask", Float) = 3
        [HideInInspector]_StencilRefDepth("Depth Stencil Ref", Float) = 0
        [HideInInspector]_StencilWriteMaskDepth("Depth Stencil Mask", Float) = 8
        [HideInInspector]_StencilRefMV("Motion Stencil Ref", Float) = 32
        [HideInInspector]_StencilWriteMaskMV("Motion Stencil Mask", Float) = 32
        [HideInInspector]_AddPrecomputedVelocity("Precomputed Velocity", Float) = 0

        _WindStrengthMeters("Full Wind Crown Displacement", Range(0, 1)) = 0.36
        _WindSpeed("Wind Animation Speed", Range(0.1, 4)) = 1.0
        _WindFrequency("Wind Spatial Frequency", Range(0.001, 0.2)) = 0.026
        _WindBaseHeightMeters("Fixed Crown Base Height", Range(0, 4)) = 0.7
        _WindHeightMeters("Crown Response Height", Range(2, 30)) = 13
        _MinimumWindIntensity("Minimum Ambient Sway", Range(0, 0.25)) = 0.035
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
    #pragma shader_feature_local _ALPHATEST_ON
    #pragma shader_feature_local _DOUBLESIDED_ON
    #pragma shader_feature_local _ADD_PRECOMPUTED_VELOCITY

    #if defined(_DOUBLESIDED_ON) && !defined(VARYINGS_NEED_CULLFACE)
        #define VARYINGS_NEED_CULLFACE
    #endif

    #define PREFER_HALF 0
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/FragInputs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/LitProperties.hlsl"

    float _WindStrengthMeters;
    float _WindSpeed;
    float _WindFrequency;
    float _WindBaseHeightMeters;
    float _WindHeightMeters;
    float _MinimumWindIntensity;

    float4 _MSC_SpruceWindDirection;
    float _MSC_SpruceWindIntensity;
    float _MSC_SpruceWindTurbulence;

    float2 ResolveSpruceWindDirection()
    {
        float2 direction = _MSC_SpruceWindDirection.xz;
        float lengthSquared = dot(direction, direction);
        return lengthSquared > 0.000001
            ? direction * rsqrt(lengthSquared)
            : float2(0.0, 1.0);
    }

    float3 ApplySpruceWind(
        float3 baseWorldPosition,
        float3 objectOriginWS,
        float timeSeconds)
    {
        float responseHeight = max(_WindHeightMeters, 0.1);
        float heightWeight = saturate(
            (baseWorldPosition.y - objectOriginWS.y -
             _WindBaseHeightMeters) / responseHeight);
        heightWeight = heightWeight * heightWeight *
            (3.0 - 2.0 * heightWeight);

        float intensity = max(
            saturate(_MSC_SpruceWindIntensity),
            _MinimumWindIntensity);
        float amplitude = intensity * intensity *
            (3.0 - 2.0 * intensity);
        float turbulence = saturate(_MSC_SpruceWindTurbulence);
        float2 direction = ResolveSpruceWindDirection();
        float2 crossDirection = float2(-direction.y, direction.x);
        float objectPhase = dot(
            objectOriginWS.xz,
            float2(0.071, 0.113));
        float spatialPhase = dot(
            baseWorldPosition.xz,
            float2(0.73, 1.17)) * _WindFrequency;

        // Wind intensity changes amplitude only. Feeding it into absolute
        // time would jump the phase whenever Enviro changes windMain.
        float phase = timeSeconds * _WindSpeed + objectPhase;
        float primaryWave =
            sin(phase + spatialPhase * 0.32) * 0.68 +
            sin(phase * 0.43 + objectPhase * 1.37) * 0.22;
        float gustWave =
            sin(phase * 0.17 + objectPhase * 0.41) * 0.5 + 0.5;
        float crossWave =
            sin(phase * 0.71 - spatialPhase * 0.24) *
            (0.10 + turbulence * 0.16);
        float2 displacement =
            direction * primaryWave *
                lerp(0.82, 1.0, gustWave * turbulence) +
            crossDirection * crossWave;

        // A soft component-wise limiter prevents combined gust harmonics
        // from producing violent crown snaps at maximum wind.
        displacement /= 1.0 + abs(displacement) * 0.28;
        return baseWorldPosition +
            float3(displacement.x, 0.0, displacement.y) *
            (_WindStrengthMeters * amplitude * heightWeight);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "HDLitShader"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }
            Stencil
            {
                WriteMask [_StencilWriteMaskDepth]
                Ref [_StencilRefDepth]
                Comp Always
                Pass Replace
            }
            Cull [_CullMode]
            AlphaToMask [_AlphaCutoffEnable]
            ZWrite On

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ WRITE_NORMAL_BUFFER
            #pragma multi_compile_fragment _ WRITE_MSAA_DEPTH

            #define HAVE_MESH_MODIFICATION
            #define SHADERPASS SHADERPASS_DEPTH_ONLY
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitDepthPass.hlsl"

            AttributesMesh ApplyMeshModification(
                AttributesMesh input,
                float3 timeParameters)
            {
                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                float3 objectOriginWS = TransformObjectToWorld(0.0);
                worldPosition = ApplySpruceWind(
                    worldPosition,
                    objectOriginWS,
                    timeParameters.x);
                input.positionOS = TransformWorldToObject(worldPosition);
                return input;
            }

            #include "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindSurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            Stencil
            {
                WriteMask [_StencilWriteMaskMV]
                Ref [_StencilRefMV]
                Comp Always
                Pass Replace
            }
            Cull [_CullMode]
            AlphaToMask [_AlphaCutoffEnable]
            ZWrite On

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ WRITE_NORMAL_BUFFER
            #pragma multi_compile_fragment _ WRITE_MSAA_DEPTH

            #define HAVE_MESH_MODIFICATION
            #define SHADERPASS SHADERPASS_MOTION_VECTORS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitSharePass.hlsl"

            AttributesMesh ApplyMeshModification(
                AttributesMesh input,
                float3 timeParameters)
            {
                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                float3 objectOriginWS = TransformObjectToWorld(0.0);
                worldPosition = ApplySpruceWind(
                    worldPosition,
                    objectOriginWS,
                    timeParameters.x);
                input.positionOS = TransformWorldToObject(worldPosition);
                return input;
            }

            #include "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindSurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassMotionVectors.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], [_AlphaSrcBlend] [_AlphaDstBlend]
            Blend 1 One OneMinusSrcAlpha
            Blend 2 One [_DstBlend2]
            Blend 3 One [_DstBlend2]
            ZWrite [_ZWrite]
            ZTest [_ZTestDepthEqualForOpaque]
            Stencil
            {
                WriteMask [_StencilWriteMask]
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }
            Cull [_CullModeForward]

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile_fragment SCREEN_SPACE_SHADOWS_OFF SCREEN_SPACE_SHADOWS_ON
            #pragma multi_compile_fragment DECALS_OFF DECALS_3RT DECALS_4RT
            #pragma multi_compile_fragment PUNCTUAL_SHADOW_LOW PUNCTUAL_SHADOW_MEDIUM PUNCTUAL_SHADOW_HIGH
            #pragma multi_compile_fragment DIRECTIONAL_SHADOW_LOW DIRECTIONAL_SHADOW_MEDIUM DIRECTIONAL_SHADOW_HIGH
            #pragma multi_compile_fragment AREA_SHADOW_MEDIUM AREA_SHADOW_HIGH
            #pragma multi_compile_fragment USE_FPTL_LIGHTLIST USE_CLUSTERED_LIGHTLIST

            #ifndef SHADER_STAGE_FRAGMENT
                #define SHADOW_LOW
                #ifndef USE_FPTL_LIGHTLIST
                    #define USE_FPTL_LIGHTLIST
                #endif
            #endif

            #define HAVE_MESH_MODIFICATION
            #define SHADERPASS SHADERPASS_FORWARD
            #define HAS_LIGHTLOOP
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/Lighting.hlsl"
            #ifdef DEBUG_DISPLAY
                #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Debug/DebugDisplay.hlsl"
            #endif
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoopDef.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/LightLoop.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitSharePass.hlsl"

            AttributesMesh ApplyMeshModification(
                AttributesMesh input,
                float3 timeParameters)
            {
                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                float3 objectOriginWS = TransformObjectToWorld(0.0);
                worldPosition = ApplySpruceWind(
                    worldPosition,
                    objectOriginWS,
                    timeParameters.x);
                input.positionOS = TransformWorldToObject(worldPosition);
                return input;
            }

            #include "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindSurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassForward.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull [_CullMode]
            ZClip [_ZClip]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #define HAVE_MESH_MODIFICATION
            #define SHADERPASS SHADERPASS_SHADOWS
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Material.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/Lit.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/Lit/ShaderPass/LitDepthPass.hlsl"

            AttributesMesh ApplyMeshModification(
                AttributesMesh input,
                float3 timeParameters)
            {
                float3 worldPosition = TransformObjectToWorld(input.positionOS);
                float3 objectOriginWS = TransformObjectToWorld(0.0);
                worldPosition = ApplySpruceWind(
                    worldPosition,
                    objectOriginWS,
                    timeParameters.x);
                input.positionOS = TransformWorldToObject(worldPosition);
                return input;
            }

            #include "Assets/Game/Presentation/Shaders/Vegetation/MSC_SpruceWindSurfaceData.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPassDepthOnly.hlsl"
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    FallBack "Hidden/Shader Graph/FallbackError"
}
