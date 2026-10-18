// 카툰 스타일 "고여 있는 물" 셰이더 (URP) v2
// - 부드러운 2~3단 툰 깊이 색 (얕은 물 / 깊은 물)
// - 물가에 얇은 거품 테두리 + 바깥쪽에 천천히 숨쉬는 잔물결 링
// - 듬성듬성 보이는 은은한 물결 무늬 (흐르지 않고 제자리에서 일렁임)
// - 반짝이는 작은 하이라이트 점
// ※ 깊이/거품은 URP Asset의 Depth Texture가 켜져 있어야 보입니다.
Shader "Custom/ToonStillWater"
{
    Properties
    {
        [Header(Color)]
        _ShallowColor ("Shallow Color", Color) = (0.45, 0.88, 0.93, 0.80)
        _DeepColor ("Deep Color", Color) = (0.18, 0.58, 0.82, 0.92)
        _DepthMaxDistance ("Depth Max Distance", Float) = 1.2
        _ColorSteps ("Color Steps (Toon)", Range(1, 5)) = 3
        _StepSoftness ("Step Softness", Range(0.001, 0.3)) = 0.05

        [Header(Shore Foam)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 0.95)
        _FoamDistance ("Foam Width", Range(0.01, 0.5)) = 0.1
        _FoamNoiseScale ("Foam Noise Scale", Float) = 2.5
        _ShoreRingColor ("Shore Ring Color", Color) = (1, 1, 1, 0.55)
        _ShoreRingDistance ("Shore Ring Distance", Range(0.05, 1)) = 0.28
        _ShoreRingWidth ("Shore Ring Width", Range(0.005, 0.1)) = 0.03
        _ShoreRingSpeed ("Shore Ring Breath Speed", Float) = 0.8

        [Header(Ripple Pattern)]
        _RippleColor ("Ripple Color", Color) = (1, 1, 1, 0.22)
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleThickness ("Ripple Line Thickness", Range(0.01, 0.3)) = 0.06
        _RippleCoverage ("Ripple Coverage", Range(0, 1)) = 0.45
        _RippleSpeed ("Ripple Wobble Speed", Float) = 0.35

        [Header(Sparkle)]
        _SparkleColor ("Sparkle Color", Color) = (1, 1, 1, 0.9)
        _SparkleScale ("Sparkle Scale", Float) = 1.2
        _SparkleSize ("Sparkle Size", Range(0, 0.2)) = 0.06
        _SparkleSpeed ("Sparkle Twinkle Speed", Float) = 1.5

        [Header(Surface)]
        _WaveHeight ("Gentle Wave Height", Float) = 0.01
        _WaveScale ("Gentle Wave Scale", Float) = 0.5
        _WaveSpeed ("Gentle Wave Speed", Float) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthMaxDistance;
                float _ColorSteps;
                float _StepSoftness;
                float4 _FoamColor;
                float _FoamDistance;
                float _FoamNoiseScale;
                float4 _ShoreRingColor;
                float _ShoreRingDistance;
                float _ShoreRingWidth;
                float _ShoreRingSpeed;
                float4 _RippleColor;
                float _RippleScale;
                float _RippleThickness;
                float _RippleCoverage;
                float _RippleSpeed;
                float4 _SparkleColor;
                float _SparkleScale;
                float _SparkleSize;
                float _SparkleSpeed;
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash22(i).x;
                float b = hash22(i + float2(1, 0)).x;
                float c = hash22(i + float2(0, 1)).x;
                float d = hash22(i + float2(1, 1)).x;
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 제자리에서 흔들리는 Voronoi
            // x: 셀 경계까지 거리, y: 가장 가까운 점까지 거리, z: 셀 랜덤값
            float3 voronoi(float2 uv, float t)
            {
                float2 cell = floor(uv);
                float2 f = frac(uv);
                float d1 = 8.0;
                float d2 = 8.0;
                float id = 0.0;

                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 g = float2(x, y);
                    float2 h = hash22(cell + g);
                    float2 o = 0.5 + 0.3 * sin(t + 6.2831 * h);
                    float d = length(g + o - f);

                    if (d < d1) { d2 = d1; d1 = d; id = h.x; }
                    else if (d < d2) { d2 = d; }
                }

                return float3(d2 - d1, d1, id);
            }

            // 안티에일리어싱된 계단 (가장자리 계단현상 방지)
            float aaStep(float edge, float x)
            {
                float w = max(fwidth(x), 1e-4);
                return smoothstep(edge - w, edge + w, x);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);

                float t = _Time.y * _WaveSpeed;
                positionWS.y += (sin(positionWS.x * _WaveScale + t) + cos(positionWS.z * _WaveScale * 0.8 + t * 1.3)) * 0.5 * _WaveHeight;

                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 wp = i.positionWS.xz;

                // ---------- 깊이 ----------
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth;

                // 직교(Orthographic) 카메라는 깊이가 선형이라 별도 계산이 필요합니다.
                if (unity_OrthoParams.w > 0.5)
                {
                #if UNITY_REVERSED_Z
                    rawDepth = 1.0 - rawDepth;
                #endif
                    sceneDepth = lerp(_ProjectionParams.y, _ProjectionParams.z, rawDepth);
                }
                else
                {
                    sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                }

                // 물 표면의 시점 깊이 (원근/직교 공통)
                float surfaceDepth = -TransformWorldToView(i.positionWS).z;
                float waterDepth = max(0.0, sceneDepth - surfaceDepth);

                // ---------- 툰 깊이 색 (부드러운 계단) ----------
                float d01 = saturate(waterDepth / max(0.001, _DepthMaxDistance));
                float steps = max(1.0, _ColorSteps);
                float scaled = d01 * steps;
                float band = floor(scaled);
                float fracPart = scaled - band;
                float stepped = (band + smoothstep(1.0 - _StepSoftness, 1.0, fracPart)) / steps;
                float4 col = lerp(_ShallowColor, _DeepColor, saturate(stepped));

                // ---------- 은은한 물결 무늬 ----------
                float3 vor = voronoi(wp * _RippleScale, t * _RippleSpeed);
                float rippleLine = 1.0 - aaStep(_RippleThickness, vor.x);
                // 큰 노이즈로 군데군데만 보이게
                float patch = valueNoise(wp * 0.15 + float2(sin(t * 0.1), cos(t * 0.13)));
                float rippleMask = smoothstep(1.0 - _RippleCoverage, 1.0 - _RippleCoverage + 0.25, patch);
                col.rgb = lerp(col.rgb, _RippleColor.rgb, rippleLine * rippleMask * _RippleColor.a);

                // ---------- 반짝이 점 ----------
                float3 sp = voronoi(wp * _SparkleScale + 17.3, t * 0.2);
                float twinkle = saturate(sin(t * _SparkleSpeed + sp.z * 40.0) * 1.5 - 0.5);
                float sparkle = (1.0 - aaStep(_SparkleSize, sp.y)) * twinkle * step(0.75, sp.z);
                col.rgb = lerp(col.rgb, _SparkleColor.rgb, sparkle * _SparkleColor.a);

                // ---------- 물가 숨쉬는 잔물결 링 ----------
                float breath = 0.5 + 0.5 * sin(t * _ShoreRingSpeed);
                float ringCenter = _ShoreRingDistance * (0.85 + 0.3 * breath);
                float ringNoise = valueNoise(wp * _FoamNoiseScale * 0.7 + 3.1) * 0.06;
                float ring = 1.0 - aaStep(_ShoreRingWidth, abs(waterDepth - ringCenter + ringNoise));
                ring *= (1.0 - breath * 0.5);
                col.rgb = lerp(col.rgb, _ShoreRingColor.rgb, ring * _ShoreRingColor.a);

                // ---------- 물가 거품 ----------
                float foamNoise = valueNoise(wp * _FoamNoiseScale + sin(t * 0.5) * 0.3);
                float foamCut = _FoamDistance * (0.6 + 0.8 * foamNoise);
                float foam = 1.0 - aaStep(foamCut, waterDepth);
                col.rgb = lerp(col.rgb, _FoamColor.rgb, foam * _FoamColor.a);
                col.a = max(col.a, foam * _FoamColor.a);

                // ---------- 조명 (밝기만 살짝 반영) ----------
                Light mainLight = GetMainLight();
                float lum = dot(mainLight.color, float3(0.299, 0.587, 0.114));
                col.rgb *= lerp(0.8, 1.0, saturate(lum));

                col.rgb = MixFog(col.rgb, i.fogFactor);
                return half4(col.rgb, saturate(col.a));
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
