using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 나무·풀 같은 장식은 캐릭터 근처에서만 그린다(2026-10-27, 사용자: "캐릭터 없는 곳에는 생성하지 말고 다가오면 생성").
    ///
    /// <para><b>오브젝트를 끄지 않고 렌더러만 끈다.</b> ProtoObstacles.Rebuild 는 꺼진 인스턴스를 건너뛴다 —
    /// 나무 오브젝트를 끄면 승합차가 터질 때(Rebuild) 나무 충돌이 사라진다. 렌더러만 끄면 충돌·발자국은 그대로이고,
    /// 그리기·그림자(화면 밖 나무도 그림자 패스에는 들어간다)·컬링 비용만 빠진다.</para>
    ///
    /// <para><b>판정은 렌더러 바운즈의 가장 가까운 점까지의 수평 거리.</b> 풀 블록(8m)처럼 큰 메시도 가장자리가
    /// 다가오면 켜진다. 켜는 거리와 끄는 거리를 벌려 경계에서 깜빡이지 않게 한다.</para>
    ///
    /// <para><b>씬 배선이 필요 없다.</b> 씬이 로드되면 대상 루트를 찾아 스스로 생긴다. 대상이 없는 씬에선 안 생긴다.
    /// 프롭은 움직이지 않으므로 바운즈는 처음 한 번만 잰다.</para>
    public class ProtoDecorStreamer : MonoBehaviour
    {
        [Tooltip("캐릭터에서 이 거리(m) 안에 들어온 장식을 그린다. 화면은 약 22×12m — 화면 밖 여유까지 포함한 값.")]
        public float showRadius = 24f;
        [Tooltip("켜진 장식은 이만큼 더 멀어져야 끈다(m). 경계에서 켜졌다 꺼졌다 반복하지 않게.")]
        public float hideMargin = 4f;
        [Tooltip("판정 주기(초). 캐릭터가 빨라도 0.2초에 수 m — 여유 거리 안이다.")]
        public float interval = 0.2f;

        // 대상: 이름으로 찾는 루트 아래 렌더러 전부 + _Props 의 나무 그룹(Tree*)
        static readonly string[] RootNames = { "_GrassBlades", "_Foliage", "_LeafCardBushes_Test", "_LeafCardPlants_Test" };
        const string PropsRoot = "_Props", TreePrefix = "Tree";

        struct Item { public Renderer r; public Vector2 min, max; public bool on; }

        readonly List<Item> _items = new List<Item>();
        Transform _target;
        float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<ProtoDecorStreamer>() != null) return;
            // DontSave 를 붙이지 않는다 — 붙이면 플레이를 멈춰도 에디터에 남고, 다음 플레이에서 이미 있다고 보고
            // 새로 안 만든다. 남은 것은 지난 플레이의(이미 사라진) 렌더러를 들고 있어 아무것도 끄지 못한다.
            var go = new GameObject("_DecorStreamer");
            var s = go.AddComponent<ProtoDecorStreamer>();
            if (s.Collect() == 0) Destroy(go);
        }

        int Collect()
        {
            _items.Clear();
            var roots = new List<Transform>();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (System.Array.IndexOf(RootNames, t.name) >= 0) roots.Add(t);
                    else if (t.name.StartsWith(TreePrefix) && t.parent != null && t.parent.name == PropsRoot) roots.Add(t);
                }
            foreach (var root in roots)
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!r.enabled) continue;   // 원래 꺼 둔 렌더러는 건드리지 않는다(다가간다고 켜지면 안 된다)
                    var b = r.bounds;
                    _items.Add(new Item { r = r, min = new Vector2(b.min.x, b.min.z), max = new Vector2(b.max.x, b.max.z), on = true });
                }
            return _items.Count;
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + interval;
            if (_target == null)
            {
                var p = FindFirstObjectByType<ProtoPlayer>();
                if (p == null) return;
                _target = p.transform;
            }
            Vector3 tp = _target.position;
            var c = new Vector2(tp.x, tp.z);
            float show2 = showRadius * showRadius, hide2 = (showRadius + hideMargin) * (showRadius + hideMargin);
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                if (it.r == null) continue;
                // 바운즈 사각형에서 캐릭터에 가장 가까운 점까지
                float dx = Mathf.Max(it.min.x - c.x, 0f, c.x - it.max.x);
                float dz = Mathf.Max(it.min.y - c.y, 0f, c.y - it.max.y);
                float d2 = dx * dx + dz * dz;
                bool want = it.on ? d2 <= hide2 : d2 <= show2;
                if (want == it.on) continue;
                it.on = want;
                it.r.enabled = want;
                _items[i] = it;
            }
        }

        /// 플레이를 멈추거나 씬을 떠날 때 꺼 둔 렌더러를 되돌린다(에디터에서 플레이 중 저장해도 꺼진 채 남지 않게).
        void OnDestroy()
        {
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].r != null) _items[i].r.enabled = true;
        }
    }
}
