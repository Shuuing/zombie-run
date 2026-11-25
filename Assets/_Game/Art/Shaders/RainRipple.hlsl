// 빗방울 파문 — 물 셰이더(BCM/BCM_StylizedWater · Toon/City/TFF_ToonWater)가 함께 쓴다.
// 2026-11-12 사용자: "비가 오면 바닥에 빗물이 튀기고 물 웅덩이에도 빗물 떨어지게 해줘".
//
// 세기는 머티리얼이 아니라 전역 _Rain_Ripple 이다 — ProtoRainFx 가 비 옵션(꺼짐·약함·강함)에 맞춰 매 프레임 올린다.
// 비가 안 오면 x = 0 이라 계산을 건너뛴다(물 셰이더 비용 그대로). UnityPerMaterial 밖에 둬야 SRP 배처가 안 끊긴다.
//
// 모양: 수면을 칸(_Rain_Ripple.z m)으로 나누고 칸마다 빗방울이 제 박자로 떨어진다. 떨어질 때마다(차례마다) 자리가 바뀌고,
// 차례마다 _Rain_Ripple.y 확률로만 떨어져 고르게 깔리지 않는다. 고리 하나가 퍼지며 옅어지고, 조금 늦게 작은 고리가 따라온다.
// 칸을 반 칸 어긋나게 두 겹 — 한 겹이면 격자 결이 보인다. 고리 중심은 칸 안쪽(0.25~0.75)이고 반지름은 0.24칸까지라 칸 밖으로 안 잘린다.
// 텍스처 없음(노이즈 텍스처 한 장도 안 늘린다).
#ifndef ZO_RAIN_RIPPLE_INCLUDED
#define ZO_RAIN_RIPPLE_INCLUDED

float4 _Rain_Ripple;   // x 세기 0~1, y 차례마다 떨어질 확률 0~1, z 칸 크기 m, w 칸 하나의 1초당 차례 수

float rr_hash(float2 p)
{
    p = p - 289.0 * floor(p / 289.0);   // 시간이 쌓여 좌표가 커져도 정밀도가 안 무너지게
    p = frac(p * float2(233.34, 851.73));
    p += dot(p, p + 23.45);
    return frac(p.x * p.y);
}

/// 0 ~ 1 — 수면 이 자리(xz, 월드 m)의 빗방울 고리 세기.
float RainRipple(float2 xz, float t)
{
    if (_Rain_Ripple.x <= 0.001) return 0.0;
    float cell = max(0.2, _Rain_Ripple.z);
    float acc = 0.0;
    [unroll] for (int l = 0; l < 2; l++)
    {
        float2 q = xz / cell + l * float2(0.5, 0.5);
        float2 id = floor(q), f = frac(q);
        float h = rr_hash(id + l * 17.0);
        float tt = t * _Rain_Ripple.w * (0.75 + 0.5 * h) + h;   // 칸마다 박자·위상이 다르다
        float cyc = floor(tt), ph = tt - cyc;                    // ph 0 → 1 = 떨어진 뒤 고리가 퍼져 사라지기까지
        float2 seed = id + cyc * float2(7.13, 3.71) + l * 31.0;
        if (rr_hash(seed + 5.5) > _Rain_Ripple.y) continue;     // 이번 차례엔 이 칸에 안 떨어진다
        float2 c = 0.25 + 0.5 * float2(rr_hash(seed), rr_hash(seed + 1.7));
        float d = length(f - c);
        float w = 0.018 + 0.02 * ph;                              // 띠 두께(칸 단위) — 퍼질수록 굵고 옅게
        float fade = (1.0 - ph) * smoothstep(0.0, 0.06, ph);
        float ring = 1.0 - smoothstep(0.0, w, abs(d - ph * 0.24));
        float ph2 = saturate((ph - 0.22) / 0.78);                 // 늦게 따라오는 작은 고리
        float ring2 = (1.0 - smoothstep(0.0, w, abs(d - ph2 * 0.16))) * step(0.001, ph2) * 0.55;
        acc = max(acc, max(ring, ring2) * fade);
    }
    return saturate(acc * _Rain_Ripple.x);
}

#endif
