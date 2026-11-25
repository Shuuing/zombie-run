using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 피해를 주는 쪽이 좀비에게 같이 넘기는 "맞은 자리 연출" 지시. 무기 연출 세트(<see cref="WeaponFxSet"/>)에서 만든다.
    /// 안 넘기면(기본값, specified = false) 좀비가 공통 효과(hit_spark · kill_burst)를 가슴 높이 추정 자리에 낸다 —
    /// 접촉·탱크·울트 같은 무기 밖의 피해 경로는 손대지 않아도 되게.
    /// </summary>
    public struct HitFx
    {
        public bool specified;
        public string spark;      // 불똥 ID. null/빈 = 없음
        public float sparkScale;
        public string kill;       // 처치 ID. null/빈 = 없음
        public float killScale;
        public bool hasPoint;     // 정확한 착탄점을 아는가(투사체·광선). 모르면 좀비가 가슴 높이로 추정
        public Vector3 point;

        /// 닿은 자리를 아는 경로(투사체 · 광선 · 블레이드).
        public static HitFx From(WeaponFxSet set, Vector3 point, bool withSpark = true)
        {
            if (set == null) set = WeaponFxSet.Default;
            return new HitFx
            {
                specified = true,
                spark = withSpark && set.hit != null && set.hit.On ? set.hit.fxId : null,
                sparkScale = set.hit != null ? set.hit.scale : 1f,
                kill = set.kill != null && set.kill.On ? set.kill.fxId : null,
                killScale = set.kill != null ? set.kill.scale : 1f,
                hasPoint = true,
                point = point,
            };
        }

        /// 폭발 범위 안의 적 — 자리 불똥은 내지 않는다(폭발이 이미 보여 준다). 처치 효과는 세트대로.
        public static HitFx Blast(WeaponFxSet set)
        {
            var h = From(set, Vector3.zero, withSpark: false);
            h.hasPoint = false;
            return h;
        }
    }
}
