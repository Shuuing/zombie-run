using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 좀비가 죽을 때 몸이 조각나 튀는 연출 — 코드 그림(2026-11-05 사용자: "적이 죽으면 피 터지며 파편이 터지는 이펙트, 기존 좀비 캐릭터를 터지게").
    /// 피 터짐·바닥 피는 카탈로그 프리팹 <c>zombie_gore</c>(템플릿 FX_ZombieGore)가 맡고, 여기는 <b>몸 조각</b>만 한다.
    ///
    /// <para><b>방법 — 본 단위 조각 → 공간 이등분 → 두께.</b> 스킨 메시의 삼각형을 "가장 무거운 본"으로 묶고(정점이 많은 본 <see cref="MaxChunks"/>개, 나머지 본은
    /// 가장 가까운 조상 조각에), 그 조각을 삼각형 무게중심 기준으로 가장 긴 축에서 반으로 거듭 가른다 — 한 변이 <see cref="PieceSize"/>(m) 아래로 내려가거나
    /// 조각 수가 <see cref="MaxPieces"/>에 닿을 때까지(사용자 11-05: "좀 더 잘게"). 조각은 표면 껍질이라 그대로면 종이처럼 보인다(사용자 11-05: "종이짝처럼 — 묵직하게 덩어리감") —
    /// 그래서 <b>안쪽 껍질</b>(법선 반대로 <see cref="Thickness"/>만큼 들여놓은 사본)과 <b>옆면</b>(경계 변을 바깥·안쪽 껍질로 잇는 사각형)을 붙여 두꺼운 덩어리로 만들고,
    /// 안쪽과 옆면은 정점색으로 살색(<see cref="FleshColor"/>)을 입힌다(셰이더가 정점색을 곱한다). 조각 메시는 메시마다 <b>한 번</b> 만들어 캐시한다
    /// (정점을 바인드 포즈로 본 로컬 좌표에 옮긴 것). 죽는 순간 각 조각을 그 본의 <b>지금</b> 월드 자리·회전에 놓으면 죽을 때 자세 그대로 쪼개진다 — 매 죽음마다 메시를 굽지 않는다(물량전).
    /// 조각 구성은 처음 만들 때 콘솔에 적는다.</para>
    ///
    /// <para><b>움직임</b>은 코드(강체 없음): 몸 가운데서 바깥 + 위 + 맞은 방향으로 포물선을 그리며 튀고(큰 조각은 느리게, 작은 조각은 빠르게 — 질량감),
    /// 과장된 중력으로 떨어져 바닥에서 한 번 낮게 튀고 눕는다. 세기 5 · 중력 18 — 묵직하되 한두 걸음은 날아간다(사용자 11-05 "묵직하게" 뒤 "조금은 날아가게"). 회전은 느리고 바닥에 닿으면 거의 멈춘다. 수명이 다하면 <see cref="DissolveTime"/> 동안 <b>디졸브</b>
    /// (셰이더 ZombieOverrun/GibDissolve, 노이즈로 녹아 없어진다)로 사라진다. 디졸브 머티리얼(Resources/VFX/M_GibDissolve)이 없으면 줄어들며 사라진다.
    /// 조각은 풀에서 돌려 쓰고 동시에 <see cref="MaxAliveChunks"/>개까지.</para>
    ///
    /// <para><b>착지 피</b>(사용자 11-05 "조각이 날아가 떨어질 때도 그 조각 주변에 피"): 조각이 처음 바닥에 닿는 자리에 핏자국(조각 반지름의 약 3배, 작은 튄 자국 1~2개)과
    /// 핏방울 3~5개를 내고, 튀어서 다른 자리에 누우면 작은 핏자국 하나 더. 핏자국은 조각과 같은 때에 사라진다(<see cref="LandingBloodLinger"/>초 더 남길 수 있다).
    /// 루트 아래 파티클 시스템 둘(GibSplats · GibDrops)에 Emit() 로만 낸다 — 머티리얼 Resources/VFX/M_GibBlood(ZombieGibsSetup 이 M_Fx_Blood 에서 복제).</para>
    ///
    /// <para>메시가 Read/Write 가 아니면(빌드) 조각을 못 만든다 — 에디터의 ZombieGibsSetup 이 적 모델의 Read/Write 를 켠다.
    /// 조각을 못 내면 false 를 돌려주고 호출 쪽(ProtoZombie.Die · FxWeaponStage.Kill)은 피 터짐만 낸다.</para>
    /// </summary>
    public static class ZombieGibs
    {
        public static bool Enabled = true;
        /// 조각 기준 본 수(정점이 많은 본부터). 바꾸면 <see cref="InvalidateCache"/>.
        public static int MaxChunks = 8;
        /// 조각 한 변의 목표(월드 m) — 이보다 긴 조각은 반으로 가른다. 바꾸면 <see cref="InvalidateCache"/>.
        public static float PieceSize = 0.2f;
        /// 몸 하나의 조각 상한. 바꾸면 <see cref="InvalidateCache"/>.
        public static int MaxPieces = 20;
        /// 조각 두께(월드 m) — 안쪽 껍질을 이만큼 들여놓고 옆면을 붙인다. 0 이면 껍질 그대로. 바꾸면 <see cref="InvalidateCache"/>.
        public static float Thickness = 0.06f;
        /// 동시 조각 상한(물량전 20마리 × 20). 넘치면 오래된 것부터 거둔다.
        public static int MaxAliveChunks = 400;
        /// 튀는 속도(m/s). 덩치의 제곱근을 곱하고, 조각 크기에 따라 큰 것은 느리게(0.7배까지) 작은 것은 빠르게(1.3배까지).
        /// 4(3판)는 거의 제자리에 떨어졌다(사용자 11-05: "조금은 날아가게") → 5.
        public static float Power = 5f;
        /// 눕기까지 포함한 수명(초, ±20%). 마지막 <see cref="DissolveTime"/> 동안 녹아 사라진다.
        public static float Life = 1.8f;
        /// 과장된 중력 — 빨리 떨어져야 묵직하고, 시선이 다음 적에게 간다. 22(3판)는 너무 눌렸다 → 18(세기 5와 함께 날아가는 거리 약 2배).
        public static float Gravity = 18f;
        /// 회전 속도(도/초). 느려야 무겁다 — 바닥에 닿으면 거의 멈춘다.
        public static float Spin = 200f;
        /// 녹아 사라지는 시간(초). 디졸브 머티리얼이 없으면 이 시간 동안 줄어든다.
        public static float DissolveTime = 0.7f;
        /// 안쪽 껍질 · 옆면의 정점색(살). 셰이더가 알베도에 곱한다.
        public static Color FleshColor = new Color(0.55f, 0.12f, 0.10f, 1f);
        /// 조각이 바닥에 닿을 때 그 자리에 핏자국을 남기고 핏방울을 튀긴다(사용자 11-05: "조각이 날아가 떨어질 때도 그 조각 주변에 피").
        public static bool LandingBlood = true;
        /// 핏자국 크기 배수(기본 = 조각 반지름의 약 3배).
        public static float LandingSplatScale = 1f;
        /// 조각이 녹아 없어진 뒤 핏자국이 더 남는 시간(초). 핏자국은 수명의 마지막 1/4 동안 옅어진다.
        /// 1.5(11-05) → 0(11-06 사용자 "바닥에 피가 오랫동안 남는다 — 파편 사라질 때 같이"): 조각이 녹는 동안 같이 옅어져 조각과 함께 없어진다.
        public static float LandingBloodLinger = 0f;

        const float Bounce = 0.25f, GroundFriction = 0.35f, MinVertexShare = 0.02f;
        const int MinPieceTris = 6;
        const string DissolveMaterialPath = "VFX/M_GibDissolve";   // Resources 아래 — ZombieGibsSetup 이 만든다
        const string BloodMaterialPath = "VFX/M_GibBlood";         // Resources 아래 — ZombieGibsSetup 이 M_Fx_Blood(얼룩) 에서 복제(소프트 파티클 끔)
        // 핏자국 색 — 검붉게(사용자 11-05 "피 색 더 어둡게"). 얼룩 텍스처가 흰색이라 이 값이 곧 화면 색. 이전: 0.42/0.62
        // 알파 0.95 → 0.6(11-06 사용자 "투명도 빼줘 — 몹들이 뭉쳐 있을 때 피가 터져 있으니 지저분"). 처치 프리팹(FX_ZombieGore)의 바닥 피(GroundAlpha)와 같은 값.
        static readonly Color SplatDark = new Color(0.26f, 0.012f, 0.018f, 0.6f), SplatBright = new Color(0.40f, 0.025f, 0.03f, 0.6f);
        /// 조각이 처음 닿을 때 핏자국을 남길 확률(사용자 11-05 "핏자국 갯수 줄여") — 핏방울은 늘 튄다.
        public static float LandingSplatChance = 0.7f;

        class ChunkSet { public Mesh[] meshes; public int[] bone; public Vector3[] center; public float[] radius; public string note; }

        class Chunk
        {
            public Transform t; public MeshFilter mf; public MeshRenderer mr; public MaterialPropertyBlock mpb;
            public Vector3 vel, axis, baseScale; public float spin, rest, dieAt; public int bounces; public bool resting;
            public float radius, ground; public Vector3 splatAt;   // 월드 반지름 · 발 높이 · 첫 핏자국 자리(착지 피)
        }

        struct Tri { public int s, a, b, c; public Vector3 center; }
        class Piece { public int bone; public List<Tri> tris = new List<Tri>(); public Bounds bounds; public float extent; }

        static readonly Dictionary<Mesh, ChunkSet> _sets = new Dictionary<Mesh, ChunkSet>();
        static readonly Dictionary<Material, Material> _chunkMats = new Dictionary<Material, Material>();
        static readonly List<Chunk> _live = new List<Chunk>();
        static readonly Stack<Chunk> _pool = new Stack<Chunk>();
        static Transform _root;
        static Material _dissolveBase;
        static bool _dissolveTried, _warnedNoDissolve;
        static ParticleSystem _splats, _drops;   // 착지 피 — 핏자국(바닥 빌보드) · 핏방울. Emit() 로만 낸다
        static Material _bloodMat;
        static bool _bloodTried, _warnedNoBlood;
        static readonly int CullID = Shader.PropertyToID("_Cull");
        static readonly int DissolveID = Shader.PropertyToID("_Dissolve");
        static readonly int NoiseOffsetID = Shader.PropertyToID("_NoiseOffset");
        static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        static readonly int HColorID = Shader.PropertyToID("_HColor");
        static readonly int SColorID = Shader.PropertyToID("_SColor");

        public static int Alive => _live.Count;
        /// 디졸브 머티리얼을 찾았나(없으면 줄어들며 사라진다). 에디터에선 매번 다시 찾는다 — ZombieGibsSetup 이 뒤늦게 만들 수 있다.
        public static bool Dissolving { get { if (_dissolveBase == null && Application.isEditor) _dissolveTried = false; return DissolveBase() != null; } }
        /// 착지 피 머티리얼을 찾았나(없으면 핏자국 없이 조각만). 에디터에선 매번 다시 찾는다.
        public static bool Bleeding { get { if (_bloodMat == null && Application.isEditor) _bloodTried = false; return BloodBase() != null; } }

        /// <summary>
        /// 이 오브젝트(좀비 루트) 아래 스킨 메시를 조각내 튀긴다. 몸이 살아 있는(활성) 상태에서 불러야 본 자리를 읽는다.
        /// center = 몸 가운데(월드), groundY = 발 높이, dir = 맞은 방향(0 이면 무작위), scale = 덩치.
        /// 조각을 하나라도 냈으면 true — 호출 쪽이 몸을 숨긴다.
        /// </summary>
        public static bool Burst(GameObject body, Vector3 center, float groundY, Vector3 dir, float scale = 1f)
        {
            if (!Enabled || body == null) return false;
            int made = 0;
            body.GetComponentsInChildren(false, _smrBuf);   // 목록 재사용 — 예전엔 처치마다 새 배열(2026-11-20)
            for (int i = 0; i < _smrBuf.Count; i++)
                made += BurstOne(_smrBuf[i], center, groundY, dir, scale);
            _smrBuf.Clear();
            return made > 0;
        }

        static readonly System.Collections.Generic.List<SkinnedMeshRenderer> _smrBuf = new System.Collections.Generic.List<SkinnedMeshRenderer>(4);

        static int BurstOne(SkinnedMeshRenderer smr, Vector3 center, float groundY, Vector3 dir, float scale)
        {
            if (smr == null || smr.sharedMesh == null || !smr.enabled) return 0;
            var set = GetSet(smr);
            if (set == null) return 0;
            var bones = smr.bones;
            if (DissolveBase() == null && !_warnedNoDissolve)
            {
                _warnedNoDissolve = true;
                Debug.LogWarning($"[파편] Resources/{DissolveMaterialPath} 가 없다 — 조각이 녹는 대신 줄어들며 사라진다(에디터에선 ZombieGibsSetup 이 만든다, 다음 포커스 때)");
            }
            var mats = ChunkMaterials(smr.sharedMaterials);
            float power = Power * Mathf.Sqrt(Mathf.Max(0.3f, scale));
            Vector3 flat = dir; flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f) flat.Normalize();
            else { float a = Random.value * Mathf.PI * 2f; flat = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

            int made = 0;
            for (int i = 0; i < set.meshes.Length; i++)
            {
                var b = set.bone[i] < bones.Length ? bones[set.bone[i]] : null;
                if (b == null) continue;
                var c = Rent();
                c.t.SetPositionAndRotation(b.position, b.rotation);
                c.baseScale = b.lossyScale;
                c.t.localScale = c.baseScale;
                c.mf.sharedMesh = set.meshes[i];
                c.mr.sharedMaterials = mats;
                c.mpb.SetFloat(DissolveID, 0f);
                c.mpb.SetVector(NoiseOffsetID, new Vector4(Random.value * 100f, Random.value * 100f, Random.value * 100f, 0f));
                c.mr.SetPropertyBlock(c.mpb);

                // 질량감 — 큰 조각은 느리고 작은 조각은 빠르다(월드 반지름 9cm 기준)
                float radiusWorld = set.radius[i] * Mathf.Max(c.baseScale.x, 0.01f);
                float mass = Mathf.Clamp(0.09f / Mathf.Max(0.04f, radiusWorld), 0.7f, 1.3f);
                float p = power * mass;

                Vector3 outward = b.TransformPoint(set.center[i]) - center;
                outward.y *= 0.4f;                                       // 위아래보다 옆으로 퍼지게
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Random.onUnitSphere;
                c.vel = outward * (p * 0.5f) + flat * (p * 0.45f)
                      + Vector3.up * (p * Random.Range(0.75f, 1.1f)) + Random.insideUnitSphere * (p * 0.22f);
                c.axis = Random.onUnitSphere;
                c.spin = Spin * Random.Range(0.4f, 1.2f) * mass;
                c.rest = groundY + set.radius[i] * 0.6f * Mathf.Max(c.baseScale.x, 0.01f);   // 반쯤 묻히지 않게
                c.radius = radiusWorld; c.ground = groundY; c.splatAt = new Vector3(float.NaN, 0f, 0f);
                c.dieAt = Time.time + Life * Random.Range(0.8f, 1.2f);
                c.bounces = 0; c.resting = false;
                c.t.gameObject.SetActive(true);
                _live.Add(c);
                made++;
            }
            while (_live.Count > MaxAliveChunks) Recycle(0);
            return made;
        }

        // ── 매 프레임 ──────────────────────────────────────────────────
        internal static void Tick(float dt)
        {
            float now = Time.time;
            bool dissolve = _dissolveBase != null;
            float fadeTime = Mathf.Max(0.05f, DissolveTime);
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var c = _live[i];
                if (c.t == null) { _live.RemoveAt(i); continue; }
                float left = c.dieAt - now;
                if (left <= 0f) { Recycle(i); continue; }
                if (!c.resting && dt > 0f)
                {
                    c.vel.y -= Gravity * dt;
                    Vector3 p = c.t.position + c.vel * dt;
                    if (p.y <= c.rest)
                    {
                        p.y = c.rest;
                        c.bounces++;
                        bool first = c.bounces == 1;
                        if (c.bounces >= 2 || Mathf.Abs(c.vel.y) < 2f) { c.resting = true; c.vel = Vector3.zero; c.spin = 0f; }
                        else { c.vel.y = -c.vel.y * Bounce; c.vel.x *= GroundFriction; c.vel.z *= GroundFriction; c.spin *= 0.3f; }
                        // 착지 피 — 처음 닿을 때 그 자리에 핏자국 + 핏방울, 튀어 다른 자리에 누우면 작은 핏자국 하나 더
                        if (first) { Splat(p, c, left, 1f, true, Random.value < LandingSplatChance); c.splatAt = p; }
                        else if (c.resting && !float.IsNaN(c.splatAt.x) && (p - c.splatAt).sqrMagnitude > 0.04f && Random.value < LandingSplatChance * 0.5f) Splat(p, c, left, 0.6f, false, true);
                    }
                    c.t.position = p;
                    if (c.spin > 0f) c.t.rotation = Quaternion.AngleAxis(c.spin * dt, c.axis) * c.t.rotation;
                }
                if (left < fadeTime)
                {
                    if (dissolve) { c.mpb.SetFloat(DissolveID, 1f - left / fadeTime); c.mr.SetPropertyBlock(c.mpb); }
                    else c.t.localScale = c.baseScale * (left / fadeTime);
                }
            }
        }

        /// 살아 있는 조각을 모두 거두고 풀을 비운다(무대 리로드 · 씬 전환). 조각 메시 캐시는 남긴다.
        public static void Clear()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Recycle(i);
            while (_pool.Count > 0) { var c = _pool.Pop(); if (c.t != null) Object.Destroy(c.t.gameObject); }
            foreach (var m in _chunkMats.Values) if (m != null) Object.Destroy(m);
            _chunkMats.Clear();
            _splats = null; _drops = null;   // 루트와 함께 사라진다
            if (_root != null) { Object.Destroy(_root.gameObject); _root = null; }
        }

        /// 조각 메시 캐시를 비운다 — MaxChunks · PieceSize · MaxPieces · Thickness 를 바꿨을 때(다음 죽음에 다시 만든다).
        public static void InvalidateCache()
        {
            foreach (var s in _sets.Values) if (s != null) foreach (var m in s.meshes) if (m != null) Object.Destroy(m);
            _sets.Clear();
        }

        internal static void OnRunnerDestroyed()
        {
            _live.Clear(); _pool.Clear(); _chunkMats.Clear(); _root = null; _splats = null; _drops = null;
        }

        // ── 풀 ──────────────────────────────────────────────────────────
        static Chunk Rent()
        {
            EnsureRoot();
            if (_pool.Count > 0) { var p = _pool.Pop(); if (p.t != null) return p; }
            var go = new GameObject("Gib");
            go.transform.SetParent(_root, false);
            var c = new Chunk { t = go.transform, mf = go.AddComponent<MeshFilter>(), mr = go.AddComponent<MeshRenderer>(), mpb = new MaterialPropertyBlock() };
            c.mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.mr.receiveShadows = false;
            c.mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return c;
        }

        static void Recycle(int i)
        {
            var c = _live[i];
            _live.RemoveAt(i);
            if (c.t == null) return;
            c.t.gameObject.SetActive(false);
            c.mf.sharedMesh = null;
            _pool.Push(c);
        }

        static void EnsureRoot()
        {
            if (_root != null) return;
            var go = new GameObject("ZombieGibs");
            go.AddComponent<ZombieGibsRunner>();
            _root = go.transform;
        }

        static Material DissolveBase()
        {
            if (_dissolveTried) return _dissolveBase;
            _dissolveTried = true;
            _dissolveBase = Resources.Load<Material>(DissolveMaterialPath);
            return _dissolveBase;
        }

        // ── 착지 피 ──────────────────────────────────────────────────────
        // 파티클 시스템 둘(핏자국 = 바닥 빌보드, 핏방울 = 빌보드 + 중력)을 루트 아래에 하나씩 두고 Emit() 로 입자를 낸다 — 조각 400개가 떨어져도 오브젝트가 늘지 않는다.
        // 핏자국 수명 = 조각이 남은 시간 + LandingBloodLinger(기본 0 — 조각이 녹는 동안 같이 옅어져 함께 사라진다).
        static Material BloodBase()
        {
            if (_bloodTried) return _bloodMat;
            _bloodTried = true;
            _bloodMat = Resources.Load<Material>(BloodMaterialPath);
            return _bloodMat;
        }

        static bool EnsureBlood()
        {
            if (_splats != null && _drops != null) return true;
            var mat = BloodBase();
            if (mat == null)
            {
                if (!_warnedNoBlood) { _warnedNoBlood = true; Debug.LogWarning($"[파편] Resources/{BloodMaterialPath} 가 없다 — 조각이 떨어져도 핏자국이 안 생긴다(에디터에선 ZombieGibsSetup 이 만든다, 다음 포커스 때)"); }
                return false;
            }
            EnsureRoot();
            _splats = MakeBloodSystem("GibSplats", mat, ParticleSystemRenderMode.HorizontalBillboard, 0f, 0.75f);
            _drops = MakeBloodSystem("GibDrops", mat, ParticleSystemRenderMode.Billboard, 1.3f, 0.6f);
            return true;
        }

        static ParticleSystem MakeBloodSystem(string name, Material mat, ParticleSystemRenderMode mode, float gravity, float fadeFrom)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.loop = true; m.playOnAwake = true; m.duration = 1f; m.maxParticles = 2000;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.gravityModifier = gravity; m.startSpeed = 0f; m.startSize = 0.2f; m.startLifetime = 1f;
            var em = ps.emission; em.enabled = false;      // Emit() 로만
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeFrom), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat; r.renderMode = mode;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.alignment = ParticleSystemRenderSpace.View;
            ps.Play();
            return ps;
        }

        /// at = 닿은 자리(월드), left = 조각이 남은 수명, mul = 핏자국 크기 배수, drops = 핏방울도 튀길지, stain = 핏자국을 남길지.
        static void Splat(Vector3 at, Chunk c, float left, float mul, bool drops, bool stain)
        {
            if (!LandingBlood || !EnsureBlood()) return;
            float life = Mathf.Max(0.4f, left + LandingBloodLinger);
            float size = Mathf.Clamp(c.radius * Random.Range(2.6f, 3.8f) * mul * LandingSplatScale, 0.1f, 0.7f);
            if (stain)
            {
                var e = new ParticleSystem.EmitParams
                {
                    position = new Vector3(at.x, c.ground + 0.025f, at.z),
                    velocity = Vector3.zero,
                    startSize = size,
                    startLifetime = life,
                    startColor = Color.Lerp(SplatDark, SplatBright, Random.value),
                    rotation = Random.Range(0f, 360f),
                };
                _splats.Emit(e, 1);
                int extra = Random.value < 0.4f ? 1 : 0;                       // 옆에 작은 튄 자국 — 열에 넷만 하나(이전 1~2개)
                for (int i = 0; i < extra; i++)
                {
                    var off = Random.insideUnitCircle * size * 0.7f;
                    e.position = new Vector3(at.x + off.x, c.ground + 0.02f, at.z + off.y);
                    e.startSize = size * Random.Range(0.25f, 0.5f);
                    e.startLifetime = life * Random.Range(0.6f, 1f);
                    e.rotation = Random.Range(0f, 360f);
                    _splats.Emit(e, 1);
                }
            }
            if (!drops) return;
            int n = Random.Range(3, 6);                                     // 닿는 순간 위로 튀는 핏방울
            for (int i = 0; i < n; i++)
            {
                var flat = Random.insideUnitCircle * Random.Range(0.6f, 1.6f);
                var d = new ParticleSystem.EmitParams
                {
                    position = at + Vector3.up * c.radius * 0.3f,
                    velocity = new Vector3(flat.x, Random.Range(1.2f, 2.6f), flat.y),
                    startSize = Random.Range(0.025f, 0.05f),
                    startLifetime = Random.Range(0.3f, 0.5f),
                    startColor = SplatBright,
                    rotation = Random.Range(0f, 360f),
                };
                _drops.Emit(d, 1);
            }
        }

        /// 조각 머티리얼 — 디졸브 셰이더 복제본에 좀비 툰 머티리얼의 알베도 · 색 · 하이라이트 · 그림자 색을 옮긴다. 디졸브가 없으면 양면(_Cull 0) 복제본.
        static Material[] ChunkMaterials(Material[] src)
        {
            var dst = new Material[src.Length];
            var dissolve = DissolveBase();
            for (int i = 0; i < src.Length; i++)
            {
                var m = src[i];
                if (m == null) { dst[i] = null; continue; }
                if (_chunkMats.TryGetValue(m, out var made) && made != null) { dst[i] = made; continue; }
                if (dissolve != null)
                {
                    made = new Material(dissolve) { name = m.name + "_gib" };
                    var tex = m.HasProperty(BaseMapID) ? m.GetTexture(BaseMapID) : m.mainTexture;
                    if (tex != null) { made.SetTexture(BaseMapID, tex); made.mainTexture = tex; }
                    if (m.HasProperty(BaseColorID)) made.SetColor(BaseColorID, m.GetColor(BaseColorID));
                    if (m.HasProperty(HColorID)) made.SetColor(HColorID, m.GetColor(HColorID));
                    if (m.HasProperty(SColorID)) made.SetColor(SColorID, m.GetColor(SColorID));
                    made.renderQueue = m.renderQueue;   // 몸과 같은 자리(밤 어둠 막 앞)
                }
                else if (m.HasProperty(CullID))
                {
                    made = new Material(m) { name = m.name + "_gib" };
                    made.SetFloat(CullID, 0f);
                }
                else made = m;
                _chunkMats[m] = made;
                dst[i] = made;
            }
            return dst;
        }

        // ── 조각 메시 ───────────────────────────────────────────────────
        static ChunkSet GetSet(SkinnedMeshRenderer smr)
        {
            var mesh = smr.sharedMesh;
            if (_sets.TryGetValue(mesh, out var set)) return set;
            set = Build(mesh, smr.bones);
            _sets[mesh] = set;   // null 도 기억 — 매 죽음마다 다시 시도하지 않게
            return set;
        }

        static ChunkSet Build(Mesh mesh, Transform[] bones)
        {
            if (!mesh.isReadable && !Application.isEditor)
            {
                Debug.LogWarning($"[파편] {mesh.name}: 메시가 Read/Write 가 아니라 조각을 못 만든다 — 모델 임포트의 Read/Write 를 켤 것(에디터는 ZombieGibsSetup 이 켠다)");
                return null;
            }
            var weights = mesh.boneWeights;
            var binds = mesh.bindposes;
            int nv = mesh.vertexCount;
            if (weights == null || weights.Length != nv || binds == null || binds.Length == 0 || bones == null || bones.Length != binds.Length)
            {
                Debug.LogWarning($"[파편] {mesh.name}: 본 가중치/바인드 포즈가 안 맞아 조각을 못 만든다 (정점 {nv}, 가중치 {(weights != null ? weights.Length : 0)}, 본 {(bones != null ? bones.Length : 0)}/{(binds != null ? binds.Length : 0)})");
                return null;
            }
            var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv; var cols = mesh.colors;
            int nb = binds.Length;

            // 정점마다 가장 무거운 본
            var dom = new int[nv]; var share = new int[nb];
            for (int v = 0; v < nv; v++)
            {
                var w = weights[v];
                int b = w.boneIndex0; float best = w.weight0;
                if (w.weight1 > best) { best = w.weight1; b = w.boneIndex1; }
                if (w.weight2 > best) { best = w.weight2; b = w.boneIndex2; }
                if (w.weight3 > best) { b = w.boneIndex3; }
                if (b < 0 || b >= nb) b = 0;
                dom[v] = b; share[b]++;
            }

            // 조각 기준 본: 정점이 많은 순서로 MaxChunks 개(정점 2% 미만은 제외)
            var order = new List<int>(nb);
            for (int b = 0; b < nb; b++) order.Add(b);
            order.Sort((a, b) => share[b].CompareTo(share[a]));
            var isGroup = new bool[nb]; int groups = 0;
            foreach (int b in order)
            {
                if (groups >= Mathf.Max(1, MaxChunks)) break;
                if (share[b] == 0 || (groups > 0 && share[b] < nv * MinVertexShare)) break;
                isGroup[b] = true; groups++;
            }
            if (groups == 0) return null;

            // 본 → 조각 본(자기 자신이거나 가장 가까운 조상 조각, 없으면 가장 큰 조각)
            var indexOf = new Dictionary<Transform, int>(nb);
            for (int b = 0; b < nb; b++) if (bones[b] != null && !indexOf.ContainsKey(bones[b])) indexOf[bones[b]] = b;
            var groupOf = new int[nb];
            for (int b = 0; b < nb; b++)
            {
                int g = -1;
                for (var t = bones[b]; t != null && g < 0; t = t.parent)
                    if (indexOf.TryGetValue(t, out int k) && isGroup[k]) g = k;
                groupOf[b] = g >= 0 ? g : order[0];
            }

            // 삼각형을 본 조각에 나눈다(세 정점 중 다수의 조각). 무게중심은 그 본의 로컬 좌표로 — 이등분 기준
            int subCount = mesh.subMeshCount;
            var byBone = new Dictionary<int, Piece>();
            for (int s = 0; s < subCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int g0 = groupOf[dom[tris[i]]], g1 = groupOf[dom[tris[i + 1]]], g2 = groupOf[dom[tris[i + 2]]];
                    int g = g0 == g1 || g0 == g2 ? g0 : (g1 == g2 ? g1 : g0);
                    if (!byBone.TryGetValue(g, out var piece)) { piece = new Piece { bone = g }; byBone[g] = piece; }
                    var c = binds[g].MultiplyPoint3x4((verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f);
                    piece.tris.Add(new Tri { s = s, a = tris[i], b = tris[i + 1], c = tris[i + 2], center = c });
                }
            }

            // 공간 이등분 — 가장 긴 조각부터 가장 긴 축에서 반으로. 한 변이 PieceSize(월드 m) 아래거나 MaxPieces 에 닿으면 멈춘다
            var pieces = new List<Piece>();
            foreach (int g in order) if (byBone.TryGetValue(g, out var p)) { Measure(p, bones); pieces.Add(p); }
            int cap = Mathf.Max(pieces.Count, MaxPieces);
            while (pieces.Count < cap)
            {
                int big = -1; float bigExt = 0f;
                for (int i = 0; i < pieces.Count; i++)
                    if (pieces[i].tris.Count >= MinPieceTris * 2 && pieces[i].extent > bigExt) { bigExt = pieces[i].extent; big = i; }
                if (big < 0 || bigExt <= PieceSize) break;
                var src = pieces[big];
                Vector3 size = src.bounds.size;
                int axis = size.x >= size.y && size.x >= size.z ? 0 : (size.y >= size.z ? 1 : 2);
                src.tris.Sort((x, y) => x.center[axis].CompareTo(y.center[axis]));
                int half = src.tris.Count / 2;
                var lo = new Piece { bone = src.bone }; var hi = new Piece { bone = src.bone };
                for (int i = 0; i < src.tris.Count; i++) (i < half ? lo : hi).tris.Add(src.tris[i]);
                Measure(lo, bones); Measure(hi, bones);
                pieces[big] = lo; pieces.Add(hi);
            }

            // 조각 메시 — 정점을 조각 본 로컬 좌표로(바인드 포즈 = 메시 공간 → 본 공간). 본의 월드 변환 × 이 좌표 = 지금 자세의 자리. 그 뒤 두께를 붙인다
            var meshes = new List<Mesh>(); var boneIdx = new List<int>(); var centers = new List<Vector3>(); var radii = new List<float>();
            var map = new int[nv];
            var cv = new List<Vector3>(); var cn = new List<Vector3>(); var cu = new List<Vector2>(); var cc = new List<Color>();
            var perBone = new Dictionary<int, int>();
            foreach (var piece in pieces)
            {
                if (piece.tris.Count == 0) continue;
                int g = piece.bone;
                var m = binds[g];
                cv.Clear(); cn.Clear(); cu.Clear(); cc.Clear();
                for (int v = 0; v < nv; v++) map[v] = -1;
                var subTris = new List<int>[subCount];
                for (int s = 0; s < subCount; s++) subTris[s] = new List<int>();
                foreach (var tri in piece.tris)
                {
                    var dst = subTris[tri.s];
                    dst.Add(MapVertex(tri.a)); dst.Add(MapVertex(tri.b)); dst.Add(MapVertex(tri.c));
                }
                int MapVertex(int v)
                {
                    if (map[v] >= 0) return map[v];
                    map[v] = cv.Count;
                    cv.Add(m.MultiplyPoint3x4(verts[v]));
                    cn.Add(norms != null && norms.Length == nv ? m.MultiplyVector(norms[v]).normalized : Vector3.up);
                    if (uvs != null && uvs.Length == nv) cu.Add(uvs[v]);
                    cc.Add(cols != null && cols.Length == nv ? cols[v] : Color.white);
                    return map[v];
                }
                float boneScale = g < bones.Length && bones[g] != null ? Mathf.Abs(bones[g].lossyScale.x) : 1f;
                if (boneScale < 1e-4f) boneScale = 1f;
                if (Thickness > 0f) Thicken(cv, cn, cu, cc, subTris, Thickness / boneScale, piece.bounds.center);

                string boneName = bones[g] != null ? bones[g].name : g.ToString();
                var cm = new Mesh { name = mesh.name + "_gib_" + boneName };
                if (cv.Count > 65000) cm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                cm.SetVertices(cv); cm.SetNormals(cn);
                if (cu.Count == cv.Count) cm.SetUVs(0, cu);
                cm.SetColors(cc);
                cm.subMeshCount = subCount;
                for (int s = 0; s < subCount; s++) cm.SetTriangles(subTris[s], s, true);
                cm.RecalculateBounds();
                meshes.Add(cm); boneIdx.Add(g); centers.Add(cm.bounds.center); radii.Add(cm.bounds.extents.magnitude);
                perBone[g] = perBone.TryGetValue(g, out int n) ? n + 1 : 1;
            }
            if (meshes.Count == 0) return null;
            var note = new System.Text.StringBuilder();
            foreach (int g in order) if (perBone.TryGetValue(g, out int n)) note.Append(note.Length > 0 ? " · " : "").Append(bones[g] != null ? bones[g].name : g.ToString()).Append('×').Append(n);
            var set = new ChunkSet { meshes = meshes.ToArray(), bone = boneIdx.ToArray(), center = centers.ToArray(), radius = radii.ToArray(), note = note.ToString() };
            Debug.Log($"[파편] {mesh.name}: 조각 {set.meshes.Length}개 (정점 {nv}, 본 {nb}, 본 조각 {groups}, 조각 크기 {PieceSize:0.00}m, 두께 {Thickness * 100f:0}cm) — {set.note}");
            return set;
        }

        /// <summary>
        /// 껍질 조각을 두꺼운 덩어리로 — ① 안쪽 껍질: 정점을 법선 반대로 두께만큼 들여놓고 법선을 뒤집은 사본(삼각형은 순서를 뒤집어)
        /// ② 옆면: 경계 변(조각 안에서 한 삼각형에만 쓰인 변)마다 바깥 정점 둘과 안쪽 정점 둘을 잇는 사각형(평면 법선, 조각 중심에서 바깥을 보게).
        /// 안쪽 껍질과 옆면은 정점색을 살색으로(바깥은 원래 색 또는 흰색). 옆면은 서브메시 0 에 넣는다. 셰이더가 양면이라 감김 방향은 보이는 데 상관없다.
        /// </summary>
        static void Thicken(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<Color> col, List<int>[] subTris, float thick, Vector3 centroid)
        {
            int outer = v.Count;
            bool hasUv = uv.Count == outer;
            for (int i = 0; i < outer; i++)
            {
                v.Add(v[i] - n[i] * thick); n.Add(-n[i]);
                if (hasUv) uv.Add(uv[i]);
                col.Add(FleshColor);
            }
            // 경계 변 — 변마다 쓰인 횟수
            var count = new Dictionary<long, int>();
            var first = new Dictionary<long, (int a, int b)>();
            for (int s = 0; s < subTris.Length; s++)
            {
                var t = subTris[s];
                for (int i = 0; i + 2 < t.Count; i += 3)
                {
                    Edge(t[i], t[i + 1]); Edge(t[i + 1], t[i + 2]); Edge(t[i + 2], t[i]);
                }
            }
            void Edge(int a, int b)
            {
                long key = (long)Mathf.Min(a, b) * outer + Mathf.Max(a, b);
                count[key] = count.TryGetValue(key, out int k) ? k + 1 : 1;
                if (!first.ContainsKey(key)) first[key] = (a, b);
            }
            var wall = new List<int>();
            foreach (var kv in count)
            {
                if (kv.Value != 1) continue;
                var (a, b) = first[kv.Key];
                Vector3 pa = v[a], pb = v[b];
                Vector3 wn = Vector3.Cross(pb - pa, n[a] + n[b]);
                if (wn.sqrMagnitude < 1e-10f) wn = (pa + pb) * 0.5f - centroid;
                if (Vector3.Dot(wn, (pa + pb) * 0.5f - centroid) < 0f) wn = -wn;
                wn = wn.sqrMagnitude > 1e-10f ? wn.normalized : Vector3.up;
                int i0 = v.Count;
                v.Add(pa); v.Add(pb); v.Add(v[b + outer]); v.Add(v[a + outer]);
                for (int k = 0; k < 4; k++) { n.Add(wn); col.Add(FleshColor); }
                if (hasUv) { uv.Add(uv[a]); uv.Add(uv[b]); uv.Add(uv[b]); uv.Add(uv[a]); }
                wall.Add(i0); wall.Add(i0 + 1); wall.Add(i0 + 2);
                wall.Add(i0); wall.Add(i0 + 2); wall.Add(i0 + 3);
            }
            // 안쪽 껍질 삼각형(순서를 뒤집어)
            for (int s = 0; s < subTris.Length; s++)
            {
                var t = subTris[s];
                int cnt = t.Count;
                for (int i = 0; i + 2 < cnt; i += 3) { t.Add(t[i + 2] + outer); t.Add(t[i + 1] + outer); t.Add(t[i] + outer); }
            }
            subTris[0].AddRange(wall);
        }

        /// 조각의 무게중심 경계와 월드 기준 가장 긴 변(본 스케일을 곱한다 — cm 단위 리그도 m 로 잰다).
        static void Measure(Piece p, Transform[] bones)
        {
            if (p.tris.Count == 0) { p.bounds = new Bounds(); p.extent = 0f; return; }
            var b = new Bounds(p.tris[0].center, Vector3.zero);
            for (int i = 1; i < p.tris.Count; i++) b.Encapsulate(p.tris[i].center);
            p.bounds = b;
            float scale = p.bone < bones.Length && bones[p.bone] != null ? Mathf.Abs(bones[p.bone].lossyScale.x) : 1f;
            if (scale < 1e-4f) scale = 1f;
            p.extent = Mathf.Max(b.size.x, b.size.y, b.size.z) * scale;
        }
    }

    /// 조각을 매 프레임 움직이는 손 — ZombieGibs 가 첫 조각 때 숨은 오브젝트에 붙인다. 씬과 함께 사라진다.
    internal sealed class ZombieGibsRunner : MonoBehaviour
    {
        void Update() => ZombieGibs.Tick(Time.deltaTime);
        void OnDestroy() => ZombieGibs.OnRunnerDestroyed();
    }
}
