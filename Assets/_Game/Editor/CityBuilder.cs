// 도시 테스트 씬 생성 — "BIG CITY LOW POLY" 레퍼런스(격자 블록 + 중앙 공원 + 로우폴리 빌딩)를 코드로 짓는다.
//
// **왜 스플랫이 아니라 모듈 격자인가.** 도로는 차선이 칼같이 곧아야 하고 보도는 턱(높이)이 있어야 한다.
// 스플랫은 경계가 부드럽게 섞이는 자연 지형용이라 둘 다 못 한다. 그래서 도로·보도·연석은 격자에 맞춘 메시로,
// 차선·횡단보도·정지선은 도로 위에 얹는 얇은 띠로 만든다(선이 텍스처 해상도와 무관하게 선명하다).
//
// **높이 규칙: 보도·공원·블록 = y 0, 도로 = y -0.12.** 캐릭터는 y=0 고정이라, 보도를 올리면 발이 보도에 묻힌다.
// 도로를 내리면 도로 위에서 12cm 뜨지만 17.76m 카메라에선 거의 안 보인다.
//
// **빌딩은 Tripo 가 아니라 코드로 쌓는다.** 레퍼런스는 깔끔한 상자 + 셋백 스타일이라, 박스를 층층이 쌓고 파사드 텍스처
// (힉스필드 GPT Image 2, ArtSource/City)를 층 높이에 맞춰 입히는 쪽이 모양도 맞고 폴리곤도 거의 안 든다.
// 한 채 = 서브메시 2개(벽 = 파사드, 지붕·난간·실외기 = 지붕). ProtoSeeThrough 가 서브메시 여럿을 한 덩어리로 페이드한다.
//
// 원본 Proto_Chapter01 은 건드리지 않는다 — 열어서 필드만 걷어내고 다른 이름으로 저장한다(StreamFieldBuilder 와 같은 방식).
using System.Collections.Generic;
using System.IO;
using Proto;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CityBuilder
{
    const string SrcScene = "Assets/_Game/Scenes/Proto_Chapter01.unity";
    const string DstScene = "Assets/_Game/Scenes/Proto_City_Test.unity";
    const string CityRoot = "Assets/_Game/Art/City";
    const string TexDir   = CityRoot + "/Textures";
    const string MatDir   = CityRoot + "/Materials";
    const string MeshAsset = CityRoot + "/CityMeshes.asset";
    const string FieldMat = "Assets/_Game/Art/Field/Materials";
    const string PropRoot = "Assets/_Game/Art/Field/Props";

    // ── 레이아웃 (m) ──────────────────────────────────────────────────
    const int   N      = 3;       // 블록 N×N, 가운데는 공원
    const float Block  = 36f;     // 블록 한 변(보도 포함)
    const float Road   = 12f;     // 도로 폭(왕복 4차선)
    const float Side   = 3f;      // 보도 폭
    const float Curb   = 0.3f;    // 연석 띠 폭
    const float Border = 8f;      // 맨 바깥 도로 밖 테두리(보도 + 여유)
    const float RoadY  = -0.12f;
    const float LineY  = RoadY + 0.006f;
    const int   Seed   = 20260930;

    static float W => N * Block + (N + 1) * Road;          // 도로 포함 도시 폭
    static float Half => W * 0.5f;
    static float RoadCenter(int k) => -Half + Road * 0.5f + k * (Block + Road);
    static float BlockMin(int i) => -Half + Road + i * (Block + Road);

    // 도로·보도는 월드 UV(m). 텍스처 한 장이 덮는 크기.
    const float TileAsphalt = 6f, TileSidewalk = 4f, TileGrass = 3.5f, TileRoof = 6f;
    // 파사드 텍스처 = 4칸 × 4층 → 한 장이 가로 4×3.0m, 세로 4×3.2m 를 덮는다. 층 높이와 텍스처가 딱 맞아야 창이 층마다 한 줄.
    const float Bay = 3.0f, Floor = 3.2f;

    static readonly List<Mesh> _meshes = new List<Mesh>();
    // 빌딩·주차장·컨테이너가 차지한 바닥(XZ). 웅덩이·풀·컨테이너를 뿌릴 때 피한다.
    static readonly List<Rect> _occupied = new List<Rect>();
    static readonly List<Rect> _parking = new List<Rect>();

    static bool Free(Rect r)
    {
        foreach (var o in _occupied) if (o.Overlaps(r)) return false;
        return true;
    }

    [MenuItem("Tools/좀비 오버런/도시 테스트 씬 생성")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(SrcScene, OpenSceneMode.Single);

        // ── 필드 걷어내기 (StreamFieldBuilder 와 같은 규칙) ──
        Transform props = null;
        foreach (var go in new List<GameObject>(scene.GetRootGameObjects()))
        {
            if (go == null) continue;
            if (IsTimestampName(go.name) || go.name == "test_water" || go.name == "_ShallowWater_Test") { Object.DestroyImmediate(go); continue; }
            if (!go.name.StartsWith("Field_")) continue;
            foreach (var child in new[] { "_Environment", "_RoadMarkings", "_Water", "_Bounds" })
            {
                var t = go.transform.Find(child);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            props = go.transform.Find("_Props");
            if (props != null) for (int i = props.childCount - 1; i >= 0; i--) Object.DestroyImmediate(props.GetChild(i).gameObject);
        }
        if (props == null) props = new GameObject("_Props").transform;   // ProtoObstacles 가 이름으로 찾는다
        var fog = GameObject.Find("_GroundFog");
        if (fog != null) fog.SetActive(false);   // 40×40 크기로 깔린 안개 — 도시가 커서 가운데만 뿌옇게 남는다

        var mats = EnsureMaterials();
        _meshes.Clear();
        var rng = new System.Random(Seed);
        var city = new GameObject("_City").transform;

        _occupied.Clear(); _parking.Clear(); _lampHeads.Clear(); _flickers.Clear();
        // 빌딩·컨테이너를 먼저 — 지면 스플랫이 건물 밑동·컨테이너 둘레의 흙을 계산할 때 그 자리를 쓴다
        BuildBlocks(city, props, mats, rng);
        BuildContainers(props, rng);
        BuildGround(city, mats);
        BuildMarkings(city, mats);
        BuildStreetLights(city, mats);
        BuildPuddles(city, rng);
        BuildFoliage(city, rng);
        BuildDecals(city);          // 피·타이어 자국 데칼 — 자기 시드·자기 메시 에셋(위 배치를 흔들지 않는다)
        BuildCracks(city);          // 도로 균열 — 같은 방식, 자기 시드·자기 메시 에셋
        SaveMeshes();

        if (Night) ApplyNight(city, mats);
        else SpawnFlickers(city, mats.lamp, null, null);

        city.gameObject.AddComponent<CityAmbience>();   // 가끔 헬기가 지나가는 소리

        var map = city.gameObject.AddComponent<CityMap>();
        map.halfSize = Half + Border;

        CityGrassBaker.Bake(city, map.halfSize);   // 지면이 칠한 풀 자리에 풀잎 — 지면·프롭이 다 놓인 뒤에

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, DstScene);
        Debug.Log($"[도시] {DstScene} 저장. 도시 {W:F0}m (+테두리 {Border:F0}m), 블록 {N}×{N}, 메시 {_meshes.Count}개.");
    }

    // ══ 지면 ═══════════════════════════════════════════════════════════
    // 도로면은 도시 전체를 덮는 한 장(y -0.12). 블록·테두리는 그 위에 올라온 판(윗면 + 옆면 = 연석 턱).
    static void BuildGround(Transform city, Mats m)
    {
        var mb = new MB(4) { cover = GroundCover };   // 0 아스팔트, 1 보도, 2 연석(밝은 콘크리트), 3 공원 잔디
        float o = Half + Border;
        mb.GridXZ(0, -o, -o, o, o, RoadY, TileAsphalt, 2f);

        // 바깥 테두리 — 네 변의 판. 가장자리가 허공이 아니라 보도로 끝나게.
        // **판을 이동 경계(테두리)보다 50m 더 밖까지 편다.** 카메라가 캐릭터 앞뒤로 10m 넘게 보므로, 테두리에서 판이 끝나면
        // 세상 끝(허공)이 화면에 걸렸다(컨테이너 확인 샷에서 실제로 보였다).
        float far = o + 50f;
        Slab(mb, -far, -far, far, -Half, 1, TileSidewalk);
        Slab(mb, -far, Half, far, far, 1, TileSidewalk);
        Slab(mb, -far, -Half, -Half, Half, 1, TileSidewalk);
        Slab(mb, Half, -Half, far, Half, 1, TileSidewalk);
        CurbRing(mb, -Half, -Half, Half, Half, true);

        for (int bz = 0; bz < N; bz++)
            for (int bx = 0; bx < N; bx++)
            {
                float x0 = BlockMin(bx), z0 = BlockMin(bz), x1 = x0 + Block, z1 = z0 + Block;
                bool park = bx == N / 2 && bz == N / 2;
                // 보도 링(바깥 Side m) + 안쪽 대지. 공원은 대지가 잔디.
                SideFaces(mb, x0, z0, x1, z1, 2);
                mb.GridXZ(1, x0, z0, x1, z0 + Side, 0f, TileSidewalk, 1.5f);
                mb.GridXZ(1, x0, z1 - Side, x1, z1, 0f, TileSidewalk, 1.5f);
                mb.GridXZ(1, x0, z0 + Side, x0 + Side, z1 - Side, 0f, TileSidewalk, 1.5f);
                mb.GridXZ(1, x1 - Side, z0 + Side, x1, z1 - Side, 0f, TileSidewalk, 1.5f);
                // 대지 — 공원도 바닥은 보도로 깔고, 잔디는 그 위에 화단처럼 얹는다
                mb.GridXZ(1, x0 + Side, z0 + Side, x1 - Side, z1 - Side, 0f, TileSidewalk, 1.5f);
                CurbRing(mb, x0, z0, x1, z1, false);
                if (park)
                {
                    // 잔디 4장 — 각각 보도 띠(ParkBand) 안쪽에 연석 테두리 + 잔디. 사용자 요청: "풀 테두리에 보도".
                    foreach (var q in ParkQuadrants())
                    {
                        Rect edge = Shrink(q, ParkBand), grass = Shrink(edge, Curb);
                        LawnCurb(mb, edge);
                        mb.GridXZ(3, grass.xMin, grass.yMin, grass.xMax, grass.yMax, 0.003f, TileGrass, 1.5f);
                    }
                }
            }
        Spawn(city, "Ground", mb.Build("CityGround"), m.asphalt, m.sidewalk, m.curb, m.grass);
    }

    // ── 공원 ──
    // 십자 산책로(반폭 ParkPath) 로 나뉜 사분면 4개. 예전엔 가운데 10m 광장이 잔디 안쪽 모서리를 덮어 잔디가 ㄱ자였다 —
    // 테두리를 두르려면 직사각형이어야 해서 광장 대신 산책로를 넓혔다(가운데 교차부가 곧 작은 광장).
    const float ParkPath = 2.4f, ParkBand = 1.2f;

    static Rect[] ParkQuadrants()
    {
        float x0 = BlockMin(N / 2) + Side, z0 = BlockMin(N / 2) + Side, x1 = BlockMin(N / 2) + Block - Side, z1 = BlockMin(N / 2) + Block - Side;
        float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
        return new[]
        {
            Rect.MinMaxRect(x0, z0, cx - ParkPath, cz - ParkPath), Rect.MinMaxRect(cx + ParkPath, z0, x1, cz - ParkPath),
            Rect.MinMaxRect(x0, cz + ParkPath, cx - ParkPath, z1), Rect.MinMaxRect(cx + ParkPath, cz + ParkPath, x1, z1),
        };
    }

    /// 공원 잔디(연석 안쪽) 사각형 4개 — 나무·풀 카드는 여기에만.
    static Rect[] ParkGrass()
    {
        var q = ParkQuadrants();
        for (int i = 0; i < q.Length; i++) q[i] = Shrink(Shrink(q[i], ParkBand), Curb);
        return q;
    }

    static Rect Shrink(Rect r, float d) { return Rect.MinMaxRect(r.xMin + d, r.yMin + d, r.xMax - d, r.yMax - d); }

    // 화단 연석 높이. 바닥과 같은 높이의 띠였을 땐 바닥 무늬처럼 납작해 보여서, 도로 연석 턱(-RoadY)만큼 올렸다.
    const float LawnCurbH = 0.12f;

    /// 잔디 둘레 연석(사각형 안쪽으로 Curb 폭) — 윗면 + 바깥(보도 쪽)·안쪽(잔디 쪽) 옆면.
    static void LawnCurb(MB mb, Rect r)
    {
        float c = Curb, y = LawnCurbH;
        mb.QuadXZ(2, r.xMin, r.yMin, r.xMax, r.yMin + c, y, 1f);
        mb.QuadXZ(2, r.xMin, r.yMax - c, r.xMax, r.yMax, y, 1f);
        mb.QuadXZ(2, r.xMin, r.yMin + c, r.xMin + c, r.yMax - c, y, 1f);
        mb.QuadXZ(2, r.xMax - c, r.yMin + c, r.xMax, r.yMax - c, y, 1f);
        // 바깥 옆면 — 반시계로 돌아 바깥을 본다(SideFaces 와 같은 순서)
        mb.Wall(2, new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), 0f, y, 1f, 1f);
        mb.Wall(2, new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMax, 0, r.yMax), 0f, y, 1f, 1f);
        mb.Wall(2, new Vector3(r.xMax, 0, r.yMax), new Vector3(r.xMin, 0, r.yMax), 0f, y, 1f, 1f);
        mb.Wall(2, new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMin, 0, r.yMin), 0f, y, 1f, 1f);
        // 안쪽 옆면 — 시계로 돌아 잔디 쪽을 본다. 밑변은 잔디 높이(0.003).
        Rect g = Shrink(r, c);
        mb.Wall(2, new Vector3(g.xMin, 0, g.yMin), new Vector3(g.xMin, 0, g.yMax), 0.003f, y, 1f, 1f);
        mb.Wall(2, new Vector3(g.xMin, 0, g.yMax), new Vector3(g.xMax, 0, g.yMax), 0.003f, y, 1f, 1f);
        mb.Wall(2, new Vector3(g.xMax, 0, g.yMax), new Vector3(g.xMax, 0, g.yMin), 0.003f, y, 1f, 1f);
        mb.Wall(2, new Vector3(g.xMax, 0, g.yMin), new Vector3(g.xMin, 0, g.yMin), 0.003f, y, 1f, 1f);
    }

    /// 도로 위로 올라온 판(윗면 + 네 옆면).
    static void Slab(MB mb, float x0, float z0, float x1, float z1, int topSub, float tile)
    {
        // 도시 밖 먼 테두리는 3m, 가까운 곳은 1.5m 격자(스플랫 덮임 해상도)
        float far = Mathf.Max(Mathf.Abs(x0), Mathf.Abs(x1), Mathf.Abs(z0), Mathf.Abs(z1));
        mb.GridXZ(topSub, x0, z0, x1, z1, 0f, tile, far > Half + Border + 1f ? 3f : 1.5f);
        SideFaces(mb, x0, z0, x1, z1, 2);
    }

    /// 판의 옆면 네 장(y RoadY → 0). 이게 연석의 '턱'이다 — 그림자와 밝은 옆면이 경계를 그린다.
    static void SideFaces(MB mb, float x0, float z0, float x1, float z1, int sub)
    {
        mb.Wall(sub, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), RoadY, 0f, 1f, 1f);   // 남쪽 면(-Z 를 봄)
        mb.Wall(sub, new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), RoadY, 0f, 1f, 1f);   // 동
        mb.Wall(sub, new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), RoadY, 0f, 1f, 1f);   // 북
        mb.Wall(sub, new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), RoadY, 0f, 1f, 1f);   // 서
    }

    /// 판 가장자리의 연석 띠(밝은 콘크리트, 보도 위 1mm). inward=false 면 판 안쪽으로, true 면 바깥 테두리용(도시 밖→안쪽 가장자리).
    static void CurbRing(MB mb, float x0, float z0, float x1, float z1, bool outerFrame)
    {
        float c = Curb, y = 0.001f;
        if (outerFrame) { x0 -= c; z0 -= c; x1 += c; z1 += c; }   // 테두리 판의 안쪽 가장자리 = 도시 바깥선 바로 밖
        mb.QuadXZ(2, x0, z0, x1, z0 + c, y, 1f);
        mb.QuadXZ(2, x0, z1 - c, x1, z1, y, 1f);
        mb.QuadXZ(2, x0, z0 + c, x0 + c, z1 - c, y, 1f);
        mb.QuadXZ(2, x1 - c, z0 + c, x1, z1 - c, y, 1f);
    }

    // ══ 노면 표시 ═══════════════════════════════════════════════════════
    // 우측 통행. 도로 k 는 교차로 사이 구간마다: 가운데 황색 복선, 차로 흰 점선, 양끝 횡단보도, 진입 쪽 정지선.
    static void BuildMarkings(Transform city, Mats m)
    {
        var mb = new MB(2) { cover = GroundCover };   // 0 흰색, 1 황색 — 노면 표시도 같은 흙·얼룩을 뒤집어쓴다
        const float cross = 3.0f, crossGap = 0.6f, stopW = 0.4f;
        for (int k = 0; k <= N; k++)
            for (int j = 0; j < N; j++)
            {
                float c = RoadCenter(k);
                float a = RoadCenter(j) + Road * 0.5f, b = RoadCenter(j + 1) - Road * 0.5f;   // 구간(교차로 사이)
                float la = a + crossGap + cross + 1.0f, lb = b - crossGap - cross - 1.0f;       // 선이 그려지는 구간
                for (int axis = 0; axis < 2; axis++)
                {
                    bool alongZ = axis == 0;   // 0 = 남북 도로(x = c), 1 = 동서 도로(z = c)
                    // 가운데 황색 복선
                    Strip(mb, 1, alongZ, c - 0.22f, c - 0.10f, la, lb);
                    Strip(mb, 1, alongZ, c + 0.10f, c + 0.22f, la, lb);
                    // 차로 흰 점선 (가운데와 가장자리 사이)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float lx = c + s * Road * 0.25f;
                        for (float t = la; t < lb - 0.5f; t += 5f) Strip(mb, 0, alongZ, lx - 0.08f, lx + 0.08f, t, Mathf.Min(t + 2.5f, lb));
                    }
                    // 횡단보도 — 구간 양끝. 줄무늬는 차 진행 방향과 나란하다.
                    for (int end = 0; end < 2; end++)
                    {
                        float t0 = end == 0 ? a + crossGap : b - crossGap - cross;
                        for (float w = c - Road * 0.5f + 0.6f; w + 0.5f <= c + Road * 0.5f - 0.4f; w += 1.0f)
                            Strip(mb, 0, alongZ, w, w + 0.5f, t0, t0 + cross);
                    }
                    // 정지선 — 진입 차로 쪽 반만. 남북: 북행(동쪽 반)은 북쪽 끝, 남행(서쪽 반)은 남쪽 끝.
                    // 동서: 동행(남쪽 반 = 진행 방향 오른쪽)은 동쪽 끝, 서행(북쪽 반)은 서쪽 끝.
                    float sEnd = b - crossGap - cross - 0.3f - stopW, sStart = a + crossGap + cross + 0.3f;
                    if (alongZ)
                    {
                        Strip(mb, 0, true, c, c + Road * 0.5f - 0.3f, sEnd, sEnd + stopW);
                        Strip(mb, 0, true, c - Road * 0.5f + 0.3f, c, sStart, sStart + stopW);
                    }
                    else
                    {
                        Strip(mb, 0, false, c - Road * 0.5f + 0.3f, c, sEnd, sEnd + stopW);
                        Strip(mb, 0, false, c, c + Road * 0.5f - 0.3f, sStart, sStart + stopW);
                    }
                }
            }
        Spawn(city, "RoadMarkings", mb.Build("CityMarkings"), m.lineWhite, m.lineYellow).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// 노면 띠. alongZ 면 x 폭 [w0,w1] × z 길이 [t0,t1], 아니면 축을 바꾼다.
    static void Strip(MB mb, int sub, bool alongZ, float w0, float w1, float t0, float t1)
    {
        if (t1 <= t0) return;
        if (alongZ) mb.QuadXZ(sub, w0, t0, w1, t1, LineY, 1f);
        else        mb.QuadXZ(sub, t0, w0, t1, w1, LineY, 1f);
    }

    // ══ 가로등 ═════════════════════════════════════════════════════════
    // 블록 네 귀퉁이 + 변마다 가운데. 충돌 없음(작고, _Props 밖) — 한 메시로 합친다.
    // 모델 = streetlamp_01.fbx(04_city_03_icon 의 Object573·574 를 떼어낸 것, 기수(팔) +Z, 서브메시 0 기둥 · 1 등).
    // 모델이 없으면 예전 상자 가로등으로 짓는다.
    const string LampModel = PropRoot + "/streetlamp_01/streetlamp_01.fbx";
    // 전부 켜져 있으면 인공적이다 — 일부만 켜고 몇 개는 깜박인다(고정 시드라 다시 지어도 같다). 꺼진 등은 M_City_LampOff.
    const float LampOnRatio = 0.5f;
    const int   LampFlickerCount = 2;
    enum LampState { Off, On, Flicker }

    static LampState[] LampStates(int count)
    {
        var rng = new System.Random(Seed + 77);
        var s = new LampState[count];
        for (int i = 0; i < count; i++) s[i] = rng.NextDouble() < LampOnRatio ? LampState.On : LampState.Off;
        for (int n = 0, guard = 0; n < LampFlickerCount && guard < 100; guard++)
        {
            int i = rng.Next(count);
            if (s[i] == LampState.Flicker) continue;
            s[i] = LampState.Flicker; n++;
        }
        return s;
    }

    // 깜박이는 등 — 각자 따로 흔들려면 렌더러가 따로여야 해서 전구만 떼어 낸 메시와 머리 위치를 남긴다(CombineLamps 가 채운다)
    static readonly List<(Mesh bulb, Vector3 head)> _flickers = new List<(Mesh, Vector3)>();

    /// 깜박이는 등 오브젝트: _City/StreetLights_Flicker/Flicker_n (전구 메시 + ProtoLampFlicker) + 자식 빛 웅덩이.
    /// 빛 웅덩이는 _LightPools 밖에 둔다 — 시작 연출(ProtoIntro)이 _LightPools 를 통째로 MPB 로 덮어쓰기 때문.
    static void SpawnFlickers(Transform city, Material lamp, Material poolMat, Mesh discMesh)
    {
        var old = city.Find("StreetLights_Flicker");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        if (_flickers.Count == 0) return;
        var root = new GameObject("StreetLights_Flicker").transform;
        root.SetParent(city, false);
        var emis = lamp.HasProperty("_EmissionColor") ? lamp.GetColor("_EmissionColor") : new Color(1.6f, 1.25f, 0.72f);
        float poolI = poolMat != null && poolMat.HasProperty("_Intensity") ? poolMat.GetFloat("_Intensity") : 0.35f;
        for (int i = 0; i < _flickers.Count; i++)
        {
            var (bulb, head) = _flickers[i];
            var go = new GameObject($"Flicker_{i}");
            go.transform.SetParent(root, false);
            go.transform.position = head;                       // 전구 메시 정점은 머리 기준
            go.AddComponent<MeshFilter>().sharedMesh = bulb;
            var br = go.AddComponent<MeshRenderer>(); br.sharedMaterial = lamp;
            var f = go.AddComponent<ProtoLampFlicker>();
            f.bulb = br; f.emission = emis; f.poolIntensity = poolI; f.seed = 1000 + i * 37;

            if (poolMat == null || discMesh == null) continue;
            var pool = new GameObject("LightPool");
            pool.transform.SetParent(go.transform, false);
            pool.transform.position = new Vector3(head.x, 0.02f, head.z);
            pool.transform.localScale = new Vector3(8f, 1f, 8f);
            pool.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var pr = pool.AddComponent<MeshRenderer>();
            pr.sharedMaterial = poolMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            f.pool = pr;
        }
    }

    /// 꺼진 전구 재질 — M_City_Lamp 를 복제해 발광만 끈다(툰 설정은 같게). 큐는 기본(어둠 막 아래로 같이 어두워진다).
    static Material EnsureLampOff(Material lamp)
    {
        string p = $"{MatDir}/M_City_LampOff.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(lamp) { name = "M_City_LampOff" }; AssetDatabase.CreateAsset(m, p); }
        m.SetFloat("_UseEmission", 0f); m.DisableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.SetColor("_BaseColor", new Color(0.42f, 0.41f, 0.38f));
        m.renderQueue = -1;
        EditorUtility.SetDirty(m);
        return m;
    }

    static List<(Vector3 p, Vector3 dir)> LampSpots()
    {
        var spots = new List<(Vector3, Vector3)>();
        for (int bz = 0; bz < N; bz++)
            for (int bx = 0; bx < N; bx++)
            {
                float x0 = BlockMin(bx) + 0.8f, z0 = BlockMin(bz) + 0.8f, x1 = BlockMin(bx) + Block - 0.8f, z1 = BlockMin(bz) + Block - 0.8f;
                float mx = (x0 + x1) * 0.5f, mz = (z0 + z1) * 0.5f;
                // (위치, 팔 방향 = 도로 쪽)
                spots.Add((new Vector3(x0, 0, z0), new Vector3(-1, 0, -1).normalized));
                spots.Add((new Vector3(x1, 0, z0), new Vector3(1, 0, -1).normalized));
                spots.Add((new Vector3(x0, 0, z1), new Vector3(-1, 0, 1).normalized));
                spots.Add((new Vector3(x1, 0, z1), new Vector3(1, 0, 1).normalized));
                spots.Add((new Vector3(mx, 0, z0), Vector3.back));
                spots.Add((new Vector3(mx, 0, z1), Vector3.forward));
                spots.Add((new Vector3(x0, 0, mz), Vector3.left));
                spots.Add((new Vector3(x1, 0, mz), Vector3.right));
            }
        return spots;
    }

    static void BuildStreetLights(Transform city, Mats m)
    {
        if (LampModelMesh(out var parts))
        {
            var mesh = CombineLamps(parts);
            _meshes.Add(mesh);
            foreach (var f in _flickers) _meshes.Add(f.bulb);   // 깜박이는 등 오브젝트는 밤 설정(ApplyNight)이 빛 웅덩이와 같이 만든다
            Spawn(city, "StreetLights", mesh, m.metal, m.lamp, EnsureLampOff(m.lamp));
            return;
        }
        var mb = new MB(2);   // 0 금속 기둥, 1 등(밝은 색) — 예전 상자 가로등은 전부 켠다
        foreach (var (p, dir) in LampSpots()) Lamp(mb, p, dir);
        Spawn(city, "StreetLights", mb.Build("CityLights"), m.metal, m.lamp);
    }

    /// 모델의 메시와 모델 루트 기준 행렬(FBX 노드 회전 포함).
    static bool LampModelMesh(out (Mesh mesh, Matrix4x4 local) parts)
    {
        parts = default;
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(LampModel);
        var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
        if (mf == null || mf.sharedMesh == null) return false;
        // 메시가 하나뿐인 FBX 는 루트 자신이 메시를 들고 있고, 축 변환 회전(-90°X)도 루트에 붙는다 — 루트 회전·배율까지 넣는다
        var root = Matrix4x4.TRS(Vector3.zero, go.transform.localRotation, go.transform.localScale);
        parts = (mf.sharedMesh, root * go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix);
        return true;
    }

    /// 자리마다 모델을 놓아 한 메시로 합친다 — 서브메시 0 기둥 · 1 켜진 전구 · 2 꺼진 전구.
    /// 켜진 등의 머리 위치만 _lampHeads 에 남긴다(빛 웅덩이·어둠 막 구멍은 켜진 등에만).
    /// 깜박이는 등은 기둥만 여기 넣고 전구는 따로 떼어 _flickers 에 남긴다.
    static Mesh CombineLamps((Mesh mesh, Matrix4x4 local) model)
    {
        var src = model.mesh;
        Vector3 head = model.local.MultiplyPoint3x4(SubMeshCenter(src, 1));
        var spots = LampSpots();
        var state = LampStates(spots.Count);
        _flickers.Clear();
        var pole = new List<CombineInstance>(); var lit = new List<CombineInstance>(); var off = new List<CombineInstance>();
        for (int i = 0; i < spots.Count; i++)
        {
            var place = Matrix4x4.TRS(spots[i].p, Quaternion.LookRotation(spots[i].dir), Vector3.one);
            var m = place * model.local;
            pole.Add(new CombineInstance { mesh = src, subMeshIndex = 0, transform = m });
            Vector3 h = place.MultiplyPoint3x4(head);
            if (state[i] == LampState.Flicker)
            {
                var bulb = new Mesh { name = $"CityLampFlicker_{_flickers.Count}" };
                bulb.CombineMeshes(new[] { new CombineInstance { mesh = src, subMeshIndex = 1, transform = Matrix4x4.Translate(-h) * m } }, true, true);
                _flickers.Add((bulb, h));
                continue;
            }
            (state[i] == LampState.On ? lit : off).Add(new CombineInstance { mesh = src, subMeshIndex = 1, transform = m });
            if (state[i] == LampState.On) _lampHeads.Add(h);
        }
        var parts = new List<CombineInstance>();
        foreach (var list in new[] { pole, lit, off })
        {
            var part = new Mesh();
            part.CombineMeshes(list.ToArray(), true, true);   // 비어 있어도 서브메시 자리는 지킨다
            parts.Add(new CombineInstance { mesh = part });
        }
        var mesh = new Mesh { name = "CityLights" };
        mesh.CombineMeshes(parts.ToArray(), false, false);
        mesh.RecalculateBounds();
        foreach (var c in parts) Object.DestroyImmediate(c.mesh);
        return mesh;
    }

    static Vector3 SubMeshCenter(Mesh mesh, int sub)
    {
        var verts = mesh.vertices; var tris = mesh.GetTriangles(sub);
        var b = new Bounds(verts[tris[0]], Vector3.zero);
        foreach (int i in tris) b.Encapsulate(verts[i]);
        return b.center;
    }

    /// 지금 열린 도시 씬의 가로등만 새 모델로 바꾼다(씬 전체를 다시 짓지 않는다 — 씬에서 손본 것 보존).
    /// 빛 웅덩이(_LightPools)와 어둠 막 구멍(ProtoNightOverlay.lamps)도 새 등 머리 위치로 옮긴다.
    [MenuItem("Tools/좀비 오버런/도시 가로등 교체 (streetlamp_01)")]
    public static void ReplaceStreetLights()
    {
        var city = GameObject.Find("_City");
        var lights = city != null ? city.transform.Find("StreetLights") : null;
        if (lights == null) { Debug.LogWarning("[도시] _City/StreetLights 가 없다 — 도시 씬을 열고 실행"); return; }
        if (!LampModelMesh(out var parts)) { Debug.LogWarning($"[도시] {LampModel} 이 없다"); return; }

        _lampHeads.Clear();
        var mesh = CombineLamps(parts);
        const string asset = CityRoot + "/CityStreetLamps.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(asset) != null) AssetDatabase.DeleteAsset(asset);
        AssetDatabase.CreateAsset(mesh, asset);
        foreach (var f in _flickers) AssetDatabase.AddObjectToAsset(f.bulb, asset);
        // 예전 상자 가로등 메시(CityMeshes.asset 의 하위 에셋)는 이제 아무도 안 쓴다 — 지운다
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshAsset))
            if (o is Mesh old && old.name == "CityLights") { AssetDatabase.RemoveObjectFromAsset(old); Object.DestroyImmediate(old, true); }
        var mr = lights.GetComponent<MeshRenderer>();
        var mats = mr.sharedMaterials;
        var lampOff = EnsureLampOff(mats.Length > 1 ? mats[1] : AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/M_City_Lamp.mat"));
        AssetDatabase.SaveAssets();
        lights.GetComponent<MeshFilter>().sharedMesh = mesh;
        mr.sharedMaterials = new[] { mats[0], mats[1], lampOff };

        // 빛 웅덩이: 켜진 등 수만큼 옮기고 나머지는 끈다(지우지 않는다 — 비율을 올리면 다시 켜진다)
        var pools = city.transform.Find("_LightPools");
        var ground = new List<Vector3>();
        for (int i = 0; i < _lampHeads.Count; i++) ground.Add(new Vector3(_lampHeads[i].x, 0f, _lampHeads[i].z));
        if (pools != null)
            for (int i = 0; i < pools.childCount; i++)
            {
                var pool = pools.GetChild(i).gameObject;
                pool.SetActive(i < ground.Count);
                if (i < ground.Count) pool.transform.position = ground[i] + Vector3.up * 0.02f;
            }
        // 깜박이는 등 — 빛 웅덩이 모양·재질은 기존 웅덩이를 따른다
        var tmpl = pools != null && pools.childCount > 0 ? pools.GetChild(0) : null;
        SpawnFlickers(city.transform, mats[1],
            tmpl != null ? tmpl.GetComponent<MeshRenderer>().sharedMaterial : null,
            tmpl != null ? tmpl.GetComponent<MeshFilter>().sharedMesh : null);

        var cam = Camera.main;
        var ov = cam != null ? cam.GetComponent<ProtoNightOverlay>() : null;
        if (ov != null) { Undo.RecordObject(ov, "가로등 교체"); ov.lamps = ground.ToArray(); EditorUtility.SetDirty(ov); }

        EditorSceneManager.MarkSceneDirty(lights.gameObject.scene);
        Debug.Log($"[도시] 가로등 {LampSpots().Count}개를 streetlamp_01 로 교체 — 켜짐 {_lampHeads.Count} · 깜박임 {_flickers.Count} · 나머지 꺼짐.");
    }

    static readonly List<Vector3> _lampHeads = new List<Vector3>();   // 등 머리 위치 — 밤 설정이 빛 웅덩이를 깐다

    static void Lamp(MB mb, Vector3 p, Vector3 dir)
    {
        const float h = 5.2f, t = 0.09f;
        _lampHeads.Add(p + dir * 1.4f + Vector3.up * (h - 0.2f));
        mb.Box(0, 0, p + new Vector3(-t, 0, -t), p + new Vector3(t, h, t), 1f);
        Vector3 tip = p + dir * 1.4f;
        Vector3 a = Vector3.Min(p, tip), b = Vector3.Max(p, tip);
        mb.Box(0, 0, new Vector3(a.x - 0.05f, h - 0.1f, a.z - 0.05f), new Vector3(b.x + 0.05f, h, b.z + 0.05f), 1f);
        mb.Box(1, 1, tip + new Vector3(-0.28f, h - 0.3f, -0.18f), tip + new Vector3(0.28f, h - 0.08f, 0.18f), 1f);
    }

    // ══ 블록 채우기: 빌딩 · 주차장 · 공원 · 가로수 · 주차된 차 ═════════
    enum Kind { Glass, Office, Brick, White }

    static void BuildBlocks(Transform city, Transform props, Mats m, System.Random rng)
    {
        var treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/Tree_Conifer.obj");
        var vanPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/Van_Abandoned.obj");
        var treeMat = AssetDatabase.LoadAssetAtPath<Material>($"{FieldMat}/M_Tree_Conifer.mat");
        var vanMat  = AssetDatabase.LoadAssetAtPath<Material>($"{FieldMat}/M_Van_Abandoned.mat");
        var gBuild = Group(props, "Building_City");
        var gTree  = Group(props, "Tree_Conifer");     // 이름이 ProtoObstacles 축소율(줄기만 0.32) 키다 — 바꾸지 말 것
        var gVan   = Group(props, "Van_Abandoned");
        var lots = new MB(2) { cover = ParkingCover };   // 주차장: 0 아스팔트, 1 흰 선 (둘 다 스플랫 머티리얼 — 정점 컬러 필수)
        int bi = 0;

        for (int bz = 0; bz < N; bz++)
            for (int bx = 0; bx < N; bx++)
            {
                float x0 = BlockMin(bx) + Side, z0 = BlockMin(bz) + Side, x1 = BlockMin(bx) + Block - Side, z1 = BlockMin(bz) + Block - Side;
                bool park = bx == N / 2 && bz == N / 2;
                if (park)
                {
                    // 잔디(연석 테두리 포함)는 차지한 자리 — 웅덩이가 연석에 걸쳐 앉지 않게(실제로 걸쳤다)
                    foreach (var q in ParkQuadrants()) _occupied.Add(Shrink(q, ParkBand - 0.2f));
                    // 공원 — 잔디 4장마다 나무 3~4그루(연석에서 1.2m 안쪽)
                    foreach (var gr in ParkGrass())
                        for (int n = 0; n < 4; n++)
                        {
                            if (n == 3 && rng.NextDouble() < 0.5) continue;
                            float px = Mathf.Lerp(gr.xMin + 1.2f, gr.xMax - 1.2f, (float)rng.NextDouble());
                            float pz = Mathf.Lerp(gr.yMin + 1.2f, gr.yMax - 1.2f, (float)rng.NextDouble());
                            PlaceProp(gTree, treePrefab, treeMat, new Vector3(px, 0, pz), (float)rng.NextDouble() * 360f, 2.0f + (float)rng.NextDouble() * 0.6f);
                        }
                    continue;
                }

                // 대지를 2×2 필지로 나눠 필지마다 빌딩(가끔 주차장)
                float gap = 1.5f, lw = (x1 - x0 - gap) * 0.5f, ld = (z1 - z0 - gap) * 0.5f;
                bool parkingDone = false;
                for (int q = 0; q < 4; q++)
                {
                    float lx0 = x0 + (q % 2) * (lw + gap), lz0 = z0 + (q / 2) * (ld + gap);
                    if (!parkingDone && rng.NextDouble() < 0.18)
                    {
                        // 목록에 먼저 넣어야 ParkingCover 가 이 주차장 가장자리를 안다
                        _parking.Add(Rect.MinMaxRect(lx0, lz0, lx0 + lw, lz0 + ld));
                        ParkingLot(lots, lx0, lz0, lx0 + lw, lz0 + ld);
                        parkingDone = true;
                        continue;
                    }
                    // 두 필지를 합쳐 큰 빌딩 하나 — 레퍼런스처럼 크기가 섞이게
                    Kind kind = PickKind(bx, bz, rng);
                    float inset = 0.6f + (float)rng.NextDouble() * 1.2f;
                    var go = MakeBuilding(gBuild, $"Building_{bi++:D2}_{kind}", kind, lx0 + inset, lz0 + inset, lx0 + lw - inset, lz0 + ld - inset, m, rng);
                    go.isStatic = true;
                    _occupied.Add(Rect.MinMaxRect(lx0 + inset, lz0 + inset, lx0 + lw - inset, lz0 + ld - inset));
                }

                // 가로수 — 보도 위, 연석에서 1.2m 안쪽, 10m 간격(귀퉁이 비움)
                float sx0 = BlockMin(bx), sz0 = BlockMin(bz), sx1 = sx0 + Block, sz1 = sz0 + Block;
                for (float t = sx0 + 7f; t < sx1 - 6f; t += 10f)
                {
                    PlaceProp(gTree, treePrefab, treeMat, new Vector3(t, 0, sz0 + 1.2f), (float)rng.NextDouble() * 360f, 1.6f);
                    PlaceProp(gTree, treePrefab, treeMat, new Vector3(t, 0, sz1 - 1.2f), (float)rng.NextDouble() * 360f, 1.6f);
                }

                // 주차된 차 — 블록 한 변의 바깥 차로에 가끔(연석에 붙여 도로 방향으로)
                if (rng.NextDouble() < 0.6)
                {
                    float vx = sx0 + 8f + (float)rng.NextDouble() * (Block - 16f);
                    PlaceProp(gVan, vanPrefab, vanMat, new Vector3(vx, RoadY, sz0 - 1.8f), rng.NextDouble() < 0.5 ? 90f : 270f, 2.634f);
                    _occupied.Add(Rect.MinMaxRect(vx - 3f, sz0 - 3.2f, vx + 3f, sz0 - 0.4f));
                }
            }
        if (lots.HasAny) Spawn(city, "ParkingLots", lots.Build("CityParking"), m.asphalt, m.lineWhite);
    }

    // 가운데(공원 옆) 블록일수록 높은 유리 빌딩, 귀퉁이 블록은 낮은 벽돌·흰 건물이 많다 — 도심 스카이라인
    static Kind PickKind(int bx, int bz, System.Random rng)
    {
        bool corner = (bx == 0 || bx == N - 1) && (bz == 0 || bz == N - 1);
        double r = rng.NextDouble();
        if (corner) return r < 0.15 ? Kind.Glass : r < 0.45 ? Kind.Office : r < 0.75 ? Kind.Brick : Kind.White;
        return r < 0.45 ? Kind.Glass : r < 0.75 ? Kind.Office : r < 0.88 ? Kind.White : Kind.Brick;
    }

    static void ParkingLot(MB mb, float x0, float z0, float x1, float z1)
    {
        mb.QuadXZ(0, x0, z0, x1, z1, 0.002f, TileAsphalt);
        // 주차 칸 — 가로 2.5m 간격 흰 선, 양쪽 줄
        float mid = (z0 + z1) * 0.5f;
        for (float x = x0 + 1f; x <= x1 - 1f; x += 2.5f)
        {
            mb.QuadXZ(1, x - 0.06f, z0 + 0.8f, x + 0.06f, mid - 1.8f, 0.004f, 1f);
            mb.QuadXZ(1, x - 0.06f, mid + 1.8f, x + 0.06f, z1 - 0.8f, 0.004f, 1f);
        }
    }

    /// 빌딩 한 채 — 층층이 쌓인 상자(셋백) + 옥상 난간 + 실외기. 서브메시 0 = 벽(파사드), 1 = 지붕류.
    static GameObject MakeBuilding(Transform parent, string name, Kind kind, float x0, float z0, float x1, float z1, Mats m, System.Random rng)
    {
        int floors, tiers;
        switch (kind)
        {
            case Kind.Glass:  floors = 12 + rng.Next(11); tiers = 2 + rng.Next(2); break;
            case Kind.Office: floors = 6 + rng.Next(7);   tiers = 1 + rng.Next(2); break;
            case Kind.White:  floors = 4 + rng.Next(5);   tiers = 1 + rng.Next(2); break;
            default:          floors = 3 + rng.Next(4);   tiers = 1; break;
        }
        var mb = new MB(2);
        float y = 0f;
        Vector2 c = new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f);
        Vector2 half = new Vector2((x1 - x0) * 0.5f, (z1 - z0) * 0.5f);
        int left = floors;
        for (int t = 0; t < tiers; t++)
        {
            // 아래층이 가장 많고 위로 갈수록 적다. 마지막 단에 남은 층 전부.
            int f = t == tiers - 1 ? left : Mathf.Max(2, Mathf.RoundToInt(left * (0.55f + (float)rng.NextDouble() * 0.15f)));
            left -= f;
            float h = f * Floor;
            Vector3 a = new Vector3(c.x - half.x, y, c.y - half.y), b = new Vector3(c.x + half.x, y + h, c.y + half.y);
            mb.Box(0, 1, a, b, 0f);   // 벽 = 파사드(층 UV), 윗면 = 지붕
            // 셋백 테라스 난간 / 옥상 난간
            Parapet(mb, a.x, a.z, b.x, b.z, b.y);
            y += h;
            if (left <= 0) break;
            // 다음 단은 한 변씩 불규칙하게 들여서(가운데 고정이 아니라) 레퍼런스처럼 비대칭 셋백
            float sx = half.x * (0.12f + (float)rng.NextDouble() * 0.18f), sz = half.y * (0.12f + (float)rng.NextDouble() * 0.18f);
            c += new Vector2(((float)rng.NextDouble() - 0.5f) * sx, ((float)rng.NextDouble() - 0.5f) * sz);
            half -= new Vector2(sx, sz);
        }
        // 옥상 실외기·계단실
        int units = 1 + rng.Next(3);
        for (int u = 0; u < units; u++)
        {
            float ux = c.x + ((float)rng.NextDouble() - 0.5f) * half.x * 1.2f, uz = c.y + ((float)rng.NextDouble() - 0.5f) * half.y * 1.2f;
            float uw = 0.8f + (float)rng.NextDouble() * 1.4f, ud = 0.8f + (float)rng.NextDouble() * 1.2f, uh = 0.8f + (float)rng.NextDouble() * 1.8f;
            mb.Box(1, 1, new Vector3(ux - uw * 0.5f, y, uz - ud * 0.5f), new Vector3(ux + uw * 0.5f, y + uh, uz + ud * 0.5f), 1f);
        }

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mb.Build(name);
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { m.Facade(kind), m.roof };
        return go;
    }

    static void Parapet(MB mb, float x0, float z0, float x1, float z1, float top)
    {
        const float th = 0.3f, h = 0.6f;
        mb.Box(1, 1, new Vector3(x0, top, z0), new Vector3(x1, top + h, z0 + th), 1f);
        mb.Box(1, 1, new Vector3(x0, top, z1 - th), new Vector3(x1, top + h, z1), 1f);
        mb.Box(1, 1, new Vector3(x0, top, z0 + th), new Vector3(x0 + th, top + h, z1 - th), 1f);
        mb.Box(1, 1, new Vector3(x1 - th, top, z0 + th), new Vector3(x1, top + h, z1 - th), 1f);
    }

    /// 나무·차 — 스트리머와 같은 규칙: 모델 루트 회전은 살리고 yaw 만 얹고, 밑면을 y 에 앉힌다.
    static void PlaceProp(Transform group, GameObject prefab, Material mat, Vector3 pos, float yaw, float scale)
    {
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
        go.transform.position = pos;
        if (mat != null) foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = mat;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
        }
        go.isStatic = true;
    }

    static Transform Group(Transform props, string name)
    {
        var t = props.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(props, false); }
        return t;
    }

    static GameObject Spawn(Transform parent, string name, Mesh mesh, params Material[] mats)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        go.isStatic = true;
        return go;
    }

    // ══ 컨테이너 ═══════════════════════════════════════════════════════
    // 도시 바깥 테두리(보도 8m)를 따라 드문드문 + 주차장 한쪽. 폐허 도시에 방치된 화물 느낌.
    // 스트리머와 같은 배율(1.2)·모델(container_01). 그룹 이름 "Container" — 스트리머와 같게.
    static void BuildContainers(Transform props, System.Random rng)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PropRoot}/container_01/container_01.fbx");
        var mat    = AssetDatabase.LoadAssetAtPath<Material>($"{PropRoot}/container_01/container_01.mat");
        if (prefab == null) { Debug.LogWarning("[도시] container_01 이 없다 — 컨테이너 생략"); return; }
        var g = Group(props, "Container");
        float mid = Half + Border * 0.5f;   // 테두리 띠 한가운데
        // 네 변마다 3~4개. 방향은 변과 나란히(가끔 비스듬히) — 벽처럼 줄 세우면 지루하다.
        for (int side = 0; side < 4; side++)
        {
            int count = 3 + rng.Next(2);
            for (int i = 0; i < count; i++)
            {
                float t = -Half + 10f + (float)rng.NextDouble() * (W - 20f);
                Vector3 p = side == 0 ? new Vector3(t, 0, -mid) : side == 1 ? new Vector3(t, 0, mid)
                          : side == 2 ? new Vector3(-mid, 0, t) : new Vector3(mid, 0, t);
                float yaw = (side < 2 ? 90f : 0f) + (rng.NextDouble() < 0.3 ? (float)rng.NextDouble() * 40f - 20f : 0f) + (rng.NextDouble() < 0.5 ? 180f : 0f);
                var r = Rect.MinMaxRect(p.x - 3.2f, p.z - 3.2f, p.x + 3.2f, p.z + 3.2f);
                if (!Free(r)) continue;
                PlaceProp(g, prefab, mat, p, yaw, 1.2f);
                _occupied.Add(r);
            }
        }
        // 주차장마다 절반 확률로 하나(주차 칸 위, 한쪽 줄)
        foreach (var lot in _parking)
        {
            if (rng.NextDouble() < 0.5) continue;
            var p = new Vector3(lot.center.x + ((float)rng.NextDouble() - 0.5f) * 4f, 0.002f, lot.yMin + 3f);
            var r = Rect.MinMaxRect(p.x - 3.2f, p.z - 2f, p.x + 3.2f, p.z + 2f);
            if (!Free(r)) continue;
            PlaceProp(g, prefab, mat, p, 90f + (float)rng.NextDouble() * 10f - 5f, 1.2f);
            _occupied.Add(r);
        }
    }

    // ══ 얕은 물 웅덩이 ═════════════════════════════════════════════════
    // 스트리밍 필드와 같은 디스크 메시·물 머티리얼(M_Water_Shallow_Test). 수면이 바닥보다 3~8cm 위 = 수심.
    // 캐릭터(y 0)가 보도·공원 웅덩이를 밟으면 발목까지 잠겨 파문(WaterContactFoam)이 난다. 도로 웅덩이는 도로가 -0.12 라
    // 수면이 발밑이다 — 비친 물로만 보인다. 충돌 없음(_Props 밖).
    static void BuildPuddles(Transform city, System.Random rng)
    {
        const string WaterTest = "Assets/_Game/Art/Field/Water/test";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{WaterTest}/ShallowPond_Test.asset");
        var mat  = AssetDatabase.LoadAssetAtPath<Material>($"{WaterTest}/M_Water_Shallow_Test.mat");
        if (mesh == null || mat == null) { Debug.LogWarning("[도시] 얕은 물 메시/머티리얼 없음 — 얕은 물 테스트 메뉴를 한 번 실행할 것. 웅덩이 생략"); return; }
        var root = new GameObject("_Puddles").transform;
        root.SetParent(city, false);
        const float MeshRadius = 1.7f;
        int placed = 0;
        for (int tries = 0; tries < 200 && placed < 16; tries++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * (Half + Border * 0.6f);
            float z = ((float)rng.NextDouble() * 2f - 1f) * (Half + Border * 0.6f);
            float r = 0.7f + (float)rng.NextDouble() * 1.1f;          // 반경 1.2~3.1m
            float rad = r * MeshRadius;
            if (!Free(Rect.MinMaxRect(x - rad, z - rad, x + rad, z + rad))) continue;
            bool onRoad = IsRoad(x, z);
            // 연석을 가로지르면 물이 턱 위에 떠 보인다 — 웅덩이 전체가 한 면(도로 또는 보도) 안에 있어야 한다
            if (IsRoad(x - rad, z) != onRoad || IsRoad(x + rad, z) != onRoad || IsRoad(x, z - rad) != onRoad || IsRoad(x, z + rad) != onRoad) continue;
            float depth = 0.03f + (float)rng.NextDouble() * 0.05f;
            var go = new GameObject($"Puddle_{placed:D2}");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, (onRoad ? RoadY : 0f) + depth, z);
            go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            go.transform.localScale = new Vector3(r * (0.8f + (float)rng.NextDouble() * 0.5f), 1f, r);   // 살짝 타원
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _occupied.Add(Rect.MinMaxRect(x - rad, z - rad, x + rad, z + rad));
            placed++;
        }
    }

    // ══ 바닥 데칼 (피 얼룩·타이어 자국) ═════════════════════════════════
    // 텍스처 5종: 사용자 원본 dithering_01.png + 절차 생성 decal_blood_A~D(ArtSource/Decals/make_blood_decals.py).
    // 데칼마다 하나를 고른다(원본 30%, 나머지 각 17.5%). 그림 선택은 별도 난수라 위치·크기·진하기 배치는 그대로다.
    // 도로 위주로, 보도·테두리에 조금.
    // **한 데칼이 연석을 걸치면 안 된다**(도로 -0.12 / 보도 0 단차에서 반쪽이 뜬다) — 네 귀퉁이가 같은 면이어야 한다.
    // 건물·컨테이너·웅덩이(_occupied)는 피하고, 데칼끼리 너무 붙지 않게. 전부 한 메시 = 한 번에 그린다.
    // 셰이더 Proto/GroundDecal(알파 블렌드 + 밝기 0.85). 밤은 어둠 막이 덮는다.
    static readonly string[] DecalTex =
    {
        "Assets/_Game/Art/Field/Textures/Ground/dithering_01.png",
        "Assets/_Game/Art/Field/Textures/Ground/decal_blood_A.png",
        "Assets/_Game/Art/Field/Textures/Ground/decal_blood_B.png",
        "Assets/_Game/Art/Field/Textures/Ground/decal_blood_C.png",
        "Assets/_Game/Art/Field/Textures/Ground/decal_blood_D.png",
    };
    static readonly float[] DecalWeight = { 0.30f, 0.175f, 0.175f, 0.175f, 0.175f };
    const string DecalMeshAsset = CityRoot + "/CityDecals.asset";
    const int DecalCount = 46, DecalSeed = 20261002;

    [MenuItem("Tools/좀비 오버런/도시 데칼 배치 (피·타이어)")]
    public static void BuildDecalsMenu()
    {
        var city = GameObject.Find("_City");
        if (city == null) { EditorUtility.DisplayDialog("도시 데칼", "열린 씬에 _City 가 없다 — Proto_City_Test 를 열고 실행", "확인"); return; }
        // 씬 재생성 없이 지금 씬에 깐다: 피할 자리(_occupied)는 씬의 건물·컨테이너·웅덩이 바운즈로 다시 만든다
        _occupied.Clear();
        var props = GameObject.Find("_Props");
        if (props != null) foreach (var r in props.GetComponentsInChildren<Renderer>()) AddOccupied(r.bounds, 0.4f);
        var pud = city.transform.Find("_Puddles");
        if (pud != null) foreach (var r in pud.GetComponentsInChildren<Renderer>()) AddOccupied(r.bounds, 0.2f);
        BuildDecals(city.transform);
        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log("[도시] 데칼 배치 완료 — 씬 저장(Ctrl+S) 필요");
    }

    static void AddOccupied(Bounds b, float pad)
    {
        _occupied.Add(Rect.MinMaxRect(b.min.x - pad, b.min.z - pad, b.max.x + pad, b.max.z + pad));
    }

    static void BuildDecals(Transform city)
    {
        var shader = Shader.Find("Proto/GroundDecal");
        if (shader == null) { Debug.LogWarning("[도시] Proto/GroundDecal 셰이더 없음 — 데칼 생략"); return; }
        // 텍스처마다 머티리얼 하나(서브메시 하나). 없는 텍스처는 건너뛴다
        var mats = new List<Material>(); var kinds = new List<int>();
        for (int t = 0; t < DecalTex.Length; t++)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DecalTex[t]);
            if (tex == null) continue;
            // 텍스처 임포트: 알파 그대로, 가장자리 반복 금지(Clamp) — Repeat 면 반대편 얼룩이 테두리에 비친다
            var ti = (TextureImporter)AssetImporter.GetAtPath(DecalTex[t]);
            if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp))
            {
                ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.anisoLevel = 4;
                ti.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DecalTex[t]);
            }
            string mp = t == 0 ? $"{MatDir}/M_City_Decal_Blood.mat" : $"{MatDir}/M_City_Decal_{System.IO.Path.GetFileNameWithoutExtension(DecalTex[t])}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, mp); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(mat);
            mats.Add(mat); kinds.Add(t);
        }
        if (mats.Count == 0) { Debug.LogWarning("[도시] 데칼 텍스처 없음 — 데칼 생략"); return; }
        var pick = new System.Random(DecalSeed + 1);
        float wsum = 0f; foreach (var k in kinds) wsum += DecalWeight[k];

        var old = city.Find("_Decals");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rng = new System.Random(DecalSeed);
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color32>();
        var tris = new List<int>[mats.Count]; for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();
        var placed = new List<Vector3>();
        float lim = Half + Border * 0.7f;
        int n = 0;
        for (int tries = 0; tries < 3000 && n < DecalCount; tries++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * lim;
            float z = ((float)rng.NextDouble() * 2f - 1f) * lim;
            bool onRoad = IsRoad(x, z);
            // 도로 70% · 보도 30% 쯤 — 보도 후보는 절반을 버린다
            if (!onRoad && rng.NextDouble() < 0.55) continue;
            float size = onRoad ? 4f + (float)rng.NextDouble() * 4f : 3f + (float)rng.NextDouble() * 2f;   // 도로 4~8m, 보도 3~5m(차로 12m 에 비해 작으면 점처럼 보였다)
            float h = size * 0.5f;
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
            var corner = new Vector2[4];
            Vector2[] local = { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(-h, h), new Vector2(h, h) };
            bool ok = true;
            float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
            for (int k = 0; k < 4 && ok; k++)
            {
                var l = local[k];
                corner[k] = new Vector2(x + l.x * c - l.y * sn, z + l.x * sn + l.y * c);
                if (IsRoad(corner[k].x, corner[k].y) != onRoad) ok = false;   // 연석 걸침 금지
                minX = Mathf.Min(minX, corner[k].x); maxX = Mathf.Max(maxX, corner[k].x);
                minZ = Mathf.Min(minZ, corner[k].y); maxZ = Mathf.Max(maxZ, corner[k].y);
            }
            if (!ok) continue;
            // 그림 실제 부분은 가운데에 몰려 있어 바운즈를 70% 로 줄여 겹침을 본다
            float sh = (maxX - minX) * 0.15f;
            if (!Free(Rect.MinMaxRect(minX + sh, minZ + sh, maxX - sh, maxZ - sh))) continue;
            bool near = false;
            foreach (var q in placed) if ((new Vector2(q.x - x, q.z - z)).sqrMagnitude < (q.y + h) * (q.y + h) * 0.8f) { near = true; break; }
            if (near) continue;
            placed.Add(new Vector3(x, h, z));

            float y = (onRoad ? RoadY : 0f) + 0.012f;   // 차선(+0.006) 위, 바닥과 z-파이팅 없는 높이
            bool flip = rng.NextDouble() < 0.5;          // 좌우 뒤집어 같은 그림이 덜 반복돼 보이게
            // 진하기 3단(사용자: "전부 같은 opacity — 어떤 건 덜 보이게"): 오래돼 바랜 것 40% · 중간 25% · 생생한 것 35%.
            // 72~100% 한 범위로 흔들었을 땐 차이가 눈에 안 띄었다.
            double roll = rng.NextDouble(), lv = rng.NextDouble();
            float op = roll < 0.40 ? Mathf.Lerp(0.25f, 0.45f, (float)lv)
                     : roll < 0.65 ? Mathf.Lerp(0.50f, 0.70f, (float)lv)
                                   : Mathf.Lerp(0.85f, 1.00f, (float)lv);
            byte a = (byte)(op * 255f);
            int b = verts.Count;
            for (int k = 0; k < 4; k++)
            {
                verts.Add(new Vector3(corner[k].x, y, corner[k].y));
                float u = (k % 2 == 0) ? 0f : 1f; float v = k < 2 ? 0f : 1f;
                uvs.Add(new Vector2(flip ? 1f - u : u, v));
                cols.Add(new Color32(255, 255, 255, a));
            }
            // 그림 고르기(가중치)
            float r0 = (float)pick.NextDouble() * wsum; int sub = mats.Count - 1;
            for (int i = 0; i < kinds.Count; i++) { r0 -= DecalWeight[kinds[i]]; if (r0 <= 0f) { sub = i; break; } }
            tris[sub].AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
            n++;
        }

        // 자기 에셋에 따로 저장 — CityMeshes.asset 은 씬 생성 때 통째로 지웠다 다시 만든다.
        // **기존 에셋이 있으면 그 메시에 데이터를 직접 다시 넣는다.** CopySerialized 로 덮으면 CPU 데이터만 바뀌고
        // GPU 에 올라간 메시는 예전 것으로 남아, 화면엔 지난 배치가 그려졌다(실제로 보도 데칼이 통째로 안 보였다).
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DecalMeshAsset);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "City_Decals";
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols);
        mesh.subMeshCount = mats.Count;
        for (int i = 0; i < mats.Count; i++) mesh.SetTriangles(tris[i], i);
        var nrm = new Vector3[verts.Count]; for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up; mesh.normals = nrm;
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, DecalMeshAsset);
        else EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();

        var go = new GameObject("_Decals");
        go.transform.SetParent(city, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterials = mats.ToArray();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        var per = new System.Text.StringBuilder();
        for (int i = 0; i < mats.Count; i++) per.Append($" {System.IO.Path.GetFileNameWithoutExtension(DecalTex[kinds[i]])}={tris[i].Count / 6}");
        Debug.Log($"[도시] 데칼 {n}장 (시도 한도 3000) —{per}");
    }

    // ══ 도로 균열 ═══════════════════════════════════════════════════════
    // 사용자 원본 crack.png(검은 선 + 알파). 피 데칼과 같은 Proto/GroundDecal — 어둠 막(2900) 앞에 그려져 밤엔 같이 어두워진다.
    // (처음 손으로 깐 판은 BCM 투명 큐 3000 이라 막 뒤에 그려져 밤에 혼자 밝게 떴다.)
    // **도로에만**, 연석 걸침 금지. 실제 균열처럼 한 자리에 2~3장씩 뭉쳐(서로 다른 각도·크기로 이어져 그물처럼 보이게),
    // 뭉치 자리는 연석 가까이(가장자리 균열)·교차로를 더 자주 고른다. 진하기 2단(옅은 실금 / 선명한 균열).
    const string CrackTex = "Assets/_Game/Art/Field/Textures/Ground/crack.png";
    const string CrackMeshAsset = CityRoot + "/CityCracks.asset";
    const int CrackClusters = 30, CrackSeed = 20261026;

    [MenuItem("Tools/좀비 오버런/도시 도로 균열 배치")]
    public static void BuildCracksMenu()
    {
        var city = GameObject.Find("_City");
        if (city == null) { EditorUtility.DisplayDialog("도로 균열", "열린 씬에 _City 가 없다 — Proto_City_Test 를 열고 실행", "확인"); return; }
        _occupied.Clear();
        var props = GameObject.Find("_Props");
        if (props != null) foreach (var r in props.GetComponentsInChildren<Renderer>()) AddOccupied(r.bounds, 0.3f);
        var pud = city.transform.Find("_Puddles");
        if (pud != null) foreach (var r in pud.GetComponentsInChildren<Renderer>()) AddOccupied(r.bounds, 0.2f);
        BuildCracks(city.transform);
        EditorSceneManager.MarkSceneDirty(city.scene);
        Debug.Log("[도시] 도로 균열 배치 완료 — 씬 저장(Ctrl+S) 필요");
    }

    static void BuildCracks(Transform city)
    {
        var shader = Shader.Find("Proto/GroundDecal");
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CrackTex);
        if (shader == null || tex == null) { Debug.LogWarning("[도시] Proto/GroundDecal 셰이더 또는 crack.png 없음 — 균열 생략"); return; }
        var ti = (TextureImporter)AssetImporter.GetAtPath(CrackTex);
        // 원본 1024 그대로 — 모바일 Override 512 로 줄면 가는 선이 밉맵에서 끊겨 카메라 거리에서 거의 안 보였다
        bool small = false;
        if (ti != null) foreach (var p in new[] { "Android", "iPhone" }) { var ps = ti.GetPlatformTextureSettings(p); if (ps.overridden && ps.maxTextureSize < 1024) small = true; }
        if (ti != null && (!ti.alphaIsTransparency || ti.wrapMode != TextureWrapMode.Clamp || small))
        {
            ti.alphaIsTransparency = true; ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.anisoLevel = 4;
            foreach (var p in new[] { "Android", "iPhone" })
            {
                var ps = ti.GetPlatformTextureSettings(p);
                if (ps.overridden && ps.maxTextureSize < 1024) { ps.maxTextureSize = 1024; ti.SetPlatformTextureSettings(ps); }
            }
            ti.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CrackTex);
        }
        string mp = $"{MatDir}/M_City_Decal_Crack.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_Tint", Color.white);   // 10-29 crack.png 를 Higgsfield 로 다시 만들면서 색(아스팔트 조각·틈)이 텍스처에 들어갔다 — 예전 흑백 그림은 0.06 갈흑색으로 칠했다
        EditorUtility.SetDirty(mat);

        var old = city.Find("_Cracks");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rng = new System.Random(CrackSeed);
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color32>(); var tris = new List<int>();
        var placed = new List<Vector3>();   // (x, 반경, z)
        int clusters = 0, n = 0;
        for (int tries = 0; tries < 4000 && clusters < CrackClusters; tries++)
        {
            float x = ((float)rng.NextDouble() * 2f - 1f) * Half;
            float z = ((float)rng.NextDouble() * 2f - 1f) * Half;
            if (!IsRoad(x, z)) continue;
            // 연석 3.5m 안쪽이거나 교차로(두 방향 모두 도로)면 그대로, 차로 한가운데는 60% 버린다
            bool cross = IsRoad(x + Road, z) && IsRoad(x - Road, z) && IsRoad(x, z + Road) && IsRoad(x, z - Road);
            if (DistRoadToCurb(x, z) > 3.5f && !cross && rng.NextDouble() < 0.6) continue;
            bool near = false;
            foreach (var q in placed) if ((new Vector2(q.x - x, q.z - z)).sqrMagnitude < 14f * 14f) { near = true; break; }
            if (near) continue;

            int count = 1 + rng.Next(3);   // 뭉치당 1~3장
            float baseAng = (float)rng.NextDouble() * 360f;
            int got = 0;
            for (int k = 0; k < count; k++)
            {
                // 첫 장은 뭉치 중심, 다음 장은 1.5~3m 옆에서 각도를 70~150° 틀어 앞 균열 끝과 이어지게
                float off = k == 0 ? 0f : 2.5f + (float)rng.NextDouble() * 2f;
                float oa = (float)rng.NextDouble() * Mathf.PI * 2f;
                float cx = x + Mathf.Cos(oa) * off, cz = z + Mathf.Sin(oa) * off;
                // 3.5~6.5m 로 깔았을 땐 선이 머리카락처럼 가늘어 안 보였다 — 판을 키워 선도 굵게
                float size = k == 0 ? 6f + (float)rng.NextDouble() * 3.5f : 3.5f + (float)rng.NextDouble() * 2.5f;
                float ang = (baseAng + k * (70f + (float)rng.NextDouble() * 80f)) * Mathf.Deg2Rad;
                float stretch = 0.8f + (float)rng.NextDouble() * 0.45f;   // 살짝 길쭉하게 — 같은 그림이 덜 반복돼 보인다
                if (AddCrackQuad(verts, uvs, cols, tris, rng, cx, cz, size, stretch, ang)) got++;
            }
            if (got == 0) continue;
            placed.Add(new Vector3(x, 0f, z));
            clusters++; n += got;
        }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CrackMeshAsset);   // 기존 메시에 다시 넣는다(이유는 BuildDecals 주석)
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "City_Cracks";
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
        var nrm = new Vector3[verts.Count]; for (int i = 0; i < nrm.Length; i++) nrm[i] = Vector3.up; mesh.normals = nrm;
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, CrackMeshAsset);
        else EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();

        var go = new GameObject("_Cracks");
        go.transform.SetParent(city, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        Debug.Log($"[도시] 도로 균열 {clusters}곳 {n}장");
    }

    static bool AddCrackQuad(List<Vector3> verts, List<Vector2> uvs, List<Color32> cols, List<int> tris, System.Random rng,
                             float x, float z, float size, float stretch, float ang)
    {
        float hx = size * 0.5f * stretch, hz = size * 0.5f;
        float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
        Vector2[] local = { new Vector2(-hx, -hz), new Vector2(hx, -hz), new Vector2(-hx, hz), new Vector2(hx, hz) };
        var corner = new Vector2[4];
        float minX = 1e9f, maxX = -1e9f, minZ = 1e9f, maxZ = -1e9f;
        for (int k = 0; k < 4; k++)
        {
            var l = local[k];
            corner[k] = new Vector2(x + l.x * c - l.y * sn, z + l.x * sn + l.y * c);
            if (!IsRoad(corner[k].x, corner[k].y)) return false;   // 연석 걸침 금지
            minX = Mathf.Min(minX, corner[k].x); maxX = Mathf.Max(maxX, corner[k].x);
            minZ = Mathf.Min(minZ, corner[k].y); maxZ = Mathf.Max(maxZ, corner[k].y);
        }
        float sh = (maxX - minX) * 0.2f;   // 선은 판 일부만 채운다 — 바운즈를 줄여 웅덩이·프롭 겹침을 본다
        if (!Free(Rect.MinMaxRect(minX + sh, minZ + sh, maxX - sh, maxZ - sh))) return false;

        float y = RoadY + 0.009f;   // 차선(+0.006) 위, 피 데칼(+0.012) 아래
        bool flip = rng.NextDouble() < 0.5;
        // 옅은 실금 40% · 선명한 균열 60%
        float op = rng.NextDouble() < 0.4 ? Mathf.Lerp(0.50f, 0.70f, (float)rng.NextDouble()) : Mathf.Lerp(0.85f, 1.00f, (float)rng.NextDouble());
        byte a = (byte)(op * 255f);
        int b = verts.Count;
        for (int k = 0; k < 4; k++)
        {
            verts.Add(new Vector3(corner[k].x, y, corner[k].y));
            float u = (k % 2 == 0) ? 0f : 1f; float v = k < 2 ? 0f : 1f;
            uvs.Add(new Vector2(flip ? 1f - u : u, v));
            cols.Add(new Color32(255, 255, 255, a));
        }
        tris.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
        return true;
    }

    // ══ 지면 스플랫 덮임 (도시가 너무 깨끗해서) ══════════════════════════
    // 정점 컬러 = 층별 덮임(0.5 가 경계). 셰이더 순서: A(풀) 먼저 → R(얼룩·재) → G(잔해) → B(흙) 위로.
    // 방치된 도시처럼: 흙은 연석을 따라 쌓이고, 풀은 연석 바로 옆 틈에서 난다. 기름 얼룩·잔해는 드문드문.
    // **풀 자리에서는 흙을 누른다(min(흙, 1-풀))** — 흙 띠가 풀 띠보다 넓어, 순서상 위에 오는 흙이 풀을 다 덮어버린다.
    static Color32 GroundCover(Vector3 p) { return Cover(p, p.y < -0.05f ? 0 : 1); }
    static Color32 ParkingCover(Vector3 p) { return Cover(p, 2); }

    /// mode 0 도로 / 1 보도 / 2 주차장.
    /// <para>도로의 R 층은 재가 아니라 <b>밝은 자갈 먼지</b>다(머티리얼이 층 텍스처를 다르게 꽂는다) — 어두운 아스팔트 위의
    /// 재 얼룩은 명도가 같아 거의 안 보였다. 보도는 밝은 타일이라 재 얼룩이 잘 보여 그대로 둔다.</para>
    /// <para>보도의 건물 밑동 흙 띠는 좁게 — 건물 사이 틈이 1.5m 라 넓게 잡으면 틈 전체가 흙으로 칠해졌다.</para>
    static Color32 Cover(Vector3 p, int mode)
    {
        float x = p.x, z = p.z;
        float n1 = Mathf.PerlinNoise(x * 0.07f + 13.1f, z * 0.07f + 71.3f);    // 큰 얼룩
        float n2 = Mathf.PerlinNoise(x * 0.09f + 5.7f, z * 0.09f + 9.2f);      // 가장자리 요철 — 주기를 길게(0.23→0.09): 짧으면 흙·풀이 자잘한 얼룩으로 끊겼다
        float n3 = Mathf.PerlinNoise(x * 0.11f + 101.4f, z * 0.11f + 33.9f);   // 잔해 뭉치
        float e, dirtW, grassW, spotT, debT;   // e = 흙·풀이 붙는 가장자리까지 거리(m)
        if (mode == 0)      { e = DistRoadToCurb(x, z); dirtW = 2.4f; grassW = 0.8f; spotT = 0.58f; debT = 0.62f; }
        else if (mode == 1) { e = Mathf.Min(DistSidewalkToCurb(x, z) * 1.4f, DistToOccupied(x, z) * 2.2f); dirtW = 0.9f; grassW = 0.6f; spotT = 0.70f; debT = 0.72f; }
        else                { e = DistToParkingEdge(x, z); dirtW = 1.2f; grassW = 0.4f; spotT = 0.60f; debT = 0.64f; }
        float grass = 0.5f + (grassW - e) / grassW * 0.35f + (n2 - 0.5f) * 0.45f + (n1 - 0.5f) * 0.35f;
        // 흙은 **띠가 아니라 불규칙한 패치**. 예전엔 '연석에서 dirtW 이내'로 깔았는데, 풀 자리에서 흙을 빼다 보니
        // 풀 띠 바로 바깥에 연석과 나란한 갈색 줄무늬가 남았고 교차로 모퉁이를 따라 꺾여 인공적으로 보였다(사용자 지적).
        // 이제 큰 주기 노이즈가 흙 자리를 정하고, 연석 근처는 약한 가중치(+0.18 까지)만 받는다.
        // 주기 ~12m, 문턱을 높게 — 0.055/0.62 로 뒀더니 패치 하나가 교차로를 통째로 덮을 만큼 컸다.
        float nD = Mathf.PerlinNoise(x * 0.08f + 211.3f, z * 0.08f + 57.9f);
        float curbBias = Mathf.Max(0f, 1f - e / dirtW) * 0.15f;
        float dirt  = 0.5f + (nD - (mode == 1 ? 0.70f : 0.67f)) * 2.0f + curbBias + (n2 - 0.5f) * 0.2f;
        // 보도 얼룩은 작고 잦게(주기 ~3m). 도로처럼 큰 얼룩(n1, 주기 ~14m)을 쓰면 밝은 타일 위 검은 재가 큰 먹물 자국처럼 떴다.
        float nSpot = mode == 1 ? Mathf.PerlinNoise(x * 0.32f + 47.7f, z * 0.32f + 88.1f) : n1;
        float spot  = 0.5f + (nSpot - spotT) * 2.2f;   // 기울기 4.5→2.2: 얼룩이 툭 생기지 않고 서서히 진해진다
        float deb   = 0.5f + (n3 - debT) * 2.2f;
        grass = Mathf.Clamp01(grass);
        dirt = Mathf.Min(Mathf.Clamp01(dirt), 1f - grass);
        return new Color32(B(spot), B(deb), B(dirt), B(grass));
    }

    /// 주차장 안 점 → 그 주차장 가장자리까지.
    static float DistToParkingEdge(float x, float z)
    {
        foreach (var r in _parking)
            if (r.Contains(new Vector2(x, z))) return Mathf.Min(Mathf.Min(x - r.xMin, r.xMax - x), Mathf.Min(z - r.yMin, r.yMax - z));
        return 99f;
    }
    static byte B(float v) { return (byte)(Mathf.Clamp01(v) * 255f + 0.5f); }

    /// 도로 위 점 → 가장 가까운 연석(블록·테두리 판 가장자리)까지.
    static float DistRoadToCurb(float x, float z)
    {
        float d = Half - Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));   // 바깥 테두리
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                float x0 = BlockMin(i), z0 = BlockMin(j), x1 = x0 + Block, z1 = z0 + Block;
                float dx = Mathf.Max(x0 - x, 0f, x - x1), dz = Mathf.Max(z0 - z, 0f, z - z1);
                d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dz * dz));
            }
        return Mathf.Max(0f, d);
    }

    /// 보도(블록·테두리 판) 위 점 → 판 가장자리(연석)까지.
    static float DistSidewalkToCurb(float x, float z)
    {
        if (Mathf.Abs(x) > Half || Mathf.Abs(z) > Half) return Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) - Half;
        for (int i = 0; i < N; i++)
            for (int j = 0; j < N; j++)
            {
                float x0 = BlockMin(i), z0 = BlockMin(j), x1 = x0 + Block, z1 = z0 + Block;
                if (x >= x0 && x <= x1 && z >= z0 && z <= z1) return Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(z - z0, z1 - z));
            }
        return 0f;
    }

    /// 건물·주차장·컨테이너 밑동까지(밖에서). 없으면 큰 값.
    static float DistToOccupied(float x, float z)
    {
        float d = 99f;
        foreach (var r in _occupied)
        {
            float dx = Mathf.Max(r.xMin - x, 0f, x - r.xMax), dz = Mathf.Max(r.yMin - z, 0f, z - r.yMax);
            d = Mathf.Min(d, Mathf.Sqrt(dx * dx + dz * dz));
        }
        return d;
    }

    /// 도로면(y -0.12)인가. 블록·테두리 판이 아니면 도로.
    static bool IsRoad(float x, float z)
    {
        if (Mathf.Abs(x) > Half || Mathf.Abs(z) > Half) return false;   // 바깥 테두리 판
        for (int i = 0; i < N; i++)
        {
            float a = BlockMin(i);
            if (x >= a && x <= a + Block)
                for (int j = 0; j < N; j++) { float b = BlockMin(j); if (z >= b && z <= b + Block) return false; }
        }
        return true;
    }

    // ══ 풀 카드 ═════════════════════════════════════════════════════════
    // 스트리밍 필드의 풀 카드(T_Foliage_Atlas)를 그대로 쓴다 — 카메라 쪽 V 자 두 장, 윗변은 카메라 반대쪽(+Z)으로 25°,
    // 노멀은 전부 위(ProtoFieldStreamer.BuildFoliageTile 과 같은 규칙, 이유는 거기 주석).
    // 공원 잔디엔 빽빽하게, 건물 밑동·보도 안쪽 가장자리·테두리엔 잡초가 드문드문. 블록마다 메시를 나눠 화면 밖은 컬링된다.
    static void BuildFoliage(Transform city, System.Random rng)
    {
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Field/Foliage/M_Foliage.mat");
        if (baseMat == null) { Debug.LogWarning("[도시] M_Foliage 없음 — 스트리밍 씬에서 '풀 카드 머티리얼 연결'을 먼저. 풀 생략"); return; }
        // 도시 공원 잔디는 밝은 초록이라 마른 풀 카드가 샛노랗게 튄다 — 초록 쪽으로 누른 도시 전용 복제본
        string p = $"{MatDir}/M_City_Foliage.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null) { mat = new Material(baseMat) { name = "M_City_Foliage" }; AssetDatabase.CreateAsset(mat, p); }
        mat.SetTexture("_BaseMap", baseMat.GetTexture("_BaseMap"));
        mat.SetColor("_BaseColor", new Color(0.62f, 0.95f, 0.48f));
        EditorUtility.SetDirty(mat);

        var parkGrass = ParkGrass();
        var root = new GameObject("_Foliage").transform;
        root.SetParent(city, false);
        const float step = 0.7f;
        for (int bz = 0; bz < N; bz++)
            for (int bx = 0; bx < N; bx++)
            {
                var fb = new FoliageMB();
                float x0 = BlockMin(bx), z0 = BlockMin(bz), x1 = x0 + Block, z1 = z0 + Block;
                bool park = bx == N / 2 && bz == N / 2;
                float cx = (x0 + x1) * 0.5f, cz = (z0 + z1) * 0.5f;
                for (float z = z0 + Side; z < z1 - Side; z += step)
                    for (float x = x0 + Side; x < x1 - Side; x += step)
                    {
                        float px = x + (float)rng.NextDouble() * step, pz = z + (float)rng.NextDouble() * step;
                        float prob;
                        if (park)
                        {
                            // 산책로(십자 폭 1.6m + 광장 5m)는 비운다
                            // 잔디 4장 안에만(연석·보도 띠·산책로 비움)
                            bool onGrass = false;
                            foreach (var gr in parkGrass) if (gr.Contains(new Vector2(px, pz))) { onGrass = true; break; }
                            if (!onGrass) continue;
                            prob = 0.275f;   // 0.55 → 절반(사용자: "너무 많아", 10-27)
                        }
                        else
                        {
                            // 잡초 — 건물·주차장 가장자리 1m 띠에서만
                            if (!Free(Rect.MinMaxRect(px - 0.3f, pz - 0.3f, px + 0.3f, pz + 0.3f))) continue;
                            bool nearEdge = !Free(Rect.MinMaxRect(px - 1.2f, pz - 1.2f, px + 1.2f, pz + 1.2f)) || px < x0 + Side + 1f || px > x1 - Side - 1f || pz < z0 + Side + 1f || pz > z1 - Side - 1f;
                            prob = nearEdge ? 0.22f : 0.02f;
                        }
                        if (rng.NextDouble() >= prob) continue;
                        // 공원: 풀더미·들국화 위주 / 잡초: 넓은잎·풀더미
                        double k = rng.NextDouble();
                        int kind = park ? (k < 0.55 ? 0 : k < 0.72 ? 1 : k < 0.82 ? 2 : 3) : (k < 0.5 ? 2 : k < 0.9 ? 0 : 3);
                        // 공원은 아틀라스 윗줄(넓은잎·들국화) 초록 풀만 — 아랫줄 마른 풀·귀리는 크기만 빌린다(사용자 요청 10-27)
                        fb.Add(new Vector3(px, -0.03f, pz), kind, 0.8f + (float)rng.NextDouble() * 0.45f, rng, park);
                    }
                if (fb.Count == 0) continue;
                var go = Spawn(root, $"Foliage_{bx}_{bz}", fb.Build($"CityFoliage_{bx}_{bz}"), mat);
                var mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        // 바깥 테두리 — 연석 쪽 가장자리를 따라 잡초
        var edge = new FoliageMB();
        float o = Half + Border;
        for (float t = -o + 1f; t < o - 1f; t += 0.9f)
            for (int s = 0; s < 4; s++)
            {
                if (rng.NextDouble() > 0.35) continue;
                float d = Half + 0.6f + (float)rng.NextDouble() * (Border - 1.2f);
                Vector3 pos = s == 0 ? new Vector3(t, -0.03f, -d) : s == 1 ? new Vector3(t, -0.03f, d) : s == 2 ? new Vector3(-d, -0.03f, t) : new Vector3(d, -0.03f, t);
                if (Mathf.Abs(pos.x) < Half && Mathf.Abs(pos.z) < Half) continue;
                if (!Free(Rect.MinMaxRect(pos.x - 0.3f, pos.z - 0.3f, pos.x + 0.3f, pos.z + 0.3f))) continue;
                edge.Add(pos, rng.NextDouble() < 0.6 ? 0 : 2, 0.8f + (float)rng.NextDouble() * 0.4f, rng);
            }
        if (edge.Count > 0)
        {
            var go = Spawn(root, "Foliage_Border", edge.Build("CityFoliage_Border"), mat);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    /// 풀 카드 메시 조립. ProtoFieldStreamer.BuildFoliageTile 과 같은 카드 모양·아틀라스 칸.
    class FoliageMB
    {
        static readonly Vector2[] Cell = { new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f) };
        static readonly float[] Height = { 1.00f, 1.35f, 0.70f, 0.85f };   // 풀더미 · 귀리 · 넓은잎 · 들국화
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Vector3> n = new List<Vector3>(); readonly List<int> t = new List<int>();
        public int Count => v.Count / 8;

        public void Add(Vector3 basePos, int kind, float scale, System.Random rng, bool greenOnly = false)
        {
            float h = Height[kind] * scale, w = h, lean = Mathf.Tan(25f * Mathf.Deg2Rad);
            float spread = Mathf.Lerp(20f, 40f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
            float turn = ((float)rng.NextDouble() - 0.5f) * 20f * Mathf.Deg2Rad;
            bool flip0 = rng.NextDouble() < 0.5;
            Vector2 cell = Cell[kind];
            if (greenOnly) cell.y = 0.5f;
            // 윗줄 칸(넓은잎 · 들국화)은 크기를 줄인다 — 공원에선 귀리(키 1.35)·풀더미(1.0)가 들국화·넓은잎 칸으로 바뀌어
            // 1.3~1.7m 로 캐릭터만 했다(2026-11-12 사용자 "꽃의 사이즈를 좀 반으로" → "이 풀(넓은잎)도 반사이즈로").
            // 이미 구운 씬은 메뉴 도시 꽃 카드 / 꽃·넓은잎 반으로 줄이기(CityFoliageFlowers).
            if (CityFoliageFlowers.Shrinks(cell)) { h *= CityFoliageFlowers.Scale; w = h; }
            for (int side = 0; side < 2; side++)
            {
                float yaw = turn + (side == 0 ? spread : -spread);
                var right = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (w * 0.5f);
                var up = new Vector3(0f, h, h * lean);
                bool flip = (side == 1) ^ flip0;
                float u0 = cell.x + (flip ? 0.5f : 0f), u1 = cell.x + (flip ? 0f : 0.5f);
                int b = v.Count;
                v.Add(basePos - right); uv.Add(new Vector2(u0, cell.y));
                v.Add(basePos - right + up); uv.Add(new Vector2(u0, cell.y + 0.5f));
                v.Add(basePos + right + up); uv.Add(new Vector2(u1, cell.y + 0.5f));
                v.Add(basePos + right); uv.Add(new Vector2(u1, cell.y));
                for (int q = 0; q < 4; q++) n.Add(Vector3.up);
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }
    }

    // ══ 밤 ═════════════════════════════════════════════════════════════
    // 사용자 요청: 밤이라 어둡되, 다가오는 좀비는 보여야 하고, 캐릭터 둘레는 랜턴처럼 밝게(레퍼런스: 리니지 던전).
    // **조명이 아니라 화면 어둠 막(Proto/NightOverlay)으로 한다** — 사용자 결정. 처음엔 달빛+랜턴 포인트 라이트였는데
    // 캐릭터가 랜턴에 하얗게 떴고, 저사양 품질(Performant)은 추가 라이트가 꺼져 랜턴이 아예 없었다.
    //  · 조명·주변광·안개·볼륨은 원본 씬(낮, Field_Look) 그대로 — 캐릭터 색이 다른 씬과 같게
    //  · 막은 큐 2900: 배경·좀비는 어두워지고, 캐릭터(4001)·이펙트(3000+)는 막 위에 원래 색으로
    //  · 가로등 = 막에 작은 구멍 + 바닥 빛 웅덩이(가법, 큐 2990 이라 막 위에 따뜻하게 번진다)
    const bool Night = true;

    static void ApplyNight(Transform city, Mats m)
    {
        // 예전 방식(랜턴 포인트 라이트)이 씬에 남아 있으면 지운다
        var player = Object.FindFirstObjectByType<ProtoPlayer>();
        if (player != null)
        {
            var old = player.transform.Find("Lantern");
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // 가로등 — 바닥 빛 웅덩이 + 막 구멍 위치
        m.lamp.EnableKeyword("_EMISSION");
        m.lamp.SetColor("_EmissionColor", new Color(1.0f, 0.78f, 0.45f) * 1.6f);
        EditorUtility.SetDirty(m.lamp);
        var poolMat = EnsureMaterialAsset("M_City_LightPool", "Proto/LightPool", mm =>
        {
            mm.SetColor("_Color", new Color(1.0f, 0.70f, 0.38f));
            mm.SetFloat("_Intensity", 0.35f);
            mm.SetFloat("_Falloff", 2.2f);
        });
        var disc = new MB(1);
        // 단위 사각형. QuadXZ 는 UV 를 좌표로 찍으므로 -0.5~0.5 가 나온다 → +0.5 해서 0~1(셰이더가 중심에서 원으로 감쇠)
        disc.QuadXZ(0, -0.5f, -0.5f, 0.5f, 0.5f, 0f, 1f);
        var discMesh = disc.Build("LightPoolDisc");
        var uvs = new List<Vector2>(); discMesh.GetUVs(0, uvs);
        for (int i = 0; i < uvs.Count; i++) uvs[i] += new Vector2(0.5f, 0.5f);
        discMesh.SetUVs(0, uvs);
        var pools = new GameObject("_LightPools").transform;
        pools.SetParent(city, false);
        var lampGround = new List<Vector3>();
        foreach (var h in _lampHeads)
        {
            var g = new Vector3(h.x, 0f, h.z);
            lampGround.Add(g);
            var go = new GameObject("LightPool");
            go.transform.SetParent(pools, false);
            go.transform.position = g + Vector3.up * 0.02f;
            go.transform.localScale = new Vector3(8f, 1f, 8f);
            go.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = poolMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }
        SpawnFlickers(city, m.lamp, poolMat, discMesh);

        // 어둠 막 — 메인 카메라에
        var overlayMat = EnsureMaterialAsset("M_City_NightOverlay", "Proto/NightOverlay", mm =>
        {
            mm.SetColor("_DarkColor", new Color(0.01f, 0.015f, 0.045f));
            mm.SetFloat("_Darkness", 0.92f);   // 0.75·0.85 는 해질녘 같았다. 0.95 는 사용자가 "너무 어둡다"고 해서 되돌림
            mm.SetColor("_WarmColor", new Color(1.0f, 0.72f, 0.42f));
            mm.SetFloat("_Warmth", 0.05f);
        });
        var cam = Camera.main;
        if (cam != null)
        {
            var ov = cam.GetComponent<ProtoNightOverlay>() ?? cam.gameObject.AddComponent<ProtoNightOverlay>();
            ov.overlayMaterial = overlayMat;
            ov.lamps = lampGround.ToArray();
            ov.playerRadius = 3.4f;   // 씬에 직렬화된 값이 클래스 기본값을 덮으므로 명시
            ov.playerEdge = 0.6f;
            ov.lampStrength = 0.4f;
            EditorUtility.SetDirty(ov);
        }
    }

    static Material EnsureMaterialAsset(string name, string shader, System.Action<Material> setup)
    {
        string p = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (mat == null) { mat = new Material(Shader.Find(shader)) { name = name }; AssetDatabase.CreateAsset(mat, p); }
        setup(mat);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ══ 메시 저장 ═══════════════════════════════════════════════════════
    // 코드로 만든 메시는 에셋으로 저장해야 씬에 남는다. 한 파일에 전부 넣는다(첫 메시가 주 에셋, 나머지는 하위 에셋).
    static void SaveMeshes()
    {
        if (!AssetDatabase.IsValidFolder(CityRoot)) AssetDatabase.CreateFolder("Assets/_Game/Art", "City");
        if (AssetDatabase.LoadAssetAtPath<Object>(MeshAsset) != null) AssetDatabase.DeleteAsset(MeshAsset);
        if (_meshes.Count == 0) return;
        AssetDatabase.CreateAsset(_meshes[0], MeshAsset);
        for (int i = 1; i < _meshes.Count; i++) AssetDatabase.AddObjectToAsset(_meshes[i], MeshAsset);
        AssetDatabase.SaveAssets();
    }

    // ══ 머티리얼 ════════════════════════════════════════════════════════
    class Mats
    {
        public Material asphalt, sidewalk, curb, grass, lineWhite, lineYellow, metal, lamp, roof;
        public Material glass, office, brick, white;
        public Material Facade(Kind k) => k == Kind.Glass ? glass : k == Kind.Office ? office : k == Kind.Brick ? brick : white;
    }

    /// 지면류는 기존 지면(M_Ground_Debris) 툰 설정을, 빌딩류는 기존 건물(M_Building_Apartment, 투시·페이드 표시 포함) 설정을 복제한다.
    /// 이미 있으면 텍스처·색만 다시 물린다(인스펙터에서 만진 조명 값은 유지).
    static Mats EnsureMaterials()
    {
        if (!AssetDatabase.IsValidFolder(CityRoot)) AssetDatabase.CreateFolder("Assets/_Game/Art", "City");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder(CityRoot, "Materials");
        var groundT = AssetDatabase.LoadAssetAtPath<Material>($"{FieldMat}/M_Ground_Debris.mat");
        var bldT    = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Field/Props/Buildings/M_Building_Apartment.mat") ?? groundT;
        var grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Field/Textures/Ground/Ground_Grass.jpg");

        var m = new Mats();
        m.asphalt   = Mat("M_City_Asphalt",   groundT, Tex("T_City_Asphalt"),  new Color(0.95f, 0.95f, 0.97f), false);
        m.sidewalk  = Mat("M_City_Sidewalk",  groundT, Tex("T_City_Sidewalk"), Color.white, false);
        m.curb      = Mat("M_City_Curb",      groundT, null, new Color(0.86f, 0.85f, 0.82f), false);
        m.grass     = Mat("M_City_Grass",     groundT, grassTex, new Color(0.62f, 0.90f, 0.42f), false);   // 레퍼런스처럼 밝은 초록
        m.lineWhite = Mat("M_City_LineWhite", groundT, null, new Color(0.96f, 0.96f, 0.94f), false);
        m.lineYellow= Mat("M_City_LineYellow",groundT, null, new Color(0.98f, 0.78f, 0.18f), false);
        m.metal     = Mat("M_City_Metal",     groundT, null, new Color(0.30f, 0.32f, 0.35f), false);
        m.lamp      = Mat("M_City_Lamp",      groundT, null, new Color(1.00f, 0.95f, 0.75f), false);
        m.roof      = Mat("M_City_Roof",      bldT, Tex("T_City_Roof"),         Color.white, true);
        m.glass     = Mat("M_City_FacadeGlass",  bldT, Tex("T_City_FacadeGlass"),  Color.white, true);
        m.office    = Mat("M_City_FacadeOffice", bldT, Tex("T_City_FacadeOffice"), Color.white, true);
        m.brick     = Mat("M_City_FacadeBrick",  bldT, Tex("T_City_FacadeBrick"),  Color.white, true);
        m.white     = Mat("M_City_FacadeWhite",  bldT, Tex("T_City_FacadeWhite"),  Color.white, true);

        // 스플랫: 도로·보도·노면 표시에 흙·풀·얼룩을 섞는다(메시 정점 컬러 = GroundCover).
        // 노면 표시는 색을 _BaseColor 가 아니라 단색 텍스처로 줘야 한다 — _BaseColor 는 섞인 결과 전체에 곱해져 흙까지 노래진다.
        m.lineWhite.SetColor("_BaseColor", Color.white);
        m.lineYellow.SetColor("_BaseColor", Color.white);
        // 도로류(아스팔트·노면 표시)는 R 층이 밝은 자갈 먼지, 보도는 재 얼룩 — Cover() 주석 참고
        Splat(m.asphalt,   null, TileAsphalt, "Ground_Gravel.png");
        Splat(m.sidewalk,  null, TileSidewalk, "Ground_Ash.png");
        Splat(m.lineWhite, SolidTex("T_City_SolidWhite", new Color(0.96f, 0.96f, 0.94f)), 1f, "Ground_Gravel.png");
        Splat(m.lineYellow, SolidTex("T_City_SolidYellow", new Color(0.98f, 0.78f, 0.18f)), 1f, "Ground_Gravel.png");
        AssetDatabase.SaveAssets();
        return m;
    }

    const string GroundTex = "Assets/_Game/Art/Field/Textures/Ground";

    /// BCM_GROUND_SPLAT 켜기. 층: R 얼룩(spotTex) · G 잔해 · B 흙 · A 풀(초록 틴트). 반복 주기는 스트리밍 필드와 같게.
    static void Splat(Material m, Texture baseOverride, float baseTile, string spotTex)
    {
        if (baseOverride != null) m.SetTexture("_BaseMap", baseOverride);
        m.SetFloat("_UseGroundSplat", 1f); m.EnableKeyword("BCM_GROUND_SPLAT");
        m.SetTexture("_SplatTex1", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTex}/{spotTex}"));
        m.SetTexture("_SplatTex2", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTex}/Ground_Debris.png"));
        m.SetTexture("_SplatTex3", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTex}/Ground_DirtPath.png"));
        m.SetTexture("_SplatTex4", AssetDatabase.LoadAssetAtPath<Texture2D>($"{GroundTex}/Ground_Grass.jpg"));
        m.SetVector("_SplatTiles", new Vector4(baseTile, 4.9f, 3.4f, 2.3f));
        m.SetFloat("_SplatTile4", 3.5f);
        m.SetColor("_SplatTint4", new Color(0.62f, 0.90f, 0.42f));   // 공원 잔디와 같은 초록
        m.SetFloat("_SplatSharpness", 0.35f);   // 아주 넓은 그라데이션(사용자: "얼룩 말고 부드럽게" 두 번)
        m.SetFloat("_SplatHeightBlend", 0.05f); // 알갱이 점박이 거의 없앰
        m.SetFloat("_SplatNoise", 0.06f);       // 경계 톱니 거의 없앰
        m.SetFloat("_SplatNoiseScale", 1f);
        // **층별 최대 불투명도.** 가장자리를 아무리 넓혀도 한가운데가 100% 덮이면 결국 '진한 얼룩'으로 읽혔다.
        // 제일 진한 곳도 바탕이 비치게 해서 수채 번짐처럼 만든다. 풀만 좀 더 진하게(연석 틈 풀은 또렷해야 읽힌다).
        m.SetVector("_SplatStrength", new Vector4(0.45f, 0.45f, 0.5f, 0.85f));
        EditorUtility.SetDirty(m);
    }

    /// 4×4 단색 텍스처 에셋(노면 표시 바탕색용). 있으면 그대로 쓴다.
    static Texture2D SolidTex(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder(TexDir)) AssetDatabase.CreateFolder(CityRoot, "Textures");
        string p = $"{TexDir}/{name}.png";
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        if (t != null) return t;
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = c;
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(p, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(p);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static Texture2D Tex(string name)
    {
        string p = $"{TexDir}/{name}.png";
        var ti = AssetImporter.GetAtPath(p) as TextureImporter;
        if (ti != null && (ti.maxTextureSize != 1024 || ti.wrapMode != TextureWrapMode.Repeat))
        {
            ti.maxTextureSize = 1024;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static Material Mat(string name, Material template, Texture tex, Color color, bool occluder)
    {
        string p = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = template != null ? new Material(template) : new Material(Shader.Find("BCM/BCM_Toony_shader"));
            m.name = name;
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", Vector2.one);   // UV 는 메시가 월드 m 단위로 이미 들고 있다
        m.SetTextureOffset("_BaseMap", Vector2.zero);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_UseGroundSplat", 0f); m.DisableKeyword("BCM_GROUND_SPLAT");
        // 가림막 표시(ProtoSeeThrough 가 캐릭터를 가리면 통째로 반투명 처리)
        m.SetFloat("_UseSeeThrough", occluder ? 1f : 0f);
        if (occluder) m.EnableKeyword("BCM_SEE_THROUGH"); else m.DisableKeyword("BCM_SEE_THROUGH");
        EditorUtility.SetDirty(m);
        return m;
    }

    static bool IsTimestampName(string n)
    {
        if (n.Length < 15 || n[14] != '_') return false;
        for (int i = 0; i < 14; i++) if (n[i] < '0' || n[i] > '9') return false;
        return true;
    }

    // ══ 메시 조립 도우미 ═════════════════════════════════════════════════
    /// 서브메시 여러 개를 가진 메시를 쌓는다. 면마다 정점을 따로 둬서(공유 안 함) 각진 툰 음영이 난다.
    class MB
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Color32> col = new List<Color32>();
        readonly List<int>[] tris;
        /// 설정하면 모든 정점에 이 함수로 스플랫 덮임(정점 컬러)을 굽는다. 스플랫이 아닌 머티리얼은 무시한다.
        public System.Func<Vector3, Color32> cover;
        public MB(int subs) { tris = new List<int>[subs]; for (int i = 0; i < subs; i++) tris[i] = new List<int>(); }
        public bool HasAny => v.Count > 0;

        void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            // a-b-c-d 는 바깥에서 봤을 때 시계 방향(유니티 앞면)
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            if (cover != null) { col.Add(cover(a)); col.Add(cover(b)); col.Add(cover(c)); col.Add(cover(d)); }
            tris[sub].Add(i); tris[sub].Add(i + 1); tris[sub].Add(i + 2);
            tris[sub].Add(i); tris[sub].Add(i + 2); tris[sub].Add(i + 3);
        }

        /// 위를 보는 수평 사각형. tile = 텍스처 한 장이 덮는 m.
        public void QuadXZ(int sub, float x0, float z0, float x1, float z1, float y, float tile)
        {
            float k = 1f / tile;
            Quad(sub, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), Vector3.up,
                 new Vector2(x0 * k, z0 * k), new Vector2(x0 * k, z1 * k), new Vector2(x1 * k, z1 * k), new Vector2(x1 * k, z0 * k));
        }

        /// 수평 사각형을 cell m 격자로 잘게 나눠 깐다 — 정점 컬러(스플랫 덮임)가 그 간격으로 찍히게.
        public void GridXZ(int sub, float x0, float z0, float x1, float z1, float y, float tile, float cell)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / cell)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / cell));
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                    QuadXZ(sub, Mathf.Lerp(x0, x1, (float)i / nx), Mathf.Lerp(z0, z1, (float)j / nz),
                                Mathf.Lerp(x0, x1, (float)(i + 1) / nx), Mathf.Lerp(z0, z1, (float)(j + 1) / nz), y, tile);
        }

        /// p0→p1 을 밑변으로 y0~y1 인 벽. 바깥쪽 = p0→p1 진행 방향의 오른쪽. uvU/uvV = 텍스처 한 장이 덮는 m(가로/세로).
        public void Wall(int sub, Vector3 p0, Vector3 p1, float y0, float y1, float uvU, float uvV)
        {
            Vector3 dir = p1 - p0; float len = dir.magnitude;
            Vector3 normal = new Vector3(-dir.z, 0f, dir.x).normalized * -1f;   // 진행 방향 오른쪽
            float u1 = len / uvU, v0 = y0 / uvV, v1 = y1 / uvV;
            Quad(sub, new Vector3(p0.x, y0, p0.z), new Vector3(p0.x, y1, p0.z), new Vector3(p1.x, y1, p1.z), new Vector3(p1.x, y0, p1.z), normal,
                 new Vector2(0, v0), new Vector2(0, v1), new Vector2(u1, v1), new Vector2(u1, v0));
        }

        /// 축 정렬 상자(바닥면 없음). wallSub/topSub. facadeUV=0 이면 벽 UV 를 파사드 규칙(Bay·Floor)으로, 아니면 그 m 단위로.
        public void Box(int wallSub, int topSub, Vector3 a, Vector3 b, float facadeUV)
        {
            float uU = facadeUV > 0 ? facadeUV : Bay * 4f, uV = facadeUV > 0 ? facadeUV : Floor * 4f;
            // 둘레를 시계 방향으로(위에서 봤을 때): 남 → 동 → 북 → 서. 각 면 바깥 = 진행 방향 오른쪽… 이 되도록 반시계로 돈다.
            Wall(wallSub, new Vector3(a.x, 0, a.z), new Vector3(b.x, 0, a.z), a.y, b.y, uU, uV);   // 남쪽 면
            Wall(wallSub, new Vector3(b.x, 0, a.z), new Vector3(b.x, 0, b.z), a.y, b.y, uU, uV);   // 동
            Wall(wallSub, new Vector3(b.x, 0, b.z), new Vector3(a.x, 0, b.z), a.y, b.y, uU, uV);   // 북
            Wall(wallSub, new Vector3(a.x, 0, b.z), new Vector3(a.x, 0, a.z), a.y, b.y, uU, uV);   // 서
            QuadXZ(topSub, a.x, a.z, b.x, b.z, b.y, TileRoof);
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            if (col.Count == v.Count) mesh.SetColors(col);
            mesh.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }
    }
}
