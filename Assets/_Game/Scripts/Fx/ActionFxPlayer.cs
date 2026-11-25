using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 시트(<see cref="ActionFxSheet"/>)의 동작 하나를 재생하는 커서.
    ///
    /// <para><b>시계는 부르는 쪽이 쥔다.</b> <see cref="Advance"/> 에 경과 시간을 넘긴다 — 보스 브레인의 <c>_t</c> 처럼
    /// 공격 배속을 이미 곱한 값이다. 그래서 레벨업 정지 · 공격 배속 · 에디터 스크럽이 전부 같은 코드로 돈다.
    /// 이벤트의 효과는 <see cref="ProtoFx"/> 로, 이름표(marker)는 <see cref="OnMarker"/> 로 나간다.</para>
    ///
    /// <para>소켓은 <see cref="sockets"/> 목록 → 없으면 계층에서 같은 이름의 트랜스폼. 보스 망치머리처럼 코드가 아는
    /// 자리는 <see cref="SetSocket"/> 으로 등록한다. 씬 배선 없이 런타임에 AddComponent 해도 된다.</para>
    /// </summary>
    public class ActionFxPlayer : MonoBehaviour
    {
        [Tooltip("비면 Resources/Data/ActionFxSheet")]
        public ActionFxSheet sheet;

        [System.Serializable]
        public class Socket { public string name; public Transform transform; }

        [Tooltip("이름 → 트랜스폼. 여기 없으면 계층에서 같은 이름을 찾는다")]
        public List<Socket> sockets = new List<Socket>();

        /// 이름표가 붙은 이벤트 시각을 지날 때. 피해 판정 같은 게임플레이가 받는다.
        public System.Action<string> OnMarker;

        ActionFxSheet.Entry _action;
        float _t = -1f;
        bool[] _fired, _ended;
        FxHandle[] _handles;
        Vector3 _target;
        float _radius, _scale = 1f;
        bool _big;
        readonly Dictionary<string, Transform> _cache = new Dictionary<string, Transform>();

        public bool Playing => _action != null;
        public ActionFxSheet.Entry Current => _action;
        public float Elapsed => _t;
        public ActionFxSheet Sheet => sheet != null ? sheet : ActionFxSheet.Load();

        public void SetSocket(string name, Transform t)
        {
            if (string.IsNullOrEmpty(name)) return;
            for (int i = 0; i < sockets.Count; i++)
                if (sockets[i] != null && sockets[i].name == name) { sockets[i].transform = t; _cache[name] = t; return; }
            sockets.Add(new Socket { name = name, transform = t });
            _cache[name] = t;
        }

        /// <summary>
        /// 동작 시작. 시트에 그 키가 없으면 false — 부르는 쪽은 예전 코드 경로로 간다.
        /// </summary>
        /// <param name="target">Target 앵커 자리(충돌점 · 착지점). 월드 좌표</param>
        /// <param name="radius">판정 반경 — 코드 그림이 링 크기로 쓴다</param>
        /// <param name="scale">덩치 배율</param>
        public bool Begin(string key, Vector3 target, float radius = 0f, bool big = false, float scale = 1f)
        {
            End();
            var s = Sheet;
            var a = s != null ? s.Find(key) : null;
            if (a == null) return false;
            _action = a;
            _t = -1f;
            _target = target; _radius = radius; _big = big; _scale = scale <= 0f ? 1f : scale;
            int n = a.events.Count;
            _fired = new bool[n]; _ended = new bool[n]; _handles = new FxHandle[n];
            return true;
        }

        public void SetTarget(Vector3 target) => _target = target;
        public Vector3 Target => _target;

        /// 경과 시간을 넘긴다(단조 증가). 그 사이 지난 이벤트를 전부 발화한다.
        public void Advance(float t)
        {
            if (_action == null) return;
            _t = t;
            var ev = _action.events;
            for (int i = 0; i < ev.Count && i < _fired.Length; i++)
            {
                var e = ev[i];
                if (e == null) continue;
                if (!_fired[i] && t >= e.time) { _fired[i] = true; Fire(i, e); }
                if (e.IsSpan && _fired[i] && !_ended[i] && t >= e.endTime) { _ended[i] = true; StopHandle(i); }
            }
        }

        /// <summary>
        /// 에디터 스크럽용. t 까지 지난 원샷은 발화 없이 지나간 것으로 치고, t 에 걸친 스팬만 살린다 —
        /// 플레이헤드를 끌 때마다 원샷이 터지면 안 된다. 이어서 <see cref="Advance"/> 하면 거기서부터 정상 재생.
        /// </summary>
        public void Seek(float t)
        {
            if (_action == null) return;
            StopSpans();
            _t = t;
            var ev = _action.events;
            for (int i = 0; i < ev.Count && i < _fired.Length; i++)
            {
                var e = ev[i];
                if (e == null) continue;
                _fired[i] = t >= e.time;
                _ended[i] = e.IsSpan && t >= e.endTime;
                if (e.IsSpan && _fired[i] && !_ended[i] && !string.IsNullOrEmpty(e.fxId))
                    _handles[i] = ProtoFx.Play(e.fxId, BuildContext(e), true);
            }
        }

        /// 처음으로. 발화 기록을 지우고 스팬을 끝낸다.
        public void Rewind()
        {
            StopSpans();
            _t = -1f;
            if (_fired != null) { System.Array.Clear(_fired, 0, _fired.Length); System.Array.Clear(_ended, 0, _ended.Length); }
        }

        /// 동작 끝 — 스팬 정리.
        public void End()
        {
            StopSpans();
            _action = null;
            _t = -1f;
        }

        public float MarkerTime(string marker, float fallback)
            => _action != null ? _action.MarkerTime(marker, fallback) : fallback;

        void StopSpans()
        {
            if (_handles == null) return;
            for (int i = 0; i < _handles.Length; i++) StopHandle(i);
        }

        void StopHandle(int i)
        {
            if (_handles == null || i >= _handles.Length || _handles[i] == null) return;
            _handles[i].Stop();
            _handles[i] = null;
        }

        void Fire(int i, ActionFxSheet.FxEvent e)
        {
            if (!string.IsNullOrEmpty(e.marker)) OnMarker?.Invoke(e.marker);
            if (e.kind != ActionFxSheet.Kind.Marker && !string.IsNullOrEmpty(e.fxId))
            {
                var h = ProtoFx.Play(e.fxId, BuildContext(e), e.IsSpan);
                if (e.IsSpan) _handles[i] = h;
            }
            if (!Application.isPlaying) return;   // 소리·흔들림·히트스톱은 플레이 중에만
            if (!string.IsNullOrEmpty(e.sfx)) ProtoAudio.Play(e.sfx, BuildContext(e).WorldPosition);
            if (e.shake != ActionFxSheet.Shake.None) ProtoCameraFollow.Shake((ProtoCameraFollow.Impact)((int)e.shake - 1));
            if (e.hitstop > 0f) ProtoFxRunner.Hitstop(e.hitstop);
        }

        /// 이벤트 하나의 효과 문맥(자리 · 앵커 · 반경 · 덩치). 에디터가 기즈모 자리에도 쓴다.
        public FxContext BuildContext(ActionFxSheet.FxEvent e)
        {
            var ctx = FxContext.At(transform.position);
            ctx.scale = _scale * (e.scale <= 0f ? 1f : e.scale);
            ctx.radius = _radius;
            ctx.big = _big;
            ctx.offset = e.offset;
            ctx.localRotation = Quaternion.Euler(e.euler);
            switch (e.anchor)
            {
                case ActionFxSheet.Anchor.Root:
                    ctx.anchor = transform; ctx.follow = e.follow;
                    break;
                case ActionFxSheet.Anchor.Socket:
                    var s = Resolve(e.socket);
                    ctx.anchor = s != null ? s : transform; ctx.follow = e.follow;
                    break;
                case ActionFxSheet.Anchor.Target:
                    ctx.anchor = null; ctx.follow = false;
                    ctx.position = _target + transform.rotation * e.offset;
                    ctx.rotation = transform.rotation * Quaternion.Euler(e.euler);
                    break;
            }
            return ctx;
        }

        /// 소켓 이름 → 트랜스폼. "Root"·빈 이름은 루트.
        public Transform Resolve(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "Root") return transform;
            for (int i = 0; i < sockets.Count; i++)
                if (sockets[i] != null && sockets[i].name == name && sockets[i].transform != null) return sockets[i].transform;
            if (_cache.TryGetValue(name, out var t) && t != null) return t;
            t = FindDeep(transform, name);
            if (t != null) _cache[name] = t;
            return t;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        void OnDisable() { End(); }
    }
}
