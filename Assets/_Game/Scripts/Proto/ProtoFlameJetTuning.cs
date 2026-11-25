using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 화염 줄기 프리팹이 <b>스스로 들고 다니는 맞춤값</b> — 카탈로그 프리팹 루트에 붙이면 <see cref="ProtoFlamePack"/> 이 세울 때 제 값(사거리 몫 · 부채꼴 · 끝 연기 …)을
    /// 이것으로 바꾼다(2026-11-06 인페르노 — 화염방사기 진화. 사용자 "컨셉 아트대로, 시트 애니메이션 말고 파티클로").
    ///
    /// <para><b>왜.</b> ProtoFlamePack 의 기본값은 팩 복사본(RedFlameThrower — 팩 단위계, 끝이 오래 녹는 불)에 맞춘 것이다. 인페르노는 미터 단위로 짜고 끝에서 부풀어 연기가 되는
    /// 불이라 "보이는 불끝이 수명 몇 할 지점인가" 부터 다르다. 프리팹이 제 값을 들고 있으면 무기마다 런타임 컴포넌트를 갈라 쓸 필요가 없다 — 무기를 바꾸면(진화)
    /// 새 프리팹의 값이 들어가고, 이 컴포넌트가 없는 프리팹으로 돌아가면 원래 값으로 돌아간다(ProtoFlamePack 이 처음 값을 기억한다).</para>
    ///
    /// <para><b>덤으로 하는 일(시스템 이름이 약속).</b> ① <c>Charge…</c> — 쏘기 시작하는 순간 한 번 터지는 점화(총구 섬광 · 불똥 · 링). 프리팹에 적은 버스트 수를 Awake 에서 읽어
    /// 두고 버스트를 지운다(ProtoFlamePack 은 모든 시스템을 반복으로 돌리고 방출만 여닫으므로 버스트를 그대로 두면 제때 안 터진다). ② <c>Ground…</c> — 바닥 빛 · 바닥 불.
    /// 총구 높이가 아니라 <b>바닥</b>에 놓는다(소유자 발 높이 + <see cref="groundLift"/>), 방향은 불줄기 그대로.
    /// ProtoFlamePack 은 이 둘을 페이드에서 빼 제 수명대로 꺼지게 한다(이름 규칙).</para>
    /// </summary>
    [DefaultExecutionOrder(100)]   // ProtoFlamePack.LateUpdate(총구 자리 · 조준) 뒤 — 바닥 자리를 같은 틀의 총구 아래에
    public class ProtoFlameJetTuning : MonoBehaviour
    {
        /// ProtoFlamePack 의 맞춤값 묶음 — 같은 이름 · 같은 뜻(설명은 ProtoFlamePack 필드 주석).
        [System.Serializable]
        public class Knobs
        {
            [Range(0.3f, 1f)] public float visibleFraction = 0.62f;
            [Range(-0.3f, 0.6f)] public float muzzleForward = 0.2f;
            [Range(0.2f, 1f)] public float coneFraction = 1f;
            [Range(0.1f, 1f)] public float flatten = 0.3f;
            [Range(0.1f, 1.5f)] public float fadeOut = 0.45f;
            [Range(0.02f, 0.3f)] public float snapOff = 0.1f;
            public float fadeDrag = 3f;
            public float fadeRise = 10f;
            [Range(0f, 0.5f)] public float rampIn = 0.15f;
            [Range(0f, 1f)] public float muzzleLinger = 0.45f;
            [Range(0.4f, 0.95f)] public float tipAge = 0.62f;
            [Range(0f, 1f)] public float tipSmokeChance = 0.25f;
            [Range(0, 16)] public int stopSmoke = 5;
            [Range(0, 40)] public int stopFragments = 18;
            [Range(0, 60)] public int stopSparks = 28;
            public float fragmentSpeed = 5f;
            public float sparkSpeed = 8f;

            public static Knobs From(ProtoFlamePack p) => new Knobs
            {
                visibleFraction = p.visibleFraction, muzzleForward = p.muzzleForward, coneFraction = p.coneFraction, flatten = p.flatten,
                fadeOut = p.fadeOut, snapOff = p.snapOff, fadeDrag = p.fadeDrag, fadeRise = p.fadeRise, rampIn = p.rampIn, muzzleLinger = p.muzzleLinger,
                tipAge = p.tipAge, tipSmokeChance = p.tipSmokeChance, stopSmoke = p.stopSmoke, stopFragments = p.stopFragments, stopSparks = p.stopSparks,
                fragmentSpeed = p.fragmentSpeed, sparkSpeed = p.sparkSpeed,
            };

            public void To(ProtoFlamePack p)
            {
                p.visibleFraction = visibleFraction; p.muzzleForward = muzzleForward; p.coneFraction = coneFraction; p.flatten = flatten;
                p.fadeOut = fadeOut; p.snapOff = snapOff; p.fadeDrag = fadeDrag; p.fadeRise = fadeRise; p.rampIn = rampIn; p.muzzleLinger = muzzleLinger;
                p.tipAge = tipAge; p.tipSmokeChance = tipSmokeChance; p.stopSmoke = stopSmoke; p.stopFragments = stopFragments; p.stopSparks = stopSparks;
                p.fragmentSpeed = fragmentSpeed; p.sparkSpeed = sparkSpeed;
            }
        }

        [Tooltip("ProtoFlamePack 이 이 프리팹을 세울 때 쓰는 값. 플레이 중 _FlamePack 인스펙터에서 바꿔 보고 맞는 값을 여기(또는 InfernoFlameSetup 의 Knobs)에 적는다")]
        public Knobs knobs = new Knobs();
        [Tooltip("바닥 시스템(Ground…)을 소유자 발 높이에서 이만큼(m) 띄운다 — 바닥과 겹쳐 깜빡이지 않게")]
        public float groundLift = 0.04f;
        [Tooltip("쏘기 시작하고 이만큼(초) 뒤에 바닥 불을 켠다 — 불줄기가 닿기 전에 바닥부터 타지 않게(GroundGlow 는 절반)")]
        public float groundDelay = 0.25f;

        ParticleSystem _root;
        ParticleSystem[] _charge = new ParticleSystem[0];
        int[] _chargeCount = new int[0];
        ParticleSystem[] _ground = new ParticleSystem[0];
        bool[] _groundIsGlow = new bool[0];   // 이름 비교를 Collect 때 한 번만 — Object.name 은 읽을 때마다 새 문자열(2026-11-20)
        bool _wasOn;
        float _onAt;

        public static bool IsCharge(string name) => name.StartsWith("Charge");
        public static bool IsGround(string name) => name.StartsWith("Ground");

        void Awake() => Collect();

        /// 이름 규칙대로 시스템을 모으고 점화 버스트 수를 읽어 둔다(버스트는 지운다 — 방출은 <see cref="Tick"/> 이 직접). 두 번 불러도 된다.
        public void Collect()
        {
            _root = GetComponent<ParticleSystem>();
            var all = GetComponentsInChildren<ParticleSystem>(true);
            var charge = new System.Collections.Generic.List<ParticleSystem>();
            var counts = new System.Collections.Generic.List<int>();
            var ground = new System.Collections.Generic.List<ParticleSystem>();
            for (int i = 0; i < all.Length; i++)
            {
                var ps = all[i];
                if (IsCharge(ps.name))
                {
                    var em = ps.emission;
                    int n = 0;
                    if (em.burstCount > 0)
                    {
                        var b = em.GetBurst(0);
                        n = Mathf.RoundToInt(b.count.mode == ParticleSystemCurveMode.TwoConstants ? Random.Range(b.count.constantMin, b.count.constantMax) : b.count.constant);
                        em.SetBursts(new ParticleSystem.Burst[0]);
                    }
                    else n = _chargeCount.Length > charge.Count ? _chargeCount[charge.Count] : 0;   // 이미 지웠다(두 번째 Collect)
                    em.rateOverTime = 0f;
                    charge.Add(ps); counts.Add(n);
                }
                else if (IsGround(ps.name)) ground.Add(ps);
            }
            _charge = charge.ToArray(); _chargeCount = counts.ToArray(); _ground = ground.ToArray();
            _groundIsGlow = new bool[_ground.Length];
            for (int gi = 0; gi < _ground.Length; gi++) _groundIsGlow[gi] = _ground[gi] != null && _ground[gi].name == "GroundGlow";
        }

        void LateUpdate() => Tick(Time.time);

        /// 틀마다 — 방출이 켜지는 순간 점화를 한 번 터뜨리고, 바닥 시스템을 총구 아래 바닥에 놓고 지연 뒤에 켠다. 에디터 미리보기도 이걸 직접 부른다(now = 시뮬레이션 시각).
        public void Tick(float now)
        {
            bool on = _root != null && _root.emission.enabled;
            if (on && !_wasOn) { Ignite(); _onAt = now; }
            _wasOn = on;
            // ProtoFlamePack 은 켜고 끌 때만 방출 스위치를 쓴다 — 그 뒤(실행 순서 100)에서 바닥만 지연을 건다
            for (int i = 0; i < _ground.Length; i++)
            {
                if (_ground[i] == null) continue;
                float delay = _groundIsGlow[i] ? groundDelay * 0.5f : groundDelay;
                bool want = on && now - _onAt >= delay;
                var em = _ground[i].emission;
                if (em.enabled != want) em.enabled = want;
            }
            PlaceGround();
        }

        /// 점화 — Charge… 시스템마다 적어 둔 수만큼 한 번에.
        public void Ignite()
        {
            for (int i = 0; i < _charge.Length; i++)
                if (_charge[i] != null && _chargeCount[i] > 0) _charge[i].Emit(_chargeCount[i]);
        }

        void PlaceGround()
        {
            if (_ground.Length == 0) return;
            // 소유자 = _FlamePack 의 부모(플레이어 · 무대 캐릭터 루트) — 발 높이가 바닥. 없으면 월드 0
            var pack = transform.parent;
            var owner = pack != null ? pack.parent : null;
            float y = (owner != null ? owner.position.y : 0f) + groundLift;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            var rot = fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd) : transform.rotation;
            Vector3 p = transform.position; p.y = y;
            for (int i = 0; i < _ground.Length; i++)
                if (_ground[i] != null) _ground[i].transform.SetPositionAndRotation(p, rot);
        }
    }
}
