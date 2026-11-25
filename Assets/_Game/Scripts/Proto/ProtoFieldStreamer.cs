using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 청크 단위로 배경을 만들고 버리는 <b>스트리밍 테스트 하네스</b>.
    ///
    /// <para><b>무엇을 재려고 만들었나.</b> "이동할 때 어디까지 미리 불러와야 하는가" 하나다.
    /// 그래서 <see cref="preloadRings"/> 를 <b>플레이 중에 키로 바꿀 수 있게</b> 했고, HUD 가
    /// 청크 수·삼각형 수·청크 하나 만드는 데 걸린 시간을 같이 띄운다. 숫자를 보면서 반경을
    /// 올렸다 내렸다 하는 것이 이 스크립트의 전부다.</para>
    ///
    /// <para><b>배경 아트가 아니다.</b> 지금 프롭은 나무·밴만 실제 메시이고 나머지는 전부
    /// 박스다(§배경 임시). 실물로 교체할 때 이 스크립트에서 고칠 곳은 <see cref="BuildKinds"/>
    /// 배열 한 곳뿐이다 — 프리미티브 대신 프리팹을 물리면 된다.</para>
    ///
    /// <para><b>왜 청크를 <c>_Props/&lt;종류&gt;/&lt;인스턴스&gt;</c> 밑에 넣는가.</b>
    /// <see cref="ProtoObstacles"/> 가 딱 그 2단 계층을 훑어 충돌 박스를 만들고, 그룹 이름으로
    /// 축소율을 고른다(나무는 줄기만 막으려고 0.32). 청크별로 계층을 새로 파면 그룹 이름이
    /// <c>Chunk_3_-2</c> 가 되어 <b>나무가 수관까지 막는 벽</b>이 된다. 그래서 계층은 종류별로
    /// 유지하고, 청크는 자기가 만든 인스턴스 목록을 따로 들고 있다가 회수한다.</para>
    /// </summary>
    public class ProtoFieldStreamer : MonoBehaviour
    {
        // ── 월드 ────────────────────────────────────────────────────────
        [Header("월드")]
        [Tooltip("청크 한 변(m). 카메라 가시 세로가 약 22m 라 40m 면 한 화면에 1~2청크가 걸린다.")]
        public float chunkSize = 40f;

        [Tooltip("월드 반경(청크). 0 이면 무한. 8 이면 ±320m 사각.")]
        public int worldHalfChunks = 8;

        // ── 스트리밍 (이게 테스트 대상이다) ─────────────────────────────
        [Header("스트리밍 — 테스트 대상")]
        [Tooltip("플레이어가 선 청크 기준 ±N 을 미리 만든다. 1 이면 3×3(= 내 칸 + 붙어 있는 칸), 2 면 5×5.")]
        [Range(0, 5)] public int preloadRings = 1;

        // **반경(칸)이 아니라 거리(m)로 버린다.** 예전에는 keepRings = preloadRings + 1 이었는데,
        // 그러면 '만들지는 않지만 버리지도 않는' 링이 한 겹 생겨서 돌아다니는 동안 살아있는 청크가
        // preload 3×3 인데도 최대 5×5(25칸)까지 불어난다. 눈에 보이는 비용은 살아있는 수가 정한다.
        // 거리로 재면 프리로드 링 바로 바깥 청크가 이 여유만큼만 버티다 사라져 9~13칸에서 묶인다.
        [Tooltip("프리로드 경계를 이만큼 더 지나가야 버린다(m). 0 이면 경계에서 만들었다 버렸다를 반복한다.")]
        [Range(0f, 40f)] public float unloadMargin = 8f;

        [Tooltip("한 프레임에 새로 만들 청크 수. 1 이면 프레임이 고르고, 크면 순간에 다 차는 대신 튄다.")]
        [Range(1, 25)] public int buildsPerFrame = 1;

        // ── 내용 ────────────────────────────────────────────────────────
        [Header("청크 내용")]
        [Tooltip("월드 시드. 같은 값이면 같은 좌표에 항상 같은 배경이 나온다.")]
        public int seed = 20260921;

        [Tooltip("청크당 웅덩이 개수 기대값. 0 이면 물 없음.")]
        [Range(0f, 3f)] public float pondsPerChunk = 0.8f;

        [Tooltip("건물(박스) 배치. 끄면 도로와 프롭만 남는다.")]
        public bool buildings = true;

        // ── 디버그 ──────────────────────────────────────────────────────
        [Header("디버그")]
        public bool showHud = true;
        public KeyCode keyFewer = KeyCode.LeftBracket;    // [
        public KeyCode keyMore  = KeyCode.RightBracket;   // ]
        public KeyCode keyHud   = KeyCode.Backslash;      // \

        // ── 에셋 (씬 빌더가 물려준다) ───────────────────────────────────
        [Header("에셋 — 빌더가 채운다")]
        // 지면은 세 톤이다. 어두운 잔해밭 위에 중간 톤 자갈 갓길, 그 위에 밝은 흙길.
        // 알파 데칼 없이 명도 3단을 만드는 가장 싼 방법이고, 밝은 길이 플레이어 실루엣을 받쳐준다(§15).
        public Material mBase;       // 잔해밭 — 청크 전체
        public Material mShoulder;   // 자갈 갓길 — 길보다 넓은 띠
        public Material mPath;       // 흙길 — 가장 밝은 띠
        public Material mAsh;        // 그을린 재 — 아직 배치하지 않는다(알파 데칼이 있어야 쓸 만하다)
        public Material mConcrete;   // 건물 박스
        // 위 네 장을 한 머티리얼에서 섞는 스플랫 지면(BCM 툰 + BCM_GROUND_SPLAT).
        // 비어 있으면 예전 층 쿼드 적층으로 돌아간다 — 비교용으로 남겨 둔 경로다.
        public Material mSplat;
        // 건물 모델(힉스필드 → Tripo, 각 2,000 tri 이하). 비어 있으면 예전 콘크리트 박스로 돌아간다.
        // FBX 피벗은 prep_building.py 가 바닥 중앙으로 옮겨 두었지만, 그래도 밑면을 재서 앉힌다.
        public GameObject[] buildingPrefabs;
        // 땅에 꽂는 풀·들꽃 카드(2x2 아틀라스, BCM 툰 + 알파 컷아웃 + 바람). 비어 있으면 풀을 안 심는다.
        public Material mFoliage;
        public Material mWater;
        public Mesh     pondMesh;
        public GameObject treePrefab, vanPrefab, containerPrefab, barrelPrefab;
        public Material mBarricade, mBarrier, mFence, mRubble, mBarrel;
        public Material mTree, mVan, mContainer;   // FBX/OBJ 원본 머티리얼 대신 프로젝트 것을 입힌다

        // ────────────────────────────────────────────────────────────────

        /// 프롭 한 종류. 실제 메시(prefab)가 있으면 그것을, 없으면 박스를 쓴다.
        /// **실물 아트로 교체할 때 건드릴 곳은 여기뿐이다.**
        struct Kind
        {
            public string name;          // _Props 아래 그룹 이름. ProtoObstacles 의 축소율 키이기도 하다
            public GameObject prefab;    // null 이면 박스
            public Vector3 boxSize;      // 박스일 때의 크기(m)
            public float scale;          // 프리팹일 때의 배율
            public int perChunk;         // 청크당 개수
            public bool offRoad;         // 노면 밖에만
            public Material mat;         // 박스에 입힐 머티리얼

            public Kind(string n, GameObject p, float s, Vector3 box, Material m, int c, bool off)
            { name = n; prefab = p; scale = s; boxSize = box; mat = m; perChunk = c; offRoad = off; lift = 0f; }

            public float lift;           // 프리팹 밑면을 y=0 에 앉히려고 올리는 높이(m). BuildKinds 가 잰다
        }

        Kind[] _kinds;

        // ── 길 ──────────────────────────────────────────────────────────
        // **격자 도로를 버렸다.** 레퍼런스(폐차장 항공샷)에는 도로가 없고 흙길이 잔해밭을 가로지른다.
        // 길은 월드 좌표만의 함수로 정의한다 — 그래야 청크가 자기 구간만 그려도 경계에서 정확히 이어진다.
        // 청크 좌표를 섞으면 이음매에서 길이 어긋난다.
        [Header("길")]
        [Tooltip("흙길 폭(m). 자갈 갓길은 이 값의 shoulderMult 배.")]
        public float pathWidth = 5.0f;
        [Tooltip("갓길 배수. 1.8 이면 흙길 5m 양옆으로 자갈이 2m 씩 더 나간다.")]
        public float shoulderMult = 1.8f;
        [Tooltip("길이 휘는 폭(m). 0 이면 직선.")]
        public float pathWander = 11f;

        // **경계가 자로 그은 직선이면 '텍스처가 선으로 잘린' 것처럼 보인다.**
        // 레퍼런스(2D 타일셋 배경)들의 공통점은 경계가 울퉁불퉁하고 테두리가 한 겹 더 있다는 것이다.
        // 그래서 스트립의 양쪽 가장자리 정점을 노이즈로 흔든다 — 텍스처를 새로 뽑지 않고 모양만 바꾼다.
        [Tooltip("길 가장자리가 울퉁불퉁한 정도(m). 0 이면 자로 그은 직선.")]
        [Range(0f, 3f)] public float edgeRough = 0.9f;

        [Tooltip("가장자리 요철의 잔 정도. 크면 자잘하게, 작으면 크게 출렁인다.")]
        [Range(0.1f, 1.5f)] public float edgeDetail = 0.55f;

        // ── 텍스처 타일링 ───────────────────────────────────────────────
        // **모든 지면 UV 는 월드 좌표로 만든다.** 청크 로컬 UV(0~1)를 쓰면 텍스처 한 장이 청크
        // 한 칸에 통째로 늘어나서 청크 경계가 그대로 격자로 보인다(실제로 그렇게 나왔다).
        // 월드 기준이면 청크를 어떻게 잘라도 무늬가 이어지고, 이음매라는 개념 자체가 사라진다.
        //
        // 층마다 반복 주기를 다르게 두는 것도 의도다. 같은 값이면 세 층의 알갱이 크기가 똑같아
        // 색만 다른 띠로 보인다 — 크기가 다르면 "거친 재 / 굵은 자갈 / 고운 흙"으로 읽힌다.
        [Header("텍스처 반복 주기(m)")]
        [Tooltip("잔해밭 바닥. 작을수록 무늬가 잦다.")]  public float tileBase     = 9f;
        [Tooltip("흙길")]                                public float tilePath     = 6f;
        [Tooltip("자갈 갓길")]                           public float tileShoulder = 8f;
        [Tooltip("재 테두리")]                           public float tileAsh      = 13f;

        // ── 지면 혼합(스플랫) ───────────────────────────────────────────
        // 층 쿼드를 y 로 쌓으면 경계가 항상 칼로 자른 선이다. 유니티 Terrain 의 레이어 블렌드처럼
        // 섞으려면 한 면 위에서 가중치로 섞어야 한다. 청크마다 격자 메시 1장을 깔고, 정점 컬러에
        // 층별 '덮임 정도'를 굽는다(R=재 G=자갈 B=흙길, 0.5 가 경계). 경계를 알갱이 단위로
        // 부수는 것은 셰이더가 한다(BCM_GroundSplat). **mSplat 이 비면 예전 적층 방식**으로 그린다.
        [Header("지면 혼합 (mSplat 이 있을 때)")]
        public bool useSplat = true;
        [Tooltip("격자 한 칸(m). 가장자리 요철의 가장 짧은 파장이 ~1.8m 라 1m 보다 크면 요철이 뭉개진다.")]
        [Range(0.5f, 4f)] public float splatCell = 1f;
        [Tooltip("경계가 0→1 로 넘어가는 폭(m). 셰이더 Edge Softness 와 곱해져 최종 번짐이 된다.")]
        [Range(0.2f, 6f)] public float splatBlend = 2.0f;

        // 풀은 잔해밭 위에 큰 덩어리로 번진다(정점 컬러 A). 길은 풀 위를 덮으므로 길 쪽은 신경 쓰지 않는다.
        // 월드 좌표 펄린 노이즈라 청크 경계에서 풀밭이 이어진다.
        [Tooltip("풀밭 비율. 0 이면 풀 없음, 1 이면 잔해밭 대부분이 풀.")]
        [Range(0f, 1f)] public float grassAmount = 0.45f;
        [Tooltip("풀밭 덩어리 크기(m). 클수록 넓은 풀밭이 드문드문.")]
        [Range(4f, 60f)] public float grassPatch = 22f;

        // ── 풀 카드(mFoliage 가 있을 때) ────────────────────────────────
        // 지면 풀밭(GrassCoverage)과 같은 노이즈로 심는다 — 바닥 풀 텍스처 위에 카드가 올라가야 "풀밭"으로 읽힌다.
        // 길(자갈·흙길)은 비운다. 청크마다 한 메시로 합쳐 드로우 콜 1개.
        [Header("풀 카드 (mFoliage 가 있을 때)")]
        [Tooltip("후보점 간격(m). 작을수록 촘촘하다. 청크당 후보 수 = (40/간격)².")]
        [Range(0.4f, 3f)] public float foliageStep = 0.7f;
        [Tooltip("풀밭 한가운데에서 후보점이 풀이 될 확률.")]
        [Range(0f, 1f)] public float foliageDense = 0.75f;
        [Tooltip("맨 잔해밭에서의 확률 — 잡초가 드문드문.")]
        [Range(0f, 1f)] public float foliageSparse = 0.06f;
        [Tooltip("크기 배율 범위.")]
        public Vector2 foliageScale = new Vector2(0.75f, 1.25f);
        [Tooltip("풀 타일 한 변(m). 카메라에 보이는 땅을 이 단위로 덮는다. 작을수록 화면 밖 낭비가 적지만 타일(드로우 콜)이 늘어난다.")]
        [Range(4f, 20f)] public float foliageTile = 8f;
        [Tooltip("보이는 땅 사각형 바깥으로 더 심는 여유(m). 카드 윗부분이 사각형 밖에서 화면 안으로 솟고, 이동 중 새 타일이 생기는 게 보이면 안 된다.")]
        [Range(0f, 10f)] public float foliageMargin = 3f;
        [Tooltip("한 프레임에 만드는 풀 타일 수 상한(순간 정지 방지).")]
        [Range(1, 16)] public int foliageBuildsPerFrame = 4;

        // 길 폭을 프롭 금지 반경으로도 쓴다. 길 한복판에 드럼통이 서 있으면 길로 안 읽힌다.
        float PathClear { get { return pathWidth * shoulderMult * 0.5f + 1.0f; } }

        class Chunk
        {
            public Vector2Int coord;
            public GameObject ground;                 // 지면·길·물을 담는 루트
            public readonly List<GameObject> props = new List<GameObject>();
            // 길 메시는 청크마다 새로 만든다. GameObject 를 지워도 Mesh 는 남으므로 따로 들고 있다가 지운다.
            public readonly List<Mesh> meshes = new List<Mesh>();
            public readonly List<Vector3> ponds = new List<Vector3>();   // 웅덩이 (x, z, 반경) — 풀 타일이 피한다
            public int tris;
        }

        readonly Dictionary<Vector2Int, Chunk> _live = new Dictionary<Vector2Int, Chunk>();
        readonly Dictionary<string, Stack<GameObject>> _pool = new Dictionary<string, Stack<GameObject>>();
        readonly Dictionary<string, Transform> _groups = new Dictionary<string, Transform>();
        readonly List<Vector2Int> _wanted = new List<Vector2Int>();
        readonly List<Vector2Int> _doomed = new List<Vector2Int>();

        Transform _propsRoot, _groundRoot;
        Transform _target;
        float _arenaHalfBackup;
        bool _dirtyObstacles;

        // HUD 용 측정값. **시간 측정은 청크 생성 구간만 잰다** — 프레임 시간은 에디터 포커스에
        // 좌우되므로(진행 상황 메모 참조) 여기서 FPS 를 말하지 않는다.
        float _lastBuildMs, _worstBuildMs;
        int   _totalTris;

        void Awake()
        {
            _target = FindFirstObjectByType<ProtoPlayer>() is ProtoPlayer p ? p.transform : null;

            var props = GameObject.Find("_Props");
            _propsRoot = props != null ? props.transform : new GameObject("_Props").transform;
            _groundRoot = new GameObject("_StreamGround").transform;

            BuildKinds();

            // 플레이어·젬·스폰이 전부 ProtoConfig.ArenaHalf 로 위치를 클램프한다.
            // 40×40 아레나 값(19m)이 그대로면 청크를 아무리 만들어도 20m 에서 막힌다.
            _arenaHalfBackup = ProtoConfig.ArenaHalf;
            ProtoConfig.ArenaHalf = worldHalfChunks > 0
                ? worldHalfChunks * chunkSize - 1f
                : 100000f;

            Debug.Log($"[스트리머] ArenaHalf {_arenaHalfBackup} -> {ProtoConfig.ArenaHalf} (월드 " +
                      (worldHalfChunks > 0 ? $"{worldHalfChunks * chunkSize * 2f:F0}×{worldHalfChunks * chunkSize * 2f:F0}m" : "무한") + ")");
        }

        // 스트리밍 씬을 빠져나온 뒤에도 static 이 남아 일반 프로토 씬의 경계가 풀려 있으면 안 된다.
        // (에디터가 도메인 리로드를 끄고 있으면 실제로 그렇게 된다.)
        void OnDestroy()
        {
            ProtoConfig.ArenaHalf = _arenaHalfBackup;
            // 풀 타일 메시는 코드로 만든 것이라 씬을 내려도 안 지워진다
            foreach (var kv in _foliage) if (kv.Value.mesh != null) Destroy(kv.Value.mesh);
            foreach (var t in _foliagePool) if (t.mesh != null) Destroy(t.mesh);
        }

        void BuildKinds()
        {
            _kinds = new[]
            {
                // 실제 메시로 남기는 셋. 배율은 FieldBuilder 가 쓰던 값 그대로다.
                // 컨테이너는 원본 바운즈가 5.0×4.0×2.3 이라 실물 20ft(6.06×2.44×2.59)에 맞춰 1.2배.
                // **14,616 tri 다** — 임시로 쓰는 것이고 폴리곤은 나중에 줄인다.
                new Kind("Tree_Conifer",    treePrefab,      2.371f, Vector3.zero,                 mTree,      5, true),
                new Kind("Van_Abandoned",   vanPrefab,       2.634f, Vector3.zero,                 mVan,       2, false),
                new Kind("Container",       containerPrefab, 1.200f, Vector3.zero,                 mContainer, 2, true),

                // 나머지는 박스. 크기는 Field/README.md 의 '실제 크기 기준' 칸을 따랐다.
                new Kind("Barricade_Wood",  null, 1f, new Vector3(2.4f, 1.10f, 0.35f), mBarricade, 5, false),
                new Kind("Barrier_Concrete",null, 1f, new Vector3(2.0f, 0.90f, 0.60f), mBarrier,   4, false),
                new Kind("Fence_Plank",     null, 1f, new Vector3(2.4f, 1.50f, 0.12f), mFence,     6, true),
                new Kind("Rubble_Concrete", null, 1f, new Vector3(2.5f, 0.70f, 2.00f), mRubble,    5, false),
                // 드럼통은 2026-09-30 사용자가 모델(44 tri)을 만들어 박스에서 실물로 바꿨다. 배율은 FieldBuilder 와 같은 0.474(높이 0.91m).
                new Kind("Barrel_Rusty",    barrelPrefab, barrelPrefab != null ? 0.474f : 1f, new Vector3(0.6f, 0.90f, 0.60f), mBarrel, 5, false),
            };

            // **나무·밴·컨테이너 메시는 피벗이 바운즈 한가운데다.** y=0 에 그대로 놓으면 높이의 절반이
            // 땅에 묻힌다(나무 2.25m / 밴 1.52m / 컨테이너 2.61m — 실제로 그렇게 나왔다).
            // FieldBuilder.GroundTo 와 같은 일을 종류마다 한 번만 재 둔다. 회전은 Y 축뿐이라 밑면 높이가 안 변한다.
            for (int i = 0; i < _kinds.Length; i++)
                if (_kinds[i].prefab != null) _kinds[i].lift = MeasureLift(_kinds[i].prefab, _kinds[i].scale);

            // 건물은 발자국(가로×세로)이 배치 판정에 쓰이므로 크기까지 잰다.
            var defs = new List<BuildingDef>();
            if (buildingPrefabs != null)
                foreach (var p in buildingPrefabs)
                {
                    if (p == null) continue;
                    var b = MeasureBounds(p, 1f);
                    defs.Add(new BuildingDef { prefab = p, size = b.size, lift = -b.min.y,
                                               centre = new Vector2(b.center.x, b.center.z) });
                }
            _buildings = defs.ToArray();
        }

        struct BuildingDef { public GameObject prefab; public Vector3 size; public float lift; public Vector2 centre; }
        BuildingDef[] _buildings = new BuildingDef[0];

        static Bounds MeasureBounds(GameObject prefab, float scale)
        {
            var go = Instantiate(prefab);
            // 루트 회전은 살린다(Place 와 같은 자세로 재야 밑면·발자국이 맞는다)
            go.transform.SetPositionAndRotation(Vector3.zero, prefab.transform.localRotation);
            go.transform.localScale = Vector3.one * scale;
            var rs = go.GetComponentsInChildren<Renderer>(true);
            var b = new Bounds();
            if (rs.Length > 0)
            {
                b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            }
            DestroyImmediate(go);
            return b;
        }

        static float MeasureLift(GameObject prefab, float scale) { return -MeasureBounds(prefab, scale).min.y; }

        void Update()
        {
            if (Input.GetKeyDown(keyHud)) showHud = !showHud;
            if (Input.GetKeyDown(keyFewer)) preloadRings = Mathf.Max(0, preloadRings - 1);
            if (Input.GetKeyDown(keyMore))  preloadRings = Mathf.Min(5, preloadRings + 1);

            if (_target == null)
            {
                var p = FindFirstObjectByType<ProtoPlayer>();
                if (p != null) _target = p.transform;
                if (_target == null) return;
            }

            var here = CoordOf(_target.position);
            Release(here, _target.position);
            Acquire(here);

            // 청크가 바뀐 프레임에만 충돌을 다시 굽는다. 매 프레임 돌리면 프롭 수에 비례해 터진다.
            if (_dirtyObstacles && ProtoObstacles.I != null)
            {
                ProtoObstacles.I.Rebuild(false);
                _dirtyObstacles = false;
            }

            // 풀은 청크가 아니라 '카메라에 보이는 땅' 기준. 카메라 추적이 LateUpdate 라 한 프레임 늦지만
            // 여유(foliageMargin)가 그걸 덮는다.
            UpdateFoliage(Camera.main);
        }

        Vector2Int CoordOf(Vector3 world)
        {
            return new Vector2Int(Mathf.FloorToInt(world.x / chunkSize + 0.5f),
                                  Mathf.FloorToInt(world.z / chunkSize + 0.5f));
        }

        /// 프리로드 경계를 <see cref="unloadMargin"/> 만큼 더 지나간 청크를 회수한다.
        ///
        /// <para>칸이 아니라 <b>플레이어 실제 위치에서의 거리</b>로 재는 것이 히스테리시스다.
        /// 경계에 서서 한 발씩 움직여도 만들었다 버렸다를 반복하지 않으면서, 링을 한 겹 통째로
        /// 살려두지는 않는다 — 살아있는 청크 수가 프리로드 칸 수 근처에서 묶인다.</para>
        void Release(Vector2Int here, Vector3 playerPos)
        {
            float limit = (preloadRings + 0.5f) * chunkSize + unloadMargin;
            _doomed.Clear();
            foreach (var kv in _live)
            {
                float cx = kv.Key.x * chunkSize, cz = kv.Key.y * chunkSize;
                float d = Mathf.Max(Mathf.Abs(playerPos.x - cx), Mathf.Abs(playerPos.z - cz));
                if (d > limit) _doomed.Add(kv.Key);
            }
            for (int i = 0; i < _doomed.Count; i++)
            {
                var c = _live[_doomed[i]];
                Recycle(c);
                _live.Remove(_doomed[i]);
                _dirtyObstacles = true;
            }
        }

        /// 가까운 청크부터 채운다. 한 프레임에 buildsPerFrame 개까지만 — 순간 정지를 피하려는 것이고,
        /// 이 값이 곧 "얼마나 미리 불러와야 끊기지 않는가"의 반대쪽 손잡이다.
        void Acquire(Vector2Int here)
        {
            _wanted.Clear();
            for (int dz = -preloadRings; dz <= preloadRings; dz++)
                for (int dx = -preloadRings; dx <= preloadRings; dx++)
                {
                    var c = new Vector2Int(here.x + dx, here.y + dz);
                    if (worldHalfChunks > 0 && (Mathf.Abs(c.x) > worldHalfChunks || Mathf.Abs(c.y) > worldHalfChunks)) continue;
                    if (_live.ContainsKey(c)) continue;
                    _wanted.Add(c);
                }
            if (_wanted.Count == 0) return;

            _wanted.Sort((a, b) =>
            {
                int da = Mathf.Abs(a.x - here.x) + Mathf.Abs(a.y - here.y);
                int db = Mathf.Abs(b.x - here.x) + Mathf.Abs(b.y - here.y);
                return da.CompareTo(db);
            });

            int n = Mathf.Min(buildsPerFrame, _wanted.Count);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) _live[_wanted[i]] = Build(_wanted[i]);
            sw.Stop();

            _lastBuildMs = (float)sw.Elapsed.TotalMilliseconds / n;
            if (_lastBuildMs > _worstBuildMs) _worstBuildMs = _lastBuildMs;
            _dirtyObstacles = true;
            RecountTris();
        }

        // ── 청크 생성 ───────────────────────────────────────────────────

        Chunk Build(Vector2Int coord)
        {
            var c = new Chunk { coord = coord };
            var rng = new System.Random(Hash(coord.x, coord.y, seed));
            Vector3 origin = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
            float half = chunkSize * 0.5f;

            // ── 지면 ── 어두운 잔해밭 위에 자갈 갓길, 그 위에 밝은 흙길. y 간격은 0.01 계단.
            var g = new GameObject($"Ground_{coord.x}_{coord.y}");
            g.transform.SetParent(_groundRoot, false);
            g.transform.position = origin;
            c.ground = g;

            if (useSplat && mSplat != null)
            {
                SplatGround(c, g.transform, origin);
            }
            else
            {
                QuadWorld(c, g.transform, "Base", chunkSize, mBase, 0.000f, tileBase, origin);

                // 길 두 줄(세로·가로)을 세 겹으로 깐다 — 재 테두리 → 자갈 갓길 → 흙길.
                // **위상을 층마다 다르게 준다.** 같은 위상이면 세 겹의 요철이 겹쳐 그냥 굵은 띠 하나가 된다.
                // 재(mAsh)는 여기서 처음 쓰인다: 알파 데칼 없이도 '가장 바깥 테두리'로는 제 역할을 한다.
                // 층 폭·위상은 Layers 표와 같은 값이다 — 스플랫 경로와 모양이 같아야 비교가 된다.
                for (int i = 0; i < Layers.Length; i++)
                {
                    var L = Layers[i];
                    Material m = i == 0 ? mAsh : i == 1 ? mShoulder : mPath;
                    float tile = i == 0 ? tileAsh : i == 1 ? tileShoulder : tilePath;
                    AddPath(c, g.transform, coord, true,  LayerWidth(i), m, L.yZ, L.phaseZ, tile);
                    AddPath(c, g.transform, coord, false, LayerWidth(i), m, L.yX, L.phaseX, tile);
                }
            }

            // ── 물 ── 수심 10cm 기준(Pond_010cm). 지면보다 위에 뜨는 것이 곧 수심이다.
            if (pondMesh != null && mWater != null && pondsPerChunk > 0f)
            {
                int ponds = Mathf.FloorToInt(pondsPerChunk) + (rng.NextDouble() < (pondsPerChunk % 1f) ? 1 : 0);
                for (int i = 0; i < ponds; i++)
                {
                    // 도로 위 웅덩이는 어색하니 노면 밖에만. 반경은 1.7m 메시를 배율로 키운다.
                    Vector2 p = OffPathPoint(rng, half, origin);
                    float depth = 0.10f + (float)rng.NextDouble() * 0.10f;
                    float r = 0.8f + (float)rng.NextDouble() * 1.4f;
                    var go = new GameObject($"Pond_{Mathf.RoundToInt(depth * 100):D3}cm");
                    go.transform.SetParent(g.transform, false);
                    go.transform.localPosition = new Vector3(p.x, depth, p.y);
                    go.transform.localScale = new Vector3(r, 1f, r);
                    c.ponds.Add(new Vector3(origin.x + p.x, origin.z + p.y, r * PondMeshRadius));
                    go.AddComponent<MeshFilter>().sharedMesh = pondMesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = mWater;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                }
            }

            // ── 건물 ── 블록 네 귀퉁이에 박스 하나씩. 콘크리트 텍스처를 그대로 쓴다.
            if (buildings)
            {
                for (int q = 0; q < 4; q++)
                {
                    if (rng.NextDouble() < 0.35) continue;
                    float sx = 6f + (float)rng.NextDouble() * 7f;
                    float sz = 6f + (float)rng.NextDouble() * 7f;
                    float sy = 4f + (float)rng.NextDouble() * 9f;
                    // 모델이 있으면 그 발자국을 쓴다. 회전은 90° 단위만 — 판정이 축 정렬 발자국 기준이라
                    // 임의 각도로 돌리면 모서리가 길을 침범한다. 90/270° 면 가로·세로가 바뀐다.
                    int bi = -1, quarter = 0;
                    if (_buildings.Length > 0)
                    {
                        bi = rng.Next(_buildings.Length);
                        quarter = rng.Next(4);
                        var d = _buildings[bi].size;
                        sx = quarter % 2 == 0 ? d.x : d.z;
                        sz = quarter % 2 == 0 ? d.z : d.x;
                    }
                    float pad = PathClear + 2f;
                    // 길 여유(pad) 밖으로 밀어낼 여지는 '청크 안에 남은 폭'까지만 쓴다. 예전처럼 0~4m 를
                    // 무작정 더하면 12m 창고는 거의 매번 청크 밖으로 삐져 탈락했다(실제로 한 채도 안 나왔다).
                    float slackX = Mathf.Min(4f, half - pad - sx), slackZ = Mathf.Min(4f, half - pad - sz);
                    if (slackX < 0f || slackZ < 0f) continue;
                    float cx = (q % 2 == 0 ? -1f : 1f) * (pad + sx * 0.5f + (float)rng.NextDouble() * slackX);
                    float cz = (q < 2 ? -1f : 1f) * (pad + sz * 0.5f + (float)rng.NextDouble() * slackZ);
                    if (Mathf.Abs(cx) + sx * 0.5f > half || Mathf.Abs(cz) + sz * 0.5f > half) continue;
                    // 곡선 길 위에 건물이 앉는 경우를 걸러낸다(사분면 배치만으로는 안 걸린다).
                    float bwx = origin.x + cx, bwz = origin.z + cz;
                    if (Mathf.Abs(bwx - PathCenterX(bwz)) < PathClear + sx * 0.5f) continue;
                    if (Mathf.Abs(bwz - PathCenterZ(bwx)) < PathClear + sz * 0.5f) continue;
                    GameObject b;
                    if (bi >= 0)
                    {
                        var d = _buildings[bi];
                        // 그룹 이름 = 모델 이름. 풀링 키이자 ProtoObstacles 의 그룹 키다(축소율은 기본 0.85).
                        b = Take(d.prefab.name, d.prefab, null);
                        float yaw = quarter * 90f;
                        // 메시 중심이 피벗에서 살짝 비껴 있을 수 있어, 회전된 중심 오프셋을 빼서 발자국 중심을 맞춘다.
                        Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(d.centre.x, 0f, d.centre.y);
                        Place(b, origin + new Vector3(cx - off.x, d.lift, cz - off.z), yaw, Vector3.one, d.prefab.transform.localRotation);
                    }
                    else
                    {
                        b = Take("Building", null, mConcrete);
                        Place(b, origin + new Vector3(cx, sy * 0.5f, cz), 0f, new Vector3(sx, sy, sz));
                    }
                    c.props.Add(b);
                }
            }

            // ── 프롭 ──
            for (int k = 0; k < _kinds.Length; k++)
            {
                var kind = _kinds[k];
                for (int i = 0; i < kind.perChunk; i++)
                {
                    Vector2 p = kind.offRoad ? OffPathPoint(rng, half, origin) : AnyPoint(rng, half);
                    var go = Take(kind.name, kind.prefab, kind.mat);
                    float yaw = (float)rng.NextDouble() * 360f;
                    if (kind.prefab != null)
                        Place(go, origin + new Vector3(p.x, kind.lift, p.y), yaw, Vector3.one * kind.scale, kind.prefab.transform.localRotation);
                    else
                        Place(go, origin + new Vector3(p.x, kind.boxSize.y * 0.5f, p.y), yaw, kind.boxSize);
                    c.props.Add(go);
                }
            }

            c.tris = CountTris(c);
            return c;
        }

        Vector2 AnyPoint(System.Random rng, float half)
        {
            float m = half - 2f;
            return new Vector2((float)(rng.NextDouble() * 2 - 1) * m, (float)(rng.NextDouble() * 2 - 1) * m);
        }

        /// 길에서 떨어진 점. 길이 곡선이라 좌표로 잘라낼 수 없고, 그 z(또는 x)에서의
        /// 길 중심을 실제로 계산해 거리를 재야 한다.
        Vector2 OffPathPoint(System.Random rng, float half, Vector3 origin)
        {
            float clear = PathClear;
            for (int t = 0; t < 16; t++)
            {
                var p = AnyPoint(rng, half);
                float wx = origin.x + p.x, wz = origin.z + p.y;
                if (Mathf.Abs(wx - PathCenterX(wz)) > clear && Mathf.Abs(wz - PathCenterZ(wx)) > clear) return p;
            }
            return AnyPoint(rng, half);   // 16번 실패하면 그냥 둔다. 길 위 프롭 하나가 무한 루프보다 싸다
        }

        // ── 길 곡선 ─────────────────────────────────────────────────────
        // **월드 좌표만의 함수다.** 청크 좌표나 청크 시드가 섞이면 이음매에서 길이 끊긴다.
        // 주기가 서로 나누어떨어지지 않는 사인 두 개를 더해 "반복되는 티"를 없앤다.
        float PathCenterX(float worldZ)
        {
            return Mathf.Sin(worldZ * 0.031f) * pathWander
                 + Mathf.Sin(worldZ * 0.0117f + 1.7f) * pathWander * 0.55f;
        }

        float PathCenterZ(float worldX)
        {
            return Mathf.Sin(worldX * 0.027f + 2.3f) * pathWander
                 + Mathf.Sin(worldX * 0.0093f + 0.4f) * pathWander * 0.6f;
        }

        /// 가장자리 요철. **월드 좌표만의 함수**여야 청크 경계에서 요철이 이어진다.
        /// 주기가 3배씩 벌어진 사인 세 개를 더해 "일정한 물결"이 아니라 "불규칙한 테두리"로 보이게 한다.
        /// phase 를 층·좌우마다 다르게 주면 흙길과 갓길의 테두리가 따로 놀아 손으로 그린 것처럼 읽힌다.
        float EdgeNoise(float worldT, float phase)
        {
            float k = edgeDetail;
            return Mathf.Sin(worldT * k + phase) * 0.55f
                 + Mathf.Sin(worldT * k * 2.7f + phase * 2.1f) * 0.30f
                 + Mathf.Sin(worldT * k * 6.3f + phase * 0.7f) * 0.15f;
        }

        // ── 길 층 정의 ──────────────────────────────────────────────────
        // 재 테두리 → 자갈 갓길 → 흙길. 폭은 LayerWidth, 위상은 세로(Z)/가로(X) 길마다 따로.
        // yZ/yX 는 적층 경로 전용(층 사이 z-fighting 을 피하는 0.001 계단).
        struct Layer { public float phaseZ, phaseX, yZ, yX; }
        static readonly Layer[] Layers =
        {
            new Layer { phaseZ =  3.7f, phaseX = 11.3f, yZ = 0.006f, yX = 0.007f },   // 재
            new Layer { phaseZ = 19.4f, phaseX = 27.8f, yZ = 0.010f, yX = 0.011f },   // 자갈
            new Layer { phaseZ =  5.2f, phaseX = 13.9f, yZ = 0.020f, yX = 0.021f },   // 흙길
        };

        float LayerWidth(int i)
        {
            return i == 0 ? pathWidth * shoulderMult * 1.28f
                 : i == 1 ? pathWidth * shoulderMult
                 : pathWidth;
        }

        /// 한 길(세로 또는 가로)에서 점이 경계 안쪽으로 몇 m 들어와 있는지. 음수면 바깥.
        /// **AddPath 의 스트립 모양과 같은 식이다** — 폭 흔들림, 좌우 따로 노는 요철, 축 방향 거리.
        float PathInside(float along, float across, bool alongZ, float width, float phase)
        {
            float centre = alongZ ? PathCenterX(along) : PathCenterZ(along);
            float d = across - centre;
            float w = width * (0.82f + 0.18f * Mathf.Sin(along * 0.07f));
            // 좌표가 작은 쪽 = eL(phase), 큰 쪽 = eR(phase + 37.1). 두 축 모두 같다.
            float e = edgeRough * EdgeNoise(along, d < 0f ? phase : phase + 37.1f);
            return w * 0.5f + e - Mathf.Abs(d);
        }

        /// 월드 점에서 층 i 의 덮임 정도(0~1, 0.5 = 경계선). 세로·가로 길 중 더 깊이 덮인 쪽.
        float Coverage(int i, float wx, float wz)
        {
            float width = LayerWidth(i);
            float inZ = PathInside(wz, wx, true,  width, Layers[i].phaseZ);
            float inX = PathInside(wx, wz, false, width, Layers[i].phaseX);
            return Mathf.Clamp01(0.5f + Mathf.Max(inZ, inX) / splatBlend);
        }

        /// 풀 덮임(0~1, 0.5 = 경계). 큰 덩어리 + 잔 요철 두 옥타브. 오프셋은 음수 좌표에서
        /// 펄린이 원점 대칭 무늬를 내지 않도록 멀리 밀어 둔 것.
        float GrassCoverage(float wx, float wz)
        {
            if (grassAmount <= 0f) return 0f;
            float f = 1f / grassPatch;
            float n = Mathf.PerlinNoise(wx * f + 1731.3f, wz * f + 911.7f) * 0.7f
                    + Mathf.PerlinNoise(wx * f * 3.1f + 77.1f, wz * f * 3.1f + 4051.9f) * 0.3f;
            // 펄린 값은 대략 0.2~0.8 에 몰린다 — 그 구간에서 grassAmount 가 문턱을 옮긴다.
            float threshold = Mathf.Lerp(0.72f, 0.28f, grassAmount);
            return Mathf.Clamp01(0.5f + (n - threshold) * 6f);
        }

        // ── 풀 카드 ─────────────────────────────────────────────────────
        // **카메라에 보이는 땅에만 심는다.** 처음엔 청크(40m)마다 통째로 심었는데, 9청크에 6,800포기 · 2.7만 tri 였고
        // 화면에 조금만 걸린 청크도 풀 전부를 그렸다. 세로 화면에서 실제로 보이는 땅은 그 몇 분의 1이다.
        // 그래서 풀만 청크와 따로, 작은 타일(foliageTile) 단위로 카메라 절두체가 땅에 닿는 사각형 + 여유 안에서만 만든다.
        // 후보점이 월드 격자 + 좌표 해시라, 타일을 버렸다 다시 만들어도 같은 자리에 같은 풀이 난다.
        const float PondMeshRadius = 1.7f;                    // ShallowPond_Test 디스크 반경(배율 1 기준)

        class FoliageTile { public GameObject go; public Mesh mesh; public int clumps; }
        readonly Dictionary<Vector2Int, FoliageTile> _foliage = new Dictionary<Vector2Int, FoliageTile>();
        readonly Stack<FoliageTile> _foliagePool = new Stack<FoliageTile>();
        readonly List<Vector2Int> _foliageWanted = new List<Vector2Int>();
        readonly List<Vector2Int> _foliageDoomed = new List<Vector2Int>();
        // 메시 생성용 버퍼 — 타일마다 새로 할당하지 않는다
        readonly List<Vector3> _fv = new List<Vector3>();
        readonly List<Vector2> _fuv = new List<Vector2>();
        readonly List<Vector3> _fn = new List<Vector3>();
        readonly List<int> _ft = new List<int>();
        Transform _foliageRoot;
        Rect _foliageView;          // 마지막으로 잰 '보이는 땅' (HUD·기즈모용)

        // 아틀라스 칸(UV 좌하단) · 실제 높이(m). pack_atlas.py 배치와 같아야 한다.
        static readonly Vector2[] FoliageCell = { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f) };
        static readonly float[]   FoliageHeight = { 1.00f, 1.35f, 0.70f, 0.85f };   // 풀더미 · 귀리 · 넓은잎 · 들국화
        // 종류 비율 — 풀밭 한가운데 / 맨 잔해밭. 풀밭은 풀더미 위주에 귀리·들꽃, 맨땅은 넓은잎 잡초 위주.
        static readonly float[] FoliageMixGrass = { 0.55f, 0.20f, 0.10f, 0.15f };
        static readonly float[] FoliageMixBare  = { 0.40f, 0.10f, 0.50f, 0.00f };

        /// 좌표만으로 정해지는 0~1 난수 k 번째. 후보점마다 System.Random 을 만들지 않으려고.
        static float Hash01(int ix, int iz, int k)
        {
            unchecked
            {
                uint h = (uint)ix * 0x9E3779B1u ^ (uint)iz * 0x85EBCA77u ^ (uint)k * 0xC2B2AE3Du ^ 0x5A17u;
                h ^= h >> 16; h *= 0x7FEB352Du; h ^= h >> 15; h *= 0x846CA68Bu; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// 카메라 절두체가 땅(y=0)에 닿는 사각형. 네 모서리 광선을 평면에 쏜다.
        /// 지평선 위로 빠지는 광선(각도 2 처럼 낮은 피치)은 maxDist 에서 자른다 — 거기부턴 안개라 풀이 안 보인다.
        public static Rect GroundViewRect(Camera cam, float maxDist = 45f)
        {
            float x0 = float.MaxValue, x1 = float.MinValue, z0 = float.MaxValue, z1 = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                var ray = cam.ViewportPointToRay(new Vector3(k & 1, k >> 1, 0f));
                float d = ray.direction.y < -1e-4f ? -ray.origin.y / ray.direction.y : maxDist;
                var p = ray.GetPoint(Mathf.Min(d, maxDist));
                x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x);
                z0 = Mathf.Min(z0, p.z); z1 = Mathf.Max(z1, p.z);
            }
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        /// 보이는 땅 + 여유 안의 타일만 살려 둔다. 청크 스트리밍과 독립이다.
        /// <para>아래 가장자리 여유가 특히 필요하다 — 카드 윗부분(최대 ~1.7m)이 땅 사각형 밖에서 화면 안으로 솟는다.</para>
        public void UpdateFoliage(Camera cam)
        {
            if (mFoliage == null || cam == null) return;
            if (_foliageRoot == null) _foliageRoot = new GameObject("_Foliage").transform;

            _foliageView = GroundViewRect(cam);
            float m = foliageMargin;
            int tx0 = Mathf.FloorToInt((_foliageView.xMin - m) / foliageTile), tx1 = Mathf.FloorToInt((_foliageView.xMax + m) / foliageTile);
            int tz0 = Mathf.FloorToInt((_foliageView.yMin - m) / foliageTile), tz1 = Mathf.FloorToInt((_foliageView.yMax + m) / foliageTile);

            // 버리기 — 한 칸 더 벗어나야 버린다(경계에서 만들었다 버렸다 반복 방지)
            _foliageDoomed.Clear();
            foreach (var kv in _foliage)
                if (kv.Key.x < tx0 - 1 || kv.Key.x > tx1 + 1 || kv.Key.y < tz0 - 1 || kv.Key.y > tz1 + 1) _foliageDoomed.Add(kv.Key);
            for (int i = 0; i < _foliageDoomed.Count; i++)
            {
                var t = _foliage[_foliageDoomed[i]];
                t.go.SetActive(false);
                _foliagePool.Push(t);
                _foliage.Remove(_foliageDoomed[i]);
            }

            // 만들기 — 카메라 중심에 가까운 것부터, 한 프레임에 foliageBuildsPerFrame 개까지
            _foliageWanted.Clear();
            for (int z = tz0; z <= tz1; z++)
                for (int x = tx0; x <= tx1; x++)
                {
                    var key = new Vector2Int(x, z);
                    if (!_foliage.ContainsKey(key)) _foliageWanted.Add(key);
                }
            if (_foliageWanted.Count == 0) return;
            Vector2 c = _foliageView.center / foliageTile;
            _foliageWanted.Sort((a, b) => ((Vector2)a - c).sqrMagnitude.CompareTo(((Vector2)b - c).sqrMagnitude));
            int n = Mathf.Min(foliageBuildsPerFrame, _foliageWanted.Count);
            for (int i = 0; i < n; i++) _foliage[_foliageWanted[i]] = BuildFoliageTile(_foliageWanted[i]);
        }

        FoliageTile TakeFoliageTile()
        {
            if (_foliagePool.Count > 0) { var t = _foliagePool.Pop(); t.go.SetActive(true); return t; }
            var nt = new FoliageTile { mesh = new Mesh { name = "Foliage" } };
            nt.mesh.MarkDynamic();
            nt.go = new GameObject("FoliageTile");
            nt.go.transform.SetParent(_foliageRoot, false);
            nt.go.AddComponent<MeshFilter>().sharedMesh = nt.mesh;
            var mr = nt.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mFoliage;
            // 수천 장의 카드 그림자는 모바일에서 비싸고, 툰 룩에서는 거의 안 보인다.
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return nt;
        }

        /// 타일 하나에 풀 카드를 심는다. 정점은 월드 좌표(타일 오브젝트는 원점).
        ///
        /// <para>카드 방향: 이 게임 카메라는 피치만 있고 요(yaw)가 없어 늘 북쪽(+Z)을 본다. 그래서 교차 카드 대신
        /// 카메라 쪽을 향한 두 장을 V 자로 벌린다 — 두 장 다 앞면이라 양면 렌더링이 필요 없다.
        /// (BCM 메인 패스는 뒷면에서 노멀을 뒤집어, 양면으로 그리면 카드 뒷면이 어둡게 뜬다.)</para>
        /// <para>노멀은 전부 위(+Y). 카드 자체 노멀을 쓰면 V 의 두 장이 서로 다른 밝기로 칠해져 종이처럼 보인다 —
        /// 위를 보게 하면 바닥과 같은 빛을 받아 땅에서 자란 것처럼 붙는다.</para>
        FoliageTile BuildFoliageTile(Vector2Int key)
        {
            var tile = TakeFoliageTile();
            tile.go.name = $"FoliageTile_{key.x}_{key.y}";
            float x0 = key.x * foliageTile, z0 = key.y * foliageTile, x1 = x0 + foliageTile, z1 = z0 + foliageTile;
            int i0 = Mathf.FloorToInt(x0 / foliageStep), i1 = Mathf.CeilToInt(x1 / foliageStep);
            int j0 = Mathf.FloorToInt(z0 / foliageStep), j1 = Mathf.CeilToInt(z1 / foliageStep);

            _fv.Clear(); _fuv.Clear(); _fn.Clear(); _ft.Clear();
            // 윗변을 카메라 **반대쪽(+Z)** 으로 눕힌다. 카메라는 남쪽 위에서 58° 로 내려다보므로, 수직 카드는 화면에서
            // cos 로 53% 까지 납작해진다. **카메라 쪽(-Z)으로 눕히면 오히려 시선과 나란해져 14% 로 찌그러진다**
            // (처음에 그렇게 해서 풀이 전부 X 자 표시처럼 보였다). 반대로 25° 면 84% 로 서 보인다.
            float lean = Mathf.Tan(25f * Mathf.Deg2Rad);

            for (int j = j0; j < j1; j++)
                for (int i = i0; i < i1; i++)
                {
                    float wx = (i + Hash01(i, j, 1)) * foliageStep;
                    float wz = (j + Hash01(i, j, 2)) * foliageStep;
                    // 경계 위의 점은 한 타일만 갖는다(반열린 구간)
                    if (wx < x0 || wx >= x1 || wz < z0 || wz >= z1) continue;
                    // 월드 밖(스트리밍 경계 너머)엔 지면이 없다
                    if (worldHalfChunks > 0 && (Mathf.Abs(wx) > (worldHalfChunks + 0.5f) * chunkSize || Mathf.Abs(wz) > (worldHalfChunks + 0.5f) * chunkSize)) continue;

                    // **싼 것부터 거른다.** 풀 덮임(펄린 2회)만으로 확률 상한을 내고 주사위를 먼저 굴린다 —
                    // 맨땅은 상한이 foliageSparse(0.06)라 후보의 ~94% 가 길 공식(사인 수십 번)까지 안 간다.
                    float g = GrassCoverage(wx, wz);
                    float tg = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.65f, g));
                    float dice = Hash01(i, j, 3);
                    float pMax = Mathf.Lerp(foliageSparse, foliageDense, tg);
                    if (dice >= pMax) continue;

                    float path = Mathf.Max(Coverage(1, wx, wz), Coverage(2, wx, wz));
                    if (path > 0.4f) continue;                                    // 자갈·흙길은 비운다
                    float ash = Mathf.Min(Coverage(0, wx, wz), 1f - g);           // SplatGround 와 같은 식
                    if (dice >= pMax * (1f - ash) * (1f - path * 2f)) continue;
                    if (InPond(wx, wz)) continue;

                    // 종류 고르기 — 풀밭 비율과 맨땅 비율을 풀 덮임으로 섞는다
                    float pick = Hash01(i, j, 4), acc = 0f; int kind = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        acc += Mathf.Lerp(FoliageMixBare[k], FoliageMixGrass[k], tg);
                        if (pick < acc) { kind = k; break; }
                    }

                    float h = FoliageHeight[kind] * Mathf.Lerp(foliageScale.x, foliageScale.y, Hash01(i, j, 5));
                    float w = h;                                  // 칸이 정사각형
                    float spread = Mathf.Lerp(20f, 40f, Hash01(i, j, 6)) * Mathf.Deg2Rad;
                    float turn = (Hash01(i, j, 7) - 0.5f) * 20f * Mathf.Deg2Rad;
                    Vector3 basePos = new Vector3(wx, -0.03f, wz);   // 3cm 박아 밑동 틈을 없앤다
                    Vector2 cell = FoliageCell[kind];

                    for (int side = 0; side < 2; side++)
                    {
                        float yaw = turn + (side == 0 ? spread : -spread);
                        var right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (w * 0.5f);
                        var up = new Vector3(0f, h, h * lean);
                        // 두 번째 장은 좌우 반전 — 같은 그림이 V 로 마주 보여 대칭 티가 나지 않게
                        bool flip = (side == 1) ^ (Hash01(i, j, 8) < 0.5f);
                        float u0 = cell.x + (flip ? 0.5f : 0f), u1 = cell.x + (flip ? 0f : 0.5f);
                        int b = _fv.Count;
                        _fv.Add(basePos - right);      _fuv.Add(new Vector2(u0, cell.y));
                        _fv.Add(basePos - right + up); _fuv.Add(new Vector2(u0, cell.y + 0.5f));
                        _fv.Add(basePos + right + up); _fuv.Add(new Vector2(u1, cell.y + 0.5f));
                        _fv.Add(basePos + right);      _fuv.Add(new Vector2(u1, cell.y));
                        for (int q = 0; q < 4; q++) _fn.Add(Vector3.up);
                        // 카메라(-Z 쪽)에서 봤을 때 시계 방향 = 앞면
                        _ft.Add(b); _ft.Add(b + 1); _ft.Add(b + 2);
                        _ft.Add(b); _ft.Add(b + 2); _ft.Add(b + 3);
                    }
                }

            tile.mesh.Clear();
            tile.mesh.SetVertices(_fv); tile.mesh.SetUVs(0, _fuv); tile.mesh.SetNormals(_fn); tile.mesh.SetTriangles(_ft, 0);
            tile.mesh.RecalculateBounds();
            tile.clumps = _fv.Count / 8;
            return tile;
        }

        /// 살아 있는 청크들의 웅덩이 안인가. 웅덩이는 청크 rng 로 놓이므로 청크가 기억한다.
        bool InPond(float wx, float wz)
        {
            foreach (var kv in _live)
            {
                var ponds = kv.Value.ponds;
                for (int k = 0; k < ponds.Count; k++)
                {
                    float dx = wx - ponds[k].x, dz = wz - ponds[k].y, r = ponds[k].z + 0.3f;
                    if (dx * dx + dz * dz < r * r) return true;
                }
            }
            return false;
        }

        int FoliageClumps() { int n = 0; foreach (var kv in _foliage) n += kv.Value.clumps; return n; }


        /// 청크 지면 한 장. 격자 정점마다 층 덮임을 정점 컬러로 굽는다(RGB = 재·자갈·흙길, A = 풀).
        /// 정점 좌표가 월드 기준 격자라 이웃 청크의 경계 정점과 값이 비트 단위로 같다 — 이음매가 없다.
        void SplatGround(Chunk c, Transform parent, Vector3 origin)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(chunkSize / splatCell));
            float step = chunkSize / n, half = chunkSize * 0.5f;
            int row = n + 1;
            var v   = new Vector3[row * row];
            var col = new Color32[row * row];
            var nrm = new Vector3[row * row];
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    int k = z * row + x;
                    float lx = -half + x * step, lz = -half + z * step;
                    float wx = origin.x + lx, wz = origin.z + lz;
                    v[k] = new Vector3(lx, 0f, lz);
                    nrm[k] = Vector3.up;
                    float grass = GrassCoverage(wx, wz);
                    // **풀밭에서는 재 테두리를 지운다.** 풀과 자갈 사이에 검은 재 띠가 끼면 풀밭이 불탄
                    // 자국에 잘린 것처럼 보였다. 풀 덮임이 경계(0.5)를 넘는 곳은 재도 경계 아래로 눌러,
                    // 풀이 곧장 자갈·흙길로 이어지게 한다. 맨 잔해밭 쪽에만 재가 남는다.
                    float ash = Mathf.Min(Coverage(0, wx, wz), 1f - grass);
                    col[k] = new Color32(
                        (byte)(ash * 255f + 0.5f),
                        (byte)(Coverage(1, wx, wz) * 255f + 0.5f),
                        (byte)(Coverage(2, wx, wz) * 255f + 0.5f),
                        (byte)(grass * 255f + 0.5f));
                }

            var tris = new int[n * n * 6];
            for (int z = 0, t = 0; z < n; z++)
                for (int x = 0; x < n; x++, t += 6)
                {
                    int a = z * row + x, b = a + 1, d = a + row, e = d + 1;
                    // QuadWorld 와 같은 감김(위에서 보이는 면).
                    tris[t] = a; tris[t + 1] = e; tris[t + 2] = b;
                    tris[t + 3] = a; tris[t + 4] = d; tris[t + 5] = e;
                }

            var mesh = new Mesh { name = "SplatGround" };
            mesh.vertices = v; mesh.normals = nrm; mesh.colors32 = col; mesh.triangles = tris;
            // UV 는 셰이더가 월드 XZ 로 직접 만든다. 그래도 채널이 비면 경고가 나는 파이프라인이 있어 채워 둔다.
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(origin.x + v[i].x, origin.z + v[i].z);
            mesh.uv = uv;
            mesh.RecalculateBounds();
            c.meshes.Add(mesh);

            var go = new GameObject("Splat");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mSplat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// 청크가 자기 구간의 길을 쿼드 스트립으로 만든다.
        /// 양 끝을 청크 밖으로 한 칸 더 뽑는 이유: 이웃 청크와 살짝 겹쳐야 경계에 실틈이 안 보인다.
        void AddPath(Chunk c, Transform parent, Vector2Int coord, bool alongZ, float width, Material mat, float y, float phase, float tile)
        {
            if (mat == null) return;

            // 요철 주기가 2m 안팎까지 내려가므로 1m 간격으로 떠야 모양이 살아난다.
            // 16 단계(2.5m)로 뜨면 고주파 항이 샘플에 안 걸려 그냥 직선처럼 보인다.
            const int Steps = 40;
            float half = chunkSize * 0.5f;
            float step = chunkSize / Steps;
            float ext  = step;                       // 겹침 한 칸

            int n = Steps + 3;                       // 양쪽 겹침 포함
            var verts = new Vector3[n * 2];
            var uvs   = new Vector2[n * 2];
            var tris  = new int[(n - 1) * 6];
            Vector3 origin = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);

            for (int i = 0; i < n; i++)
            {
                float t = -half - ext + i * step;    // 청크 로컬 축 좌표
                float worldT = (alongZ ? origin.z : origin.x) + t;
                float w = width * (0.82f + 0.18f * Mathf.Sin(worldT * 0.07f));
                // 좌우를 서로 다른 위상으로 흔든다. 같은 값을 쓰면 폭만 일정한 채 뱀처럼 휘어
                // '울퉁불퉁한 테두리'가 아니라 '구불구불한 띠'가 된다.
                float eL = edgeRough * EdgeNoise(worldT, phase);
                float eR = edgeRough * EdgeNoise(worldT, phase + 37.1f);
                float centre;
                Vector3 a, b;
                if (alongZ)
                {
                    centre = PathCenterX(origin.z + t) - origin.x;
                    a = new Vector3(centre - w * 0.5f - eL, 0f, t);
                    b = new Vector3(centre + w * 0.5f + eR, 0f, t);
                }
                else
                {
                    centre = PathCenterZ(origin.x + t) - origin.z;
                    // **a/b 를 뒤집는다.** 축을 바꾸면 삼각형 감김이 반대가 되어 면이 아래를 보고,
                    // 백페이스 컬링에 먹혀 가로 길이 통째로 안 보인다(실제로 그렇게 나왔다).
                    a = new Vector3(t, 0f, centre + w * 0.5f + eR);
                    b = new Vector3(t, 0f, centre - w * 0.5f - eL);
                }
                verts[i * 2] = a; verts[i * 2 + 1] = b;
                // **UV 는 폭을 0~1 로 펴지 않고 월드 평면으로 찍는다.** 0~1 로 펴면 폭 3m 짜리 띠에
                // 텍스처 한 장이 가로로 눌려 들어가 알갱이가 늘어나고, 결국 단색처럼 보인다.
                // 월드 평면이면 바닥과 텍셀 밀도가 같아져 띠가 '칠한 면'이 아니라 '깔린 땅'으로 읽힌다.
                uvs[i * 2]     = new Vector2((origin.x + a.x) / tile, (origin.z + a.z) / tile);
                uvs[i * 2 + 1] = new Vector2((origin.x + b.x) / tile, (origin.z + b.z) / tile);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int o = i * 6, q = i * 2;
                tris[o] = q; tris[o + 1] = q + 2; tris[o + 2] = q + 1;
                tris[o + 3] = q + 1; tris[o + 4] = q + 2; tris[o + 5] = q + 3;
            }

            var mesh = new Mesh { name = "Path" };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            var up = new Vector3[verts.Length];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;
            mesh.RecalculateBounds();
            c.meshes.Add(mesh);

            var go = new GameObject(alongZ ? "Path_Z" : "Path_X");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ── 인스턴스 풀 ─────────────────────────────────────────────────

        Transform Group(string name)
        {
            Transform t;
            if (_groups.TryGetValue(name, out t) && t != null) return t;
            var existing = _propsRoot.Find(name);
            t = existing != null ? existing : new GameObject(name).transform;
            if (existing == null) t.SetParent(_propsRoot, false);
            _groups[name] = t;
            return t;
        }

        GameObject Take(string kind, GameObject prefab, Material mat)
        {
            Stack<GameObject> s;
            if (_pool.TryGetValue(kind, out s) && s.Count > 0)
            {
                var reused = s.Pop();
                reused.SetActive(true);
                return reused;
            }

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab);
                if (mat != null)
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true)) mr.sharedMaterial = mat;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());   // 충돌은 ProtoObstacles 가 본다(§16, Unity Physics 미사용)
                if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            go.name = kind;
            go.transform.SetParent(Group(kind), false);
            return go;
        }

        static void Place(GameObject go, Vector3 pos, float yaw, Vector3 scale) { Place(go, pos, yaw, scale, Quaternion.identity); }

        /// baseRot = 모델 루트에 원래 붙어 있던 회전. 3ds Max·블렌더 FBX 는 루트에 X 270°/-90° 가 붙어 온다 —
        /// yaw 로 덮어쓰면 모델이 옆으로 눕는다(container_01 이 그랬다). 그 위에 yaw 만 얹는다.
        static void Place(GameObject go, Vector3 pos, float yaw, Vector3 scale, Quaternion baseRot)
        {
            var t = go.transform;
            t.position = pos;
            t.rotation = Quaternion.Euler(0f, yaw, 0f) * baseRot;
            t.localScale = scale;
        }

        void Recycle(Chunk c)
        {
            for (int i = 0; i < c.props.Count; i++)
            {
                var go = c.props[i];
                if (go == null) continue;
                go.SetActive(false);
                Stack<GameObject> s;
                if (!_pool.TryGetValue(go.name, out s)) { s = new Stack<GameObject>(); _pool[go.name] = s; }
                s.Push(go);
            }
            c.props.Clear();
            if (c.ground != null) Destroy(c.ground);   // 지면은 청크마다 크기가 같아 풀링 이득이 없다
            // GameObject 만 지우면 길 메시가 메모리에 그대로 남는다. 오래 돌면 이것만으로 샌다.
            for (int i = 0; i < c.meshes.Count; i++) if (c.meshes[i] != null) Destroy(c.meshes[i]);
            c.meshes.Clear();
        }

        // ── 메시 유틸 ───────────────────────────────────────────────────

        void QuadWorld(Chunk c, Transform parent, string name, float size, Material mat, float y, float tile, Vector3 origin)
        {
            if (mat == null) return;
            float h = size * 0.5f;
            var mesh = new Mesh { name = "Ground" };
            var v = new[]
            {
                new Vector3(-h, 0f, -h), new Vector3(h, 0f, -h),
                new Vector3(h, 0f,  h), new Vector3(-h, 0f,  h)
            };
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++)
                uv[i] = new Vector2((origin.x + v[i].x) / tile, (origin.z + v[i].z) / tile);
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            c.meshes.Add(mesh);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static int Hash(int x, int z, int s)
        {
            unchecked
            {
                // **예전 식(s*73856093 ^ x*19349663 ^ z*83492791)은 대각선 청크끼리 시드가 부호만 달랐다.**
                // (-1,-1)=8736669 / (1,-1)=-8736669 — System.Random 은 음수 시드에 절댓값을 쓰므로
                // 네 귀퉁이 청크의 프롭 배치가 똑같이 복사됐다(80m 떨어진 곳에 같은 건물이 같은 자리에).
                // 좌표를 섞은 뒤 비트를 흩뜨리고(murmur3 fmix) 부호 비트를 떼어 항상 양수로 만든다.
                uint h = (uint)s ^ (uint)x * 0x9E3779B1u ^ (uint)z * 0x85EBCA77u;
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        static int TrisOf(GameObject go)
        {
            int n = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null) continue;
                for (int i = 0; i < m.subMeshCount; i++) n += (int)(m.GetIndexCount(i) / 3);
            }
            return n;
        }

        static int CountTris(Chunk c)
        {
            int n = c.ground != null ? TrisOf(c.ground) : 0;
            for (int i = 0; i < c.props.Count; i++) if (c.props[i] != null) n += TrisOf(c.props[i]);
            return n;
        }

        void RecountTris()
        {
            int n = 0;
            foreach (var kv in _live) n += kv.Value.tris;
            _totalTris = n;
        }

        // ── HUD ─────────────────────────────────────────────────────────

        void OnGUI()
        {
            if (!showHud) return;
            if (ProtoHUD.CaptureMode) return;   // UI 화면 캡처(F10) 중에는 숨긴다

            const float w = 300f, h = 168f;
            var r = new Rect(8f, Screen.height - h - 8f, w, h);
            GUI.Box(r, GUIContent.none);

            var st = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            GUILayout.BeginArea(new Rect(r.x + 10f, r.y + 8f, r.width - 20f, r.height - 16f));

            int side = preloadRings * 2 + 1;
            GUILayout.Label($"<b>배경 스트리밍</b>   청크 {chunkSize:F0}m", st);
            GUILayout.Label($"미리 불러오기  <b>±{preloadRings}</b>  ({side}×{side} = {side * side}칸, 반경 {(preloadRings + 0.5f) * chunkSize:F0}m)", st);
            GUILayout.Label($"버리는 거리    {(preloadRings + 0.5f) * chunkSize + unloadMargin:F0}m  (여유 {unloadMargin:F0}m)", st);
            GUILayout.Label($"살아있는 청크  <b>{_live.Count}</b>   풀 대기 {PoolCount()}", st);
            GUILayout.Label($"삼각형 합계    <b>{_totalTris:N0}</b>", st);
            GUILayout.Label($"청크 1개 생성  {_lastBuildMs:F2} ms  (최악 {_worstBuildMs:F2} ms)", st);
            GUILayout.Label($"풀 타일 {_foliage.Count}  포기 <b>{FoliageClumps():N0}</b>  ({FoliageClumps() * 4:N0} tri)", st);
            GUILayout.Label($"<color=#9fd>[ / ] 반경 조절   \\ HUD</color>", st);

            GUILayout.EndArea();
        }

        int PoolCount()
        {
            int n = 0;
            foreach (var kv in _pool) n += kv.Value.Count;
            return n;
        }

        void OnDrawGizmosSelected()
        {
            if (_target == null) return;
            var here = CoordOf(_target.position);
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            for (int dz = -preloadRings; dz <= preloadRings; dz++)
                for (int dx = -preloadRings; dx <= preloadRings; dx++)
                    Gizmos.DrawWireCube(new Vector3((here.x + dx) * chunkSize, 0.2f, (here.y + dz) * chunkSize),
                                        new Vector3(chunkSize, 0.4f, chunkSize));
        }
    }
}
