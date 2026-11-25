using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 풀 상호작용 필드 — 플레이어를 따라다니는 40m 텍스처 한 장(<c>Proto/Grass</c> 가 읽는다).
    ///
    /// <para><b>채널:</b> R = 발자국(플레이어·좀비·탱크가 밟음 — 짧게 누웠다 2초쯤에 걸쳐 일어선다),
    /// G = 폭발(<see cref="ProtoExplosion"/> 이 부른다 — 바닥까지 눕고 그을린 채 오래 있다가 천천히 일어선다).
    /// 회복 속도를 채널마다 따로 둔다(2026-10-20 사용자와 정한 방향: 발자국과 폭발은 다른 효과).</para>
    ///
    /// <para><b>매 프레임:</b> ① 원점이 움직인 만큼 밀면서 회복(Blit 1번, 256²) ② 밟힌 자리를 한 번에 찍는다(GL 사각형,
    /// 그리기 호출 1번). 좀비 수백 마리도 사각형 하나씩이라 비용이 거의 없다 — 그래서 프레임을 건너뛰지 않는다(자국이 끊긴다).</para>
    ///
    /// <para>회복은 원래 "눌린 시각을 기록해 셰이더가 계산"하는 방식으로 정했는데, 텍스처가 플레이어를 따라 움직이려면
    /// 어차피 매 프레임 밀어 주는 패스가 필요해서 회복을 그 패스에 얹었다. 채널별 회복 속도는 그대로 따로다.</para>
    ///
    /// <para>게임 시간(scaled)으로 돈다 — 레벨업 정지 중에는 자국도 그대로 멈춘다. 풀 블록 루트(<see cref="ProtoGrassBlades"/>)가 Ensure.</para>
    /// </summary>
    public class ProtoGrassField : MonoBehaviour
    {
        public const float Size = 40f;            // 필드 한 변(m) — 화면(가로 ~22m)보다 넉넉히
        const int   Res = 256;                    // 텍셀 ~0.16m
        // 세기가 1 을 넘는 만큼은 "완전히 누운 채 버티는 시간"이다(셰이더가 saturate). 회복 = 초당 깎는 양.
        const float FootStrength  = 1.2f, FootRecover  = 0.5f;    // 0.4초 누운 채 → 2초에 걸쳐 일어선다
        const float BlastStrength = 1.6f, BlastRecover = 0.22f;   // 2.7초 누운 채 → 4.5초에 걸쳐 일어선다
        const float PlayerRadius = 0.55f, TankRadius = 1.9f, ZombieRadiusMul = 1.4f, BlastRadiusMul = 1.15f;
        const int   MaxBlasts = 48;

        static ProtoGrassField _instance;
        static readonly List<Vector3> _blasts = new List<Vector3>();   // x, z, 반경

        static readonly int IdTex = Shader.PropertyToID("_GrassFieldTex");
        static readonly int IdField = Shader.PropertyToID("_GrassField");
        static readonly int IdScroll = Shader.PropertyToID("_Scroll");

        RenderTexture _a, _b;
        Material _mat;
        Vector2 _origin;
        bool _hasOrigin;

        public static void Ensure()
        {
            if (_instance != null) return;
            new GameObject("_GrassField").AddComponent<ProtoGrassField>();
        }

        /// 폭발 — 풀을 바닥까지 눕히고 그을린다. 필드가 없으면(풀 없는 씬) 아무것도 안 한다.
        public static void Blast(Vector3 p, float radius)
        {
            if (_instance == null || _blasts.Count >= MaxBlasts) return;
            _blasts.Add(new Vector3(p.x, p.z, radius * BlastRadiusMul));
        }

        void Awake()
        {
            _instance = this;
            // 셰이더는 ShaderRefs 참조 머티리얼로 빌드에 넣는다(다른 Proto 셰이더와 같은 방식)
            var refMat = Resources.Load<Material>("ShaderRefs/Ref_Proto_GrassField");
            var shader = refMat != null ? refMat.shader : Shader.Find("Proto/GrassField");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[풀] Proto/GrassField 셰이더 없음 — 풀이 밟혀도 눕지 않는다(바람만)");
                enabled = false;
                return;
            }
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            // 반정밀 실수(값 1 초과 = 누운 채 버티는 시간). 못 쓰는 기기는 8비트 — 버티는 시간만 없어진다.
            var fmt = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGHalf) ? RenderTextureFormat.RGHalf : RenderTextureFormat.ARGB32;
            _a = MakeRT(fmt, "GrassFieldA");
            _b = MakeRT(fmt, "GrassFieldB");
        }

        RenderTexture MakeRT(RenderTextureFormat fmt, string name)
        {
            var rt = new RenderTexture(Res, Res, 0, fmt, RenderTextureReadWrite.Linear)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, useMipMap = false,
            };
            rt.Create();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = prev;
            return rt;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            _blasts.Clear();
            Shader.SetGlobalVector(IdField, Vector4.zero);   // 셰이더가 필드를 끈다(바람만)
            if (_a != null) { _a.Release(); Destroy(_a); }
            if (_b != null) { _b.Release(); Destroy(_b); }
            if (_mat != null) Destroy(_mat);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // 레벨업 정지 — 자국도 멈춘다

            var run = ProtoRun.I;
            Transform center = run != null && run.player != null ? run.player.transform
                             : Camera.main != null ? Camera.main.transform : null;
            if (center == null) return;

            // 원점은 텍셀 단위로 맞춘다 — 소수점으로 밀면 매 프레임 보간되어 자국이 번진다
            float texel = Size / Res;
            Vector3 c = center.position;
            var origin = new Vector2(Mathf.Floor(c.x / texel) * texel - Size * 0.5f, Mathf.Floor(c.z / texel) * texel - Size * 0.5f);
            if (!_hasOrigin) { _origin = origin; _hasOrigin = true; }
            Vector2 scroll = (origin - _origin) / Size;

            _mat.SetVector(IdScroll, new Vector4(scroll.x, scroll.y, FootRecover * dt, BlastRecover * dt));
            Graphics.Blit(_a, _b, _mat, 0);
            _origin = origin;
            Stamp(_b, run);
            var t = _a; _a = _b; _b = t;

            Shader.SetGlobalTexture(IdTex, _a);
            Shader.SetGlobalVector(IdField, new Vector4(_origin.x, _origin.y, 1f / Size, 1.5f / Res));
        }

        void Stamp(RenderTexture rt, ProtoRun run)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadOrtho();
            _mat.SetPass(1);
            GL.Begin(GL.QUADS);

            if (run != null && run.player != null)
            {
                Vector3 p = run.player.transform.position;
                Quad(p.x, p.z, ProtoTank.Ridden != null ? TankRadius : PlayerRadius, FootStrength, 0f);
            }
            var dir = run != null ? run.director : null;
            if (dir != null)
            {
                var list = dir.Active;
                for (int i = 0; i < list.Count; i++)
                {
                    var z = list[i];
                    if (z == null || !z.Alive) continue;
                    Vector3 p = z.transform.position;
                    Quad(p.x, p.z, z.bodyRadius * ZombieRadiusMul, FootStrength, 0f);
                }
            }
            for (int i = 0; i < _blasts.Count; i++) Quad(_blasts[i].x, _blasts[i].y, _blasts[i].z, 0f, BlastStrength);
            _blasts.Clear();

            GL.End();
            GL.PopMatrix();
            RenderTexture.active = prev;
        }

        void Quad(float x, float z, float r, float foot, float blast)
        {
            float u = (x - _origin.x) / Size, v = (z - _origin.y) / Size, ru = r / Size;
            if (u + ru < 0f || u - ru > 1f || v + ru < 0f || v - ru > 1f) return;   // 필드 밖
            // GL.TexCoord2 는 "모든" 텍스처 유닛을 덮어쓴다 — 유닛 1 의 세기가 지워진다. 유닛 0 만 따로 쓴다.
            GL.MultiTexCoord3(1, foot, blast, 0f);
            GL.MultiTexCoord2(0, -1f, -1f); GL.Vertex3(u - ru, v - ru, 0f);
            GL.MultiTexCoord2(0, -1f,  1f); GL.Vertex3(u - ru, v + ru, 0f);
            GL.MultiTexCoord2(0,  1f,  1f); GL.Vertex3(u + ru, v + ru, 0f);
            GL.MultiTexCoord2(0,  1f, -1f); GL.Vertex3(u + ru, v - ru, 0f);
        }
    }
}
