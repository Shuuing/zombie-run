using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 효과 카탈로그 — 효과 ID 하나에 "무엇을 그리나(프리팹 또는 코드 그림)"와 예산 규칙을 적는다.
    ///
    /// <para><b>왜 필요한가.</b> 지금 효과는 전부 코드가 메시·머티리얼까지 만들고(ProtoExplosion · ProtoBossFx …),
    /// 부르는 쪽도 제각각이다. 아트가 프리팹으로 바꿔 끼우려면 "코드는 때와 자리만, 모양은 에셋이"로 갈라야 하고,
    /// 그 경계가 이 카탈로그다. 코드는 <see cref="ProtoFx.Play"/> 에 ID 만 넘긴다.</para>
    ///
    /// <para><b>폴백.</b> 프리팹이 비어 있으면 <see cref="Entry.builtin"/> 이름의 코드 그림을 부른다
    /// (<see cref="ProtoFxBuiltins"/> 가 등록한다). 그래서 효과를 하나씩 프리팹으로 옮기는 동안에도 게임은 계속 돈다.
    /// 카탈로그 자체가 없어도 돈다 — 그때는 ID 를 코드 그림 이름으로 그대로 쓴다.</para>
    ///
    /// <para>Resources/Data/FxCatalog 에 하나 둔다. 만들기: Tools/좀비 오버런/FX 툴/카탈로그·시트 만들기.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "좀비 오버런/FX 카탈로그", fileName = "FxCatalog")]
    public class FxCatalog : ScriptableObject
    {
        public const string ResourcePath = "Data/FxCatalog";

        public enum Clock
        {
            /// 레벨업 카드·옵션이 시간을 멈추면 같이 멈춘다 (전투 효과 전부)
            Game,
            /// 계속 돈다 (UI · 캐릭터 교체처럼 정지 중에도 보여야 하는 것)
            Unscaled,
        }

        [System.Serializable]
        public class Entry
        {
            [Tooltip("코드가 부르는 이름. 예: boss_impact")]
            public string id;
            [Tooltip("이 효과가 무엇을 알려주는지 한 줄. 어느 층인지(정보 / 타격감 / 세리머니 / 분위기)도 적는다.")]
            public string note;

            [Header("모양")]
            [Tooltip("있으면 이 프리팹을 풀에서 꺼내 쓴다. 파티클·트레일·메시 무엇이든.")]
            public GameObject prefab;
            [Tooltip("프리팹이 비어 있을 때 부를 코드 그림 이름(ProtoFxBuiltins). 둘 다 비면 아무것도 안 나온다.")]
            public string builtin;
            [Tooltip("항상 위에 — 총·적 몸에 가려지지 않게 오버레이 카메라(FxOverlay 레이어, 깊이 지움)가 따로 그린다. 총구 화염·피격 불똥에 켜고, 바닥 효과·연기·피는 끈다. 프리팹 효과만.")]
            public bool onTop;

            [Header("예산")]
            [Tooltip("프리팹 원샷이 풀로 돌아가기까지(초). 0 이면 파티클이 전부 멈출 때.")]
            public float lifetime = 2f;
            [Tooltip("풀에 미리 만들어 둘 개수 — 아직은 필요할 때 만든다. 자리만 잡아 둔 값.")]
            public int poolSize = 4;
            [Tooltip("동시에 살아 있을 수 있는 수. 0 = 무제한. 넘으면 가장 오래된 것을 회수한다(물량전 보호).")]
            public int maxAlive = 0;
            public Clock clock = Clock.Game;

            [Header("쇼케이스")]
            [Tooltip("FX 쇼케이스에서 더미 좀비 기준 어디에 낼지(더미 로컬, m). 피격 불똥은 가슴(0, 0.95, 0), 바닥 효과는 0.")]
            public Vector3 previewOffset;
        }

        public List<Entry> entries = new List<Entry>();
        [Tooltip("한 번만 적용하는 기본값 손질 단계(FxAssetsSetup.Upgrades). 1 = 총구·피격 4종 onTop 켬(2026-11-05)")]
        public int upgrade;

        // ID → 항목 사전. 재생마다(타격 · 처치 · 젬) 52개를 문자열로 훑던 것을 바꿨다(2026-11-20 최적화).
        // 같은 ID 가 둘이면 예전처럼 앞의 것. 항목 수가 바뀌거나(에디터 도구가 더함) 인스펙터에서 고치면(OnValidate) 다시 만든다.
        [System.NonSerialized] Dictionary<string, Entry> _map;
        [System.NonSerialized] int _mapCount = -1;

        void BuildMap()
        {
            if (_map == null) _map = new Dictionary<string, Entry>(System.StringComparer.Ordinal); else _map.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && !string.IsNullOrEmpty(e.id) && !_map.ContainsKey(e.id)) _map[e.id] = e;
            }
            _mapCount = entries.Count;
        }

        /// 코드로 항목의 ID 를 바꿨을 때(개수는 그대로) 부른다.
        public void InvalidateLookup() { _map = null; _mapCount = -1; }

        void OnValidate() => InvalidateLookup();

        public Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_map == null || _mapCount != entries.Count) BuildMap();
            if (_map.TryGetValue(id, out var e))
            {
                if (e != null && e.id == id) return e;
                BuildMap();                                   // 그 사이 ID 가 바뀌었다 — 다시 만들어 한 번 더
                return _map.TryGetValue(id, out e) ? e : null;
            }
            return null;
        }

        static FxCatalog _loaded;
        static bool _tried;

        /// Resources 의 카탈로그. 없으면 null.
        public static FxCatalog Load()
        {
            if (_loaded == null && !_tried) { _tried = true; _loaded = Resources.Load<FxCatalog>(ResourcePath); }
            return _loaded;
        }

        /// 에셋을 새로 만들었을 때(에디터 메뉴) 다시 읽게 한다.
        public static void Reload() { if (_loaded != null) _loaded.InvalidateLookup(); _loaded = null; _tried = false; }
    }
}
