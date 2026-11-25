using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 탱크 바퀴(궤도) 자국 — 프로토 한정 (<see cref="ProtoTank"/> 가 소유).
    ///
    /// <para>궤도 두 줄이 지나간 자리를 <b>끊기지 않는 띠</b>로 잇는다. 도장 찍듯 낱장을 놓으면 회전할 때
    /// 틈이 벌어져서, 직전 지점 → 지금 지점을 잇는 사각형을 한 장씩 붙인다. 오래된 것부터 지워지는
    /// 링 버퍼 + 시간에 따라 흐려지는 정점 알파. 메시 하나라 드로우콜 1.</para>
    ///
    /// <para>큐 2899 = 밤 어둠 막(2900) <b>바로 앞</b>. 뒤에 그리면 어둠 위에 자국만 밝게 떠 보인다.</para>
    /// </summary>
    public class ProtoTankTracks : MonoBehaviour
    {
        const int   MaxQuads   = 480;     // 두 줄 합계. 0.3m 간격이면 줄당 ~70m
        const float Step       = 0.3f;    // 이만큼 움직이면 한 장
        const float Life       = 9f;      // 초 — 이 시간에 걸쳐 사라진다
        const float FadeStart  = 0.5f;    // 수명의 이 비율부터 흐려지기 시작
        const float Alpha      = 0.55f;
        const float TreadTile  = 0.28f;   // 무늬 한 칸 길이(m)
        const float Y          = 0.025f;

        // 궤도 위치 (탱크 로컬). 차체 폭 2.36m, 궤도 폭 ~0.5m
        const float TrackX = 0.92f, TrackW = 0.5f, TrackZ = -1.2f;

        Mesh _mesh;
        Vector3[] _v; Color32[] _c; Vector2[] _uv; int[] _tri;
        float[] _born;
        int _head, _count;

        readonly Vector3[] _last = new Vector3[2];
        readonly float[] _vAccum = new float[2];
        bool _hasLast;
        Material _mat;
        Texture2D _tex;

        public static ProtoTankTracks Create()
        {
            var go = new GameObject("_TankTracks");
            return go.AddComponent<ProtoTankTracks>();
        }

        void Awake()
        {
            _v = new Vector3[MaxQuads * 4];
            _c = new Color32[MaxQuads * 4];
            _uv = new Vector2[MaxQuads * 4];
            _tri = new int[MaxQuads * 6];
            _born = new float[MaxQuads];
            for (int i = 0; i < MaxQuads; i++)
            {
                int v = i * 4, t = i * 6;
                _tri[t] = v; _tri[t + 1] = v + 2; _tri[t + 2] = v + 1;
                _tri[t + 3] = v + 2; _tri[t + 4] = v + 3; _tri[t + 5] = v + 1;
                _born[i] = -999f;
            }
            _mesh = new Mesh { name = "TankTracks" };
            _mesh.MarkDynamic();
            _mesh.vertices = _v; _mesh.colors32 = _c; _mesh.uv = _uv; _mesh.triangles = _tri;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 10f, 4000f));   // 컬링당하지 않게

            _tex = BuildTreadTexture();
            _mat = ProtoFxMat.New("TankTracks", _tex, 2899);   // (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_mat != null) Destroy(_mat);
            if (_tex != null) Destroy(_tex);
        }

        /// 가로 줄무늬(궤도 판) — 가운데가 진하고 양 끝은 흐리게.
        static Texture2D BuildTreadTexture()
        {
            const int W = 16, H = 16;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;
                    float edge = Mathf.Clamp01(Mathf.Min(u, 1f - u) * 6f);        // 양 끝 흐림
                    bool bar = y < H * 0.62f;                                       // 판 / 판 사이 틈
                    float a = edge * (bar ? 1f : 0.35f);
                    px[y * W + x] = new Color32(38, 30, 24, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// 탑승 중 매 프레임. emit=false 면 이어 그리기를 끊는다(내렸다가 다시 타면 순간이동 띠가 생기지 않게).
        public void Tick(Transform tank, bool emit)
        {
            if (emit)
            {
                for (int s = 0; s < 2; s++)
                {
                    Vector3 p = tank.TransformPoint(new Vector3(s == 0 ? -TrackX : TrackX, 0f, TrackZ));
                    p.y = Y;
                    if (!_hasLast) { _last[s] = p; continue; }
                    Vector3 d = p - _last[s];
                    float len = d.magnitude;
                    if (len < Step) continue;
                    if (len > Step * 8f) { _last[s] = p; continue; }   // 튐(순간이동) — 잇지 않는다
                    AddQuad(_last[s], p, tank.right, s);
                    _last[s] = p;
                }
                _hasLast = true;
            }
            else _hasLast = false;
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 right, int side)
        {
            int i = _head;
            _head = (_head + 1) % MaxQuads;
            _count = Mathf.Min(_count + 1, MaxQuads);
            _born[i] = Time.time;
            _geomDirty = true; _lastBorn = Time.time; _faded = false;

            Vector3 r = right; r.y = 0f; r = r.normalized * (TrackW * 0.5f);
            // 무늬가 장과 장 사이에서 끊기지 않게 V 를 이어 붙인다. 정수 칸만큼 감아도 무늬 위상은 같다(정밀도 유지).
            float v0 = _vAccum[side];
            float v1 = v0 + (b - a).magnitude / TreadTile;
            _vAccum[side] = v1 >= 1000f ? v1 - 1000f : v1;
            int k = i * 4;
            _v[k]     = a - r; _uv[k]     = new Vector2(0f, v0);
            _v[k + 1] = a + r; _uv[k + 1] = new Vector2(1f, v0);
            _v[k + 2] = b - r; _uv[k + 2] = new Vector2(0f, v1);
            _v[k + 3] = b + r; _uv[k + 3] = new Vector2(1f, v1);
        }

        // 모양은 새 자국이 생겼을 때만, 색은 사라지는 동안만 올린다 — 예전엔 다 사라진 뒤에도 매 프레임 정점 · UV · 색 전부(2026-11-20)
        bool _geomDirty, _faded;
        float _lastBorn;

        void LateUpdate()
        {
            if (_count == 0 || _faded) return;
            float now = Time.time;
            bool allGone = now - _lastBorn > Life;   // 가장 늦은 자국까지 다 사라졌다 — 이번에 알파 0 을 한 번 올리고 쉰다
            for (int i = 0; i < MaxQuads; i++)
            {
                float age = (now - _born[i]) / Life;
                float a = age >= 1f ? 0f : Alpha * (age <= FadeStart ? 1f : 1f - (age - FadeStart) / (1f - FadeStart));
                var c = new Color32(255, 255, 255, (byte)(a * 255));
                int k = i * 4;
                _c[k] = c; _c[k + 1] = c; _c[k + 2] = c; _c[k + 3] = c;
            }
            if (_geomDirty) { _mesh.vertices = _v; _mesh.uv = _uv; _geomDirty = false; }
            _mesh.colors32 = _c;
            if (allGone) _faded = true;
        }
    }
}
