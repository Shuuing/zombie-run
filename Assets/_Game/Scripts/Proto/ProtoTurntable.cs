using UnityEngine;

namespace Proto
{
    /// 제자리 회전(턴테이블) — 캐릭터·프롭을 돌려 가며 화면 녹화할 때 (사용자 요청 2026-10-28).
    ///
    /// 플레이 없이 Scene 뷰에서도 돈다. 편집 중엔 Update 가 저절로 불리지 않으므로 EditorApplication.update 로 굴리고 뷰를 다시 그린다.
    /// 끄거나(체크 해제·컴포넌트 제거) 멈추면 원래 방향으로 돌아간다 — 씬에 돌아간 각도가 남지 않게.
    [ExecuteAlways]
    public class ProtoTurntable : MonoBehaviour
    {
        [Tooltip("초당 회전 각도. 30 이면 12초에 한 바퀴. 음수면 반대 방향.")]
        public float degreesPerSecond = 30f;

        [Tooltip("끄면 그 자리에 멈춘다(원래 방향으로 돌아가지 않음).")]
        public bool spinning = true;

        Quaternion _startRot;
        bool _hasStart;
        double _last;

        void OnEnable()
        {
            _startRot = transform.localRotation;
            _hasStart = true;
            _last = Now;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.update += EditorTick;
                UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            }
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
#endif
            if (_hasStart) transform.localRotation = _startRot;
        }

        void Update()
        {
            if (Application.isPlaying) Spin();
        }

#if UNITY_EDITOR
        /// 도는 도중에 씬을 저장해도 원래 방향으로 저장되게 — 저장 직전에 되돌리고, 다음 틱부터 다시 돈다.
        void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (this != null && _hasStart && scene == gameObject.scene) transform.localRotation = _startRot;
        }

        void EditorTick()
        {
            if (this == null) return;
            Spin();
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();   // Game 뷰도 같이 갱신
        }
#endif

        void Spin()
        {
            double now = Now;
            float dt = Mathf.Min((float)(now - _last), 0.1f);   // 에디터가 잠깐 멈췄다 와도 튀지 않게
            _last = now;
            if (!spinning) return;
            transform.Rotate(0f, degreesPerSecond * dt, 0f, Space.World);
        }

        static double Now =>
#if UNITY_EDITOR
            Application.isPlaying ? Time.realtimeSinceStartupAsDouble : UnityEditor.EditorApplication.timeSinceStartup;
#else
            Time.realtimeSinceStartupAsDouble;
#endif

        /// 원래 방향으로 — 컴포넌트 ⋮ 메뉴
        [ContextMenu("원래 방향으로")]
        void ResetRotation() { if (_hasStart) transform.localRotation = _startRot; }
    }
}
