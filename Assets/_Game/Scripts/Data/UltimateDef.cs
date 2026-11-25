using UnityEngine;

/// <summary>
/// 필사기(§06 v0.7) 수치 에셋의 <b>공통 부모</b>. 실제 칸은 종류별 자식 클래스에 있다 —
/// 표적 사살(<see cref="UltTargetKillDef"/>) · 불꽃 고리(<see cref="UltFlameRingDef"/>) · 광란 난사(<see cref="UltBarrageDef"/>).
///
/// <para><b>왜 자식 클래스인가.</b> 세 필사기는 공통 칸이 거의 없다(표적 수 · 고리 반경 · 부채 각도).
/// 한 클래스에 다 넣으면 인스펙터에 남의 칸이 2/3 이고, 어느 칸이 내 것인지 메모로 가려야 한다.
/// 자식으로 가르면 에셋마다 제 칸만 보인다. 코드(<c>ProtoUltimate</c>)는 타입으로 행동을 고른다.</para>
///
/// <para><b>강화 I · II · 갈래 A/B</b> 는 트리 선택(<c>UltTree</c>)이 고르고, 선택마다 쓸 숫자가 여기에 나란히 있다
/// (예: <c>damageMul</c> 과 <c>damageMulI</c>). 새 캐릭터의 필사기가 기존 셋과 같은 틀이면 에셋만 복제한다.</para>
/// </summary>
public abstract class UltimateDef : ScriptableObject
{
    [Tooltip("버튼·발동 팝업에 쓰는 이름.")]
    public string displayName;

    [Tooltip("팝업·표시 색. 표적 고리·광선 같은 연출 색으로도 쓴다.")]
    public Color color = Color.white;

    [TextArea(2, 6)]
    [Tooltip("메모.")]
    public string note;

    public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
}
