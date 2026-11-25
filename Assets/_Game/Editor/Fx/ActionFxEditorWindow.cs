using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Proto;

/// <summary>
/// FX 툴 창 — 액션 시트 편집 · 카탈로그 둘러보기 · 쇼케이스 무대 조종을 한 창에서.
///
/// <para><b>구성.</b> 툴바(시트 · 에셋 만들기 · [▶ 무대]) → 탭 [액션 시트 | 카탈로그] → 무대 줄(쇼케이스가 플레이 중일 때만:
/// 배속 · 정지 · 다시 · 반복 · 턴테이블 · 밤 · 큰 한 방 · 반경). 액션 시트 탭은 타임라인(눈금 · 이벤트 마커 · 플레이헤드)과
/// 이벤트 목록 · 인스펙터. 카탈로그 탭은 ID 목록과 항목 설정. 씬 뷰에는 이벤트 자리와 선택한 이벤트의 오프셋 핸들이 그려진다.</para>
///
/// <para><b>미리보기는 두 가지.</b>
/// ① 창 자체 리그 — [미리보기 배치] 가 지금 씬에 모델(시트의 previewPrefab)을 놓는다. 에디트 모드에선 AnimationMode 로 클립을
///    샘플링해 포즈를 잡고(PoseClipBaker 와 같은 방식), 프리팹 효과는 파티클을 수동으로 진행시켜 스크럽·배속이 그대로 먹는다.
///    코드 그림 효과(폭발 · 망치 불 등 Time 에 묶인 것)는 여기선 안 나온다.
/// ② 무대 — [▶ 무대] 로 쇼케이스 씬을 열고 플레이하면 <see cref="FxShowcase"/> 가 이 창에 연결된다. 그때부터 액션 선택 · 재생 ·
///    스크럽 · 배속 · 카탈로그 항목 선택은 전부 쇼케이스 씬의 모델·더미로 간다(인게임 색감 · 밤 · 코드 그림까지 전부).
///    창의 리그는 치운다. Game 뷰의 키(Space · [ ] · 1~4 …)도 그대로 먹고, 창은 그 상태를 따라간다.</para>
///
/// <para>미리보기 오브젝트는 전부 DontSave 라 씬에 저장되지 않고, 플레이 모드 전환 때 사라진다.</para>
/// </summary>
public class ActionFxEditorWindow : EditorWindow
{
    const string RigName = "__액션FX미리보기__";
    const string ModelName = "모델";
    static readonly float[] Speeds = { 0.1f, 0.25f, 0.5f, 1f };
    static readonly string[] SpeedLabels = { "0.1×", "0.25×", "0.5×", "1×" };
    static readonly string[] StageSpeedLabels = { "1×", "0.5×", "0.25×", "0.1×" };   // FxShowcase.Speeds 순서(1~4 키)
    static readonly string[] TabLabels = { "액션 시트", "카탈로그", "무기" };

    [MenuItem("Tools/좀비 오버런/FX 툴/FX 툴 창 (액션 · 카탈로그 · 무대)")]
    public static void Open() => GetWindow<ActionFxEditorWindow>("FX 툴");

    [SerializeField] ActionFxSheet _sheet;
    [SerializeField] int _tab;
    [SerializeField] int _actionIndex;
    [SerializeField] int _selected = -1;
    [SerializeField] float _time;
    [SerializeField] int _speedIndex = 3;
    [SerializeField] bool _loop = true;
    [SerializeField] bool _showGizmos = true;
    [SerializeField] int _catIndex = -1;
    [SerializeField] string _catFilter = "";
    [SerializeField] float _radius = 2.4f;
    [SerializeField] bool _big;

    // 무기 탭 — 고른 캐릭터 프리팹 · 무기 에셋(경로로 기억해 창을 닫았다 열어도 남는다. 빈 무기 = 캐릭터 기본)
    [SerializeField] string _charPath = "";
    [SerializeField] string _weaponPath = "";
    [SerializeField] bool _tracerFold;
    Vector2 _scrollWeapon;
    readonly List<GameObject> _chars = new List<GameObject>();
    readonly List<WeaponModelDef> _weapons = new List<WeaponModelDef>();
    string[] _charLabels = new string[0], _weaponLabels = new string[0];
    SerializedObject _fxSo, _statsSo;
    const string CharFolder = "Assets/_Game/Art/character";

    bool _playing;
    double _lastTick;
    Vector2 _scrollEvents, _scrollInspector, _scrollCat;
    bool _dragPlayhead;
    int _dragEvent = -1;
    bool _dragEnd;
    float _dragGrab;
    GameObject _rig;
    string[] _socketNames;
    GameObject _socketNamesFor;
    AnimationClip _stateFor;
    string _stateName;
    bool _stateChecked;

    // 카탈로그 탭
    class CatItem
    {
        public string id;
        public FxCatalog.Entry entry;
        public bool builtinOnly;   // 카탈로그에 없는 코드 그림 — ID 가 곧 코드 그림 이름
        public bool IsSpan => entry.lifetime <= 0f;
    }
    readonly List<CatItem> _cat = new List<CatItem>();
    FxHandle _catHandle;      // 창 자체 미리보기로 켠 카탈로그 항목
    double _catUntil;         // 스팬 항목을 자동으로 끌 시각(에디터 시계). 0 = 없음

    // 무대(쇼케이스) 연결
    bool _wasConnected;
    string _syncedLabel;      // 무대가 보여 주는 항목 — 바뀌면 창의 선택을 따라 옮긴다

    ActionFxSheet.Entry Action =>
        _sheet != null && _actionIndex >= 0 && _actionIndex < _sheet.actions.Count ? _sheet.actions[_actionIndex] : null;

    ActionFxSheet.FxEvent Selected =>
        Action != null && _selected >= 0 && _selected < Action.events.Count ? Action.events[_selected] : null;

    /// 플레이 중인 쇼케이스. 없으면 null.
    static FxShowcase Stage => Application.isPlaying ? FxShowcase.Instance : null;
    /// 무대가 지금 이 액션을 보여 주고 있나.
    static bool OnStage(FxShowcase st, ActionFxSheet.Entry a) => st != null && a != null && st.Current != null && st.Current.action == a;

    static FxCatalog Catalog => AssetDatabase.LoadAssetAtPath<FxCatalog>(FxAssetsSetup.CatalogPath);

    // ── 수명 ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        if (_sheet == null) _sheet = AssetDatabase.LoadAssetAtPath<ActionFxSheet>(FxAssetsSetup.SheetPath);
        ProtoFxBuiltins.Register();   // 코드 그림 이름 목록(효과 ID 드롭다운 · 카탈로그 탭)에 쓴다
        BuildCatalogList();
        BuildWeaponLists();
        SceneView.duringSceneGui += OnSceneGUI;
        EditorApplication.update += OnUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Undo.undoRedoPerformed += OnUndo;
        _lastTick = EditorApplication.timeSinceStartup;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorApplication.update -= OnUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        Undo.undoRedoPerformed -= OnUndo;
        StopCatalogPreview();
        ClearRig();
    }

    void OnUndo() { ResyncPlayer(); Repaint(); SceneView.RepaintAll(); }

    void OnPlayModeChanged(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.ExitingEditMode || s == PlayModeStateChange.ExitingPlayMode)
        {
            _catHandle = null; _catUntil = 0.0;
            _wasConnected = false; _syncedLabel = null;
            ClearRig();
        }
    }

    void Dirty() { if (_sheet != null) EditorUtility.SetDirty(_sheet); }

    // ── 미리보기 리그 ───────────────────────────────────────────────
    GameObject FindRig()
    {
        if (_rig != null) return _rig;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())   // DontSave 오브젝트도 찾는다
            if (go.name == RigName && go.transform.parent == null && go.scene.IsValid()) { _rig = go; return go; }
        return null;
    }

    static GameObject Model(GameObject rig)
    {
        if (rig == null) return null;
        var t = rig.transform.Find(ModelName);
        return t != null ? t.gameObject : null;
    }

    ActionFxPlayer Player
    {
        get { var m = Model(FindRig()); return m != null ? m.GetComponent<ActionFxPlayer>() : null; }
    }

    GameObject BuildRig()
    {
        ClearRig();
        var a = Action;
        var prefab = a != null && a.previewPrefab != null ? a.previewPrefab : (_sheet != null ? _sheet.defaultPreviewPrefab : null);
        if (prefab == null)
        {
            Debug.LogWarning("[FX 툴] 미리보기 모델이 없다 — 액션이나 시트에 프리팹을 지정할 것");
            return null;
        }
        var rig = new GameObject(RigName);
        var model = Instantiate(prefab, rig.transform);
        model.name = ModelName;
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        if (a != null && a.previewScale > 0f) model.transform.localScale *= a.previewScale;   // 최종 보스는 스폰 때 ×1.3

        // 게임플레이 컴포넌트는 끈다 — 플레이 모드에서 좀비 AI 가 움직이면 안 된다. 미리보기는 포즈만 샘플링한다
        foreach (var mb in model.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is ActionFxPlayer)) mb.enabled = false;

        var player = model.GetComponent<ActionFxPlayer>();
        if (player == null) player = model.AddComponent<ActionFxPlayer>();
        player.enabled = true;
        player.sheet = _sheet;
        // 보스 망치 머리 — 브레인이 런타임에 등록하는 것과 같게
        var brain = model.GetComponent<ProtoBossBrain>();
        if (brain != null && brain.hammerHead != null) player.SetSocket("HammerHead", brain.hammerHead);

        foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;

        // 씬 뷰가 보는 자리에 놓는다
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { var p = sv.pivot; p.y = 0f; rig.transform.position = p; }

        _rig = rig;
        _socketNamesFor = null;
        _stateChecked = false;
        BeginAction();
        SetTime(0f, seek: true);
        return rig;
    }

    void ClearRig()
    {
        _playing = false;
        var player = Player;
        if (player != null) player.End();
        if (!Application.isPlaying)
        {
            ProtoFx.Clear();
            _catHandle = null; _catUntil = 0.0;
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }
        var rig = FindRig();
        if (rig != null) DestroyImmediate(rig);
        _rig = null;
        SceneView.RepaintAll();
    }

    Vector3 TargetWorld()
    {
        var a = Action; var model = Model(FindRig());
        if (a == null || model == null) return Vector3.zero;
        return model.transform.TransformPoint(a.previewTarget);
    }

    /// 플레이어에 지금 액션을 건다 — 반경·덩치는 브레인이 넘기는 것과 같은 식(인스펙터 값 × lossyScale).
    void BeginAction()
    {
        var a = Action; var model = Model(FindRig()); var player = Player;
        if (a == null || model == null || player == null) return;
        float s = model.transform.lossyScale.x;
        player.Begin(a.key, model.transform.TransformPoint(a.previewTarget), a.previewRadius * s, false, s);
    }

    /// 이벤트 수가 바뀌었거나 시각을 옮겼을 때 — 플레이어의 발화 기록을 지금 시각에 맞춘다.
    void ResyncPlayer()
    {
        var a = Action;
        var st = Stage;
        if (st != null)
        {
            if (OnStage(st, a)) st.Seek(_time);
            SceneView.RepaintAll();
            return;
        }
        var player = Player;
        if (player == null || a == null) return;
        BeginAction();
        player.Seek(_time);
        SceneView.RepaintAll();
    }

    void OnActionChanged()
    {
        _playing = false;
        _selected = -1;
        _time = 0f;
        _stateChecked = false;
        if (FindRig() != null) BuildRig();   // 모델이 다를 수 있다
    }

    // ── 무대(쇼케이스) 연결 ───────────────────────────────────────────
    void OnStageConnected(FxShowcase st)
    {
        _wasConnected = true;
        _playing = false;
        StopCatalogPreview();
        ClearRig();            // 모델은 무대가 세운다 — 둘이 겹치지 않게
        st.showUi = false;     // Game 뷰의 글자는 이 창이 대신한다(무대 줄의 토글로 다시 켤 수 있다)
        _syncedLabel = null;
        if (_tab == 2) StartWeaponOnStage(st);   // [무기] 탭에서 [▶ 무대] 를 눌렀으면 바로 그 캐릭터·무기를 세운다
    }

    /// 무대가 보여 주는 항목을 창의 선택에 반영한다 — Game 뷰에서 키로 넘겨도 창이 따라온다.
    void SyncFromStage(FxShowcase st)
    {
        if (st.WeaponMode) { _syncedLabel = null; return; }   // 무기 무대 중엔 Current 가 쉬고 있다 — 탭을 옮기지 않는다
        var cur = st.Current;
        string label = cur != null ? cur.label : null;
        if (label != _syncedLabel)
        {
            _syncedLabel = label;
            if (cur != null && cur.IsAction && _sheet != null)
            {
                int i = _sheet.actions.IndexOf(cur.action);
                if (i >= 0 && i != _actionIndex) { _actionIndex = i; _selected = -1; _stateChecked = false; }
                _tab = 0;
            }
            else if (cur != null)
            {
                int i = _cat.FindIndex(x => x.id == label);
                if (i >= 0) _catIndex = i;
                _tab = 1;
            }
        }
        if (cur != null && cur.IsAction && cur.action == Action && !_dragPlayhead) _time = st.Elapsed;
    }

    /// 액션을 무대에 올린다. 새로 만든 액션이라 목록에 없으면 무대가 시트를 다시 읽는다.
    void PutOnStage(ActionFxSheet.Entry a, FxShowcase st)
    {
        if (a == null || string.IsNullOrEmpty(a.key)) return;
        if (!st.Select(a.key)) { st.Reload(); st.Select(a.key); }
        _syncedLabel = a.key;
    }

    // ── 시간 · 포즈 ─────────────────────────────────────────────────
    void SetTime(float t, bool seek)
    {
        var a = Action;
        if (a == null) return;
        t = Mathf.Clamp(t, 0f, a.Length);
        var st = Stage;
        if (st != null)
        {
            // 무대 위 모델을 스크럽한다 — 멈추고 시각만 옮긴다. ▶ 로 다시 돈다
            if (OnStage(st, a)) { st.SetPaused(true); st.Seek(t); }
            _time = t;
            Repaint(); SceneView.RepaintAll();
            return;
        }
        var player = Player;
        if (player != null)
        {
            if (!player.Playing || player.Current != a) BeginAction();
            player.SetTarget(TargetWorld());
            if (seek || t < _time) player.Seek(t); else player.Advance(t);
        }
        _time = t;
        SamplePose(t);
        Repaint();
        SceneView.RepaintAll();
    }

    void SamplePose(float t)
    {
        var a = Action; var model = Model(FindRig());
        if (a == null || a.clip == null || model == null) return;
        var anim = model.GetComponentInChildren<Animator>();
        var go = anim != null ? anim.gameObject : model;
        float ct = Mathf.Clamp(t, 0f, a.clip.length);

        if (!Application.isPlaying)
        {
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, a.clip, ct);
            AnimationMode.EndSampling();
            return;
        }

        // 플레이 모드(무대 없이): 애니메이터 상태를 직접 잡는다(휴머노이드 클립은 Animator 를 거쳐야 포즈가 나온다)
        if (anim == null) { a.clip.SampleAnimation(go, ct); return; }
        string state = StateFor(anim, a.clip);
        if (state == null) { anim.enabled = false; a.clip.SampleAnimation(go, ct); return; }
        anim.enabled = true;
        anim.speed = 0f;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        anim.Play(state, 0, a.clip.length > 0f ? ct / a.clip.length : 0f);
        anim.Update(0f);
    }

    /// 컨트롤러에서 이 클립을 모션으로 쓰는 상태 이름. 없으면 null.
    string StateFor(Animator anim, AnimationClip clip)
    {
        if (_stateChecked && _stateFor == clip) return _stateName;
        _stateChecked = true; _stateFor = clip; _stateName = null;
        var ac = anim.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ac == null) return null;
        foreach (var layer in ac.layers)
            foreach (var cs in layer.stateMachine.states)
                if (cs.state.motion == clip) { _stateName = cs.state.name; return _stateName; }
        return null;
    }

    // ── 재생(창 자체 리그) ───────────────────────────────────────────
    void StartPlayback()
    {
        if (FindRig() == null && BuildRig() == null) return;
        var a = Action; if (a == null) return;
        if (_time >= a.Length - 0.001f) RestartPlayback(true);
        _playing = true;
        _lastTick = EditorApplication.timeSinceStartup;
    }

    void RestartPlayback(bool keepPlaying)
    {
        var a = Action; var player = Player;
        if (a == null) return;
        if (player != null)
        {
            if (!player.Playing || player.Current != a) BeginAction();
            player.SetTarget(TargetWorld());
            player.Rewind();
        }
        _time = 0f;
        SamplePose(0f);
        if (player != null) player.Advance(0f);   // 0초 이벤트도 발화한다 (Seek 는 지나간 것으로 친다)
        _playing = keepPlaying;
        Repaint(); SceneView.RepaintAll();
    }

    void OnUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - _lastTick), 0.1f);
        _lastTick = now;

        var st = Stage;
        if (st != null)
        {
            if (!_wasConnected) OnStageConnected(st);
            SyncFromStage(st);
            Repaint();         // 플레이헤드·무대 줄이 따라가게
            return;            // 재생은 무대가 한다
        }
        if (_wasConnected) { _wasConnected = false; _syncedLabel = null; }

        if (_catHandle != null && _catUntil > 0.0 && now >= _catUntil) StopCatalogPreview();

        var a = Action;
        if (_playing && a != null)
        {
            if (FindRig() == null) { _playing = false; return; }
            float speed = Speeds[Mathf.Clamp(_speedIndex, 0, Speeds.Length - 1)];
            float nt = _time + dt * speed;
            if (nt >= a.Length)
            {
                if (_loop) RestartPlayback(true);
                else { _playing = false; SetTime(a.Length, false); }
            }
            else SetTime(nt, false);
            if (!Application.isPlaying) ProtoFx.Tick(dt * speed, dt * speed);
        }
        else if (!Application.isPlaying && ProtoFx.Live.Count > 0)
        {
            ProtoFx.Tick(dt, dt);   // 스크럽으로 켜진 스팬 프리팹 · 카탈로그 미리보기가 계속 돌게
            SceneView.RepaintAll();
        }
    }

    // ── GUI ──────────────────────────────────────────────────────────
    void OnGUI()
    {
        DrawToolbar();
        var st = Stage;
        _tab = GUILayout.Toolbar(_tab, TabLabels);
        if (st != null) DrawStageRow(st);
        if (_tab == 1) { DrawCatalogTab(st); return; }
        if (_tab == 2) { DrawWeaponTab(st); return; }

        if (_sheet == null)
        {
            EditorGUILayout.HelpBox("시트가 없다. [카탈로그·시트 만들기] 를 누르면 Resources/Data 에 생기고 보스 내려찍기·도약이 채워진다.", MessageType.Info);
            return;
        }
        var a = Action;
        if (a == null)
        {
            DrawActionPicker();
            EditorGUILayout.HelpBox("액션을 고르거나 [+ 액션] 으로 만든다.", MessageType.Info);
            return;
        }
        DrawActionPicker();
        DrawActionFields(a);
        EditorGUILayout.Space(4);
        DrawTransport(a);
        DrawTimeline(a);
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        DrawEventList(a);
        DrawInspector(a);
        EditorGUILayout.EndHorizontal();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUI.BeginChangeCheck();
        var ns = (ActionFxSheet)EditorGUILayout.ObjectField(_sheet, typeof(ActionFxSheet), false, GUILayout.Width(200));
        if (EditorGUI.EndChangeCheck()) { _sheet = ns; _actionIndex = 0; _selected = -1; ClearRig(); }
        if (GUILayout.Button("카탈로그·시트 만들기", EditorStyles.toolbarButton))
        {
            FxAssetsSetup.CreateOrUpdate();
            _sheet = AssetDatabase.LoadAssetAtPath<ActionFxSheet>(FxAssetsSetup.SheetPath);
            BuildCatalogList();
            BuildWeaponLists();
            _fxSo = null; _statsSo = null;   // 무기 연출 에셋이 새로 생겼을 수 있다
            if (Stage != null) Stage.Reload();
        }

        var st = Stage;
        if (st != null)
        {
            if (GUILayout.Button(new GUIContent("■ 무대 멈추기", "플레이 모드를 끝낸다"), EditorStyles.toolbarButton)) EditorApplication.isPlaying = false;
        }
        else if (Application.isPlaying)
        {
            using (new EditorGUI.DisabledScope(true)) GUILayout.Button("무대 없음 (다른 씬 플레이 중)", EditorStyles.toolbarButton);
        }
        else if (GUILayout.Button(new GUIContent("▶ 무대", "쇼케이스 씬을 열고(없으면 굽고) 플레이한다. 그러면 이 창이 그 씬을 조종한다 — 인게임 색감·밤·코드 그림까지 전부 보인다"), EditorStyles.toolbarButton))
            FxShowcaseSceneBuilder.OpenAndPlay();

        GUILayout.FlexibleSpace();
        var rig = FindRig();
        using (new EditorGUI.DisabledScope(st != null))
        {
            if (GUILayout.Button(new GUIContent(rig == null ? "미리보기 배치" : "미리보기 다시 배치", "지금 씬에 모델을 놓고 창에서 바로 본다(무대 없이)"), EditorStyles.toolbarButton)) BuildRig();
            using (new EditorGUI.DisabledScope(rig == null))
                if (GUILayout.Button("정리", EditorStyles.toolbarButton)) ClearRig();
        }
        _showGizmos = GUILayout.Toggle(_showGizmos, "씬 기즈모", EditorStyles.toolbarButton);
        EditorGUILayout.EndHorizontal();
    }

    /// 무대 줄 — 쇼케이스가 플레이 중일 때만. Game 뷰의 키와 같은 일을 한다.
    void DrawStageRow(FxShowcase st)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label(new GUIContent("무대", "쇼케이스 씬이 플레이 중 — 이 줄과 탭의 선택이 그 씬을 조종한다"), EditorStyles.boldLabel, GUILayout.Width(30));
        int si = EditorGUILayout.Popup(st.SpeedIndex, StageSpeedLabels, GUILayout.Width(56));
        if (si != st.SpeedIndex) st.SetSpeed(si);
        if (GUILayout.Button(new GUIContent(st.Paused ? "▶" : "❚❚", st.Paused ? "계속 (P)" : "정지 (P)"), GUILayout.Width(30))) st.SetPaused(!st.Paused);
        if (GUILayout.Button(new GUIContent("↻ 다시", "지금 항목을 처음부터 (Space)"), GUILayout.Width(54))) st.Replay();
        st.loop = GUILayout.Toggle(st.loop, new GUIContent("반복", "끝나면 다시 튼다 (L)"), EditorStyles.miniButton, GUILayout.Width(40));
        if (st.turntable != null)
        {
            st.turntable.spinning = GUILayout.Toggle(st.turntable.spinning, new GUIContent("턴테이블", "(T)"), EditorStyles.miniButton, GUILayout.Width(58));
            if (GUILayout.Button(new GUIContent("↺", "방향 초기화 (R)"), GUILayout.Width(26))) st.ResetTurntable();
        }
        if (st.night != null) st.night.enabled = GUILayout.Toggle(st.night.enabled, new GUIContent("밤", "밤 어둠 (N)"), EditorStyles.miniButton, GUILayout.Width(32));
        st.big = GUILayout.Toggle(st.big, new GUIContent("큰 한 방", "도약 착지 같은 big (B)"), EditorStyles.miniButton, GUILayout.Width(60));
        GUILayout.Label(new GUIContent("반경", "코드 그림이 링 크기로 쓰는 판정 반경 (+/-)"), GUILayout.Width(28));
        st.radius = EditorGUILayout.Slider(st.radius, 0.5f, 8f, GUILayout.MinWidth(110));
        if (GUILayout.Button(new GUIContent("다시 읽기", "카탈로그·시트를 다시 읽는다 (F5)"), GUILayout.Width(64))) { st.Reload(); BuildCatalogList(); _syncedLabel = null; }
        st.showUi = GUILayout.Toggle(st.showUi, new GUIContent("화면 글자", "Game 뷰의 목록·도움말 (H)"), EditorStyles.miniButton, GUILayout.Width(62));
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(st.Status)) EditorGUILayout.HelpBox(st.Status, MessageType.Warning);
    }

    void DrawActionPicker()
    {
        EditorGUILayout.BeginHorizontal();
        var keys = _sheet.actions.Select((x, i) => x != null && !string.IsNullOrEmpty(x.key) ? x.key : $"(이름 없음 {i})").ToArray();
        EditorGUI.BeginChangeCheck();
        int ni = keys.Length > 0 ? EditorGUILayout.Popup("액션", Mathf.Clamp(_actionIndex, 0, keys.Length - 1), keys) : _actionIndex;
        if (EditorGUI.EndChangeCheck() && ni != _actionIndex)
        {
            _actionIndex = ni; OnActionChanged();
            if (Stage != null) PutOnStage(Action, Stage);
        }
        if (GUILayout.Button("+ 액션", GUILayout.Width(64)))
        {
            Undo.RecordObject(_sheet, "액션 추가");
            _sheet.actions.Add(new ActionFxSheet.Entry { key = "New/Action" });
            _actionIndex = _sheet.actions.Count - 1;
            Dirty();
            OnActionChanged();
        }
        using (new EditorGUI.DisabledScope(Action == null))
        {
            if (GUILayout.Button("− 액션", GUILayout.Width(64)) &&
                EditorUtility.DisplayDialog("액션 삭제", $"'{Action.key}' 를 지울까? (되돌리기 가능)", "지운다", "취소"))
            {
                Undo.RecordObject(_sheet, "액션 삭제");
                _sheet.actions.RemoveAt(_actionIndex);
                _actionIndex = Mathf.Clamp(_actionIndex, 0, _sheet.actions.Count - 1);
                Dirty();
                OnActionChanged();
                if (Stage != null) Stage.Reload();
                EditorGUILayout.EndHorizontal();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    void DrawActionFields(ActionFxSheet.Entry a)
    {
        EditorGUI.BeginChangeCheck();
        string key = EditorGUILayout.TextField(new GUIContent("키", "코드가 부르는 이름. 예: Boss_Final/Slam"), a.key);
        var clip = (AnimationClip)EditorGUILayout.ObjectField("클립", a.clip, typeof(AnimationClip), false);
        var prefab = (GameObject)EditorGUILayout.ObjectField("미리보기 모델", a.previewPrefab, typeof(GameObject), false);
        var pt = EditorGUILayout.Vector3Field(new GUIContent("Target (루트 로컬)", "충돌점·착지점의 미리보기 자리. 게임에서는 코드가 넘긴다"), a.previewTarget);
        EditorGUILayout.BeginHorizontal();
        float pr = EditorGUILayout.FloatField(new GUIContent("판정 반경", "Target 원의 반경(덩치 배율 전)"), a.previewRadius);
        float ps = EditorGUILayout.FloatField(new GUIContent("덩치", "스폰 때 곱해지는 배율. 최종 보스 1.3"), a.previewScale);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_sheet, "액션 설정");
            bool rebuild = prefab != a.previewPrefab || !Mathf.Approximately(ps, a.previewScale) || clip != a.clip || key != a.key;
            a.key = key; a.clip = clip; a.previewPrefab = prefab; a.previewTarget = pt; a.previewRadius = pr; a.previewScale = ps;
            _stateChecked = false;
            Dirty();
            var st = Stage;
            if (st != null) { if (rebuild) { st.Reload(); PutOnStage(a, st); } else ResyncPlayer(); }
            else if (rebuild && FindRig() != null) BuildRig(); else ResyncPlayer();
        }
    }

    void DrawTransport(ActionFxSheet.Entry a)
    {
        var st = Stage;
        if (st != null) { DrawStageTransport(a, st); return; }

        var rig = FindRig();
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(rig == null))
        {
            if (GUILayout.Button("|◀", GUILayout.Width(36))) RestartPlayback(false);
            if (GUILayout.Button(_playing ? "■" : "▶", GUILayout.Width(36)))
            {
                if (_playing) _playing = false; else StartPlayback();
            }
        }
        EditorGUI.BeginChangeCheck();
        float t = EditorGUILayout.Slider(_time, 0f, a.Length);
        if (EditorGUI.EndChangeCheck()) { _playing = false; SetTime(t, true); }
        _speedIndex = EditorGUILayout.Popup(_speedIndex, SpeedLabels, GUILayout.Width(64));
        _loop = GUILayout.Toggle(_loop, "반복", GUILayout.Width(48));
        EditorGUILayout.EndHorizontal();
        if (rig == null)
            EditorGUILayout.HelpBox("[미리보기 배치] 를 누르면 지금 씬에 모델이 놓이고 스크럽·재생이 된다(프리팹 효과만). " +
                                    "코드 그림·소리·흔들림·인게임 색감까지 보려면 [▶ 무대] — 쇼케이스 씬을 플레이하고 이 창이 그걸 조종한다.", MessageType.None);
        else if (Application.isPlaying)
            EditorGUILayout.HelpBox("플레이 모드(무대 없이): 코드 그림 효과까지 전부 나온다. 배속은 포즈에만 걸린다(효과는 게임 시간).", MessageType.None);
    }

    /// 무대가 연결됐을 때의 재생 줄 — 창의 리그 대신 쇼케이스 씬의 모델을 돌린다.
    void DrawStageTransport(ActionFxSheet.Entry a, FxShowcase st)
    {
        bool on = OnStage(st, a);
        EditorGUILayout.BeginHorizontal();
        if (!on)
        {
            if (GUILayout.Button(new GUIContent("▶ 무대에 올리기", "이 액션을 쇼케이스 씬에서 재생한다"), GUILayout.Width(110))) PutOnStage(a, st);
            GUILayout.FlexibleSpace();
        }
        else
        {
            if (GUILayout.Button(new GUIContent("|◀", "처음부터 (Space)"), GUILayout.Width(36))) st.Replay();
            if (GUILayout.Button(new GUIContent(st.Paused ? "▶" : "❚❚", st.Paused ? "계속" : "정지"), GUILayout.Width(36))) st.SetPaused(!st.Paused);
            EditorGUI.BeginChangeCheck();
            float t = EditorGUILayout.Slider(_time, 0f, a.Length);
            if (EditorGUI.EndChangeCheck()) SetTime(t, true);
            int si = EditorGUILayout.Popup(st.SpeedIndex, StageSpeedLabels, GUILayout.Width(64));
            if (si != st.SpeedIndex) st.SetSpeed(si);
            st.loop = GUILayout.Toggle(st.loop, "반복", GUILayout.Width(48));
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox(on ? "무대 재생 중 — 쇼케이스 씬의 모델로 돈다. 코드 그림·소리·흔들림까지 전부 나온다. 이벤트를 고치면 다음 반복부터 반영되고, 스크럽은 포즈만 옮긴다(멈춘다 — ▶ 로 계속)."
                                   : "이 액션은 지금 무대에 없다. [▶ 무대에 올리기] 를 누르거나 Game 뷰에서 [ ] 키로 넘긴다.", MessageType.None);
    }

    // ── 타임라인 ──────────────────────────────────────────────────────
    static Color KindColor(ActionFxSheet.FxEvent e)
    {
        switch (e.kind)
        {
            case ActionFxSheet.Kind.Span:   return new Color(1f, 0.55f, 0.15f);
            case ActionFxSheet.Kind.Marker: return new Color(0.35f, 0.85f, 1f);
            default:                        return new Color(1f, 0.82f, 0.3f);
        }
    }

    void DrawTimeline(ActionFxSheet.Entry a)
    {
        float len = a.Length;
        int rows = Mathf.Max(1, a.events.Count);
        const float rowH = 18f, rulerH = 20f, pad = 10f, labelW = 130f;
        Rect area = EditorGUILayout.GetControlRect(false, rulerH + rows * rowH + 10f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(area, new Color(0.14f, 0.14f, 0.14f));
        Rect ruler = new Rect(area.x + pad, area.y + 2f, area.width - pad * 2f - labelW, rulerH);
        Rect body = new Rect(ruler.x, ruler.yMax, ruler.width, rows * rowH + 4f);
        float X(float t) => ruler.x + Mathf.Clamp01(t / len) * ruler.width;
        float T(float x) => Mathf.Clamp((x - ruler.x) / ruler.width * len, 0f, len);

        // 눈금
        float step = len > 6f ? 0.5f : 0.1f, major = len > 6f ? 2f : 0.5f;
        var mini = EditorStyles.miniLabel;
        for (float t = 0f; t <= len + 0.0001f; t += step)
        {
            bool big = Mathf.Abs(t / major - Mathf.Round(t / major)) < 0.001f;
            float x = X(t);
            EditorGUI.DrawRect(new Rect(x, ruler.yMax - (big ? 8f : 4f), 1f, big ? 8f : 4f), big ? Color.gray : new Color(0.4f, 0.4f, 0.4f));
            if (big) GUI.Label(new Rect(x - 14f, ruler.y, 40f, 14f), t.ToString("0.0"), mini);
        }
        // 클립 끝
        if (a.clip != null)
        {
            float cx = X(a.clip.length);
            EditorGUI.DrawRect(new Rect(cx, body.y, 1f, body.height), new Color(0.5f, 0.7f, 1f, 0.6f));
            GUI.Label(new Rect(cx - 30f, body.yMax - 14f, 60f, 14f), "클립 끝", mini);
        }

        // 이벤트 줄
        for (int i = 0; i < a.events.Count; i++)
        {
            var e = a.events[i];
            if (e == null) continue;
            Rect row = new Rect(body.x, body.y + 2f + i * rowH, body.width, rowH - 2f);
            if (i == _selected) EditorGUI.DrawRect(new Rect(area.x, row.y, area.width, row.height), new Color(1f, 1f, 1f, 0.07f));
            Color col = KindColor(e);
            float x0 = X(e.time);
            if (e.IsSpan)
            {
                float x1 = X(e.EndOrTime);
                EditorGUI.DrawRect(new Rect(x0, row.y + 4f, Mathf.Max(3f, x1 - x0), row.height - 8f), col);
                EditorGUI.DrawRect(new Rect(x1 - 3f, row.y + 2f, 6f, row.height - 4f), new Color(0.9f, 0.9f, 0.9f));   // 끝 손잡이
            }
            else
            {
                EditorGUI.DrawRect(new Rect(x0 - 1f, row.y + 1f, 3f, row.height - 2f), col);
                EditorGUI.DrawRect(new Rect(x0 - 5f, row.y + 4f, 10f, 8f), col);
            }
            string label = (string.IsNullOrEmpty(e.marker) ? "" : "[" + e.marker + "] ") + e.name;
            GUI.Label(new Rect(ruler.xMax + 6f, row.y, labelW - 8f, row.height), label, i == _selected ? EditorStyles.whiteMiniLabel : mini);
        }

        // 플레이헤드
        float px = X(_time);
        EditorGUI.DrawRect(new Rect(px - 1f, ruler.y, 2f, area.height - 4f), new Color(1f, 0.25f, 0.25f));
        GUI.Label(new Rect(px + 3f, ruler.y - 1f, 60f, 14f), _time.ToString("0.00") + "s", EditorStyles.whiteMiniLabel);

        HandleTimelineInput(a, ruler, body, rowH, X, T);
    }

    void HandleTimelineInput(ActionFxSheet.Entry a, Rect ruler, Rect body, float rowH, System.Func<float, float> X, System.Func<float, float> T)
    {
        var ev = Event.current;
        switch (ev.type)
        {
            case EventType.MouseDown:
                if (ev.button != 0) break;
                if (ruler.Contains(ev.mousePosition))
                {
                    _dragPlayhead = true; _playing = false;
                    SetTime(T(ev.mousePosition.x), true);
                    ev.Use();
                    break;
                }
                if (!body.Contains(ev.mousePosition)) break;
                {
                    int row = Mathf.FloorToInt((ev.mousePosition.y - body.y - 2f) / rowH);
                    if (row >= 0 && row < a.events.Count && a.events[row] != null)
                    {
                        var e = a.events[row];
                        float x0 = X(e.time), x1 = X(e.EndOrTime);
                        bool hit = e.IsSpan ? (ev.mousePosition.x >= x0 - 6f && ev.mousePosition.x <= x1 + 6f)
                                            : Mathf.Abs(ev.mousePosition.x - x0) <= 8f;
                        _selected = row;
                        if (hit)
                        {
                            Undo.RecordObject(_sheet, "이벤트 이동");
                            _dragEvent = row;
                            _dragEnd = e.IsSpan && Mathf.Abs(ev.mousePosition.x - x1) <= 6f;
                            _dragGrab = T(ev.mousePosition.x) - (_dragEnd ? e.endTime : e.time);
                        }
                        else { _dragPlayhead = true; _playing = false; SetTime(T(ev.mousePosition.x), true); }
                    }
                    else
                    {
                        _selected = -1; _dragPlayhead = true; _playing = false;
                        SetTime(T(ev.mousePosition.x), true);
                    }
                    ev.Use(); Repaint();
                }
                break;

            case EventType.MouseDrag:
                if (_dragPlayhead) { SetTime(T(ev.mousePosition.x), true); ev.Use(); }
                else if (_dragEvent >= 0 && _dragEvent < a.events.Count)
                {
                    var e = a.events[_dragEvent];
                    float t = Snap(T(ev.mousePosition.x) - _dragGrab);
                    if (_dragEnd) e.endTime = Mathf.Max(e.time, t);
                    else
                    {
                        float d = e.endTime - e.time;
                        e.time = t;
                        if (e.IsSpan) e.endTime = t + Mathf.Max(0f, d);
                    }
                    Dirty(); ev.Use(); Repaint(); SceneView.RepaintAll();
                }
                break;

            case EventType.MouseUp:
                if (_dragPlayhead || _dragEvent >= 0)
                {
                    _dragPlayhead = false; _dragEvent = -1;
                    ResyncPlayer();
                    ev.Use();
                }
                break;
        }
    }

    static float Snap(float t) => Mathf.Round(t * 100f) / 100f;

    // ── 이벤트 목록 ───────────────────────────────────────────────────
    void DrawEventList(ActionFxSheet.Entry a)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(250));
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 원샷")) AddEvent(a, ActionFxSheet.Kind.OneShot);
        if (GUILayout.Button("+ 스팬")) AddEvent(a, ActionFxSheet.Kind.Span);
        if (GUILayout.Button("+ 마커")) AddEvent(a, ActionFxSheet.Kind.Marker);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(Selected == null))
        {
            if (GUILayout.Button("복제"))
            {
                Undo.RecordObject(_sheet, "이벤트 복제");
                a.events.Insert(_selected + 1, Selected.Clone());
                _selected++; Dirty(); ResyncPlayer();
            }
            if (GUILayout.Button("삭제"))
            {
                Undo.RecordObject(_sheet, "이벤트 삭제");
                a.events.RemoveAt(_selected);
                _selected = Mathf.Min(_selected, a.events.Count - 1);
                Dirty(); ResyncPlayer();
            }
            if (GUILayout.Button("▲", GUILayout.Width(26)) && _selected > 0)
            {
                Undo.RecordObject(_sheet, "이벤트 순서");
                var tmp = a.events[_selected]; a.events[_selected] = a.events[_selected - 1]; a.events[_selected - 1] = tmp;
                _selected--; Dirty(); ResyncPlayer();
            }
            if (GUILayout.Button("▼", GUILayout.Width(26)) && _selected < a.events.Count - 1)
            {
                Undo.RecordObject(_sheet, "이벤트 순서");
                var tmp = a.events[_selected]; a.events[_selected] = a.events[_selected + 1]; a.events[_selected + 1] = tmp;
                _selected++; Dirty(); ResyncPlayer();
            }
        }
        EditorGUILayout.EndHorizontal();

        _scrollEvents = EditorGUILayout.BeginScrollView(_scrollEvents);
        for (int i = 0; i < a.events.Count; i++)
        {
            var e = a.events[i];
            if (e == null) continue;
            var rect = EditorGUILayout.GetControlRect(false, 18f, GUILayout.ExpandWidth(true));
            if (i == _selected) EditorGUI.DrawRect(rect, new Color(0.24f, 0.37f, 0.58f, 0.8f));
            EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 4f, 10f, 10f), KindColor(e));
            string span = e.IsSpan ? "–" + e.endTime.ToString("0.00") : "";
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height), $"{e.time:0.00}{span}  {e.name}");
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selected = i; Event.current.Use(); Repaint(); SceneView.RepaintAll();
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void AddEvent(ActionFxSheet.Entry a, ActionFxSheet.Kind kind)
    {
        Undo.RecordObject(_sheet, "이벤트 추가");
        var e = new ActionFxSheet.FxEvent
        {
            name = kind == ActionFxSheet.Kind.Span ? "새 스팬" : kind == ActionFxSheet.Kind.Marker ? "새 마커" : "새 원샷",
            kind = kind,
            time = Snap(_time),
            endTime = Snap(_time + 0.3f),
            anchor = kind == ActionFxSheet.Kind.Marker ? ActionFxSheet.Anchor.Root : ActionFxSheet.Anchor.Socket,
            socket = "Root",
        };
        a.events.Add(e);
        _selected = a.events.Count - 1;
        Dirty();
        ResyncPlayer();
    }

    // ── 인스펙터 ──────────────────────────────────────────────────────
    void DrawInspector(ActionFxSheet.Entry a)
    {
        EditorGUILayout.BeginVertical();
        _scrollInspector = EditorGUILayout.BeginScrollView(_scrollInspector);
        var e = Selected;
        if (e == null)
        {
            EditorGUILayout.HelpBox("타임라인이나 목록에서 이벤트를 고른다. 씬 뷰에서는 고른 이벤트의 자리를 핸들로 끌 수 있다.", MessageType.None);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.BeginChangeCheck();
        string name = EditorGUILayout.TextField("이름", e.name);
        var kind = (ActionFxSheet.Kind)EditorGUILayout.EnumPopup("종류", e.kind);
        float time = EditorGUILayout.FloatField("시작(초)", e.time);
        float end = kind == ActionFxSheet.Kind.Span ? EditorGUILayout.FloatField("끝(초)", e.endTime) : e.endTime;
        string marker = EditorGUILayout.TextField(new GUIContent("이름표(marker)", "코드가 읽는 시각. impact = 피해 판정, end = 동작 끝"), e.marker);

        string fxId = e.fxId; var anchor = e.anchor; string socket = e.socket;
        Vector3 off = e.offset, eul = e.euler; float sc = e.scale; bool follow = e.follow;
        if (kind != ActionFxSheet.Kind.Marker)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("효과", EditorStyles.boldLabel);
            fxId = IdField(e);
            anchor = (ActionFxSheet.Anchor)EditorGUILayout.EnumPopup(new GUIContent("기준", "Root = 발밑 · Socket = 본/소켓 · Target = 코드가 넘긴 지점(충돌점)"), e.anchor);
            if (anchor == ActionFxSheet.Anchor.Socket) socket = SocketField(e);
            off = EditorGUILayout.Vector3Field("오프셋 (기준 로컬)", e.offset);
            eul = EditorGUILayout.Vector3Field("회전", e.euler);
            sc = EditorGUILayout.FloatField("크기 배율", e.scale);
            if (anchor != ActionFxSheet.Anchor.Target) follow = EditorGUILayout.Toggle(new GUIContent("따라가기", "사는 동안 기준을 따라다닌다"), e.follow);
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("소리 · 흔들림 · 히트스톱 (플레이 중에만)", EditorStyles.boldLabel);
        string sfx = EditorGUILayout.TextField(new GUIContent("소리 키", "Resources/SFX 의 키(ProtoAudio)"), e.sfx);
        var shake = (ActionFxSheet.Shake)EditorGUILayout.EnumPopup("흔들림", e.shake);
        float hs = EditorGUILayout.FloatField(new GUIContent("히트스톱(초)", "0.03~0.06 권장. 0 = 없음"), e.hitstop);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(_sheet, "이벤트 편집");
            e.name = name; e.kind = kind; e.time = Mathf.Max(0f, time); e.endTime = Mathf.Max(e.time, end); e.marker = marker;
            e.fxId = fxId; e.anchor = anchor; e.socket = socket; e.offset = off; e.euler = eul; e.scale = sc; e.follow = follow;
            e.sfx = sfx; e.shake = shake; e.hitstop = Mathf.Max(0f, hs);
            Dirty();
            ResyncPlayer();
        }

        if (kind != ActionFxSheet.Kind.Marker) DrawFxStatus(e);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    /// 효과 — 프리팹 칸(놓으면 그 프리팹의 카탈로그 항목을 걸고, 없으면 만든다) + ID 텍스트 + 드롭다운(카탈로그 ID 와 코드 그림 이름).
    string IdField(ActionFxSheet.FxEvent e)
    {
        var cur = FxCatalogEdit.PrefabOf(e.fxId);
        EditorGUI.BeginChangeCheck();
        var np = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("프리팹", "효과 프리팹을 끌어다 놓는다. 카탈로그에 그 프리팹의 항목이 없으면 프리팹 이름으로 만든다. 비우면 효과 없음"),
            cur, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && np != cur)
        {
            string id = np == null ? "" : FxCatalogEdit.EnsureEntryForPrefab(np);
            BuildCatalogList();
            if (Stage != null) Stage.Reload();
            SetId(e, id);
            return e.fxId;
        }
        EditorGUILayout.BeginHorizontal();
        string v = EditorGUILayout.TextField(new GUIContent("효과 ID", "카탈로그 ID 또는 코드 그림 이름. 프리팹을 놓으면 저절로 채워진다 — 코드 그림을 쓸 때만 ▾ 로 고른다"), e.fxId);
        if (EditorGUILayout.DropdownButton(new GUIContent("▾"), FocusType.Passive, GUILayout.Width(22f)))
            FxCatalogEdit.ShowIdMenu(e.fxId, id => SetId(e, id));
        EditorGUILayout.EndHorizontal();
        return v;
    }

    void SetId(ActionFxSheet.FxEvent e, string id)
    {
        Undo.RecordObject(_sheet, "효과 ID");
        e.fxId = id; Dirty(); ResyncPlayer(); Repaint();
    }

    /// 소켓 이름 — 텍스트 + 드롭다운(미리보기 모델의 본 이름).
    string SocketField(ActionFxSheet.FxEvent e)
    {
        EditorGUILayout.BeginHorizontal();
        string v = EditorGUILayout.TextField(new GUIContent("소켓 / 본", "Root 또는 본 이름. 보스 망치 머리 = HammerHead"), e.socket);
        if (EditorGUILayout.DropdownButton(new GUIContent("▾"), FocusType.Passive, GUILayout.Width(22f)))
        {
            CurrentModel(out var model, out var player);
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Root"), e.socket == "Root" || string.IsNullOrEmpty(e.socket), () => SetSocket(e, "Root"));
            if (player != null)
                foreach (var s in player.sockets.Where(s => s != null && !string.IsNullOrEmpty(s.name)))
                {
                    string n = s.name;
                    menu.AddItem(new GUIContent("소켓/" + n), n == e.socket, () => SetSocket(e, n));
                }
            if (model != null)
            {
                if (_socketNamesFor != model)
                {
                    _socketNamesFor = model;
                    _socketNames = model.GetComponentsInChildren<Transform>(true).Select(t => t.name).Distinct().OrderBy(n => n).ToArray();
                }
                foreach (var n in _socketNames)
                {
                    string nn = n;
                    menu.AddItem(new GUIContent("본/" + nn), nn == e.socket, () => SetSocket(e, nn));
                }
            }
            else menu.AddDisabledItem(new GUIContent("(미리보기를 배치하거나 무대에 올리면 본 목록이 뜬다)"));
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();
        return v;
    }

    void SetSocket(ActionFxSheet.FxEvent e, string name)
    {
        Undo.RecordObject(_sheet, "소켓");
        e.socket = name; Dirty(); ResyncPlayer(); Repaint();
    }

    void DrawFxStatus(ActionFxSheet.FxEvent e)
    {
        var cat = Catalog;
        var entry = cat != null ? cat.Find(e.fxId) : null;
        string msg; MessageType type = MessageType.None;
        if (string.IsNullOrEmpty(e.fxId)) { msg = "효과 ID 가 비어 있다 — 소리·흔들림만 난다."; }
        else if (entry == null)
        {
            if (ProtoFx.HasBuiltin(e.fxId)) msg = "카탈로그에 없음 — 코드 그림 이름으로 바로 쓴다(무대·플레이 모드에서만 보인다).";
            else { msg = "카탈로그에도 코드 그림에도 없는 ID — 아무것도 안 나온다."; type = MessageType.Warning; }
        }
        else if (entry.prefab != null) msg = $"프리팹: {entry.prefab.name} — 에디터에서도 보인다. 수명 {(entry.lifetime > 0f ? entry.lifetime + "s" : "파티클이 끝날 때")}.";
        else if (ProtoFx.HasBuiltin(entry.builtin)) msg = $"코드 그림: {entry.builtin} — 무대·플레이 모드에서만 보인다. 카탈로그에 프리팹을 넣으면 그걸로 바뀐다.";
        else { msg = "카탈로그 항목에 프리팹도 코드 그림도 없다."; type = MessageType.Warning; }
        EditorGUILayout.HelpBox(msg, type);
        EditorGUILayout.BeginHorizontal();
        if (!string.IsNullOrEmpty(e.fxId) && GUILayout.Button("카탈로그 탭에서 보기"))
        {
            int i = _cat.FindIndex(x => x.id == e.fxId);
            if (i >= 0) { _tab = 1; SelectCatalog(i); }
        }
        if (cat != null && GUILayout.Button("카탈로그 에셋")) Selection.activeObject = cat;
        EditorGUILayout.EndHorizontal();
    }

    // ── 카탈로그 탭 ───────────────────────────────────────────────────
    void BuildCatalogList()
    {
        _cat.Clear();
        var cat = Catalog;
        var seen = new HashSet<string>();
        if (cat != null)
            foreach (var e in cat.entries)
                if (e != null && !string.IsNullOrEmpty(e.id) && seen.Add(e.id))
                    _cat.Add(new CatItem { id = e.id, entry = e });
        // 카탈로그에 아직 없는 코드 그림 — 그대로 이름으로 부를 수 있으니 같이 보여 준다(쇼케이스 목록과 같은 구성)
        foreach (var n in ProtoFx.BuiltinNames.OrderBy(x => x))
            if (seen.Add(n))
                _cat.Add(new CatItem { id = n, builtinOnly = true, entry = new FxCatalog.Entry { id = n, builtin = n, lifetime = 1.5f } });
        _catIndex = Mathf.Clamp(_catIndex, -1, _cat.Count - 1);
    }

    static Color CatColor(CatItem it)
    {
        if (it.entry.prefab != null) return new Color(0.55f, 0.85f, 0.55f);
        if (!string.IsNullOrEmpty(it.entry.builtin) && ProtoFx.HasBuiltin(it.entry.builtin)) return new Color(0.6f, 0.6f, 0.6f);
        return new Color(0.95f, 0.4f, 0.35f);
    }

    void DrawCatalogTab(FxShowcase st)
    {
        if (_cat.Count == 0) BuildCatalogList();
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        _catFilter = EditorGUILayout.TextField(_catFilter, EditorStyles.toolbarSearchField);
        if (GUILayout.Button(new GUIContent("+ 빈 항목", "ID 만 있는 항목 — 코드 그림 이름을 적거나 프리팹을 나중에 넣는다"), EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            var ne = FxCatalogEdit.AddEmpty();
            BuildCatalogList();
            if (ne != null) _catIndex = _cat.FindIndex(x => x.entry == ne);
            if (st != null) st.Reload();
        }
        if (GUILayout.Button(new GUIContent("다시 읽기", "카탈로그 항목이 바뀌었으면"), EditorStyles.toolbarButton, GUILayout.Width(64)))
        {
            BuildCatalogList();
            if (st != null) st.Reload();
        }
        EditorGUILayout.EndHorizontal();

        // 프리팹을 놓으면 그 프리팹의 항목을 찾거나 만들어 고른다 — ID 를 손으로 적지 않는 길
        EditorGUI.BeginChangeCheck();
        var dropped = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("프리팹 → 항목", "여기 효과 프리팹을 끌어다 놓으면 그 프리팹의 항목(없으면 프리팹 이름으로 새로)을 골라 바로 재생한다"),
            null, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck() && dropped != null)
        {
            string id = FxCatalogEdit.EnsureEntryForPrefab(dropped);
            BuildCatalogList();
            if (st != null) st.Reload();
            int i = _cat.FindIndex(x => x.id == id);
            if (i >= 0) SelectCatalog(i);
        }
        if (st == null)
            EditorGUILayout.HelpBox("항목을 고르면 바로 재생한다. 에디터에서는 프리팹 항목만 보인다(미리보기 모델이 있으면 그 자리에, 없으면 씬 뷰 중심에). " +
                                    "코드 그림까지 보려면 [▶ 무대].", MessageType.None);

        EditorGUILayout.BeginHorizontal();
        // 목록
        EditorGUILayout.BeginVertical(GUILayout.Width(260));
        _scrollCat = EditorGUILayout.BeginScrollView(_scrollCat);
        string f = string.IsNullOrEmpty(_catFilter) ? null : _catFilter.Trim().ToLowerInvariant();
        for (int i = 0; i < _cat.Count; i++)
        {
            var it = _cat[i];
            if (f != null && !it.id.ToLowerInvariant().Contains(f)) continue;
            var rect = EditorGUILayout.GetControlRect(false, 18f, GUILayout.ExpandWidth(true));
            bool cur = i == _catIndex;
            if (cur) EditorGUI.DrawRect(rect, new Color(0.24f, 0.37f, 0.58f, 0.8f));
            EditorGUI.DrawRect(new Rect(rect.x + 2f, rect.y + 4f, 10f, 10f), CatColor(it));
            string tag = it.builtinOnly ? "  (카탈로그 밖)" : it.IsSpan ? "  (스팬)" : "";
            GUI.Label(new Rect(rect.x + 16f, rect.y, rect.width - 16f, rect.height), it.id + tag, cur ? EditorStyles.whiteLabel : EditorStyles.label);
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                SelectCatalog(i); Event.current.Use();
            }
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 항목
        EditorGUILayout.BeginVertical();
        DrawCatalogEntry(st);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    void DrawCatalogEntry(FxShowcase st)
    {
        var it = _catIndex >= 0 && _catIndex < _cat.Count ? _cat[_catIndex] : null;
        if (it == null)
        {
            EditorGUILayout.HelpBox("왼쪽 목록에서 항목을 고르거나, 위 칸에 프리팹을 끌어다 놓는다. 초록 = 프리팹 · 회색 = 코드 그림 · 빨강 = 비어 있음.", MessageType.None);
            return;
        }
        var cat = Catalog;
        var e = it.entry;

        if (it.builtinOnly)
        {
            EditorGUILayout.LabelField(it.id, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("카탈로그에 없는 코드 그림 — ID 를 그대로 코드 그림 이름으로 쓴다. [카탈로그·시트 만들기] 가 알고 있는 항목이면 그걸로 채워진다.", MessageType.None);
        }
        else if (cat != null)
        {
            // ID — 바꾸면 이 ID 를 부르는 시트 이벤트·무기 슬롯·코드는 따라오지 않는다
            EditorGUILayout.BeginHorizontal();
            string nid = EditorGUILayout.DelayedTextField(new GUIContent("ID", "코드·시트·무기 슬롯이 부르는 이름. 바꾸면 부르는 쪽도 같이 고쳐야 한다"), e.id);
            if (nid != e.id && !FxCatalogEdit.Rename(e, nid)) Debug.LogWarning($"[FX 카탈로그] ID '{nid}' 는 비었거나 이미 있다");
            if (GUILayout.Button(new GUIContent("− 삭제", "이 항목을 지운다(되돌리기 가능)"), GUILayout.Width(54)) &&
                EditorUtility.DisplayDialog("카탈로그 항목 삭제", $"'{e.id}' 를 지울까? 이 ID 를 부르는 이벤트·슬롯은 아무것도 안 내게 된다.", "지운다", "취소"))
            {
                StopCatalogPreview();
                FxCatalogEdit.Remove(e);
                BuildCatalogList();
                _catIndex = -1;
                if (st != null) st.Reload();
                EditorGUILayout.EndHorizontal();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            var prefab = (GameObject)EditorGUILayout.ObjectField(new GUIContent("프리팹", "있으면 이걸로 그린다. 없으면 코드 그림"), e.prefab, typeof(GameObject), false);
            string builtin = EditorGUILayout.TextField(new GUIContent("코드 그림", "프리팹이 없을 때의 폴백 이름(ProtoFxBuiltins)"), e.builtin);
            EditorGUILayout.BeginHorizontal();
            float life = EditorGUILayout.FloatField(new GUIContent("수명(초)", "0 = 스팬(코드가 켜고 끈다)"), e.lifetime);
            using (new EditorGUI.DisabledScope(prefab == null))
                if (GUILayout.Button(new GUIContent("다시 재기", "프리팹 파티클에서 한 번 재생 길이를 다시 잰다(지연 + 마지막 버스트 + 입자 수명, 빈 껍데기 시스템은 뺀다)"), GUILayout.Width(64)))
                    life = FxCatalogEdit.EstimateLifetime(prefab);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            int pool =EditorGUILayout.IntField(new GUIContent("풀", "미리 만들어 둘 수"), e.poolSize);
            int max = EditorGUILayout.IntField(new GUIContent("동시 상한", "0 = 제한 없음. 넘치면 오래된 것부터 회수"), e.maxAlive);
            EditorGUILayout.EndHorizontal();
            var clock = (FxCatalog.Clock)EditorGUILayout.EnumPopup(new GUIContent("시계", "게임 시간(히트스톱·슬로모션을 탄다) / 실시간"), e.clock);
            bool onTop = EditorGUILayout.Toggle(new GUIContent("항상 위에", "총·적 몸에 가려지지 않게 오버레이 카메라(FxOverlay 레이어, 깊이 지움)가 따로 그린다 — 총구 화염·피격 불똥. 바닥 효과·연기·피는 끈다. 프리팹 효과만, 플레이 중에만"), e.onTop);
            var off = EditorGUILayout.Vector3Field(new GUIContent("쇼케이스 오프셋", "더미 기준 자리 — 피격 불똥은 가슴(0, 0.95, 0), 바닥 효과는 발"), e.previewOffset);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(cat, "카탈로그 항목");
                bool rebuild = prefab != e.prefab;
                if (rebuild && prefab != null && e.prefab == null && !string.IsNullOrEmpty(e.builtin))
                    life = FxCatalogEdit.EstimateLifetime(prefab);   // 코드 그림 자리에 프리팹을 처음 넣을 때 — 수명을 파티클에서 잰다
                e.prefab = prefab; e.builtin = builtin; e.lifetime = life; e.poolSize = pool; e.maxAlive = max; e.clock = clock; e.previewOffset = off; e.onTop = onTop;
                EditorUtility.SetDirty(cat);
                if (rebuild) { ProtoFx.StopAll(); ProtoFx.Clear(); }   // 풀에 남은 옛 프리팹 인스턴스를 버린다
                if (st != null) { st.Select(it.id); _syncedLabel = it.id; } else PlayCatalogPreview();
            }
            if (e.prefab != null) DrawPrefabSize(e, it.id, st);
            if (!string.IsNullOrEmpty(e.note)) EditorGUILayout.HelpBox(e.note, MessageType.None);
        }

        string msg; MessageType type = MessageType.None;
        if (e.prefab != null) msg = $"프리팹 {e.prefab.name} — 에디터에서도 보인다.";
        else if (!string.IsNullOrEmpty(e.builtin) && ProtoFx.HasBuiltin(e.builtin)) msg = $"코드 그림 {e.builtin} — 무대·플레이 모드에서만 보인다. 프리팹을 넣으면 그걸로 바뀐다.";
        else if (!string.IsNullOrEmpty(e.builtin)) { msg = $"코드 그림 '{e.builtin}' 이 등록돼 있지 않다(ProtoFxBuiltins)."; type = MessageType.Warning; }
        else { msg = "프리팹도 코드 그림도 없다 — 아무것도 안 나온다."; type = MessageType.Warning; }
        EditorGUILayout.HelpBox(msg, type);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("▶ 재생", st != null ? "무대에서 다시 (Space)" : "창 자체 미리보기")))
        {
            if (st != null) { st.Select(it.id); _syncedLabel = it.id; } else PlayCatalogPreview();
        }
        using (new EditorGUI.DisabledScope(st != null || _catHandle == null))
            if (GUILayout.Button("■ 멈춤")) StopCatalogPreview();
        if (cat != null && GUILayout.Button("카탈로그 에셋")) Selection.activeObject = cat;
        EditorGUILayout.EndHorizontal();

        if (st == null)
        {
            EditorGUILayout.BeginHorizontal();
            _radius = EditorGUILayout.Slider(new GUIContent("반경", "코드 그림이 링 크기로 쓰는 판정 반경 — 창 미리보기용(무대는 무대 줄의 값)"), _radius, 0.5f, 8f);
            _big = GUILayout.Toggle(_big, "큰 한 방", GUILayout.Width(64));
            EditorGUILayout.EndHorizontal();
        }
    }

    void SelectCatalog(int i)
    {
        if (i < 0 || i >= _cat.Count) return;
        _catIndex = i;
        var it = _cat[i];
        var st = Stage;
        if (st != null)
        {
            if (!st.Select(it.id)) { st.Reload(); st.Select(it.id); }
            _syncedLabel = it.id;
        }
        else PlayCatalogPreview();
        Repaint();
    }

    /// 창 자체 미리보기 — 리그 모델이 있으면 그 자리(쇼케이스 오프셋), 없으면 씬 뷰 중심 바닥.
    void PlayCatalogPreview()
    {
        StopCatalogPreview();
        var it = _catIndex >= 0 && _catIndex < _cat.Count ? _cat[_catIndex] : null;
        if (it == null) return;
        var model = Model(FindRig());
        FxContext ctx;
        if (model != null) { ctx = FxContext.On(model.transform, true); ctx.offset = it.entry.previewOffset; }
        else
        {
            var sv = SceneView.lastActiveSceneView;
            var p = sv != null ? sv.pivot : Vector3.zero;
            p.y = 0f;
            ctx = FxContext.At(p + it.entry.previewOffset);
        }
        ctx.radius = _radius; ctx.big = _big; ctx.scale = 1f;
        _catHandle = ProtoFx.Play(it.id, ctx, it.IsSpan);
        _catUntil = it.IsSpan ? EditorApplication.timeSinceStartup + 1.6 : 0.0;
        SceneView.RepaintAll();
    }

    void StopCatalogPreview()
    {
        if (_catHandle != null) { ProtoFx.Stop(_catHandle); _catHandle = null; }
        _catUntil = 0.0;
    }

    /// 크기 — 효과 자체(프리팹 루트 배율)를 바꾼다. 이 효과를 쓰는 모든 무기 · 이벤트에 먹는다(11-13 사용자 "FX 툴에 사이즈 수정 기능").
    /// 한 무기의 한 자리만이면 [무기] 탭 슬롯의 '크기' 슬라이더(ctx.scale). ProtoFx 는 낼 때마다 루트 배율을 읽어 다음 발부터 바로 반영된다.
    void DrawPrefabSize(FxCatalog.Entry e, string id, FxShowcase st)
    {
        var root = e.prefab.transform;
        Vector3 s = root.localScale;
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        float v = EditorGUILayout.DelayedFloatField(new GUIContent("크기 (프리팹 루트)",
            $"프리팹 {e.prefab.name} 의 루트 배율을 바로 고쳐 저장한다 — 이 효과를 쓰는 모든 무기 · 이벤트가 같이 커진다. " +
            "한 무기만 바꾸려면 [무기] 탭 슬롯의 '크기'. 그 프리팹을 프리팹 모드로 열어 두었으면 닫고 고칠 것(그쪽 저장이 덮어쓴다). Ctrl+Z 로 되돌리면 저장(Ctrl+S)."), s.x);
        bool typed = EditorGUI.EndChangeCheck();
        float mul = 1f;
        if (GUILayout.Button("×0.8", EditorStyles.miniButtonLeft, GUILayout.Width(40))) mul = 0.8f;
        if (GUILayout.Button("×1.25", EditorStyles.miniButtonMid, GUILayout.Width(44))) mul = 1.25f;
        if (GUILayout.Button("×1.5", EditorStyles.miniButtonMid, GUILayout.Width(40))) mul = 1.5f;
        if (GUILayout.Button("×2", EditorStyles.miniButtonRight, GUILayout.Width(32))) mul = 2f;
        EditorGUILayout.EndHorizontal();
        if (!typed && mul == 1f) return;
        Vector3 ns = mul != 1f ? s * mul : (s.x > 0.0001f ? s * (v / s.x) : Vector3.one * v);
        if (ns.x <= 0.0001f || ns == s) return;
        FxCatalogEdit.SetPrefabRootScale(e.prefab, ns);
        if (st != null) { st.Select(id); _syncedLabel = id; } else PlayCatalogPreview();
        Repaint();
    }

    // ── 무기 탭 ───────────────────────────────────────────────────────
    // 캐릭터와 무기를 고르면 무대(쇼케이스)에 캐릭터가 서서 더미를 향해 쏜다(FxWeaponStage). 연출 세트(WFX_*)는 여기서 바로 고친다.
    // 에디트 모드에선 세울 수 없다 — 탄피·예광탄·코드 그림이 전부 플레이 중에만 돌고, 무기도 소켓이 Awake 에서 물린다.

    void BuildWeaponLists()
    {
        _chars.Clear(); _weapons.Clear();
        foreach (var guid in AssetDatabase.FindAssets("Char_ t:Prefab", new[] { CharFolder }))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            string n = System.IO.Path.GetFileNameWithoutExtension(p);
            if (!n.StartsWith("Char_") || n.Contains("_OLD")) continue;   // 이름 규약 Char_01_Rookie … (_OLD 는 보관본)
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go != null) _chars.Add(go);
        }
        _chars.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (var guid in AssetDatabase.FindAssets("t:WeaponModelDef"))
        {
            var w = AssetDatabase.LoadAssetAtPath<WeaponModelDef>(AssetDatabase.GUIDToAssetPath(guid));
            if (w == null || w.name.EndsWith("_v1")) continue;   // _v1 = 옛 모델 보관본
            _weapons.Add(w);
        }
        _weapons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        _charLabels = _chars.Select(c => c.name).ToArray();
        _weaponLabels = new[] { "(캐릭터 기본 무기)" }.Concat(_weapons.Select(WeaponLabel)).ToArray();
        if (_chars.Count > 0 && CharPrefab == null) _charPath = AssetDatabase.GetAssetPath(_chars[0]);
    }

    static string WeaponLabel(WeaponModelDef w) => string.IsNullOrEmpty(w.displayName) ? w.name : $"{w.displayName}  ({w.name})";
    GameObject CharPrefab => string.IsNullOrEmpty(_charPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(_charPath);
    WeaponModelDef PickedWeapon => string.IsNullOrEmpty(_weaponPath) ? null : AssetDatabase.LoadAssetAtPath<WeaponModelDef>(_weaponPath);

    /// 캐릭터 프리팹이 드는 기본 무기(소켓의 weapon).
    static WeaponModelDef DefaultWeaponOf(GameObject charPrefab)
    {
        if (charPrefab == null) return null;
        var s = charPrefab.GetComponentInChildren<WeaponSocket>(true);
        return s != null ? s.weapon : null;
    }

    /// 무대에 세울 무기 — 고른 것, 없으면 캐릭터 기본.
    WeaponModelDef EffectiveWeapon => PickedWeapon != null ? PickedWeapon : DefaultWeaponOf(CharPrefab);

    void StartWeaponOnStage(FxShowcase st)
    {
        if (st == null) return;
        st.BeginWeapon(CharPrefab, PickedWeapon);
        _syncedLabel = null;
        Repaint();
    }

    void DrawWeaponTab(FxShowcase st)
    {
        if (_chars.Count == 0 && _weapons.Count == 0) BuildWeaponLists();
        var ws = st != null ? st.WeaponStage : null;
        bool onStage = st != null && st.WeaponMode;

        // 캐릭터 · 무기 고르기
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUI.BeginChangeCheck();
        int ci = Mathf.Max(0, _chars.IndexOf(CharPrefab));
        ci = EditorGUILayout.Popup(ci, _charLabels, EditorStyles.toolbarPopup, GUILayout.Width(170));
        int wi = PickedWeapon != null ? _weapons.IndexOf(PickedWeapon) + 1 : 0;
        wi = EditorGUILayout.Popup(wi, _weaponLabels, EditorStyles.toolbarPopup, GUILayout.MinWidth(220));
        if (EditorGUI.EndChangeCheck())
        {
            _charPath = ci >= 0 && ci < _chars.Count ? AssetDatabase.GetAssetPath(_chars[ci]) : "";
            _weaponPath = wi > 0 && wi - 1 < _weapons.Count ? AssetDatabase.GetAssetPath(_weapons[wi - 1]) : "";
            _fxSo = null; _statsSo = null;
            if (onStage) StartWeaponOnStage(st);   // 무대 위면 바로 갈아 끼운다
        }
        if (GUILayout.Button(new GUIContent("다시 읽기", "캐릭터·무기 에셋 목록을 다시 찾는다"), EditorStyles.toolbarButton, GUILayout.Width(64))) BuildWeaponLists();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        var charPrefab = CharPrefab;
        var weapon = EffectiveWeapon;
        var stats = weapon != null ? weapon.stats : null;
        if (charPrefab == null)
        {
            EditorGUILayout.HelpBox($"캐릭터 프리팹이 없다 — {CharFolder} 아래 Char_* 프리팹을 찾는다.", MessageType.Warning);
            return;
        }

        // 한 줄 요약
        string wname = weapon == null ? "무기 없음" : (string.IsNullOrEmpty(weapon.displayName) ? weapon.name : weapon.displayName);
        string summary = $"{charPrefab.name} · {wname}";
        if (stats != null)
            summary += $" · {stats.mode}{(stats.beam ? "(광선)" : "")} · 간격 {stats.interval:0.00}s · 사거리 {stats.range:0.#}m" +
                       (stats.aoeRadius > 0.01f ? $" · 폭발 {stats.aoeRadius:0.#}m" : "") +
                       $" · 연출 세트 {(stats.fx != null ? stats.fx.name : "(기본)")}";
        else if (weapon != null) summary += " · 수치 에셋 없음";
        if (weapon != null && weapon.mount != WeaponMount.Hand) summary += " · 손에 들지 않는 장비";
        EditorGUILayout.HelpBox(summary, MessageType.None);

        _scrollWeapon = EditorGUILayout.BeginScrollView(_scrollWeapon);

        // 무대 조종
        if (st == null)
        {
            EditorGUILayout.HelpBox("무대를 켜면 이 캐릭터가 무기를 들고 더미 좀비를 향해 연사한다 — 총구·탄피·예광탄·착탄·처치·폭발이 게임과 같은 경로로 나온다. " +
                                    "아래 연출 세트를 고치면 다음 발부터 바로 반영된다.", MessageType.Info);
            if (!Application.isPlaying &&
                GUILayout.Button(new GUIContent("▶ 무대에서 쏘기", "쇼케이스 씬을 열고(없으면 굽고) 플레이한 뒤 이 캐릭터·무기를 세운다"), GUILayout.Height(28)))
                FxShowcaseSceneBuilder.OpenAndPlay();
        }
        else
        {
            bool same = onStage && ws.CharacterPrefab == charPrefab && ws.Weapon == weapon;
            if (!same && GUILayout.Button(new GUIContent(onStage ? "▶ 이 캐릭터·무기로 바꾸기" : "▶ 무대에 세우기", "쇼케이스 무대에 캐릭터를 세우고 더미를 향해 쏜다"), GUILayout.Height(28)))
                StartWeaponOnStage(st);
            if (onStage) DrawWeaponStageControls(st, ws);
        }

        if (stats != null) DrawFxSetInspector(stats);
        else if (weapon != null) EditorGUILayout.HelpBox($"{weapon.name} 에 수치 에셋(stats)이 없다 — 연출 세트는 WeaponStats.fx 에 물린다.", MessageType.Warning);
        if (stats != null) DrawTracerFields(stats);
        EditorGUILayout.EndScrollView();
    }

    /// 무대 위 캐릭터를 조종하는 줄 — 연사 · 한 발 · 공속 · 치명타 · 소리 · 플래시 · 더미 체력, 그리고 세트 자리 하나씩 재생.
    void DrawWeaponStageControls(FxShowcase st, FxWeaponStage ws)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        using (new EditorGUI.DisabledScope(!ws.CanFire))
        {
            ws.autoFire = GUILayout.Toggle(ws.autoFire, new GUIContent(ws.autoFire ? "■ 연사 중" : "▶ 연사", "더미를 향해 계속 쏜다 (F)"), EditorStyles.miniButton, GUILayout.Width(70));
            if (GUILayout.Button(new GUIContent("한 발", "(Space)"), GUILayout.Width(44))) ws.FireOnce();
        }
        GUILayout.Label(new GUIContent("공속", "1 = 수치 에셋의 간격 그대로"), GUILayout.Width(28));
        ws.fireRateMult = EditorGUILayout.Slider(ws.fireRateMult, 0.25f, 4f, GUILayout.MinWidth(90));
        GUILayout.Label(new GUIContent("치명타", "확률 — 불똥 ×1.5 · 치명타 소리 · 약한 흔들림"), GUILayout.Width(40));
        ws.critChance = EditorGUILayout.Slider(ws.critChance, 0f, 1f, GUILayout.MinWidth(80));
        ws.sound = GUILayout.Toggle(ws.sound, new GUIContent("소리", "발사음 · 탄착음 · 사망음"), EditorStyles.miniButton, GUILayout.Width(40));
        ws.dummyFlash = GUILayout.Toggle(ws.dummyFlash, new GUIContent("플래시", "더미 흰 피격 플래시"), EditorStyles.miniButton, GUILayout.Width(48));
        ws.airstrike = GUILayout.Toggle(ws.airstrike, new GUIContent("폭격", "융단 폭격 로켓의 주기 폭격도 부른다"), EditorStyles.miniButton, GUILayout.Width(40));
        GUILayout.Label(new GUIContent("더미 체력", "이만큼 피해를 주면 처치 효과가 나고 잠깐 사라진다"), GUILayout.Width(56));
        ws.dummyHp = Mathf.Max(1f, EditorGUILayout.FloatField(ws.dummyHp, GUILayout.Width(44)));
        EditorGUILayout.EndHorizontal();

        // 발사 때 맞추기 — ProtoFireSync(게임과 같은 규칙). 정적 값이라 플레이 중 바로 먹고, 리로드하면 기본값으로 돌아온다
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        ProtoFireSync.Enabled = GUILayout.Toggle(ProtoFireSync.Enabled, new GUIContent("애니 동기화", "켜면 사격 자세 · 사격 상태(Shoot Rifle)에 들어온 뒤 총구 화염 · 총알 · 소리를 낸다. 끄면 트리거 순간에(전 방식, 비교용)"), EditorStyles.miniButton, GUILayout.Width(74));
        using (new EditorGUI.DisabledScope(!ProtoFireSync.Enabled))
        {
            GUILayout.Label(new GUIContent("발사 프레임", "사격 클립의 정규화 시각(0~1). 이 시각을 지나야 낸다. 0 = 상태 전환(0.08s)이 끝나자마자(클립 90ms). 소총 클립 Firing_Rifle_edit(1.167s)의 반동은 267~367ms = 0.23~0.31. 대기 상한 300ms 때문에 0.26 위는 더 늦지 않는다. 맞는 값을 찾으면 ProtoFireSync.FireAt 기본값에 적는다"), GUILayout.Width(64));
            ProtoFireSync.FireAt = EditorGUILayout.Slider(ProtoFireSync.FireAt, 0f, 0.9f, GUILayout.MinWidth(120));
        }
        GUILayout.Label($"지난 발 지연 {ws.LastShotDelay * 1000f:0}ms (상한 {ProtoFireSync.MaxWait * 1000f:0}ms)", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(ws.LastShotAnim))
            EditorGUILayout.LabelField(new GUIContent("지난 발 애니  " + ws.LastShotAnim, "총구 화염 · 총알 · 소리를 낸 순간 사격 클립(Shoot Rifle)이 어디였나. 반동과 맞는지 이 숫자로 본다"), EditorStyles.miniLabel);

        // 처치 파편(ZombieGibs, 코드 그림) — 정적 값이라 플레이 중 바로 먹고, 리로드하면 기본값으로. 맞는 값을 찾으면 ZombieGibs 의 기본값에 적는다
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        ZombieGibs.Enabled = GUILayout.Toggle(ZombieGibs.Enabled, new GUIContent("파편", "처치 때 더미(좀비 모델)가 본 단위로 조각나 튄다 + 피 터짐(zombie_gore). 끄면 처치 효과만"), EditorStyles.miniButton, GUILayout.Width(40));
        using (new EditorGUI.DisabledScope(!ZombieGibs.Enabled))
        {
            GUILayout.Label(new GUIContent("크기", "조각 한 변의 목표(m) — 이보다 긴 조각은 반으로 가른다. 바꾸면 다음 처치부터"), GUILayout.Width(28));
            float piece = EditorGUILayout.Slider(ZombieGibs.PieceSize, 0.06f, 0.5f, GUILayout.MinWidth(90));
            GUILayout.Label(new GUIContent("최대", "몸 하나의 조각 상한. 바꾸면 다음 처치부터"), GUILayout.Width(28));
            int pieces = EditorGUILayout.IntSlider(ZombieGibs.MaxPieces, 4, 60, GUILayout.MinWidth(90));
            GUILayout.Label(new GUIContent("두께", "조각 두께(cm) — 안쪽 껍질을 들여놓고 옆면을 붙여 덩어리로. 0 이면 껍질 그대로(종이처럼 보인다). 바꾸면 다음 처치부터"), GUILayout.Width(28));
            float thick = EditorGUILayout.Slider(ZombieGibs.Thickness * 100f, 0f, 15f, GUILayout.MinWidth(80)) / 100f;
            if (!Mathf.Approximately(piece, ZombieGibs.PieceSize) || pieces != ZombieGibs.MaxPieces || !Mathf.Approximately(thick, ZombieGibs.Thickness))
            { ZombieGibs.PieceSize = piece; ZombieGibs.MaxPieces = pieces; ZombieGibs.Thickness = thick; ZombieGibs.InvalidateCache(); }
            GUILayout.Label(new GUIContent("디졸브", "녹아 사라지는 시간(초). 디졸브 머티리얼이 없으면 이 시간 동안 줄어든다"), GUILayout.Width(40));
            ZombieGibs.DissolveTime = EditorGUILayout.Slider(ZombieGibs.DissolveTime, 0.2f, 2f, GUILayout.MinWidth(90));
        }
        GUILayout.Label(ZombieGibs.Dissolving ? $"조각 {ZombieGibs.Alive}개" : $"조각 {ZombieGibs.Alive}개 · 디졸브 머티리얼 없음", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        using (new EditorGUI.DisabledScope(!ZombieGibs.Enabled))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(44);
            GUILayout.Label(new GUIContent("세기", "튀는 속도(m/s)"), GUILayout.Width(28));
            ZombieGibs.Power = EditorGUILayout.Slider(ZombieGibs.Power, 1f, 10f, GUILayout.MinWidth(90));
            GUILayout.Label(new GUIContent("수명", "눕기까지 포함한 수명(초). 마지막 디졸브 시간 동안 녹아 사라진다"), GUILayout.Width(28));
            ZombieGibs.Life = EditorGUILayout.Slider(ZombieGibs.Life, 0.4f, 4f, GUILayout.MinWidth(90));
            GUILayout.Label(new GUIContent("중력", "과장된 중력(m/s²)"), GUILayout.Width(28));
            ZombieGibs.Gravity = EditorGUILayout.Slider(ZombieGibs.Gravity, 4f, 30f, GUILayout.MinWidth(90));
            GUILayout.Label(new GUIContent("회전", "회전 속도(도/초). 느려야 무겁다"), GUILayout.Width(28));
            ZombieGibs.Spin = EditorGUILayout.Slider(ZombieGibs.Spin, 0f, 720f, GUILayout.MinWidth(80));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(44);
            ZombieGibs.LandingBlood = GUILayout.Toggle(ZombieGibs.LandingBlood, new GUIContent("착지 피", "조각이 바닥에 닿는 자리에 핏자국 + 핏방울. 튀어서 다른 자리에 누우면 작은 핏자국 하나 더"), GUILayout.Width(60));
            using (new EditorGUI.DisabledScope(!ZombieGibs.LandingBlood))
            {
                GUILayout.Label(new GUIContent("크기", "핏자국 크기 배수(기본 = 조각 반지름의 약 3배)"), GUILayout.Width(28));
                ZombieGibs.LandingSplatScale = EditorGUILayout.Slider(ZombieGibs.LandingSplatScale, 0.3f, 3f, GUILayout.MinWidth(90));
                GUILayout.Label(new GUIContent("유지", "조각이 녹은 뒤 핏자국이 더 남는 시간(초). 마지막 1/4 동안 옅어진다"), GUILayout.Width(28));
                ZombieGibs.LandingBloodLinger = EditorGUILayout.Slider(ZombieGibs.LandingBloodLinger, 0f, 5f, GUILayout.MinWidth(90));
            }
            if (ZombieGibs.LandingBlood && !ZombieGibs.Bleeding) GUILayout.Label("착지 피 머티리얼 없음 (Resources/VFX/M_GibBlood — 포커스 한 번 더)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();

        DrawShotTimeline(ws);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"발사 {ws.Shots} · 명중 {ws.Hits} · 빗나감 {ws.Misses} · 처치 {ws.Kills} · 더미 체력 {ws.DummyHpNow:0}/{ws.dummyHp:0}", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(new GUIContent("슬롯 재생", "세트의 자리 하나만 낸다 — 총구는 총구에서, 피격·처치는 더미에, 폭발은 더미 발밑에(반경 = 무기 aoe, 없으면 무대 줄의 반경)"), GUILayout.Width(56));
        if (GUILayout.Button("총구 ▶", GUILayout.Width(56))) ws.PlaySlot(FxWeaponStage.SlotKind.Muzzle, st.radius, st.big);
        if (GUILayout.Button("피격 ▶", GUILayout.Width(56))) ws.PlaySlot(FxWeaponStage.SlotKind.Hit, st.radius, st.big);
        if (GUILayout.Button("처치 ▶", GUILayout.Width(56))) ws.PlaySlot(FxWeaponStage.SlotKind.Kill, st.radius, st.big);
        if (GUILayout.Button("폭발 ▶", GUILayout.Width(56))) ws.PlaySlot(FxWeaponStage.SlotKind.Explode, st.radius, st.big);
        EditorGUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(ws.Status)) EditorGUILayout.HelpBox(ws.Status, MessageType.Warning);
    }

    /// 한 발 타임라인 — [● 한 발 녹화] 로 한 발을 기록한 뒤 슬라이더로 시각을 옮겨 포즈 · 총구 화염 · 총알 · 착탄 · 처치를 본다.
    /// 눈금 바에 트리거(0) · 발사 · 착탄 · 처치 시각을 표시한다. 코드 그림(탄피 · 광선 · 기본 총구)은 되감지 못해 안 보인다.
    void DrawShotTimeline(FxWeaponStage ws)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label(new GUIContent("타임라인", "한 발을 녹화한 뒤 슬라이더로 시각을 옮겨 본다. 포즈 · 몸 틀기 · 프리팹 효과 · 총알 · 더미 플래시가 그 시각으로 간다"), EditorStyles.boldLabel, GUILayout.Width(52));
        if (!ws.Scrubbing)
        {
            using (new EditorGUI.DisabledScope(!ws.CanFire || ws.Recording))
                if (GUILayout.Button(new GUIContent(ws.Recording ? "● 녹화 중…" : "● 한 발 녹화", "연사를 끄고 한 발을 쏘며 기록한다. 끝나면 무대가 멈추고 슬라이더가 열린다"), GUILayout.Width(92))) ws.BeginRecord();
            GUILayout.Label(ws.Recording ? $"쏘는 중 — {ws.recordSeconds:0.0}초 뒤 슬라이더가 열린다" : "한 발을 녹화해 되감아 본다 (탄피 · 광선 같은 코드 그림은 안 보인다)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(new GUIContent("길이", "녹화 길이(초)"), GUILayout.Width(26));
            ws.recordSeconds = EditorGUILayout.FloatField(ws.recordSeconds, GUILayout.Width(36));
        }
        else
        {
            const float Step = 1f / 60f;
            if (GUILayout.Button(new GUIContent("|◀", "처음(트리거)"), GUILayout.Width(30))) ws.SetScrub(0f);
            if (GUILayout.Button(new GUIContent("◀", "1/60초 뒤로"), GUILayout.Width(26))) ws.SetScrub(ws.ScrubTime - Step);
            EditorGUI.BeginChangeCheck();
            float t = EditorGUILayout.Slider(ws.ScrubTime, 0f, ws.ScrubLength, GUILayout.MinWidth(160));
            if (EditorGUI.EndChangeCheck()) ws.SetScrub(t);
            if (GUILayout.Button(new GUIContent("▶", "1/60초 앞으로"), GUILayout.Width(26))) ws.SetScrub(ws.ScrubTime + Step);
            using (new EditorGUI.DisabledScope(ws.RecFireAt < 0f))
                if (GUILayout.Button(new GUIContent("발사", "총구 화염 · 총알이 나간 시각으로"), GUILayout.Width(40))) ws.SetScrub(ws.RecFireAt);
            using (new EditorGUI.DisabledScope(ws.RecHitAt < 0f))
                if (GUILayout.Button(new GUIContent("착탄", "더미에 맞은 시각으로"), GUILayout.Width(40))) ws.SetScrub(ws.RecHitAt);
            if (GUILayout.Button(new GUIContent("■ 나가기", "되감기를 끝내고 살아 있는 무대로"), GUILayout.Width(64))) ws.EndScrub();
        }
        EditorGUILayout.EndHorizontal();
        if (!ws.Scrubbing) return;

        // 눈금 바 — 트리거 · 발사 · 착탄 · 처치 자리와 플레이헤드
        var rect = EditorGUILayout.GetControlRect(false, 22f, GUILayout.ExpandWidth(true));
        var bar = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, 6f);
        EditorGUI.DrawRect(bar, new Color(0.2f, 0.2f, 0.2f));
        float len = Mathf.Max(0.01f, ws.ScrubLength);
        void Mark(float at, Color c, string label)
        {
            if (at < 0f) return;
            float x = bar.x + Mathf.Clamp01(at / len) * bar.width;
            EditorGUI.DrawRect(new Rect(x - 1f, bar.y - 2f, 2f, bar.height + 4f), c);
            GUI.Label(new Rect(x - 20f, bar.yMax + 1f, 60f, 12f), $"{label} {at * 1000f:0}", EditorStyles.miniLabel);
        }
        Mark(0f, Color.gray, "트리거");
        Mark(ws.RecFireAt, new Color(1f, 0.82f, 0.3f), "발사");
        Mark(ws.RecHitAt, new Color(1f, 0.45f, 0.2f), "착탄");
        Mark(ws.RecKillAt, new Color(0.9f, 0.3f, 0.3f), "처치");
        float px = bar.x + Mathf.Clamp01(ws.ScrubTime / len) * bar.width;
        EditorGUI.DrawRect(new Rect(px - 1f, bar.y - 4f, 2f, bar.height + 8f), new Color(1f, 0.25f, 0.25f));

        string info = $"시각 {ws.ScrubTime * 1000f:0}ms / {ws.ScrubLength * 1000f:0}ms · 발사 {(ws.RecFireAt >= 0f ? $"+{ws.RecFireAt * 1000f:0}ms" : "—")} · 착탄 {(ws.RecHitAt >= 0f ? $"+{ws.RecHitAt * 1000f:0}ms" : "—")} · 처치 {(ws.RecKillAt >= 0f ? $"+{ws.RecKillAt * 1000f:0}ms" : "—")}";
        if (ws.RecCodeOnly > 0) info += $" · 코드 그림 {ws.RecCodeOnly}개는 되감기에 안 보임";
        EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
        if (!string.IsNullOrEmpty(ws.RecFireAnim)) EditorGUILayout.LabelField("발사 순간 애니  " + ws.RecFireAnim, EditorStyles.miniLabel);
    }

    /// 무기 연출 세트(WFX_*) 인스펙터 — 슬롯 드로어 그대로(첫 줄 프리팹 칸). 고치면 다음 발부터 반영된다(세트는 쏠 때마다 읽는다).
    void DrawFxSetInspector(WeaponStats stats)
    {
        EditorGUILayout.Space(6);
        if (stats.fx == null)
        {
            EditorGUILayout.HelpBox($"{stats.name} 에 연출 세트가 없다 — 기본 세트(공통 효과, 배율 1)로 쏜다.", MessageType.Info);
            if (GUILayout.Button("연출 에셋 만들기 (빠진 무기만)")) { FxAssetsSetup.CreateWeaponFx(); _fxSo = null; }
            return;
        }
        if (_fxSo == null || _fxSo.targetObject != stats.fx) _fxSo = new SerializedObject(stats.fx);
        _fxSo.Update();
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"연출 세트 — {stats.fx.name}", EditorStyles.boldLabel);
        if (GUILayout.Button("에셋 선택", GUILayout.Width(70))) Selection.activeObject = stats.fx;
        EditorGUILayout.EndHorizontal();
        var it = _fxSo.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            if (it.name == "m_Script") continue;
            EditorGUILayout.PropertyField(it, true);
        }
        if (_fxSo.ApplyModifiedProperties()) BuildCatalogList();   // 프리팹을 놓으면 카탈로그 항목이 생겼을 수 있다
        EditorGUILayout.HelpBox("슬롯 첫 줄에 프리팹을 놓으면 ID 가 저절로 들어간다(카탈로그에 항목이 없으면 프리팹 이름으로 만든다 — 총구 · 피격 칸이면 '항상 위에'를 켠 채로). 비우면 그 자리는 꺼진다. " +
                                "'크기' 슬라이더 = 이 무기의 이 자리만(1 = 소총 · 잡좀비 기준). '항상 위에' · 효과 자체 크기(프리팹 루트)는 카탈로그 항목 값이라 그 효과를 쓰는 모든 무기에 먹는다(카탈로그 탭). 고치면 다음 발부터 반영.", MessageType.None);
    }

    /// 예광탄·발사체 — 수치 에셋(WS_*)에 있지만 보이는 것이라 여기서 같이 만진다. 다른 밸런스 값은 건드리지 않는다.
    void DrawTracerFields(WeaponStats stats)
    {
        EditorGUILayout.Space(4);
        _tracerFold = EditorGUILayout.Foldout(_tracerFold, $"예광탄 · 발사체 (수치 에셋 {stats.name})", true);
        if (!_tracerFold) return;
        if (_statsSo == null || _statsSo.targetObject != stats) _statsSo = new SerializedObject(stats);
        _statsSo.Update();
        foreach (var n in new[] { "projectileSize", "projectileModel", "projectileFacesTravel", "tracerColor", "tracerLength", "tracerWidth" })
        {
            var p = _statsSo.FindProperty(n);
            if (p != null) EditorGUILayout.PropertyField(p, true);
        }
        _statsSo.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("기본 예광탄(발사체 모델 없음)의 색·꼬리 길이·굵기. 로켓처럼 모델이 있으면 색·길이는 안 쓰인다. 광선(슬러그 레일)은 tracerColor 만.", MessageType.None);
    }

    // ── 씬 뷰 ────────────────────────────────────────────────────────
    /// 지금 액션을 보여 주는 모델과 플레이어 — 무대가 연결됐으면 무대 것, 아니면 창 리그 것.
    void CurrentModel(out GameObject model, out ActionFxPlayer player)
    {
        var st = Stage;
        if (st != null)
        {
            bool on = OnStage(st, Action);
            model = on ? st.Model : null;
            player = on ? st.Player : null;
            return;
        }
        model = Model(FindRig());
        player = Player;
    }

    void OnSceneGUI(SceneView sv)
    {
        if (!_showGizmos) return;
        var a = Action;
        CurrentModel(out var model, out var player);
        if (a == null || model == null || player == null) return;
        var st = Stage;
        if (st == null)
        {
            if (!player.Playing || player.Current != a) BeginAction();
            player.SetTarget(TargetWorld());
        }
        Vector3 tp = st != null ? st.ActionTarget() : TargetWorld();
        float s = model.transform.lossyScale.x;

        // Target 원 — 테두리가 판정 반경(코드 그림이 링 크기로 쓴다)
        Handles.color = new Color(1f, 0.3f, 0.15f, 0.9f);
        Handles.DrawWireDisc(tp, Vector3.up, a.previewRadius * s);
        Handles.Label(tp + Vector3.up * 0.1f, "Target");

        var sel = Selected;
        for (int i = 0; i < a.events.Count; i++)
        {
            var e = a.events[i];
            if (e == null || e.kind == ActionFxSheet.Kind.Marker) continue;
            var ctx = player.BuildContext(e);
            Vector3 p = ctx.WorldPosition;
            bool active = _time >= e.time && _time <= e.EndOrTime + 0.05f;
            Color c = KindColor(e);
            if (!active) c.a = 0.35f;
            Handles.color = e == sel ? Color.white : c;
            float size = HandleUtility.GetHandleSize(p) * 0.08f;
            Handles.SphereHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
            Handles.Label(p + Vector3.up * size * 1.6f, e.name);
        }

        if (sel == null) return;
        if (sel.anchor == ActionFxSheet.Anchor.Target)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 np = Handles.PositionHandle(tp, model.transform.rotation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_sheet, "Target 이동");
                a.previewTarget = model.transform.InverseTransformPoint(np);
                Dirty(); Repaint();
            }
        }
        else
        {
            var ctx = player.BuildContext(sel);
            Vector3 wp = ctx.WorldPosition;
            Quaternion wr = ctx.anchor != null ? ctx.anchor.rotation : Quaternion.identity;
            EditorGUI.BeginChangeCheck();
            Vector3 np = Handles.PositionHandle(wp, wr);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_sheet, "오프셋 이동");
                sel.offset = ctx.anchor != null ? ctx.anchor.InverseTransformPoint(np) : sel.offset + (np - wp);
                Dirty(); Repaint();
            }
        }
    }
}
