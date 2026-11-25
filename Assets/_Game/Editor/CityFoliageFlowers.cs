using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 도시 풀 카드(_City/_Foliage — CityBuilder.BuildFoliage) 도우미. 메뉴는 없다 — 풀 두 벌 전환(<see cref="CityGrassVersions"/>)이 쓴다.
///
/// <para><b>윗줄 카드 반으로</b>(2026-11-12 사용자: "_Foliage 오브젝트에 들어있는 꽃의 사이즈를 좀 반으로" → "이 풀(넓은잎)도 반사이즈로"):
/// 카드 하나 = V 자 두 장(정점 8), 아틀라스 칸으로 종류가 갈린다 — T_Foliage_Atlas 윗줄(UV v 0.5~1) 넓은잎(u 0~0.5) · 들국화(u 0.5~1),
/// 아랫줄 마른 풀더미 · 귀리. 공원은 아랫줄 칸을 윗줄로 바꾸면서 크기는 원래 종류 것을 써 1.3~1.7m 로 솟았다.
/// 원본 메시(CityMeshes.asset)는 그대로 두고 윗줄 판만 밑동 기준 ×<see cref="Scale"/> 한 복사본을 <c>CityFoliage_SmallFlowers.asset</c> 에 둔다
/// (이름 = 원본 이름 + "_SmallFlowers"). 최종본은 복사본, 처음 버전은 원본을 끼운다.</para>
///
/// <para><b>최종본에서 켜 둘 블록</b>(<see cref="KeepInFinal"/>): 판 밑동이 잔디 위인 비율이 절반 이상이고, 넓은 잔디(테스트 잔디) 위는 절반 미만인 블록.
/// 11-12 "가운데 잔디 4칸이랑 오른쪽 큰 잔디 외에 풀들 지워줘" → 보도·가장자리 잡초 블록을 끄고, 11-13 넓은 잔디 블록(2_1 — 건물 블록 시절
/// 가장자리 잡초 줄이라 연석 위로 솟았다)도 끈다. 넓은 잔디의 꽃·여러 풀은 풀 패치 굽기(ToonFantasyCityFlora 7판)가 안쪽에 새로 심는다.
/// 도시 씬에선 공원(1_1)만 남는다.</para>
/// CityBuilder 로 도시를 다시 만들면 거기서부터 윗줄 카드가 줄인 크기로 나온다(FoliageMB) — 그때는 [최종본 다시 굽기].
/// </summary>
public static class CityFoliageFlowers
{
    public const float Scale = 0.5f;
    internal const string CopyAsset = "Assets/_Game/Art/City/CityFoliage_SmallFlowers.asset";
    internal const string OriginalAsset = "Assets/_Game/Art/City/CityMeshes.asset";
    internal const string Suffix = "_SmallFlowers";
    const float LawnShare = 0.5f;

    /// UV 가 아틀라스 윗줄(넓은잎 · 들국화)인가 — CityBuilder.FoliageMB 의 칸 규칙과 같다.
    public static bool Shrinks(Vector2 cell) => cell.y > 0.25f;

    /// _Foliage 블록 오브젝트 이름(Foliage_x_y) → 원본 메시 이름(CityFoliage_x_y).
    internal static string OriginalName(GameObject block) => "City" + block.name;

    /// 원본 메시의 반으로 줄인 복사본 — 있으면 그대로, 없으면 만든다(원본이 읽을 수 있어야 한다).
    internal static Mesh SmallCopy(Mesh original, Dictionary<string, Mesh> copies)
    {
        if (original == null) return null;
        if (copies.TryGetValue(original.name + Suffix, out var have)) return have;
        if (!original.isReadable) { Debug.LogWarning($"[도시 풀 카드] {original.name} 를 읽을 수 없어 줄인 복사본을 못 만든다 — 원본을 쓴다."); return original; }
        if (AssetDatabase.LoadMainAssetAtPath(CopyAsset) == null)
            // 대표(메인) 오브젝트는 빈 메시로 따로 둔다 — 복사본을 메인으로 넣으면 이름이 파일 이름으로 바뀐다
            AssetDatabase.CreateAsset(new Mesh { name = Path.GetFileNameWithoutExtension(CopyAsset) }, CopyAsset);
        var copy = Object.Instantiate(original);
        copy.name = original.name + Suffix;
        copy.SetVertices(ShrunkVertices(original, out _, out _));
        copy.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(copy, CopyAsset);
        copies[copy.name] = copy;
        return copy;
    }

    /// 최종본에서 이 블록을 켜 둘까 — 판 밑동(정점 0·3 가운데)을 잔디 지도에 찍어 본다.
    internal static bool KeepInFinal(Mesh mesh, Matrix4x4 l2w, ToonFantasyCityFlora.LawnGrid lawn, out string why)
    {
        why = "메시 없음";
        if (mesh == null) return false;
        var v = mesh.vertices;
        int quads = 0, onLawn = 0, onTest = 0;
        for (int q = 0; q + 3 < v.Length; q += 4)
        {
            var b = l2w.MultiplyPoint3x4((v[q] + v[q + 3]) * 0.5f);
            int k = lawn.Index(b.x, b.z);
            quads++;
            if (k >= 0 && lawn.lawn[k]) { onLawn++; if (lawn.src[k] == ToonFantasyCityFlora.SrcTest) onTest++; }
        }
        why = $"잔디 위 {onLawn}/{quads}" + (onTest > 0 ? $" · 넓은 잔디 {onTest}" : "");
        return quads > 0 && onLawn >= quads * LawnShare && onTest < quads * LawnShare;
    }

    /// 판(정점 4개 — 밑 왼쪽 · 위 왼쪽 · 위 오른쪽 · 밑 오른쪽, CityBuilder.FoliageMB 순서) 중 윗줄 칸만 밑동 가운데 기준으로 줄인 정점.
    public static List<Vector3> ShrunkVertices(Mesh src, out int leafQuads, out int flowerQuads)
    {
        var v = new List<Vector3>(); src.GetVertices(v);
        var uv = new List<Vector2>(); src.GetUVs(0, uv);
        leafQuads = flowerQuads = 0;
        for (int q = 0; q + 3 < v.Count && q + 3 < uv.Count; q += 4)
        {
            Vector2 a = uv[q], b = uv[q + 1], c = uv[q + 2], d = uv[q + 3];
            // 밑변(0·3)은 칸 아래, 윗변(1·2)은 칸 위 — 이 모양이 아니면 판이 아니다
            bool card = Mathf.Abs(a.y - d.y) < 1e-4f && Mathf.Abs(b.y - c.y) < 1e-4f && b.y > a.y;
            var cell = new Vector2(Mathf.Min(a.x, b.x, c.x, d.x), Mathf.Min(a.y, b.y, c.y, d.y));
            if (!card || !Shrinks(cell)) continue;
            var basePos = (v[q] + v[q + 3]) * 0.5f;
            for (int k = 0; k < 4; k++) v[q + k] = basePos + (v[q + k] - basePos) * Scale;
            if (cell.x > 0.25f) flowerQuads++; else leafQuads++;
        }
        return v;
    }
}
