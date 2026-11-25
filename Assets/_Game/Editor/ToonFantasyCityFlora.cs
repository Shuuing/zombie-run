using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Proto_City_Test 의 나무·덤불·꽃과 풀을 Toon Fantasy Nature 데모(Demo_Scene_Field)에 쓰인 에셋으로 바꾸고, 원래대로 되돌린다.
/// 2026-11-12 사용자: "풀이랑 나무 같은것도 데모에 있는걸로" — 풀은 "팩 풀 패치로 교체", 나무는 "데모 Field 구성".
///
/// <para><b>나무·덤불·꽃</b> — 손으로 놓은 잎 카드 FBX 인스턴스(Tree_Round/Cone/Spruce_Test · Bush_A/B_Test · Plant_A/B_Test)마다
/// 같은 부모·같은 자리에 팩 프리팹을 놓고 원래 것은 끈다(지우지 않는다). 데모 Field 에 쓰인 것만 쓴다:
/// 둥근 → Oak_01A · 원뿔 → Birch_01A/02A · 침엽 → Aspen_01A/02A/03A · 덤불 → Bush_01A/02A/03A/03C · 화초 → Flower_Patch_02A/01E/01C.
/// 크기는 <b>원래 나무의 키에 맞춘다</b> — 팩 나무는 7~20m 라 그대로면 화면(가로 ~22m)을 덮는다.
/// 팩 머티리얼(TFF_Atlas_1A_D · TFF_Atlas_Vegetation_1A_D)은 도시용 셰이더 복사본(<c>FloraMaterials/</c>)으로 바꿔 끼운다 —
/// 투시(ProtoSeeThrough)·충돌(ProtoObstacles)이 BCM_SEE_THROUGH · _ALPHATEST_ON 키워드로 가림막·잎 카드를 알아보기 때문.
/// 꽃 패치는 팩 풀 셰이더 그대로(작아서 가리지도 막지도 않는다). 팩 프리팹의 콜라이더는 끈다(게임은 물리를 안 쓴다).</para>
///
/// <para><b>풀</b> — 도시 잔디 면(공원 · 넓은 잔디)에 팩 Grass_Patch_01A/B/C · 꽃 패치 · 포인트 풀 카드를 심어 8m 블록 메시로 굽는다
/// (<c>_City/_GrassPatches_TFF</c>, 메시 <c>TFF_CityGrassPatches.asset</c>) — <see cref="BakeFinal"/>. 굽기 규칙은 아래 상수의 1~7판 주석.
/// 이것이 풀 <b>최종본</b>이고, 11-12 풀 작업 전의 <b>처음 버전</b>(구운 풀잎 + 도시 풀 카드 원래 크기)과의 전환은 <see cref="CityGrassVersions"/>
/// (메뉴 Tools/좀비 오버런/풀/). 밟거나 폭발로 풀이 눕는 효과(Proto/Grass 셰이더)는 팩 풀 셰이더에 없어 최종본에선 사라진다(사용자 선택).
/// 데모처럼 패치 3만 개를 오브젝트로 깔지 않는다 — 모바일에서 오브젝트 수가 감당이 안 된다.</para>
///
/// <para>메뉴는 없다 — 맵 두 벌 전환(<see cref="CityMapVersions"/>, Tools/좀비 오버런/맵 버전/)이 <see cref="PlantsToFirst"/> · <see cref="PlantsToLast"/> ·
/// 풀(<see cref="CityGrassVersions"/>)을 부른다. 나무·덤불·꽃은 놓은 팩 프리팹과 원래 잎 카드를 <b>둘 다 남기고 켜고 끈다</b>(11-13 전에는 [원래대로] 가 지웠다).
/// 짝은 <c>Flora_Original.asset</c>(<see cref="ToonFantasyFloraSnapshot"/>)에 적혀 있고 지우지 않는다.</para>
/// </summary>
public static class ToonFantasyCityFlora
{
    const string Root = ToonFantasyCitySwap.Root;
    const string FloraSnap = Root + "/Flora_Original.asset";
    const string FloraMatDir = Root + "/FloraMaterials";
    internal static string GrassMeshAsset => ToonFantasyAssets.GrassMeshPath;   // Grass/ (옮기기 전이면 루트)
    internal static string LawnMaskAsset => ToonFantasyAssets.LawnMaskPath;
    // 팩 프리팹·머티리얼은 이름으로 찾는다(ToonFantasyAssets.Find) — 팩 원래 자리든 ToonFantasyCity 주제 폴더로 옮긴 뒤든
    const string Tag = " [TFF]";

    // ── 잎 카드 FBX 이름 → 데모 Field 에 쓰인 팩 프리팹 ──
    static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
    {
        { "Tree_Round_Test",  new[] { "Trees/TFF_Oak_Tree_01A" } },
        { "Tree_Cone_Test",   new[] { "Trees/TFF_Birch_Tree_01A", "Trees/TFF_Birch_Tree_02A" } },
        { "Tree_Spruce_Test", new[] { "Trees/TFF_Aspen_Tree_01A", "Trees/TFF_Aspen_Tree_02A", "Trees/TFF_Aspen_Tree_03A" } },
        { "Bush_A_Test",      new[] { "Plants/TFF_Bush_01A", "Plants/TFF_Bush_02A", "Plants/TFF_Bush_03A", "Plants/TFF_Bush_03C" } },
        { "Bush_B_Test",      new[] { "Plants/TFF_Bush_01A", "Plants/TFF_Bush_02A", "Plants/TFF_Bush_03A", "Plants/TFF_Bush_03C" } },
        { "Plant_A_Test",     new[] { "Plants/TFF_Flower_Patch_02A", "Plants/TFF_Flower_Patch_02A", "Plants/TFF_Flower_Patch_01E", "Plants/TFF_Flower_Patch_01C" } },
        { "Plant_B_Test",     new[] { "Plants/TFF_Flower_Patch_02A", "Plants/TFF_Flower_Patch_02A", "Plants/TFF_Flower_Patch_01E", "Plants/TFF_Flower_Patch_01C" } },
    };

    // ── 풀 ── 데모 Field 의 비율(01A 25912 · 01B 5204 · 01C 2235)
    static readonly string[] GrassPrefabs = { "Plants/TFF_Grass_Patch_01A", "Plants/TFF_Grass_Patch_01B", "Plants/TFF_Grass_Patch_01C" };
    static readonly float[] GrassWeights = { 0.78f, 0.16f, 0.06f };
    const int GrassLod = 1;                 // LOD1 = 정점 80. 게임 카메라(18m)에서 데모 LOD 는 2~3(36·16)까지 내려가니 이걸로 충분하다
    // 2판(2026-11-12, 사용자 스크린샷과 함께: "나무랑 풀 심겨져있는 영역안에 잔디 가득 심어주고 보도블럭 밖으로는 안나가게 해줘"):
    //  1판은 풀 지도(0.5m 칸)에 1/m² 로 심어 듬성듬성했고, 1.9m 폭 패치가 칸 경계를 넘어 연석·보도로 넘쳤으며, 길가 잡초 패치가 보도에 솟았다.
    //  → 자리는 도시 바닥 메시의 잔디 윗면을 0.1m 칸으로 직접 읽고(공원 잔디 = Ground 서브메시 3, 테스트 잔디 = _TestLawn 서브메시 0),
    //    길가 잡초는 심지 않고, 잔디 가장자리(LawnInset 안쪽)를 넘는 정점은 패치 중심 쪽으로 당겨 가장자리에서 멈춘다. 밀도 5/m², 키 0.7배.
    //    (같은 날 2판은 넘는 삼각형을 통째로 버려 가장자리 0.5~1m 띠가 비었다 — 사용자 "바깥 네모 영역까지 심어줘" → 3판에서 당기기로)
    const float LawnCell = 0.1f;            // 잔디 지도 칸(m)
    const float LawnInset = 0.1f;           // 가장자리에서 이만큼 안쪽까지만 풀잎 — 연석 안쪽 옆면(높이 0.12)에 기대 넘지 않게
    internal const int LawnSubmesh = 3;              // CityBuilder: Ground 서브메시 3 = 공원 잔디(0.003m, 둘레 연석 0.3m 폭 · 0.12m 높이)
    const float PatchesPerM2 = 5f;          // 패치 한 장 1.9m 폭(3.6m²) — 5/m² 라야 바닥이 거의 안 가린다(1판 1.0 은 듬성듬성, 2.5 도 바닥이 군데군데 보였다)
    static readonly Vector2 LawnScale = new Vector2(0.85f, 1.15f);   // 가로 크기
    const float LawnHeight = 0.7f;          // 키는 가로의 0.7배(원본 키 0.79m → 0.47~0.63m) — 잔디가 덤불처럼 솟지 않게
    // 4판(2026-11-12, 사용자 스크린샷에 화면 아래쪽 잔디를 칠하고: "풀을 front 뷰로 해서 심어줘. 바닥 비어보이는게 이상해"):
    //  카메라에 가까운 쪽(화면 아래)은 내려다보는 각이 커서 사방으로 뻗은 패치 풀잎이 가늘게 보이고 바닥이 비쳤다.
    //  → 카메라를 정면으로 보는 풀 카드(팩 풀 텍스처 한 장 = 패치 판 하나와 같은 그림·바람, 윗변을 카메라 반대쪽으로 25°)를
    //    카메라 쪽 가장자리 띠에 촘촘히, 좌우 가장자리 띠에 덜 촘촘히 더 깐다. 띠 끝은 부드럽게 줄어든다. 카메라는 늘 +Z 를 본다(yaw 0).
    const float FrontBand = 2.5f;           // 카메라 쪽(−Z) 가장자리에서 이만큼 안까지
    const float SideBand = 1.0f;            // 좌우(±X) 가장자리 띠
    const float SideDensity = 0.6f;         // 좌우 띠는 앞쪽 띠의 이만큼
    const float FrontCardsPerM2 = 12f;      // 카드 한 장 ≈ 0.7 × 0.5m. 8 은 맨 앞 가장자리에 바닥 틈이 남았다
    static readonly Vector2 CardWidth = new Vector2(0.55f, 0.85f);
    static readonly Vector2 CardHeight = new Vector2(0.38f, 0.58f);   // 패치 풀잎 키(0.47~0.63 × 기울기)와 비슷하게
    const float CardLean = 25f;             // 윗변을 카메라 반대쪽으로 — CityBuilder 풀 카드와 같은 각
    const float CardYaw = 15f;              // 정면에서 좌우로 흔드는 각(도)
    // 5판(2026-11-12, 사용자: "들국화 좀 더 군데 군데 심어주고, 다른 색상의 꽃들도 있을까? 포인트로 좀 섞어서 심어줘"):
    //  잔디 곳곳에 작은 꽃 무더기(3~7송이, 무더기마다 한 종류)를 더 심는다. 반쯤은 들국화 — 도시 풀 카드(_City/_Foliage)와 같은
    //  아틀라스 칸(T_Foliage_Atlas 윗줄 오른쪽)·같은 머티리얼의 V 자 카드라 기존 들국화와 똑같아 보인다.
    //  나머지는 팩 꽃 패치(01A 빨강 · 01B 하늘 · 01D 자홍, LOD0)가 돌아가며 나눠 포인트가 된다.
    //  팩 꽃은 송이가 작아 풀 키(0.5~0.7m)로 줄이면 게임 카메라에서 점으로 묻혔다(임시 프로젝트 렌더) → 키 0.75~0.95m · 무더기 5~9송이.
    //  02A(연분홍)는 송이가 듬성해 그렇게 키워도 거의 안 보여 뺐다.
    const float FlowerClustersPerM2 = 0.1f;  // 무더기 하나에 ~10m² — 화면(잔디 ~100m²)에 열 군데 남짓
    static readonly Vector2Int ClusterCount = new Vector2Int(3, 7);
    static readonly Vector2Int ColorClusterCount = new Vector2Int(5, 9);   // 색 꽃 무더기 — 들국화(3~7)보다 많이
    static readonly Vector2 ClusterRadius = new Vector2(0.35f, 0.8f);
    static readonly string[] FlowerPrefabs = { "Plants/TFF_Flower_Patch_01A", "Plants/TFF_Flower_Patch_01B", "Plants/TFF_Flower_Patch_01D" };   // 빨강 · 하늘 · 자홍
    const float DaisyShare = 0.5f;          // 무더기 중 들국화 비율
    static readonly Vector2 FlowerHeight = new Vector2(0.75f, 0.95f);   // 팩 꽃 패치 키(m) — 메시 높이로 나눠 크기를 정한다
    static readonly Vector2 DaisyHeight = new Vector2(0.45f, 0.7f);    // 들국화 카드 키 = 가로(정사각 칸) — 반으로 줄인 _Foliage 들국화(0.34~0.84)와 비슷하게
    const int FlowerLod = 0;
    static readonly Vector2 DaisyCell = new Vector2(0.5f, 0.5f);       // CityBuilder.FoliageMB 의 들국화 칸
    const string DaisyMatFallback = "Assets/_Game/Art/City/Materials/M_City_Foliage.mat";
    // 6판(2026-11-13, 넓은 잔디(테스트 잔디) 스크린샷과 함께: "넓은 잔디 쪽에도 잔디 외에 풀은 삭제해주고 경계 라인도 잘 맞춰서 풀 심어줘. 외곽 라인이 안맞아"):
    //  ① 팩 풀 셰이더(TFF_CustomGrass) 바람 = 노이즈^2.5 × 노이즈 × 정점색 — 둘 다 0 이상이라 풀 끝이 늘 오브젝트 공간 +X·+Y·+Z 로만 밀린다
    //     (TFF_Noise_2 로 재면 평균 0.083m · 90% 0.18m · 99% 0.35m). 데모는 패치마다 돌려 놓아 방향이 섞이지만, 여기선 패치를 월드 좌표 한 메시로
    //     구워 모든 풀이 같은 쪽(화면 오른쪽·위)으로 쏠렸다 → 오른쪽·위 가장자리는 연석·보도로 넘치고 왼쪽·아래는 잔디 바닥이 띠로 드러났다.
    //     → 구울 때 평균 이동(WindMean × 정점색)만큼 X·Z 를 미리 반대로 옮겨 흔들림의 가운데가 제자리가 되게 한다(Y 는 그대로 — 키가 줄어 보이지 않게).
    //  ② 가장자리 EdgeBand 안은 바람(정점색)을 0 쪽으로 줄여 흔들려 넘지 않게 하고, 키를 EdgeHeight 배까지 낮춰 원근으로 기울어 넘는 것도 줄인다.
    //  ③ 테스트 잔디는 연석이 잔디와 같은 높이(0.007 · 잔디 0.006)라 기대 넘을 턱이 없다 → 안쪽 여백 0, 풀이 연석 줄까지 찬다(공원은 0.12m 턱이라 0.1 그대로).
    //  ④ 테스트 잔디는 잔디만 — 꽃 무더기를 심지 않고, 그 블록의 도시 풀 카드(_Foliage — 넓은잎·들국화·마른 풀더미)도 끈다(지금은 CityFoliageFlowers.KeepInFinal — 꽃은 7판에서 다시).
    //  ⑤ 공원·테스트 잔디가 난수를 따로 쓴다 — 한쪽을 고쳐도 다른 쪽 배치가 바뀌지 않게(이 판에서 공원 배치는 한 번 새로 섞인다).
    const float WindMean = 0.083f;          // 바람 평균 이동(m, 정점색 1 기준) — TFF_Noise_2 두 번 샘플(uv · 2uv)을 독립으로 보고 E[n^2.5]·E[n]
    const float EdgeBand = 0.4f;            // 가장자리에서 이 안쪽까지 바람·키를 줄인다
    const float EdgeHeight = 0.6f;          // 가장자리 맨 끝 풀의 키 배율
    const float TestLawnInset = 0f;         // 테스트 잔디 안쪽 여백(③)
    // 7판(2026-11-13, 6판을 심어 보고: "영역 안에 이쁘게 잘 심겨졌는데 그전에 꽃이랑 여러가지 이쁘게 섞여있던 풀들이 없어졌어. 다시 이쁘게 배치해줘"):
    //  6판의 "잔디 외에 풀은 삭제" 는 "잔디 밖(가장자리·연석 위) 풀" 이라는 뜻이었다 — 잔디 안의 꽃·여러 풀은 남겨야 했다.
    //  → 테스트 잔디에도 꽃 무더기를 다시 심고, 꺼 둔 Foliage_2_1(건물 블록 시절 가장자리 잡초 줄이라 연석 위로 솟았다) 대신
    //    같은 _Foliage 아틀라스 카드(마른 풀더미 · 귀리 · 넓은잎)를 잔디 안쪽에 무더기로 새로 심는다. 들판처럼 진하고 옅은 곳은 펄린 노이즈.
    //    둘 다 가장자리 TestEdgeMargin 안에는 안 심어 6판의 깔끔한 외곽선을 지킨다. 공원은 건드리지 않는다(Foliage_1_1 카드 그대로, 난수도 따로).
    const bool TestLawnFlowers = true;      // 테스트 잔디에 꽃 무더기(④ → 7판에서 다시 켬)
    const float TestEdgeMargin = 0.6f;      // 테스트 잔디 꽃·포인트 풀 밑동은 가장자리에서 이만큼 안쪽부터(키 큰 것이 원근으로 연석 위에 걸치지 않게)
    const float AccentClumpsPerM2 = 0.07f;  // 포인트 풀 무더기 — 넓은 잔디 864m² 에 60곳 남짓(예전 Foliage_2_1 카드 182장과 비슷한 양)
    const float AccentNoise = 0.15f;        // 들판 결 노이즈 주파수(1/m) — 6~7m 마다 진하고 옅은 곳
    // 종류: _Foliage 아틀라스 칸 · 키(m) · 무더기 하나에 몇 장 · 고를 비율. 넓은잎은 풀(0.47~0.63m) 위로 고개를 내밀 만큼.
    static readonly Vector2[] AccentCell = { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f) };   // 마른 풀더미 · 귀리 · 넓은잎
    static readonly Vector2[] AccentHeight = { new Vector2(0.7f, 1.0f), new Vector2(0.8f, 1.1f), new Vector2(0.45f, 0.62f) };
    static readonly Vector2Int[] AccentCount = { new Vector2Int(1, 3), new Vector2Int(1, 3), new Vector2Int(2, 5) };
    static readonly float[] AccentWeight = { 0.35f, 0.15f, 0.5f };
    static readonly Vector2 AccentRadius = new Vector2(0.2f, 0.6f);
    internal const byte SrcPark = 1, SrcTest = 2;   // 잔디 지도 칸의 출처 — 공원 잔디(Ground 서브메시 3) · 테스트 잔디(_TestLawn)
    static readonly Color32 White32 = new Color32(255, 255, 255, 255);
    const float Block = 8f;                 // CityGrassBaker 와 같은 블록(화면 밖 블록 컬링)
    const int Seed = 1112;

    // ================================================================================================ 나무·덤불·꽃

    /// 마지막 — 잎 카드 나무·덤불·화초는 끄고 데모 팩 식생을 켠다. 기록(Flora_Original.asset)에 없는 잎 카드(새로 놓았거나 도시를 다시 만든 것)는
    /// 같은 부모·같은 자리에 팩 프리팹을 새로 놓는다. 이미 놓은 것은 켜기만 한다(지웠으면 다시 놓는다). 저장·Undo 묶기는 부르는 쪽(CityMapVersions)이.
    internal static string PlantsToLast(Scene scene)
    {
        var snap = LoadOrCreate(FloraSnap);
        var byOriginal = new Dictionary<string, ToonFantasyFloraSnapshot.Swap>();
        foreach (var s in snap.swaps) byOriginal[s.original] = s;
        var mats = FloraMaterials();
        var packH = new Dictionary<string, float>();
        int on = 0, made = 0;
        var counts = new Dictionary<string, int>();
        foreach (var inst in LeafCardInstances(scene))
        {
            string id = Id(inst);
            GameObject rep = null;
            if (byOriginal.TryGetValue(id, out var sw))
            {
                rep = Resolve<GameObject>(sw.replacement);
                if (rep == null) snap.swaps.Remove(sw);   // 놓은 것이 지워졌다 — 아래에서 다시 놓는다
            }
            if (rep == null)
            {
                rep = PlaceReplacement(inst, mats, packH, counts);
                if (rep == null) continue;
                snap.swaps.Add(new ToonFantasyFloraSnapshot.Swap { name = inst.name, original = id, wasActive = inst.activeSelf, replacement = Id(rep) });
                made++;
            }
            SetActiveRecorded(rep, true, "데모 식생 켬");
            SetActiveRecorded(inst, false, "잎 카드 식생 끔");
            on++;
        }
        SaveSnap(snap, FloraSnap);
        var list = new List<string>(); foreach (var kv in counts) list.Add($"{Path.GetFileName(kv.Key)} {kv.Value}");
        return $"나무·덤불·꽃 데모 팩 {on}개" + (made > 0 ? $"(새로 놓음 {made}: {string.Join(", ", list)})" : "");
    }

    /// 데모 팩 식생(이름 끝 " [TFF]")이 켜져 있는가 — 메뉴 체크 표시용이라 기록(GlobalObjectId) 대신 이름으로 빠르게 센다.
    internal static bool PlantsAreLast(Scene scene)
    {
        int on = 0, all = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(Tag)) { all++; if (t.gameObject.activeSelf) on++; }
        return all > 0 && on * 2 >= all;
    }

    /// 처음 — 데모 팩 식생을 끄고 잎 카드 나무·덤불·화초를 원래 켜짐 상태로. 지우지 않는다(마지막으로 갈 때 그대로 켠다).
    internal static string PlantsToFirst(Scene scene)
    {
        var snap = AssetDatabase.LoadAssetAtPath<ToonFantasyFloraSnapshot>(FloraSnap);
        if (snap == null) return "나무·덤불·꽃 — 바꾼 적 없음(그대로)";
        int back = 0; var missing = new List<string>();
        foreach (var s in snap.swaps)
        {
            var rep = Resolve<GameObject>(s.replacement);
            if (rep != null) SetActiveRecorded(rep, false, "데모 식생 끔");
            var orig = Resolve<GameObject>(s.original);
            if (orig == null) { missing.Add(s.name); continue; }
            SetActiveRecorded(orig, s.wasActive, "잎 카드 식생 켬");
            back++;
        }
        return $"나무·덤불·꽃 잎 카드 {back}개" + (missing.Count > 0 ? $" · 못 찾은 원래 것: {string.Join(", ", missing)}" : "");
    }

    /// 켜고 끄기 — 프리팹 인스턴스면 오버라이드로 바로 적는다. 안 적으면 같은 호출 안에서 뒤따르는 프리팹 작업(셰이더 BCM 의 오버라이드 걷기)이
    /// 인스턴스를 다시 합치면서 방금 바꾼 켜짐을 예전 값으로 되돌린다(임시 프로젝트에서 처음으로 돌아갈 때 잎 카드 나무가 꺼진 채 남았다).
    static void SetActiveRecorded(GameObject go, bool on, string undo)
    {
        if (go.activeSelf == on) return;
        Undo.RecordObject(go, undo);
        go.SetActive(on);
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.RecordPrefabInstancePropertyModifications(go);
    }

    static GameObject PlaceReplacement(GameObject inst, Dictionary<Material, Material> mats, Dictionary<string, float> packH, Dictionary<string, int> counts)
    {
        string kind = KindOf(inst);
        var options = Map[kind];
        var pos = inst.transform.position;
        string pick = options[Mathf.Abs(Mathf.FloorToInt(pos.x * 7.31f + pos.z * 3.17f)) % options.Length];
        var prefab = ToonFantasyAssets.Find<GameObject>(Path.GetFileName(pick));
        if (prefab == null) { Debug.LogWarning($"[맵 버전] 팩 프리팹 없음: {pick} — {inst.name} 건너뜀"); return null; }

        if (!packH.TryGetValue(pick, out float ph)) { ph = MeshBounds(prefab, out var pb) ? pb.size.y : 1f; packH[pick] = ph; }
        float h = MeshBounds(inst, out var ob) ? ob.size.y : ph;
        float scale = h / Mathf.Max(0.01f, ph);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, inst.scene);
        Undo.RegisterCreatedObjectUndo(go, "데모 식생");
        go.name = prefab.name + Tag;
        go.transform.SetParent(inst.transform.parent, false);
        go.transform.SetSiblingIndex(inst.transform.GetSiblingIndex() + 1);
        var r = inst.transform.right;   // FBX 루트가 X 로 눕혀져 있어도 Y 회전만 뽑는다
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Mathf.Atan2(-r.z, r.x) * Mathf.Rad2Deg, 0f));
        go.transform.localScale = Vector3.one * scale;
        go.layer = inst.layer;
        var flags = GameObjectUtility.GetStaticEditorFlags(inst);
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
            t.gameObject.layer = inst.layer;
        }
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var rend in go.GetComponentsInChildren<Renderer>(true))
        {
            var ms = rend.sharedMaterials; bool ch = false;
            for (int i = 0; i < ms.Length; i++)
                if (ms[i] != null && mats.TryGetValue(ms[i], out var city)) { ms[i] = city; ch = true; }
            if (ch) rend.sharedMaterials = ms;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        foreach (var c in go.GetComponentsInChildren<Component>(true))
            if (c != null && PrefabUtility.IsPartOfPrefabInstance(c)) PrefabUtility.RecordPrefabInstancePropertyModifications(c);
        counts[pick] = counts.TryGetValue(pick, out int c0) ? c0 + 1 : 1;
        return go;
    }

    /// 씬의 잎 카드 FBX 인스턴스 루트(꺼진 것 포함 — 테스트 잔디 보관함의 나무도 같이 바꿔 둬야 되돌릴 때 섞이지 않는다).
    static IEnumerable<GameObject> LeafCardInstances(Scene scene)
    {
        var seen = new HashSet<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                if (KindOf(t.gameObject) == null || !seen.Add(t.gameObject)) continue;
                yield return t.gameObject;
            }
    }

    static string KindOf(GameObject inst)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromOriginalSource(inst);
        if (src == null) return null;
        string name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(src));
        return Map.ContainsKey(name) ? name : null;
    }

    /// 팩 나무·덤불 머티리얼 → 도시용 셰이더 복사본(같은 이름의 값 그대로 + 투시·잎 카드 키워드). 없으면 만든다.
    static Dictionary<Material, Material> FloraMaterials()
    {
        var d = new Dictionary<Material, Material>();
        var solid = ToonFantasyAssets.Find<Material>("TFF_Atlas_1A_D");
        var veg = ToonFantasyAssets.Find<Material>("TFF_Atlas_Vegetation_1A_D");
        if (solid != null) d[solid] = CityCopy(solid, "Sujin/Toon/Outline", false);
        if (veg != null) d[veg] = CityCopy(veg, "Sujin/Toon/Vegetation", true);
        foreach (var k in new List<Material>(d.Keys)) if (d[k] == null) d.Remove(k);
        return d;
    }

    static Material CityCopy(Material src, string shaderName, bool cutout)
    {
        string path = $"{FloraMatDir}/{src.name}_City.mat";
        var have = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (have != null) return have;   // 손본 값 유지
        var sh = Shader.Find(shaderName);
        if (sh == null) { Debug.LogError($"[Toon Fantasy] 셰이더 {shaderName} 없음 — {Root}/Shaders 임포트 확인"); return null; }
        var m = new Material(src) { name = src.name + "_City", shader = sh };   // 팩과 같은 속성 이름(텍스처·램프·외곽선·바람) 그대로 남는다
        m.SetColor("_BaseColor", Color.white);
        m.SetColor("_EmissionColor", Color.black);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Cull", 2f);
        m.SetFloat("_SrcBlend", 1f); m.SetFloat("_DstBlend", 0f); m.SetFloat("_ZWrite", 1f);
        m.SetFloat("_UseSeeThrough", 1f);
        m.EnableKeyword("BCM_SEE_THROUGH");   // 도시 잎 카드 나무·덤불도 가림막이었다(투시·충돌이 이 키워드로 고른다)
        if (cutout)
        {
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_WindStrength", 1f);   // 팩 메시는 정점색 바람 마스크가 칠해져 있다 — 팩 그대로
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EnsureFolder(FloraMatDir);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ================================================================================================ 풀

    /// 최종본 풀 패치를 굽는다 — 메시(TFF_CityGrassPatches.asset)와 풀 조각 튀기용 잔디 지도(TFF_GrassMask_Lawn.asset)를 새로 쓰고
    /// _City/_GrassPatches_TFF 를 다시 만든다. 풀잎 끄기 · 도시 풀 카드 · 풀 지도 끼우기 · 씬 저장은 <see cref="CityGrassVersions"/> 가 이어서 한다.
    /// 메뉴: Tools/좀비 오버런/풀/최종본 다시 굽기 — 굽기 규칙(상수)을 바꿨거나 CityBuilder 로 도시를 다시 만들었을 때만.
    internal static bool BakeFinal(GameObject city, out string report)
    {
        report = null;
        if (city == null || city.transform.Find("Ground") == null)
        {
            EditorUtility.DisplayDialog("풀", "_City/Ground(도시 바닥 메시)가 없다 — 도시 테스트 씬에서 실행.", "확인");
            return false;
        }
        var map = city.GetComponent<Proto.CityMap>();

        // 패치 메시·머티리얼 — 서브메시 순서: 풀 3 · 팩 꽃 · (들국화·포인트 풀 카드). 블록마다 빈 서브메시는 뺀다.
        var meshList = new List<Mesh>(); var matList = new List<Material>();
        foreach (var gp in GrassPrefabs)
        {
            if (!PackLod(gp, GrassLod, out var me, out var ma)) { EditorUtility.DisplayDialog("풀", $"팩 풀 패치 {gp} 를 못 읽었다.", "확인"); return false; }
            meshList.Add(me); matList.Add(ma);
        }
        var flowerNames = new List<string>();
        foreach (var fp in FlowerPrefabs)
        {
            if (!PackLod(fp, FlowerLod, out var me, out var ma)) { Debug.LogWarning($"[풀] 팩 꽃 패치 {fp} 를 못 읽었다 — 그 색은 빼고 심는다."); continue; }
            meshList.Add(me); matList.Add(ma); flowerNames.Add(fp.Substring(fp.LastIndexOf('_') + 1));
        }
        var daisyMat = DaisyMaterial(city.transform);
        if (daisyMat != null) matList.Add(daisyMat);
        else Debug.LogWarning($"[풀] 들국화 머티리얼을 못 찾았다(_City/_Foliage 렌더러 · {DaisyMatFallback}) — 들국화·포인트 풀 카드는 빼고 심는다.");
        var meshes = meshList.ToArray(); var mats = matList.ToArray();

        var lawn = BuildLawn(city.transform, map != null ? map.halfSize : 86f);
        var built = BuildPatchBlocks(lawn, meshes, GrassPrefabs.Length, daisyMat != null, out int patches, out int verts, out int clipped, out int cards,
                                     out int clusters, out int flowers, out int daisies, out int accents);

        // 메시 저장(에셋 하나에) → 읽기 끔(런타임 CPU 사본 없음)
        string meshPath = GrassMeshAsset;   // 자리는 한 번만 정한다(지우고 나면 새 자리로 바뀌어 보인다)
        if (AssetDatabase.LoadAssetAtPath<Object>(meshPath) != null) AssetDatabase.DeleteAsset(meshPath);
        EnsureFolder(Path.GetDirectoryName(meshPath).Replace('\\', '/'));
        for (int i = 0; i < built.Count; i++)
        {
            if (i == 0) AssetDatabase.CreateAsset(built[i].mesh, meshPath);
            else AssetDatabase.AddObjectToAsset(built[i].mesh, meshPath);
        }
        AssetDatabase.SaveAssets();
        foreach (var b in built)
        {
            var so = new SerializedObject(b.mesh);
            var rd = so.FindProperty("m_IsReadable");
            if (rd != null) { rd.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
        AssetDatabase.SaveAssets();

        var old = city.transform.Find("_GrassPatches_TFF");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("_GrassPatches_TFF");
        Undo.RegisterCreatedObjectUndo(root, "풀 굽기");
        root.transform.SetParent(city.transform, false);
        root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;   // 정점이 월드 좌표다
        int drawCalls = 0;
        foreach (var b in built)
        {
            var go = new GameObject(b.mesh.name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var bm = new Material[b.subs.Length];
            for (int i = 0; i < bm.Length; i++) bm[i] = mats[b.subs[i]];
            mr.sharedMaterials = bm;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            drawCalls += bm.Length;
        }
        LawnMask(lawn, AssetDatabase.LoadAssetAtPath<Proto.ProtoGrassMask>(CityGrassVersions.OriginalMaskPath));

        int lawnCells = 0, testCells = 0;
        for (int i = 0; i < lawn.inner.Length; i++) if (lawn.inner[i]) { lawnCells++; if (lawn.src[i] == SrcTest) testCells++; }
        report = $"잔디 {lawnCells * LawnCell * LawnCell:N0}m²(넓은 잔디 {testCells * LawnCell * LawnCell:N0}m²)에 블록 {built.Count}개(서브메시 {drawCalls}개), 패치 {patches:N0}장, 정점 {verts:N0}개 " +
                 $"(정점 {PackedStride}바이트 · 읽기 끔) + 카메라 정면 풀 카드 {cards:N0}장 + 꽃 무더기 {clusters:N0}곳 {flowers:N0}송이(들국화 {daisies:N0} · 팩 꽃 {string.Join("/", flowerNames)} {flowers - daisies:N0}) " +
                 $"+ 넓은 잔디 포인트 풀 카드 {accents:N0}장. 가장자리를 넘어 당긴 정점 {clipped:N0}개";
        return true;
    }

    /// 팩 식생 프리팹의 LOD 메시·머티리얼.
    static bool PackLod(string prefab, int lodIndex, out Mesh mesh, out Material mat)
    {
        mesh = null; mat = null;
        var p = ToonFantasyAssets.Find<GameObject>(Path.GetFileName(prefab));
        var lod = p != null ? p.GetComponent<LODGroup>() : null;
        var lods = lod != null ? lod.GetLODs() : null;
        var rend = lods != null && lods.Length > 0 ? lods[Mathf.Min(lodIndex, lods.Length - 1)].renderers[0] : (p != null ? p.GetComponentInChildren<Renderer>() : null);
        var mf = rend != null ? rend.GetComponent<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return false;
        mesh = mf.sharedMesh; mat = rend.sharedMaterial;
        return true;
    }

    /// 들국화 카드 머티리얼 = 도시 풀 카드(_City/_Foliage)가 지금 쓰는 것 — 셰이더 교체(톤 복사본)·원래대로 상태를 그대로 따른다.
    /// (교체·원래대로는 씬 렌더러를 전부 훑으므로 이 패치 렌더러의 들국화 칸도 같이 바뀐다)
    static Material DaisyMaterial(Transform city)
    {
        Material m = null;
        var fol = city.Find("_Foliage");
        if (fol != null)
            foreach (var r in fol.GetComponentsInChildren<MeshRenderer>(true))
                if (r.sharedMaterial != null) { m = r.sharedMaterial; break; }
        if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>(DaisyMatFallback);
        return CityGrassVersions.FinalMaterial(city.gameObject, m);   // 처음 버전(BCM)에서 굽더라도 최종본 셰이더 쪽으로
    }

    /// 바닥 메시 윗면을 칸으로 읽은 잔디 지도. 칸마다 가장 높은 면이 이긴다(연석 윗면이 잔디를 덮으면 잔디 아님).
    /// 풀 두 벌 전환(CityGrassVersions → CityFoliageFlowers.KeepInFinal — 최종본에 켜 둘 도시 풀 카드 블록)도 쓴다.
    internal class LawnGrid
    {
        public float half, cell; public int n;
        public float[] y; public bool[] lawn, inner;
        public byte[] src;   // 잔디 칸의 출처(SrcPark · SrcTest), 잔디 아니면 0
        public int Index(float x, float z)
        {
            int ix = Mathf.FloorToInt((x + half) / cell), iz = Mathf.FloorToInt((z + half) / cell);
            return ix < 0 || iz < 0 || ix >= n || iz >= n ? -1 : iz * n + ix;
        }
        public bool Inner(float x, float z) { int i = Index(x, z); return i >= 0 && inner[i]; }
    }

    /// 공원 잔디(Ground 서브메시 3)와 테스트 잔디(_TestLawn 서브메시 0) — 보도·연석·주차장 면이 위에 있으면 뺀다.
    /// inner = 가장자리에서 LawnInset 안쪽(연석 안쪽 옆면에 기대 솟는 풀잎까지 막는다). 테스트 잔디는 TestLawnInset(연석이 바닥과 같은 높이).
    internal static LawnGrid BuildLawn(Transform city, float half)
    {
        var g = new LawnGrid { half = half, cell = LawnCell, n = Mathf.CeilToInt(half * 2f / LawnCell) };
        int cells = g.n * g.n;
        g.y = new float[cells]; g.lawn = new bool[cells]; g.inner = new bool[cells]; g.src = new byte[cells];
        for (int i = 0; i < cells; i++) g.y[i] = float.NegativeInfinity;
        RasterTop(g, city.Find("Ground"), LawnSubmesh, SrcPark);
        RasterTop(g, city.Find("ParkingLots"), -1, 0);
        RasterTop(g, city.Find(CityTestLawn.LawnName), 0, SrcTest);
        int rPark = Mathf.Max(0, Mathf.CeilToInt(LawnInset / LawnCell - 1e-4f));
        int rTest = Mathf.Max(0, Mathf.CeilToInt(TestLawnInset / LawnCell - 1e-4f));
        for (int iz = 0; iz < g.n; iz++)
            for (int ix = 0; ix < g.n; ix++)
            {
                int k = iz * g.n + ix;
                if (!g.lawn[k]) continue;
                int r = g.src[k] == SrcTest ? rTest : rPark;
                bool ok = true;
                for (int dz = -r; dz <= r && ok; dz++)
                    for (int dx = -r; dx <= r && ok; dx++)
                    {
                        int x = ix + dx, z = iz + dz;
                        ok = x >= 0 && z >= 0 && x < g.n && z < g.n && g.lawn[z * g.n + x];
                    }
                g.inner[k] = ok;
            }
        return g;
    }

    /// CityGrassBaker.Raster 와 같은 규칙 — 위를 보는 면만, 칸 중심이 삼각형 안이면 그 높이, 더 높은 면이 이긴다.
    static void RasterTop(LawnGrid g, Transform t, int lawnSub, byte srcId)
    {
        if (t == null) return;
        var mf = t.GetComponent<MeshFilter>();
        var mesh = mf != null ? mf.sharedMesh : null;
        if (mesh == null || !mesh.isReadable) return;
        var l2w = t.localToWorldMatrix;
        var vs = mesh.vertices;
        var wv = new Vector3[vs.Length];
        for (int i = 0; i < vs.Length; i++) wv[i] = l2w.MultiplyPoint3x4(vs[i]);
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tris = mesh.GetTriangles(s);
            for (int k = 0; k < tris.Length; k += 3)
            {
                Vector3 a = wv[tris[k]], b = wv[tris[k + 1]], c = wv[tris[k + 2]];
                Vector3 nrm = Vector3.Cross(b - a, c - a);
                if (nrm.sqrMagnitude < 1e-10f || Mathf.Abs(nrm.normalized.y) < 0.7f) continue;   // 옆면(연석 턱)
                float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
                float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                int ix0 = Mathf.Max(0, Mathf.FloorToInt((minX + g.half) / g.cell)), ix1 = Mathf.Min(g.n - 1, Mathf.FloorToInt((maxX + g.half) / g.cell));
                int iz0 = Mathf.Max(0, Mathf.FloorToInt((minZ + g.half) / g.cell)), iz1 = Mathf.Min(g.n - 1, Mathf.FloorToInt((maxZ + g.half) / g.cell));
                float det = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(det) < 1e-9f) continue;
                for (int iz = iz0; iz <= iz1; iz++)
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        float px = -g.half + (ix + 0.5f) * g.cell, pz = -g.half + (iz + 0.5f) * g.cell;
                        float w0 = ((b.z - c.z) * (px - c.x) + (c.x - b.x) * (pz - c.z)) / det;
                        float w1 = ((c.z - a.z) * (px - c.x) + (a.x - c.x) * (pz - c.z)) / det;
                        float w2 = 1f - w0 - w1;
                        if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                        float y = a.y * w0 + b.y * w1 + c.y * w2;
                        int idx = iz * g.n + ix;
                        if (y <= g.y[idx]) continue;
                        g.y[idx] = y;
                        g.lawn[idx] = s == lawnSub;
                        g.src[idx] = s == lawnSub ? srcId : (byte)0;
                    }
            }
        }
    }

    /// root(패치 중심, 잔디 안) → w 선분에서 잔디 안(inner)인 가장 먼 점. 높이도 같은 비율로 — 가장자리 풀잎은 짧아진다.
    static Vector3 PullInside(LawnGrid g, Vector3 root, Vector3 w)
    {
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 10; i++)
        {
            float mid = (lo + hi) * 0.5f;
            var p = root + (w - root) * mid;
            if (g.Inner(p.x, p.z)) lo = mid; else hi = mid;
        }
        return root + (w - root) * lo;
    }

    /// 잔디 칸(inner)마다 가장자리(inner 아닌 칸)까지 거리(m, 칸 가운데 기준 — 맨 가장자리 칸 0.05). 8방향 챔퍼 두 번.
    static float[] EdgeDistance(LawnGrid g)
    {
        int n = g.n;
        var d = new float[n * n];
        float big = Mathf.Max(EdgeBand, TestEdgeMargin) / g.cell + 4f;   // 그보다 먼 곳은 정확하지 않아도 된다(그 값으로 묶인다)
        for (int k = 0; k < d.Length; k++) d[k] = g.inner[k] ? big : 0f;
        const float D1 = 1f, D2 = 1.4142136f;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int k = z * n + x; float v = d[k];
                if (v == 0f) continue;
                if (x > 0) v = Mathf.Min(v, d[k - 1] + D1);
                if (z > 0)
                {
                    v = Mathf.Min(v, d[k - n] + D1);
                    if (x > 0) v = Mathf.Min(v, d[k - n - 1] + D2);
                    if (x < n - 1) v = Mathf.Min(v, d[k - n + 1] + D2);
                }
                d[k] = v;
            }
        for (int z = n - 1; z >= 0; z--)
            for (int x = n - 1; x >= 0; x--)
            {
                int k = z * n + x; float v = d[k];
                if (v == 0f) continue;
                if (x < n - 1) v = Mathf.Min(v, d[k + 1] + D1);
                if (z < n - 1)
                {
                    v = Mathf.Min(v, d[k + n] + D1);
                    if (x < n - 1) v = Mathf.Min(v, d[k + n + 1] + D2);
                    if (x > 0) v = Mathf.Min(v, d[k + n - 1] + D2);
                }
                d[k] = v;
            }
        for (int k = 0; k < d.Length; k++) d[k] = d[k] > 0f ? (d[k] - 0.5f) * g.cell : 0f;
        return d;
    }

    /// 정점 하나 마무리(6판): 가장자리 가까이는 키를 줄이고 바람(정점색)을 줄인 뒤, 팩 풀 셰이더 정점이면 남은 바람의 평균 이동만큼 X·Z 를 미리 반대로.
    /// groundY = 밑동 높이(키는 이 위로만 줄인다 — 옆으로 당기면 가장자리에 틈이 생긴다).
    static Vector3 Settle(LawnGrid g, float[] edge, Vector3 w, float groundY, ref Color32 c, bool packWind)
    {
        int k = g.Index(w.x, w.z);
        float t = Mathf.SmoothStep(0f, 1f, (k >= 0 ? edge[k] : 0f) / EdgeBand);
        if (t < 1f)
        {
            w.y = groundY + (w.y - groundY) * Mathf.Lerp(EdgeHeight, 1f, t);
            c = new Color32((byte)Mathf.RoundToInt(c.r * t), (byte)Mathf.RoundToInt(c.g * t), (byte)Mathf.RoundToInt(c.b * t), c.a);
        }
        if (packWind)
        {
            var s = new Vector3(w.x - WindMean * c.r / 255f, w.y, w.z - WindMean * c.b / 255f);
            if (g.Inner(s.x, s.z)) w = s;   // 맨 가장자리(바람이 거의 0)에서 몇 mm 라도 밖으로 밀리면 그대로 둔다
        }
        return w;
    }

    internal struct PatchBlock { public Mesh mesh; public int[] subs; }   // subs = 이 블록 메시의 서브메시 i 가 쓰는 전체 머티리얼 칸

    // ── 최적화(11-13 "최적화도 해줘") ── 정점 36 → 28바이트: 위치 float32×3 · 노멀 half×4 · 색 8비트×4 · UV half×2.
    //  위치는 월드 좌표(±70m)라 half 로는 3cm 계단이 생겨 그대로 둔다. UV 는 팩 텍스처 0~1 이라 half(1/2048) 로 충분하다.
    //  여기에 읽기 끔(런타임 CPU 사본 없음) · 정점 캐시 순서 정리(MeshUtility.Optimize) · 빈 서브메시 빼기.
    [StructLayout(LayoutKind.Sequential)]
    struct PackedVertex { public Vector3 pos; public ushort nx, ny, nz, nw; public Color32 col; public ushort u, v; }
    const int PackedStride = 28;
    static readonly VertexAttributeDescriptor[] PackedLayout =
    {
        new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
        new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float16, 4),
        new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
        new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2),
    };

    static Mesh PackMesh(string name, Chunk ch, List<int> used)
    {
        int n = ch.v.Count;
        var data = new PackedVertex[n];
        for (int i = 0; i < n; i++)
        {
            var nr = ch.nrm[i]; var uv = ch.uv[i];
            data[i] = new PackedVertex
            {
                pos = ch.v[i],
                nx = Mathf.FloatToHalf(nr.x), ny = Mathf.FloatToHalf(nr.y), nz = Mathf.FloatToHalf(nr.z), nw = 0,
                col = ch.col[i],
                u = Mathf.FloatToHalf(uv.x), v = Mathf.FloatToHalf(uv.y),
            };
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertexBufferParams(n, PackedLayout);
        mesh.SetVertexBufferData(data, 0, 0, n);
        mesh.subMeshCount = used.Count;
        for (int i = 0; i < used.Count; i++) mesh.SetTriangles(ch.tri[used[i]], i, false);
        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(mesh.bounds.center, mesh.bounds.size + new Vector3(0.5f, 0.5f, 0.5f));   // 바람 흔들림 여유
        MeshUtility.Optimize(mesh);
        return mesh;
    }

    class Chunk
    {
        public readonly List<Vector3> v = new List<Vector3>(), nrm = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<Color32> col = new List<Color32>();
        public readonly List<int>[] tri;
        public Chunk(int subs) { tri = new List<int>[subs]; for (int i = 0; i < subs; i++) tri[i] = new List<int>(); }
    }

    /// 잔디 칸(inner)에 패치를 심고, 꼭짓점 하나라도 잔디 밖(inner 아님)인 삼각형은 버린다 — 풀잎이 연석·보도로 넘지 않는다.
    static List<PatchBlock> BuildPatchBlocks(LawnGrid g, Mesh[] src, int grassCount, bool daisy, out int patches, out int verts, out int clipped, out int cards,
                                       out int clusters, out int flowers, out int daisies, out int accents)
    {
        int subs = src.Length + (daisy ? 1 : 0);   // 풀 · 팩 꽃 · (들국화 — 마지막 칸)
        var sv = new List<Vector3>[src.Length]; var sn = new List<Vector3>[src.Length]; var suv = new List<Vector2>[src.Length];
        var sc = new List<Color32>[src.Length]; var st = new int[src.Length][];
        for (int i = 0; i < src.Length; i++)
        {
            sv[i] = new List<Vector3>(); src[i].GetVertices(sv[i]);
            sn[i] = new List<Vector3>(); src[i].GetNormals(sn[i]);
            suv[i] = new List<Vector2>(); src[i].GetUVs(0, suv[i]);
            sc[i] = new List<Color32>(); src[i].GetColors(sc[i]);
            st[i] = src[i].triangles;
        }
        var rngPark = new System.Random(Seed); var rngTest = new System.Random(Seed + 1);   // ⑤ 잔디마다 따로
        System.Random RngOf(int cellIndex) => g.src[cellIndex] == SrcTest ? rngTest : rngPark;
        var edge = EdgeDistance(g);
        int blocks = Mathf.CeilToInt(g.half * 2f / Block);
        var chunks = new Dictionary<int, Chunk>();
        float pCell = PatchesPerM2 * g.cell * g.cell;   // 잔디 칸 하나에 패치가 놓일 확률
        var wv = new List<Vector3>(); var wc = new List<Color32>(); var remap = new List<int>();
        patches = 0; clipped = 0;
        for (int iz = 0; iz < g.n; iz++)
            for (int ix = 0; ix < g.n; ix++)
            {
                int k = iz * g.n + ix;
                if (!g.inner[k]) continue;
                var rng = RngOf(k);
                if (rng.NextDouble() > pCell) continue;
                int variant = Pick(rng);
                float s = Mathf.Lerp(LawnScale.x, LawnScale.y, (float)rng.NextDouble());
                float px = -g.half + (ix + (float)rng.NextDouble()) * g.cell;
                float pz = -g.half + (iz + (float)rng.NextDouble()) * g.cell;
                var rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                var scale = new Vector3(s, s * LawnHeight, s);
                var m = Matrix4x4.TRS(new Vector3(px, g.y[k], pz), rot, scale);

                int bx = Mathf.Clamp(Mathf.FloorToInt((px + g.half) / Block), 0, blocks - 1);
                int bz = Mathf.Clamp(Mathf.FloorToInt((pz + g.half) / Block), 0, blocks - 1);
                int key = bz * blocks + bx;
                if (!chunks.TryGetValue(key, out var ch)) chunks[key] = ch = new Chunk(subs);
                if (ch.v.Count + sv[variant].Count > 65000) continue;   // 16비트 인덱스(모바일)

                wv.Clear(); wc.Clear(); remap.Clear();
                var root = new Vector3(px, g.y[k], pz);
                for (int vi = 0; vi < sv[variant].Count; vi++)
                {
                    var w = m.MultiplyPoint3x4(sv[variant][vi]);
                    // 잔디 가장자리(Inset 안쪽)를 넘는 정점은 패치 중심 쪽으로 당겨 가장자리에서 멈추게 한다 — 풀잎이 짧아질 뿐 남는다.
                    // 2판은 넘는 삼각형을 버려 가장자리 0.5~1m 띠가 비었다(사용자 스크린샷: "바깥 네모 영역까지 심어줘").
                    if (!g.Inner(w.x, w.z)) { w = PullInside(g, root, w); clipped++; }
                    var c0 = vi < sc[variant].Count ? sc[variant][vi] : White32;
                    w = Settle(g, edge, w, root.y, ref c0, true);
                    wv.Add(w); wc.Add(c0); remap.Add(-1);
                }
                var tris = st[variant]; int kept = 0;
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    foreach (int vi in new[] { a, b, c })
                    {
                        if (remap[vi] < 0)
                        {
                            remap[vi] = ch.v.Count;
                            ch.v.Add(wv[vi]);
                            var n0 = vi < sn[variant].Count ? sn[variant][vi] : Vector3.up;
                            ch.nrm.Add((rot * new Vector3(n0.x / scale.x, n0.y / scale.y, n0.z / scale.z)).normalized);   // 비균등 크기 → 역크기로
                            ch.uv.Add(vi < suv[variant].Count ? suv[variant][vi] : Vector2.zero);
                            ch.col.Add(wc[vi]);
                        }
                        ch.tri[variant].Add(remap[vi]);
                    }
                    kept++;
                }
                if (kept > 0) patches++;
            }

        // ── 카메라 정면 풀 카드 ── 카메라 쪽(−Z) 가장자리까지 잔디 칸 거리 · 좌우(±X) 가장자리까지 거리
        int n = g.n;
        var nearRun = new int[n * n]; var leftRun = new int[n * n]; var rightRun = new int[n * n];
        for (int ix = 0; ix < n; ix++)
            for (int iz = 0; iz < n; iz++) { int k = iz * n + ix; nearRun[k] = g.inner[k] ? (iz > 0 ? nearRun[k - n] : 0) + 1 : 0; }
        for (int iz = 0; iz < n; iz++)
        {
            for (int ix = 0; ix < n; ix++) { int k = iz * n + ix; leftRun[k] = g.inner[k] ? (ix > 0 ? leftRun[k - 1] : 0) + 1 : 0; }
            for (int ix = n - 1; ix >= 0; ix--) { int k = iz * n + ix; rightRun[k] = g.inner[k] ? (ix < n - 1 ? rightRun[k + 1] : 0) + 1 : 0; }
        }
        var lean = Mathf.Tan(CardLean * Mathf.Deg2Rad);
        var cardUV = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        var black = new Color32(0, 0, 0, 255); var white = new Color32(255, 255, 255, 255);   // 패치와 같은 바람 마스크(밑동 0 · 끝 1)
        float pCard = FrontCardsPerM2 * g.cell * g.cell;
        cards = 0;
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                int k = iz * n + ix;
                if (!g.inner[k]) continue;
                float dNear = nearRun[k] * g.cell, dSide = Mathf.Min(leftRun[k], rightRun[k]) * g.cell;
                float w = Mathf.Max(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FrontBand * 0.7f, FrontBand, dNear)),
                                    SideDensity * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SideBand * 0.6f, SideBand, dSide))));
                var rng = RngOf(k);
                if (w <= 0f || rng.NextDouble() > pCard * w) continue;
                int variant = Pick(rng);
                float cw = Mathf.Lerp(CardWidth.x, CardWidth.y, (float)rng.NextDouble());
                float ch2 = Mathf.Lerp(CardHeight.x, CardHeight.y, (float)rng.NextDouble());
                float yaw = ((float)rng.NextDouble() * 2f - 1f) * CardYaw * Mathf.Deg2Rad;
                bool flip = rng.NextDouble() < 0.5;
                float px = -g.half + (ix + (float)rng.NextDouble()) * g.cell;
                float pz = -g.half + (iz + (float)rng.NextDouble()) * g.cell;
                var basePos = new Vector3(px, g.y[k] - 0.02f, pz);   // 밑동이 바닥에 살짝 묻히게
                var right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (cw * 0.5f);
                var up = new Vector3(0f, ch2, ch2 * lean);
                var quad = new[] { basePos - right, basePos - right + up, basePos + right + up, basePos + right };

                int bx = Mathf.Clamp(Mathf.FloorToInt((px + g.half) / Block), 0, blocks - 1);
                int bz = Mathf.Clamp(Mathf.FloorToInt((pz + g.half) / Block), 0, blocks - 1);
                int key = bz * blocks + bx;
                if (!chunks.TryGetValue(key, out var chunk)) chunks[key] = chunk = new Chunk(subs);
                if (chunk.v.Count + 4 > 65000) continue;
                int b0 = chunk.v.Count;
                for (int q = 0; q < 4; q++)
                {
                    var p = quad[q];
                    if (!g.Inner(p.x, p.z)) { p = PullInside(g, basePos, p); clipped++; }
                    var qc = q == 1 || q == 2 ? white : black;
                    p = Settle(g, edge, p, basePos.y, ref qc, true);
                    chunk.v.Add(p);
                    chunk.nrm.Add(Vector3.up);   // CityBuilder 풀 카드처럼 위 — 카드 앞뒤 밝기가 같고 패치 풀과 같은 밝기
                    var uv = cardUV[q]; if (flip) uv.x = 1f - uv.x;
                    chunk.uv.Add(uv);
                    chunk.col.Add(qc);
                }
                // 카메라(−Z) 쪽이 앞면 — 셰이더가 뒷면을 안 그린다(CityBuilder.FoliageMB 와 같은 감기)
                var tri = chunk.tri[variant];
                tri.Add(b0); tri.Add(b0 + 1); tri.Add(b0 + 2); tri.Add(b0); tri.Add(b0 + 2); tri.Add(b0 + 3);
                cards++;
            }

        // ── 꽃 무더기 ── 잔디 칸마다 확률로 무더기 자리를 잡고, 둘레(반지름 0.35~0.8m)에 같은 종류 3~7송이.
        //  거리를 √ 없이 뽑아 가운데로 몰린다(무더기처럼). 잔디 밖에 떨어진 송이는 버리고, 가장자리를 넘는 잎은 패치처럼 당긴다.
        int nFlower = src.Length - grassCount, daisySub = src.Length;
        // 색은 섞은 주머니에서 차례로 — 운에 따라 한 색이 아예 안 나오는 일이 없게. 주머니도 잔디마다 따로(같이 쓰면 넓은 잔디 꽃이 공원 색 순서를 바꾼다).
        var bagPark = new List<int>(); var bagTest = new List<int>();
        float pCluster = nFlower + (daisy ? 1 : 0) > 0 ? FlowerClustersPerM2 * g.cell * g.cell : 0f;
        clusters = 0; flowers = 0; daisies = 0;
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                int k = iz * n + ix;
                if (!g.inner[k]) continue;
                if (g.src[k] == SrcTest && (!TestLawnFlowers || edge[k] < TestEdgeMargin)) continue;   // 넓은 잔디: 가장자리 띠는 비운다(7판)
                var rng = RngOf(k);
                if (rng.NextDouble() > pCluster) continue;
                bool isDaisy = daisy && (nFlower == 0 || rng.NextDouble() < DaisyShare);
                int sub = daisySub;
                if (!isDaisy)
                {
                    var bag = g.src[k] == SrcTest ? bagTest : bagPark;
                    if (bag.Count == 0) { for (int b = 0; b < nFlower; b++) bag.Insert(rng.Next(bag.Count + 1), b); }
                    sub = grassCount + bag[bag.Count - 1]; bag.RemoveAt(bag.Count - 1);
                }
                float cx = -g.half + (ix + 0.5f) * g.cell, cz = -g.half + (iz + 0.5f) * g.cell;
                float radius = Mathf.Lerp(ClusterRadius.x, ClusterRadius.y, (float)rng.NextDouble());
                var cc = isDaisy ? ClusterCount : ColorClusterCount;
                int count = rng.Next(cc.x, cc.y + 1), placed = 0;
                for (int j = 0; j < count; j++)
                {
                    float ang = (float)rng.NextDouble() * Mathf.PI * 2f, dist = radius * (float)rng.NextDouble();
                    float px = cx + Mathf.Cos(ang) * dist, pz = cz + Mathf.Sin(ang) * dist;
                    int kk = g.Index(px, pz);
                    if (kk < 0 || !g.inner[kk] || (g.src[kk] == SrcTest && (!TestLawnFlowers || edge[kk] < TestEdgeMargin))) continue;
                    var root = new Vector3(px, g.y[kk], pz);
                    int bx = Mathf.Clamp(Mathf.FloorToInt((px + g.half) / Block), 0, blocks - 1);
                    int bz = Mathf.Clamp(Mathf.FloorToInt((pz + g.half) / Block), 0, blocks - 1);
                    int key = bz * blocks + bx;
                    if (!chunks.TryGetValue(key, out var fc)) chunks[key] = fc = new Chunk(subs);

                    if (!isDaisy)
                    {
                        // 팩 꽃 패치 — 고른 키에 맞춘 고른 크기, 아무 방향
                        var fv = sv[sub];
                        if (fc.v.Count + fv.Count > 65000) continue;
                        float fs = Mathf.Lerp(FlowerHeight.x, FlowerHeight.y, (float)rng.NextDouble()) / Mathf.Max(0.01f, src[sub].bounds.max.y);
                        var frot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                        var fm = Matrix4x4.TRS(root, frot, Vector3.one * fs);
                        int fb = fc.v.Count;
                        for (int vi = 0; vi < fv.Count; vi++)
                        {
                            var w = fm.MultiplyPoint3x4(fv[vi]);
                            if (!g.Inner(w.x, w.z)) { w = PullInside(g, root, w); clipped++; }
                            var fcol = vi < sc[sub].Count ? sc[sub][vi] : white;
                            w = Settle(g, edge, w, root.y, ref fcol, true);
                            fc.v.Add(w);
                            fc.nrm.Add(frot * (vi < sn[sub].Count ? sn[sub][vi] : Vector3.up));
                            fc.uv.Add(vi < suv[sub].Count ? suv[sub][vi] : Vector2.zero);
                            fc.col.Add(fcol);
                        }
                        foreach (int t in st[sub]) fc.tri[sub].Add(fb + t);
                    }
                    else
                    {
                        // 들국화 — CityBuilder.FoliageMB 와 같은 V 자 두 장(윗변 카메라 반대쪽 25°, 노멀 위)
                        if (fc.v.Count + 8 > 65000) continue;
                        float h = Mathf.Lerp(DaisyHeight.x, DaisyHeight.y, (float)rng.NextDouble());
                        clipped += VCard(fc, daisySub, root + new Vector3(0f, -0.02f, 0f), DaisyCell, h, rng, g, edge);
                        daisies++;
                    }
                    placed++;
                }
                if (placed > 0) { clusters++; flowers += placed; }
            }

        // ── 포인트 풀(7판) ── 넓은 잔디에 _Foliage 아틀라스 카드(마른 풀더미 · 귀리 · 넓은잎)를 무더기로. 들국화와 같은 머티리얼 · 같은 서브메시.
        //  무더기 자리 확률에 펄린 노이즈(0.25~1.75배)를 곱해 들판처럼 진하고 옅은 곳을 둔다. 가장자리 TestEdgeMargin 안은 비운다.
        //  공원 칸은 난수를 뽑기 전에 건너뛴다 — 공원 배치는 6판 그대로.
        accents = 0;
        if (daisy)
        {
            float pAccent = AccentClumpsPerM2 * g.cell * g.cell;
            float wSum = 0f; foreach (float aw in AccentWeight) wSum += aw;
            for (int iz = 0; iz < n; iz++)
                for (int ix = 0; ix < n; ix++)
                {
                    int k = iz * n + ix;
                    if (!g.inner[k] || g.src[k] != SrcTest || edge[k] < TestEdgeMargin) continue;
                    var rng = RngOf(k);
                    float cx = -g.half + (ix + 0.5f) * g.cell, cz = -g.half + (iz + 0.5f) * g.cell;
                    float field = Mathf.Lerp(0.25f, 1.75f, Mathf.Clamp01(Mathf.PerlinNoise(cx * AccentNoise + 31.7f, cz * AccentNoise + 17.3f)));
                    if (rng.NextDouble() > pAccent * field) continue;
                    double r = rng.NextDouble() * wSum, acc = 0; int kind = AccentWeight.Length - 1;
                    for (int a = 0; a < AccentWeight.Length; a++) { acc += AccentWeight[a]; if (r <= acc) { kind = a; break; } }
                    float radius = Mathf.Lerp(AccentRadius.x, AccentRadius.y, (float)rng.NextDouble());
                    int count = rng.Next(AccentCount[kind].x, AccentCount[kind].y + 1);
                    for (int j = 0; j < count; j++)
                    {
                        float ang = (float)rng.NextDouble() * Mathf.PI * 2f, dist = radius * Mathf.Sqrt((float)rng.NextDouble());
                        float h = Mathf.Lerp(AccentHeight[kind].x, AccentHeight[kind].y, (float)rng.NextDouble());
                        float px = cx + Mathf.Cos(ang) * dist, pz = cz + Mathf.Sin(ang) * dist;
                        int kk = g.Index(px, pz);
                        if (kk < 0 || !g.inner[kk] || edge[kk] < TestEdgeMargin) continue;
                        int bx = Mathf.Clamp(Mathf.FloorToInt((px + g.half) / Block), 0, blocks - 1);
                        int bz = Mathf.Clamp(Mathf.FloorToInt((pz + g.half) / Block), 0, blocks - 1);
                        int key = bz * blocks + bx;
                        if (!chunks.TryGetValue(key, out var ac)) chunks[key] = ac = new Chunk(subs);
                        if (ac.v.Count + 8 > 65000) continue;
                        clipped += VCard(ac, daisySub, new Vector3(px, g.y[kk] - 0.03f, pz), AccentCell[kind], h, rng, g, edge);   // 밑동은 CityBuilder 카드처럼 살짝 묻는다
                        accents++;
                    }
                }
        }

        var list = new List<PatchBlock>(); verts = 0;
        foreach (var kv in chunks)
        {
            var ch = kv.Value;
            if (ch.v.Count == 0) continue;
            var used = new List<int>();
            for (int i = 0; i < subs; i++) if (ch.tri[i].Count > 0) used.Add(i);   // 빈 서브메시는 빼고 머티리얼 칸도 맞춘다
            list.Add(new PatchBlock { mesh = PackMesh($"TFFGrass_{kv.Key % blocks}_{kv.Key / blocks}", ch, used), subs = used.ToArray() });
            verts += ch.v.Count;
        }
        return list;
    }

    /// _Foliage 아틀라스 V 자 카드 한 장(두 판) — CityBuilder.FoliageMB 와 같은 모양: 키 = 가로 h, 윗변을 카메라 반대쪽으로 25°, 노멀 위,
    /// 두 판을 20~40° 벌리고 ±10° 돌린다. 정점색 밑동 0 · 끝 1(톤 식생 셰이더 바람 마스크). 가장자리를 넘는 꼭짓점은 당기고 Settle 로 마무리
    /// (바람 세기 0.07 이라 쏠림 상쇄는 안 한다). 난수는 벌림 · 돌림 · 뒤집기 순서 — 5판 들국화와 같아 공원 들국화 배치가 그대로다. 돌려준 값 = 당긴 꼭짓점 수.
    static int VCard(Chunk fc, int sub, Vector3 basePos, Vector2 cell, float h, System.Random rng, LawnGrid g, float[] edge)
    {
        float lean = Mathf.Tan(CardLean * Mathf.Deg2Rad);
        float spread = Mathf.Lerp(20f, 40f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
        float turn = ((float)rng.NextDouble() - 0.5f) * 20f * Mathf.Deg2Rad;
        bool flip0 = rng.NextDouble() < 0.5;
        var black = new Color32(0, 0, 0, 255);
        int clipped = 0;
        for (int side = 0; side < 2; side++)
        {
            float yaw = turn + (side == 0 ? spread : -spread);
            var right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (h * 0.5f);
            var up = new Vector3(0f, h, h * lean);
            bool flip = (side == 1) ^ flip0;
            float u0 = cell.x + (flip ? 0.5f : 0f), u1 = cell.x + (flip ? 0f : 0.5f);
            var quad = new[] { basePos - right, basePos - right + up, basePos + right + up, basePos + right };
            var quv = new[] { new Vector2(u0, cell.y), new Vector2(u0, cell.y + 0.5f), new Vector2(u1, cell.y + 0.5f), new Vector2(u1, cell.y) };
            int db = fc.v.Count;
            for (int q = 0; q < 4; q++)
            {
                var qp = quad[q];
                if (!g.Inner(qp.x, qp.z)) { qp = PullInside(g, basePos, qp); clipped++; }
                var dc = q == 1 || q == 2 ? White32 : black;
                qp = Settle(g, edge, qp, basePos.y, ref dc, false);
                fc.v.Add(qp); fc.nrm.Add(Vector3.up); fc.uv.Add(quv[q]);
                fc.col.Add(dc);
            }
            var dt = fc.tri[sub];
            dt.Add(db); dt.Add(db + 1); dt.Add(db + 2); dt.Add(db); dt.Add(db + 2); dt.Add(db + 3);
        }
        return clipped;
    }

    /// 풀 조각 튀기(ProtoGrassKick)용 풀 지도를 잔디만으로 — 길가 잡초 자리에선 이제 풀이 안 보이므로 조각도 안 튀게.
    /// 원래 지도(CityGrassMask.asset)는 다른 굽기와 같이 쓰이므로 건드리지 않고 복사본을 만든다.
    static Proto.ProtoGrassMask LawnMask(LawnGrid g, Proto.ProtoGrassMask like)
    {
        float rc = like != null && like.cell > 0f ? like.cell : 0.5f;
        int k = Mathf.Max(1, Mathf.RoundToInt(rc / g.cell));
        int n = Mathf.CeilToInt(g.n / (float)k);
        var dens = new byte[n * n]; var lawnB = new byte[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int cnt = 0;
                for (int dz = 0; dz < k; dz++)
                    for (int dx = 0; dx < k; dx++)
                    {
                        int ix = x * k + dx, iz = z * k + dz;
                        if (ix < g.n && iz < g.n && g.inner[iz * g.n + ix]) cnt++;
                    }
                dens[z * n + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(cnt / (float)(k * k) * 255f), 0, 255);
                lawnB[z * n + x] = (byte)(cnt > 0 ? 1 : 0);
            }
        string maskPath = LawnMaskAsset;
        var asset = AssetDatabase.LoadAssetAtPath<Proto.ProtoGrassMask>(maskPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<Proto.ProtoGrassMask>();
            EnsureFolder(Path.GetDirectoryName(maskPath).Replace('\\', '/'));
            AssetDatabase.CreateAsset(asset, maskPath);
        }
        asset.half = g.half; asset.cell = k * g.cell; asset.n = n;
        asset.density = dens; asset.lawn = lawnB;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    static int Pick(System.Random rng)
    {
        double r = rng.NextDouble(), acc = 0;
        for (int i = 0; i < GrassWeights.Length; i++) { acc += GrassWeights[i]; if (r <= acc) return i; }
        return GrassWeights.Length - 1;
    }

    // ================================================================================================ 공통

    static ToonFantasyFloraSnapshot LoadOrCreate(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<ToonFantasyFloraSnapshot>(path);
        if (s != null) return s;
        s = ScriptableObject.CreateInstance<ToonFantasyFloraSnapshot>();
        s.takenAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return s;
    }

    static void SaveSnap(ToonFantasyFloraSnapshot s, string path)
    {
        if (!AssetDatabase.Contains(s)) { EnsureFolder(Root); AssetDatabase.CreateAsset(s, path); }
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
    }


    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    /// 메시 바운즈를 월드로 합친 것(꺼진 오브젝트도 잰다 — Renderer.bounds 는 꺼지면 0 이다).
    static bool MeshBounds(GameObject go, out Bounds b)
    {
        b = default; bool has = false;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var m = mf.transform.localToWorldMatrix; var mb = mf.sharedMesh.bounds;
            var c = m.MultiplyPoint3x4(mb.center); var e = mb.extents;
            var ax = m.MultiplyVector(new Vector3(e.x, 0, 0)); var ay = m.MultiplyVector(new Vector3(0, e.y, 0)); var az = m.MultiplyVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            var wb = new Bounds(c, ext * 2f);
            if (!has) { b = wb; has = true; } else b.Encapsulate(wb);
        }
        return has;
    }

    static string Id(Object o) => GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();

    static T Resolve<T>(string id) where T : Object
    {
        if (string.IsNullOrEmpty(id) || !GlobalObjectId.TryParse(id, out var gid)) return null;
        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as T;
    }
}
