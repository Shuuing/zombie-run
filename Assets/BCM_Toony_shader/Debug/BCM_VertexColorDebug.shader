// BCM_Toony_shader - vertex color visualiser.
//
// Dev tool, not part of the shipping shader. Assign it to a model to see what the vertex
// colour attribute actually contains after a 3ds Max -> FBX -> Unity round trip.
//
// Pipeline note: the single pass carries no LightMode tag. Built-in RP draws untagged passes
// directly, and URP treats them as SRPDefaultUnlit, so this works in both without depending
// on BCM_PipelineConfig.cginc.
//
// CAVEAT: a mesh with NO vertex colour data feeds white (1,1,1,1) into the COLOR semantic,
// which looks identical to "painted white". To tell those apart use
// Tools > BCM > Inspect Vertex Colors, which reads the mesh data itself.

Shader "BCM/Debug/Vertex Color"
{
	Properties
	{
		[Enum(RGB,0,R,1,G,2,B,3,Alpha,4,Decoded Normal,5,Outline Mask Preview,6)]
		_View ("View", Float) = 0

		[Header(Outline Mask Preview)]
		[Space(4)]
		[Enum(R,0,G,1,B,2,Alpha,3)] _MaskChannel ("Mask Channel", Float) = 3
		// Default convention: channel value IS the outline strength, so black = no outline.
		// Flip this if you paint the other way round (white = no outline).
		[Toggle] _MaskInvert ("Invert (white = no outline)", Float) = 0

		[Space(8)]
		[Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			Cull [_Cull]
			ZWrite On

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 2.0

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color  : COLOR;
			};

			struct v2f
			{
				float4 pos   : SV_POSITION;
				float4 color : TEXCOORD0;
			};

			float _View;
			float _MaskChannel;
			float _MaskInvert;

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				return o;
			}

			float4 frag (v2f i) : SV_Target
			{
				float4 c = i.color;
				int view = (int)(_View + 0.5);

				if (view == 1) return float4(c.rrr, 1);
				if (view == 2) return float4(c.ggg, 1);
				if (view == 3) return float4(c.bbb, 1);
				if (view == 4) return float4(c.aaa, 1);

				// Decoded as a normal, the way _NormalsSource = Vertex Colors reads it
				// (BCM_Toony_Include.cginc: normal = color.xyz * 2 - 1). A proper smoothed-normal
				// bake looks like a pastel normal map; flat paint looks like flat colour.
				if (view == 5)
				{
					float3 n = normalize(c.rgb * 2.0 - 1.0);
					return float4(n * 0.5 + 0.5, 1);
				}

				// Outline mask preview: red = no outline, green = outline.
				// The yellow band in between is the interpolation zone - a triangle straddling the
				// boundary tapers instead of cutting cleanly. Split that edge in Max to remove it.
				if (view == 6)
				{
					float m = c.r;
					int ch = (int)(_MaskChannel + 0.5);
					if (ch == 1) m = c.g;
					else if (ch == 2) m = c.b;
					else if (ch == 3) m = c.a;

					if (_MaskInvert > 0.5) m = 1.0 - m;
					return float4(lerp(float3(1, 0, 0), float3(0, 1, 0), m), 1);
				}

				return float4(c.rgb, 1);
			}
			ENDCG
		}
	}

	Fallback Off
}
