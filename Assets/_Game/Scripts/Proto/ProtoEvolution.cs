using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 무기 진화 (「무기 진화 시스템 기획」 2026-10-20, 조건 개정 2026-10-21).
    ///
    /// <para><b>조건:</b> 주무기 강화 카드 Lv5 + <b>진화권</b> 1장. 진화권은 엘리트·중간 보스가 떨군 상자를
    /// 주우면 생긴다(<see cref="OnChestOpened"/>). 짝 패시브 조건은 2026-10-21 에 뺐다 — 모르는 사람이 헷갈린다.</para>
    ///
    /// <para><b>순서는 상관없다.</b> Lv5 를 먼저 채우면 다음 상자를 줍는 순간, 상자를 먼저 주우면 진화권을
    /// 들고 있다가 Lv5 가 되는 순간 진화한다(<see cref="TryEvolve"/>). 진화권 1장 = 진화 1번.
    /// 이번 판에 쓸 데가 없으면(이미 진화한 무기) 진화권은 남아 있다가 다른 캐릭터로 바꾸면 쓰인다.</para>
    ///
    /// <para><b>진화:</b> 손에 든 무기(<see cref="WeaponSocket"/>)를 진화 무기 에셋으로 갈아 끼운다.
    /// <see cref="ProtoShooter"/> 는 수치 에셋이 바뀐 것을 보고 스스로 다시 셋업한다. 주무기 강화 레벨은 그대로 남는다.</para>
    ///
    /// <para>이번 판에 진화한 기본 무기를 기억한다 — 캐릭터를 바꿔도 그 무기를 든 캐릭터면 진화 무기로 돌아온다
    /// (<see cref="Reapply"/>). 재시작하면 기본 무기로 되돌린다(<see cref="ResetRun"/>).</para>
    /// </summary>
    public static class ProtoEvolution
    {
        /// 주무기 강화 카드 id. 예전 "소총 강화" 카드라 id 가 rifle 이다 — 이름만 바꾸고 id 는 둔다.
        public const string WeaponCardId = "rifle";
        public const int WeaponCardMax = 5;
        public static readonly Color Gold = new Color(1f, 0.79f, 0.29f);

        static ProtoEvolutionTable _table;
        static readonly HashSet<WeaponModelDef> _evolved = new HashSet<WeaponModelDef>();
        static int _tickets;

        /// 들고 있는 진화권 수(상자를 주웠지만 아직 쓰지 않은 것).
        public static int Tickets => _tickets;

        static ProtoEvolutionTable Table
        {
            get
            {
                if (_table == null) _table = Resources.Load<ProtoEvolutionTable>("Data/EvolutionTable");
                return _table;
            }
        }

        static WeaponSocket SocketOf(ProtoPlayer p) => p != null ? p.GetComponentInChildren<WeaponSocket>(true) : null;

        /// 지금 손에 든 무기의 진화표 줄. 진화가 없는 무기거나 이미 진화했으면 null.
        static ProtoEvolutionTable.Entry RecipeFor(ProtoPlayer p)
        {
            var s = SocketOf(p);
            var t = Table;
            if (s == null || t == null) return null;
            var e = t.FindFrom(s.weapon);
            return e != null && e.to != null ? e : null;
        }

        static int Lv(ProtoRun run, string id) => run.upgradeLevels.TryGetValue(id, out var l) ? l : 0;

        /// 진화권만 있으면 진화할 수 있는 상태인가(주무기 Lv5 + 진화할 무기를 들고 있다).
        public static bool Ready(ProtoRun run)
        {
            if (run == null || run.player == null) return false;
            return RecipeFor(run.player) != null && Lv(run, WeaponCardId) >= WeaponCardMax;
        }

        /// 진화권이 있고 조건이 맞으면 진화한다. 카드 선택·상자 줍기·캐릭터 교체 뒤에 부른다.
        public static bool TryEvolve(ProtoRun run)
        {
            if (_tickets <= 0 || !Ready(run)) return false;
            _tickets--;
            Evolve(run.player);
            return true;
        }

        /// 엘리트·중간 보스 상자를 주웠다(<see cref="ProtoPickups"/>).
        public static void OnChestOpened(ProtoPlayer p)
        {
            _tickets++;
            var run = ProtoRun.I;
            if (TryEvolve(run)) return;   // 진화 연출이 문구를 띄운다

            var e = RecipeFor(p);
            ProtoHUD.ShowPickup(e != null ? $"진화권 획득! 주무기 Lv{WeaponCardMax} 에서 진화" : "진화권 획득!", Gold);
            ProtoAudio.Play("levelup", p.transform.position, 0.8f);
        }

        /// 진화 — 무기를 갈아 끼우고 연출. 디버그 키(F5)도 이걸 부른다(조건·진화권 무시).
        public static void Evolve(ProtoPlayer p)
        {
            var e = RecipeFor(p);
            var s = SocketOf(p);
            if (e == null || s == null) return;
            _evolved.Add(e.from);
            s.Equip(e.to);

            // 카탈로그 weapon_change_appear 에 프리팹이 꽂혀 있으면 빛기둥 대신 그걸 낸다(2026-11-13 노란 텔레포트 등장, 발밑을 따라다님)
            if (ProtoFx.HasPrefab(FxIds.WeaponChangeAppear))
                ProtoFx.Play(FxIds.WeaponChangeAppear, FxContext.On(p.transform, follow: true));
            else
            {
                var ctx = FxContext.At(p.transform.position);
                ctx.color = Gold;
                ProtoFx.Play(FxIds.EvolveBeam, ctx);   // 카탈로그 evolve_beam(기본 = 레벨업 빛기둥을 금색으로)
            }
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Heavy);
            ProtoAudio.Play2D("levelup");
            ProtoHUD.ShowPickup("진화! " + Short(e.to), Gold);
            Debug.Log($"[진화] {e.from.name} → {e.to.name} (남은 진화권 {_tickets})");
        }

        /// 캐릭터를 바꾼 뒤 — 새 캐릭터의 기본 무기가 이번 판에 이미 진화했으면 진화 무기로.
        /// 아직 진화 전이고 진화권과 Lv5 가 있으면 여기서 진화한다.
        public static void Reapply(ProtoPlayer p)
        {
            var e = RecipeFor(p);
            var s = SocketOf(p);
            if (e != null && s != null && _evolved.Contains(e.from)) { s.Equip(e.to); return; }
            var run = ProtoRun.I;
            if (run != null && run.player == p) TryEvolve(run);
        }

        /// 재시작 — 이번 판에 진화한 무기를 들고 있으면 기본 무기로 되돌리고 기록·진화권을 지운다.
        /// 처음부터 진화 무기를 든 캐릭터(프리팹에 그렇게 배선된 경우)는 건드리지 않는다.
        public static void ResetRun(ProtoPlayer p)
        {
            var s = SocketOf(p);
            var t = Table;
            var back = s != null && t != null ? t.FindTo(s.weapon) : null;
            if (back != null && back.from != null && _evolved.Contains(back.from)) s.Equip(back.from);
            _evolved.Clear();
            _tickets = 0;
        }

        /// 진화 전 기본 무기의 수치. 필사기 피해는 진화를 따르지 않는다(§06) — 진화 무기를 들고 있으면 진화표를 거꾸로 짚는다.
        /// 프로토의 루키처럼 처음부터 진화 무기(캐논)를 든 캐릭터도 기본 무기(소총) 수치로 계산된다.
        public static WeaponStats BaseStats(ProtoPlayer p)
        {
            var s = SocketOf(p);
            if (s == null || s.weapon == null) return null;
            var t = Table;
            var back = t != null ? t.FindTo(s.weapon) : null;
            var w = back != null && back.from != null ? back.from : s.weapon;
            return w.stats;
        }

        /// 주무기 강화 카드 설명 끝에 붙일 진화 안내. 다른 카드거나 진화할 무기가 아니면 null.
        public static string HintFor(ProtoRun run, string upgradeId)
        {
            if (run == null || upgradeId != WeaponCardId) return null;
            var e = RecipeFor(run.player);
            if (e == null) return null;
            return _tickets > 0 ? $"Lv{WeaponCardMax} 에서 진화: {Short(e.to)}"
                                : $"Lv{WeaponCardMax} + 보스 상자 → {Short(e.to)}";
        }

        /// HUD 진행도 두 줄. 진화와 상관없는 무기면 null.
        public static string HudLine(ProtoRun run)
        {
            if (run == null || run.player == null) return null;
            var s = SocketOf(run.player);
            var t = Table;
            if (s == null || t == null || s.weapon == null) return null;

            var e = t.FindFrom(s.weapon);
            if (e != null && e.to != null)
            {
                int lv = Mathf.Min(Lv(run, WeaponCardId), WeaponCardMax);
                string lvPart = lv >= WeaponCardMax ? $"<color=#7CE08A>주무기 Lv {lv}/{WeaponCardMax}</color>"
                                                    : $"주무기 Lv {lv}/{WeaponCardMax}";
                string boxPart = _tickets > 0 ? $"<color=#7CE08A>진화권 ×{_tickets}</color>"
                                              : "<color=#FFFFFF99>보스 상자 필요</color>";
                return $"<color=#FFC94A>진화 ▶ {Short(e.to)}</color>\n{lvPart} · {boxPart}";
            }
            var back = t.FindTo(s.weapon);
            if (back != null)
                return $"<color=#FFC94A>진화 완료 ▶ {Short(s.weapon)}</color>" + (_tickets > 0 ? $"\n진화권 ×{_tickets}" : "");
            return null;
        }

        /// "탕탕 캐논 (루키 진화)" → "탕탕 캐논"
        static string Short(WeaponModelDef d)
        {
            if (d == null) return "?";
            string n = string.IsNullOrEmpty(d.displayName) ? d.name : d.displayName;
            int k = n.IndexOf(" (");
            return k > 0 ? n.Substring(0, k) : n;
        }
    }
}
