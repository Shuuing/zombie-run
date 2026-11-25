using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    public enum UltBranch { None, A, B }

    /// 캐릭터 한 명의 스킬 트리 중 필사기 줄(§06 v0.7). 정식 게임에서는 로비에서 노드를 연다.
    /// 프로토에는 로비가 없어서 ProtoRun 인스펙터·T키로 고른다. 버프 줄 4노드는 아직 없다.
    [System.Serializable]
    public class UltTree
    {
        [Tooltip("1단 필사기 강화 I")] public bool tier1;
        [Tooltip("2단 필사기 강화 II")] public bool tier2;
        [Tooltip("3단 갈래 택1. 로비에서 언제든 무료로 바꾼다(결정 4).")] public UltBranch branch;

        public string Short()
        {
            if (!tier1 && !tier2 && branch == UltBranch.None) return "기본";
            string s = tier1 ? "I" : "";
            if (tier2) s += s.Length > 0 ? "·II" : "II";
            if (branch != UltBranch.None) s += (s.Length > 0 ? "·" : "") + branch;
            return s;
        }

        /// T키 순환: 기본 → I → I·II → I·II·A → I·II·B → 기본
        public void Cycle()
        {
            if (!tier1) { tier1 = true; tier2 = false; branch = UltBranch.None; }
            else if (!tier2) tier2 = true;
            else if (branch == UltBranch.None) branch = UltBranch.A;
            else if (branch == UltBranch.A) branch = UltBranch.B;
            else { tier1 = tier2 = false; branch = UltBranch.None; }
        }
    }

    /// <summary>
    /// 필사기 (GDD §06 v0.7) — <b>캐릭터마다 고유한 버튼 스킬</b>. 2026-10-29 결정으로 v0.6 의 파트너 12초 합류(ProtoAlly)를 지우고
    /// 이걸로 바꿨다. 먼저 만든 것은 루키·파이로 2종이고, 나머지 4명은 버튼이 "준비 중"으로 잠긴다.
    ///
    /// <para><b>루키 — 표적 사살.</b> 화면 안에서 보스 → 엘리트 → 체력 순으로 5체를 골라 표시(0.35초)하고 한 발씩 관통탄을 날린다.
    /// 대상당 주무기 피해 ×25. 갈래 A 제압 = 8체, 갈래 B 저격 = 1체 ×3. 탄은 FX 카탈로그 ult_target_kill_bullet(금빛 혜성탄, 32m/s, 닿을 때 피해 —
    /// 2026-11-18 사용자 요청으로 즉발 금색 선 대신).</para>
    ///
    /// <para><b>파이로 — 불꽃 고리.</b> 8초간 몸 둘레 반경 3.5m 에 불 고리. 테두리 띠(폭 1.4m)에 닿은 적에게 초당 주무기 피해 ×3 + 화상.
    /// 갈래 A 대화염 = 반경 5m, 갈래 B 연소 = 끝날 때 화상 걸린 적 전부 폭발. 그림은 FX 카탈로그 ult_fire_ring(프리팹 MFX_FireRing — 불덩이 2개가 마주 보며 돌고
    /// 각자 반 바퀴 불길을 끌어 한 고리로 이어짐(11-18), 2026-11-13 사용자 요청으로 예전 테두리 선 + 코드 불 입자를 대신).</para>
    ///
    /// <para><b>브레이커 — 광란 난사.</b> 미스 포춘 궁(사용자 참고 자료)처럼 앞쪽 부채꼴에 탄을 3초간 쏟아붓는다. 방향은 발동 순간 적이 가장 많은 쪽으로 잠그고
    /// 좌우로 흔들며 0.06초마다 산탄 9발. 이동은 그대로, 평소 자동 사격은 그동안 쉰다. 마지막에 한 번 크게 터뜨린다.
    /// 갈래 A 확산 = 부채 110°, 갈래 B 집중 = 부채 35° · 관통 +3.</para>
    ///
    /// <para>피해 기준은 <see cref="ProtoEvolution.BaseStats"/> — 주무기 레벨·공통 버프(공격력 배율)는 따르고 진화는 따르지 않는다(§06).
    /// 시간은 멈추지 않는다. 판정은 게임 시간(<see cref="ProtoConfig.Delta"/>)이라 레벨업 카드가 떠 있으면 같이 멈춘다.
    /// 씬 배선 없이 처음 부를 때 스스로 생긴다(<see cref="ProtoBeamFx"/> 와 같은 방식).</para>
    /// </summary>
    public class ProtoUltimate : MonoBehaviour
    {
        /// 필사기의 <b>행동 틀</b>. 숫자는 전부 에셋(<see cref="UltimateDef"/> 의 자식)에 있고, 여기는 에셋 타입으로 틀을 고른다.
        /// 트리 선택(강화 I·II, 갈래)마다 쓸 칸이 ProtoRun 에 종류별로 있어서(rookieTree …) 종류 enum 은 남겨 둔다.
        public enum Kind { None, Rookie, Pyro, Breaker }

        WeaponStats _barrageStats;

        static ProtoUltimate _inst;
        Coroutine _running;
        float _activeUntil;

        /// 필사기가 지금 효과를 내는 중인가(표적 사살의 표시·사격, 불꽃 고리 지속).
        public static bool Active => _inst != null && _inst._running != null;
        public static float Remaining => Active ? Mathf.Max(0f, _inst._activeUntil - Now) : 0f;

        /// 지금 나와 있는 캐릭터의 필사기 에셋(캐릭터 테이블 → CharacterDef.ultimate). 없으면 null = 준비 중.
        /// 아직 한 번도 안 바꿨으면(씬 원본) 명단 1번이다(ProtoSceneBuilder 기본값).
        public static UltimateDef DefOf(ProtoPlayer p)
        {
            if (p == null) return null;
            var sw = p.GetComponent<ProtoCharacterSwitcher>();
            var cd = sw != null ? sw.CurrentDef : null;
            if (cd == null) { var t = CharacterTable.I; cd = t != null ? t.At(0) : null; }
            return cd != null ? cd.ultimate : null;
        }

        /// 에셋 타입 → 행동 틀.
        public static Kind KindOf(UltimateDef d)
            => d is UltTargetKillDef ? Kind.Rookie : d is UltFlameRingDef ? Kind.Pyro : d is UltBarrageDef ? Kind.Breaker : Kind.None;

        public static Kind KindOf(ProtoPlayer p) => KindOf(DefOf(p));

        /// 버튼·팝업에 쓰는 이름. 에셋의 displayName.
        public static string NameOf(ProtoPlayer p)
        {
            var d = DefOf(p);
            return d != null ? d.Label : "준비 중";
        }

        /// 발동. 쓸 수 없으면(해당 캐릭터 필사기 없음·화면에 표적 없음) false — 게이지를 쓰지 않는다.
        public static bool Play(ProtoPlayer p, UltTree tree)
        {
            if (p == null || Active) return false;
            var def = DefOf(p);
            var k = KindOf(def);
            if (k == Kind.None) return false;
            if (_inst == null) _inst = new GameObject("_Ultimate").AddComponent<ProtoUltimate>();
            tree = tree ?? new UltTree();

            if (def is UltTargetKillDef tk)
            {
                int n = tree.branch == UltBranch.A ? tk.targetsA : tree.branch == UltBranch.B ? tk.targetsB : tk.targets;
                var targets = PickTargets(Mathf.Max(1, n));
                if (targets.Count == 0) { ProtoHUD.ShowPickup("표적 없음", tk.color); return false; }
                _inst._running = _inst.StartCoroutine(_inst.Rookie(p, tree, tk, targets));
            }
            else if (def is UltBarrageDef bd)
            {
                _inst._running = _inst.StartCoroutine(_inst.Breaker(p, tree, bd));
            }
            else if (def is UltFlameRingDef fr)
            {
                _inst._running = _inst.StartCoroutine(_inst.Pyro(p, tree, fr));
            }
            ProtoHUD.ShowPickup(def.Label + "!", def.color);
            Debug.Log($"[필사기] {def.Label} 발동 (트리 {tree.Short()})");
            return true;
        }

        /// 즉시 끊는다(재시작·결과 화면).
        public static void Stop()
        {
            if (_inst == null) return;
            _inst.StopAllCoroutines();
            _inst._running = null;
            if (_inst._holdShooter != null) { _inst._holdShooter.holdFireUntil = 0f; _inst._holdShooter = null; }
            _inst.HideAll();
        }

        ProtoShooter _holdShooter;   // 난사 중 평소 사격을 멈춰 둔 슈터 — 끊길 때 풀어 준다

        void OnDestroy() { if (_inst == this) _inst = null; }

        static float Now => ProtoRun.I != null ? ProtoRun.I.Elapsed : Time.time;

        static float BaseDamage(ProtoPlayer p)
        {
            var st = ProtoEvolution.BaseStats(p);
            return (st != null ? st.damage : ProtoConfig.WeaponDamage) * p.damageMult;
        }

        // ── 루키 ─────────────────────────────────────────────────────────

        static readonly List<ProtoZombie> _pick = new List<ProtoZombie>(64);

        /// 화면 안 적 중 보스 → 엘리트 → 남은 체력 순으로 n 체. 화면 밖을 쏘면 무엇을 맞혔는지 보이지 않는다.
        static List<ProtoZombie> PickTargets(int n)
        {
            var result = new List<ProtoZombie>(n);
            var sd = ProtoSpawnDirector.I;
            var cam = Camera.main;
            if (sd == null) return result;
            _pick.Clear();
            var list = sd.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                if (cam != null)
                {
                    var v = cam.WorldToViewportPoint(z.transform.position + Vector3.up);
                    if (v.z <= 0f || v.x < 0.03f || v.x > 0.97f || v.y < 0.03f || v.y > 0.97f) continue;
                }
                _pick.Add(z);
            }
            _pick.Sort((a, b) =>
            {
                int ra = a.IsBoss ? 2 : a.IsElite ? 1 : 0, rb = b.IsBoss ? 2 : b.IsElite ? 1 : 0;
                return ra != rb ? rb.CompareTo(ra) : b.hp.CompareTo(a.hp);
            });
            for (int i = 0; i < _pick.Count && result.Count < n; i++) result.Add(_pick[i]);
            return result;
        }

        IEnumerator Rookie(ProtoPlayer p, UltTree tree, UltTargetKillDef d, List<ProtoZombie> targets)
        {
            bool snipe = tree.branch == UltBranch.B;
            float dmg = BaseDamage(p) * (tree.tier1 ? d.damageMulI : d.damageMul) * (snipe ? d.snipeMul : 1f);
            _activeUntil = Now + d.markTime + d.shotGap * targets.Count + 0.1f;

            // (1) 표시 — 표적 발밑에 금색 고리. 표적을 따라다닌다.
            ProtoAudio.Play("crit", p.transform.position, 0.9f);
            for (int i = 0; i < targets.Count; i++) Marker(i).Show(snipe ? 1.1f : 0.75f, d.color);
            for (float t = 0f; t < d.markTime; t += ProtoConfig.Delta)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var z = targets[i];
                    if (z != null && z.Alive) Marker(i).Follow(z.transform.position, 1f + 0.25f * Mathf.Sin(t * 40f));
                    else Marker(i).Hide();
                }
                yield return null;
            }

            // (2) 한 발씩 — 몸에서 표적까지 탄이 날아가 몸 표면에 닿을 때 피해 + 금빛 히트, 탄은 거기서 사라진다(bulletPierce 0). 판정은 표적 하나(+강화 II 파편).
            //     2026-11-18 사용자 "표적 되는 적에게 총알 날라가는 이펙트 좀 더 화려한걸로" — 즉발 금색 선 대신 FX 카탈로그
            //     ult_target_kill_bullet(금빛 혜성탄)을 날린다. 프리팹이 없으면 예전 그대로 금색 선 + 즉발 피해(Fly 안).
            float shrapnel = BaseDamage(p) * d.shrapnelMul;
            for (int i = 0; i < targets.Count; i++)
            {
                var z = targets[i];
                Marker(i).Hide();
                if (z == null || !z.Alive || p == null) continue;

                Vector3 from = p.transform.position + Vector3.up * 1.1f;
                ProtoAudio.Play("cannon", p.transform.position, snipe ? 1.2f : 0.9f);
                float flight = (Vector3.Distance(from, z.transform.position + Vector3.up) + d.bulletPierce) / Mathf.Max(1f, d.bulletSpeed);
                _activeUntil = Mathf.Max(_activeUntil, Now + flight + 0.05f);
                _flying++;
                StartCoroutine(Fly(tree, d, z, from, p.transform.forward, dmg, shrapnel, snipe));

                for (float t = 0f; t < d.shotGap; t += ProtoConfig.Delta) yield return null;
            }
            while (_flying > 0) yield return null;   // 탄이 다 닿고 사라질 때까지 필사기는 켜진 상태(Active)
            _running = null;
        }

        // ── 그림: 표적 사살 탄(FX 카탈로그 ult_target_kill_bullet) ─────────────────────

        int _flying;                                            // 날고 있는 탄 수 — Rookie 가 이게 0 이 될 때까지 기다린다
        readonly List<FxHandle> _bullets = new List<FxHandle>(8);   // Stop() 이 코루틴을 끊을 때 남은 탄을 거둔다

        /// 탄 하나: 가슴 높이로 표적을 따라 꺾이며 날아가 <b>몸 표면(쏜 쪽, bodyRadius 앞)</b>에 닿는 순간 피해 + 흔들림 + 히트(ult_target_kill_hit),
        /// bulletPierce(기본 0 — 11-18 사용자 "너무 통과하는 것 같다, 맞으면 히트 연출 나오면서 사라지게") 만큼 더 가서 끝난다.
        /// 표적이 먼저 죽으면 마지막 자리까지 날아가 피해 없이 히트만 내고 끝난다(풀로 돌아간 좀비를 다시 쫓지 않게 그때부터 추적을 끊는다).
        IEnumerator Fly(UltTree tree, UltTargetKillDef d, ProtoZombie z, Vector3 from, Vector3 fallbackDir, float dmg, float shrapnel, bool snipe)
        {
            Vector3 aim = z.transform.position + Vector3.up * 1.0f;
            Vector3 vel = aim - from;
            vel = vel.sqrMagnitude > 0.0001f ? vel.normalized : fallbackDir;
            var ctx = FxContext.At(from);
            ctx.rotation = Quaternion.LookRotation(vel);
            var h = ProtoFx.Play(FxIds.UltTargetKillBullet, ctx, span: true);

            if (h == null || h.Instance == null)
            {
                // 프리팹이 없다 — 예전 그림(즉발 금색 선)
                Vector3 dir = Flat(vel, fallbackDir);
                ProtoBeamFx.Play(from, aim + dir * 2f, d.color, true);
                ProtoCameraFollow.Shake(snipe ? ProtoCameraFollow.Impact.Heavy : ProtoCameraFollow.Impact.Light);
                HitTarget(tree, d, z, aim, dir, dmg, shrapnel);
                _flying--;
                yield break;
            }

            _bullets.Add(h);
            float speed = Mathf.Max(1f, d.bulletSpeed);
            float left = Mathf.Max(0f, d.bulletPierce);
            float surf = Mathf.Clamp(z.bodyRadius, 0.2f, 1.2f);   // 가운데가 아니라 몸 표면에서 맞는다 — 히트가 몸에 묻히지 않게
            bool hit = false;
            Vector3 pos = from;
            for (float t = 0f; t < 2f; t += ProtoConfig.Delta)   // 2초 = 안전장치(멈춤 화면에선 Delta 0 이라 같이 멈춘다)
            {
                float step = speed * ProtoConfig.Delta;
                if (!hit)
                {
                    if (z != null && !z.Alive) z = null;
                    if (z != null) { aim = z.transform.position + Vector3.up * 1.0f; surf = Mathf.Clamp(z.bodyRadius, 0.2f, 1.2f); }
                    Vector3 to = aim - pos;
                    float dist = to.magnitude;
                    if (dist > 0.0001f) vel = to / dist;
                    if (dist - surf <= step)
                    {
                        float adv = Mathf.Max(0f, dist - surf);
                        pos += vel * adv; step -= adv; hit = true;
                        Vector3 flat = Flat(vel, fallbackDir);
                        ProtoCameraFollow.Shake(snipe ? ProtoCameraFollow.Impact.Heavy : ProtoCameraFollow.Impact.Light);
                        ProtoFx.Play(FxIds.UltTargetKillHit, pos, -flat);   // 금빛 낙뢰 히트 — +Z 가 쏜 사람 쪽
                        if (z != null) HitTarget(tree, d, z, pos, flat, dmg, shrapnel);
                    }
                    else { pos += vel * step; step = 0f; }
                }
                if (hit)
                {
                    float s = Mathf.Min(step, left);
                    pos += vel * s; left -= s;
                }
                if (!h.Alive) break;   // 다른 곳(재시작 등)에서 거뒀다
                h.MoveTo(pos);
                h.Instance.transform.rotation = Quaternion.LookRotation(vel);
                if (hit && left <= 0f) break;
                yield return null;
            }
            _bullets.Remove(h);
            // 탄에 붙은 머리(로컬 시뮬레이션 — 불덩이 메시 · 빛 · 가시)는 탄과 함께 바로 지운다. 방출만 멈추면 수명(최대 1초, 흐려지지도 않음)
            // 동안 멈춘 자리에 떠 있다가 회수 때 뚝 사라졌다(11-18 사용자 "히트되면 이펙트가 안 사라지고 멈춰 있다가 사라져").
            // 월드에 남은 꼬리 · 불티만 제 수명대로 사그라들고 카탈로그 lifetime(0.4초) 뒤 회수된다.
            if (h.Alive && h.Instance != null)
                foreach (var ps in h.Instance.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            h.Stop();
            _flying--;
        }

        void HitTarget(UltTree tree, UltTargetKillDef d, ProtoZombie z, Vector3 at, Vector3 dir, float dmg, float shrapnel)
        {
            if (z == null || !z.Alive) return;
            Vector3 feet = z.transform.position;
            z.TakeDamage(dmg, dir, true, true, HitFx.From(null, at));   // 기본 세트의 불똥을 정확한 착탄점에
            if (tree.tier2) Splash(feet, d.shrapnelRadius, shrapnel, z);
        }

        static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            if (v.sqrMagnitude > 0.0001f) return v.normalized;
            fallback.y = 0f;
            return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
        }

        void KillBullets()
        {
            foreach (var h in _bullets) if (h != null) h.Kill();
            _bullets.Clear();
            _flying = 0;
        }

        // ── 파이로 ───────────────────────────────────────────────────────

        IEnumerator Pyro(ProtoPlayer p, UltTree tree, UltFlameRingDef d)
        {
            var st = ProtoEvolution.BaseStats(p);
            float radius = tree.branch == UltBranch.A ? d.radiusA : d.radius;
            float dur = tree.tier2 ? d.durationII : d.duration;
            float dps = BaseDamage(p) * (tree.tier1 ? d.damageMulI : d.damageMul);
            float burnDur = st != null ? st.burnDuration : 2f;
            float burnDps = (st != null ? st.burnDps : 4f) * p.damageMult;
            float tickLen = Mathf.Max(0.02f, d.tick);
            _activeUntil = Now + dur;

            ProtoAudio.Play("explosion", p.transform.position, 0.6f);
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Light);
            Vector3 c0 = p.transform.position; c0.y = 0f;
            PlayRing(c0, radius);

            float nextTick = 0f;
            for (float t = 0f; t < dur; t += ProtoConfig.Delta)
            {
                if (p == null) break;
                Vector3 c = p.transform.position; c.y = 0f;
                // 그림 — 플레이어를 따라가되 회전은 따르지 않는다(조준으로 몸을 돌릴 때 고리가 같이 휘둘리지 않게).
                // 프리팹 파티클은 로컬 시뮬이라 위치만 옮기면 고리 전체가 같이 온다.
                if (_ring != null && _ring.Instance != null) _ring.Instance.transform.SetPositionAndRotation(c, Quaternion.identity);

                if (t >= nextTick)
                {
                    nextTick += tickLen;
                    RingDamage(c, radius, d.band, dps * tickLen, burnDur, burnDps);
                }
                yield return null;
            }
            EndRing();

            if (tree.branch == UltBranch.B) Combust(BaseDamage(p), d);
            _running = null;
        }

        // ── 그림: 불꽃 고리(FX 카탈로그 ult_fire_ring) ─────────────────────
        // 2026-11-13 사용자 "기존꺼 빼고 새로 만든걸로" — 바닥 테두리 선 + 코드로 뿜던 몸불 입자를 지우고 프리팹 MFX_FireRing 으로.

        FxHandle _ring;

        void PlayRing(Vector3 c, float radius)
        {
            KillRing();
            var h = ProtoFx.Play(FxIds.UltFireRing, FxContext.At(c), span: true);
            if (h == null || h.Instance == null) return;   // 카탈로그에 프리팹이 없으면 그림 없이 판정만
            FitRing(h, radius);
            _ring = h;
        }

        /// 프리팹은 반지름 2.5m 로 만들어져 있다. 트랜스폼 배율로 키우면 불꽃 그림까지 같이 커지므로(사용자가 '반으로' 맞춘 크기)
        /// 원 둘레 방출 반지름만 판정 반경으로 넓히고, 둘레가 길어진 만큼 초당 개수 · 상한을 늘려 촘촘함을 지킨다.
        /// 풀에서 다시 꺼낸 것도 맞도록 매번 원본 프리팹 값에서 계산하고, 처음부터 다시 틀어(prewarm · 띠 방출점 버스트) 새 반지름으로 채운다.
        static void FitRing(FxHandle h, float radius)
        {
            var cat = FxCatalog.Load();
            var entry = cat != null ? cat.Find(FxIds.UltFireRing) : null;
            if (entry == null || entry.prefab == null) return;
            var src = entry.prefab.GetComponentsInChildren<ParticleSystem>(true);
            var dst = h.Instance.GetComponentsInChildren<ParticleSystem>(true);
            if (src.Length != dst.Length) return;

            float baseR = 0f;
            foreach (var s in src)
            {
                var sh = s.shape;
                if (sh.enabled && sh.shapeType == ParticleSystemShapeType.Circle) { baseR = sh.radius; break; }
            }
            if (baseR <= 0.01f) return;
            float k = radius / baseR;

            for (int i = 0; i < dst.Length; i++)
            {
                var ssh = src[i].shape;
                if (!ssh.enabled || ssh.shapeType != ParticleSystemShapeType.Circle) continue;
                var dsh = dst[i].shape; dsh.radius = ssh.radius * k;
                var se = src[i].emission;
                if (se.rateOverTimeMultiplier > 0f)
                {
                    var de = dst[i].emission; de.rateOverTimeMultiplier = se.rateOverTimeMultiplier * k;
                    var dm = dst[i].main; dm.maxParticles = Mathf.CeilToInt(src[i].main.maxParticles * k);
                }
            }
            for (int i = 0; i < dst.Length; i++) dst[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < dst.Length; i++) dst[i].Play(false);
        }

        /// 끝 — 불길은 방출만 멈춰 사그라들게(카탈로그 lifetime 뒤 회수). 띠 방출점은 수명이 사실상 영원이라 Stop 으로 안 사라지니 바로 지운다.
        void EndRing()
        {
            var h = _ring; _ring = null;
            if (h == null || !h.Alive) return;
            if (h.Instance != null)
                foreach (var ps in h.Instance.GetComponentsInChildren<ParticleSystem>(true))
                    if (ps.main.startLifetime.constantMax > 100f) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            h.Stop();
        }

        void KillRing()
        {
            if (_ring != null) _ring.Kill();
            _ring = null;
        }

        // ── 브레이커 ─────────────────────────────────────────────────────

        IEnumerator Breaker(ProtoPlayer p, UltTree tree, UltBarrageDef d)
        {
            var sh = p.GetComponent<ProtoShooter>();
            var st = ProtoEvolution.BaseStats(p);
            if (sh == null || st == null) { _running = null; yield break; }
            _holdShooter = sh;

            // 난사용 탄 — 산탄총 수치 사본에서 사거리·관통·넉백·색만 바꾼다(에셋은 공유 수치라 건드리지 않는다). 바꾸는 값은 필사기 에셋의 '난사 탄' 칸
            if (_barrageStats != null) Destroy(_barrageStats);
            _barrageStats = Instantiate(st);
            _barrageStats.name = "(필사기 난사)";
            _barrageStats.projectileSpeed = d.projectileSpeed;
            _barrageStats.projectileLife = d.projectileLife;
            _barrageStats.knockbackMult = d.knockbackMult;
            _barrageStats.pierce = st.pierce + (tree.branch == UltBranch.B ? d.pierceB : 0);
            _barrageStats.tracerColor = d.tracerColor.a > 0f ? d.tracerColor : d.color;
            _barrageStats.tracerLength = d.tracerLength;
            _barrageStats.tracerWidth = st.tracerWidth * d.tracerWidthMul;

            bool focus = tree.branch == UltBranch.B;
            float spread = tree.branch == UltBranch.A ? d.spreadA : focus ? d.spreadB : d.spread;
            float dur = tree.tier2 ? d.durationII : d.duration;
            float dmg = BaseDamage(p) * (tree.tier1 ? d.damageMulI : d.damageMul);
            float gap = Mathf.Max(0.01f, d.gap);
            Vector3 aim = DensestDir(p.transform.position, p.FacingDir, spread, d.aimRange);
            _activeUntil = Now + dur;

            ProtoAudio.Play("crit", p.transform.position, 1f);
            // 광소 — 쏘기 시작과 함께 여자가 미친 듯이 웃는다(10-29 사용자 요청·참고 음성으로 Seed Audio 생성 SFX/laugh_1, 5초라 강화 II 4초도 덮는다). 총성에 묻히지 않게 화면 전체 소리로.
            ProtoAudio.Play2D("laugh", 1.2f);
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Light);

            int wave = 0;
            for (float t = 0f; t < dur; t += gap)
            {
                if (p == null || !p.IsAlive) break;
                sh.holdFireUntil = Time.time + 0.25f;
                // 좌우로 흔든다 — 사인 둘을 겹쳐 박자가 일정하지 않게(기계가 아니라 미친 사람이 휘갈기는 느낌)
                float sweep = d.sweep * (Mathf.Sin(t * 9f) * 0.7f + Mathf.Sin(t * 23f + 1.3f) * 0.3f);
                Vector3 dir = Quaternion.Euler(0f, focus ? sweep * 0.3f : sweep, 0f) * aim;
                sh.UltVolley(_barrageStats, dir, d.pellets, spread, dmg, wave % 3 == 0 ? d.bigFlashEvery3 : 1f);
                ProtoCameraFollow.Shake(0.07f, 0.09f);
                wave++;
                for (float w = 0f; w < gap; w += ProtoConfig.Delta) yield return null;
            }

            // 마무리 — 한 번에 크게
            if (p != null && p.IsAlive)
            {
                sh.UltVolley(_barrageStats, aim, d.finalePellets, spread * 1.1f, dmg * d.finaleDamageMul, d.finaleFlash);
                ProtoAudio.Play("explosion", p.transform.position + aim * 2f, 0.7f);
                ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Heavy);
            }
            sh.holdFireUntil = Time.time + 0.15f;
            _holdShooter = null;
            _running = null;
        }

        /// aimRange 안 적이 가장 많이 드는 부채 방향(24방향 중). 아무도 없으면 바라보던 쪽.
        static Vector3 DensestDir(Vector3 from, Vector3 fallback, float spread, float aimRange)
        {
            fallback.y = 0f;
            fallback = fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return fallback;
            float cosHalf = Mathf.Cos(Mathf.Max(spread, 40f) * 0.5f * Mathf.Deg2Rad);
            Vector3 best = fallback; float bestScore = 0f;
            for (int k = 0; k < 24; k++)
            {
                float a = k * 15f * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                float score = 0f;
                foreach (var z in sd.Active)
                {
                    if (z == null || !z.Alive) continue;
                    Vector3 v = z.transform.position - from; v.y = 0f;
                    float m = v.magnitude;
                    if (m > aimRange || m < 0.01f) continue;
                    if (Vector3.Dot(v / m, dir) < cosHalf) continue;
                    score += z.IsBoss ? 6f : z.IsElite ? 3f : 1f;
                }
                if (score > bestScore) { bestScore = score; best = dir; }
            }
            return best;
        }

        static void RingDamage(Vector3 c, float radius, float band, float dmg, float burnDur, float burnDps)
        {
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var list = sd.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 d = z.transform.position - c; d.y = 0f;
                float extra = Mathf.Max(0f, z.bodyRadius - ProtoConfig.BaseBodyRadius);
                float dist = d.magnitude;
                if (dist > radius + 0.3f + extra || dist < radius - band - extra) continue;
                z.TakeDamage(dmg, dist > 0.01f ? d / dist * 0.4f : Vector3.zero, false, false, HitFx.Blast(null));   // 살짝 바깥으로 밀어 낸다. 불 고리 — 자리 불똥 없음
                z.ApplyBurn(burnDur, burnDps);
            }
        }

        /// 갈래 B 연소 — 화상 걸린 적 전부가 터진다. 터진 자리 둘레에도 작은 피해.
        static void Combust(float baseDmg, UltFlameRingDef d)
        {
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var burning = new List<ProtoZombie>();
            foreach (var z in sd.Active) if (z != null && z.Alive && z.IsBurning) burning.Add(z);
            int fx = 0;
            foreach (var z in burning)
            {
                if (z == null) continue;
                Vector3 at = z.transform.position;
                if (fx++ < d.combustFxMax) ProtoFx.PlayBlast(FxIds.UltCombust, at, d.combustSplash, big: true, quiet: fx != 1);   // 첫 발만 흔들림
                if (z.Alive) z.TakeDamage(baseDmg * d.combustMul, Vector3.zero, false, true, HitFx.Blast(null));
                Splash(at, d.combustSplash, baseDmg * d.combustSplashMul, z);
            }
        }

        static void Splash(Vector3 at, float radius, float dmg, ProtoZombie except)
        {
            var sd = ProtoSpawnDirector.I;
            if (sd == null) return;
            var list = sd.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive || z == except) continue;
                Vector3 d = z.transform.position - at; d.y = 0f;
                float r = radius + Mathf.Max(0f, z.bodyRadius - ProtoConfig.BaseBodyRadius);
                if (d.sqrMagnitude > r * r) continue;
                z.TakeDamage(dmg, d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero, false, false, HitFx.Blast(null));   // 파편 — 자리 불똥 없음
            }
        }

        // ── 그림: 바닥 고리(표적 표시) ─────────────────────
        // 테두리가 곧 판정 반경이다(§15 — 데미지 숫자 대신 테두리로 "무엇이 맞았나"를 읽는다).
        // 불꽃 고리는 2026-11-13 부터 테두리 선 없이 불길 고리(ult_fire_ring) 자체가 반경을 보여 준다(사용자 "원형 라인이 안 보이게").

        class Circle
        {
            public LineRenderer lr;
            float _r;
            const int Seg = 40;

            public Circle(Transform parent, Material mat, float width)
            {
                var go = new GameObject("Circle");
                go.transform.SetParent(parent, false);
                lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = Seg;
                lr.widthMultiplier = width;
                lr.alignment = LineAlignment.TransformZ;   // 바닥에 눕힌다
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                go.SetActive(false);
            }

            public void Show(float r, Color c) { _r = r; _c = c; _a0 = 0f; _graded = false; lr.startColor = lr.endColor = c; lr.gameObject.SetActive(true); }

            Color _c;
            float _a0;   // 첫 점의 각도(도). Spin 이 돌린다
            bool _graded;

            /// 테두리에 밝은 마디 4개를 두고 불꽃 덩어리와 같이 돌린다 — 선 자체가 돌아 보이게.
            /// 선 색 그라디언트는 점 순서를 따르므로 첫 점의 각도를 옮기면 마디가 같이 돈다.
            public void Spin(float deg)
            {
                _a0 = deg;
                if (_graded) return;
                _graded = true;
                var g = new Gradient();
                var ck = new[] { new GradientColorKey(_c, 0f), new GradientColorKey(_c, 1f) };
                var ak = new GradientAlphaKey[8];
                for (int i = 0; i < 8; i++) ak[i] = new GradientAlphaKey(i % 2 == 0 ? 1f : 0.25f, i / 8f);
                g.SetKeys(ck, ak);
                lr.colorGradient = g;
            }
            public void Hide() { if (lr != null) lr.gameObject.SetActive(false); }

            public void Follow(Vector3 center, float scale)
            {
                float r = _r * scale;
                for (int i = 0; i < Seg; i++)
                {
                    float a = _a0 * Mathf.Deg2Rad + i * Mathf.PI * 2f / Seg;
                    lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, 0.06f, center.z + Mathf.Sin(a) * r));
                }
            }
        }

        static Material _lineMat;
        readonly List<Circle> _markers = new List<Circle>();

        static Material LineMat
        {
            get
            {
                // 밤 어둠 막(큐 2900)보다 뒤 — 빛이라 어둠에 묻히면 안 된다(ProtoBeamFx 와 같다)
                if (_lineMat == null) _lineMat = ProtoFxMat.New("UltLine", null, 3100);   // (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
                return _lineMat;
            }
        }

        Circle Marker(int i)
        {
            while (_markers.Count <= i) _markers.Add(new Circle(transform, LineMat, 0.12f));
            return _markers[i];
        }

        void HideAll()
        {
            foreach (var m in _markers) m.Hide();
            KillRing();
            KillBullets();
        }
    }
}
