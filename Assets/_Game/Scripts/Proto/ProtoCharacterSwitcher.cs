using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 플레이 중 캐릭터를 갈아끼우는 <b>테스트용</b> 패널. 숫자키 1~6 또는 화면 버튼.
    ///
    /// <para><b>임시 도구다.</b> 실제 스쿼드는 편성 화면에서 3명을 고르고 쓰러질 때 순차 교체된다(§04).
    /// 여기서는 "6종이 제대로 보이는지, 무기가 제대로 들리는지"만 확인한다 — 편성·승계·사망 처리는 없다.</para>
    ///
    /// <para><b>왜 프리팹을 바꾸는 게 아니라 자식을 교체하는가.</b> 게임플레이는 <c>Player</c> 루트에
    /// 붙어 있고(이동·사격·체력·발밑 링·물 자국) 캐릭터는 그 아래 <c>Visual</c> 자식 하나다.
    /// 자식만 갈아끼우면 진행 중인 런이 끊기지 않는다 — 위치·체력·레벨이 그대로 유지된다.</para>
    /// </summary>
    public class ProtoCharacterSwitcher : MonoBehaviour
    {
        const string VisualName = "Visual";

        [Tooltip("명단. 비워 두면 Resources/Data/CharacterTable 의 명단을 쓴다(보통 비워 둔다). 테스트로 일부만 띄우고 싶을 때만 채운다.")]
        public CharacterDef[] roster;

        [Tooltip("패널 접기/펴기")]
        public KeyCode toggleKey = KeyCode.C;

        public bool panelOpen = true;

        [Header("교체 연출")]
        [Tooltip("사라지는 데 걸리는 시간(초). 번개가 지면에 닿는 시각(약 0.10초)보다 짧게 두면 번쩍이기 전에 이미 없다.")]
        public float vanishTime = 0.09f;

        [Tooltip("새 캐릭터가 떨어지는 높이(m). 0 이면 그냥 제자리에서 커진다.")]
        public float dropHeight = 1.15f;

        [Tooltip("떨어지는 데 걸리는 시간(초). 짧을수록 쿵 하고 꽂힌다.")]
        public float fallTime = 0.11f;

        [Tooltip("착지 후 찌그러졌다 돌아오는 시간(초).")]
        public float landRecover = 0.17f;

        [Tooltip("착지 순간 눌리는 정도. 0.7 이면 세로 70%, 가로는 그만큼 퍼진다.")]
        [Range(0.4f, 1f)] public float landSquash = 0.72f;

        [Tooltip("착지 먼지 프리팹 경로 (Resources 아래). 없으면 먼지 없이 진행한다.")]
        public string landingFxPath = "VFX/FX_SwapLanding";

        [Header("교체 피해")]
        [Tooltip("교체 순간 주변 좀비에게 주는 피해. 거리 감쇠 없이 반경 안이면 전부 같은 값.")]
        public float swapDamage = 40f;

        [Tooltip("판정 반경(m). 지면 링 테두리가 곧 이 반경이 되도록 연출 크기를 여기에 맞춘다.")]
        public float swapRadius = 1.86f;

        public bool swapDamageEnabled = true;

        int _current = -1;
        public int Current => _current;

        /// 지금 캐릭터의 테이블 줄. 필사기(ProtoUltimate.DefOf)가 여기서 필사기 에셋을 읽는다.
        /// 아직 한 번도 안 눌렀으면(씬 원본) null — 부르는 쪽이 명단 1번으로 친다.
        public CharacterDef CurrentDef => _current >= 0 && _current < Count ? Roster[_current] : null;

        Coroutine _swapping;

        // 씬 빌더가 실측 높이(1.70m)로 잡아둔 스케일. **연출 중에 줄어든 값을 물려받으면 안 되므로**
        // 따로 캐시한다 — 예전에는 교체할 때마다 옛 Visual 의 localScale 을 읽었는데,
        // 사라지는 연출이 붙은 지금은 그 값이 0 에 가까울 수 있다.
        Vector3 _baseScale = Vector3.one;
        bool _baseScaleKnown;

        // 씬에 원래 놓여 있던 Visual 이 몇 번 캐릭터인지는 **추측하지 않는다.**
        // 무기로 맞추면 몸 머티리얼이 다른데도 같다고 나오고(베이스 프리팹이 그렇다),
        // 머티리얼로 맞추면 무기가 다른데도 같다고 나온다. 어느 쪽이든 패널이 자신 있게
        // 틀린 이름을 띄우게 되므로, 사용자가 한 번 누를 때까지 '?' 로 둔다.

        static readonly CharacterDef[] Empty = new CharacterDef[0];
        CharacterDef[] _roster;

        /// 쓸 명단. 인스펙터에 채운 것이 있으면 그것, 아니면 캐릭터 테이블.
        CharacterDef[] Roster
        {
            get
            {
                if (_roster != null) return _roster;
                if (roster != null && roster.Length > 0) { _roster = roster; return _roster; }
                var t = CharacterTable.I;
                _roster = t != null && t.roster != null ? t.roster : Empty;
                return _roster;
            }
        }

        int Count => Roster.Length;

        GameObject PrefabAt(int i) => i >= 0 && i < Count && Roster[i] != null ? Roster[i].prefab : null;

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) panelOpen = !panelOpen;

            for (int i = 0; i < Count && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) && !ProtoTank.PlayerRiding)   // 탱크 탑승 중엔 교체 금지 (숨긴 모델이 되살아난다)
                    Select(i);
        }

        void Awake()
        {
            CacheBaseScale();
            useGUILayout = false;   // GUILayout 을 안 쓴다 — IMGUI 레이아웃 패스(프레임마다 한 번 더)를 건너뛴다(2026-11-20)
        }

        /// 씬에 원래 놓여 있던 Visual 의 스케일을 한 번만 붙잡아 둔다.
        void CacheBaseScale()
        {
            if (_baseScaleKnown) return;
            var v = transform.Find(VisualName);
            if (v == null) return;
            _baseScale = v.localScale;
            _baseScaleKnown = true;
        }

        /// <summary>
        /// index 번 캐릭터로 교체한다. 런은 그대로 이어진다.
        ///
        /// <para><b>즉시 바뀌지 않는다.</b> 번개가 지면에 닿는 순간(약 0.10초)에 맞춰
        /// 옛 몸이 줄어들어 사라지고 → 교체 + 주변 피해 → 새 몸이 커지며 나타난다.
        /// 그 사이에도 이동·사격은 계속된다(게임플레이는 Player 루트에 있고 Visual 은 자식일 뿐이다).</para>
        ///
        /// <para>패널 표시용 <see cref="Current"/> 만은 즉시 갱신한다 — 누르자마자 반응이 없으면
        /// 입력이 씹힌 것처럼 느껴진다.</para>
        /// </summary>
        public void Select(int index)
        {
            if (PrefabAt(index) == null) return;
            // 시작 연출(낙하산) 중에는 바꾸지 않는다 — ProtoIntro 가 붙잡은 Visual 이 파괴되어 연출이 멈추고 런이 시작되지 않는다(10-29 확인)
            if (ProtoRun.I != null && ProtoRun.I.state == RunState.Intro) return;

            CacheBaseScale();
            _current = index;

            // 연타 대비: 돌던 시퀀스를 끊는다. 줄어들다 만 Visual 은 새 시퀀스가 그 자리에서
            // 이어받아 0 까지 줄이므로 따로 되돌리지 않는다.
            if (_swapping != null) { StopCoroutine(_swapping); _swapping = null; }

            // 비활성이면 코루틴이 안 돈다 — 그때는 연출 없이 즉시 교체한다.
            if (!isActiveAndEnabled) { SwapVisualNow(index); return; }
            _swapping = StartCoroutine(SwapRoutine(index));
        }

        System.Collections.IEnumerator SwapRoutine(int index)
        {
            Vector3 groundPos = transform.position;

            // 링 테두리가 곧 판정 반경이 되도록 **연출을 판정에 맞춘다.** 착탄 폭발이 같은 원칙을
            // 쓴다(Proto_Explosion.shader 주석) — 데미지 숫자를 안 쓰는 게임(§15)에서
            // "무엇이 맞았나"를 읽을 수 있는 유일한 단서가 테두리다.
            // 카탈로그 char_swap_appear 에 프리팹이 꽂혀 있으면 번개 대신 그걸 낸다(2026-11-13 노란 텔레포트 등장).
            // 플레이어를 따라다닌다 — 새 몸은 Player 의 자식이라 교체 중에도 같이 움직이므로 연출도 발밑을 지켜야 한다.
            // 교체 시각(ImpactDelay)은 번개를 안 내도 기본값(0.10초)을 돌려주므로 아래 순서는 그대로다.
            if (ProtoFx.HasPrefab(FxIds.CharSwapAppear))
                ProtoFx.Play(FxIds.CharSwapAppear, FxContext.On(transform, follow: true));
            else
            {
                float ring = ProtoLightningStrike.GroundRingRadius;
                float scaleMul = (swapRadius > 0.01f && ring > 0.01f) ? swapRadius / ring : 1f;
                ProtoLightningStrike.Play(groundPos, scaleMul);
            }

            float impact = Mathf.Max(ProtoLightningStrike.ImpactDelay, 0f);

            // (1) 사라진다 — 번개가 내려오는 동안 줄어든다.
            // **반드시 unscaled.** 레벨업 카드가 떠 있는 동안 교체가 일어나면 timeScale 이 0 이라
            // scaled 로 재면 연출이 통째로 얼어붙는다 (번개도 같은 이유로 unscaled 다).
            var old = transform.Find(VisualName);
            float t = 0f;
            float vt = Mathf.Max(vanishTime, 0.0001f);
            while (t < impact)
            {
                t += ProtoConfig.UnscaledDelta;
                if (old != null)
                {
                    float k = 1f - Mathf.Clamp01(t / vt);
                    old.localScale = _baseScale * (k * k);   // 제곱 — 끝에서 빨리 꺼진다
                }
                yield return null;
            }
            if (old != null) old.localScale = Vector3.zero;

            // (2) 착탄 — 교체와 피해가 같은 프레임에 일어난다.
            SwapVisualNow(index);
            if (swapDamageEnabled) SwapBlast(groundPos, swapRadius, swapDamage);

            // (3) 떨어져 내려와 착지한다.
            //
            // 그냥 커지기만 하면 "뚝 나타났다"로 보인다. 위에서 떨어뜨렸다가 바닥에서 한 번
            // 눌러주면 같은 0.3초인데도 '도착했다'로 읽힌다 — 스쿼시&스트레치의 기본이다.
            var fresh = transform.Find(VisualName);
            float ft = Mathf.Max(fallTime, 0.0001f);
            float rt = Mathf.Max(landRecover, 0.0001f);

            // ③-1 낙하: 가속하며 떨어진다(t^2). 등속으로 떨어뜨리면 '내려놓는' 느낌이 된다.
            t = 0f;
            while (t < ft)
            {
                t += ProtoConfig.UnscaledDelta;
                float u = Mathf.Clamp01(t / ft);
                if (fresh != null)
                {
                    fresh.localPosition = new Vector3(0f, dropHeight * (1f - u * u), 0f);
                    // 떨어지는 동안 세로로 늘어난다(스트레치). 크기도 이때 제자리를 찾는다.
                    float grow = Mathf.Clamp01(u / 0.5f);
                    float stretch = Mathf.Lerp(1.18f, 1.0f, u);
                    fresh.localScale = Vector3.Scale(_baseScale,
                        new Vector3(grow / stretch, grow * stretch, grow / stretch));
                }
                yield return null;
            }
            if (fresh != null) fresh.localPosition = Vector3.zero;

            // ③-2 착지 순간 — 먼지가 여기서 터진다. 번개 착탄(0.10s)과 겹치지 않게 떨어진 뒤다.
            PlayLandingFx(groundPos);

            // ③-3 눌렸다 돌아온다.
            t = 0f;
            while (t < rt)
            {
                t += ProtoConfig.UnscaledDelta;
                float u = Mathf.Clamp01(t / rt);
                if (fresh != null)
                {
                    // 눌린 상태에서 시작해 1 로 돌아오되 살짝 넘긴다.
                    float k = BackOut(u);
                    float sq = Mathf.Lerp(landSquash, 1f, k);
                    float wide = Mathf.Lerp(2f - landSquash, 1f, k);   // 세로로 눌린 만큼 가로로 퍼진다
                    fresh.localScale = Vector3.Scale(_baseScale, new Vector3(wide, sq, wide));
                }
                yield return null;
            }
            if (fresh != null) { fresh.localScale = _baseScale; fresh.localPosition = Vector3.zero; }
            _swapping = null;
        }

        /// 착지 먼지. 프리팹이 없으면 조용히 넘어간다 — 연출 하나 때문에 교체가 막히면 안 된다.
        /// 재생이 끝나면 스스로 사라진다(파티클 수명이 1초 미만이라 2초면 충분하다).
        public void PlayLandingFx(Vector3 groundPos)
        {
            if (string.IsNullOrEmpty(landingFxPath)) return;
            var prefab = Resources.Load<GameObject>(landingFxPath);
            if (prefab == null) return;

            ProtoAudio.Play("land", groundPos);
            var fx = Instantiate(prefab, groundPos, Quaternion.identity);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                // 번개·교체 연출이 전부 unscaled 라 먼지만 timeScale 을 따르면 혼자 얼어붙는다.
                m.useUnscaledTime = true;
                ps.Play();
            }
            Destroy(fx, 2f);
        }

        /// 1 을 살짝 넘겼다 돌아오는 이징. 그냥 커지기만 하면 "튀어나온다"가 아니라 "늘어난다"로 보인다.
        static float BackOut(float u)
        {
            const float s = 1.55f;
            float p = u - 1f;
            return 1f + (s + 1f) * p * p * p + s * p * p;
        }

        /// <summary>
        /// 교체 순간 주변 좀비에게 같은 피해를 준다. <b>거리 감쇠는 없다</b> — 반경 안이면 전부 같은 값이다.
        /// 데미지 숫자를 쓰지 않는 게임(§15)이라 감쇠를 넣으면 "왜 얘만 안 죽었지"가 화면에서 읽히지 않는다.
        /// </summary>
        /// <returns>맞은 좀비 수</returns>
        public static int SwapBlast(Vector3 center, float radius, float damage)
        {
            var dir = ProtoSpawnDirector.I;
            if (dir == null || damage <= 0f || radius <= 0.01f) return 0;

            int hit = 0;
            var list = dir.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;

                Vector3 d = z.transform.position - center;
                d.y = 0f;
                // 덩치가 기준(잡좀비)보다 큰 적은 그 초과분만큼 판정을 넓힌다 — 보스는 몸 반경이
                // 0.55라 중심만 재면 어깨가 링 안에 들어와 있는데도 안 맞는다.
                float r = radius + Mathf.Max(0f, z.bodyRadius - ProtoConfig.BaseBodyRadius);
                if (d.sqrMagnitude > r * r) continue;

                z.TakeDamage(damage, d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward, false, true, HitFx.Blast(null));   // 큰 한 방 — 데미지 숫자. 착지 먼지가 연출
                hit++;
            }
            return hit;
        }

        /// Visual 자식을 실제로 갈아끼운다. 연출 없이 한 프레임에 끝난다.
        void SwapVisualNow(int index)
        {
            var prefab = PrefabAt(index);
            if (prefab == null) return;
            CacheBaseScale();

            var old = transform.Find(VisualName);
            if (old != null)
            {
                // Destroy 는 프레임 끝에 실행된다. 그래서 이름을 비켜주는 것만으로는 부족하고
                // **부모에서 떼어내야** 한다 — 안 떼면 아래 RebindVisual 의
                // GetComponentInChildren<WeaponSocket>(true) 가 아직 자식으로 남아 있는
                // 옛 무기 소켓을 먼저 잡아서, 캐릭터는 바뀌었는데 무기 수치만 이전 것으로 남는다.
                // (실제로 이 버그를 밟았다. 비활성으로 만들어도 includeInactive=true 라 찾힌다.)
                old.name = "_VisualDiscarded";
                old.SetParent(null, true);
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            var go = Instantiate(prefab, transform);
            go.name = VisualName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // 연출 중이면 0 에서 시작한다 — 커지는 건 SwapRoutine 이 이어서 한다.
            // 연출 없이 불린 경우(비활성)는 바로 제 크기로 둔다.
            go.transform.localScale = _swapping != null ? Vector3.zero : _baseScale;

            // 자식을 갈아끼운 쪽이 캐시를 다시 잡아줘야 한다. 이 세 줄을 빼먹으면
            // 애니메이션이 멈추거나(Animator 죽은 참조) 캐릭터가 발밑 링에 덮여 사라진다.
            // Unity 오브젝트에 `?.` 를 쓰면 안 된다 — `?.` 는 참조 비교라서 유니티의 '가짜 null'
            // (파괴됐거나 없는 컴포넌트)을 통과시킨다. 반드시 `!= null` 로 검사한다.
            var pl = GetComponent<ProtoPlayer>();
            if (pl != null) pl.RebindVisual();
            if (pl != null) ProtoEvolution.Reapply(pl);   // 이 캐릭터의 기본 무기가 이번 판에 진화했으면 진화 무기로(아래 슈터가 무기를 다시 읽기 전에)
            var sh = GetComponent<ProtoShooter>();
            if (sh != null) sh.RebindVisual();
            var ring = GetComponentInChildren<ProtoGroundRing>(true);
            if (ring != null) ring.RaisePlayerQueue();

            Debug.Log($"[캐릭터 교체] {LabelOf(index)}  무기={WeaponNameOf(index)}");
        }

        string LabelOf(int i)
        {
            var c = i >= 0 && i < Count ? Roster[i] : null;
            if (c == null) return "?";
            return c.prefab != null ? c.Label : c.Label + " (프리팹 없음)";
        }

        string WeaponNameOf(int i)
        {
            var c = i >= 0 && i < Count ? Roster[i] : null;
            if (c == null) return "-";
            var def = c.weapon;
            if (def == null) return "손에 없음 (궤도/추종)";
            if (string.IsNullOrEmpty(def.displayName)) return def.name;
            // displayName 이 "산탄총 (브레이커)" 꼴이라 캐릭터 이름이 두 번 나온다. 괄호를 떼어낸다.
            int a = def.displayName.IndexOf('(');
            return a > 0 ? def.displayName.Substring(0, a).TrimEnd() : def.displayName;
        }

        /// 수치가 실제로 물려 있는지 화면에서 바로 보이게 한다 — 에셋만 만들어두고
        /// 배선을 빼먹으면 "교체했는데 감각이 같다"로만 나타나서 원인을 찾기 어렵다.
        string StatLineOf(int i)
        {
            var c = i >= 0 && i < Count ? Roster[i] : null;
            if (c == null) return "";
            var def = c.weapon;
            if (def == null || def.stats == null) return "수치 없음 → ProtoConfig 전역값";
            var s = def.stats;
            string extent = s.mode == FireMode.Orbit ? $"궤도 {s.orbitRadius:0.#}m" : $"{s.range:0.#}m";
            return $"{s.mode} · {s.Dps:0} DPS · {extent}";
        }

        // ── 패널 ────────────────────────────────────────────────────────
        GUIStyle _title, _row, _cur;
        // 패널 글자는 바뀔 때만 만든다 — 예전엔 OnGUI 마다(프레임당 두 번 이상) 줄마다 새 문자열 · 보조 장비 요약(2026-11-20)
        string[] _rowLabels;
        string _toggleLabel, _ultLine, _curLine;
        int _infoFor = int.MinValue;
        float _infoAt;

        // 화면 밖 화살표(ProtoTankIndicator)가 이 창을 피하도록 — 마지막으로 그린 창 자리(화면 픽셀, 왼쪽 아래 원점)
        static UnityEngine.Rect _screenRect;
        static int _screenRectFrame = -100;
        public static bool TryGetScreenRect(out UnityEngine.Rect r)
        {
            r = _screenRect;
            return Time.frameCount - _screenRectFrame <= 2;
        }

        void OnGUI()
        {
            // 레벨업 카드·결과 화면은 전체를 덮는 오버레이라 그 위에 버튼을 겹치면
            // 카드가 눌리지 않는다. 그 동안에는 패널을 접는다.
            if (ProtoRun.I != null && !ProtoRun.I.IsRunning) return;
            if (ProtoHUD.CaptureMode) return;   // UI 화면 캡처(F10) 중에는 숨긴다
            if (Count == 0) return;

            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                _row   = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
                _cur   = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
                _title.normal.textColor = new Color(1f, 0.86f, 0.45f);
            }

            const float W = 208f, RowH = 24f;
            float h = 30f + Count * (RowH + 3f) + 72f + 22f;

            // 11-18 사용자 "HUD 랑 안 겹치게 가독성 있게": 처치 수 바로 밑(위 HUD 띠 안)에 붙어 있던 창을
            // 위 HUD 띠 아래 · 오른쪽 아래 묶음(필사기 + 하차) 위 사이로 내리고, 오른쪽 끝을 HUD 여백(24)에 맞춘다.
            // 캔버스 배율(1080×1920 반반)을 따라 커져 폰에서도 읽히고, 두 칸 사이에 안 들어가면 그만큼 줄인다.
            var safe = Screen.safeArea;
            float s = Mathf.Sqrt(Screen.width / 1080f * (Screen.height / 1920f));
            float top = Screen.height - safe.yMax + (ProtoHUD.HudTopBand + 12f) * s;               // GUI 좌표(위 원점)
            float limit = Screen.height - safe.yMin - (ProtoHUD.RightStackTop + 16f) * s;
            float k = Mathf.Max(1f, s * 1.4f);
            float fit = (limit - top) / h;
            if (panelOpen && fit < k) k = Mathf.Max(0.7f, fit);
            float right = safe.xMax - ProtoHUD.LayoutPad * s;
            float x = right - W * k;

            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(x, top, 0f), Quaternion.identity, new Vector3(k, k, 1f)) * oldMatrix;
            try { DrawPanel(W, RowH, h); }
            finally { GUI.matrix = oldMatrix; }

            float drawnH = panelOpen ? h : 22f, drawnX = panelOpen ? x : right - 92f * k;
            _screenRect = new UnityEngine.Rect(drawnX, Screen.height - top - drawnH * k, right - drawnX, drawnH * k);
            _screenRectFrame = Time.frameCount;
        }

        // 창 내용 — 원점(0,0) 기준 · 배율은 호출 쪽 GUI.matrix
        void DrawPanel(float W, float RowH, float h)
        {
            const float x = 0f, y = 0f;
            if (!panelOpen)
            {
                if (_toggleLabel == null) _toggleLabel = $"캐릭터 ({toggleKey})";
                if (GUI.Button(new UnityEngine.Rect(x + W - 92f, y, 92f, 22f), _toggleLabel)) panelOpen = true;
                return;
            }

            // 바탕 — 기본 상자보다 진하게(게임 화면 위에서 글자가 읽히게)
            var box = new UnityEngine.Rect(x, y, W, h);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.03f, 0.04f, 0.07f, 0.8f), 0f, 6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(1f, 1f, 1f, 0.16f), 1f, 6f);

            GUI.Label(new Rect(x + 8f, y + 5f, W - 40f, 20f), "캐릭터 교체 (임시)", _title);
            if (GUI.Button(new Rect(x + W - 26f, y + 4f, 20f, 18f), "×")) panelOpen = false;

            float ry = y + 27f;
            if (_rowLabels == null || _rowLabels.Length != Count)
            {
                _rowLabels = new string[Count];
                for (int i = 0; i < Count; i++) _rowLabels[i] = $" {i + 1}. {LabelOf(i)}";
            }
            for (int i = 0; i < Count; i++)
            {
                bool sel = i == _current;
                var old = GUI.color;
                if (sel) GUI.color = new Color(0.55f, 1f, 0.65f);
                if (GUI.Button(new Rect(x + 8f, ry, W - 16f, RowH), _rowLabels[i], _row)) Select(i);
                GUI.color = old;
                ry += RowH + 3f;
            }

            // 아래 두 줄은 캐릭터를 바꿨을 때 · 0.25초마다만 다시 만든다(보조 장비 · 필사기 트리는 카드로 바뀐다 — 2026-11-20)
            if (_current != _infoFor || Time.unscaledTime >= _infoAt || _ultLine == null)
            {
                _infoFor = _current; _infoAt = Time.unscaledTime + 0.25f;
                // 보조 장비는 카드로 얻으므로 캐릭터와 무관하다 — 그래서 현재 상태를 따로 보여준다.
                var gear = GetComponent<ProtoSupportGear>();
                string gearLine = gear != null ? "\n보조: " + gear.Summary() : "";

                // 필사기는 캐릭터 고유다(§06 v0.7) — 지금은 루키·파이로만 있다. 트리는 T키로 돌린다.
                var run = ProtoRun.I;
                var kind = run != null ? run.UltKind : ProtoUltimate.Kind.None;
                var tree = run != null ? run.TreeOf(kind) : null;
                _ultLine = tree != null ? $"필사기: {ProtoUltimate.NameOf(run.player)} [{tree.Short()}] (T)" : "필사기: 준비 중";
                _curLine = _current < 0 ? "현재: ? (씬 원본 — 하나 누르면 확정)" + gearLine
                                        : $"현재: {LabelOf(_current)}\n무기: {WeaponNameOf(_current)}\n{StatLineOf(_current)}{gearLine}";
            }
            GUI.Label(new Rect(x + 8f, ry + 2f, W - 16f, 20f), _ultLine, _cur);
            ry += 22f;
            GUI.Label(new Rect(x + 8f, ry + 2f, W - 16f, 68f), _curLine, _cur);
        }
    }
}
