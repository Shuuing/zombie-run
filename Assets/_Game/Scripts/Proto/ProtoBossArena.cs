using UnityEngine;

namespace Proto
{
    /// 최종 보스전 울타리 (탕탕특공대식 — 사용자 요청 2026-10-13).
    ///
    /// 최종 보스가 나오면 플레이어 주변에 사각 울타리가 솟고, 보스를 잡을 때까지
    /// 플레이어·탱크·보스가 그 안에서만 움직인다.
    ///
    /// 크기는 **그 순간 카메라가 보는 지면 × SizeMult** 다. 화면비·카메라 프리셋이 바뀌어도
    /// 비율이 유지된다. SizeMult 가 1 이하면 카메라를 울타리 중심에 고정해 전체를 보여주고,
    /// 1 보다 크면(현재 1.65) 카메라는 평소처럼 플레이어를 따라간다.
    ///
    /// **모양 — 불꽃 테두리** (2026-11-13 사용자: 그라인드 서바이버즈 보스전처럼 "아웃라인이 불꽃이 일렁이는" 테두리).
    /// 울타리 선(모서리를 CornerR 로 둥글린 사각형)을 따라 메시 두 장을 코드로 만들고 셰이더 Proto/FlameBorder 로 그린다.
    ///  · 바닥 띠 — 일렁이는 빨간 불 선, 안쪽으로 번지는 빛, 울타리 바깥 어둠.
    ///  · 위로 타오르는 불꽃 — 선 위에 카메라를 보고 선 카드(CardSpacing 간격)에 1판의 검붉은 혀 · 밝은 작은 혀를 세웠다.
    ///    (2판, 같은 날 사용자 "불꽃이 옆으로 누워 있는 것 같다 — 그전으로 돌리고 방향만 위로". 1판은 혀가 바닥에 누워 바깥으로 뻗었다)
    /// 셰이더를 못 찾으면(빌드에서 빠짐 등) 예전 세로 벽(이펙트 셰이더 그라데이션 — ProtoFxMat)으로 대신한다.
    /// 이동 제한도 같은 둥근 모서리를 따른다 — 모서리에서 몸이 불 선을 넘지 않게.
    public class ProtoBossArena : MonoBehaviour
    {
        // ── 튜닝 ─────────────────────────────────────────────────────
        // 화면 가장자리 여백(뷰포트 비율). 아래는 조이스틱, 위는 타이머·보스 체력 바가 덮는다.
        const float ViewLeft = 0.06f, ViewRight = 0.94f, ViewBottom = 0.16f, ViewTop = 0.78f;
        // 화면 크기 대비 배율 (사용자 2026-10-13: 화면 크기는 너무 좁다, 2.5~3배).
        // 1 보다 크면 울타리가 한 화면에 안 들어오므로 카메라를 고정하지 않고 플레이어를 따라간다 —
        // 벽은 가까이 가야 보인다(탕탕특공대도 같다).
        const float SizeMult = 1.65f;    // 2.75 → 1.375(넓다, 50%↓) → 1.65(작다, 20%↑) (2026-10-13 사용자)
        const float WallHeight = 1.3f;    // 예전 세로 벽(셰이더가 없을 때만)
        const float RiseTime   = 0.45f;   // 벽이 솟는 / 불이 붙는 시간
        const float FallTime   = 0.6f;    // 보스를 잡으면 가라앉는 / 꺼지는 시간
        static readonly Color WallColor = new Color(1.00f, 0.32f, 0.12f, 1f);

        // 불꽃 테두리 (Proto/FlameBorder)
        const float CornerR    = 1.2f;    // 모서리 둥글기 — BandIn 보다 커야 안쪽 띠가 모서리에서 접히지 않는다
        const float BandIn     = 1.0f;    // 선 안쪽 띠 폭(안쪽 빛 번짐)
        const float BandOut    = 2.6f;    // 선 바깥 띠 폭(어둠이 진해지는 구간). 셰이더 _OuterW 와 같은 값
        const float DarkFar    = 40f;     // 그 바깥으로 고른 어둠을 깔 폭 — 화면 밖까지
        // 위로 타오르는 불꽃 카드(2판). 불꽃 혀 길이는 셰이더 _FlameLen(1.9m — 1판에서 바닥에 눕던 혀와 같은 값)
        const float CardHeight  = 2.0f;   // 카드 키(m) — _FlameLen 보다 조금 길게(혀 끝이 잘리지 않게)
        const float CardWidth   = 1.2f;   // 카드 폭(m) — 가장자리는 셰이더가 비워 불꽃 몸통은 약 0.8m
        const float CardSpacing = 0.5f;   // 카드 간격(m) — 폭보다 촘촘해 서로 겹쳐 이어진 불 벽이 된다
        const float CellSize   = 0.8f;    // 불 선 일렁임 한 굽이의 길이(m) — 둘레를 정수 칸으로 나눠 이음매를 없앤다
        const float GroundLift = 0.05f;   // 바닥과 겹쳐 깜빡이지 않게(바닥 링 ProtoGroundRing 0.06 과 비슷하게)
        const string FlameRefMat = "ShaderRefs/Ref_Proto_FlameBorder";

        public static bool Active => _cur != null && !_cur._ending;
        public static Vector3 Center => _cur != null ? _cur._center : Vector3.zero;

        static ProtoBossArena _cur;

        Vector3 _center;
        float _minX, _maxX, _minZ, _maxZ;
        float _bossOffsetZ;   // 보스 등장 지점 — 울타리 크기와 무관하게 화면 위쪽 안
        float _t;
        bool  _ending;
        Material _mat;
        Transform _walls;
        // 불꽃 테두리
        float _cornerR;                     // 이동 제한 모서리 둥글기(예전 벽이면 0 = 직각)
        Material _groundMat, _flameMat;
        Mesh _groundMesh, _flameMesh;
        static readonly int IdFlameT = Shader.PropertyToID("_FlameT"), IdReveal = Shader.PropertyToID("_Reveal");

        /// 플레이어 위치를 중심으로 울타리를 세운다. 이미 있으면 아무것도 하지 않는다.
        public static void Begin(Vector3 around)
        {
            if (_cur != null) return;
            var go = new GameObject("_BossArena");
            _cur = go.AddComponent<ProtoBossArena>();
            _cur.Build(around);
        }

        /// 보스 격파 — 벽이 가라앉고 카메라가 다시 플레이어를 따라간다.
        public static void End()
        {
            if (_cur == null || _cur._ending) return;
            _cur._ending = true;
            _cur._t = 0f;
            if (ProtoCameraFollow.I != null) ProtoCameraFollow.I.focusOverride = null;
        }

        /// 재시작용. 연출 없이 즉시 없앤다.
        public static void Clear()
        {
            if (ProtoCameraFollow.I != null) ProtoCameraFollow.I.focusOverride = null;
            if (_cur != null) Destroy(_cur.gameObject);
            _cur = null;
        }

        /// 울타리 안으로 밀어 넣는다. radius 는 몸 반경 — 몸이 벽을 뚫고 나가지 않게.
        /// 안에 있으면 p 를 그대로 돌려준다(ProtoPlayer 가 != 로 막혔는지 본다).
        public static Vector3 Clamp(Vector3 p, float radius)
        {
            if (!Active) return p;
            var a = _cur;
            float r = a._cornerR;
            if (r <= radius)
            {
                // 직각 모서리(예전 벽) — 또는 몸이 모서리 둥글기보다 커서 직각과 다름없는 경우
                p.x = Mathf.Clamp(p.x, a._minX + radius, a._maxX - radius);
                p.z = Mathf.Clamp(p.z, a._minZ + radius, a._maxZ - radius);
                return p;
            }
            // 둥근 모서리 사각형 = 안쪽 사각형(모서리 원 중심들)에서 r 만큼 부풀린 모양.
            // 몸 반경만큼 줄이면 안쪽 사각형에서 (r - radius) 안이 갈 수 있는 곳이다.
            float qx = Mathf.Clamp(p.x, a._minX + r, a._maxX - r);
            float qz = Mathf.Clamp(p.z, a._minZ + r, a._maxZ - r);
            float dx = p.x - qx, dz = p.z - qz, lim = r - radius;
            float d2 = dx * dx + dz * dz;
            if (d2 <= lim * lim) return p;
            float k = lim / Mathf.Sqrt(d2);
            p.x = qx + dx * k;
            p.z = qz + dz * k;
            return p;
        }

        /// 보스 등장 지점 — 울타리 안쪽 먼 벽 앞(화면 위쪽). 플레이어 쪽으로 걸어 내려온다.
        public static Vector3 BossSpawnPoint()
        {
            if (_cur == null) return Vector3.zero;
            var a = _cur;
            return new Vector3(a._center.x, 0f, Mathf.Min(a._center.z + a._bossOffsetZ, a._maxZ - 1f));
        }

        void Build(Vector3 around)
        {
            around.y = 0f;
            MeasureView(out float halfX, out float back, out float fwd);
            _bossOffsetZ = fwd * 0.75f;   // 화면 기준 — 등장하는 모습이 보이게

            // 맵보다 크면 맵 크기로 자른다(아레나가 좁은 씬 대비)
            float lim = ProtoConfig.ArenaHalf;
            // 정사각형(사용자 2026-10-13: 옆으로 긴 모양 말고). 화면 비율을 따르면 가로 화면에선 옆으로,
            // 세로 화면에선 위아래로 길어진다 — 넓이는 화면 기준 그대로 두고 모양만 정사각형으로, 플레이어 중심.
            float half = Mathf.Sqrt(halfX * (back + fwd) * 0.5f) * SizeMult;
            half = Mathf.Min(half, lim);
            halfX = back = fwd = half;

            // 울타리가 맵 밖으로 나가지 않게 중심을 안쪽으로 민다
            _center = around;
            _center.x = Mathf.Clamp(_center.x, -lim + halfX, lim - halfX);
            _center.z = Mathf.Clamp(_center.z, -lim + back, lim - fwd);
            _minX = _center.x - halfX; _maxX = _center.x + halfX;
            _minZ = _center.z - back;  _maxZ = _center.z + fwd;

            if (!BuildFlameBorder()) BuildWalls();
            if (SizeMult <= 1f && ProtoCameraFollow.I != null) ProtoCameraFollow.I.focusOverride = _center;
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Medium);   // 울타리가 솟는다
            Debug.Log($"[보스 울타리] 중심 {_center:F1}  X {_minX:F1}~{_maxX:F1}  Z {_minZ:F1}~{_maxZ:F1}");
        }

        /// 지금 카메라가 보는 지면 사다리꼴에서 울타리 크기를 잰다. 카메라가 보는 화면 중심점이
        /// 울타리 중심이 된다고 보고, 중심에서 아래/위 벽까지 거리와 좁은 쪽(아래) 폭을 쓴다.
        static void MeasureView(out float halfX, out float back, out float fwd)
        {
            halfX = 4.5f; back = 5f; fwd = 7f;   // 카메라가 없을 때의 대략값
            var cam = ProtoCameraFollow.I != null ? ProtoCameraFollow.I.GetComponent<Camera>() : Camera.main;
            if (cam == null) return;

            if (!Ground(cam, 0.5f, 0.5f, out Vector3 c)) return;
            if (!Ground(cam, ViewLeft,  ViewBottom, out Vector3 bl)) return;
            if (!Ground(cam, ViewRight, ViewBottom, out Vector3 br)) return;
            if (!Ground(cam, 0.5f, ViewTop, out Vector3 t)) return;

            // 탱크 탑승 중이면 카메라가 물러나 있다(vehicleZoom). 울타리는 하차한 뒤의 화면 기준이어야
            // 하므로 그 배율만큼 줄인다 — 지면에 비치는 범위는 카메라 거리에 거의 비례한다.
            // 시작 연출(introZoom)로 물러나 있을 때도 같은 이유로 나눈다.
            var cf = ProtoCameraFollow.I;
            float zoom = cf != null ? Mathf.Max(1f, cf.vehicleZoom * cf.introZoom) : 1f;
            halfX = Mathf.Max(2.5f, (br.x - bl.x) * 0.5f / zoom);   // 원근이라 아래쪽이 가장 좁다
            back  = Mathf.Max(2.5f, (c.z - bl.z) / zoom);
            fwd   = Mathf.Max(2.5f, (t.z - c.z) / zoom);
        }

        static bool Ground(Camera cam, float vx, float vy, out Vector3 hit)
        {
            Ray r = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
            hit = Vector3.zero;
            if (r.direction.y > -0.01f) return false;
            float d = -r.origin.y / r.direction.y;
            hit = r.origin + r.direction * d;
            return true;
        }

        /// 불꽃 테두리(바닥 띠 + 세로 불꽃)를 만든다. 셰이더를 못 찾으면 false — 예전 세로 벽으로 대신한다.
        /// 색·폭 같은 셰이더 값은 Resources/ShaderRefs/Ref_Proto_FlameBorder 머티리얼을 복사해 쓰므로 거기서 바꾸면 된다.
        bool BuildFlameBorder()
        {
            var refMat = Resources.Load<Material>(FlameRefMat);
            var shader = refMat != null ? refMat.shader : Shader.Find("Proto/FlameBorder");
            if (shader == null || !shader.isSupported) return false;

            // 울타리 선 — 모서리를 둥글린 사각형. 오른쪽 아래 모서리부터 반시계로 모서리 호 4개를 잇는다
            // (호 끝과 다음 호 시작 사이가 곧은 변). 점마다 바깥 방향(법선)과 둘레를 따라 잰 거리(u)를 같이 둔다.
            const int Seg = 10;   // 모서리 호 하나의 조각 수
            float R = CornerR;
            Vector2[] centers =
            {
                new Vector2(_maxX - R, _minZ + R), new Vector2(_maxX - R, _maxZ - R),
                new Vector2(_minX + R, _maxZ - R), new Vector2(_minX + R, _minZ + R),
            };
            int n = 4 * (Seg + 1) + 1;   // 마지막 점 = 첫 점(둘레 끝 u 로 고리를 닫는다)
            var pos = new Vector2[n]; var nrm = new Vector2[n]; var u = new float[n];
            int k = 0;
            for (int c = 0; c < 4; c++)
                for (int s = 0; s <= Seg; s++)
                {
                    float ang = (c * 90f - 90f + 90f * s / Seg) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    nrm[k] = dir; pos[k] = centers[c] + dir * R; k++;
                }
            pos[k] = pos[0]; nrm[k] = nrm[0];
            for (int i = 1; i < n; i++) u[i] = u[i - 1] + Vector2.Distance(pos[i], pos[i - 1]);
            float perim = u[n - 1];
            float cells = Mathf.Max(8f, Mathf.Round(perim / CellSize));   // 정수 칸 — 셰이더 노이즈 주기

            // 바닥 띠: 선 안쪽 BandIn ~ 바깥 BandOut(노이즈 띠) ~ 바깥 BandOut+DarkFar(고른 어둠). uv = (둘레 거리, 선에서 거리)
            float[] rows = { -BandIn, BandOut, BandOut + DarkFar };
            var gv = new Vector3[n * rows.Length]; var guv = new Vector2[gv.Length];
            for (int i = 0; i < n; i++)
                for (int r = 0; r < rows.Length; r++)
                {
                    var p = pos[i] + nrm[i] * rows[r];
                    gv[i * rows.Length + r]  = new Vector3(p.x, GroundLift, p.y);
                    guv[i * rows.Length + r] = new Vector2(u[i], rows[r]);
                }
            _groundMesh = StripMesh("BossArenaFlameGround", gv, guv, n, rows.Length);

            _groundMat = refMat != null ? new Material(refMat) : new Material(shader);
            _groundMat.name = "BossArenaFlameGround";
            _groundMat.renderQueue = 3100;   // 밤 어둠 막(2900) 뒤
            _groundMat.SetFloat("_Perim", perim);
            _groundMat.SetFloat("_Cells", cells);
            _groundMat.SetFloat("_InnerW", BandIn);
            _groundMat.SetFloat("_OuterW", BandOut);
            _groundMat.SetFloat("_Wall", 0f);
            _groundMat.SetFloat(IdReveal, 0f);
            MakeRenderer("FlameGround", _groundMesh, _groundMat);

            // 위로 타오르는 불꽃(2판 — 사용자 "불꽃이 옆으로 누워 있는 것 같다, 방향만 위로"): 바닥에 눕던 혀를 선 위에 선 카드로 옮겼다.
            // 카드는 선 위 CardSpacing 간격, 꼭짓점 넷이 같은 자리(밑변 가운데)를 갖고 셰이더가 카메라 쪽으로 펼친다(빌보드) —
            // 이어진 세로 띠로 세우면 옆 벽(화면 위아래로 뻗은 선)에서 띠가 거의 옆면만 보여 불꽃이 다시 옆으로 누워 보인다.
            // 키 · 폭 · 나타남은 셰이더가 맡는다(_WallH · _CardW · _Reveal). uv2.x = 카드마다 다른 무늬.
            int cards = Mathf.Max(8, Mathf.RoundToInt(u[n - 1] / CardSpacing));
            var cp = new Vector3[cards];
            int seg = 0;
            for (int c = 0; c < cards; c++)
            {
                float target = (c + 0.5f) * u[n - 1] / cards;
                while (seg < n - 2 && u[seg + 1] < target) seg++;
                float a = Mathf.Clamp01((target - u[seg]) / Mathf.Max(1e-4f, u[seg + 1] - u[seg]));
                var p = Vector2.Lerp(pos[seg], pos[seg + 1], a);
                cp[c] = new Vector3(p.x, GroundLift, p.y);
            }
            // 메시 안에서는 정렬이 없어 순서대로 그린다 — 카메라가 늘 −Z 에서 +Z 를 보므로 먼 것(+Z)부터 그리게 줄 세운다
            System.Array.Sort(cp, (l, r) => r.z.CompareTo(l.z));
            var cv = new Vector3[cards * 4]; var cuv = new Vector2[cards * 4]; var cuv2 = new Vector2[cards * 4];
            var ct = new int[cards * 6];
            for (int c = 0; c < cards; c++)
            {
                float seed = Random.value;
                for (int q = 0; q < 4; q++)
                {
                    cv[c * 4 + q] = cp[c];
                    cuv2[c * 4 + q] = new Vector2(seed, 0f);
                }
                cuv[c * 4] = new Vector2(0, 0); cuv[c * 4 + 1] = new Vector2(1, 0);
                cuv[c * 4 + 2] = new Vector2(1, 1); cuv[c * 4 + 3] = new Vector2(0, 1);
                int t = c * 6, o = c * 4;
                ct[t] = o; ct[t + 1] = o + 2; ct[t + 2] = o + 1;
                ct[t + 3] = o; ct[t + 4] = o + 3; ct[t + 5] = o + 2;
            }
            _flameMesh = new Mesh { name = "BossArenaFlameCards", vertices = cv, uv = cuv, uv2 = cuv2, triangles = ct };
            _flameMesh.RecalculateBounds();
            var bounds = _flameMesh.bounds;   // 꼭짓점이 밑변 가운데에 몰려 있어 펼친 크기만큼 넓혀 둔다(화면 밖으로 잘못 걸러지지 않게)
            bounds.Expand(new Vector3(CardWidth + CardHeight, CardHeight * 2f, CardWidth + CardHeight));
            _flameMesh.bounds = bounds;
            _flameMat = new Material(_groundMat) { name = "BossArenaFlameCards", renderQueue = 3101 };
            _flameMat.SetFloat("_Wall", 1f);
            _flameMat.SetFloat("_WallH", CardHeight);
            _flameMat.SetFloat("_CardW", CardWidth);
            MakeRenderer("FlameCards", _flameMesh, _flameMat);

            _cornerR = CornerR;
            return true;
        }

        /// 점 n 개 × 줄 rows 개 격자를 띠 메시로 잇는다(양면 — 셰이더가 Cull Off).
        static Mesh StripMesh(string name, Vector3[] v, Vector2[] uv, int n, int rows)
        {
            var tri = new int[(n - 1) * (rows - 1) * 6];
            int t = 0;
            for (int i = 0; i < n - 1; i++)
                for (int r = 0; r < rows - 1; r++)
                {
                    int a = i * rows + r, b = (i + 1) * rows + r;
                    tri[t++] = a; tri[t++] = a + 1; tri[t++] = b + 1;
                    tri[t++] = a; tri[t++] = b + 1; tri[t++] = b;
                }
            var mesh = new Mesh { name = name, vertices = v, uv = uv, triangles = tri };
            mesh.RecalculateBounds();
            return mesh;
        }

        Transform MakeRenderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go.transform;
        }

        /// 예전 세로 벽(탕탕특공대식 빛 장벽) — 불꽃 테두리 셰이더를 못 찾을 때만.
        void BuildWalls()
        {
            // 세로 그라데이션 텍스처 — 바닥은 진하고 위로 갈수록 투명. 빛나는 결계처럼 보인다.
            var tex = new Texture2D(1, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++)
            {
                float h = y / 31f;
                float a = Mathf.Pow(1f - h, 1.6f) * 0.85f + (h > 0.94f ? 0.6f : 0f);   // 윗단에 얇은 선
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();

            // 이펙트 셰이더 (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23) — 못 찾으면 빌드에 항상 들어가는 Sprites/Default.
            // 큐 3100 — 밤 어둠 막(2900)보다 뒤에 그려 어둠에 묻히지 않는다.
            _mat = ProtoFxMat.New("ArenaWall", tex, 3100);
            _mat.color = WallColor;

            _walls = new GameObject("Walls").transform;
            _walls.SetParent(transform, false);
            var mesh = new Mesh { name = "BossArenaWalls" };
            var v = new Vector3[16]; var uv = new Vector2[16]; var tri = new int[24];
            Vector3[] corners =
            {
                new Vector3(_minX, 0f, _minZ), new Vector3(_maxX, 0f, _minZ),
                new Vector3(_maxX, 0f, _maxZ), new Vector3(_minX, 0f, _maxZ),
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % 4];
                int o = i * 4;
                v[o] = a; v[o + 1] = b; v[o + 2] = b + Vector3.up; v[o + 3] = a + Vector3.up;
                uv[o] = new Vector2(0, 0); uv[o + 1] = new Vector2(1, 0); uv[o + 2] = new Vector2(1, 1); uv[o + 3] = new Vector2(0, 1);
                int t = i * 6;
                tri[t] = o; tri[t + 1] = o + 2; tri[t + 2] = o + 1;
                tri[t + 3] = o; tri[t + 4] = o + 3; tri[t + 5] = o + 2;
            }
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri;
            mesh.RecalculateBounds();

            _walls.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = _walls.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            // 이펙트 셰이더(옛 Sprites/Default)는 Cull Off 라 안쪽·바깥쪽 면이 다 보인다
            _walls.localScale = new Vector3(1f, 0.001f, 1f);
        }

        void Update()
        {
            _t += Time.deltaTime;

            // k: 0 = 없음, 1 = 다 섰음/다 붙었음
            float k;
            if (_ending) k = 1f - Mathf.Clamp01(_t / FallTime);
            else
            {
                k = Mathf.Clamp01(_t / RiseTime);
                k = 1f - (1f - k) * (1f - k);   // 끝에서 감속
            }

            if (_groundMat != null)
            {
                // 불꽃 테두리 — 불이 번지듯 띠가 진해지고, 카드 불꽃이 키가 자라며 혀가 길어진다(둘 다 셰이더가 _Reveal 로)
                float time = Time.time;
                _groundMat.SetFloat(IdFlameT, time);
                _groundMat.SetFloat(IdReveal, k);
                if (_flameMat != null) { _flameMat.SetFloat(IdFlameT, time); _flameMat.SetFloat(IdReveal, k); }
            }
            else if (_walls != null)
            {
                // 예전 세로 벽
                _walls.localScale = new Vector3(1f, Mathf.Max(0.001f, WallHeight * k), 1f);
                // 맥동 — 정지된 벽보다 "에너지 장벽"으로 읽힌다
                if (!_ending && _mat != null)
                {
                    float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 5f);
                    _mat.color = new Color(WallColor.r, WallColor.g, WallColor.b, pulse);
                }
            }

            if (_ending && k <= 0f) { if (_cur == this) _cur = null; Destroy(gameObject); }
        }

        void OnDestroy()
        {
            if (_cur == this) _cur = null;
            if (_mat != null) { Destroy(_mat.mainTexture); Destroy(_mat); }
            if (_groundMat != null) Destroy(_groundMat);
            if (_flameMat != null) Destroy(_flameMat);
            if (_groundMesh != null) Destroy(_groundMesh);
            if (_flameMesh != null) Destroy(_flameMesh);
        }
    }
}
