using UnityEngine;

namespace Proto
{
    /// 간판 스프라이트 애니메이션 — 격자 스프라이트 시트를 발광 텍스처(_EmissionMap)로 넣고 프레임마다 _EmissionMap_ST 를 옮긴다.
    /// 간판 메시는 04_city_03_icon 아틀라스의 한 칸(uvMin~uvMax)만 쓰므로, 그 칸이 한 프레임을 꽉 채우도록 배율·오프셋을 계산한다.
    /// 공유 머티리얼은 그대로 두고 MPB 로만 바꾼다. 에디터에서도 돌아 배치하면서 바로 보인다.
    [ExecuteAlways]
    public class ProtoSpriteSign : MonoBehaviour
    {
        public Renderer target;
        [Tooltip("간판 메시가 쓰는 UV 칸(아틀라스 안 사각형).")]
        public Vector2 uvMin, uvMax = Vector2.one;
        public int columns = 5, rows = 5;
        [Tooltip("0 이면 columns × rows 전부.")]
        public int frameCount = 0;
        public float fps = 12f;
        [Tooltip("프레임 가로 중 보여 줄 비율(가운데 기준). 간판이 프레임보다 홀쭉할 때 찌그러지지 않게 양옆을 자른다.")]
        [Range(0.1f, 1f)] public float cropX = 1f;
        [Range(0.1f, 1f)] public float cropY = 1f;
        [Tooltip("잘라 낸 창의 위치(프레임 안, 0~1). 가로로 긴 간판은 y 를 1 쪽으로 — 얼굴이 보이게.")]
        public Vector2 cropAnchor = new Vector2(0.5f, 0.5f);
        [Tooltip("시작 프레임을 밀어 둔다 — 같은 시트를 쓰는 간판끼리 똑같이 움직이지 않게.")]
        public int frameOffset = 0;

        static readonly int IdST = Shader.PropertyToID("_EmissionMap_ST");
        MaterialPropertyBlock _mpb;
        int _last = -1;

        // 켜지자마자 한 번 맞춘다 — 스크립트 다시 불러오기 뒤엔 MPB 가 날아가 시트 전체가 반복돼 보인다
        void OnEnable() { _mpb ??= new MaterialPropertyBlock(); _last = -1; Update(); }

        void Update()
        {
            if (target == null || columns < 1 || rows < 1) return;
            int count = frameCount > 0 ? Mathf.Min(frameCount, columns * rows) : columns * rows;
            float t = Application.isPlaying ? Time.time : (float)UnityEditorTime();
            int f = (Mathf.FloorToInt(t * fps) + frameOffset) % count;
            if (f == _last) return;
            _last = f;

            // 시트 왼쪽 위부터 오른쪽으로, 줄은 아래로(텍스처 v 는 아래가 0)
            int col = f % columns, row = f / columns;
            Vector2 size = new Vector2(1f / columns, 1f / rows);
            Vector2 view = new Vector2(size.x * cropX, size.y * cropY);
            Vector2 min = new Vector2(col * size.x + (size.x - view.x) * cropAnchor.x,
                                      1f - (row + 1) * size.y + (size.y - view.y) * cropAnchor.y);
            Vector2 span = uvMax - uvMin;
            Vector2 tiling = new Vector2(view.x / span.x, view.y / span.y);
            Vector2 offset = min - Vector2.Scale(uvMin, tiling);

            target.GetPropertyBlock(_mpb);
            _mpb.SetVector(IdST, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
            target.SetPropertyBlock(_mpb);
        }

        static double UnityEditorTime()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }

#if UNITY_EDITOR
        void OnValidate() => _last = -1;
        // 에디터에서 멈춰 있을 때도 프레임이 넘어가게 씬 뷰를 계속 다시 그린다
        void OnDrawGizmos() { if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate(); }
#endif

        void OnDisable() { if (target != null) target.SetPropertyBlock(null); }
    }
}
