using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 데미지 숫자 — <b>밀도에 맞춰 자동으로</b>(2026-10-06 사용자 결정).
    /// <b>중요한 피해</b>(치명타 · 보스 · 큰 한 방 = 로켓 폭발·융단 폭격·교체 피해·필사기)는 항상 뜬다.
    /// <b>평범한 피해</b>(잡좀비가 맞는 탄·화염·블레이드)는 화면에 떠 있는 숫자가 일정 개수 미만일 때만 뜬다.
    /// 지금 프로토(화면에 좀비 20~30)는 거의 항상 다 뜨고, 기획서 목표(300마리)로 밀도가 오르면 저절로 중요한 것만 남는다.
    ///
    /// <para><b>모양은 두 가지</b>(<see cref="Style"/> — 옵션 "데미지 숫자"가 YAZS 식 → 기존 → 꺼짐으로 돈다. 기본 YAZS 식).</para>
    ///
    /// <para><b>YAZS 식</b>(2026-11-10 사용자 요청 "피해 숫자도 YAZS 처럼") — Yet Another Zombie Survivors 가
    /// Damage Numbers Pro(유료 에셋)에 넣어 둔 설정값을 그대로 옮겼다. <b>그 플러그인을 쓰는 게 아니다</b> — 프로젝트에 없고
    /// (에셋 스토어에서 사야 한다), 숫자마다 TextMeshPro 오브젝트를 하나씩 만드는 방식이라 물량전·모바일에는 이쪽(메시 하나)이 가볍다.
    /// <list type="bullet">
    /// <item>일반: 얇고 좁은 흰 숫자, 외곽선 없음. 0.2초 동안 나타나며 <b>무작위 자리로 미끄러져 가서 멈춘다</b>
    /// (가로 ±0.4m · 위로 0.5~1m, 남은 거리의 5×dt 씩) → 1초까지 머문 뒤 0.2초에 사라진다.</item>
    /// <item>치명타: 굵은 글자 + 얇은 검은 외곽선, <b>일반의 2배</b>. 2초 머문다. 나타날 때 2배 크기에서 줄어들며
    /// 큰 잔상과 본체가 위아래에서 겹쳐 들어온다(DNP 의 Scale / Cross Scale / Shake Fade In). 사라질 땐 0.4배로 줄어든다.</item>
    /// <item><b>합치지 않는다</b> — 맞을 때마다 한 개씩(YAZS 의 Combination 꺼짐). 겹침은 무작위 자리가 풀어 준다.</item>
    /// <item>떠 있는 숫자가 <see cref="YazsCrowdLimit"/> 개를 넘으면 평범한 피해는 건너뛴다
    /// (YAZS 는 "많을 땐 80 이상만" — 같은 뜻의 규칙).</item>
    /// </list>
    /// YAZS 에서 못 읽은 것: 치명타 색(프리팹엔 흰색, 코드가 피해 종류별 색을 넣는다 — 스크린샷에선 흰색·검붉은색이 보인다) → 우리 노랑을 그대로 쓴다.
    /// 글자 크기는 YAZS(글자 0.2m / 0.4m — 1080p 화면에서 숫자 높이 11px / 24px, 실측)의 2.38배(<c>YSizeScale</c>) — 28px / 56px.
    /// 1.7배면 캐릭터 대비 숫자 크기가 YAZS 와 같은데(우리 캐릭터가 1.5배 크다), 폰에서 작아 사용자가 거기서 1.4배 더 키웠다.</para>
    ///
    /// <para><b>기존 식</b>(2026-10-06) — 굵은 Impact 글자, 톡 튀어나와 위로 떠오른다.
    /// 보스는 연사에 맞으면 숫자가 줄줄이 쌓이므로 <b>같은 대상의 0.5초 안 피해를 하나로 합친다</b>(치명타 포함 — 섞이면 노랑. 숫자가 커지며 다시 튄다).</para>
    ///
    /// <para><b>숫자 전부가 메시 하나·드로우콜 하나다.</b> 매 프레임 쿼드를 카메라 쪽으로 세워 다시 채운다.
    /// 아틀라스 = <c>Resources/VFX/T_DamageDigitsYazs.png</c>(ArtSource/VFX/make_damage_digits_yazs.py — 윗줄 일반, 아랫줄 치명타)
    /// · <c>T_DamageDigits.png</c>(make_damage_digits.py — 기존 식), 셰이더 = <c>Proto/DamageText</c>(빌드 포함은 Resources/ShaderRefs/Ref_Proto_DamageText.mat).</para>
    /// </summary>
    public class ProtoDamageText : MonoBehaviour
    {
        /// 숫자 모양. 값은 <see cref="ProtoSettings.DamageStyle"/> 에 저장되는 번호와 같다.
        public enum Style { Yazs = 0, Classic = 1 }

        /// 동시에 떠 있을 수 있는 숫자(YAZS 식). 넘치면 가장 많이 산 것을 덮어쓴다. 기존 식은 앞의 <see cref="ClassicPopups"/> 개만 쓴다.
        public const int MaxPopups = 64;
        const int ClassicPopups = 24;
        const int MaxDigits = 6;               // 999,999 까지
        const int GhostQuads = 16 * MaxDigits; // 치명타가 나타나는 0.2초 동안은 한 숫자를 두 벌 그린다 — 그 여유분

        static readonly Color ColNormal = new Color(1f, 1f, 1f);
        static readonly Color ColCrit   = new Color(1f, 0.82f, 0.12f);

        // ── 플레이어가 받는 피해(2026-11-12 사용자: "-9" 처럼 마이너스, 기본 피해 빨강 · 독 초록) ──
        /// 기본 피해(좀비 접촉 · 보스 · 폭발 등) 숫자 색
        public static readonly Color ColPlayerHurt   = new Color(1f, 0.24f, 0.2f);
        /// 독 피해(원거리 좀비 혼불탄) 숫자 색 — 독가스 초록과 같은 계열
        public static readonly Color ColPlayerPoison = new Color(0.45f, 1f, 0.3f);
        /// 플레이어 숫자는 YAZS 식에서 굵은 글자(외곽선)로, 일반 글자 높이의 이 배수로 그린다 — 전장 위에서 색 글자가 읽히게
        const float YPlayerSize = 1.5f;

        // ── 기존 식 ─────────────────────────────────────────────────────────────
        const float MergeWindow = 0.5f;
        /// 평범한 피해는 떠 있는 숫자가 이 개수 미만일 때만. 넘으면 중요한 것만(A안과 같아진다).
        public const int CrowdLimit = 12;
        const float HeightMinor = 0.34f;
        const float LifeMinor   = 0.5f;   // 소총 간격 0.35초보다 길어야 보스 숫자가 합쳐진다(0.3 은 매 발 따로 떴다)

        // 글자 높이(m). 게임 카메라에 보이는 땅이 가로 약 10m 라 0.42m ≈ 화면 폭 4%.
        const float HeightNormal = 0.42f;
        const float HeightCrit   = 0.62f;
        const float Life     = 0.6f;
        const float LifeCrit = 0.75f;
        const float Rise     = 0.75f;          // 사는 동안 떠오르는 높이(m)
        const float Kern     = -0.06f;         // 글자 간격(높이 배율). 외곽선이 두꺼워 살짝 겹친다

        // ── YAZS 식 — 괄호 안은 Damage Numbers Pro 의 항목 이름과 YAZS 값 ─────────────
        /// 평범한 피해는 떠 있는 숫자가 이 개수 미만일 때만(YAZS 식).
        public const int YazsCrowdLimit = 32;
        // ── 글자 크기 ──
        // YAZS 값: 글자 em 0.2m(폰트 크기 4, size -2) · 치명타 0.4m(크기 6, size -2). 카메라가 YAZS 와 같으므로(52° · 65° · 12m)
        // 같은 값이면 화면에서도 같은 픽셀 크기다.
        // **YAZS 화면 실측**(2026-11-10, 스팀 커뮤니티의 1920×1080 플레이 스크린샷을 픽셀 단위로): 일반 "62" 숫자 높이 11px(화면 높이의 1.0%),
        // 굵은 "179" 24px(2.2%), 캐릭터 91px. 프리팹 값으로 계산하면 11.9px · 23.8px · 92px — 맞는다. 즉 YAZS 의 숫자는 원래 작다.
        // 우리 화면(같은 1080p): 배율 1.7 이면 일반 20px(1.8%) · 치명타 40px(3.7%). 우리 캐릭터가 2.7m 로 YAZS(1.8m)보다 커서(화면 148px)
        // **캐릭터 대비 비율은 배율 1.7 에서 YAZS 와 거의 같다**(일반 = 캐릭터 키의 13% / YAZS 12%, 치명타 27% / 27%).
        // 지금은 2.38(= 1.7 × 1.4) — 일반 28px(2.6%) · 치명타 56px(5.2%). 측정 결과를 보고 사용자가 "1.4배 키우기"를 골랐다(2026-11-10):
        // 폰(가로로 쥔 6.5인치)에서 1.7 은 일반 숫자가 1.2mm 밖에 안 된다. 2.38 이면 약 1.8mm.
        /// YAZS 크기의 몇 배로 그릴지. <b>1 = YAZS 와 똑같은 픽셀 크기</b>(일반 12px · 치명타 24px @1080p), 1.7 = 캐릭터 대비 YAZS 와 같은 비율.
        /// 숫자를 키우거나 줄일 때는 이 값 하나만 고친다 — 일반 : 치명타 = 1 : 2 는 그대로 따라간다.
        const float YSizeScale  = 2.38f;
        // 글자 높이(m, 아틀라스 칸 높이 — 숫자 자체는 이 값의 약 0.69. PT Sans 숫자가 em 의 0.70 이라 칸 높이 = em 으로 두면 맞는다)
        const float YHeight     = 0.2f * YSizeScale;
        const float YHeightCrit = 0.4f * YSizeScale;
        const float YKern       = 0f;          // 아틀라스 폭에 좌우 여백이 들어 있다
        const float YLife       = 1f;          // (lifetime 1) 이 뒤에 사라지는 시간이 붙는다 → 떠 있는 시간 1.2초
        const float YLifeCrit   = 2f;          // (치명타 lifetime 2) → 2.2초
        const float YFadeIn     = 0.2f;        // (durationFadeIn)
        const float YFadeOut    = 0.2f;        // (durationFadeOut)
        const float YDriftSpeed = 5f;          // (lerpSettings.speed) 남은 거리의 5×dt 씩 — 0.2초에 63%, 0.6초에 95%
        const float YDriftX     = 0.4f;        // (lerpSettings.minX~maxX = -0.4~0.4) 가로 ±m
        const float YDriftYMin  = 0.5f;        // (lerpSettings.minY~maxY = 0.5~1) 위로 m
        const float YDriftYMax  = 1f;
        // 치명타가 나타날 때(0.2초) — 두 벌(큰 잔상 + 본체)이 어긋나 있다가 제자리에서 겹친다
        const float YCritScaleIn   = 2f;       // (scaleFadeIn 2,2) 전체가 2배에서 1배로
        // (crossScaleFadeIn 4,4) 잔상은 이 배율에서, 본체는 그 역수에서 1배로. YAZS 값은 4 인데 우리는 훨씬 작게 쓴다 —
        // 잔상은 글자 크기에 비례해 커지는데, 글자를 YAZS 보다 키웠기 때문이다. 글자 1.7배일 때 4 는 화면을 너무 크게 덮어 2 로 줄였고
        // (프레임별 미리보기 그림으로 확인, 잔상 높이 약 1.7m), 글자를 2.38배로 키우면서 1.6 으로 한 번 더 줄였다(약 2.0m).
        const float YCritCross     = 1.6f;
        const float YCritShake     = 1.5f;     // (shakeOffsetFadeIn 0,1.5) 위아래로 어긋나는 거리(m)
        const float YCritShakeFreq = 4f;       // (shakeFrequencyFadeIn 4)
        const float YCritGhostAlpha = 0.35f;   // 잔상 불투명도(본체 대비). 외곽선이 검어서 짙으면 화면이 탁해진다
        const float YCritScaleOut  = 0.4f;     // (scaleFadeOut 0.4,0.4) 사라질 때 이 크기까지 줄어든다

        struct Popup
        {
            public Vector3 pos;
            public float value, age, life, jitter;
            public Vector2 drift; // YAZS 식 — 미끄러져 가서 멈출 자리(카메라 오른쪽·위, m)
            public bool crit;
            public bool minor;    // 기존 식 — 평범한 피해: 작게, 짧게
            public int key;       // 기존 식 — 합치기용 대상 id(0 = 합치지 않음)
            public bool alive;
            // 플레이어 피해(ShowPlayer) — 색을 정해 주고, 앞에 마이너스, YAZS 식에선 굵은 글자 · 크기 배율
            public bool custom, minus, bold;
            public Color col;
            public float sizeMul;
        }

        static ProtoDamageText _i;
        readonly Popup[] _p = new Popup[MaxPopups];
        float[] _widths      = { 0.92f, 0.72f, 0.88f, 0.91f, 0.88f, 0.92f, 0.93f, 0.73f, 0.92f, 0.93f };   // 기존 식(Impact)
        float[] _widthsY     = { 0.62f, 0.56f, 0.63f, 0.63f, 0.67f, 0.64f, 0.62f, 0.62f, 0.63f, 0.62f };   // YAZS 식 일반
        float[] _widthsYBold = { 0.72f, 0.66f, 0.72f, 0.72f, 0.76f, 0.73f, 0.71f, 0.71f, 0.71f, 0.71f };   // YAZS 식 치명타
        Mesh _mesh;
        Material _mat;
        Texture2D _texClassic, _texYazs;
        Style _style;
        bool _styleSet;
        int _quadCap, _usedQuads;
        Vector3[] _v; Vector2[] _uv; Color32[] _c; int[] _tri;   // 색은 Color32 — 정점당 16 → 4바이트(매 프레임 올리는 양, 2026-11-20). 숫자 색은 모두 0~1
        readonly int[] _digits = new int[MaxDigits + 1];   // + 마이너스 한 칸
        /// 아틀라스 가로 칸 수 — 10(숫자만) 또는 11(11번째 = 마이너스). 칸 폭 json 의 길이로 안다(예전 아틀라스면 마이너스 없이 그린다)
        int _glyphs = 10, _glyphsY = 10;
        const int Minus = 10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _i = null;

        /// <summary>
        /// 숫자 하나를 띄운다. minor = 평범한 피해(붐비면 건너뛴다).
        /// key 는 기존 식에서만 쓴다 — 0 이 아니면 같은 key 의 최근(0.5초 안) 숫자에 더한다(치명타가 섞이면 노랑).
        /// </summary>
        public static void Show(Vector3 worldPos, float amount, bool crit, int key = 0, bool minor = false)
        {
            if (amount < 0.5f || !ProtoSettings.DamageNumbers) return;
            if (_i == null)
            {
                var go = new GameObject("_DamageText");
                _i = go.AddComponent<ProtoDamageText>();
                if (!_i.Build()) { Destroy(go); _i = null; return; }
            }
            _i.SyncStyle();
            // 평범한 피해는 붐빌 때 버린다. 기존 식은 이미 떠 있는 같은 대상 숫자에는 더한다(합치기는 화면을 더 채우지 않는다).
            if (minor && _i.TooCrowded(key)) return;
            if (_i._style == Style.Yazs) _i.AddYazs(worldPos, amount, crit);
            else _i.AddClassic(worldPos, amount, crit, key, minor);
        }

        /// <summary>
        /// 플레이어가 받은 피해 — "-9" 처럼 앞에 마이너스, 정해 준 색(<see cref="ColPlayerHurt"/> · <see cref="ColPlayerPoison"/>).
        /// 붐벼도 건너뛰지 않고 합치지도 않는다. YAZS 식에선 굵은 글자를 일반의 <see cref="YPlayerSize"/> 배로.
        /// </summary>
        public static void ShowPlayer(Vector3 worldPos, float amount, Color color)
        {
            if (amount < 0.5f || !ProtoSettings.DamageNumbers) return;
            if (_i == null)
            {
                var go = new GameObject("_DamageText");
                _i = go.AddComponent<ProtoDamageText>();
                if (!_i.Build()) { Destroy(go); _i = null; return; }
            }
            _i.SyncStyle();
            if (_i._style == Style.Yazs) _i.AddYazs(worldPos, amount, false, color, minus: true, bold: true, sizeMul: YPlayerSize);
            else _i.AddClassic(worldPos, amount, false, 0, false, color, minus: true);
        }

        bool TooCrowded(int key)
        {
            if (_style == Style.Yazs) return LiveCount() >= YazsCrowdLimit;
            return LiveCount() >= CrowdLimit && !HasRecent(key);
        }

        int LiveCount()
        {
            int n = 0;
            for (int k = 0; k < MaxPopups; k++) if (_p[k].alive) n++;
            return n;
        }

        bool HasRecent(int key)
        {
            if (key == 0) return false;
            for (int k = 0; k < MaxPopups; k++) if (_p[k].alive && _p[k].key == key && _p[k].age <= MergeWindow) return true;
            return false;
        }

        /// 떠 있는 숫자를 전부 지운다(재시작 · 옵션에서 모양을 바꿀 때).
        public static void Clear()
        {
            if (_i == null) return;
            for (int k = 0; k < MaxPopups; k++) _i._p[k].alive = false;
        }

        bool Build()
        {
            var refMat = Resources.Load<Material>("ShaderRefs/Ref_Proto_DamageText");
            _texClassic = Resources.Load<Texture2D>("VFX/T_DamageDigits");
            _texYazs = Resources.Load<Texture2D>("VFX/T_DamageDigitsYazs");
            if (refMat == null || (_texClassic == null && _texYazs == null))
            {
                Debug.LogWarning("[DamageText] Ref_Proto_DamageText 머티리얼 또는 숫자 텍스처(T_DamageDigitsYazs · T_DamageDigits)가 없어 숫자를 끈다");
                return false;
            }
            var meta = Resources.Load<TextAsset>("VFX/T_DamageDigits");
            // 칸 폭 배열 길이 = 아틀라스 칸 수(10 = 숫자만, 11 = 11번째에 마이너스. 2026-11-12 마이너스 추가)
            if (meta != null)
            {
                var j = JsonUtility.FromJson<Meta>(meta.text);
                if (j != null && j.widths != null && (j.widths.Length == 10 || j.widths.Length == 11)) { _widths = j.widths; _glyphs = j.widths.Length; }
            }
            meta = Resources.Load<TextAsset>("VFX/T_DamageDigitsYazs");
            if (meta != null)
            {
                var j = JsonUtility.FromJson<Meta>(meta.text);
                if (j != null && j.widths != null && j.widthsBold != null && j.widths.Length == j.widthsBold.Length
                    && (j.widths.Length == 10 || j.widths.Length == 11))
                {
                    _widthsY = j.widths; _widthsYBold = j.widthsBold; _glyphsY = j.widths.Length;
                }
            }

            _mat = new Material(refMat);

            _quadCap = MaxPopups * (MaxDigits + 1) + GhostQuads;   // + 1 = 플레이어 피해의 마이너스 칸
            _v = new Vector3[_quadCap * 4]; _uv = new Vector2[_quadCap * 4]; _c = new Color32[_quadCap * 4]; _tri = new int[_quadCap * 6];
            for (int q = 0; q < _quadCap; q++)
            {
                int v = q * 4, t = q * 6;
                _tri[t] = v; _tri[t + 1] = v + 2; _tri[t + 2] = v + 1;
                _tri[t + 3] = v + 2; _tri[t + 4] = v + 3; _tri[t + 5] = v + 1;
            }
            _mesh = new Mesh { name = "DamageText" };
            _mesh.MarkDynamic();
            _mesh.vertices = _v; _mesh.uv = _uv; _mesh.colors32 = _c; _mesh.triangles = _tri;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);   // 정점이 월드 좌표라 컬링되지 않게

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            SyncStyle();
            return true;
        }

        [System.Serializable] class Meta { public float[] widths; public float[] widthsBold; }

        /// 옵션이 고른 모양으로 맞춘다(텍스처 교체). 모양이 바뀌면 떠 있던 숫자는 지운다 — 두 모양은 수명·자리 계산이 다르다.
        void SyncStyle()
        {
            Style want = ProtoSettings.DamageStyle == (int)Style.Classic ? Style.Classic : Style.Yazs;
            if (want == Style.Yazs && _texYazs == null) want = Style.Classic;       // 아틀라스가 없으면 있는 쪽으로
            if (want == Style.Classic && _texClassic == null) want = Style.Yazs;
            if (_styleSet && want == _style) return;
            _style = want;
            _styleSet = true;
            _mat.SetTexture("_BaseMap", want == Style.Yazs ? _texYazs : _texClassic);
            for (int k = 0; k < MaxPopups; k++) _p[k].alive = false;
        }

        // ════════════════════════════════════════════════════════════════
        // 띄우기

        void AddYazs(Vector3 pos, float amount, bool crit, Color? col = null, bool minus = false, bool bold = false, float sizeMul = 1f)
        {
            // 빈 칸, 없으면 수명을 가장 많이 쓴 것
            int slot = 0;
            float most = -1f;
            for (int k = 0; k < MaxPopups; k++)
            {
                if (!_p[k].alive) { slot = k; break; }
                float used = _p[k].age / (_p[k].life + YFadeOut);
                if (used > most) { most = used; slot = k; }
            }
            _p[slot] = new Popup
            {
                pos = pos, value = amount, crit = crit, alive = true, age = 0f,
                life = crit ? YLifeCrit : YLife,
                drift = new Vector2(Random.Range(-YDriftX, YDriftX), Random.Range(YDriftYMin, YDriftYMax)),
                custom = col.HasValue, col = col ?? ColNormal, minus = minus, bold = bold, sizeMul = sizeMul,
            };
        }

        void AddClassic(Vector3 pos, float amount, bool crit, int key, bool minor, Color? col = null, bool minus = false)
        {
            if (key != 0)
            {
                for (int k = 0; k < ClassicPopups; k++)
                {
                    ref var p = ref _p[k];
                    if (!p.alive || p.key != key || p.age > MergeWindow) continue;
                    // 치명타도 같은 숫자에 합친다(따로 띄우면 같은 머리 위에서 겹쳤다). 하나라도 섞이면 노랑·큰 글자.
                    p.value += amount;
                    if (crit && !p.crit) { p.crit = true; p.life = LifeCrit; }
                    if (!minor && p.minor) { p.minor = false; if (!p.crit) p.life = Life; }   // 중요한 피해가 섞이면 제 크기로
                    p.age = 0.05f;      // 다시 튄다(처음부터 크게 튀면 너무 요란하다)
                    p.pos = pos;
                    return;
                }
            }

            int slot = 0;
            float oldest = -1f;
            for (int k = 0; k < ClassicPopups; k++)
            {
                if (!_p[k].alive) { slot = k; oldest = float.MaxValue; break; }
                if (_p[k].age > oldest) { oldest = _p[k].age; slot = k; }
            }
            // 막 뜬 숫자 바로 옆이면 위로 비켜 쌓는다 — 폭발 하나에 여러 마리가 맞으면 숫자가 한 덩어리로 뭉갰다
            for (int k = 0; k < ClassicPopups; k++)
            {
                ref var o = ref _p[k];
                if (!o.alive || o.age > 0.25f) continue;
                Vector3 d = o.pos - pos; d.y = 0f;
                if (d.sqrMagnitude < 0.36f && Mathf.Abs(o.pos.y - pos.y) < 0.4f) pos.y = o.pos.y + 0.45f;
            }

            _p[slot] = new Popup
            {
                pos = pos, value = amount, crit = crit, key = key, alive = true, minor = minor && !crit,
                age = 0f, life = crit ? LifeCrit : (minor ? LifeMinor : Life), jitter = Random.Range(-0.3f, 0.3f),
                custom = col.HasValue, col = col ?? ColNormal, minus = minus, sizeMul = 1f,
            };
        }

        // ════════════════════════════════════════════════════════════════
        // 그리기

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || _mesh == null) return;
            SyncStyle();     // 옵션에서 모양을 바꾸면 바로
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float dt = Time.deltaTime;

            int q = 0;
            if (_style == Style.Yazs) TickYazs(ref q, right, up, dt);
            else TickClassic(ref q, right, up, dt);

            if (q == 0 && _usedQuads == 0) return;     // 떠 있는 게 없으면 메시를 다시 올리지 않는다
            // 지난 프레임에 쓰고 이번엔 안 쓴 쿼드는 접어서 안 보이게
            for (int r = q * 4; r < _usedQuads * 4; r++) _v[r] = Vector3.zero;
            _usedQuads = q;

            _mesh.vertices = _v; _mesh.uv = _uv; _mesh.colors32 = _c;
        }

        void TickYazs(ref int q, Vector3 right, Vector3 up, float dt)
        {
            for (int k = 0; k < MaxPopups; k++)
            {
                ref var p = ref _p[k];
                if (!p.alive) continue;
                p.age += dt;
                if (p.age >= p.life + YFadeOut) p.alive = false;
            }

            // 일반을 먼저, 치명타를 나중에(위에) 그린다
            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 0; k < MaxPopups; k++)
                {
                    ref var p = ref _p[k];
                    if (!p.alive || p.crit != (pass == 1)) continue;

                    // 나타남(0.2초) → 머무름 → 사라짐(0.2초)
                    float fade = p.age < YFadeIn ? p.age / YFadeIn
                               : p.age > p.life ? 1f - (p.age - p.life) / YFadeOut : 1f;
                    // 무작위 자리로 미끄러져 가서 멈춘다
                    float moved = 1f - Mathf.Exp(-YDriftSpeed * p.age);
                    Vector3 center = p.pos + right * (p.drift.x * moved) + up * (p.drift.y * moved);
                    int n = FillDigits(p.value, p.minus && _glyphsY > Minus);

                    if (!p.crit)
                    {
                        Color c = p.custom ? p.col : ColNormal; c.a = fade;
                        if (p.bold) Emit(ref q, center, right, up, YHeight * p.sizeMul, n, c, _widthsYBold, YKern, 0f, 0.5f, _glyphsY);   // 플레이어 피해: 굵은 줄
                        else Emit(ref q, center, right, up, YHeight, n, c, _widthsY, YKern, 0.5f, 1f, _glyphsY);
                        continue;
                    }

                    Color col = ColCrit;
                    if (p.age < YFadeIn)
                    {
                        // 큰 잔상과 본체가 위아래로 어긋나 있다가 제 크기·제자리에서 겹친다
                        float inv = 1f - fade;
                        float baseScale = Mathf.Lerp(YCritScaleIn, 1f, fade);
                        float shake = YCritShake * Mathf.Sin(inv * YCritShakeFreq) * inv * baseScale;
                        col.a = fade * YCritGhostAlpha;
                        Emit(ref q, center + up * shake, right, up, YHeightCrit * baseScale * Mathf.Lerp(YCritCross, 1f, fade),
                             n, col, _widthsYBold, YKern, 0f, 0.5f, _glyphsY);
                        col.a = fade;
                        Emit(ref q, center - up * shake, right, up, YHeightCrit * baseScale * Mathf.Lerp(1f / YCritCross, 1f, fade),
                             n, col, _widthsYBold, YKern, 0f, 0.5f, _glyphsY);
                    }
                    else
                    {
                        float s = p.age > p.life ? Mathf.Lerp(YCritScaleOut, 1f, fade) : 1f;
                        col.a = fade;
                        Emit(ref q, center, right, up, YHeightCrit * s, n, col, _widthsYBold, YKern, 0f, 0.5f, _glyphsY);
                    }
                }
            }
        }

        void TickClassic(ref int q, Vector3 right, Vector3 up, float dt)
        {
            for (int k = 0; k < ClassicPopups; k++)
            {
                ref var p = ref _p[k];
                if (!p.alive) continue;
                p.age += dt;
                if (p.age >= p.life) { p.alive = false; continue; }

                float u = p.age / p.life;
                // 톡 튀어나온다: 0.08초에 1.35배 → 0.18초에 1배
                float s = p.age < 0.08f ? Mathf.Lerp(0.3f, 1.35f, p.age / 0.08f)
                        : p.age < 0.18f ? Mathf.Lerp(1.35f, 1f, (p.age - 0.08f) / 0.1f) : 1f;
                float rise = Rise * (1f - (1f - u) * (1f - u));            // 처음 빠르게, 나중 느리게
                float a = u < 0.7f ? 1f : 1f - (u - 0.7f) / 0.3f;
                float h = (p.crit ? HeightCrit : p.minor ? HeightMinor : HeightNormal) * s;
                Color col = p.custom ? p.col : p.crit ? ColCrit : ColNormal; col.a = a;

                Emit(ref q, p.pos + up * rise + right * p.jitter, right, up, h, FillDigits(p.value, p.minus && _glyphs > Minus), col, _widths, Kern, 0f, 1f, _glyphs);
            }
        }

        /// 값을 자릿수로 풀어 <see cref="_digits"/> 에 넣는다(낮은 자리부터). minus 면 맨 끝(= 맨 왼쪽에 그려진다)에 마이너스 칸.
        /// 칸 수(자릿수 + 마이너스)를 돌려준다.
        int FillDigits(float value, bool minus = false)
        {
            int n = 0, val = Mathf.Max(1, Mathf.RoundToInt(value));
            while (val > 0 && n < MaxDigits) { _digits[n++] = val % 10; val /= 10; }
            if (minus) _digits[n++] = Minus;
            return n;
        }

        /// <see cref="_digits"/> 의 n 칸을 center 에 가운데 정렬해 쿼드로 채운다. h = 글자(칸) 높이, v0~v1 = 아틀라스에서 쓸 줄,
        /// glyphs = 아틀라스 가로 칸 수(10 또는 11).
        void Emit(ref int q, Vector3 center, Vector3 right, Vector3 up, float h, int n, Color col, float[] widths, float kern, float v0, float v1, int glyphs)
        {
            if (q + n > _quadCap) return;      // 쿼드가 모자라면 이 숫자는 건너뛴다(치명타 잔상이 한꺼번에 몰릴 때만)
            float cellW = h * (64f / 96f);
            float total = 0f;
            for (int d = n - 1; d >= 0; d--) total += widths[_digits[d]] * cellW + (d > 0 ? kern * h : 0f);

            float x = -total * 0.5f;
            Color32 col32 = col;
            for (int d = n - 1; d >= 0; d--)
            {
                int g = _digits[d];
                float w = widths[g] * cellW;
                Vector3 bl = center + right * x - up * (h * 0.5f);
                int v = q * 4;
                _v[v]     = bl;
                _v[v + 1] = bl + right * w;
                _v[v + 2] = bl + up * h;
                _v[v + 3] = bl + right * w + up * h;
                float u0 = (g + 0.5f - widths[g] * 0.5f) / glyphs, u1 = (g + 0.5f + widths[g] * 0.5f) / glyphs;
                _uv[v] = new Vector2(u0, v0); _uv[v + 1] = new Vector2(u1, v0);
                _uv[v + 2] = new Vector2(u0, v1); _uv[v + 3] = new Vector2(u1, v1);
                _c[v] = _c[v + 1] = _c[v + 2] = _c[v + 3] = col32;
                x += w + kern * h;
                q++;
            }
        }

        void OnDestroy()
        {
            if (_i == this) _i = null;
            if (_mat != null) Destroy(_mat);
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
