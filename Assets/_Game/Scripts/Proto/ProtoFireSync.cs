using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 발사와 사격 애니메이션의 때를 맞춘다 (2026-11-04 사용자: "자세를 잡고 사격 애니메이션이 나갈 때 총구 효과가 나와야 한다").
    ///
    /// <para>전에는 발사 트리거를 거는 순간에 총구 화염 · 탄피 · 총알 · 발사음이 바로 났다. 그런데 컨트롤러(sushi_girl_rifle)는
    /// Any State → "Shoot Rifle" 로 0.08초 전환하고(자기 자신으로도 — 매 발 클립이 처음부터 다시 돈다), 몸을 조준 방향으로 트는
    /// 데(ProtoShooter.TickAimTwist, 57° 를 600°/s) 0.1초쯤 걸린다. 그래서 첫 발은 대기 자세의 총구에서, 총이 옆을 보는 채로 났다.</para>
    ///
    /// <para>이제 슈터와 무기 무대는 발사를 <b>줄에 세워 두고</b>(트리거는 바로 건다), 아래 조건이 맞는 LateUpdate(애니메이터가 본을 움직인 뒤)에 낸다:
    /// ① 사격 상태("Shoot Rifle")에 들어와 전환이 끝났고 클립 정규화 시각이 <see cref="FireAt"/> 를 지났다 ② 몸 틀기가 <see cref="AimSettleDeg"/> 안으로 들어왔다.
    /// 애니메이터가 없거나 컨트롤러가 달라 끝내 못 맞추면 <see cref="MaxWait"/> 뒤에 그냥 낸다 — 연출 때문에 총이 안 나가면 안 된다.
    /// 발사 간격(DPS)은 그대로다 — 줄을 서는 시간만큼 전체가 일정하게 늦을 뿐이다.</para>
    ///
    /// <para><see cref="FireAt"/> 는 FX 툴 [무기] 탭에서 플레이 중에 조절한다(정적 값 — 리로드하면 기본값으로). 맞는 값을 찾으면 기본값에 적는다.</para>
    /// </summary>
    public static class ProtoFireSync
    {
        public const string ShootState = "Shoot Rifle";
        static readonly int ShootHash = Animator.StringToHash(ShootState);

        /// 끄면 전처럼 트리거 순간에 낸다(비교용 — FX 툴 [무기] 탭).
        public static bool Enabled = true;

        /// 사격 클립의 정규화 시각(0~1). 이 시각을 지나야 낸다. 0 = 상태 전환(0.08초)이 끝나자마자 — 클립 90ms(프레임 2~3) 쯤.
        ///
        /// <para>2026-11-05 실측(클립 YAML 근육 곡선): 인게임 사격 클립은 Mixamo 원본이 아니라 Max 수정 자세를 구운
        /// <c>Firing_Rifle_edit</c>(1.167초, 30fps 35프레임, FireEditClipBuilder)이다. 0~267ms(프레임 0~8)는 거의 정지(왼팔이 당겨지고 고개가 숙는 준비),
        /// 반동(오른 어깨 뒤로 +0.11 · 오른손 들림 · 방아쇠 손가락 · 고개 숙임)은 267~367ms(프레임 8~11, 정점 300ms = 정규화 0.26),
        /// 그 뒤 900ms 까지 천천히 돌아온다. 그래서 0 이면 섬광이 반동보다 ~200ms(6프레임) 앞선다. 반동에 맞추려면 0.25
        /// (<see cref="MaxWait"/> 300ms 안에 드는 최대값) — 대신 트리거→발사가 0.3초 늦는다. 지연 없이 맞추려면 클립 머리 267ms 를 잘라야 한다.</para>
        public static float FireAt = 0.0f;

        /// 이 시간(초, 게임 시간) 안에 조건이 안 맞으면 그냥 낸다.
        public const float MaxWait = 0.3f;

        /// 몸 틀기가 목표에서 이 각도 안이면 "자세를 잡았다".
        public const float AimSettleDeg = 8f;

        /// 트리거가 전환을 일으키지 않은 것처럼 보일 때(드물다 — 컨트롤러가 다르거나 이미 같은 프레임에 전환이 끝났을 때) 이만큼 기다렸다 낸다.
        const float NoTransitionGrace = 0.12f;

        /// <summary>
        /// 줄 선 발사를 지금 내도 되나. 매 LateUpdate 에 부른다.
        /// <paramref name="sawTransition"/> 은 "트리거 뒤 사격 상태로의 전환을 봤다" 는 기억 — 호출 쪽이 줄 세울 때 false 로 두고 넘긴다.
        /// 연사 중에도 매 발 자기 자신으로 0.08초 전환하며 클립이 처음부터 다시 돌므로, 전환을 보고 그것이 끝난 뒤에 내야 반동과 맞는다
        /// (트리거 순간에 내면 섬광이 반동보다 0.08초 앞선다).
        /// </summary>
        public static bool ShouldRelease(Animator anim, bool aimSettled, float queuedAt, ref bool sawTransition)
        {
            if (!Enabled) return true;
            float waited = Time.time - queuedAt;
            if (waited >= MaxWait) return true;
            if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return aimSettled;   // 기다릴 애니가 없다

            if (AnyLayerTransitioningTo(anim)) { sawTransition = true; return false; }
            if (!aimSettled) return false;
            if (!HasShootState(anim)) return true;                       // 사격 상태가 어느 레이어에도 없는 컨트롤러(다른 캐릭터 · 테스트 리그)
            if (!sawTransition) return waited >= NoTransitionGrace;      // 전환을 못 봤다 — 잠깐 기다렸다 낸다
            return InShootPoseNow(anim);
        }

        /// 어느 레이어든 사격 상태에 있고 전환 중이 아니며 클립이 FireAt 를 지났나.
        static bool InShootPoseNow(Animator anim)
        {
            for (int l = 0; l < anim.layerCount; l++)
            {
                if (anim.IsInTransition(l)) continue;
                var st = anim.GetCurrentAnimatorStateInfo(l);
                if (st.shortNameHash != ShootHash) continue;
                float t = st.normalizedTime - Mathf.Floor(st.normalizedTime);
                if (t >= FireAt) return true;
            }
            return false;
        }

        /// 어느 레이어든 사격 상태에 있거나 그리로 가는 중인가.
        static bool HasShootState(Animator anim)
        {
            for (int l = 0; l < anim.layerCount; l++)
            {
                if (anim.GetCurrentAnimatorStateInfo(l).shortNameHash == ShootHash) return true;
                if (anim.IsInTransition(l) && anim.GetNextAnimatorStateInfo(l).shortNameHash == ShootHash) return true;
            }
            return false;
        }

        /// 어느 레이어든 사격 상태로 전환 중인가(Any State → Shoot Rifle 의 0.08초 — 자기 자신으로의 전환 포함).
        static bool AnyLayerTransitioningTo(Animator anim)
        {
            for (int l = 0; l < anim.layerCount; l++)
                if (anim.IsInTransition(l) && anim.GetNextAnimatorStateInfo(l).shortNameHash == ShootHash) return true;
            return false;
        }

        /// <summary>
        /// 지금 애니메이터가 사격 클립의 어디에 있는지 한 줄로 — "Shoot Rifle 92ms/1167ms · 프레임 2.8/35 · 정규화 0.08".
        /// 발사를 낸 LateUpdate 에서 불러 FX 툴 [무기] 탭 "지난 발" 에 보인다(어느 동작에서 총이 나가는지 눈으로 확인용).
        /// <paramref name="waited"/> 는 줄 선 시간 — 상한에 걸려 낸 발은 그렇다고 적는다.
        /// </summary>
        public static string Describe(Animator anim, float waited)
        {
            if (!Enabled) return "동기화 꺼짐 — 트리거 순간에 냄";
            if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return "애니메이터 없음";
            string capped = waited >= MaxWait ? " · 대기 상한에 걸려 냄" : "";
            for (int l = 0; l < anim.layerCount; l++)
            {
                bool inTr = anim.IsInTransition(l);
                bool next = inTr && anim.GetNextAnimatorStateInfo(l).shortNameHash == ShootHash;
                var info = next ? anim.GetNextAnimatorStateInfo(l) : anim.GetCurrentAnimatorStateInfo(l);
                if (info.shortNameHash != ShootHash) continue;
                var clips = next ? anim.GetNextAnimatorClipInfo(l) : anim.GetCurrentAnimatorClipInfo(l);
                float fps = clips.Length > 0 && clips[0].clip != null ? clips[0].clip.frameRate : 30f;
                float len = Mathf.Max(info.length, 1e-4f);
                float t = (info.normalizedTime - Mathf.Floor(info.normalizedTime)) * len;
                return $"{ShootState} {t * 1000f:0}ms/{len * 1000f:0}ms · 프레임 {t * fps:0.#}/{len * fps:0} · 정규화 {t / len:0.00}"
                     + (inTr ? " (전환 중)" : "") + capped;
            }
            var c0 = anim.GetCurrentAnimatorClipInfo(0);
            string clip0 = c0.Length > 0 && c0[0].clip != null ? c0[0].clip.name : "?";
            return $"사격 상태 아님 (레이어0 클립 {clip0}){capped}";
        }
    }
}
