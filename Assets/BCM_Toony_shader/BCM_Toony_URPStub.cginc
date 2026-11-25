// BCM_Toony_shader - URP SubShader stub.
//
// Compiled only when BCM_URP is NOT defined (i.e. the Universal Render Pipeline package is not
// installed). It provides trivial no-op vertex/fragment programs for every entry point used by the
// URP SubShader, so that SubShader imports without errors in Built-in Render Pipeline projects.
// The Built-in RP always renders through the second (Built-in) SubShader, so these stubs never run.

#include "UnityCG.cginc"

struct bcm_stub_in
{
	float4 vertex : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

float4 Vertex(bcm_stub_in v) : SV_POSITION            { return UnityObjectToClipPos(v.vertex); }
float4 ShadowPassVertex(bcm_stub_in v) : SV_POSITION  { return UnityObjectToClipPos(v.vertex); }
float4 DepthOnlyVertex(bcm_stub_in v) : SV_POSITION   { return UnityObjectToClipPos(v.vertex); }
float4 DepthNormalsVertex(bcm_stub_in v) : SV_POSITION{ return UnityObjectToClipPos(v.vertex); }
float4 vertex_outline(bcm_stub_in v) : SV_POSITION    { return UnityObjectToClipPos(v.vertex); }

fixed4 Fragment() : SV_Target             { return 0; }
fixed4 ShadowPassFragment() : SV_Target   { return 0; }
fixed4 DepthOnlyFragment() : SV_Target    { return 0; }
fixed4 DepthNormalsFragment() : SV_Target { return 0; }
fixed4 fragment_outline() : SV_Target     { return 0; }
