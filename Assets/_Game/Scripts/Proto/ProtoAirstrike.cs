using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 융단 폭격 (기획서 §05 — 로켓 발사기 진화: 2.2초마다 화면 6지점, 지점당 260).
    ///
    /// <para><b>프로토 임시 배치:</b> 지금은 필사기(ULTIMATE) 버튼이 이걸 부른다. 정식 게임에서는 기획서대로
    /// 로켓 발사기 진화 무기로 붙인다 — 그래서 수치·연출은 §05 를 따르고 발동 경로만 임시다.</para>
    ///
    /// <para><b>연출:</b> 매 회차마다 공격헬기 그림자가 플레이어 근처를 대각선으로 가로지르고, 그림자가 지나는
    /// 자리를 따라 미사일(<c>airstrike_bomb_01</c>)이 떨어진다. 떨어질 지점은 경로 근처의 좀비 쪽으로 붙인다.
    /// 폭발·피해·소리는 로켓 착탄과 같은 <see cref="ProtoExplosion"/> + 반경 피해(링 테두리 = 판정 반경).</para>
    ///
    /// <para>게임 시간(scaled)으로 돈다 — 레벨업 카드로 멈추면 폭격도 멈춘다. 씬 배선 없음(지연 생성 싱글턴).</para>
    /// </summary>
    public class ProtoAirstrike : MonoBehaviour
    {
        // ── §05 수치 ─────────────────────────────────────────────────────
        public const float Interval = 2.2f;     // 회차 간격
        public const int   Points   = 6;        // 회차당 지점
        public const float Damage   = 260f;     // 지점당 피해(공격력 배율을 곱한다)
        public const float Radius   = 2.4f;     // 폭발 반경 — 로켓 착탄과 같다(§05 에 별도 값 없음)
        // ── 프로토 임시 ─────────────────────────────────────────────────
        public const int   Waves    = 3;        // 필사기 1회 = 3회차(컷인 8초 동안)
        const float FirstWave = 0.6f;

        // ── 연출 ─────────────────────────────────────────────────────────
        const float HeliSpeed   = 15f;          // 그림자 이동 속도(m/s)
        const float HeliHalfLen = 20f;          // 경로 반길이 — 화면(가로 ~11m) 밖에서 들어와 밖으로 나간다
        const float HeliLength  = 7.5f;         // 그림자 동체 길이(m)
        const float RotorSize   = 7.0f;         // 로터 원판 지름(m)
        const float ShadowY     = 0.04f;
        const float DropHeight  = 9f;           // 카메라(높이 ~15m)에 너무 가까우면 원근으로 미사일이 거대해 보인다
        const float FallTime    = 0.55f;
        const float BombScale   = 1.0f;

        static ProtoAirstrike _instance;

        // silent = 헬기 소리 없이(무전기 무기는 2.2초마다 부르므로 14초짜리 소리를 매번 겹치지 않는다), ult = 필사기가 부른 회차
        class Wave { public float t0; public Vector3 a, dir; public bool shadowOn; public AudioSource src; public float fade = -1f; public bool silent, ult; }
        class Bomb { public float drop; public Vector3 target; public Vector3 from; public Transform tf; public bool done; public float dmg; public WeaponFxSet fx; }

        readonly List<Wave> _waves = new List<Wave>();
        readonly List<Bomb> _bombs = new List<Bomb>();
        readonly Stack<Transform> _bombPool = new Stack<Transform>();
        float _t = -1f;
        Transform _heli, _rotor;
        GameObject _bombPrefab;
        bool _built;

        // ── 헬기 소리: 도시 배경음의 통과음(Resources/Ambience/heli_N, 14초)을 다시 쓴다 ──
        // 그 클립은 길이의 65% 지점에서 머리 위를 지나도록 만들어져 있다(CityAmbience peakAt 0.65).
        // 그래서 그림자가 화면 한가운데(경로 중앙)에 오는 순간에 그 지점이 오도록 클립 중간부터 튼다.
        const float HeliPeakAt = 0.65f;
        const float HeliVolume = 0.9f;
        AudioClip[] _heliClips;
        int _heliNext;
        Camera _cam;

        public static bool Active => _instance != null && _instance._t >= 0f;

        /// 큰 공습(F3 디버그)이 도는 중인가. 데몰리셔 진화는 판 내내 폭격하므로, "폭격 중이면 미룬다"는 쪽(헬기 추락)은 이걸 본다.
        public static bool UltimateActive
        {
            get
            {
                if (_instance == null || _instance._t < 0f) return false;
                foreach (var w in _instance._waves) if (w.ult) return true;
                return false;
            }
        }

        // ── 무전기 무기(로켓 발사기 진화) — 한 회차씩 ───────────────────────
        const float StrikeLead = 0.15f;   // 부른 뒤 헬기가 들어오기까지
        // 회차 3번에 한 번만 헬기 소리 — 데몰리셔 진화는 5초마다 부르므로 15초 간격(14초짜리 소리가 안 겹친다).
        // 폭격 간격(WS_AirstrikeRadio.airstrikeEvery)을 줄이면 이 값도 같이 볼 것.
        const int   StrikeVoiceEvery = 3;
        int _strikeCount;

        /// 무전기 한 회차: 헬기 한 대가 center 근처를 가로지르며 Points 지점을 폭격한다(지점당 damagePerPoint × 공격력 배율).
        /// 필사기 폭격과 같이 돌아도 서로 끊지 않는다.
        public static void Strike(Vector3 center, float damagePerPoint, WeaponFxSet fx = null)
        {
            if (_instance == null) new GameObject("_Airstrike").AddComponent<ProtoAirstrike>();
            var i = _instance;
            i.Build();
            if (i._t < 0f) i._t = 0f;
            bool silent = (i._strikeCount++ % StrikeVoiceEvery) != 0;
            i.AddWave(center, i._t + StrikeLead, damagePerPoint, silent, false, fx);
        }

        /// 필사기 발동. center = 플레이어 위치(경로가 그 근처를 지난다).
        /// 2026-10-28 필사기에서 뺐다 — 지금은 디버그 F3 만 부른다.
        public static void Play(Vector3 center)
        {
            if (_instance == null)
            {
                var go = new GameObject("_Airstrike");
                _instance = go.AddComponent<ProtoAirstrike>();
            }
            _instance.Begin(center);
        }

        /// 런 시작 때 미리 준비(헬기 소리 14초 Vorbis 를 첫 발동 전에 불러 둔다 — 안 하면 첫 회차 소리가 늦는다)
        public static void Preload()
        {
            if (_instance == null)
            {
                var go = new GameObject("_Airstrike");
                _instance = go.AddComponent<ProtoAirstrike>();
            }
            _instance.Build();
        }

        public static void Stop()
        {
            if (_instance == null) return;
            _instance.EndAll();
        }

        void OnDestroy() { if (_instance == this) _instance = null; }

        // ── 준비 ─────────────────────────────────────────────────────────
        void Build()
        {
            if (_built) return;
            _built = true;
            var shader = Shader.Find("Proto/GroundShadow");
            if (shader == null) Debug.LogWarning("[Airstrike] Proto/GroundShadow 셰이더 없음 — 헬기 그림자가 안 보인다");
            _heli  = MakeQuad("HeliShadow", Resources.Load<Texture2D>("VFX/T_HeliShadow"), shader, new Vector2(HeliLength * 0.5f, HeliLength), 0.55f);
            _rotor = MakeQuad("HeliRotor",  Resources.Load<Texture2D>("VFX/T_HeliRotor"),  shader, new Vector2(RotorSize, RotorSize), 0.42f);
            _rotor.SetParent(_heli, false);
            // 로터 축 = 동체 텍스처 위에서 38% 지점(스텁 날개 바로 위)
            _rotor.localPosition = new Vector3(0f, 0.002f, HeliLength * (0.5f - 0.38f));
            _heli.gameObject.SetActive(false);
            var hc = new List<AudioClip>();
            for (int i = 1; i <= 4; i++)
            {
                var c = Resources.Load<AudioClip>("Ambience/heli_" + i);
                if (c == null) break;
                c.LoadAudioData();   // 미리 불러 두지 않으면 첫 재생에서 탐색(time 지정)이 늦게 먹는다
                hc.Add(c);
            }
            _heliClips = hc.ToArray();
            _bombPrefab = Resources.Load<GameObject>("VFX/AirstrikeBomb");
            if (_bombPrefab == null) Debug.LogWarning("[Airstrike] Resources/VFX/AirstrikeBomb 없음 — 미사일 없이 폭발만 난다");
        }

        Transform MakeQuad(string name, Texture2D tex, Shader shader, Vector2 size, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            var m = new Mesh { name = name };
            // 바닥에 눕힌 쿼드. 텍스처 위쪽(v=1) = 기수 = 로컬 +Z
            m.vertices  = new[] { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz) };
            m.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateBounds();
            mf.sharedMesh = m;
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", new Color(0.02f, 0.03f, 0.06f, alpha));
                mr.sharedMaterial = mat;
            }
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.transform;
        }

        // ── 진행 ─────────────────────────────────────────────────────────
        void Begin(Vector3 center)
        {
            Build();
            EndAll();
            _t = 0f;
            for (int w = 0; w < Waves; w++) AddWave(center, FirstWave + w * Interval, Damage, false, true);
        }

        /// fx = 부른 무기의 연출 세트(폭발 · 처치). 필사기·디버그는 비워서 기본 세트
        void AddWave(Vector3 center, float t0, float dmg, bool silent, bool ult, WeaponFxSet fx = null)
        {
            center.y = 0f;
            // 회차마다 다른 대각선. 플레이어에서 0~3m 비켜 지나간다
            float ang = Random.Range(0f, 360f);
            var dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            var side = Vector3.Cross(Vector3.up, dir);
            var through = center + side * Random.Range(-3f, 3f);
            var wave = new Wave { t0 = t0, dir = dir, a = through - dir * HeliHalfLen, silent = silent, ult = ult };
            _waves.Add(wave);
            PlanBombs(wave, through, side, dmg, fx);
        }

        void PlanBombs(Wave wv, Vector3 through, Vector3 side, float dmg, WeaponFxSet fx)
        {
            var dir = ProtoRun.I != null ? ProtoRun.I.director : null;
            var used = new List<Vector3>();
            for (int i = 0; i < Points; i++)
            {
                // 화면 안(경로 기준 ±8m)에 고르게, 옆으로 ±3.5m 흔든다
                float s = Mathf.Lerp(-8f, 8f, (i + Random.Range(0.15f, 0.85f)) / Points);
                Vector3 p = through + wv.dir * s + side * Random.Range(-3.5f, 3.5f);
                // 근처(3m 안) 좀비가 있으면 그 위로 — 랜덤 폭격이지만 허공에만 떨어지면 맛이 없다
                if (dir != null)
                {
                    float best = 9f; Vector3 bp = p;
                    var list = dir.Active;
                    for (int k = 0; k < list.Count; k++)
                    {
                        var z = list[k];
                        if (z == null || !z.Alive) continue;
                        Vector3 zp = z.transform.position; zp.y = 0f;
                        float dsq = (zp - p).sqrMagnitude;
                        if (dsq < best && !TooClose(used, zp)) { best = dsq; bp = zp; }
                    }
                    p = bp;
                }
                p.y = 0f;
                used.Add(p);
                // 헬기 그림자가 이 지점의 경로상 위치를 지날 때 떨어뜨린다
                float along = Vector3.Dot(p - wv.a, wv.dir);
                float drop = wv.t0 + along / HeliSpeed;
                _bombs.Add(new Bomb
                {
                    drop = drop,
                    dmg = dmg,
                    fx = fx,
                    target = p,
                    from = p - wv.dir * 3.5f + Vector3.up * DropHeight,   // 헬기 진행 방향으로 비스듬히 떨어진다
                });
            }
        }

        static bool TooClose(List<Vector3> used, Vector3 p)
        {
            for (int i = 0; i < used.Count; i++) if ((used[i] - p).sqrMagnitude < 2.0f * 2.0f) return true;
            return false;
        }

        void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;

            // 헬기 그림자: 지금 돌고 있는 회차 하나만 보인다
            Wave cur = null;
            for (int i = 0; i < _waves.Count; i++)
            {
                var wv = _waves[i];
                float lt = _t - wv.t0;
                if (lt >= 0f && lt * HeliSpeed <= HeliHalfLen * 2f) cur = wv;
            }
            TickHeliAudio();
            if (cur != null)
            {
                if (!_heli.gameObject.activeSelf) _heli.gameObject.SetActive(true);
                float lt = _t - cur.t0;
                _heli.position = cur.a + cur.dir * (lt * HeliSpeed) + Vector3.up * ShadowY;
                _heli.rotation = Quaternion.LookRotation(cur.dir, Vector3.up);
                _rotor.localRotation = Quaternion.Euler(0f, _t * 900f, 0f);
            }
            else if (_heli.gameObject.activeSelf) _heli.gameObject.SetActive(false);

            // 미사일
            bool allDone = true;
            for (int i = 0; i < _bombs.Count; i++)
            {
                var b = _bombs[i];
                if (b.done) continue;
                allDone = false;
                float lt = _t - b.drop;
                if (lt < 0f) continue;
                if (b.tf == null && _bombPrefab != null)
                {
                    b.tf = _bombPool.Count > 0 ? _bombPool.Pop() : Instantiate(_bombPrefab, transform).transform;
                    b.tf.gameObject.SetActive(true);
                    b.tf.localScale = Vector3.one * BombScale;
                }
                float k = Mathf.Clamp01(lt / FallTime);
                if (b.tf != null)
                {
                    // 가속 낙하(k²) — 코(+Z)를 진행 방향으로
                    Vector3 pos = Vector3.Lerp(b.from, b.target, k * k);
                    b.tf.position = pos;
                    b.tf.rotation = Quaternion.LookRotation((b.target - b.from).normalized, Vector3.up);
                }
                if (k >= 1f)
                {
                    b.done = true;
                    if (b.tf != null) { b.tf.gameObject.SetActive(false); _bombPool.Push(b.tf); b.tf = null; }
                    Impact(b.target, b.dmg, b.fx);
                }
            }

            // 끝난 미사일·회차는 버린다 — 무전기 무기는 회차가 계속 겹쳐 들어와 EndAll 까지 가지 않으므로 목록이 쌓인다
            _bombs.RemoveAll(b => b.done);
            bool noVoice = _heliClips == null || _heliClips.Length == 0;
            // 거꾸로 돌며 지운다 — 예전 RemoveAll(람다)은 noVoice 를 잡는 클로저를 매 프레임 만들었다(공습이 없어도, 2026-11-20)
            for (int wi = _waves.Count - 1; wi >= 0; wi--)
            {
                var w = _waves[wi];
                if ((_t - w.t0) * HeliSpeed > HeliHalfLen * 2f && w.src == null && (w.silent || w.fade >= 1f || noVoice)) _waves.RemoveAt(wi);
            }
            if (allDone && _waves.Count == 0) EndAll();
        }

        void TickHeliAudio()
        {
            if (_cam == null) _cam = Camera.main;
            float vol = ProtoSettings.SfxVolume * HeliVolume;
            // 레벨업 카드·옵션으로 게임이 멈추면 그림자가 멈추므로 소리도 같이 멈춘다(싱크 유지)
            bool paused = Time.timeScale <= 0.0001f;
            for (int i = 0; i < _waves.Count; i++)
            {
                var s = _waves[i].src;
                if (s == null) continue;
                if (paused && s.isPlaying) s.Pause();
                else if (!paused && !s.isPlaying) s.UnPause();
            }
            if (paused) return;
            for (int i = 0; i < _waves.Count; i++)
            {
                var wv = _waves[i];
                float lt = _t - wv.t0;
                if (lt < 0f) continue;
                // 시작: 클립 정점(머리 위)이 그림자가 경로 중앙에 오는 순간과 겹치게 앞을 잘라 낸다
                if (wv.src == null && wv.fade < 0f && !wv.silent && _heliClips != null && _heliClips.Length > 0)
                {
                    var clip = _heliClips[_heliNext++ % _heliClips.Length];
                    var go = new GameObject("HeliVoice");
                    go.transform.SetParent(transform, false);
                    wv.src = go.AddComponent<AudioSource>();
                    wv.src.playOnAwake = false;
                    wv.src.spatialBlend = 0f;
                    wv.src.clip = clip;
                    wv.src.volume = vol;
                    float overhead = HeliHalfLen / HeliSpeed;
                    wv.src.time = Mathf.Clamp(clip.length * HeliPeakAt - overhead + lt, 0f, clip.length - 0.05f);
                    wv.src.Play();
                }
                if (wv.src == null) continue;
                // 좌우: 그림자 위치를 카메라 오른쪽 축에 투영(화면 반폭 ~5.5m)
                Vector3 hp = wv.a + wv.dir * (lt * HeliSpeed);
                if (_cam != null)
                    wv.src.panStereo = Mathf.Clamp(Vector3.Dot(hp - _cam.transform.position, _cam.transform.right) / 7f, -0.8f, 0.8f);
                // 그림자가 화면 밖으로 나간 뒤 0.8초에 걸쳐 줄인다
                float exitT = HeliHalfLen * 2f / HeliSpeed;
                if (lt > exitT)
                {
                    if (wv.fade < 0f) wv.fade = 0f;
                    wv.fade += Time.deltaTime / 0.8f;
                    wv.src.volume = vol * Mathf.Clamp01(1f - wv.fade);
                    if (wv.fade >= 1f) { Destroy(wv.src.gameObject); wv.src = null; }
                }
                else wv.src.volume = vol;
            }
        }

        void Impact(Vector3 p, float baseDamage, WeaponFxSet fx)
        {
            // 폭발은 부른 무기의 연출 세트(explode 자리)대로. 기본은 코드 그림 explosion — 링 테두리 = 판정 반경
            // (예전의 ignoreDuck 은 컷인 덕킹이 2026-10-29 에 빠져 의미가 없어졌다)
            var set = fx != null ? fx : WeaponFxSet.Default;
            if (set.explode != null && set.explode.On) ProtoFx.PlayBlast(set.explode.fxId, p, Radius, big: true, scale: set.explode.scale);
            var run = ProtoRun.I;
            var dir = run != null ? run.director : null;
            if (dir == null) return;
            float dmg = baseDamage * (run.player != null ? run.player.damageMult : 1f);
            float rSq = Radius * Radius;
            var list = dir.Active;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 d = z.transform.position - p;
                d.y = 0f;
                if (d.sqrMagnitude > rSq) continue;
                z.ApplyBlastSoot();   // 피해보다 먼저(이 폭발로 죽어도 그을린 채 쓰러지게 — ProtoProjectile 과 같은 규칙)
                Vector3 push = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
                z.TakeDamage(dmg, push, false, true, HitFx.Blast(set));   // 큰 한 방 — 데미지 숫자. 자리 불똥 없음
            }
        }

        void EndAll()
        {
            _t = -1f;
            for (int i = 0; i < _waves.Count; i++)
                if (_waves[i].src != null) Destroy(_waves[i].src.gameObject);
            _waves.Clear();
            for (int i = 0; i < _bombs.Count; i++)
                if (_bombs[i].tf != null) { _bombs[i].tf.gameObject.SetActive(false); _bombPool.Push(_bombs[i].tf); }
            _bombs.Clear();
            if (_heli != null) _heli.gameObject.SetActive(false);
        }
    }
}
