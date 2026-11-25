// 도시 풀잎 (CityGrassBaker 가 8m 블록 단위로 구운 결합 메시).
//
// 풀잎 하나 = 정점 5개·삼각형 3개, 불투명(알파 컷아웃 없음 — 모바일에서 오버드로가 비싸다).
// 정점 데이터: uv0 = (좌우 -1..1, 높이 비율 h 0..1), uv1 = 뿌리 xz, uv2 = (풀잎 높이 m, 난수, 뿌리 y),
//              color.rgb = 풀잎별 색 변화, color.a = 1 잔디 / 0 길가 잡초(마른 색).
//
// 휨 = 상호작용 필드(ProtoGrassField 가 플레이어 주변 40m 에 그리는 텍스처) + 바람.
//   필드 R = 발자국(좀비·플레이어가 밟음), G = 폭발. 값이 클수록 눕고, 기울기 반대쪽(중심에서 바깥)으로 눕는다.
//   필드가 없으면(_GrassField.z == 0) 바람만 분다.
// 노멀은 전부 위 — 바닥(잔디 텍스처)과 같은 밝기로 섞이게(ProtoFieldStreamer 풀 카드와 같은 규칙).
Shader "Proto/Grass"
{
    Properties
    {
        _RootColor   ("Root", Color)       = (0.22, 0.38, 0.13, 1)
        _TipColor    ("Tip", Color)        = (0.58, 0.82, 0.32, 1)
        _DryColor    ("Weeds (dry)", Color) = (0.60, 0.62, 0.30, 1)
        _ScorchColor ("Scorched", Color)   = (0.10, 0.09, 0.07, 1)
        _AmbientMul  ("Ambient", Float)    = 1.0
        _SunMul      ("Sun", Float)        = 0.8
        _WindDir     ("Wind dir (xy = world xz)", Vector) = (0.8, 0.6, 0, 0)
        _WindStrength("Wind angle (rad)", Float) = 0.16
        _WindSpeed   ("Wind speed", Float) = 1.7
        _FootBend    ("Footprint bend (rad)", Float) = 1.15
        _BlastBend   ("Blast bend (rad)", Float)     = 1.45
        _TrampleDark ("Trampled brightness", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _RootColor, _TipColor, _DryColor, _ScorchColor;
            float4 _WindDir;
            float _AmbientMul, _SunMul, _WindStrength, _WindSpeed, _FootBend, _BlastBend, _TrampleDark;
        CBUFFER_END

        // 전역(ProtoGrassField). xy = 필드 원점(월드 xz), z = 1/크기(0 이면 필드 없음), w = 기울기 샘플 간격(uv)
        TEXTURE2D(_GrassFieldTex); SAMPLER(sampler_GrassFieldTex);
        float4 _GrassField;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv0 : TEXCOORD0;
            float2 uv1 : TEXCOORD1;
            float3 uv2 : TEXCOORD2;
            half4  color : COLOR;
        };

        // 휜 뒤 월드 위치. scorch = 폭발 자국(0..1), trample = 눌린 정도(0..1)
        // 카메라가 ~18m 위라 0.3m 풀잎이 눕는 모양만으로는 거의 안 읽힌다 — 눌린 풀은 어둡게 칠해 "밟고 간 길"이 보이게 한다.
        float3 BendBlade(Attributes v, out half scorch, out half trample)
        {
            float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
            float3 root  = TransformObjectToWorld(float3(v.uv1.x, v.uv2.z, v.uv1.y));
            float3 off   = posWS - root;
            float  h     = v.uv0.y;
            float  rnd   = v.uv2.y;
            float2 rdir  = float2(cos(rnd * 6.2832), sin(rnd * 6.2832));

            float  press = 0;
            float2 pdir  = rdir;
            scorch = 0;
            trample = 0;
            if (_GrassField.z > 0)
            {
                float2 uv = (root.xz - _GrassField.xy) * _GrassField.z;
                if (all(uv > 0.0) && all(uv < 1.0))
                {
                    float2 s  = SAMPLE_TEXTURE2D_LOD(_GrassFieldTex, sampler_GrassFieldTex, uv, 0).rg;
                    float2 sx = SAMPLE_TEXTURE2D_LOD(_GrassFieldTex, sampler_GrassFieldTex, uv + float2(_GrassField.w, 0), 0).rg;
                    float2 sz = SAMPLE_TEXTURE2D_LOD(_GrassFieldTex, sampler_GrassFieldTex, uv + float2(0, _GrassField.w), 0).rg;
                    float  c  = s.r + s.g;
                    float2 g  = float2(sx.r + sx.g - c, sz.r + sz.g - c);
                    float  gl = length(g);
                    // 찍힌 원의 중심이 값이 가장 크다 — 기울기 반대쪽 = 중심에서 바깥으로 눕는다. 한가운데(평평)는 풀잎마다 제멋대로
                    pdir   = gl > 1e-4 ? -g / gl : rdir;
                    press  = max(saturate(s.r) * _FootBend, saturate(s.g) * _BlastBend);
                    scorch = saturate(s.g);
                    trample = saturate(max(s.r, s.g));
                }
            }

            float2 wd   = normalize(_WindDir.xy + 1e-5);
            float  wph  = _Time.y * _WindSpeed + dot(root.xz, float2(0.35, 0.21)) + rnd * 1.3;
            float  wind = (sin(wph) * 0.6 + sin(wph * 2.3 + 1.7) * 0.4) * _WindStrength * (1 - saturate(press / max(_FootBend, 1e-3)));

            float2 bv  = pdir * press + wd * wind;
            float  ang = min(length(bv), 1.5);
            float2 bd  = ang > 1e-4 ? bv / max(length(bv), 1e-4) : rdir;
            float  a   = ang * h;   // 끝으로 갈수록 더 휜다
            float  y   = off.y;
            float3 bent = float3(off.x + bd.x * y * sin(a), y * cos(a), off.z + bd.y * y * sin(a));
            return root + bent;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half4  color      : COLOR;
                half4  hsft       : TEXCOORD1;   // h, scorch, fog, trample
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                half scorch, trample;
                float3 p = BendBlade(v, scorch, trample);
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.color = v.color;
                o.hsft = half4(v.uv0.y, scorch, ComputeFogFactor(o.positionCS.z), trample);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half h = i.hsft.x;
                half3 alb = lerp(_RootColor.rgb, _TipColor.rgb, h);
                alb = lerp(_DryColor.rgb * lerp(0.65h, 1.0h, h), alb, i.color.a);
                alb *= i.color.rgb;
                alb *= lerp(1.0h, _TrampleDark, i.hsft.w);
                alb = lerp(alb, _ScorchColor.rgb, i.hsft.y * 0.8h);

                Light L = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = saturate(L.direction.y);
                half3 col = alb * (SampleSH(half3(0, 1, 0)) * _AmbientMul + L.color * (L.shadowAttenuation * ndl * _SunMul));
                col = MixFog(col, i.hsft.z);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes v) : SV_POSITION { half s, t; return TransformWorldToHClip(BendBlade(v, s, t)); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(Attributes v) : SV_POSITION { half s, t; return TransformWorldToHClip(BendBlade(v, s, t)); }
            half4 frag() : SV_Target { return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
