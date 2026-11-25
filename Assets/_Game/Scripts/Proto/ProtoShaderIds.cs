namespace Proto
{
    /// 자주 쓰는 셰이더 속성 ID — 매 프레임 이름 문자열로 찾지 않게 모아 둔다(2026-11-20 코드 정리).
    public static class ProtoShaderIds
    {
        public static readonly int _Alpha = UnityEngine.Shader.PropertyToID("_Alpha");
        public static readonly int _BCM_NoFog = UnityEngine.Shader.PropertyToID("_BCM_NoFog");
        public static readonly int _BaseColor = UnityEngine.Shader.PropertyToID("_BaseColor");
        public static readonly int _BaseMap = UnityEngine.Shader.PropertyToID("_BaseMap");
        public static readonly int _Blend = UnityEngine.Shader.PropertyToID("_Blend");
        public static readonly int _ClearCenter = UnityEngine.Shader.PropertyToID("_ClearCenter");
        public static readonly int _ClearFeather = UnityEngine.Shader.PropertyToID("_ClearFeather");
        public static readonly int _ClearRadius = UnityEngine.Shader.PropertyToID("_ClearRadius");
        public static readonly int _Color = UnityEngine.Shader.PropertyToID("_Color");
        public static readonly int _CoreColor = UnityEngine.Shader.PropertyToID("_CoreColor");
        public static readonly int _CoreWidth = UnityEngine.Shader.PropertyToID("_CoreWidth");
        public static readonly int _Coverage = UnityEngine.Shader.PropertyToID("_Coverage");
        public static readonly int _DistBand = UnityEngine.Shader.PropertyToID("_DistBand");
        public static readonly int _Drift = UnityEngine.Shader.PropertyToID("_Drift");
        public static readonly int _DstBlend = UnityEngine.Shader.PropertyToID("_DstBlend");
        public static readonly int _EmissionColor = UnityEngine.Shader.PropertyToID("_EmissionColor");
        public static readonly int _GlowStrength = UnityEngine.Shader.PropertyToID("_GlowStrength");
        public static readonly int _Intensity = UnityEngine.Shader.PropertyToID("_Intensity");
        public static readonly int _MainTex = UnityEngine.Shader.PropertyToID("_MainTex");
        public static readonly int _NoiseScale = UnityEngine.Shader.PropertyToID("_NoiseScale");
        public static readonly int _Progress = UnityEngine.Shader.PropertyToID("_Progress");
        public static readonly int _Radial = UnityEngine.Shader.PropertyToID("_Radial");
        public static readonly int _RayCount = UnityEngine.Shader.PropertyToID("_RayCount");
        public static readonly int _RayStrength = UnityEngine.Shader.PropertyToID("_RayStrength");
        public static readonly int _Softness = UnityEngine.Shader.PropertyToID("_Softness");
        public static readonly int _SrcBlend = UnityEngine.Shader.PropertyToID("_SrcBlend");
        public static readonly int _Tint = UnityEngine.Shader.PropertyToID("_Tint");
    }

    /// 애니메이터 파라미터 해시 — SetFloat("Speed") 처럼 문자열을 매번 해시하지 않게.
    public static class ProtoAnimIds
    {
        public static readonly int Attack = UnityEngine.Animator.StringToHash("Attack");
        public static readonly int IsDead = UnityEngine.Animator.StringToHash("IsDead");
        public static readonly int JumpAttack = UnityEngine.Animator.StringToHash("JumpAttack");
        public static readonly int Speed = UnityEngine.Animator.StringToHash("Speed");
    }
}
