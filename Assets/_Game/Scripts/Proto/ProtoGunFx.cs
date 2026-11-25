using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 총 연출 — 예광탄(총알), 총구 화염, 탄피 (사용자 요청 2026-10-13, 좀비 뱀서류 레퍼런스).
    /// 예전 총알은 노란 구였다. 구는 방향이 없어서 "날아간다"보다 "떠다닌다"로 읽혔다.
    ///
    /// 씬 배선이 없다 — 처음 부를 때 관리자가 생긴다(ProtoExplosion·ProtoBossFx 와 같은 방식).
    /// 셰이더: 예광탄 · 섬광은 이펙트 셰이더 하나(ProtoFxMat — ZombieOverrun/FX/Particle, 2026-11-23 · 옛 Sprites/Default), 탄피는 URP Unlit.
    /// 빛나는 것(예광탄·섬광)은 큐 3100 — 밤 어둠 막(2900) 뒤에 그려 어둠 속에서 빛나 보이게 한다.
    public class ProtoGunFx : MonoBehaviour
    {
        // ── 튜닝 ─────────────────────────────────────────────────────
        // 예광탄: 총알 크기(projectileSize, 소총 0.22) 기준 배율. 메시는 크기 1 일 때의 값으로 만든다.
        const float TracerLength = 1.25f / 0.22f;   // 소총에서 1.25m
        const float TracerWidth  = 0.242f / 0.22f;  // 소총에서 0.242m — 0.11 → 0.121(2026-10-13) → ×2 (2026-10-21 사용자: 바늘 같다)
        static readonly Color TracerColor = new Color(1f, 0.86f, 0.45f, 1f);

        const float FlashLife = 0.055f;             // 섬광 수명 — 연사 간격(캐논 0.08s)보다 짧아야 깜빡인다
        const int   MaxCasings = 80;                // 캐논은 초당 12발 — 넘치면 가장 오래된 것부터 재활용
        const float CasingRest = 0.25f;             // 땅에 떨어진 뒤 남아 있는 시간 — 1.4 → 0.6 → 0.25 (2026-10-13 사용자: 더 줄여 달라)
        const float CasingFade = 0.2f;              // 그 뒤 작아지며 사라지는 시간 (0.3 → 0.2)
        // 너무 밝다 → 30% 어둡게 (0.95, 0.70, 0.28) × 0.7 (2026-10-13 사용자)
        static readonly Color Brass = new Color(0.665f, 0.49f, 0.196f, 1f);
        // 원기둥 프리미티브(지름 1, 높이 2) 기준 — 지름 7cm, 길이 16cm. 실물보다 크다: 사선 카메라 15m 거리에서 보이는 하한
        static readonly Vector3 CasingSize = new Vector3(0.07f, 0.08f, 0.07f);

        static ProtoGunFx _i;
        static ProtoGunFx I
        {
            get
            {
                if (_i == null) { var go = new GameObject("_GunFx"); _i = go.AddComponent<ProtoGunFx>(); _i.Build(); }
                return _i;
            }
        }

        Mesh _tracerMesh, _flashMesh, _casingMesh;
        Material _tracerMat, _flashMat, _casingMat;

        /// 총알(기본 모델)용 예광탄 메시·머티리얼. ProtoShooter 가 풀을 만들 때 가져간다.
        public static Mesh TracerMesh => I._tracerMesh;
        public static Material TracerMaterial => I._tracerMat;

        // ── 섬광 ──────────────────────────────────────────────────
        struct Flash { public Transform tf; public float born, size; }
        readonly List<Flash> _flashes = new List<Flash>();
        readonly Stack<Transform> _flashPool = new Stack<Transform>();

        /// 총구 화염. dir = 총열 방향. scale 1 = 소총.
        public static void MuzzleFlash(Vector3 pos, Vector3 dir, float scale)
        {
            var i = I;
            var tf = i._flashPool.Count > 0 ? i._flashPool.Pop() : i.NewRenderer("Flash", i._flashMesh, i._flashMat);
            tf.gameObject.SetActive(true);
            tf.position = pos;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            // 진행 방향 축으로 무작위로 돌린다 — 매 발 같은 모양이면 도장 찍은 것처럼 보인다
            tf.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float s = scale * Random.Range(0.85f, 1.2f);
            tf.localScale = Vector3.one * s;
            i._flashes.Add(new Flash { tf = tf, born = Time.time, size = s });
        }

        // ── 탄피 ──────────────────────────────────────────────────
        struct Casing { public Transform tf; public Vector3 v, spin; public float landedAt; public bool landed; public float born; }
        readonly List<Casing> _casings = new List<Casing>();
        readonly Stack<Transform> _casingPool = new Stack<Transform>();

        /// 탄피 하나. port = 배출구 위치, right = 총의 오른쪽(배출 방향), scale 1 = 소총.
        public static void EjectCasing(Vector3 port, Vector3 right, Vector3 barrel, float scale)
        {
            var i = I;
            Transform tf;
            if (i._casingPool.Count > 0) tf = i._casingPool.Pop();
            else if (i._casings.Count >= MaxCasings)
            {
                tf = i._casings[0].tf;          // 가장 오래된 것을 다시 쓴다
                i._casings.RemoveAt(0);
            }
            else tf = i.NewRenderer("Casing", i._casingMesh, i._casingMat);
            tf.gameObject.SetActive(true);
            tf.position = port;
            tf.rotation = Quaternion.LookRotation(right) * Quaternion.Euler(90f, 0f, 0f);
            tf.localScale = CasingSize * scale;
            right.y = 0f; right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
            barrel.y = 0f; barrel = barrel.sqrMagnitude > 0.0001f ? barrel.normalized : Vector3.forward;
            // 오른쪽 위로 튀고, 살짝 뒤로 — 실제 소총 배출 방향
            Vector3 v = right * Random.Range(1.6f, 2.6f) + Vector3.up * Random.Range(2.2f, 3.2f) - barrel * Random.Range(0.2f, 0.7f);
            i._casings.Add(new Casing { tf = tf, v = v, spin = new Vector3(Random.Range(-900f, 900f), Random.Range(-300f, 300f), Random.Range(-900f, 900f)), born = Time.time });
        }

        // ───────────────────────────────────────────────────────────
        void Build()
        {
            var unlit  = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

            // 예광탄 · 섬광 = 이펙트 셰이더 (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23). 탄피는 물체라 URP Unlit 그대로.
            _tracerMat = ProtoFxMat.New("Tracer", MakeTracerTex(), 3100, TracerColor);
            _flashMat  = ProtoFxMat.New("MuzzleFlash", MakeFlashTex(), 3101, new Color(1f, 0.85f, 0.5f, 1f));
            _casingMat = new Material(unlit) { renderQueue = 3050 };   // 어둠 막 뒤 — 작은 놋쇠 조각이 밤에도 보이게
            if (_casingMat.HasProperty("_BaseColor")) _casingMat.SetColor("_BaseColor", Brass);
            _casingMat.color = Brass;

            _tracerMesh = BuildCrossQuad(TracerWidth, TracerLength, tailAtOrigin: false);
            _flashMesh  = BuildFlashMesh();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _casingMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }

        Transform NewRenderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (name == "Casing") go.transform.localScale = Vector3.one;
            return go.transform;
        }

        void Update()
        {
            float now = Time.time;
            float dt = ProtoConfig.Delta;

            for (int k = _flashes.Count - 1; k >= 0; k--)
            {
                var f = _flashes[k];
                float t = (now - f.born) / FlashLife;
                if (t >= 1f) { f.tf.gameObject.SetActive(false); _flashPool.Push(f.tf); _flashes.RemoveAt(k); continue; }
                // 터지자마자 가장 크고 빠르게 줄어든다
                f.tf.localScale = Vector3.one * f.size * (1.15f - 0.6f * t);
            }

            for (int k = _casings.Count - 1; k >= 0; k--)
            {
                var c = _casings[k];
                if (!c.landed)
                {
                    c.v += Vector3.down * 18f * dt;
                    Vector3 p = c.tf.position + c.v * dt;
                    c.tf.Rotate(c.spin * dt, Space.Self);
                    const float floor = 0.02f;
                    if (p.y <= floor)
                    {
                        p.y = floor;
                        if (Mathf.Abs(c.v.y) > 1.2f)
                        {
                            // 한두 번 튕긴다 — 금속이 바닥에 떨어지는 느낌
                            c.v = new Vector3(c.v.x * 0.45f, -c.v.y * 0.35f, c.v.z * 0.45f);
                            c.spin *= 0.5f;
                        }
                        else
                        {
                            c.landed = true; c.landedAt = now;
                            // 눕힌다 — 원기둥 축(로컬 Y)을 수평으로
                            Vector3 lie = new Vector3(c.v.x, 0f, c.v.z);
                            if (lie.sqrMagnitude < 0.0001f) lie = Random.insideUnitSphere; lie.y = 0f;
                            c.tf.rotation = Quaternion.LookRotation(lie.normalized) * Quaternion.Euler(90f, 0f, 0f);
                        }
                    }
                    c.tf.position = p;
                }
                else
                {
                    float age = now - c.landedAt;
                    if (age >= CasingRest)
                    {
                        float k2 = 1f - (age - CasingRest) / CasingFade;
                        if (k2 <= 0f) { c.tf.gameObject.SetActive(false); _casingPool.Push(c.tf); _casings.RemoveAt(k); continue; }
                        c.tf.localScale *= Mathf.Lerp(0.8f, 1f, k2);
                    }
                }
                _casings[k] = c;
            }
        }

        void OnDestroy() { if (_i == this) _i = null; }

        // ── 메시 · 텍스처 ─────────────────────────────────────────
        /// 진행 방향(+Z)으로 긴 십자 판 두 장 — 수평 판은 위에서, 수직 판은 비스듬한 카메라에서 보인다.
        /// 머리가 원점(z=0), 꼬리가 -Z. 총알 위치가 곧 탄두라 꼬리가 뒤로 끌린다.
        static Mesh BuildCrossQuad(float w, float len, bool tailAtOrigin)
        {
            float z0 = -len, z1 = 0f, h = w * 0.5f;
            var m = new Mesh { name = "TracerCross" };
            m.vertices = new[]
            {
                new Vector3(-h, 0, z0), new Vector3(h, 0, z0), new Vector3(h, 0, z1), new Vector3(-h, 0, z1),   // 수평
                new Vector3(0, -h, z0), new Vector3(0, h, z0), new Vector3(0, h, z1), new Vector3(0, -h, z1),   // 수직
            };
            m.uv = new[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 };
            m.RecalculateBounds();
            return m;
        }

        /// 총구 섬광: 정면을 보는 별(카메라에서 보임) + 앞으로 뻗는 십자 불꽃 두 장.
        static Mesh BuildFlashMesh()
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
            {
                int o = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                tri.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
            float r = 0.32f;   // 소총 기준 별 반지름 m
            // 별(정면) — 텍스처 왼쪽 절반
            Quad(new Vector3(-r, -r, 0), new Vector3(r, -r, 0), new Vector3(r, r, 0), new Vector3(-r, r, 0),
                 new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, 1));
            // 앞으로 뻗는 불꽃(수평·수직) — 텍스처 오른쪽 절반, 길이 0.75m
            float len = 0.75f, w = 0.24f;
            Quad(new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, 0, len), new Vector3(-w, 0, len),
                 new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0.5f, 1));
            Quad(new Vector3(0, -w, 0), new Vector3(0, w, 0), new Vector3(0, w, len), new Vector3(0, -w, len),
                 new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0.5f, 1));
            var m = new Mesh { name = "MuzzleFlash" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// 가로 = 폭, 세로 = 길이(0 꼬리 → 1 머리). 머리는 하얗게 뜨겁고 꼬리로 갈수록 투명.
        static Texture2D MakeTracerTex()
        {
            int W = 16, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f, v = (y + 0.5f) / H;
                    float across = Mathf.Clamp01(1f - Mathf.Abs(u));
                    across = across * across;
                    float along = Mathf.Pow(v, 1.6f);                    // 꼬리 → 머리로 진해짐
                    float head = Mathf.Clamp01((v - 0.88f) / 0.12f);     // 머리 끝 하얀 점
                    float a = Mathf.Clamp01(across * along * 1.3f);
                    Color c = Color.Lerp(new Color(1f, 0.75f, 0.35f), Color.white, head * across);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        /// 왼쪽 절반: 6갈래 별 / 오른쪽 절반: 앞으로 뻗는 불꽃(뿌리가 진하고 끝이 뾰족).
        static Texture2D MakeFlashTex()
        {
            int H = 64, W = 128;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c;
                    if (x < 64)
                    {
                        float dx = (x + 0.5f) / 64f * 2f - 1f, dy = (y + 0.5f) / 64f * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float ang = Mathf.Atan2(dy, dx);
                        float spike = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3f)), 8f);   // 6갈래
                        float a = Mathf.Clamp01(1f - r / (0.35f + 0.65f * spike));
                        float core = Mathf.Clamp01(1f - r / 0.3f);
                        c = Color.Lerp(new Color(1f, 0.6f, 0.15f), Color.white, core);
                        c.a = Mathf.Clamp01(a * 1.4f);
                    }
                    else
                    {
                        float u = (x - 64 + 0.5f) / 64f * 2f - 1f, v = (y + 0.5f) / 64f;   // v: 0 뿌리 → 1 끝
                        float width = Mathf.Lerp(1f, 0.05f, v);
                        float a = Mathf.Clamp01(1f - Mathf.Abs(u) / width) * (1f - v);
                        c = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.1f), v);
                        c.a = a;
                    }
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }
    }
}
