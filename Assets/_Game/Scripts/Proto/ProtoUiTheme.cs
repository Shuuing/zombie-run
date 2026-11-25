using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 전투 화면 UI 그림체 고르기 — 에셋 <c>Resources/Data/UiTheme.asset</c> 의 <see cref="theme"/> 하나로
    /// 전투 HUD · 일시정지 · 결과 · 설정(<see cref="ProtoSaBattleUiSkin"/>)과 레벨업 3택(<see cref="ProtoSkillSelectSkin"/>)이 함께 바뀐다.
    ///
    /// <para>SurvivorAce = <c>Resources/Data/SaBattleUiSkin</c> · <c>SkillSelectSkin</c>(Survivor Ace 원작 사본, 11-16),
    /// TangTang = <c>Resources/Data/TtBattleUiSkin</c> · <c>TtSkillSelectSkin</c>(탕탕특공대 원작 사본, 11-17).
    /// 고른 쪽 스킨이 없으면 Survivor Ace 쪽을 쓴다. 둘 다 참고용 원작 그림 — 출시 전에 우리 그림으로 바꾼다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Proto/UI Theme", fileName = "UiTheme")]
    public class ProtoUiTheme : ScriptableObject
    {
        public const string ResourcePath = "Data/UiTheme";

        public enum Theme { SurvivorAce, TangTang }

        [Tooltip("전투 화면 UI 그림체. 플레이 중에 바꾸면 다음 실행부터 적용된다")]
        public Theme theme = Theme.TangTang;

        static ProtoUiTheme _loaded;
        static bool _tried;

        public static Theme Current
        {
            get
            {
                if (_loaded == null && !_tried) { _tried = true; _loaded = Resources.Load<ProtoUiTheme>(ResourcePath); }
                return _loaded != null ? _loaded.theme : Theme.SurvivorAce;
            }
        }
    }
}
