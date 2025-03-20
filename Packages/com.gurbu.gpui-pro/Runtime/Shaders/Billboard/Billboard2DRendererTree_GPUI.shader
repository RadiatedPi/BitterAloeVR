Shader "GPUInstancerPro/Billboard/2DRendererSpeedTree" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_AlbedoAtlas ("Albedo Atlas", 2D) = "white" {}
		_NormalAtlas("Normal Atlas", 2D) = "white" {}
		_Cutoff_GPUI ("Cutoff_GPUI", Range(0,1)) = 0.5
		_FrameCount_GPUI("FrameCount_GPUI", Float) = 8
		_NormalStrength_GPUI("_NormalStrength_GPUI", Range(0,1)) = 0.5
		_SPDHueVariation("Hue Vatiation", Color) = (0,0,0,0)
		[Toggle(SPDTREE_HUE_VARIATION)]
		_UseSPDHueVariation ("Use Hue Variation", Float) = 0
		[Toggle(BILLBOARD_FACE_CAMERA_POS)] _BillboardFaceCamPos("BillboardFaceCamPos", Float) = 0

	}
	SubShader {
		Tags { "RenderType" = "TransparentCutout" "Queue" = "Transparent" "DisableBatching"="True" }
		LOD 400
		CGPROGRAM

		sampler2D _AlbedoAtlas;
		sampler2D _NormalAtlas;
		float _Cutoff_GPUI;
		float _FrameCount_GPUI;
		float _NormalStrength_GPUI;
		half4 _SPDHueVariation;
		half4 _Color;

		#include "UnityCG.cginc"
		#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
		#include "../Include/GPUIShaderUtils.hlsl"

		#pragma instancing_options procedural:setupGPUI
		#pragma surface surf Lambert noshadow vertex:vert //addshadow exclude_path:deferred
		#pragma multi_compile _ LOD_FADE_CROSSFADE
		#pragma shader_feature SPDTREE_HUE_VARIATION
		#pragma multi_compile __ BILLBOARD_FACE_CAMERA_POS
		#pragma multi_compile __ NORMALSALWAYSUP
		#pragma target 4.5

		struct Input {
			float4 screenPos;
			float2 atlasUV;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			//float3 o.worldPosActual = unity_ObjectToWorld._m03_m13_m23;
			
			GPUIBillboardVertex_float(v.vertex.xyz, 0, v.vertex.xyz);
			GPUIBillboardAtlasUV_float(v.texcoord.xy, _FrameCount_GPUI, o.atlasUV);
			GPUIBillboardNormalTangent(v.normal, v.tangent);

#ifdef NORMALSALWAYSUP
			v.normal = float3(0,1,0);
#endif
		}

		void surf (Input IN, inout SurfaceOutput o) {
#ifdef LOD_FADE_CROSSFADE
			float2 vpos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
			UNITY_APPLY_DITHER_CROSSFADE(vpos);
#endif
			half4 c = tex2D (_AlbedoAtlas, IN.atlasUV);
			clip(c.a - _Cutoff_GPUI);
			o.Alpha = c.a;
			
#ifdef SPDTREE_HUE_VARIATION
			CalculateHueVariation(_SPDHueVariation, c);
#endif
			c.rgb = c.rgb * _Color;
			
#ifndef NORMALSALWAYSUP
			float depth;
			GPUIBillboardFragmentNormal_float(tex2D(_NormalAtlas, IN.atlasUV), _NormalStrength_GPUI, o.Normal, depth);
			o.Albedo = lerp (c.rgb, float3(0,0,0), depth);
#else
			o.Albedo = c.rgb;
#endif
		}
		ENDCG
	}

}