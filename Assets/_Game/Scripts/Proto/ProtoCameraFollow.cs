using UnityEngine;

namespace Proto
{
    /// 카메라 추적 (GDD §03): 피치 58°, 거리 15.5m, SmoothDamp 0.16s.
    /// 이동 방향 리드 오프셋은 GDD §03 이 1.4m 였으나 2026-11-10 사용자 결정으로 0 —
    /// 아래 leadDistance 주석 참조.
    /// 2026-11-10 부터 기본 구도는 각도 3(Yet Another Zombie Survivors 식 — 낮고 가깝고 넓은 화각)이다.
    /// 아래 "각도 3" 주석 참조. 각도 1(위 수치, GDD §03 계열)·각도 2 는 비교용으로 남아 있다.
    ///
    /// 화면 흔들림도 여기서 담당한다. §03 은 "폭발과 보스 착지에만, 진폭 0.12m 이하,
    /// 접근성 옵션에서 0" 으로 규정한다 — 상한과 온/오프를 이 컴포넌트가 들고 있어야
    /// 부르는 쪽(폭발·보스)이 제각각 세게 흔들 수 없다.
    public class ProtoCameraFollow : MonoBehaviour
    {
        public static ProtoCameraFollow I { get; private set; }

        public Transform target;
        public float pitch      = 58f;
        public float distance   = 17.76f;  // GDD §03은 15.5m. 22.2m로 쓰다가 16:9 전환 후 20% 당김
        // 따라가는 시정수. 달리는 동안 카메라가 플레이어보다 (이동 속도 × 이 값)만큼 뒤에 있다.
        // GDD §03 은 0.16s(4.2m/s 에서 0.67m). 2026-11-10 사용자 요청("약 0.6m 늦게")으로 0.143s — 4.2 × 0.143 = 0.60m.
        // 씬에 저장된 값이 우선이라 씬 4개도 같이 0.143 으로 바꿨다.
        public float smoothTime = 0.143f;
        // 이동 방향으로 앞서 보는 거리(m). GDD §03 은 1.4m 였는데, 그 값이면 달리는 동안 카메라가
        // 플레이어보다 약 0.7m 앞서 나가고(리드 1.4 − 지연 4.2m/s × 0.16s) 방향을 꺾을 때 2.8m 를
        // 쓸고 지나가 어색했다. 2026-11-10 사용자 결정으로 0 — 이제 카메라는 플레이어보다
        // 늦게 따라간다(지금은 약 0.6m — 위 smoothTime). 더 늦추려면 smoothTime 을 올린다(0.3s 를 넘기면 가는 쪽 시야가 준다).
        // 씬에 저장된 값이 우선이므로 씬 4개(Proto_Chapter01 · Proto_City_Test · Proto_Stream_Test ·
        // FX_Showcase)도 같이 0 으로 바꿨다.
        public float leadDistance = 0f;

        // ── 맵 끝에서 멈추기 (2026-11-10) ────────────────────────────────
        // 플레이어는 ProtoConfig.ArenaHalf 에서 보이지 않는 벽에 멈추는데 카메라는 끝까지 플레이어를
        // 화면 중앙에 두었다. 그래서 맵 끝에 서면 화면의 큰 부분이 맵 밖이었다 — Proto_City_Test
        // 기준 위 8.6m · 아래 4.5m · 좌우 10.1m. 맵 끝 근처에서는 카메라를 먼저 세우고 플레이어만
        // 화면 가장자리 쪽으로 걸어가게 한다(탕탕특공대 · Survivor Ace 가 쓰는 방식).
        // 보스 울타리(focusOverride) 중에는 적용하지 않는다 — 울타리 전체가 보여야 하고,
        // ProtoBossArena 가 중심을 이미 맵 안쪽으로 밀어 둔다.
        [Header("맵 끝에서 멈추기")]
        // 0.6 일 때 Proto_City_Test 에서 맵 밖이 비치는 양(도시 가장자리 기준):
        //   각도 1 — 위 2.9m · 아래 0.3m · 좌우 2.9m
        //   각도 3 — 위 6.1m · 아래 0.1m · 좌우 3.3m. 넓은 화각이라 위쪽이 멀리 보인다(그 구간은 거리 안개 속이다).
        //            **이 줄은 55°·화각 60° 때 잰 값이다.** 52°·65° 로 바꾼 뒤에는 다시 재지 않았다 — 위쪽이 더 비칠 것이다.
        [Tooltip("맵 끝에서 플레이어가 화면 중앙을 벗어나도 되는 정도(화면 기준). " +
                 "0 = 끔(항상 중앙) · 0.6 = 화면 중앙에서 끝까지 거리의 60% 지점까지. " +
                 "키우면 맵 밖이 덜 보이는 대신 플레이어가 화면 끝에 붙는다 — 0.7 을 넘기면 맵 위쪽 끝에서 머리가 화면 끝에 닿는다.")]
        [Range(0f, 0.8f)] public float edgeDrift = 0.6f;

        // 시작 연출(ProtoIntro)이 만지는 값. 평소엔 1 / 0.
        [System.NonSerialized] public float introZoom = 1f;   // 타깃까지 거리 배율
        [System.NonSerialized] public float introLift = 0f;   // 초점을 이만큼 위로(m) — 공중의 캐릭터를 따라간다
        [System.NonSerialized] public Vector3? focusOverride;   // 값이 있으면 플레이어 대신 이 지점을 본다 (보스 울타리)
        [System.NonSerialized] public float vehicleZoom = 1f; // 탱크 탑승 중 거리 배율 (ProtoTank 가 서서히 올리고 내린다)

        // ── 카메라 각도 고르기 (임시 — 어느 쪽으로 갈지 정하면 정리한다) ──
        /// <summary>어느 각도로 볼지. 인스펙터에서 고른다. 플레이 중에 바꿔도 바로 반영된다.</summary>
        public enum AnglePreset
        {
            /// <summary>1 — 원래 각도. 아래 pitch·distance 값을 쓴다 (GDD §03 계열).</summary>
            Angle1_Original,
            /// <summary>2 — 씬에서 눈으로 맞춘 낮고 가까운 각도.</summary>
            Angle2_Low,
            /// <summary>3 — Yet Another Zombie Survivors 식(게임 데이터 값). 내려보는 각 52° · 화각 65° · 캐릭터까지 약 12m, 아래로 달리면 카메라가 물러나 캐릭터가 화면 위로 올라간다.</summary>
            Angle3_Yazs,
        }

        [Header("카메라 각도 — 셋 중 선택 (플레이 중 전환 가능)")]
        [Tooltip("1 = 위의 pitch·distance (원래) · 2 = 아래 낮은 각도 · 3 = Yet Another Zombie Survivors 식")]
        public AnglePreset preset = AnglePreset.Angle3_Yazs;

        // 각도 2 만 따로 들고 있는 이유: 각도 1 은 위의 pitch·distance 가 이미 들고 있고,
        // 그 값은 ProtoGroundFog · SceneLookSetup 주석이 기준으로 참조한다. 여기에 58/17.76 을
        // 한 벌 더 적으면 한쪽만 고쳐지는 날이 온다.
        //
        // 각도 2 는 pitch·distance 로 역산해 넣지 않고 **오프셋과 회전을 따로** 둔다.
        // pitch 하나로 둘을 동시에 정하는 방식(각도 1)은 카메라가 타깃을 정확히 겨누게 만드는데,
        // 씬에서 손으로 맞춘 구도는 그렇지 않았다 — 실측으로 회전은 37.3°인데 위치에서 역산한
        // 각은 35.3°였다(타깃이 화면 중앙보다 조금 위에 오게 잡아둔 것). 그 2°가 곧 구도라서
        // 역산해 넣으면 눈으로 맞춘 그림이 미묘하게 달라진다.
        [Tooltip("각도 2의 타깃 기준 상대 위치")]
        public Vector3 angle2Offset = new Vector3(0f, 6.67f, -9.41f);
        [Tooltip("각도 2의 내려보는 각(회전 X). 위 오프셋과 따로 둔다 — 주석 참조")]
        public float angle2Pitch = 37.3f;

        // ── 각도 3 — Yet Another Zombie Survivors 식 (2026-11-10 사용자 요청) ─────────────
        // 처음엔 YAZS 가 이 PC 에 없어 스팀 스크린샷·플레이 영상에서 쟀다(각 50° → 사용자 요청으로 55° ·
        // 화각 60° · 거리 13m · 캐릭터 높이 40% 고정). 같은 날 게임 데이터(v1.0.1a — GameMaster.prefab 의
        // 시네머신 가상 카메라)를 읽었고, 사용자 요청("실제 값으로 맞춰 줘")으로 **그 값으로 바꿨다**:
        //   · 내려보는 각 52° · 세로 화각 65° — 가상 카메라 전부 같다.
        //   · 카메라 위치(캐릭터 기준). 회전은 그대로 두고 위치만 다르다:
        //       평소            `CM vcam1_MovementRegular` (0, 9.25, -7.75) — 캐릭터까지 12.07m, 캐릭터는 화면 위에서 47.3%
        //       아래로 달릴 때  `CM vcam1_MovementDown`    (0, 9,    -9.25) — 12.91m, 39.3%. 뒤로 물러나 아래쪽 바닥이 더 보인다
        //       옵션 Centered   `CM vcam1_Centered`        (0, 9,    -8)    — 12.04m, 45.0%, 감쇠 0. 우리는 안 넣었다
        //   · 두 위치 사이는 2.2초에 걸쳐 옮겨 간다(GameplayMaster.cameraBlendTime).
        //     **"언제 옮기나"는 추정이다** — YAZS 코드 본문은 복원되지 않아 가상 카메라 이름(MovementDown)과
        //     필드 이름(lastPlayerMovementDirection)으로 짐작했다: 화면 아래쪽으로 달리면 아래 위치로,
        //     다른 쪽으로 달리면 평소 위치로, 서 있으면 그대로(TickDownShift). 어색하면 yazsDownShift 를 끈다.
        //   · 따라가기(리드 0 · smoothTime 0.143s)는 **안 바꿨다.** YAZS 는 감쇠 1.5 인 시네머신이 캐릭터가 아니라
        //     별도 추적 오브젝트(CameraFollowTarget)를 따라가는데, 그 오브젝트를 움직이는 코드를 읽지 못했다.
        //     지금 값은 영상에서 잰 "약 0.6m 늦게"에 사용자가 맞춘 것이다.
        //   · 줌은 없다(개발사: 카메라 거리는 성능·밸런스에 묶여 고정).
        //
        // **필드 이름이 angle3* 가 아니라 yazs* 인 이유**: 에디터가 열려 있으면 씬의 컴포넌트가 옛 값(55/13/60/0.2)을
        // 메모리에 들고 있어, 코드 기본값만 고쳐서는 플레이에 반영되지 않는다. 이름을 바꾸면 옛 값이 버려지고
        // 아래 기본값이 들어간다. 씬 파일에는 이 값들이 저장돼 있지 않았다(씬을 저장하면 그때부터 씬 값이 우선이다).
        //
        // 이 구도에서 캐릭터 기준으로 보이는 바닥(16:9, 기하 계산 — **화면에서 재지는 않았다**):
        //   평소 위 18.4m · 아래 6.9m · 옆 13.7m, 화면 위쪽 모서리 32m. 아래로 달릴 때 위 16.2m · 아래 8.4m.
        //   (바꾸기 전 55°·60°·13m 는 위 13.4 · 아래 8.5 · 옆 14.5 · 모서리 26m / 각도 1 은 위 10.6 · 아래 6.5 · 옆 12.1 · 모서리 19m)
        // 화면 위쪽이 5m 더 멀리 보인다. 그래서 생기는 일:
        //   ① 좀비 스폰 원(약 19m)이 화면 위쪽에 걸린다 → ProtoSpawnDirector.OffscreenDistance 가 화면 밖으로 민다.
        //      위에서 오는 좀비는 그만큼 더 멀리서 걸어온다.
        //   ② 거리 안개(19.5~29m)·바닥 안개·장식 표시 반경(ProtoDecorStreamer 24m)·소리 컷(20m)은 각도 1 기준
        //      그대로다. 화면 맨 위 가운데는 카메라에서 27.7m 라 안개가 약 86%, 위쪽 모서리는 38m 라 100% 다.
        //      장식이 24m 에서 켜지는 자리는 화면 안에서 카메라로부터 약 30m 이상 — 안개 끝(29m) 바로 바깥이라
        //      아슬아슬하게 가려진다. 안개를 걷거나 옅게 하면 팝인이 드러나니 showRadius 를 같이 올려야 한다.
        [Header("각도 3 — Yet Another Zombie Survivors 식 (게임 데이터 값)")]
        [Tooltip("내려보는 각(°). YAZS 가상 카메라 전부 52°")]
        public float yazsPitch = 52f;
        [Tooltip("세로 화각(°). YAZS 65°. 각도 1·2 는 카메라에 저장된 화각(42°)을 그대로 쓴다")]
        public float yazsFov = 65f;
        [Tooltip("평소 카메라 위치(캐릭터 기준, m) — YAZS 'CM vcam1_MovementRegular'. " +
                 "캐릭터까지 12.07m, 캐릭터가 화면 위에서 47% 높이에 선다")]
        public Vector3 yazsOffset = new Vector3(0f, 9.25f, -7.75f);
        [Tooltip("화면 아래쪽으로 달릴 때 아래의 위치로 옮겨 간다. 끄면 항상 위의 평소 위치")]
        public bool yazsDownShift = true;
        [Tooltip("아래로 달릴 때의 카메라 위치(캐릭터 기준, m) — YAZS 'CM vcam1_MovementDown'. " +
                 "캐릭터까지 12.91m, 캐릭터가 위에서 39% 높이로 올라가 아래쪽 바닥이 더 보인다")]
        public Vector3 yazsDownOffset = new Vector3(0f, 9f, -9.25f);
        [Tooltip("두 위치 사이를 옮겨 가는 시간(초). YAZS 2.2")]
        public float yazsDownBlendTime = 2.2f;

        // 이동 방향의 아래(-Z) 성분이 이보다 커야 "아래로 달린다"로 본다(수평에서 약 20° 아래).
        // YAZS 의 기준은 읽지 못했다 — 추정값이다. 너무 낮으면 옆으로 달리다 살짝만 내려가도 카메라가 물러난다.
        const float DownShiftMinDown = 0.35f;

        // ── 휠로 각도 전환 (임시 — 구도를 확정하면 지운다) ────────────────
        // 인스펙터에서 preset 을 바꿔 가며 보면 플레이 모드를 들락거리게 되어 감이 끊긴다.
        // 굴려서 바로 갈아 보는 편이 빠르다. **에디터·개발 빌드에서만 돈다.**
        [Header("휠로 각도 전환 (임시)")]
        [Tooltip("마우스 휠로 preset 을 순환한다. 끄면 휠을 완전히 무시한다.")]
        public bool scrollSwitchesPreset = true;

        [Tooltip("바꾼 직후 화면 구석에 어느 각도인지 잠깐 띄운다 (초). 0 이면 안 띄운다.")]
        public float presetReadoutTime = 1.6f;

        // 휠 한 번에 여러 프레임이 들어오는 환경이 있어 연타로 읽히면 두 번 넘어간다.
        float _nextPresetSwitch;
        float _readoutUntil;

        bool UsingAngle2 => preset == AnglePreset.Angle2_Low;
        bool UsingAngle3 => preset == AnglePreset.Angle3_Yazs;

        /// 각도 3 — 지금 쓸 카메라 위치(캐릭터 기준). YAZS 처럼 평소 위치와 "아래로 달릴 때" 위치를 섞는다.
        Vector3 YazsOffsetNow => Vector3.Lerp(yazsOffset, yazsDownOffset, Mathf.SmoothStep(0f, 1f, _downBlend));

        /// 지금 고른 각도의, 초점(화면 중앙의 바닥 점) 기준 상대 위치.
        Vector3 CurrentOffset
        {
            get
            {
                if (UsingAngle2) return angle2Offset;
                if (UsingAngle3)
                {
                    // 높이는 YAZS 오프셋 그대로, 뒤로 물러난 거리는 내려보는 각에서 역산한다 — 그래야 이 점이
                    // 화면 정중앙이다. YAZS 오프셋은 그보다 더 뒤에 있고, 그 차이가 캐릭터를 화면 위로 올린다(CurrentRise).
                    float h = Mathf.Max(0.1f, YazsOffsetNow.y);
                    return new Vector3(0f, h, -h / Mathf.Tan(Mathf.Clamp(yazsPitch, 5f, 89f) * Mathf.Deg2Rad));
                }
                float rad = pitch * Mathf.Deg2Rad;
                return new Vector3(0f, Mathf.Sin(rad) * distance, -Mathf.Cos(rad) * distance);
            }
        }

        /// 지금 고른 각도의 회전(X).
        float CurrentPitch => UsingAngle2 ? angle2Pitch : UsingAngle3 ? yazsPitch : pitch;

        /// 지금 고른 각도의 세로 화각. 각도 1·2 는 씬 카메라에 저장된 값(_baseFov)을 그대로 쓴다.
        float CurrentFov => UsingAngle3 ? yazsFov : _baseFov;

        /// 캐릭터를 화면 중앙보다 위에 두는 정도(화면 중앙→위 끝 = 1). 각도 3 만 쓴다.
        /// YAZS 오프셋에서 캐릭터가 찍히는 화면 높이를 구한다(평소 0.054 = 위에서 47% · 아래로 달릴 때 0.215 = 39%).
        /// 바닥 거리가 아니라 화면 비율로 넘기는 이유: RiseMeters 가 탱크 줌을 넣어 다시 바닥 거리로 바꾸므로
        /// 줌이 걸려도 캐릭터가 같은 높이에 선다.
        float CurrentRise
        {
            get
            {
                if (!UsingAngle3) return 0f;
                Vector3 o = YazsOffsetNow;
                float toChar = Mathf.Atan2(o.y, -o.z);                // 카메라에서 캐릭터 발밑을 내려보는 각
                float above  = yazsPitch * Mathf.Deg2Rad - toChar;    // 그 각이 화면 중앙(내려보는 각)보다 얼마나 위인가
                float k = Mathf.Tan(above) / Mathf.Tan(Mathf.Max(1f, yazsFov) * 0.5f * Mathf.Deg2Rad);
                return Mathf.Clamp(k, 0f, 0.5f);
            }
        }

        /// <summary>
        /// 씬에서 카메라를 옮겨 맞춘 구도를 각도 2 에 받아 적는다.
        /// 컴포넌트 우클릭 메뉴에서 쓴다 — 각도를 계속 손보는 동안 숫자를 옮겨 적지 않아도 된다.
        /// </summary>
        [ContextMenu("현재 씬 카메라를 각도 2로 저장")]
        void CaptureAngle2()
        {
            if (target == null) { Debug.LogWarning("[카메라] target 이 비어 있어 저장할 수 없다"); return; }
            angle2Offset = transform.position - target.position;
            angle2Pitch = transform.eulerAngles.x;
            preset = AnglePreset.Angle2_Low;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
            Debug.Log($"[카메라] 각도 2 저장 — 오프셋 {angle2Offset:F2} (거리 {angle2Offset.magnitude:F2}m), "
                      + $"내려보는 각 {angle2Pitch:F1}°");
        }

        [Header("화면 흔들림 (GDD §03)")]
        [Tooltip("접근성 옵션 — 끄면 흔들림이 완전히 0 이 된다")]
        public bool shakeEnabled = true;
        [Tooltip("진폭 상한 m. 기획서 §03 은 0.12m 였는데 카메라가 18m 떨어져 있어 거의 안 보였다 — " +
                 "2026-10-13 사용자 요청(큰 충격에 흔들림)으로 0.45 로 올렸다. 씬에 0.12 가 저장돼 있어도 Awake 에서 끌어올린다")]
        public float shakeMaxAmplitude = 0.45f;
        public const float MinMaxAmplitude = 0.45f;

        /// 충격 단계. 호출하는 쪽은 진폭 숫자 대신 이걸 고른다 — 같은 종류의 충격이 화면마다 다르게 흔들리지 않게.
        public enum Impact
        {
            Light,    // 치명타 적중 — 잦다. 손끝에 '탁' 정도
            Medium,   // 로켓 폭발·포위 등장·보스에게 크게 맞음
            Heavy,    // 보스 내려찍기·최종 보스 등장·탱크 파괴
            Huge,     // 보스 도약 착지·승합차 폭발
        }

        /// 단계별 (진폭 m, 길이 s). 진폭은 상한(shakeMaxAmplitude)으로 다시 잘린다.
        public static void Shake(Impact level)
        {
            switch (level)
            {
                case Impact.Light:  Shake(0.05f, 0.10f); break;
                case Impact.Medium: Shake(0.14f, 0.25f); break;
                case Impact.Heavy:  Shake(0.28f, 0.40f); break;
                case Impact.Huge:   Shake(0.42f, 0.60f); break;
            }
        }
        [Tooltip("흔들림 주파수 Hz. 낮으면 '카메라가 흐른다', 높으면 '지글거린다'")]
        public float shakeFrequency = 26f;

        Vector3 _vel;
        Vector3 _basePos;      // 흔들림을 뺀 위치. 이걸 따로 두지 않으면 오프셋이 다음 프레임
                               // SmoothDamp 의 입력이 되어 카메라가 흔들림만큼 계속 밀려난다.
        ProtoPlayer _player;
        Camera _cam;           // 화면이 바닥을 얼마나 비추는지 재는 데 쓴다 (맵 끝에서 멈추기) · 화각을 각도에 맞춘다
        float _baseFov = 42f;  // 씬 카메라에 저장된 화각. 각도 3 에서 돌아올 때 되돌릴 값 (Awake 에서 읽는다)
        bool _snapped;        // 첫 프레임은 보간 없이 정확한 위치로 스냅한다
        float _downBlend;     // 각도 3 — 0 = 평소 위치(yazsOffset) · 1 = 아래로 달릴 때 위치(yazsDownOffset)
        bool _downWanted;     // 마지막으로 달린 방향이 화면 아래쪽이었나 (서 있는 동안 유지)

        float _shakeAmp, _shakeLeft, _shakeLen, _shakeSeed;

        /// <summary>폭발·보스 착지에서 부른다. 진폭은 <see cref="shakeMaxAmplitude"/> 로 잘린다.</summary>
        public static void Shake(float amplitude, float duration)
        {
            if (I != null) I.AddShake(amplitude, duration);
        }

        void AddShake(float amplitude, float duration)
        {
            if (!shakeEnabled) return;
            amplitude = Mathf.Min(amplitude, shakeMaxAmplitude);
            if (amplitude <= 0.0005f || duration <= 0.01f) return;

            // 동시에 여러 발이 터질 때(융단 폭격 6지점) 진폭을 더하면 화면이 뒤집힌다.
            // 남은 흔들림보다 센 것만 받아 갈아탄다 — 약한 게 강한 걸 끊지도 않는다.
            float current = _shakeLeft > 0f ? _shakeAmp * (_shakeLeft / Mathf.Max(0.01f, _shakeLen)) : 0f;
            if (amplitude <= current) return;

            _shakeAmp  = amplitude;
            _shakeLen  = duration;
            _shakeLeft = duration;
            _shakeSeed = Random.value * 100f;
        }

        /// <summary>
        /// 마우스 휠로 <see cref="preset"/> 을 순환한다. <b>임시 도구다</b> — 구도를 확정하면
        /// 이 메서드와 <see cref="OnGUI"/>, 위의 필드를 통째로 지우면 된다.
        ///
        /// <para>각도 값 자체는 건드리지 않는다. 두 구도를 <b>나란히 비교</b>하는 게 목적이라
        /// 굴릴 때마다 숫자가 변하면 오히려 비교가 안 된다 — 각도 1 · 2 · 3 을 차례로 오갈 뿐이다.</para>
        ///
        /// <para><b>플레이 모드를 나가면 preset 도 되돌아간다</b>(유니티가 컴포넌트를 원상복구한다).
        /// 마음에 드는 쪽이 정해지면 인스펙터에서 같은 값으로 바꿔 두어야 한다 — 바꿀 때마다
        /// 콘솔에 어느 각도인지 찍어 두므로 나가서 그대로 보면 된다.</para>
        /// </summary>
        void TickPresetScroll()
        {
            // 폰에서도 비교해 보려고 일반 빌드에도 넣는다(2026-10-01). 휠 = 두 손가락 핀치.
            if (!scrollSwitchesPreset) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f) wheel = PinchStep();
            if (Mathf.Abs(wheel) < 0.01f) return;
            if (Time.unscaledTime < _nextPresetSwitch) return;
            _nextPresetSwitch = Time.unscaledTime + 0.12f;

            var values = (AnglePreset[])System.Enum.GetValues(typeof(AnglePreset));
            int i = System.Array.IndexOf(values, preset);
            // 휠 방향대로 순환한다 (1 → 2 → 3 → 1, 반대로 굴리면 거꾸로).
            i =((i + (wheel > 0f ? 1 : -1)) % values.Length + values.Length) % values.Length;
            preset = values[i];

            // 위치는 SmoothDamp 가 따라가지만 회전은 LateUpdate 가 매 프레임 덮어쓰므로
            // 여기서 따로 손댈 것이 없다. 스냅을 풀면 새 각도로 부드럽게 흘러간다.
            _readoutUntil = Time.unscaledTime + presetReadoutTime;
            Debug.Log($"[카메라] {preset} — 오프셋 {CurrentOffset:F2} "
                      + $"(거리 {CurrentOffset.magnitude:F2}m), 내려보는 각 {CurrentPitch:F1}°, 화각 {CurrentFov:F0}°");
        }

        /// <summary>
        /// 두 손가락 핀치를 휠 한 칸으로 바꾼다. 벌리면 +1(휠 위), 오므리면 -1. 손가락 사이 거리가
        /// 화면 짧은 변의 <see cref="PinchStepRatio"/> 만큼 변할 때마다 한 칸 — 손을 떼지 않고 여러 칸 넘길 수 있다.
        /// </summary>
        float PinchStep()
        {
            if (Input.touchCount != 2) { _pinchActive = false; return 0f; }
            float d = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            if (!_pinchActive || Input.GetTouch(0).phase == TouchPhase.Began || Input.GetTouch(1).phase == TouchPhase.Began)
            {
                _pinchActive = true; _pinchStart = d; return 0f;
            }
            float diff = d - _pinchStart;
            if (Mathf.Abs(diff) < Mathf.Min(Screen.width, Screen.height) * PinchStepRatio) return 0f;
            _pinchStart = d;           // 한 칸 넘긴 자리부터 다시 잰다
            return diff > 0f ? 1f : -1f;
        }

        const float PinchStepRatio = 0.12f;
        bool _pinchActive;
        float _pinchStart;

        GUIStyle _readoutStyle;

        void OnGUI()
        {
            if (!scrollSwitchesPreset || presetReadoutTime <= 0f) return;
            if (ProtoHUD.CaptureMode) return;   // UI 화면 캡처(F10) 중에는 숨긴다
            if (Time.unscaledTime > _readoutUntil) return;
            if (_readoutStyle == null) _readoutStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };   // 예전엔 OnGUI 마다 새 스타일(2026-11-20)
            var st = _readoutStyle;
            // 11-18 왼쪽 아래 구석은 개발 정보 상자(ProtoHUD.DevPanelSize) 자리 — 그 바로 위에 띄운다
            var safe = Screen.safeArea;
            float s = Mathf.Sqrt(Screen.width / 1080f * (Screen.height / 1920f));
            var r = new Rect(safe.xMin + ProtoHUD.LayoutPad * s,
                             Screen.height - safe.yMin - (ProtoHUD.LayoutPad + ProtoHUD.DevPanelSize.y + 10f) * s - 34f, 430f, 34f);
            GUI.Box(r, GUIContent.none);
            GUI.Label(new Rect(r.x + 8f, r.y + 3f, r.width - 16f, 30f),
                      $"{preset}   내려보는 각 {CurrentPitch:F1}°   거리 {CurrentOffset.magnitude:F2}m   화각 {CurrentFov:F0}°"
                      + "\n휠·두 손가락 핀치로 각도 전환 (임시 도구)", st);
        }

        void Awake()
        {
            I = this;
            useGUILayout = false;   // GUI.Box · GUI.Label 만 쓴다 — 레이아웃 패스를 건너뛴다(2026-11-20)
            _cam = GetComponent<Camera>();
            if (_cam != null) _baseFov = _cam.fieldOfView;
            shakeMaxAmplitude = Mathf.Max(shakeMaxAmplitude, MinMaxAmplitude);   // 씬에 저장된 옛 상한(0.12)을 올린다
        }
        void OnDestroy() { if (I == this) I = null; }

        void Start()
        {
            if (target != null) _player = target.GetComponent<ProtoPlayer>();
            if (_cam != null && !_cam.orthographic) _cam.fieldOfView = CurrentFov;
            transform.rotation = Quaternion.Euler(CurrentPitch, 0f, 0f);
            _basePos = DesiredPosition(Vector3.zero);
            transform.position = _basePos;
            Debug.Log($"[카메라] {preset} — 오프셋 {CurrentOffset:F2} "
                      + $"(거리 {CurrentOffset.magnitude:F2}m), 내려보는 각 {CurrentPitch:F1}°, 화각 {CurrentFov:F0}°");
        }

        /// 화각을 지금 각도의 값으로 맞춘다. 휠로 각도를 바꾸면 위치·회전과 같은 시정수로 따라간다.
        /// 총구 화염 같은 "항상 위에" 효과를 그리는 오버레이 카메라(FxOverlay)는 만들 때 한 번만 화각을
        /// 복사하므로, 여기서 같이 맞춰 주지 않으면 그 효과만 다른 화각으로 그려져 자리가 어긋난다.
        void TickFov()
        {
            if (_cam == null || _cam.orthographic) return;
            float want = CurrentFov;
            if (!Mathf.Approximately(_cam.fieldOfView, want))
            {
                float f = Mathf.Lerp(_cam.fieldOfView, want,
                                     1f - Mathf.Exp(-ProtoConfig.UnscaledDelta / Mathf.Max(0.01f, smoothTime)));
                _cam.fieldOfView = Mathf.Abs(f - want) < 0.02f ? want : f;
            }
            FxOverlay.SyncFov(_cam);
        }

        /// 각도 3 — 화면 아래쪽(-Z)으로 달리는 동안 "아래로 달릴 때" 위치 쪽으로, 다른 쪽으로 달리면 평소 위치 쪽으로
        /// yazsDownBlendTime 에 걸쳐 옮겨 간다. 서 있으면 직전 상태 그대로. **이 규칙은 추정이다**(각도 3 주석 참조).
        /// 게임 시간으로 센다 — 레벨업 정지 중에는 멈춘다. 위치는 뒤의 SmoothDamp 를 한 번 더 거치므로 툭 끊기지 않는다.
        void TickDownShift()
        {
            if (!UsingAngle3 || !yazsDownShift) { _downBlend = 0f; _downWanted = false; return; }

            if (_player != null)
            {
                Vector3 d = _player.MoveDir;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) _downWanted = d.normalized.z < -DownShiftMinDown;
            }
            _downBlend = Mathf.MoveTowards(_downBlend, _downWanted ? 1f : 0f,
                                           ProtoConfig.Delta / Mathf.Max(0.05f, yazsDownBlendTime));
        }

        void LateUpdate()
        {
            TickPresetScroll();
            TickFov();
            TickDownShift();
            if (target == null) return;

            Vector3 lead = Vector3.zero;
            if (_player != null)
            {
                Vector3 d = _player.MoveDir;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) lead = d.normalized * leadDistance;
            }

            Vector3 want = DesiredPosition(lead);

            if (!_snapped)
            {
                // 시작 직후 첫 프레임의 deltaTime 은 수백 ms 가 될 수 있다. 그 상태로 보간하면
                // 카메라가 한 번 크게 밀려 "튕기는" 것처럼 보이므로 첫 프레임은 그냥 맞춘다.
                _basePos = want;
                _vel = Vector3.zero;
                _snapped = true;
            }
            else
            {
                // timeScale 0(레벨업 정지) 중에도 카메라가 튀지 않게 unscaled 사용
                _basePos = Vector3.SmoothDamp(_basePos, want, ref _vel,
                                              smoothTime, Mathf.Infinity, ProtoConfig.UnscaledDelta);
            }

            // 각도를 플레이 중에 바꿔도 뚝 끊기지 않게 회전도 위치와 같은 시정수로 따라간다.
            // (위치는 아래 SmoothDamp 가 이미 그렇게 한다)
            var wantRot = Quaternion.Euler(CurrentPitch, 0f, 0f);
            transform.rotation = _snapped
                ? Quaternion.Slerp(transform.rotation, wantRot,
                                   1f - Mathf.Exp(-ProtoConfig.UnscaledDelta / Mathf.Max(0.01f, smoothTime)))
                : wantRot;
            transform.position = _basePos + ShakeOffset();
        }

        /// 화면 축(카메라 right/up)으로만 흔든다. 월드 XZ 로 흔들면 피치 58° 때문에
        /// 세로 성분이 깊이로 새어나가 "카메라가 다가온다"로 보인다.
        Vector3 ShakeOffset()
        {
            if (_shakeLeft <= 0f) return Vector3.zero;

            _shakeLeft -= ProtoConfig.UnscaledDelta;
            if (_shakeLeft <= 0f) { _shakeLeft = 0f; return Vector3.zero; }

            float k = _shakeLeft / Mathf.Max(0.01f, _shakeLen);
            float amp = _shakeAmp * k * k;   // 제곱 감쇠 — 선형이면 끝이 질질 끌린다

            // PerlinNoise 는 정수 격자에서 항상 0.5 라 축마다 다른 좌표로 뽑는다.
            float t = Time.unscaledTime * shakeFrequency;
            float ox = (Mathf.PerlinNoise(_shakeSeed + t, 0.37f) - 0.5f) * 2f;
            float oy = (Mathf.PerlinNoise(0.71f, _shakeSeed + t) - 0.5f) * 2f;

            return (transform.right * ox + transform.up * oy) * amp;
        }

        Vector3 DesiredPosition(Vector3 lead)
        {
            // 최종 보스 울타리(ProtoBossArena) 중에는 울타리 중심에 고정 — 울타리 전체가 화면에 보여야 한다
            if (focusOverride.HasValue) return focusOverride.Value + CurrentOffset * (introZoom * vehicleZoom);

            Vector3 p = (target != null ? target.position : Vector3.zero) + lead;
            // 각도 3 — 캐릭터가 화면 중앙보다 위에 서게, 초점(화면 중앙의 바닥 점)을 카메라 쪽(-Z)으로 당긴다.
            // 회전이 피치뿐이라 화면 위쪽이 곧 월드 +Z 다.
            p.z -= RiseMeters();
            Vector3 focus = ClampToMap(p) + Vector3.up * introLift;
            return focus + CurrentOffset * (introZoom * vehicleZoom);
        }

        /// <see cref="CurrentRise"/>(화면 비율)를 바닥 거리(m)로 바꾼 값. 초점에서 이만큼 앞(+Z)에 선 캐릭터가
        /// 화면의 그 높이에 찍힌다. 각도 3 은 평소 0.52m · 아래로 달릴 때 2.22m.
        float RiseMeters()
        {
            float rise = CurrentRise;
            if (rise <= 0f) return 0f;
            return GroundReach(rise, out float ahead, out _, out _) ? ahead : 0f;
        }

        /// 초점(카메라가 보는 바닥 점)을 맵 안쪽으로 당긴다. 플레이어가 맵 끝(= 갈 수 있는 끝,
        /// <see cref="ProtoConfig.ArenaHalf"/>)에 닿았을 때 화면 중앙→끝 거리의 <see cref="edgeDrift"/>
        /// 지점에 서 있도록, 그만큼 못 미친 곳에서 카메라가 선다.
        /// 무한 월드(ArenaHalf 100000)에서는 한계가 닿지 않아 아무 일도 하지 않는다.
        Vector3 ClampToMap(Vector3 focus)
        {
            if (edgeDrift <= 0f) return focus;
            // 캐릭터가 평소에 서는 높이(CurrentRise)보다 좁게 재면, 맵 한가운데서도 아래 안전망이 초점을
            // 캐릭터 쪽으로 끌어와 그 높이를 지워 버린다 — 둘 중 큰 쪽으로 잰다.
            float k = Mathf.Max(edgeDrift, CurrentRise);
            if (!GroundReach(k, out float ahead, out float behind, out float halfW)) return focus;

            float lim = ProtoConfig.ArenaHalf;
            focus.x = ClampAxis(focus.x, -lim + halfW, lim - halfW);
            focus.z = ClampAxis(focus.z, -lim + behind, lim - ahead);

            // 안전망 — 타깃이 ArenaHalf 보다 바깥에 있어도(경계 값이 씬과 안 맞을 때) 화면 밖으로
            // 내보내지 않는다. 타깃이 맵 안에 있으면 위 한계가 이 조건을 이미 만족시켜 아무 일도 하지 않는다.
            if (target != null)
            {
                Vector3 t = target.position;
                focus.x = Mathf.Clamp(focus.x, t.x - halfW, t.x + halfW);
                focus.z = Mathf.Clamp(focus.z, t.z - ahead, t.z + behind);
            }
            return focus;
        }

        // 맵이 화면보다 좁으면 lo > hi 가 된다 — 그때는 가운데에 둔다.
        static float ClampAxis(float v, float lo, float hi)
            => lo <= hi ? Mathf.Clamp(v, lo, hi) : (lo + hi) * 0.5f;

        /// 초점에서, **화면** 중앙→끝 거리의 <paramref name="k"/> 지점에 비치는 바닥 점까지의 거리(m).
        /// k = 1 이면 화면 끝까지다(각도 1: 위 10.6 · 아래 6.5 · 옆 12.1m / 각도 3: 위 18.9 · 아래 6.3 · 옆 13.3m).
        /// 옆은 초점 깊이에서 잰 값이다.
        ///
        /// 바닥 거리에 k 를 곱하지 않고 화면 비율로 재는 이유: 원근이라 화면 위쪽은 같은 화면 높이에
        /// 훨씬 긴 바닥이 눌려 담긴다. 바닥 거리의 60% 로 잡으면 낮은 각도(각도 2, 37°)에서는 그 점이
        /// 화면 77% 높이에 찍혀 맵 위쪽 끝에서 캐릭터 머리가 화면 밖으로 나간다.
        ///
        /// 실제 transform 이 아니라 **목표 구도**(오프셋·피치·줌)에서 계산한다. transform 에는 보간과
        /// 흔들림이 섞여 있고, 그 값으로 한계를 재면 한계가 다시 transform 을 움직여 서로 물린다.
        /// (ProtoBossArena.MeasureView 는 한 번만 재면 되어 실제 화면 광선을 쓴다.)
        bool GroundReach(float k, out float ahead, out float behind, out float halfWidth)
        {
            ahead = behind = halfWidth = 0f;
            if (_cam == null || _cam.orthographic) return false;

            // 탱크 줌(vehicleZoom)은 넣고 시작 연출 줌(introZoom)은 뺀다. 연출은 몇 초짜리 정해진 구도라
            // 건드리면 안 되고, 넣으면 좁은 맵(40×40)에서 물러난 화면이 맵보다 커져 초점이 맵 한가운데로 끌려간다.
            Vector3 off = CurrentOffset * vehicleZoom;
            float h = off.y, back = -off.z;
            if (h <= 0.01f) return false;

            // 화각도 카메라의 지금 값이 아니라 목표 값 — 각도를 바꾸는 동안에는 카메라 화각이 보간 중이다.
            float tanHalf = Mathf.Tan(CurrentFov * 0.5f * Mathf.Deg2Rad);
            // 화면 세로 k 지점을 지나는 광선이 카메라 정면축과 이루는 각
            float span = Mathf.Atan(k * tanHalf) * Mathf.Rad2Deg;
            // 광선이 수평에 가까우면 바닥에 닿는 곳이 끝없이 멀어진다 — 5° 에서 막는다.
            float top = Mathf.Max(5f, CurrentPitch - span) * Mathf.Deg2Rad;
            float bot = Mathf.Min(89f, CurrentPitch + span) * Mathf.Deg2Rad;
            ahead  = h / Mathf.Tan(top) - back;
            behind = back - h / Mathf.Tan(bot);

            // 초점까지의 깊이(카메라 정면 방향 성분) × tan(수직 FOV 절반) × 화면비 × k
            float p = CurrentPitch * Mathf.Deg2Rad;
            float depth = back * Mathf.Cos(p) + h * Mathf.Sin(p);
            halfWidth = k * depth * tanHalf * _cam.aspect;

            return ahead > 0f && behind > 0f && halfWidth > 0f;
        }
    }
}
