// 좀비 오버런 (TANGTANG) — 프롭 충돌 발자국 굽기 (2026-10-13).
//
// 폰에서 "옆으로 이동이 안 된다"의 원인: 정적 배칭이 프롭 메시를 Combined Mesh 로 합친 뒤 ProtoObstacles 가
// 그 바운드로 충돌을 재서 도시 구역만 한 박스가 생겼다. 폰 빌드는 빌드할 때 합쳐 두므로 시작부터 그렇다.
//
// IProcessSceneWithReport 는 빌드 때 씬마다, 에디터에서는 플레이로 씬을 열 때 불린다 — 둘 다 **정적 배칭보다 먼저**.
// 여기서 _Props/<종류>/<인스턴스> 마다 발자국을 재서 ProtoFootprint 로 붙여 두면, 런타임은 메시를 보지 않는다.
// 씬 파일에는 저장되지 않는다(처리 단계에서만 붙는다) — 프롭을 옮겨도 다시 구울 필요가 없다.
using Proto;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class FootprintBaker : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report) => Bake(scene, report != null ? "빌드" : "플레이");

    /// 에디터에서 처음 연 씬은 플레이를 눌러도 OnProcessScene 이 안 불린다(2026-10-13 확인).
    /// 그래서 씬 파일에도 구워 저장해 둔다 — 빌드 때는 위 콜백이 같은 값을 다시 덮어쓴다.
    /// 프롭을 옮기거나 새로 깔면(도시 재생성·승합차 뿌리기) 다시 돌릴 것.
    [UnityEditor.MenuItem("Tools/좀비 오버런/프롭 충돌 발자국 굽기 (열린 씬)")]
    public static void BakeOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        Bake(scene, "메뉴");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    static void Bake(Scene scene, string why)
    {
        int n = 0;
        // _Props 는 최상위가 아닐 수 있다(도시 씬은 Field_Chapter01/_Props) — 깊이와 상관없이 찾는다
        var propsRoots = new System.Collections.Generic.List<Transform>();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == "_Props") propsRoots.Add(t);
        foreach (var root in propsRoots)
        {
            foreach (Transform group in root)
                foreach (Transform inst in group)
                {
                    if (!ProtoObstacles.MeasureFootprint(inst, out var c, out var h, out var yaw, out var height)) continue;
                    var fp = inst.GetComponent<ProtoFootprint>();
                    if (fp == null) fp = inst.gameObject.AddComponent<ProtoFootprint>();
                    fp.center = c; fp.half = h; fp.yaw = yaw; fp.height = height;
                    n++;
                }
        }
        if (n > 0) Debug.Log($"[FootprintBaker] {scene.name}: 프롭 발자국 {n}개를 배칭 전에 구웠다 ({why})");
    }
}
