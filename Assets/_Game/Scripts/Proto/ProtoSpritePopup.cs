using UnityEngine;

namespace Proto
{
    /// 스프라이트 시트 애니메이션 팝업 (스테이지 클리어) — <b>시계·소리·시트만</b> 쥔다.
    /// 그리는 것은 ProtoHUD(uGUI RawImage)다. 예전엔 IMGUI 라 여기서 GUI.DrawTexture 로 직접 그렸다.
    /// 시트는 Resources/SpriteAnims 에 두고 이름으로 부른다(빌드에 포함되려면 Resources 여야 한다).
    /// 지금은 캐릭터 1종(초밥소녀) 시트만 있다. 캐릭터별 시트가 생기면 이름 규칙 <종류>_<캐릭터>.png 로 늘린다.
    public static class ProtoSpritePopup
    {
        public class Sheet
        {
            public Texture2D tex;
            public int cols, rows, count;
            public float fps;
            public float Aspect => tex == null ? 1f : (tex.width / (float)cols) / (tex.height / (float)rows);

            /// frame 칸의 UV. 시트는 왼쪽 위부터 가로로 읽는다. UV 는 아래가 0 이라 행을 뒤집는다.
            public Rect UV(int frame)
            {
                frame = Mathf.Clamp(frame, 0, count - 1);
                int c = frame % cols, row = frame / cols;
                return new Rect(c / (float)cols, 1f - (row + 1) / (float)rows, 1f / cols, 1f / rows);
            }
        }

        static Sheet Load(string name, int cols, int rows, int count, float fps)
        {
            var t = Resources.Load<Texture2D>("SpriteAnims/" + name);
            if (t == null) Debug.LogWarning($"[SpritePopup] Resources/SpriteAnims/{name} 없음");
            return new Sheet { tex = t, cols = cols, rows = rows, count = count, fps = fps };
        }

        /// 시트 옆 <이름>.json(make_cutin_sheet.py 가 만든다)이 있으면 칸 수·프레임 수·fps 를 거기서 읽는다.
        static Sheet LoadAuto(string name, int cols, int rows, int count, float fps)
        {
            var j = Resources.Load<TextAsset>("SpriteAnims/" + name);
            if (j != null)
            {
                var m = JsonUtility.FromJson<SheetMeta>(j.text);
                if (m != null && m.count > 0) { cols = m.cols; rows = m.rows; count = m.count; fps = m.fps; }
            }
            return Load(name, cols, rows, count, fps);
        }

        [System.Serializable] class SheetMeta { public int cols, rows, count; public float fps; }

        // 필사기 컷인(cutin_sushi_girl)은 2026-10-29 사용자 요청으로 뺐다(시트·소리 파일도 삭제). 지금은 클리어만.
        static Sheet _clear;
        public static Sheet Clear => _clear ??= LoadAuto("clear_sushi_girl", 3, 3, 9, 8f);

        static AudioSource _voice;
        static AudioClip _clearClip;
        static bool _clearLoaded;

        /// 클리어 소리 전용 소스. 효과음 풀(ProtoAudio)은 보이스 상한이 있어 연사 중에 긴 소리가 밀려 끊긴다.
        static AudioSource Voice
        {
            get
            {
                if (_voice == null)
                {
                    var go = new GameObject("PopupVoice");
                    Object.DontDestroyOnLoad(go);
                    _voice = go.AddComponent<AudioSource>();
                    _voice.playOnAwake = false;
                    _voice.spatialBlend = 0f;
                    _voice.ignoreListenerPause = true;
                }
                return _voice;
            }
        }

        static void PlayVoice(AudioClip clip)
        {
            var v = Voice;
            v.Stop();
            v.clip = clip;
            v.volume = ProtoSettings.SfxVolume;
            v.time = 0f;
            v.Play();
        }

        public static void Stop()
        {
            _clearStart = -1f;
            if (_voice != null) _voice.Stop();
        }

        // ── 스테이지 클리어 ─────────────────────────────────────────────
        // 원본 영상(9.9초) 8fps 시트 + 영상 소리. 소리는 결과 화면이 뜰 때 **한 번**, 그 한 바퀴는 소리 위치로 싱크.
        // 소리가 끝난 뒤에는 애니만 소리 없이 계속 돈다(결과 화면에 머무는 시간은 정해져 있지 않다).
        static float _clearStart = -1f;

        static AudioClip ClearClip
        {
            get
            {
                if (!_clearLoaded) { _clearClip = Resources.Load<AudioClip>("SpriteAnims/clear_sushi_girl"); _clearLoaded = true; }
                return _clearClip;
            }
        }

        /// 생존 성공 순간(ProtoRun.EndRun)에 부른다.
        public static void PlayClear()
        {
            _ = Clear;
            _clearStart = Time.unscaledTime;
            var clip = ClearClip;
            if (clip != null) PlayVoice(clip);
        }

        /// <summary>
        /// 결과 화면(생존 성공)의 클리어 애니 상태. since = 결과 화면이 뜬 실시간(PlayClear 를 안 거쳤을 때의 대체 시작점).
        /// pop = 크기 배율(0.3초 동안 0.6 → 살짝 튀며 1), alpha = 처음 0.12초 페이드 인.
        /// </summary>
        public static bool TryGetClear(float since, out int frame, out float pop, out float alpha)
        {
            frame = 0; pop = 1f; alpha = 1f;
            var s = Clear;
            if (s.tex == null) return false;
            if (_clearStart >= 0f) since = _clearStart;
            float t = Time.unscaledTime - since;
            // 첫 바퀴는 소리 위치가 시계다
            float clock = (_voice != null && _voice.isPlaying && _voice.clip == _clearClip && _clearClip != null) ? _voice.time : t;
            pop = Mathf.Lerp(0.6f, 1f, t < 0.3f ? EaseOutBack(t / 0.3f) : 1f);
            alpha = Mathf.Clamp01(t / 0.12f);
            frame = Mathf.FloorToInt(clock * s.fps) % s.count;
            return true;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
