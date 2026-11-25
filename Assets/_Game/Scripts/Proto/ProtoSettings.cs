using UnityEngine;

namespace Proto
{
    /// 플레이어 옵션. PlayerPrefs 에 저장되고 다음 실행에 그대로 살아난다.
    ///
    /// 왜 정적 클래스인가: 옵션은 씬을 넘어 유지돼야 하고(로비 → 인게임), 어느
    /// 컴포넌트가 먼저 깨어나는지에 의존하면 안 된다. MonoBehaviour 싱글턴으로 두면
    /// "옵션 오브젝트가 있는 씬" 에서만 동작하는 물건이 된다.
    ///
    /// **항목을 추가하는 법**: 키 상수 + 프로퍼티 + Apply 안의 한 줄. 프로퍼티 setter 가
    /// 저장과 적용을 같이 하므로 부르는 쪽(HUD)은 대입만 하면 된다.
    /// ProtoCameraFollow.shakeEnabled 도 GDD §03 이 접근성 옵션으로 규정한 항목이라
    /// 여기로 올라올 후보다 (지금은 인스펙터 전용).
    public static class ProtoSettings
    {
        const string KeyFog = "proto.opt.fog";
        const string KeySfx = "proto.opt.sfx";
        const string KeyBurn = "proto.opt.burnfx";
        const string KeyRain = "proto.opt.rainlevel";
        const string KeyFlame = "proto.opt.flamestyle";
        const string KeyDmg = "proto.opt.damagenumbers";
        const string KeyDmgStyle = "proto.opt.damagestyle";

        static bool _fog;
        static int _dmgStyle;

        static bool _burn;
        static bool _dmg;
        static int _rain;
        static int _flame;
        static float _sfx;
        static bool _loaded;

        /// 효과음 음량 0~1. **소리별 상대 음량은 여기서 만지지 않는다** — 그것은 파형에
        /// 구워져 있다(synth.py 의 목표 피크 표). 두 군데서 음량을 만지면 어느 쪽이
        /// 기준인지 알 수 없게 된다. 여기는 전체 크기 하나만 쥔다.
        ///
        /// ProtoAudio 는 재생할 때마다 이 값을 읽으므로 Apply 가 필요 없다 —
        /// 슬라이더를 끌면 다음 발사부터 바로 반영되고, 화염 루프는 매 프레임 반영된다.
        public static float SfxVolume
        {
            get { Load(); return _sfx; }
            set
            {
                Load();
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(_sfx, v)) return;
                _sfx = v;
                PlayerPrefs.SetFloat(KeySfx, v);
                PlayerPrefs.Save();
            }
        }

        /// 안개 온/오프. 거리 안개(RenderSettings)와 바닥 안개 시트를 같이 끈다.
        public static bool FogEnabled
        {
            get { Load(); return _fog; }
            set
            {
                Load();
                if (_fog == value) return;
                _fog = value;
                PlayerPrefs.SetInt(KeyFog, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyFog();
            }
        }

        /// 데미지 숫자를 띄울지(ProtoDamageText). 기본 켜짐. 모양은 <see cref="DamageStyle"/>.
        public static bool DamageNumbers
        {
            get { Load(); return _dmg; }
            set
            {
                Load();
                if (_dmg == value) return;
                _dmg = value;
                PlayerPrefs.SetInt(KeyDmg, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// 데미지 숫자 모양. <b>0 = YAZS 식</b>(얇은 숫자 · 치명타 2배 · 맞을 때마다 한 개) · <b>1 = 기존</b>(굵은 숫자 · 같은 대상 피해를 합침).
        /// 번호는 <see cref="ProtoDamageText.Style"/> 과 같다. ProtoDamageText 가 매 프레임 읽으므로 Apply 가 필요 없다.
        public static int DamageStyle
        {
            get { Load(); return _dmgStyle; }
            set
            {
                Load();
                int v = Mathf.Clamp(value, 0, 1);
                if (_dmgStyle == v) return;
                _dmgStyle = v;
                PlayerPrefs.SetInt(KeyDmgStyle, v);
                PlayerPrefs.Save();
            }
        }

        /// 옵션 한 줄이 세 가지를 돈다: YAZS 식 → 기존 → 꺼짐 → YAZS 식.
        public static void CycleDamageNumbers()
        {
            Load();
            if (!_dmg) { DamageStyle = 0; DamageNumbers = true; }
            else if (_dmgStyle == 0) DamageStyle = 1;
            else DamageNumbers = false;
        }

        /// 화면에 보여줄 이름.
        public static string DamageNumbersName
        {
            get { Load(); return !_dmg ? "꺼짐" : (_dmgStyle == 0 ? "YAZS 식" : "기존"); }
        }

        /// 화염에 타는 좀비 몸에 불 입자를 붙일지. 끄면 지금까지처럼 **틴트만** 남는다
        /// (그을린 색 + 이미션은 옵션과 무관하게 항상 나온다 — 무엇이 타는 중인지는
        /// 게임플레이 정보라 끌 수 있으면 안 된다).
        public static bool BurningBodies
        {
            get { Load(); return _burn; }
            set
            {
                Load();
                if (_burn == value) return;
                _burn = value;
                PlayerPrefs.SetInt(KeyBurn, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// 비 세기. <b>0 = 꺼짐 · 1 = 약함(가랑비) · 2 = 강함(폭우)</b>.
        /// 빗줄기는 카메라 앞 쿼드 한 장이라 세기를 바꿔도 비용이 거의 그대로다 —
        /// 시트 텍스처와 불투명도, 바닥 튐 양만 달라진다.
        public static int RainLevel
        {
            get { Load(); return _rain; }
            set
            {
                Load();
                int v = Mathf.Clamp(value, 0, 2);
                if (_rain == v) return;
                _rain = v;
                PlayerPrefs.SetInt(KeyRain, v);
                PlayerPrefs.Save();
            }
        }

        /// 비가 오는가. 세기를 모르는 쪽(연출 켜고 끄기)이 쓴다.
        public static bool RainEnabled
        {
            get { Load(); return _rain > 0; }
            set { RainLevel = value ? 1 : 0; }
        }

        /// 화염방사기 불을 무엇으로 그릴지. <b>0 = 파티클, 1 = 스프라이트</b>.
        /// 판정은 둘 다 건드리지 않는다 — <see cref="ProtoShooter"/> 가 콘 수치로 직접
        /// 계산하므로 어느 쪽을 켜도 데미지는 같다. 순수하게 보기와 비용만 다르다.
        public static int FlameStyle
        {
            get { Load(); return _flame; }
            set
            {
                Load();
                int v = Mathf.Clamp(value, 0, 1);
                if (_flame == v) return;
                _flame = v;
                PlayerPrefs.SetInt(KeyFlame, v);
                PlayerPrefs.Save();
            }
        }

        /// 화면에 보여줄 이름.
        public static string FlameStyleName
        {
            get { Load(); return _flame == 0 ? "파티클" : "스프라이트"; }
        }

        /// 화면에 보여줄 이름.
        public static string RainLevelName
        {
            get { Load(); return _rain == 0 ? "꺼짐" : (_rain == 1 ? "약함" : "강함"); }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _rain = Mathf.Clamp(PlayerPrefs.GetInt(KeyRain, 1), 0, 2);   // 기본값은 약함
            // 기본값은 파티클. 2026-09-18 에 스프라이트 판(ProtoFlameSprite)과 나란히 놓고 봤고,
            // 그림이 마음에 안 든다는 판단으로 파티클을 유지하기로 했다. 비용은 둘이 사실상 같다
            // (지금 밀도에서는 양쪽 다 측정 잡음 안 — Scripts/Proto/README.md 의 표).
            _flame = Mathf.Clamp(PlayerPrefs.GetInt(KeyFlame, 0), 0, 1);
            _burn = PlayerPrefs.GetInt(KeyBurn, 1) != 0;   // 기본값은 켜짐
            _dmg = PlayerPrefs.GetInt(KeyDmg, 1) != 0;     // 기본값은 켜짐
            _dmgStyle = Mathf.Clamp(PlayerPrefs.GetInt(KeyDmgStyle, 0), 0, 1);   // 기본값은 YAZS 식(2026-11-10)
            _fog = PlayerPrefs.GetInt(KeyFog, 1) != 0;   // 기본값은 켜짐
            // 0.8 로 시작한다. 1.0 은 물량전에서 탄착·신음이 겹쳐 쌓일 때 여유가 없다.
            _sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, 0.8f));
        }

        /// 저장된 옵션을 씬에 반영한다. 런 시작 시 ProtoRun 이 한 번 부른다.
        public static void ApplyAll()
        {
            ApplyFog();
        }

        /// 안개 두 갈래를 같이 끈다 — 거리 안개만 꺼도 바닥 안개 시트가 남고,
        /// 시트만 꺼도 원경이 계속 뿌옇다. 둘은 한 스위치여야 한다.
        public static void ApplyFog()
        {
            // 에디터 편집 모드에서는 손대지 않는다. RenderSettings.fog 는 **씬에 저장되는 값**이라
            // 여기서 끄면 플레이어 개인 설정이 씬 파일에 박혀 팀 전체에 커밋된다.
            // 편집 모드의 안개 값은 SceneLookSetup 이 단독으로 소유한다.
            if (!Application.isPlaying) return;

            bool on = FogEnabled;
            RenderSettings.fog = on;

            // 비활성 오브젝트까지 찾는다 — 이전 실행에서 꺼진 채로 저장됐을 수 있다.
            var fog = Object.FindObjectOfType<ProtoGroundFog>(true);
            if (fog != null) fog.SetVisible(on);
        }
    }
}
