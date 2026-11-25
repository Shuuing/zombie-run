using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 머리카락 흔들림 — 이름이 <see cref="bonePrefix"/> 로 시작하는 뼈 사슬에 관성 스프링(VRM SpringBone 방식의 간단판).
    /// 2026-10-21 사용자 요청: 새 캐릭터(cha_02, 브레이커)의 긴 머리에만. 뼈는 Blender 에서 Head 아래에 넣었다
    /// (ArtSource/character_02/cha_02_rig.blend — 뒷머리 3줄기×3, 옆머리 2줄기×2).
    ///
    /// <para>애니메이션 클립이 아니라 스크립트인 이유: Humanoid 클립은 사람 뼈만 움직여 추가 뼈에 닿지 않는다.
    /// 스크립트면 걷기·달리기·피격·교체 연출 어디서나 저절로 반응한다.</para>
    ///
    /// <para><b>끄면(enabled=false) 머리카락 뼈가 머리에 고정된 원래 모습이다.</b>
    /// 메뉴 Tools/좀비 오버런/브레이커 머리 흔들림 으로 켜고 끈다. 게임 시간으로 돈다 — 레벨업 정지 중엔 멈춘다.</para>
    ///
    /// <para>수치는 캐릭터 키 1m(스케일 1) 기준이다. 실제 크기(Visual 1.6배)는 lossyScale 로 곱한다.</para>
    /// </summary>
    public class ProtoHairSpring : MonoBehaviour
    {
        [Tooltip("흔들 뼈 이름 접두사")] public string bonePrefix = "hair_";
        [Tooltip("원래 자세로 돌아가려는 힘. 크면 뻣뻣하다")] [Range(0f, 4f)] public float stiffness = 1.2f;
        [Tooltip("감쇠. 크면 출렁임이 빨리 가라앉는다")] [Range(0f, 1f)] public float drag = 0.35f;
        [Tooltip("아래로 당기는 힘 (m/s²)")] public float gravity = 0.6f;
        [Tooltip("몸통 움직임에 머리카락이 남는 정도. 1 이면 그대로, 크면 과장 — 위에서 보는 카메라라 조금 과장한다")]
        [Range(0f, 3f)] public float inertia = 1.4f;
        [Tooltip("머리카락 끝 충돌 반경 (m)")] public float hairRadius = 0.02f;

        [Header("충돌 구 — 머리·가슴·엉덩이 (키 1m 기준)")]
        public float headRadius = 0.17f;
        [Tooltip("머리 뼈에서 머리 중심까지 (캐릭터 루트 기준 축)")] public Vector3 headOffset = new Vector3(0f, 0.14f, 0f);
        public float chestRadius = 0.11f;
        public float hipsRadius = 0.11f;

        class Joint
        {
            public Transform t;
            public Quaternion restLocal;
            public Vector3 axis;      // 로컬, 다음 마디(끝) 방향
            public float length;      // 키 1m 기준
            public Vector3 tip, prevTip;
            public Quaternion lastLocal;
        }

        readonly List<Joint> _joints = new List<Joint>();   // 부모가 먼저 오게 정렬
        Transform _head, _chest, _hips;
        Vector3 _lastRoot;
        bool _ready;

        void OnEnable() => Build();

        void OnDisable()
        {
            // 원래 자세로 — 끄면 머리에 고정된 모습
            foreach (var j in _joints) if (j.t != null) j.t.localRotation = j.restLocal;
        }

        void Build()
        {
            _joints.Clear();
            _ready = false;
            var anim = GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                _head = anim.GetBoneTransform(HumanBodyBones.Head);
                _chest = anim.GetBoneTransform(HumanBodyBones.UpperChest);
                if (_chest == null) _chest = anim.GetBoneTransform(HumanBodyBones.Chest);
                _hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            }
            float s = Scale();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith(bonePrefix)) continue;
                if (t.parent != null && t.parent.name.StartsWith(bonePrefix)) continue;   // 사슬 뿌리만
                AddChain(t, s);
            }
            _lastRoot = transform.position;
            _ready = _joints.Count > 0;
        }

        void AddChain(Transform root, float s)
        {
            Transform t = root;
            Vector3 lastAxis = Vector3.up; float lastLen = 0.05f;
            while (t != null)
            {
                Transform next = null;
                foreach (Transform c in t) if (c.name.StartsWith(bonePrefix)) { next = c; break; }
                var j = new Joint { t = t, restLocal = t.localRotation };
                if (next != null)
                {
                    Vector3 local = next.localPosition;
                    j.axis = local.normalized;
                    j.length = Vector3.Scale(local, t.lossyScale).magnitude / s;
                }
                else { j.axis = lastAxis; j.length = lastLen; }   // 끝 마디 — 앞 마디 길이를 이어 쓴다
                lastAxis = j.axis; lastLen = j.length;
                j.tip = j.prevTip = t.position + t.rotation * j.axis * (j.length * s);
                j.lastLocal = j.restLocal;
                _joints.Add(j);
                t = next;
            }
        }

        float Scale() => Mathf.Max(0.0001f, transform.lossyScale.y);

        void LateUpdate()
        {
            if (!_ready) return;
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                // 정지 중 — 마지막 모양을 그대로 유지(애니메이터가 안 건드리므로 다시 쓸 필요는 없지만 안전하게)
                foreach (var j in _joints) j.t.localRotation = j.lastLocal;
                return;
            }
            dt = Mathf.Min(dt, 0.05f);
            float s = Scale();

            // 순간이동(재시작·교체 연출) — 끌려가며 휘날리지 않게 바로 원래 자리로
            if ((transform.position - _lastRoot).sqrMagnitude > 1f) ResetTips(s);
            _lastRoot = transform.position;

            // 머리 중심 — 머리 뼈 축 방향은 리그마다 달라서 캐릭터 루트 기준으로 올린다
            Vector3 headC = _head != null ? _head.position + transform.rotation * (headOffset * s) : Vector3.zero;
            foreach (var j in _joints)
            {
                j.t.localRotation = j.restLocal;   // 부모가 먼저 풀렸으므로 지금 회전 = 원래 자세가 따라간 자리
                Vector3 pos = j.t.position;
                float len = j.length * s;
                Vector3 restDir = j.t.rotation * j.axis;

                // Verlet: 관성 + 원래 방향으로 돌아가는 힘 + 중력
                Vector3 vel = (j.tip - j.prevTip) * (1f - drag);
                Vector3 next = j.tip + vel * inertia
                             + restDir * (stiffness * len * dt * 10f)
                             + Vector3.down * (gravity * s * dt * dt);
                // 길이 유지
                next = pos + (next - pos).normalized * len;
                // 충돌 — 머리·가슴·엉덩이를 뚫지 않게 밀어낸다
                float hr = hairRadius * s;
                if (_head != null) next = Push(next, headC, headRadius * s + hr, pos, len);
                if (_chest != null) next = Push(next, _chest.position, chestRadius * s + hr, pos, len);
                if (_hips != null) next = Push(next, _hips.position, hipsRadius * s + hr, pos, len);

                j.prevTip = j.tip; j.tip = next;
                Quaternion turn = Quaternion.FromToRotation(restDir, next - pos);
                j.t.rotation = turn * j.t.rotation;
                j.lastLocal = j.t.localRotation;
            }
        }

        static Vector3 Push(Vector3 p, Vector3 c, float r, Vector3 root, float len)
        {
            Vector3 d = p - c;
            float m = d.magnitude;
            if (m >= r || m < 1e-5f) return p;
            p = c + d / m * r;
            return root + (p - root).normalized * len;
        }

        void ResetTips(float s)
        {
            foreach (var j in _joints)
            {
                j.t.localRotation = j.restLocal;
            }
            foreach (var j in _joints)
            {
                j.tip = j.prevTip = j.t.position + j.t.rotation * j.axis * (j.length * s);
                j.lastLocal = j.restLocal;
            }
        }
    }
}
