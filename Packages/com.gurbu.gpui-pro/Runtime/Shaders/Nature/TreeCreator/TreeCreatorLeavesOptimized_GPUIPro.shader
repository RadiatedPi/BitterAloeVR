// GPUInstancer enabled version of Unity built-in shader "Nature/Tree Creator Leaves Optimized"

Shader "Hidden/GPUInstancerPro/Nature/Tree Creator Leaves Optimized" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_TranslucencyColor ("Translucency Color", Color) = (0.73,0.85,0.41,1)
		_Cutoff ("Alpha cutoff", Range(0,1)) = 0.3
		_TranslucencyViewDependency ("View dependency", Range(0,1)) = 0.7
		_ShadowStrength("Shadow Strength", Range(0,1)) = 0.8
		_ShadowOffsetScale ("Shadow Offset Scale", Float) = 1

		_MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
		_ShadowTex ("Shadow (RGB)", 2D) = "white" {}
		_BumpSpecMap ("Normalmap (GA) Spec (R) Shadow Offset (B)", 2D) = "bump" {}
		_TranslucencyMap ("Trans (B) Gloss(A)", 2D) = "white" {}

		// These are here only to provide default values
		[HideInInspector] _TreeInstanceColor ("TreeInstanceColor", Vector) = (1,1,1,1)
		[HideInInspector] _TreeInstanceScale ("TreeInstanceScale", Vector) = (1,1,1,1)
		[HideInInspector] _SquashAmount ("Squash", Float) = 1
	}

	SubShader {
		Tags {
			"IgnoreProjector"="True"
			"RenderType"="TreeLeaf"
		}
		LOD 200

		CGPROGRAM
	
		#pragma multi_compile_instancing
		#pragma instancing_options procedural:setupGPUI
		#pragma surface surf TreeLeaf alphatest:_Cutoff vertex:TreeVertLeaf nolightmap noforwardadd addshadow
		#pragma multi_compile _ LOD_FADE_CROSSFADE
		//#pragma multi_compile __ BILLBOARD_FACE_CAMERA_POS
		#include "UnityBuiltin3xTreeLibrary.cginc"
		#include "UnityCG.cginc"
		#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
		
		#pragma multi_compile _ GPUI_TREE_INSTANCE_COLOR
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && defined(GPUI_TREE_INSTANCE_COLOR)       
        StructuredBuffer<float4> gpuiTreeInstanceDataBuffer;
#endif

		sampler2D _MainTex;
		sampler2D _BumpSpecMap;
		sampler2D _TranslucencyMap;

		struct Input {
			float4 screenPos;
			float2 uv_MainTex;
			fixed4 color : COLOR;
		};

		void surf (Input IN, inout LeafSurfaceOutput o) {
#ifdef LOD_FADE_CROSSFADE
			float2 vpos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
			UNITY_APPLY_DITHER_CROSSFADE(vpos);
#endif
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) && defined(GPUI_TREE_INSTANCE_COLOR)
			c *= gpuiTreeInstanceDataBuffer[gpui_InstanceID];
#endif
			o.Albedo = c.rgb * IN.color.rgb * IN.color.a;

			fixed4 trngls = tex2D (_TranslucencyMap, IN.uv_MainTex);
			o.Translucency = trngls.b;
			o.Gloss = trngls.a * _Color.r;
			o.Alpha = c.a;
			half4 norspc = tex2D (_BumpSpecMap, IN.uv_MainTex);
			o.Specular = norspc.r;
			o.Normal = UnpackNormalDXT5nm(norspc);
		}
		ENDCG
	}
}
