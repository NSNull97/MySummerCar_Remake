#ifndef MSC_SPRUCE_WIND_SURFACE_DATA_INCLUDED
#define MSC_SPRUCE_WIND_SURFACE_DATA_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/BuiltinUtilities.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/Material/MaterialUtilities.hlsl"

float _MSC_VegetationWetness;

void GetSurfaceAndBuiltinData(
    FragInputs input,
    float3 V,
    inout PositionInputs posInput,
    out SurfaceData surfaceData,
    out BuiltinData builtinData
    RAY_TRACING_OPTIONAL_PARAMETERS)
{
    #ifdef LOD_FADE_CROSSFADE
        LODDitheringTransition(
            ComputeFadeMaskSeed(V, posInput.positionSS),
            unity_LODFade.x);
    #endif

    float3 doubleSidedConstants = float3(1.0, 1.0, 1.0);
    #ifdef _DOUBLESIDED_ON
        doubleSidedConstants = _DoubleSidedConstants.xyz;
        ApplyDoubleSidedFlipOrMirror(input, doubleSidedConstants);
    #endif

    float2 uv = TRANSFORM_TEX(input.texCoord0.xy, _BaseColorMap);
    float4 sampled = SAMPLE_TEXTURE2D(
        _BaseColorMap,
        sampler_BaseColorMap,
        uv);
    float alpha = lerp(
        _AlphaRemapMin,
        _AlphaRemapMax,
        sampled.a) * _BaseColor.a;

    #ifdef _ALPHATEST_ON
        GENERIC_ALPHA_TEST(alpha, _AlphaCutoff);
    #endif

    float3 normalTS = UnpackNormalMapRGorAG(
        SAMPLE_TEXTURE2D(
            _NormalMap,
            sampler_NormalMap,
            uv),
        _NormalScale);
    normalTS = normalize(normalTS);

    ZERO_INITIALIZE(SurfaceData, surfaceData);
    float3 dryColor = sampled.rgb * _BaseColor.rgb;
    float wetness = saturate(_MSC_VegetationWetness);
    surfaceData.materialFeatures =
        MATERIALFEATUREFLAGS_LIT_STANDARD |
        MATERIALFEATUREFLAGS_LIT_SPECULAR_COLOR;
    surfaceData.baseColor = lerp(
        dryColor,
        dryColor * 0.72,
        wetness);
    surfaceData.specularOcclusion = 1.0;
    surfaceData.perceptualSmoothness = lerp(
        _Smoothness,
        saturate(_Smoothness + 0.08),
        wetness);
    surfaceData.ambientOcclusion = 1.0;
    // Tree needles are a dielectric. Keep the source package's packed/specular
    // conventions from leaking into the project-owned HDRP surface contract.
    surfaceData.metallic = 0.0;
    surfaceData.specularColor = _SpecularColor.rgb;
    GetNormalWS(
        input,
        normalTS,
        surfaceData.normalWS,
        doubleSidedConstants);
    surfaceData.geomNormalWS = normalize(input.tangentToWorld[2]);
    surfaceData.tangentWS = Orthonormalize(
        normalize(input.tangentToWorld[0]),
        surfaceData.normalWS);
    surfaceData.ior = 1.0;
    surfaceData.transmittanceColor = 1.0;
    surfaceData.atDistance = 1.0;

    InitBuiltinData(
        posInput,
        alpha,
        surfaceData.normalWS,
        -input.tangentToWorld[2],
        input.texCoord1,
        input.texCoord2,
        builtinData);
    builtinData.emissiveColor = 0.0;
    #ifdef _ALPHATEST_ON
        builtinData.alphaClipTreshold = _AlphaCutoff;
    #endif
    PostInitBuiltinData(V, posInput, surfaceData, builtinData);
}

#endif
