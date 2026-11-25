using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 쏴서 터뜨리는 버려진 승합차 (사용자 요청 2026-10-13, 프로토 한정 — 기획서에 없는 기능).
    ///
    /// 씬의 _Props/Van_Abandoned 아래 인스턴스를 전부 폭발물로 본다. 씬 배선이 없다 —
    /// ProtoRun 이 Ensure() 로 깨우면 스스로 훑는다(배경 스트리밍처럼 차가 생기고 사라지는 씬을 위해 주기적으로 다시 훑는다).
    ///
    /// 흐름: 총알·로켓·화염에 맞음 → 머리 위 HP 바가 줄고, 절반 아래면 지붕에서 불 → 0 이면 폭발하고 **사라진다**
    /// → RespawnAfter 초 뒤 같은 자리에 다시 생긴다(플레이어가 그 자리에 서 있으면 비킬 때까지 기다린다).
    /// 폭발은 반경 안 좀비에게 큰 피해 + 그을음, 반경 안의 다른 차는 잠깐 뒤 연쇄 폭발.
    /// 사라진 동안은 길을 막지 않는다(ProtoObstacles 를 다시 짓는다 — 꺼진 프롭은 건너뛴다).
    /// 플레이어는 폭발 피해를 받지 않는다(프로토 결정 — 바꾸려면 PlayerDamage).
    ///
    /// 자동 조준: 좀비가 차 주변에 MinZombiesToTarget 마리 이상 모이면 그 차를 우선 노린다
    /// (ProtoShooter 가 PreferredTarget 을 묻는다). 조준과 상관없이 날아가던 총알이 차에 맞아도 피해가 들어간다.
    public class ProtoDestructibles : MonoBehaviour
    {
        // ── 튜닝 ─────────────────────────────────────────────────────
        // 120 이었다 — 너무 금방 터진다(사용자 2026-10-13). 소총 12 → 34발(약 12초), 캐논 45 → 9발
        public const float VanHP          = 400f;
        public const float BlastRadius    = 5.0f;
        /// 폭발 프리팹(FX_Car_Explosion) 배율 — 승합차가 기준(탱크 1.7 · 헬기 추락 2.2 는 이것에 견준 값).
        /// 11-13 처음 1 은 좀비 처치 폭발만 해서 "안 보인다" → 2(불덩이가 판정 반경 5m 의 절반쯤)
        public const float BlastFxScale   = 2.0f;
        public const float BlastDamage    = 200f;   // 잡좀비는 확실히, 중간 보스(약 870)는 1/4
        public const float PlayerDamage   = 0f;     // 0 = 플레이어는 안 다친다
        public const float ChainDelay     = 0.18f;  // 옆 차가 연쇄로 터지기까지
        public const float RespawnAfter   = 25f;    // 터진 뒤 다시 생기기까지(초)
        public const int   MinZombiesToTarget = 3;  // 차 주변에 이만큼 모이면 자동 조준이 차를 노린다
        const float BurnAfter   = 3f;               // 터진 자리에서 불이 남는 시간
        const float RescanEvery = 2f;
        const float PopTime     = 0.35f;            // 재생성 때 커지며 나타나는 시간

        // HP 바 — 차 지붕 위, 카메라를 본다
        const float BarWidth = 2.2f, BarHeight = 0.22f, BarLift = 0.6f;
        static readonly Color BarBack = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color BarFull = new Color(0.45f, 0.95f, 0.35f, 1f);
        static readonly Color BarMid  = new Color(1f, 0.82f, 0.2f, 1f);
        static readonly Color BarLow  = new Color(1f, 0.25f, 0.15f, 1f);

        public static ProtoDestructibles I { get; private set; }

        class Van
        {
            public Transform tf;
            public Vector3 center;          // XZ 중심(지면)
            public Vector3 axis;            // 길이 방향(수평 단위 벡터)
            public float halfLen, halfWid, height;
            public float hp;
            public bool dead;
            public float diedAt, chainAt = -1f, bornAt = -99f;
            public Vector3 homePos, homeScale; public Quaternion homeRot;
            public Renderer[] rends;
            public float fireCarry;
            public Transform barBack, barFill;
            public MeshRenderer barFillR;
        }

        readonly List<Van> _vans = new List<Van>();
        readonly HashSet<Transform> _known = new HashSet<Transform>();
        float _nextScan;
        MaterialPropertyBlock _mpb;
        ParticleSystem _fire;
        ParticleSystem.EmitParams _ep;
        Material _barBackMat, _barFillMat;
        Mesh _barCentered, _barLeft;

        public static void Ensure()
        {
            if (I != null) return;
            new GameObject("_Destructibles").AddComponent<ProtoDestructibles>();
        }

        void Awake()
        {
            I = this;
            _mpb = new MaterialPropertyBlock();
            var fire = Resources.Load<GameObject>(ProtoBurnFx.FxPath);
            if (fire != null)
            {
                var go = Instantiate(fire, transform);
                go.name = "VanFire";
                _fire = go.GetComponentInChildren<ParticleSystem>(true);
                if (_fire != null) _fire.Play();
            }
            _ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };

            // HP 바: 밤 어둠 막(2900) 위에 그린다
            var sprite = Shader.Find("Sprites/Default");
            _barBackMat = new Material(sprite) { renderQueue = 3150, color = BarBack };
            _barFillMat = new Material(sprite) { renderQueue = 3151, color = Color.white };
            _barCentered = Quad(-0.5f, 0.5f);
            _barLeft     = Quad(0f, 1f);       // 피벗이 왼쪽 끝 — 가로 배율만으로 줄어든다
            Scan();
        }

        void OnDestroy() { if (I == this) I = null; }

        bool HasVan(Transform t)
        {
            for (int i = 0; i < _vans.Count; i++) if (_vans[i].tf == t) return true;
            return false;
        }

        void Scan()
        {
            _nextScan = Time.time + RescanEvery;
            for (int i = _vans.Count - 1; i >= 0; i--)
                if (_vans[i].tf == null) { DestroyBar(_vans[i]); _vans.RemoveAt(i); }
            var props = GameObject.Find("_Props");
            if (props == null) return;
            var group = props.transform.Find("Van_Abandoned");
            if (group == null) return;
            for (int ci = 0; ci < group.childCount; ci++)   // 예전 foreach(Transform)·Exists(람다)는 훑을 때마다 할당(2026-11-20)
            {
                var t = group.GetChild(ci);
                if (_known.Contains(t) && HasVan(t)) continue;
                var v = Make(t);
                if (v != null) { _vans.Add(v); _known.Add(t); }
            }
        }

        /// 메시 바운드로 차의 길이·폭 방향을 잰다. 루트 회전이 모델마다 달라 월드에서 판단한다.
        static Van Make(Transform t)
        {
            // 씬 처리 단계에서 미리 재 둔 발자국이 있으면 그걸 쓴다(ProtoFootprint — 폰은 시작부터 메시가 합쳐져 있다).
            // 축 규약은 유니티 Euler Y 와 같다: half.x 축 = (cos, 0, -sin), half.y 축 = (sin, 0, cos).
            // (2026-11-10 까지 (cos, sin)/(-sin, cos) 로 잡아 비스듬히 선 차의 총알 판정·폭발 불꽃 자리가 반대로 돌아가 있었다.)
            var fpc = t.GetComponent<ProtoFootprint>();
            if (fpc != null)
            {
                bool xLong = fpc.half.x >= fpc.half.y;
                Vector3 ax2 = xLong ? new Vector3(Mathf.Cos(fpc.yaw), 0f, -Mathf.Sin(fpc.yaw)) : new Vector3(Mathf.Sin(fpc.yaw), 0f, Mathf.Cos(fpc.yaw));
                return new Van
                {
                    tf = t, center = new Vector3(fpc.center.x, 0f, fpc.center.y), axis = ax2,
                    halfLen = Mathf.Max(fpc.half.x, fpc.half.y), halfWid = Mathf.Min(fpc.half.x, fpc.half.y), height = fpc.height,
                    hp = VanHP, homePos = t.position, homeRot = t.rotation, homeScale = t.localScale,
                    rends = t.GetComponentsInChildren<Renderer>(true),
                };
            }

            var mf = t.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return null;
            // 플레이 도중 정적 배칭이 메시를 "Combined Mesh" 로 바꿔 끼우면 그 바운드는 도시 한 구역 크기다
            // (ProtoObstacles 순간이동 사고와 같은 원인). 그때는 렌더러의 월드 바운드로 대신 잰다 — 덜 정확하지만 안전하다.
            if (mf.sharedMesh.name.StartsWith("Combined Mesh"))
            {
                var r = t.GetComponentInChildren<Renderer>(true);
                if (r == null) return null;
                var wb = r.bounds;
                bool alongX = wb.extents.x >= wb.extents.z;
                Vector3 cc = wb.center; cc.y = 0f;
                return new Van
                {
                    tf = t, center = cc, axis = alongX ? Vector3.right : Vector3.forward,
                    halfLen = Mathf.Max(wb.extents.x, wb.extents.z), halfWid = Mathf.Min(wb.extents.x, wb.extents.z), height = wb.size.y,
                    hp = VanHP, homePos = t.position, homeRot = t.rotation, homeScale = t.localScale,
                    rends = t.GetComponentsInChildren<Renderer>(true),
                };
            }
            var b = mf.sharedMesh.bounds;
            var m = mf.transform;
            Vector3 ax = m.TransformVector(new Vector3(b.extents.x, 0, 0));
            Vector3 ay = m.TransformVector(new Vector3(0, b.extents.y, 0));
            Vector3 az = m.TransformVector(new Vector3(0, 0, b.extents.z));
            // 수직 성분이 가장 큰 축이 높이, 나머지 둘 중 긴 쪽이 길이
            Vector3[] axes = { ax, ay, az };
            int up = 0; for (int i = 1; i < 3; i++) if (Mathf.Abs(axes[i].y) > Mathf.Abs(axes[up].y)) up = i;
            Vector3 h1 = axes[(up + 1) % 3], h2 = axes[(up + 2) % 3];
            h1.y = 0; h2.y = 0;
            Vector3 lenV = h1.sqrMagnitude >= h2.sqrMagnitude ? h1 : h2;
            Vector3 widV = h1.sqrMagnitude >= h2.sqrMagnitude ? h2 : h1;
            Vector3 c = m.TransformPoint(b.center); c.y = 0f;
            return new Van
            {
                tf = t, center = c, axis = lenV.normalized,
                halfLen = lenV.magnitude, halfWid = widV.magnitude, height = Mathf.Abs(axes[up].y) * 2f,
                hp = VanHP, homePos = t.position, homeRot = t.rotation, homeScale = t.localScale,
                rends = t.GetComponentsInChildren<Renderer>(true),
            };
        }

        // ── 판정 ─────────────────────────────────────────────────
        /// p 에서 반경 r 이 살아 있는 차 몸통(수평 사각형)에 닿는가. 높이는 무시 — 총알은 허리 높이로 난다.
        Van Hit(Vector3 p, float r)
        {
            for (int i = 0; i < _vans.Count; i++)
            {
                var v = _vans[i];
                if (v.dead || v.tf == null) continue;
                Vector3 d = p - v.center; d.y = 0f;
                float along = Vector3.Dot(d, v.axis);
                Vector3 side = d - v.axis * along;
                if (Mathf.Abs(along) <= v.halfLen + r && side.magnitude <= v.halfWid + r) return v;
            }
            return null;
        }

        /// 투사체가 차에 맞았나. 맞았으면 피해를 넣고 true — 총알은 거기서 멈춘다(차는 관통하지 않는다).
        public static bool ProjectileHit(Vector3 p, float r, float damage)
        {
            if (I == null) return false;
            var v = I.Hit(p, r);
            if (v == null) return false;
            I.Damage(v, damage, p);
            return true;
        }

        /// 폭발(로켓 등) 반경 안의 차에 피해.
        public static void DamageInRadius(Vector3 c, float radius, float damage)
        {
            if (I == null) return;
            foreach (var v in I._vans)
            {
                if (v.dead || v.tf == null) continue;
                Vector3 d = v.center - c; d.y = 0f;
                if (d.magnitude <= radius + v.halfWid) I.Damage(v, damage, v.center);
            }
        }

        /// 화염 부채꼴(꼭짓점 from, 방향 dir) 안의 차에 피해.
        public static void DamageInCone(Vector3 from, Vector3 dir, float range, float cosHalf, float damage)
        {
            if (I == null) return;
            foreach (var v in I._vans)
            {
                if (v.dead || v.tf == null) continue;
                Vector3 d = v.center - from; d.y = 0f;
                float dist = d.magnitude;
                if (dist > range + v.halfLen) continue;
                if (dist > 0.01f && Vector3.Dot(d / dist, dir) < cosHalf - 0.15f) continue;
                I.Damage(v, damage, v.center);
            }
        }

        /// 자동 조준 후보: 사거리 안의 살아 있는 차 중 주변에 좀비가 가장 많이 모인 차(최소 MinZombiesToTarget).
        public static bool PreferredTarget(Vector3 from, float range, out Vector3 aimPoint)
        {
            aimPoint = Vector3.zero;
            if (I == null || ProtoSpawnDirector.I == null) return false;
            var list = ProtoSpawnDirector.I.Active;
            if (list.Count < MinZombiesToTarget) return false;   // 좀비가 모자라면 차를 볼 것도 없다(2026-11-20: 매 프레임 사격이 부른다)
            int best = MinZombiesToTarget - 1;
            bool found = false;
            float rangeSq = range * range;
            foreach (var v in I._vans)
            {
                if (v.dead || v.tf == null) continue;
                Vector3 d = v.center - from; d.y = 0f;
                if (d.sqrMagnitude > rangeSq) continue;
                int n = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 e = z.transform.position - v.center; e.y = 0f;
                    if (e.sqrMagnitude <= BlastRadius * BlastRadius) n++;
                }
                if (n > best) { best = n; aimPoint = v.center; found = true; }
            }
            return found;
        }

        // ── 피해 · 폭발 · 재생성 ─────────────────────────────────
        void Damage(Van v, float amount, Vector3 at)
        {
            if (v.dead) return;
            v.hp -= amount;
            // 맞은 자리에 작은 불똥 — 카탈로그 van_hit(기본 = 총구 섬광 메시를 작게 재사용)
            ProtoFx.Play(FxIds.VanHit, new Vector3(at.x, Mathf.Clamp(at.y, 0.6f, 1.4f), at.z), Vector3.up);
            if (v.hp <= 0f) Explode(v);
        }

        void Explode(Van v)
        {
            if (v.dead) return;
            v.dead = true; v.diedAt = Time.time; v.hp = 0f;
            Vector3 c = v.center;

            // 카탈로그 van_blast — 프리팹 FX_Car_Explosion(2026-11-13, 바닥에 BlastFxScale 배) + 흔들림(Huge) · 폭발음 · 풀은 ImpactFeel.
            // 프리팹을 비우면 예전 코드 그림(보스 내려찍기 연출 재사용: 섬광·화구·충격파·파편·불씨·균열·연기·흔들림·폭발음)
            VehicleBlastFx.Play(FxIds.VanBlast, c, BlastRadius, BlastFxScale, () => ProtoBossFx.ImpactFeel(c, BlastRadius, true));

            // 좀비 피해 — 폭심에서 밖으로 밀어낸다
            var sd = ProtoSpawnDirector.I;
            if (sd != null)
            {
                var list = sd.Active;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 d = z.transform.position - c; d.y = 0f;
                    if (d.magnitude > BlastRadius) continue;
                    z.ApplyBlastSoot();
                    z.TakeDamage(BlastDamage, (d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward) * 2.5f, false, true, HitFx.Blast(null));
                }
            }
            if (PlayerDamage > 0f && ProtoRun.I != null && ProtoRun.I.player != null)
            {
                Vector3 d = ProtoRun.I.player.transform.position - c; d.y = 0f;
                if (d.magnitude <= BlastRadius) ProtoRun.I.player.TakeDamage(PlayerDamage);
            }

            // 연쇄 — 반경 안 다른 차는 잠깐 뒤에 터진다(동시에 터지면 연쇄로 안 읽힌다)
            foreach (var o in _vans)
            {
                if (o == v || o.dead || o.tf == null || o.chainAt > 0f) continue;
                Vector3 d = o.center - c; d.y = 0f;
                if (d.magnitude <= BlastRadius + o.halfWid) o.chainAt = Time.time + ChainDelay;
            }

            // 버프 아이템 하나 (HP·자석·아드레날린·보호막 — ProtoPickups)
            ProtoPickups.DropFromVan(c);

            // 차는 사라진다 — 파편과 불길만 남는다. 길도 열린다.
            v.tf.gameObject.SetActive(false);
            SetBar(v, false);
            ProtoObstacles.I?.Rebuild(false);
        }

        /// 같은 자리에 다시 생긴다. 플레이어가 그 자리에 서 있으면 겹쳐 끼이므로 미룬다.
        bool TryRespawn(Van v)
        {
            if (ProtoRun.I != null && ProtoRun.I.player != null)
            {
                Vector3 d = ProtoRun.I.player.transform.position - v.center; d.y = 0f;
                float along = Vector3.Dot(d, v.axis);
                Vector3 side = d - v.axis * along;
                if (Mathf.Abs(along) <= v.halfLen + 1.2f && side.magnitude <= v.halfWid + 1.2f) return false;
            }
            v.dead = false; v.hp = VanHP; v.chainAt = -1f; v.bornAt = Time.time;
            v.tf.SetPositionAndRotation(v.homePos, v.homeRot);
            v.tf.localScale = v.homeScale * 0.6f;
            foreach (var r in v.rends) if (r != null) r.SetPropertyBlock(null);
            v.tf.gameObject.SetActive(true);
            ProtoFx.Play(FxIds.VanAppearDust, v.center, Vector3.zero);   // 먼지와 함께 나타난다 (카탈로그 van_appear_dust)
            ProtoObstacles.I?.Rebuild(false);
            return true;
        }

        void Update()
        {
            if (Time.time >= _nextScan) Scan();
            float now = Time.time, dt = ProtoConfig.Delta;
            var cam = Camera.main;

            foreach (var v in _vans)
            {
                if (v.tf == null) continue;
                if (!v.dead && v.chainAt > 0f && now >= v.chainAt) { v.chainAt = -1f; Explode(v); }

                if (v.dead)
                {
                    float t = now - v.diedAt;
                    if (t < BurnAfter) EmitFire(v, dt, Mathf.Lerp(80f, 10f, t / BurnAfter));   // 터진 자리에 남는 불길
                    if (t >= RespawnAfter && !TryRespawn(v)) v.diedAt += 1f;   // 비키면 1초 뒤 다시 시도
                    continue;
                }

                // 재생성 팝 — 0.6 → 1.08 → 1
                float pt = (now - v.bornAt) / PopTime;
                if (pt < 1f) v.tf.localScale = v.homeScale * (pt < 0.7f ? Mathf.Lerp(0.6f, 1.08f, pt / 0.7f) : Mathf.Lerp(1.08f, 1f, (pt - 0.7f) / 0.3f));
                else if (pt < 1.5f) v.tf.localScale = v.homeScale;

                // 반쯤 부서지면 지붕에서 불이 오른다 — "곧 터진다" 예고
                if (v.hp < VanHP * 0.5f) EmitFire(v, dt, 18f);

                UpdateBar(v, cam);
            }
        }

        // ── HP 바 ─────────────────────────────────────────────────
        void UpdateBar(Van v, Camera cam)
        {
            if (cam == null) return;
            // 화면 밖이면 그리지 않는다 — 22대 전부 매 프레임 굴릴 이유가 없다
            Vector3 top = v.center + Vector3.up * (v.height + BarLift);
            Vector3 vp = cam.WorldToViewportPoint(top);
            bool onScreen = vp.z > 0f && vp.x > -0.1f && vp.x < 1.1f && vp.y > -0.1f && vp.y < 1.1f;
            if (!onScreen) { SetBar(v, false); return; }

            if (v.barBack == null) MakeBar(v);
            SetBar(v, true);
            Quaternion rot = cam.transform.rotation;
            Vector3 right = cam.transform.right, toCam = -cam.transform.forward;
            float ratio = Mathf.Clamp01(v.hp / VanHP);
            v.barBack.SetPositionAndRotation(top, rot);
            v.barBack.localScale = new Vector3(BarWidth + 0.08f, BarHeight + 0.08f, 1f);
            v.barFill.SetPositionAndRotation(top - right * (BarWidth * 0.5f) + toCam * 0.01f, rot);
            v.barFill.localScale = new Vector3(BarWidth * Mathf.Max(0.001f, ratio), BarHeight, 1f);
            Color c = ratio > 0.5f ? Color.Lerp(BarMid, BarFull, (ratio - 0.5f) * 2f) : Color.Lerp(BarLow, BarMid, ratio * 2f);
            _mpb.Clear(); _mpb.SetColor(ProtoShaderIds._Color, c);
            v.barFillR.SetPropertyBlock(_mpb);
        }

        void MakeBar(Van v)
        {
            v.barBack = NewQuad("VanHPBack", _barCentered, _barBackMat).transform;
            var fill = NewQuad("VanHPFill", _barLeft, _barFillMat);
            v.barFill = fill.transform;
            v.barFillR = fill.GetComponent<MeshRenderer>();
        }

        GameObject NewQuad(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        static void SetBar(Van v, bool on)
        {
            if (v.barBack != null && v.barBack.gameObject.activeSelf != on) v.barBack.gameObject.SetActive(on);
            if (v.barFill != null && v.barFill.gameObject.activeSelf != on) v.barFill.gameObject.SetActive(on);
        }

        static void DestroyBar(Van v)
        {
            if (v.barBack != null) Destroy(v.barBack.gameObject);
            if (v.barFill != null) Destroy(v.barFill.gameObject);
        }

        /// x0~x1 가로, 높이 1 의 판. 카메라 회전을 그대로 받으므로 화면을 정면으로 본다.
        static Mesh Quad(float x0, float x1)
        {
            var m = new Mesh { name = "VanHPQuad" };
            m.vertices = new[] { new Vector3(x0, -0.5f, 0), new Vector3(x1, -0.5f, 0), new Vector3(x1, 0.5f, 0), new Vector3(x0, 0.5f, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        void EmitFire(Van v, float dt, float perSecond)
        {
            if (_fire == null) return;
            v.fireCarry += perSecond * dt;
            int n = (int)v.fireCarry; v.fireCarry -= n;
            for (int k = 0; k < n; k++)
            {
                float a = Random.Range(-0.8f, 0.8f), s = Random.Range(-0.7f, 0.7f);
                Vector3 side = Vector3.Cross(Vector3.up, v.axis);
                // 살아 있으면 지붕 위, 터진 뒤에는 바닥(차가 없다)에서 오른다
                float y = v.dead ? Random.Range(0.1f, 0.8f) : v.height * Random.Range(0.6f, 1.0f);
                _ep.position = v.center + v.axis * a * v.halfLen + side * s * v.halfWid + Vector3.up * y;
                _ep.velocity = Vector3.up * Random.Range(1.0f, 2.2f);
                _ep.startSize = Random.Range(0.6f, 1.3f);
                _fire.Emit(_ep, 1);
            }
        }

        /// 재시작 — 모든 차를 원래대로.
        public static void ResetAll()
        {
            if (I == null) return;
            foreach (var v in I._vans)
            {
                if (v.tf == null) continue;
                v.hp = VanHP; v.dead = false; v.chainAt = -1f; v.bornAt = -99f;
                v.tf.SetPositionAndRotation(v.homePos, v.homeRot);
                v.tf.localScale = v.homeScale;
                foreach (var r in v.rends) if (r != null) r.SetPropertyBlock(null);
                v.tf.gameObject.SetActive(true);
            }
            ProtoObstacles.I?.Rebuild(false);
        }
    }
}
