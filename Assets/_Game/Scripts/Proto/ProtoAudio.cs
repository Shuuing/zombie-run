using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 효과음 재생기. <b>씬 배선이 필요 없다</b> — 처음 호출될 때 자기 GameObject 를 만들어 붙는다
    /// (<see cref="ProtoLevelUpBeam"/>·<see cref="ProtoExplosion"/> 과 같은 방식).
    ///
    /// <para><b>왜 AudioSource 를 오브젝트마다 달지 않는가.</b> 좀비가 260마리까지 살아 있고
    /// 초당 수십 번 탄착 판정이 난다(§16 물량전). 개체마다 AudioSource 를 두면 그 수만큼
    /// 보이스를 잡고, 같은 프레임에 40발이 맞으면 40개가 동시에 울려 소리가 찌그러진다.
    /// 여기서 <b>정해진 수의 보이스를 돌려쓰고</b>, 소리마다 최소 간격과 동시 발음 수를
    /// <see cref="Policy"/> 표로 제한한다. 이 표가 곧 이 게임의 믹스 설계다.</para>
    ///
    /// <para><b>왜 3D 사운드가 아닌가.</b> 카메라는 피치 58° 로 내려보고 지면 15.3~25.0m 띠만
    /// 화면에 들어온다. Unity 3D 감쇠는 리스너 기준 구면이라 이 구도에서는 화면 안에 있는
    /// 소리가 거리 때문에 작아진다. 전부 2D 로 재생하고 <b>플레이어와의 수평 거리</b>로만
    /// 음량·좌우를 계산한다 — 화면에 보이면 들린다가 보장된다.</para>
    ///
    /// <para><b>키와 변형.</b> `Resources/SFX` 의 파일 이름에서 뒤의 `_숫자` 를 떼면 키가 된다.
    /// `hit_1 hit_2 hit_3` → 키 `hit` 의 변형 3개. 재생할 때마다 <b>직전과 다른 변형</b>을 골라
    /// 같은 파형이 연속으로 나오는 기계적인 느낌을 없앤다. 파일을 추가하면 코드를 고치지
    /// 않아도 변형이 늘어난다.</para>
    /// </summary>
    public class ProtoAudio : MonoBehaviour
    {
        // ── 보이스 수 ────────────────────────────────────────────────────
        // 24개. 모바일에서 실제로 동시에 섞을 수 있는 채널 수의 안전한 상한이고,
        // 이보다 늘려도 물량전에서는 어차피 서로를 가려 더 들리지 않는다.
        const int VoiceCount = 24;

        // 화면에 들어오는 지면 띠의 바깥쪽 경계(25m)보다 조금 넉넉하게 둔다.
        // 이 거리를 넘으면 아예 재생하지 않는다 — 보이지 않는 곳의 소리에 보이스를 쓰면
        // 정작 눈앞의 탄착음이 상한에 밀려 잘린다.
        // **감쇠는 거의 몸에 붙은 지점부터 시작해야 한다.** 예전에는 6m 까지 감쇠가 0 이었는데,
        // 좀비는 0~11m 에 몰려 있어서(실측: 5m 안 4마리 · 5~11m 11마리) **몬스터 소리 대부분이
        // 감쇠 구간에 들어가지도 않았다** — 곡선을 고쳐도 "다 크게 들린다"가 그대로였다.
        // 접촉 사거리가 0.85m(EnemyDef.contactRange)이므로 붙어서 때리는 좀비는 여전히 원본이다.
        const float NearRadius = 2.5f;  // 여기까지는 감쇠 없음 = '원본 음량'
        // 아레나 반폭이 19m 이고 화면에 보이는 것은 플레이어 기준 ~15m 까지다.
        // 20m 밖은 화면 밖이므로 아예 재생하지 않는다(보이스를 눈앞의 소리에 남긴다).
        const float FarRadius  = 20f;   // 여기서 0

        // 참고 — 실제 음량 (아래 (1-t)² 곡선):
        //   1m 100% · 4m 84% · 6m 64% · 9m 40% · 11m 26% · 14m 12% · 17m 3% · 20m 0%

        // 신음 앰비언스가 "주변에 좀비가 있다"로 읽히려면 그 반경이 **화면에 보이는 범위**여야
        // 한다. 카메라가 보는 지면 띠(15.3~25.0m)는 카메라 기준 거리이고, 플레이어를 중심으로
        // 보면 대략 이 정도가 화면 안이다. FarRadius(28m)를 쓰면 시야 밖 스폰 링이 전부 들어온다.
        const float GroanRadius = 11f;

        /// <summary>소리 한 종류의 재생 규칙. 이 표가 믹스 설계다.</summary>
        struct Policy
        {
            /// 같은 키를 다시 재생할 수 있게 되기까지의 최소 간격(초). 물량전에서 같은
            /// 소리가 프레임마다 겹쳐 쌓이는 것을 막는다.
            public float cooldown;
            /// 이 키가 동시에 잡을 수 있는 보이스 수.
            public int maxVoices;
            /// 재생마다 피치를 ±이 비율로 흔든다. 같은 파일이 반복돼도 다르게 들린다.
            public float pitchJitter;
            /// 거리 감쇠를 적용하지 않는다(UI·결과·레벨업처럼 화면 전체에 속하는 소리).
            public bool flat;

            /// <summary>
            /// 이 소리가 닿는 거리의 배율(<see cref="FarRadius"/> 에 곱한다). 1 이 기본이다.
            /// 큰 소리는 실제로 더 멀리 간다 — 폭발이 소총 탄착과 같은 속도로 작아지면
            /// 로켓을 최대 사거리(11m)에 꽂았을 때 남의 일처럼 들린다.
            /// </summary>
            public float carry;

            public Policy(float cd, int max, float jitter, bool flat = false, float carry = 1f)
            { cooldown = cd; maxVoices = max; pitchJitter = jitter; this.flat = flat; this.carry = carry; }
        }

        // 값의 근거를 같이 적어 둔다 — 나중에 "왜 이 소리만 안 들리지" 를 표에서 답할 수 있어야 한다.
        static readonly Dictionary<string, Policy> Policies = new Dictionary<string, Policy>
        {
            // 탄착: 가장 자주 나는 소리. 30ms 간격·5보이스면 초당 최대 33회까지만 들린다.
            // 더 열어주면 관통·산탄이 한 프레임에 20발을 맞출 때 소리가 벽처럼 뭉친다.
            ["hit"]         = new Policy(0.030f, 5, 0.10f),
            ["crit"]        = new Policy(0.060f, 3, 0.06f),

            // 총성: 발사 간격(§05 0.35~1.7초)이 이미 빈도를 정한다. 쿨다운은 같은 프레임에
            // 두 번 불리는 사고만 막는 값이면 된다. 산탄총은 펠릿마다 부르지 않게 주의.
            ["rifle"]       = new Policy(0.020f, 3, 0.05f),
            // 캐논만 예외적으로 넉넉하다. 발사 간격이 0.08초(초당 12.5발)인데 클립이 340ms 라
            // 늘 4~5발이 겹친다. **겹치는 것이 정상이다** — 참고 녹음(초당 29발)도 꼬리가
            // 250ms 넘게 울리며 겹치고, 그 겹침이 연사의 질감을 만든다. 예전에 클립을 130ms 로
            // 짧게 깎아 겹침을 피하려 했더니 "퉁퉁퉁" 하는 소리가 됐다.
            ["cannon"]      = new Policy(0.015f, 6, 0.06f),
            ["shotgun"]     = new Policy(0.040f, 2, 0.04f),
            ["rocket_fire"] = new Policy(0.060f, 2, 0.03f),

            // 보조 장비: 주무기 위에 계속 겹치는 소리라 가장 인색하게 준다(§07).
            ["blade"]       = new Policy(0.110f, 2, 0.12f),
            ["drone"]       = new Policy(0.070f, 2, 0.10f),

            // 좀비: 발성은 탄착보다 드물게. 260마리가 동시에 죽어도 3보이스를 넘지 않는다.
            ["zattack"]     = new Policy(0.080f, 3, 0.12f),
            ["zhurt"]       = new Policy(0.110f, 3, 0.12f),
            ["zdie"]        = new Policy(0.090f, 3, 0.10f),
            ["zgroan"]      = new Policy(0.700f, 2, 0.15f),
            ["boss_roar"]   = new Policy(0.500f, 1, 0.03f, flat: true),

            // 폭발만 멀리 간다(carry 1.7 → 34m 에서 0). 로켓 사거리가 11m 라 기본 곡선으로는
            // 최대 사거리 착탄이 26% 로 떨어져 남의 일처럼 들린다. 1.7 이면 11m 에서 55% 다.
            ["explosion"]   = new Policy(0.050f, 3, 0.08f, carry: 1.7f),

            // 플레이어 피격: 무적 시간(§03)이 이미 0.5초 간격을 보장하지만, 여기서도
            // 1보이스로 묶는다. 두 겹이 되면 저역이 배가 되어 화면이 먹먹해진다.
            ["player_hurt"] = new Policy(0.250f, 1, 0.05f, flat: true),
            // 브레이커 필사기 광소(광란 난사) — 한 번에 하나, 겹치면 웃음이 뭉개진다. 화면 전체 소리(거리 감쇠 없음)
            ["laugh"]       = new Policy(1.000f, 1, 0.03f, flat: true),

            ["gem"]         = new Policy(0.030f, 4, 0.03f),
            ["step"]        = new Policy(0.100f, 2, 0.08f),

            // 캐릭터 교체 연출 (§04). 둘 다 플레이어 발밑에서 나고 교체할 때만 울리므로
            // 빈도 걱정이 없다 — 1보이스로 묶어 겹침만 막는다.
            // 번개는 화면 전체에 속하는 사건이라 flat(거리 감쇠 없음), 착지는 몸에서 나는
            // 소리라 거리 감쇠를 받는다(플레이어 몸에서 나니 실질적으로 늘 최대 음량이다).
            ["thunder"]     = new Policy(0.300f, 1, 0.05f, flat: true),
            ["land"]        = new Policy(0.200f, 1, 0.07f),
            ["chute"]       = new Policy(0.300f, 1, 0.05f),   // 인트로 낙하산 펴짐 '펑'(예전 land 소리) — 한 판에 한 번

            // 화면 전체에 속하는 소리 — 거리 감쇠 없음.
            ["levelup"]     = new Policy(0.300f, 1, 0.00f, flat: true),
            ["gameover"]    = new Policy(0.500f, 1, 0.00f, flat: true),
            ["victory"]     = new Policy(0.500f, 1, 0.00f, flat: true),
            ["ui_click"]    = new Policy(0.030f, 2, 0.04f, flat: true),
        };

        static readonly Policy DefaultPolicy = new Policy(0.05f, 3, 0.06f);

        // ── 인스턴스 ─────────────────────────────────────────────────────
        static ProtoAudio _instance;
        static bool _failed;   // 뱅크를 못 찾았으면 매 호출마다 다시 시도하지 않는다

        /// <summary>
        /// 정적 상태를 판마다 초기화한다. Enter Play Mode 의 도메인 리로드를 끄면
        /// static 이 에디터 세션 내내 살아남으므로, 한 번 <see cref="_failed"/> 가 서면
        /// 뱅크를 고친 뒤에도 그 에디터에서는 영영 소리가 안 난다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            _failed = false;
        }

        readonly Dictionary<string, AudioClip[]> _bank = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> _lastVariant = new Dictionary<string, int>();
        readonly Dictionary<string, float> _nextAllowed = new Dictionary<string, float>();

        AudioSource[] _voices;
        string[] _voiceKey;
        float[] _voiceFreeAt;
        float[] _voiceBase;   // 덕킹 전 원래 음량(재생 중에도 덕킹을 다시 곱하려고)
        bool[] _voiceNoDuck;  // 덕킹 예외(필사기 자신이 내는 소리)

        // ── 덕킹: 필사기 컷인 중에 다른 효과음을 줄이던 장치. 컷인을 2026-10-29 에 빼서 지금은 1 고정(줄이지 않음).
        // 소리를 잠깐 깔아야 할 연출이 다시 생기면 _duck 을 움직이면 된다(재생 중인 소리·루프에 이미 곱해진다).
        float _duck = 1f;

        AudioSource _loopSource;
        string _loopKey;
        float _loopTarget, _loopVolume;

        Transform _listenPoint;   // 플레이어. 없으면 카메라.
        Camera _cam;

        // 젬 계단 — 연속으로 주울 때 음이 올라간다. 뱀서류의 관습적인 처리로,
        // 물량 픽업이 소음이 아니라 상승감으로 들리게 한다.
        int _ladder;
        float _ladderResetAt;

        // 좀비 신음 앰비언스. 좀비 스크립트가 아니라 여기서 굴린다 —
        // 개체가 각자 울면 260마리가 각자 타이머를 돌리고, 결국 상한에 걸려 잘린다.
        float _nextGroan;

        static ProtoAudio Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (_failed) return null;
                // **플레이 중에만 만든다.** 에디터 편집 모드에서 Play() 가 불리면(툴·테스트 코드가
                // 부를 수 있다) 씬에 속하지 않는 `_ProtoAudio` 가 만들어져 플레이 모드를 나가도
                // 사라지지 않는다. 실제로 그 상태가 됐었다 — AudioListener 를 두 개 들고 있는
                // 유령 오브젝트가 남아 소리가 이상해진다. 편집 모드의 미리듣기는
                // `좀비 오버런/효과음 들어보기` 창이 담당한다.
                if (!Application.isPlaying) return null;

                var go = new GameObject("_ProtoAudio");
                // 실수로 씬에 저장되지 않게 못 박는다. 저장되면 다음 실행에 두 벌이 된다.
                go.hideFlags = HideFlags.DontSave;
                _instance = go.AddComponent<ProtoAudio>();
                return _failed ? null : _instance;
            }
        }

        // ── 정적 진입점 ──────────────────────────────────────────────────
        /// <summary>월드 좌표에서 한 번 재생한다. 플레이어와의 수평 거리로 음량·좌우가 정해진다.</summary>
        /// <param name="ignoreDuck">필사기 컷인 덕킹을 받지 않는다(그 필사기가 만든 소리 — 융단 폭격 폭발 등).</param>
        public static void Play(string key, Vector3 pos, float volScale = 1f, bool ignoreDuck = false)
        {
            var i = Instance;
            if (i != null) i.PlayInternal(key, pos, volScale, positional: true, ignoreDuck: ignoreDuck);
        }

        /// <summary>화면 전체에 속하는 소리(UI·레벨업·결과). 거리 감쇠가 없다.</summary>
        public static void Play2D(string key, float volScale = 1f)
        {
            var i = Instance;
            if (i != null) i.PlayInternal(key, Vector3.zero, volScale, positional: false);
        }

        /// <summary>
        /// 연속으로 부를 때 음이 한 단씩 올라간다(젬 획득). 잠깐 끊기면 처음 음으로 돌아간다.
        /// </summary>
        public static void PlayLadder(string key, Vector3 pos, float volScale = 1f)
        {
            var i = Instance;
            if (i == null) return;
            if (Time.unscaledTime > i._ladderResetAt) i._ladder = 0;
            i._ladderResetAt = Time.unscaledTime + 1.1f;
            i.PlayInternal(key, pos, volScale, positional: true, variant: i._ladder);
            i._ladder++;
        }

        /// <summary>
        /// 지속음을 켜고 끈다(화염방사기). 한 번에 하나만 돌아간다 — 주무기는 1종이므로
        /// 두 개가 동시에 필요한 경우가 없고, 그 전제를 코드로 굳혀 두면 실수로 겹치지 않는다.
        ///
        /// <para>0.1초마다 한 발씩 재생하는 방식으로는 화염이 잘게 끊긴다. 루프 클립 하나를
        /// 페이드로 켜고 끄는 것이 유일하게 "계속 지지는" 소리를 만든다.</para>
        /// </summary>
        public static void SetLoop(string key, bool on, float volScale = 1f)
        {
            var i = Instance;
            if (i == null) return;
            i.SetLoopInternal(key, on, volScale);
        }

        /// <summary>런을 다시 시작할 때 울리던 소리를 전부 끊는다.</summary>
        public static void StopAll()
        {
            var i = _instance;
            if (i == null) return;
            for (int v = 0; v < i._voices.Length; v++)
            {
                i._voices[v].Stop();
                i._voiceKey[v] = null;
                i._voiceFreeAt[v] = 0f;
            }
            i.SetLoopInternal(null, false, 0f);
            i._nextAllowed.Clear();
            i._ladder = 0;
        }

        // ── 준비 ─────────────────────────────────────────────────────────
        void Awake()
        {
            if (_instance == null) _instance = this;
            LoadBank();
            if (_failed) { Destroy(gameObject); return; }
            BuildVoices();
            EnsureListener();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        /// <summary>
        /// `Resources/SFX` 를 통째로 읽어 키별 변형 배열로 묶는다.
        ///
        /// <para><b>왜 Resources 인가.</b> 프로토 씬은 <see cref="ProtoSceneBuilder"/> 가 만들고,
        /// 인스펙터에 클립 43개를 물리려면 씬을 다시 생성해야 한다. 지금 그 실행은 다른 변경을
        /// 끌고 온다. 이름으로 찾는 방식이면 파일을 넣는 것만으로 끝난다 — 프로토 단계의
        /// 선택이고, 실제 빌드에서는 AudioMixer + 뱅크 에셋으로 옮겨야 한다.</para>
        /// </summary>
        void LoadBank()
        {
            var clips = Resources.LoadAll<AudioClip>("SFX");
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning("[오디오] Assets/_Game/Resources/SFX 에 클립이 없다 — 효과음 없이 진행한다. "
                               + "synth.py / hf_post.py 를 돌려 뱅크를 만들고 Unity 를 새로 고쳐라.");
                _failed = true;
                return;
            }

            var groups = new Dictionary<string, List<AudioClip>>();
            foreach (var c in clips)
            {
                string key = StripVariant(c.name);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<AudioClip>();
                list.Add(c);
            }
            foreach (var kv in groups)
            {
                // 변형 순서를 이름으로 고정한다. LoadAll 의 순서는 보장되지 않는데,
                // 젬 계단(gem_1 → gem_2 → gem_3)은 순서가 곧 음높이라 흔들리면 안 된다.
                kv.Value.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                _bank[kv.Key] = kv.Value.ToArray();
            }
            Debug.Log($"[오디오] 클립 {clips.Length}개 / 키 {_bank.Count}종 적재");
        }

        /// `hit_2` → `hit`, `flame_loop` → `flame_loop` (뒤가 숫자일 때만 떼어낸다).
        static string StripVariant(string name)
        {
            int u = name.LastIndexOf('_');
            if (u <= 0 || u == name.Length - 1) return name;
            for (int i = u + 1; i < name.Length; i++)
                if (name[i] < '0' || name[i] > '9') return name;
            return name.Substring(0, u);
        }

        void BuildVoices()
        {
            _voices = new AudioSource[VoiceCount];
            _voiceKey = new string[VoiceCount];
            _voiceFreeAt = new float[VoiceCount];
            _voiceBase = new float[VoiceCount];
            _voiceNoDuck = new bool[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var go = new GameObject($"voice{i:00}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;        // 2D — 거리는 우리가 계산한다
                src.bypassReverbZones = true;
                src.dopplerLevel = 0f;
                _voices[i] = src;
            }

            var lgo = new GameObject("loop");
            lgo.transform.SetParent(transform, false);
            _loopSource = lgo.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            _loopSource.spatialBlend = 0f;
            _loopSource.bypassReverbZones = true;
            _loopSource.volume = 0f;
        }

        /// <summary>
        /// 리스너가 없으면 아무 소리도 나지 않는다. 씬에 따라 카메라에 붙어 있지 않을 수 있고,
        /// 그 경우 증상이 "코드는 다 도는데 소리만 없음" 이라 원인을 찾기 어렵다 — 여기서 못 박는다.
        /// </summary>
        void EnsureListener()
        {
            if (FindObjectOfType<AudioListener>() != null) return;
            var cam = Camera.main;
            var host = cam != null ? cam.gameObject : gameObject;
            host.AddComponent<AudioListener>();
            Debug.Log($"[오디오] AudioListener 가 없어 {host.name} 에 붙였다");
        }

        Transform ListenPoint()
        {
            if (_listenPoint != null) return _listenPoint;
            var p = FindObjectOfType<ProtoPlayer>();
            if (p != null) _listenPoint = p.transform;
            else
            {
                if (_cam == null) _cam = Camera.main;
                if (_cam != null) _listenPoint = _cam.transform;
            }
            return _listenPoint;
        }

        // ── 재생 ─────────────────────────────────────────────────────────
        void PlayInternal(string key, Vector3 pos, float volScale, bool positional, int variant = -1, bool ignoreDuck = false)
        {
            if (string.IsNullOrEmpty(key) || volScale <= 0.0001f) return;
            if (!_bank.TryGetValue(key, out var clips) || clips.Length == 0) return;

            float master = ProtoSettings.SfxVolume;
            if (master <= 0.0001f) return;

            var pol = Policies.TryGetValue(key, out var p) ? p : DefaultPolicy;

            // 쿨다운·타이머는 전부 unscaledTime 이다. 레벨업 카드와 옵션 창이 timeScale 을 0 으로
            // 내리는데(§07), Time.time 을 쓰면 그 화면에서 UI 클릭 소리가 한 번 난 뒤 영영 막힌다.
            float now = Time.unscaledTime;
            if (_nextAllowed.TryGetValue(key, out float allowed) && now < allowed) return;

            float vol = master * volScale;
            float pan = 0f;
            if (positional && !pol.flat)
            {
                var lp = ListenPoint();
                if (lp != null)
                {
                    Vector3 d = pos - lp.position;
                    d.y = 0f;                           // 높이는 무시 — 카메라가 내려보는 구도라 의미가 없다
                    float dist = d.magnitude;
                    // 이 소리가 닿는 거리. 폭발처럼 큰 소리만 1 보다 크다.
                    float far = FarRadius * (pol.carry > 0f ? pol.carry : 1f);
                    if (dist > far) return;             // 화면 밖 — 보이스를 쓰지 않는다
                    if (dist > NearRadius)
                    {
                        float t = (dist - NearRadius) / (far - NearRadius);
                        // (1-t)² 이다. 예전에 1-t² 로 써 놨었는데 그건 **반대 곡선**이라
                        // 15m 에서도 음량이 83% 로 남았다 — 멀리 있는 소리가 안 작아져서
                        // 시야 밖 좀비들의 소리가 전부 가까이 있는 것처럼 들렸다.
                        float k = 1f - t;
                        vol *= k * k;
                    }
                    if (_cam == null) _cam = Camera.main;
                    if (_cam != null)
                    {
                        // 카메라 오른쪽 축으로 투영해 좌우를 만든다. **기준은 언제나 FarRadius** 이지
                        // carry 가 아니다 — 폭발만 좌우가 덜 벌어지면 같은 자리에서 난 소리인데
                        // 방향이 달라 들린다. ±0.7 로 묶는 이유는 완전히 한쪽으로 몰면
                        // 이어폰에서 소리가 머리 밖으로 빠지기 때문이다.
                        float side = Vector3.Dot(d, _cam.transform.right) / FarRadius;
                        pan = Mathf.Clamp(side * 2.2f, -0.7f, 0.7f);
                    }
                }
            }
            if (vol <= 0.004f) return;   // 들리지 않을 소리에 보이스를 쓰지 않는다

            int slot = TakeVoice(key, pol, now);
            if (slot < 0) return;

            int idx = variant >= 0
                ? Mathf.Min(variant, clips.Length - 1)
                : PickVariant(key, clips.Length);
            var clip = clips[idx];

            float pitch = 1f + (pol.pitchJitter > 0f ? Random.Range(-pol.pitchJitter, pol.pitchJitter) : 0f);

            var src = _voices[slot];
            src.clip = clip;
            _voiceBase[slot] = Mathf.Min(1f, vol);
            _voiceNoDuck[slot] = ignoreDuck;
            src.volume = _voiceBase[slot] * (ignoreDuck ? 1f : _duck);
            src.pitch = pitch;
            src.panStereo = pan;
            src.Play();

            _voiceKey[slot] = key;
            _voiceFreeAt[slot] = now + clip.length / Mathf.Max(0.2f, pitch);
            _nextAllowed[key] = now + pol.cooldown;
        }

        /// 직전과 다른 변형을 고른다. 완전 무작위로 두면 같은 파일이 두세 번 연속으로 나와
        /// "변형이 없다" 처럼 들린다.
        int PickVariant(string key, int count)
        {
            if (count <= 1) return 0;
            int last = _lastVariant.TryGetValue(key, out int l) ? l : -1;
            int idx = Random.Range(0, count - 1);
            if (idx >= last) idx++;            // last 를 건너뛰어 균등하게 고른다
            if (idx >= count) idx = 0;
            _lastVariant[key] = idx;
            return idx;
        }

        /// <summary>
        /// 보이스 하나를 확보한다. 같은 키가 이미 상한만큼 울리고 있으면 <b>재생을 포기</b>한다 —
        /// 그 경우 소리를 빼앗아 새로 내도 귀에는 같게 들리고, 대신 다른 종류의 소리를 밀어낸다.
        /// </summary>
        int TakeVoice(string key, Policy pol, float now)
        {
            int sameKey = 0, free = -1;
            int oldest = -1; float oldestFreeAt = float.MaxValue;

            for (int i = 0; i < _voices.Length; i++)
            {
                bool busy = now < _voiceFreeAt[i];
                if (!busy)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (_voiceKey[i] == key) sameKey++;
                // 가장 먼저 끝날 보이스를 기억해 둔다 — 다 찼을 때 이것만 빼앗는다.
                if (_voiceFreeAt[i] < oldestFreeAt) { oldestFreeAt = _voiceFreeAt[i]; oldest = i; }
            }

            if (sameKey >= pol.maxVoices) return -1;
            if (free >= 0) return free;
            // 전부 차 있으면 가장 먼저 끝날 것을 빼앗는다. 그쪽이 이미 꼬리만 남았을 가능성이 높다.
            return oldest;
        }

        // ── 지속음 ───────────────────────────────────────────────────────
        void SetLoopInternal(string key, bool on, float volScale)
        {
            if (on)
            {
                if (!_bank.TryGetValue(key, out var clips) || clips.Length == 0) return;
                if (_loopKey != key)
                {
                    _loopKey = key;
                    _loopSource.clip = clips[0];
                    _loopSource.volume = 0f;
                    _loopSource.Play();
                }
                _loopTarget = volScale;
            }
            else
            {
                _loopTarget = 0f;
                if (key != null && _loopKey != key) return;   // 다른 무기의 루프였으면 건드리지 않는다
            }
        }

        void Update()
        {
            TickLoop();
            TickAmbientGroan();
        }

        /// 루프는 페이드로만 켜고 끈다. 바로 Play/Stop 하면 파형 중간에서 끊겨 딸깍거린다.
        void TickLoop()
        {
            if (_loopSource == null) return;

            float target = _loopTarget * ProtoSettings.SfxVolume * _duck;
            // 정지 중(레벨업 카드·옵션·결과)에는 화염이 계속 지지면 안 된다.
            if (Time.timeScale <= 0.0001f) target = 0f;

            // 켜질 때는 빠르게(0.06초), 꺼질 때는 조금 느리게(0.14초) — 사격을 멈춘 순간
            // 소리가 뚝 끊기면 화염이 실제로는 아직 판정 중인 것과 어긋나 보인다.
            float dt = Time.unscaledDeltaTime;
            float speed = target > _loopVolume ? dt / 0.06f : dt / 0.14f;
            _loopVolume = Mathf.MoveTowards(_loopVolume, target, speed);
            _loopSource.volume = _loopVolume;

            if (_loopVolume <= 0.0005f && _loopSource.isPlaying && _loopTarget <= 0f)
            {
                _loopSource.Stop();
                _loopKey = null;
            }
        }

        /// <summary>
        /// 주변 좀비 수에 따라 신음을 간간이 깔아 준다. 개체가 각자 울지 않는 이유는
        /// 260개의 타이머가 동시에 터지면 결국 상한(zgroan 2보이스)에 밀려 잘리기 때문이다.
        /// 여기서 한 마리를 골라 그 위치에서 울린다 — 소리의 수는 우리가 정하고,
        /// 위치는 실제로 거기 있는 좀비가 준다.
        /// </summary>
        void TickAmbientGroan()
        {
            var run = ProtoRun.I;
            if (run == null || !run.IsRunning) return;
            if (Time.unscaledTime < _nextGroan) return;

            var dir = ProtoSpawnDirector.I;
            var lp = ListenPoint();
            if (dir == null || lp == null) { _nextGroan = Time.unscaledTime + 1f; return; }

            // **반경 안에 실제로 있는 놈만 센다.** 예전에는 필드 전체에서 무작위 6마리를 뽑아
            // 그중 가까운 놈을 울렸는데, 좀비는 시야 밖 스폰 링(15~25m)에 항상 남아 있으므로
            // 후보가 떨어지는 일이 없었다 — 주변이 비어도 신음이 계속 나서 배경음처럼 들렸다.
            //
            // 한 번에 훑는 비용이 아깝지 않은 이유: 이 함수는 몇 초에 한 번만 여기까지 온다.
            // 반면 표본 추출은 "가까운 놈이 있는데 못 뽑는" 경우를 만들어, 빈도가 좀비 수가
            // 아니라 운에 따라 흔들린다.
            var list = dir.Active;
            int near = 0;
            ProtoZombie pick = null;
            float rSq = GroanRadius * GroanRadius;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 d = z.transform.position - lp.position;
                d.y = 0f;
                if (d.sqrMagnitude > rSq) continue;
                near++;
                // 반경 안에서 **무작위로 한 마리**를 고른다(reservoir sampling).
                // 항상 최근접을 고르면 신음이 늘 같은 방향에서 나서 포위된 느낌이 안 난다.
                if (Random.Range(0, near) == 0) pick = z;
            }

            if (near == 0 || pick == null)
            {
                // 주변이 비었으면 **아무 소리도 내지 않는다.** 조금 뒤에 다시 본다.
                _nextGroan = Time.unscaledTime + 1.0f;
                return;
            }

            // 가까이 몰릴수록 자주. 1~2마리 5.0초 / 4마리 3.5초 / 15마리 1.8초 / 20마리 이상 1.6초.
            // 예전 값(0.55~2.6초, 그것도 **전체 생존 수** 기준)은 25마리만 살아 있어도
            // 0.64초마다 울려서 신음이 끊이지 않았다.
            //
            // **여기가 신음 빈도를 만지는 유일한 자리다.** 아직 시끄러우면 분자(7.0)를 키우거나
            // 하한(1.6)을 올린다. 조용해서 좀비가 다가오는 게 안 느껴지면 반대로 한다.
            float interval = Mathf.Clamp(7.0f / Mathf.Sqrt(near), 1.6f, 5.0f);
            _nextGroan = Time.unscaledTime + interval * Random.Range(0.75f, 1.25f);

            Play("zgroan", pick.transform.position, 0.9f);
        }
    }
}
