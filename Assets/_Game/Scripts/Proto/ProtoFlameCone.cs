using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 화염방사기의 화염 (GDD §05 Cone). Unity Particle Pack 의 <c>FlameThrower</c> 에서
    /// 화염·잉걸불만 떼어낸 <c>Resources/VFX/FX_FlameJet</c> 을 구동한다.
    ///
    /// <para><b>절차적 부채꼴은 걷어냈다.</b> 예전에는 <c>Proto/FlameCone</c> 셰이더로 지면에
    /// 누운 부채꼴 메시를 구워서 판정 범위를 그렸다. 지금은 파티클만 쓴다 —
    /// <c>Proto_FlameCone.shader</c> 와 <c>M_FlameCone.mat</c> 은 지우지 않고 남겨 뒀지만
    /// <b>아무도 참조하지 않는다.</b> 되돌리려면 이 클래스의 히스토리를 보면 된다.</para>
    ///
    /// <para><b>그래서 판정 범위는 파티클이 혼자 보여줘야 한다.</b> 콘 각도·속도·크기를
    /// <see cref="WeaponStats.coneDegrees"/>·<see cref="WeaponStats.range"/> 에서 역산하는 이유가
    /// 그것이다. 팩 원본은 1° 직선 제트에 10m 였다 — 손으로 맞춘 값을 박아두면 밸런스가
    /// 사거리를 고치는 순간 그림과 판정이 어긋나고, 그 어긋남은 플레이어에게
    /// "맞았는데 안 맞은 것처럼 보인다"로 나타난다.</para>
    ///
    /// <para><b>납작하게 눌러야 한다.</b> 판정은 지면에 누운 <b>평면</b> 부채꼴인데 파티클 콘은
    /// <b>입체</b>라 위아래로도 똑같이 퍼진다. 그대로 두면 불이 도로를 뚫고 내려가 노면 밑에서
    /// 불줄기가 삐져나온다 — <see cref="jetFlatten"/> 참고.</para>
    /// </summary>
    public class ProtoFlameCone : MonoBehaviour, IProtoFlameFx
    {
        /// <summary>
        /// 분사 상태를 유지하는 시간. <see cref="Pulse"/> 는 <b>발사 간격(0.20초)마다 한 번</b>
        /// 불리므로 이 값이 그보다 짧으면 연사 중에도 방출이 끊긴다.
        ///
        /// <para><b>원래 0.05초였고, 그게 버그였다.</b> 0.05초 켜고 0.15초 끄는 25% 듀티로
        /// 돌아서 입자가 정상 상태의 15%(147알 중 23알)밖에 안 살아 있었다. 불이 한 덩어리로
        /// 이어지지 않고 덩어리 몇 개로 끊겨 보이던 원인이 이것이다.</para>
        ///
        /// <para>분사를 끊은 뒤의 사그라듦은 따로 만들 필요가 없다 — 방출만 멈추면 남은 입자가
        /// 제 수명(0.60~0.74초)만큼 타다가 꺼진다. 예전에 있던 <c>FadeInTime</c>/<c>FadeOutTime</c>
        /// 과 <c>_fade</c> 는 <b>계산만 하고 아무 데도 쓰이지 않던 죽은 코드</b>라 걷어냈다.</para>
        /// </summary>
        const float HoldTime = 0.30f;

        /// 총구를 못 받았을 때 쓰는 발밑 기준 높이. 0 이면 불이 노면에 파묻히고,
        /// 너무 높으면 좀비 위에 떠 보인다. <b>이건 어디까지나 대비책이다</b> —
        /// 정상 경로에서는 <see cref="ProtoShooter.muzzle"/> 를 받아 거기서 쏜다.
        const float FallbackHeight = 0.36f;

        /// 파티클 제트 프리팹. 씬 배선 없이 잡으려고 Resources 아래 둔다 (번개·SFX 와 같은 이유).
        public const string JetPath = "VFX/FX_FlameJet";

        /// 입자 한 알의 수명(초). 속도는 <c>사거리 / 이 값</c> 으로 역산하므로,
        /// 짧을수록 빠르고 촘촘한 제트가 된다. 0.5 를 넘기면 불이 사거리 밖으로 흘러나간다.
        const float JetLife = 0.34f;

        /// <summary>
        /// 파티클 콘을 세로로 누르는 배율. 1 이면 입체 원뿔이라 불이 위아래로 똑같이 퍼져
        /// <b>도로를 뚫고 내려간다</b>(노면 밑에서 불줄기가 삐져나온다). 판정이 평면 부채꼴이므로
        /// 그림도 납작해야 맞다. 0 에 가까우면 종이처럼 얇아진다.
        ///
        /// <para>구현은 <c>scalingMode = Shape</c> 다 — 스케일을 <b>모양에만</b> 걸고 입자 크기·속도에는
        /// 안 걸기 때문에 사거리를 유지한 채 위아래 퍼짐만 줄어든다. <c>Hierarchy</c> 로 두면
        /// 크기와 속도까지 같이 눌려서 불이 쪼그라든다.</para>
        /// </summary>
        [Range(0.1f, 1f)] public float jetFlatten = 0.26f;

        ParticleSystem[] _jet;
        float _lastPulse = -999f;
        bool _emitting;
        Transform _muzzle;

        float _jetDegrees = -1f, _jetRange = -1f, _jetFlatten = -1f;
        Color _jetTint = new Color(-1f, -1f, -1f, -1f);
        public const string TintedMatPath = "VFX/M_FlameJet_Tinted";
        Material _flameMatOrig;
        Gradient _emberGradOrig;
        static Material _tintedMat;

        /// <summary>수치가 바뀌었으면 제트를 다시 맞춘다. 같은 값이면 아무것도 하지 않는다.</summary>
        public void Configure(WeaponStats st, Transform muzzle)
        {
            _muzzle = muzzle;
            transform.localRotation = Quaternion.identity;
            PlaceAtMuzzle();
            ConfigureJet(st);
        }

        /// <summary>
        /// 총구로 옮긴다. <b>매 프레임 해야 한다</b> — 총구가 지금은 Player 바로 아래 고정
        /// 자식이지만, 나중에 손 본이나 무기 모델에 붙으면 애니메이션을 따라 움직인다.
        /// 그때 이 코드를 고칠 필요가 없게 월드 좌표로 따라간다.
        ///
        /// <para>회전은 여기서 안 한다 — <see cref="AimAlongBarrel"/> 가 총열의 <b>수평 방향만</b> 따른다.
        /// 예전에는 몸 정면(부모 회전)을 그대로 썼는데, 총이 정면을 덜 보는 순간 불이 '옆에서' 나가 보였다.
        /// 위아래(피치)는 버린다 — 손이 위아래로 흔들릴 때 불줄기가 땅을 파고들거나 하늘로 솟으면 안 된다.</para>
        /// </summary>
        void PlaceAtMuzzle()
        {
            if (_muzzle != null) transform.position = _muzzle.position;
            else transform.localPosition = new Vector3(0f, FallbackHeight, 0f);
        }

        /// 파티클 제트를 붙이고 무기 수치에 맞춘다. 프리팹이 없으면 경고 한 줄만 남기고
        /// <b>조용히 판정만 돌게 둔다</b> — 이펙트 하나 때문에 무기가 안 나가면 안 된다.
        void ConfigureJet(WeaponStats st)
        {
            if (_jet == null)
            {
                var prefab = Resources.Load<GameObject>(JetPath);
                if (prefab == null)
                {
                    Debug.LogWarning("[화염] " + JetPath + " 가 없다 — 판정만 돌고 불은 안 보인다");
                    _jet = new ParticleSystem[0];
                    return;
                }
                var go = Instantiate(prefab, transform);
                go.name = "Jet";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                _jet = go.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in _jet)
                {
                    ps.Play();                       // 방출 자체는 emission.enabled 로 여닫는다
                    var em0 = ps.emission; em0.enabled = false;
                }
                _emitting = false;
            }
            if (_jet.Length == 0) return;
            if (Mathf.Approximately(_jetDegrees, st.coneDegrees) &&
                Mathf.Approximately(_jetRange, st.range) &&
                Mathf.Approximately(_jetFlatten, jetFlatten) &&
                _jetTint == st.flameTint) return;

            _jetDegrees = st.coneDegrees;
            _jetRange = st.range;
            _jetFlatten = jetFlatten;
            _jetTint = st.flameTint;
            Color tint = st.flameTint;   // 진화 무기(인페르노) 불 색. 흰색이면 기본 불 그대로

            foreach (var ps in _jet)
            {
                var m = ps.main;
                var sh = ps.shape;
                bool ember = ps.name == "Embers";

                // Unity 의 Cone angle 은 **반각**이다. coneDegrees 는 전각이라 절반으로 넣는다.
                // 부채꼴 메시를 걷어낸 뒤로는 **불이 판정 범위를 혼자 그린다.** 그래서 전각을
                // 깎지 않는다 — 깎으면 실제로는 맞는데 안 맞는 것처럼 보이는 가장자리가 생긴다.
                // 잉걸불만 조금 좁게 둔다(밖으로 새는 불똥이 판정 밖까지 맞는 인상을 준다).
                sh.angle = st.coneDegrees * 0.5f * (ember ? 0.85f : 1f);
                sh.radius = 0.10f;

                // 수명을 길게 잡으면 동시에 살아 있는 입자가 늘어 **서로 더 겹친다** —
                // "불 여러 개"가 "불 하나"로 보이는 데 크기 다음으로 큰 레버다.
                // 속도가 사거리/수명이라 사거리는 그대로 유지되고 불만 느리게 뭉실거린다.
                float life = (ember ? JetLife * 1.25f : JetLife) * 1.75f;
                m.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.75f, life);
                // 속도는 사거리에서 역산한다. 수명이 다할 때 딱 사거리 끝에 닿는다.
                // 판정은 플레이어에서 st.range 까지인데 그림은 총구에서 시작하므로,
                // 총구가 앞선 만큼 짧게 뻗어야 **불끝과 판정 끝이 같은 자리**에 온다.
                float reach = Reach(st);
                m.startSpeed = new ParticleSystem.MinMaxCurve(reach / life * 0.82f,
                                                              reach / life * 1.00f);
                // **크고 적게.** 작은 입자를 많이 뿌리면 알갱이가 하나하나 보여서 "불 여러 개"로
                // 읽힌다. 레퍼런스처럼 "불 하나가 넓게" 보이려면 입자가 서로 충분히 겹쳐야 한다.
                // 다만 2m 를 넘기면 캐릭터를 덮고 콘 모양 자체가 사라진다 — 1.4m 근처가 균형점.
                // 입자 크기는 **사거리가 아니라 부채꼴 폭**에서 뽑는다. 예전에는 사거리에만
                // 걸려 있었는데(range * 0.32), 그러면 콘 각도를 좁히는 순간 입자가 부채꼴보다
                // 커져서 불이 판정 밖으로 삐져나온다 — 콘을 50°에서 30°로 좁혔을 때 부채꼴은
                // 4.20m -> 2.41m 로 줄었는데 입자는 1.44m 그대로였다.
                // 계수 0.343 은 예전 값을 콘 50° 기준으로 환산한 것이라 그때와 같은 그림이 나온다.
                float fanW = 2f * st.range * Mathf.Tan(Mathf.Clamp(st.coneDegrees, 1f, 170f) * 0.5f * Mathf.Deg2Rad);
                float sz = ember ? st.range * 0.035f : fanW * 0.343f;
                m.startSize = new ParticleSystem.MinMaxCurve(sz * 0.75f, sz);

                // 같은 스프라이트가 반복되는 티를 없앤다. 큰 입자일수록 반복이 눈에 띈다.
                if (!ember)
                {
                    m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                    // flipRotation 0.5 = 절반은 반대로 돈다. 예전 이름은 randomizeRotationDirection
                    // 인데 유니티가 이름을 바꿨고(UnityUpgradable), 옛 이름을 쓰면 컴파일할 때마다
                    // "Script Updating Consent" 창이 떠서 에디터를 막는다.
                    // 값도 1 이 아니라 0.5 가 맞다 — 1 은 '전부 반대로'(= 그냥 균일한 역방향)다.
                    m.flipRotation = 0.5f;
                    var rol = ps.rotationOverLifetime;
                    rol.enabled = true;
                    rol.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
                }

                // HDR 로 태운다. 1 을 넘겨야 블룸 임계값(0.90)을 통과해 번진다 —
                // 이게 없으면 가법으로 바꿔도 '주황색 종이'에 머문다.
                bool tinted = tint != Color.white;
                // 불 텍스처(Fireball)가 이미 주황이라 색을 곱해서는 파래지지 않는다. 색을 입힐 때는
                // 파티클 셰이더의 Color 모드(_COLORCOLOR_ON — 밝기는 텍스처, 색상은 입자 색) 머티리얼로 갈아 끼운다.
                // 키워드 변형이 빌드에서 빠지지 않게 머티리얼을 Resources 에 에셋으로 둔다.
                if (ember)
                {
                    // 잉걸불은 프리팹의 주황 그라디언트를 쓴다 — 색을 입힐 때만 흰색 → 그 색으로 바꾸고, 원본은 기억해 둔다
                    var ecol = ps.colorOverLifetime;
                    if (_emberGradOrig == null && ecol.enabled) _emberGradOrig = ecol.color.gradient;
                    if (tinted)
                    {
                        var eg = new Gradient();
                        eg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(tint, 1f) },
                                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                        ecol.enabled = true;
                        ecol.color = new ParticleSystem.MinMaxGradient(eg);
                    }
                    else if (_emberGradOrig != null) ecol.color = new ParticleSystem.MinMaxGradient(_emberGradOrig);
                }
                else
                {
                    var pr = ps.GetComponent<ParticleSystemRenderer>();
                    if (pr != null)
                    {
                        if (_flameMatOrig == null) _flameMatOrig = pr.sharedMaterial;
                        if (tinted && _tintedMat == null) _tintedMat = Resources.Load<Material>(TintedMatPath);
                        pr.sharedMaterial = tinted && _tintedMat != null ? _tintedMat : _flameMatOrig;
                    }
                }
                m.startColor = ember
                    ? new ParticleSystem.MinMaxGradient(tinted ? new Color(2.4f * tint.r, 2.4f * tint.g, 2.4f * tint.b, 1f)
                                                               : new Color(2.4f, 1.10f, 0.30f, 1f))
                    : new ParticleSystem.MinMaxGradient(new Color(1.9f, 1.9f, 1.9f, 1f));

                // 수명에 따른 색. 총구 쪽은 백열, 끝으로 갈수록 주황 -> 붉은색으로 식는다.
                // 팩 원본 그라디언트는 흰색에서 흰색으로 가는 거나 마찬가지라 통째로 노란
                // 덩어리로 보였다. 불은 '온도 차'가 보여야 불로 읽힌다.
                if (!ember)
                {
                    var col = ps.colorOverLifetime;
                    col.enabled = true;
                    var g = new Gradient();
                    // 색을 입힌 불(인페르노)은 같은 온도 차를 그 색으로 — 백열 → 옅은 색 → 색 → 어두운 색.
                    // 주황 그라디언트에 곱하면 탁한 회색이 되므로 곱하지 않고 통째로 바꾼다.
                    var keys = tinted
                        ? new GradientColorKey[] {
                            new GradientColorKey(new Color(1.00f, 1.00f, 1.00f), 0.00f),
                            new GradientColorKey(Color.Lerp(Color.white, tint, 0.55f), 0.22f),
                            new GradientColorKey(tint, 0.58f),
                            new GradientColorKey(tint * 0.45f, 1.00f) }
                        : new GradientColorKey[] {
                            new GradientColorKey(new Color(1.00f, 1.00f, 0.95f), 0.00f),
                            new GradientColorKey(new Color(1.00f, 0.82f, 0.38f), 0.22f),
                            new GradientColorKey(new Color(1.00f, 0.45f, 0.10f), 0.58f),
                            new GradientColorKey(new Color(0.72f, 0.14f, 0.02f), 1.00f) };
                    g.SetKeys(
                        keys,
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.85f, 0.00f),
                            new GradientAlphaKey(1.00f, 0.18f),
                            new GradientAlphaKey(0.75f, 0.65f),
                            new GradientAlphaKey(0.00f, 1.00f) });
                    col.color = new ParticleSystem.MinMaxGradient(g);
                }

                // 스케일을 '모양'에만 건다 — 사거리는 그대로, 위아래 퍼짐만 줄어든다.
                m.scalingMode = ParticleSystemScalingMode.Shape;
                ps.transform.localScale = new Vector3(1f, jetFlatten, 1f);

                // 약한 부력. 납작하게 누른 것으로 도로 관통은 막았고, 이건 불이 끝에서
                // 살짝 말려 올라가게 하는 용도다.
                m.gravityModifier = ember ? -0.10f : -0.18f;

                var em = ps.emission;
                em.rateOverTime = ember ? 90f : 170f;
            }
        }

        /// <summary>
        /// 불이 실제로 뻗어야 하는 길이. 판정은 <b>플레이어</b>에서 <c>st.range</c> 까지인데
        /// 그림은 <b>총구</b>에서 시작하므로, 총구가 앞선 만큼 빼야 불끝과 판정 끝이 맞는다.
        ///
        /// <para>총구가 옆으로 치우친 것은 무시하고 <b>앞뒤 성분만</b> 본다 — 옆으로 치우친
        /// 것까지 보정하려면 콘을 기울여야 하는데, 그건 판정(플레이어 중심 부채꼴)과 더
        /// 어긋난다. 자세에 따라 총구가 뒤로 갈 수도 있어 0 이상으로 자른다.</para>
        /// </summary>
        float Reach(WeaponStats st)
        {
            float ahead = Mathf.Clamp(transform.localPosition.z, 0f, st.range * 0.5f);
            return Mathf.Max(0.5f, st.range - ahead);
        }

        /// <summary>이번 프레임에 분사했다. 매 발사 틱마다 부른다.</summary>
        public void Pulse() => _lastPulse = Time.time;

        /// <summary>
        /// 총열이 몸 정면에서 이만큼(도) 넘게 벗어나 있으면 불을 내지 않는다. 쏘기 시작한 순간에는 상체가 아직
        /// 대기 자세(총이 옆을 봄)라, 그때 내면 총구에서 엉뚱한 쪽으로 뿜는다(사용자 녹화에서 확인). 사격 자세가
        /// 섞여 들어오는 0.1초 남짓만 막힌다.
        /// </summary>
        public const float MaxBarrelOffset = 35f;

        /// <summary>
        /// 불줄기 방향 = 총열 방향의 수평 성분. 몸 정면으로 내면 총이 정면을 덜 보는 순간 '옆에서' 나간다.
        /// 판정은 여전히 몸 정면 부채꼴이다(<c>ProtoShooter.FireCone</c>) — 사격 자세에선 둘이 거의 겹친다.
        /// 반환값: 총열이 정면 근처라 불을 내도 되는가.
        /// </summary>
        bool AimAlongBarrel() => AimAlongBarrel(transform, _muzzle);

        /// 스프라이트 화염(<see cref="ProtoFlameSprite"/>)도 같은 규칙을 쓴다.
        public static bool AimAlongBarrel(Transform flame, Transform muzzle)
        {
            var parent = flame.parent;
            if (muzzle == null || parent == null) return true;
            Vector3 barrel = muzzle.forward; barrel.y = 0f;
            Vector3 body = parent.forward; body.y = 0f;
            if (barrel.sqrMagnitude < 0.0001f || body.sqrMagnitude < 0.0001f) return true;
            if (Vector3.Angle(body, barrel) > MaxBarrelOffset) { flame.rotation = Quaternion.LookRotation(body); return false; }
            flame.rotation = Quaternion.LookRotation(barrel);
            return true;
        }

        void LateUpdate()
        {
            PlaceAtMuzzle();
            bool aligned = AimAlongBarrel();

            bool on = aligned && Time.time - _lastPulse < HoldTime;

            // 방출만 여닫는다. Stop 을 쓰면 이미 날아가던 불이 공중에서 통째로 사라진다 —
            // 분사를 끊었을 때 남은 불이 자연스럽게 타서 꺼지는 게 맞다.
            if (_jet != null && _emitting != on)
            {
                _emitting = on;
                foreach (var ps in _jet)
                {
                    var em = ps.emission;
                    em.enabled = on;
                }
            }
        }
    }
}
