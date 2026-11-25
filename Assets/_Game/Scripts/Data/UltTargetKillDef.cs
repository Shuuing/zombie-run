using UnityEngine;

/// <summary>
/// 표적 사살(루키) — 화면 안 적을 골라 표시하고 한 발씩 관통탄을 날린다(2026-11-18 까지는 즉발 광선).
/// 피해 = 주무기 피해 × 배율. 갈래 A 제압 = 표적 수 ↑, 갈래 B 저격 = 1체 × 저격 배율.
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/필사기/표적 사살", fileName = "ULT_TargetKill")]
public class UltTargetKillDef : UltimateDef
{
    [Header("표적")]
    [Tooltip("기본 표적 수.")] public int targets = 5;
    [Tooltip("갈래 A(제압) 표적 수.")] public int targetsA = 8;
    [Tooltip("갈래 B(저격) 표적 수.")] public int targetsB = 1;
    [Tooltip("표시 → 사격까지(초).")] public float markTime = 0.35f;
    [Tooltip("한 발과 다음 발 사이(초). 한 번에 다 그으면 몇 발인지 안 읽힌다.")] public float shotGap = 0.07f;

    [Header("탄 (2026-11-18 — 날아가는 탄, 그림은 FX 카탈로그 ult_target_kill_bullet)")]
    [Tooltip("탄 속도(m/s). 닿는 순간 피해가 들어간다 — 화면 끝 표적(약 8m)까지 0.25초쯤.")] public float bulletSpeed = 32f;
    [Tooltip("표적을 뚫고 더 가는 거리(m). 0 = 몸 표면에 맞는 자리에서 히트(ult_target_kill_hit)와 함께 사라진다(11-18 사용자 \"너무 통과하는 것 같다\" → 0). 판정은 표적 하나뿐이다.")] public float bulletPierce = 0f;

    [Header("피해 (주무기 피해 ×)")]
    public float damageMul = 25f;
    [Tooltip("강화 I.")] public float damageMulI = 30f;
    [Tooltip("갈래 B 저격 — 한 발에 추가로 곱한다.")] public float snipeMul = 3f;

    [Header("강화 II — 표적 주변 파편")]
    public float shrapnelRadius = 1.5f;
    public float shrapnelMul = 5f;
}
