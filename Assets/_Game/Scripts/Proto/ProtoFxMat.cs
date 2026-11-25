using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 코드가 그리는 이펙트(궤적 · 선 · 링 · 바닥 자국 · 구슬 · 연기 판)의 머티리얼 — 게임 이펙트 셰이더 하나
    /// <c>ZombieOverrun/FX/Particle</c> 로 만든다(2026-11-23, 사용자 "이펙트는 통합 셰이더로", 예전엔 Sprites/Default).
    /// <para>통합 셰이더의 기본값(알파 섞기 · 양면 · 깊이 안 씀 · LEqual · 큐 3000)이 Sprites/Default 와 같고, 안개만 끈다
    /// (Sprites/Default 는 안개를 안 받았다) → 그림이 같다. 정점색(LineRenderer · TrailRenderer 색)도 똑같이 곱한다.</para>
    /// <para>색은 <c>material.color</c>(= _BaseColor), 텍스처는 <c>mainTexture</c>(= _BaseMap)로 넣으면 된다.
    /// <b>MaterialPropertyBlock 으로 줄 땐 이름이 다르다</b> — <see cref="ColorId"/> · <see cref="TexId"/> 를 쓴다
    /// (셰이더를 못 찾아 Sprites/Default 로 떨어지면 알아서 _Color · _MainTex).</para>
    /// <para>빌드 포함: <c>Resources/ShaderRefs/Ref_ZombieOverrun_FX_Particle.mat</c>(키워드 없는 기본 변형 — 여기서 만드는 머티리얼이 쓰는 것).</para>
    /// <para>탄피 · 파편 · 체력 바처럼 이펙트가 아닌 물체는 예전 셰이더(URP Unlit · Sprites/Default) 그대로다.</para>
    /// </summary>
    public static class ProtoFxMat
    {
        public const string ShaderName = "ZombieOverrun/FX/Particle";
        const string Fallback = "Sprites/Default";

        static Shader _shader;
        static bool _unified;
        static readonly int FogId = Shader.PropertyToID("_Fog");

        /// 이펙트용 셰이더 — 통합 셰이더, 없으면 Sprites/Default.
        public static Shader FxShader
        {
            get
            {
                if (_shader == null)
                {
                    _shader = Shader.Find(ShaderName);
                    _unified = _shader != null;
                    if (!_unified)
                    {
                        Debug.LogWarning($"[이펙트] {ShaderName} 셰이더를 못 찾았다 — 코드 이펙트를 {Fallback} 로 그린다");
                        _shader = Shader.Find(Fallback);
                    }
                }
                return _shader;
            }
        }

        /// 통합 셰이더로 그리는 중인가(못 찾아 Sprites/Default 로 떨어졌으면 false).
        public static bool Unified { get { var _ = FxShader; return _unified; } }

        /// MaterialPropertyBlock 색 이름 — 통합 셰이더 _BaseColor, 대체 셰이더 _Color.
        public static int ColorId => Unified ? ProtoShaderIds._BaseColor : ProtoShaderIds._Color;

        /// MaterialPropertyBlock 텍스처 이름 — 통합 셰이더 _BaseMap, 대체 셰이더 _MainTex.
        public static int TexId => Unified ? ProtoShaderIds._BaseMap : ProtoShaderIds._MainTex;

        /// 새 이펙트 머티리얼(Sprites/Default 와 같은 그림). queue 가 음수면 셰이더 기본(3000).
        public static Material New(string name, Texture tex = null, int queue = -1, Color? color = null)
        {
            var m = new Material(FxShader);
            if (!string.IsNullOrEmpty(name)) m.name = name;
            if (_unified) m.SetFloat(FogId, 0f);
            if (tex != null) m.mainTexture = tex;
            if (queue >= 0) m.renderQueue = queue;
            if (color.HasValue) m.color = color.Value;
            return m;
        }
    }
}
