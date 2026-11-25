using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 즉발 광선 연출 — 슬러그 레일(WeaponStats.beam, 2026-10-21 사용자). 쏘는 순간 총구에서 끝까지 한 번에 그어지고
    /// <see cref="Life"/> 초 동안 가늘어지며 사라진다. 판정은 ProtoShooter.FireBeam 이 이미 끝냈고 여기는 그림만이다.
    ///
    /// <para>선 두 겹: 가운데 흰 심 + 바깥 색 번짐. LineRenderer 를 풀로 돌린다(씬 배선 없음).
    /// 밤 어둠 막(큐 2900)보다 뒤(3100)에 그린다 — 예광탄과 같이 "빛"이라 어둠에 묻히면 안 된다.</para>
    /// </summary>
    public class ProtoBeamFx : MonoBehaviour
    {
        const float Life = 0.16f;
        const float CoreWidth = 0.09f, GlowWidth = 0.42f;
        static readonly Color DefaultColor = new Color(0.55f, 0.8f, 1f, 1f);

        class Beam { public LineRenderer core, glow; public float born; public Color col; public float widthMul; public bool active; }

        static ProtoBeamFx _inst;
        static Material _mat;
        readonly List<Beam> _beams = new List<Beam>();

        /// 광선 하나. color 알파 0 이면 기본 청백색. HDR 값(1 초과)은 0~1 로 눌러 쓴다(선 정점 색은 HDR 이 안 된다).
        public static void Play(Vector3 from, Vector3 to, Color color, bool crit)
        {
            if (_inst == null) _inst = new GameObject("_BeamFx").AddComponent<ProtoBeamFx>();
            _inst.Spawn(from, to, color.a > 0f ? Normalize(color) : DefaultColor, crit ? 1.5f : 1f);
        }

        static Color Normalize(Color c)
        {
            float m = Mathf.Max(1f, Mathf.Max(c.r, Mathf.Max(c.g, c.b)));
            return new Color(c.r / m, c.g / m, c.b / m, 1f);
        }

        void OnDestroy() { if (_inst == this) _inst = null; }

        void Spawn(Vector3 from, Vector3 to, Color col, float widthMul)
        {
            Beam b = null;
            foreach (var x in _beams) if (!x.active) { b = x; break; }
            if (b == null)
            {
                b = new Beam { glow = MakeLine("Glow", 3100), core = MakeLine("Core", 3101) };
                _beams.Add(b);
            }
            b.active = true; b.born = Time.time; b.col = col; b.widthMul = widthMul;
            SetLine(b.glow, from, to);   // 예전 foreach (new[] {...}) 는 빔마다 배열을 만들었다(2026-11-20)
            SetLine(b.core, from, to);
            Tick(b, 0f);
        }

        LineRenderer MakeLine(string name, int queue)
        {
            if (_mat == null) _mat = ProtoFxMat.New("BeamFx", null, 3100);   // (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _mat;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = queue - 3100;   // 같은 큐 안에서 심이 번짐 위로
            return lr;
        }

        void Update()
        {
            foreach (var b in _beams)
            {
                if (!b.active) continue;
                float k = (Time.time - b.born) / Life;
                if (k >= 1f) { b.active = false; b.core.gameObject.SetActive(false); b.glow.gameObject.SetActive(false); continue; }
                Tick(b, k);
            }
        }

        void Tick(Beam b, float k)
        {
            // 처음엔 굵고 밝게 번쩍 → 가늘어지며 사라진다
            float fade = 1f - k;
            float w = b.widthMul * (0.35f + 0.65f * fade);
            b.core.widthMultiplier = CoreWidth * w;
            b.glow.widthMultiplier = GlowWidth * w;
            var core = Color.Lerp(Color.white, b.col, 0.25f); core.a = fade;
            var glow = Color.Lerp(b.col, Color.white, 0.3f); glow.a = 0.55f * fade * fade;   // 순색이면 짙은 파랑으로 가라앉는다 — 청백색 쪽으로
            b.core.startColor = b.core.endColor = core;
            // 끝으로 갈수록 살짝 옅게 — 총구에서 나간 빛으로 읽힌다
            b.glow.startColor = glow;
            glow.a *= 0.6f;
            b.glow.endColor = glow;
        }
    
        static void SetLine(LineRenderer lr, Vector3 from, Vector3 to)
        {
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.gameObject.SetActive(true);
        }
    }
}
