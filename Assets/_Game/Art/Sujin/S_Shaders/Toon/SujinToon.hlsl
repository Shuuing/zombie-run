#ifndef TFF_CITY_INCLUDED
#define TFF_CITY_INCLUDED

// ---------------------------------------------------------------------------------------------
// Sujin/Toon/* 셰이더(옛 Toon/City/TFF_*)가 같이 쓰는 투시(see-through) 디더.
// BCM_Toony_Include.cginc 의 BCM_SeeThroughClip 과 같은 식·같은 전역 이름이다 — ProtoSeeThrough 가
// BCM 머티리얼과 이 머티리얼을 구분하지 않고 그대로 구동한다(전역 값 + 잎 카드는 렌더러 MPB).
// 전역은 UnityPerMaterial 밖에 둔다(안이면 SRP 배처가 끊긴다).
// ---------------------------------------------------------------------------------------------
float4 _BCM_SeeThroughPos;     // xyz = 대상 월드 위치(가슴 높이), w = 1 이면 켜짐
float4 _BCM_SeeThroughParams;  // x = 반지름(화면 높이 비율) y = 가장자리 폭 z = 최소 불투명도 w = 깊이 여유(m)

#if defined(BCM_SEE_THROUGH)
float TFF_Bayer2(float2 a) { a = floor(a); return frac(a.x * 0.5 + a.y * a.y * 0.75); }
float TFF_Bayer4(float2 pixel) { return TFF_Bayer2(pixel * 0.5) * 0.25 + TFF_Bayer2(pixel) + 1.0 / 32.0; }

void TFF_SeeThroughClip(float3 positionWS, float2 pixel)
{
	if (_BCM_SeeThroughPos.w < 0.5) return;
	float4 cp = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
	float4 ct = mul(UNITY_MATRIX_VP, float4(_BCM_SeeThroughPos.xyz, 1.0));
	float2 d = (cp.xy / cp.w - ct.xy / ct.w) * 0.5;
	d.x *= _ScreenParams.x / _ScreenParams.y;
	float r = length(d);
	float inCircle = 1.0 - smoothstep(_BCM_SeeThroughParams.x - _BCM_SeeThroughParams.y, _BCM_SeeThroughParams.x, r);
	float front = saturate((ct.w - cp.w - _BCM_SeeThroughParams.w) * 2.0);
	float opacity = lerp(1.0, _BCM_SeeThroughParams.z, inCircle * front);
	clip(opacity - TFF_Bayer4(pixel));
}
	#define BCM_SEE_THROUGH_CLIP(posWS, pixel) TFF_SeeThroughClip(posWS, pixel)
#else
	#define BCM_SEE_THROUGH_CLIP(posWS, pixel)
#endif

#endif
