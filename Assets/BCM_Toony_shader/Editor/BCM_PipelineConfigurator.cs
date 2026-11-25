// BCM_Toony_shader - pipeline auto-configurator.
//
// Detects whether the Universal Render Pipeline package is present and writes the matching value into
// BCM_PipelineConfig.cginc, then reimports the shader. This is what makes the single shader file
// portable: drop the folder into any project and the URP SubShader is either enabled (URP present)
// or stubbed out (URP absent) automatically, with no manual steps.

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BCM
{
	[InitializeOnLoad]
	public static class BCM_PipelineConfigurator
	{
		const string DefineLine = "#define BCM_URP";

		static BCM_PipelineConfigurator()
		{
			// Defer so the asset database is ready.
			EditorApplication.delayCall += () => Configure(false);
		}

		[MenuItem("Tools/BCM/Reconfigure Toony Shader Pipeline")]
		static void ReconfigureMenu()
		{
			Configure(true);
		}

		static bool IsUrpInstalled()
		{
			// True if the URP runtime type can be resolved -> the package is installed.
			return System.AppDomain.CurrentDomain.GetAssemblies()
				.Any(a => a.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset") != null);
		}

		static string FindConfigPath()
		{
			var guids = AssetDatabase.FindAssets("BCM_PipelineConfig");
			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (path.EndsWith("BCM_PipelineConfig.cginc"))
					return path;
			}
			return null;
		}

		static void Configure(bool verbose)
		{
			string path = FindConfigPath();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				if (verbose) Debug.LogWarning("[BCM_Toony] BCM_PipelineConfig.cginc not found; skipping pipeline configuration.");
				return;
			}

			bool urp = IsUrpInstalled();
			string text = File.ReadAllText(path);
			var lines = text.Replace("\r\n", "\n").Split('\n');
			bool changed = false;

			for (int i = 0; i < lines.Length; i++)
			{
				string trimmed = lines[i].TrimStart();
				bool isDefine = trimmed == DefineLine;
				bool isCommentedDefine = trimmed == "// " + DefineLine || trimmed == "//" + DefineLine;

				if (isDefine && !urp)          { lines[i] = "// " + DefineLine; changed = true; }
				else if (isCommentedDefine && urp) { lines[i] = DefineLine;      changed = true; }
			}

			if (changed)
			{
				File.WriteAllText(path, string.Join("\n", lines));
				AssetDatabase.ImportAsset(path);

				// Reimport the shader so the new configuration takes effect.
				var shaderGuids = AssetDatabase.FindAssets("BCM_Toony_shader t:Shader");
				foreach (var g in shaderGuids)
					AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g));

				Debug.Log($"[BCM_Toony] Pipeline configured: URP {(urp ? "ENABLED" : "disabled (Built-in RP)")}.");
			}
			else if (verbose)
			{
				Debug.Log($"[BCM_Toony] Pipeline already correct: URP {(urp ? "ENABLED" : "disabled (Built-in RP)")}.");
			}
		}
	}
}
