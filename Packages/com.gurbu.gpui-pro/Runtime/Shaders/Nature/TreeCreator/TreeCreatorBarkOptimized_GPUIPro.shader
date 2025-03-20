// GPUInstancer enabled version of Unity built-in shader "Nature/Tree Creator Bark Optimized"

Shader "Hidden/GPUInstancerPro/Nature/Tree Creator Bark Optimized" {
	Properties {
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB) Alpha (A)", 2D) = "white" {}
		_BumpSpecMap ("Normalmap (GA) Spec (R)", 2D) = "bump" {}
		_TranslucencyMap ("Trans (RGB) Gloss(A)", 2D) = "white" {}
		_Cutoff("Alpha cutoff", Range(0,1)) = 0.3

		// These are here only to provide default values
		_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
		[HideInInspector] _TreeInstanceColor ("TreeInstanceColor", Vector) = (1,1,1,1)
		[HideInInspector] _TreeInstanceScale ("TreeInstanceScale", Vector) = (1,1,1,1)
		[HideInInspector] _SquashAmount ("Squash", Float) = 1
	}

	SubShader {
		Tags { "IgnoreProjector"="True" "RenderType"="TreeBark" }
		LOD 200

	CGPROGRAM

	#pragma multi_compile_instancing
	#pragma instancing_options procedural:setupGPUI
	#pragma surface surf BlinnPhong vertex:TreeVertBark addshadow nolightmap
	#pragma multi_compile _ LOD_FADE_CROSSFADE
	#pragma multi_compile __ BILLBOARD_FACE_CAMERA_POS
	#include "UnityCG.cginc"
	#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
	#include "UnityBuiltin3xTreeLibrary.cginc"

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

	void surf (Input IN, inout SurfaceOutput o) {
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
		o.Gloss = trngls.a * _Color.r;
		o.Alpha = c.a;
#if defined(BILLBOARD_FACE_CAMERA_POS)
		float coverage = 1.0;
		if (_TreeInstanceColor.a < 1.0)
			coverage = ComputeAlphaCoverage(IN.screenPos, _TreeInstanceColor.a);
		o.Alpha *= coverage;
#endif
		half4 norspc = tex2D (_BumpSpecMap, IN.uv_MainTex);
		o.Specular = norspc.r;
		o.Normal = UnpackNormalDXT5nm(norspc);
	}
	ENDCG
	}
}
