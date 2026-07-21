Shader "Hidden/MSC/UI/SeparableGaussianBlur"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Source", 2D) = "black" {}
        _BlurDirection ("Blur Direction", Vector) = (1, 0, 0, 0)
        _BlurRadius ("Blur Radius", Float) = 1
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _BlurDirection;
            float _BlurRadius;

            struct Attributes
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 offset =
                    _MainTex_TexelSize.xy * _BlurDirection.xy * _BlurRadius;

                float3 color = tex2D(_MainTex, input.uv).rgb * 0.2270270270;
                color += tex2D(
                    _MainTex,
                    input.uv + offset * 1.3846153846).rgb * 0.3162162162;
                color += tex2D(
                    _MainTex,
                    input.uv - offset * 1.3846153846).rgb * 0.3162162162;
                color += tex2D(
                    _MainTex,
                    input.uv + offset * 3.2307692308).rgb * 0.0702702703;
                color += tex2D(
                    _MainTex,
                    input.uv - offset * 3.2307692308).rgb * 0.0702702703;
                return float4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
