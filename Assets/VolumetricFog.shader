Shader "Universal Render Pipeline/VolumetricFogVolume"
{
    Properties
    {
        _FogColor("Fog Color", Color) = (0.6,0.7,0.8,1)
        _Density("Density", Float) = 1.0
        _StepCount("Step Count", Range(8,128)) = 48
        _NoiseScale("Noise Scale", Float) = 2.0
        _NoiseIntensity("Noise Intensity", Range(0,1)) = 0.5
        _DistanceFalloff("Distance Falloff", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "VolumetricFog"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _FogColor;
            float _Density;
            float _StepCount;
            float _NoiseScale;
            float _NoiseIntensity;
            float _DistanceFalloff;

            float3 hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(float3(p.x * p.y, p.y * p.z, p.z * p.x));
            }

            float noise3d(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = dot(hash3(i + float3(0,0,0)), f - float3(0,0,0));
                float n100 = dot(hash3(i + float3(1,0,0)), f - float3(1,0,0));
                float n010 = dot(hash3(i + float3(0,1,0)), f - float3(0,1,0));
                float n110 = dot(hash3(i + float3(1,1,0)), f - float3(1,1,0));
                float n001 = dot(hash3(i + float3(0,0,1)), f - float3(0,0,1));
                float n101 = dot(hash3(i + float3(1,0,1)), f - float3(1,0,1));
                float n011 = dot(hash3(i + float3(0,1,1)), f - float3(0,1,1));
                float n111 = dot(hash3(i + float3(1,1,1)), f - float3(1,1,1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                return lerp(nxy0, nxy1, u.z);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                o.worldPos = world;
                o.positionCS = TransformWorldToHClip(world);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 camWS = GetCameraPositionWS();
                float3 camOS = mul(unity_WorldToObject, float4(camWS,1)).xyz;

                float3 dirWS = normalize(i.worldPos - camWS);
                float3 dirOS = mul(unity_WorldToObject, float4(dirWS,0)).xyz;

                float3 boxMin = float3(-0.5,-0.5,-0.5);
                float3 boxMax = float3(0.5,0.5,0.5);

                float3 t1 = (boxMin - camOS) / dirOS;
                float3 t2 = (boxMax - camOS) / dirOS;

                float3 tMin = min(t1, t2);
                float3 tMax = max(t1, t2);

                float tNear = max(max(tMin.x, tMin.y), tMin.z);
                float tFar = min(min(tMax.x, tMax.y), tMax.z);

                if (tFar <= 0.0 || tNear >= tFar)
                    return float4(0,0,0,0);

                tNear = max(tNear, 0.0);

                float steps = max(_StepCount, 1.0);
                float dt = (tFar - tNear) / steps;

                float transmittance = 1.0;
                float3 fogAccum = 0.0;

                float t = tNear;
                [loop]
                for (int s = 0; s < 256; s++)
                {
                    if (s >= steps) break;

                    float3 sampleOS = camOS + dirOS * t;
                    float3 sampleWS = mul(unity_ObjectToWorld, float4(sampleOS,1)).xyz;
                    float dist = length(sampleWS - camWS);

                    float n = noise3d(sampleOS * _NoiseScale);
                    float d = _Density;
                    d *= lerp(1.0, 1.0 + n, _NoiseIntensity);
                    d *= exp(-dist * _DistanceFalloff);

                    float absorb = d * dt;
                    float stepTrans = exp(-absorb);
                    float contrib = transmittance * (1.0 - stepTrans);

                    fogAccum += _FogColor.rgb * contrib;
                    transmittance *= stepTrans;

                    if (transmittance < 0.01) break;

                    t += dt;
                }

                float alpha = saturate(1.0 - transmittance);
                return float4(fogAccum, alpha);
            }

            ENDHLSL
        }
    }
}
