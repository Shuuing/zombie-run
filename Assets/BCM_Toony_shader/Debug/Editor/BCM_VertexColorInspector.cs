// BCM_Toony_shader - vertex colour data report.
//
// Dev tool, not part of the shipping shader. Answers the question a visualiser shader cannot:
// did the vertex colour attribute (especially ALPHA) actually survive the 3ds Max -> FBX ->
// Unity round trip, or is Unity just feeding white into the COLOR semantic because the mesh
// carries no colour data at all?
//
// Tools > BCM > Inspect Vertex Colors, with meshes / GameObjects / prefabs / FBX selected.

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BCM
{
	public static class VertexColorInspector
	{
		const int MaxDistinctToTrack = 32;

		[MenuItem("Tools/BCM/Inspect Vertex Colors", false, 100)]
		static void Inspect()
		{
			var meshes = CollectMeshes();
			if (meshes.Count == 0)
			{
				Debug.LogWarning("[BCM] Nothing to inspect. Select a mesh, an FBX, a prefab, or GameObjects " +
								 "with a MeshFilter / SkinnedMeshRenderer (children are included).");
				return;
			}

			var sb = new StringBuilder();
			sb.AppendLine("[BCM] Vertex colour report - " + meshes.Count + " mesh(es)");

			foreach (var kv in meshes)
			{
				sb.AppendLine();
				sb.AppendLine(Report(kv.Key, kv.Value));
			}

			Debug.Log(sb.ToString());
		}

		[MenuItem("Tools/BCM/Inspect Vertex Colors", true)]
		static bool InspectValidate()
		{
			return Selection.objects != null && Selection.objects.Length > 0;
		}

		// mesh -> where it came from, so the log names something the user recognises
		static Dictionary<Mesh, string> CollectMeshes()
		{
			var result = new Dictionary<Mesh, string>();

			foreach (var obj in Selection.objects)
			{
				var asMesh = obj as Mesh;
				if (asMesh != null)
				{
					Add(result, asMesh, asMesh.name);
					continue;
				}

				var go = obj as GameObject;
				if (go != null)
				{
					foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
						if (mf.sharedMesh != null) Add(result, mf.sharedMesh, mf.sharedMesh.name + "  (" + mf.gameObject.name + ")");
					foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
						if (smr.sharedMesh != null) Add(result, smr.sharedMesh, smr.sharedMesh.name + "  (" + smr.gameObject.name + ", skinned)");
					continue;
				}

				// an FBX / model asset selected in the Project window
				var path = AssetDatabase.GetAssetPath(obj);
				if (!string.IsNullOrEmpty(path))
					foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
					{
						var m = sub as Mesh;
						if (m != null) Add(result, m, m.name + "  (" + System.IO.Path.GetFileName(path) + ")");
					}
			}

			return result;
		}

		static void Add(Dictionary<Mesh, string> dict, Mesh m, string label)
		{
			if (!dict.ContainsKey(m)) dict.Add(m, label);
		}

		static string Report(Mesh mesh, string label)
		{
			var sb = new StringBuilder();
			sb.AppendLine("── " + label);
			sb.AppendLine("   vertices: " + mesh.vertexCount);

			bool hasColorStream = mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color);
			var colors = mesh.colors;   // empty array when the mesh has no colour data

			if (!hasColorStream || colors == null || colors.Length == 0)
			{
				sb.AppendLine("   vertex colours: NONE");
				sb.AppendLine("   -> The shader will read white (1,1,1,1). Nothing was painted, or the");
				sb.AppendLine("      FBX export dropped it. In 3ds Max check Export > Geometry > Vertex Colors,");
				sb.AppendLine("      and make sure the VertexPaint modifier is collapsed into the mesh.");
				return sb.ToString().TrimEnd();
			}

			sb.AppendLine("   vertex colours: present (" + colors.Length + " entries" +
						  (colors.Length != mesh.vertexCount ? " - COUNT MISMATCH with vertexCount" : "") + ")");
			sb.AppendLine("   format: " + mesh.GetVertexAttributeFormat(UnityEngine.Rendering.VertexAttribute.Color) +
						  " x" + mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.Color));

			sb.AppendLine("   " + Channel("R", colors, 0));
			sb.AppendLine("   " + Channel("G", colors, 1));
			sb.AppendLine("   " + Channel("B", colors, 2));
			sb.AppendLine("   " + Channel("A", colors, 3));

			// The verdict the user is actually after.
			var alpha = Stats(colors, 3);
			int dim = mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.Color);
			sb.AppendLine();
			if (dim < 4)
			{
				sb.AppendLine("   >> ALPHA NOT USABLE: the colour attribute has only " + dim + " components.");
				sb.AppendLine("      Alpha did not survive the export - paint into R/G/B instead, or fix the");
				sb.AppendLine("      export so a 4-component colour channel is written.");
			}
			else if (alpha.Min >= 0.999f && alpha.Max >= 0.999f)
			{
				sb.AppendLine("   >> ALPHA IS UNIFORMLY 1.0 - indistinguishable from 'not authored'.");
				sb.AppendLine("      If you painted alpha in Max, it was lost. Paint a test region to a value");
				sb.AppendLine("      clearly below 1 and re-export to confirm the channel round-trips.");
			}
			else
			{
				sb.AppendLine("   >> ALPHA CARRIES DATA (range " + alpha.Min.ToString("0.###") + " .. " +
							  alpha.Max.ToString("0.###") + ", " + alpha.DistinctLabel + ") - usable as an outline mask.");
			}

			return sb.ToString().TrimEnd();
		}

		struct ChannelStats
		{
			public float Min;
			public float Max;
			public int ZeroCount;
			public int OneCount;
			public int Distinct;
			public bool DistinctOverflow;

			public string DistinctLabel
			{
				get { return DistinctOverflow ? MaxDistinctToTrack + "+ distinct values" : Distinct + " distinct value(s)"; }
			}
		}

		static ChannelStats Stats(Color[] colors, int channel)
		{
			var s = new ChannelStats { Min = float.MaxValue, Max = float.MinValue };
			var distinct = new HashSet<float>();

			for (int i = 0; i < colors.Length; i++)
			{
				float v = channel == 0 ? colors[i].r : channel == 1 ? colors[i].g : channel == 2 ? colors[i].b : colors[i].a;
				if (v < s.Min) s.Min = v;
				if (v > s.Max) s.Max = v;
				if (v <= 0.001f) s.ZeroCount++;
				if (v >= 0.999f) s.OneCount++;
				if (!s.DistinctOverflow)
				{
					// quantise to 8 bit - vertex colours are byte data anyway, and this keeps
					// float noise from inflating the count
					distinct.Add(Mathf.Round(v * 255f));
					if (distinct.Count > MaxDistinctToTrack) s.DistinctOverflow = true;
				}
			}

			s.Distinct = distinct.Count;
			return s;
		}

		static string Channel(string name, Color[] colors, int channel)
		{
			var s = Stats(colors, channel);
			int total = colors.Length;
			return string.Format("{0}: {1:0.###} .. {2:0.###}   |  =0: {3} ({4:0.#}%)  =1: {5} ({6:0.#}%)  |  {7}",
				name, s.Min, s.Max,
				s.ZeroCount, 100f * s.ZeroCount / total,
				s.OneCount, 100f * s.OneCount / total,
				s.DistinctLabel);
		}
	}
}
