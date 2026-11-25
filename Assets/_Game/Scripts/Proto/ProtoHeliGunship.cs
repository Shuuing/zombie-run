using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 헬리콥터 공습 — 공격 헬기의 기총 소사 (사용자 요청 2026-10-28, 참고 GIF: 다른 게임의 "헬리콥터 공습" 카드).
    ///
    /// <para><b>흐름(GIF 그대로):</b> ① 바닥에 노란 경고 띠가 깔리고 화살표(∨)가 비행 방향으로 흐른다(WarnTime)
    /// → ② 헬기가 화면 위에서 낮게 들어와 띠를 따라 지나가며 양 날개 기관포로 띠 위를 쓸어 버린다
    /// → ③ 화면 아래(카메라 쪽)로 빠져나간다. 레벨이 오르면 한 번 부를 때 지나가는 횟수가 는다(띠마다 다른 줄).</para>
    ///
    /// <para>사격은 즉발 — 예광 광선(<see cref="ProtoBeamFx"/>)과 판정이 같은 프레임이다. 헬기 앞쪽 띠 위의 좀비를 골라 쏜다.
    /// 모델은 헬기 추락 이벤트와 같은 Resources/Vehicles/Heli01(기수 +Z, 날개 포드 Object002·003).</para>
    ///
    /// <para>카메라: 플레이어 위 15m·58° 로 내려다본다. 헬기는 낮게(3m) 날아야 화면 안에 든다 —
    /// 처음엔 플레이어 위에 떠서 쏘는 방식이었는데 높이 띄우면 화면 밖, 낮추면 화면을 덮었다.</para>
    ///
    /// <para>게임 시간으로 돈다(레벨업 카드 중엔 멈춘다). 씬 배선 없음(지연 생성 싱글턴).</para>
    /// </summary>
    public class ProtoHeliGunship : MonoBehaviour
    {
        // ── 경고 띠 ──────────────────────────────────────────────────────
        const float WarnTime   = 1.6f;    // 띠가 깔리고 헬기가 화면에 들어오기까지
        const float LaneWidth  = 3.2f;
        const float LaneHalf   = 18f;     // 띠 반길이 — 화면(가로 ~26m, 세로 ~15m) 밖까지
        const float ChevronGap = 2.4f;    // 화살표 간격(m)
        const float ChevronScroll = 3.5f; // 화살표가 흐르는 속도(m/s)
        static readonly Color LaneColor = new Color(1f, 0.78f, 0.18f, 0.8f);

        // ── 비행 ─────────────────────────────────────────────────────────
        const float Scale     = 0.85f;
        const float Height    = 3.0f;
        const float FlyHalf   = 20f;      // 경로 반길이 — 화면 밖에서 들어와 밖으로 나간다
        const float Speed     = 15f;      // m/s → 한 번 지나가는 데 약 2.7초
        const float PassGap   = 0.5f;     // 여러 번 지나갈 때 앞 헬기가 나가고 다음 띠까지
        const float AimReach  = 9f;       // 이 거리 안(플레이어 기준)의 좀비 무리를 띠가 지나간다

        // ── 사격 ─────────────────────────────────────────────────────────
        const float FireRate  = 16f;      // 초당 발수(양 날개 번갈아)
        const float FireZone  = 13f;      // 띠 중심에서 앞뒤 이 거리 안에서만 쏜다(화면 안)
        const float AheadMin  = 1.5f, AheadMax = 7f;   // 헬기 앞쪽 이 구간의 띠 위를 쏜다
        const float HitRadius = 0.9f;
        static readonly Color Tracer = new Color(1f, 0.8f, 0.35f, 1f);

        static ProtoHeliGunship _instance;
        static Texture2D _chevronTex;

        class Pass
        {
            public Vector3 center, dir;   // 띠 중심(지면), 비행 방향(수평 단위)
            public float t0;              // 이 회차의 경고 시작 시각(_t 기준)
            public Transform lane; public Material laneMat;
            public Transform heli, rotor, tail;
            public Vector3 muzzleL, muzzleR;
            public bool done;
        }

        readonly List<Pass> _passes = new List<Pass>();
        Transform _follow;
        float _t = -1f, _dmg, _nextShot;
        int _shot;
        AudioSource _voice;
        float _voiceFade = -1f;

        public static bool Active => _instance != null && _instance._t >= 0f;

        /// 출격. follow = 플레이어, damagePerShot = 한 발 피해(공격력 배율·치명타는 여기서), passes = 지나가는 횟수,
        /// fx = 무기 연출 세트(총구·착탄 불똥·처치. 비우면 기본 세트). 이미 출격 중이면 무시한다(쿨타임 45초라 겹칠 일이 없다).
        public static void Call(Transform follow, float damagePerShot, int passes, WeaponFxSet fx = null)
        {
            if (_instance == null) _instance = new GameObject("_HeliGunship").AddComponent<ProtoHeliGunship>();
            if (Active) return;
            _instance.Begin(follow, damagePerShot, Mathf.Max(1, passes), fx);
        }

        /// 결과 화면·재시작 — 헬기·띠를 치운다.
        public static void Stop()
        {
            if (_instance != null) _instance.Clear();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        /// 차선 메시는 지날 때마다 새로 만든다 — 오브젝트와 같이 지운다(2026-11-20: 예전엔 메시가 남아 쌓였다)
        static void DestroyLaneMesh(Transform lane)
        {
            var mf = lane.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
        }

        WeaponFxSet _fx;

        void Begin(Transform follow, float dmg, int passes, WeaponFxSet fx)
        {
            Clear();
            _follow = follow;
            _dmg = dmg;
            _fx = fx != null ? fx : WeaponFxSet.Default;
            float pass = FlyHalf * 2f / Speed;
            for (int i = 0; i < passes; i++)
                _passes.Add(new Pass { t0 = i * (WarnTime + pass + PassGap) });
            _t = 0f;
            _nextShot = 0f;
            _shot = 0;
            StartVoice(WarnTime + FlyHalf / Speed);
        }

        // ── 회차 준비 ────────────────────────────────────────────────────
        void Arm(Pass p)
        {
            CamAxes(out var camRight, out var camFwd);
            Vector3 me = _follow != null ? _follow.position : Vector3.zero; me.y = 0f;
            // GIF 처럼 화면 위 → 아래(카메라 쪽)로 지나간다. 회차마다 기울기를 조금씩 바꾼다
            p.dir = Quaternion.Euler(0f, Random.Range(-28f, 28f), 0f) * (-camFwd);
            p.center = PickLaneCenter(me, p.dir);

            p.lane = BuildLane(out p.laneMat);
            p.lane.position = p.center + Vector3.up * 0.04f;
            p.lane.rotation = Quaternion.LookRotation(p.dir);
        }

        /// 플레이어 근처(AimReach)에서 띠 폭 안에 좀비가 가장 많이 드는 줄. 없으면 플레이어 옆 2m.
        Vector3 PickLaneCenter(Vector3 me, Vector3 dir)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 best = me + side * (Random.value < 0.5f ? 2f : -2f);
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return best;
            var list = sd.Active;
            int bestN = 0;
            int seen = 0;
            for (int i = 0; i < list.Count && seen < 40; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 zp = z.transform.position; zp.y = 0f;
                if ((zp - me).sqrMagnitude > AimReach * AimReach) continue;
                seen++;
                // 이 좀비를 지나는 줄에 몇 마리가 드나 — 옆 거리만 본다(줄은 화면 밖까지 길다)
                int n = 0;
                for (int j = 0; j < list.Count; j++)
                {
                    var o = list[j];
                    if (o == null || !o.Alive) continue;
                    Vector3 d = o.transform.position - zp; d.y = 0f;
                    if (Mathf.Abs(Vector3.Dot(d, side)) <= LaneWidth * 0.5f && Mathf.Abs(Vector3.Dot(d, dir)) <= FireZone) n++;
                }
                if (n > bestN) { bestN = n; best = zp; }
            }
            return best;
        }

        void Launch(Pass p)
        {
            var prefab = Resources.Load<GameObject>("Vehicles/Heli01");
            if (prefab == null) { Debug.LogWarning("[헬기 공습] Resources/Vehicles/Heli01 없음"); p.done = true; return; }
            Vector3 start = p.center - p.dir * FlyHalf + Vector3.up * Height;
            p.heli = Instantiate(prefab, start, Quaternion.LookRotation(p.dir), transform).transform;
            p.heli.name = "Gunship";
            p.heli.localScale = Vector3.one * Scale;
            p.rotor = MakePivot(p.heli, "Object001");
            p.tail  = MakePivot(p.heli, "Object007");
            p.muzzleL = PodTip(p.heli, "Object003", new Vector3(-0.8f, 0.7f, 0.9f));
            p.muzzleR = PodTip(p.heli, "Object002", new Vector3(0.8f, 0.7f, 0.9f));
        }

        static void CamAxes(out Vector3 right, out Vector3 fwd)
        {
            var cam = Camera.main;
            fwd = cam != null ? cam.transform.forward : Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            right = Vector3.Cross(Vector3.up, fwd);
        }

        /// 메시 피벗이 모델 원점이라 그대로 돌리면 큰 원을 그린다 — 바운드 중심에 빈 축을 끼운다(ProtoHeliCrash 와 같다).
        static Transform MakePivot(Transform heli, string mesh)
        {
            foreach (var r in heli.GetComponentsInChildren<Renderer>())
            {
                if (r.name != mesh) continue;
                var pivot = new GameObject(mesh + "_Spin").transform;
                pivot.SetParent(r.transform.parent, false);
                pivot.position = r.bounds.center;
                pivot.rotation = heli.rotation;
                r.transform.SetParent(pivot, true);
                return pivot;
            }
            return null;
        }

        /// 날개 포드 앞 끝(헬기 로컬). 못 찾으면 fallback(모델 5배 기준 로컬값을 그대로 쓴다).
        static Vector3 PodTip(Transform heli, string mesh, Vector3 fallback)
        {
            foreach (var r in heli.GetComponentsInChildren<Renderer>())
            {
                if (r.name != mesh) continue;
                var b = r.bounds;
                Vector3 f = heli.forward;
                float reach = Mathf.Abs(f.x) * b.extents.x + Mathf.Abs(f.y) * b.extents.y + Mathf.Abs(f.z) * b.extents.z;
                return heli.InverseTransformPoint(b.center + f * reach);
            }
            return fallback;
        }

        // ── 진행 ─────────────────────────────────────────────────────────
        void Update()
        {
            if (_t < 0f) return;
            TickVoice();
            var run = ProtoRun.I;
            if (run != null && run.state != RunState.Running) return;   // 레벨업·결과 중엔 멈춘다

            float dt = Time.deltaTime;
            _t += dt;
            float passTime = FlyHalf * 2f / Speed;
            bool anyLeft = false;

            foreach (var p in _passes)
            {
                if (p.done) continue;
                anyLeft = true;
                float lt = _t - p.t0;
                if (lt < 0f) continue;
                if (p.lane == null && p.heli == null) Arm(p);

                // 띠: 처음 0.25초에 나타나고, 경고 중엔 깜박이듯 숨쉬고, 헬기가 나간 뒤 0.4초에 사라진다
                float a;
                if (lt < 0.25f) a = lt / 0.25f;
                else if (lt < WarnTime) a = 0.75f + 0.25f * Mathf.Sin(lt * 14f);
                else a = 1f - Mathf.Clamp01((lt - WarnTime - passTime + 0.6f) / 0.4f);
                if (p.laneMat != null)
                {
                    var c = LaneColor; c.a *= a;
                    p.laneMat.SetColor(ProtoShaderIds._BaseColor, c);
                    // 화살표가 비행 방향으로 흐른다 — v 가 진행 방향이라 오프셋을 줄인다
                    p.laneMat.SetTextureOffset(ProtoShaderIds._BaseMap, new Vector2(0f, -lt * ChevronScroll / ChevronGap));
                }

                if (lt < WarnTime) continue;
                if (p.heli == null) { Launch(p); if (p.done) continue; }

                float ft = lt - WarnTime;
                float along = -FlyHalf + ft * Speed;   // 띠 중심 기준 헬기 위치
                p.heli.position = p.center + p.dir * along + Vector3.up * (Height + Mathf.Sin(ft * 3f) * 0.08f);
                p.heli.rotation = Quaternion.LookRotation(p.dir) * Quaternion.Euler(12f, 0f, Mathf.Sin(ft * 2.2f) * 3f);
                if (p.rotor != null) p.rotor.Rotate(0f, 1300f * dt, 0f, Space.Self);
                if (p.tail != null)  p.tail.Rotate(1800f * dt, 0f, 0f, Space.Self);

                if (Mathf.Abs(along) <= FireZone && _t >= _nextShot)
                {
                    _nextShot = _t + 1f / FireRate;
                    Fire(p, along);
                }

                if (ft >= passTime)
                {
                    Destroy(p.heli.gameObject); p.heli = null;
                    if (p.lane != null) { DestroyLaneMesh(p.lane); Destroy(p.lane.gameObject); Destroy(p.laneMat); p.lane = null; }
                    p.done = true;
                }
            }

            if (!anyLeft) { FadeVoice(); if (_voice == null) Clear(); }
        }

        // ── 사격 ─────────────────────────────────────────────────────────
        readonly List<ProtoZombie> _cands = new List<ProtoZombie>(32);

        void Fire(Pass p, float along)
        {
            bool left = (_shot++ & 1) == 0;
            Vector3 muzzle = p.heli.TransformPoint(left ? p.muzzleL : p.muzzleR);
            Vector3 side = Vector3.Cross(Vector3.up, p.dir);

            // 헬기 앞쪽 띠 위의 좀비 중 하나 — 없으면 띠 위 아무 데나(탄착 먼지만)
            _cands.Clear();
            var sd = ProtoSpawnDirector.I;
            if (sd != null)
            {
                var list = sd.Active;
                for (int i = 0; i < list.Count; i++)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 d = z.transform.position - p.center; d.y = 0f;
                    float a = Vector3.Dot(d, p.dir) - along;
                    if (a < AheadMin - 1f || a > AheadMax + 1f) continue;
                    if (Mathf.Abs(Vector3.Dot(d, side)) > LaneWidth * 0.5f + 0.3f) continue;
                    _cands.Add(z);
                }
            }
            Vector3 hit;
            ProtoZombie target = null;
            if (_cands.Count > 0)
            {
                target = _cands[Random.Range(0, _cands.Count)];
                hit = target.transform.position;
            }
            else
                hit = p.center + p.dir * (along + Random.Range(AheadMin, AheadMax)) + side * Random.Range(-LaneWidth * 0.5f, LaneWidth * 0.5f);
            hit.y = target != null ? 0.8f : 0.05f;
            Vector3 dir = (hit - muzzle).normalized;

            var run = ProtoRun.I;
            var pl = run != null ? run.player : null;
            bool crit = pl != null && Random.value < pl.critChance;
            float dmg = _dmg * (pl != null ? pl.damageMult : 1f) * (crit ? ProtoConfig.CritMultiplier : 1f);

            ProtoBeamFx.Play(muzzle, hit, Tracer, crit);
            // 총구·착탄 불똥은 무기 연출 세트(WFX_HeliStrike)대로 — 예전 상수 1.8 / 0.55 는 세트의 scale 로 옮겼다
            var set = _fx != null ? _fx : WeaponFxSet.Default;
            if (set.muzzle != null && set.muzzle.On) ProtoFx.Play(set.muzzle.fxId, muzzle, dir, set.muzzle.scale);
            // 좀비를 맞혔으면 TakeDamage 가 세트대로 착탄 불똥을 내고(HitFx), 빈 땅이면 여기서 위로 튄다
            if (target == null && set.hit != null && set.hit.On) ProtoFx.Play(set.hit.fxId, hit, Vector3.up, set.hit.scale);
            if ((_shot & 1) == 0) ProtoAudio.Play("rifle", hit, 0.6f);   // 16발/초를 다 울리면 총성이 다른 소리를 덮는다

            // 착탄점 주변 — 큰 한 방(데미지 숫자, GIF 처럼)
            Vector3 push = new Vector3(dir.x, 0f, dir.z).normalized;
            if (target != null) target.TakeDamage(dmg, push * 0.6f, crit, true, HitFx.From(set, hit));
            if (sd == null) return;
            var all = sd.Active;
            float rSq = HitRadius * HitRadius;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var z = all[i];
                if (z == null || z == target || !z.Alive) continue;
                Vector3 d = z.transform.position - hit; d.y = 0f;
                if (d.sqrMagnitude <= rSq) z.TakeDamage(dmg * 0.5f, push * 0.4f, false, false, HitFx.Blast(set));
            }
            ProtoDestructibles.ProjectileHit(new Vector3(hit.x, 0.8f, hit.z), 0.3f, dmg);   // 승합차도 맞는다
        }

        // ── 경고 띠 ──────────────────────────────────────────────────────
        Transform BuildLane(out Material mat)
        {
            var go = new GameObject("GunshipLane");
            go.transform.SetParent(transform, false);
            float hx = LaneWidth * 0.5f, hz = LaneHalf;
            var m = new Mesh { name = "GunshipLane" };
            m.vertices  = new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz) };
            float v = LaneHalf * 2f / ChevronGap;
            m.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, v), new Vector2(1, v) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            // 헬기 그림자와 같은 바닥 셰이더 — 좀비 발밑에 깔리고 몸통이 띠를 가린다
            var sh = Shader.Find("Proto/GroundShadow");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(sh) { name = "M_GunshipLane" };
            mat.SetTexture(ProtoShaderIds._BaseMap, ChevronTex());
            mat.SetColor(ProtoShaderIds._BaseColor, new Color(LaneColor.r, LaneColor.g, LaneColor.b, 0f));
            mr.sharedMaterial = mat;
            return go.transform;
        }

        /// 띠 한 칸: 양쪽 가장자리 선 + 진행 방향(+v)을 가리키는 ∨ 화살표, 바탕은 옅게. U 는 고정, V 는 반복.
        static Texture2D ChevronTex()
        {
            if (_chevronTex != null) return _chevronTex;
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "T_GunshipChevron", wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W, vv = (y + 0.5f) / H;
                    float a = 0.16f;                                             // 바탕
                    float edge = Mathf.Min(u, 1f - u);
                    if (edge < 0.05f) a = 0.95f;                                 // 가장자리 선
                    // ∨ : 끝(꼭짓점)이 위(+v)를 향한다. 팔은 가운데에서 바깥으로 갈수록 뒤(-v)로
                    float tip = 0.78f - Mathf.Abs(u - 0.5f) * 0.9f;
                    float d = tip - vv;
                    if (d >= 0f && d <= 0.2f && Mathf.Abs(u - 0.5f) < 0.42f) a = Mathf.Max(a, 0.9f);
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _chevronTex = tex;
            return tex;
        }

        // ── 소리: 도시 배경음의 헬기 통과음(14초)을 다시 쓴다 — 머리 위 지점(65%)을 첫 헬기가 띠 중심을 지날 때에 맞춘다 ──
        void StartVoice(float overheadAt)
        {
            var clip = Resources.Load<AudioClip>("Ambience/heli_" + Random.Range(1, 5));
            if (clip == null) clip = Resources.Load<AudioClip>("Ambience/heli_1");
            if (clip == null) return;
            var go = new GameObject("GunshipVoice");
            go.transform.SetParent(transform, false);
            _voice = go.AddComponent<AudioSource>();
            _voice.playOnAwake = false;
            _voice.spatialBlend = 0f;
            _voice.loop = true;   // 여러 번 지나가면 클립보다 길다
            _voice.clip = clip;
            _voice.volume = ProtoSettings.SfxVolume * 0.8f;
            _voice.time = Mathf.Clamp(clip.length * 0.65f - overheadAt, 0f, clip.length - 0.05f);
            _voice.Play();
            _voiceFade = -1f;
        }

        void FadeVoice() { if (_voice != null && _voiceFade < 0f) _voiceFade = 0f; }

        void TickVoice()
        {
            if (_voice == null) return;
            bool paused = Time.timeScale <= 0.0001f;
            if (paused) { if (_voice.isPlaying) _voice.Pause(); return; }
            if (!_voice.isPlaying) _voice.UnPause();
            if (_voiceFade < 0f) return;
            _voiceFade += Time.deltaTime / 0.8f;
            _voice.volume = ProtoSettings.SfxVolume * 0.8f * Mathf.Clamp01(1f - _voiceFade);
            if (_voiceFade >= 1f) { Destroy(_voice.gameObject); _voice = null; }
        }

        void Clear()
        {
            _t = -1f;
            foreach (var p in _passes)
            {
                if (p.heli != null) Destroy(p.heli.gameObject);
                if (p.lane != null) { DestroyLaneMesh(p.lane); Destroy(p.lane.gameObject); }
                if (p.laneMat != null) Destroy(p.laneMat);
            }
            _passes.Clear();
            if (_voice != null) Destroy(_voice.gameObject);
            _voice = null;
        }
    }
}
