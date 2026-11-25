using UnityEngine;

/// <summary>
/// 캐릭터 명단 — 순서가 곧 숫자키 순서(1~6). <c>Resources/Data/CharacterTable.asset</c> 하나.
/// 교체 패널(<c>ProtoCharacterSwitcher</c>)·필사기(<c>ProtoUltimate</c>)·프리팹 굽기 메뉴가 전부 여기서 읽는다.
/// </summary>
[CreateAssetMenu(menuName = "좀비 오버런/캐릭터 테이블", fileName = "CharacterTable")]
public class CharacterTable : ScriptableObject
{
    public const string ResourcePath = "Data/CharacterTable";

    [Tooltip("순서 = 숫자키 순서. 1번이 씬 원본(기본) 캐릭터다.")]
    public CharacterDef[] roster;

    public int Count => roster != null ? roster.Length : 0;

    public CharacterDef At(int i) => roster != null && i >= 0 && i < roster.Length ? roster[i] : null;

    static CharacterTable _loaded;
    static bool _tried;

    public static CharacterTable I
    {
        get
        {
            if (_loaded == null && !_tried)
            {
                _tried = true;
                _loaded = Resources.Load<CharacterTable>(ResourcePath);
                if (_loaded == null) Debug.LogError("[캐릭터 테이블] Resources/" + ResourcePath + " 이 없다 — 교체 패널이 비고 필사기가 잠긴다.");
            }
            return _loaded;
        }
    }

    public static void Reload() { _loaded = null; _tried = false; }
}
