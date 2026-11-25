using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 시작 연출의 안개 — 착지 지점 둘레 땅 위에 낮게 깔렸다가, 캐릭터가 착지하면 양옆으로 갈라져 사라진다.
    /// 2026-11-12 오후 사용자: "처음 시작할 때 포그도 좀 깔아줘. 캐릭터가 착지하면 양옆으로 사라지게 해줘".
    ///
    /// <para>같은 날 오전에 뺀 안개(어둠 막 Proto/IntroVeil 안의 노이즈 결)와 다르다 — 그건 막과 같이 어두워져 "검은 안개"로 보였다.
    /// 이 안개는 밤 어둠 막(큐 4060) 위 · 비행기·낙하산·캐릭터(4062) 아래(4061)에 그려 어둠을 안 탄다(Proto/IntroFog 머리 주석).</para>
    ///
    /// <para><b>2판</b>(같은 날 저녁, 사용자가 땅에 낮게 깔린 푸른 밤안개 사진을 보이며 "구름 텍스쳐 모양도 너무 도드라지게 잘보이고, 안개 처럼 이쁘게"):
    /// 1판은 팩 안개 결 텍스처 판 99장을 카메라 쪽으로 세워 솜뭉치가 하나하나 보였다. 이제는 땅과 나란한 넓은 층 몇 장(아래일수록 짙게)을
    /// 겹치고, 짙기는 셰이더가 아주 크고 부드러운 값 노이즈(화면 가로로 길게)로만 흔든다 — 텍스처를 안 쓴다.
    /// 갈라질 때는 착지 지점을 지나는 화면 세로선에서 양옆으로 밀려난다(<see cref="SetPart"/> 0 → 1).</para>
    ///
    /// <para><b>3판</b>(같은 날 저녁, 사용자: "안개가 가만히만 있는데 조금씩 움직이면서 착지하면 서서히 옅어지게"):
    /// 결이 띠를 가로질러 밀리고 일렁인다(셰이더 _Fog_Flow). 착지하면 ProtoIntro 가 <see cref="Release"/> 를 불러 안개를 연출 오브젝트에서
    /// 떼어 내고, 안개가 스스로 4초에 걸쳐 서서히 옅어진 뒤 지운다 — 연출 오브젝트는 착지 3초 뒤 사라지므로 거기 묶여 있으면 천천히 못 옅어진다.</para>
    ///
    /// <para><b>5판</b>(사용자: "낙하산 타고 내려오면서부터 서서히 사라지면서 착지하면 다 사라지게"): ProtoIntro 가 낙하 진행으로 <see cref="SetPart"/> 를
    /// 직접 올린다(착지 = 1 → 치움). <see cref="Release"/> 는 이제 건너뛰기 때만 — 지금 옅어진 정도에서 이어서 짧게 옅어진다.</para>
    ///
    /// 머티리얼은 <c>Resources/Intro/M_IntroFog</c>(셰이더 Proto/IntroFog). 없으면 안개 없이 진행한다.
    /// </summary>
    public class ProtoIntroFog : MonoBehaviour
    {
        // 층 — 땅 위 높이(m)와 불투명도. 아래일수록 짙게(사진처럼 바닥에 깔리고 위로 옅어진다)
        static readonly float[] LayerHeight = { 0.25f, 0.7f, 1.2f, 1.8f, 2.5f };
        static readonly float[] LayerAlpha  = { 0.3f, 0.25f, 0.2f, 0.15f, 0.1f };
        const float LayerHalf = 44f;                      // 층 반폭 m — 시작 줌(거리 ×1.55)에서 보이는 땅(가로 ~34m)보다 넓게. 끝은 셰이더가 둘레로 옅게
        const float SpeedMin  = 0.85f, SpeedMax = 1.2f;   // 층마다 속도 배율(띠 따라 흐름 · 옆으로 퍼짐) — 한꺼번에 미닫이처럼 움직이지 않게
        public const float Spread   = 6f;                 // 옅어지는 동안 양옆으로 퍼지는 거리 m (2판 26m 는 갈라져 밀려나는 연출이었다)
        const float CenterFirst     = 0.6f;               // 캐릭터 둘레가 먼저 얇아지는 정도 0~1
        const int Seed = 20261112;

        static readonly int IdPart = Shader.PropertyToID("_Fog_Part");
        static readonly int IdLight = Shader.PropertyToID("_Fog_Light");
        static readonly int IdLit = Shader.PropertyToID("_Fog_Lit");
        static readonly int IdAxis = Shader.PropertyToID("_Fog_Axis");
        // 빛 밖(달빛) · 빛 안 안개 밝기 배율. 빛 밖 0.8 → 0.25(11-12 밤, 사용자: "처음 등장할때 좀더 어둡게. 지금은 바닥에 착지할때가 더 어두워" — "포그가 밝아서 그런건지"):
        // 안개는 밤 어둠 막 위에 그려져 어둠을 안 타는데, 빛이 켜지기 전에도 0.8배 푸른 회색이 화면을 덮어 시작 화면이 착지 때보다 밝았다
        // (임시 프로젝트 화면 평균 밝기: 안개 없이 24 · 0.8 안개 86 · 착지 84, 0.4 로는 58 이라 아직 환해 0.25).
        // 이제 시작엔 달빛 받은 어두운 안개이고, 캐릭터 빛이 켜지는 착지 지점 둘레만 밝아진다.
        const float DarkBright = 0.2f, LitBright = 1.2f;
        // ↑ 같은 날 밤 "안개를 더 어두운걸로 해줘" — 안개 색(M_IntroFog _Color) 자체를 (0.46, 0.56, 0.70) → (0.30, 0.37, 0.48) 짙은 푸른 회색으로
        //   (처음 보여 준 안개 사진의 가장 짙은 곳 ≈ (72, 96, 128) 쪽). 색이 리니어로 약 2.4배 어두워져 빛 밖 배율은 0.25 → 0.45, 빛 안 1.35 → 1.2 로 다시 맞췄다.
        //   11-13 "첫 등장할때 캐릭터 주변외에는 살짝의 실루엣만" — 어둠 막이 화면 끝을 0.06 까지 누르는데 막 위의 안개가 빛 밖을 다시 밝혔다 → 빛 밖 0.45 → 0.2.
        const float DarkOpacity = 0.65f;                    // 빛이 켜지기 전 불투명도 배율 — 어둠 위라 대비만으로 짙어 보인다(0.8 → 0.65)

        Material _mat;
        Mesh _mesh;
        bool _released;
        float _fade, _fadeTime;

        /// 착지 지점 center 둘레에 안개를 깐다. camRight = 지금 카메라 오른쪽(좌우 가르기 기준). 머티리얼이 없으면 null.
        public static ProtoIntroFog Create(Transform parent, Vector3 center, Vector3 camRight)
        {
            var src = Resources.Load<Material>("Intro/M_IntroFog");
            if (src == null || src.shader == null || !src.shader.isSupported)
            {
                Debug.LogWarning("[인트로] Resources/Intro/M_IntroFog(Proto/IntroFog) 가 없어 안개 없이 진행한다");
                return null;
            }
            var right = new Vector3(camRight.x, 0f, camRight.z);
            right = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;

            var go = new GameObject("IntroFog");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // 정점이 월드 좌표다
            var fog = go.AddComponent<ProtoIntroFog>();
            fog._mat = new Material(src) { name = "IntroFog" };
            fog._mat.SetVector(IdAxis, new Vector4(right.x, right.z, 0f, 0f));
            fog._mesh = BuildMesh(center, right);
            go.AddComponent<MeshFilter>().sharedMesh = fog._mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = fog._mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            fog.SetPart(0f);
            fog.SetLight(center, 3f, 0f);
            return fog;
        }

        /// 어둠 막과 같은 빛 원(착지 지점 center, 반경 radius m, 세기 level 0~1)이 안개도 밝힌다.
        public void SetLight(Vector3 center, float radius, float level)
        {
            if (_mat == null) return;
            _mat.SetVector(IdLight, new Vector4(center.x, center.y, center.z, Mathf.Max(0.1f, radius)));
            _mat.SetVector(IdLit, new Vector4(Mathf.Clamp01(level), DarkBright, LitBright, DarkOpacity));
        }

        /// 0 = 깔려 있음, 1 = 다 옅어져 사라짐.
        public void SetPart(float part)
        {
            _fade = Mathf.Clamp01(part);   // Release 뒤 스스로 옅어질 때 여기서부터 이어 간다(0 부터 다시 세면 반쯤 옅어진 안개가 다시 짙어진다)
            if (_mat != null) _mat.SetVector(IdPart, new Vector4(_fade, Spread, CenterFirst, 1f));
        }

        /// 착지 — 연출 오브젝트에서 떼어 내고 seconds 초에 걸쳐 스스로 옅어진 뒤 지운다. 여러 번 불러도 되고, 더 짧은 시간만 받아들인다(건너뛰기).
        public void Release(float seconds)
        {
            seconds = Mathf.Max(0.05f, seconds);
            if (!_released)
            {
                _released = true;
                _fadeTime = seconds;
                transform.SetParent(null, true);   // 정점이 월드 좌표라 위치는 그대로
            }
            else _fadeTime = Mathf.Min(_fadeTime, seconds);
        }

        void Update()
        {
            if (!_released) return;
            _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / _fadeTime);
            SetPart(_fade);
            if (_fade >= 1f) Destroy(gameObject);
        }

        /// 땅과 나란한 층 몇 장 — 정점은 층 모서리(월드). TEXCOORD1 = (불투명도, 노이즈 씨앗, 걷힘 속도 배율, 흐름 방향 ±1 — 4판부터 셰이더가 안 쓴다: 층이 모두 같은 쪽으로 흐른다).
        static Mesh BuildMesh(Vector3 center, Vector3 right)
        {
            var far = Vector3.Cross(right, Vector3.up);   // 카메라에서 멀어지는 방향(땅 위)
            var rng = new System.Random(Seed);
            float R() => (float)rng.NextDouble();

            var v = new List<Vector3>(); var info = new List<Vector4>(); var tri = new List<int>();
            var corners = new[] { new Vector2(-1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f) };
            float baseSeed = 3f + R() * 40f;
            for (int l = 0; l < LayerHeight.Length; l++)
            {
                // 씨앗은 층마다 조금만 다르게 — 다 다르면 다섯 장의 결이 서로 메워 평평한 푸른 막이 됐다(2판 첫 렌더)
                var layer = new Vector4(LayerAlpha[l], baseSeed + l * 0.12f, Mathf.Lerp(SpeedMin, SpeedMax, R()), (l & 1) == 0 ? 1f : -1f);
                int b0 = v.Count;
                foreach (var c in corners)
                {
                    v.Add(center + right * (c.x * LayerHalf) + far * (c.y * LayerHalf) + Vector3.up * LayerHeight[l]);
                    info.Add(layer);
                }
                tri.Add(b0); tri.Add(b0 + 1); tri.Add(b0 + 2); tri.Add(b0); tri.Add(b0 + 2); tri.Add(b0 + 3);
            }
            var m = new Mesh { name = "IntroFog" };
            m.SetVertices(v); m.SetUVs(1, info); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
