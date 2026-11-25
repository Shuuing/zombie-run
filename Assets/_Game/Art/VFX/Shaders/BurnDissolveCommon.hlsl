// 화상 사망 디졸브(ZombieBurnOut) 공통 — 본체(BurnDissolve)와 불선(BurnEdge)이 **같은 식**으로 타들어가는 자리를 구해야 선이 구멍 가장자리에 정확히 얹힌다.
// 두 셰이더가 이 파일 하나를 같이 쓴다. 식을 고치면 양쪽이 함께 바뀐다.
#ifndef ZO_BURN_DISSOLVE_COMMON_INCLUDED
#define ZO_BURN_DISSOLVE_COMMON_INCLUDED

// 값 노이즈(텍스처 없이) — GibDissolve 와 같은 것
float BurnHash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
float BurnNoise(float3 p)
{
    float3 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = BurnHash31(i),                      n100 = BurnHash31(i + float3(1, 0, 0));
    float n010 = BurnHash31(i + float3(0, 1, 0)),    n110 = BurnHash31(i + float3(1, 1, 0));
    float n001 = BurnHash31(i + float3(0, 0, 1)),    n101 = BurnHash31(i + float3(1, 0, 1));
    float n011 = BurnHash31(i + float3(0, 1, 1)),    n111 = BurnHash31(i + float3(1, 1, 1));
    float nx00 = lerp(n000, n100, f.x), nx10 = lerp(n010, n110, f.x);
    float nx01 = lerp(n001, n101, f.x), nx11 = lerp(n011, n111, f.x);
    return lerp(lerp(nx00, nx10, f.y), lerp(nx01, nx11, f.y), f.z);
}

// 타는 순서 값(0~1). 문턱(BurnThreshold)보다 낮은 곳부터 사라진다.
//  · 무늬 = 월드 좌표 노이즈. **몸 좌표(positionOS)를 안 쓰는 이유**: 스킨 메시의 로컬 좌표는 리그마다 단위가 달라(cm 리그 · m 리그) 무늬 크기가 모델마다 달라진다.
//    월드 좌표는 늘 m 다. 대신 몸이 움직이면 무늬가 몸 위를 흐르므로 런타임이 사망 애니를 느리게 돌린다(ZombieBurnOut.AnimSpeed).
//  · 높이 기울기 = 머리 쪽이 먼저 타고 발밑이 마지막 — 불이 위에서 타 내려와 바닥에서 꺼진다. anchor.xyz = 죽은 자리(발), anchor.w = 몸 높이(m).
float BurnField(float3 positionWS, float4 anchor, float noiseScale, float3 noiseOffset, float heightBias)
{
    float3 np = (positionWS - anchor.xyz) * noiseScale + noiseOffset;
    float n = 0.6 * BurnNoise(np) + 0.4 * BurnNoise(np * 2.7 + 5.3);
    // 두 겹을 섞은 값 노이즈는 0.2~0.8 에 몰려 있다 — 0~1 로 편다. 안 펴면 몸이 진행도 70% 쯤에 이미 다 사라지고 나머지 시간은 빈 화면이다(렌더로 확인).
    n = saturate((n - 0.18) / 0.64);
    float h = saturate((positionWS.y - anchor.y) / max(anchor.w, 0.01));
    return lerp(n, 1.0 - h, heightBias);
}

// _Dissolve(0~1) → 문턱. 0 이면 아무것도 안 깎이고(-0.02), 1 이면 아무것도 안 남는다(1.02).
// 불선 띠는 문턱 **앞쪽**(아직 안 깎인 자리)에 있어 문턱과 함께 지나가므로 띠 폭만큼 더 밀 필요가 없다 — 밀면 끝의 그만큼이 빈 화면이 된다.
float BurnThreshold(float dissolve) { return dissolve * 1.04 - 0.02; }

// 시작하자마자 온몸에 선이 번쩍 뜨지 않게 — 처음 1/6 동안 서서히
float BurnFadeIn(float dissolve) { return saturate(dissolve * 6.0); }

#endif
