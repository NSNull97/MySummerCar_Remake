Shader "MSC/UI/MainMenuGaragePlate"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Garage Plate", 2D) = "black" {}
        _ContactShadow ("Contact Patch (center / radii)", Vector) = (0.39, 0.27, 0.19, 0.027)
        _ContactShadowAxis ("Contact Patch Ground Direction", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="HDRenderPipeline" "RenderType"="Opaque" "Queue"="Background" }
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off ZTest LEqual Blend Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _ContactShadow;
                float4 _ContactShadowAxis;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.uv = input.uv;
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float2 delta = input.uv - _ContactShadow.xy;
                float2 axis = _ContactShadowAxis.xy;
                float2 contact = float2(dot(delta, axis), dot(delta, float2(-axis.y, axis.x))) /
                    max(_ContactShadow.zw, 0.001);
                float shade = 1.0 - 0.52 * exp(-dot(contact, contact) * 1.5);
                // Match HDRP's unlit de-exposure path. The plate is already lit;
                // the preview's isolated key/fill lights affect only the car.
                float3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;
                return float4(color * shade * _DeExposureMultiplier, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
