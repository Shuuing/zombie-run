using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 무기를 캐릭터 손에 눈으로 맞추고, 그 배치를 <see cref="WeaponModelDef"/> 에 굽는 툴.
///
/// <para><b>왜 툴이 필요한가.</b> 그립 지점은 계산으로 안 나온다. 지오메트리로 "아래로 뻗은 덩어리"를
/// 손잡이로 짚었다가 틀린 적이 있고(엉뚱한 덩어리였다), 손목이 꺾인 채 쥐는 각도는 축 정렬로도 안 나온다.
/// 사람이 씬에서 한 번 맞추는 게 유일하게 맞는 방법이라, 남은 문제는 <b>그 배치를 숫자로 되돌려
/// 에셋에 남기는 것</b>뿐이다. 이 툴이 그 왕복을 담당한다.</para>
///
/// <para><b>쓰는 법.</b> 무기를 고르고 [씬에 셋업] → 씬 뷰에서 <b>무기 오브젝트만</b> 이동·회전
/// (크기는 이 창의 배율 슬라이더로) → [에셋에 저장] → [정리].</para>
///
/// <para><b>총구.</b> 셋업하면 무기 안에 빨간 구슬(총구마커)이 생긴다. 총알·화염·미사일이 나가는 점이다.
/// 총구 구멍에 맞게 옮기고 [에셋에 저장] 하면 <see cref="WeaponModelDef.muzzlePoint"/> 에 같이 들어간다.
/// 저장값이 없으면 총열 끝 단면의 중심(<see cref="MuzzleFinder"/>)에서 시작한다.</para>
/// </summary>
public class WeaponGripTool : EditorWindow
{
    const string RigName = "__무기그립리그__";
    const string DefaultCharacter = "Assets/_Game/Art/character/an/Prefabs/SushiGirl_Rifle.prefab";
    const string MarkerName = "총구마커";

    WeaponModelDef _def;
    GameObject _characterPrefab;
    float _scale = 0.090277f;
    Vector2 _scroll;

    [MenuItem("Tools/좀비 오버런/무기 그립 맞추기")]
    static void Open() => GetWindow<WeaponGripTool>("무기 그립");

    void OnEnable()
    {
        if (_characterPrefab == null)
            _characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultCharacter);
    }

    // 씬에 떠 있는 리그를 매번 찾아온다. 창이 리그를 필드로 들고 있으면 씬을 다시 열었을 때 죽은
    // 참조가 남아 "셋업했는데 아무 일도 안 일어난다"가 된다.
    static GameObject FindRig()
    {
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            if (go.name == RigName && go.transform.parent == null) return go;
        return null;
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        EditorGUILayout.LabelField("무기 목록", EditorStyles.boldLabel);
        var defs = AssetDatabase.FindAssets("t:WeaponModelDef")
            .Select(g => AssetDatabase.LoadAssetAtPath<WeaponModelDef>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null)
            .OrderBy(d => d.name)
            .ToList();

        if (defs.Count == 0)
            EditorGUILayout.HelpBox("WeaponModelDef 에셋이 없습니다.", MessageType.Warning);

        foreach (var d in defs)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool sel = _def == d;
                // 아직 눈으로 안 맞춘 무기를 한눈에 구분할 수 있어야 한다.
                // 손에 쥐지 않는 무기는 그립이라는 개념 자체가 없으므로 ●/○ 대신 '—' 로 둔다.
                var mark = d.mount != WeaponMount.Hand ? "— " : (d.calibrated ? "● " : "○ ");
                var label = mark + d.name + (string.IsNullOrEmpty(d.displayName) ? "" : "  (" + d.displayName + ")");
                if (GUILayout.Toggle(sel, label, "Button") && !sel) Select(d);
                if (GUILayout.Button("에셋", GUILayout.Width(44))) Selection.activeObject = d;
            }
        }
        EditorGUILayout.LabelField("● 씬에서 맞춰 저장한 값 · ○ 아직 기본값 · — 손에 쥐지 않는 무기", EditorStyles.miniLabel);

        EditorGUILayout.Space(8);
        _characterPrefab = (GameObject)EditorGUILayout.ObjectField("캐릭터", _characterPrefab, typeof(GameObject), false);

        var rig = FindRig();

        if (_def != null && _def.mount != WeaponMount.Hand)
            EditorGUILayout.HelpBox(
                _def.name + " 은 " + _def.mount + " 무기다 — 손 소켓에 물리지 않으므로 맞출 그립이 없다.\n" +
                "위치는 런타임(궤도·추종)이 정한다.", MessageType.None);

        using (new EditorGUI.DisabledScope(_def == null || _def.model == null || _characterPrefab == null
                                           || _def.mount != WeaponMount.Hand))
        {
            if (GUILayout.Button(rig == null ? "씬에 셋업" : "씬에 다시 셋업", GUILayout.Height(26))) Setup();
        }

        if (rig != null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("크기", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _scale = EditorGUILayout.Slider("무기 배율", _scale, 0.02f, 0.3f);
            if (EditorGUI.EndChangeCheck()) ApplyScale(rig);

            var weaponT = FindWeapon(rig);
            if (weaponT != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.LabelField("현재 배치 (무기 로컬)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("위치", weaponT.localPosition.ToString("F4"));
                EditorGUILayout.LabelField("회전", weaponT.localEulerAngles.ToString("F3"));
                if (GUILayout.Button("무기 선택 (씬 뷰에서 옮기기)")) Selection.activeGameObject = weaponT.gameObject;

                var marker = weaponT.Find(MarkerName);
                if (marker != null)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("총구 (무기 로컬)", marker.localPosition.ToString("F3"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("총구 마커 선택")) Selection.activeGameObject = marker.gameObject;
                        if (GUILayout.Button("총구 자동 계산")) PlaceMarker(weaponT, AutoMuzzle(weaponT));
                    }
                }
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(_def == null || weaponT == null))
                if (GUILayout.Button("에셋에 저장", GUILayout.Height(26))) Save(rig);

            if (GUILayout.Button("정리 (리그 삭제)")) Undo.DestroyObjectImmediate(rig);
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "무기 오브젝트만 이동·회전하세요. 소켓이나 캐릭터를 옮기면 저장값이 어긋납니다.\n" +
            "크기는 슬라이더로만 바꾸세요 — 무기 루트 스케일에는 FBX 에 구워진 무기별 비율이 들어 있습니다.",
            MessageType.Info);

        EditorGUILayout.EndScrollView();
    }

    void Select(WeaponModelDef d)
    {
        _def = d;
        _scale = d.weaponScale;
    }

    static Transform FindWeapon(GameObject rig)
    {
        var s = DeepFind(rig.transform, "그립소켓");
        return s != null && s.childCount > 0 ? s.GetChild(0) : null;
    }

    static Transform DeepFind(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = DeepFind(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    void Setup()
    {
        var old = FindRig();
        if (old != null) Undo.DestroyObjectImmediate(old);

        var rig = new GameObject(RigName);
        Undo.RegisterCreatedObjectUndo(rig, "무기 그립 리그");

        var character = (GameObject)PrefabUtility.InstantiatePrefab(_characterPrefab, rig.transform);
        character.name = "캐릭터";

        var hand = FindHand(character);
        if (hand == null)
        {
            Debug.LogError("[무기 그립] 오른손 본을 찾지 못했습니다. 리그가 Humanoid 인지 확인하세요.");
            return;
        }

        // 런타임 WeaponSocket.Awake 와 같은 순서로 만든다. 여기서 순서가 달라지면 씬에서 맞춘 값이
        // 플레이할 때 다른 자리에 나온다.
        var socket = new GameObject("그립소켓");
        socket.transform.SetParent(hand, false);
        socket.transform.localPosition = Vector3.zero;
        socket.transform.localRotation = Quaternion.identity;

        var weapon = (GameObject)PrefabUtility.InstantiatePrefab(_def.model, socket.transform);
        WeaponSocket.ApplyPlacement(weapon.transform, _def.gripPoint, _def.weaponEuler);

        ApplyScale(rig);

        // 자동 계산은 마커를 만들기 전에 — 마커 구슬이 무기 메시로 잡히면 안 된다.
        var muzzle = _def.hasMuzzle ? _def.muzzlePoint : AutoMuzzle(weapon.transform);
        PrefabUtility.UnpackPrefabInstance(weapon, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);   // 자식을 붙이려면 풀어야 한다
        var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        m.name = MarkerName;
        Object.DestroyImmediate(m.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
        m.GetComponent<Renderer>().sharedMaterial = mat;
        m.transform.SetParent(weapon.transform, false);
        PlaceMarker(weapon.transform, muzzle);

        Selection.activeGameObject = weapon;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    /// 총열 끝 단면의 중심. 손(소켓 원점)에서 먼 끝을 고른다.
    static Vector3 AutoMuzzle(Transform weapon)
    {
        var grip = weapon.parent != null ? weapon.InverseTransformPoint(weapon.parent.position) : Vector3.zero;
        return MuzzleFinder.TryFind(weapon, grip, true, out var local) ? local : Vector3.zero;
    }

    /// 마커를 무기 로컬 위치에 두고, 월드에서 지름 3cm 로 보이게 크기를 맞춘다.
    static void PlaceMarker(Transform weapon, Vector3 local)
    {
        var m = weapon.Find(MarkerName);
        if (m == null) return;
        m.localPosition = local;
        m.localRotation = Quaternion.identity;
        float s = weapon.lossyScale.x;
        m.localScale = Vector3.one * (s > 0.0001f ? 0.03f / s : 1f);
    }

    static Transform FindHand(GameObject character)
    {
        var animator = character.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            var b = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (b != null) return b;
        }
        // Humanoid 매핑이 에디터에서 안 잡히는 경우가 있어 이름으로도 찾는다.
        return DeepFind(character.transform, "mixamorig:RightHand");
    }

    void ApplyScale(GameObject rig)
    {
        var socket = DeepFind(rig.transform, "그립소켓");
        if (socket == null) return;
        var animator = socket.GetComponentInParent<Animator>();
        float comp = WeaponSocket.CompensationFor(socket.parent, true, animator != null ? animator.transform : null);
        socket.localScale = Vector3.one * comp * _scale;
    }

    void Save(GameObject rig)
    {
        var socket = DeepFind(rig.transform, "그립소켓");
        var weapon = FindWeapon(rig);
        if (socket == null || weapon == null) return;

        // 무기 루트를 실수로 키웠다면 그 몫을 배율로 접어넣고 그립 좌표를 그만큼 되돌린다.
        // (그러지 않으면 다음 셋업에서 무기가 손에서 빠져나온다.)
        float authored = _def.model.transform.localScale.x;
        float ratio = authored > 0.0001f ? weapon.localScale.x / authored : 1f;

        Vector3 grip, euler;
        WeaponSocket.ReadPlacement(weapon, ratio, out grip, out euler);

        Undo.RecordObject(_def, "무기 그립 저장");
        _def.gripPoint = grip;
        _def.weaponEuler = euler;
        _def.weaponScale = _scale * ratio;
        _def.calibrated = true;
        var marker = weapon.Find(MarkerName);
        if (marker != null) { _def.muzzlePoint = marker.localPosition; _def.hasMuzzle = true; }
        EditorUtility.SetDirty(_def);
        AssetDatabase.SaveAssets();

        // 저장한 값으로 다시 배치해 왕복 오차를 눈으로 확인시킨다. 어긋나 보이면 저장이 틀린 것이다.
        _scale = _def.weaponScale;
        weapon.localScale = Vector3.one * authored;
        WeaponSocket.ApplyPlacement(weapon, _def.gripPoint, _def.weaponEuler);
        ApplyScale(rig);

        PlaceMarker(weapon, _def.muzzlePoint);
        Debug.Log("[무기 그립] " + _def.name + " 저장 — grip=" + grip.ToString("F4") +
                  " euler=" + euler.ToString("F3") + " scale=" + _def.weaponScale.ToString("F6") +
                  " muzzle=" + _def.muzzlePoint.ToString("F3"));
    }
}
