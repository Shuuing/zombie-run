using UnityEngine;

/// <summary>
/// 광란 난사(브레이커) — 앞쪽 부채꼴에 산탄을 쏟아붓고 마지막에 한 번 크게. 평소 자동 사격은 그동안 쉰다.
/// 갈래 A 확산 = 부채 ↑, 갈래 B 집중 = 부채 ↓ · 관통 +.
/// 탄은 주무기(산탄총) 수치 사본에서 아래 '난사 탄' 칸만 바꿔 쓴다 — 에셋은 공유 수치라 건드리지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/필사기/광란 난사", fileName = "ULT_Barrage")]
public class UltBarrageDef : UltimateDef
{
    [Header("난사")]
    public float duration = 3f;
    [Tooltip("강화 II 지속.")] public float durationII = 4f;
    [Tooltip("회차 간격(초). 0.06 = 초당 약 17회.")] public float gap = 0.06f;
    [Tooltip("회차당 펠릿 수.")] public int pellets = 9;
    [Tooltip("잠근 방향에서 좌우로 흔드는 폭(도).")] public float sweep = 14f;
    [Tooltip("발동 순간 적이 가장 많은 쪽을 고르는 거리(m).")] public float aimRange = 10f;

    [Header("부채 (도)")]
    public float spread = 70f;
    [Tooltip("갈래 A(확산).")] public float spreadA = 110f;
    [Tooltip("갈래 B(집중).")] public float spreadB = 35f;
    [Tooltip("갈래 B 관통 추가.")] public int pierceB = 3;

    [Header("피해 (주무기 피해 ×)")]
    public float damageMul = 1f;
    [Tooltip("강화 I.")] public float damageMulI = 1.3f;

    [Header("마무리")]
    [Tooltip("마지막 한 번의 펠릿 수.")] public int finalePellets = 30;
    [Tooltip("마무리 피해 배율(난사 피해 ×).")] public float finaleDamageMul = 1.5f;
    [Tooltip("마무리 총구 화염 배율.")] public float finaleFlash = 2.6f;
    [Tooltip("3회차마다 총구 화염을 이만큼 키운다.")] public float bigFlashEvery3 = 1.8f;

    [Header("난사 탄 (산탄총 사본에서 바꾸는 값)")]
    public float projectileSpeed = 24f;
    [Tooltip("탄 수명(초). 0.42 ≈ 10m.")] public float projectileLife = 0.42f;
    [Tooltip("평소(2.2)대로 밀면 첫 줄이 날아가 뒤 줄이 안 맞는다.")] public float knockbackMult = 0.8f;
    public float tracerLength = 1.6f;
    [Tooltip("예광탄 굵기 배율.")] public float tracerWidthMul = 1.4f;
    [Tooltip("예광탄 색. 비우면(알파 0) 필사기 색.")] public Color tracerColor = new Color(1f, 0.78f, 0.35f, 1f);
}
