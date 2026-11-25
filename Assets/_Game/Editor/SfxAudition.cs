using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// 효과음 뱅크를 <b>플레이 모드에 들어가지 않고</b> 들어보는 창.
    /// 메뉴: <c>좀비 오버런 / 효과음 들어보기</c>
    ///
    /// <para><b>왜 필요한가.</b> 파형은 스크립트가 만들고(<c>synth.py</c>) 일부는 생성 모델이
    /// 만든다(<c>hf_post.py</c>). 둘 다 숫자로는 맞아도 귀에 맞는지는 들어봐야 알고, 그
    /// 확인을 위해 매번 판을 시작해 좀비를 만나야 한다면 아무도 확인하지 않게 된다.</para>
    ///
    /// <para>여기서 키별로 묶어 보여주는 이유는 <see cref="ProtoAudio"/> 가 파일 이름의
    /// <c>_숫자</c> 를 떼어 변형으로 묶기 때문이다. 이 창에서 한 줄로 보이는 것이
    /// 인게임에서 하나의 키다 — 변형이 제대로 묶였는지도 같이 확인된다.</para>
    /// </summary>
    public class SfxAudition : EditorWindow
    {
        const string ResourcePath = "SFX";

        class Group
        {
            public string key;
            public List<AudioClip> clips = new List<AudioClip>();
        }

        List<Group> _groups;
        Vector2 _scroll;
        string _filter = "";
        float _volume = 0.8f;
        string _playing;

        [MenuItem("좀비 오버런/효과음 들어보기", false, 200)]
        static void Open() => GetWindow<SfxAudition>("효과음").minSize = new Vector2(420, 320);

        void OnEnable() { Reload(); }

        void Reload()
        {
            var clips = Resources.LoadAll<AudioClip>(ResourcePath);
            var map = new Dictionary<string, Group>();
            foreach (var c in clips)
            {
                string key = StripVariant(c.name);
                if (!map.TryGetValue(key, out var g)) map[key] = g = new Group { key = key };
                g.clips.Add(c);
            }
            foreach (var g in map.Values)
                g.clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _groups = map.Values.OrderBy(g => g.key, StringComparer.Ordinal).ToList();
        }

        /// <see cref="ProtoAudio"/> 와 **같은 규칙**이어야 한다. 다르면 이 창에서 들은 묶음과
        /// 인게임 묶음이 어긋나고, 그 차이는 눈에 보이지 않는다.
        static string StripVariant(string name)
        {
            int u = name.LastIndexOf('_');
            if (u <= 0 || u == name.Length - 1) return name;
            for (int i = u + 1; i < name.Length; i++)
                if (name[i] < '0' || name[i] > '9') return name;
            return name.Substring(0, u);
        }

        void OnGUI()
        {
            if (_groups == null) Reload();

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    Reload();
                if (GUILayout.Button("정지", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    StopPreview();
                GUILayout.Space(8);
                GUILayout.Label("음량", GUILayout.Width(30));
                _volume = GUILayout.HorizontalSlider(_volume, 0f, 1f, GUILayout.Width(70));
                GUILayout.FlexibleSpace();
                _filter = GUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(120));
            }

            if (_groups.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Assets/_Game/Resources/SFX 에 클립이 없다.\n"
                    + "synth.py / hf_post.py 를 돌린 뒤 Assets 를 새로 고쳐라.",
                    MessageType.Warning);
                return;
            }

            int total = _groups.Sum(g => g.clips.Count);
            EditorGUILayout.LabelField($"키 {_groups.Count}종 · 클립 {total}개", EditorStyles.miniLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var g in _groups)
            {
                if (!string.IsNullOrEmpty(_filter) &&
                    g.key.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isPlaying = _playing != null && _playing.StartsWith(g.key + "_")
                                     || _playing == g.key;
                    var style = isPlaying ? EditorStyles.boldLabel : EditorStyles.label;
                    EditorGUILayout.LabelField(g.key, style, GUILayout.Width(120));

                    // 변형이 여러 개면 번호 버튼을 나란히 둔다. 어느 변형이 튀는지
                    // 하나씩 들어봐야 고칠 수 있다.
                    for (int i = 0; i < g.clips.Count; i++)
                    {
                        var c = g.clips[i];
                        string label = g.clips.Count == 1 ? "▶" : $"▶{i + 1}";
                        if (GUILayout.Button(label, GUILayout.Width(g.clips.Count == 1 ? 34 : 30)))
                            PlayPreview(c);
                    }

                    GUILayout.FlexibleSpace();
                    var first = g.clips[0];
                    EditorGUILayout.LabelField(
                        $"{first.length * 1000f:F0}ms", EditorStyles.miniLabel, GUILayout.Width(52));
                    if (GUILayout.Button("선택", EditorStyles.miniButton, GUILayout.Width(40)))
                        Selection.activeObject = first;
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "인게임 상대 음량은 파형에 구워져 있다(synth.py 의 목표 피크 표). "
                + "여기 음량 슬라이더는 미리듣기 전용이라 게임에 영향을 주지 않는다.",
                MessageType.None);
        }

        // ── 에디터 미리듣기 ──────────────────────────────────────────────
        // UnityEditor.AudioUtil 은 공개 API 가 아니라 리플렉션으로 부른다. 대안이 없다:
        // 플레이 모드 밖에서는 AudioSource 가 재생되지 않는다.
        // 메서드 이름이 버전마다 바뀌었으므로(2020.2 에서 PlayClip → PlayPreviewClip)
        // 후보를 순서대로 찾는다. 못 찾으면 창이 죽지 않고 경고만 남긴다.
        static MethodInfo _play, _stop;
        static bool _probed;

        static void ProbeAudioUtil()
        {
            if (_probed) return;
            _probed = true;
            var type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (type == null) return;

            foreach (var name in new[] { "PlayPreviewClip", "PlayClip" })
            {
                _play = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public,
                                       null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                     ?? type.GetMethod(name, BindingFlags.Static | BindingFlags.Public,
                                       null, new[] { typeof(AudioClip) }, null);
                if (_play != null) break;
            }
            foreach (var name in new[] { "StopAllPreviewClips", "StopAllClips" })
            {
                _stop = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
                if (_stop != null) break;
            }
        }

        void PlayPreview(AudioClip clip)
        {
            ProbeAudioUtil();
            if (_play == null)
            {
                Debug.LogWarning("[효과음] 이 Unity 버전에서 AudioUtil 미리듣기를 찾지 못했다. "
                               + "프로젝트 창에서 클립을 골라 인스펙터 하단의 재생 버튼을 쓰면 된다.");
                return;
            }
            StopPreview();
            _playing = clip.name;
            var ps = _play.GetParameters();
            _play.Invoke(null, ps.Length == 3 ? new object[] { clip, 0, false } : new object[] { clip });
            Repaint();
        }

        void StopPreview()
        {
            ProbeAudioUtil();
            _stop?.Invoke(null, null);
            _playing = null;
        }

        void OnDisable() => StopPreview();
    }
}
