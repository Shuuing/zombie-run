using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 카툰 폭발(FX_ToonExplosion) 루트에 붙는다 — 불덩이 셰이더(<c>ZombieOverrun/ToonFireball</c>)에 "바닥이 어디고 배율이 얼마인지"를 알려준다.
    ///
    /// <para>왜 필요한가(2026-11-12 임시 프로젝트 렌더로 확인): 메시 파티클은 시뮬레이션 공간이 Local 이어도 정점이 <b>월드 좌표</b>로 구워져
    /// 셰이더가 자기 시스템 · 루트 위치를 모른다. 폭발을 4m 위에 놓자 "덩어리 위쪽부터 식는다 · 아래부터 사라진다"(높이 항)가 어긋나 색이 달라졌다.
    /// 그래서 루트의 월드 위치(xyz)와 배율(w)을 렌더러마다 MaterialPropertyBlock 의 <c>_Ground</c> 로 넘긴다.</para>
    /// <para>풀에서 꺼낼 때 ProtoFx 가 자리를 잡은 뒤 켜므로(OnEnable) 그때 한 번, 그 뒤로는 옮겨졌을 때만 다시 넣는다(에디터에서 끌어 옮기기 포함).</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class ToonExplosionAnchor : MonoBehaviour
    {
        static readonly int GroundId = Shader.PropertyToID("_Ground");

        ParticleSystemRenderer[] _renderers;
        MaterialPropertyBlock _mpb;
        Vector4 _last = new Vector4(float.NaN, 0f, 0f, 0f);

        void OnEnable() => Apply(true);
        void LateUpdate() => Apply(false);

        /// force = 값이 같아도 다시 넣는다(새로 켜졌거나 렌더러가 바뀌었을 때).
        public void Apply(bool force)
        {
            var t = transform;
            Vector3 p = t.position;
            var v = new Vector4(p.x, p.y, p.z, Mathf.Max(1e-3f, t.lossyScale.y));
            if (!force && v == _last) return;
            _last = v;
            if (_renderers == null || force) _renderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty(GroundId)) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetVector(GroundId, v);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
