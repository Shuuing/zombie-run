using UnityEditor;
using UnityEngine;

/// <summary>
/// 파편(ZombieGibs)이 돌 준비 — 에디터가 뜰 때 한 번.
/// ① 적 모델 3종의 FBX Read/Write 를 켠다(스킨 메시의 정점·본 가중치를 읽어야 한다. 빌드에서는 안 켜면 못 읽는다). 한 번 다시 임포트.
/// ② 디졸브 머티리얼 <c>Resources/VFX/M_GibDissolve.mat</c>(셰이더 ZombieOverrun/GibDissolve)을 만든다 — 런타임이 Resources.Load 로 찾아 조각마다 복제한다.
///    Resources 에 두는 이유: 셰이더가 빌드에 들어가려면 에셋이 참조해야 한다.
/// ③ 착지 피 머티리얼 <c>Resources/VFX/M_GibBlood.mat</c> — FX 템플릿의 바닥 피 얼룩 M_Fx_Blood(URP Particles Unlit · 알파 · T_Fx_Splat)를 복제하고 소프트 파티클만 끈다
///    (바닥에 붙는 얼룩은 깊이 차가 0 이라 깊이 텍스처를 켜는 날 옅어진다). 템플릿이 아직 안 구워졌으면 다음 로드에 만든다.
/// </summary>
public static class ZombieGibsSetup
{
    static readonly string[] EnemyPrefabs =
    {
        "Assets/_Game/Art/Enemies/Zombie/Zombie_Walker.prefab",
        "Assets/_Game/Resources/Enemies/Zombie_Dog.prefab",
        "Assets/_Game/Resources/Enemies/Zombie_Ranged.prefab",
    };
    const string DissolveShader = "ZombieOverrun/GibDissolve";
    const string DissolveMatPath = "Assets/_Game/Resources/VFX/M_GibDissolve.mat";
    const string BloodSrcPath = "Assets/_Game/Art/VFX/Common/M_Fx_Blood.mat";     // FxTemplatePrefabs 가 만든다(AlphaBlend · Splat)
    const string BloodMatPath = "Assets/_Game/Resources/VFX/M_GibBlood.mat";

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { AutoSetup(); return; }
            int n = EnableReadWrite(false);
            if (n > 0) Debug.Log($"[파편] 적 모델 {n}개의 Read/Write 를 켰다 — 죽을 때 몸 조각을 만들려면 필요하다");
            if (EnsureDissolveMaterial(false)) Debug.Log($"[파편] 디졸브 머티리얼을 만들었다 — {DissolveMatPath}");
            if (EnsureBloodMaterial(false)) Debug.Log($"[파편] 착지 피 머티리얼을 만들었다 — {BloodMatPath}");
        };
    }

    [MenuItem("Tools/좀비 오버런/FX 툴/적 모델 Read-Write 켜기 · 파편 머티리얼 만들기")]
    public static void SetupMenu()
    {
        int n = EnableReadWrite(true);
        bool made = EnsureDissolveMaterial(true);
        bool blood = EnsureBloodMaterial(true);
        Debug.Log((n > 0 ? $"[파편] 적 모델 {n}개의 Read/Write 를 켰다. " : "[파편] 적 모델은 모두 Read/Write 가 켜져 있다. ") +
                  (made ? $"디졸브 머티리얼을 만들었다 — {DissolveMatPath}. " : "디졸브 머티리얼은 있다. ") +
                  (blood ? $"착지 피 머티리얼을 만들었다 — {BloodMatPath}" : "착지 피 머티리얼은 있다"));
    }

    static int EnableReadWrite(bool verbose)
    {
        int changed = 0;
        foreach (var path in EnemyPrefabs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { if (verbose) Debug.LogWarning($"[파편] 적 프리팹이 없다: {path}"); continue; }
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                var imp = AssetImporter.GetAtPath(meshPath) as ModelImporter;
                if (imp == null) { if (verbose) Debug.LogWarning($"[파편] 모델 임포터가 아니다: {meshPath}"); continue; }
                if (imp.isReadable) continue;
                imp.isReadable = true;
                imp.SaveAndReimport();
                changed++;
                Debug.Log($"[파편] Read/Write 켬: {meshPath} ({prefab.name})");
            }
        }
        return changed;
    }

    /// 디졸브 바탕 머티리얼. 셰이더가 아직 임포트 전이면(처음 뜰 때) 다음 로드에 만든다.
    static bool EnsureDissolveMaterial(bool verbose)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(DissolveMatPath) != null) return false;
        var sh = Shader.Find(DissolveShader);
        if (sh == null)
        {
            if (verbose) Debug.LogWarning($"[파편] 셰이더 {DissolveShader} 를 못 찾았다 — Art/VFX/Shaders/GibDissolve.shader 가 임포트됐는지 볼 것");
            return false;
        }
        var m = new Material(sh) { name = "M_GibDissolve" };
        m.renderQueue = 2450;   // 좀비 몸(툰, AlphaTest)과 같은 자리 — 밤 어둠 막(2900) 앞
        AssetDatabase.CreateAsset(m, DissolveMatPath);
        AssetDatabase.SaveAssets();
        return true;
    }

    /// 착지 피 바탕 머티리얼 — 템플릿의 바닥 피 얼룩 M_Fx_Blood 복제본(소프트 파티클 끔). 원본이 아직 없으면(템플릿 미생성) 다음 로드에.
    static bool EnsureBloodMaterial(bool verbose)
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(BloodMatPath) != null) return false;
        var src = AssetDatabase.LoadAssetAtPath<Material>(BloodSrcPath);
        if (src == null)
        {
            if (verbose) Debug.LogWarning($"[파편] {BloodSrcPath} 가 없다 — FX 템플릿(FxTemplatePrefabs)이 먼저 구워져야 한다. 포커스 한 번 더");
            return false;
        }
        var m = new Material(src) { name = "M_GibBlood" };
        if (m.HasProperty("_SoftParticlesEnabled")) m.SetFloat("_SoftParticlesEnabled", 0f);
        m.DisableKeyword("_SOFTPARTICLES_ON");
        AssetDatabase.CreateAsset(m, BloodMatPath);
        AssetDatabase.SaveAssets();
        return true;
    }
}
