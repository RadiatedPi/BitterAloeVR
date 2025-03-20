Shader "GPUInstancerPro/Billboard/BillboardBuiltin_GPUIPro" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_AlbedoAtlas ("Albedo Atlas", 2D) = "white" {}
		_NormalAtlas("Normal Atlas", 2D) = "bump" {}
		_Cutoff_GPUI ("Cutoff_GPUI", Range(0,1)) = 0.5
		_FrameCount_GPUI("FrameCount_GPUI", Float) = 8
		_NormalStrength_GPUI("_NormalStrength_GPUI", Range(0,1)) = 0.5

		[Toggle(BILLBOARD_FACE_CAMERA_POS)] _BillboardFaceCamPos("BillboardFaceCamPos", Float) = 0
	}
	SubShader {
		Tags { "RenderType" = "TransparentCutout"  "Queue" = "Transparent"  "DisableBatching"="True" }
		LOD 400

		CGPROGRAM

		sampler2D _AlbedoAtlas;
		sampler2D _NormalAtlas;
		float _Cutoff_GPUI;
		float _FrameCount_GPUI;
		float _NormalStrength_GPUI;
		half4 _Color;

		#include "UnityCG.cginc"
		#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
		#include "../Include/GPUIShaderUtils.hlsl"

        #pragma multi_compile __ BILLBOARD_FACE_CAMERA_POS
		#pragma instancing_options procedural:setupGPUI
		#pragma surface surf Standard vertex:vert noshadow
		#pragma multi_compile _ LOD_FADE_CROSSFADE
		#pragma target 4.5

		struct Input {
			float4 screenPos;
			float2 atlasUV;
		};

		void vert(inout appdata_full v, out Input o) 
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			GPUIBillboardVertex_float(v.vertex.xyz, 0, v.vertex.xyz);
			GPUIBillboardAtlasUV_float(v.texcoord.xy, _FrameCount_GPUI, o.atlasUV);
			GPUIBillboardNormalTangent(v.normal, v.tangent);
		}

		void surf (Input IN, inout SurfaceOutputStandard o) {
#ifdef LOD_FADE_CROSSFADE
			float2 vpos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
			UNITY_APPLY_DITHER_CROSSFADE(vpos);
#endif
			float4 color = tex2D (_AlbedoAtlas, IN.atlasUV);
			float4 normal = tex2D(_NormalAtlas, IN.atlasUV);
			clip(color.a - _Cutoff_GPUI);
			o.Alpha = color.a;
	
			float depth;
			GPUIBillboardFragmentNormal_float(normal, _NormalStrength_GPUI, o.Normal, depth);

			o.Albedo = color.rgb * _Color;
			o.Metallic = 0;
			o.Smoothness = 0;
			// o.Occlusion = depth;
			o.Emission = 0;
		}
		ENDCG
	}
}