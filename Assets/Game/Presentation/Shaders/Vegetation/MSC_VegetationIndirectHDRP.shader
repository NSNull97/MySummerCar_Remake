Shader "MSC/HDRP/Vegetation Indirect"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.24, 0.52, 0.16, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.45
        _WindStrength("Wind Strength", Range(0, 1)) = 0.12
        _WindSpeed("Wind Speed", Range(0, 8)) = 1.5
        _WindFrequency("Wind Frequency", Range(0, 2)) = 0.17
        _ColorVariation("Color Variation", Range(0, 0.5)) = 0.08
        _Smoothness("Smoothness", Range(0, 1)) = 0.04
        _SpecularColor("Foliage Specular F0", Color) = (0.018, 0.018, 0.018, 1)

        [HideInInspector]_EmissiveColor("Emissive Color", Color) = (0, 0, 0, 0)
        [HideInInspector]_EmissionColor("Legacy GI Emission", Color) = (0, 0, 0, 0)
        [HideInInspector]_EmissiveExposureWeight("Emissive Exposure", Float) = 1
    }

    HLSLINCLUDE
    #pragma target 4.5
    #pragma multi_compile_instancing
    #pragma instancing_options procedural:SetupVegetation

    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/ShaderPass/ShaderPass.cs.hlsl"

    struct VegetationInstanceRecord
    {
        float3 worldPosition;
        uint packedNormal;
        uint packedYawScale;
        uint packedVariation;
    };

    StructuredBuffer<VegetationInstanceRecord> _VegetationInstanceBuffer;

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float _Cutoff;
    float _WindStrength;
    float _WindSpeed;
    float _WindFrequency;
    float _ColorVariation;
    float _Smoothness;
    float4 _SpecularColor;
    float _VegetationLodFade;
    float _VegetationLodFadeDirection;

    struct Attributes
    {
        float3 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float2 uv : TEXCOORD0;
        uint instanceID : SV_InstanceID;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 normalWS : TEXCOORD1;
        float colorVariation : TEXCOORD2;
        float4 currentPositionCS : TEXCOORD3;
        float4 previousPositionCS : TEXCOORD4;
        float2 lodDitherPosition : TEXCOORD5;
        float3 positionRWS : TEXCOORD6;
    };

    void SetupVegetation()
    {
    }

    float UnpackSignedUnit(uint packed)
    {
        return (packed / 65535.0) * 2.0 - 1.0;
    }

    float3 DecodeOctNormal(uint packed)
    {
        float x = UnpackSignedUnit(packed & 0xffffu);
        float z = UnpackSignedUnit(packed >> 16);
        float3 normal = float3(x, 1.0 - abs(x) - abs(z), z);
        if (normal.y < 0.0)
        {
            float oldX = normal.x;
            normal.x = (1.0 - abs(normal.z)) * (oldX >= 0.0 ? 1.0 : -1.0);
            normal.z = (1.0 - abs(oldX)) * (normal.z >= 0.0 ? 1.0 : -1.0);
        }
        return normalize(normal);
    }

    float DecodeYaw(uint packed)
    {
        return ((packed & 0xffffu) / 65535.0) * (2.0 * PI);
    }

    float DecodeScale(uint packed)
    {
        return lerp(0.01, 8.0, (packed >> 16) / 65535.0);
    }

    void BuildBasis(float3 up, out float3 right, out float3 forward)
    {
        float3 referenceAxis = abs(up.z) < 0.95
            ? float3(0.0, 0.0, 1.0)
            : float3(1.0, 0.0, 0.0);
        right = normalize(cross(referenceAxis, up));
        forward = normalize(cross(up, right));
    }

    float3 RotateIntoInstance(
        float3 value,
        float3 up,
        float yaw,
        bool includeScale,
        float scale)
    {
        float3 right;
        float3 forward;
        BuildBasis(up, right, forward);
        float sine;
        float cosine;
        sincos(yaw, sine, cosine);
        float3 rotated =
            right * (value.x * cosine - value.z * sine) +
            up * value.y +
            forward * (value.x * sine + value.z * cosine);
        return includeScale ? rotated * scale : rotated;
    }

    float3 ApplyWind(
        float3 worldPosition,
        float localHeight,
        float windPhase,
        float timeSeconds)
    {
        float heightWeight = saturate(localHeight);
        float wave = sin(
            timeSeconds * _WindSpeed +
            windPhase +
            dot(worldPosition.xz, float2(0.73, 1.17)) * _WindFrequency);
        float crossWave = cos(
            timeSeconds * (_WindSpeed * 0.73) +
            windPhase * 1.41 +
            dot(worldPosition.xz, float2(-0.41, 0.91)) * _WindFrequency);
        return worldPosition +
            float3(wave, 0.0, crossWave) *
            (_WindStrength * heightWeight * heightWeight);
    }

    float DitherNoise(float2 screenPosition)
    {
        float2 pixel = floor(screenPosition);
        return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
    }

    void ApplyAlphaAndLodClip(Varyings input, out float4 sampled)
    {
        sampled = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
        clip(sampled.a - _Cutoff);
        float lodFade = saturate(_VegetationLodFade);
        if (lodFade <= 0.0)
        {
            clip(-1.0);
        }
        if (lodFade >= 1.0)
        {
            return;
        }

        // SV_POSITION contains the temporal projection jitter. Feeding it into
        // the LOD mask makes the alpha-tested coverage crawl every frame, which
        // is especially destructive for TAA/DLSS/DLAA history. Seed the mask
        // from the non-jittered clip position instead.
        float dither = DitherNoise(input.lodDitherPosition);
        if (_VegetationLodFadeDirection < 0.0)
        {
            clip(dither - (1.0 - lodFade));
        }
        else
        {
            clip(lodFade - dither);
        }
    }

    Varyings VegetationVertex(Attributes input)
    {
        VegetationInstanceRecord instance =
            _VegetationInstanceBuffer[input.instanceID];
        float3 surfaceNormal = DecodeOctNormal(instance.packedNormal);
        float yaw = DecodeYaw(instance.packedYawScale);
        float scale = DecodeScale(instance.packedYawScale);
        float windPhase =
            ((instance.packedVariation >> 16) & 0xffffu) / 65535.0 *
            (2.0 * PI);

        float3 worldPosition = instance.worldPosition +
            RotateIntoInstance(
                input.positionOS,
                surfaceNormal,
                yaw,
                true,
                scale);
        float3 worldNormal = normalize(
            RotateIntoInstance(
                input.normalOS,
                surfaceNormal,
                yaw,
                false,
                1.0));
        float3 currentWorldPosition = ApplyWind(
            worldPosition,
            input.positionOS.y,
            windPhase,
            _TimeParameters.x);
        float3 previousWorldPosition = ApplyWind(
            worldPosition,
            input.positionOS.y,
            windPhase,
            _LastTimeParameters.x);
        float3 currentRws = GetCameraRelativePositionWS(currentWorldPosition);
        float3 previousRws = GetCameraRelativePositionWS(previousWorldPosition);

        Varyings output;
        output.positionCS = TransformWorldToHClip(currentRws);
        output.positionRWS = currentRws;
        output.currentPositionCS =
            mul(UNITY_MATRIX_UNJITTERED_VP, float4(currentRws, 1.0));
        output.previousPositionCS =
            mul(UNITY_MATRIX_PREV_VP, float4(previousRws, 1.0));
        float2 lodDitherUv =
            output.currentPositionCS.xy / output.currentPositionCS.w * 0.5 + 0.5;
        #if UNITY_UV_STARTS_AT_TOP
            lodDitherUv.y = 1.0 - lodDitherUv.y;
        #endif
        output.lodDitherPosition = lodDitherUv * _ScreenSize.xy;
        output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
        output.normalWS = worldNormal;
        output.colorVariation =
            ((instance.packedVariation >> 8) & 0xffu) / 255.0;
        return output;
    }

    float4 VegetationDepthFragment(Varyings input) : SV_Target
    {
        float4 sampled;
        ApplyAlphaAndLodClip(input, sampled);
        return 0.0;
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

        Cull Off
        ZWrite On

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
            #pragma multi_compile_fragment SCREEN_SPACE_SHADOWS_OFF SCREEN_SPACE_SHADOWS_ON
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

            #pragma vertex VegetationVertex
            #pragma fragment VegetationForwardFragment

            float3 BuildVegetationTangent(float3 normalWS)
            {
                float3 referenceAxis = abs(normalWS.y) < 0.999
                    ? float3(0.0, 1.0, 0.0)
                    : float3(1.0, 0.0, 0.0);
                return normalize(cross(referenceAxis, normalWS));
            }

            float4 VegetationForwardFragment(
                Varyings input,
                FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float4 sampled;
                ApplyAlphaAndLodClip(input, sampled);

                float faceSign = IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
                float3 normalWS = normalize(input.normalWS) * faceSign;
                float variation =
                    (input.colorVariation - 0.5) * 2.0 * _ColorVariation;

                SurfaceData surfaceData;
                ZERO_INITIALIZE(SurfaceData, surfaceData);
                surfaceData.materialFeatures =
                    MATERIALFEATUREFLAGS_LIT_STANDARD |
                    MATERIALFEATUREFLAGS_LIT_SPECULAR_COLOR;
                surfaceData.baseColor =
                    sampled.rgb * max(0.0, 1.0 + variation);
                surfaceData.specularOcclusion = 1.0;
                surfaceData.normalWS = normalWS;
                surfaceData.perceptualSmoothness = saturate(_Smoothness);
                surfaceData.ambientOcclusion = 1.0;
                surfaceData.metallic = 0.0;
                surfaceData.specularColor = max(0.0, _SpecularColor.rgb);
                surfaceData.geomNormalWS = normalWS;
                surfaceData.tangentWS = BuildVegetationTangent(normalWS);
                surfaceData.ior = 1.0;
                surfaceData.transmittanceColor = 1.0;
                surfaceData.atDistance = 1.0;

                uint2 tileIndex = uint2(input.positionCS.xy) / GetTileSize();
                PositionInputs posInput = GetPositionInput(
                    input.positionCS.xy,
                    _ScreenSize.zw,
                    input.positionCS.z,
                    input.positionCS.w,
                    input.positionRWS,
                    tileIndex);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionRWS);

                BuiltinData builtinData;
                InitBuiltinData(
                    posInput,
                    sampled.a,
                    normalWS,
                    -normalWS,
                    float4(0.0, 0.0, 0.0, 0.0),
                    float4(0.0, 0.0, 0.0, 0.0),
                    builtinData);
                builtinData.emissiveColor = 0.0;
                builtinData.alphaClipTreshold = _Cutoff;
                PostInitBuiltinData(V, posInput, surfaceData, builtinData);

                BSDFData bsdfData = ConvertSurfaceDataToBSDFData(
                    uint2(input.positionCS.xy),
                    surfaceData);
                PreLightData preLightData = GetPreLightData(
                    V,
                    posInput,
                    bsdfData);
                LightLoopOutput lightLoopOutput;
                LightLoop(
                    V,
                    posInput,
                    preLightData,
                    bsdfData,
                    builtinData,
                    LIGHT_FEATURE_MASK_FLAGS_OPAQUE,
                    lightLoopOutput);

                float exposure = GetCurrentExposureMultiplier();
                float3 diffuseLighting =
                    lightLoopOutput.diffuseLighting * exposure;
                float3 specularLighting =
                    lightLoopOutput.specularLighting * exposure;
                return ApplyBlendMode(
                    diffuseLighting,
                    specularLighting,
                    builtinData.opacity);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthForwardOnly" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex VegetationVertex
            #pragma fragment VegetationDepthFragment
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex VegetationVertex
            #pragma fragment VegetationDepthFragment
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma vertex VegetationVertex
            #pragma fragment VegetationMotionFragment

            float4 VegetationMotionFragment(Varyings input) : SV_Target
            {
                float4 sampled;
                ApplyAlphaAndLodClip(input, sampled);
                float2 currentNdc =
                    input.currentPositionCS.xy / input.currentPositionCS.w;
                float2 previousNdc =
                    input.previousPositionCS.xy / input.previousPositionCS.w;
                float2 motionVector = clamp(
                    currentNdc - previousNdc,
                    -2.0,
                    2.0);
                #if UNITY_UV_STARTS_AT_TOP
                    motionVector.y = -motionVector.y;
                #endif
                return float4(motionVector * 0.5, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
