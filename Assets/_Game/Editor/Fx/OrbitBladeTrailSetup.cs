using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 회전 블레이드(보조 장비 '몸 주위를 도는 톱날') 꼬리 — 2026-11-18 사용자 "회전 톱니에 궤적 이펙트 만들어줘".
/// 코드로 굽는 우리 그림(외부 팩 · 추출본 없음): 텍스처 2 · 머티리얼 2 · 프리팹 <see cref="PrefabPath"/>.
///
/// <para><b>모양.</b> 톱날 뒤로 끌리는 빛 띠 두 겹(넓은 하늘색 빛 + 가는 흰 심) + 테두리에서 튀는 쇳불똥.
/// 띠는 파티클 리본(입자를 잇는 띠)이고, 입자 색 · 크기가 나이에 따라 흐려지고 가늘어져 꼬리 끝이 저절로 사라진다.
/// 머리(톱날 쪽)는 투명 · 가늘게 시작한다 — 잘린 단면이 보이지 않게(11-18 물고기 꼬리에서 사용자가 짚은 결함).</para>
///
/// <para><b>자리 규약.</b> 루트 = 톱날 가운데, +Z = 나아가는 쪽(궤도 접선), +Y = 위. ProtoSupportGear 가 톱날마다 피벗을 두고 스팬으로 따라가게 한다.
/// 띠 두 층은 시뮬레이션 공간이 <b>Custom</b> — 코드가 궤도 루트(플레이어를 따라다니는 _Gear_ 루트)에 묶는다(<c>FxHandle.BindSimulationSpace</c>).
/// 그래서 플레이어가 달려도 꼬리가 뒤로 끌려 휘지 않고 궤도 원을 그린다(월드 꼬리면 이동 4.2m/s 가 톱날 11m/s 에 섞여 한쪽만 길어진다).
/// 묶지 않으면(쇼케이스 등) 월드 공간처럼 돈다. 불똥은 월드 공간 — 튄 뒤엔 제 갈 길을 간다.</para>
///
/// <para>다시 굽기는 같은 경로에 덮어써 GUID 가 그대로다(카탈로그 · WFX 참조 유지). 손으로 고친 프리팹은 사라지니 묻고 한다.</para>
/// </summary>
public static class OrbitBladeTrailSetup
{
    public const string Dir = "Assets/_Game/Art/Sujin/S_VFX/Weapon/OrbitBlade";   // 11-23 옮김(옛 Art/VFX/Weapons/OrbitBlade)
    public const string PrefabPath = Dir + "/FX_OrbitBlade_Trail.prefab";
    const string TexDir = "Assets/_Game/Art/Sujin/S_Textures/VFX";   // 11-25: Sujin 텍스처는 S_Textures 한 곳
    const string RibbonTexPath = TexDir + "/T_OrbitTrail_Ribbon.png";
    const string SparkTexPath  = TexDir + "/T_OrbitTrail_Spark.png";
    const string RibbonMatPath = Dir + "/M_OrbitTrail_Ribbon.mat";
    const string SparkMatPath  = Dir + "/M_OrbitTrail_Spark.mat";

    /// 밤 어둠 막(2900) 뒤에 그려 어둠 속에서도 빛나 보이게 — FxTemplatePrefabs.GlowQueue 와 같은 규칙.
    /// URP 파티클 셰이더는 큐를 3000 + _QueueOffset 으로 다시 계산하므로 둘 다 건다.
    const int GlowQueue = 3100;

    // ── 값 (톱날: 궤도 반지름 2.5m · 42rpm → 접선 속도 약 11m/s, 톱날 지름 약 0.9m) ──
    // 빛 띠: 수명 0.26초 ≈ 궤도 66°. 심: 0.17초 ≈ 43°.
    // 너비 11-19 사용자 "띠 너비를 좀 얇게" → 0.95 / 0.30 에서 0.6 / 0.18 → "더 얇게" → 0.4 / 0.12(톱날 지름 0.85m 의 절반쯤)
    public const float GlowLife = 0.26f, GlowWidth = 0.4f, CoreLife = 0.17f, CoreWidth = 0.12f;
    const float RibbonRate = 120f;   // 초당 입자 — 띠 마디(11m/s ÷ 120 ≈ 0.09m) 가 궤도 곡선을 매끈하게 따른다
    static readonly Color GlowColor = new Color(0.42f, 0.78f, 1f, 1f);    // 하늘색 — 쇠가 공기를 가르는 빛(주황 톱날 · 불 무기와 겹치지 않게)
    static readonly Color CoreColor = new Color(0.92f, 0.98f, 1f, 1f);    // 거의 흰 심
    static readonly Color SparkHot  = new Color(1f, 0.93f, 0.62f, 1f);    // 쇳불똥 — 노랑 → 주황
    static readonly Color SparkWarm = new Color(1f, 0.70f, 0.28f, 1f);
    static readonly Color SparkEnd  = new Color(1f, 0.42f, 0.10f, 0f);
    const float GlowBoost = 1.35f;    // 가산 머티리얼 밝기(블룸 문턱) — 띠는 넓어서 공통 1.6 보다 낮게

    [MenuItem("Tools/좀비 오버런/FX 툴/회전 블레이드 꼬리 다시 굽기 (덮어쓰기)")]
    static void RebuildMenu()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog("회전 블레이드 꼬리", "FX_OrbitBlade_Trail 프리팹 · 머티리얼 · 텍스처를 코드 값으로 덮어쓴다. 손으로 고친 것은 사라진다(GUID 는 그대로).", "덮어쓰기", "취소"))
            return;
        Build();
    }

    public static GameObject Build()
    {
        EnsureFolder(Dir);
        var ribbonTex = BakeTex(RibbonTexPath, 256, 64, RibbonAlpha, clampU: true);
        var sparkTex  = BakeTex(SparkTexPath, 64, 64, SparkAlpha, clampU: true);
        var ribbonMat = AdditiveMat(RibbonMatPath, ribbonTex);
        var sparkMat  = AdditiveMat(SparkMatPath, sparkTex);

        var root = new GameObject("FX_OrbitBlade_Trail");
        try
        {
            Ribbon(root, "Ribbon_Glow", ribbonMat, GlowLife, GlowWidth, GlowColor,
                   alpha: new[] { (0f, 0f), (0.10f, 0.75f), (0.45f, 0.42f), (1f, 0f) },
                   size: new[] { (0f, 0.35f), (0.12f, 1f), (0.55f, 0.75f), (1f, 0.2f) });
            Ribbon(root, "Ribbon_Core", ribbonMat, CoreLife, CoreWidth, CoreColor,
                   alpha: new[] { (0f, 0f), (0.10f, 1f), (0.40f, 0.65f), (1f, 0f) },
                   size: new[] { (0f, 0.4f), (0.14f, 1f), (1f, 0.15f) });
            Sparks(root, sparkMat);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[회전 블레이드 꼬리] 구움 — {PrefabPath} (빛 띠 {GlowLife}s × {GlowWidth}m · 심 {CoreLife}s × {CoreWidth}m · 쇳불똥)");
            return prefab;
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ── 층 ─────────────────────────────────────────────────────────
    /// 리본 한 층. 입자는 그리지 않고(렌더 모드 None) 입자를 잇는 띠만 그린다. 띠 굵기 = 입자 크기, 띠 색 = 입자 색.
    static ParticleSystem Ribbon(GameObject root, string name, Material mat, float life, float width, Color color,
                                 (float t, float a)[] alpha, (float t, float s)[] size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();

        var m = ps.main;
        m.duration = 1f;
        m.loop = true;
        m.playOnAwake = true;
        m.startLifetime = life;
        m.startSpeed = 0f;
        m.startSize = width;
        m.startColor = Color.white;
        m.simulationSpace = ParticleSystemSimulationSpace.Custom;   // 코드가 궤도 루트에 묶는다(없으면 월드)
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        m.maxParticles = Mathf.CeilToInt(RibbonRate * life * 1.6f) + 8;
        m.stopAction = ParticleSystemStopAction.None;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = RibbonRate;

        var sh = ps.shape;
        sh.enabled = false;   // 톱날 가운데 한 점

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        var ak = new GradientAlphaKey[alpha.Length];
        for (int i = 0; i < alpha.Length; i++) ak[i] = new GradientAlphaKey(alpha[i].a, alpha[i].t);
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.Lerp(color, GlowColor, 0.5f), 1f) }, ak);
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve();
        foreach (var k in size) curve.AddKey(new Keyframe(k.t, k.s));
        for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var tr = ps.trails;
        tr.enabled = true;
        tr.mode = ParticleSystemTrailMode.Ribbon;
        tr.ribbonCount = 1;
        tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
        tr.sizeAffectsWidth = true;
        tr.inheritParticleColor = true;
        tr.widthOverTrail = 1f;
        tr.colorOverTrail = Color.white;
        tr.dieWithParticles = true;
        // 함정(11-18 렌더 확인): attachRibbonsToTransform 을 켜면 Custom 공간에서 가끔 띠 머리가 공간 원점(플레이어 발밑)으로 이어져
        // 톱날에서 플레이어로 직선이 그어졌다. 끈다 — 마지막 입자와 톱날 사이는 한 프레임 이동(≈0.2m)뿐이고 톱날 몸(0.85m)에 가린다.
        tr.attachRibbonsToTransform = false;

        r.renderMode = ParticleSystemRenderMode.None;   // 띠만
        r.trailMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    /// 쇳불똥 — 톱날 테두리(지름 약 0.85m 원)에서 바깥으로 튀고, 톱날 속도를 조금 물려받아 앞으로 흩어진다. 월드 공간.
    static ParticleSystem Sparks(GameObject root, Material mat)
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();

        var m = ps.main;
        m.duration = 1f;
        m.loop = true;
        m.playOnAwake = true;
        m.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        m.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.5f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.12f);
        m.startColor = new ParticleSystem.MinMaxGradient(SparkHot, SparkWarm);
        m.gravityModifier = 0.7f;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        m.maxParticles = 24;
        m.stopAction = ParticleSystemStopAction.None;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 11f;

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = 0.42f;
        sh.radiusThickness = 0f;               // 테두리에서만
        sh.rotation = new Vector3(90f, 0f, 0f); // 원은 기본 XY — 눕혀 톱날 면(XZ)에
        sh.randomDirectionAmount = 0.15f;

        var iv = ps.inheritVelocity;
        iv.enabled = true;
        iv.mode = ParticleSystemInheritVelocityMode.Initial;
        iv.curve = 0.35f;                      // 접선으로 조금 끌려간다(톱날 회전 방향으로 흩뿌림)

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(SparkEnd, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));

        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.035f;
        r.lengthScale = 1.4f;
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // ── 텍스처 ─────────────────────────────────────────────────────
    /// 띠 단면(V = 폭) — 페더 먹은 듯 넓게 번지는 몸통 + 가운데 밝은 줄 + 가는 속도선 두 가닥(잔결 없이 매끈하게).
    /// U(길이)는 양 끝만 살짝 흐리게 — 길이 방향 흐려짐은 입자 색이 맡는다.
    static float RibbonAlpha(float u, float v)
    {
        float d = Mathf.Abs(v * 2f - 1f);                                  // 0 = 가운데, 1 = 가장자리
        float body = Mathf.Pow(1f - Smooth(0f, 1f, d), 1.4f);              // 넓은 페더
        float core = Mathf.Exp(-(d * d) / (2f * 0.16f * 0.16f));          // 가운데 밝은 줄
        float lineA = Line(v, 0.30f, 0.035f) * Smooth(0.05f, 0.35f, u) * (1f - Smooth(0.6f, 0.95f, u));
        float lineB = Line(v, 0.72f, 0.030f) * Smooth(0.15f, 0.5f, u) * (1f - Smooth(0.75f, 1f, u));
        float a = body * 0.55f + core * 0.45f + (lineA + lineB) * 0.28f * (1f - d);
        float ends = Smooth(0f, 0.04f, u) * (1f - Smooth(0.96f, 1f, u));
        return Mathf.Clamp01(a * ends);
    }

    static float Line(float v, float at, float w) { float x = (v - at) / w; return Mathf.Exp(-x * x); }

    /// 셰이더의 smoothstep(e0, e1, x). 함정: Unity Mathf.SmoothStep(from, to, t) 는 'from~to 사이를 t 로 부드럽게 보간'이라 뜻이 다르다 —
    /// 그걸 썼더니 띠 텍스처 알파가 전부 0 으로 구워져 꼬리가 안 보였다(11-18).
    static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

    /// 부드러운 점(불똥 — 늘인 빌보드로 가늘게 늘어난다).
    static float SparkAlpha(float u, float v)
    {
        float x = u * 2f - 1f, y = v * 2f - 1f;
        float r = Mathf.Sqrt(x * x + y * y);
        return Mathf.Pow(Mathf.Clamp01(1f - r), 1.5f);
    }

    static Texture2D BakeTex(string path, int w, int h, System.Func<float, float, float> alpha, bool clampU)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                t.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / w, (y + 0.5f) / h)));
        t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = clampU ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.sRGBTexture = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ── 머티리얼 ───────────────────────────────────────────────────
    /// URP Particles/Unlit 가산. 있으면 그 에셋을 고쳐 쓴다(GUID 유지).
    static Material AdditiveMat(string path, Texture2D tex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        var sh = Shader.Find(FxShaderUnify.ShaderName);
        if (create) m = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = Path.GetFileNameWithoutExtension(path) };
        else if (sh != null && m.shader != sh) m.shader = sh;

        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        m.mainTexture = tex;
        void F(string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        var boost = new Color(GlowBoost, GlowBoost, GlowBoost, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", boost);
        if (m.HasProperty("_Color")) m.SetColor("_Color", boost);
        F("_Surface", 1f); F("_Blend", 2f); F("_SrcBlend", 5f); F("_DstBlend", 1f); F("_SrcBlendAlpha", 1f); F("_DstBlendAlpha", 1f);
        F("_ZWrite", 0f); F("_Cull", 0f); F("_ColorMode", 0f); F("_SoftParticlesEnabled", 0f); F("_FlipbookBlending", 0f);
        F("_EmissionEnabled", 0f);
        F("_QueueOffset", GlowQueue - 3000);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        foreach (var k in new[] { "_ALPHAPREMULTIPLY_ON", "_ALPHAMODULATE_ON", "_EMISSION", "_FLIPBOOKBLENDING_ON", "_SOFTPARTICLES_ON",
                                  "_COLORCOLOR_ON", "_COLOROVERLAY_ON", "_COLORADDSUBDIFF_ON" })
            m.DisableKeyword(k);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        m.renderQueue = GlowQueue;

        if (create) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
