using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이를 끝낼 때 Time Scale 을 1 로 되돌린다.
///
/// <para><b>왜 필요한가.</b> Unity 는 플레이 중에 바뀐 <c>Time.timeScale</c> 을 플레이를 꺼도 되돌리지 않는다 —
/// 프로젝트 설정(Project Settings > Time) 값 자체가 바뀐다. 레벨업 카드 · 결과 화면 · 옵션 창 · 히트스톱이
/// 0 으로 멈춰 둔 순간에 플레이를 끄면 0 이 남고, 그러면 씬 뷰 파티클 미리보기의 Play 버튼과 Playback Speed 가
/// 꺼진다("Play is disabled, because the Time Scale in the Time Manager is set to 0.0."). 그 상태로 프로젝트가
/// 저장되면 <c>ProjectSettings/TimeManager.asset</c> 에 0 으로 남아 에디터를 다시 켜도 그대로다.</para>
///
/// <para>게임 쪽은 <c>ProtoRun.Awake</c> 가 시작할 때 1 로 되돌리므로 플레이에는 영향이 없다. 여기서는
/// 플레이가 끝난 뒤의 에디터 쪽만 맡는다.</para>
/// </summary>
public static class TimeScaleGuard
{
    const string TimeManagerPath = "ProjectSettings/TimeManager.asset";

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        // 에디터를 켰을 때 · 스크립트를 다시 읽었을 때도 한 번 본다 — 이미 0 으로 저장돼 있는 값을 고친다.
        EditorApplication.delayCall += Restore;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) Restore();
    }

    static void Restore()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;   // 플레이 중의 값은 게임이 쥔다
        float was = Time.timeScale;
        if (Mathf.Approximately(was, 1f)) return;

        Time.timeScale = 1f;
        // 바뀐 값이 다음 저장 때 파일에도 적히게 한다 — 안 그러면 에디터를 켤 때마다 옛 값으로 시작한다.
        var assets = AssetDatabase.LoadAllAssetsAtPath(TimeManagerPath);
        if (assets != null && assets.Length > 0 && assets[0] != null) EditorUtility.SetDirty(assets[0]);

        Debug.Log($"[Time Scale] 플레이가 {was:0.##} 로 남겨 둔 Time Scale 을 1 로 되돌렸다.");
    }
}
