using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Proto
{
    /// <summary>
    /// "항상 위에" 그리는 효과 — 총구 화염·피격 불똥처럼 총이나 적 몸에 가려지면 안 되는 것(사용자 2026-11-05: "발사 이펙트가 총보다 밑에 있다 — 총 위로, 피격도 적 위로").
    ///
    /// <para><b>방법.</b> 전용 레이어 <see cref="LayerName"/> 를 메인 카메라의 URP 카메라 스택에 쌓은 <b>오버레이 카메라</b>(깊이 지움, 그 레이어만 컬링)가 따로 그린다.
    /// 깊이를 지우고 그리니 총·적·벽 무엇에도 가려지지 않고, 셰이더를 손댈 필요가 없다(팩 셰이더·URP 파티클·코드 그림 모두). 메인 카메라는 그 레이어를 빼고 그린다.
    /// 카탈로그 항목의 <c>onTop</c>(창의 "항상 위에")이 켜진 효과를 <see cref="ProtoFx"/> 가 낼 때마다 인스턴스 전체를 그 레이어에 놓는다(끄면 Default 로 되돌린다).</para>
    ///
    /// <para>오버레이 카메라는 플레이 중 처음 필요할 때 메인 카메라의 자식으로 만든다(씬에 저장하지 않는다). 메인 카메라가 바뀌면(씬 전환) 다시 만든다.
    /// 에디터 미리보기(플레이 아님)에선 만들지 않는다 — 레이어만 바꾸고 메인 카메라가 그대로 그린다(가려질 수 있다).
    /// 레이어가 없으면(TagManager — 에디터의 FxAssetsSetup 이 만든다) 경고 한 번 내고 평소대로 그린다.
    /// 카메라 스택은 Universal Renderer(포워드)에서만 — 메인 카메라가 오버레이거나 2D 렌더러면 건너뛴다.</para>
    /// </summary>
    public static class FxOverlay
    {
        public const string LayerName = "FxOverlay";
        public const string CameraName = "FxOverlayCamera";

        static int _layer = -2;
        static Camera _overlay, _base;
        static bool _warnedNoLayer, _warnedNoStack;

        /// 레이어 번호. 없으면 -1.
        public static int Layer
        {
            get
            {
                if (_layer == -2 || (_layer < 0 && Application.isEditor)) _layer = LayerMask.NameToLayer(LayerName);
                return _layer;
            }
        }

        public static bool Ready => _overlay != null && _base != null && _base == Camera.main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _overlay = null; _base = null; _warnedNoLayer = false; _warnedNoStack = false; _appliedLayer.Clear(); }

        // 풀에서 다시 나온 인스턴스는 레이어가 이미 맞다 — 처음(또는 레이어가 바뀔 때)만 자식까지 훑는다(2026-11-20 최적화: 예전엔 재생마다 전체를 재귀로 돌았다)
        static readonly System.Collections.Generic.Dictionary<GameObject, int> _appliedLayer = new System.Collections.Generic.Dictionary<GameObject, int>();

        /// 풀을 비울 때(ProtoFx.Clear) 같이 — 사라진 인스턴스를 잡고 있지 않게.
        public static void Forget() => _appliedLayer.Clear();

        /// 인스턴스 전체를 오버레이 레이어에(on) 또는 Default 로(off). on 이면 오버레이 카메라도 준비한다(플레이 중에만).
        public static void Apply(GameObject go, bool on)
        {
            if (go == null) return;
            int layer = on ? Layer : 0;
            if (on && layer < 0)
            {
                if (!_warnedNoLayer) { _warnedNoLayer = true; Debug.LogWarning($"[FX] 레이어 {LayerName} 가 없다 — 항상 위에 그리기를 못 한다(에디터의 FxAssetsSetup 이 만든다, 포커스 한 번 더)"); }
                return;
            }
            if (!_appliedLayer.TryGetValue(go, out int cur) || cur != layer)
            {
                SetLayer(go.transform, layer);
                _appliedLayer[go] = layer;
            }
            if (on && Application.isPlaying) Ensure();
        }

        static void SetLayer(Transform t, int layer)
        {
            if (t.gameObject.layer != layer) t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// 메인 카메라의 화각이 바뀌면 오버레이 카메라도 맞춘다. CopyFrom 은 만들 때 한 번뿐이라, 플레이 중에
        /// 화각을 바꾸는 쪽(ProtoCameraFollow 의 각도 프리셋 — 각도 3 은 60°, 나머지는 42°)이 매 프레임 부른다.
        /// 안 맞추면 이 레이어의 효과만 다른 화각으로 그려져 총구·피격 자리에서 어긋난다.
        public static void SyncFov(Camera main)
        {
            if (_overlay == null || _base != main || main == null) return;
            if (!Mathf.Approximately(_overlay.fieldOfView, main.fieldOfView)) _overlay.fieldOfView = main.fieldOfView;
        }

        /// 메인 카메라 위에 오버레이 카메라를 쌓는다. 이미 있으면 그대로.
        public static void Ensure()
        {
            var main = Camera.main;
            if (main == null) return;
            if (_overlay != null && _base == main) return;
            int layer = Layer;
            if (layer < 0) return;

            var baseData = main.GetUniversalAdditionalCameraData();
            if (baseData == null || baseData.renderType != CameraRenderType.Base || baseData.cameraStack == null)
            {
                if (!_warnedNoStack) { _warnedNoStack = true; Debug.LogWarning("[FX] 메인 카메라가 Base 가 아니거나 카메라 스택을 못 쓴다(렌더러) — 항상 위에 그리기를 못 한다"); }
                return;
            }

            // 이미 붙어 있는 것(같은 카메라에 두 번 만들지 않게)
            Camera cam = null;
            var old = main.transform.Find(CameraName);
            if (old != null) cam = old.GetComponent<Camera>();
            if (cam == null)
            {
                var go = new GameObject(CameraName);
                go.transform.SetParent(main.transform, false);
                cam = go.AddComponent<Camera>();
                cam.CopyFrom(main);                       // 투영·클리핑·뷰포트를 그대로 — 자식이라 자리·회전도 따라간다
                cam.targetTexture = main.targetTexture;
                cam.cullingMask = 1 << layer;
                cam.depth = main.depth + 1;
                cam.clearFlags = CameraClearFlags.Depth;
                cam.useOcclusionCulling = false;
                var od = cam.GetUniversalAdditionalCameraData();
                od.renderType = CameraRenderType.Overlay;
                // 핵심 — 깊이를 지우고 그리니 아무것에도 가려지지 않는다. clearDepth 는 읽기 전용(기본 true)이라 혹시 false 면 리플렉션으로 켠다
                if (!od.clearDepth)
                {
                    var f = typeof(UniversalAdditionalCameraData).GetField("m_ClearDepth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (f != null) f.SetValue(od, true);
                    else Debug.LogWarning("[FX] 오버레이 카메라의 Clear Depth 를 못 켰다 — 총구·피격이 여전히 가려질 수 있다");
                }
                od.renderShadows = false;
                od.requiresDepthOption = CameraOverrideOption.Off;
                od.requiresColorOption = CameraOverrideOption.Off;
                od.renderPostProcessing = baseData.renderPostProcessing;
                od.antialiasing = baseData.antialiasing;
                od.volumeLayerMask = baseData.volumeLayerMask;
                od.volumeTrigger = baseData.volumeTrigger;
            }
            if (!baseData.cameraStack.Contains(cam)) baseData.cameraStack.Add(cam);
            main.cullingMask &= ~(1 << layer);            // 메인은 이 레이어를 안 그린다(두 번 그리지 않게)
            _overlay = cam;
            _base = main;
        }
    }
}
