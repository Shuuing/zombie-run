using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 풀 조각 튀기 — 풀밭을 걸으면 발밑에서 풀 조각이 주변으로 흩날린다(2026-10-21 사용자 요청).
    /// 폭발이 풀밭에 떨어지면 크게 한 번 터진다(<see cref="Burst"/>, ProtoExplosion 이 부른다).
    ///
    /// <para><b>풀밭 판정:</b> 풀잎 메시만으로는 "여기가 풀밭인가"를 모른다 — 굽기 때 같이 만든 풀 지도
    /// (<see cref="ProtoGrassMask"/>, <see cref="ProtoGrassBlades.Mask"/>)를 읽는다. 풀이 눕는 효과(ProtoGrassField)와는 따로다.</para>
    ///
    /// <para><b>양:</b> 걸은 거리에 비례(1m 당 PerMeterOld · PerMeterTff × 풀 밀도) — 프레임레이트와 무관하고, 서 있으면 안 튄다.
    /// 탱크는 더 많이·세게. 좀비는 안 튀긴다 — 수백 마리가 튀기면 화면이 풀 조각으로 덮인다.</para>
    ///
    /// <para>파티클 하나(코드로 짓는다, 씬 배선 없음). 밤 어둠 막(큐 2900)보다 <b>먼저</b> 그려 주변과 같이 어두워진다 —
    /// 뒤에 그리면 밤에 풀 조각만 형광처럼 빛난다. 게임 시간으로 돈다(레벨업 정지 중엔 공중에 멈춘다).</para>
    /// </summary>
    public class ProtoGrassKick : MonoBehaviour
    {
        // 값 두 벌 — 런 시작에 깔린 풀로 고른다(_tffGrass). 2026-11-13 사용자가 풀을 "11월 전" 처음 버전 ↔ 최종본으로 오가게 해서(CityGrassVersions),
        // 처음 버전(구운 풀잎)은 11월 전 값 그대로 · 최종본(팩 풀 패치)은 11-12 저녁 값.
        // 11-12 저녁(사용자: "풀밭에 지나갈때 풀잎이 날리는게 너무 눈에 안띄는데", 팩 풀 위에서): 조각이 0.14~0.24m 에 잎 폭이 가늘고 풀과 같은 색이라
        // 게임 카메라(18m)에서 묻혔다 → 크기 ×1.6 · 잎 폭 ×1.5(BuildBladeTex) · 팩 풀 색 밝기 1.3 → 1.6 · 수 7 → 10 · 조금 더 오래(0.6~0.95초).
        const float PerMeterOld = 7f, TankPerMeterOld = 15f;   // 걸은 1m 당 조각 수 (밀도 1 기준) — 2026-10-21 사용자: 50% 줄임(14 → 7)
        const float PerMeterTff = 10f, TankPerMeterTff = 18f;  // 11-12 "너무 눈에 안 띈다" → 10
        static readonly Vector2 StepSizeOld = new Vector2(0.14f, 0.24f), StepLifeOld = new Vector2(0.5f, 0.8f), BurstSizeOld = new Vector2(0.16f, 0.28f);
        static readonly Vector2 StepSizeTff = new Vector2(0.22f, 0.38f), StepLifeTff = new Vector2(0.6f, 0.95f), BurstSizeTff = new Vector2(0.24f, 0.42f);
        const float BladeHalfWOld = 0.16f, BladeTipOld = 0.01f, BladeHalfWTff = 0.24f, BladeTipTff = 0.015f;   // 잎 폭(0.16 은 18m 카메라에서 실처럼 가늘었다 — 최종본만 넓힘)
        const float MinSpeed = 0.5f;                     // 이보다 느리면(밀림·미끄러짐) 안 튄다
        const float FootRadius = 0.25f, TankRadius = 1.4f;
        const float BlastPerM2 = 1.25f;
        const int   BlastMax = 40, MaxParticles = 700;

        // Proto/Grass 머티리얼과 같은 색(M_City_GrassBlades) — 튀는 조각이 깔린 풀과 같은 풀로 읽혀야 한다
        static readonly Color Tip = new Color(0.58f, 0.82f, 0.32f);
        static readonly Color Dry = new Color(0.60f, 0.62f, 0.30f), Scorch = new Color(0.10f, 0.09f, 0.07f);

        // 팩 풀 패치(ToonFantasyCityFlora — _City/_GrassPatches_TFF)가 깔린 씬의 풀 색. 2026-11-12 사용자: "풀 밟으면 풀이 날리는데 색상이 너무 녹색인데, 풀 색상이랑 맞게".
        // 위 Tip 은 예전 풀잎(Proto/Grass) 색이라 팩 풀(회녹색·짙은 녹색에 올리브·회색 끝이 섞인)보다 훨씬 밝은 연두였다.
        // 값은 임시 프로젝트에서 팩 풀을 밤 조명(Demo 밤 2판)으로 찍은 화면의 풀 픽셀을 다섯 색으로 묶은 것(sRGB)과 그 비율 +
        // 붉은 기가 도는 잎(13%, 따로 셌다). 이웃 색끼리 섞으므로 비슷한 색끼리 붙여 둔다. 밝기는 TffBright 배 — 1.15 는 풀 위에서 조각이 묻혔다.
        static readonly Color[] TffGrass =
        {
            new Color(0.21f, 0.31f, 0.16f),   // 짙은 녹색
            new Color(0.31f, 0.38f, 0.27f),   // 회녹색
            new Color(0.43f, 0.44f, 0.38f),   // 밝은 회색 끝
            new Color(0.43f, 0.33f, 0.29f),   // 붉은 갈색 잎
            new Color(0.35f, 0.40f, 0.12f),   // 올리브
            new Color(0.44f, 0.50f, 0.13f),   // 노란 올리브
        };
        static readonly float[] TffWeight = { 0.30f, 0.31f, 0.11f, 0.13f, 0.09f, 0.06f };
        const float TffBright = 1.6f;   // 1.3 도 풀 위에서 묻혔다(11-12 저녁)

        static ProtoGrassKick _inst;
        bool _tffGrass;   // 팩 풀 패치가 켜진 씬인가 — 런 시작(Awake)에 한 번 본다
        ParticleSystem _ps;
        Vector3 _last;
        bool _hasLast;
        float _carry;

        public static void Ensure()
        {
            if (_inst != null) return;
            new GameObject("_GrassKick").AddComponent<ProtoGrassKick>();
        }

        /// 폭발 — 반경 안 풀 조각을 바깥으로 크게 날린다. 풀이 없는 자리·씬이면 아무것도 안 한다.
        public static void Burst(Vector3 p, float radius)
        {
            var mask = ProtoGrassBlades.Mask;
            if (_inst == null || mask == null) return;
            float dens = mask.DensityAt(p, out bool lawn);
            if (dens < 0.05f) return;
            int n = Mathf.Min(BlastMax, Mathf.RoundToInt(Mathf.PI * radius * radius * BlastPerM2 * dens));
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius * 0.8f;
                Vector3 outDir = r.sqrMagnitude > 0.0001f ? new Vector3(r.x, 0f, r.y).normalized : Vector3.forward;
                var v = outDir * Random.Range(2.5f, 5.5f) + Vector3.up * Random.Range(3f, 5.5f);
                // 폭발 조각은 반쯤 그을려 있다(풀이 그을리는 것과 맞춘다)
                var col = Color.Lerp(PickColor(lawn), Scorch, Random.Range(0.2f, 0.7f));
                var bs = _inst._tffGrass ? BurstSizeTff : BurstSizeOld;
                _inst.Emit(new Vector3(p.x + r.x, p.y + 0.1f, p.z + r.y), v, col, Random.Range(bs.x, bs.y), Random.Range(0.8f, 1.2f));
            }
        }

        void Awake()
        {
            _inst = this;
            var tff = GameObject.Find("_GrassPatches_TFF");   // 꺼져 있으면 못 찾는다 = 예전 풀잎 씬(처음 버전)
            _tffGrass = tff != null && tff.activeInHierarchy;
            _ps = BuildSystem();   // 잎 폭이 _tffGrass 를 따른다
        }

        void OnDestroy() { if (_inst == this) _inst = null; }

        void Update()
        {
            var run = ProtoRun.I;
            var mask = ProtoGrassBlades.Mask;
            if (run == null || run.player == null || mask == null || !run.IsRunning) { _hasLast = false; return; }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 pos = run.player.transform.position;
            if (!_hasLast) { _last = pos; _hasLast = true; return; }
            Vector3 d = pos - _last; d.y = 0f;
            _last = pos;
            float dist = d.magnitude;
            if (dist > 2f || dist / dt < MinSpeed) { _carry = 0f; return; }   // 순간이동(재시작·교체)·제자리

            float dens = mask.DensityAt(pos, out bool lawn);
            if (dens < 0.05f) { _carry = 0f; return; }

            bool tank = ProtoTank.PlayerRiding;
            _carry += dist * (tank ? (_tffGrass ? TankPerMeterTff : TankPerMeterOld) : (_tffGrass ? PerMeterTff : PerMeterOld)) * dens;
            int n = (int)_carry;
            _carry -= n;
            Vector3 move = d / dist;
            float footR = tank ? TankRadius : FootRadius;
            float power = tank ? 1.35f : 1f;
            for (int i = 0; i < n; i++)
            {
                Vector2 r = Random.insideUnitCircle * footR;
                // 발 뒤쪽·옆으로 차인다. 위로 솟았다가 떨어진다
                Vector3 side = new Vector3(r.x, 0f, r.y);
                Vector3 h = side.normalized * 0.6f - move * 0.8f + new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));
                h.y = 0f;
                var v = h.normalized * Random.Range(0.6f, 1.6f) * power + Vector3.up * Random.Range(1.6f, 3.0f) * power;
                Vector2 ss = _tffGrass ? StepSizeTff : StepSizeOld, sl = _tffGrass ? StepLifeTff : StepLifeOld;
                _inst.Emit(new Vector3(pos.x + r.x, pos.y + 0.05f, pos.z + r.y), v, PickColor(lawn), Random.Range(ss.x, ss.y), Random.Range(sl.x, sl.y));
            }
        }

        static Color PickColor(bool lawn)
        {
            // 깔린 풀보다 한 톤 밝게 — 같은 색이면 풀밭 위에서 조각이 묻혀 안 보인다(2026-10-21 실측)
            if (lawn && _inst != null && _inst._tffGrass)
            {
                float r = Random.value; int k = 0;
                for (float acc = TffWeight[0]; k < TffGrass.Length - 1 && r > acc; acc += TffWeight[++k]) { }
                var c = Color.Lerp(TffGrass[k], TffGrass[(k + 1) % TffGrass.Length], Random.Range(0f, 0.3f));   // 이웃 색으로 살짝 섞어 몇 가지 색이 따로 놀지 않게
                return c * TffBright;
            }
            return (lawn ? Color.Lerp(Tip, new Color(0.80f, 0.95f, 0.45f), Random.value) : Color.Lerp(Dry, new Color(0.85f, 0.82f, 0.45f), Random.value)) * 1.15f;
        }

        void Emit(Vector3 pos, Vector3 vel, Color col, float size, float life)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startColor = col,
                startSize = size,
                startLifetime = life,
                rotation = Random.Range(0f, 360f),
                angularVelocity = Random.Range(-720f, 720f),
            };
            _ps.Emit(ep, 1);
        }

        ParticleSystem BuildSystem()
        {
            var ps = gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.gravityModifier = 0.9f;
            main.maxParticles = MaxParticles;
            var em = ps.emission; em.enabled = false;     // Emit 으로만 낸다
            var sh = ps.shape; sh.enabled = false;
            // 끝 30% 에서 옅어진다 — 땅에 닿기 전후로 사라지게
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
            // 공기 저항 — 떨어질 때 살랑거리며 느려진다
            var lim = ps.limitVelocityOverLifetime;
            lim.enabled = true; lim.limit = 6f; lim.dampen = 0.08f;

            var r = GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var mat = ProtoFxMat.New("GrassKick", BuildBladeTex(_tffGrass), 2850);   // (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            r.sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        /// 풀잎 한 장 — 아래가 넓고 끝이 뾰족하며 살짝 휜다. 흰색이라 입자 색이 그대로 입혀진다.
        static Texture2D BuildBladeTex(bool wide)
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = (y + 0.5f) / N;                       // 0 = 뿌리, 1 = 끝
                    float u = (x + 0.5f) / N - 0.5f;
                    float center = 0.12f * v * v;                   // 끝으로 갈수록 휜다
                    float halfW = wide ? BladeHalfWTff * (1f - v) + BladeTipTff : BladeHalfWOld * (1f - v) + BladeTipOld;
                    float a = Mathf.Clamp01((halfW - Mathf.Abs(u - center)) * N * 0.6f);
                    if (v < 0.04f) a = 0f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}
