using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombieOverrun.EditorTools
{
    /// <summary>
    /// 테스트용 안드로이드 APK 빌드(도시 씬 하나).
    ///
    /// <para><b>패키지 이름을 고유하게 둔다.</b> 템플릿 기본값(com.UnityTechnologies.com.unity.template.urpblank)으로 빌드했더니
    /// 폰에서 "앱이 설치되지 않음"이 떴다(2026-10-01) — 같은 템플릿으로 다른 PC 에서 만든 테스트 앱들이 같은 이름에
    /// 다른 디버그 서명이라 안드로이드가 덮어쓰기로 보고 막는다. 32비트(ARMv7) 전용이던 것도 64비트(ARM64, IL2CPP)로 바꿨다 —
    /// 64비트 전용 기기에선 그것만으로도 설치가 안 된다.</para>
    ///
    /// <para>빌드는 에디터를 수 분 붙잡는다. 진행·결과는 <c>Builds/Android/build_status.txt</c> 에 남긴다
    /// (MCP 호출은 그 사이 타임아웃 나도 빌드는 계속된다 — 같은 호출을 다시 보내지 말 것).</para>
    /// </summary>
    public static class AndroidBuild
    {
        public const string PackageId = "com.afewgoodsoft.zombieoverrun";
        public const string ProductName = "Zombie Overrun";
        const string Scene = "Assets/_Game/Scenes/Proto_City_Test.unity";
        const string OutDir = "Builds/Android";
        const string StatusFile = OutDir + "/build_status.txt";

        [MenuItem("Tools/Zombie Overrun/안드로이드 빌드 (도시 씬)")]
        public static void BuildMenu() => Build();

        public static string Build()
        {
            Directory.CreateDirectory(OutDir);
            if (File.Exists(StatusFile) && File.ReadAllText(StatusFile).StartsWith("BUILDING"))
                return "이미 빌드 중 — " + StatusFile;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageId);
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel24)
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            EditorUserBuildSettings.buildAppBundle = false;
            AppIconSetup.ApplyIfEmpty();   // 앱 아이콘(Art/Sujin/S_UI/AppIcon) — 비어 있을 때만
            AssetDatabase.SaveAssets();

            string apk = OutDir + "/ZombieOverrun_City.apk";
            File.WriteAllText(StatusFile, "BUILDING " + System.DateTime.Now + "\n");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            BuildReport rep = null;
            try { rep = BuildPipeline.BuildPlayer(opts); }
            catch (System.Exception e) { File.WriteAllText(StatusFile, "FAILED exception " + e + "\n"); throw; }

            var s = rep.summary;
            string msg = $"{s.result} {System.DateTime.Now}\n{apk}\n{s.totalSize / (1024f * 1024f):F1}MB, {s.totalTime.TotalMinutes:F1}분, 오류 {s.totalErrors}\n" +
                         $"package {PackageId}, IL2CPP ARM64+ARMv7\n";
            File.WriteAllText(StatusFile, msg);
            return msg;
        }
    }
}
