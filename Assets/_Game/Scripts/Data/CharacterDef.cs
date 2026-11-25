using UnityEngine;

/// <summary>
/// 플레이어 캐릭터 1명 — <b>무엇을 들고, 어떤 필사기를 쓰고, 어떻게 생겼나</b>를 잇는 줄.
///
/// <para>예전에는 이 연결이 세 곳에 흩어져 있었다: 프리팹 배리언트를 굽는 에디터 코드의 표(머티리얼·무기),
/// 교체 패널의 프리팹 배열(순서·이름), 필사기 코드의 이름 검사(<c>name.Contains("Rookie")</c>).
/// 캐릭터를 하나 더 만들려면 셋을 다 고쳐야 했고 하나는 반드시 빠졌다. 이제 이 에셋 하나가 진실이다.</para>
///
/// <para>전투 수치(체력·이동)는 아직 모든 캐릭터가 같아서(<c>ProtoConfig</c>) 여기 없다.
/// 캐릭터마다 달라지는 순간 여기에 칸을 더한다.</para>
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/캐릭터", fileName = "C_NewCharacter")]
public class CharacterDef : ScriptableObject
{
    [Tooltip("기획서 가칭. 교체 패널·로그에 쓴다.")]
    public string displayName;

    [Tooltip("프리팹 배리언트(Char_01_Rookie …). 비어 있으면 '캐릭터 프리팹 만들기' 메뉴가 베이스에서 만들어 채운다.")]
    public GameObject prefab;

    [Tooltip("몸 머티리얼. 프리팹을 구울 때 베이스의 몸 렌더러(Object137)에 입힌다.")]
    public Material bodyMaterial;

    [Tooltip("주무기. 비우면 손에 드는 무기가 없는 캐릭터.")]
    public WeaponModelDef weapon;

    [Tooltip("필사기. 비우면 버튼이 '준비 중'으로 잠긴다.")]
    public UltimateDef ultimate;

    [TextArea(2, 5)]
    [Tooltip("메모. 왜 이 무기인지 등.")]
    public string note;

    /// 패널·로그에 쓸 이름. displayName 이 비어 있으면 에셋 이름.
    public string Label => string.IsNullOrEmpty(displayName) ? name : displayName;
}
