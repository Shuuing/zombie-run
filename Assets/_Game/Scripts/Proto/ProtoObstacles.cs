using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 배경 프롭 충돌. Unity Physics를 쓰지 않는다 (GDD §16).
    ///
    /// 씬의 _Props 계층을 훑어 프롭 인스턴스마다 XZ 평면 OBB(Y축 회전 사각형)를 만들고,
    /// 이동하려는 위치가 박스 안에 들어오면 침투가 가장 얕은 축으로 밀어낸다.
    /// 프롭이 랜덤 Y회전을 갖기 때문에 원형 근사보다 회전 사각형이 실제 형태에 훨씬 가깝다.
    ///
    /// <para><b>_Props 밖 세트피스</b>(디오라마·묘비·드럼통)는 See Through 머티리얼인 렌더러마다 실제 면을 칸으로 찍어 막는다 —
    /// <see cref="AddSetpieceBoxes"/> 참고.</para>
    /// <para>움직이는 것(플레이어·좀비·탱크·적 투사체)은 <see cref="ResolveMove"/>, 자리 찾기(드롭·탱크 하차·헬기 추락)는
    /// <see cref="Resolve"/> 를 쓴다. 지금은 둘 다 프롭과 블로커를 막는다. 가린 것은 막히는 것과 별개로 <see cref="ProtoSeeThrough"/> 가 비운다.</para>
    /// <para>2026-11-10 하루 동안 YAZS 처럼 프롭을 전부 통과시켰다가, 같은 날 사용자 요청("통과하지 말고 뒤로 가면 투명하게만")으로
    /// 다시 막았다. 통과 실험은 <see cref="propsPassable"/> 를 켜면 그대로 돌아온다.</para>
    public class ProtoObstacles : MonoBehaviour
    {
        public static ProtoObstacles I { get; private set; }

        [Tooltip("프롭 루트 이름. 이 하위를 훑어 충돌 박스를 만든다.")]
        public string propsRootName = "_Props";

        // 이름이 propsBlockMovement(기본 false = 통과)였다가 뜻을 뒤집으며 바꿨다 — 열린 에디터가 옛 값(false)을 들고 있어도
        // 새 이름이라 버려지고 기본값(막음)이 들어간다. FormerlySerializedAs 를 붙이지 말 것.
        [Tooltip("켜면 _Props 프롭(건물·컨테이너·차·나무)을 YAZS 처럼 플레이어·좀비·탱크·적 투사체가 통과한다. 기본은 꺼짐(막음). " +
                 "손으로 놓은 ProtoBlocker(호수·절벽)와 맵 경계는 켜도 막는다. 자리 찾기(Resolve)는 켜도 프롭을 피한다.")]
        public bool propsPassable = false;

        [Tooltip("적도 프롭·블로커에 막히게 할지. 끄면 적은 전부 통과한다(뱀서류 관행 — AI 가 끼는 문제를 피함).")]
        public bool blockEnemies = true;

        [Tooltip("디버그: 박스를 씬 뷰에 그린다.")]
        public bool drawGizmos = false;

        // ── _Props 밖 세트피스 (2026-11-10, 사용자: "통과하지 말고 뒤로 가면 투명하게만" → 세트피스도 막아 달라) ──
        // 고속도로·영화관 디오라마는 조각(차·건물·기둥·드럼통)이 수십 개라 인스턴스 하나를 상자 하나로 막으면 길까지 막힌다.
        // 그래서 **렌더러마다** 바닥 사각형을 만들고, 낮은 조각(길·바닥)과 떠 있는 조각(다리 상판·간판)은 뺀다.
        // 대상은 가림막 표시(See Through 머티리얼)가 켜진 렌더러 — "가리는 것은 막는다". ProtoSeeThrough 와 같은 표시다.
        [Header("세트피스 (_Props 밖)")]
        [Tooltip("_Props 밖에 손으로 놓은 세트피스(고속도로·영화관 디오라마, 묘비, 드럼통)도 막을지. See Through 머티리얼이 켜진 렌더러마다 바닥 사각형을 만든다.")]
        public bool blockSetpieces = true;
        [Tooltip("윗면이 바닥(0)에서 이보다 낮은 조각(길·바닥판·차선·잔해)은 막지 않는다(m).")]
        public float setpieceMinHeight = 0.6f;
        [Tooltip("밑면이 이보다 높게 떠 있는 조각(다리 상판·간판·전광판 글자)은 막지 않는다 — 그 아래로 지나간다(m).")]
        public float setpieceMaxBase = 1.0f;
        [Tooltip("이 높이 구간(m)을 지나는 면만 막는다(무릎~가슴). 바닥판·차선(아래)과 차양·다리 상판(위)은 지나간다.")]
        public Vector2 setpieceSlab = new Vector2(0.3f, 1.5f);
        [Tooltip("막는 칸 크기(m). 조각의 실제 면을 이 칸으로 찍어 사각형으로 합친다. 작을수록 모양에 맞지만 상자가 늘어난다.")]
        public float setpieceCell = 0.4f;
        [Tooltip("메시가 Read/Write 꺼짐이라 면을 못 읽을 때만 쓰는 상한(m) — 바운즈 사각형 한 변이 이보다 길면 막지 않는다.")]
        public float setpieceMaxSize = 16f;

        // ── 나무·덤불·풀 (2026-11-12, 사용자: "식물의 줄기랑 나무의 중앙(가지)만 막고 나머지는 통과") ──
        // 잎 카드(알파 컷아웃 머티리얼)는 막지 않는다. 나무는 줄기·가지 머티리얼(불투명) 면 중 무릎~가슴 높이만 막고,
        // 잎 카드뿐인 덤불·풀은 줄기가 따로 없으므로(줄기도 잎 텍스처에 그려져 있다) 밑동 가운데 작은 사각형만 막는다.
        [Header("나무·덤불·풀")]
        [Tooltip("잎 카드뿐인 덤불·풀의 줄기로 막는 사각형 한 변(m). 줄기를 읽을 수 없는 나무(Read/Write 꺼짐)도 이 크기로 가운데를 막는다. " +
                 "키가 setpieceMinHeight 보다 낮은 풀은 막지 않는다.")]
        public float plantStemSize = 0.3f;
        const float TrunkCell = 0.2f;   // 줄기는 가늘어서 세트피스 칸(0.4)으로 찍으면 두 배로 부푼다
        const float SetpieceShrink = 0.9f;
        const string OccluderKeyword = "BCM_SEE_THROUGH";
        const string CutoutKeyword = "_ALPHATEST_ON";
        // 세트피스 렌더러마다 처음 잰 상자들(정적 배칭·폭발 Rebuild 대비 — Rebuild 주석)
        readonly Dictionary<Transform, Box[]> _setBoxes = new Dictionary<Transform, Box[]>();
        static readonly Box[] NoBoxes = new Box[0];

        // 프롭별 충돌 크기 배율. 1보다 작게 잡아 시각적 실루엣보다 조금 관대하게 만든다.
        // 나무는 수관(잎)이 아니라 줄기만 막아야 하므로 특히 작다.
        static readonly Dictionary<string, float> Shrink = new Dictionary<string, float>
        {
            { "Van_Abandoned",    0.90f },
            { "Barrier_Concrete", 0.90f },
            { "Barricade_Wood",   0.85f },
            { "Fence_Plank",      0.85f },
            { "Barrel_Rusty",     0.80f },
            { "Rubble_Concrete",  0.70f },
            { "Tree_Conifer",     0.32f },   // 줄기만
        };
        const float DefaultShrink = 0.85f;

        struct Box
        {
            public Vector2 center;
            public Vector2 half;
            public float cos, sin;      // Y축 회전(유니티 Euler Y 규약) — 박스 x 축 = (cos, -sin), z 축 = (sin, cos) (XZ)
            public float bound;         // 브로드페이즈용 외접 반경
            public bool ellipse;        // true 면 사각형이 아니라 타원 (ProtoBlocker.Shape.Ellipse)
        }

        Box[] _boxes = new Box[0];
        int _propCount;   // _boxes 앞쪽 이만큼이 프롭, 뒤는 블로커(ResolveMove 가 프롭을 건너뛸 때 시작점)
        // 인스턴스별로 처음 잰 박스. 프롭은 움직이지 않으므로 다시 잴 이유가 없고, 다시 재면 위험하다(Rebuild 주석).
        readonly Dictionary<Transform, Box> _instBox = new Dictionary<Transform, Box>();
        public int BoxCount => _boxes.Length;

        void Awake()
        {
            I = this;
            // 발자국 캐시는 static 이다. 도메인 리로드 없이 플레이를 다시 누르면 모델을 고쳐 리임포트해도
            // 옛 값이 남는다(메시 오브젝트가 같은 인스턴스로 재사용된다) — 세션마다 비운다.
            _fpCache.Clear(); _fpNone.Clear();
            Rebuild();
        }

        void OnDestroy() { if (I == this) I = null; }

        public void Rebuild(bool verbose = true)
        {
            var list = new List<Box>();

            var root = GameObject.Find(propsRootName);
            if (root == null)
                Debug.LogWarning($"[Obstacles] '{propsRootName}' 를 찾지 못했다. 프롭 충돌 없음.");
            else
            {
                // 계층: _Props / <프롭 종류> / <인스턴스>
                foreach (Transform group in root.transform)
                {
                    float shrink;
                    if (!Shrink.TryGetValue(group.name, out shrink)) shrink = DefaultShrink;

                    foreach (Transform inst in group)
                    {
                        // 꺼진 프롭은 길을 막지 않는다 — 터져서 사라진 승합차(ProtoDestructibles)가 재생성 전까지 꺼져 있다
                        if (!inst.gameObject.activeInHierarchy) continue;
                        // **처음 잰 박스를 다시 쓴다.** 플레이가 시작되면 정적 배칭이 프롭 메시를
                        // "Combined Mesh (root: scene)" 하나로 합쳐 MeshFilter 에 꽂는다. 그 뒤 다시 재면 도시 한 구역만 한
                        // 박스가 생겨 캐릭터가 순간이동하듯 밀려나고 길이 막혔다(2026-10-13, 승합차 폭발 때 Rebuild).
                        Box b;
                        Box[] many;
                        if (_setBoxes.TryGetValue(inst, out many)) { list.AddRange(many); continue; }
                        if (_instBox.TryGetValue(inst, out b)) { list.Add(b); continue; }
                        // 잎 카드 나무·덤불은 잎을 지나가게 하고 줄기만 막는다(위 '나무·덤불·풀' 주석)
                        if (TryLeafPlant(inst, list)) continue;
                        // 폰 빌드는 시작부터 메시가 합쳐져 있다 — 씬 처리 단계에서 미리 재 둔 발자국을 먼저 쓴다
                        var fp = inst.GetComponent<ProtoFootprint>();
                        if (fp != null ? FromFootprint(fp, shrink, out b) : TryMakeBox(inst, shrink, out b)) { list.Add(b); _instBox[inst] = b; }
                    }
                }
            }
            int onlyProps = list.Count;
            if (blockSetpieces) AddSetpieceBoxes(list, root != null ? root.transform : null);
            int propBoxes = list.Count;   // 프롭 + 세트피스 — propsPassable 이면 둘 다 통과한다

            // 손으로 놓은 세트피스 볼륨 (호수 디오라마 등). 프롭 계층 밖에 있다.
            var blockers = FindObjectsOfType<ProtoBlocker>();
            for (int i = 0; i < blockers.Length; i++)
            {
                Box b;
                if (MakeBlockerBox(blockers[i], out b)) list.Add(b);
            }

            _boxes = list.ToArray();
            _propCount = propBoxes;
            BuildBuckets();
            if (verbose)
                Debug.Log($"[Obstacles] 충돌 볼륨 {_boxes.Length}개 (프롭 {onlyProps} + 세트피스 {propBoxes - onlyProps} + 블로커 {_boxes.Length - propBoxes}) — " +
                          (propsPassable ? "프롭은 통과(자리 찾기에만 씀)" : "프롭이 이동을 막는다"));
        }

        static readonly List<Material> _mats = new List<Material>();

        /// _Props 밖 가림막 렌더러마다 막는 상자들. 낮은 것·떠 있는 것은 통째로 빼고, 나머지는 실제 면을 칸으로 찍는다.
        /// <para>디오라마 조각은 기둥 두 개·휜 조형물·비스듬히 기댄 판처럼 속이 빈 것이 많아 바운즈 사각형 하나로 막으면
        /// 빈 곳(광장·다리 밑)까지 막혔다(2026-11-10 임시 프로젝트로 확인). 그래서 무릎~가슴 높이(setpieceSlab)를 지나는
        /// 삼각형만 잘라 바닥 칸(setpieceCell)에 찍고, 둘러싸인 안쪽은 채운 뒤(건물 속에 갇히지 않게) 사각형으로 합친다.</para>
        void AddSetpieceBoxes(List<Box> list, Transform propsRoot)
        {
            List<string> unreadable = null;
            foreach (var r in FindObjectsOfType<MeshRenderer>())
            {
                if (propsRoot != null && r.transform.IsChildOf(propsRoot)) continue;   // 프롭은 위에서 인스턴스 단위로 했다
                Box[] boxes;
                // 처음 잰 값을 먼저 본다 — 장식 스트리머(ProtoDecorStreamer)가 멀리 있는 덤불의 렌더러를 꺼 두므로
                // 꺼짐 검사를 앞에 두면 승합차 폭발 때 Rebuild 하면서 그 덤불의 줄기가 사라진다
                if (_setBoxes.TryGetValue(r.transform, out boxes)) { list.AddRange(boxes); continue; }
                if (!r.enabled) continue;
                if (!IsOccluder(r)) continue;
                var wb = r.bounds;
                if (wb.max.y - Mathf.Max(wb.min.y, 0f) < setpieceMinHeight) continue;   // 길·바닥판·차선·낮은 풀
                if (wb.min.y > setpieceMaxBase) continue;                                // 떠 있는 것 — 그 아래로 지나간다
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                Box b;
                if (HasCutout(r))                                                        // 덤불·풀·나무 — 줄기만
                {
                    var tmp = new List<Box>();
                    PlantBoxes(r, mf, tmp, ref unreadable);
                    boxes = tmp.ToArray();
                }
                else if (mesh.name.StartsWith("Combined Mesh"))                         // 정적 배칭이면 렌더러 바운즈로
                    boxes = BoxFromBounds(wb, SetpieceShrink, out b) ? new[] { b } : NoBoxes;
                else if (mesh.isReadable)
                    boxes = Rasterize(mf, null, setpieceCell);
                else
                {
                    (unreadable ?? (unreadable = new List<string>())).Add(mesh.name);
                    boxes = CornerRect(mf, SetpieceShrink, out b) && b.half.x * 2f <= setpieceMaxSize && b.half.y * 2f <= setpieceMaxSize
                        ? new[] { b } : NoBoxes;
                }
                _setBoxes[r.transform] = boxes;
                list.AddRange(boxes);
            }
            WarnUnreadable(unreadable);
        }

        static void WarnUnreadable(List<string> unreadable)
        {
            if (unreadable == null) return;
            Debug.LogWarning($"[Obstacles] 메시 {unreadable.Count}개가 Read/Write 꺼짐 — 면을 못 읽어 세트피스는 바운즈 사각형, 나무는 가운데 사각형으로 막는다. " +
                             $"모델 임포터의 Read/Write 를 켤 것: {string.Join(", ", unreadable.GetRange(0, Mathf.Min(8, unreadable.Count)))}");
        }

        static bool HasCutout(Renderer r)
        {
            r.GetSharedMaterials(_mats);
            for (int i = 0; i < _mats.Count; i++)
                if (_mats[i] != null && _mats[i].IsKeywordEnabled(CutoutKeyword)) return true;
            return false;
        }

        /// _Props 인스턴스에 잎 카드 머티리얼이 있으면 줄기만 막는 상자를 만들어 넣고 true. 없으면 false(예전 방식으로).
        bool TryLeafPlant(Transform inst, List<Box> list)
        {
            var rs = inst.GetComponentsInChildren<MeshRenderer>(true);
            bool leafy = false;
            foreach (var r in rs) if (HasCutout(r)) { leafy = true; break; }
            if (!leafy) return false;
            var res = new List<Box>();
            List<string> unreadable = null;
            foreach (var r in rs) PlantBoxes(r, r.GetComponent<MeshFilter>(), res, ref unreadable);
            WarnUnreadable(unreadable);
            var arr = res.ToArray();
            _setBoxes[inst] = arr;
            list.AddRange(arr);
            return true;
        }

        /// 잎 카드가 섞인 렌더러: 줄기·가지(불투명) 서브메시 중 무릎~가슴 높이만 막는다.
        /// 줄기가 따로 없거나(덤불·풀) 면을 못 읽으면 밑동 가운데 plantStemSize 사각형 하나.
        void PlantBoxes(Renderer r, MeshFilter mf, List<Box> res, ref List<string> unreadable)
        {
            var mesh = mf != null ? mf.sharedMesh : null;
            int subs = mesh != null ? mesh.subMeshCount : 0;
            r.GetSharedMaterials(_mats);
            var wood = new bool[subs];
            bool anyWood = false;
            for (int s = 0; s < subs; s++)
            {
                // 머티리얼이 서브메시보다 적으면 남는 서브메시는 안 그려진다 — 마지막 것으로 본다
                var m = _mats.Count == 0 ? null : _mats[Mathf.Min(s, _mats.Count - 1)];
                wood[s] = m != null && !m.IsKeywordEnabled(CutoutKeyword);
                anyWood |= wood[s];
            }
            if (anyWood && mesh.isReadable && !mesh.name.StartsWith("Combined Mesh"))
            {
                res.AddRange(Rasterize(mf, wood, Mathf.Min(setpieceCell, TrunkCell)));
                return;
            }
            if (anyWood && !mesh.isReadable) (unreadable ?? (unreadable = new List<string>())).Add(mesh.name);
            var wb = r.bounds;
            if (wb.max.y - Mathf.Max(wb.min.y, 0f) < setpieceMinHeight) return;   // 낮은 풀은 막지 않는다
            if (wb.min.y > setpieceMaxBase) return;
            float h = Mathf.Max(0.05f, plantStemSize * 0.5f);
            res.Add(new Box { center = new Vector2(wb.center.x, wb.center.z), half = new Vector2(h, h), cos = 1f, sin = 0f, bound = h * 1.4142f });
        }

        /// 메시 바운즈 8 모서리를 바닥에 투영한 최소 사각형(Read/Write 가 꺼진 메시용).
        static bool CornerRect(MeshFilter mf, float shrink, out Box box)
        {
            box = default(Box);
            var mb = mf.sharedMesh.bounds;
            var m = mf.transform.localToWorldMatrix;
            var pts = new List<Vector2>(8);
            for (int k = 0; k < 8; k++)
            {
                var c = mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                var w = m.MultiplyPoint3x4(c);
                pts.Add(new Vector2(w.x, w.z));
            }
            Footprint fp;
            if (!MinAreaRect(pts, out fp)) return false;
            float hx = fp.half.x * shrink, hz = fp.half.y * shrink;
            if (hx < 0.05f || hz < 0.05f) return false;
            box.center = fp.center;
            box.half   = new Vector2(hx, hz);
            box.cos    = Mathf.Cos(fp.angle);
            box.sin    = Mathf.Sin(fp.angle);
            box.bound  = box.half.magnitude;
            return true;
        }

        static readonly List<Vector3> _tv = new List<Vector3>();
        static readonly List<int> _ti = new List<int>();
        static readonly List<Vector3> _clipA = new List<Vector3>(), _clipB = new List<Vector3>();

        /// 무릎~가슴 높이를 지나는 면을 바닥 칸에 찍어 사각형들로. 칸 방향은 조각 바운즈의 최소 사각형 방향(대개 놓인 방향)이다.
        /// include 가 있으면 그 서브메시만(나무 줄기), null 이면 전부.
        Box[] Rasterize(MeshFilter mf, bool[] include, float cellSize)
        {
            var mesh = mf.sharedMesh;
            var m = mf.transform.localToWorldMatrix;
            float yaw = 0f;
            Box frame;
            if (CornerRect(mf, 1f, out frame)) yaw = Mathf.Atan2(frame.sin, frame.cos);
            float cs = Mathf.Cos(yaw), sn = Mathf.Sin(yaw);   // u 축 = (cs, -sn), v 축 = (sn, cs) — 유니티 규약
            float lo = setpieceSlab.x, hi = setpieceSlab.y, cell = Mathf.Max(0.1f, cellSize);

            mesh.GetVertices(_tv);
            for (int i = 0; i < _tv.Count; i++) _tv[i] = m.MultiplyPoint3x4(_tv[i]);

            // 1) 구간 안 삼각형 조각 → (u, v) 다각형
            var polys = new List<Vector2[]>();
            float umin = float.MaxValue, umax = float.MinValue, vmin = float.MaxValue, vmax = float.MinValue;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (include != null && (s >= include.Length || !include[s])) continue;
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                mesh.GetTriangles(_ti, s);
                for (int t = 0; t + 2 < _ti.Count; t += 3)
                {
                    Vector3 a = _tv[_ti[t]], b = _tv[_ti[t + 1]], c = _tv[_ti[t + 2]];
                    float ymin = Mathf.Min(a.y, Mathf.Min(b.y, c.y)), ymax = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                    if (ymax < lo || ymin > hi) continue;
                    _clipA.Clear(); _clipA.Add(a); _clipA.Add(b); _clipA.Add(c);
                    ClipY(_clipA, _clipB, lo, true);
                    ClipY(_clipB, _clipA, hi, false);
                    if (_clipA.Count == 0) continue;
                    var poly = new Vector2[_clipA.Count];
                    for (int k = 0; k < poly.Length; k++)
                    {
                        var p = _clipA[k];
                        float u = p.x * cs - p.z * sn, v = p.x * sn + p.z * cs;
                        poly[k] = new Vector2(u, v);
                        if (u < umin) umin = u; if (u > umax) umax = u;
                        if (v < vmin) vmin = v; if (v > vmax) vmax = v;
                    }
                    polys.Add(poly);
                }
            }
            if (polys.Count == 0) return NoBoxes;

            // 2) 칸 찍기 — 둘레에 한 칸씩 여백(바깥 채우기 시작점)
            float u0 = umin - cell, v0 = vmin - cell;
            int nu = Mathf.CeilToInt((umax - u0) / cell) + 2, nv = Mathf.CeilToInt((vmax - v0) / cell) + 2;
            if ((long)nu * nv > 400000)   // 칸이 너무 많은 큰 조각 — 바운즈 사각형으로
            {
                Box big;
                return CornerRect(mf, SetpieceShrink, out big) ? new[] { big } : NoBoxes;
            }
            var occ = new bool[nu * nv];
            float step = cell * 0.25f;
            foreach (var poly in polys)
            {
                // 변: 세운 벽은 바닥에 선으로만 남으므로 변을 따라 찍어야 한다
                for (int k = 0; k < poly.Length; k++)
                {
                    Vector2 p = poly[k], q = poly[(k + 1) % poly.Length];
                    int n = Mathf.CeilToInt((q - p).magnitude / step) + 1;
                    for (int i = 0; i <= n; i++) Mark(occ, nu, nv, Vector2.Lerp(p, q, i / (float)n), u0, v0, cell);
                }
                // 안: 칸 중심이 다각형(볼록) 안이면
                if (poly.Length < 3) continue;
                float pu0 = float.MaxValue, pu1 = float.MinValue, pv0 = float.MaxValue, pv1 = float.MinValue;
                foreach (var p in poly) { pu0 = Mathf.Min(pu0, p.x); pu1 = Mathf.Max(pu1, p.x); pv0 = Mathf.Min(pv0, p.y); pv1 = Mathf.Max(pv1, p.y); }
                int i0 = Mathf.FloorToInt((pu0 - u0) / cell), i1 = Mathf.FloorToInt((pu1 - u0) / cell);
                int j0 = Mathf.FloorToInt((pv0 - v0) / cell), j1 = Mathf.FloorToInt((pv1 - v0) / cell);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        if (InConvex(poly, new Vector2(u0 + (i + 0.5f) * cell, v0 + (j + 0.5f) * cell))) occ[j * nu + i] = true;
            }

            // 3) 둘러싸인 안쪽 채우기 — 바깥 테두리에서 빈 칸을 따라 번져 닿지 않은 빈 칸은 안쪽이다(건물 속에 스폰돼 갇히지 않게)
            var outside = new bool[nu * nv];
            var queue = new Queue<int>();
            for (int i = 0; i < nu; i++) { Seed(occ, outside, queue, i); Seed(occ, outside, queue, (nv - 1) * nu + i); }
            for (int j = 0; j < nv; j++) { Seed(occ, outside, queue, j * nu); Seed(occ, outside, queue, j * nu + nu - 1); }
            while (queue.Count > 0)
            {
                int id = queue.Dequeue(), i = id % nu, j = id / nu;
                if (i > 0) Seed(occ, outside, queue, id - 1);
                if (i < nu - 1) Seed(occ, outside, queue, id + 1);
                if (j > 0) Seed(occ, outside, queue, id - nu);
                if (j < nv - 1) Seed(occ, outside, queue, id + nu);
            }
            for (int id = 0; id < occ.Length; id++) if (!outside[id]) occ[id] = true;

            // 4) 칸 → 사각형(가로로 늘이고 같은 폭으로 세로로 늘인다)
            var res = new List<Box>();
            var used = new bool[nu * nv];
            float shrink = cell * 0.15f;   // 칸은 면보다 넉넉하다 — 조금 깎아 벽에 붙어 미끄러질 때 덜 걸리게
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int id = j * nu + i;
                    if (!occ[id] || used[id]) continue;
                    int ie = i;
                    while (ie + 1 < nu && occ[j * nu + ie + 1] && !used[j * nu + ie + 1]) ie++;
                    int je = j;
                    while (je + 1 < nv)
                    {
                        bool full = true;
                        for (int x = i; x <= ie && full; x++) { int q = (je + 1) * nu + x; full = occ[q] && !used[q]; }
                        if (!full) break;
                        je++;
                    }
                    for (int y = j; y <= je; y++) for (int x = i; x <= ie; x++) used[y * nu + x] = true;
                    float cu = u0 + (i + ie + 1) * 0.5f * cell, cv = v0 + (j + je + 1) * 0.5f * cell;
                    float hx = Mathf.Max(0.05f, (ie - i + 1) * cell * 0.5f - shrink), hz = Mathf.Max(0.05f, (je - j + 1) * cell * 0.5f - shrink);
                    var bx = new Box
                    {
                        center = new Vector2(cu * cs + cv * sn, -cu * sn + cv * cs),   // (u, v) → 월드 XZ
                        half = new Vector2(hx, hz), cos = cs, sin = sn,
                    };
                    bx.bound = bx.half.magnitude;
                    res.Add(bx);
                }
            return res.ToArray();
        }

        static void Mark(bool[] occ, int nu, int nv, Vector2 p, float u0, float v0, float cell)
        {
            int i = Mathf.FloorToInt((p.x - u0) / cell), j = Mathf.FloorToInt((p.y - v0) / cell);
            if (i >= 0 && i < nu && j >= 0 && j < nv) occ[j * nu + i] = true;
        }

        static void Seed(bool[] occ, bool[] outside, Queue<int> q, int id)
        {
            if (occ[id] || outside[id]) return;
            outside[id] = true; q.Enqueue(id);
        }

        /// 볼록 다각형 안(감는 방향 무관). 면적이 0 인 다각형(세운 벽)은 false — 그건 변 찍기가 맡는다.
        static bool InConvex(Vector2[] poly, Vector2 p)
        {
            int sign = 0;
            for (int k = 0; k < poly.Length; k++)
            {
                Vector2 a = poly[k], b = poly[(k + 1) % poly.Length];
                float cr = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (Mathf.Abs(cr) < 1e-9f) continue;
                int sg = cr > 0f ? 1 : -1;
                if (sign == 0) sign = sg; else if (sg != sign) return false;
            }
            return sign != 0;
        }

        /// 다각형을 수평면 y 로 자른다(keepAbove 면 y 이상만 남김). Sutherland–Hodgman.
        static void ClipY(List<Vector3> src, List<Vector3> dst, float y, bool keepAbove)
        {
            dst.Clear();
            int n = src.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = src[i], b = src[(i + 1) % n];
                bool ain = keepAbove ? a.y >= y : a.y <= y, bin = keepAbove ? b.y >= y : b.y <= y;
                if (ain) dst.Add(a);
                if (ain != bin) dst.Add(Vector3.Lerp(a, b, (y - a.y) / (b.y - a.y)));
            }
        }

        static bool IsOccluder(Renderer r)
        {
            r.GetSharedMaterials(_mats);
            for (int i = 0; i < _mats.Count; i++)
                if (_mats[i] != null && _mats[i].IsKeywordEnabled(OccluderKeyword)) return true;
            return false;
        }

        static bool BoxFromBounds(Bounds wb, float shrink, out Box box)
        {
            box = default(Box);
            float hx = wb.extents.x * shrink, hz = wb.extents.z * shrink;
            if (hx < 0.05f || hz < 0.05f) return false;
            box.center = new Vector2(wb.center.x, wb.center.z);
            box.half   = new Vector2(hx, hz);
            box.cos    = 1f;
            box.sin    = 0f;
            box.bound  = box.half.magnitude;
            return true;
        }

        static bool FromFootprint(ProtoFootprint fp, float shrink, out Box box)
        {
            box = default(Box);
            float hx = fp.half.x * shrink, hz = fp.half.y * shrink;
            if (hx < 0.05f || hz < 0.05f) return false;
            box.center = fp.center;
            box.half   = new Vector2(hx, hz);
            box.cos    = Mathf.Cos(fp.yaw);
            box.sin    = Mathf.Sin(fp.yaw);
            box.bound  = box.half.magnitude;
            return true;
        }

        /// 에디터(씬 처리 단계, 메시가 합쳐지기 전)에서 발자국을 잴 때 쓴다. 축소율 적용 전 값.
        public static bool MeasureFootprint(Transform inst, out Vector2 center, out Vector2 half, out float yaw, out float height)
        {
            center = half = Vector2.zero; yaw = 0f; height = 0f;
            if (!TryMakeBox(inst, 1f, out var b)) return false;
            center = b.center; half = b.half; yaw = Mathf.Atan2(b.sin, b.cos);
            bool any = false; Bounds wb = default;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            { if (!any) { wb = r.bounds; any = true; } else wb.Encapsulate(r.bounds); }
            height = any ? wb.max.y - Mathf.Max(0f, wb.min.y) : 2f;
            return true;
        }

        static bool MakeBlockerBox(ProtoBlocker bl, out Box box)
        {
            box = default(Box);
            var t = bl.transform;
            Vector3 scale = t.lossyScale;
            float hx = bl.halfExtents.x * Mathf.Abs(scale.x);
            float hz = bl.halfExtents.y * Mathf.Abs(scale.z);
            if (hx < 0.05f || hz < 0.05f) return false;

            Vector3 world = t.TransformPoint(new Vector3(bl.center.x, 0f, bl.center.y));
            float yaw = t.eulerAngles.y * Mathf.Deg2Rad;
            box.center  = new Vector2(world.x, world.z);
            box.half    = new Vector2(hx, hz);
            box.cos     = Mathf.Cos(yaw);
            box.sin     = Mathf.Sin(yaw);
            box.bound   = new Vector2(hx, hz).magnitude;
            box.ellipse = bl.shape == ProtoBlocker.Shape.Ellipse;
            return true;
        }

        static bool TryMakeBox(Transform inst, float shrink, out Box box)
            => TryMakeBox(inst, inst.GetComponentsInChildren<MeshFilter>(true), shrink, out box);

        /// inst 의 yaw 틀에서 mfs 메시들을 합친 바닥 사각형. 프롭은 인스턴스 아래 전부, 세트피스는 렌더러 하나씩 넘긴다.
        static bool TryMakeBox(Transform inst, MeshFilter[] mfs, float shrink, out Box box)
        {
            box = default(Box);

            // **인스턴스의 yaw 만 뺀 틀**에서 메시 바운즈를 합친다(스케일은 월드 그대로).
            // 예전엔 인스턴스 로컬 공간을 썼는데, 3ds Max·블렌더 FBX 처럼 루트에 X -90°/270° 가 붙은 모델은
            // 로컬 Z 가 '높이' 축이라 발자국 대신 높이로 충돌 박스를 만들었다(container_01 이 그랬다).
            // yaw 틀이면 루트 회전이 무엇이든 X/Z 가 항상 바닥 평면이다. 회전이 yaw 뿐인 기존 프롭은 결과가 같다.
            float yawDeg = inst.eulerAngles.y;
            Matrix4x4 frame = Matrix4x4.TRS(inst.position, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one);
            Matrix4x4 toFrameBase = frame.inverse;
            bool any = false;
            Bounds local = new Bounds();
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                Matrix4x4 toInst = toFrameBase * mf.transform.localToWorldMatrix;
                Vector3 c = toInst.MultiplyPoint3x4(mb.center);
                Vector3 e = mb.extents;
                Vector3 ex = new Vector3(
                    Mathf.Abs(toInst.m00) * e.x + Mathf.Abs(toInst.m01) * e.y + Mathf.Abs(toInst.m02) * e.z,
                    Mathf.Abs(toInst.m10) * e.x + Mathf.Abs(toInst.m11) * e.y + Mathf.Abs(toInst.m12) * e.z,
                    Mathf.Abs(toInst.m20) * e.x + Mathf.Abs(toInst.m21) * e.y + Mathf.Abs(toInst.m22) * e.z);
                var bb = new Bounds(c, ex * 2f);
                if (!any) { local = bb; any = true; } else local.Encapsulate(bb);
            }
            if (!any) return false;

            Vector3 worldCenter = frame.MultiplyPoint3x4(local.center);
            float hx = local.extents.x * shrink;   // 틀에 스케일이 없으므로 이미 월드 크기
            float hz = local.extents.z * shrink;
            float yaw = yawDeg * Mathf.Deg2Rad;

            // 모델이 파일 안에서 비스듬히 돌아가 있으면(container_01 은 22°) 축 정렬 박스가 실제보다 훨씬 넓다.
            // 꼭짓점으로 최소 면적 회전 사각형을 구해, 확실히 작을 때만(면적 90% 미만) 그걸 쓴다 — 축에 맞게 만든 모델은 결과가 그대로.
            Footprint fp;
            if (TryTightFootprint(mfs, toFrameBase, out fp) && fp.area < local.size.x * local.size.z * 0.9f)
            {
                // fp.center 는 yaw 틀 XZ. 틀의 X/Z 축은 월드에서 yaw 만큼 돌아가 있다.
                worldCenter = frame.MultiplyPoint3x4(new Vector3(fp.center.x, 0f, fp.center.y));
                hx = fp.half.x * shrink;
                hz = fp.half.y * shrink;
                yaw += fp.angle;
            }
            if (hx < 0.05f || hz < 0.05f) return false;
            box.center = new Vector2(worldCenter.x, worldCenter.z);
            box.half   = new Vector2(hx, hz);
            box.cos    = Mathf.Cos(yaw);
            box.sin    = Mathf.Sin(yaw);
            box.bound  = new Vector2(hx, hz).magnitude;
            return true;
        }

        // ── 딱 맞는 발자국(최소 면적 회전 사각형) ───────────────────────
        struct Footprint { public Vector2 center, half; public float angle, area; }

        // 모델 종류마다 한 번만 계산한다. 키 = 메시들 + (yaw 를 뺀) 자세·스케일. 같은 프롭이 yaw 만 달리해 수백 개 놓이므로
        // 인스턴스마다 다시 구하면 청크가 바뀔 때마다 꼭짓점 수만 개를 돌게 된다.
        static readonly Dictionary<long, Footprint> _fpCache = new Dictionary<long, Footprint>();
        static readonly Dictionary<long, bool> _fpNone = new Dictionary<long, bool>();
        static readonly List<Vector2> _pts = new List<Vector2>();
        static readonly List<Vector2> _hull = new List<Vector2>();
        static readonly List<Vector3> _verts = new List<Vector3>();
        static bool _warnedUnreadable;

        /// yaw 틀(toFrame) 기준으로 인스턴스 메시 꼭짓점의 XZ 최소 면적 사각형.
        /// <para><b>메시가 Read/Write 가능해야 한다.</b> 빌드에서는 불가능한 메시의 꼭짓점을 못 읽는다 — 그럴 땐 false 를 돌려
        /// 축 정렬 박스로 떨어진다(예전 동작). 모델 임포터의 Read/Write 를 켜 둘 것.</para>
        static bool TryTightFootprint(MeshFilter[] mfs, Matrix4x4 toFrame, out Footprint fp)
        {
            fp = default(Footprint);

            // 캐시 키: 메시 id + 위치를 뺀 변환 행렬(소수 셋째 자리 반올림)
            long key = 17;
            unchecked
            {
                foreach (var mf in mfs)
                {
                    if (mf.sharedMesh == null) continue;
                    var m = toFrame * mf.transform.localToWorldMatrix;
                    // 인스턴스 위치 성분은 틀이 이미 뺐다. 자식 오프셋은 남아 있으므로 키에 넣는다.
                    key = key * 31 + mf.sharedMesh.GetInstanceID();
                    for (int i = 0; i < 12; i++) key = key * 31 + Mathf.RoundToInt(m[i % 3, i / 3] * 1000f);
                }
            }
            if (_fpCache.TryGetValue(key, out fp)) return true;
            if (_fpNone.ContainsKey(key)) return false;

            _pts.Clear();
            foreach (var mf in mfs)
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable)
                {
                    if (!_warnedUnreadable)
                    {
                        Debug.LogWarning($"[Obstacles] '{mesh.name}' 메시가 Read/Write 꺼짐 — 발자국을 축 정렬 박스로 잰다. " +
                                         "모델이 파일 안에서 비스듬하면 충돌이 넓어진다(임포터 Read/Write 를 켤 것).");
                        _warnedUnreadable = true;
                    }
                    _fpNone[key] = true;
                    return false;
                }
                var m = toFrame * mf.transform.localToWorldMatrix;
                mesh.GetVertices(_verts);
                for (int i = 0; i < _verts.Count; i++)
                {
                    var p = m.MultiplyPoint3x4(_verts[i]);
                    _pts.Add(new Vector2(p.x, p.z));
                }
            }
            if (!MinAreaRect(_pts, out fp)) { _fpNone[key] = true; return false; }
            _fpCache[key] = fp;
            return true;
        }

        /// 점들의 최소 면적 회전 사각형. 한 변이 볼록 껍질의 어느 변과 나란하다 — 껍질 변만 시험하면 정확하다.
        /// 각도는 유니티 yaw 규약: Euler(0,a,0) * right = (cos a, -sin a) (XZ), * forward = (sin a, cos a). pts 는 정렬된다.
        static bool MinAreaRect(List<Vector2> pts, out Footprint fp)
        {
            fp = default(Footprint);
            if (pts.Count < 3) return false;
            ConvexHull(pts, _hull);
            float best = float.MaxValue;
            for (int e = 0; e < _hull.Count; e++)
            {
                Vector2 d = _hull[(e + 1) % _hull.Count] - _hull[e];
                if (d.sqrMagnitude < 1e-8f) continue;
                float a = Mathf.Atan2(-d.y, d.x);
                Vector2 u = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)), v = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
                for (int i = 0; i < _hull.Count; i++)
                {
                    float pu = Vector2.Dot(_hull[i], u), pv = Vector2.Dot(_hull[i], v);
                    if (pu < u0) u0 = pu; if (pu > u1) u1 = pu;
                    if (pv < v0) v0 = pv; if (pv > v1) v1 = pv;
                }
                float area = (u1 - u0) * (v1 - v0);
                if (area < best)
                {
                    best = area;
                    float cu = (u0 + u1) * 0.5f, cv = (v0 + v1) * 0.5f;
                    fp.center = u * cu + v * cv;
                    fp.half = new Vector2((u1 - u0) * 0.5f, (v1 - v0) * 0.5f);
                    fp.angle = a;
                    fp.area = area;
                }
            }
            return best < float.MaxValue;
        }

        /// 단조 사슬(Andrew) 볼록 껍질. 반시계 방향.
        static void ConvexHull(List<Vector2> pts, List<Vector2> hull)
        {
            pts.Sort((p, q) => p.x != q.x ? p.x.CompareTo(q.x) : p.y.CompareTo(q.y));
            hull.Clear();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                for (int k = 0; k < pts.Count; k++)
                {
                    var p = pts[pass == 0 ? k : pts.Count - 1 - k];
                    while (hull.Count >= start + 2)
                    {
                        Vector2 a = hull[hull.Count - 2], b = hull[hull.Count - 1];
                        if ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) > 0f) break;
                        hull.RemoveAt(hull.Count - 1);
                    }
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);   // 다음 사슬의 시작점과 겹친다
            }
        }

        /// <b>자리 찾기용.</b> 원하는 위치를 프롭·블로커 밖으로 밀어낸 결과를 돌려준다(드롭·탱크 하차·헬기 추락 지점).
        /// 움직이는 것(플레이어·좀비·탱크·투사체)은 <see cref="ResolveMove"/> 를 쓴다.
        /// radius = 이동체 반지름. iterations 는 모서리에서 두 박스에 동시에 끼는 경우를 위한 반복.
        public Vector3 Resolve(Vector3 desired, float radius, int iterations = 2) => ResolveFrom(0, desired, radius, iterations);

        /// <b>이동용.</b> 기본은 프롭·블로커 모두 막는다. <see cref="propsPassable"/> 를 켜면 프롭은 통과하고 블로커만 막는다.
        public Vector3 ResolveMove(Vector3 desired, float radius, int iterations = 2)
            => ResolveFrom(propsPassable ? _propCount : 0, desired, radius, iterations);

        // ── 칸 색인: 세트피스가 상자를 수백 개 보태므로 좀비 300마리가 매 프레임 전부를 훑지 않게 한다 ──
        const float BucketSize = 8f;
        const float BucketMargin = 4f;   // 이동체 반지름 상한(헬기 추락 2.5·탱크 1.55)보다 넉넉히
        readonly Dictionary<long, List<int>> _buckets = new Dictionary<long, List<int>>();
        static long BucketKey(int x, int z) => ((long)x << 32) ^ (uint)z;

        void BuildBuckets()
        {
            // 목록은 비워서 다시 쓴다 — 예전엔 차가 터질 때마다(Rebuild) 칸 목록 수백 개를 새로 만들었다(2026-11-20).
            // 빈 칸은 없는 칸과 같다(ResolveFrom 이 0번 돌고 끝난다).
            foreach (var l0 in _buckets.Values) l0.Clear();
            for (int i = 0; i < _boxes.Length; i++)
            {
                var b = _boxes[i];
                float r = b.bound + BucketMargin;
                int x0 = Mathf.FloorToInt((b.center.x - r) / BucketSize), x1 = Mathf.FloorToInt((b.center.x + r) / BucketSize);
                int z0 = Mathf.FloorToInt((b.center.y - r) / BucketSize), z1 = Mathf.FloorToInt((b.center.y + r) / BucketSize);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        List<int> l;
                        if (!_buckets.TryGetValue(BucketKey(x, z), out l)) _buckets[BucketKey(x, z)] = l = new List<int>();
                        l.Add(i);
                    }
            }
        }

        Vector3 ResolveFrom(int first, Vector3 desired, float radius, int iterations)
        {
            if (first >= _boxes.Length) return desired;

            Vector2 p = new Vector2(desired.x, desired.z);

            for (int it = 0; it < iterations; it++)
            {
                bool moved = false;
                List<int> near;
                if (!_buckets.TryGetValue(BucketKey(Mathf.FloorToInt(p.x / BucketSize), Mathf.FloorToInt(p.y / BucketSize)), out near)) break;

                for (int k = 0; k < near.Count; k++)
                {
                    int i = near[k];
                    if (i < first) continue;
                    ref readonly var b = ref _boxes[i];   // 상자 구조체를 복사하지 않고 읽기만(좀비마다 매 프레임 — 2026-11-20)

                    // 브로드페이즈
                    float dx = p.x - b.center.x, dz = p.y - b.center.y;
                    float reach = b.bound + radius;
                    if (dx * dx + dz * dz > reach * reach) continue;

                    // 박스 로컬로 회전 (역회전). **유니티 규약**: Euler(0, yaw, 0) * right = (cos, -sin), * forward = (sin, cos).
                    // 2026-11-10 까지는 x 축을 (cos, sin) 으로 잡아 비스듬한 프롭(승합차·컨테이너)의 판정이 실제 모습과
                    // 반대로 돌아가 있었다 — 축에 맞게 놓인 것(0°·90°·180°)만 맞았다. 박스를 만드는 쪽(TryMakeBox·발자국·블로커)과
                    // 기즈모는 처음부터 유니티 규약이었다.
                    float lx = dx * b.cos - dz * b.sin;
                    float lz = dx * b.sin + dz * b.cos;

                    float ex = b.half.x + radius;
                    float ez = b.half.y + radius;

                    if (b.ellipse)
                    {
                        // 타원 → 단위원 공간으로 정규화한 뒤 반경 방향으로 경계까지 밀어낸다.
                        float nx = lx / ex, nz = lz / ez;
                        float len = Mathf.Sqrt(nx * nx + nz * nz);
                        if (len >= 1f) continue;              // 타원 밖
                        if (len < 1e-4f) { nx = 1f; nz = 0f; }  // 정중앙 — 아무 방향
                        else             { nx /= len; nz /= len; }
                        lx = nx * ex;
                        lz = nz * ez;
                    }
                    else
                    {
                        float penX = ex - Mathf.Abs(lx);
                        float penZ = ez - Mathf.Abs(lz);
                        if (penX <= 0f || penZ <= 0f) continue;   // 박스 밖

                        // 침투가 얕은 축으로 밀어낸다 → 벽을 따라 미끄러지는 느낌이 난다
                        if (penX < penZ) lx += Mathf.Sign(lx == 0f ? 1f : lx) * penX;
                        else             lz += Mathf.Sign(lz == 0f ? 1f : lz) * penZ;
                    }

                    // 월드로 복귀
                    p.x = b.center.x + ( lx * b.cos + lz * b.sin);
                    p.y = b.center.y + (-lx * b.sin + lz * b.cos);
                    moved = true;
                }

                if (!moved) break;
            }

            return new Vector3(p.x, desired.y, p.y);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _boxes == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.7f);
            for (int i = 0; i < _boxes.Length; i++)
            {
                var b = _boxes[i];
                var m = Matrix4x4.TRS(
                    new Vector3(b.center.x, 0.1f, b.center.y),
                    Quaternion.Euler(0f, Mathf.Atan2(b.sin, b.cos) * Mathf.Rad2Deg, 0f),
                    Vector3.one);
                Gizmos.matrix = m;
                if (b.ellipse)
                {
                    Vector3 prev = new Vector3(b.half.x, 0f, 0f);
                    for (int k = 1; k <= 40; k++)
                    {
                        float a = k / 40f * Mathf.PI * 2f;
                        var pt = new Vector3(Mathf.Cos(a) * b.half.x, 0f, Mathf.Sin(a) * b.half.y);
                        Gizmos.DrawLine(prev, pt);
                        prev = pt;
                    }
                }
                else Gizmos.DrawWireCube(Vector3.zero, new Vector3(b.half.x * 2f, 0.2f, b.half.y * 2f));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
