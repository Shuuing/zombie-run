using UnityEngine;

namespace Proto
{
    /// 프롭 계층(_Props) 밖에 있는 세트피스에 손으로 놓는 충돌 볼륨.
    ///
    /// ProtoObstacles 는 _Props 하위 프롭마다 렌더러 바운즈로 OBB 를 자동 생성하지만,
    /// 호수 디오라마처럼 "메시 바운즈 = 막고 싶은 영역"이 아닌 오브젝트에는 그게 통하지 않는다.
    /// (디오라마 전체 바운즈는 11.3×11.7 이라 절벽 뒤 빈 공간까지 통째로 막아버린다.)
    /// 그래서 막을 부분만 이 컴포넌트로 명시한다.
    ///
    /// 좌표는 이 트랜스폼 기준 로컬. 부모가 회전·이동해도 같이 따라간다.
    public class ProtoBlocker : MonoBehaviour
    {
        public enum Shape
        {
            Box,        // Y축 회전 사각형. 절벽·벽처럼 직선인 것
            Ellipse,    // 타원. 호수 림처럼 둥근 것 — 사각형으로 덮으면 모서리에 보이지 않는 벽이 생긴다
        }

        public Shape shape = Shape.Box;

        [Tooltip("로컬 XZ 반경. lossyScale 이 곱해진다.")]
        public Vector2 halfExtents = new Vector2(1f, 1f);

        [Tooltip("로컬 XZ 중심 오프셋.")]
        public Vector2 center = Vector2.zero;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f);
            Vector3 c = transform.TransformPoint(new Vector3(center.x, 0f, center.y));
            Gizmos.matrix = Matrix4x4.TRS(new Vector3(c.x, 0.1f, c.z),
                                          Quaternion.Euler(0f, transform.eulerAngles.y, 0f),
                                          Vector3.one);
            var s = transform.lossyScale;
            float hx = halfExtents.x * Mathf.Abs(s.x);
            float hz = halfExtents.y * Mathf.Abs(s.z);

            if (shape == Shape.Box)
            {
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(hx * 2f, 0.2f, hz * 2f));
            }
            else
            {
                const int N = 40;
                Vector3 prev = new Vector3(hx, 0f, 0f);
                for (int i = 1; i <= N; i++)
                {
                    float a = i / (float)N * Mathf.PI * 2f;
                    var p = new Vector3(Mathf.Cos(a) * hx, 0f, Mathf.Sin(a) * hz);
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
