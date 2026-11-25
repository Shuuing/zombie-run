using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 시작 연출 비행기 색을 씬 조명과 상관없이 선명하게 — 2026-11-18 사용자 "배경 셰이더를 수정하면서 처음 게임 시작할 때 비행기 색상이 칙칙하게 나온다, 선명하게".
    ///
    /// <para><b>원인</b>: 도시 맵 '마지막' 버전(11-13~16)의 밤 조명 — 달빛 (0.75, 0.82, 0.95)×1.1, 푸른 보라 3색 주변광(지평 (1.09, 1.03, 1.48)) —
    /// 을 비행기 BCM 툰 머티리얼도 그대로 받는다. 직사광은 낮 해(흰빛 1.2)의 절반쯤으로 어둡고 푸르며, 주변광은 낮의 4.6배(위·옆 휘도 0.908 ← 0.197)라
    /// 초록 위장색이 명암 없는 푸른 회색으로 씻겼다(임시 프로젝트 렌더 채도 0.364 → 0.224). 비행기는 밤 어둠 막 위(큐 4062)라 막에는 안 어두워지지만 조명은 탄다.</para>
    ///
    /// <para><b>고침</b>(비행기 머티리얼 사본에서만 — 필드 프롭 M_Airplane 은 그대로): 비행기는 낮 해 하나만 받는다.
    /// 직사광은 지금 해의 색·세기를 지우고 처음(11월 전) 맵의 낮 해를 받은 것처럼(<c>_LightReceivedColor</c> = 목표 ÷ 지금 해, 해 색은 런타임에 읽는다),
    /// 주변광은 끈다(<c>_IndirectIntensity</c> 0) — 대신 해를 그만큼 세게. 주변광을 낮 밝기로 줄이기만 한 판은 남은 푸른 기 때문에 채도 0.310 에 그쳤고,
    /// 이 판이 0.356 · 밝기 0.675 로 예전(0.364 · 0.666)에 가장 가까웠다(밤 후처리 TFF_City_Night 포함 렌더). 그늘 쪽은 툰 그늘색(_SColor)이 받쳐 검게 죽지 않는다.</para>
    /// </summary>
    public static class ProtoIntroPlaneColor
    {
        // 목표 직사광 — 처음 맵 버전의 해(Look_Original: 색 (1, 0.975, 0.94), 세기 1.2) × 끈 주변광 몫 1.2
        static readonly Color SunColor = new Color(1f, 0.975f, 0.94f);
        const float SunIntensity = 1.2f * 1.2f;
        // 해가 아주 어두운 씬에서 한 채널을 과하게 키우지 않게
        const float MaxGain = 4f;

        static readonly int IdRecvColor = Shader.PropertyToID("_LightReceivedColor");
        static readonly int IdIndirect  = Shader.PropertyToID("_IndirectIntensity");

        /// root 아래 머티리얼(사본)에 적용한다. 파티클은 건너뛴다.
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            // 직사광 — 셰이더가 받는 해 색(선형 × 세기)으로 목표를 나눈다. 해가 없으면 직사광은 그대로 두고 주변광만 끈다.
            bool hasRecv = false;
            Color recv = Color.white;
            var sun = FindSun();
            if (sun != null)
            {
                Color have = sun.color.linear * sun.intensity;
                if (sun.useColorTemperature) have *= Mathf.CorrelatedColorTemperatureToRGB(sun.colorTemperature);
                Color want = SunColor.linear * SunIntensity;
                recv = new Color(Gain(want.r, have.r), Gain(want.g, have.g), Gain(want.b, have.b), 1f);
                hasRecv = true;
            }

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var m in r.materials)
                {
                    if (m == null) continue;
                    // SetColor 는 감마 색으로 받아 선형으로 바꿔 올린다 — 선형 값을 넘기려면 .gamma
                    if (hasRecv && m.HasProperty(IdRecvColor)) m.SetColor(IdRecvColor, recv.gamma);
                    if (hasRecv && m.HasProperty(IdIndirect)) m.SetFloat(IdIndirect, 0f);
                }
            }
        }

        static float Gain(float want, float have) => Mathf.Min(want / Mathf.Max(have, 0.001f), MaxGain);

        /// URP 메인 라이트와 같은 고르기 — RenderSettings.sun, 없으면 켜진 디렉셔널 중 가장 센 것
        static Light FindSun()
        {
            var s = RenderSettings.sun;
            if (s != null && s.isActiveAndEnabled && s.type == LightType.Directional) return s;
            Light best = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Directional || !l.isActiveAndEnabled) continue;
                if (best == null || l.intensity > best.intensity) best = l;
            }
            return best;
        }
    }
}
