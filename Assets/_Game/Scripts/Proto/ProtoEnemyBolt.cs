using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 원거리 좀비(<see cref="ZombieKind.Ranged"/>)가 던지는 독 구슬 — 프로토.
    ///
    /// <para><b>피할 수 있어야 한다</b>가 설계의 전부다. 쏘는 순간 플레이어가 있던 자리로 곧게 날아가고
    /// (유도 없음), 속도(6.5)가 플레이어 이동속도(4.2)의 1.5배라 옆으로 비키면 빗나간다.
    /// 시전 동작(손을 뒤로 젖혔다 내미는 1.2초)과 손에 모이는 구슬이 예고 역할을 한다.</para>
    ///
    /// <para>Unity 물리는 쓰지 않는다(GDD §16) — 평면 거리로 맞힘을 보고, 프롭에는
    /// ProtoObstacles.Resolve 가 밀어낼 때 막힌 것으로 본다. 풀은 이 클래스가 정적으로 들고 있다.</para>
    ///
    /// 밤 어둠 막(큐 2900)보다 위에 그린다(3100) — 어두운 화면에서도 날아오는 게 보여야 피한다.
    /// </summary>
    public class ProtoEnemyBolt : MonoBehaviour
    {
        const float Life        = 2.6f;     // 6.5 m/s × 2.6 = 약 17m — 화면에 한 번도 안 보인 구슬의 수명(예전 규칙)
        // 빗나간 구슬은 화면 밖으로 나가서 사라진다(2026-11-12 사용자: "안 맞으면 딱지처럼 남는다 — 화면 밖으로 지나가며 자연스럽게").
        // 한 번 화면에 보인 구슬은 Life 대신 화면 밖으로 나갈 때까지 날고(최대 MaxLife), 나간 뒤 꼬리가 마저 빠져나갈 LeaveTail 만큼 더 날다 거둔다.
        const float MaxLife     = 6f;
        const float LeaveTail   = 0.35f;    // 혼불탄 꼬리 0.3초보다 길게
        const float ViewMargin  = 0.04f;    // 뷰포트 밖 여유(화면 폭 비율) — 구슬 머리가 다 나간 뒤
        const float HitRadius   = 0.42f;    // 플레이어 몸 반경 + 구슬 반경
        const float PopTime     = 0.18f;    // 맞거나 막혔을 때 터지는 연출
        const float Size        = 0.55f;
        static readonly Color CoreCol  = new Color(0.62f, 1f, 0.28f, 1f);
        static readonly Color TrailCol = new Color(0.40f, 0.95f, 0.20f, 0.75f);

        static readonly List<ProtoEnemyBolt> _all  = new List<ProtoEnemyBolt>();
        static readonly Stack<ProtoEnemyBolt> _free = new Stack<ProtoEnemyBolt>();
        static Material _mat, _trailMat;
        static Mesh _quad;
        static Transform _root;

        Vector3 _vel;
        float _damage, _dieAt, _popUntil, _born;
        bool _popping, _seen, _leaving;
        Transform _core;
        TrailRenderer _trail;
        MeshRenderer _mr;
        MaterialPropertyBlock _mpb;
        FxHandle _art;   // 카탈로그 poison_bolt 프리팹(있으면 코드 구슬·꼬리 대신, 구슬을 따라간다)

        // ── 풀 ────────────────────────────────────────────────────────
        public static void Fire(Vector3 from, Vector3 dir, float speed, float damage)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            var b = _free.Count > 0 ? _free.Pop() : Create();
            if (b == null) return;
            b.Launch(from, dir.normalized * speed, damage);
        }

        /// 판을 다시 시작할 때 날아가던 구슬을 전부 거둔다.
        public static void ClearAll()
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].gameObject.activeSelf) _all[i].Recycle();
        }

        /// 시전 중 손에 모이는 구슬(예고) — 구슬과 같은 머티리얼·모양. 위치·크기는 좀비가 매 프레임 정한다.
        public static Transform CreateChargeOrb(Transform parent)
        {
            EnsureAssets();
            var go = new GameObject("ChargeOrb");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(ColorID, CoreCol);
            mr.SetPropertyBlock(mpb);
            go.SetActive(false);
            return go.transform;
        }

        static ProtoEnemyBolt Create()
        {
            if (_root == null) _root = new GameObject("_EnemyBolts").transform;
            EnsureAssets();
            var go = new GameObject("Bolt");
            go.transform.SetParent(_root, false);
            var b = go.AddComponent<ProtoEnemyBolt>();

            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            core.AddComponent<MeshFilter>().sharedMesh = _quad;
            b._mr = core.AddComponent<MeshRenderer>();
            b._mr.sharedMaterial = _mat;
            b._mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b._mr.receiveShadows = false;
            b._core = core.transform;

            b._trail = go.AddComponent<TrailRenderer>();
            b._trail.sharedMaterial = _trailMat;
            b._trail.time = 0.22f;
            b._trail.minVertexDistance = 0.08f;
            b._trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.36f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(TrailCol, 0f), new GradientColorKey(TrailCol, 1f) },
                      new[] { new GradientAlphaKey(TrailCol.a, 0f), new GradientAlphaKey(0f, 1f) });
            b._trail.colorGradient = g;
            b._trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b._trail.receiveShadows = false;
            b._trail.alignment = LineAlignment.View;

            b._mpb = new MaterialPropertyBlock();
            _all.Add(b);
            return b;
        }

        static void EnsureAssets()
        {
            if (_mat != null) return;
            // 이펙트 셰이더 (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            _mat = ProtoFxMat.New("EnemyBolt", BuildOrb(64), 3100);
            _trailMat = ProtoFxMat.New("EnemyBoltTrail", null, 3099);

            _quad = new Mesh { name = "BoltQuad" };
            _quad.vertices  = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            _quad.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
        }

        /// 가운데가 하얗게 타는 구슬 — 색은 정점색(MPB 의 색 — 이펙트 셰이더의 _BaseColor, ProtoFxMat.ColorId)으로 입힌다.
        static Texture2D BuildOrb(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;               // 0 중심 → 1 가장자리
                    float a = Mathf.Clamp01((1f - r) * 3f);                       // 테두리만 부드럽게
                    float white = Mathf.Clamp01(1f - r * 1.8f);                   // 속은 흰빛
                    byte c = (byte)(Mathf.Lerp(0.75f, 1f, white) * 255);
                    px[y * n + x] = new Color32(c, c, c, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        void Launch(Vector3 from, Vector3 vel, float damage)
        {
            transform.position = from;
            // 나는 쪽을 본다 — 프리팹 탄(머리 + 꼬리가 있는 혼불탄 등)은 앵커 회전을 따른다. 코드 구슬은 빌보드라 상관없다
            if (vel.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(vel);
            _vel = vel; _damage = damage;
            _born = Time.time;
            _dieAt = Time.time + Life;
            _popping = false; _seen = false; _leaving = false;
            _core.localScale = Vector3.one * Size;
            SetColor(CoreCol);
            gameObject.SetActive(true);
            _trail.Clear();

            // 모양: 카탈로그에 프리팹이 있으면 그것(scale 1 = 구슬 지름 0.55m), 없으면 코드 구슬 + 꼬리.
            // 맞힘 판정은 어느 쪽이든 이 오브젝트 위치로 한다.
            KillArt();
            bool art = ProtoFx.HasPrefab(FxIds.PoisonBolt);
            _mr.enabled = !art;
            _trail.emitting = !art;
            if (art)
            {
                _art = ProtoFx.Play(FxIds.PoisonBolt, FxContext.On(transform, follow: true), span: true);
                SetArtTrails(true);   // 풀에서 나온 인스턴스의 꼬리는 지난 자리를 기억한다 — 비우고 다시 켠다
            }
        }

        /// 프리팹 구슬을 **바로** 거둔다(ProtoFx.Kill). 예전엔 방출만 멈춰(Stop) 혼불탄 해골 입자(수명 1.5초)가 사라진 자리에
        /// 딱지처럼 떠 있었다. 맞을 땐 점액 튐이 그 자리를 덮고, 빗나갈 땐 화면 밖에서 거두므로 바로 꺼도 보이지 않는다.
        void KillArt()
        {
            if (_art == null) return;
            SetArtTrails(false);
            _art.Kill();
            _art = null;
        }

        /// 프리팹 안의 TrailRenderer(점액 구슬의 꼬리) — 켤 때는 비우고 켜고, 끌 때는 방출만 끈다.
        void SetArtTrails(bool on)
        {
            var go = _art != null ? _art.Instance : null;
            if (go == null) return;
            if (!ReferenceEquals(go, _trailsOf)) { _trailsOf = go; _trails = go.GetComponentsInChildren<TrailRenderer>(true); }   // 인스턴스가 같으면 한 번만 훑는다(2026-11-20)
            foreach (var tr in _trails)
            {
                if (on) tr.Clear();
                tr.emitting = on;
            }
        }

        static int ColorID => ProtoFxMat.ColorId;   // _BaseColor(이펙트 셰이더, 2026-11-23 — 옛 Sprites/Default 의 _Color)
        GameObject _trailsOf;
        TrailRenderer[] _trails;

        void SetColor(Color c)
        {
            _mpb.SetColor(ColorID, c);
            _mr.SetPropertyBlock(_mpb);
        }

        void Recycle()
        {
            KillArt();
            _trail.emitting = false;
            gameObject.SetActive(false);
            _free.Push(this);
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam != null) _core.rotation = cam.transform.rotation;   // 빌보드

            if (_popping)
            {
                float k = 1f - Mathf.Clamp01((_popUntil - Time.time) / PopTime);
                _core.localScale = Vector3.one * Size * (1f + k * 1.6f);
                var c = CoreCol; c.a = 1f - k; SetColor(c);
                if (k >= 1f) Recycle();
                return;
            }

            float dt = ProtoConfig.Delta;
            if (dt <= 0f) return;   // 레벨업 정지 중엔 공중에 멈춰 있는다
            if (Time.time >= _dieAt) { Recycle(); return; }

            Vector3 next = transform.position + _vel * dt;
            // 프롭에 막히면 터진다 — 밀려난 거리가 크면 벽 안으로 들어간 것.
            // 플레이어와 같은 규칙(ResolveMove)을 쓴다 — ProtoObstacles.propsPassable 로 프롭을 통과시키면 투사체도 같이 통과한다
            // (안 그러면 건물 안에 들어간 플레이어를 못 맞힌다)
            var obs = ProtoObstacles.I;
            if (obs != null)
            {
                Vector3 flat = new Vector3(next.x, 0f, next.z);
                Vector3 res = obs.ResolveMove(flat, 0.12f, 1);
                if ((res - flat).sqrMagnitude > 0.01f) { transform.position = next; Pop(); return; }
            }
            transform.position = next;
            // 살짝 맥동 — 정지 화면에서도 "살아 있는 위험"으로 읽히게
            _core.localScale = Vector3.one * Size * (1f + 0.12f * Mathf.Sin(Time.time * 22f));

            // 화면 밖으로 나가면 거둔다 — 한 번 보인 구슬은 화면을 벗어날 때까지 날고(최대 MaxLife),
            // 벗어나면 꼬리 방출을 끄고 LeaveTail 만큼 더 날아 꼬리까지 화면 밖으로 빠진 뒤 사라진다
            if (!_leaving && cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(next);
                bool inside = vp.z > 0f && vp.x > -ViewMargin && vp.x < 1f + ViewMargin && vp.y > -ViewMargin && vp.y < 1f + ViewMargin;
                if (inside)
                {
                    if (!_seen) { _seen = true; _dieAt = Mathf.Max(_dieAt, _born + MaxLife); }
                }
                else if (_seen)
                {
                    _leaving = true;
                    _dieAt = Time.time + LeaveTail;
                    _trail.emitting = false;
                    SetArtTrails(false);
                }
            }

            var run = ProtoRun.I;
            var player = run != null ? run.player : null;
            if (player == null || !run.IsRunning) return;
            Vector3 d = player.transform.position - next; d.y = 0f;
            float reach = ProtoTank.PlayerRiding ? ProtoTank.HitRadius : HitRadius;   // 탑승 중엔 차체가 맞는다
            if (d.sqrMagnitude <= reach * reach)
            {
                player.TakeDamage(_damage, ProtoPlayer.DamageKind.Poison);   // 초록 숫자 · 초록 번쩍임 · 몸 피격 이펙트
                ProtoAudio.Play("hit", next, 0.8f);
                Pop();
            }
        }

        void Pop()
        {
            _popping = true;
            _popUntil = Time.time + PopTime;
            _trail.emitting = false;
            // 프리팹 구슬은 맞거나 막히는 순간 바로 거두고, 그 자리에 점액 튐(poison_bolt_hit — 프리팹이 있을 때만 나온다)
            if (_art != null) ProtoFx.Play(FxIds.PoisonBoltHit, transform.position, Vector3.zero);
            KillArt();
        }
    }
}
