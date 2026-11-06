Shader "Plunderlust/Ocean/WakeFoam"
{
    Properties
    {
        _Color ("Foam Color", Color) = (0.95, 0.99, 1.0, 0.85)
        _BaseColor ("Base Color", Color) = (0.95, 0.99, 1.0, 0.85)
        _RingWidth ("Ring Softness", Range(0.1, 1.0)) = 0.65
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent+100" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BaseColor;
                float _RingWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = (input.color.a > 0.001) ? input.color : float4(1.0, 1.0, 1.0, 1.0);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Soft expanding wake ripple disc / ring
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float dist = length(centeredUV);

                if (dist > 1.0) discard;

                // Frothy foam edge with soft core
                float edgeFade = saturate((1.0 - dist) * 2.8);
                float ripple = sin(dist * 3.14159);
                
                float4 col = (_Color.a > 0.001) ? _Color : _BaseColor;
                float alpha = edgeFade * ripple * input.color.a * col.a;

                return float4(col.rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
