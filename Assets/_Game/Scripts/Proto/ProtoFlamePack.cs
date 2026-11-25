using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 외부 팩 화염 줄기 프리팹을 <b>그 모양 그대로</b> 쓰는 화염 연출(2026-11-05 사용자: "RedFlameThrower 프리팹 이펙트를 복사해서 W_Flamethrower 무기 이펙트로").
    /// 무기 연출 세트(<see cref="WeaponFxSet.jet"/>)가 카탈로그 ID 를 가리키면 <see cref="ProtoShooter"/> · <see cref="FxWeaponStage"/> 가
    /// <see cref="ProtoFlameCone"/> 대신 이것을 붙인다. 프리팹은 카탈로그 항목의 prefab(복사본 — 원본 팩 프리팹은 안 쓴다).
    ///
    /// <para><b>ProtoFlameCone 과 다른 점.</b> 색 · 크기 · 그라디언트 · 머티리얼을 코드로 다시 짜지 않는다 — 팩이 만든 모양이 곧 연출이다. 코드는 넷만 한다:
    /// ① 루트를 <b>균일하게</b> 키워 불끝이 사거리에 닿게(프리팹의 "속도 × 수명" 을 재서 사거리로 나눈다 — 팩 단위계가 커도 상관없다, 입자는 Hierarchy 스케일),
    /// ② 콘 모양이면 반각을 판정 부채꼴 쪽으로 넓히고(<see cref="coneFraction"/>), ③ 모양(shape) 스케일로 세로로 눌러 도로를 안 뚫게(<see cref="flatten"/>),
    /// ④ 방출만 여닫는다(끊으면 남은 불이 제 수명만큼 타다 꺼진다). 진화 무기의 불 색(<c>flameTint</c>)은 안 입힌다 — 팩 머티리얼 색이 그대로.</para>
    ///
    /// <para><b>멈출 때(v11, 사용자 참고 영상 effect_01).</b> 총구 쪽 가는 심은 <see cref="snapOff"/> 안에 뚝 끊기고(영상: 줄기가 총구에서 떨어져 나간다), 몸통은 <see cref="fadeOut"/> 동안
    /// 떠오르며 검붉게 식어 녹는다. 끊는 순간 <see cref="SpawnStopDebris"/> 가 남은 불 입자 자리에서 연기(Smoke) · 불꽃 파편(Fragments) · 불똥(Sparks)을 내어
    /// "불이 연기와 파편으로 바뀐다". 총구 심(Head)은 <see cref="muzzleLinger"/> 동안 가늘어지며 꺼진다. 프리팹 쪽 시스템 이름이 약속이다(FxPackImport.StyleFlameJet).</para>
    ///
    /// <para>크기는 세트 jet 슬롯의 scale(사거리에 맞춘 뒤 곱한다)로, 넓이 · 눌림은 이 컴포넌트의 값으로 맞춘다. 카탈로그 항목에 프리팹이 없으면 경고 한 줄, 판정만 돈다.</para>
    /// </summary>
    public class ProtoFlamePack : MonoBehaviour, IProtoFlameFx
    {
        /// 분사 상태를 유지하는 시간 — 발사 간격(0.20초)보다 길어야 연사 중 방출이 안 끊긴다(ProtoFlameCone 과 같다).
        /// 0.30 → 0.25(사용자 11-05 "불꽃 사라지는 게 느리다 — 총을 내리는데 아직 있다"): 발사 간격 0.20 보다는 길어야 한다(한 틀 33ms 여유).
        const float HoldTime = 0.25f;
        const float FallbackHeight = 0.36f;
        /// 수명이 이만큼 지난 입자부터 "몸통"으로 본다 — 그 앞은 총구에서 막 나온 가는 심(끊을 때 뚝 끊기고, 연기 · 파편 자리로도 안 쓴다).
        const float BodyAge = 0.3f;

        /// 입자가 수명 동안 가는 거리 중 "불"로 읽히는 몫. 팩 제트는 알파가 수명 끝까지 서서히 꺼져 끝 쪽은 거의 안 보인다 — 1 이면 불이 사거리보다 짧아 보인다.
        /// 0.8 → 0.62(사용자 11-05 "사거리가 적 앞쪽까지만 닿는다 — 끝까지"): 알파가 끝에서 꺼지고 끌림으로 느려져 눈에 보이는 불은 계산 거리보다 짧다 — 더 길게 세운다.
        [Range(0.3f, 1f)] public float visibleFraction = 0.62f;
        /// 불 시작점을 총구에서 이만큼(m) 앞으로(사용자 11-05 "조금 더 앞으로"). 늘림 빌보드의 뒤쪽 절반이 총구 뒤로 안 나오게.
        [Range(-0.3f, 0.6f)] public float muzzleForward = 0.2f;
        /// 콘 반각 = 판정 전각 × 0.5 × 이 값. 1 이면 판정 부채꼴을 꽉 채우고(맞는 곳에 불이 다 보인다 — 사용자 11-05 "총구에서 퍼져 나가는 부채꼴", 참고 영상의 부채꼴 ≈ 판정 30°), 작을수록 팩의 가는 제트. 원본보다 좁히지는 않는다.
        [Range(0.2f, 1f)] public float coneFraction = 1f;
        /// 세로 눌림(ProtoFlameCone.jetFlatten 과 같은 뜻) — 모양 스케일 y. 1 이면 입체 콘이라 불이 위아래로 똑같이 퍼져 도로를 뚫는다.
        [Range(0.1f, 1f)] public float flatten = 0.3f;
        /// 분사를 끊은 뒤 남은 불 몸통이 사그라드는 시간(초). 방출만 끊으면 총구 쪽부터 비고 끝의 덩어리만 남아 떠 있다가 뚝 사라진다(사용자 11-05 "중간에 뚝 끊긴다 — 서서히 사그라들게") —
        /// 그래서 남은 불 입자의 수명을 이 시간 안으로 당겨(색이 검붉게 식고 · 부풀고 · 녹아 꺼진다) 함께 꺼뜨린다. 연기 · 불똥 · 파편은 당기지 않는다 — 제 수명대로 떠올라 흩어진다(월드 공간).
        /// v11: 0.3 → 0.45 — 끊는 순간 연기 · 파편이 몸통 자리를 덮으므로 불이 조금 더 남아도 "아직 쏜다"로 안 읽힌다(영상은 몸통이 0.5초쯤 불로 남았다 연기가 된다).
        [Range(0.1f, 1.5f)] public float fadeOut = 0.45f;
        /// 끊는 순간 총구 쪽 가는 심(수명 BodyAge 전 입자)이 사라지는 시간(초) — 크기를 0 으로 줄인다(영상: 줄기가 총구에서 '뚝' 떨어져 나가고 몸통만 떠간다).
        [Range(0.02f, 0.3f)] public float snapOff = 0.1f;
        /// 사그라들 때 속도 감쇠(1/s). v10: 10 → 4 — 제자리에 서지 않고 불줄기가 총구에서 떨어져 조금 더 나아가며 꺼진다(참고 영상). v11: 3.
        public float fadeDrag = 3f;
        /// 사그라들 때 위로 떠오르는 가속(팩 단위/s², 루트 스케일을 따른다 — 월드 공간 시스템은 루트 스케일을 곱해 넣는다) — 남은 불덩이가 위로 말리며 연기로 바뀌는 느낌.
        public float fadeRise = 10f;
        /// 쏘기 시작할 때 방출을 25% → 100% 로 올리는 시간(초) — 첫 틀에 꽉 찬 불이 툭 생기지 않고 총구에서 피어나게.
        [Range(0f, 0.5f)] public float rampIn = 0.15f;
        /// 멈춘 뒤 총구 심(Head)이 가늘어지며 꺼지는 시간(초) — 영상: 줄기가 떨어져 나간 뒤 총구에 작은 불이 잠깐 남아 깜빡이다 꺼진다.
        [Range(0f, 1f)] public float muzzleLinger = 0.45f;
        /// 불줄기 끝 — 불 입자가 수명의 이 몫을 지나는 순간 그 자리에서 검은 연기가 핀다(사용자 11-05 "불 이펙트 끝에 검은 연기가 되는 느낌" → "끝부분에서 연기가 나오게").
        /// 불 입자는 0.65 까지 알파 1, 0.85 에 0.7, 끝에 0. v15: 0.7 → 0.62 — 불덩이가 아직 꽉 찬 끝부분에서 피어오르게(0.7 은 이미 녹아 가는 자리라 "불이 사라질 때 연기가 난다"로 보였다).
        [Range(0.4f, 0.95f)] public float tipAge = 0.62f;
        /// 끝을 지나는 불 입자 중 연기가 되는 몫 — 1 이면 전부(불 55~220/s 라 너무 많다), 0 이면 끝 연기 없음. v15: 0.3 → 0.25(연기가 불 앞에 그려지므로 조금만).
        [Range(0f, 1f)] public float tipSmokeChance = 0.25f;
        /// 멈추는 순간 남은 불 몸통 자리에 내는 연기 수 — 불이 연기로 바뀌는 자리(입자 자리 · 속도 · 크기를 물려받는다). v15: 8 → 5(연기는 분사 중 끝에서 나는 게 주, 멈출 땐 조금만).
        [Range(0, 16)] public int stopSmoke = 5;
        /// 멈추는 순간 몸통에서 떨어져 나오는 불꽃 파편 수.
        [Range(0, 40)] public int stopFragments = 18;
        /// 멈추는 순간 몸통에서 튀는 불똥 수.
        [Range(0, 60)] public int stopSparks = 28;
        /// 파편이 튀는 속도(팩 단위/s, 루트 스케일을 따른다) — 몸통 속도의 45% 에 더해진다.
        public float fragmentSpeed = 5f;
        /// 불똥이 튀는 속도(팩 단위/s, 루트 스케일을 따른다).
        public float sparkSpeed = 8f;

        ParticleSystem[] _ps = null;
        Color _tint = Color.white;
        float[] _angle0, _rate0, _rateFit;
        bool[] _linger;              // 연기 · 불똥 · 파편 · 총구 심 — 페이드가 수명을 당기지 않는다(제 수명대로 떠올라 흩어진다)
        ParticleSystem _root, _smoke, _fragments, _sparks, _head;
        int _headIdx = -1;
        float _onStart = -1f;
        bool _ramping;
        Vector3[] _shape0;
        GameObject _inst, _prefab;   // _prefab: 세운 카탈로그 프리팹 — 에디터가 복사본을 다시 만들면(FxPackImport 판 올림) 다른 에셋이 되므로 재생 중인 무대도 새 것으로 갈아 끼운다
        string _id;
        float _native = -1f;          // 프리팹 루트 제트의 속도 × 수명(프리팹 단위)
        float _lastPulse = -999f;
        bool _emitting;
        float _fadeStart = -1f, _headOff = -1f;
        ParticleSystem.Particle[] _buf;
        Transform _muzzle;
        float _range = -1f, _deg = -1f, _slot = -1f, _cone = -1f, _flat = -1f, _fwd = -1f, _vis = -1f;
        static bool _warned, _logged;
        ProtoFlameJetTuning.Knobs _baseKnobs;   // 프리팹이 맞춤값(ProtoFlameJetTuning)을 들고 오기 전의 내 값 — 그런 프리팹에서 벗어나면 되돌린다(인페르노 11-06)

        public void Configure(WeaponStats st, Transform muzzle)
        {
            _muzzle = muzzle;
            transform.localRotation = Quaternion.identity;
            Place();
            var set = WeaponFxSet.Of(st);
            string id = set != null && set.jet != null && set.jet.On ? set.jet.fxId : null;
            float slot = set != null && set.jet != null && set.jet.scale > 0f ? set.jet.scale : 1f;
            _tint = st.flameTint;
            if (_ps == null || _id != id || PrefabChanged(id)) Build(id);
            if (_ps.Length == 0) return;
            if (Mathf.Approximately(_range, st.range) && Mathf.Approximately(_deg, st.coneDegrees) && Mathf.Approximately(_slot, slot) &&
                Mathf.Approximately(_cone, coneFraction) && Mathf.Approximately(_flat, flatten) && Mathf.Approximately(_fwd, muzzleForward) && Mathf.Approximately(_vis, visibleFraction)) return;
            _range = st.range; _deg = st.coneDegrees; _slot = slot; _cone = coneFraction; _flat = flatten; _fwd = muzzleForward; _vis = visibleFraction;
            Fit(st, slot);
        }

        public void Pulse() => _lastPulse = Time.time;

        /// 카탈로그 항목의 프리팹이 세울 때와 다른 에셋인가 — 에디터가 복사본을 지우고 다시 만들면(FxPackImport) 참조가 바뀐다. 무대(FxWeaponStage)가 재생 중이어도 새 프리팹이 바로 보이게.
        bool PrefabChanged(string id)
        {
            if (_inst == null || string.IsNullOrEmpty(id)) return false;
            var cat = FxCatalog.Load();
            var e = cat != null ? cat.Find(id) : null;
            return e != null && e.prefab != null && e.prefab != _prefab;
        }

        void Place()
        {
            if (_muzzle != null) transform.position = _muzzle.position;
            else transform.localPosition = new Vector3(0f, FallbackHeight, 0f);
        }

        /// 카탈로그 항목의 프리팹을 자식으로 세우고 원본 값(콘 반각 · 모양 스케일 · 방출률)과 "속도 × 수명"을 기억한다.
        void Build(string id)
        {
            if (_inst != null) Destroy(_inst);
            _inst = null; _id = id; _ps = new ParticleSystem[0]; _native = -1f;
            _range = -1f;   // 다시 맞추게
            var cat = FxCatalog.Load();
            var e = cat != null && !string.IsNullOrEmpty(id) ? cat.Find(id) : null;
            var prefab = e != null ? e.prefab : null;
            if (prefab == null)
            {
                if (!_warned) { _warned = true; Debug.LogWarning($"[화염] 카탈로그 {id} 에 프리팹이 없다 — 판정만 돌고 불은 안 보인다(무기 연출 세트 jet 슬롯 확인 — 지금 화염은 inferno_jet)"); }
                return;
            }
            _inst = Instantiate(prefab, transform);
            _prefab = prefab;
            _inst.name = "Jet";
            _inst.transform.localPosition = Vector3.zero;
            _inst.transform.localRotation = Quaternion.identity;
            _inst.transform.localScale = Vector3.one;
            // 프리팹이 제 맞춤값을 들고 오면 그 값으로(인페르노 — 미터 단위 · 끝에서 연기가 되는 불), 없는 프리팹으로 돌아가면 원래 값으로
            var tune = _inst.GetComponent<ProtoFlameJetTuning>();
            if (tune != null) { if (_baseKnobs == null) _baseKnobs = ProtoFlameJetTuning.Knobs.From(this); tune.knobs.To(this); }
            else if (_baseKnobs != null) { _baseKnobs.To(this); _baseKnobs = null; }
            _ps = _inst.GetComponentsInChildren<ParticleSystem>(true);
            _angle0 = new float[_ps.Length]; _rate0 = new float[_ps.Length]; _rateFit = new float[_ps.Length]; _shape0 = new Vector3[_ps.Length];
            _linger = new bool[_ps.Length]; _smoke = null; _fragments = null; _sparks = null; _head = null; _headIdx = -1;
            _root = _inst.GetComponent<ParticleSystem>();
            for (int i = 0; i < _ps.Length; i++)
            {
                var ps = _ps[i];
                switch (ps.name)
                {
                    case "Smoke":     _smoke = ps; break;
                    case "Fragments": _fragments = ps; break;
                    case "Sparks":    _sparks = ps; break;
                    case "Head":      _head = ps; _headIdx = i; break;
                }
                _linger[i] = ps == _smoke || ps == _fragments || ps == _sparks || ps == _head || ps.name == "Embers" ||
                             ProtoFlameJetTuning.IsCharge(ps.name) || ProtoFlameJetTuning.IsGround(ps.name);   // 점화 · 바닥 불은 제 수명대로(인페르노)
                var main = ps.main;
                main.loop = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // 루트를 줄이면 입자 · 속도 · 모양이 같이 줄게
                main.stopAction = ParticleSystemStopAction.None;
                var sh = ps.shape; _angle0[i] = sh.angle; _shape0[i] = sh.scale;
                var em = ps.emission; _rate0[i] = Max(em.rateOverTime); _rateFit[i] = _rate0[i];
                em.enabled = false;                                       // 방출은 LateUpdate 가 여닫는다
                ps.Play();
            }
            // 사거리 기준은 루트 제트(없으면 앞으로 가는 것 중 가장 긴 것)
            if (_root != null) _native = Travel(_root);
            if (_native <= 0f) foreach (var ps in _ps) _native = Mathf.Max(_native, Travel(ps));
            if (_native <= 0f) _native = 1f;
            _emitting = false;
            _fadeStart = -1f; _headOff = -1f;
        }

        /// 입자가 수명 동안 가는 거리(프리팹 단위). 끌림(limitVelocityOverLifetime.drag)이 있으면 v/d·(1−e^(−d·t)) — 복사본 v2 는 끝에서 모이게 끌림 0.9 를 쓴다.
        static float Travel(ParticleSystem ps)
        {
            var m = ps.main;
            float v = Max(m.startSpeed), life = Max(m.startLifetime);
            if (v <= 0f || life <= 0f || life > 60f) return 0f;
            var lim = ps.limitVelocityOverLifetime;
            float d = lim.enabled ? Max(lim.drag) : 0f;
            if (d > 0.001f) return v / d * (1f - Mathf.Exp(-d * life));
            return v * life;
        }

        static float Max(ParticleSystem.MinMaxCurve c)
        {
            switch (c.mode)
            {
                case ParticleSystemCurveMode.Constant:     return c.constant;
                case ParticleSystemCurveMode.TwoConstants: return Mathf.Max(c.constantMin, c.constantMax);
                default:                                   return c.curveMultiplier;
            }
        }

        /// 사거리 · 판정 각 · 슬롯 배율에 맞춘다. 같은 값이면 Configure 가 여기까지 안 온다.
        void Fit(WeaponStats st, float slot)
        {
            float reach = Reach(st);
            // (시트 화염 ProtoFlameSheetFx 갈래는 11-23 화염방사기 시트를 지우며 뺐다 — 백업 _Backup/20261123_SujinFolder)
            float s = reach / (_native * Mathf.Max(0.3f, visibleFraction)) * slot;
            _inst.transform.localScale = Vector3.one * s;
            _inst.transform.localPosition = new Vector3(0f, 0f, muzzleForward);
            float half = Mathf.Clamp(st.coneDegrees * 0.5f * coneFraction, 1f, 89f);
            for (int i = 0; i < _ps.Length; i++)
            {
                var ps = _ps[i];
                var sh = ps.shape;
                if (sh.enabled && (sh.shapeType == ParticleSystemShapeType.Cone || sh.shapeType == ParticleSystemShapeType.ConeVolume))
                {
                    float a = Mathf.Max(_angle0[i], half);
                    sh.angle = a;
                    // 넓힌 만큼 방출을 늘린다(각 비율의 0.75제곱, 최대 4배) — 가는 제트(5.8°)를 15° 부채꼴로 펴면 밀도가 1/2.6 로 성기므로
                    var em = ps.emission;
                    if (_rate0[i] > 0f) { _rateFit[i] = _rate0[i] * Mathf.Clamp(Mathf.Pow(a / Mathf.Max(0.5f, _angle0[i]), 0.75f), 1f, 4f); em.rateOverTime = _rateFit[i]; }
                }
                sh.scale = Vector3.Scale(_shape0[i], new Vector3(1f, flatten, 1f));
            }
            if (!_logged)
            {
                _logged = true;
                Debug.Log($"[화염] 팩 제트 세움 — {_id}: 프리팹 거리 {_native:0.0} 유닛 → 사거리 {reach:0.00}m, 루트 ×{s:0.000}, 콘 반각 {half:0.#}°(판정 {st.coneDegrees}°), 눌림 {flatten}, 총구 {(_muzzle != null ? _muzzle.name + " " + _muzzle.position.ToString("0.00") : "없음(발밑 대비책)")}");
            }
        }

        /// 불이 뻗어야 하는 길이 — 판정은 플레이어에서 st.range 까지인데 그림은 총구에서 시작하므로 총구가 앞선 만큼 뺀다(ProtoFlameCone.Reach 와 같다).
        float Reach(WeaponStats st)
        {
            float ahead = Mathf.Clamp(transform.localPosition.z + muzzleForward, 0f, st.range * 0.5f);
            return Mathf.Max(0.5f, st.range - ahead);
        }

        void LateUpdate()
        {
            Place();
            bool aligned = ProtoFlameCone.AimAlongBarrel(transform, _muzzle);
            bool on = aligned && Time.time - _lastPulse < HoldTime;
            if (_ps != null && _emitting != on)
            {
                _emitting = on;
                foreach (var ps in _ps) { if (ps == _head) continue; var em = ps.emission; em.enabled = on; }   // 총구 심은 따로(멈춘 뒤 잠깐 남는다)
                if (on)
                {
                    _fadeStart = -1f; _headOff = -1f;   // 다시 쏘면 페이드 그만(남은 입자는 이어 탄다)
                    _onStart = Time.time; _ramping = rampIn > 0f; if (_ramping) TickRamp();
                    if (_head != null) { var em = _head.emission; em.enabled = true; em.rateOverTime = _rateFit[_headIdx]; }
                }
                else
                {
                    _fadeStart = Time.time; _ramping = false;   // 끊는 순간부터 사그라든다
                    _headOff = _head != null ? Time.time : -1f;
                    SpawnStopDebris();                          // 남은 불 몸통 자리에서 연기 · 파편 · 불똥 — 불이 연기와 파편으로 바뀐다
                }
            }
            if (on && _ramping) TickRamp();
            if (on) TickTipSmoke();                          // 분사 중 — 불줄기 끝이 검은 연기가 된다(멈춘 뒤는 SpawnStopDebris · TickFade 몫)
            if (!on && _fadeStart >= 0f) TickFade();
            if (!on && _headOff >= 0f) TickMuzzle();
        }

        /// 분사 중 틀마다 — 루트 불 입자 중 이번 틀에 수명 tipAge 를 넘은 것(= 불줄기 끝에 닿아 꺼지기 시작하는 것)을 골라 그 자리에서 연기를 낸다.
        /// 모양(콘 부피)에서 아무 데나 태어나던 v10~12 연기는 불과 따로 노는 덩어리로 보였다 — 불 입자가 실제로 있는 자리(월드 공간이라 줄기가 휘어도 따라간다)에서만 피운다.
        void TickTipSmoke()
        {
            if (_root == null || _smoke == null || tipSmokeChance <= 0f) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            int max = _root.main.maxParticles;
            if (_buf == null || _buf.Length < max) _buf = new ParticleSystem.Particle[max];
            int n = _root.GetParticles(_buf);
            if (n == 0) return;
            bool world = _root.main.simulationSpace == ParticleSystemSimulationSpace.World;
            float unit = _inst.transform.lossyScale.x;
            var tr = _root.transform;
            float step = dt * _root.main.simulationSpeed;   // 이번 틀에 늙은 만큼
            for (int i = 0; i < n; i++)
            {
                var p = _buf[i];
                float life = p.startLifetime;
                if (life <= 0f) continue;
                float ageNow = 1f - p.remainingLifetime / life;
                float agePrev = 1f - (p.remainingLifetime + step) / life;
                if (agePrev >= tipAge || ageNow < tipAge) continue;          // 이번 틀에 끝을 넘은 입자만 — 한 입자당 한 번
                if (Random.value >= tipSmokeChance) continue;
                Pos(p, world, tr, out var pos, out var vel);
                // v14: 불 속도를 물려받아 불줄기 방향으로 날아가며 흩어진다(사용자 "검은 연기로 변해서 날아가면서 사라지게"). v15: 60% + 위로 1.2 — 끝부분 위로 피어오르며 앞으로
                EmitSmoke(pos, vel * 0.6f + Vector3.up * (1.2f * unit), p.GetCurrentSize(_root) * 0.9f, Random.Range(0.8f, 1.2f));
            }
        }

        /// 연기 한 덩이 — 불 입자의 자리 · 속도 · 크기(size: 불 입자의 현재 크기, 연기 시스템과 같은 단위)를 물려받는다.
        /// 연기 크기 곡선(0.6 → 1 → 1.4)이 위에 곱해지므로 불덩이의 0.6배로 태어나 1.4배로 부풀며 흩어진다.
        void EmitSmoke(Vector3 pos, Vector3 vel, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
            };
            _smoke.Emit(ep, 1);
        }

        /// 쏘기 시작 rampIn 동안 불 방출을 25% → 100% (연기 · 불똥 · 파편 · 심은 처음부터 — 연기는 알파가 서서히 들어온다).
        void TickRamp()
        {
            float t = rampIn > 0f ? (Time.time - _onStart) / rampIn : 1f;
            float f = t >= 1f ? 1f : Mathf.Lerp(0.25f, 1f, t);
            for (int i = 0; i < _ps.Length; i++)
            {
                if (_linger[i]) continue;
                var em = _ps[i].emission;
                em.rateOverTime = _rateFit[i] * f;
            }
            if (t >= 1f) _ramping = false;
        }

        /// 멈춘 뒤 총구 심(Head)의 방출을 muzzleLinger 동안 줄여 끈다 — 입자 수명이 짧아(0.2~0.35초) 방출이 줄면 곧 가늘어지며 꺼진다.
        void TickMuzzle()
        {
            if (_head == null) { _headOff = -1f; return; }
            float t = muzzleLinger > 0f ? (Time.time - _headOff) / muzzleLinger : 1f;
            var em = _head.emission;
            if (t >= 1f) { em.enabled = false; _headOff = -1f; return; }
            float k = 1f - t;
            em.rateOverTime = _rateFit[_headIdx] * k * k;
        }

        /// 분사를 끊은 뒤 남은 불 입자를 꺼뜨린다(v10, 참고 영상: 불줄기가 총구에서 떨어져 나가 떠오르며 연기로 바뀐다) —
        /// 몸통(수명 BodyAge 지난 입자)은 fadeOut 동안: 속도는 조금만 줄이고(fadeDrag — 떨어져 나아간다) 위로 띄우며(fadeRise), 남은 수명을 남은 페이드 시간에 맞춰 당긴다
        /// (색이 검붉게 식고 · 크기 곡선대로 부풀고 · 셰이더 커스텀 데이터로 녹아 꺼진다). 총구 쪽 가는 심(어린 입자)은 snapOff 동안 크기를 0 으로 — 수명을 당기면 곡선이 끝으로 달려
        /// 총구 앞에서 부풀어 튀므로 크기만 줄인다(v11, 영상: 총구에서 '뚝' 끊긴다). 연기 · 불똥 · 파편 · 심(_linger)은 손대지 않는다.
        void TickFade()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _ps == null) return;
            float elapsed = Time.time - _fadeStart;
            float left = fadeOut - elapsed;
            if (left <= 0f)
            {
                for (int j = 0; j < _ps.Length; j++) if (!_linger[j]) _ps[j].Clear(false);
                _fadeStart = -1f;
                return;
            }
            float k = Mathf.Clamp01(dt / left);               // 이번 틀에 줄일 몫 — 남은 시간 끝에 정확히 0
            float leftNear = snapOff - elapsed;
            float kNear = leftNear > 0f ? Mathf.Clamp01(dt / leftNear) : 1f;
            float drag = Mathf.Exp(-fadeDrag * dt);
            for (int j = 0; j < _ps.Length; j++)
            {
                if (_linger[j]) continue;
                var ps = _ps[j];
                int max = ps.main.maxParticles;
                if (_buf == null || _buf.Length < max) _buf = new ParticleSystem.Particle[max];
                int n = ps.GetParticles(_buf);
                if (n == 0) continue;
                // 월드 공간이면 속도가 월드 단위(팩 단위 × 루트 스케일) — 떠오름도 같은 단위로
                float unit = ps.main.simulationSpace == ParticleSystemSimulationSpace.World ? ps.transform.lossyScale.x : 1f;
                Vector3 rise = new Vector3(0f, fadeRise * unit * dt, 0f);
                for (int i = 0; i < n; i++)
                {
                    var p = _buf[i];
                    p.velocity = p.velocity * drag + rise;
                    if (p.startLifetime > 0f && 1f - p.remainingLifetime / p.startLifetime < BodyAge)
                    {
                        // 총구 쪽 가는 심 — 끊긴다
                        if (kNear >= 1f) p.remainingLifetime = 0f;
                        else p.startSize *= 1f - kNear;
                    }
                    else p.remainingLifetime = Mathf.Max(0f, p.remainingLifetime - p.remainingLifetime * k);
                    _buf[i] = p;
                }
                ps.SetParticles(_buf, n);
            }
        }

        /// 끊는 순간 — 루트 제트에 남은 불 몸통 입자(수명 BodyAge 지난 것) 자리에서 연기 · 불꽃 파편 · 불똥을 낸다(v11, 참고 영상 effect_01:
        /// 떨어져 나간 몸통이 검붉은 연기가 되고 그 사이로 작은 불꽃 조각과 불똥이 사방으로 흩어진다). 입자의 자리 · 속도 · 크기를 물려받아 "그 불이 바뀐 것"으로 읽히게.
        /// 자리는 월드 — 연기 · 파편 · 불똥 시스템은 모두 월드 공간이라 EmitParams.position 을 월드로 준다(루트가 로컬 공간이면 변환한다).
        void SpawnStopDebris()
        {
            if (_root == null || _inst == null) return;
            int max = _root.main.maxParticles;
            if (_buf == null || _buf.Length < max) _buf = new ParticleSystem.Particle[max];
            int n = _root.GetParticles(_buf);
            if (n == 0) return;
            bool world = _root.main.simulationSpace == ParticleSystemSimulationSpace.World;
            float unit = _inst.transform.lossyScale.x;   // 팩 단위 → 월드 단위
            // 몸통만 앞으로 모은다 — 총구 쪽 가는 심은 그냥 끊긴다
            int body = 0;
            for (int i = 0; i < n; i++)
            {
                var p = _buf[i];
                if (p.startLifetime > 0f && 1f - p.remainingLifetime / p.startLifetime >= BodyAge) _buf[body++] = p;
            }
            if (body == 0) return;
            var tr = _root.transform;
            Vector3 up = Vector3.up;

            // 연기는 불 입자 크기를 물려받고(EmitSmoke — GetParticles 와 EmitParams 는 같은 입자 단위), 파편 · 불똥은 제 시스템 값(0.5~1 · 0.12~0.26 팩 단위, Hierarchy 스케일대로).
            // 흩뿌리는 반경 · 속도는 팩 단위 × 루트 스케일 = 월드 단위.
            float spread = 1f * unit;
            int smokeN = _smoke != null ? Mathf.Min(stopSmoke, body) : 0;
            for (int j = 0; j < smokeN; j++)
            {
                var p = _buf[Pick(j, smokeN, body)];
                Pos(p, world, tr, out var pos, out var vel);
                // v12: 수명 1.1~1.6 → 0.8~1.2(사용자 "연기가 너무 오래 살아 있다") — 영상의 연기는 1초 안에 흩어진다. v14: 몸통 속도의 60% 로 날아가며
                EmitSmoke(pos, vel * 0.6f + up * (1.0f * unit), p.GetCurrentSize(_root), Random.Range(0.8f, 1.2f));
            }
            int fragN = _fragments != null ? Mathf.Min(stopFragments, body * 2) : 0;
            for (int j = 0; j < fragN; j++)
            {
                var p = _buf[Pick(j, fragN, body)];
                Pos(p, world, tr, out var pos, out var vel);
                var ep = new ParticleSystem.EmitParams
                {
                    applyShapeToPosition = false,
                    position = pos + Random.insideUnitSphere * spread,
                    velocity = vel * 0.45f + Random.onUnitSphere * (fragmentSpeed * unit * Random.Range(0.6f, 1.2f)) + up * (2f * unit),
                };
                _fragments.Emit(ep, 1);
            }
            int sparkN = _sparks != null ? Mathf.Min(stopSparks, body * 3) : 0;
            for (int j = 0; j < sparkN; j++)
            {
                var p = _buf[Pick(j, sparkN, body)];
                Pos(p, world, tr, out var pos, out var vel);
                var ep = new ParticleSystem.EmitParams
                {
                    applyShapeToPosition = false,
                    position = pos + Random.insideUnitSphere * spread,
                    velocity = vel * 0.3f + Random.onUnitSphere * (sparkSpeed * unit * Random.Range(0.5f, 1.3f)) + up * (3f * unit),
                };
                _sparks.Emit(ep, 1);
            }
        }

        /// j 번째(count 중)가 고를 몸통 입자 — 고른 간격에 무작위를 섞어 몸통 전체에 퍼지게.
        static int Pick(int j, int count, int body) => Mathf.Clamp(Mathf.FloorToInt((j + Random.value) * body / count), 0, body - 1);

        /// 입자의 자리 · 속도를 월드로.
        static void Pos(in ParticleSystem.Particle p, bool world, Transform tr, out Vector3 pos, out Vector3 vel)
        {
            if (world) { pos = p.position; vel = p.velocity; }
            else { pos = tr.TransformPoint(p.position); vel = tr.TransformVector(p.velocity); }
        }
    }
}
