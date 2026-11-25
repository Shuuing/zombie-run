using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 무기 진화표 — 기본 주무기 → 진화 무기.
    /// Resources/Data/EvolutionTable 에 하나 둔다(<see cref="ProtoEvolution"/> 이 읽는다).
    /// 기획: 「무기 진화 시스템 기획」 문서(2026-10-20). 짝 패시브(catalystId)는 2026-10-21 조건에서 빠졌다.
    /// </summary>
    [CreateAssetMenu(menuName = "좀비 오버런/무기 진화표", fileName = "EvolutionTable")]
    public class ProtoEvolutionTable : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("기본 주무기(캐릭터가 처음 드는 것)")]
            public WeaponModelDef from;
            [Tooltip("진화 무기")]
            public WeaponModelDef to;
            [Tooltip("무엇이 바뀌는지 한 줄 (기획 메모 — 진화 카드가 없어져 지금은 화면에 안 나온다)")]
            public string blurb;
        }

        public Entry[] entries = new Entry[0];

        public Entry FindFrom(WeaponModelDef w)
        {
            if (w == null) return null;
            foreach (var e in entries) if (e != null && e.from == w) return e;
            return null;
        }

        public Entry FindTo(WeaponModelDef w)
        {
            if (w == null) return null;
            foreach (var e in entries) if (e != null && e.to == w) return e;
            return null;
        }
    }
}
