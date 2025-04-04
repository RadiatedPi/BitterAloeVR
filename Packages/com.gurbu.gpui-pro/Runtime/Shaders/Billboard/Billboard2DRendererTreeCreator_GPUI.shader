﻿Shader "GPUInstancerPro/Billboard/2DRendererTreeCreator" {
	Properties {
		_AlbedoAtlas ("Albedo Atlas", 2D) = "white" {}
		_NormalAtlas("Normal Atlas", 2D) = "white" {}
		_Cutoff_GPUI ("Cutoff_GPUI", Range(0,1)) = 0.5
		_FrameCount_GPUI("FrameCount_GPUI", Float) = 8
		_NormalStrength_GPUI("_NormalStrength_GPUI", Range(0,1)) = 0.5

		_TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1)
		_TranslucencyViewDependency ("View dependency", Range(0,1)) = 0.7
		_ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
		
		[Toggle(BILLBOARD_FACE_CAMERA_POS)] _BillboardFaceCamPos("BillboardFaceCamPos", Float) = 0
	}
	SubShader {
		Tags { "RenderType" = "TransparentCutout" "Queue" = "Transparent" "DisableBatching"="True" } //"ForceNoShadowCasting" = "True" }
		LOD 400
		CGPROGRAM

		sampler2D _AlbedoAtlas;
		sampler2D _NormalAtlas;
		float _Cutoff_GPUI;
		float _FrameCount_GPUI;
		float _NormalStrength_GPUI;

		fixed3 _TranslucencyColor;
		fixed _TranslucencyViewDependency;
		half _ShadowStrength;

		#include "UnityCG.cginc"
		#include "Lighting.cginc"
		#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
		#include "../Include/GPUIShaderUtils.hlsl"

		#pragma multi_compile __ BILLBOARD_FACE_CAMERA_POS
		#pragma instancing_options procedural:setupGPUI
		#pragma surface surf TreeLeaf vertex:vert nolightmap noforwardadd //addshadow //exclude_path:deferred
		#pragma multi_compile _ LOD_FADE_CROSSFADE
		#pragma target 4.5
		#pragma multi_compile _ GPUI_TREE_INSTANCE_COLOR
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && defined(GPUI_TREE_INSTANCE_COLOR)       
        StructuredBuffer<float4> gpuiTreeInstanceDataBuffer;
#endif

		struct Input {
			float4 screenPos;
			float2 atlasUV;
		};

		struct LeafSurfaceOutput {
			fixed3 Albedo;
			fixed3 Normal;
			fixed3 Emission;
			fixed Translucency;
			fixed Alpha;
			float Depth;
		};
		
		inline half4 LightingTreeLeaf(LeafSurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
		{
			half3 h = normalize (lightDir + viewDir);

			half nl = dot (s.Normal, lightDir);

			// view dependent back contribution for translucency
			fixed backContrib = saturate(dot(viewDir, -lightDir));

			// normally translucency is more like -nl, but looks better when it's view dependent
			backContrib = lerp(saturate(-nl), backContrib, _TranslucencyViewDependency);

			fixed3 translucencyColor = backContrib * s.Translucency * _TranslucencyColor;

			// wrap-around diffuse
			nl = max(0, nl * 0.6 + 0.4);

			fixed4 c;
			c.rgb = s.Albedo * (translucencyColor * 2 + nl);
			c.rgb = c.rgb * _LightColor0.rgb;// + spec;
			c.rgb = lerp (c.rgb, float3(0,0,0), s.Depth * 1.75);

			// For directional lights, apply less shadow attenuation
			// based on shadow strength parameter.
			#if defined(DIRECTIONAL) || defined(DIRECTIONAL_COOKIE)
			c.rgb *= lerp(1, atten, _ShadowStrength);
			#else
			c.rgb *= atten;
			#endif

			c.a = s.Alpha;

			return c;
		}

		void vert (inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
		
			GPUIBillboardVertex_float(v.vertex.xyz, 0, v.vertex.xyz);
			GPUIBillboardAtlasUV_float(v.texcoord.xy, _FrameCount_GPUI, o.atlasUV);
			GPUIBillboardNormalTangent(v.normal, v.tangent);
		}

		void surf (Input IN, inout LeafSurfaceOutput o) {
#ifdef LOD_FADE_CROSSFADE
			float2 vpos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
			UNITY_APPLY_DITHER_CROSSFADE(vpos);
#endif
			float4 c = tex2D (_AlbedoAtlas, IN.atlasUV);
			float4 n = tex2D(_NormalAtlas, IN.atlasUV);
			clip(c.a - 0.5);
			
			float depth;
			GPUIBillboardFragmentNormal_float(n, _NormalStrength_GPUI, o.Normal, depth);
			o.Albedo = c.rgb;//lerp (c.rgb, float3(0,0,0), depth);
			o.Depth = depth;
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && defined(GPUI_TREE_INSTANCE_COLOR)
			o.Albedo *= gpuiTreeInstanceDataBuffer[gpui_InstanceID].xyz;
#endif

			o.Translucency = 1; //TODO: use as property
			o.Alpha = c.a;
		}
		ENDCG
	}

}