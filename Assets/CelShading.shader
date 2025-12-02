Shader "Universal Render Pipeline/CelPointLightsBasic"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white" {}
        _Color("Tint Color", Color) = (1,1,1,1)
        _Bands("Shading Bands", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float4 _Color;
            float _Bands;

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float fogFactor   : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                output.positionWS = positionWS;
                output.normalWS   = normalWS;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;
                float3 N = normalize(input.normalWS);

                float3 color = 0;

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                [loop]
                for (uint i = 0u; i < lightCount; ++i)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    float3 L = normalize(light.direction);
                    float ndotl = saturate(dot(N, L));

                    float bands = max(_Bands, 1.0);
                    float denom = max(bands - 1.0, 1.0);
                    float banded = floor(ndotl * bands) / denom;

                    float3 contrib = albedo * light.color * banded * light.distanceAttenuation;
                    color += contrib;
                }
                #endif

                float3 sh = SampleSH(N);
                float3 ambient = albedo * sh;
                color += ambient;

                color = MixFog(color, input.fogFactor);

                return float4(color, 1.0);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
