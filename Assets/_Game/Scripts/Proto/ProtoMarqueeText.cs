using UnityEngine;

namespace Proto
{
    /// 건물 둘레 띠 전광판 글자 흐르기 — 04_city_03_icon 영화관 아래 빨강·흰 띠(위에서 보면 모서리가 둥근 사각형 고리)를 따라
    /// 글자 메시가 한 바퀴씩 돈다. 글자는 단단한 덩어리로 옮기지 않고 정점마다 띠 위 위치(s)를 밀어 모서리에서 곡면을 따라 휜다.
    /// 정점 자료(rest)는 에디터에서 한 번 계산해 넣는다: x = 띠 둘레 위치 s(m), y = 높이(월드 y), z = 띠 바깥쪽으로 떨어진 거리.
    /// 노멀도 같은 틀(접선·위·바깥)로 저장한다. 메시는 실행 때 사본을 만들어 고친다(원본 에셋은 그대로).
    public class ProtoMarqueeText : MonoBehaviour
    {
        [Header("띠 고리(월드 XZ, 위에서 본 모서리 둥근 사각형)")]
        public float xMin, xMax, zMin, zMax;
        public float radius = 0.9f;

        [Tooltip("초당 이동 거리(m). 음수면 반대로 돈다.")]
        public float speed = 1.5f;

        [HideInInspector] public Vector3[] rest;        // (s, y, 바깥 거리)
        [HideInInspector] public Vector3[] restNormal;  // (접선, 위, 바깥) 성분

        Mesh _mesh;
        Vector3[] _v, _n;
        float _offset;
        Renderer _renderer;

        public float Length => 2f * (xMax - xMin + zMax - zMin) - (8f - 2f * Mathf.PI) * radius;

        /// 둘레 위치 s → 띠 위 점(XZ)과 바깥쪽 방향, 진행 방향.
        /// s = 0 은 앞면(zMin) 오른쪽 끝, 앞면은 -X 쪽으로 진행 → 왼쪽 면 → 뒷면 → 오른쪽 면 → 처음.
        public void Eval(float s, out Vector2 p, out Vector2 outward, out Vector2 tangent) => Eval(s, Length, out p, out outward, out tangent);

        /// L = 둘레(Length). 정점마다 다시 계산하지 않게 넘겨받는다(2026-11-20).
        public void Eval(float s, float L, out Vector2 p, out Vector2 outward, out Vector2 tangent)
        {
            float r = radius, w = xMax - xMin - 2f * r, d = zMax - zMin - 2f * r, q = 0.5f * Mathf.PI * r;
            s %= L; if (s < 0f) s += L;
            // 직선 4개 + 사분원 4개
            if (s < w) { p = new Vector2(xMax - r - s, zMin); outward = new Vector2(0, -1); tangent = new Vector2(-1, 0); return; }
            s -= w;
            if (s < q) { Arc(xMin + r, zMin + r, -90f - s / q * 90f, out p, out outward, out tangent); return; }
            s -= q;
            if (s < d) { p = new Vector2(xMin, zMin + r + s); outward = new Vector2(-1, 0); tangent = new Vector2(0, 1); return; }
            s -= d;
            if (s < q) { Arc(xMin + r, zMax - r, 180f - s / q * 90f, out p, out outward, out tangent); return; }
            s -= q;
            if (s < w) { p = new Vector2(xMin + r + s, zMax); outward = new Vector2(0, 1); tangent = new Vector2(1, 0); return; }
            s -= w;
            if (s < q) { Arc(xMax - r, zMax - r, 90f - s / q * 90f, out p, out outward, out tangent); return; }
            s -= q;
            if (s < d) { p = new Vector2(xMax, zMax - r - s); outward = new Vector2(1, 0); tangent = new Vector2(0, -1); return; }
            s -= d;
            Arc(xMax - r, zMin + r, 0f - Mathf.Min(s, q) / q * 90f, out p, out outward, out tangent);
        }

        void Arc(float cx, float cz, float deg, out Vector2 p, out Vector2 outward, out Vector2 tangent)
        {
            float a = deg * Mathf.Deg2Rad;
            outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            p = new Vector2(cx, cz) + outward * radius;
            tangent = new Vector2(outward.y, -outward.x);   // 각도가 줄어드는 쪽(시계 방향)
        }

        void Start()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || rest == null || rest.Length != mf.sharedMesh.vertexCount) { enabled = false; return; }
            _mesh = Instantiate(mf.sharedMesh);
            _mesh.name = mf.sharedMesh.name + " (흐름)";
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;
            _v = new Vector3[rest.Length];
            _n = new Vector3[rest.Length];
            // 경계는 띠 고리 전체로 — 글자가 돌아도 컬링에 잘리지 않게
            var c = new Vector3((xMin + xMax) * 0.5f, 0f, (zMin + zMax) * 0.5f);
            float y0 = float.MaxValue, y1 = float.MinValue;
            foreach (var r in rest) { y0 = Mathf.Min(y0, r.y); y1 = Mathf.Max(y1, r.y); }
            var wb = new Bounds(new Vector3(c.x, (y0 + y1) * 0.5f, c.z), new Vector3(xMax - xMin + 2f, y1 - y0 + 1f, zMax - zMin + 2f));
            _mesh.bounds = TransformBounds(transform.worldToLocalMatrix, wb);
        }

        void Update()
        {
            if (_mesh == null) return;
            _offset += speed * Time.deltaTime;
            float L = Length;
            if (_offset > L) _offset -= L; else if (_offset < -L) _offset += L;
            // 화면에 안 보이면 정점(만 개 넘음) 계산 · 올리기를 건너뛴다 — 흐름(_offset)은 계속 간다(2026-11-20)
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer != null && !_renderer.isVisible) return;
            var toLocal = transform.worldToLocalMatrix;
            for (int i = 0; i < rest.Length; i++)
            {
                var r = rest[i];
                Eval(r.x + _offset, L, out var p, out var o, out var t);
                var w = new Vector3(p.x + o.x * r.z, r.y, p.y + o.y * r.z);
                _v[i] = toLocal.MultiplyPoint3x4(w);
                var rn = restNormal[i];
                _n[i] = toLocal.MultiplyVector(new Vector3(t.x * rn.x + o.x * rn.z, rn.y, t.y * rn.x + o.y * rn.z)).normalized;
            }
            _mesh.vertices = _v;
            _mesh.normals = _n;
        }

        static Bounds TransformBounds(Matrix4x4 m, Bounds b)
        {
            var c = m.MultiplyPoint3x4(b.center);
            var e = b.extents;
            var ax = m.MultiplyVector(new Vector3(e.x, 0, 0)); var ay = m.MultiplyVector(new Vector3(0, e.y, 0)); var az = m.MultiplyVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2f);
        }

        void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
