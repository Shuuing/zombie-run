using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 경험치 젬. 픽업 반경 안에 들어오면 플레이어로 빨려온다 (자석).
    /// 개별 MonoBehaviour 대신 풀이 전부를 한 루프로 굴린다.
    public class ProtoGemPool : MonoBehaviour
    {
        public static ProtoGemPool I { get; private set; }

        public ProtoPlayer player;

        // ── 스프라이트 모드 (임시 테스트) ────────────────────────────────
        // 기존 박스 경로는 **하나도 지우지 않았다.** 배열을 비우면 그대로 큐브로 돌아간다.
        // 어느 그림이 나올지는 XP 로 정한다(값이 클수록 큰 물고기) — 순서가 곧 등급이다.
        [Header("스프라이트 모드 (임시 — 비우면 기존 큐브)")]
        [Tooltip("XP 낮은 순서. [0]=잡좀비, [1]=러너, [2]=보스. 비면 큐브를 쓴다.")]
        public Texture2D[] gemSprites;

        [Tooltip("스프라이트 크기 (월드 높이 m). 인덱스는 gemSprites 와 짝.")]
        public float[] gemSpriteSizes = { 0.42f, 0.52f, 0.95f };

        [Tooltip("위아래 살랑거림 속도(사인 계수)")]
        public float bobSpeed = 2.4f;

        // ── 떨어지고 튕기기 ─────────────────────────────────────────────
        // 예전에는 좀비가 죽은 자리에 y=0.35 로 **툭 나타났다**. 떨어지는 과정이 없으니
        // 파닥거림이 어디서 시작되는지도 안 읽힌다. 물리 엔진은 쓰지 않고(§16 방침)
        // 수직 속도만 적분해 바닥에서 튕긴다. 튕길 때마다 파닥이 다시 터진다 — 그게 "떨어져서
        // 파닥거린다"로 읽히는 지점이다.
        [Header("떨어지고 튕기기")]
        // 수치 근거 — 처음에 소심하게 잡았다가 "변한 게 없다"는 소리를 들었다.
        // dropUpSpeed 1.2 / bounciness 0.42 면 튕김이 **한 번, 11cm, 0.2초**다.
        // 카메라가 17.76m 떨어져 있고 물고기가 0.42m 라 그 정도는 화면에서 안 보인다.
        // 아래 값이면 정점 1.23m → 튕김 0.27m → 0.08m → 0.02m 로 **세 번** 튄다(총 약 1.1초).
        [Tooltip("바닥(눕는 높이)에서 얼마나 위에서 생겨나는지 m")]
        public float dropHeight = 0.75f;
        [Tooltip("생겨날 때 위로 튀는 초기 속도 m/s. 좀비에서 튀어나오는 느낌.")]
        public float dropUpSpeed = 2.6f;
        [Tooltip("중력 m/s². 현실(9.8)보다 세게 잡아야 경쾌하다.")]
        public float gravity = 26f;
        // 튕김 계수는 **체공 시간**을 정한다 → 수평 이동 거리를 정한다. 0.55 였을 때
        // 체공이 0.35 → 0.27 → 0.13s 로 급하게 줄어서 두 번째 홉부터 거리가 안 나왔다.
        [Tooltip("튕김 계수. 체공 시간을 좌우해 수평 이동 거리도 여기서 정해진다.")]
        public float bounciness = 0.65f;
        [Tooltip("이 속도 밑으로 떨어지면 그만 튄다 m/s. 낮추면 잔진동이 오래 남는다.")]
        public float bounceStopSpeed = 0.7f;

        // 좀비 시체가 죽은 자리에 2.2초(DeathLinger) 남아 있어서, 그 자리에 떨어지면 젬이 통째로
        // 가린다. 스폰 좌표를 순간이동으로 옮기지 않고 **수평 속도를 줘서 튀어나오게** 한다 —
        // 시체에서 빠져나오는 궤적 자체가 "저기서 나왔다"를 알려준다.
        // 2.0 이었을 때는 홉이 0.70 → 0.37m 였고, 방향 반전이 그걸 상쇄해서 최종 변위가
        // 0.64m 밖에 안 남았다 — 화면에서 "제자리"로 보였다. 3.2 면 첫 홉이 1.15m 다
        // (물고기 몸길이 0.42m 의 2.7배).
        [Tooltip("옆으로 튀어나가는 속도 m/s. 0 이면 좀비 자리에 그대로 떨어진다.")]
        public float scatterSpeed = 3.2f;
        [Tooltip("카메라 쪽(화면 아래)으로 치우치는 정도 0~1. 시체 뒤로 떨어지면 안 보이므로 앞쪽을 선호한다.")]
        [Range(0f, 1f)] public float scatterCameraBias = 0.55f;
        [Tooltip("튕길 때 수평 속도가 얼마나 남는지. 1 이면 계속 미끄러진다.")]
        [Range(0f, 1f)] public float scatterFriction = 0.8f;

        // 한 방향으로만 호를 그리면 "미끄러진다"로 보인다. 파닥거리는 물고기는 튕길 때마다
        // 엉뚱한 쪽으로 튄다 — 앞으로 갔다, 되돌아오고, 옆으로. 그래서 착지마다 방향을 다시 뽑는다.
        // 180° 근처면 왔던 자리로 돌아가고 90° 근처면 옆으로 간다.
        [Header("튕길 때마다 방향 바꾸기")]
        [Tooltip("착지할 때 이전 방향에서 꺾는 최소 각도")]
        public float bounceTurnMin = 80f;
        [Tooltip("착지할 때 이전 방향에서 꺾는 최대 각도. 180 을 넘으면 되돌아오는 쪽이 섞인다.")]
        public float bounceTurnMax = 200f;
        [Tooltip("바닥에 눕는 높이 m. 도로와 z-fighting 나지 않게 살짝 띄운다.")]
        public float restY = 0.35f;
        [Tooltip("떨어지는 동안 흔들리는 각도. 0 이면 판때기처럼 뻣뻣하게 떨어진다.")]
        public float airborneTiltDegrees = 14f;

        // ── 파닥거림 (정지 이미지 1장으로 낼 수 있는 것) ────────────────
        // **계속 파닥거리게 두면 안 된다.** 젬이 화면에 수십 개 깔리는 게임이라 전부가 계속
        // 움직이면 §15 의 "대비를 캐릭터에 몰아준다"가 깨져 플레이어를 못 찾는다.
        // 그래서 상태를 셋으로 나눈다: 착지 직후 세게 파닥 → 거의 정지 → 자석에 끌릴 때 다시 활발.
        [Header("파닥거림 — 착지 직후만 세게")]
        [Tooltip("착지 한 번당 파닥거리는 시간(초). 튕길 때마다 다시 시작된다.")]
        public float flopTime = 0.40f;
        [Tooltip("파닥일 때 화면 기준 좌우 회전 각도")]
        public float flopTiltDegrees = 34f;
        [Tooltip("파닥일 때 회전 속도(사인 계수). 느리면 '흐느적'으로 보인다.")]
        public float flopTiltSpeed = 30f;
        [Tooltip("바닥에 누운 뒤 파닥거리며 들썩이는 높이 m. 튕김과 별개다.")]
        public float flopHopHeight = 0.09f;

        [Header("파닥 이후 — 거의 정지")]
        [Tooltip("가만히 있을 때 갸웃하는 각도. 작게 둘 것.")]
        public float idleTiltDegrees = 2.5f;
        [Tooltip("가만히 있을 때 위아래 진폭 m (호흡)")]
        public float bobAmplitudeIdle = 0.02f;

        [Header("자석에 끌릴 때")]
        [Tooltip("끌려갈 때 갸웃하는 각도 — 헤엄쳐 오는 것처럼")]
        public float magnetTiltDegrees = 11f;
        // 2026-11-19 사용자 "물고기 흡수되는 속도를 빠르게" — 예전엔 일정 5m/s 라 3m 에서 0.6초,
        // 달리는 캐릭터(4.2~7.5m/s)를 겨우 따라붙거나 못 따라붙었다. 이제 출발 속도에서 가속한다:
        // 8 + 30t → 3m 를 약 0.25초, 자석 아이템 6m 를 약 0.42초. 이름을 바꾼 건 씬에 저장된 옛 값(5)을 버리려고.
        [Tooltip("플레이어에게 빨려오기 시작할 때 속도 m/s")]
        public float magnetStartSpeed = 8f;
        [Tooltip("빨려오는 동안 붙는 가속도 m/s² — 끌릴수록 빨라져 '쏙' 빨려 들어간다")]
        public float magnetAccel = 30f;
        [Tooltip("빨려오는 속도 상한 m/s")]
        public float magnetMaxSpeed = 24f;
        [Tooltip("끌려올 때 바닥에서 떠오르는 높이 m. 0 이면 바닥에 붙은 채 미끄러진다.")]
        public float magnetLift = 0.34f;
        [Tooltip("떠오르는 데 걸리는 시간 s. 이동 시간(3m 에서 약 0.25초)보다 짧아야 뜨는 게 보인다.")]
        public float magnetLiftTime = 0.18f;
        [Tooltip("끌려올 때 위아래로 헤엄치는 진폭 m")]
        public float magnetSwimAmplitude = 0.07f;
        [Tooltip("끌려올 때 위아래로 헤엄치는 빠르기")]
        public float magnetSwimSpeed = 13f;
        [Tooltip("끌려올 때 몸을 휘는 세기(셰이더 _Flop). 0 이면 뻣뻣하게 날아온다.")]
        [Range(0f, 1f)] public float magnetBend = 0.45f;

        [Header("공통")]
        [Tooltip("가로/세로가 서로 반대로 눌리는 양. 스쿼시&스트레치.")]
        public float swimSquash = 0.07f;
        [Tooltip("떨어질 때 톡 튀어나오는 시간. 0 이면 즉시 제 크기.")]
        public float popTime = 0.22f;
        [Tooltip("자석에 끌릴 때 머리를 진행 방향으로 돌린다(좌우 반전).")]
        public bool faceTravelDirection = true;
        [Tooltip("원본 그림이 왼쪽을 보고 있는지. 물고기 3장은 전부 왼쪽을 본다.")]
        public bool spritesFaceLeft = true;

        // ── 반짝이 · 흡수 꼬리 (2026-11-18 사용자 "떨어지는 물고기 주변에 반짝이, 캐릭터로 흡수될 때 꼬리 — 탕탕에서 가져와서") ──
        // 모양은 카탈로그 프리팹(gem_sparkle = 탕탕 드롭 별 반짝임 · gem_trail = 탕탕 얼음 화살 꼬리 띠 + 물방울을 노랗게, 띠 머리는 가늘게).
        // 프리팹 칸을 비우면 아무것도 안 나온다. 젬은 풀에서 돌려 쓰므로 앵커 대신 매 프레임 MoveTo 로 따라간다.
        [Header("반짝이 · 흡수 꼬리 (카탈로그 gem_sparkle · gem_trail)")]
        [Tooltip("바닥에 떨어진 물고기 둘레에 반짝이를 띄운다. 자석에 걸리면 사그라든다.")]
        public bool sparkleOnGround = true;
        [Tooltip("자석에 끌려오는 동안 꼬리를 단다. 캐릭터에 닿으면 꼬리가 짧아지며 사라진다.")]
        public bool trailWhileMagnet = true;
        // 2026-11-19 사용자 "꼬리 이펙트도 좀 줄여" — 굵기 2.4 → 1.6, 길이는 시간으로 0.3 → 0.07초.
        // 흡수가 빨라져(평균 ~12m/s) 0.3초 그대로면 꼬리가 3m 넘게 늘어난다. 0.07초면 0.6~1m(예전 1.5m).
        // 굵기 칸 이름을 바꾼 건 씬에 저장된 옛 값(2.4)을 버리려고.
        [Tooltip("꼬리 굵기 = 물고기 키 × 이 값(m) — 이 폭이 꼬리 띠의 가장 넓은 곳(프리팹 굵기 곡선 0.75 · 머리 0.05)에 곱해진다. 꼬리 그림의 빛줄기가 가운데 좁은 띠라 키보다 넓게 잡는다.")]
        public float trailWidthPerFishHeight = 1.6f;
        [Tooltip("꼬리 띠 길이(초) — 띠 길이 = 이 시간 × 끌려오는 속도. 프리팹 값(0.3)을 덮어쓴다. 0 이하면 프리팹 값 그대로.")]
        public float trailTime = 0.07f;

        bool UseSprites => gemSprites != null && gemSprites.Length > 0;

        class Gem
        {
            public Transform tf;
            public int xp;
            public bool magnetised;
            public bool sprite;      // 어느 풀로 돌려보낼지
            public float bobPhase;   // 같은 리듬으로 흔들리면 줄지어 춤추는 것처럼 보인다
            public float baseY;
            public Vector3 baseScale;  // 좌우 반전·스쿼시가 곱해질 원본 크기
            public float spawnAt;      // 톡 튀어나오는 연출 · 파닥거림 기준 시각
            public bool flipped;       // 진행 방향에 따라 좌우가 뒤집혔는지
            public Renderer mr;        // 젬마다 다른 파닥 세기를 셰이더에 넣으려면 필요하다
            public float vy;           // 수직 속도 (낙하·튕김)
            public Vector3 vh;         // 수평 속도 (좀비 옆으로 튀어나가기)
            public bool grounded;      // 다 튕기고 바닥에 누웠는지
            public float flopAt;       // 마지막 착지 시각 — 파닥 세기의 기준
            public float lastFlop = -1f;   // 마지막으로 MPB 에 넣은 파닥 값 — 같으면 다시 안 넣는다(2026-11-20)
            public float magnetAt;     // 자석에 걸린 시각 — 떠오르는 정도의 기준
            public float magnetY0;     // 걸린 순간의 높이. 공중에서 걸려도 안 튀게 여기서 출발한다
            public int tier;           // 0 작음 · 1 중간 · 2 큼 — 효과 크기
            public float height;       // 물고기 키 m — 꼬리 굵기
            public FxHandle sparkle;   // 바닥 반짝이(떨어질 때 ~ 자석에 걸릴 때)
            public FxHandle trail;     // 흡수 꼬리(자석에 걸릴 때 ~ 먹힐 때)
            public bool gold;          // 2 버전 금색 물고기(3D 메시) — 금색 물고기 풀로 돌려보낸다
        }

        readonly List<Gem> _live = new List<Gem>();
        readonly Queue<Transform> _pool = new Queue<Transform>();
        readonly Queue<Transform> _spritePool = new Queue<Transform>();
        readonly Queue<Transform> _goldPool = new Queue<Transform>();
        Transform _poolRoot;
        Material _matSmall, _matBig;
        Material[] _spriteMats;
        Mesh _quad;
        Camera _cam;
        MaterialPropertyBlock _mpb;
        static readonly int FlopID  = Shader.PropertyToID("_Flop");
        static readonly int PhaseID = Shader.PropertyToID("_Phase");

        public int LiveGems => _live.Count;

        void Awake()
        {
            I = this;
            _poolRoot = new GameObject("_GemPool").transform;
            _poolRoot.SetParent(transform, false);
            _matSmall = MakeMat(new Color(0.35f, 0.85f, 1f));
            _matBig   = MakeMat(new Color(1f, 0.55f, 0.95f));
            if (UseSprites) BuildSpriteAssets();
        }

        void OnDestroy() { if (I == this) I = null; }

        static Material MakeMat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            return m;
        }

        public void Drop(Vector3 pos, int xp)
        {
            pos.y = restY;
            // 모양 버전(2026-11-18) — Resources/Data/GemLook.asset. 2 = 금색 물고기(3D), 1(또는 에셋 없음) = 아래 예전 그대로(물고기 그림 · 큐브)
            if (ProtoGemLook.CurrentVersion == ProtoGemLook.Version.GoldFish) { DropSprite(pos, xp, gold: true); return; }
            if (UseSprites) { DropSprite(pos, xp); return; }

            Transform tf = _pool.Count > 0 ? _pool.Dequeue() : CreateGem();
            tf.SetParent(null, true);
            tf.position = pos;
            tf.localScale = Vector3.one * (xp >= 2 ? 0.34f : 0.24f);
            tf.GetComponent<MeshRenderer>().sharedMaterial = xp >= 2 ? _matBig : _matSmall;
            tf.gameObject.SetActive(true);
            var g = new Gem { tf = tf, xp = xp, magnetised = false, sprite = false, tier = xp >= 2 ? 1 : 0, height = tf.localScale.y };
            g.sparkle = StartSparkle(g);
            _live.Add(g);
        }

        Transform CreateGem()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "XPGem";
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }

        // ── 스프라이트 경로 (1 물고기 그림 · 2 금색 물고기 3D 가 같이 탄다) ──────────────
        // 2 금색 물고기(2026-11-18)는 메시만 다르고 상자 규약(x -0.5 코 … +0.5 꼬리, 머리 -x)이 그림과 같아서
        // 카메라 세우기 · 갸웃 · 좌우 반전 · 스쿼시 · 셰이더 몸 휨(_Flop · _Phase MPB)을 그대로 쓴다.
        void DropSprite(Vector3 pos, int xp, bool gold = false)
        {
            int idx;
            float h, aspect, depth;
            Transform tf;
            Material mat;
            if (gold)
            {
                var look = ProtoGemLook.Current;
                idx = TierFor(xp);
                h = look.GoldFishSize(idx);
                aspect = Mathf.Max(0.1f, look.goldFishAspect);
                depth = h * Mathf.Max(0.05f, look.goldFishThickness);   // 3D 라 두께도 키에 맞춘다
                tf = _goldPool.Count > 0 ? _goldPool.Dequeue() : CreateGoldFishGem();
                tf.GetComponent<MeshFilter>().sharedMesh = look.GoldMesh;   // 에셋에서 바꿨으면 다음 젬부터 반영
                mat = look.GoldMaterial;
                if (_mpb == null) _mpb = new MaterialPropertyBlock();       // 그림 배열이 비어 BuildSpriteAssets 를 안 탔을 때
            }
            else
            {
                idx = SpriteIndexFor(xp);
                var tex = gemSprites[idx];
                h = idx < gemSpriteSizes.Length ? gemSpriteSizes[idx] : 0.45f;
                // 정사각 쿼드에 그리면 물고기가 눌린다 — 텍스처 비율로 가로를 늘린다.
                aspect = (tex != null && tex.height > 0) ? tex.width / (float)tex.height : 1f;
                depth = 1f;
                tf = _spritePool.Count > 0 ? _spritePool.Dequeue() : CreateSpriteGem();
                mat = _spriteMats[idx];
            }
            tf.SetParent(null, true);
            // 좀비 몸 높이에서 위로 튀어나와 떨어진다. pos.y 는 눕는 높이(restY)다.
            pos.y = restY + dropHeight;
            tf.position = pos;

            var baseScale = new Vector3(h * aspect, h, depth);
            tf.localScale = baseScale;
            var mr = tf.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            tf.gameObject.SetActive(true);

            var g = new Gem
            {
                tf = tf, xp = xp, magnetised = false, sprite = true, gold = gold,
                bobPhase = Random.value * 6.283f,   // 위상을 흩어야 줄지어 춤추지 않는다
                baseY = restY,
                baseScale = baseScale,
                spawnAt = Time.time,
                mr = mr,
                vy = dropUpSpeed,
                vh = ScatterVelocity(),
                grounded = false,
                // 공중에 있는 동안은 파닥거리지 않는다 — 첫 착지에서 시작한다.
                flopAt = -999f,
                tier = idx,
                height = h,
            };
            // 떨어지는 순간부터 반짝인다 — 튀어나와 구르는 동안에도 따라간다(별은 로컬 공간이라 물고기와 같이 움직인다)
            g.sparkle = StartSparkle(g);
            _live.Add(g);
        }

        /// <summary>
        /// 좀비에서 옆으로 튀어나가는 수평 속도. 방향은 무작위지만 <b>카메라 쪽으로 치우친다</b> —
        /// 카메라가 −Z 에서 내려다보므로 −Z 로 튀면 화면 아래쪽, 즉 시체 앞에 떨어져 가리지 않는다.
        /// 좌우로만 튀게 하면 다음 좀비에게 가리고, +Z 로 튀면 시체 뒤라 아예 안 보인다.
        /// </summary>
        Vector3 ScatterVelocity()
        {
            if (scatterSpeed <= 0.0001f) return Vector3.zero;
            float a = Random.value * Mathf.PI * 2f;
            var rnd = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var dir = Vector3.Lerp(rnd, Vector3.back, scatterCameraBias);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.back;
            return dir.normalized * scatterSpeed * Random.Range(0.75f, 1.25f);
        }

        /// <summary>
        /// 착지할 때 수평 속도를 꺾는다. 세기는 <see cref="scatterFriction"/> 만큼 남고 방향은
        /// 이전에서 80~200° 돌아간다 — 180° 근처면 왔던 자리로, 90° 근처면 옆으로 간다.
        /// 방향을 새로 뽑지 않고 <b>이전 방향을 기준으로 꺾는</b> 이유: 완전 무작위로 뽑으면
        /// 우연히 같은 방향이 걸려 그냥 미끄러지는 것처럼 보이는 착지가 섞인다.
        /// </summary>
        Vector3 TurnOnBounce(Vector3 vh)
        {
            float sp = vh.magnitude * scatterFriction;
            if (sp < 0.05f) return Vector3.zero;
            Vector3 dir = vh.sqrMagnitude > 0.0001f ? vh.normalized : Vector3.back;
            float turn = Random.Range(bounceTurnMin, bounceTurnMax) * (Random.value < 0.5f ? -1f : 1f);
            return (Quaternion.Euler(0f, turn, 0f) * dir) * sp;
        }

        /// XP 가 클수록 큰 물고기. 배열 길이에 맞춰 잘린다.
        int SpriteIndexFor(int xp) => Mathf.Clamp(TierFor(xp), 0, gemSprites.Length - 1);

        /// XP 등급 — 0 잡좀비 · 1 2XP 이상 · 2 보스급. 물고기 그림 · 주화 크기 · 효과 크기가 같이 쓴다.
        static int TierFor(int xp)
        {
            var t = EnemyTable.I;
            int bossXp = t != null && t.midBoss != null ? t.midBoss.xp : 30;   // 보스급 경험치부터 가장 큰 것
            return xp >= bossXp ? 2 : (xp >= 2 ? 1 : 0);
        }

        /// 2 금색 물고기 젬 — 메시는 떨어질 때 GemLook 에서 꽂는다(DropSprite). 물고기 그림처럼 그림자 없음 — 수십 마리라 아깝다.
        Transform CreateGoldFishGem()
        {
            var go = new GameObject("XPGemGoldFish");
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        void BuildSpriteAssets()
        {
            _quad = BuildFishQuad();
            _mpb = new MaterialPropertyBlock();
            _spriteMats = new Material[gemSprites.Length];

            // 전용 셰이더가 없으면(파일이 빠졌거나 컴파일 실패) URP Unlit 으로 떨어진다.
            // 몸은 안 휘지만 그림은 나온다 — 젬이 통째로 안 보이는 것보다 낫다.
            var fish = Shader.Find("Proto/GemFish");
            var fallback = fish == null ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (fish == null) Debug.LogWarning("[젬] Proto/GemFish 셰이더를 못 찾았다 — URP Unlit 으로 대체한다(몸 휨 없음).");

            for (int i = 0; i < gemSprites.Length; i++)
            {
                var m = new Material(fish != null ? fish : fallback);
                m.SetTexture("_BaseMap", gemSprites[i]);
                m.SetFloat("_Cutoff", 0.5f);
                if (fish == null)
                {
                    // 알파 컷아웃을 쓴다. 반투명으로 두면 젬이 수십 개 겹칠 때 정렬이 흔들려 깜빡인다.
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                    m.SetFloat("_AlphaClip", 1f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = 2450;
                    // **양면으로 그린다(Cull Off).** URP Unlit 기본값은 백페이스 컬링인데,
                    // 쿼드를 카메라 쪽으로 돌려놔도(dot=1.000) 보이는 면이 반대쪽이라 통째로
                    // 잘려나갔다 — 젬이 27개 살아 있는데 화면에 하나도 없는 증상으로 실제로 걸렸다.
                    if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
                }
                _spriteMats[i] = m;
            }
        }

        Transform CreateSpriteGem()
        {
            var go = new GameObject("XPGemSprite");
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        /// <summary>
        /// 가로로 쪼갠 단위 쿼드. **쪼개는 이유가 전부다** — 정점이 4개면 셰이더가 몸을 휘게 할
        /// 정점이 없어서 사각형이 통째로 기울 뿐이다. 12칸이면 꼬리 젓는 곡선이 부드럽게 나온다.
        /// 세로는 2줄이면 충분하다(같은 x 의 위·아래가 같이 움직이면 몸이 휜 것으로 읽힌다).
        /// 바운즈는 휨 진폭만큼 넉넉히 잡는다 — 안 그러면 크게 휠 때 화면 경계에서 잘려 사라진다.
        /// </summary>
        static Mesh BuildFishQuad()
        {
            const int COLS = 12;
            var verts = new Vector3[(COLS + 1) * 2];
            var uvs   = new Vector2[verts.Length];
            for (int c = 0; c <= COLS; c++)
            {
                float u = c / (float)COLS;
                float x = u - 0.5f;
                verts[c * 2 + 0] = new Vector3(x, -0.5f, 0f);
                verts[c * 2 + 1] = new Vector3(x,  0.5f, 0f);
                uvs[c * 2 + 0] = new Vector2(u, 0f);
                uvs[c * 2 + 1] = new Vector2(u, 1f);
            }

            var tris = new int[COLS * 6];
            int k = 0;
            for (int c = 0; c < COLS; c++)
            {
                int i0 = c * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                tris[k++] = i0; tris[k++] = i1; tris[k++] = i2;
                tris[k++] = i2; tris[k++] = i1; tris[k++] = i3;
            }

            var m = new Mesh { name = "GemFishQuad" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.bounds = new Bounds(Vector3.zero, new Vector3(1.1f, 1.5f, 0.2f));
            return m;
        }

        void Update()
        {
            if (player == null || ProtoRun.I == null || !ProtoRun.I.IsRunning) return;

            float range = player.PickupRange;
            float rangeSq = range * range;
            Vector3 ppos = player.transform.position;
            float dt = ProtoConfig.Delta;

            // 모으는 중이면 남은 시간 안에 닿게 속도를 올린다. 마감이 지나면 남은 건 바로 먹는다.
            if (_gathering && Time.time >= _gatherEnd) { FinishGather(); return; }
            float gatherLeft = _gathering ? Mathf.Max(0.05f, _gatherEnd - Time.time) : 0f;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var g = _live[i];
                if (g.tf == null) { EndFx(g, true); _live.RemoveAt(i); continue; }

                Vector3 to = ppos - g.tf.position;
                to.y = 0f;
                float sq = to.sqrMagnitude;

                // 진행 방향을 알아야 머리를 돌릴 수 있어서 to 를 먼저 구한다.
                if (g.sprite)
                {
                    // 낙하·튕김은 자석보다 먼저. 자석이 잡으면 y 는 자석이 가진다.
                    if (!g.grounded && !g.magnetised) TickFall(g, dt);
                    TickSpriteLook(g, to);
                }
                else g.tf.Rotate(0f, 180f * dt, 0f, Space.Self);

                if (!g.magnetised && sq <= rangeSq) Magnetise(g);

                if (g.magnetised)
                {
                    // 9m/s 였을 때는 픽업 반경 1.8m 를 0.15초에 통과해서 "물고기가 헤엄쳐 온다"가
                    // 눈에 안 보였다(진행 방향 반전도 관찰이 안 될 정도). 그래서 일정 5m/s 로 낮췄는데
                    // 11-19 "흡수가 느리다" → 느리게 출발해 가속(머리 돌리는 게 첫 순간에 보이고, 끝은 빠르다).
                    float spd = Mathf.Min(magnetMaxSpeed, magnetStartSpeed + magnetAccel * (Time.time - g.magnetAt));
                    if (_gathering) spd = Mathf.Max(spd, Mathf.Sqrt(sq) / gatherLeft);
                    g.tf.position += to.normalized * Mathf.Min(spd * dt, Mathf.Sqrt(sq));   // 빠를 때 지나쳐 되돌아오지 않게
                    FollowFx(g);   // 꼬리가 먹히는 마지막 자리까지 닿게 — 흡수 판정보다 먼저
                    if (sq < 0.36f || (_gathering && (ppos - g.tf.position).sqrMagnitude < 0.36f))
                    {
                        // 연속으로 주울 때 음이 한 단씩 올라간다(PlayLadder). 젬이 무더기로
                        // 딸려 들어오는 것이 이 게임의 보상 리듬이라, 같은 음으로 40번 울리면
                        // 보상이 아니라 소음이 된다.
                        ProtoAudio.PlayLadder("gem", g.tf.position, 0.9f);
                        // 흡수 반짝임(카탈로그 gem_pickup — 모양은 프리팹). 크기 = 젬 등급(작음 0.8 · 중간 1 · 큼 1.4)
                        // 등급은 떨어질 때 정해 둔 값(g.tier) — 예전엔 여기서 SpriteIndexFor 를 불러 큐브 경로(그림 배열 없음)에서 터질 수 있었다
                        ProtoFx.Play(FxIds.GemPickup, g.tf.position, Vector3.zero, TierScale(g.tier));
                        if (_gathering) _heldXp += g.xp * player.xpMult;   // 모으는 중엔 쥐고 있다가 끝에 한 번에
                        else ProtoRun.I.AddXP(g.xp * player.xpMult);
                        Recycle(g, i);
                    }
                }
                else FollowFx(g);
            }
        }

        // ── 반짝이 · 흡수 꼬리 ──────────────────────────────────────────
        static float TierScale(int tier) => tier >= 2 ? 1.4f : tier == 1 ? 1f : 0.8f;

        /// 자석에 걸린다 — 끌려오기 시작. 바닥 반짝이는 사그라들게 두고(남은 별은 제 수명대로) 꼬리를 단다.
        /// 픽업 반경 · 자석 아이템(MagnetRadius) · 보스 직전 모으기(MagnetAll) 셋이 다 여기로 온다.
        void Magnetise(Gem g)
        {
            if (g.magnetised) return;
            g.magnetised = true;
            g.magnetAt = Time.time;
            g.magnetY0 = g.tf.position.y;   // 공중에서 걸려도 높이가 안 튀게
            if (g.sparkle != null) { g.sparkle.Stop(); g.sparkle = null; }
            if (trailWhileMagnet && g.trail == null) g.trail = StartTrail(g);
        }

        FxHandle StartSparkle(Gem g)
        {
            if (!sparkleOnGround) return null;
            var c = FxContext.At(g.tf.position);
            c.scale = TierScale(g.tier);
            var h = ProtoFx.Play(FxIds.GemSparkle, c, span: true);
            return h.Alive ? h : null;   // 카탈로그에 프리팹이 없으면 아무것도 안 나온다
        }

        readonly Dictionary<GameObject, TrailRenderer[]> _trailCache = new Dictionary<GameObject, TrailRenderer[]>();

        TrailRenderer[] TrailsOf(GameObject go)
        {
            if (go == null) return null;
            if (!_trailCache.TryGetValue(go, out var trs)) { trs = go.GetComponentsInChildren<TrailRenderer>(true); _trailCache[go] = trs; }
            return trs;
        }

        FxHandle StartTrail(Gem g)
        {
            var c = FxContext.At(g.tf.position);
            c.scale = TierScale(g.tier);
            var h = ProtoFx.Play(FxIds.GemTrail, c, span: true);
            if (!h.Alive) return null;
            // 풀에서 다시 나온 꼬리는 지난 자리를 기억해 줄을 긋는다 — 켤 때 비운다(ProtoEnemyBolt 와 같은 처리).
            // 굵기는 트랜스폼 배율을 안 타서 물고기 키로 직접 넣는다.
            var trs = TrailsOf(h.Instance);
            if (trs != null)
            {
                float w = Mathf.Max(0.05f, g.height * trailWidthPerFishHeight);
                foreach (var tr in trs)
                {
                    if (tr == null) continue;
                    tr.Clear(); tr.emitting = true; tr.widthMultiplier = w;
                    if (trailTime > 0f) tr.time = trailTime;
                }
            }
            return h;
        }

        void FollowFx(Gem g)
        {
            if (g.sparkle != null) { if (g.sparkle.Alive) g.sparkle.MoveTo(g.tf.position); else g.sparkle = null; }   // 동시 개수 상한에 걸려 거둬졌으면 놓는다
            if (g.trail != null) { if (g.trail.Alive) g.trail.MoveTo(g.tf.position); else g.trail = null; }
        }

        /// 젬이 사라질 때 효과 정리. instant = 재시작처럼 바로 거둔다. 아니면 반짝이는 남은 별을 사그라들게,
        /// 꼬리는 방출을 끄고 따라가기를 끊어 남은 줄이 제 시간에 짧아지며 사라지게 한다.
        void EndFx(Gem g, bool instant)
        {
            if (g.sparkle != null)
            {
                if (instant) g.sparkle.Kill(); else g.sparkle.Stop();
                g.sparkle = null;
            }
            if (g.trail != null)
            {
                if (instant) g.trail.Kill();
                else if (g.trail.Alive)
                {
                    var trs = TrailsOf(g.trail.Instance);
                    if (trs != null) foreach (var tr in trs) if (tr != null) tr.emitting = false;
                    g.trail.Stop(detach: true);
                }
                g.trail = null;
            }
        }

        /// <summary>
        /// 카드보드처럼 카메라를 향해 세우고, 정지 이미지 한 장으로 낼 수 있는 만큼 살아 있게 만든다.
        /// 관절이 없으니 <b>기울임 · 스쿼시 · 상하 살랑거림 · 등장 팝 · 좌우 반전</b> 다섯 가지가 전부다.
        /// 클립도 셰이더도 필요 없어서 임시 테스트에 딱 맞고, 그림을 갈아끼워도 그대로 돈다.
        /// </summary>
        /// <summary>
        /// 낙하와 튕김. 유니티 물리를 쓰지 않고 수직 속도만 적분한다(§16 — 원-원 판정만 필요한
        /// 게임에 물리 엔진은 과잉이다). <b>튕길 때마다 파닥이 다시 터진다</b> — 착지 충격과
        /// 파닥이 같은 순간에 나야 "떨어져서 파닥거린다"로 읽힌다.
        /// </summary>
        void TickFall(Gem g, float dt)
        {
            g.vy -= gravity * dt;
            var p = g.tf.position;
            p.y += g.vy * dt;
            p += g.vh * dt;                       // 옆으로 튀어나가는 성분

            if (p.y <= g.baseY)
            {
                p.y = g.baseY;
                g.flopAt = Time.time;             // 착지 → 파닥 시작(또는 재시작)
                g.vh = TurnOnBounce(g.vh);        // 방향을 꺾고 세기를 줄인다
                // 반동이 약해지면 그만 튄다. 안 그러면 바닥에서 미세하게 영원히 떤다.
                if (-g.vy > bounceStopSpeed) g.vy = -g.vy * bounciness;
                else { g.vy = 0f; g.vh = Vector3.zero; g.grounded = true; }
            }

            // 아레나 밖으로 튀어나가지 않게 (§04 이동 클램프와 같은 값)
            float lim = ProtoConfig.ArenaHalf;
            p.x = Mathf.Clamp(p.x, -lim, lim);
            p.z = Mathf.Clamp(p.z, -lim, lim);
            g.tf.position = p;
        }

        // 카메라에서 나오는 값은 프레임마다 한 번만 — 예전엔 젬마다 LookRotation 두 번 · 카메라 축을 다시 읽었다(2026-11-20)
        int _camFrame = -1;
        Quaternion _camLook;
        Vector3 _camRight;
        bool _headRightUnflipped;

        void RefreshCamFrame()
        {
            if (_camFrame == Time.frameCount) return;
            _camFrame = Time.frameCount;
            EnsureCam();
            if (_cam == null) return;
            var ct = _cam.transform;
            _camLook = Quaternion.LookRotation(-ct.forward, Vector3.up);
            _camRight = ct.right;
            Vector3 headLocal = spritesFaceLeft ? Vector3.left : Vector3.right;   // 머리 = 그림이 보는 쪽(금색 물고기 메시도 -x 가 머리)
            _headRightUnflipped = Vector3.Dot(_camLook * headLocal, _camRight) > 0f;
        }

        void TickSpriteLook(Gem g, Vector3 toPlayer)
        {
            RefreshCamFrame();

            // ── 상태 → 세기 ─────────────────────────────────────────────
            // 파닥거림은 **착지 순간**에 1 이고 사그라든다. 공중에서는 0 이다 —
            // 떨어지는 중에 파닥거리면 어디서 부딪혔는지가 안 읽힌다.
            float flop = 0f;
            if (flopTime > 0.0001f && g.flopAt > 0f)
            {
                float t01 = (Time.time - g.flopAt) / flopTime;
                if (t01 < 1f) flop = 1f - t01 * t01;    // 처음 세게, 뒤로 갈수록 빠르게 잦아든다
            }
            bool magnet = g.magnetised;
            bool airborne = !g.grounded && !magnet;

            // 끌려오는 동안은 **파닥을 계속 켜 둔다.** 예전에는 착지 파닥이 사그라든 뒤라
            // _Flop 이 0 이었고, 그래서 물고기가 몸을 안 휘고 통째로 미끄러졌다.
            // 이 한 줄이 몸 휨(셰이더)·갸웃 각도·갸웃 속도·스쿼시를 한꺼번에 올린다.
            if (magnet) flop = Mathf.Max(flop, magnetBend);

            // 상태별 기본값 위에 파닥 세기를 얹는다. 공중에서도 조금은 흔들려야 —
            // 뻣뻣하게 떨어지면 그림이 아니라 판때기가 떨어지는 것으로 보인다.
            float baseTilt  = magnet ? magnetTiltDegrees : (airborne ? airborneTiltDegrees : idleTiltDegrees);
            float baseSpeed = magnet ? bobSpeed * 1.8f   : (airborne ? 7f : bobSpeed);
            float tilt  = Mathf.Lerp(baseTilt,  flopTiltDegrees, flop);
            float speed = Mathf.Lerp(baseSpeed, flopTiltSpeed,   flop);
            float t = Time.time * speed + g.bobPhase;

            if (_cam != null)
            {
                var look = _camLook;
                // 뷰 축(Z) 회전이라 화면에서 좌우로 갸웃하는 것처럼 보인다.
                // 월드 축으로 돌리면 카메라 각도 때문에 위아래로 눕는 것처럼 보인다.
                if (tilt > 0.01f) look *= Quaternion.Euler(0f, 0f, Mathf.Sin(t) * tilt);
                g.tf.rotation = look;
            }

            // 몸 휨은 셰이더가 한다(정점 변형). 젬마다 세기·위상이 달라야 하므로 MPB 로 넣는다.
            // MPB 는 SRP Batcher 를 끊어 젬 개수만큼 드로우콜이 늘어난다 — 임시 테스트라 감수한다.
            if (g.mr != null && _mpb != null && flop != g.lastFlop)   // 바닥에 가만히 있는 젬은 0 그대로 — 매 프레임 다시 넣지 않는다
            {
                _mpb.SetFloat(FlopID, flop);
                _mpb.SetFloat(PhaseID, g.bobPhase);
                g.mr.SetPropertyBlock(_mpb);
                g.lastFlop = flop;
            }

            // 진행 방향으로 머리를 돌린다. 원본 그림이 왼쪽을 보므로 오른쪽으로 갈 때 뒤집는다.
            // 가만히 있는 물고기가 이유 없이 방향을 바꾸면 거슬리므로 자석에 끌릴 때만 본다.
            //
            // **화면 기준으로 계산한다**(2026-11-18). 카메라를 향해 세우는 회전 LookRotation(-카메라 앞)은 로컬 +x 를 화면 **왼쪽**으로
            // 보낸다 — 그래서 그림이 원래 좌우가 뒤집힌 채(왼쪽을 보는 그림이 오른쪽을 보며) 놓인다. 예전 식
            // (`spritesFaceLeft ? goingRight : !goingRight`)은 이걸 몰라 머리가 진행 반대쪽을 봤다 — 물고기가 **꼬리부터** 빨려 왔다
            // (임시 프로젝트에서 게임과 같은 회전으로 그림 물고기 · 금색 물고기를 찍어 확인). 이제 안 뒤집었을 때 머리가 화면 어느 쪽인지를
            // 회전에서 직접 구해, 진행 쪽과 다르면 뒤집는다 — 카메라가 돌아가도 맞다.
            if (faceTravelDirection && magnet && _cam != null)
            {
                bool goingRight = Vector3.Dot(toPlayer, _camRight) > 0.01f;
                g.flipped = _headRightUnflipped != goingRight;
            }

            // 스쿼시 & 스트레치 — 가로가 늘면 세로가 줄어 부피가 보존되는 것처럼 보인다.
            float sq = swimSquash > 0.0001f ? Mathf.Sin(t * 0.5f) * swimSquash * (1f + flop) : 0f;
            float pop = Pop(g);
            var s = g.baseScale;
            s.x *= (1f + sq) * pop * (g.flipped ? -1f : 1f);
            s.y *= (1f - sq) * pop;
            s.z *= pop;   // 2 금색 물고기(3D)는 두께도 같이 톡 튀어나온다 — 그림 쿼드는 두께가 없어 상관없다
            g.tf.localScale = s;

            TickHeight(g, flop, bobAmplitudeIdle);
        }

        /// 씬 빌더가 만든 카메라에는 MainCamera 태그가 없을 수 있어서 디렉터 것을 먼저 본다.
        void EnsureCam()
        {
            if (_cam != null && _cam.isActiveAndEnabled) return;
            var d = ProtoSpawnDirector.I;
            _cam = (d != null && d.viewCamera != null) ? d.viewCamera : Camera.main;
        }

        /// <summary>
        /// 높이: 공중이면 TickFall 이 주인이라 손대지 않는다. 바닥에 누운 뒤에만
        /// 파닥이며 살짝 들썩이거나(파닥 중, flop &gt; 0) 아주 약하게 호흡한다(정지, idleAmp).
        /// 물고기 · 주화가 같이 쓴다(주화는 flop 0).
        /// </summary>
        void TickHeight(Gem g, float flop, float idleAmp)
        {
            // **끌려올 때는 자석이 주인이다.** 예전에는 여기가 `!magnet` 으로 막혀 있어서
            // y 를 아무도 안 건드렸고, 자석 이동은 수평 성분만 더하므로(to.y = 0)
            // 물고기가 바닥에 붙은 채 미끄러졌다.
            if (g.magnetised)
            {
                float mt = magnetLiftTime > 0.0001f
                         ? Mathf.Clamp01((Time.time - g.magnetAt) / magnetLiftTime) : 1f;
                mt = mt * mt * (3f - 2f * mt);          // smoothstep — 뜨기 시작이 툭 튀지 않게
                float y = Mathf.Lerp(g.magnetY0, g.baseY + magnetLift, mt)
                        + Mathf.Sin(Time.time * magnetSwimSpeed + g.bobPhase) * magnetSwimAmplitude * mt;
                var mp = g.tf.position; mp.y = y; g.tf.position = mp;
            }
            else if (g.grounded)
            {
                float y = g.baseY;
                if (flop > 0.001f && flopHopHeight > 0.0001f)
                    y += Mathf.Abs(Mathf.Sin(Time.time * flopTiltSpeed * 0.5f + g.bobPhase)) * flopHopHeight * flop;
                else if (idleAmp > 0.0001f)
                    y += Mathf.Sin(Time.time * bobSpeed + g.bobPhase) * idleAmp;
                var p = g.tf.position; p.y = y; g.tf.position = p;
            }
        }

        /// 등장 순간 톡 튀어나오는 배율. ease-out-back — 1 을 살짝 넘겼다 되돌아온다.
        /// 좌표를 튀게 하지 않고 크기만 건드리는 이유는, 떨어진 자리가 곧 먹으러 갈 자리라서다.
        float Pop(Gem g)
        {
            if (popTime <= 0.0001f) return 1f;
            float t01 = (Time.time - g.spawnAt) / popTime;
            if (t01 >= 1f) return 1f;
            float u = t01 - 1f;
            const float back = 1.9f;
            float e = 1f + (back + 1f) * u * u * u + back * u * u;
            return Mathf.Lerp(0.35f, 1f, e);
        }

        void Recycle(Gem g, int index, bool instantFx = false)
        {
            EndFx(g, instantFx);
            _live.RemoveAt(index);
            g.tf.gameObject.SetActive(false);
            g.tf.SetParent(_poolRoot, true);
            if (g.gold) _goldPool.Enqueue(g.tf);
            else if (g.sprite) _spritePool.Enqueue(g.tf);
            else _pool.Enqueue(g.tf);
        }

        public void ClearAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Recycle(_live[i], i, instantFx: true);   // 재시작 — 반짝이 · 꼬리도 바로 거둔다
            _gathering = false; _heldXp = 0f;   // 재시작 — 모으던 중이었으면 버린다
        }

        /// 떨어져 있는 젬을 전부 플레이어에게 끌어온다. 최종 보스 울타리가 서기 직전에 쓴다 —
        /// 울타리 밖에 남은 젬은 보스를 잡을 때까지 주울 수 없게 되기 때문이다.
        /// 중심에서 radius 안에 있는 젬만 끌어온다. 자석 아이템(ProtoPickups)용 —
        /// 전부 끌어오면 너무 강하다(2026-10-14 사용자). 끌려오는 동작은 MagnetAll 과 같다.
        public void MagnetRadius(Vector3 center, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _live.Count; i++)
            {
                var g = _live[i];
                if (g.magnetised) continue;
                Vector3 d = g.tf.position - center; d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                Magnetise(g);
            }
        }

        // ── 최종 보스 직전 모으기 (2026-10-21 사용자) ─────────────────────
        // 끌어오기(MagnetAll)로는 몇 초에 걸쳐 들어와서 보스전 도중에 레벨업 카드가 계속 끼어들었다.
        // 그래서 seconds 안에 전부 날아오게 속도를 맞추고, 그동안 먹은 경험치는 **쥐고 있다가** 끝에 한 번에 넣는다.
        // 밀린 레벨업은 ProtoRun 이 연달아 처리하고, 카드를 다 고른 뒤에야 보스가 나온다(ProtoSpawnDirector.TrySpawnFinal).
        bool _gathering;
        float _gatherEnd, _heldXp;

        /// 모으는 중인가. 끝나면(경험치를 넣은 뒤) false.
        public bool Gathering => _gathering;

        public void StartGather(float seconds)
        {
            if (_gathering) return;
            _gathering = true;
            _gatherEnd = Time.time + Mathf.Max(0.1f, seconds);
            _heldXp = 0f;
            MagnetAll();
        }

        /// 모으기 마감 — 아직 안 들어온 젬(모으는 사이 새로 떨어진 것 포함)은 그 자리에서 먹고, 쥐고 있던 경험치를 넣는다.
        void FinishGather()
        {
            _gathering = false;
            for (int i = _live.Count - 1; i >= 0; i--) { _heldXp += _live[i].xp * player.xpMult; Recycle(_live[i], i); }
            float xp = _heldXp; _heldXp = 0f;
            if (xp > 0f) ProtoRun.I.AddXP(xp);
        }

        public void MagnetAll()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var g = _live[i];
                if (g.magnetised) continue;
                Magnetise(g);
            }
        }
    }
}
