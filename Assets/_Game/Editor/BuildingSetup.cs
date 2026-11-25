// 건물 모델 셋업 — ArtSource/Buildings 에서 뽑아 온 FBX 에 BCM 툰 머티리얼을 물리고 스트리머에 연결한다.
//
// 원본 파이프라인: 힉스필드 GPT Image 2 컨셉 → Tripo 3D(face_limit) → ArtSource/Buildings/prep_building.py
// (2,000 tri 상한 · 피벗 바닥 중앙 · 실제 크기 · 베이스컬러 PNG 분리). 이 메뉴는 그 결과물만 다룬다.
//
// **머티리얼은 importer 리맵으로 물린다.** materialLocation 을 External 로 바꾸면 리임포트 때
// 머티리얼을 새로 뽑아 이름이 바뀌고 리맵이 조용히 풀린다(무기 모델에서 실제로 밟았다).
using System.Collections.Generic;
using System.IO;
using Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildingSetup
{
    public const string Folder = "Assets/_Game/Art/Field/Props/Buildings";
    const string TemplateMat = "Assets/_Game/Art/Field/Materials/M_Van_Abandoned.mat";   // 프롭과 같은 툰 톤
    const int MaxTexture = 1024;   // Tripo 텍스처는 4096² 19MB — 17.76m 카메라에서 1024 로 충분하다

    [MenuItem("Tools/좀비 오버런/건물 모델 셋업")]
    public static void Setup()
    {
        var prefabs = Configure();
        var fs = Object.FindFirstObjectByType<ProtoFieldStreamer>();
        if (fs != null)
        {
            Undo.RecordObject(fs, "건물 모델 연결");
            fs.buildingPrefabs = prefabs;
            EditorUtility.SetDirty(fs);
            EditorSceneManager.MarkSceneDirty(fs.gameObject.scene);
        }
        Debug.Log($"[건물] {prefabs.Length}종 셋업" + (fs != null ? $", {fs.name} 에 연결" : " (열린 씬에 스트리머 없음 — 연결 생략)"));
    }

    /// 폴더의 Building_*.fbx 전부: 텍스처 임포트 → 머티리얼 생성/갱신 → FBX 리맵. 로드된 모델을 돌려준다.
    public static GameObject[] Configure()
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>(TemplateMat);
        var result = new List<GameObject>();
        if (!AssetDatabase.IsValidFolder(Folder)) return result.ToArray();

        foreach (var guid in AssetDatabase.FindAssets("Building_ t:Model", new[] { Folder }))
        {
            string fbx = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(fbx);
            string texPath = $"{Folder}/{name}_BaseMap.png";

            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti != null && ti.maxTextureSize != MaxTexture)
            {
                ti.maxTextureSize = MaxTexture;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = $"{Folder}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = template != null ? new Material(template) : new Material(Shader.Find("BCM/BCM_Toony_shader"));
                mat.name = "M_" + name;
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", tex);
            mat.SetTextureScale("_BaseMap", Vector2.one);
            mat.SetTextureOffset("_BaseMap", Vector2.zero);
            // 건물은 카메라와 캐릭터 사이에 자주 온다 — 가린 부분만 디더로 비친다(ProtoSeeThrough 가 수치를 정한다).
            mat.SetFloat("_UseSeeThrough", 1f);
            mat.EnableKeyword("BCM_SEE_THROUGH");
            EditorUtility.SetDirty(mat);

            var mi = (ModelImporter)AssetImporter.GetAtPath(fbx);
            bool dirty = false;
            if (mi.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            { mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard; dirty = true; }
            if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
            // 축 변환은 prep_building.py 가 FBX 에 이미 구워 둔다(bake_space_transform) — 여기서 또 굽지 않는다.
            // (임포터 bakeAxisConversion 을 켜 봤더니 루트 회전이 -90° 에서 +90° 로 뒤집혔을 뿐이었다.)
            if (mi.bakeAxisConversion) { mi.bakeAxisConversion = false; dirty = true; }
            // Read/Write 켬 — ProtoObstacles 가 꼭짓점으로 딱 맞는 충돌 발자국을 잰다(빌드에서는 이게 없으면 못 읽는다).
            if (!mi.isReadable) { mi.isReadable = true; dirty = true; }
            // FBX 에 들어 있는 머티리얼 이름마다 우리 머티리얼로 리맵
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                if (o is Material src)
                {
                    mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                    dirty = true;
                }
            if (dirty) mi.SaveAndReimport();

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (go == null) continue;
            // 스트리머가 회전을 yaw 로 덮어쓰므로 루트에 회전이 남아 있으면 건물이 눕는다. 조용히 틀리는 지점이라 경고한다.
            if (Quaternion.Angle(go.transform.localRotation, Quaternion.identity) > 0.5f)
                Debug.LogWarning($"[건물] {name} 루트 회전 {go.transform.localEulerAngles} — prep_building.py 로 다시 뽑을 것(bake_space_transform).");
            result.Add(go);
        }
        AssetDatabase.SaveAssets();
        return result.ToArray();
    }
}
