Shader "Custom/URP/VeinLit"
{
    Properties
    {
        _BaseColor     ("Base Color", Color) = (0.2, 0.2, 0.2, 1)
        _VeinColor     ("Vein Color", Color) = (0.8, 0.1, 0.1, 1)

        [Range(0,1)]
        _VeinIntensity ("Vein Intensity", Range(0,1)) = 1

        _VeinScale     ("Vein Scale", Float) = 4.0
        _VeinSharpness ("Vein Sharpness", Float) = 3.0
        _VeinContrast  ("Vein Contrast", Float) = 2.0
        _ScrollSpeed   ("Vein Scroll Speed", Float) = 0.3

        _Smoothness    ("Smoothness", Range(0,1)) = 0.4
        _Metallic      ("Metallic", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _VeinColor;
            float  _VeinIntensity;
            float  _VeinScale;
            float  _VeinSharpness;
            float  _VeinContrast;
            float  _ScrollSpeed;
            float  _Smoothness;
            float  _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS              : SV_POSITION;
                float3 positionWS               : TEXCOORD0;
                float3 normalWS                 : TEXCOORD1;
                float2 uv                       : TEXCOORD2;
                half4  fogFactorAndVertexLight : TEXCOORD3;
            };

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise3(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                float n000 = hash(i + float3(0,0,0));
                float n100 = hash(i + float3(1,0,0));
                float n010 = hash(i + float3(0,1,0));
                float n110 = hash(i + float3(1,1,0));
                float n001 = hash(i + float3(0,0,1));
                float n101 = hash(i + float3(1,0,1));
                float n011 = hash(i + float3(0,1,1));
                float n111 = hash(i + float3(1,1,1));

                float3 u = f * f * (3.0 - 2.0 * f);

                float n00 = lerp(n000, n100, u.x);
                float n10 = lerp(n010, n110, u.x);
                float n01 = lerp(n001, n101, u.x);
                float n11 = lerp(n011, n111, u.x);

                float n0 = lerp(n00, n10, u.y);
                float n1 = lerp(n01, n11, u.y);

                return lerp(n0, n1, u.z);
            }

            float fbm(float3 p)
            {
                float value = 0.0;
                float amp   = 0.5;
                float freq  = 1.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += amp * noise3(p * freq);
                    freq  *= 2.0;
                    amp   *= 0.5;
                }
                return value;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);

                OUT.positionHCS = TransformWorldToHClip(positionWS);
                OUT.positionWS  = positionWS;
                OUT.normalWS    = normalize(normalWS);
                OUT.uv          = IN.uv;

                half fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                half3 vertexLight = VertexLighting(positionWS, normalWS);
                OUT.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _ScrollSpeed;
                float3 p = IN.positionWS * _VeinScale + float3(0, t, 0);

                float n = fbm(p);
                float veins = abs(n - 0.5) * 2.0;
                veins = pow(saturate(1.0 - veins), _VeinSharpness);
                veins = pow(veins, _VeinContrast);

                float veinMask = saturate(veins * _VeinIntensity);
                float3 albedo = lerp(_BaseColor.rgb, _VeinColor.rgb, veinMask);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half NdotL = saturate(dot(IN.normalWS, mainLight.direction));
                half3 lighting = albedo * (mainLight.color * NdotL + 0.05);
                lighting += IN.fogFactorAndVertexLight.yzw * albedo;

                lighting = MixFog(lighting, IN.fogFactorAndVertexLight.x);

                return half4(lighting, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
