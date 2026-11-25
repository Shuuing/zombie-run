using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 경험치 젬 모양 고르기 — 에셋 <c>Resources/Data/GemLook.asset</c> 의 <see cref="version"/> 하나로(UiTheme 과 같은 방식, 2026-11-18).
    /// <list type="bullet">
    /// <item><b>1 물고기</b> — 씬 <see cref="ProtoGemPool.gemSprites"/> 그림(열대어 · 고등어 · 상어)이 떨어져 파닥인다(그림 배열이 비면 예전 큐브).</item>
    /// <item><b>2 금색 물고기</b> — 코드로 짠 3D 물고기 메시(<see cref="GoldFishMesh"/>) + 툰 금색 셰이더(Proto/GoldFish).
    ///   물고기 그림과 같은 상자 규약이라 떨어지고 파닥이고 헤엄쳐 오는 움직임이 1 버전과 똑같다 — 그림만 금색 3D 물고기로 바뀐다.</item>
    /// </list>
    /// 메뉴 <c>Tools/좀비 오버런/젬 모양/</c> 에서 바꾼다(체크 표시가 지금 버전). 플레이 중에 바꾸면 그다음에 떨어지는 젬부터 바뀐다.
    /// 에셋이 없으면 1 물고기(예전 그대로). 반짝이 · 흡수 꼬리 · 흡수 반짝임은 두 버전이 같다.
    /// <para>처음엔 2 = 황금 주화였다가 같은 날 사용자 "동전은 아니고 그냥 금색 물고기로"로 바꿨다(주화는 _Backup/20261118_GoldCoin).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Proto/Gem Look", fileName = "GemLook")]
    public class ProtoGemLook : ScriptableObject
    {
        public const string ResourcePath = "Data/GemLook";

        public enum Version { Fish = 1, GoldFish = 2 }

        [Tooltip("1 물고기(그림) · 2 금색 물고기(3D). 메뉴 Tools/좀비 오버런/젬 모양/ 에서도 바꾼다")]
        public Version version = Version.GoldFish;

        [Header("2 금색 물고기")]
        [Tooltip("물고기 메시. 비면 GoldFishMesh 로 바로 만든다. 메뉴 '금색 물고기 메시 · 머티리얼 다시 만들기'가 채운다")]
        public Mesh goldFishMesh;
        [Tooltip("물고기 머티리얼(Proto/GoldFish). 비면 셰이더로 바로 만든다")]
        public Material goldFishMaterial;
        [Tooltip("물고기 키(m, 지느러미 포함). [0] 잡좀비(1XP) · [1] 2XP 이상 · [2] 보스급. 1 버전 그림과 같은 0.42 · 0.52 · 0.95")]
        public float[] goldFishSizes = { 0.42f, 0.52f, 0.95f };
        [Tooltip("길이 / 키. 메시는 가로 · 세로 1 상자에 짜여 있고 가로를 이만큼 늘린다. 2판(귀여운 통통한 몸)은 1.3 — 메뉴로 다시 구우면 GoldFishMesh.Aspect 로 맞춘다")]
        public float goldFishAspect = GoldFishMesh.Aspect;
        [Tooltip("두께 / 키. 1 이면 메시 그대로(몸 두께 = 키의 약 30%)")]
        public float goldFishThickness = 1f;

        static ProtoGemLook _loaded;
        static bool _tried;

        /// 에셋(없으면 null). 플레이 중 인스펙터에서 바꾼 값이 바로 읽힌다(같은 에셋 객체).
        public static ProtoGemLook Current
        {
            get
            {
                if (_loaded == null && !_tried) { _tried = true; _loaded = Resources.Load<ProtoGemLook>(ResourcePath); }
                return _loaded;
            }
        }

        /// 지금 버전. 에셋이 없으면 1 물고기.
        public static Version CurrentVersion => Current != null ? Current.version : Version.Fish;

        /// 에디터 메뉴가 버전을 바꾼 뒤 다시 읽게.
        public static void Reload() { _loaded = null; _tried = false; }

        Mesh _runtimeMesh;
        Material _runtimeMat;

        public Mesh GoldMesh
        {
            get
            {
                if (goldFishMesh != null) return goldFishMesh;
                if (_runtimeMesh == null) _runtimeMesh = GoldFishMesh.Build();
                return _runtimeMesh;
            }
        }

        public Material GoldMaterial
        {
            get
            {
                if (goldFishMaterial != null) return goldFishMaterial;
                if (_runtimeMat == null)
                {
                    var sh = Shader.Find("Proto/GoldFish");
                    if (sh != null) _runtimeMat = new Material(sh);
                    else
                    {
                        // 셰이더가 빠졌으면 단색 금 — 안 보이는 것보다 낫다(몸 휨 없음)
                        _runtimeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        _runtimeMat.SetColor("_BaseColor", new Color(1f, 0.74f, 0.18f, 1f));
                        Debug.LogWarning("[젬] Proto/GoldFish 셰이더를 못 찾았다 — 단색 금으로 대체한다.");
                    }
                }
                return _runtimeMat;
            }
        }

        /// 등급(0 작음 · 1 중간 · 2 큼)별 키(m).
        public float GoldFishSize(int tier)
            => goldFishSizes != null && goldFishSizes.Length > 0 ? goldFishSizes[Mathf.Clamp(tier, 0, goldFishSizes.Length - 1)] : 0.45f;
    }
}
