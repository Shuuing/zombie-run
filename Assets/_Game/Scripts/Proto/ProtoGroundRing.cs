using UnityEngine;

namespace Proto
{
    /// 발밑 링 인디케이터 (GDD §15: 대비를 캐릭터에 몰아준다).
    ///
    /// 3D 사선 카메라에서 좀비 더미에 묻히면 캐릭터가 아예 안 보인다. 링은 깊이 테스트를
    /// 무시하는 오버레이 셰이더로 그려서, 몸이 가려져도 내 위치는 항상 읽히게 한다.
    /// 링 메시는 코드로 생성한다 — 텍스처도 프리팹도 필요 없다.
    public class ProtoGroundRing : MonoBehaviour
    {
        [Header("모양")]
        public float radius    = 0.62f;
        public float thickness = 0.13f;
        public int   segments  = 48;
        public float height    = 0.06f;   // 지면에서 살짝 띄워 Z파이팅을 피한다

        [Header("색")]
        public Color normalColor = new Color(0.30f, 1.00f, 0.45f, 0.95f);
        public Color hitColor    = new Color(1.00f, 0.25f, 0.20f, 1.00f);
        public Color lowHPColor  = new Color(1.00f, 0.65f, 0.15f, 1.00f);
        [Tooltip("이 비율 이하면 경고색으로 맥동한다")]
        public float lowHPRatio = 0.3f;

        [Header("렌더 순서")]
        // 링은 ZTest Always 라서 그대로 두면 좀비뿐 아니라 캐릭터까지 덮어버린다.
        // 캐릭터 머티리얼의 렌더 큐를 링보다 1 높이면 순서가 [좀비] → [링] → [캐릭터] 가 되어
        // 링은 좀비를 관통해 보이면서 캐릭터는 링에 가려지지 않는다.
        // 캐릭터는 계속 일반 깊이 테스트(LEqual)를 하므로 앞에 선 좀비에는 정상적으로 가려진다.
        [Tooltip("캐릭터를 링 위에 그린다. 끄면 링이 캐릭터를 덮는다.")]
        public bool drawPlayerAboveRing = true;

        ProtoPlayer _player;
        Material _mat;
        MeshRenderer _mr;

        void Awake()
        {
            _player = GetComponentInParent<ProtoPlayer>();

            var mesh = BuildRing(radius, thickness, segments);
            // Unity 오브젝트에는 ?? 를 쓰면 안 된다 — GetComponent 는 컴포넌트가 없을 때
            // C# 기준 null 이 아닌 '가짜 null' 을 돌려주므로 ?? 가 AddComponent 를 건너뛴다.
            var mf = gameObject.GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            _mr = gameObject.GetComponent<MeshRenderer>();
            if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;

            var shader = Shader.Find("Proto/OverlayUnlit");
            if (shader == null)
            {
                Debug.LogWarning("[GroundRing] Proto/OverlayUnlit 셰이더를 찾지 못했다 — 링이 프롭에 가려질 수 있다");
                shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            }
            _mat = new Material(shader);
            SetColor(normalColor);
            _mr.sharedMaterial = _mat;

            transform.localPosition = new Vector3(0f, height, 0f);
            transform.localRotation = Quaternion.identity;
        }

        // Awake 가 아니라 Start 에서 올린다 — WeaponSocket 이 Awake 에서 무기를 생성하므로
        // Awake 시점에는 무기 렌더러가 아직 없어서 무기만 링에 덮이게 된다.
        void Start()
        {
            if (drawPlayerAboveRing) RaisePlayerQueue();
        }

        /// 캐릭터 머티리얼을 링보다 뒤에 그리게 한다.
        /// r.materials 는 인스턴스를 만들므로 프로젝트의 공유 머티리얼 에셋은 건드리지 않는다.
        ///
        /// <b>캐릭터를 갈아끼우면 반드시 다시 불러야 한다</b> — 새 렌더러는 큐가 올라가 있지 않아
        /// 링에 덮여 캐릭터가 사라진 것처럼 보인다. 여러 번 불려도 결과는 같다(멱등).
        public void RaisePlayerQueue()
        {
            if (_player == null) return;
            RaiseQueueOf(_player.gameObject);
        }

        /// <summary>
        /// root 아래 캐릭터 머티리얼을 링 뒤(= 플레이어와 같은 큐)로 올린다. 탱크·인트로 비행기·낙하산이 쓴다 —
        /// 안 올리면 본체 옆에 서 있을 때 링에 덮이고, 밤 씬에서는 어둠 막(큐 2900)에 덮여 혼자 어둡게 나온다.
        /// </summary>
        public void RaiseQueueOf(GameObject root)
        {
            if (root == null || _mat == null) return;
            int q = _mat.renderQueue + 1;
            int changed = 0;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == _mr) continue;                 // 링 자신은 제외

                // 파티클은 건너뛴다. 화염 제트(_FlameCone/Jet)도 Player 자식이라 여기 걸리는데,
                // r.materials 가 **머티리얼 사본을 만든다** — 캐릭터를 교체할 때마다 불 머티리얼
                // 인스턴스가 하나씩 쌓인다. 게다가 파티클은 자기 정렬 규칙이 따로 있어서
                // 큐를 강제로 올릴 이유도 없다. (이 함수의 목적은 '캐릭터가 링에 안 덮이게' 다.)
                if (r is ParticleSystemRenderer) continue;

                var mats = r.materials;                  // 인스턴스 사본
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    mats[i].renderQueue = q;
                    changed++;
                }
                r.materials = mats;
            }
            Debug.Log($"[GroundRing] 링 queue={_mat.renderQueue}, 캐릭터 머티리얼 {changed}개를 queue={q} 로 올림");
        }

        void LateUpdate()
        {
            if (_player == null) return;

            // 부모가 회전해도 링은 항상 지면에 평평하게 눕는다
            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(_player.transform.position.x, height, _player.transform.position.z);

            if (!_player.IsAlive) { _mr.enabled = false; return; }
            _mr.enabled = true;

            if (_player.IsInvulnerable)
            {
                // 피격 직후 무적 프레임 동안 적색 점멸 — 맞았다는 신호가 링에서 바로 읽힌다
                bool on = Mathf.Repeat(Time.time * 14f, 1f) < 0.5f;
                SetColor(on ? hitColor : normalColor);
                return;
            }

            float ratio = _player.maxHP > 0f ? _player.hp / _player.maxHP : 1f;
            if (ratio <= lowHPRatio)
            {
                float t = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                SetColor(Color.Lerp(normalColor, lowHPColor, t));
            }
            else SetColor(normalColor);
        }

        void SetColor(Color c)
        {
            if (_mat == null) return;
            if (_mat.HasProperty(ProtoShaderIds._BaseColor)) _mat.SetColor(ProtoShaderIds._BaseColor, c);
            else _mat.color = c;
        }

        /// XZ 평면에 눕는 고리(annulus) 메시
        static Mesh BuildRing(float radius, float thickness, int segments)
        {
            segments = Mathf.Max(8, segments);
            float rIn  = Mathf.Max(0.01f, radius - thickness * 0.5f);
            float rOut = radius + thickness * 0.5f;

            var verts = new Vector3[segments * 2];
            var tris  = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                verts[i * 2 + 0] = new Vector3(ca * rIn,  0f, sa * rIn);
                verts[i * 2 + 1] = new Vector3(ca * rOut, 0f, sa * rOut);
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2, i1 = i * 2 + 1;
                int j0 = ((i + 1) % segments) * 2, j1 = ((i + 1) % segments) * 2 + 1;
                int t = i * 6;
                tris[t + 0] = i0; tris[t + 1] = i1; tris[t + 2] = j1;
                tris[t + 3] = i0; tris[t + 4] = j1; tris[t + 5] = j0;
            }

            var mesh = new Mesh { name = "GroundRing" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
