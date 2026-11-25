using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 최종 보스(망치) 연출. 사용자가 준 콘티 4컷을 따른다 (2026-10-13):
    ///  1 준비   — 망치를 치켜들며 불이 붙고, 발밑에 먼지.            → WindupDust · HammerFlames
    ///  2 휘두르기 — 내려치는 궤적에 불꽃 띠.                          → HammerTrail
    ///  3 충돌   — 섬광·화구·충격파 링, 파편이 사방으로, 카메라 흔들림.  → Impact (ProtoExplosion + 파편 + 불씨)
    ///  4 여운   — 갈라진 땅에서 빛이 식어 가고 연기가 오른다.          → Impact 의 균열·연기
    /// 여기에 기획서 §08 규칙(예고 → 실행)의 **바닥 예고 원**을 더했다.
    ///
    /// 씬 배선이 없다 — 처음 부를 때 관리자가 스스로 생긴다(ProtoExplosion 과 같은 방식).
    /// 셰이더: 링 · 균열 · 궤적은 이펙트 셰이더 하나(ProtoFxMat — ZombieOverrun/FX/Particle, 2026-11-23 · 옛 Sprites/Default), 파편은 URP Unlit(ShaderRefs 에 등록됨).
    /// 땅을 **어둡게 남기는 자국은 넣지 않는다** — 로켓 그을음을 같은 이유로 뺐다(ProtoExplosion 주석).
    /// 균열은 빛나다가 완전히 사라진다.
    public class ProtoBossFx : MonoBehaviour
    {
        // 밤 어둠 막(큐 2900)보다 뒤에 그려야 어둠에 안 묻힌다
        const int QueueGround = 3040;

        static ProtoBossFx _i;
        static ProtoBossFx I
        {
            get
            {
                if (_i == null) { var go = new GameObject("_BossFx"); _i = go.AddComponent<ProtoBossFx>(); _i.Build(); }
                return _i;
            }
        }

        Material _ringMat, _fillMat, _crackMatSrc, _debrisMat, _trailMat;
        Texture2D _ringTex, _discTex, _crackTex;
        Mesh _quad, _cube;
        GameObject _landingPrefab;
        ParticleSystem _fire;
        ParticleSystem.EmitParams _ep;
        float _fireCarry;

        // ── 예고 원 ────────────────────────────────────────────────
        public class Telegraph
        {
            internal GameObject ring, fill;
            internal FxHandle art;   // 카탈로그 boss_telegraph 프리팹(있으면 코드 원 위에 얹는다)
            internal float radius, dur, t;
            internal bool dead;
            public void Cancel() { dead = true; }
        }
        readonly List<Telegraph> _teles = new List<Telegraph>();

        /// 바닥 예고 원. 바깥 테두리 = 피해 범위, 안쪽 채움이 커지다 가득 차는 순간이 충돌이다.
        public static Telegraph ShowTelegraph(Vector3 pos, float radius, float duration)
        {
            var i = I;
            // 채움이 가득 차는 순간 = 충돌 순간이어야 한다. 예전엔 최소 0.8s 로 늘렸는데, 공격이 빨라지면(attackSpeed)
            // 원이 다 차기 전에 맞아서 예고가 거짓말이 됐다 — 실제 충돌 시각에 맞춘다.
            var tl = new Telegraph { radius = radius, dur = Mathf.Max(0.2f, duration) };
            tl.ring = i.GroundQuad("TeleRing", pos + Vector3.up * 0.05f, radius * 2f, i._ringMat);
            tl.fill = i.GroundQuad("TeleFill", pos + Vector3.up * 0.04f, 0.01f, i._fillMat);
            // 프리팹 강조(있을 때만) — 코드 테두리(= 판정)와 차오르는 채움(= 충돌 시각)은 그대로 두고 위에 얹는다. scale = 판정 반경
            if (ProtoFx.HasPrefab(FxIds.BossTelegraph))
            {
                var c = FxContext.At(pos);
                c.scale = radius;
                tl.art = ProtoFx.Play(FxIds.BossTelegraph, c, span: true);
            }
            i._teles.Add(tl);
            return tl;
        }

        // ── 1컷: 발밑 먼지 ─────────────────────────────────────────
        public static void WindupDust(Vector3 feet, float scale) => I.Landing(feet, 0.9f * scale, 0f);

        // ── 1컷: 망치에 불 ─────────────────────────────────────────
        public static void HammerFlames(Transform head, float dt)
        {
            var i = I;
            if (head == null || i._fire == null) return;
            i._fireCarry += 70f * dt;
            int n = (int)i._fireCarry; i._fireCarry -= n;
            float s = head.lossyScale.x;
            for (int k = 0; k < n; k++)
            {
                i._ep.position = head.position + Random.insideUnitSphere * 0.55f * s / 0.57f;
                i._ep.velocity = Vector3.up * Random.Range(0.6f, 1.6f);
                i._ep.startSize = Random.Range(0.45f, 0.85f);
                i._fire.Emit(i._ep, 1);
            }
        }

        // ── 2컷: 휘두르는 궤적 ─────────────────────────────────────
        public static void HammerTrail(Transform head, bool on)
        {
            if (head == null) return;
            var tr = head.GetComponent<TrailRenderer>();
            if (tr == null)
            {
                if (!on) return;
                tr = head.gameObject.AddComponent<TrailRenderer>();
                tr.sharedMaterial = I._trailMat;
                tr.time = 0.22f;
                tr.minVertexDistance = 0.08f;
                tr.widthMultiplier = 1.1f;
                tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.35f), new GradientColorKey(new Color(0.8f, 0.15f, 0.05f), 1f) },
                    new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = g;
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tr.receiveShadows = false;
            }
            if (on && !tr.emitting) tr.Clear();
            tr.emitting = on;
        }

        // ── 3·4컷: 충돌 ────────────────────────────────────────────
        public static void Impact(Vector3 pos, float radius, bool big)
        {
            var i = I;
            pos.y = 0f;
            ProtoExplosion.Play(pos, radius * 0.85f);                 // 섬광 → 화구 → 충격파 링 → 연기
            i.Landing(pos, radius * 0.75f, 0f);                        // 먼지 고리 + 잔돌
            i.Debris(pos, radius, big ? 22 : 14);                       // 파편이 사방으로
            i.Embers(pos, radius, big ? 60 : 35);                       // 불씨
            i.Cracks(pos, radius * (big ? 1.0f : 0.85f));               // 갈라진 땅이 빛나다 식는다
            for (int k = 0; k < (big ? 3 : 2); k++)                     // 4컷의 피어오르는 연기 — 균열 끝에서 늦게
            {
                Vector3 off = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * radius * Random.Range(0.4f, 0.8f);
                i.Landing(pos + off, radius * 0.45f, 0.25f + 0.15f * k);
            }
            ProtoCameraFollow.Shake(big ? ProtoCameraFollow.Impact.Huge : ProtoCameraFollow.Impact.Heavy);   // 도약·승합차 폭발 / 내려찍기
            ProtoAudio.Play("explosion", pos, big ? 1.4f : 1.15f);
            ProtoAudio.Play("land", pos, 1.2f);
        }

        /// <summary>
        /// 충돌의 그림 말고 나머지만 — 풀 눕힘 · 흔들림 · 폭발음 · 쿵 소리. <see cref="Impact"/> 와 같은 값이다(하나를 바꾸면 같이).
        /// 그림을 프리팹이 대신하는 탈것 폭발(승합차 · 헬기 추락 — <see cref="VehicleBlastFx"/>, 2026-11-13)이 부른다.
        /// </summary>
        public static void ImpactFeel(Vector3 pos, float radius, bool big)
        {
            pos.y = 0f;
            ProtoExplosion.Aftermath(pos, radius * 0.85f);   // Impact 안의 ProtoExplosion.Play 몫(풀 · 폭발음)
            ProtoCameraFollow.Shake(big ? ProtoCameraFollow.Impact.Huge : ProtoCameraFollow.Impact.Heavy);
            ProtoAudio.Play("explosion", pos, big ? 1.4f : 1.15f);
            ProtoAudio.Play("land", pos, 1.2f);
        }

        // ───────────────────────────────────────────────────────────
        void Build()
        {
            var unlit  = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");   // 파편(물체)만

            _ringTex  = MakeRadialTex(128, ring: true);
            _discTex  = MakeRadialTex(128, ring: false);
            _crackTex = MakeCrackTex(256);

            // 예고 링 · 채움 · 균열 · 망치 궤적 = 이펙트 셰이더 (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            _ringMat = ProtoFxMat.New("BossRing", _ringTex, QueueGround + 1, new Color(1f, 0.25f, 0.12f, 0.95f));
            _fillMat = ProtoFxMat.New("BossFill", _discTex, QueueGround, new Color(1f, 0.2f, 0.08f, 0.45f));
            _crackMatSrc = ProtoFxMat.New("BossCrack", _crackTex, QueueGround);
            _trailMat = ProtoFxMat.New("BossHammerTrail", null, 3100);
            _debrisMat = new Material(unlit);
            if (_debrisMat.HasProperty(ProtoShaderIds._BaseColor)) _debrisMat.SetColor(ProtoShaderIds._BaseColor, Color.white);
            _debrisMat.enableInstancing = true;

            _quad = BuildGroundQuad();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);

            _landingPrefab = Resources.Load<GameObject>("VFX/FX_SwapLanding");

            var fire = Resources.Load<GameObject>(ProtoBurnFx.FxPath);
            if (fire != null)
            {
                var go = Instantiate(fire, transform);
                go.name = "HammerFire";
                _fire = go.GetComponentInChildren<ParticleSystem>(true);
                if (_fire != null) { _fire.Play(); }
            }
            _ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        }

        GameObject GroundQuad(string name, Vector3 pos, float size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size, 1f, size);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void Landing(Vector3 pos, float scale, float delay)
        {
            if (_landingPrefab == null) return;
            _pendingDust.Add(new Pending { pos = pos, scale = scale, at = Time.time + delay });
        }
        struct Pending { public Vector3 pos; public float scale, at; }
        readonly List<Pending> _pendingDust = new List<Pending>();

        void SpawnLanding(Vector3 pos, float scale)
        {
            var go = Instantiate(_landingPrefab, pos, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * scale;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
                ps.Play();
            }
            Destroy(go, 2.5f);
        }

        // ── 파편 ──────────────────────────────────────────────────
        struct Chunk { public Transform tf; public Vector3 v, spin; public float born, life; }
        readonly List<Chunk> _chunks = new List<Chunk>();
        readonly Stack<Transform> _chunkPool = new Stack<Transform>();
        MaterialPropertyBlock _mpb;

        void Debris(Vector3 pos, float radius, int count)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int k = 0; k < count; k++)
            {
                Transform tf = _chunkPool.Count > 0 ? _chunkPool.Pop() : NewChunk();
                tf.gameObject.SetActive(true);
                float a = Random.Range(0f, Mathf.PI * 2f);
                Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                tf.position = pos + outDir * Random.Range(0.1f, 0.5f) + Vector3.up * 0.2f;
                tf.rotation = Random.rotation;
                float s = Random.Range(0.12f, 0.34f) * Mathf.Clamp(radius / 3f, 0.7f, 1.4f);
                tf.localScale = new Vector3(s, s * Random.Range(0.5f, 0.9f), s * Random.Range(0.7f, 1.2f));
                // 대부분은 돌 색, 일부는 달아오른 조각(콘티 3컷)
                bool hot = Random.value < 0.3f;
                Color c = hot ? new Color(1f, Random.Range(0.35f, 0.6f), 0.1f) : Color.Lerp(new Color(0.18f, 0.16f, 0.15f), new Color(0.36f, 0.32f, 0.28f), Random.value);
                _mpb.SetColor(ProtoShaderIds._BaseColor, c); _mpb.SetColor(ProtoShaderIds._Color, c);
                tf.GetComponent<MeshRenderer>().SetPropertyBlock(_mpb);
                Vector3 v = outDir * Random.Range(2.5f, 6.5f) * Mathf.Clamp(radius / 3f, 0.8f, 1.3f) + Vector3.up * Random.Range(4f, 8.5f);
                _chunks.Add(new Chunk { tf = tf, v = v, spin = Random.insideUnitSphere * 720f, born = Time.time, life = Random.Range(0.9f, 1.4f) });
            }
        }

        Transform NewChunk()
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _cube;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _debrisMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        // ── 불씨 ──────────────────────────────────────────────────
        void Embers(Vector3 pos, float radius, int count)
        {
            if (_fire == null) return;
            for (int k = 0; k < count; k++)
            {
                Vector3 d = Random.insideUnitSphere; d.y = Mathf.Abs(d.y) * 1.6f + 0.3f;
                _ep.position = pos + new Vector3(d.x, 0.2f, d.z) * radius * 0.3f;
                _ep.velocity = d.normalized * Random.Range(3f, 9f);
                _ep.startSize = Random.Range(0.3f, 0.9f);
                _fire.Emit(_ep, 1);
            }
        }

        // ── 균열 ──────────────────────────────────────────────────
        struct Crack { public GameObject go; public Material mat; public float born; }
        readonly List<Crack> _cracks = new List<Crack>();
        const float CrackLife = 3.2f;

        void Cracks(Vector3 pos, float radius)
        {
            var mat = new Material(_crackMatSrc);
            var go = GroundQuad("Crack", pos + Vector3.up * 0.03f, radius * 2.2f, mat);
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            _cracks.Add(new Crack { go = go, mat = mat, born = Time.time });
        }

        void Update()
        {
            float dt = ProtoConfig.Delta;
            float now = Time.time;

            for (int k = _pendingDust.Count - 1; k >= 0; k--)
                if (now >= _pendingDust[k].at) { SpawnLanding(_pendingDust[k].pos, _pendingDust[k].scale); _pendingDust.RemoveAt(k); }

            // 예고 원 — 채움이 0 → 반경까지 자라고, 끝나면 사라진다
            for (int k = _teles.Count - 1; k >= 0; k--)
            {
                var tl = _teles[k];
                tl.t += dt;
                if (tl.dead || tl.t > tl.dur + 0.3f)
                {
                    Destroy(tl.ring); Destroy(tl.fill); tl.art?.Stop(); _teles.RemoveAt(k); continue;
                }
                float f = Mathf.Clamp01(tl.t / tl.dur);
                float d = tl.radius * 2f * f;
                tl.fill.transform.localScale = new Vector3(d, 1f, d);
                // 끝에 가까울수록 테두리가 빠르게 깜빡인다 — "곧 떨어진다"
                float blink = f > 0.7f ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(now * 22f)) : 1f;
                tl.ring.GetComponent<MeshRenderer>().sharedMaterial.color = new Color(1f, 0.25f, 0.12f, 0.95f * blink);
            }

            // 파편 — 포물선, 땅에 닿으면 미끄러지다 작아져 사라진다
            for (int k = _chunks.Count - 1; k >= 0; k--)
            {
                var c = _chunks[k];
                float age = now - c.born;
                c.v += Vector3.down * 22f * dt;
                Vector3 p = c.tf.position + c.v * dt;
                if (p.y < 0.05f) { p.y = 0.05f; c.v = new Vector3(c.v.x * 0.5f, Mathf.Abs(c.v.y) * 0.25f, c.v.z * 0.5f); c.spin *= 0.5f; }
                c.tf.position = p;
                c.tf.Rotate(c.spin * dt, Space.World);
                float shrink = Mathf.Clamp01((c.life - age) / 0.3f);
                if (age >= c.life) { c.tf.gameObject.SetActive(false); _chunkPool.Push(c.tf); _chunks.RemoveAt(k); continue; }
                if (shrink < 1f) c.tf.localScale *= Mathf.Lerp(0.85f, 1f, shrink);
                _chunks[k] = c;
            }

            // 균열 — 흰 열기 → 주황 → 붉게 식으며 투명해진다
            for (int k = _cracks.Count - 1; k >= 0; k--)
            {
                var c = _cracks[k];
                float t = (now - c.born) / CrackLife;
                if (t >= 1f) { Destroy(c.go); Destroy(c.mat); _cracks.RemoveAt(k); continue; }
                Color hot = new Color(1f, 0.9f, 0.55f), warm = new Color(1f, 0.45f, 0.08f), cold = new Color(0.55f, 0.08f, 0.02f);
                Color col = t < 0.15f ? Color.Lerp(hot, warm, t / 0.15f) : Color.Lerp(warm, cold, (t - 0.15f) / 0.85f);
                col.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                c.mat.color = col;
            }
        }

        void OnDestroy() { if (_i == this) _i = null; }

        // ── 절차 생성 ─────────────────────────────────────────────
        static Mesh BuildGroundQuad()
        {
            var m = new Mesh { name = "BossFxGroundQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(-0.5f, 0, 0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        /// ring=true: 가장자리 테두리 / false: 가장자리로 갈수록 진해지는 원판
        static Texture2D MakeRadialTex(int n, bool ring)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (r > 1f) a = 0f;
                    else if (ring) a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / 0.05f);
                    else a = Mathf.Lerp(0.35f, 1f, r * r) * Mathf.Clamp01((1f - r) / 0.03f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        /// 중심에서 뻗는 지그재그 균열 + 곁가지. 중심부는 빛이 뭉쳐 있다.
        static Texture2D MakeCrackTex(int n)
        {
            var px = new float[n * n];
            var rnd = new System.Random(7);
            // 굵고 짧은 줄기 7개 + 곁가지 적게 — 가늘고 많으면 번개처럼 읽힌다(2026-10-13 확인)
            int arms = 7;
            for (int a = 0; a < arms; a++)
            {
                float ang = a / (float)arms * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.6f;
                DrawCrack(px, n, 0.5f, 0.5f, ang, 0.30f + (float)rnd.NextDouble() * 0.12f, 5.5f, rnd, 1);
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / 0.16f);
                    float a = Mathf.Clamp01(px[y * n + x] + core * core * 0.8f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        static void DrawCrack(float[] px, int n, float x, float y, float ang, float len, float width, System.Random rnd, int depth)
        {
            int steps = 14;
            float seg = len / steps;
            for (int s = 0; s < steps; s++)
            {
                ang += ((float)rnd.NextDouble() - 0.5f) * 0.45f;
                float nx = x + Mathf.Cos(ang) * seg, ny = y + Mathf.Sin(ang) * seg;
                float w = width * (1f - s / (float)steps * 0.75f);
                Line(px, n, x, y, nx, ny, w);
                if (depth > 0 && rnd.NextDouble() < 0.18)
                    DrawCrack(px, n, nx, ny, ang + ((float)rnd.NextDouble() > 0.5 ? 0.9f : -0.9f), len * 0.35f, w * 0.7f, rnd, depth - 1);
                x = nx; y = ny;
            }
        }

        static void Line(float[] px, int n, float x0, float y0, float x1, float y1, float w)
        {
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0)) * n) + 1;
            int r = Mathf.CeilToInt(w);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t) * n), cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t) * n);
                for (int oy = -r; oy <= r; oy++)
                    for (int ox = -r; ox <= r; ox++)
                    {
                        int xx = cx + ox, yy = cy + oy;
                        if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                        float d = Mathf.Sqrt(ox * ox + oy * oy);
                        float a = Mathf.Clamp01(1f - d / (w + 0.5f));
                        int idx = yy * n + xx;
                        if (a > px[idx]) px[idx] = a;
                    }
            }
        }
    }
}
