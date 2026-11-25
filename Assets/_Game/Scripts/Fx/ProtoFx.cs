using System;
using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 효과를 부를 때 넘기는 문맥. <b>코드가 아는 게임플레이 값</b>(자리 · 판정 반경 · 덩치)은 여기에,
    /// 모양은 카탈로그(<see cref="FxCatalog"/>)에. 코드 그림(builtin)과 프리팹이 같은 문맥을 받는다.
    /// </summary>
    public struct FxContext
    {
        public Vector3 position;        // anchor 가 없을 때의 월드 위치
        public Quaternion rotation;     // anchor 가 없을 때의 월드 회전
        public float scale;             // 덩치 배율(최종 보스 1.3 등). 0 이면 1
        public Transform anchor;        // 기준 트랜스폼(본·소켓). 있으면 position 대신 anchor + offset
        public bool follow;             // 사는 동안 anchor 를 따라다닌다
        public Vector3 offset;          // anchor 로컬 오프셋
        public Quaternion localRotation;
        public float radius;            // 판정 반경 등 — 코드 그림이 쓴다(폭발 링 테두리 = 반경)
        public bool big;                // 큰 한 방(도약 착지 등)
        public bool quiet;              // 흔들림·소리 없이(탱크 포의 착탄 폭발처럼 화면을 흔들면 안 되는 자리)
        public Color color;             // 알파 0 = 기본색

        public static FxContext At(Vector3 pos)
            => new FxContext { position = pos, rotation = Quaternion.identity, scale = 1f, localRotation = Quaternion.identity };

        public static FxContext On(Transform anchor, bool follow = true)
        {
            var c = At(anchor != null ? anchor.position : Vector3.zero);
            c.anchor = anchor; c.follow = follow;
            if (anchor != null) c.rotation = anchor.rotation;
            return c;
        }

        public Vector3 WorldPosition => anchor != null ? anchor.TransformPoint(offset) : position;
        public Quaternion WorldRotation => anchor != null ? anchor.rotation * localRotation : rotation;
    }

    /// <summary>살아 있는 효과 하나. 스팬(켜고 끄는 효과)은 <see cref="Stop"/> 으로 끝낸다.</summary>
    public class FxHandle
    {
        internal FxCatalog.Entry entry;
        internal string id;
        internal GameObject go;
        internal ParticleSystem[] particles;
        internal Vector3 baseScale = Vector3.one;
        internal ProtoFx.Builtin builtin;
        internal FxContext ctx;
        internal float age;
        internal bool span;
        internal bool alive;

        public bool Alive => alive;
        public string Id => id;
        public GameObject Instance => go;
        public void Stop() => ProtoFx.Stop(this);
        /// detach = true 면 앵커를 더는 따라가지 않는다 — 앵커가 풀로 돌아가 다른 자리에서 다시 쓰일 때(투사체)
        /// 사그라드는 잔여 입자·트레일이 새 자리로 끌려가지 않게.
        public void Stop(bool detach)
        {
            if (detach) ctx.follow = false;
            ProtoFx.Stop(this);
        }
        /// 바로 거둔다 — 남은 입자를 기다리지 않는다(<see cref="ProtoFx.Kill"/>).
        public void Kill() => ProtoFx.Kill(this);
        /// 앵커 없이 낸 효과를 코드가 직접 옮긴다(회전 · 배율은 그대로). 풀에서 돌려 쓰는 물체(물고기 젬)처럼
        /// 앵커 트랜스폼을 넘기기 곤란할 때 매 프레임 부른다. 이미 거둔 핸들이면 아무것도 안 한다 —
        /// 거둔 인스턴스는 다른 핸들이 다시 빌려 쓰고 있을 수 있다(2026-11-18).
        public void MoveTo(Vector3 pos)
        {
            ctx.position = pos;
            if (alive && go != null) go.transform.position = pos;
        }

        /// 앵커 자리 · 회전에 지금 바로 맞춘다. 앵커를 매 프레임 옮기는 쪽이 옮긴 직후 부른다 — 틱(ProtoFxRunner.Update)이
        /// 그쪽 Update 보다 먼저 돌면 한 프레임 늦게 따라가 빠른 물체(회전 블레이드 11m/s) 뒤에서 꼬리가 떨어진다(2026-11-18).
        public void SnapToAnchor()
        {
            if (!alive || go == null || ctx.anchor == null) return;
            go.transform.SetPositionAndRotation(ctx.WorldPosition, ctx.WorldRotation);
        }

        /// 프리팹 안에서 시뮬레이션 공간을 <b>Custom</b> 으로 둔 파티클 층을 이 트랜스폼 기준으로 돌린다. 월드 · 로컬 층은 그대로.
        /// 회전 블레이드 꼬리(2026-11-18) — 궤도 루트(플레이어를 따라다니고 돌지 않는다)에 묶으면 플레이어가 달려도 꼬리가 궤도 원을 그린다.
        /// 묶지 않은 Custom 층은 월드처럼 돈다. 풀에서 다시 빌려 쓰면 예전 공간이 남아 있으니 낼 때마다 부른다.
        public void BindSimulationSpace(Transform space)
        {
            if (!alive || particles == null) return;
            for (int i = 0; i < particles.Length; i++)
            {
                var ps = particles[i];
                if (ps == null) continue;
                var m = ps.main;
                if (m.simulationSpace == ParticleSystemSimulationSpace.Custom) m.customSimulationSpace = space;
            }
        }
    }

    /// <summary>
    /// 효과 진입점 — 코드는 ID 와 문맥만 넘긴다. 카탈로그에 프리팹이 있으면 풀에서 꺼내 쓰고, 없으면 등록된
    /// 코드 그림(<see cref="RegisterBuiltin"/>)으로 폴백한다. 기존 ProtoExplosion · ProtoBossFx 는 그대로 두고
    /// 이름만 붙여 놓은 것이라(<see cref="ProtoFxBuiltins"/>), 효과를 하나씩 프리팹으로 옮겨도 게임이 안 깨진다.
    ///
    /// <para>틱은 플레이 중엔 <see cref="ProtoFxRunner"/> 가, 에디터 미리보기에선 창이 직접 <see cref="Tick"/> 을 부른다.
    /// 코드 그림은 플레이 중에만 돈다(Time 에 묶여 있다) — 에디터에선 프리팹 효과만 보인다.</para>
    /// </summary>
    public static class ProtoFx
    {
        internal class Builtin
        {
            public Action<FxContext> begin;
            public Action<FxContext, float> tick;   // 스팬일 때 매 프레임(dt)
            public Action<FxContext> end;           // 스팬 종료
        }

        static readonly Dictionary<string, Builtin> _builtins = new Dictionary<string, Builtin>();
        static readonly Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
        static readonly List<FxHandle> _live = new List<FxHandle>();
        static readonly Dictionary<GameObject, ParticleSystem[]> _particlesOf = new Dictionary<GameObject, ParticleSystem[]>();
        static Transform _poolRoot;

        /// 코드 그림을 이름으로 등록한다. 원샷은 begin 만, 스팬은 begin / tick / end.
        public static void RegisterBuiltin(string name, Action<FxContext> begin, Action<FxContext, float> tick = null, Action<FxContext> end = null)
        {
            if (string.IsNullOrEmpty(name)) return;
            _builtins[name] = new Builtin { begin = begin, tick = tick, end = end };
        }

        public static bool HasBuiltin(string name) => !string.IsNullOrEmpty(name) && _builtins.ContainsKey(name);

        /// 카탈로그 항목에 프리팹이 꽂혀 있는가. 코드 그림이 따로 있는 효과(독가스 퍼프 · 독 구슬 등)가
        /// "프리팹이 있으면 내 그림을 숨기고 프리팹을 낸다"를 고를 때 쓴다(2026-11-12).
        public static bool HasPrefab(string id)
        {
            var cat = FxCatalog.Load();
            var e = cat != null ? cat.Find(id) : null;
            return e != null && e.prefab != null;
        }
        public static IEnumerable<string> BuiltinNames => _builtins.Keys;
        public static IReadOnlyList<FxHandle> Live => _live;

        /// <summary>
        /// 효과를 낸다. <paramref name="span"/> 이면 <see cref="Stop"/> 할 때까지 산다(망치 궤적처럼 켜고 끄는 것).
        /// 돌려주는 핸들은 원샷이면 바로 죽어 있을 수 있다(코드 그림 원샷) — 들고 있을 필요 없다.
        /// </summary>
        /// 자리·방향·크기만으로 원샷을 낸다 — 총구 화염·피격 불똥처럼 앵커 없이 한 점에서 터지는 것.
        /// dir 이 0 이면 회전 없음(위를 보는 효과는 프리팹 안에서 정한다).
        public static FxHandle Play(string id, Vector3 pos, Vector3 dir, float scale = 1f, bool big = false)
        {
            var c = FxContext.At(pos);
            if (dir.sqrMagnitude > 0.0001f) c.rotation = Quaternion.LookRotation(dir);
            c.scale = scale;
            c.big = big;
            return Play(id, c);
        }

        /// 반경이 있는 폭발류 원샷 — 코드 그림은 링 테두리 = 판정 반경으로 그린다. quiet 면 흔들림·소리 없이.
        public static FxHandle PlayBlast(string id, Vector3 pos, float radius, bool big = false, bool quiet = false, float scale = 1f)
        {
            var c = FxContext.At(pos);
            c.radius = radius;
            c.big = big;
            c.quiet = quiet;
            c.scale = scale;
            return Play(id, c);
        }

        public static FxHandle Play(string id, FxContext ctx, bool span = false)
        {
            var h = new FxHandle { id = id, ctx = ctx, span = span };
            if (string.IsNullOrEmpty(id)) return h;
            if (h.ctx.scale <= 0f) h.ctx.scale = 1f;

            var cat = FxCatalog.Load();
            var entry = cat != null ? cat.Find(id) : null;
            h.entry = entry;
            GameObject prefab = entry != null ? entry.prefab : null;
            string builtinName = entry != null ? entry.builtin : id;   // 카탈로그에 없으면 ID 를 코드 그림 이름으로

            if (prefab != null)
            {
                if (entry.maxAlive > 0) EvictOldest(id, entry.maxAlive - 1);
                h.go = Rent(id, prefab);
                FxOverlay.Apply(h.go, entry.onTop);   // 항상 위에(총구·피격) — 오버레이 레이어로, 아니면 Default 로
                h.baseScale = prefab.transform.localScale;
                // 인스턴스마다 한 번만 훑어 둔다 — 풀에서 다시 나온 것은 같은 배열(2026-11-20 최적화: 예전엔 재생마다 새 배열)
                if (!_particlesOf.TryGetValue(h.go, out var ps0) || ps0 == null)
                    _particlesOf[h.go] = ps0 = h.go.GetComponentsInChildren<ParticleSystem>(true);
                h.particles = ps0;
                Place(h);
                h.go.SetActive(true);
                for (int i = 0; i < h.particles.Length; i++)
                {
                    var ps = h.particles[i];
                    if (ps == null) continue;
                    ps.Clear(false);
                    ps.Play(false);
                }
                h.alive = true;
            }
            else if (!string.IsNullOrEmpty(builtinName) && _builtins.TryGetValue(builtinName, out var b))
            {
                if (!Application.isPlaying) return h;          // 코드 그림은 Time 에 묶여 있어 플레이 중에만
                h.builtin = b;
                b.begin?.Invoke(h.ctx);
                if (!span) return h;                           // 원샷 코드 그림은 여기서 끝(자기 수명은 자기가 관리)
                h.alive = true;
            }
            else return h;

            ProtoFxRunner.Ensure();
            _live.Add(h);
            return h;
        }

        /// 스팬을 끝낸다. 프리팹이면 방출만 멈추고 남은 입자가 사라진 뒤 회수한다 — 뚝 끊기지 않게.
        public static void Stop(FxHandle h)
        {
            if (h == null || !h.alive) return;
            if (h.builtin != null)
            {
                h.builtin.end?.Invoke(h.ctx);
                h.alive = false;
                _live.Remove(h);
                return;
            }
            if (h.go != null && h.particles != null && h.particles.Length > 0)
            {
                for (int i = 0; i < h.particles.Length; i++)
                    if (h.particles[i] != null) h.particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
                h.span = false;   // 이제부터 원샷처럼 수명 판정
                h.age = 0f;
                return;
            }
            Return(h);
        }

        /// 하나를 바로 거둔다 — Stop 과 달리 남은 입자가 사그라들기를 기다리지 않고 꺼서 풀에 넣는다.
        /// 투사체가 맞았거나 화면 밖으로 나갔을 때처럼, 남은 입자가 그 자리에 떠 있으면 어색한 경우(2026-11-12 혼불탄 "딱지").
        public static void Kill(FxHandle h)
        {
            if (h == null || !h.alive) return;
            if (h.builtin != null)
            {
                h.builtin.end?.Invoke(h.ctx);
                h.alive = false;
                _live.Remove(h);
                return;
            }
            Return(h);
        }

        /// 살아 있는 것을 전부 끝낸다(결과 화면 · 재시작 · 에디터 정리).
        public static void StopAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var h = _live[i];
                if (h.builtin != null) h.builtin.end?.Invoke(h.ctx);
                else if (h.go != null) { h.go.SetActive(false); ParkInPool(h.go); Pool(h.id).Enqueue(h.go); }
                h.alive = false;
            }
            _live.Clear();
        }

        /// 풀까지 비운다(씬 전환 · 에디터 미리보기 정리).
        public static void Clear()
        {
            StopAll();
            ZombieGibs.Clear();   // 몸 조각(코드 그림)도 같이
            foreach (var q in _pools.Values)
                while (q.Count > 0) { var g = q.Dequeue(); if (g != null) DestroyAny(g); }
            _pools.Clear();
            _particlesOf.Clear();
            FxOverlay.Forget();
            if (_poolRoot != null) { DestroyAny(_poolRoot.gameObject); _poolRoot = null; }
        }

        /// 한 프레임. 플레이 중엔 ProtoFxRunner 가, 에디터 미리보기에선 창이 부른다.
        public static void Tick(float gameDt, float unscaledDt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var h = _live[i];
                if (!h.alive) { _live.RemoveAt(i); continue; }
                float dt = (h.entry != null && h.entry.clock == FxCatalog.Clock.Unscaled) ? unscaledDt : gameDt;
                h.age += dt;
                if (h.ctx.follow && h.ctx.anchor != null) Place(h);

                if (h.builtin != null) { h.builtin.tick?.Invoke(h.ctx, dt); continue; }   // 스팬 코드 그림: Stop 까지
                if (h.go == null) { h.alive = false; _live.RemoveAt(i); continue; }

                // 에디터에선 파티클이 스스로 돌지 않는다 — 수동으로 진행시킨다(스크럽·배속이 그대로 먹는다)
                if (!Application.isPlaying && dt > 0f && h.particles != null)
                    for (int k = 0; k < h.particles.Length; k++)
                        if (h.particles[k] != null) h.particles[k].Simulate(dt, false, false);

                if (h.span) continue;
                float life = h.entry != null ? h.entry.lifetime : 2f;
                bool done = life > 0f ? h.age >= life : !AnyAlive(h.particles);
                if (done) Return(h);
            }
        }

        // ── 풀 ───────────────────────────────────────────────────────
        static Transform PoolRoot
        {
            get
            {
                if (_poolRoot == null)
                {
                    var go = new GameObject("_FxPool");
                    go.hideFlags = HideFlags.DontSave;
                    _poolRoot = go.transform;
                }
                return _poolRoot;
            }
        }

        static Queue<GameObject> Pool(string id)
        {
            if (!_pools.TryGetValue(id, out var q)) { q = new Queue<GameObject>(); _pools[id] = q; }
            return q;
        }

        static GameObject Rent(string id, GameObject prefab)
        {
            var q = Pool(id);
            while (q.Count > 0) { var g = q.Dequeue(); if (g != null) { g.transform.SetParent(null, false); return g; } }
            var go = UnityEngine.Object.Instantiate(prefab);
            go.name = "FX_" + id;
            if (!Application.isPlaying) go.hideFlags = HideFlags.DontSave;   // 미리보기 중 씬에 저장되지 않게
            return go;
        }

        static void Return(FxHandle h)
        {
            h.alive = false;
            _live.Remove(h);
            if (h.go == null) return;
            h.go.SetActive(false);
            ParkInPool(h.go);
            Pool(h.id).Enqueue(h.go);
        }

        static void ParkInPool(GameObject go) => go.transform.SetParent(PoolRoot, false);

        static void EvictOldest(string id, int keep)
        {
            int count = 0;
            for (int i = 0; i < _live.Count; i++) if (_live[i].id == id && _live[i].go != null) count++;
            for (int i = 0; i < _live.Count && count > keep; i++)
                if (_live[i].id == id && _live[i].go != null) { Return(_live[i]); i--; count--; }
        }

        static void Place(FxHandle h)
        {
            if (h.go == null) return;
            var t = h.go.transform;
            t.SetPositionAndRotation(h.ctx.WorldPosition, h.ctx.WorldRotation);
            t.localScale = h.baseScale * h.ctx.scale;
        }

        static bool AnyAlive(ParticleSystem[] ps)
        {
            if (ps == null || ps.Length == 0) return false;
            for (int i = 0; i < ps.Length; i++) if (ps[i] != null && ps[i].IsAlive(false)) return true;
            return false;
        }

        static void DestroyAny(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// 런타임 틱 + 히트스톱. 처음 효과를 낼 때 스스로 생긴다(씬 배선 없음).
    /// 히트스톱은 아주 짧게(0.03~0.06s) 시간을 멈추는 타격감 장치 — 레퍼런스 뱀서라이크 대부분이 치명타·처치에 쓴다.
    /// 레벨업 카드·옵션이 이미 시간을 쥐고 있으면 건드리지 않는다.
    /// </summary>
    public class ProtoFxRunner : MonoBehaviour
    {
        static ProtoFxRunner _i;
        float _hitstopUntil = -1f;

        /// 히트스톱이 끝나고 돌아갈 배속. 평소 1. FX 쇼케이스가 슬로모션(0.25 등)으로 보는 동안 바꿔 둔다 —
        /// 아니면 히트스톱 한 번에 슬로모션이 풀린다.
        public static float BaseTimeScale = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _i = null; BaseTimeScale = 1f; }

        public static void Ensure()
        {
            if (_i != null || !Application.isPlaying) return;
            var go = new GameObject("_ProtoFx");
            go.hideFlags = HideFlags.DontSave;
            _i = go.AddComponent<ProtoFxRunner>();
        }

        public static void Hitstop(float seconds)
        {
            Ensure();
            if (_i == null || seconds <= 0f) return;
            if (Time.timeScale < BaseTimeScale - 0.001f && _i._hitstopUntil < 0f) return;   // 다른 누군가가 멈춘 상태 — 끼어들지 않는다
            _i._hitstopUntil = Mathf.Max(_i._hitstopUntil, Time.unscaledTime + seconds);
            Time.timeScale = 0f;
        }

        void Update()
        {
            ProtoFx.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            if (_hitstopUntil >= 0f && Time.unscaledTime >= _hitstopUntil)
            {
                _hitstopUntil = -1f;
                // 히트스톱 중에 레벨업이 시작됐으면 그쪽이 시간을 쥔다 — 되돌리지 않는다
                if (ProtoRun.I == null || ProtoRun.I.IsRunning) Time.timeScale = BaseTimeScale;
            }
        }

        void OnDestroy()
        {
            if (_i != this) return;
            _i = null;
            ProtoFx.Clear();
        }
    }
}
