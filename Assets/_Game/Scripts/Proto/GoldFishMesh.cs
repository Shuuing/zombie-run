using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 금색 물고기 메시를 코드로 짠다(2026-11-18 사용자 "동전은 아니고 그냥 금색 물고기로" → 같은 날 "좀 더 귀여운 걸로" 2판).
    ///
    /// <para><b>물고기 그림(1 버전)과 같은 상자 규약</b>: x = -0.5(코) … +0.5(꼬리 끝), y = -0.5 … +0.5(지느러미 포함), 머리 = -x.
    /// 그래서 <see cref="ProtoGemPool"/> 의 카메라 세우기 · 갸웃 · 좌우 반전 · 스쿼시와 셰이더 몸 휨(꼬리로 갈수록 y 로 휜다)을 그대로 탄다.
    /// 두께는 z(앞 = +z = 카메라 쪽), 배율 z 는 키와 같게 준다. 가로 배율 = 키 × <see cref="Aspect"/>.</para>
    ///
    /// <para>2판(귀엽게): 1판(길쭉한 몸 · 뾰족한 갈래 꼬리 · 작은 눈)에서 —
    /// 몸을 짧고 통통하게(가로/세로 1.6 → 1.3, 가장 높은 곳 0.265 → 0.33, 두께 비 0.56 → 0.7, 코를 더 뭉툭하게, 꼬리자루 굵게),
    /// 눈을 크게(반지름 0.08 → 0.112, 몸 가운데 쪽으로) + 하이라이트 둘(큰 것 · 작은 것),
    /// 지느러미를 전부 둥글게(꼬리 = 둥근 잎 두 장 부채, 등 · 뒷지느러미 = 반달 혹, 가슴지느러미 = 작은 손 같은 타원).
    /// 볼터치 · 웃는 입 · 비늘은 셰이더가 좌표로 그린다(Proto/GoldFish).</para>
    ///
    /// <para>정점 색: r = 그늘 배율, g = 지느러미, b = 눈(0.25 흰자 · 0.5 하이라이트 · 1 검은자). UV 없음.</para>
    /// </summary>
    public static class GoldFishMesh
    {
        /// 가로 / 세로 — <see cref="ProtoGemLook.goldFishAspect"/> 기본값. 메뉴로 다시 구울 때 에셋에도 같이 넣는다.
        public const float Aspect = 1.3f;

        const float XNose = -0.5f, XTail = 0.16f;   // 몸통 범위(그 뒤는 꼬리지느러미)
        const float HMax = 0.33f, HPed = 0.11f;     // 몸 반높이 — 가장 높은 곳 · 꼬리자루(굵게 = 뭉툭하고 귀엽다)
        const float UPeak = 0.45f;                  // 가장 높은 곳(코에서 몸통 길이 비율) — 가운데쯤이라 동글동글
        const float NosePow = 0.38f;                // 작을수록 코가 뭉툭(둥근 이마)
        const float ZRatio = 0.70f;                 // 두께 / 높이 — 통통하게
        const float Belly = 0.02f;                  // 배가 처지는 양
        const float FinGap = 0.004f;                // 지느러미 앞뒤 두 장 사이

        public static float U(float x) => Mathf.InverseLerp(XNose, XTail, x);

        /// 몸 반높이(u = 코 0 … 꼬리자루 1)
        public static float HalfH(float u)
        {
            u = Mathf.Clamp01(u);
            if (u <= UPeak) return HMax * Mathf.Pow(Mathf.Sin(u / UPeak * Mathf.PI * 0.5f), NosePow);
            float t = (u - UPeak) / (1f - UPeak);
            return HPed + (HMax - HPed) * Mathf.Pow(Mathf.Cos(t * Mathf.PI * 0.5f), 0.9f);
        }

        public static float CenterY(float u) => -Belly * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u));
        static float TopY(float x) { float u = U(x); return CenterY(u) + HalfH(u); }
        static float BotY(float x) { float u = U(x); return CenterY(u) - HalfH(u); }

        /// 몸 겉면의 z(앞쪽, 양수) — 타원 단면
        static float SurfZ(float x, float y)
        {
            float u = U(x);
            float hh = HalfH(u);
            if (hh < 1e-4f) return 0f;
            float t = (y - CenterY(u)) / hh;
            return hh * ZRatio * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
        }

        public static Mesh Build(int rings = 32, int around = 28)
        {
            var v = new List<Vector3>(2048);
            var c = new List<Color>(2048);
            var t = new List<int>(6144);

            // 유니티는 cross(b-a, c-a) 쪽이 앞면 — facing 쪽을 보게 감는다
            void TriIdx(int a, int b, int cc, Vector3 facing)
            {
                Vector3 n = Vector3.Cross(v[b] - v[a], v[cc] - v[a]);
                if (n.sqrMagnitude < 1e-12f) return;   // 코 끝 같은 넓이 0 삼각형은 버린다
                if (Vector3.Dot(n, facing) < 0f) (b, cc) = (cc, b);
                t.Add(a); t.Add(b); t.Add(cc);
            }
            int Add(Vector3 p, Color col) { v.Add(p); c.Add(col); return v.Count - 1; }

            // ── 몸통 — 고리를 이어 붙인 관. 정점을 고리 안에서 공유해 법선이 부드럽게 ──
            var body = new Color(1f, 0f, 0f, 1f);
            int ringStart = v.Count;
            for (int i = 0; i <= rings; i++)
            {
                float u = i / (float)rings;
                float x = Mathf.Lerp(XNose, XTail, u);
                float hh = HalfH(u), hz = hh * ZRatio, cy = CenterY(u);
                for (int j = 0; j < around; j++)
                {
                    float a = j * Mathf.PI * 2f / around;
                    Add(new Vector3(x, cy + hh * Mathf.Cos(a), hz * Mathf.Sin(a)), body);
                }
            }
            for (int i = 0; i < rings; i++)
            {
                float u = (i + 0.5f) / rings;
                float cym = CenterY(u);
                for (int j = 0; j < around; j++)
                {
                    int a = ringStart + i * around + j, b = ringStart + i * around + (j + 1) % around;
                    int d = ringStart + (i + 1) * around + j, e = ringStart + (i + 1) * around + (j + 1) % around;
                    Vector3 mid = (v[a] + v[b] + v[d] + v[e]) * 0.25f;
                    Vector3 facing = new Vector3(i == 0 ? -0.5f : 0f, mid.y - cym, mid.z);   // 바깥쪽(코 고리는 앞쪽도)
                    TriIdx(a, b, e, facing);
                    TriIdx(a, e, d, facing);
                }
            }
            // 꼬리자루 끝 뚜껑(꼬리지느러미에 거의 가려진다)
            {
                int last = ringStart + rings * around;
                int cap = Add(new Vector3(XTail, CenterY(1f), 0f), body);
                for (int j = 0; j < around; j++) TriIdx(cap, last + j, last + (j + 1) % around, Vector3.right);
            }

            // ── 지느러미 — 납작한 판 두 장(앞 +z · 뒤 -z), 기준점에서 부채꼴로. 전부 둥근 모양 ──
            var fin = new Color(0.97f, 1f, 0f, 1f);
            void Fin(Vector2 root, List<Vector2> outline)
            {
                for (int side = 0; side < 2; side++)
                {
                    float s = side == 0 ? 1f : -1f;
                    float z = FinGap * s;
                    int r = Add(new Vector3(root.x, root.y, z), fin);
                    int first = v.Count;
                    foreach (var p in outline) Add(new Vector3(p.x, p.y, z), fin);
                    for (int k = 0; k < outline.Count - 1; k++) TriIdx(r, first + k, first + k + 1, new Vector3(0f, 0f, s));
                }
            }

            // 꼬리 — 둥근 잎 두 장 부채(가운데가 살짝 오목). 반지름 r(t) = R(0.70 + 0.30 sin(π|t|)), t = -1(아래) … 1(위)
            {
                float cyT = CenterY(1f);
                var root = new Vector2(0.12f, cyT);
                const float R = 0.40f, ThetaMax = 72f * Mathf.Deg2Rad;
                var ol = new List<Vector2> { new Vector2(0.11f, cyT + 0.10f) };
                const int N = 24;
                for (int k = 0; k <= N; k++)
                {
                    float tt = 1f - 2f * k / N;                       // 1 → -1 (위에서 아래로)
                    float th = tt * ThetaMax;
                    float r = R * (0.70f + 0.30f * Mathf.Sin(Mathf.PI * Mathf.Abs(tt)));
                    ol.Add(new Vector2(root.x + 0.02f + Mathf.Cos(th) * r, root.y + Mathf.Sin(th) * r));
                }
                ol.Add(new Vector2(0.11f, cyT - 0.10f));
                Fin(root, ol);
            }
            // 등지느러미 — 뒤로 살짝 누운 반달 혹
            ArcFin(-0.13f, 0.09f, 0.135f, top: true, lean: 0.035f);
            // 뒷지느러미 — 배 아래 꼬리 쪽 작은 반달
            ArcFin(0.00f, 0.13f, 0.085f, top: false, lean: 0.03f);

            void ArcFin(float xa, float xb, float height, bool top, float lean)
            {
                const int N = 14;
                float cx = (xa + xb) * 0.5f, rx = (xb - xa) * 0.5f;
                float baseA = top ? TopY(xa) - 0.035f : BotY(xa) + 0.035f;
                float baseB = top ? TopY(xb) - 0.035f : BotY(xb) + 0.035f;
                float sgn = top ? 1f : -1f;
                var ol = new List<Vector2>();
                for (int k = 0; k <= N; k++)
                {
                    float a = Mathf.PI * (1f - k / (float)N);       // π(앞) → 0(뒤)
                    float bx = cx + Mathf.Cos(a) * rx;
                    float baseY = Mathf.Lerp(baseA, baseB, (bx - xa) / (xb - xa));
                    float h = Mathf.Sin(a) * height;
                    ol.Add(new Vector2(bx + Mathf.Sin(a) * lean, baseY + sgn * h));
                }
                var root = new Vector2(cx, (baseA + baseB) * 0.5f - sgn * 0.02f);
                Fin(root, ol);
            }

            // 가슴지느러미 — 눈 뒤아래 옆면에 붙은 작은 손 같은 타원. 앞쪽은 몸에 붙고 뒤로 갈수록 들린다(양쪽)
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? 1f : -1f;
                Vector2 ctr = new Vector2(-0.07f, -0.11f);
                const float rx = 0.085f, ry = 0.048f, rot = -28f * Mathf.Deg2Rad;
                const int N = 16;
                for (int face = 0; face < 2; face++)
                {
                    float f = face == 0 ? 1f : -1f;           // 바깥 면 · 안쪽 면
                    float baseLift = face == 0 ? 0.012f : 0.006f;
                    float Lift(Vector2 p) => baseLift + 0.06f * Mathf.Clamp01((p.x - (ctr.x - rx)) / (2f * rx));
                    Vector3 P(Vector2 p) => new Vector3(p.x, p.y, s * (SurfZ(p.x, p.y) + Lift(p)));
                    int center = Add(P(ctr), fin);
                    int first = v.Count;
                    for (int k = 0; k <= N; k++)
                    {
                        float a = k * Mathf.PI * 2f / N;
                        var e = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                        e = new Vector2(e.x * Mathf.Cos(rot) - e.y * Mathf.Sin(rot), e.x * Mathf.Sin(rot) + e.y * Mathf.Cos(rot));
                        Add(P(ctr + e), fin);
                    }
                    for (int k = 0; k < N; k++) TriIdx(center, first + k, first + k + 1, new Vector3(0f, 0f, s * f));
                }
            }

            // ── 눈 — 크게. 몸 겉면을 따라 휜 원판(흰자 · 검은자 · 큰 하이라이트 · 작은 하이라이트) ──
            for (int side = 0; side < 2; side++)
            {
                float s = side == 0 ? 1f : -1f;
                float ex = -0.25f, ey = CenterY(U(ex)) + 0.07f;
                float px = ex - 0.008f, py = ey - 0.006f;   // 검은자는 살짝 앞 · 아래(앞을 보는 눈)
                Disc(ex, ey, 0.112f, 0.005f, new Color(1f, 0f, 0.25f, 1f), s);               // 흰자
                Disc(px, py, 0.088f, 0.009f, new Color(1f, 0f, 1f, 1f), s);                  // 검은자
                Disc(px - 0.028f, py + 0.030f, 0.034f, 0.013f, new Color(1f, 0f, 0.5f, 1f), s); // 큰 하이라이트
                Disc(px + 0.030f, py - 0.030f, 0.015f, 0.013f, new Color(1f, 0f, 0.5f, 1f), s); // 작은 하이라이트
            }

            void Disc(float cx, float cy, float r, float lift, Color col, float s)
            {
                const int N = 22;
                Vector3 P(float x, float y) => new Vector3(x, y, s * (SurfZ(x, y) + lift));
                int center = Add(P(cx, cy), col);
                int first = v.Count;
                for (int k = 0; k <= N; k++)
                {
                    float a = k * Mathf.PI * 2f / N;
                    Add(P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r), col);
                }
                for (int k = 0; k < N; k++) TriIdx(center, first + k, first + k + 1, new Vector3(0f, 0f, s));
            }

            var m = new Mesh { name = "GoldFish" };
            m.SetVertices(v);
            m.SetColors(c);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            // 셰이더가 꼬리를 위아래로 휘고(최대 ±0.17) 갸웃 · 스쿼시가 붙는다 — 바운즈를 넉넉히(물고기 쿼드와 같은 이유)
            m.bounds = new Bounds(Vector3.zero, new Vector3(1.2f, 1.6f, 0.7f));
            return m;
        }
    }
}
