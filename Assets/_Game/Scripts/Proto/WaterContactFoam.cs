using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 물에 들어간 캐릭터가 수면에 남기는 자국 — **퍼지는 파문**(기본) 과 **몸 둘레 접촉 띠**(선택).
    ///
    /// 보통 접촉 띠는 뎁스 텍스처로 만든다(수면 깊이와 그 뒤 씬 깊이의 차). 이 프로젝트는 두 모바일
    /// URP 프리셋에서 _CameraDepthTexture 가 꺼져 있고(BCM_StylizedWater.shader 머리말), 켜는 순간
    /// 뎁스 프리패스가 통째로 붙는다. 그래서 (위치, 반지름, 세기, 두께)를 전역 배열로 올리고
    /// 셰이더가 수면에 원을 그린다 — 반지름이 자라면 파문, 고정이면 띠. 그리는 쪽 코드는 같다.
    ///
    /// **왜 파문이 기본인가**: 접촉 띠는 캐릭터가 아무리 움직여도 그대로라 몸에 붙은 스티커처럼
    /// 읽힌다(사용자 피드백). 파문은 움직임 자체가 원인이라 물을 헤치고 간다는 게 그려진다.
    /// 띠는 지우지 않고 남겼다 — 파문만 쓰면 캐릭터가 물 위에 뜬 것처럼 보일 수 있어서
    /// mode 로 켜고 끄며 비교할 수 있게 뒀다.
    ///
    /// 붙이는 곳 — 물에 들어갈 수 있는 캐릭터의 루트(발바닥이 원점인 오브젝트).
    /// 받는 곳 — BCM/BCM_StylizedWater 머티리얼에서 Contact and Ripples 토글이 켜져 있어야 한다.
    /// 도시 웅덩이를 데모 물로 바꾼 Toon/City/TFF_ToonWater(ToonFantasyCityWater)도 같은 전역·같은 토글로 받는다.
    [DisallowMultipleComponent]
    public class WaterContactFoam : MonoBehaviour
    {
        /// 셰이더의 BCM_MAX_CONTACTS 와 반드시 같아야 한다.
        /// **전역 배열 길이는 프로세스에서 한 번 고정된다** — 8 에서 늘렸을 때 에디터를 재시작하기 전까지
        /// 9번째 칸부터 셰이더에 도달하지 않는 것을 실제로 확인했다(스크린샷 slots12_check.png).
        /// 값을 바꾸면 셰이더의 define 도 같이 바꾸고 에디터를 다시 켤 것.
        /// 루프는 실제 개수까지만 도니 남는 칸의 런타임 비용은 없다.
        public const int MaxContacts = 16;

        /// 파문을 만들 자격을 주는 개체 수 상한 (점수 = priority 우선, 그다음 카메라 근접).
        /// **좀비가 몇 마리 물에 들어가든 화면의 파문 수가 고정되도록 하는 장치다.**
        /// 이게 없으면 물량전에서 파문이 서로를 밀어내며 깜빡인다.
        public static int MaxEmitters = 5;

        public enum Mode
        {
            Both,         // 파문 + 수심이 깊을 때만 띠 (기본)
            Ripples,      // 파문만
            ContactBand,  // 띠만 (수심 페이드는 그대로 적용된다)
        }

        [Header("무엇을 그릴지")]
        public Mode mode = Mode.Both;

        [Header("파문")]
        [Tooltip("이만큼 걸을 때마다 파문 하나. 발걸음 간격이라고 보면 된다")]
        public float stepDistance = 0.8f;
        [Tooltip("생길 때 반지름(m)")]
        public float startRadius = 0.14f;
        [Tooltip("다 퍼졌을 때 반지름(m)")]
        public float endRadius = 0.70f;
        [Tooltip("생겨서 사라질 때까지(초)")]
        public float life = 0.85f;
        [Tooltip("고리의 굵기(m). 퍼져도 굵기는 유지된다 — 실제 물결처럼")]
        public float ringThickness = 0.075f;
        // 파문은 거의 정원이어야 물결로 읽힌다. 머티리얼의 Wobble 을 그대로 쓰면(=1)
        // 몸 둘레 띠에 맞춘 세기라 링이 감자 모양이 된다.
        [Tooltip("테두리 흔들림 배수. 1 = 머티리얼 값 그대로. 파문은 낮게")]
        [Range(0f, 1f)] public float ringWobble = 0.35f;
        [Tooltip("좌우 발을 번갈아 어긋내는 폭(m). 0 이면 정중앙")]
        public float sideOffset = 0.12f;
        [Range(0f, 1f)] public float strength = 1f;
        [Tooltip("입수 순간 하나 크게 — 그 배수. 0 이면 안 만든다")]
        public float entrySplash = 1.4f;
        [Tooltip("가만히 서 있을 때 이 간격(초)으로도 하나씩. 0 이면 안 만든다")]
        public float idleInterval = 0f;

        [Header("접촉 띠 (mode 에 포함될 때만)")]
        // **띠는 깊은 물에서만 나온다.** 얕은 물에서는 잠긴 몸이 거의 없어 띠가 가릴 것이 없고,
        // 그러면 발 주위에 링만 떠서 어색하다(실측 확인). 발목만 잠기는 구간은 파문에 맡긴다.
        [Tooltip("이 깊이부터 띠가 보이기 시작한다(m)")]
        public float bandDepthFadeIn = 0.28f;
        [Tooltip("이 깊이면 띠가 최대(m). 호수는 0.55 다")]
        public float bandDepthFull = 0.48f;
        [Tooltip("수면 높이에서의 몸통 반지름과 거의 같게 — 크면 훌라후프처럼 몸에서 떨어진다")]
        public float bandRadius = 0.30f;
        [Range(0.02f, 1f)] public float bandWidth = 0.45f;
        [Range(0f, 1f)] public float bandInner = 0.25f;

        [Header("물속 판정")]
        [Tooltip("발바닥이 오브젝트 원점과 다르면 보정한다")]
        public float feetOffset = 0f;
        [Tooltip("발이 수면보다 이만큼 위까지는 아직 물속으로 본다 — 물가에서 깜빡이는 걸 막는다")]
        public float surfaceSlack = 0.1f;
        [Tooltip("수면보다 이보다 더 깊이 내려가면 다른 물 밑을 지나는 것으로 보고 끈다")]
        public float maxDepth = 4f;
        [Tooltip("수면 메시 바운즈 바깥 여유(m)")]
        public float edgeSlack = 0.4f;
        [Tooltip("자리가 모자랄 때 누구를 남길지. 클수록 먼저 남는다 — 플레이어를 크게 준다")]
        public int priority = 0;

        // ── 등록 ────────────────────────────────────────────────────────
        static readonly List<WaterContactFoam> _sources = new List<WaterContactFoam>();

        void OnEnable()
        {
            _sources.Add(this);
            _inWater = false;
            Driver.Ensure();
        }

        void OnDisable()
        {
            _sources.Remove(this);
            // 남긴 파문은 지우지 않는다 — 좀비가 물에서 죽을 때 자취가 툭 사라지면 눈에 띈다.
            // 수명이 끝나면 알아서 빠지고, 오브젝트가 파괴되면 owner 가 null 이 되어 걷힌다.
            if (_sources.Count == 0 && _ripples.Count == 0) Upload(0);
        }

        // ── 수면 목록 ───────────────────────────────────────────────────
        // 물 메시는 씬을 지을 때 놓고 그대로 있으므로 한 번만 훑는다.
        // 런타임에 물을 새로 만들면 Rescan() 을 부를 것.
        struct Surface { public Bounds bounds; public float y; public float minX, maxX, minZ, maxZ; }   // 모서리는 미리 — Bounds.min/max 는 읽을 때마다 계산(좀비마다 매 프레임, 2026-11-20)
        static readonly List<Surface> _surfaces = new List<Surface>();
        static bool _scanned;

        public static void Rescan()
        {
            _surfaces.Clear();
            _scanned = true;

            foreach (var mr in Object.FindObjectsOfType<MeshRenderer>())
            {
                var mat = mr.sharedMaterial;
                if (mat == null || mat.shader == null) continue;
                if (mat.shader.name != "BCM/BCM_StylizedWater" && mat.shader.name != "Toon/City/TFF_ToonWater") continue;
                // 토글이 꺼진 머티리얼은 그릴 수 없으니 수면으로 치지 않는다 (폭포 시트 등)
                if (!mat.IsKeywordEnabled("BCM_WATER_CONTACT")) continue;

                var b = mr.bounds;
                // 수면 메시는 사실상 평면이라 바운즈 윗면이 곧 수면 높이다.
                // **경사진 물(흘러내리는 개울)에는 안 맞는다** — 그때는 발밑 레이캐스트로 바꿀 것.
                _surfaces.Add(new Surface { bounds = b, y = b.max.y, minX = b.min.x, maxX = b.max.x, minZ = b.min.z, maxZ = b.max.z });
            }
        }

        static List<Surface> Surfaces
        {
            get { if (!_scanned) Rescan(); return _surfaces; }
        }

        /// 지금 물에 들어가 있으면 수면 높이를 돌려준다.
        bool TryGetSurfaceY(out float surfaceY)
        {
            surfaceY = 0f;
            Vector3 p = transform.position;
            float feet = p.y + feetOffset;

            var list = Surfaces;
            bool found = false;

            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                // 바운즈(사각형)로만 거른다. 호수는 둥근 사각형이라 네 모서리가 새지만,
                // 그 자리엔 물 픽셀 자체가 없어 셰이더가 아무것도 안 그린다 — 게이트일 뿐 모양이 아니다.
                if (p.x < s.minX - edgeSlack || p.x > s.maxX + edgeSlack) continue;
                if (p.z < s.minZ - edgeSlack || p.z > s.maxZ + edgeSlack) continue;
                if (feet > s.y + surfaceSlack) continue;   // 수면 위 — 림이나 다리 위에 서 있다
                if (feet < s.y - maxDepth)     continue;   // 너무 깊다 — 이 물과 상관없는 위치

                if (!found || s.y > surfaceY) { surfaceY = s.y; found = true; }
            }
            return found;
        }

        /// 지금 물에 들어가 있으면 (x, 수면높이, z, 반지름) 과 잠긴 깊이를 채워 돌려준다. (접촉 띠용)
        bool TryGetContact(out Vector4 contact, out float depth)
        {
            contact = default; depth = 0f;
            if (bandRadius <= 0f) return false;
            if (!TryGetSurfaceY(out float y)) return false;
            Vector3 p = transform.position;
            depth = y - (p.y + feetOffset);      // 발바닥에서 수면까지 = 잠긴 깊이
            contact = new Vector4(p.x, y, p.z, bandRadius);
            return true;
        }

        // ── 파문 ────────────────────────────────────────────────────────
        struct Ripple
        {
            public WaterContactFoam owner;
            public Vector3 pos;      // y = 수면 높이
            public float birth, life, r0, r1, thick, str, wobble;
        }
        static readonly List<Ripple> _ripples = new List<Ripple>(MaxContacts * 2);

        bool _inWater;
        Vector3 _lastEmit;
        bool _leftFoot;
        float _lastIdle;

        void EmitStep(float now, float y)
        {
            Vector3 p = transform.position;
            p.y = y;

            if (!_inWater)
            {
                // 입수 순간 — 첫 한 방은 크게 (첨벙)
                _inWater = true;
                _lastEmit = p;
                _lastIdle = now;
                if (entrySplash > 0f) Spawn(p, now, entrySplash);
                return;
            }

            float moved = new Vector2(p.x - _lastEmit.x, p.z - _lastEmit.z).magnitude;
            if (stepDistance > 0f && moved >= stepDistance)
            {
                // 좌우 발을 번갈아 어긋내면 발자국처럼 읽힌다. 정중앙에서만 나오면 기계 같다.
                Vector3 dir = p - _lastEmit; dir.y = 0f;
                Vector3 side = dir.sqrMagnitude > 1e-4f
                    ? Vector3.Cross(Vector3.up, dir.normalized) * (_leftFoot ? sideOffset : -sideOffset)
                    : Vector3.zero;
                _leftFoot = !_leftFoot;
                Spawn(p + side, now, 1f);
                _lastEmit = p;
                _lastIdle = now;
            }
            else if (idleInterval > 0f && now - _lastIdle >= idleInterval)
            {
                Spawn(p, now, 0.6f);
                _lastIdle = now;
            }
        }

        void Spawn(Vector3 pos, float now, float scale)
        {
            // 자리가 꽉 차면 **가장 오래된 것을 버린다** — 새 파문이 안 나오면 발이 멈춘 것처럼 보인다
            while (_ripples.Count >= MaxContacts)
            {
                int oldest = 0;
                for (int i = 1; i < _ripples.Count; i++)
                    if (_ripples[i].birth < _ripples[oldest].birth) oldest = i;
                _ripples.RemoveAt(oldest);
            }

            _ripples.Add(new Ripple
            {
                owner = this,
                pos   = pos,
                birth = now,
                life  = life,
                r0    = startRadius * scale,
                r1    = endRadius   * scale,
                thick = ringThickness,
                str   = strength,
                wobble = ringWobble,
            });
        }

        // ── 업로드 ──────────────────────────────────────────────────────
        static readonly Vector4[] _buf = new Vector4[MaxContacts];
        static readonly Vector4[] _fx  = new Vector4[MaxContacts];
        static readonly int _idContacts = Shader.PropertyToID("_BCM_WaterContacts");
        static readonly int _idFx       = Shader.PropertyToID("_BCM_WaterContactFx");
        static readonly int _idCount    = Shader.PropertyToID("_BCM_WaterContactCount");
        static Camera _cam;

        // 상위 K 선발용 고정 버퍼 — 매 프레임 할당이 없어야 한다
        static readonly List<Cand> _cands = new List<Cand>(64);
        static readonly WaterContactFoam[] _top = new WaterContactFoam[32];
        static readonly float[] _topScore = new float[32];

        struct Cand { public WaterContactFoam src; public float y; public float score; }

        static void Collect()
        {
            float now = Time.time;
            if (_cam == null) _cam = Camera.main;
            Vector3 eye = _cam != null ? _cam.transform.position : Vector3.zero;

            // 1) 물에 들어가 있는 개체를 모은다. 여기까지는 전부 검사한다 —
            //    개체당 수면 바운즈 몇 번 비교라 좀비가 수백이어도 무시할 수 있는 비용이다.
            _cands.Clear();
            for (int i = 0; i < _sources.Count; i++)
            {
                var src = _sources[i];
                if (src == null || src.mode == Mode.ContactBand) continue;
                float sy;
                if (!src.TryGetSurfaceY(out sy)) { src._inWater = false; continue; }
                _cands.Add(new Cand
                {
                    src = src, y = sy,
                    score = src.priority * 1000f - (eye - src.transform.position).sqrMagnitude,
                });
            }

            // 2) 상위 MaxEmitters 개만 파문을 만든다. 최솟값 교체라 O(n x K), 할당 없음.
            int budget = Mathf.Clamp(MaxEmitters, 0, _top.Length);
            int topN = 0;
            for (int i = 0; i < _cands.Count; i++)
            {
                float sc = _cands[i].score;
                if (topN < budget) { _top[topN] = _cands[i].src; _topScore[topN] = sc; topN++; continue; }
                if (budget == 0) break;
                int worst = 0;
                for (int k = 1; k < topN; k++) if (_topScore[k] < _topScore[worst]) worst = k;
                if (sc > _topScore[worst]) { _top[worst] = _cands[i].src; _topScore[worst] = sc; }
            }

            for (int i = 0; i < _cands.Count; i++)
            {
                var c = _cands[i];
                bool picked = false;
                for (int k = 0; k < topN; k++) if (_top[k] == c.src) { picked = true; break; }
                if (picked) { c.src.EmitStep(now, c.y); continue; }
                // 안 뽑힌 개체도 '물속'으로 표시해 둔다 — 안 하면 나중에 뽑히는 순간
                // 입수 첨벙이 한 번 더 터진다(물 한복판에서 갑자기 첨벙).
                c.src._inWater = true;
                c.src._lastEmit = c.src.transform.position;
            }

            for (int i = 0; i < _top.Length; i++) _top[i] = null;   // 죽은 참조를 물고 있지 않게
            // 수명이 끝난 것과 주인이 파괴된 것을 걷어낸다.
            // RemoveAll(람다) 는 now 를 캡처해 매 프레임 클로저를 하나 할당한다 — 뒤에서부터 직접 지운다.
            for (int i = _ripples.Count - 1; i >= 0; i--)
            {
                var r = _ripples[i];
                if (r.owner == null || now - r.birth >= r.life) _ripples.RemoveAt(i);
            }

            int n = 0;

            // 2) 접촉 띠 — 파문보다 먼저 자리를 잡는다(항상 떠 있어야 하므로)
            for (int i = 0; i < _sources.Count && n < MaxContacts; i++)
            {
                var src = _sources[i];
                if (src == null || src.mode == Mode.Ripples) continue;
                Vector4 v; float depth;
                if (!src.TryGetContact(out v, out depth)) continue;

                // 깊을수록 진하게. 얕은 물에서는 0 이라 파문만 남는다.
                float t01 = Mathf.InverseLerp(src.bandDepthFadeIn, Mathf.Max(src.bandDepthFadeIn + 0.01f, src.bandDepthFull), depth);
                float bandStr = src.strength * Mathf.SmoothStep(0f, 1f, t01);
                if (bandStr <= 0.004f) continue;

                _buf[n] = v;
                _fx[n]  = new Vector4(bandStr, src.bandWidth, src.bandInner, 1f);   // 띠는 흔들림 그대로
                n++;
            }

            // 3) 파문 — 새것부터 넣는다. 자리가 모자라면 오래된 것(=이미 옅은 것)이 잘린다.
            for (int i = _ripples.Count - 1; i >= 0 && n < MaxContacts; i--)
            {
                var r = _ripples[i];
                float a = Mathf.Clamp01((now - r.birth) / Mathf.Max(0.01f, r.life));

                // 반지름은 처음 빠르게 퍼지고 점점 느려진다 (실제 물결이 그렇다)
                float radius = Mathf.Lerp(r.r0, r.r1, 1f - (1f - a) * (1f - a));
                // 세기는 잠깐 살아났다 서서히 빠진다. 곧바로 최대면 툭 튀어나온 것처럼 보인다.
                float str = r.str * Mathf.Clamp01(a / 0.12f) * Mathf.Pow(1f - a, 1.3f);
                if (str <= 0.002f) continue;

                _buf[n] = new Vector4(r.pos.x, r.pos.y, r.pos.z, radius);
                // 폭은 반지름 대비 값이라, 굵기(m)를 반지름으로 나눠야 퍼져도 굵기가 유지된다
                _fx[n]  = new Vector4(str, Mathf.Clamp(r.thick / radius, 0.02f, 1f), 0f, r.wobble);
                n++;
            }

            Upload(n);
        }

        static void Upload(int count)
        {
            // 남는 칸은 반드시 0 으로 지운다 — 셰이더가 count 로 자르긴 하지만,
            // 예전 프레임 값이 남아 있으면 count 가 늘었을 때 한 프레임 유령이 뜬다.
            for (int i = count; i < MaxContacts; i++) { _buf[i] = Vector4.zero; _fx[i] = Vector4.zero; }
            // 전역 배열은 처음 올린 길이로 고정된다. 항상 MaxContacts 칸을 통째로 올릴 것.
            Shader.SetGlobalVectorArray(_idContacts, _buf);
            Shader.SetGlobalVectorArray(_idFx, _fx);
            Shader.SetGlobalFloat(_idCount, count);
        }

        // ── 구동 ────────────────────────────────────────────────────────
        /// 매 프레임 한 번만 올리려고 두는 숨은 오브젝트.
        /// 각 컴포넌트가 스스로 올리면 캐릭터 수만큼 SetGlobalVectorArray 가 불린다.
        class Driver : MonoBehaviour
        {
            static Driver _instance;

            public static void Ensure()
            {
                if (_instance != null) return;
                var go = new GameObject("~WaterContactFoamDriver") { hideFlags = HideFlags.HideAndDontSave };
                _instance = go.AddComponent<Driver>();
                DontDestroyOnLoad(go);
            }

            // 캐릭터 이동이 모두 끝난 뒤 위치를 읽어야 파문이 한 프레임 뒤에 찍히지 않는다
            void LateUpdate() => Collect();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // 도메인 리로드를 꺼두면 static 이 플레이 모드 사이에 살아남는다 (죽은 참조가 남는다)
            _sources.Clear();
            _ripples.Clear();
            _cands.Clear();
            _surfaces.Clear();
            _scanned = false;
            _cam = null;
        }
    }
}
