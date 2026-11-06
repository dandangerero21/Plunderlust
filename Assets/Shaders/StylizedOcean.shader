Shader "Plunderlust/Ocean/StylizedOcean"
{
    Properties
    {
        [Header(Water Colors)]
        _DeepColor ("Deep Oceanic Color", Color) = (0.04, 0.18, 0.35, 0.98)
        _MidColor ("Mid Sea Swell Color", Color) = (0.07, 0.48, 0.62, 0.95)
        _ShallowColor ("Shallow Tropical Turquoise", Color) = (0.12, 0.78, 0.75, 0.90)
        _CrestColor ("Wave Crest Aquamarine", Color) = (0.35, 0.88, 0.85, 1.0)
        _DepthDistance ("Depth Fade Distance", Float) = 9.0

        [Header(Shoreline and Surf Foam)]
        _FoamColor ("Foam & Surf Color", Color) = (0.95, 0.99, 1.0, 1.0)
        _FoamDistance ("Shoreline Foam Width", Float) = 2.6
        _FoamNoiseScale ("Foam Noise Scale", Float) = 0.45
        _FoamSpeed ("Foam Scroll & Surf Speed", Float) = 0.65

        [Header(Animated Voronoi Caustics)]
        _CausticScale ("Caustic Ripple Scale", Float) = 0.28
        _CausticSpeed ("Caustic Ripple Speed", Float) = 0.38
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.48

        [Header(Sun Specular and Reflections)]
        _SunSpecularColor ("Sun Specular Color", Color) = (1.0, 0.96, 0.82, 1.0)
        _SunSpecularGloss ("Sun Specular Gloss", Float) = 90.0
        _SunSpecularIntensity ("Sun Specular Intensity", Float) = 2.2
        _FresnelColor ("Horizon Fresnel Color", Color) = (0.20, 0.65, 0.78, 1.0)
        _FresnelPower ("Fresnel Power", Float) = 4.0

        [Header(Wave Crest Whitecaps)]
        _CrestThreshold ("Wave Crest Threshold", Float) = 0.40
        _CrestFoamIntensity ("Crest Foam Intensity", Range(0, 1)) = 0.65

        [Header(Gerstner Wave Parameters)]
        _Wave1Direction ("Wave 1 Direction", Vector) = (1.0, 0.35, 0, 0)
        _Wave1Wavelength ("Wave 1 Wavelength", Float) = 36.0
        _Wave1Amplitude ("Wave 1 Amplitude", Float) = 0.12
        _Wave1Speed ("Wave 1 Speed", Float) = 1.8
        _Wave1Steepness ("Wave 1 Steepness", Float) = 0.05

        _Wave2Direction ("Wave 2 Direction", Vector) = (-0.4, 0.9, 0, 0)
        _Wave2Wavelength ("Wave 2 Wavelength", Float) = 15.0
        _Wave2Amplitude ("Wave 2 Amplitude", Float) = 0.05
        _Wave2Speed ("Wave 2 Speed", Float) = 1.3
        _Wave2Steepness ("Wave 2 Steepness", Float) = 0.03
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float4 screenPos  : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _MidColor;
                float4 _ShallowColor;
                float4 _CrestColor;
                float4 _FoamColor;
                float4 _SunSpecularColor;
                float4 _FresnelColor;
                float _DepthDistance;
                float _FoamDistance;
                float _FoamNoiseScale;
                float _FoamSpeed;
                float _CausticScale;
                float _CausticSpeed;
                float _CausticStrength;
                float _SunSpecularGloss;
                float _SunSpecularIntensity;
                float _FresnelPower;
                float _CrestThreshold;
                float _CrestFoamIntensity;
                float4 _Wave1Direction;
                float _Wave1Wavelength;
                float _Wave1Amplitude;
                float _Wave1Speed;
                float _Wave1Steepness;
                float4 _Wave2Direction;
                float _Wave2Wavelength;
                float _Wave2Amplitude;
                float _Wave2Speed;
                float _Wave2Steepness;
            CBUFFER_END

            // Gerstner Wave Displacement & Analytical Normal (GPU accelerated)
            float3 GerstnerDisplace(float3 worldPos, float time, out float3 normal)
            {
                float3 p = worldPos;
                float3 n = float3(0.0, 1.0, 0.0);

                // Wave 1: Primary Caribbean swell
                float2 d1 = normalize(_Wave1Direction.xy);
                float k1 = 6.2831853 / max(1.0, _Wave1Wavelength);
                float omega1 = k1 * _Wave1Speed;
                float a1 = _Wave1Amplitude;
                float q1 = _Wave1Steepness / max(0.001, k1 * a1 * 3.0);
                float phase1 = k1 * (d1.x * p.x + d1.y * p.z) - omega1 * time;
                float cos1 = cos(phase1);
                float sin1 = sin(phase1);

                p.x -= q1 * a1 * d1.x * cos1;
                p.y += a1 * sin1;
                p.z -= q1 * a1 * d1.y * cos1;

                // Wave 2: Secondary cross-chop
                float2 d2 = normalize(_Wave2Direction.xy);
                float k2 = 6.2831853 / max(1.0, _Wave2Wavelength);
                float omega2 = k2 * _Wave2Speed;
                float a2 = _Wave2Amplitude;
                float q2 = _Wave2Steepness / max(0.001, k2 * a2 * 3.0);
                float phase2 = k2 * (d2.x * p.x + d2.y * p.z) - omega2 * time;
                float cos2 = cos(phase2);
                float sin2 = sin(phase2);

                p.x -= q2 * a2 * d2.x * cos2;
                p.y += a2 * sin2;
                p.z -= q2 * a2 * d2.y * cos2;

                // Wave 3: Micro surface ripples
                float2 d3 = normalize(float2(0.7, -0.7));
                float k3 = 6.2831853 / 6.0;
                float omega3 = k3 * 1.0;
                float a3 = 0.02;
                float phase3 = k3 * (d3.x * p.x + d3.y * p.z) - omega3 * time;
                float cos3 = cos(phase3);
                float sin3 = sin(phase3);

                p.y += a3 * sin3;

                // Compute exact analytical surface normal
                float wa1 = k1 * a1;
                float wa2 = k2 * a2;
                float wa3 = k3 * a3;
                n.x = -(d1.x * wa1 * cos1 + d2.x * wa2 * cos2 + d3.x * wa3 * cos3);
                n.z = -(d1.y * wa1 * cos1 + d2.y * wa2 * cos2 + d3.y * wa3 * cos3);
                n.y = 1.0 - (q1 * wa1 * sin1 + q2 * wa2 * sin2);
                normal = normalize(n);

                return p;
            }

            // Fast Voronoi noise for crisp crystalline tropical caustics
            float2 VoronoiHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float FastVoronoi(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float res = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 b = float2(x, y);
                        float2 r = b - f + VoronoiHash(g + b);
                        float d = dot(r, r);
                        res = min(res, d);
                    }
                }
                return sqrt(res);
            }

            float ShorelineNoise(float2 worldXZ, float time)
            {
                float2 p = worldXZ * _FoamNoiseScale + float2(time * _FoamSpeed, time * _FoamSpeed * 0.3);
                float n1 = sin(p.x * 2.0 + p.y * 1.5) * 0.5 + 0.5;
                float n2 = cos(p.x * 1.2 - p.y * 2.4) * 0.5 + 0.5;
                return n1 * n2;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                float3 waveNormal;
                worldPos = GerstnerDisplace(worldPos, _Time.y, waveNormal);

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS   = waveNormal;
                output.uv         = input.uv;
                output.screenPos  = ComputeScreenPos(output.positionCS);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float time = _Time.y;

                // 1. Scene Depth Sampling for Shorelines and Reef Shallows
                float2 screenUV = input.screenPos.xy / max(0.0001, input.screenPos.w);
                
                // Fallback default depth (deep ocean)
                float waterDepth = 40.0;
                #if defined(UNITY_DECLARE_DEPTH_TEXTURE_INCLUDED)
                float rawDepth = SampleSceneDepth(screenUV);
                bool hasDepth = (rawDepth > 0.0001 && rawDepth < 0.9999);
                if (hasDepth)
                {
                    float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float surfaceEyeDepth = input.screenPos.w;
                    waterDepth = max(0.0, sceneEyeDepth - surfaceEyeDepth);
                }
                #endif

                // 2. Caribbean Multi-Tone Color Gradient (Shallow Turquoise -> Tropical Azure -> Deep Sapphire)
                float depthFactor = saturate(waterDepth / max(0.01, _DepthDistance));
                float waveHeightNorm = saturate((input.positionWS.y + 0.6) * 0.7);
                float colorFactor = saturate(depthFactor * 0.82 - (waveHeightNorm - 0.5) * 0.22);

                float4 baseWaterColor;
                if (colorFactor < 0.45)
                {
                    baseWaterColor = lerp(_ShallowColor, _MidColor, colorFactor / 0.45);
                }
                else
                {
                    baseWaterColor = lerp(_MidColor, _DeepColor, (colorFactor - 0.45) / 0.55);
                }

                // 3. Animated Voronoi Caustics / Crystalline Water Ripples
                float2 uv1 = input.positionWS.xz * _CausticScale + float2(time * _CausticSpeed, time * _CausticSpeed * 0.35);
                float2 uv2 = input.positionWS.xz * (_CausticScale * 1.35) - float2(time * _CausticSpeed * 0.65, -time * _CausticSpeed * 0.45);
                float v1 = FastVoronoi(uv1);
                float v2 = FastVoronoi(uv2);
                float caustic = pow(saturate(1.0 - min(v1, v2)), 2.2) * 1.8;
                
                float causticVisibility = lerp(1.2, 0.38, depthFactor) * _CausticStrength;
                baseWaterColor.rgb += _CrestColor.rgb * (caustic * causticVisibility);

                // 4. Shoreline Surf with Rhythmic Pulsing Foam
                float surfPulse = sin(time * _FoamSpeed * 3.1415 - waterDepth * 3.2) * 0.5 + 0.5;
                float foamDistThreshold = _FoamDistance * (1.0 + surfPulse * 0.32);
                float foamFactor = saturate(1.0 - (waterDepth / max(0.01, foamDistThreshold)));
                
                float noise = ShorelineNoise(input.positionWS.xz, time);
                foamFactor = smoothstep(0.18, 0.70, foamFactor + (noise - 0.5) * 0.35);
                baseWaterColor.rgb = lerp(baseWaterColor.rgb, _FoamColor.rgb, foamFactor);
                baseWaterColor.a = max(baseWaterColor.a, foamFactor);

                // 5. Wave Crest Whitecaps
                float crest = saturate((input.positionWS.y - _CrestThreshold) * 3.2);
                float crestNoise = sin(input.positionWS.x * 1.1 + time * 1.4) * cos(input.positionWS.z * 1.1 - time * 1.1);
                crest *= saturate(crestNoise * 1.4 + 0.35);
                baseWaterColor.rgb = lerp(baseWaterColor.rgb, _FoamColor.rgb, crest * _CrestFoamIntensity);

                // 6. Sunlight Specular Highlight (Ocean Glimmer / Diamond Reflection Path)
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfVector = normalize(lightDir + viewDirWS);

                // Perturb normal with caustic waves for glittering sunlight reflections
                float3 perturbedNormal = normalize(normalWS + float3(cos(input.positionWS.z * 1.8 + time * 1.5) * 0.14, 0.0, sin(input.positionWS.x * 1.8 + time * 1.5) * 0.14));
                float NdotH = saturate(dot(perturbedNormal, halfVector));
                float specular = pow(NdotH, _SunSpecularGloss) * _SunSpecularIntensity;
                baseWaterColor.rgb += mainLight.color * _SunSpecularColor.rgb * specular;

                // 7. Horizon Fresnel Glow
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = pow(1.0 - NdotV, _FresnelPower);
                baseWaterColor.rgb = lerp(baseWaterColor.rgb, _FresnelColor.rgb, fresnel * 0.65);

                return baseWaterColor;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}