using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 전투 화면 UI(<see cref="ProtoSaBattleUi"/>)에 쓸 프리팹 셋. 에셋: <c>Resources/Data/SaBattleUiSkin.asset</c>(파일 자리 <c>Assets/_Reference/SurvivorAce/UI/Resources/Data</c> — GitHub 에 없어 받은 프로젝트에선 탕탕 판만).
    /// 없거나 <see cref="useThisUi"/> 가 꺼져 있으면 ProtoHUD 의 예전 코드 HUD · 결과 · 옵션 화면이 그대로 뜬다.
    ///
    /// <para>지금 값은 Survivor Ace 원작 사본(<c>Assets/_Reference/SurvivorAce/UI/Battle — GitHub 에 없음</c>)을 가리킨다 — 출시 전에 우리 그림으로 바꾼다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Proto/Battle UI Skin", fileName = "SaBattleUiSkin")]
    public class ProtoSaBattleUiSkin : ScriptableObject
    {
        public const string ResourcePath = "Data/SaBattleUiSkin";

        [Tooltip("끄면 예전 코드 HUD(ProtoHUD.BuildHud) · 결과 · 옵션 화면으로 돌아간다")]
        public bool useThisUi = true;

        [Tooltip("전투 HUD — 경험치 · 시간 · 처치 수 · 일시정지 버튼 · 체력/연료 · 보스(엘리트) 바 · 경고 띠. " +
                 "UI_ZombieHUD = 예전 HUD 자리(11-16 2판), UI_ZombieHUD_v1 = 원작 배치(체력 바가 발밑을 따라다님)")]
        public GameObject hudPrefab;
        [Tooltip("일시정지 화면 — 보유 무기/강화 · 계속하기 · 다시 시작 · 설정(예전 옵션 창을 연다)")]
        public GameObject pausePrefab;
        [Tooltip("결과 화면 — 생존 성공/전투 실패 · 생존 시간 · 처치 수 · 획득한 강화")]
        public GameObject resultPrefab;
        [Tooltip("설정 창(원작 전투 설정 틀) — 일시정지의 톱니로 연다. 비우면 예전 코드 옵션 창")]
        public GameObject settingsPrefab;

        [Tooltip("원작 배치(v1)에서만: 주인공 체력 바를 발밑에서 얼마나 내릴지(캔버스 단위)")]
        public float heroBarOffset = 70f;
        [Tooltip("캐릭터 발밑 체력 바(HUD 프리팹에 Follow/FeetHpBar 가 있을 때 — 탕탕 판)를 발에서 얼마나 내릴지(캔버스 단위, 바 위 끝 기준)")]
        public float feetBarOffset = 34f;
        [Tooltip("원작 배치(v1)에서만: 엘리트 체력 바 높이(엘리트 발 위치에서 월드 단위로 위로)")]
        public float eliteBarHeight = 2.6f;
        [Tooltip("체력이 이 비율 아래면 화면 가장자리가 붉게 깜빡인다(0 이면 끔)")]
        [Range(0f, 1f)] public float lowHpRatio = 0.3f;

        static ProtoSaBattleUiSkin _loaded;
        static bool _tried;

        /// 탕탕특공대 판 스킨(같은 클래스, 프리팹만 다름 — UI_TTHUD 등).
        public const string TangTangPath = "Data/TtBattleUiSkin";

        /// 지금 그림체(<see cref="ProtoUiTheme"/>)의 스킨. 탕탕을 골랐는데 탕탕 스킨이 없으면 Survivor Ace 스킨.
        public static ProtoSaBattleUiSkin Load()
        {
            if (_loaded == null && !_tried)
            {
                _tried = true;
                if (ProtoUiTheme.Current == ProtoUiTheme.Theme.TangTang) _loaded = Resources.Load<ProtoSaBattleUiSkin>(TangTangPath);
                if (_loaded == null) _loaded = Resources.Load<ProtoSaBattleUiSkin>(ResourcePath);
            }
            return _loaded;
        }
    }
}
