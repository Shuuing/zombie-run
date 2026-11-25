using Proto;
using UnityEngine;

/// <summary>
/// 무기 하나의 <b>연출 세트</b> — 쏠 때·맞혔을 때·처치했을 때·터질 때 어느 효과(FX 카탈로그 ID)를 얼마나 크게 낼지.
/// 모양은 카탈로그(프리팹)가, 자리와 때는 코드가, <b>어느 무기가 무엇을 쓰는지</b>는 이 에셋이 정한다.
/// 그래서 무기를 바꾸거나 새로 만들 때 코드를 열지 않는다 — 이 에셋을 복제해 ID 와 배율만 고친다.
///
/// <para>왜 WeaponStats 안에 두지 않나: 수치(밸런스)와 연출(아트)은 바뀌는 이유가 다르다(WeaponModelDef 주석과 같은 원칙).
/// <see cref="WeaponStats.fx"/> 가 이 에셋을 가리키고, 비어 있으면 <see cref="Default"/>(모든 자리가 공통 ID)로 돈다.</para>
///
/// <para>자리 규약(코드가 지킨다): 총구 = 총구점에서 +Z 총열 방향 · 피격 = 닿은 자리에서 +Z 날아온 쪽 · 처치 = 몸 가운데 · 폭발 = 폭심, radius = 판정 반경.
/// scale 1 = 소총 · 잡좀비. 덩치(보스·엘리트)와 치명타 배율은 코드가 곱한다.</para>
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/무기 연출", fileName = "WFX_NewWeapon")]
public class WeaponFxSet : ScriptableObject
{
    [System.Serializable]
    public class Slot
    {
        [Tooltip("FX 카탈로그 ID. 비우면 이 자리는 아무것도 안 낸다")]
        public string fxId;
        [Tooltip("크기 배율. 1 = 기본(소총 · 잡좀비)")]
        public float scale = 1f;

        public Slot() { }
        public Slot(string id, float scale = 1f) { fxId = id; this.scale = scale; }
        public bool On => !string.IsNullOrEmpty(fxId);
    }

    [Header("쏠 때")]
    [Tooltip("총구 화염. 자리 = 총구, +Z = 총열. 발사체 모델이 곧 연출인 무기(로켓)는 비운다")]
    public Slot muzzle = new Slot(FxIds.MuzzleFlash);
    [Tooltip("탄피(ProtoGunFx 코드 그림) 크기. 0 이면 안 나온다. 1 = 소총, 샷건 1.5")]
    public float casingScale = 1f;
    [Tooltip("배출구 = 총구에서 총열을 따라 이만큼 뒤(m). 총이 길면 크게(캐논 0.55)")]
    public float casingPortBack = 0.35f;
    [Tooltip("총구 화염 자리 보정(m). 총구 기준 — z = 총열 앞(+) / 뒤(−), y = 위, x = 오른쪽. 외부 팩 프리팹처럼 원점이 불꽃 가운데인 모양을 총구 끝에 맞출 때 쓴다. 탄피 자리엔 안 걸린다")]
    public Vector3 muzzleOffset;

    /// 총구 화염을 낼 자리 — 총구점에 muzzleOffset 을 총열 방향 기준으로 더한 것. 보정이 없거나 방향이 없으면 총구점 그대로.
    public Vector3 MuzzlePos(Vector3 muzzle, Vector3 aim)
        => muzzleOffset == Vector3.zero || aim.sqrMagnitude < 0.0001f ? muzzle : muzzle + Quaternion.LookRotation(aim) * muzzleOffset;

    [Header("맞혔을 때")]
    [Tooltip("투사체·광선·블레이드가 적에 닿은 자리의 불똥. +Z = 날아온 쪽. 비우면 없음(화염처럼 틱이 잦은 무기)")]
    public Slot hit = new Slot(FxIds.HitSpark);
    [Tooltip("이 무기로 처치했을 때 몸 가운데. 비우면 없음")]
    public Slot kill = new Slot(FxIds.KillBurst);

    [Header("화염 (콘 무기)")]
    [Tooltip("화염 줄기 — 외부 팩 프리팹을 그 모양 그대로 쓸 때 카탈로그 ID(예: flamethrower_jet, ProtoFlamePack). 비우면 코드가 색·크기를 맞추는 FX_FlameJet(ProtoFlameCone). scale = 크기 배율(사거리에 맞춘 뒤 곱한다)")]
    public Slot jet = new Slot(null);

    [Header("꼬리 (몸에서 떨어져 움직이는 부품)")]
    [Tooltip("회전 블레이드처럼 몸 둘레를 도는 부품마다 붙어 다니는 꼬리(스팬). 자리 = 부품 가운데, +Z = 나아가는 쪽, +Y = 위. " +
             "프리팹 안에서 시뮬레이션 공간을 Custom 으로 둔 층은 궤도 루트(플레이어를 따라다님)에 묶여 플레이어가 달려도 원을 그린다. 비우면 꼬리 없음(2026-11-18)")]
    public Slot trail = new Slot(null);

    [Header("터질 때 (aoeRadius 가 있는 무기)")]
    [Tooltip("착탄 폭발. 판정 반경이 radius 로 넘어간다(코드 그림 explosion 은 링 테두리 = 반경)")]
    public Slot explode = new Slot(FxIds.Explosion);
    [Tooltip("가까이서 터지면 작게 — 탄이 총구에서 거의 안 날아가고 터질 때 explode 배율에 곱하는 값. 1 = 거리와 상관없이 같은 크기(기본). " +
             "탱크 포처럼 쏘는 몸 바로 앞에서도 터지는 무기가 자기 몸을 덮지 않게(2026-11-17). 프리팹 효과만 — 코드 그림은 radius 로 그린다")]
    [Range(0.1f, 1f)] public float explodeNearScale = 1f;
    [Tooltip("탄이 이 거리(m) 이상 날아가 터지면 원래 크기. 그 안쪽은 explodeNearScale 에서 부드럽게(smoothstep) 커진다")]
    public float explodeFullDistance = 5f;

    /// 탄이 traveled m 날아가 터졌을 때 explode 배율에 곱할 값(1 = 원래 크기).
    public float ExplodeDistanceScale(float traveled)
    {
        if (explodeNearScale >= 0.999f) return 1f;
        float k = Mathf.SmoothStep(0f, 1f, traveled / Mathf.Max(0.01f, explodeFullDistance));
        return Mathf.Lerp(explodeNearScale, 1f, k);
    }

    [TextArea(2, 4)]
    [Tooltip("메모. 왜 이 값인지.")]
    public string note;

    static WeaponFxSet _default;

    /// 연출 에셋이 없는 무기가 쓰는 기본 세트 — 모든 자리가 공통 ID, 배율 1.
    public static WeaponFxSet Default
    {
        get
        {
            if (_default == null)
            {
                _default = CreateInstance<WeaponFxSet>();
                _default.name = "(기본 연출)";
                _default.hideFlags = HideFlags.HideAndDontSave;
            }
            return _default;
        }
    }

    /// 무기 수치 에셋에 물린 세트, 없으면 기본.
    public static WeaponFxSet Of(WeaponStats st) => st != null && st.fx != null ? st.fx : Default;
}
