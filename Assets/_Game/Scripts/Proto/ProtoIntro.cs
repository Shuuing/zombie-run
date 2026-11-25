using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 게임 시작 연출 — 수송기가 시작 지점 위를 지나가며 캐릭터를 떨어뜨리고, 낙하산을 편 캐릭터가
    /// 흔들리며 내려와 착지하면 런이 시작된다. 화면을 누르거나 아무 키나 치면 건너뛴다.
    ///
    /// <para><b>게임플레이는 건드리지 않는다.</b> Player 루트는 처음부터 시작 지점(바닥)에 있고,
    /// 공중에 있는 것은 <c>Visual</c> 자식뿐이다. 그 동안 런은 <see cref="RunState.Intro"/> 라
    /// 스폰·사격·타이머·이동이 전부 멈춰 있다 — 착지 순간 <see cref="RunState.Running"/> 으로 넘긴다.</para>
    ///
    /// <para>카메라는 <see cref="ProtoCameraFollow.introZoom"/>(거리 배율)·<see cref="ProtoCameraFollow.introLift"/>
    /// (초점 높이)만 만진다. 처음엔 넓게 빠져 있다가 캐릭터가 내려올수록 평소 구도로 돌아온다.</para>
    ///
    /// <para><b>밤 어둠 막</b>(2026-11-10) — 떨어지기 전에는 밤이라 깜깜해서 도시가 안 보이고(Proto/IntroVeil), 캐릭터가 내려와
    /// 착지하면서 착지 지점 둘레부터 빛이 번지듯 서서히 밝아진다. 끝은 평소 밤 화면(캐릭터 둘레만 밝고 화면 끝은 어둡다)이다.
    /// 비행기·낙하산·캐릭터만 막 위에 그린다. 아래 "밤 어둠 막" 상수 참고.</para>
    ///
    /// <para><b>시작 안개</b>(2026-11-12 오후, <see cref="ProtoIntroFog"/>) — 착지 지점 둘레 땅 위에 낮게 깔려 천천히 흐르다가 착지하면 서서히 옅어진다(저녁 3판).
    /// 오전에 뺀 안개(어둠 막 안의 노이즈 결, "검은 안개")와 달리 어둠 막 위에 그려 어둠을 안 탄다. 아래 "시작 안개" 상수 참고.</para>
    ///
    /// <para>모델: <c>Resources/Intro/IntroPlane·IntroParachute</c>(필드 프롭 airplane/parachute.fbx 의 프리팹),
    /// 비행기 그림자 = <c>Resources/VFX/T_PlaneShadow.png</c>(모델을 위에서 찍은 실루엣, 기수 = 텍스처 위).</para>
    /// </summary>
    public class ProtoIntro : MonoBehaviour
    {
        // ── 비행기 ──
        const float PlaneScale    = 4.5f;    // 모델 날개폭 1.18 → 약 5.3m (7 은 카메라에 너무 가까워 화면을 덮었다)
        const float PlaneAltitude = 9f;
        const float PlaneSpeed    = 17f;     // m/s
        const float PlaneStartX   = -24f;    // 시작 지점 기준 왼쪽에서 들어온다(+X 로 비행)
        const float PlaneLife     = 4.2f;
        const float PropSpin      = 1800f;   // 도/초
        const float ShadowModelSize = 1.276f; // T_PlaneShadow 한 변이 덮는 모델 단위 길이

        // ── 낙하 ──
        const float DropTime   = 1.45f;      // 비행기가 시작 지점 위를 지나는 시각(24m / 17m/s ≈ 1.41s)
        const float FreeFall   = 0.45f;      // 낙하산 펴기 전 자유낙하
        const float OpenTime   = 0.32f;      // 낙하산이 펴지는 데 걸리는 시간
        const float SinkSpeed  = 3.1f;       // 낙하산 하강 속도
        const float SwayDeg    = 9f;
        const float SwayHz     = 0.55f;

        // ── 낙하산 ──
        const float ChuteScale = 3.1f;       // 캐노피 폭 1.1 → 3.4m
        static readonly Vector3 ChuteHarness = new Vector3(-0.044f, 0.146f, -0.010f); // 줄이 모이는 점(모델 단위)
        const float HarnessHeight = 1.25f;   // 캐릭터 발에서 줄이 붙는 높이(어깨 근처)

        // ── 착지 뒤 낙하산 (ChuteAway) ──
        // 2026-11-18 사용자: "캐릭터가 떨어지면 낙하산이 바닥으로 떨어져서 없어지지 말고 하늘 위로 올라가서 사라지게".
        // 그전엔 바람 쪽(+X)으로 날리며 아래로 떨어져 접혔다. 지금은 무게가 빠진 캐노피가 톡 부풀며 떠올라 점점 빨라지며 하늘로 —
        // 위로 오르면서 화면 안쪽(+Z, 카메라에서 멀어짐)·바람 쪽(+X)으로 조금, 좌우로 살랑이고, 작아지며 사라진다(캐노피 가운데 기준).
        // 카메라가 52° 로 내려다봐서 위로만 오르면 카메라 쪽으로 다가와 커지다가 0.8초 만에 화면 위로 빠져나갔다(9m 위 · 6m 안쪽, 임시 프로젝트 렌더) —
        // 안쪽으로 더 멀리 보내 화면에선 위로 오르며 멀어져 작아지고, 화면 위 끝에 닿을 무렵(약 1.2초) 반쯤 작아져 있게 했다.
        const float ChuteFlyTime    = 1.8f;   // 초 — 연출 오브젝트 수명(착지 뒤 3초)보다 짧아야 한다
        const float ChuteFlyUp      = 5f;     // 오르는 높이(m)
        const float ChuteFlyBack    = 9f;     // 화면 안쪽(+Z)으로 가는 거리(m)
        const float ChuteFlyWind    = 2f;     // 바람 쪽(+X)으로 가는 거리(m)
        const float ChuteFlyLaunch  = 0.3f;   // 처음 속도 몫 — 0 이면 멈췄다가 출발, 1 이면 일정한 속도
        const float ChuteFlySwayDeg = 10f;    // 살랑임(°)
        const float ChuteFlySwayHz  = 1.1f;
        const float ChuteFlyLean    = 8f;     // 바람 쪽으로 기우는 각(°)
        const float ChuteFlyPuff    = 0.12f;  // 놓이는 순간 캐노피가 부푸는 정도
        const float ChuteFlyPuffEnd = 0.25f;  // 부풂이 끝나는 진행(0~1)
        const float ChuteFlyShrinkFrom = 0.3f;  // 이 진행부터 작아져 끝에 0

        // ── 밤 씬 조명 ──
        // 캐릭터 둘레의 빛(어둠 막이 걷히는 자리)·랜턴은 내려올수록 서서히 밝아지고 넓어진다
        // (2026-11-10 사용자 — "바닥에 가까워질수록 앞부분이 자연스럽게 밝아지게"). ProtoNightOverlay 가 PlayerLightScale 을 세기로 읽어
        // 착지 후 모습(부드럽게 퍼지는 빛)을 그만큼만 켠다. 그전에는 높이 2.5m 에서 또렷한 원을 0.45초에 톡 켰다.
        // 가로등은 지금도 그 높이에서 가까운 것부터 차례로 켠다.
        const float LampOnHeight = 2.5f;     // 이 높이까지 내려오면 가로등을 켠다
        // 낙하 진행(0 = 떨어지는 순간, 1 = 착지)이 LightFrom 일 때부터 캐릭터 둘레 빛이 켜지기 시작해 LightFull 에서 다 켜진다.
        // 착지하는 순간에 맞춰 다 켜지게 했다(2026-11-10 사용자 — "내려오면서 착지하면서 주변이 서서히 밝아지게").
        // 0.1~0.85 일 땐 높이 2.5m 에서 이미 90% 라 남은 0.8초는 다 밝은 채로 내려왔다. 지금은 높이 6m 5% → 4m 33% → 2.5m 70% → 착지 100%.
        const float LightFrom    = 0.05f;
        const float LightFull    = 1f;

        // ── 밤 어둠 막 (Proto/IntroVeil) ──
        // 2026-11-10 사용자(1): "처음 등장할 때 차량이나 배경 곳곳에 머티리얼이 보인다 → 떨어지기 전에는 어둡게,
        // 바닥에 가까워질수록 앞부분이 자연스럽게 밝아지게". 보이던 것은 밤 어둠 막(큐 2900)보다 나중에 그려지는 것들이다 —
        // 차 유리·물·바닥 안개 시트·픽업(3000~), 필드에 놓인 탱크(4001), 건물 비쳐 보이기(4051). 이 막은 그 전부를 덮는다.
        // 2026-11-10 사용자(2): "밤이라 어두워서 도시가 안 보이는 것. 캐릭터가 내려와 착지하면서 주변이 서서히 밝아지게, 너무 밝게는 말고".
        // 막은 무늬 없는 고른 어둠이다. 착지 지점에서 빛이 번지듯(반경·세기가 같이 자란다) 옅어지고, 착지 뒤 남은 어둠이 마저 걷힌다.
        // 막이 다 걷힌 뒤의 밝기는 평소 밤 화면(ProtoNightOverlay — 캐릭터 둘레만 밝고 화면 끝은 어둡다) 그대로라 더 밝아지지 않는다.
        // 막 안의 안개 결은 뺐다(2026-11-12 오전 사용자 "그냥 안개는 빼줘") — 어둠 위에 노이즈 결을 얹었다가 "검은 안개"로 보였고,
        // 회청색 밤안개로 바꾼 판도 마음에 들지 않았다. 같은 날 오후 사용자가 안개를 다시 원했다 → 아래 "시작 안개"(막 위, 따로).
        const int   VeilQueue      = 4060;   // 셰이더 태그(Overlay+60)와 같은 값
        // 비행기·낙하산·캐릭터 — 막 위에 그린다. 4061 이었다가 시작 안개(4061)를 막과 캐릭터 사이에 끼우느라 4062 로(11-12 오후)
        const int   AboveVeilQueue = 4062;
        // 막의 빛(어둠이 옅어지는 정도) — 낙하 진행이 VeilLightFrom 일 때 켜지기 시작해 VeilLightFull 에서 다 켜진다.
        // 캐릭터 둘레 빛(LightFrom~LightFull)보다 조금 앞선다 — 가운데 밝기는 그쪽이 정하고, 막은 "어디까지 보이나"만 맡는다.
        const float VeilLightFrom  = 0f;
        const float VeilLightFull  = 0.85f;
        const float VeilLightR0    = 3f;     // 빛이 처음 번질 때의 반경(m) — 이 거리에서 밝기가 가운데의 35%
        // 착지 순간의 반경(m). 13 이었다가 9 로 — 2026-11-10 사용자 "캐릭터 주변만 좀 밝고 기본적으로 밤인데 좀 더 어둡게"(평소 밤 화면도 같이 어둡게 했다)
        const float VeilLightR1    = 9f;
        const float VeilFront      = 0.7f;   // 카메라 쪽(화면 앞) 거리 배율 — 1 보다 작을수록 앞이 먼저 밝아진다
        const float VeilBack       = 1.15f;  // 먼 쪽(화면 위) 거리 배율 — 클수록 먼 곳이 늦게 밝아진다
        // 착지 뒤 남은(먼 곳의) 어둠이 걷히는 시간 — 먼 가로등·빛 웅덩이가 이 동안 드러난다. 연출 오브젝트 수명(착지 뒤 3초)보다 짧아야 한다
        const float VeilOutTime    = 1.4f;
        // 떨어지기 전 화면 전체의 밝기(눈 기준, 1 = 어둠 없음). 2026-11-12 사용자 "시작을 너무 어둡게 시작하는 것 같아" — 그전엔 0(깜깜)이었다.
        // 이 동안 어둠은 이 막 하나가 화면 전체에 고르게 맡고 평소 밤 어둠 막은 걷어 둔다(ProtoNightOverlay.IntroAmbient) —
        // 둘을 겹치면 도시는 둘을 곱한 만큼(거의 검정) 어두워지고, 밤 어둠 막을 안 타는 탱크·유리만 이 밝기로 떠 보인다.
        // 캐릭터가 내려올수록 화면 끝은 평소 밤 바닥 밝기(ProtoNightOverlay.FloorSeen ≈ 0.06)로 내려앉고 가운데는 캐릭터 빛으로 밝아진다.
        // 2026-11-13 사용자(시작 화면 스크린샷과 함께): "첫 등장할때 전체적으로 화면이 좀 더 어둡게 해줘. 캐릭터 주변외에는 살짝의 실루엣만 보일정도로,
        // 그리고 착륙하고 나서 밤 배경을 조금만 밝게" → 시작 0.25 → 0.06, 내려오는 동안 화면 끝은 DescentEdge 로 더 가라앉고(가운데는 막의 빛 원),
        // 착지 뒤 막이 걷히는 VeilOutTime 동안 평소 밤 바닥 밝기(이번에 0.06 → 0.09 로 올렸다)로 올라온다 — 시작 연출이 평소 밤보다 어둡다.
        // (11-12 엔 안개가 막 위에서 화면을 밝혀 시작이 착지보다 밝아 보였다 — 안개도 빛 밖은 어둡게: ProtoIntroFog.DarkBright)
        const float StartLevel     = 0.06f;
        const float DescentEdge    = 0.045f; // 내려오는 동안(캐릭터 빛이 켜질수록) 화면 끝 밝기 — 실루엣만
        const float VeilSkipTime   = 0.25f;  // 건너뛰었을 때 걷히는 시간

        // ── 시작 안개 (ProtoIntroFog · Proto/IntroFog) ──
        // 2026-11-12 오후 사용자: "처음 시작할 때 포그도 좀 깔아줘. 캐릭터가 착지하면 양옆으로 사라지게 해줘".
        // 큐 4061 = 밤 어둠 막(4060) 위 · 비행기·낙하산·캐릭터(AboveVeilQueue 4062) 아래 — 어둠을 안 타는 옅은 회색 안개, 캐릭터는 안개 위로 보인다.
        // **낙하산을 타고 내려오는 동안 서서히 옅어져 착지하면 다 사라진다** — 옅어짐 = 낙하 진행(_reveal)을 FogFadeFrom ~ 1 로 늘인 값,
        // 캐릭터 둘레부터 얇아지고 양옆으로 조금 퍼진다(Proto/IntroFog). 착지(진행 1)에서 0 이 되어 치운다.
        // 이력(같은 날 저녁): 착지 뒤 1.8초에 양옆으로 갈라짐 → "착지하면 서서히 옅어지게" 로 착지 뒤 4초(연출에서 떼어 내 스스로) →
        // "낙하산 타고 내려오면서부터 서서히 사라지면서 착지하면 다 사라지게" 로 지금 방식.
        const float FogFadeFrom    = 0.1f;   // 낙하 진행 — 자유낙하(0.45초, 약 1m)가 끝나고 낙하산이 펴질 무렵부터 옅어진다
        const float FogFadeCurve   = 1.6f;   // 진행에 거듭제곱 — 1(곧게)은 바닥이 같이 밝아져 진행 0.75 에 이미 거의 안 보였다(임시 프로젝트 렌더)
        const float FogSkipTime    = 0.25f;  // 건너뛰었을 때 — 연출 오브젝트가 0.3초 뒤 사라지므로 안개를 떼어 내 스스로 옅어지게

        // ── 비행기 소리 ── (SFX/plane_loop = synth.py build_plane, 2초 프로펠러 루프)
        // 지나가는 느낌은 파형이 아니라 여기서 만든다: 거리로 음량, 접근/이탈 속도로 도플러 피치, 좌우로 팬.
        const float SoundRefDist  = 9f;      // 이 거리 안이면 최대 음량
        const float SoundListenerH = 8f;     // 듣는 자리(시작 지점 위)와 비행 경로 사이 높이차 — 거리 계산용
        const float DopplerBoost  = 3f;      // 실제 17m/s 도플러는 ±5% 라 안 들린다. 3배로 과장(카툰)
        const float EngineVolume  = 0.9f;

        // ── 카메라 ──
        const float CamZoomStart = 1.55f;

        static ProtoIntro _cur;
        public static bool Playing => _cur != null;
        /// 밤 어둠 막이 떠 있는 동안(낙하 중 + 착지 뒤 남은 어둠이 걷힐 때까지). 건물 비쳐 보이기(ProtoSeeThrough)가 이 동안 쉰다 —
        /// 비쳐 보이는 건물은 막보다 앞 큐(4051)라 문제없지만, 그 깊이 선기록(4050)이 막 위로 올린 캐릭터(4062)를 가린다.
        public static bool Veiled => _cur != null && _cur._veil != null;

        static readonly int IdVeilCenter   = Shader.PropertyToID("_Veil_Center");
        static readonly int IdVeilParams   = Shader.PropertyToID("_Veil_Params");
        static readonly int IdVeilDarkCol  = Shader.PropertyToID("_DarkColor");

        ProtoPlayer _player;
        Transform _visual;
        Vector3 _visualBaseScale;
        GameObject _ring;
        Transform _plane, _shadow, _chute;
        Transform[] _props;
        Vector3 _chuteBase;      // 프리팹 루트 스케일(FBX 단위 변환 100 이 루트에 걸려 있다)
        Vector3 _start;          // 시작 지점(바닥)
        Vector3 _dropPos;        // 떨어지기 시작한 공중 위치
        float _t;
        float _fallY, _fallVel;  // 캐릭터 높이·수직 속도
        Vector3 _drift;          // 떨어진 자리에서 시작 지점으로 미끄러지는 수평 오프셋
        bool _dropped, _landed, _skipped;
        bool _lampsOn;           // 가로등을 켰는가
        float _reveal;           // 낙하 진행 — 0 = 떨어지기 전, 1 = 착지. 어둠이 걷히는 정도와 바닥 밝기가 여기서 나온다
        float _veilOut;          // 착지 뒤 0→1 — 남은(먼 곳의) 어둠이 걷힌다
        ProtoNightOverlay _night; // 평소 밤 어둠 막(없으면 낮 씬)
        GameObject _veil;        // 밤 어둠 막(화면 덮개). 없으면(셰이더 없음) 예전처럼 막 없이 진행
        Material _veilMat;
        Mesh _veilMesh;
        ProtoIntroFog _fog;      // 시작 안개. 없으면(머티리얼 없음) 안개 없이 진행
        // 막 위로 올린 캐릭터 머티리얼과 원래 큐 — 막이 사라질 때 되돌린다
        readonly System.Collections.Generic.List<Material> _raisedMats = new System.Collections.Generic.List<Material>();
        readonly System.Collections.Generic.List<int> _raisedQueues = new System.Collections.Generic.List<int>();
        AudioSource _engine;
        float _engineFade = 1f;  // 건너뛰면 0.25초에 걸쳐 0 으로
        bool _engineOut;
        Renderer[] _bulbs;       // 도시 가로등 메시(전구 = 발광 머티리얼). 꺼 둘 때 MPB 로 발광만 검정
        Renderer[] _pools;       // 가로등 아래 바닥 빛 웅덩이(CityBuilder _LightPools, Proto/LightPool)
        float[] _poolBase;
        MaterialPropertyBlock _mpb;
        Quaternion _faceRot = Quaternion.Euler(0f, 180f, 0f);   // 카메라 쪽을 보고 내려온다

        /// 런 시작 시 ProtoRun 이 부른다. 연출을 못 하면(프리팹 없음 등) 바로 Running 으로 넘긴다.
        public static void Play(ProtoRun run)
        {
            Stop();
            if (run == null || run.player == null) return;
            var go = new GameObject("_Intro");
            _cur = go.AddComponent<ProtoIntro>();
            if (!_cur.Begin(run.player)) { Destroy(go); _cur = null; run.state = RunState.Running; return; }
            run.state = RunState.Intro;
        }

        /// 화면 탭과 같은 건너뛰기(바로 착지 → Running). 개발용 UI 캡처(ProtoHUD F10)가 다시 시작한 판에서 부른다.
        public static void Skip()
        {
            if (_cur != null && !_cur._landed) _cur.Land(true);
        }

        /// 즉시 치운다(재시작 등). 런 상태는 부른 쪽이 정한다.
        public static void Stop()
        {
            if (_cur == null) return;
            _cur.Cleanup();
            Destroy(_cur.gameObject);
            _cur = null;
        }

        bool Begin(ProtoPlayer player)
        {
            _player = player;
            _visual = player.transform.Find("Visual");
            var planePrefab = Resources.Load<GameObject>("Intro/IntroPlane");
            var chutePrefab = Resources.Load<GameObject>("Intro/IntroParachute");
            if (_visual == null || planePrefab == null || chutePrefab == null)
            {
                Debug.LogWarning("[인트로] Visual 또는 Resources/Intro 프리팹이 없어 건너뛴다");
                return false;
            }
            _start = player.transform.position; _start.y = 0f;
            _visualBaseScale = _visual.localScale;
            player.transform.rotation = _faceRot;

            var ring = player.GetComponentInChildren<ProtoGroundRing>(true);
            if (ring != null) { _ring = ring.gameObject; _ring.SetActive(false); }

            _plane = Instantiate(planePrefab, transform).transform;
            _plane.localScale = Vector3.one * PlaneScale;
            _plane.rotation = Quaternion.LookRotation(Vector3.right);   // 기수 +Z → 비행 방향 +X
            // 프로펠러 2개(Object001/002)는 피벗이 원점이라 그대로 돌리면 크게 원을 그린다 —
            // 메시 중심에 축을 하나 끼워 넣어 그 축을 돌린다.
            var props = new System.Collections.Generic.List<Transform>();
            foreach (var r in _plane.GetComponentsInChildren<Renderer>())
            {
                if (!r.name.StartsWith("Object")) continue;
                var pivot = new GameObject(r.name + "_Axis").transform;
                pivot.SetParent(r.transform.parent, false);
                pivot.position = r.bounds.center;
                pivot.rotation = _plane.rotation;
                r.transform.SetParent(pivot, true);
                props.Add(pivot);
            }
            _props = props.ToArray();

            _chute = Instantiate(chutePrefab, transform).transform;
            _chuteBase = _chute.localScale;
            _chute.gameObject.SetActive(false);

            _shadow = MakeShadow();
            _engine = MakeEngineSound();

            // 밤 씬 어둠 막(큐 2900)에 묻히지 않게 캐릭터와 같은 큐로 올린다.
            if (ring != null) { ring.RaiseQueueOf(_plane.gameObject); ring.RaiseQueueOf(_chute.gameObject); }
            // 시작 줌에서 카메라가 비행기까지 ~20m+ 라 거리 안개(19.5~29m)에 걸려 뿌옇게 나온다 — 안개를 끈다.
            // r.materials 는 사본이라 필드 프롭(같은 M_Airplane·M_Parachute)에는 영향이 없다.
            NoFog(_plane.gameObject); NoFog(_chute.gameObject);
            // 밤 맵 조명(푸른 달빛 · 센 푸른 주변광)에 위장색이 푸른 회색으로 씻기지 않게 — 비행기는 낮 해 하나만 받는다(11-18, 사본 머티리얼)
            ProtoIntroPlaneColor.Apply(_plane.gameObject);

            // 밤 어둠 막 — 비행기·낙하산은 막 위에 그린다(캐릭터는 떨어지는 순간 올린다: Drop).
            _night = FindFirstObjectByType<ProtoNightOverlay>();
            _veil = MakeVeil();
            if (_veil != null) { RaiseAboveVeil(_plane.gameObject, false); RaiseAboveVeil(_chute.gameObject, false); }
            // 시작 안개 — 좌우는 지금 카메라 오른쪽 기준(연출 동안 카메라는 거리만 바뀌고 각도는 그대로다)
            var cam = Camera.main;
            _fog = ProtoIntroFog.Create(transform, _start, cam != null ? cam.transform.right : Vector3.right);

            _visual.localScale = Vector3.zero;   // 비행기에서 떨어질 때까지 숨긴다
            ProtoNightOverlay.PlayerLightScale = 0f;
            ProtoNightOverlay.LampsOnAt = float.MaxValue;   // 가로등도 꺼 둔다 — 내려오면 가까운 것부터 켜진다
            BulbsOff();
            PlacePlane();
            SetCamera(CamZoomStart, PlaneAltitude * 0.55f);
            TickVeil();
            return true;
        }

        /// 화면 덮개 — 정점은 셰이더가 클립 공간에 그대로 까니 위치는 상관없고, 컬링에 안 걸리게 바운즈만 크게(ProtoNightOverlay 와 같은 방식).
        GameObject MakeVeil()
        {
            // 빌드에서는 아무 머티리얼도 참조하지 않는 셰이더가 빠진다 — Resources 의 머티리얼로 물고 온다.
            var refMat = Resources.Load<Material>("ShaderRefs/Ref_Proto_IntroVeil");
            var shader = refMat != null ? refMat.shader : Shader.Find("Proto/IntroVeil");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[인트로] Proto/IntroVeil 셰이더가 없어 어둠 막 없이 진행한다");
                return null;
            }
            _veilMat = new Material(shader) { name = "IntroVeil", renderQueue = VeilQueue };
            // 밤 씬은 밤 어둠 막(ProtoNightOverlay)의 어둠 색을 그대로 쓴다 — 막이 걷혀 평소 밤 화면으로 넘어갈 때 색이 바뀌지 않는다.
            // 낮 씬은 그 씬의 거리 안개 색을 어둡게 해서 쓴다.
            var night = _night;
            if (night != null && night.isActiveAndEnabled)
            {
                var om = night.overlayMaterial;
                if (om != null && om.HasProperty(IdVeilDarkCol)) _veilMat.SetColor(IdVeilDarkCol, om.GetColor(IdVeilDarkCol));
            }
            else
            {
                Color fog = RenderSettings.fog ? RenderSettings.fogColor : new Color(0.62f, 0.66f, 0.72f);
                _veilMat.SetColor(IdVeilDarkCol, new Color(fog.r * 0.3f, fog.g * 0.3f, fog.b * 0.3f, 1f));
            }

            var go = new GameObject("IntroVeil");
            go.transform.SetParent(transform, false);
            _veilMesh = new Mesh { name = "IntroVeil" };
            _veilMesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            _veilMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _veilMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            go.AddComponent<MeshFilter>().sharedMesh = _veilMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _veilMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// root 아래 머티리얼(사본)을 어둠 막 위 큐로 올린다. remember 면 원래 큐를 적어 뒀다가 막이 사라질 때 되돌린다(캐릭터).
        /// 파티클과 발밑 링은 건드리지 않는다 — ProtoGroundRing.RaiseQueueOf 와 같은 대상.
        void RaiseAboveVeil(GameObject root, bool remember)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.GetComponent<ProtoGroundRing>() != null) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (remember) { _raisedMats.Add(mats[i]); _raisedQueues.Add(mats[i].renderQueue); }
                    mats[i].renderQueue = AboveVeilQueue;
                }
                r.materials = mats;
            }
        }

        /// 매 프레임 — 낙하 진행으로 바닥 밝기와 밤 어둠 막을 맞춘다. 착지 뒤에도(남은 어둠이 걷힐 때까지) 돈다.
        void TickVeil()
        {
            // 낙하 진행: 떨어지기 전 0, 낙하 중엔 높이 그대로, 착지(또는 건너뛰기)하면 1 로
            if (_landed) _reveal = Mathf.MoveTowards(_reveal, 1f, Time.unscaledDeltaTime / VeilSkipTime);
            else if (_dropped) _reveal = 1f - Mathf.Clamp01(_fallY / Mathf.Max(0.01f, _dropPos.y));

            // 바닥 밝기 — 밤 씬 캐릭터 둘레 원·랜턴이 내려올수록 커진다
            float light = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(LightFrom, LightFull, _reveal));
            ProtoNightOverlay.PlayerLightScale = light;

            if (_veil != null && _landed) _veilOut = Mathf.MoveTowards(_veilOut, 1f, Time.unscaledDeltaTime / (_skipped ? VeilSkipTime : VeilOutTime));
            float floorSeen = _night != null ? _night.FloorSeen : 0.06f;
            // 화면 끝에서 보일 밝기(눈 기준): 떨어지기 전 StartLevel → 내려오며 DescentEdge → 착지 뒤 막이 걷히며 평소 밤 바닥 밝기(floorSeen)
            float introEdge = Mathf.Lerp(StartLevel, DescentEdge, light);
            float veilFloor;
            if (_veil != null)
            {
                // 화면 끝 = 이 막의 바닥 밝기 × 평소 밤 어둠 막의 끝 밝기. 떨어지기 전엔 평소 밤 어둠 막을 다 걷고(IntroAmbient 1) 이 막 하나가 고르게 어둡힌다 —
                // 둘을 겹치면 밤 어둠 막을 안 타는 탱크·유리·물만 떠 보인다(11-10). 캐릭터 빛이 켜질수록 평소 밤 어둠 막을 제 모양(캐릭터 둘레 빛 + 바깥 어둠)으로
                // 돌려놓고, 이 막은 화면 끝이 목표 밝기가 되게 그만큼 옅어진다. (11-13 처음엔 끝까지 걷어 둔 채 막만 어둡게 했더니 막의 빛 원(착지 9m)이
                // 화면 대부분을 덮는 착지 무렵 그 안이 어둠 없이 환해 게임 화면보다 밝았다 — 임시 프로젝트 렌더.)
                float amb = 1f - light;
                ProtoNightOverlay.IntroAmbient = amb;
                float nightAtEdge = floorSeen + amb * (1f - floorSeen);
                float veilBase = Mathf.Clamp01(introEdge / Mathf.Max(nightAtEdge, 0.01f));
                // 착지 뒤 막이 걷히면(1) 화면 끝 = floorSeen — veilBase × floorSeen(= DescentEdge) 에서 부드럽게 올라온다
                veilFloor = Mathf.Lerp(veilBase, 1f, Mathf.SmoothStep(0f, 1f, _veilOut));
            }
            else
            {
                // 막이 없는 씬 — 밤 어둠 막만으로(그 바닥 밝기보다 어둡게는 못 한다)
                veilFloor = 1f;
                ProtoNightOverlay.IntroAmbient = Mathf.Clamp01((introEdge - floorSeen) / Mathf.Max(1f - floorSeen, 0.01f));
            }

            if (_veil == null) return;
            if (_veilOut >= 1f) { EndVeil(); return; }

            // 빛: 떨어지기 전엔 0(화면 전체가 바닥 밝기) → 내려올수록 세지고 넓어진다 → 착지 뒤 남은 어둠이 마저 걷힌다(w → 1)
            float level  = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VeilLightFrom, VeilLightFull, _reveal));
            float radius = Mathf.Lerp(VeilLightR0, VeilLightR1, Mathf.SmoothStep(0f, 1f, _reveal));
            _veilMat.SetVector(IdVeilCenter, new Vector4(_start.x, 0f, _start.z, radius));
            _veilMat.SetVector(IdVeilParams, new Vector4(level, VeilFront, VeilBack, veilFloor));
        }

        /// 시작 안개 — 떨어지기 전엔 깔려 있고, 낙하산으로 내려오는 동안 서서히 옅어져 착지하면 다 사라진다(치운다).
        void TickFog()
        {
            if (_fog == null) return;
            // 어둠 막과 같은 빛 원이 안개도 밝힌다(TickVeil 과 같은 식)
            float level  = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(VeilLightFrom, VeilLightFull, _reveal));
            float radius = Mathf.Lerp(VeilLightR0, VeilLightR1, Mathf.SmoothStep(0f, 1f, _reveal));
            _fog.SetLight(_start, radius, level);
            if (_skipped) { _fog.Release(FogSkipTime); return; }   // 떼어 내 0.25초에 스스로 옅어진다(여러 번 불려도 된다)
            float part = Mathf.Pow(Mathf.InverseLerp(FogFadeFrom, 1f, _reveal), FogFadeCurve);
            _fog.SetPart(part);
            if (_landed && part >= 1f) { Destroy(_fog.gameObject); _fog = null; }
        }

        /// 밤 어둠 막을 치우고 캐릭터 큐를 되돌린다. 여러 번 불려도 된다.
        void EndVeil()
        {
            if (_veil != null) { Destroy(_veil); _veil = null; }
            if (_veilMat != null) { Destroy(_veilMat); _veilMat = null; }
            if (_veilMesh != null) { Destroy(_veilMesh); _veilMesh = null; }
            for (int i = 0; i < _raisedMats.Count; i++)
                if (_raisedMats[i] != null) _raisedMats[i].renderQueue = _raisedQueues[i];
            _raisedMats.Clear();
            _raisedQueues.Clear();
        }

        static void NoFog(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.materials)
                    if (m.HasProperty(ProtoShaderIds._BCM_NoFog)) m.SetFloat(ProtoShaderIds._BCM_NoFog, 1f);
        }

        Transform MakeShadow()
        {
            var shader = Shader.Find("Proto/GroundShadow");
            var tex = Resources.Load<Texture2D>("VFX/T_PlaneShadow");
            if (shader == null || tex == null) return null;
            var go = new GameObject("PlaneShadow");
            go.transform.SetParent(transform, false);
            float h = ShadowModelSize * PlaneScale * 0.5f;
            var m = new Mesh { name = "PlaneShadow" };
            m.vertices  = new[] { new Vector3(-h, 0, -h), new Vector3(h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h) };
            m.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(shader);
            mat.SetTexture(ProtoShaderIds._BaseMap, tex);
            mat.SetColor(ProtoShaderIds._BaseColor, new Color(0.02f, 0.03f, 0.06f, 0.5f));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        void Update()
        {
            TickEngine();
            TickVeil();
            TickFog();
            if (_landed) return;

            // 건너뛰기 — 첫 프레임 입력(씬 로드 직후 클릭)은 무시한다
            if (_t > 0.2f && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                Land(true);
                return;
            }

            float dt = ProtoConfig.Delta;
            _t += dt;
            PlacePlane();

            if (!_dropped && _t >= DropTime) Drop();
            if (_dropped) TickFall(dt);

            if (_t > PlaneLife && _plane != null) { Destroy(_plane.gameObject); _plane = null; if (_shadow != null) Destroy(_shadow.gameObject); _shadow = null; }
        }

        AudioSource MakeEngineSound()
        {
            var clip = Resources.Load<AudioClip>("SFX/plane_loop");
            if (clip == null) { Debug.LogWarning("[인트로] Resources/SFX/plane_loop 없음 — 비행기 소리 없이 진행"); return null; }
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.playOnAwake = false;
            a.spatialBlend = 0f;     // 위치감은 아래에서 팬·음량으로 직접 만든다(ProtoAudio 와 같은 방식)
            a.volume = 0f;
            a.time = Random.Range(0f, clip.length);
            a.Play();
            return a;
        }

        /// 비행기 위치(경로는 시간으로 정해져 있다)로 음량·피치·팬을 매 프레임 맞춘다. 비행기가 지워진 뒤에도 같은 식이라 끊김이 없다.
        void TickEngine()
        {
            if (_engine == null) return;
            if (_engineOut) _engineFade = Mathf.Max(0f, _engineFade - Time.unscaledDeltaTime / 0.25f);

            float dx = PlaneStartX + PlaneSpeed * _t;                         // 시작 지점 기준 비행기 가로 위치
            float dist = Mathf.Sqrt(dx * dx + SoundListenerH * SoundListenerH);
            float vol = Mathf.Pow(Mathf.Clamp01(SoundRefDist / dist), 1.3f);
            vol *= Mathf.Clamp01(_t / 0.3f);                                    // 시작 딸깍 방지
            vol *= Mathf.Clamp01((PlaneLife - _t) / 0.6f);                       // 비행기가 지워지기 전에 다 줄인다
            _engine.volume = vol * EngineVolume * ProtoSettings.SfxVolume * _engineFade;

            float vr = PlaneSpeed * dx / dist * DopplerBoost;                     // 멀어지는 속도(+), 다가오면 −
            _engine.pitch = 343f / (343f + vr);
            _engine.panStereo = Mathf.Clamp(dx / 16f, -1f, 1f) * 0.85f;

            if (_engine.volume <= 0.0001f && (_t > PlaneLife || _engineFade <= 0f)) { _engine.Stop(); _engine = null; }
        }

        void PlacePlane()
        {
            if (_plane == null) return;
            var p = new Vector3(_start.x + PlaneStartX + PlaneSpeed * _t, PlaneAltitude, _start.z);
            // 살짝 오르내리며 날개를 기울인다 — 일직선 등속이면 레일 위를 미끄러지는 것처럼 보인다
            p.y += Mathf.Sin(_t * 1.7f) * 0.25f;
            _plane.position = p;
            _plane.rotation = Quaternion.LookRotation(Vector3.right) * Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 1.3f) * 4f);
            if (_props != null)
                foreach (var a in _props) if (a != null) a.Rotate(0f, 0f, PropSpin * ProtoConfig.Delta, Space.Self);
            if (_shadow != null)
            {
                _shadow.position = new Vector3(p.x, 0.03f, p.z);
                _shadow.rotation = Quaternion.LookRotation(Vector3.right);
            }
        }

        void Drop()
        {
            _dropped = true;
            // 동체 배 밑에서 떨어진다. 비행기가 시작 지점을 약간 지난 자리라 거기서 시작 지점으로 미끄러진다.
            _dropPos = _plane != null ? _plane.position + Vector3.down * 0.8f : _start + Vector3.up * PlaneAltitude;
            _fallY = _dropPos.y;
            _fallVel = 0f;
            _drift = new Vector3(_dropPos.x - _start.x, 0f, _dropPos.z - _start.z);
            _visual.localScale = _visualBaseScale;
            // 캐릭터도 어둠 막 위에 그린다. Begin 이 아니라 여기서 — 발밑 링의 Start(캐릭터를 4001 로 올린다)가
            // ProtoRun.Start 보다 늦게 돌 수 있어, 먼저 올려 두면 도로 4001 로 덮인다. 막이 사라질 때 되돌린다(EndVeil).
            if (_veil != null) RaiseAboveVeil(_player.gameObject, true);
        }

        void TickFall(float dt)
        {
            float since = _t - DropTime;
            bool open = since >= FreeFall;

            if (!open) { _fallVel += 9.8f * dt; }
            else
            {
                if (!_chute.gameObject.activeSelf)
                {
                    _chute.gameObject.SetActive(true);
                    // 낙하산 펴지는 '펑' — 펴지는 순간에 낸다. 처음엔 뛰어내릴 때(0.45초 일찍) 냈다가 사용자가 "빠르다"고 해서 옮겼다.
                    // 착지('land')를 '착'으로 바꿀 때 이 소리는 예전 '펑' 그대로 두기로 해서 키를 갈랐다(synth.py build_chute)
                    ProtoAudio.Play("chute", _visual.position);
                }
                // 펴지는 순간 급감속 — 자유낙하 속도에서 하강 속도로 0.25초 안에 붙는다
                _fallVel = Mathf.MoveTowards(_fallVel, SinkSpeed, 30f * dt);
            }
            _fallY -= _fallVel * dt;

            // 수평은 하강하는 동안 시작 지점으로 미끄러진다(높이 비례)
            float k = Mathf.Clamp01(_fallY / Mathf.Max(0.01f, _dropPos.y));
            Vector3 horiz = _start + _drift * (k * k);

            // 흔들림: 줄이 붙은 점(어깨 위) 기준으로 몸이 진자처럼 흔들린다
            float sway = open ? Mathf.Sin((since - FreeFall) * SwayHz * Mathf.PI * 2f) * SwayDeg : 0f;
            float settle = Mathf.Clamp01(_fallY / 2f);              // 땅 가까이선 흔들림을 줄인다
            var tilt = Quaternion.Euler(sway * 0.35f * settle, 0f, sway * settle);
            _player.transform.rotation = _faceRot * tilt;

            Vector3 harness = new Vector3(horiz.x, _fallY + HarnessHeight, horiz.z);
            _visual.position = harness - _player.transform.rotation * (Vector3.up * HarnessHeight);

            if (open)
            {
                float u = Mathf.Clamp01((since - FreeFall) / OpenTime);
                float pop = BackOut(u);
                _chute.rotation = _player.transform.rotation;
                // ChuteHarness 는 프리팹을 그대로 놓았을 때의 좌표라 배율(mul)만 곱하면 된다
                var mul = new Vector3(ChuteScale * pop, ChuteScale * Mathf.Lerp(0.2f, 1f, pop), ChuteScale * pop);
                _chute.localScale = Vector3.Scale(_chuteBase, mul);
                _chute.position = harness - _chute.rotation * Vector3.Scale(ChuteHarness, mul);
            }

            // 카메라: 처음엔 넓게, 내려올수록 평소 구도로. 초점은 캐릭터 몸통 높이를 따라간다
            float hk = Mathf.Clamp01(_fallY / PlaneAltitude);
            SetCamera(Mathf.Lerp(1f, CamZoomStart, Mathf.SmoothStep(0f, 1f, hk)), Mathf.Max(0f, _fallY * 0.6f));

            if (!_lampsOn && _fallY <= LampOnHeight) LampsOn();

            if (_fallY <= 0f) Land(false);
        }

        void Land(bool skipped)
        {
            if (_landed) return;
            _landed = true;
            _skipped = skipped;      // 바닥 밝기·밤 어둠 막은 TickVeil 이 마저 맞춘다(건너뛰면 0.25초, 아니면 남은 어둠 1.4초)
            _visual.localPosition = Vector3.zero;
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = _visualBaseScale;
            _player.transform.rotation = _faceRot;
            SetCamera(1f, 0f);

            if (!skipped)
            {
                // 착지 이펙트(2026-11-16 사용자 "낙하산 타고 내려올 때 착지 이펙트") — 카탈로그 intro_landing(템플릿 FX_IntroLanding:
                // 바닥으로 퍼지는 먼지 고리 · 피어오르는 먼지 · 잔돌 · 먼지 충격파). 비어 있으면 예전처럼 교체 착지 먼지(같은 소리 land 포함)
                if (ProtoFx.HasPrefab(FxIds.IntroLanding))
                {
                    ProtoFx.Play(FxIds.IntroLanding, _start, Vector3.zero);
                    ProtoAudio.Play("land", _start);
                }
                else
                {
                    var sw = _player.GetComponent<ProtoCharacterSwitcher>();
                    if (sw != null) sw.PlayLandingFx(_start);
                }
                ProtoCameraFollow.Shake(0.08f, 0.18f);
                StartCoroutine(Squash());
                if (!_lampsOn) LampsOn();
                // 낙하산은 캐릭터를 놓고 하늘로 떠올라 사라진다(ChuteAway)
                if (_chute != null && _chute.gameObject.activeSelf) StartCoroutine(ChuteAway());
                else if (_chute != null) _chute.gameObject.SetActive(false);
            }
            else
            {
                if (_chute != null) _chute.gameObject.SetActive(false);
                _lampsOn = true;
                ProtoNightOverlay.LampsOnAt = 0f;
                BulbsOn();
            }

            if (_plane != null && skipped) { Destroy(_plane.gameObject); _plane = null; }
            if (_shadow != null && skipped) { Destroy(_shadow.gameObject); _shadow = null; }
            if (_ring != null) _ring.SetActive(true);
            if (ProtoRun.I != null && ProtoRun.I.state == RunState.Intro) ProtoRun.I.state = RunState.Running;
            _engineOut = skipped;   // 건너뛰면 엔진 소리를 0.25초에 줄인다(바로 끊으면 딸깍)
            Destroy(gameObject, skipped ? 0.3f : 3f);   // 먼 가로등이 차례로 다 켜질 시간까지 / 건너뛰기는 소리가 줄어들 시간만
        }

        System.Collections.IEnumerator Squash()
        {
            const float T = 0.2f;
            for (float t = 0f; t < T; t += ProtoConfig.Delta)
            {
                float k = BackOut(Mathf.Clamp01(t / T));
                float sq = Mathf.Lerp(0.72f, 1f, k), wide = Mathf.Lerp(1.28f, 1f, k);
                if (_visual != null) _visual.localScale = Vector3.Scale(_visualBaseScale, new Vector3(wide, sq, wide));
                yield return null;
            }
            if (_visual != null) _visual.localScale = _visualBaseScale;
        }

        /// 착지 뒤 낙하산 — 캐릭터를 놓고 하늘로 떠올라 작아지며 사라진다(위 "착지 뒤 낙하산" 상수).
        System.Collections.IEnumerator ChuteAway()
        {
            Vector3 p0 = _chute.position;
            Quaternion r0 = _chute.rotation;
            Vector3 s0 = _chute.localScale;
            // 캐노피 가운데(바운즈 중심)를 기준으로 움직이고 줄인다 — 루트 피벗은 줄 끝 근처라 그대로 줄이면 줄 끝으로 빨려 든다
            var rs = _chute.GetComponentsInChildren<Renderer>();
            Vector3 c0 = p0;
            if (rs.Length > 0) { var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); c0 = b.center; }
            Vector3 cLocal = Quaternion.Inverse(r0) * (c0 - p0);   // 루트 → 가운데(회전 뺀, 지금 크기 그대로)

            for (float t = 0f; t < ChuteFlyTime; t += ProtoConfig.Delta)
            {
                if (_chute == null) yield break;
                ChuteFlyPose(t, c0, cLocal, r0, out var pos, out var rot, out var f);
                _chute.rotation = rot;
                _chute.localScale = Vector3.Scale(s0, f);
                _chute.position = pos;
                yield return null;
            }
            if (_chute != null) _chute.gameObject.SetActive(false);
        }

        /// 날아가는 낙하산의 t 초 자세. c0 = 처음 캐노피 가운데(월드), cLocal = 루트 → 가운데(회전 뺀, 처음 크기), f = 처음 크기에 곱할 배율.
        static void ChuteFlyPose(float t, Vector3 c0, Vector3 cLocal, Quaternion r0, out Vector3 rootPos, out Quaternion rot, out Vector3 f)
        {
            float u = Mathf.Clamp01(t / ChuteFlyTime);
            // 점점 빨라진다 — 처음 속도 몫 ChuteFlyLaunch, 끝에 1
            float e = u * (ChuteFlyLaunch + (1f - ChuteFlyLaunch) * u);
            Vector3 center = c0 + new Vector3(ChuteFlyWind * e, ChuteFlyUp * e, ChuteFlyBack * e);
            // 좌우로 살랑이며 바람 쪽으로 조금 기운다
            float sway = Mathf.Sin(t * ChuteFlySwayHz * Mathf.PI * 2f) * ChuteFlySwayDeg;
            rot = r0 * Quaternion.Euler(0f, 0f, sway - ChuteFlyLean * e);
            // 놓이는 순간 톡 부풀었다가(폭 먼저) 뒤쪽에 작아져 사라진다
            float puff = u < ChuteFlyPuffEnd ? Mathf.Sin(u / ChuteFlyPuffEnd * Mathf.PI) * ChuteFlyPuff : 0f;
            float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ChuteFlyShrinkFrom, 1f, u));
            f = new Vector3(k * (1f + puff), k * (1f + puff * 0.5f), k * (1f + puff));
            rootPos = center - rot * Vector3.Scale(cLocal, f);
        }

        /// 가로등을 켠다 — 캐릭터에서 가까운 것부터 차례로(ProtoNightOverlay.LampK). 캐릭터 둘레 원은 TickVeil 이 맡는다.
        void LampsOn()
        {
            _lampsOn = true;
            ProtoNightOverlay.LampsOnAt = Time.time;
            BulbsOn();
        }

        static void SetCamera(float zoom, float lift)
        {
            var cam = ProtoCameraFollow.I;
            if (cam == null) return;
            cam.introZoom = zoom;
            cam.introLift = lift;
        }

        void Cleanup()
        {
            if (_visual != null) { _visual.localPosition = Vector3.zero; _visual.localRotation = Quaternion.identity; _visual.localScale = _visualBaseScale; }
            if (_ring != null) _ring.SetActive(true);
            EndVeil();
            ProtoNightOverlay.PlayerLightScale = 1f;
            ProtoNightOverlay.IntroAmbient = 0f;
            ProtoNightOverlay.LampsOnAt = 0f;
            BulbsOn();
            SetCamera(1f, 0f);
        }

        // 가로등 전구(CityBuilder 의 StreetLights, M_City_Lamp 발광). 공유 머티리얼을 건드리면 에디터에서 에셋이 바뀌므로 MPB 로만.
        void BulbsOff()
        {
            var pr = GameObject.Find("_LightPools");
            _pools = pr != null ? pr.GetComponentsInChildren<Renderer>() : null;
            if (_pools != null)
            {
                _mpb = new MaterialPropertyBlock();
                _poolBase = new float[_pools.Length];
                for (int i = 0; i < _pools.Length; i++)
                {
                    var m = _pools[i].sharedMaterial;
                    _poolBase[i] = m != null && m.HasProperty(ProtoShaderIds._Intensity) ? m.GetFloat(ProtoShaderIds._Intensity) : 0.35f;
                }
                TickPools();
            }

            var go = GameObject.Find("StreetLights");
            _bulbs = go != null ? go.GetComponentsInChildren<Renderer>() : null;
            if (_bulbs == null) return;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(ProtoShaderIds._EmissionColor, Color.black);
            foreach (var r in _bulbs) if (r != null) r.SetPropertyBlock(mpb);
        }

        /// 빛 웅덩이를 가로등 구멍과 같은 켜짐 값으로. 다 켜지면 MPB 를 걷어 원래 머티리얼로 돌린다.
        void TickPools()
        {
            if (_pools == null) return;
            Vector3 c = _player != null ? _player.transform.position : Vector3.zero;
            bool all = true;
            for (int i = 0; i < _pools.Length; i++)
            {
                var r = _pools[i]; if (r == null) continue;
                Vector3 d = r.transform.position - c; d.y = 0f;
                float k = ProtoNightOverlay.LampK(d.magnitude);
                if (k < 1f || ProtoNightOverlay.LampsOnAt == float.MaxValue) all = false;
                _mpb.SetFloat(ProtoShaderIds._Intensity, _poolBase[i] * k);
                r.SetPropertyBlock(_mpb);
            }
            if (all) RestorePools();
        }

        void RestorePools()
        {
            if (_pools == null) return;
            foreach (var r in _pools) if (r != null) r.SetPropertyBlock(null);
            _pools = null;
        }

        void LateUpdate() => TickPools();   // 착지 뒤에도(오브젝트가 남아 있는 동안) 계속 켜 나간다

        void BulbsOn()
        {
            if (_bulbs == null) return;
            foreach (var r in _bulbs) if (r != null) r.SetPropertyBlock(null);
            _bulbs = null;
        }

        void OnDestroy()
        {
            // 이 연출이 아직 '지금 것'이면(다시 시작으로 새 연출이 대신 들어서지 않았으면) 바닥 밝기를 다 켠 채로 끝낸다
            if (_cur == this) { _cur = null; ProtoNightOverlay.PlayerLightScale = 1f; ProtoNightOverlay.IntroAmbient = 0f; }
            EndVeil();        // 어둠이 다 걷히기 전에 없어져도 막·캐릭터 큐가 남지 않게
            RestorePools();   // 다 켜지기 전에 사라져도 꺼진 채로 남지 않게
            BulbsOn();
        }

        static float BackOut(float u)
        {
            const float s = 1.55f;
            float p = u - 1f;
            return 1f + (s + 1f) * p * p * p + s * p * p;
        }
    }
}
