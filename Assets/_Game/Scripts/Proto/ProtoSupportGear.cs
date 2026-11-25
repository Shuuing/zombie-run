using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 보조 장비 — 레벨업 카드로 얻는 <b>자동 공격 장치</b> (회전 블레이드 · 지원 드론 · 수류탄).
    ///
    /// <para><b>왜 주무기에서 떼어냈나.</b> 이 둘은 손에 쥐지도, 조준하지도 않는다. 플레이어가
    /// 사거리를 관리하지 않으므로 나머지 네 무기와 조작 감각이 다른 축에 있다. 캐릭터에 고정하면
    /// "무기를 든 캐릭터"와 "장치가 도는 캐릭터"가 같은 슬롯을 공유하게 되어 §04 의 캐릭터 정의
    /// (주무기 1종 + 패시브 1개)가 흐려진다. 카드로 옮기면 누구나 붙일 수 있고,
    /// 대신 <b>슬롯 상한 2</b> 로 무기 4슬롯 시절로 되돌아가는 것을 막는다.</para>
    ///
    /// <para>에셋은 주무기와 똑같이 <see cref="WeaponModelDef"/>(모델·배율) + <see cref="WeaponStats"/>(수치)
    /// 한 쌍을 쓴다 — 동작은 이미 그 안에 정의돼 있고 <b>얻는 경로만</b> 달라졌기 때문이다.</para>
    /// </summary>
    public class ProtoSupportGear : MonoBehaviour
    {
        /// GDD §07 — 상한이 없으면 결핍이 사라지고 무기 4슬롯 시절로 되돌아간다.
        public const int MaxSlots = 2;

        class Slot
        {
            public WeaponModelDef def;
            public int level = 1;
            public Transform root;
            public readonly List<Transform> parts = new List<Transform>();
            public float angle, nextTick, nextFire;
            public readonly List<Nade> nades = new List<Nade>();   // 날아가는 수류탄 (Grenade)
            // 톱날 꼬리(2026-11-18) — 톱날마다 피벗(돌지 않고 궤도 접선을 본다) + 연출 세트 trail 스팬
            public readonly List<Transform> pivots = new List<Transform>();
            public readonly List<FxHandle> trails = new List<FxHandle>();
            public bool trailsLive;   // 켜 봤다(세트에 꼬리가 없거나 프리팹이 없어도) — 매 프레임 다시 찾지 않게
            public WeaponStats St => def.stats;
        }

        class Nade
        {
            public Transform tf;
            public Vector3 from, to;
            public float t, dur, apex, dmg, radius, knock;
            public bool crit;
            public Vector3 spinAxis;
            public WeaponFxSet fx;   // 던진 무기의 연출 세트(폭발 · 처치)
        }

        readonly List<Slot> _slots = new List<Slot>();
        ProtoPlayer _player;
        ProtoShooter _shooter;

        public int SlotCount => _slots.Count;
        public bool IsFull => _slots.Count >= MaxSlots;

        void Awake()
        {
            _player = GetComponent<ProtoPlayer>();
            _shooter = GetComponent<ProtoShooter>();
        }

        public int LevelOf(WeaponModelDef def)
        {
            var s = Find(def);
            return s != null ? s.level : 0;
        }

        /// 화면 표시용 — "블레이드 Lv2, 드론 Lv1" 같은 요약.
        public string Summary()
        {
            if (_slots.Count == 0) return "없음";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _slots.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var d = _slots[i].def;
                sb.Append(string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName.Split('(')[0].TrimEnd());
                sb.Append(" Lv").Append(_slots[i].level);
            }
            return sb.ToString();
        }

        Slot Find(WeaponModelDef def)
        {
            for (int i = 0; i < _slots.Count; i++) if (_slots[i].def == def) return _slots[i];
            return null;
        }

        /// <summary>카드를 먹었을 때. 처음이면 슬롯을 차지하고, 이미 있으면 레벨만 올린다.</summary>
        public void Acquire(WeaponModelDef def)
        {
            if (def == null || def.stats == null || !def.stats.IsSupportGear)
            {
                Debug.LogError("[보조장비] 보조 장비 에셋이 아니다: " + (def != null ? def.name : "<null>"));
                return;
            }

            var s = Find(def);
            if (s == null)
            {
                if (IsFull) { Debug.LogWarning("[보조장비] 슬롯이 꽉 찼다 — 카드가 후보에 들면 안 된다."); return; }
                s = new Slot { def = def, level = 1 };
                _slots.Add(s);
            }
            else s.level++;

            Rebuild(s);
        }

        public void ClearAll()
        {
            for (int i = 0; i < _slots.Count; i++) Teardown(_slots[i]);
            _slots.Clear();
        }

        void Teardown(Slot s)
        {
            // 꼬리는 궤도 루트 공간에 묶여 있다 — 루트를 없애기 전에 바로 거둔다(남은 띠가 원점으로 튀지 않게)
            StopTrails(s, kill: true);
            DetachAndDestroy(s.root);
            s.root = null;
            s.parts.Clear();
            s.pivots.Clear();
            s.nades.Clear();   // 수류탄은 root 자식이라 같이 사라진다
        }

        /// Destroy 는 프레임 끝에 실행되므로 부모에서 떼어낸 뒤 없앤다.
        /// 안 떼면 레벨업한 프레임 동안 옛 블레이드가 그대로 남아 개수가 두 배로 보인다.
        static void DetachAndDestroy(Transform t)
        {
            if (t == null) return;
            t.SetParent(null, true);
            t.gameObject.SetActive(false);
            Destroy(t.gameObject);
        }

        void Rebuild(Slot s)
        {
            Teardown(s);
            var st = s.St;
            // 수류탄은 몸에 붙는 부품이 없다 — 던질 때마다 만든다. 담아 둘 월드 루트만 둔다.
            // 레벨업해도 다음 투척 시각은 그대로(nextFire 는 Slot 에 남는다).
            if (st.mode == FireMode.Grenade || st.mode == FireMode.HeliCall)
            {
                s.root = new GameObject("_Gear_" + s.def.name).transform;
                return;
            }
            if (s.def.model == null)
            {
                Debug.LogError("[보조장비] " + s.def.name + " 에 모델이 없다.");
                return;
            }

            int n = Mathf.Max(1, st.CountAtLevel(s.level));
            s.root = new GameObject("_Gear_" + s.def.name).transform;
            // 궤도는 플레이어를 따라다녀야 하므로 자식으로, 소환물은 스스로 쫓아오므로 월드에 둔다.
            if (st.mode == FireMode.Orbit) s.root.SetParent(transform, false);

            for (int i = 0; i < n; i++)
            {
                var go = Instantiate(s.def.model, s.root);
                go.name = (st.mode == FireMode.Orbit ? "Blade" : "Drone") + (i + 1);
                go.transform.localScale = go.transform.localScale * s.def.weaponScale;
                if (st.mode != FireMode.Orbit)
                    go.transform.position = transform.position + OffsetFor(st, i, n);
                s.parts.Add(go.transform);
                if (st.mode == FireMode.Orbit)
                {
                    var p = new GameObject("TrailPivot" + (i + 1)).transform;
                    p.SetParent(s.root, false);
                    s.pivots.Add(p);
                }
            }
            // 꼬리는 톱날을 궤도 자리에 놓은 뒤 켠다 — 원점에서 켜면 첫 띠가 플레이어 발밑에서 궤도까지 이어진다
            if (st.mode == FireMode.Orbit) { PlaceOrbit(s); StartTrails(s); }
        }

        /// 소환물이 여러 기일 때 서로 겹치지 않게 플레이어 주위에 벌려 놓는다.
        static Vector3 OffsetFor(WeaponStats st, int i, int n)
        {
            if (n <= 1) return st.companionOffset;
            float a = (360f / n) * i * Mathf.Deg2Rad;
            float r = new Vector2(st.companionOffset.x, st.companionOffset.z).magnitude;
            return new Vector3(Mathf.Sin(a) * r, st.companionOffset.y, -Mathf.Cos(a) * r);
        }

        void Update()
        {
            if (ProtoRun.I == null || !ProtoRun.I.IsRunning) return;
            if (_player == null || !_player.IsAlive)
            {
                // 톱날이 멈춘 채 꼬리만 뿜으면 제자리에 빛 덩어리가 맺힌다 — 방출을 끄고 남은 띠만 사그라들게
                for (int i = 0; i < _slots.Count; i++) StopTrails(_slots[i], kill: false);
                return;
            }

            float dt = ProtoConfig.Delta;
            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (s.root == null) continue;
                if (s.St.mode == FireMode.Orbit) TickOrbit(s, dt);
                else if (s.St.mode == FireMode.Grenade) TickGrenade(s, dt);
                else if (s.St.mode == FireMode.HeliCall) TickHeli(s);
                else TickCompanion(s, dt);
            }
        }

        float DamageOf(Slot s)
        {
            float dmg = s.St.damage * s.St.DamageMultAtLevel(s.level) * _player.damageMult;
            if (Random.value < _player.critChance) dmg *= ProtoConfig.CritMultiplier;
            return dmg;
        }

        float IntervalOf(Slot s) =>
            Mathf.Max(0.03f, s.St.interval / Mathf.Max(0.05f, _player.AtkSpeed));

        // ── 궤도 ────────────────────────────────────────────────────────
        void TickOrbit(Slot s, float dt)
        {
            var st = s.St;

            s.angle += st.orbitRpm * 6f * dt;          // rpm → 초당 각도
            if (s.angle > 360f)
            {
                s.angle -= 360f;
                // 한 바퀴에 한 번 공기를 가르는 소리. **피해 판정(nextTick)이 아니라 회전에**
                // 묶는 이유: 적을 때릴 때만 울리면 톱날이 돌고 있다는 것이 안 들리고,
                // 반대로 판정마다 울리면 물량전에서 쉬익 소리가 끊이지 않는다.
                // 회전수(42rpm → 1.4초마다)에 저절로 맞춰지는 것도 이쪽이다.
                ProtoAudio.Play("blade", transform.position, 0.8f);
            }

            PlaceOrbit(s);
            if (!s.trailsLive) StartTrails(s);   // 쓰러졌다 다시 돌 때 — 세트에 꼬리가 없으면 아무것도 안 한다
            int n = s.parts.Count;

            if (Time.time < s.nextTick) return;
            s.nextTick = Time.time + IntervalOf(s);

            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var list = sd.Active;
            for (int b = 0; b < n; b++)
            {
                if (s.parts[b] == null) continue;
                Vector3 bp = s.parts[b].position;
                for (int i = 0; i < list.Count; i++)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 d = z.transform.position - bp;
                    d.y = 0f;
                    float r = st.orbitHitRadius + Mathf.Max(0f, z.bodyRadius - ProtoConfig.BaseBodyRadius);
                    if (d.sqrMagnitude > r * r) continue;
                    // 바깥으로 밀어낸다 — 궤도 안쪽으로 밀면 플레이어에게 붙는다.
                    Vector3 push = z.transform.position - transform.position; push.y = 0f;
                    // 연출은 블레이드 무기의 세트대로, 자리 = 블레이드가 닿은 곳
                    z.TakeDamage(DamageOf(s), push.sqrMagnitude > 0.0001f ? push.normalized * st.knockbackMult : Vector3.zero,
                                 false, false, HitFx.From(WeaponFxSet.Of(st), bp));
                }
            }
        }

        /// 톱날 · 꼬리 피벗을 지금 각도(s.angle)의 궤도 자리에 놓는다. 각도는 TickOrbit 이 돌린다.
        void PlaceOrbit(Slot s)
        {
            var st = s.St;
            // 플레이어가 조준으로 돌아도 궤도는 월드 기준으로 돈다.
            // 안 그러면 조준 회전이 궤도에 얹혀 블레이드가 순간이동하듯 튄다.
            s.root.rotation = Quaternion.identity;
            int n = s.parts.Count;
            for (int i = 0; i < n; i++)
            {
                var t = s.parts[i];
                if (t == null) continue;
                float a = (s.angle + i * (360f / n)) * Mathf.Deg2Rad;
                var lp = new Vector3(Mathf.Cos(a) * st.orbitRadius, st.orbitHeight, Mathf.Sin(a) * st.orbitRadius);
                t.localPosition = lp;
                // 눕힌 뒤(모델 축 보정) 제자리에서 돌린다. 보정을 나중에 곱하면 축이 같이 돌아
                // 디스크가 회전문처럼 두께를 오간다.
                t.localRotation = Quaternion.Euler(0f, -s.angle * 4f, 0f) * Quaternion.Euler(st.modelRotationOffset);
                if (i < s.pivots.Count && s.pivots[i] != null)
                {
                    // 꼬리 피벗은 톱날처럼 제자리에서 돌지 않는다 — +Z = 궤도 접선(각도가 느는 쪽 = 나아가는 쪽), +Y = 위
                    s.pivots[i].localPosition = lp;
                    s.pivots[i].localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
                }
            }
            // 꼬리를 바로 붙인다 — FX 틱이 이 Update 보다 먼저 돌면 한 프레임 뒤처져 톱날 뒤에서 띠가 떨어진다
            for (int i = 0; i < s.trails.Count; i++) s.trails[i].SnapToAnchor();
        }

        // ── 톱날 꼬리 (2026-11-18) ──────────────────────────────────────
        // 무기 연출 세트의 trail 칸(WFX_OrbitBlade → orbit_blade_trail)을 톱날마다 스팬으로 켠다. 비우면 꼬리 없음.
        // 띠 층(Custom 공간)은 궤도 루트에 묶는다 — 플레이어가 달려도 꼬리가 뒤로 끌려 휘지 않고 궤도 원을 그린다.
        void StartTrails(Slot s)
        {
            s.trailsLive = true;
            if (s.root == null || s.pivots.Count == 0) return;
            var set = WeaponFxSet.Of(s.St);
            if (set.trail == null || !set.trail.On) return;
            for (int i = 0; i < s.pivots.Count; i++)
            {
                if (s.pivots[i] == null) continue;
                var ctx = FxContext.On(s.pivots[i]);
                ctx.scale = set.trail.scale;
                var h = ProtoFx.Play(set.trail.fxId, ctx, span: true);
                if (!h.Alive) return;   // 카탈로그에 프리팹이 없다 — 나머지도 같다
                h.BindSimulationSpace(s.root);
                s.trails.Add(h);
            }
        }

        /// kill = 바로 거둔다(톱날을 다시 만들 때 · 루트를 없앨 때). 아니면 방출만 멈추고 남은 띠가 사그라든다.
        static void StopTrails(Slot s, bool kill)
        {
            for (int i = 0; i < s.trails.Count; i++)
            {
                if (kill) s.trails[i].Kill();
                else s.trails[i].Stop(detach: true);
            }
            s.trails.Clear();
            s.trailsLive = false;
        }

        // ── 소환물 ──────────────────────────────────────────────────────
        void TickCompanion(Slot s, float dt)
        {
            var st = s.St;
            int n = s.parts.Count;
            float lerp = 1f - Mathf.Exp(-Mathf.Max(0.1f, st.companionFollowLerp) * dt);
            bool canFire = Time.time >= s.nextFire;
            bool fired = false;

            for (int i = 0; i < n; i++)
            {
                var t = s.parts[i];
                if (t == null) continue;
                t.position = Vector3.Lerp(t.position, transform.position + OffsetFor(st, i, n), lerp);

                // 소환물은 자기 위치에서 따로 조준한다 (§05 "독립 조준").
                var target = FindTarget(t.position, st.range);
                if (target == null) continue;

                Vector3 to = target.transform.position + Vector3.up * 0.9f - t.position;
                Vector3 flat = new Vector3(to.x, 0f, to.z);
                if (flat.sqrMagnitude > 0.0001f)
                    t.rotation = Quaternion.LookRotation(flat.normalized) * Quaternion.Euler(st.modelRotationOffset);

                if (!canFire || _shooter == null) continue;
                _shooter.FireExternal(st, to.normalized, t.position, DamageOf(s));
                // 여러 기가 동시에 쏘아도 소리는 **한 번**이다. 기수마다 울리면 4기일 때
                // 같은 소리가 네 겹으로 겹쳐 주무기 총성을 덮는다.
                if (!fired) ProtoAudio.Play("drone", t.position, 0.9f);
                fired = true;
            }
            if (fired) s.nextFire = Time.time + IntervalOf(s);
        }

        // ── 헬리콥터 공습 ───────────────────────────────────────────────
        // 쿨타임마다 공격 헬기(ProtoHeliGunship)를 부른다 — 바닥 경고 띠를 따라 지나가며 기총 소사.
        // 쿨타임은 공속을 받지 않는다(45초짜리가 공속 카드로 녹으면 상시 출격이 된다).
        // 첫 출격은 카드를 먹고 1초 뒤 — 고른 카드가 뭘 하는지 바로 보여야 한다.
        void TickHeli(Slot s)
        {
            var st = s.St;
            if (s.nextFire <= 0f) s.nextFire = Time.time + 1f;
            if (Time.time < s.nextFire) return;
            // 근처에 좀비가 없으면 아껴 둔다 — 빈 거리에 쿨타임을 날리면 아깝다
            if (FindTarget(transform.position, st.range) == null) { s.nextFire = Time.time + 0.5f; return; }
            float dmg = st.damage * st.DamageMultAtLevel(s.level);   // 공격력 배율·치명타는 ProtoHeliGunship.Fire 가 곱한다
            ProtoHeliGunship.Call(transform, dmg, st.CountAtLevel(s.level), WeaponFxSet.Of(st));
            ProtoHUD.ShowPickup("헬기 공습!", new Color(1f, 0.79f, 0.29f));
            s.nextFire = Time.time + Mathf.Max(1f, st.interval);
        }

        // ── 수류탄 ──────────────────────────────────────────────────────
        // 간격마다 사거리 안에서 좀비가 가장 많이 모인 곳에 던진다. 여러 개면 서로 다른 무리를 고른다
        // (같은 자리에 두 개 떨어지면 한 개를 버린 셈이다). 포물선으로 날아가 떨어진 자리에서 터진다.
        const float NadeSize = 0.26f;      // 화면(카메라 ~15m 위)에서 읽히는 크기. 실물보다 크다
        const float NadeSpeed = 13f;       // 수평 속도 — 비행 시간 = 거리 / 이 값 (0.35~0.8초로 자른다)
        static Material _nadeMat;

        void TickGrenade(Slot s, float dt)
        {
            var st = s.St;
            for (int i = s.nades.Count - 1; i >= 0; i--)
            {
                var n = s.nades[i];
                n.t += dt;
                float k = Mathf.Clamp01(n.t / n.dur);
                if (n.tf != null)
                {
                    Vector3 p = Vector3.Lerp(n.from, n.to, k);
                    p.y = Mathf.Lerp(n.from.y, n.to.y, k) + n.apex * 4f * k * (1f - k);
                    n.tf.position = p;
                    n.tf.Rotate(n.spinAxis, 720f * dt, Space.World);
                }
                if (k < 1f) continue;
                if (n.tf != null) Destroy(n.tf.gameObject);
                s.nades.RemoveAt(i);
                Blast(n);
            }

            if (Time.time < s.nextFire) return;
            int count = Mathf.Max(1, st.CountAtLevel(s.level));
            float radius = Mathf.Max(0.5f, st.aoeRadius) * st.RadiusMultAtLevel(s.level);
            var picked = new List<Vector3>(count);
            for (int c = 0; c < count; c++)
            {
                if (!PickCluster(st.range, radius, picked, out var at)) break;
                picked.Add(at);
                Throw(s, at, radius);
            }
            // 던질 곳이 없으면 짧게 다시 본다 — 간격을 통째로 기다리면 적이 들어와도 한참 안 던진다
            s.nextFire = Time.time + (picked.Count > 0 ? IntervalOf(s) : 0.25f);
        }

        /// 사거리 안 좀비 중, 반경 안에 이웃이 가장 많은 좀비 자리. 이미 고른 자리 근처(반경 1.5배)는 뺀다.
        bool PickCluster(float range, float radius, List<Vector3> exclude, out Vector3 at)
        {
            at = Vector3.zero;
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return false;
            var list = sd.Active;
            Vector3 me = transform.position;
            float rangeSq = range * range, rSq = radius * radius, exSq = rSq * 2.25f;
            int best = 0; float bestDist = float.MaxValue;
            // 좀비가 수백일 수 있다 — 후보는 앞에서부터 최대 60마리만 본다(이웃 수는 전부 센다)
            int checkedN = 0;
            for (int i = 0; i < list.Count && checkedN < 60; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 p = z.transform.position; p.y = 0f;
                Vector3 dm = p - me; dm.y = 0f;
                float dSq = dm.sqrMagnitude;
                if (dSq > rangeSq) continue;
                bool near = false;
                for (int e = 0; e < exclude.Count; e++) if ((exclude[e] - p).sqrMagnitude < exSq) { near = true; break; }
                if (near) continue;
                checkedN++;
                int cnt = 0;
                for (int j = 0; j < list.Count; j++)
                {
                    var o = list[j];
                    if (o == null || !o.Alive) continue;
                    Vector3 d = o.transform.position - p; d.y = 0f;
                    if (d.sqrMagnitude <= rSq) cnt++;
                }
                // 같은 수면 가까운 쪽 — 멀리 던지면 날아가는 동안 무리가 흩어진다
                if (cnt > best || (cnt == best && dSq < bestDist)) { best = cnt; bestDist = dSq; at = p; }
            }
            return best > 0;
        }

        void Throw(Slot s, Vector3 to, float radius)
        {
            Vector3 from = transform.position + Vector3.up * 1.3f;
            Vector3 flat = to - from; flat.y = 0f;
            float dist = flat.magnitude;
            var n = new Nade
            {
                from = from, to = to + Vector3.up * 0.1f,
                dur = Mathf.Clamp(dist / NadeSpeed, 0.35f, 0.8f),
                apex = 1.6f + dist * 0.18f,
                radius = radius,
                knock = s.St.knockbackMult,
                spinAxis = Random.onUnitSphere,
                fx = WeaponFxSet.Of(s.St),
            };
            n.crit = Random.value < _player.critChance;
            n.dmg = s.St.damage * s.St.DamageMultAtLevel(s.level) * _player.damageMult * (n.crit ? ProtoConfig.CritMultiplier : 1f);
            n.tf = MakeNade(s);
            n.tf.position = from;
            s.nades.Add(n);
        }

        /// 모델이 있으면 그걸, 없으면 임시 구슬(국방색). 충돌체는 없다 — 판정은 착지 지점에서만 한다.
        Transform MakeNade(Slot s)
        {
            GameObject go;
            if (s.def.model != null)
            {
                go = Instantiate(s.def.model, s.root);
                go.transform.localScale = go.transform.localScale * s.def.weaponScale;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(s.root, false);
                go.transform.localScale = new Vector3(NadeSize, NadeSize * 1.25f, NadeSize);
                if (_nadeMat == null)
                {
                    var sh = Shader.Find("BCM/BCM_Toony_shader");
                    if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
                    _nadeMat = new Material(sh) { name = "M_Grenade_Temp" };
                    _nadeMat.SetColor("_BaseColor", new Color(0.30f, 0.36f, 0.18f));
                }
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = _nadeMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            go.name = "Grenade";
            return go.transform;
        }

        /// 착지 폭발 — 로켓 착탄(ProtoProjectile.Explode)과 같은 규칙: 링 테두리 = 판정 반경, 그을음은 피해보다 먼저.
        void Blast(Nade n)
        {
            Vector3 c = n.to; c.y = 0f;
            var set = n.fx != null ? n.fx : WeaponFxSet.Default;
            if (set.explode != null && set.explode.On) ProtoFx.PlayBlast(set.explode.fxId, c, n.radius, big: true, scale: set.explode.scale);
            ProtoDestructibles.DamageInRadius(c, n.radius, n.dmg);   // 반경 안 승합차도
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var list = sd.Active;
            float rSq = n.radius * n.radius;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 d = z.transform.position - c; d.y = 0f;
                if (d.sqrMagnitude > rSq) continue;
                z.ApplyBlastSoot();
                Vector3 push = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
                z.TakeDamage(n.dmg, push * n.knock, n.crit, true, HitFx.Blast(set));
            }
        }

        static ProtoZombie FindTarget(Vector3 origin, float range)
        {
            var dir = ProtoSpawnDirector.I;
            if (dir == null) return null;
            ProtoZombie best = null;
            float bestSq = range * range;
            float bestHP = float.MaxValue;
            var list = dir.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                float sq = (z.transform.position - origin).sqrMagnitude;
                if (sq > bestSq + 0.01f) continue;
                if (sq < bestSq - 0.25f || (Mathf.Abs(sq - bestSq) <= 0.25f && z.hp < bestHP))
                {
                    bestSq = sq; bestHP = z.hp; best = z;
                }
            }
            return best;
        }
    }
}
