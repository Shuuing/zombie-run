using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 도시 풀 두 벌 — 맵 두 벌 전환(<see cref="CityMapVersions"/>, 메뉴 Tools/좀비 오버런/맵 버전/)의 풀 부분. 이 클래스엔 메뉴가 없다.
/// 2026-11-13 사용자: "풀 교체 작업을 정리 좀 해줘. 지금 최종본과 내가 젤 처음 수정하기 전 버전 두개만 on/off 할수 있게 정리해줘. 그리고 최적화도 해줘"
/// → 같은 날 "내가 말한 처음 버전은 나무랑 꽃이랑 등등, 맵 전체지역에 있는 걸 다 말하는거야" 로 맵 전체 전환으로 묶었다.
///
/// <para><b>최종본(마지막)</b> — 팩 풀 패치(<see cref="ToonFantasyCityFlora.BakeFinal"/>, 7판): _GrassPatches_TFF 켬 · 구운 풀잎 끔 ·
/// 도시 풀 카드(_Foliage)는 공원 블록만(윗줄 카드 반 크기 복사본, <see cref="CityFoliageFlowers.KeepInFinal"/>) · 풀 조각 튀기 지도는 잔디만.</para>
/// <para><b>처음 버전</b> — 11월 전(사용자: "처음버전은 11월 전 버전" · "풀 모델링 수정을 했는데 그 전으로"): 구운 풀잎(CityGrassBaker) 켬 ·
/// 도시 풀 카드(CityBuilder, 10-29) 전부 켬 · 원래 크기 · 원래 풀 지도 · 풀 패치 끔 · <b>풀 카드·잔디 바닥 재질도 원본 BCM</b>(11-12 셰이더 교체 전) ·
/// 풀 조각 튀기도 11월 전 값(ProtoGrassKick 이 풀 패치가 꺼져 있으면 예전 값). 풀 에셋(풀잎 셰이더·머티리얼, 풀 카드 메시)은 11월에 바뀐 적이 없다 —
/// 11-10 씬 백업(가장 오래된 것)과 견줘 11월에 바뀐 풀 쪽은 재질(톤 복사본)뿐이었다. 풀잎 메시는 11-13 20:35 사용자가 [테스트 잔디 블록] 을 다시 만들며 같은 굽기로 다시 구웠다.</para>
///
/// <para><b>안 쓰는 쪽 메시는 MeshFilter 에서 뺀다</b> — 렌더러가 꺼져 있어도 메시를 물고 있으면 빌드에 들어가 메모리에 올라간다
/// (11-13 재 보니 최종본 씬이 예전 풀잎 CityGrass.asset 정점 91만 · 정점 버퍼 40MB 를 쓰지도 않으면서 물고 있었다).
/// 다시 고르면 에셋에서 <b>오브젝트 이름 = 메시 이름</b>으로 찾아 끼운다 — 기록 파일이 없고 몇 번을 오가도 같다.
/// 풀잎을 다시 굽거나(CityGrassBaker) 도시를 다시 만들어도(CityBuilder) 이름 규칙이 같아 그대로 된다. 풀 패치가 없으면 마지막으로 갈 때 굽기를 묻는다.</para>
///
/// <para>풀이 눕는 효과는 처음 버전에서만 돈다(ProtoGrassBlades 가 그려지는 풀잎이 있을 때만 켠다).</para>
/// </summary>
public static class CityGrassVersions
{
    internal const string BladeMeshAsset = "Assets/_Game/Art/City/CityGrass.asset";        // CityGrassBaker — 풀잎 블록 메시(이름 = 블록 오브젝트 이름)
    internal const string OriginalMaskPath = "Assets/_Game/Art/City/CityGrassMask.asset";  // CityGrassBaker — 원래 풀 지도(길가 잡초 칸 포함)
    const string PatchRoot = "_GrassPatches_TFF", BladeRoot = "_GrassBlades", FoliageRoot = "_Foliage";

    public enum Version { Unknown, Final, Original }

    /// 지금 씬의 풀이 어느 쪽인가 — 풀 패치가 켜져 있으면 최종본, 아니면(풀잎 루트가 있으면) 처음 버전.
    public static Version Current()
    {
        var city = GameObject.Find("_City");
        if (city == null) return Version.Unknown;
        var p = city.transform.Find(PatchRoot);
        if (p != null && p.gameObject.activeSelf) return Version.Final;
        return city.transform.Find(BladeRoot) != null ? Version.Original : Version.Unknown;
    }

    /// 최종본 풀 패치가 구워져 있는가(루트와 메시 에셋).
    internal static bool HasFinal(GameObject city) =>
        city != null && city.transform.Find(PatchRoot) != null && AssetDatabase.LoadMainAssetAtPath(ToonFantasyCityFlora.GrassMeshAsset) != null;

    /// 최종본 풀 패치를 굽고 최종본으로 맞춘다(씬 저장·Undo 묶기는 부르는 쪽이).
    internal static bool Bake(GameObject city, out string report)
    {
        report = null;
        if (!ToonFantasyCityFlora.BakeFinal(city, out string bake)) return false;
        report = $"풀 패치 구움 — {bake} · " + Apply(city, Version.Final);
        return true;
    }

    /// 씬을 v 쪽으로 맞춘다(이미 그 상태인 것은 건드리지 않는다). 저장은 부르는 쪽이.
    internal static string Apply(GameObject city, Version v)
    {
        bool fin = v == Version.Final;
        var lines = new List<string>();

        // ① 풀 패치 — 최종본만. 처음 버전에선 끄고 메시를 뺀다(빌드·메모리에 안 실리게)
        var patches = city.transform.Find(PatchRoot);
        if (patches != null)
        {
            var have = fin ? Meshes(ToonFantasyCityFlora.GrassMeshAsset) : null;
            int n = 0, missing = 0;
            foreach (var mf in patches.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh want = null;
                if (fin && !have.TryGetValue(mf.gameObject.name, out want)) missing++;
                if (mf.sharedMesh != want) { Undo.RecordObject(mf, "풀 전환"); mf.sharedMesh = want; }
                n++;
            }
            if (patches.gameObject.activeSelf != fin) { Undo.RecordObject(patches.gameObject, "풀 전환"); patches.gameObject.SetActive(fin); }
            lines.Add($"풀 패치 블록 {n}개 {(fin ? "켬" : "끔 · 메시 뺌")}" + (missing > 0 ? $"(메시 못 찾음 {missing} — [최종본 다시 굽기])" : ""));
        }
        else lines.Add("풀 패치 없음");

        // ② 구운 풀잎 — 처음 버전만. 최종본에선 렌더러를 끄고 메시를 뺀다. 풀 조각 튀기 지도도 바꾼다.
        var blades = city.transform.Find(BladeRoot);
        if (blades != null)
        {
            var have = fin ? null : Meshes(CityGrassVersions.BladeMeshAsset);
            int n = 0, missing = 0;
            foreach (var mr in blades.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                Mesh want = null;
                if (!fin && !have.TryGetValue(mr.gameObject.name, out want)) missing++;
                if (mf != null && mf.sharedMesh != want) { Undo.RecordObject(mf, "풀 전환"); mf.sharedMesh = want; }
                bool on = !fin && want != null;
                if (mr.enabled != on) { Undo.RecordObject(mr, "풀 전환"); mr.enabled = on; }
                n++;
            }
            string maskPath = fin ? ToonFantasyCityFlora.LawnMaskAsset : OriginalMaskPath;
            var mask = AssetDatabase.LoadAssetAtPath<Proto.ProtoGrassMask>(maskPath);
            var pgb = blades.GetComponent<Proto.ProtoGrassBlades>();
            if (pgb != null && mask != null && pgb.mask != mask) { Undo.RecordObject(pgb, "풀 전환"); pgb.mask = mask; }
            lines.Add($"구운 풀잎 블록 {n}개 {(fin ? "끔 · 메시 뺌" : "켬")}" + (missing > 0 ? $"(메시 못 찾음 {missing} — CityGrassBaker 로 다시 굽기)" : "") +
                      $" · 풀 조각 지도 {System.IO.Path.GetFileNameWithoutExtension(maskPath)}" + (mask == null ? "(없음!)" : ""));
        }
        else lines.Add("구운 풀잎 없음");

        // ③ 도시 풀 카드 — 처음 버전: 전부 · 원래 크기 / 최종본: 공원 블록만 · 윗줄 반 크기
        var fol = city.transform.Find(FoliageRoot);
        if (fol != null)
        {
            var orig = Meshes(CityFoliageFlowers.OriginalAsset);
            var copies = Meshes(CityFoliageFlowers.CopyAsset);
            int copiesBefore = copies.Count;
            ToonFantasyCityFlora.LawnGrid lawn = null;
            if (fin) { var map = city.GetComponent<Proto.CityMap>(); lawn = ToonFantasyCityFlora.BuildLawn(city.transform, map != null ? map.halfSize : 86f); }
            var on = new List<string>(); int off = 0; var skipped = new List<string>();
            foreach (Transform t in fol)
            {
                var mf = t.GetComponent<MeshFilter>();
                if (mf == null) continue;
                if (!orig.TryGetValue(CityFoliageFlowers.OriginalName(t.gameObject), out var om) && mf.sharedMesh != null)
                {
                    string nm = mf.sharedMesh.name;   // 이름 규칙이 안 맞으면 지금 끼운 메시 이름에서
                    if (nm.EndsWith(CityFoliageFlowers.Suffix)) nm = nm.Substring(0, nm.Length - CityFoliageFlowers.Suffix.Length);
                    orig.TryGetValue(nm, out om);
                }
                if (om == null) { skipped.Add(t.name); continue; }
                var want = fin ? CityFoliageFlowers.SmallCopy(om, copies) : om;
                bool active = !fin || CityFoliageFlowers.KeepInFinal(om, t.localToWorldMatrix, lawn, out _);
                if (mf.sharedMesh != want) { Undo.RecordObject(mf, "풀 전환"); mf.sharedMesh = want; }
                if (t.gameObject.activeSelf != active) { Undo.RecordObject(t.gameObject, "풀 전환"); t.gameObject.SetActive(active); }
                if (active) on.Add(t.name); else off++;
            }
            if (copies.Count != copiesBefore) AssetDatabase.SaveAssets();
            lines.Add($"도시 풀 카드 {(fin ? "윗줄 반 크기" : "원래 크기")} 켬 {on.Count}개" + (fin ? $"({string.Join(", ", on)}) · 끔 {off}개" : "") +
                      (skipped.Count > 0 ? $" · 원본 메시 못 찾아 그대로: {string.Join(", ", skipped)}" : ""));
        }

        // ④ 풀 재질 — 도시 풀 카드 · 잔디 바닥(공원 Ground 잔디 칸 · 넓은 잔디 _TestLawn 잔디 칸). 처음 버전 = 11월 전 원본 BCM,
        //    최종본 = 씬의 다른 바닥이 톤 복사본이면 톤 복사본(셰이더 교체 상태를 따른다). 풀잎(Proto/Grass)은 셰이더 교체 대상이 아니라 그대로다.
        int slots = 0;
        void Slot(Renderer r, int i)
        {
            if (r == null) return;
            var ms = r.sharedMaterials;
            if (i < 0 || i >= ms.Length || ms[i] == null) return;
            var want = fin ? FinalMaterial(city, ms[i]) : ToonFantasyCitySwap.OriginalOf(ms[i]);
            if (want == null || want == ms[i]) return;
            Undo.RecordObject(r, "풀 전환");
            ms[i] = want; r.sharedMaterials = ms; slots++;
        }
        if (fol != null)
            foreach (var r in fol.GetComponentsInChildren<MeshRenderer>(true))
                for (int i = 0; i < r.sharedMaterials.Length; i++) Slot(r, i);
        Slot(city.transform.Find("Ground")?.GetComponent<MeshRenderer>(), ToonFantasyCityFlora.LawnSubmesh);
        Slot(city.transform.Find(CityTestLawn.LawnName)?.GetComponent<MeshRenderer>(), 0);
        lines.Add($"풀 카드·잔디 바닥 재질 {(fin ? (ToonFantasyCitySwap.SceneIsToon(city) ? "톤 복사본" : "원본 BCM") : "원본 BCM(11월 전)")} — 바꾼 칸 {slots}개");
        return string.Join(" · ", lines);
    }

    /// 최종본에서 풀 재질 칸에 쓸 머티리얼 — 씬의 다른 바닥(보도·연석·차도)이 톤 복사본이면 톤 복사본, 아니면 원본.
    internal static Material FinalMaterial(GameObject city, Material m)
    {
        if (m == null) return null;
        var orig = ToonFantasyCitySwap.OriginalOf(m);
        return ToonFantasyCitySwap.SceneIsToon(city) ? ToonFantasyCitySwap.ToonCopyOf(orig) ?? m : orig;
    }

    static Dictionary<string, Mesh> Meshes(string path)
    {
        var d = new Dictionary<string, Mesh>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Mesh m && !d.ContainsKey(m.name)) d[m.name] = m;
        return d;
    }
}
