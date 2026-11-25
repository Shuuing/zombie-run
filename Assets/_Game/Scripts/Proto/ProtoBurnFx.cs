using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 화염에 타는 좀비 몸에 붙는 불. <b>ParticleSystem 하나가 전부를 담당한다.</b>
    ///
    /// <para><b>왜 좀비마다 붙이지 않는가.</b> 동시 생존 상한이 90마리(<see cref="ProtoConfig.BudgetCap"/>)다.
    /// 좀비마다 ParticleSystem 을 달면 시뮬레이션 90개 + 드로우콜 90개가 되고, 풀에서 꺼내고
    /// 넣는 비용까지 붙는다. 여기서는 시스템 하나에 <c>Emit</c> 으로 좌표를 직접 찍는다 —
    /// 좀비가 몇 마리든 <b>드로우콜 1개, 시뮬레이션 1개</b>다.</para>
    ///
    /// <para><b>화면 채우기는 걱정거리가 아니다.</b> 재 보면 불 입자(0.35m)는 12m 거리에서
    /// 약 41px 이고, 12마리×3알이면 1080p 화면의 2% 를 칠할 뿐이다. 같은 잣대로 화염방사기
    /// 제트(1.44m 입자 150알)는 <b>1.6 화면</b>이다 — 이미 쓰고 있는 것이 80배 비싸다.
    /// 진짜 비용은 CPU 쪽(시스템 개수)이었고, 그래서 하나로 합쳤다.</para>
    ///
    /// <para><b>씬 배선이 필요 없다.</b> <see cref="ProtoRun"/> 이 한 번 깨우면 스스로 붙는다
    /// (<see cref="ProtoLevelUpBeam"/>·<see cref="ProtoAudio"/> 와 같은 방식).</para>
    ///
    /// <para>옵션 <see cref="ProtoSettings.BurningBodies"/> 로 끌 수 있다. <b>꺼도 그을린 색과
    /// 이미션은 그대로 남는다</b> — 무엇이 타는 중인지는 게임플레이 정보라 끌 수 있으면 안 된다
    /// (<see cref="ProtoZombie.PushVisual"/> 가 그쪽을 쥐고 있고 이 클래스와 무관하다).</para>
    ///
    /// <para><b>화상 사망(2026-11-10, <see cref="ZombieBurnOut"/>).</b> 불타 죽은 몸은 타들어가며 사라진다 — 그동안의 불도 여기서 낸다.
    /// 불꽃은 같은 시스템에 불선 아래로만, 갈수록 작고 드물게 뿌려 <b>바닥에서 사그라들고</b>,
    /// 불씨는 시스템 하나(<c>Embers</c>, 코드로 만든다)를 더 두어 불선 높이에서 <b>톡톡 튀긴다</b>(사용자: "불씨가 튀기고 불꽃도 사그라드는 느낌").
    /// 둘 다 Emit 이라 타 죽는 좀비가 몇 마리든 시스템은 둘뿐이다.</para>
    /// </summary>
    public class ProtoBurnFx : MonoBehaviour
    {
        /// Resources 아래 경로. 씬 배선 없이 잡으려고 여기 둔다 (번개·화염과 같은 이유).
        public const string FxPath = "VFX/FX_BurnBody";

        [Tooltip("초당 좀비 한 마리에 뿌리는 입자 수. 올리면 불이 두꺼워지고 채우기 비용이 는다.")]
        public float particlesPerSecond = 26f;

        [Tooltip("동시에 입자를 받는 좀비 수 상한. 넘치면 플레이어에게 가까운 순으로 자른다.")]
        public int maxBurningShown = 12;

        [Tooltip("이 거리(m) 밖의 좀비는 입자를 안 받는다. 화면 밖에서 타는 것을 그릴 이유가 없다.")]
        public float cullDistance = 26f;

        [Tooltip("불이 붙는 높이 범위(m). 좀비 발밑 기준.")]
        public float bodyLow = 0.25f, bodyHigh = 1.45f;

        [Tooltip("몸 중심에서 이만큼 바깥 링에 뿌린다(m). 몸 안쪽에 뿌리면 깊이 테스트에 걸려 안 보인다.")]
        public float ringInner = 0.30f, ringOuter = 0.46f;

        [Header("화상 사망 — 타들어가는 동안 (ZombieBurnOut)")]
        [Tooltip("타들어가는 좀비 한 마리가 초당 튀기는 불씨 수. 가운데쯤이 가장 많고 처음과 끝은 적다.")]
        public float embersPerSecond = 36f;

        [Tooltip("죽는 순간 한꺼번에 튀는 불씨 수.")]
        public int emberBurst = 9;

        [Tooltip("불씨 수명(초) 범위.")]
        public float emberLifeMin = 0.55f, emberLifeMax = 1.2f;

        [Tooltip("불씨 크기(m) 범위. 0.06 아래는 화면에서 2~3px 라 안 보인다.")]
        public float emberSizeMin = 0.06f, emberSizeMax = 0.11f;

        [Tooltip("다 탈 때쯤의 불꽃 크기 배율 — 불이 사그라든다. 1 이면 끝까지 같은 크기.")]
        public float flameEndSize = 0.4f;

        [Tooltip("다 탈 때쯤의 불꽃 방출 배율.")]
        public float flameEndRate = 0.25f;

        /// Resources 아래 — 불씨 머티리얼(가산 점). 에디터의 ZombieBurnOutSetup 이 FX 템플릿의 M_Fx_Glow 를 복제해 만든다.
        public const string EmberMatPath = "VFX/M_BurnEmber";

        static ProtoBurnFx _instance;

        ParticleSystem _ps;
        ParticleSystem.EmitParams _ep;
        float _carry;            // 소수점 이하로 남은 방출량. 버리면 프레임률에 따라 불이 얇아진다.

        ParticleSystem _embers;                      // 불씨 — 타들어가는 몸에서 튄다
        ParticleSystem.EmitParams _epOut, _epEmber;  // 사그라드는 불꽃(크기를 줄여 낸다) · 불씨
        float _flameSizeMin = 0.34f, _flameSizeMax = 0.58f;   // 프리팹의 불 입자 크기 — Build 가 읽어 온다

        /// 없으면 만들어 둔다. <see cref="ProtoRun"/> 이 런 시작에 한 번 부른다.
        public static void Ensure()
        {
            if (_instance != null) return;
            var go = new GameObject("_BurnFx");
            _instance = go.AddComponent<ProtoBurnFx>();
        }

        void Awake()
        {
            if (_instance == null) _instance = this;
            Build();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        void Build()
        {
            if (_ps != null) return;
            var prefab = Resources.Load<GameObject>(FxPath);
            if (prefab == null)
            {
                Debug.LogWarning("[몸불] " + FxPath + " 가 없다 — 그을린 색만 나온다");
                enabled = false;
                return;
            }
            var go = Instantiate(prefab, transform);
            go.name = "Fx";
            // 프리팹의 파티클은 **자식**에 있다(화염 제트의 Flame 시스템을 복사해 만들었다 —
            // 처음부터 만든 시스템은 플립북에 필요한 textureSheetAnimation·정점 스트림이 없어서
            // 화면에 아무것도 안 나왔다). GetComponent 로 루트만 보면 null 이다.
            _ps = go.GetComponentInChildren<ParticleSystem>(true);
            if (_ps == null) { Debug.LogWarning("[몸불] 프리팹에 ParticleSystem 이 없다"); enabled = false; return; }
            _ps.Play();     // 방출은 Emit 으로만 한다(emission 모듈은 프리팹에서 꺼져 있다)

            // 사그라드는 불꽃은 입자 크기를 직접 줄여 낸다 — 프리팹이 정한 크기 범위를 기준으로 삼는다.
            var ss = _ps.main.startSize;
            if (ss.mode == ParticleSystemCurveMode.TwoConstants) { _flameSizeMin = ss.constantMin; _flameSizeMax = ss.constantMax; }
            else if (ss.mode == ParticleSystemCurveMode.Constant) { _flameSizeMin = _flameSizeMax = ss.constant; }
            BuildEmbers();
        }

        /// 불씨 시스템 — 톡 튀어 나간 뒤(Emit 이 준 속도) 공기 저항에 잡혀 느려지고, 가벼워서 위로 떠오르며 식는다.
        /// 빠를 때는 줄, 느려지면 점으로 보이게 늘임(Stretch) 렌더.
        void BuildEmbers()
        {
            var mat = Resources.Load<Material>(EmberMatPath);
            if (mat == null)
            {
                Debug.LogWarning("[몸불] " + EmberMatPath + " 가 없다 — 타들어가는 몸에서 불씨가 안 튄다(에디터에선 ZombieBurnOutSetup 이 만든다, 다음 포커스 때)");
                return;
            }
            var go = new GameObject("Embers");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main;
            m.loop = true; m.playOnAwake = true; m.duration = 1f; m.maxParticles = 1500;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startSpeed = 0f; m.startSize = 0.08f; m.startLifetime = 0.9f;
            m.gravityModifier = -0.22f;                    // 가벼운 불씨 — 튄 뒤엔 위로 뜬다
            var em = ps.emission; em.enabled = false;      // Emit() 로만
            var sh = ps.shape; sh.enabled = false;
            var lim = ps.limitVelocityOverLifetime;        // 속도 상한은 안 쓰고(50) 공기 저항만
            lim.enabled = true; lim.limit = 50f; lim.dampen = 0f; lim.drag = 2.4f;
            lim.multiplyDragByParticleSize = false; lim.multiplyDragByParticleVelocity = false;
            var nz = ps.noise;                             // 떠오르며 하늘거린다
            nz.enabled = true; nz.strength = 0.55f; nz.frequency = 1.4f; nz.scrollSpeed = 0.8f;
            nz.quality = ParticleSystemNoiseQuality.Low;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.35f), new GradientColorKey(new Color(0.75f, 0.12f, 0.03f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;                                 // 밝은 노랑 → 주황 → 검붉게 식으며 꺼진다
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f; r.lengthScale = 1f; r.cameraVelocityScale = 0f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
            _embers = ps;
        }

        /// <summary>타들어가기 시작하는 순간 한꺼번에 튀는 불씨 — <see cref="ProtoZombie"/> 가 불타 죽는 순간 부른다.</summary>
        public static void EmberBurst(ProtoZombie z)
        {
            var fx = _instance;
            if (fx == null || fx._embers == null || z == null || !ProtoSettings.BurningBodies) return;
            Vector3 p = z.transform.position;
            float h = z.BurnHeight;
            for (int i = 0; i < fx.emberBurst; i++) fx.EmitEmber(p, h, Random.Range(0.35f, 1f), 1.35f);
        }

        /// 불씨 한 알. front01 = 튀는 높이(1 = 머리, 0 = 발밑) — 그 둘레에서 바깥 + 위로 튄다.
        void EmitEmber(Vector3 p, float height, float front01, float speedMul = 1f)
        {
            float a = Random.value * Mathf.PI * 2f;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float y = Mathf.Clamp(front01 + Random.Range(-0.22f, 0.10f), 0.04f, 1f) * height;
            _epEmber.position = p + outward * Random.Range(0.08f, ringOuter * 0.8f) + Vector3.up * y;
            _epEmber.velocity = outward * (Random.Range(0.5f, 2.2f) * speedMul) + Vector3.up * (Random.Range(0.9f, 2.6f) * speedMul);
            _epEmber.startLifetime = Random.Range(emberLifeMin, emberLifeMax);
            _epEmber.startSize = Random.Range(emberSizeMin, emberSizeMax);
            _embers.Emit(_epEmber, 1);
        }

        /// <summary>
        /// 타들어가는 몸 한 마리. 불꽃은 남은 몸(불선 아래)에만 — 수 · 크기 · 둘레가 함께 줄어 바닥에서 꺼진다.
        /// 불씨는 불선 높이에서 튄다 — 한창 탈 때(가운데)가 가장 많다.
        /// </summary>
        void EmitBurnOut(ProtoZombie z, Vector3 p, float dt)
        {
            float k = z.BurnOut01;
            float h = z.BurnHeight;
            float front = ZombieBurnOut.FrontHeight01(k);

            if (k < 0.92f)
            {
                int n = Stochastic(particlesPerSecond * Mathf.Lerp(1f, flameEndRate, k) * dt);
                float low = bodyLow * 0.5f;
                float top = Mathf.Max(low + 0.1f, Mathf.Min(bodyHigh, h) * front);
                float shrink = Mathf.Lerp(1f, flameEndSize, k);
                float ring = Mathf.Lerp(1f, 0.6f, k);
                for (int i = 0; i < n; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float rr = Random.Range(ringInner, ringOuter) * ring;
                    _epOut.position = p + new Vector3(Mathf.Cos(a) * rr, Random.Range(low, top), Mathf.Sin(a) * rr);
                    _epOut.startSize = Random.Range(_flameSizeMin, _flameSizeMax) * shrink;
                    _ps.Emit(_epOut, 1);
                }
            }

            if (_embers != null)
            {
                float env = 0.25f + 0.75f * Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                float tail = 1f - Mathf.Clamp01((k - 0.85f) / 0.15f);
                int n = Stochastic(embersPerSecond * env * tail * dt);
                for (int i = 0; i < n; i++) EmitEmber(p, h, front);
            }
        }

        /// 소수 방출량을 확률로 올림한다 — 마리마다 남은 양을 들고 다니지 않아도 평균이 맞는다.
        static int Stochastic(float x)
        {
            int n = Mathf.FloorToInt(x);
            if (Random.value < x - n) n++;
            return n;
        }

        void LateUpdate()
        {
            if (_ps == null) return;

            // 옵션이 꺼져 있으면 남은 불만 타서 꺼지게 두고 더 뿌리지 않는다.
            if (!ProtoSettings.BurningBodies) { _carry = 0f; return; }

            var dir = ProtoSpawnDirector.I;
            if (dir == null) return;

            // 프레임당 뿌릴 양. **소수점을 들고 간다** — 60fps 에서 마리당 0.43알이라
            // 반올림해 버리면 전부 0 이 되어 불이 아예 안 보인다.
            // (0 이어도 돌아가지 않는다 — 타들어가는 몸은 아래에서 따로, 매 프레임 확률 올림으로 낸다.)
            float dt = ProtoConfig.Delta;
            _carry += particlesPerSecond * dt;
            int perZombie = Mathf.FloorToInt(_carry);
            _carry -= perZombie;

            Vector3 eye = Camera.main != null ? Camera.main.transform.position : transform.position;
            float cullSq = cullDistance * cullDistance;

            // 상한을 넘으면 가까운 것부터 그린다. 정렬은 하지 않는다 — 90마리를 매 프레임
            // 정렬하면 이 클래스를 만든 이유가 없어진다. 거리 컷으로 한 번 거르고 선착순으로
            // 채운다(불타는 좀비는 어차피 플레이어 앞에 몰려 있다).
            int shown = 0;
            var list = dir.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null) continue;
                bool burning = z.IsBurning;
                bool burningOut = !burning && z.BurningOut;
                if (!burning && !burningOut) continue;

                Vector3 p = z.transform.position;
                if ((p - eye).sqrMagnitude > cullSq) continue;

                // 타들어가는 몸 — 상한(maxBurningShown)과 따로 센다: 1초 남짓이면 끝나고, 빠지면 불 없이 사라져 어색하다.
                if (burningOut) { EmitBurnOut(z, p, dt); continue; }

                if (perZombie <= 0 || shown >= maxBurningShown) continue;
                shown++;
                for (int k = 0; k < perZombie; k++)
                {
                    // **몸 중심이 아니라 바깥 링에 뿌린다.** 중심에 뿌렸더니 입자가 통째로
                    // 메시 안쪽에 생겨서 깊이 테스트에 걸려 하나도 안 보였다(가법 블렌딩이라
                    // ZWrite 는 꺼져 있지만 ZTest 는 살아 있다). 실루엣 가장자리에 걸쳐야
                    // 몸을 감싸고 타는 것처럼 보인다.
                    float a = Random.value * Mathf.PI * 2f;
                    float rr = Random.Range(ringInner, ringOuter);
                    _ep.position = p + new Vector3(Mathf.Cos(a) * rr,
                                                   Random.Range(bodyLow, bodyHigh),
                                                   Mathf.Sin(a) * rr);
                    _ps.Emit(_ep, 1);
                }
            }
        }
    }
}
