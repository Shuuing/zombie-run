using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace ZombieOverrun.EditorTools
{
    /// <summary>
    /// 앱 아이콘(스시걸 · 민트 줄무늬 시안 C, 2026-11-18)을 플레이어 설정에 넣는다.
    ///
    /// <para>안드로이드 세 종류를 모두 채운다 — 적응형(Android 8+, 배경 + 캐릭터 두 장, 런처가 모양대로 자른다),
    /// 원형(Android 7.1), 예전(그 이하). 기본 아이콘(Default Icon)도 같이.</para>
    ///
    /// <para>그림 원본·다른 시안(A 피치 · B 보라)·만드는 스크립트는 <c>ArtSource/AppIcon/</c>.
    /// 시안을 바꾸려면 <c>Art/Sujin/S_UI/AppIcon/</c> 의 네 장을 같은 이름으로 덮고 메뉴를 다시 누른다.</para>
    /// </summary>
    public static class AppIconSetup
    {
        const string Dir = "Assets/_Game/Art/Sujin/S_UI/AppIcon/";
        const string Bg = Dir + "AppIcon_Background.png";
        const string Fg = Dir + "AppIcon_Foreground.png";
        const string RoundPath = Dir + "AppIcon_Round.png";
        const string LegacyPath = Dir + "AppIcon_Legacy.png";

        [MenuItem("Tools/Zombie Overrun/앱 아이콘 적용")]
        static void Menu()
        {
            if (Apply()) Debug.Log("[앱 아이콘] 안드로이드 아이콘을 넣었다 — " + Dir);
            else Debug.LogWarning("[앱 아이콘] 그림 파일을 못 찾았다 — " + Dir);
        }

        // 안드로이드 아이콘이 비어 있을 때만 자동으로 넣는다(손으로 바꾼 아이콘은 건드리지 않음).
        // 넣은 뒤엔 텍스처를 가리키므로, 같은 이름의 PNG 를 덮어쓰면 아이콘도 따라 바뀐다.
        [InitializeOnLoadMethod]
        static void AutoWhenEmpty() => EditorApplication.delayCall += () => ApplyIfEmpty();

        public static void ApplyIfEmpty()
        {
            if (HasAndroidIcons()) return;
            if (Apply()) Debug.Log("[앱 아이콘] 안드로이드 아이콘이 비어 있어 자동으로 넣었다 — " + Dir);
        }

        static bool HasAndroidIcons()
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, AndroidPlatformIconKind.Adaptive);
            return icons.Any(i => i.GetTextures().Any(t => t != null));
        }

        /// <summary>그림 네 장을 아이콘 칸에 넣는다. 그림이 하나라도 없으면 아무것도 안 바꾸고 false.</summary>
        public static bool Apply()
        {
            var bg = Load(Bg);
            var fg = Load(Fg);
            var round = Load(RoundPath);
            var legacy = Load(LegacyPath);
            if (bg == null || fg == null || round == null || legacy == null) return false;

            var android = NamedBuildTarget.Android;
            Fill(android, AndroidPlatformIconKind.Adaptive, bg, fg);   // 레이어 순서: 배경, 앞(캐릭터)
            Fill(android, AndroidPlatformIconKind.Round, round);
            Fill(android, AndroidPlatformIconKind.Legacy, legacy);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void Fill(NamedBuildTarget target, PlatformIconKind kind, params Texture2D[] layers)
        {
            var icons = PlayerSettings.GetPlatformIcons(target, kind);
            foreach (var icon in icons) icon.SetTextures(layers);
            PlayerSettings.SetPlatformIcons(target, kind, icons);
        }

        // 아이콘용 가져오기 설정(압축 없음 · 밉맵 없음 · 크기 그대로)을 맞춘 뒤 읽는다.
        static Texture2D Load(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return null;
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Default) { imp.textureType = TextureImporterType.Default; dirty = true; }
            if (!imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }
            if (imp.mipmapEnabled) { imp.mipmapEnabled = false; dirty = true; }
            if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (imp.maxTextureSize < 1024) { imp.maxTextureSize = 1024; dirty = true; }
            if (dirty) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
