using UnityEngine;

/// <summary>
/// 불꽃 고리(파이로) — 몸 둘레에 불 고리. 테두리 띠에 닿은 적에게 초당 피해 + 화상.
/// 갈래 A 대화염 = 반경 ↑, 갈래 B 연소 = 끝날 때 화상 걸린 적 전부 폭발.
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/필사기/불꽃 고리", fileName = "ULT_FlameRing")]
public class UltFlameRingDef : UltimateDef
{
    [Header("고리")]
    public float radius = 3.5f;
    [Tooltip("갈래 A(대화염) 반경.")] public float radiusA = 5f;
    [Tooltip("테두리 안쪽으로 이만큼이 판정 띠(m).")] public float band = 1.4f;
    public float duration = 8f;
    [Tooltip("강화 II 지속.")] public float durationII = 10f;
    [Tooltip("판정 틱 간격(초).")] public float tick = 0.25f;

    [Header("피해 (주무기 피해 ×, 초당)")]
    public float damageMul = 3f;
    [Tooltip("강화 I.")] public float damageMulI = 3.5f;

    [Header("갈래 B — 연소")]
    [Tooltip("화상 걸린 적 본체 피해 배율.")] public float combustMul = 12f;
    [Tooltip("터진 자리 둘레 반경(m).")] public float combustSplash = 1.6f;
    [Tooltip("둘레 피해 배율.")] public float combustSplashMul = 4f;
    [Tooltip("폭발 그림 상한 — 판정은 전부, 그림은 앞에서부터.")] public int combustFxMax = 14;

    // 연출은 FX 카탈로그 ult_fire_ring(프리팹 MFX_FireRing)이 맡는다 — 2026-11-13 코드 그림(테두리 선 + 불 입자, 칸 5개)을 뺐다.
    // 도는 속도 · 불꽃 양은 프리팹의 파티클 값에서 고친다(Assets/_Game/Art/Sujin/S_VFX/Shared/SurvivorAce/README_FeiLunFire.md 표).
}
