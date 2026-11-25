using UnityEditor;
using UnityEngine;

/// <summary>
/// 화상 사망(ZombieBurnOut — 불타 죽은 좀비가 타들어가며 사라진다)이 돌 준비 — 에디터가 뜰 때 한 번. 런타임이 Resources.Load 로 찾는 머티리얼 셋을 만든다.
/// ① 본체 <c>Resources/VFX/M_BurnDissolve.mat</c>(셰이더 ZombieOverrun/BurnDissolve, 큐 2450 = 좀비 몸과 같은 자리) — 런타임이 좀비 머티리얼마다 복제해 텍스처·색을 옮긴다.
/// ② 불선 <c>Resources/VFX/M_BurnEdge.mat</c>(셰이더 ZombieOverrun/BurnEdge, 가산, 큐 3100 = 밤 어둠 막 뒤) — 타들어가는 경계의 밝은 선. 선 색·깜빡임은 이 머티리얼에서 고친다.
/// ③ 불씨 <c>Resources/VFX/M_BurnEmber.mat</c> — FX 템플릿의 가산 점 M_Fx_Glow 복제본(ProtoBurnFx 의 Embers 시스템이 쓴다).
/// Resources 에 두는 이유: 셰이더가 빌드에 들어가려면 에셋이 참조해야 한다(ZombieGibsSetup 과 같다).
/// 이미 있으면 건드리지 않는다 — 머티리얼에서 손본 색이 지워지지 않는다. 처음 값으로 되돌리려면 메뉴 '화상 사망 머티리얼 다시 만들기'.
/// </summary>
public static class ZombieBurnOutSetup
{
    const string BodyShader = "ZombieOverrun/BurnDissolve";
    const string EdgeShader = "ZombieOverrun/BurnEdge";
    const string BodyMatPath = "Assets/_Game/Resources/VFX/M_BurnDissolve.mat";
    const string EdgeMatPath = "Assets/_Game/Resources/VFX/M_BurnEdge.mat";
    const string EmberMatPath = "Assets/_Game/Resources/VFX/M_BurnEmber.mat";

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { AutoSetup(); return; }
            int made = Ensure(false);
            if (made > 0) Debug.Log($"[화상 사망] 머티리얼 {made}개를 만들었다 — Assets/_Game/Resources/VFX/M_BurnDissolve · M_BurnEdge · M_BurnEmber");
        };
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/화상 사망 머티리얼 만들기 (빠진 것만)")]
    public static void SetupMenu()
    {
        int made = Ensure(true);
        Debug.Log(made > 0 ? $"[화상 사망] 머티리얼 {made}개를 만들었다" : "[화상 사망] 머티리얼 셋은 모두 있다");
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/화상 사망 머티리얼 다시 만들기 (덮어쓰기)")]
    public static void RebuildMenu()
    {
        if (!EditorUtility.DisplayDialog("화상 사망 머티리얼 다시 만들기", "M_BurnDissolve · M_BurnEdge · M_BurnEmber 를 처음 값으로 다시 만든다.\n머티리얼에서 손본 값(선 색 등)은 사라진다.", "다시 만들기", "취소")) return;
        foreach (var p in new[] { BodyMatPath, EdgeMatPath, EmberMatPath })
            if (AssetDatabase.LoadAssetAtPath<Material>(p) != null) AssetDatabase.DeleteAsset(p);
        int made = Ensure(true);
        Debug.Log($"[화상 사망] 머티리얼 {made}개를 다시 만들었다");
    }

    /// 빠진 머티리얼을 만든다. 돌려주는 값 = 만든 수. 셰이더가 아직 임포트 전이면(처음 뜰 때) 다음 로드에 만든다.
    static int Ensure(bool verbose)
    {
        int made = 0;
        if (FromShader(BodyMatPath, BodyShader, "M_BurnDissolve", 2450, verbose)) made++;   // 좀비 몸(툰, AlphaTest)과 같은 자리 — 밤 어둠 막(2900) 앞
        if (FromShader(EdgeMatPath, EdgeShader, "M_BurnEdge", 3100, verbose)) made++;       // 밤 어둠 막 뒤 — 어둠 속에서 빛난다(FX 가산 머티리얼과 같은 큐)
        if (EnsureEmber(verbose)) made++;
        if (made > 0) AssetDatabase.SaveAssets();
        return made;
    }

    static bool FromShader(string path, string shaderName, string name, int queue, bool verbose)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return false;
        var sh = Shader.Find(shaderName);
        if (sh == null)
        {
            if (verbose) Debug.LogWarning($"[화상 사망] 셰이더 {shaderName} 를 못 찾았다 — Art/VFX/Shaders 의 BurnDissolve · BurnEdge 가 임포트됐는지(오류 없이) 볼 것");
            return false;
        }
        var m = new Material(sh) { name = name };
        m.renderQueue = queue;
        AssetDatabase.CreateAsset(m, path);
        return true;
    }

    /// 불씨 머티리얼 — 템플릿의 가산 점(M_Fx_Glow) 복제본. 원본이 없으면 템플릿 쪽이 만든다.
    static bool EnsureEmber(bool verbose)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(EmberMatPath) != null) return false;
        var src = FxTemplatePrefabs.GlowDot();
        if (src == null)
        {
            if (verbose) Debug.LogWarning("[화상 사망] 가산 점 머티리얼(M_Fx_Glow)을 못 만들었다 — FX 템플릿(FxTemplatePrefabs)을 먼저 볼 것");
            return false;
        }
        var m = new Material(src) { name = "M_BurnEmber" };
        AssetDatabase.CreateAsset(m, EmberMatPath);
        return true;
    }
}
