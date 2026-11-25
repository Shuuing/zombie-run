using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 액션 FX 시트 — 동작(클립) 하나에 "언제 · 어디서 · 무엇을" 이벤트 목록.
    ///
    /// <para>보스 내려찍기의 준비 / 휘두름 / 충돌 시각처럼 손으로 재서 코드에 박았던 상수가 여기로 온다.
    /// 코드는 <see cref="ActionFxPlayer"/> 로 동작을 시작하고 경과 시간을 넘길 뿐이다. <b>피해 판정은 코드에 남고</b>,
    /// 그 시각만 <see cref="FxEvent.marker"/>("impact" 등)로 시트에서 읽는다 — 아트가 연출 시각을 옮기면 판정도 따라온다.</para>
    ///
    /// <para>Resources/Data/ActionFxSheet 에 하나. 편집: Tools/좀비 오버런/FX 툴/FX 툴 창 (액션 시트 탭).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "좀비 오버런/액션 FX 시트", fileName = "ActionFxSheet")]
    public class ActionFxSheet : ScriptableObject
    {
        public const string ResourcePath = "Data/ActionFxSheet";

        public enum Kind
        {
            /// 그 순간 한 번
            OneShot,
            /// time 에 켜고 endTime 에 끈다(망치 불 · 궤적)
            Span,
            /// 효과 없이 코드에 시각만 알린다(종료 등)
            Marker,
        }

        public enum Anchor
        {
            /// 동작 주인의 루트(발밑)
            Root,
            /// 본·소켓 이름으로 찾는다(HammerHead · Muzzle …)
            Socket,
            /// 코드가 넘긴 지점(충돌점 · 착지점). 에디터에선 previewTarget
            Target,
        }

        public enum Shake { None, Light, Medium, Heavy, Huge }

        [System.Serializable]
        public class FxEvent
        {
            public string name = "이벤트";
            public Kind kind = Kind.OneShot;
            [Tooltip("시작 시각(초). 1배속 클립 기준 — 공격 배속은 부르는 쪽이 곱한다.")]
            public float time;
            [Tooltip("Span 의 끝 시각(초)")]
            public float endTime;
            [Tooltip("FxCatalog 의 ID. Marker 는 비워 둔다.")]
            public string fxId;
            public Anchor anchor = Anchor.Socket;
            [Tooltip("본·소켓 이름(Anchor.Socket). 플레이어 sockets 목록 → 없으면 계층에서 같은 이름을 찾는다")]
            public string socket;
            public Vector3 offset;
            public Vector3 euler;
            public float scale = 1f;
            [Tooltip("켜면 사는 동안 앵커를 따라다닌다")]
            public bool follow = true;
            [Tooltip("ProtoAudio 키. 비면 없음")]
            public string sfx;
            public Shake shake = Shake.None;
            [Tooltip("히트스톱(초). 0 = 없음")]
            public float hitstop;
            [Tooltip("코드가 읽는 이름표(impact / end …). 비면 없음")]
            public string marker;

            public float EndOrTime => kind == Kind.Span ? Mathf.Max(time, endTime) : time;
            public bool IsSpan => kind == Kind.Span;

            public FxEvent Clone()
            {
                return new FxEvent
                {
                    name = name, kind = kind, time = time, endTime = endTime, fxId = fxId, anchor = anchor, socket = socket,
                    offset = offset, euler = euler, scale = scale, follow = follow, sfx = sfx, shake = shake, hitstop = hitstop, marker = marker,
                };
            }
        }

        [System.Serializable]
        public class Entry
        {
            [Tooltip("코드가 부르는 키. 예: Boss_Final/Slam")]
            public string key;
            [Tooltip("미리보기·길이에 쓰는 클립")]
            public AnimationClip clip;
            [Tooltip("에디터 미리보기 모델. 비면 시트 기본값")]
            public GameObject previewPrefab;
            [Tooltip("Target 앵커의 미리보기 자리(루트 기준 로컬). 실제 게임에서는 코드가 넘긴다")]
            public Vector3 previewTarget = new Vector3(0f, 0f, 2f);
            [Tooltip("미리보기 판정 반경(Target 원)")]
            public float previewRadius = 2f;
            [Tooltip("미리보기 덩치 배율(최종 보스 1.3 등)")]
            public float previewScale = 1f;
            public List<FxEvent> events = new List<FxEvent>();

            /// 동작 길이 — 클립 길이와 마지막 이벤트 중 긴 쪽.
            public float Length
            {
                get
                {
                    float l = clip != null ? clip.length : 0f;
                    for (int i = 0; i < events.Count; i++) if (events[i] != null) l = Mathf.Max(l, events[i].EndOrTime);
                    return Mathf.Max(0.1f, l);
                }
            }

            public bool HasMarker(string marker)
            {
                if (string.IsNullOrEmpty(marker)) return false;
                for (int i = 0; i < events.Count; i++) if (events[i] != null && events[i].marker == marker) return true;
                return false;
            }

            /// 이름표가 붙은 이벤트의 시각. 없으면 fallback.
            public float MarkerTime(string marker, float fallback)
            {
                if (string.IsNullOrEmpty(marker)) return fallback;
                for (int i = 0; i < events.Count; i++) if (events[i] != null && events[i].marker == marker) return events[i].time;
                return fallback;
            }
        }

        [Tooltip("액션에 미리보기 모델이 없을 때 쓰는 기본 모델")]
        public GameObject defaultPreviewPrefab;
        public List<Entry> actions = new List<Entry>();

        public Entry Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            for (int i = 0; i < actions.Count; i++)
                if (actions[i] != null && actions[i].key == key) return actions[i];
            return null;
        }

        static ActionFxSheet _loaded;
        static bool _tried;

        /// Resources 의 시트. 없으면 null — 그러면 부르는 쪽은 예전 코드 경로로 간다.
        public static ActionFxSheet Load()
        {
            if (_loaded == null && !_tried) { _tried = true; _loaded = Resources.Load<ActionFxSheet>(ResourcePath); }
            return _loaded;
        }

        public static void Reload() { _loaded = null; _tried = false; }
    }
}
