using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 탱크 파괴 시 부품이 흩어지는 연출 — 프로토 한정 (<see cref="ProtoTank"/> 가 파괴될 때 붙인다).
    ///
    /// <para>tank_01.fbx 는 부품이 메시 10개로 나뉘어 있다. 그런데 <b>피벗이 전부 모델 원점</b>이라 그대로 돌리면
    /// 탱크 중심을 축으로 크게 원을 그린다(인트로 비행기 프로펠러와 같은 문제). 그래서 부품마다 바운드 중심에
    /// 빈 축을 끼워 넣고 그 축을 굴린다.</para>
    ///
    /// <para>Unity 물리는 안 쓴다(GDD §16). 중력 + 바닥 반사 + 마찰을 직접 계산한다 — 부품이 열 개뿐이고,
    /// 바닥 판정은 렌더러 월드 바운드의 최저점(y)으로 충분하다. 다 멈추면 컴포넌트가 스스로 꺼진다.</para>
    ///
    /// 묶음 (2026-10-08 바운드 실측):
    /// <list type="bullet">
    /// <item>차체 — tank_04(몸통)·07/08(양쪽 궤도+바퀴, 한 메시에 양쪽이 같이 들어 있다)·09(앞 판). 같이 살짝 뜬다.</item>
    /// <item>포탑 — tank_01. 위로 높이 솟구치며 돈다(가장 눈에 띄는 한 방).</item>
    /// <item>포신 tank_03 · 해치/안테나 05·06 · 옆 상자 02 · 라이트 10 — 작은 조각, 멀리 튄다.</item>
    /// </list>
    /// </summary>
    public class ProtoTankDebris : MonoBehaviour
    {
        const float Gravity     = 20f;
        const float Bounce      = 0.32f;   // 바닥에 닿을 때 세로 속도 보존율
        const float GroundFric  = 0.55f;   // 닿을 때 가로 속도 보존율
        const float SpinFric    = 0.5f;
        const float RestSpeed   = 0.35f;
        const float GroundY     = 0f;

        static readonly HashSet<string> Hull = new HashSet<string> { "tank_04", "tank_07", "tank_08", "tank_09" };

        class Piece
        {
            public Transform t;
            public Renderer[] rs;
            public Vector3 vel, spin;      // spin = 도/초, 월드 축
            public bool resting;
        }

        readonly List<Piece> _pieces = new List<Piece>();

        /// <summary>root 아래 렌더러를 떼어 흩뿌린다. exclude 아래(HP 바 등)는 건드리지 않는다.
        /// hull 을 주면 탱크 차체 대신 그 이름의 메시들을 한 덩어리로 묶는다(헬기 추락 — <see cref="ProtoHeliCrash"/>).</summary>
        public static ProtoTankDebris Shatter(Transform root, Transform exclude, Vector3 center, HashSet<string> hull = null)
        {
            var d = root.gameObject.AddComponent<ProtoTankDebris>();
            d.Build(root, exclude, center, hull ?? Hull);
            return d;
        }

        void Build(Transform root, Transform exclude, Vector3 center, HashSet<string> hull)
        {
            var hullRs = new List<Renderer>();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                if (exclude != null && r.transform.IsChildOf(exclude)) continue;
                if (hull.Contains(r.name)) { hullRs.Add(r); continue; }
                AddPiece(root, new[] { r }, center);
            }
            if (hullRs.Count > 0) AddPiece(root, hullRs.ToArray(), center);
        }

        void AddPiece(Transform root, Renderer[] rs, Vector3 center)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

            var pivot = new GameObject(rs[0].name + "_Piece").transform;
            pivot.SetParent(root, false);
            pivot.position = b.center;
            pivot.rotation = Quaternion.identity;
            foreach (var r in rs) r.transform.SetParent(pivot, true);

            Vector3 away = b.center - center; away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized
                 : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;

            var p = new Piece { t = pivot, rs = rs };
            string n = rs.Length > 1 ? "hull" : rs[0].name;
            Vector3 randAxis = Random.onUnitSphere;
            switch (n)
            {
                case "hull":     // 무겁다 — 살짝 들렸다 기울며 내려앉는다
                    p.vel  = Vector3.up * 3.2f + away * 0.3f;
                    p.spin = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * Random.Range(25f, 45f);
                    break;
                case "tank_01":  // 포탑 — 높이 솟구치며 돈다
                    p.vel  = Vector3.up * Random.Range(10f, 12f) + Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away * Random.Range(1.5f, 2.5f);
                    p.spin = randAxis * Random.Range(160f, 260f);
                    break;
                default:         // 작은 조각 — 멀리 튄다
                    p.vel  = Vector3.up * Random.Range(6f, 9f) + away * Random.Range(3.5f, 6.5f);
                    p.spin = randAxis * Random.Range(300f, 700f);
                    break;
            }
            _pieces.Add(p);
        }

        void Update()
        {
            float dt = ProtoConfig.Delta;
            if (dt <= 0f) return;   // 레벨업 정지 중

            bool any = false;
            foreach (var p in _pieces)
            {
                if (p.resting || p.t == null) continue;
                any = true;

                p.vel.y -= Gravity * dt;
                p.t.position += p.vel * dt;
                if (p.spin.sqrMagnitude > 0.01f)
                    p.t.rotation = Quaternion.AngleAxis(p.spin.magnitude * dt, p.spin.normalized) * p.t.rotation;

                // 바닥 — 렌더러 월드 바운드의 최저점이 땅을 뚫으면 올려 놓고 튕긴다
                float minY = float.MaxValue;
                foreach (var r in p.rs) if (r != null) minY = Mathf.Min(minY, r.bounds.min.y);
                if (minY < GroundY)
                {
                    p.t.position += Vector3.up * (GroundY - minY);
                    if (p.vel.y < 0f) p.vel.y = -p.vel.y * Bounce;
                    p.vel.x *= GroundFric; p.vel.z *= GroundFric;
                    p.spin *= SpinFric;
                    if (p.vel.magnitude < RestSpeed) { p.vel = Vector3.zero; p.spin = Vector3.zero; p.resting = true; }
                }
            }
            if (!any) enabled = false;
        }
    }
}
