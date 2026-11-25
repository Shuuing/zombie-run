using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 화상 사망 — 불타 죽은 좀비가 <b>타들어가며 사라진다</b>(2026-11-10 사용자: "불타 죽는 좀비는 디졸브로 타들어가는 느낌으로.
    /// 경계라인은 add 로 넣어주고 불씨가 튀기고 불꽃도 사그라드는 느낌으로"). 그 전엔 0.55초 동안 새까매진 채 쓰러져 2.2초 누워 있었다.
    ///
    /// <para><b>나눠 맡는다.</b>
    /// 여기 = 머티리얼(본체 디졸브 + 가산 불선)과 조절 값. 몸의 머티리얼을 갈아끼우고 진행도를 미는 것은 <see cref="ProtoZombie"/>(풀 재사용 때 되돌려야 해서),
    /// 불씨와 사그라드는 불꽃은 <see cref="ProtoBurnFx"/>(타는 몸의 불을 이미 한 시스템으로 그리고 있다).</para>
    ///
    /// <para><b>머티리얼 둘.</b> 본체 <c>Resources/VFX/M_BurnDissolve</c>(셰이더 ZombieOverrun/BurnDissolve, 큐는 몸과 같게)는 좀비 머티리얼마다 복제해
    /// 알베도 · 하이라이트 · 그림자 색을 옮긴다(<see cref="ZombieGibs"/> 의 조각 머티리얼과 같은 방식). 불선 <c>Resources/VFX/M_BurnEdge</c>(ZombieOverrun/BurnEdge, 가산, 큐 3100)는
    /// 렌더러의 <b>마지막 머티리얼로 덧붙인다</b> — 서브메시보다 머티리얼이 많으면 Unity 가 마지막 서브메시를 그 머티리얼로 한 번 더 그린다.
    /// 한 셰이더의 두 번째 패스로 하지 않은 이유: 밤 어둠 막(큐 2900)보다 뒤에 그려야 어둠 속에서 빛나는데, 큐는 머티리얼 단위다.
    /// 서브메시가 둘 이상인 모델은 마지막 서브메시에만 선이 얹힌다(지금 적 모델은 모두 하나).</para>
    ///
    /// <para>둘 다 에디터의 ZombieBurnOutSetup 이 만든다. 본체가 없으면 <see cref="Ready"/> 가 false — 옛 연출(새까매지며 쓰러진다)로 떨어진다. 불선만 없으면 선 없이 타들어간다.</para>
    ///
    /// <para>값은 정적이라 플레이 중 바로 먹고, 리로드하면 기본값으로 돌아간다. 맞는 값을 찾으면 여기 기본값에 적는다.</para>
    /// </summary>
    public static class ZombieBurnOut
    {
        public static bool Enabled = true;
        /// 타들어가 다 사라지기까지(초). 끝나면 곧바로 풀로 돌아간다.
        public static float Duration = 1.15f;
        /// 타는 동안 사망 애니의 빠르기. 무늬가 월드 좌표라 몸이 빨리 움직이면 무늬가 몸 위를 흐른다 — 천천히 무너지며 타게 한다. 1 이면 원래 속도.
        public static float AnimSpeed = 0.2f;
        /// 무늬 크기(1/m, 덩치 1 기준). 4.5 면 무늬 한 칸이 약 22cm — 불선이 들쭉날쭉한 한 줄로 읽힌다. 올리면 잘게 부서지고, 내리면 뭉툭해진다.
        public static float NoiseScale = 4.5f;
        /// 불선 띠의 폭(타는 순서 값 기준). 0.08 이면 몸 높이의 약 1/9 — 게임 카메라에서 8px 안팎의 밝은 띠. 본체의 그을음 띠가 이 바깥에서 시작한다.
        /// 0.16(첫 값)은 윗몸 전체가 용암처럼 빛나 "선"이 아니었고, 0.06 은 화면에서 너무 가늘었다(임시 프로젝트 렌더로 비교, 11-10).
        public static float EdgeWidth = 0.08f;
        /// 위에서 아래로 타 내려오는 정도(0 = 노이즈만, 1 = 일직선). 0.7 = 머리부터 타 내려와 마지막 불이 발밑에서 꺼진다. 0.55 는 불선이 몸 여기저기 흩어졌다.
        public static float HeightBias = 0.7f;

        const string BodyPath = "VFX/M_BurnDissolve";   // Resources 아래 — ZombieBurnOutSetup 이 만든다
        const string EdgePath = "VFX/M_BurnEdge";

        static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
        static readonly int HColorID = Shader.PropertyToID("_HColor");
        static readonly int SColorID = Shader.PropertyToID("_SColor");

        static Material _bodyBase, _edge;
        static bool _tried, _warned;
        static readonly Dictionary<Material, Material> _bodyMats = new Dictionary<Material, Material>();   // 좀비 머티리얼 → 타는 본체 복제본

        /// 본체 디졸브 머티리얼을 찾았나. 에디터에선 못 찾았으면 매번 다시 찾는다 — ZombieBurnOutSetup 이 뒤늦게 만들 수 있다.
        public static bool Ready
        {
            get
            {
                if (_bodyBase == null && Application.isEditor) _tried = false;
                Load();
                return _bodyBase != null;
            }
        }

        static void Load()
        {
            if (_tried) return;
            _tried = true;
            _bodyBase = Resources.Load<Material>(BodyPath);
            _edge = Resources.Load<Material>(EdgePath);
            if (_warned) return;
            if (_bodyBase == null) { _warned = true; Debug.LogWarning($"[화상 사망] Resources/{BodyPath} 가 없다 — 타들어가는 대신 옛 연출(새까매지며 쓰러진다)로 나온다(에디터에선 ZombieBurnOutSetup 이 만든다, 다음 포커스 때)"); }
            else if (_edge == null) { _warned = true; Debug.LogWarning($"[화상 사망] Resources/{EdgePath} 가 없다 — 불선(가산) 없이 타들어간다(에디터에선 ZombieBurnOutSetup 이 만든다, 다음 포커스 때)"); }
        }

        /// <summary>
        /// 렌더러의 지금 머티리얼(src)에 맞는 타는 머티리얼 묶음 — 자리마다 본체 복제본, 맨 끝에 불선 하나.
        /// 좀비 몸(툰: _HColor 가 있다)이 하나도 없으면 null — 그 렌더러는 손대지 않는다(손의 구슬 · 물 파문 같은 것).
        /// </summary>
        public static Material[] Materials(Material[] src)
        {
            if (!Enabled || src == null || src.Length == 0 || !Ready) return null;
            bool any = false;
            for (int i = 0; i < src.Length; i++) if (src[i] != null && src[i].HasProperty(HColorID)) { any = true; break; }
            if (!any) return null;

            var dst = new Material[src.Length + (_edge != null ? 1 : 0)];
            for (int i = 0; i < src.Length; i++)
            {
                var m = src[i];
                if (m == null || !m.HasProperty(HColorID)) { dst[i] = m; continue; }
                if (!_bodyMats.TryGetValue(m, out var made) || made == null)
                {
                    made = new Material(_bodyBase) { name = m.name + "_burn" };
                    var tex = m.HasProperty(BaseMapID) ? m.GetTexture(BaseMapID) : m.mainTexture;
                    if (tex != null) made.SetTexture(BaseMapID, tex);
                    if (m.HasProperty(BaseColorID)) made.SetColor(BaseColorID, m.GetColor(BaseColorID));
                    made.SetColor(HColorID, m.GetColor(HColorID));
                    if (m.HasProperty(SColorID)) made.SetColor(SColorID, m.GetColor(SColorID));
                    made.renderQueue = m.renderQueue;   // 몸과 같은 자리(밤 어둠 막 앞) — 타는 동안에도 밤이면 같이 어둡다. 빛나는 건 불선이 맡는다
                    _bodyMats[m] = made;
                }
                dst[i] = made;
            }
            if (_edge != null) dst[src.Length] = _edge;
            return dst;
        }

        /// <summary>
        /// 진행도 k(0~1)일 때 불선이 지나는 높이(1 = 머리, 0 = 발밑) — 셰이더(BurnDissolveCommon.hlsl)의 식을 노이즈 평균(0.5)으로 푼 것.
        /// <see cref="ProtoBurnFx"/> 가 불씨를 이 높이에서 튀기고 불꽃을 이 아래로만 낸다. 셰이더의 식을 고치면 여기도.
        /// </summary>
        public static float FrontHeight01(float k)
        {
            float d = k * 1.04f - 0.02f;
            float b = Mathf.Max(0.05f, HeightBias);
            return Mathf.Clamp01(1f - (d - 0.5f * (1f - b)) / b);
        }

        // 복제본은 좀비 머티리얼 가짓수만큼(열 개 안쪽)이라 판이 끝날 때까지 들고 있는다 — 타는 중인 몸이 쓰고 있을 수 있어 중간에 버리지 않는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _bodyMats.Clear(); _bodyBase = null; _edge = null; _tried = false; _warned = false;
        }
    }
}
