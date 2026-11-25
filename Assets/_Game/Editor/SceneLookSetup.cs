// 화면 색감(룩) 셋업 — URP 포스트 프로세싱으로 인게임 톤을 잡는다.
//
// "카메라 앞에 반투명 면을 깔아 색을 덮는" 옛 방식은 URP 에서 쓰지 않는다.
// 그 면은 안개·하이라이트·UI 까지 똑같이 덮어 하이라이트가 탁해지고, 소팅과
// 화면비에 늘 걸린다. URP 는 같은 비용(풀스크린 1패스)으로 Volume 이 같은 일을
// 더 정확히 한다 — White Balance 로 색온도, Color Adjustments 로 채도·콘트라스트.
//
// 이 메뉴가 하는 일 세 가지:
//   1) VolumeProfile 에셋을 아래 상수대로 (재)생성
//   2) 현재 씬에 글로벌 Volume 을 놓고 메인 카메라의 포스트 프로세싱을 켠다
//      — 카메라에 UniversalAdditionalCameraData 가 없으면 포스트가 아예 안 걸린다.
//        코드로 AddComponent<Camera>() 한 카메라가 딱 그 상태였다.
//   3) 앰비언트·안개를 청회색에서 중성 회색으로 되돌린다 (차가운 느낌의 실제 원인)
//   4) 필드 바닥 안개(_GroundFog, Proto/GroundFog 셰이더)를 놓는다
//   5) 지면 판을 세분화한다 — 안개가 거리대로 먹으려면 필수 (GroundMeshDensity)
//
// FieldBuilder 가 이 함수를 호출하므로 '필드 생성' 을 다시 돌려도 룩은 살아남는다.
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SceneLookSetup
{
    const string FxRoot      = "Assets/_Game/Art/PostFX";
    const string ProfilePath = FxRoot + "/Field_Look.asset";

    // ── 그레이딩 상수 — 색감은 전부 여기서만 만진다 ────────────────────────
    // 색온도. +가 따뜻함.
    // 2026-09-04: 16 은 노을처럼 보인다는 판단으로 5 로 내렸다. "차갑지 않은데
    // 고유색은 살아 있는" 구간이 4~7 이고, 12 를 넘기면 흰색이 크림색이 되면서
    // 아스팔트 회색과 콘크리트 회색이 같은 베이지로 붙어버린다.
    const float Temperature = 5f;
    const float Tint        = 1f;    // +가 마젠타. 초목의 형광 초록을 살짝 눌러준다

    const float PostExposure = 0.05f;
    const float Contrast     = 8f;
    // 온도를 내린 만큼 채도를 올린다 — 웜 필터가 색을 뭉쳐 만들던 "풍부함"을
    // 채도로 대체하는 것. 이래야 노랗지 않으면서도 초록·주황이 각자 제 색으로 읽힌다.
    const float Saturation    = 10f;
    // 거의 중성. 여기에 색을 넣으면 온도를 내린 게 상쇄된다.
    static readonly Color ColorFilter = new Color(1.00f, 0.995f, 0.985f);

    // 그림자는 살짝 차갑게, 하이라이트만 아주 약하게 따뜻하게 — 화면 전체를 균일하게
    // 덮는 반투명 면으로는 낼 수 없는 깊이감이 여기서 나온다.
    // 하이라이트 웜은 0.94 → 0.99 로 줄였다(석양기의 절반이 여기서 나왔다).
    static readonly Vector4 ShadowTint    = new Vector4(0.97f, 0.99f, 1.04f, 0f);
    static readonly Vector4 MidtoneTint   = new Vector4(1.00f, 1.00f, 1.00f, 0f);
    static readonly Vector4 HighlightTint = new Vector4(1.02f, 1.01f, 0.99f, 0f);

    const float BloomThreshold = 1.05f;
    const float BloomIntensity = 0.35f;
    const float BloomScatter   = 0.62f;

    // 비네트 = "중앙은 그대로, 밖으로 갈수록 어둡게". 카메라가 플레이어를 정확히
    // 화면 중심에 두므로(ProtoCameraFollow: 회전이 피치뿐) 중심 0.5,0.5 가 곧 캐릭터다.
    // rounded=false 로 화면비를 따라가게 둔다 — true 면 세로 화면에서 위아래가 과하게 먹힌다.
    // 2026-09-04: 0.45 는 너무 어둡다는 판단으로 0.26 으로 내렸다. 동시에 Smoothness 를
    // 0.55→0.7 로 올려 경계를 더 넓게 풀었다 — **어둡기를 줄일 때 이 둘을 같이 만져야 한다.**
    // Intensity 만 내리면 어두운 양은 줄지만 감쇠 폭이 그대로여서 귀퉁이에 테두리 선처럼 보인다.
    const float VignetteIntensity  = 0.26f;
    const float VignetteSmoothness = 0.70f;
    const bool  VignetteRounded    = false;

    // ── 씬 조명 상수 ──────────────────────────────────────────────────────
    // GDD §15 의 "원경 청회색 헤이즈" 를 중성 회색까지만 끌어온다. 파랑을 완전히
    // 빼지 않는 건 원경 대기감이 죽기 때문이고, 최종 온도는 위 Temperature 가 잡는다.
    static readonly Color AmbientSky     = new Color(0.60f, 0.61f, 0.63f);
    static readonly Color AmbientEquator = new Color(0.46f, 0.46f, 0.46f);
    static readonly Color AmbientGround  = new Color(0.26f, 0.25f, 0.24f);

    // 안개색 — GDD §15 의 "원경 청회색 헤이즈".
    //
    // 이력: 2026-09-04 에 "화면이 차갑다" 는 이유로 앰비언트와 안개를 **같이** 중성 회색
    // (0.68, 0.69, 0.70) 까지 끌어왔다. 2026-09-15 사용자 요청으로 안개만 다시 파랑 계통으로
    // 돌린다 — **앰비언트는 중성 그대로 둔다.**
    // 둘을 갈라놓는 게 요점이다: 앰비언트는 화면의 모든 면을 조명해서 파랗게 하면 근경까지
    // 차가워지지만, 안개는 거리에 비례해서만 섞이므로 원경만 파래진다. 9월 4일에 문제였던
    // "차가운 느낌" 의 실제 출처는 앰비언트 쪽이었다.
    //
    // 더 진한 파랑을 원하면 아래 Blue 쪽 값으로 바꾼다. 휘도(약 0.67)를 맞춰 뒀으니
    // 색만 바뀌고 안개가 짙어/옅어 보이지는 않는다.
    //   은은한 청회색 (현재): new Color(0.62f, 0.68f, 0.78f)
    //   뚜렷한 파랑        : new Color(0.57f, 0.67f, 0.86f)
    static readonly Color FogColor       = new Color(0.62f, 0.68f, 0.78f);

    // 2026-09-15: 26~68m 은 **화면에 단 1픽셀도 걸리지 않았다.** 카메라가 보는 지면의
    // 거리 범위는 카메라높이/sin(피치 ± FOV절반) 으로 결정되는데,
    // 현재 값(ProtoCameraFollow.distance 17.76 · pitch 58° · 수직 FOV 42° → 높이 15.06m)에서
    //   화면 하단 15.3m · 중앙(플레이어) 17.8m · 상단 25.0m
    // 이다. 즉 보이는 지면 전체가 26m 앞이라 안개가 켜져 있어도 아무 일도 일어나지 않았다.
    // 화면 전체가 겨우 10m 폭의 거리 띠라는 게 핵심이다. 여기에 40m 짜리 안개 구간을
    // 걸면 상단조차 절반도 안 섞여서 "안 넣은 것과 구별이 안 되는" 상태가 된다
    // (18~34m 로 처음 잡았을 때가 정확히 그랬다). 구간을 그 10m 띠에 맞춰야 한다:
    //   상단 73% / 플레이어 7% 를 원하면  (25-s)/(e-s)=0.73, (17.8-s)/(e-s)=0.07
    //   → e-s ≈ 11, s ≈ 17  →  17~28m
    // **카메라 거리·피치·FOV 를 바꾸면 위 식으로 다시 풀고 ProtoGroundFog 의 주석도 같이 고칠 것.**
    //
    // 2026-09-15 재조정(19.5~29): 캐릭터 주변 맑은 구역을 넣으면서 시작점을 더 밀었다.
    // URP 내장 거리 안개는 **카메라로부터의 거리**로만 계산돼 캐릭터 중심 구멍을 뚫을 수가
    // 없다 — 그러려면 씬의 모든 셰이더(BCM 툰·물·캐릭터)를 고쳐야 한다. 그래서 내장 안개는
    // 맑은 구역 안에서 애초에 거의 안 깔리게 밀어두고, 안개의 질감은 바닥 안개가 맡는다.
    // 구역 안 최악 지점은 캐릭터 정면 4.5m(= 카메라로부터 20.5m). 거기서 10% 이하를 원하면
    //   (20.5-s)/(29-s) ≤ 0.10  →  s ≥ 19.6
    // 이고, 그때 화면 상단(25m)은 58% 가 남는다.
    const float FogStart = 19.5f, FogEnd = 29f;

    // 맑은 구역은 전투 범위에 맞춘다 — 무기 사거리가 2.5~11m 라 최단 사거리까지는 완전히
    // 맑고, 최장 사거리 밖에서 안개가 100% 가 된다. 경계 폭이 반경보다 넓어야
    // 캐릭터를 따라다니는 동그란 원으로 안 보인다.
    const float FogClearRadius = 4.5f, FogClearFeather = 6.5f;

    // 태양도 노랑을 뺀다. 직사광이 노란색이면 그레이딩을 중성으로 내려도
    // 조명 단계에서 이미 노란 픽셀이 들어와 되돌릴 수 없다.
    static readonly Color SunColor = new Color(1.00f, 0.975f, 0.94f);
    const float SunIntensity = 1.2f;

    [MenuItem("Tools/좀비 오버런/화면 색감(포스트FX) 셋업")]
    public static void ApplyToOpenScene()
    {
        Apply(null);
        Debug.Log("[Look] 현재 씬에 적용했다. 씬 저장은 직접 할 것 (Ctrl+S).");
    }

    /// 룩을 현재 열린 씬에 적용한다.
    /// lightRoot 가 있으면 Volume 을 그 아래에 붙인다 (FieldBuilder 의 _Lighting).
    public static void Apply(GameObject lightRoot)
    {
        var profile = BuildProfile();

        // ── 글로벌 Volume ────────────────────────────────────────────────
        var volGO = GameObject.Find("_PostFX");
        if (volGO == null)
        {
            volGO = new GameObject("_PostFX");
            if (lightRoot != null) volGO.transform.SetParent(lightRoot.transform, false);
        }
        var vol = volGO.GetComponent<Volume>();
        if (vol == null) vol = volGO.AddComponent<Volume>();
        vol.isGlobal      = true;
        vol.priority      = 0f;
        vol.weight        = 1f;
        vol.sharedProfile = profile;

        // ── 카메라: 포스트 프로세싱 켜기 ─────────────────────────────────
        int camCount = 0;
        foreach (var cam in Object.FindObjectsOfType<Camera>())
        {
            if (cam.cameraType != CameraType.Game) continue;
            var acd = cam.GetComponent<UniversalAdditionalCameraData>();
            if (acd == null) acd = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            acd.renderPostProcessing = true;
            acd.antialiasing         = AntialiasingMode.FastApproximateAntialiasing;
            acd.renderShadows        = true;
            cam.backgroundColor      = FogColor;   // 안개와 스카이박스 경계가 안 보이게
            camCount++;
        }
        if (camCount == 0) Debug.LogWarning("[Look] 씬에 게임 카메라가 없다 — 포스트 프로세싱을 켤 대상이 없음");

        // 안개보다 **먼저** 돌려야 한다. 내장 안개는 정점 단위로 계산돼 보간되므로,
        // 정점 네 개짜리 40m 지면 판에서는 판 전체가 한 가지 안개값으로 뭉개진다 —
        // 아래에서 FogStart/FogEnd 를 아무리 정확히 풀어도 지면에는 안 먹는다.
        // 자세한 근거는 GroundMeshDensity.cs 주석에 있다.
        int groundFixed = GroundMeshDensity.Repair();

        ApplyEnvironment();
        int fogSheets = EnsureGroundFog(lightRoot);

        Debug.Log($"[Look] Volume 1개 · 카메라 {camCount}개 · 지면 판 {groundFixed}개 세분화 · 바닥 안개 {fogSheets}층 · 색온도 {Temperature:+0;-0} 적용. 프로파일: {ProfilePath}");
    }

    /// 필드 바닥 안개. 거리 안개(ApplyEnvironment)만으로는 원경이 균일하게 흐려질 뿐
    /// "안개 덩어리" 의 질감이 안 나와서, 낮게 깔린 시트를 따로 얹는다.
    /// 시트와 재질은 ProtoGroundFog 가 런타임에 만든다 — 에셋을 굽지 않으므로
    /// 여기서는 컴포넌트를 놓고 수치만 채운다.
    static int EnsureGroundFog(GameObject lightRoot)
    {
        // GameObject.Find 를 쓰면 안 된다 — **비활성 오브젝트를 못 찾는다.** 안개를 잠깐 꺼두고
        // 이 메뉴를 다시 돌리면 기존 _GroundFog 를 못 찾아 매번 새로 만들어서, 씬에 꺼진
        // 안개와 켜진 안개가 겹겹이 쌓인다. 컴포넌트로 찾되 비활성까지 포함한다.
        var fog = Object.FindObjectOfType<Proto.ProtoGroundFog>(true);
        if (fog == null)
        {
            var fogGO = new GameObject("_GroundFog");
            if (lightRoot != null) fogGO.transform.SetParent(lightRoot.transform, false);
            fog = fogGO.AddComponent<Proto.ProtoGroundFog>();
        }
        fog.gameObject.SetActive(true);

        // 차오르는 구간은 거리 안개와 같은 선(FogStart)에서 시작해 화면 상단(약 25m)에서
        // 최대가 된다. 페이드아웃(+12·+18)은 거리 안개가 이미 100% 인 한참 뒤라 평소엔
        // 쓰이지 않는다 — 카메라를 뒤로 뺐을 때 시트의 네모난 가장자리가 드러나는 걸
        // 막는 안전장치다.
        // 화면 하단(15.3m) 부터 옅게라도 깔리도록 그 앞에서 열고 플레이어 거리 언저리에서
        // 최대가 된다. 캐릭터 주변을 비우는 건 아래 맑은 구역이 맡으므로 이 띠는 플레이어를
        // 피해 갈 필요가 없다 — 오히려 피하면 안개가 캐릭터 뒤쪽에도 없어서 반달이 된다.
        // 페이드아웃은 거리 안개가 이미 100% 인 한참 뒤라, 카메라를 뒤로 뺐을 때 시트의
        // 네모난 가장자리가 드러나는 걸 막는 안전장치다.
        fog.distanceBand = new Vector4(13f, 19f, FogEnd + 11f, FogEnd + 17f);
        fog.clearAroundPlayer = true;
        fog.clearRadius       = FogClearRadius;
        fog.clearFeather      = FogClearFeather;
        fog.followSceneFogColor = true;
        fog.color = FogColor;

        // 수치는 머티리얼에 구워지므로 필드만 바꿔서는 화면이 안 변한다.
        fog.Rebuild();
        EditorUtility.SetDirty(fog);
        return fog.sheets != null ? fog.sheets.Length : 0;
    }

    /// 앰비언트·안개·태양 — Volume 없이도 먹는 씬 자체 조명값.
    public static void ApplyEnvironment()
    {
        RenderSettings.ambientMode         = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor     = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor  = AmbientGround;
        RenderSettings.fog                 = true;
        RenderSettings.fogMode             = FogMode.Linear;
        RenderSettings.fogColor            = FogColor;
        RenderSettings.fogStartDistance    = FogStart;
        RenderSettings.fogEndDistance      = FogEnd;

        foreach (var l in Object.FindObjectsOfType<Light>())
        {
            if (l.type != LightType.Directional) continue;
            l.color     = SunColor;
            l.intensity = SunIntensity;
        }
    }

    /// 프로파일 에셋을 상수대로 다시 굽는다. 손으로 만진 슬라이더는 덮어쓴다 —
    /// 값을 고정하려면 위 상수를 고칠 것.
    public static VolumeProfile BuildProfile()
    {
        if (!AssetDatabase.IsValidFolder(FxRoot))
            AssetDatabase.CreateFolder("Assets/_Game/Art", "PostFX");

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        bool isNew = profile == null;
        if (isNew) profile = ScriptableObject.CreateInstance<VolumeProfile>();
        else       profile.components.Clear();

        var tone = profile.Add<Tonemapping>();
        Set(tone.mode, TonemappingMode.Neutral);   // ACES 는 모바일에서 색이 눌린다

        var wb = profile.Add<WhiteBalance>();
        Set(wb.temperature, Temperature);
        Set(wb.tint, Tint);

        var ca = profile.Add<ColorAdjustments>();
        Set(ca.postExposure, PostExposure);
        Set(ca.contrast, Contrast);
        Set(ca.colorFilter, ColorFilter);
        Set(ca.saturation, Saturation);

        var smh = profile.Add<ShadowsMidtonesHighlights>();
        Set(smh.shadows, ShadowTint);
        Set(smh.midtones, MidtoneTint);
        Set(smh.highlights, HighlightTint);

        var bloom = profile.Add<Bloom>();
        Set(bloom.threshold, BloomThreshold);
        Set(bloom.intensity, BloomIntensity);
        Set(bloom.scatter, BloomScatter);

        var vig = profile.Add<Vignette>();
        Set(vig.center, new Vector2(0.5f, 0.5f));   // 캐릭터가 늘 화면 중심에 온다
        Set(vig.intensity, VignetteIntensity);
        Set(vig.smoothness, VignetteSmoothness);
        Set(vig.rounded, VignetteRounded);

        if (isNew) AssetDatabase.CreateAsset(profile, ProfilePath);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// VolumeParameter 는 값만 넣어도 overrideState 가 false 면 무시된다.
    /// 둘을 같이 세팅하는 걸 잊지 않게 한 곳으로 묶는다.
    static void Set<T>(VolumeParameter<T> p, T value)
    {
        p.value         = value;
        p.overrideState = true;
    }
}
