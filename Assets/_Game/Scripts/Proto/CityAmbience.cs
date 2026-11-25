using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 도시 배경음 — 가끔 헬기가 지나가고, 멀리서 비명이 들리고, 구급차가 지나간다(사용자 요청, 2026-09-30).
    ///
    /// <para>클립은 <c>Resources/Ambience/</c> 의 <c>{채널}_N.wav</c>(heli_1, scream_2 …). 파일을 추가하면 변형이 는다.
    /// 원본·후처리: <c>ArtSource/SFX/heli/heli_post.py</c>, <c>ArtSource/SFX/city_amb/amb_post.py</c>.
    /// 다가왔다 멀어지는 음량 곡선·고역 감쇠(거리감)는 클립에 이미 구워져 있다.</para>
    /// <para>채널마다 간격을 따로 쥐고, 전체로는 <see cref="minGap"/> 초 안에 두 소리가 연달아 시작하지 않게 한다
    /// (헬기 위에 비명·사이렌이 한꺼번에 얹히면 배경이 아니라 소음이 된다).</para>
    /// <para>ProtoAudio 뱅크(Resources/SFX)에 넣지 않은 이유: 효과음 동시 발음·쿨다운 표에 안 맞고, 긴 원샷에 팬을
    /// 계속 움직여야 해서 채널마다 전용 AudioSource 하나가 단순하다.</para>
    public class CityAmbience : MonoBehaviour
    {
        public enum Pan { Sweep, Fixed }

        [System.Serializable]
        public class Channel
        {
            [Tooltip("클립 이름 앞부분(Resources/Ambience/이름_N).")]
            public string prefix = "heli";
            [Tooltip("다음 재생까지 대기 범위(초, 클립 길이 뒤부터).")]
            public Vector2 interval = new Vector2(35f, 90f);
            [Tooltip("씬 시작 후 첫 재생까지(초).")]
            public Vector2 firstDelay = new Vector2(12f, 25f);
            [Tooltip("음량(효과음 음량 설정에 곱해진다).")]
            [Range(0f, 1f)] public float volume = 0.7f;
            [Tooltip("Sweep = 한쪽→반대쪽으로 지나감(헬기·사이렌), Fixed = 한 방향에서 들림(비명).")]
            public Pan pan = Pan.Sweep;
            [Range(0f, 1f)] public float panWidth = 0.8f;
            [Tooltip("Sweep 일 때 정중앙을 지나는 순간(클립 길이 비율) — 가장 큰 순간에 맞춘다.")]
            [Range(0.1f, 0.9f)] public float peakAt = 0.6f;
            [Tooltip("재생마다 음높이 흔들림(±).")]
            [Range(0f, 0.2f)] public float pitchJitter = 0.06f;

            [System.NonSerialized] public AudioClip[] clips;
            [System.NonSerialized] public AudioSource src;
            [System.NonSerialized] public float next, dir;
            [System.NonSerialized] public int last = -1;
        }

        public List<Channel> channels = new List<Channel>
        {
            new Channel { prefix = "heli",   interval = new Vector2(35f, 90f),  firstDelay = new Vector2(12f, 25f), volume = 0.7f,  pan = Pan.Sweep, peakAt = 0.65f },
            // 멀리서 들리는 비명 — 드물게, 작게, 한 방향. 음높이를 더 흔들어 매번 다른 사람처럼
            new Channel { prefix = "scream", interval = new Vector2(30f, 75f),  firstDelay = new Vector2(20f, 40f), volume = 0.45f, pan = Pan.Fixed, panWidth = 0.7f, pitchJitter = 0.12f },
            new Channel { prefix = "siren",  interval = new Vector2(60f, 140f), firstDelay = new Vector2(35f, 60f), volume = 0.5f,  pan = Pan.Sweep, peakAt = 0.4f, pitchJitter = 0.03f },
        };

        [Tooltip("어떤 두 소리도 이 간격(초) 안에 연달아 시작하지 않는다.")]
        public float minGap = 6f;

        float _lastStart = -999f;

        void Awake() { Init(); }

        /// 채널 준비. <b>플레이 중에 스크립트가 다시 컴파일되면</b> 유니티가 Awake 를 다시 부르지 않고
        /// NonSerialized 필드(clips·src)만 비워 둔다 — 그때도 Update 가 스스로 다시 부른다(실제로 그렇게 멈췄다).
        void Init()
        {
            foreach (var old in GetComponents<AudioSource>()) Destroy(old);   // 이전 버전·이전 Init 이 만든 소스
            var all = Resources.LoadAll<AudioClip>("Ambience");
            foreach (var c in channels)
            {
                var list = new List<AudioClip>();
                foreach (var a in all) if (a.name.StartsWith(c.prefix + "_")) list.Add(a);
                c.clips = list.ToArray();
                c.src = gameObject.AddComponent<AudioSource>();
                c.src.playOnAwake = false;
                c.src.spatialBlend = 0f;   // 2D — 탑뷰에서 3D 위치는 의미가 없고 팬으로 방향만 준다
                c.src.loop = false;
                c.src.priority = 200;      // 효과음(총성·피격)에 채널을 양보
                c.next = Time.time + Random.Range(c.firstDelay.x, c.firstDelay.y);
            }
        }

        void Update()
        {
            if (channels.Count > 0 && channels[0].src == null) Init();
            foreach (var c in channels)
            {
                if (c.clips == null || c.clips.Length == 0) continue;
                if (c.src.isPlaying) { Steer(c); continue; }
                if (Time.time < c.next) continue;
                // 융단 폭격(필사기) 중에는 배경 헬기를 쉰다 — 폭격 헬기 소리와 겹치면 두 대처럼 들린다
                if (c.prefix == "heli" && ProtoAirstrike.Active) { c.next = Time.time + 6f; continue; }
                if (Time.time - _lastStart < minGap) { c.next = _lastStart + minGap + Random.Range(0.5f, 3f); continue; }
                Play(c);
            }
        }

        void Play(Channel c)
        {
            int i = Random.Range(0, c.clips.Length);
            if (c.clips.Length > 1 && i == c.last) i = (i + 1) % c.clips.Length;   // 같은 클립 연속 금지
            c.last = i;
            c.dir = Random.value < 0.5f ? 1f : -1f;
            c.src.clip = c.clips[i];
            c.src.pitch = 1f + Random.Range(-c.pitchJitter, c.pitchJitter);
            c.src.volume = c.volume * ProtoSettings.SfxVolume;
            // Fixed 는 한쪽 어딘가(가운데 제외) — 멀리 한 방향에서 들린 소리
            c.src.panStereo = c.pan == Pan.Sweep ? -c.panWidth * c.dir : c.dir * Random.Range(0.35f, c.panWidth);
            c.src.Play();
            _lastStart = Time.time;
            c.next = Time.time + c.src.clip.length / c.src.pitch + Random.Range(c.interval.x, c.interval.y);
        }

        void Steer(Channel c)
        {
            c.src.volume = c.volume * ProtoSettings.SfxVolume;   // 설정 슬라이더를 끌면 재생 중에도 반영
            if (c.pan != Pan.Sweep) return;
            float t = c.src.time / Mathf.Max(0.01f, c.src.clip.length);
            float s = t < c.peakAt ? Mathf.Lerp(-1f, 0f, t / c.peakAt) : Mathf.Lerp(0f, 1f, (t - c.peakAt) / (1f - c.peakAt));
            c.src.panStereo = s * c.panWidth * c.dir;
        }
    }
}
