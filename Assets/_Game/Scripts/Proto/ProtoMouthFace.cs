using UnityEngine;

namespace Proto
{
    /// 상황별 입모양 (사용자 요청 2026-10-28).
    ///
    /// 입은 별도 메시(sd_rnd_bcm01_mouth*)이고 텍스처는 8×8 입모양 시트다. 메시 UV 는 한 칸만 덮으므로
    /// _BaseMap 오프셋을 칸 단위(0.125)로 옮기면 다른 입이 보인다 — 메시·텍스처는 손대지 않는다.
    ///
    /// 머티리얼(Character_Mouth.mat)은 캐릭터들이 **같이 쓴다.** 머티리얼 값을 바꾸면 다른 캐릭터까지 같이 바뀌므로
    /// MaterialPropertyBlock 으로 이 렌더러에만 넣는다.
    ///
    /// Player 루트에 붙는다 — Visual 이 교체돼도(ProtoCharacterSwitcher) 다시 찾아 이어 간다.
    /// 우선순위: 사망 > 승리 > 피격 > 기쁨(레벨업·버프 줍기·회복) > 아드레날린 > 체력 낮음 > 기본(캐릭터 원래 입).
    public class ProtoMouthFace : MonoBehaviour
    {
        const int   Grid = 8;
        const float Cell = 1f / Grid;
        const string MouthMat = "Character_Mouth";

        // 칸 = (열, 행) — 왼쪽 위가 (0,0). 시트 아래쪽 청록색 칸(4행 5열~)은 같은 모양의 단색판이라 쓰지 않는다.
        [Header("입모양 칸 (열, 행 — 왼쪽 위 0,0)")]
        public Vector2Int hurt       = new Vector2Int(3, 3);   // 이를 악문 입
        public Vector2Int joy        = new Vector2Int(6, 0);   // 활짝 웃음
        public Vector2Int adrenaline = new Vector2Int(1, 1);   // 크게 벌리고 송곳니
        public Vector2Int lowHP      = new Vector2Int(6, 2);   // 물결 — 불안
        public Vector2Int dead       = new Vector2Int(0, 3);   // 비명
        public Vector2Int victory    = new Vector2Int(1, 2);   // 이 보이며 활짝

        [Header("유지 시간(초, 실제 시간)")]
        public float hurtTime = 0.45f;
        public float joyTime  = 1.2f;
        [Range(0f, 1f)] public float lowHPRatio = 0.3f;

        ProtoPlayer _player;
        Transform _visual;
        Renderer _mouth;
        Vector2Int _base;
        Vector2Int _shown = new Vector2Int(-99, -99);
        MaterialPropertyBlock _mpb;
        static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        float _lastHP = -1f, _hurtUntil, _joyUntil;
        bool _wasAdren, _wasShield;
        RunState _lastState;

        void Awake()
        {
            _player = GetComponent<ProtoPlayer>();
            _mpb = new MaterialPropertyBlock();
        }

        /// 다른 코드가 기쁜 표정을 띄우고 싶을 때(보스 처치 등).
        public void Joy() => _joyUntil = Time.unscaledTime + joyTime;

        // ── 확인용 — 컴포넌트 ⋮ 메뉴. 플레이 중엔 2초 동안 띄우고, 플레이 전엔 그대로 둔다(씬에 저장되지 않는다) ──
        Vector2Int _testCell; float _testUntil;
        [ContextMenu("확인/피격")]      void TestHurt()       => Test(hurt);
        [ContextMenu("확인/기쁨")]      void TestJoy()        => Test(joy);
        [ContextMenu("확인/아드레날린")] void TestAdrenaline() => Test(adrenaline);
        [ContextMenu("확인/체력 낮음")]  void TestLowHP()      => Test(lowHP);
        [ContextMenu("확인/사망")]      void TestDead()       => Test(dead);
        [ContextMenu("확인/승리")]      void TestVictory()    => Test(victory);
        [ContextMenu("확인/원래 입")]    void TestReset()      => Test(new Vector2Int(-1, -1));

        void Test(Vector2Int c)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_player == null) _player = GetComponent<ProtoPlayer>();
            Bind(transform.Find("Visual"));
            if (_mouth == null) { Debug.LogWarning("[입모양] Visual 아래에서 입을 못 찾음"); return; }
            if (Application.isPlaying) { _testCell = c; _testUntil = Time.unscaledTime + 2f; }
            else Show(c.x < 0 ? _base : c);
        }

        void LateUpdate()
        {
            if (_player == null) return;
            // 이름 찾기는 지금 Visual 이 떨어져 나갔거나 사라졌을 때만(캐릭터 교체) — 매 프레임 Find 대신(2026-11-20)
            var vis = (_visual != null && _visual.parent == transform) ? _visual : transform.Find("Visual");
            if (vis != _visual) Bind(vis);
            if (_mouth == null) return;

            float now = Time.unscaledTime;   // 레벨업 카드 중엔 timeScale 0 — 표정은 그래도 바뀌어야 한다
            var run = ProtoRun.I;
            var state = run != null ? run.state : RunState.Running;

            // 이벤트 감지 — 다른 스크립트에 손대지 않고 상태 변화로 읽는다
            float hp = _player.hp;
            if (_lastHP >= 0f)
            {
                if (hp < _lastHP - 0.01f) _hurtUntil = now + hurtTime;
                else if (hp > _lastHP + 0.01f && state != RunState.LevelUp) Joy();   // 회복 (최대 체력 증가 카드는 레벨업 중이라 기쁨이 이미 떠 있다)
            }
            _lastHP = hp;
            if (_player.IsAdrenaline && !_wasAdren) Joy();
            if (_player.IsShielded && !_wasShield) Joy();
            _wasAdren = _player.IsAdrenaline; _wasShield = _player.IsShielded;
            if (state == RunState.LevelUp && _lastState != RunState.LevelUp) Joy();
            _lastState = state;

            Vector2Int want;
            if (now < _testUntil)                                  want = _testCell.x < 0 ? _base : _testCell;
            else if (!_player.IsAlive)                             want = dead;
            else if (state == RunState.Result)                     want = victory;   // 살아서 끝났다 = 버텼다
            else if (now < _hurtUntil)                             want = hurt;
            else if (now < _joyUntil || state == RunState.LevelUp) want = joy;
            else if (_player.IsAdrenaline)                         want = adrenaline;
            else if (_player.hp < _player.maxHP * lowHPRatio)      want = lowHP;
            else                                                   want = _base;
            Show(want);
        }

        void Bind(Transform vis)
        {
            _visual = vis;
            _mouth = null;
            _shown = new Vector2Int(-99, -99);
            if (vis == null) return;
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.StartsWith(MouthMat)) { _mouth = r; break; }
            }
            if (_mouth == null) return;
            var mc = _mouth.GetComponent<ProtoMouthCell>();
            _base = mc != null ? mc.cell : Vector2Int.zero;
            if (mc == null) Debug.LogWarning($"[입모양] {vis.name} 입에 ProtoMouthCell 이 없다 — 0,0 칸으로 가정. Tools/좀비 오버런/입모양 기준 칸 굽기 를 돌릴 것");
        }

        void Show(Vector2Int c)
        {
            if (c == _shown) return;
            _shown = c;
            _mouth.GetPropertyBlock(_mpb);
            if (c == _base) _mpb.Clear();   // 기본 입 — 블록을 비워 머티리얼 원래 값으로
            // 행은 위에서부터 세므로 V 는 반대로 간다
            else _mpb.SetVector(BaseMapST, new Vector4(1f, 1f, (c.x - _base.x) * Cell, -(c.y - _base.y) * Cell));
            _mouth.SetPropertyBlock(_mpb);
        }
    }
}
