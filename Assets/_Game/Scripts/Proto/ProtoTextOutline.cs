using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// 글자 외곽선 — 8방향(두꺼우면 안쪽 고리를 한 겹 더) 겹쳐 찍는다.
    /// 11-17 사용자 "레벨 표시나 다른 ui에 텍스트가 좀 깨지는것같아. 선명하게" — 유니티 기본 Outline 은 대각선 4방향만 찍어서
    /// 3~4px 두께에선 위아래 · 좌우가 비어 테두리가 계단처럼 깨졌다. 색 · 두께 · 글자 알파 따르기는 Shadow 값 그대로(effectColor · effectDistance · useGraphicAlpha).
    /// 탕탕 판 UI 프리팹(TtZombieFit 조립)의 Outline 을 이걸로 바꿔 둔다.
    [AddComponentMenu("UI/Effects/Proto Text Outline (8방향)")]
    public class ProtoTextOutline : Shadow
    {
        static readonly List<UIVertex> _verts = new List<UIVertex>(1024);
        static readonly Vector2[] Dir8 =
        {
            new Vector2(1f, 0f), new Vector2(0.7071f, 0.7071f), new Vector2(0f, 1f), new Vector2(-0.7071f, 0.7071f),
            new Vector2(-1f, 0f), new Vector2(-0.7071f, -0.7071f), new Vector2(0f, -1f), new Vector2(0.7071f, -0.7071f),
        };

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            _verts.Clear();
            vh.GetUIVertexStream(_verts);
            int n = _verts.Count;
            float d = Mathf.Max(Mathf.Abs(effectDistance.x), Mathf.Abs(effectDistance.y));
            if (d <= 0.01f) return;
            int rings = d > 2.2f ? 2 : 1;   // 굵으면 반지름 절반 고리를 더해 8방향 사이 틈을 메운다
            int need = n * (1 + 8 * rings);
            if (_verts.Capacity < need) _verts.Capacity = need;
            int start = 0, end = n;
            for (int ring = 0; ring < rings; ring++)
            {
                float r = ring == 0 ? d : d * 0.55f;
                for (int k = 0; k < 8; k++)
                {
                    // 마지막에 붙은 원본 묶음을 외곽선 색으로 바꾸고 원본을 다시 뒤에 붙인다(원본이 맨 위)
                    ApplyShadowZeroAlloc(_verts, effectColor, start, _verts.Count, Dir8[k].x * r, Dir8[k].y * r);
                    start = end; end = _verts.Count;
                }
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }
    }
}
