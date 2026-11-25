using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 화염방사기의 불 — <b>쿼드 한 장 + 15칸 플립북</b> 판. 파티클 판
    /// (<see cref="ProtoFlameCone"/>) 과 눈으로 비교하려고 만든 두 번째 구현이고,
    /// 옵션 <see cref="ProtoSettings.FlameStyle"/> 로 갈아 끼운다.
    ///
    /// <para><b>이 판은 안 쓰기로 했다 (2026-09-18).</b> 파티클 판과 나란히 놓고 봤고
    /// 그림이 마음에 안 든다는 판단이 나왔다. 지우지 않고 남겨 둔 이유는 아래 비용 때문이다.</para>
    ///
    /// <para><b>비용 (1920x1080, 24라운드 교차, 최소값).</b> 파티클 판의 깜빡임을 고친 뒤
    /// (입자 146알) 기준이다.
    /// <list type="bullet">
    /// <item>파티클 +0.170 ms — 칠하는 화소 1,566,175 = 1080p 화면 한 장의 <b>75.5%</b></item>
    /// <item>스프라이트 +0.004 ms — 칠하는 화소 118,034 = 화면의 <b>5.7%</b></item>
    /// </list>
    /// 데스크톱 시간 차이는 작지만 <b>칠하는 화소가 13배</b>다. 모바일에서 진짜 병목은 이쪽이라,
    /// A54 에서 화염이 프레임을 갉아먹으면 여기부터 꺼내 보면 된다.</para>
    ///
    /// <para>파티클은 겹쳐 그리는 양이 곧 비용이라 불을 두껍게 하려면 그만큼 낸다
    /// (겹침 15배). 플립북은 겹침이 항상 1배고, 쿼드를 넓이 9배로 키워도 시간이 안 늘었다.</para>
    ///
    /// <para><b>쿼드를 조준 축으로 빌보드한다.</b> 조준 방향을 로컬 +Z 로 고정한 채
    /// 그 축을 중심으로만 돌려 카메라를 본다(파티클의 Stretched Billboard 와 같은 방식).
    /// 카메라 쪽으로 정면으로 쏠 때 쿼드가 옆으로 서 버리는 게 이 방식의 약점인데,
    /// 이 게임은 카메라가 37~58° 내려보고 제트는 수평이라 <b>축과 시선이 절대 일치하지
    /// 않는다</b> — 최악이어도 sin(37°)=0.61 만큼 보인다. 위에서 내려보는 카메라가
    /// 약점을 막아 주는 셈이다.</para>
    ///
    /// <para><b>지면에 눕히는 쪽도 열어 뒀다</b>(<see cref="groundLie"/>). 1 이면 쿼드가
    /// 판정 부채꼴과 똑같이 바닥에 눕는다. 판정과 완벽히 겹치지만 카메라 각도에 따라
    /// 그림이 짜부라진다. 0 이면 항상 제 비율로 보이는 대신 판정보다 세로로 부풀어 보인다.</para>
    ///
    /// <para>아틀라스는 <c>ArtSource/VFX/bake_flamejet.py</c> 가 굽는다. 프레임마다
    /// <b>불의 뿌리를 노즐에 맞춰</b> 놨기 때문에 여기서 길이를 만질 필요가 없다 —
    /// 점화 프레임이 짧은 것은 그림 자체가 짧아서지 쿼드를 줄여서가 아니다.</para>
    /// </summary>
    public class ProtoFlameSprite : MonoBehaviour, IProtoFlameFx
    {
        public const string AtlasPath = "VFX/T_FlameJet";
        public const string MatPath   = "VFX/M_FlameSprite";

        // ── 굽는 쪽(bake_flamejet.py)이 알려준 값. 고치려면 저기서 다시 구워야 한다 ──
        // 그림의 가로세로비는 1.977 이다. widthScale 기본값 0.72 는 여기서 나온 값으로,
        // 판정 부채꼴 폭(4.2m)에 0.72 를 곱하면 그림이 제 비율에서 크게 안 벗어난다.
        const float NozzleV = 0.5466f;  // 칸 위에서부터 잰 불의 축 높이

        // 프레임 구간. 0~5 점화(연기 → 불티 → 작은 불 → 뻗음), 5~10 지속, 11~14 소화.
        static readonly int[] Ignite  = { 0, 1, 2, 3, 4, 5 };
        static readonly int[] Loop    = { 5, 6, 7, 8, 9, 10 };
        static readonly int[] Shutoff = { 11, 12, 13, 14 };

        /// 총구를 못 받았을 때 쓰는 발밑 기준 높이. 파티클 판과 같은 값이다 —
        /// 둘을 바꿔 켤 때 불이 위아래로 튀면 안 된다.
        const float FallbackHeight = 0.36f;

        /// <summary>
        /// 분사를 유지하는 시간. <see cref="Pulse"/> 는 <b>발사 간격(0.2초)마다</b> 한 번만
        /// 불리므로 이 값이 그보다 짧으면 연사 중에도 불이 깜빡인다.
        /// </summary>
        const float HoldTime = 0.30f;

        [Tooltip("초당 프레임 수. 15장뿐이라 셰이더에서 두 프레임을 섞어 부드럽게 만든다.")]
        public float fps = 20f;

        [Tooltip("점화 구간만 이 배로 빠르게 넘긴다. 방아쇠를 당기면 불은 즉시 붙어야 한다.")]
        public float igniteSpeed = 1.6f;

        [Tooltip("쿼드 세로 길이를 판정 부채꼴 폭의 몇 배로 잡을지. 1 이면 판정과 같은 폭이다.")]
        [Range(0.3f, 1.2f)] public float widthScale = 0.72f;

        [Tooltip("0 = 조준축 빌보드(항상 제 비율), 1 = 지면에 눕히기(판정과 정확히 일치).")]
        [Range(0f, 1f)] public float groundLie = 0f;

        [Tooltip("가법 밝기. 블룸 문턱(0.90)을 넘겨야 불이 번진다.")]
        public float intensity = 1.35f;

        Transform _quad;
        MeshRenderer _mr;
        MaterialPropertyBlock _mpb;
        Camera _cam;

        Transform _muzzle;
        float _lastPulse = -999f;
        float _cursor;                 // 현재 구간 안에서의 위치(프레임 단위)
        int[] _phase = Shutoff;        // 시작은 '꺼진 상태'
        bool _visible;

        float _builtRange = -1f, _builtCone = -1f, _builtWidth = -1f;

        static readonly int IdFrameA = Shader.PropertyToID("_FrameA");
        static readonly int IdFrameB = Shader.PropertyToID("_FrameB");
        static readonly int IdFrameT = Shader.PropertyToID("_FrameT");
        static readonly int IdInt    = Shader.PropertyToID("_Intensity");
        static readonly int IdTint   = Shader.PropertyToID("_Tint");
        static readonly int IdTintMix = Shader.PropertyToID("_TintMix");
        Color _tint = Color.white;   // 무기 불 색(WeaponStats.flameTint) — 인페르노는 푸르다

        public void Configure(WeaponStats st, Transform muzzle)
        {
            _muzzle = muzzle;
            _tint = st.flameTint;
            transform.localRotation = Quaternion.identity;
            PlaceAtMuzzle();
            Build(st);
        }

        /// 총구로 옮긴다. 매 프레임 한다 — 총구가 나중에 손 본이나 무기 모델에 붙으면
        /// 애니메이션을 따라 움직인다. 회전은 안 건드린다(조준의 주인은 부모다).
        void PlaceAtMuzzle()
        {
            if (_muzzle != null) transform.position = _muzzle.position;
            else transform.localPosition = new Vector3(0f, FallbackHeight, 0f);
        }

        void Build(WeaponStats st)
        {
            if (_quad == null)
            {
                var mat = Resources.Load<Material>(MatPath);
                if (mat == null)
                {
                    Debug.LogWarning("[화염스프라이트] " + MatPath + " 가 없다 — 판정만 돌고 불은 안 보인다");
                    enabled = false;
                    return;
                }
                var go = new GameObject("JetSprite");
                go.transform.SetParent(transform, false);
                _mr = go.AddComponent<MeshRenderer>();
                _mr.sharedMaterial = mat;
                _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mr.receiveShadows = false;
                _mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                go.AddComponent<MeshFilter>();
                _quad = go.transform;
                _mpb = new MaterialPropertyBlock();
                _visible = true;
                SetVisible(false);
            }
            if (_quad == null) return;

            if (Mathf.Approximately(_builtRange, st.range) &&
                Mathf.Approximately(_builtCone, st.coneDegrees) &&
                Mathf.Approximately(_builtWidth, widthScale)) return;
            _builtRange = st.range;
            _builtCone = st.coneDegrees;
            _builtWidth = widthScale;

            // 길이는 사거리 그대로. 세로는 판정 부채꼴이 사거리 끝에서 벌어지는 폭에 맞춘다 —
            // 손으로 박아 두면 밸런스가 사거리를 고치는 순간 그림과 판정이 어긋난다
            // (파티클 판이 콘 각도를 역산하는 것과 같은 이유).
            // 판정은 플레이어에서 st.range 까지고 그림은 총구에서 시작한다. 총구가 앞선
            // 만큼 빼야 불끝과 판정 끝이 같은 자리에 온다 (파티클 판의 Reach 와 같은 계산).
            float ahead = Mathf.Clamp(transform.localPosition.z, 0f, st.range * 0.5f);
            float L = Mathf.Max(0.2f, st.range - ahead);
            float fanW = 2f * L * Mathf.Tan(Mathf.Clamp(st.coneDegrees, 1f, 170f) * 0.5f * Mathf.Deg2Rad);
            float H = fanW * widthScale;

            float down = (1f - NozzleV) * H;   // 노즐보다 아래로 내려가는 만큼
            float up   = NozzleV * H;

            var mesh = new Mesh { name = "FlameJetQuad" };
            // 로컬 +Z = 분사 방향, 로컬 +Y = 쿼드 세로, x=0 평면에 눕는다.
            mesh.vertices = new[] {
                new Vector3(0f, -down, 0f), new Vector3(0f, up, 0f),
                new Vector3(0f, -down, L),  new Vector3(0f, up, L) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1),
                              new Vector2(1, 0), new Vector2(1, 1) };
            mesh.triangles = new[] { 0, 1, 2, 2, 1, 3 };
            // 바운드를 직접 준다. 쿼드가 매 프레임 도는데 자동 바운드는 로컬 기준이라
            // 조준 방향에 따라 화면 밖으로 판정돼 통째로 사라지는 일이 생긴다.
            float r = Mathf.Max(L, H);
            mesh.bounds = new Bounds(new Vector3(0f, 0f, L * 0.5f), new Vector3(r, r, r) * 1.2f);

            var mf = _quad.GetComponent<MeshFilter>();
            if (mf.sharedMesh != null) DestroyImmediate(mf.sharedMesh);
            mf.sharedMesh = mesh;
        }

        public void Pulse() => _lastPulse = Time.time;

        void SetVisible(bool on)
        {
            if (_visible == on || _quad == null) return;
            _visible = on;
            _quad.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            if (_quad == null) return;
            PlaceAtMuzzle();
            // 총열을 따라 낸다 — 총이 아직 정면을 안 보면 켜지 않는다(ProtoFlameCone.AimAlongBarrel 참고)
            bool aligned = ProtoFlameCone.AimAlongBarrel(transform, _muzzle);

            bool on = aligned && Time.time - _lastPulse < HoldTime;
            AdvancePhase(on);
            if (!_visible) return;

            Orient();
            PushFrames();
        }

        /// 점화 → 지속 → 소화 상태 기계. 커서는 '구간 안 프레임 위치'다.
        void AdvancePhase(bool on)
        {
            float dt = ProtoConfig.Delta;
            _cursor += fps * (_phase == Ignite ? igniteSpeed : 1f) * dt;

            if (_phase == Loop)
            {
                if (!on) { _phase = Shutoff; _cursor = 0f; }
                else _cursor = Mathf.Repeat(_cursor, Loop.Length);   // 감기는 지점도 셰이더가 섞는다
            }
            else if (_phase == Ignite)
            {
                if (!on) { _phase = Shutoff; _cursor = 0f; }
                else if (_cursor >= Ignite.Length - 1) { _phase = Loop; _cursor = 0f; }
            }
            else // Shutoff
            {
                if (on) { _phase = Ignite; _cursor = 0f; SetVisible(true); }
                else if (_cursor >= Shutoff.Length - 1) { SetVisible(false); _cursor = Shutoff.Length - 1; }
            }
        }

        /// 조준축을 중심으로만 돌려 카메라를 본다. <see cref="groundLie"/> 가 1 이면 바닥에 눕는다.
        void Orient()
        {
            if (_cam == null) _cam = Camera.main;
            Vector3 aim = transform.forward;      // AimAlongBarrel 이 총열 수평 방향으로 맞춰 둔다
            aim.y = 0f;
            if (aim.sqrMagnitude < 1e-6f) return;
            aim.Normalize();

            // 조준축에 수직이면서 카메라를 가장 잘 향하는 법선
            Vector3 n = Vector3.up;
            if (_cam != null)
            {
                Vector3 toCam = _cam.transform.position - _quad.position;
                Vector3 camPerp = toCam - aim * Vector3.Dot(toCam, aim);
                if (camPerp.sqrMagnitude > 1e-6f)
                    n = Vector3.Slerp(camPerp.normalized, Vector3.up, groundLie);
            }

            Vector3 upAxis = Vector3.Cross(aim, n);
            if (upAxis.sqrMagnitude < 1e-6f) return;
            upAxis.Normalize();
            // 불이 뒤집히지 않게 한다. 빌보드 축이 시선과 겹치는 순간 부호가 뒤집히는데,
            // 이 게임에서는 카메라 정면으로 쏠 때뿐이고 그때 쿼드는 이미 거의 바닥에 누워 있다.
            if (upAxis.y < -0.0001f) upAxis = -upAxis;

            _quad.rotation = Quaternion.LookRotation(aim, upAxis);
        }

        void PushFrames()
        {
            int len = _phase.Length;
            int i = Mathf.Clamp(Mathf.FloorToInt(_cursor), 0, len - 1);
            float t = _cursor - Mathf.Floor(_cursor);
            // 지속 구간만 감는다. 점화/소화는 마지막 프레임에서 멈춘다.
            int j = _phase == Loop ? (i + 1) % len : Mathf.Min(i + 1, len - 1);

            _mr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(IdFrameA, _phase[i]);
            _mpb.SetFloat(IdFrameB, _phase[j]);
            _mpb.SetFloat(IdFrameT, t);
            _mpb.SetFloat(IdInt, intensity);
            _mpb.SetColor(IdTint, _tint);
            _mpb.SetFloat(IdTintMix, _tint == Color.white ? 0f : 1f);   // 색을 입힌 불은 곱하지 않고 다시 칠한다
            _mr.SetPropertyBlock(_mpb);
        }
    }
}
