using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 무언가가 캐릭터를 가리면 비쳐 보이게 한다. 두 방식이 있다.
    ///
    /// <para><b>WholeFade (기본)</b> — 카메라→캐릭터 광선에 걸린 가림막을 <b>통째로</b> 비운다. 건물 뒤에서 오는 좀비까지 보인다.
    /// 가림막 종류에 따라 셋으로 나뉜다(<see cref="Kind"/>).
    /// <list type="bullet">
    /// <item><b>Proxy</b> — 불투명이고 정적 배칭이 아닌 것(세트피스·뉴욕 차·드럼통·묘비·간판). 머티리얼을 반투명 복제본으로 바꾸고,
    /// 같은 메시를 깊이만 먼저 그려 '가장 앞면'만 칠한다 → 뒷벽·지붕 안쪽이 겹쳐 비치지 않는다(Proto_DepthPrepass.shader).</item>
    /// <item><b>ZWrite</b> — 불투명이고 정적 배칭된 것(건물·컨테이너·OBJ 승합차). 정적 배칭은 MeshFilter 에 도시 한 구역을 합친
    /// "Combined Mesh" 를 꽂으므로(월드 좌표로 구워져 있다) 그 메시로 깊이 대리를 그리면 엉뚱한 자리에 깊이가 찍혀 구멍이 났다.
    /// 대신 반투명 복제본이 스스로 깊이를 쓴다 — 뒷면 컬링 + 깊이 쓰기면 상자 모양 건물은 앞면만 남는다.
    /// 판정도 합친 메시 대신 발자국(ProtoFootprint) 상자, 없으면 렌더러 바운즈로 한다.</item>
    /// <item><b>Dither</b> — 알파 컷아웃(나무·덤불 잎 카드). 잎 카드는 여러 겹이라 반투명으로 하면 겹칠수록 진해져 캐릭터가 안 보인다.
    /// 화면 고정 디더(셰이더의 BCM_SeeThroughClip)로 비우면 몇 겹이든 같은 픽셀이 빠져 비율대로 비친다. 머티리얼은 그대로 두고
    /// 렌더러 MPB 로 투시 값만 켠다 — 캐릭터 가슴보다 카메라 쪽 픽셀만 빠지므로 캐릭터 뒤의 잎·줄기는 남고, 바람 흔들림도 그대로다.</item>
    /// </list>
    /// 다 돌아오면 원래 머티리얼·MPB 로 되돌린다(그 순간 배칭도 복귀).</para>
    /// <para><b>CircleDither</b> — 예전 방식. 캐릭터 둘레 원만 디더로 비친다(셰이더 BCM_SEE_THROUGH, 전역 값만 올림).</para>
    ///
    /// <para>대상은 머티리얼에 See Through 토글(BCM_SEE_THROUGH 키워드)이 켜진 렌더러 — 머티리얼 중 하나라도 켜져 있으면 된다.
    /// 키워드가 곧 '가림막' 표시다. 2026-11-10 나무·덤불·드럼통·승합차에도 켰다(사용자: "뒤로 가면 투명하게").</para>
    /// <para><b>씬 배선이 필요 없다.</b> 씬이 로드되면 숨은 오브젝트로 스스로 생긴다.</para>
    [DefaultExecutionOrder(1000)]   // 카메라 추적(LateUpdate)보다 뒤 — 이번 프레임 카메라 위치로 판정
    public class ProtoSeeThrough : MonoBehaviour
    {
        public enum Mode { WholeFade, CircleDither }
        public Mode mode = Mode.WholeFade;

        [Header("통째 페이드 (WholeFade)")]
        [Tooltip("가렸을 때 불투명 가림막(건물·차·드럼통)의 불투명도. 0 이면 사라진다 — 있다는 걸 알아야 하므로 0 으로 두지 말 것.")]
        [Range(0.05f, 1f)] public float fadeOpacity = 0.4f;
        [Tooltip("옅어지고 돌아오는 데 걸리는 시간(초). 너무 짧으면 가장자리에서 번쩍인다.")]
        [Range(0.02f, 1f)] public float fadeTime = 0.25f;
        [Tooltip("판정 광선을 쏘는 캐릭터 높이들(m). 인게임 캐릭터 키 ≈ 2.7m(Visual 1.598배).")]
        public float[] probeHeights = { 0.3f, 1.3f, 2.4f };
        [Tooltip("캐릭터 좌우로도 광선을 쏘는 폭(m). 0 이면 가운데 한 줄 — 어깨만 가려지면 안 옅어진다.")]
        [Range(0f, 1.5f)] public float probeWidth = 0.5f;

        [Header("잎 카드 디더 (WholeFade 중 알파 컷아웃 가림막)")]
        [Tooltip("나무·덤불(알파 컷아웃)이 가렸을 때 남기는 픽셀 비율. 잎이 몇 겹이든 이 비율만 남는다.")]
        [Range(0.05f, 1f)] public float ditherOpacity = 0.4f;   // 반투명(fadeOpacity)과 같은 값 — 디더 점은 화면에서 섞여 같은 농도로 보인다
        [Tooltip("캐릭터 가슴(targetHeight)보다 이만큼(m) 더 카메라 쪽인 픽셀만 비운다 — 캐릭터 뒤의 잎·줄기는 그대로 둔다.")]
        [Range(0f, 3f)] public float ditherDepthMargin = 0.5f;

        [Header("원 디더 (CircleDither)")]
        [Range(0.03f, 0.4f)] public float radius = 0.13f;
        [Range(0f, 0.2f)] public float edge = 0.05f;
        [Range(0f, 1f)] public float minOpacity = 0.3f;
        [Range(0f, 5f)] public float depthMargin = 1.0f;
        [Tooltip("캐릭터 가슴 높이(m). 원 디더의 중심이자 잎 카드 디더의 깊이 기준.")]
        public float targetHeight = 1.3f;

        static readonly int IdPos      = Shader.PropertyToID("_BCM_SeeThroughPos");
        static readonly int IdParams   = Shader.PropertyToID("_BCM_SeeThroughParams");
        static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        const string OccluderKeyword = "BCM_SEE_THROUGH";
        const string CutoutKeyword = "_ALPHATEST_ON";
        const int QueueFade = 4051;   // 깊이 선기록(4050) 바로 뒤. 캐릭터(4001)보다 뒤여야 한다 — Proto_DepthPrepass.shader 참고
        const float WholeScreen = 10f; // 디더 원 반지름(화면 높이 비율). 이만하면 렌더러 전체가 원 안이다

        enum Kind { Proxy, ZWrite, Dither }

        /// 가림막 후보 하나. 처음 본 순간 한 번 만들어 둔다 — 그때 잡은 원본 메시는 나중에 정적 배칭이 끼어들어도 그대로 맞다.
        class Cand
        {
            public Renderer r;
            public Kind kind;
            public Mesh mesh;             // 판정·깊이 대리용 원본 메시(정적 배칭 전). 로컬 바운즈 = 회전된 실제 상자
            public bool fixedBox;         // 정적 배칭: 메시 대신 미리 잰 월드 상자로 판정
            public Matrix4x4 worldToBox;
            public Bounds box;
        }

        class Faded
        {
            public Cand c;
            public Material[] original;
            public float alpha = 1f;
            public bool swapped;
            public GameObject proxy;   // 깊이 선기록 대리(서브메시 전부) — Proxy 종류만
        }

        Transform _target;
        Material _depthMat;
        readonly Dictionary<Material, Material> _fadeMats  = new Dictionary<Material, Material>();   // 깊이 안 씀(깊이 대리와 짝)
        readonly Dictionary<Material, Material> _fadeMatsZ = new Dictionary<Material, Material>();   // 깊이 씀(정적 배칭)
        readonly Dictionary<Renderer, Cand> _info = new Dictionary<Renderer, Cand>();               // null = 가림막 아님
        readonly Dictionary<Renderer, Faded> _faded = new Dictionary<Renderer, Faded>();
        readonly List<Cand> _candidates = new List<Cand>();
        readonly HashSet<Renderer> _hitNow = new HashSet<Renderer>();
        readonly List<Renderer> _done = new List<Renderer>();
        readonly List<Renderer> _stale = new List<Renderer>();
        readonly List<Material> _mats = new List<Material>();
        MaterialPropertyBlock _mpb;
        float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<ProtoSeeThrough>() != null) return;
            var go = new GameObject("_SeeThrough") { hideFlags = HideFlags.DontSave };
            go.AddComponent<ProtoSeeThrough>();
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            // 숨은 셰이더는 빌드에서 Shader.Find 로 못 찾는다(아무 머티리얼도 참조하지 않으면 빌드에서 빠진다) —
            // Resources 의 머티리얼로 물고 온다.
            _depthMat = Resources.Load<Material>("M_DepthPrepass");
            if (_depthMat == null) Debug.LogWarning("[SeeThrough] Resources/M_DepthPrepass 가 없다 — 불투명 가림막도 깊이 쓰기 방식으로 비운다.");
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                var p = FindFirstObjectByType<ProtoPlayer>();
                _target = p != null ? p.transform : null;
            }

            if (mode == Mode.CircleDither && _target != null)
            {
                Vector3 t = _target.position + Vector3.up * targetHeight;
                Shader.SetGlobalVector(IdPos, new Vector4(t.x, t.y, t.z, 1f));
                Shader.SetGlobalVector(IdParams, new Vector4(radius, edge, minOpacity, depthMargin));
            }
            else Shader.SetGlobalVector(IdPos, Vector4.zero);   // 원 디더 끔(잎 카드 디더는 렌더러 MPB 로 따로 켠다)

            // 시작 연출의 안개 막이 떠 있는 동안은 쉰다(ProtoIntro.Veiled) — 캐릭터가 막 위 큐(4062)에 올라가 있어
            // 깊이 선기록(4050)이 캐릭터를 가려 버린다. 어차피 그동안 캐릭터는 공중에 있거나 막 착지한 참이다.
            if (mode == Mode.WholeFade && _target != null && !ProtoIntro.Veiled) UpdateFade(Camera.main);
            else RestoreAll();
        }

        // ── 통째 페이드 ─────────────────────────────────────────────────
        void UpdateFade(Camera cam)
        {
            if (cam == null) return;

            // 후보 목록: 스트리머가 청크마다 건물을 풀에서 꺼내고 넣으므로 주기적으로 다시 훑는다.
            // 매 프레임 FindObjectsByType 은 비싸고, 0.5초 늦게 잡혀도 새 건물은 아직 화면 밖이다.
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + 0.5f;
                Scan();
            }

            // 판정: 카메라 → 캐릭터 몇 점. 가림막의 상자(메시 로컬 바운즈 또는 미리 잰 상자)와 교차하고,
            // 교차 지점이 캐릭터보다 카메라 쪽이면 가림.
            _hitNow.Clear();
            Vector3 camPos = cam.transform.position;
            Vector3 side = cam.transform.right * probeWidth * 0.5f;
            var toTarget = new Ray(camPos, _target.position - camPos);
            for (int ci = 0; ci < _candidates.Count; ci++)
            {
                var c = _candidates[ci];
                var r = c.r;
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                // 싼 거르기: 월드 바운즈가 카메라-캐릭터 선분 근처에도 없으면 넘긴다
                var wb = r.bounds;
                wb.Expand(probeWidth + 3f);
                if (!wb.IntersectRay(toTarget)) continue;

                Matrix4x4 toLocal;
                Bounds lb;
                if (c.fixedBox) { toLocal = c.worldToBox; lb = c.box; }
                else
                {
                    if (c.mesh == null) continue;
                    toLocal = r.transform.worldToLocalMatrix;
                    lb = c.mesh.bounds;
                }
                bool hit = false;
                Vector3 lo = toLocal.MultiplyPoint3x4(camPos);   // 카메라 점은 후보마다 한 번 — 예전엔 탐침(최대 9개)마다(2026-11-20)
                Vector3 tpos = _target.position;
                for (int h = 0; h < probeHeights.Length && !hit; h++)
                    for (int s = -1; s <= 1 && !hit; s++)
                    {
                        if (s != 0 && probeWidth <= 0f) continue;
                        Vector3 p = tpos + Vector3.up * probeHeights[h] + side * s;
                        Vector3 lp = toLocal.MultiplyPoint3x4(p);
                        Vector3 d = lp - lo;
                        float len = d.magnitude;
                        float dist;
                        if (lb.IntersectRay(new Ray(lo, d / len), out dist) && dist < len - 0.05f) hit = true;
                    }
                if (hit) _hitNow.Add(r);
            }

            foreach (var r in _hitNow)
                if (!_faded.ContainsKey(r)) _faded[r] = new Faded { c = _info[r], original = r.sharedMaterials };

            // 목표 불투명도로 서서히
            Vector3 chest = _target.position + Vector3.up * targetHeight;
            var tgt = new Vector4(chest.x, chest.y, chest.z, 1f);
            _done.Clear();
            foreach (var kv in _faded)
            {
                var f = kv.Value;
                var r = f.c.r;
                if (r == null || !r.gameObject.activeInHierarchy)
                {
                    // 청크 회수로 꺼진 가림막 — 풀에서 다시 나올 때 반투명으로 나오면 안 되므로 즉시 되돌린다
                    if (r != null) Restore(f);
                    _done.Add(kv.Key); continue;
                }
                float low = f.c.kind == Kind.Dither ? ditherOpacity : fadeOpacity;
                float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime) * (1f - low);
                float target = _hitNow.Contains(kv.Key) ? low : 1f;
                f.alpha = Mathf.MoveTowards(f.alpha, target, step);
                if (f.alpha >= 0.999f && target >= 1f) { Restore(f); _done.Add(kv.Key); continue; }
                Apply(f, tgt);
            }
            for (int i = 0; i < _done.Count; i++) _faded.Remove(_done[i]);
        }

        void Scan()
        {
            _candidates.Clear();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                Cand c;
                if (!_info.TryGetValue(r, out c))
                {
                    c = MakeCand(r);
                    _info[r] = c;
                }
                if (c != null) _candidates.Add(c);
            }
            // 파괴된 렌더러 정리(키가 유니티 null)
            _stale.Clear();
            foreach (var kv in _info) if (kv.Key == null) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) _info.Remove(_stale[i]);
        }

        /// 가림막이 아니면 null. 머티리얼 중 하나라도 BCM_SEE_THROUGH 가 켜져 있으면 가림막이다.
        Cand MakeCand(Renderer r)
        {
            r.GetSharedMaterials(_mats);
            bool occluder = false, cutout = false;
            for (int i = 0; i < _mats.Count; i++)
            {
                var m = _mats[i];
                if (m == null) continue;
                if (m.IsKeywordEnabled(OccluderKeyword)) occluder = true;
                if (m.IsKeywordEnabled(CutoutKeyword)) cutout = true;
            }
            if (!occluder) return null;

            var mf = r.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            bool batched = r.isPartOfStaticBatch || (mesh != null && mesh.name.StartsWith("Combined Mesh"));

            var c = new Cand { r = r };
            c.kind = cutout ? Kind.Dither : (batched || _depthMat == null ? Kind.ZWrite : Kind.Proxy);
            if (!batched)
            {
                if (mesh == null) return null;
                c.mesh = mesh;
                return c;
            }

            c.fixedBox = true;
            var fp = r.GetComponentInParent<ProtoFootprint>();
            if (fp != null && fp.height > 0.05f && fp.half.x > 0.01f && fp.half.y > 0.01f)
            {
                // 발자국 = 바닥 회전 사각형 + 높이. 씬 처리 단계(배칭 전)에 잰 값이다(FootprintBaker).
                // yaw 는 유니티 규약 — half.x 축 = Euler(0, yaw, 0) * right.
                var m = Matrix4x4.TRS(new Vector3(fp.center.x, fp.height * 0.5f, fp.center.y),
                                      Quaternion.Euler(0f, fp.yaw * Mathf.Rad2Deg, 0f), Vector3.one);
                c.worldToBox = m.inverse;
                c.box = new Bounds(Vector3.zero, new Vector3(fp.half.x * 2f, fp.height, fp.half.y * 2f));
            }
            else
            {
                // 발자국이 없으면 렌더러 월드 바운즈(축 정렬). 비스듬히 놓인 것은 실제보다 조금 넓게 잡힌다.
                c.worldToBox = Matrix4x4.identity;
                c.box = r.bounds;
            }
            return c;
        }

        /// Dither: 셰이더 투시 디더를 이 렌더러에만 켠다. Proxy/ZWrite: 반투명 복제본으로 바꾸고 알파를 내린다.
        void Apply(Faded f, Vector4 chest)
        {
            var r = f.c.r;
            if (f.c.kind == Kind.Dither)
            {
                // 원은 화면 전체, 최소 불투명도 = 지금 알파, 깊이 여유 = 캐릭터 가슴보다 이만큼 앞만 비운다.
                // 전역 _BCM_SeeThroughPos 는 0 이라(원 디더 끔) MPB 를 받은 이 렌더러만 비워진다.
                r.GetPropertyBlock(_mpb);
                _mpb.SetVector(IdPos, chest);
                _mpb.SetVector(IdParams, new Vector4(WholeScreen, 0.01f, f.alpha, ditherDepthMargin));
                r.SetPropertyBlock(_mpb);
                return;
            }

            if (!f.swapped)
            {
                if (f.original.Length == 0 || f.original[0] == null) return;
                bool zwrite = f.c.kind == Kind.ZWrite;
                var fades = new Material[f.original.Length];
                for (int i = 0; i < fades.Length; i++) fades[i] = f.original[i] != null ? FadeMaterial(f.original[i], zwrite) : null;
                r.sharedMaterials = fades;

                // 깊이 선기록은 **같은 메시를 그리는 대리 오브젝트**가 맡는다.
                // 처음엔 [깊이, 반투명] 두 장을 한 렌더러에 끼웠는데, 그건 서브메시가 하나일 때만 맞다 —
                // 서브메시 수를 넘는 머티리얼은 '마지막 서브메시'에만 그려지므로 깊이가 일부만 기록된다.
                // 대리 오브젝트에 서브메시 수만큼 깊이 머티리얼을 끼우면 모든 면이 기록된다.
                if (f.c.kind == Kind.Proxy && _depthMat != null && f.c.mesh != null)
                {
                    f.proxy = new GameObject("_FadeDepth");
                    f.proxy.hideFlags = HideFlags.DontSave;
                    f.proxy.transform.SetParent(r.transform, false);
                    f.proxy.AddComponent<MeshFilter>().sharedMesh = f.c.mesh;
                    var pr = f.proxy.AddComponent<MeshRenderer>();
                    var dm = new Material[f.c.mesh.subMeshCount];
                    for (int i = 0; i < dm.Length; i++) dm[i] = _depthMat;
                    pr.sharedMaterials = dm;
                    pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    pr.receiveShadows = false;
                }
                f.swapped = true;
            }
            // 알파는 렌더러·머티리얼별로(같은 건물 종류가 서로 다른 정도로 옅어질 수 있다). 페이드 중엔 어차피 배칭 밖이다.
            for (int i = 0; i < f.original.Length; i++)
            {
                var src = f.original[i];
                if (src == null) continue;
                Color c = src.HasProperty(IdBaseColor) ? src.GetColor(IdBaseColor) : Color.white;
                c.a *= f.alpha;
                r.GetPropertyBlock(_mpb, i);
                _mpb.SetColor(IdBaseColor, c);
                r.SetPropertyBlock(_mpb, i);
            }
        }

        void Restore(Faded f)
        {
            if (f.proxy != null) { if (Application.isPlaying) Destroy(f.proxy); else DestroyImmediate(f.proxy); f.proxy = null; }
            var r = f.c.r;
            if (r == null) return;
            if (f.swapped) r.sharedMaterials = f.original;
            // MPB 를 비워야 SRP 배처로 돌아간다 — 머티리얼별로 넣었으니 머티리얼별로 비운다(디더는 렌더러 단위)
            for (int i = 0; i < f.original.Length; i++) r.SetPropertyBlock(null, i);
            r.SetPropertyBlock(null);
            f.swapped = false;
        }

        void RestoreAll()
        {
            foreach (var kv in _faded) Restore(kv.Value);
            _faded.Clear();
        }

        /// 원본 툰 머티리얼의 반투명 복제본(종류·깊이 쓰기 여부마다 하나). BCM 의 Fade 모드와 같은 설정.
        Material FadeMaterial(Material src, bool zwrite)
        {
            var cache = zwrite ? _fadeMatsZ : _fadeMats;
            Material m;
            if (cache.TryGetValue(src, out m) && m != null) return m;
            m = new Material(src) { name = src.name + (zwrite ? " (FadeZ)" : " (Fade)") };
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            // 깊이 대리가 없으면 스스로 깊이를 써야 앞면만 남는다(정적 배칭 — Kind.ZWrite)
            m.SetFloat("_ZWrite", zwrite ? 1f : 0f);
            m.SetFloat("_RenderingMode", 2f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.DisableKeyword(OccluderKeyword);   // 원 디더 clip 이 반투명 위에 겹치지 않게
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = QueueFade;
            m.SetShaderPassEnabled("Outline", false);
            cache[src] = m;
            return m;
        }

        void OnDisable()
        {
            Shader.SetGlobalVector(IdPos, Vector4.zero);
            RestoreAll();
        }

        void OnDestroy()
        {
            foreach (var kv in _fadeMats) if (kv.Value != null) Destroy(kv.Value);
            foreach (var kv in _fadeMatsZ) if (kv.Value != null) Destroy(kv.Value);
            _fadeMats.Clear();
            _fadeMatsZ.Clear();
        }
    }
}
