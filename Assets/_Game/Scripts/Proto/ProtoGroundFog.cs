using UnityEngine;
using UnityEngine.Rendering;

namespace Proto
{
    /// 필드 바닥 안개 (GDD §15). 셰이더는 Proto/GroundFog.
    ///
    /// 수평 시트 몇 장을 낮게 깔고 카메라를 따라 XZ 로만 옮긴다. 안개 무늬 자체는
    /// 셰이더가 **월드 좌표**로 그리므로 시트가 따라와도 안개는 월드에 붙어 있다 —
    /// 플레이어가 달리면 안개가 옆으로 흘러간다.
    ///
    /// ── 시트 높이를 왜 이렇게 낮게 잡나 ─────────────────────────────────
    /// 깊이 텍스처(_CameraDepthTexture)를 쓰지 않는다. URP 에서 그걸 켜면 포워드+MSAA4
    /// 조합상 깊이 프리패스가 추가돼 불투명 드로우가 두 배가 된다 — 좀비 물량전에서
    /// 낼 비용이 아니다. 대신 시트를 정강이 높이까지만 낮춰서, 깊이 페이드 없이 생기는
    /// "캐릭터를 가로지르는 선"이 화면에서 1~2px 로 묻히게 한다.
    /// 시트를 1m 이상으로 올리면 그 선이 눈에 띄기 시작한다.
    ///
    /// ── 거리 구간(distanceBand)을 다시 계산하는 법 ──────────────────────
    /// 보이는 지면의 카메라 거리 = 카메라높이 / sin(피치 ± FOV절반).
    ///   카메라높이 = ProtoCameraFollow.distance × sin(pitch)
    /// 현재값(distance 17.76, pitch 58°, 수직 FOV 42°): 높이 15.06m →
    ///   하단 15.3m · 중앙(플레이어) 17.8m · 상단 25.0m.
    /// 보이는 지면이 10m 띠뿐이라 안개 구간도 그 안에 들어와야 한다 — 기본값 (13, 19, …) 은
    /// 화면 하단부터 옅게 깔려 상단(25m)에서 최대가 되도록 잡은 값이다. 캐릭터 주변을
    /// 비우는 일은 거리가 아니라 clearRadius/clearFeather 가 맡는다.
    /// **카메라 거리·피치·FOV 중 하나라도 바꾸면 여기와 SceneLookSetup 의 FogStart/End 를
    /// 같이 고쳐야 한다.** 안 고치면 안개가 화면 밖으로 밀려나 통째로 안 보인다
    /// (2026-09-15 이전 상태가 정확히 그거였다: fog 26~68m, 화면 상단이 25m).
    [ExecuteAlways]
    public class ProtoGroundFog : MonoBehaviour
    {
        [System.Serializable]
        public struct Sheet
        {
            [Tooltip("지면에서의 높이 m")]           public float height;
            [Tooltip("이 층 하나의 최대 불투명도")]   public float alpha;
            [Tooltip("노이즈 밀도 1/m. 0.17 ≈ 6m 덩어리")] public float noiseScale;
            [Tooltip("월드 기준 흐름 m/s")]          public Vector2 drift;
            [Tooltip("덮는 비율. 높을수록 듬성듬성")] public float coverage;
            [Tooltip("덩어리 가장자리 부드럽기")]     public float softness;
        }

        [Header("층 — 줄이려면 여기부터 지울 것 (층 = 오버드로 겹수)")]
        public Sheet[] sheets =
        {
            new Sheet { height = 0.15f, alpha = 0.42f, noiseScale = 0.17f,
                        drift = new Vector2( 0.60f, 0.35f), coverage = 0.46f, softness = 0.30f },
            new Sheet { height = 0.55f, alpha = 0.26f, noiseScale = 0.11f,
                        drift = new Vector2(-0.42f, 0.22f), coverage = 0.52f, softness = 0.34f },
        };

        [Header("거리 구간 (카메라로부터 m)")]
        [Tooltip("x=차오르기 시작, y=최대, z=옅어지기 시작, w=사라짐")]
        // 아래 맑은 구역이 캐릭터 주변을 책임지므로, 이 띠는 화면 하단(15.3m)부터
        // 옅게라도 깔리게 일찍 연다. 둘 다 플레이어 거리에서 끊으면 안개가 캐릭터
        // **뒤쪽**(화면 아래)에도 없어서 구역이 반달처럼 보인다.
        public Vector4 distanceBand = new Vector4(13f, 19f, 40f, 46f);

        [Header("맑은 구역 — 캐릭터 주변")]
        [Tooltip("끄면 거리 게이트만 남아 안개가 화면 아래→위로만 깔린다")]
        public bool clearAroundPlayer = true;
        [Tooltip("비워두면 ProtoPlayer, 그것도 없으면 화면 중앙의 지면을 쓴다")]
        public Transform clearTarget;
        // 전투 범위에 맞춘다. 무기 사거리는 2.5~11m 이므로 최단 사거리까지는 완전히 맑고
        // 최장 사거리 밖에서 안개가 100% 가 된다 — "쏘는 곳은 선명" 이 규칙이다.
        [Tooltip("이 반경 안은 안개 0. 최단 무기 사거리(4.5m)에 맞춤")]
        public float clearRadius = 4.5f;
        [Tooltip("여기서 안개가 100%. 반경보다 넉넉해야 동그란 원으로 안 보인다")]
        public float clearFeather = 6.5f;

        [Header("색")]
        [Tooltip("켜면 RenderSettings 의 안개색을 그대로 쓴다 — 거리 안개와 색이 갈라지지 않게")]
        public bool followSceneFogColor = true;
        public Color color = new Color(0.68f, 0.69f, 0.70f);

        // 거리 안개와 **똑같은** 색을 쓰면 원경에서는 아무 일도 일어나지 않는다 — 이미
        // 그 색으로 덮인 픽셀에 같은 색을 더 얹는 꼴이라, 정작 안개 덩어리가 보여야 할
        // 곳에서 대비가 0 이 된다 (실측: 평균 차이 1~3/255). 바닥 안개는 더 가깝고 짙어
        // 빛을 더 받는 층이므로 살짝 밝게 가는 게 물리적으로도 맞고, 그 차이가 덩어리의
        // 윤곽을 만든다. 1.0 으로 두면 거리 안개에 그대로 묻힌다.
        [Range(0.8f, 1.4f)]
        [Tooltip("거리 안개 색 대비 밝기. 1.0 이면 원경에서 안개 덩어리가 안 보인다")]
        public float brightness = 1.08f;

        Transform[] _sheets;
        MeshRenderer[] _rends;
        Material[]  _mats;
        Mesh        _mesh;
        Color       _appliedColor;
        ProtoPlayer _player;

        void OnEnable()  { Rebuild(); }
        void OnDisable() { Teardown(); }

#if UNITY_EDITOR
        void OnValidate()
        {
            // 인스펙터에서 슬라이더를 만지는 동안 바로 보이게. OnValidate 안에서
            // 오브젝트를 만들면 Unity 가 경고하므로 다음 에디터 틱으로 미룬다.
            if (!isActiveAndEnabled) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled) return;
                Rebuild();
            };
        }
#endif

        /// 수치를 코드에서 바꾼 뒤에는 이걸 불러야 한다 — 값이 머티리얼에 구워져 있다.
        public void Rebuild()
        {
            Teardown();
            if (sheets == null || sheets.Length == 0) return;

            var shader = Shader.Find("Proto/GroundFog");
            if (shader == null)
            {
                Debug.LogWarning("[GroundFog] Proto/GroundFog 셰이더를 찾지 못했다 — 안개를 건너뛴다");
                return;
            }

            // 쿼드는 구간 바깥까지 넉넉히 덮는다. 시트는 Camera.main 을 따라가지만
            // 게이트는 셰이더가 _WorldSpaceCameraPos 로 카메라마다 따로 계산하므로,
            // 쿼드가 크면 씬 뷰 카메라에서도 안개가 제 위치에 보인다.
            float half = distanceBand.w + 20f;
            _mesh = BuildQuad(half);

            _sheets = new Transform[sheets.Length];
            _rends  = new MeshRenderer[sheets.Length];
            _mats   = new Material[sheets.Length];

            for (int i = 0; i < sheets.Length; i++)
            {
                var s = sheets[i];

                var go = new GameObject($"FogSheet_{i}");
                // 씬에 저장되면 안 된다 — 씬을 열 때마다 시트가 쌓인다.
                go.hideFlags = HideFlags.HideAndDontSave;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, s.height, 0f);

                go.AddComponent<MeshFilter>().sharedMesh = _mesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode      = ShadowCastingMode.Off;
                mr.receiveShadows         = false;
                mr.lightProbeUsage        = LightProbeUsage.Off;
                mr.reflectionProbeUsage   = ReflectionProbeUsage.Off;
                mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                mat.SetFloat(ProtoShaderIds._Alpha,      s.alpha);
                mat.SetFloat(ProtoShaderIds._NoiseScale, s.noiseScale);
                mat.SetVector(ProtoShaderIds._Drift,     new Vector4(s.drift.x, s.drift.y, 0f, 0f));
                mat.SetFloat(ProtoShaderIds._Coverage,   s.coverage);
                mat.SetFloat(ProtoShaderIds._Softness,   Mathf.Max(0.01f, s.softness));
                mat.SetVector(ProtoShaderIds._DistBand,  distanceBand);
                mr.sharedMaterial = mat;

                _sheets[i] = go.transform;
                _rends[i]  = mr;
                _mats[i]   = mat;
            }

            _appliedColor = default;   // 다음 ApplyColor 에서 반드시 한 번 쓰이게
            ApplyColor();

            // 시트를 새로 만들었으니 옵션 상태를 다시 입힌다 — 안 하면 안개를 꺼둔
            // 플레이어에게 리빌드 직후 한 번 안개가 돌아온다.
            if (Application.isPlaying) SetVisible(ProtoSettings.FogEnabled);
        }

        void Teardown()
        {
            if (_sheets != null)
                foreach (var t in _sheets)
                    if (t != null) SafeDestroy(t.gameObject);

            if (_mats != null)
                foreach (var m in _mats)
                    if (m != null) SafeDestroy(m);

            if (_mesh != null) SafeDestroy(_mesh);

            _sheets = null;
            _rends  = null;
            _mats   = null;
            _mesh   = null;
        }

        static void SafeDestroy(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else                       DestroyImmediate(o);
        }

        /// 옵션의 안개 온/오프. **게임오브젝트가 아니라 렌더러를 끈다** —
        /// 오브젝트를 끄면 OnDisable 이 시트를 파괴하고, 스스로는 다시 켤 수 없어
        /// 옵션을 되돌릴 방법이 사라진다.
        public void SetVisible(bool on)
        {
            if (_rends == null) return;
            foreach (var r in _rends)
                if (r != null) r.enabled = on;
        }

        void LateUpdate()
        {
            if (_sheets == null) return;

            var cam = Camera.main;
            if (cam != null)
            {
                // XZ 만 따라간다. Y 는 시트별 높이 그대로 둔다.
                var p = cam.transform.position;
                transform.position = new Vector3(p.x, 0f, p.z);
            }

            ApplyClearZone(cam);
            ApplyColor();
        }

        /// 맑은 구역의 중심을 매 프레임 셰이더에 넘긴다.
        void ApplyClearZone(Camera cam)
        {
            if (_mats == null) return;

            float radius  = clearAroundPlayer ? clearRadius  : 0f;
            float feather = clearAroundPlayer ? clearFeather : 0.01f;

            Vector3 center = transform.position;
            if (clearAroundPlayer)
            {
                var t = clearTarget;
                if (t == null)
                {
                    if (_player == null) _player = FindObjectOfType<ProtoPlayer>();
                    if (_player != null) t = _player.transform;
                }

                if (t != null) center = t.position;
                else if (cam != null) center = ScreenCenterOnGround(cam, center);
            }

            var c = new Vector4(center.x, center.y, center.z, 0f);
            foreach (var m in _mats)
            {
                if (m == null) continue;
                m.SetVector(ProtoShaderIds._ClearCenter, c);
                m.SetFloat(ProtoShaderIds._ClearRadius,  radius);
                m.SetFloat(ProtoShaderIds._ClearFeather, feather);
            }
        }

        /// 플레이어가 없을 때(에디터·컷신)의 폴백 — 카메라가 보는 화면 중앙의 지면.
        static Vector3 ScreenCenterOnGround(Camera cam, Vector3 fallback)
        {
            var t = cam.transform;
            float down = -t.forward.y;
            if (down < 0.001f) return fallback;         // 카메라가 수평 이상을 보고 있다
            return t.position + t.forward * (t.position.y / down);
        }

        void ApplyColor()
        {
            if (_mats == null) return;

            Color c = followSceneFogColor ? RenderSettings.fogColor : color;
            c = new Color(c.r * brightness, c.g * brightness, c.b * brightness, 1f);
            if (c == _appliedColor) return;
            _appliedColor = c;

            foreach (var m in _mats)
                if (m != null) m.SetColor(ProtoShaderIds._Color, c);
        }

        /// XZ 평면에 놓인 쿼드 한 장. 법선은 +Y (셰이더는 Cull Off 라 뒤집혀도 보이긴 한다).
        static Mesh BuildQuad(float half)
        {
            var mesh = new Mesh { name = "GroundFogQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3( half, 0f, -half),
                new Vector3(-half, 0f,  half),
                new Vector3( half, 0f,  half),
            };
            mesh.normals  = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            // 시트는 항상 카메라를 따라오므로 컬링당하면 안 된다.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(half * 2f, 10f, half * 2f));
            return mesh;
        }
    }
}
