using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 엘리트 · 중간 보스 발밑 빨간 원 + 보스 머리 높이(머리 위 체력 막대 자리).
    /// 11-17 사용자 "보스 발 밑에 빨간색 동그라미 표시 해주고 머리 위에 hp 바도 추가해줘"
    ///   → 같은 날 "마지막 보스에는 바닥 표시 빼주고, 엘리트랑 중간 보스만 넣어줘" — 최종 보스는 부르는 쪽(ProtoSpawnDirector)이 넘기지 않는다.
    ///  - 원: 바닥에 눕힌 사각형 1장 + 코드로 그린 256 텍스처(남색 테 · 굵은 빨간 띠 · 바깥 빛 · 안쪽 옅은 빨강 · 안쪽을 가리키는 쐐기 4개).
    ///    숨 쉬듯 살짝 커졌다 작아지고 쐐기가 천천히 돈다. 크기 = 몸 반경 × 1.8 + 0.25 (중간 보스 반경 약 1.2m).
    ///    칸 두 개(Boss · Elite) — 중간 보스와 엘리트가 같이 있어도 각자 원이 있다.
    ///    밤 어둠 막(큐 2900)보다 뒤(3035)에 그려 어둠에 안 묻히고, 깊이 검사는 해서 다리가 원을 가린다.
    ///    보스 공격 예고 원(ProtoBossFx — 주황빛 테 + 차오르는 채움, 큐 3040~3041)과 헷갈리지 않게 모양을 다르게 했다.
    ///  - 머리 높이: 렌더러(스킨 · 메시) 바운즈 윗면 − 발 높이를 보스마다 한 번 재 둔다. ProtoHUD 가 이 점에 머리 위 체력 막대를 띄운다.
    /// 씬 배선 없음 — ProtoSpawnDirector 가 매 프레임 Track(칸, 대상) 을 부르면 관리자가 스스로 생긴다.
    /// (다른 게임 코드에 기대지 않는다 — UI 임시 프로젝트 미리보기에서도 그대로 쓴다)
    public class ProtoBossMarker : MonoBehaviour
    {
        const int Queue = 3035;
        const float BaseBodyRadius = 0.28f;   // ProtoConfig.BaseBodyRadius 와 같은 값(머리 높이를 못 잴 때 덩치 배율)
        public const float RingScale = 1.8f, RingPad = 0.25f;
        public const int Boss = 0, Elite = 1;

        static ProtoBossMarker _i;
        static readonly Dictionary<int, float> _heights = new Dictionary<int, float>();

        class Slot { public Transform target; public float radius, t; public GameObject ring; public Material mat; }
        readonly Slot[] _slots = new Slot[2];

        /// 칸(Boss · Elite)마다 대상을 따라 발밑 원을 그린다. null 이면 그 칸 숨김. 매 프레임 불러도 된다.
        public static void Track(int slot, Transform target, float bodyRadius)
        {
            if (target == null) { if (_i != null) _i.SetTarget(slot, null, 0f); return; }
            if (_i == null)
            {
                var go = new GameObject("_BossMarker");
                _i = go.AddComponent<ProtoBossMarker>();
                _i.Build();
            }
            _i.SetTarget(slot, target, bodyRadius);
        }
        public static void Track(Transform boss, float bodyRadius) => Track(Boss, boss, bodyRadius);

        /// 원 오브젝트(미리보기 · 검사용). 아직 안 만들어졌으면 null.
        public static GameObject RingOf(int slot) => _i != null && slot >= 0 && slot < _i._slots.Length ? _i._slots[slot].ring : null;
        public static GameObject Ring => RingOf(Boss);

        /// 보스 머리 위 한 뼘(월드 좌표). 높이는 보스마다 처음 잴 수 있을 때 한 번 잰다.
        public static Vector3 HeadOf(Transform boss, float bodyRadius)
        {
            if (boss == null) return Vector3.zero;
            int id = boss.GetInstanceID();
            if (!_heights.TryGetValue(id, out float h))
            {
                h = MeasureHeight(boss);
                if (h > 0.2f) _heights[id] = h;                                          // 잰 값만 기억(숨겨져 있던 프레임 값은 버림)
                else h = 1.9f * Mathf.Max(1f, bodyRadius / BaseBodyRadius);              // 못 재면 피해 숫자와 같은 높이
            }
            return boss.position + Vector3.up * (h + 0.2f);
        }

        static float MeasureHeight(Transform body)
        {
            float top = float.NegativeInfinity;
            foreach (var r in body.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!(r is SkinnedMeshRenderer || r is MeshRenderer)) continue;   // 파티클 · 선은 빼고 몸만
                top = Mathf.Max(top, r.bounds.max.y);
            }
            float h = top - body.position.y;
            return h > 0.2f && h < 20f ? h : 0f;
        }

        // ───────────────────────────────────────────────────────────
        void SetTarget(int slot, Transform t, float r)
        {
            if (slot < 0 || slot >= _slots.Length) return;
            var s = _slots[slot];
            if (t != s.target) s.t = 0f;
            s.target = t; s.radius = r;
            bool on = t != null;
            if (s.ring.activeSelf != on) s.ring.SetActive(on);
        }

        void LateUpdate()
        {
            foreach (var s in _slots)
            {
                if (s.target == null || !s.target.gameObject.activeInHierarchy)
                {
                    if (s.ring.activeSelf) s.ring.SetActive(false);
                    s.target = null;
                    continue;
                }
                s.t += Time.deltaTime;
                Place(s.ring.transform, s.target.position, s.radius, s.t);
                float a = Mathf.Clamp01(s.t / 0.35f) * (0.88f + 0.12f * Mathf.Sin(s.t * 4f));
                s.mat.color = new Color(1f, 1f, 1f, a);
            }
        }

        /// 원 자리 · 크기 · 회전(쐐기가 천천히 돈다). 미리보기도 같은 함수를 쓴다.
        public static void Place(Transform ring, Vector3 feet, float bodyRadius, float t)
        {
            float d = (Mathf.Max(0.3f, bodyRadius) * RingScale + RingPad) * 2f * (1f + 0.04f * Mathf.Sin(t * 4f));
            ring.position = feet + Vector3.up * 0.035f;
            ring.rotation = Quaternion.Euler(0f, t * 25f, 0f);
            ring.localScale = new Vector3(d, 1f, d);
        }

        void Build()
        {
            var tex = MakeTex(256);
            var quad = BuildQuad();
            for (int k = 0; k < _slots.Length; k++)
            {
                var s = new Slot { mat = ProtoFxMat.New(k == Boss ? "BossMarker" : "EliteMarker", tex, Queue, Color.white) };   // (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
                s.ring = new GameObject(k == Boss ? "BossRing" : "EliteRing");
                s.ring.transform.SetParent(transform, false);
                s.ring.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = s.ring.AddComponent<MeshRenderer>();
                mr.sharedMaterial = s.mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                s.ring.SetActive(false);
                _slots[k] = s;
            }
        }

        static Mesh BuildQuad()
        {
            var m = new Mesh { name = "BossMarkerQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.RecalculateBounds();
            return m;
        }

        // 원 그림 — r = 가운데에서의 거리(0~1). 바깥부터: 빛(0.90~1) · 남색 테(0.87~0.90) · 빨간 띠(0.73~0.87) · 남색 테(0.70~0.73) ·
        // 안쪽 옅은 빨강(~0.70) + 안쪽을 가리키는 쐐기 4개(0.52~0.70). 탕탕 판 HUD 와 같은 굵은 남색 외곽선 그림체.
        public static Texture2D MakeTex(int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "BossMarkerRing", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            float aa = 1.5f / (n * 0.5f);
            var ink = new Color(0.30f, 0.04f, 0.10f);
            var red = new Color(1f, 0.20f, 0.22f);
            var hot = new Color(1f, 0.52f, 0.46f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);
                    var c = new Color(red.r, red.g, red.b, 0f);
                    // 안쪽 옅은 빨강(가장자리로 갈수록 진하게)
                    float fill = Band(r, -1f, 0.70f, aa) * (0.08f + 0.22f * Mathf.Pow(Mathf.Clamp01(r / 0.70f), 2f));
                    c = Over(c, red, fill);
                    // 쐐기 4개(45° 마다 90° 간격) — 안쪽 끝이 뾰족
                    float wedge = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        float da = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, 45f + 90f * k)) * Mathf.Deg2Rad;
                        float half = Mathf.Clamp01((r - 0.52f) / 0.18f) * 0.17f;            // 0.52 에서 0 → 0.70 에서 ±0.17rad
                        float inR = Band(r, 0.52f, 0.71f, aa);
                        wedge = Mathf.Max(wedge, inR * Mathf.Clamp01((half - da) * r / aa + 0.5f));
                        float edge = inR * Mathf.Clamp01((half + 0.035f - da) * r / aa + 0.5f);   // 쐐기 외곽선
                        c = Over(c, ink, edge * 0.95f);
                    }
                    c = Over(c, red, wedge);
                    // 띠 · 테 · 빛
                    c = Over(c, ink, Band(r, 0.70f, 0.90f, aa));
                    float bandT = Mathf.Clamp01((r - 0.73f) / 0.14f);
                    var bandCol = Color.Lerp(red, hot, 1f - Mathf.Abs(bandT - 0.35f) * 2.2f);   // 띠 가운데 조금 위가 밝다
                    c = Over(c, bandCol, Band(r, 0.73f, 0.87f, aa));
                    float glow = r > 0.90f ? Mathf.Pow(Mathf.Clamp01(1f - (r - 0.90f) / 0.10f), 2f) * 0.55f : 0f;
                    c = Over(c, red, glow * (1f - Band(r, -1f, 0.90f, aa)));
                    px[y * n + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float Band(float r, float a, float b, float aa) => Mathf.Clamp01((r - a) / aa + 0.5f) * Mathf.Clamp01((b - r) / aa + 0.5f);

        static Color Over(Color dst, Color src, float a)
        {
            if (a <= 0f) return dst;
            float oa = a + dst.a * (1f - a);
            if (oa <= 1e-5f) return new Color(src.r, src.g, src.b, 0f);
            float k = a / oa;
            return new Color(Mathf.Lerp(dst.r, src.r, k), Mathf.Lerp(dst.g, src.g, k), Mathf.Lerp(dst.b, src.b, k), oa);
        }
    }
}
