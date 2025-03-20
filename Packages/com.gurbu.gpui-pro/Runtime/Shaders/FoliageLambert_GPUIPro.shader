// GPU Instancer Pro
// Copyright (c) GurBu Technologies

Shader "GPUInstancerPro/FoliageLambert"
{
	Properties
	{
		_Cutoff( "Mask Clip Value", Float ) = 0.5
		_WindWaveNormalTexture("Wind Wave Normal Texture", 2D) = "bump" {}
		_WindWaveSize("Wind Wave Size", Range( 0 , 1)) = 0.8
		_DryColor("Dry Color", Color) = (1,1,1,0)
		_HealthyColor("Healthy Color", Color) = (1,1,1,0)
		_MainTex("MainTex", 2D) = "white" {}
		_GradientContrastRatioTint("Gradient/Contrast/HealthyDry/WaveTint", Vector) = (0.2, 1, 1, 0.5)
		_NoiseSpread("Noise Spread", Float) = 0.1
		_NormalMap("Normal Map", 2D) = "bump" {}
		_WindVector("Wind Vector", Vector) = (0.4,0.8,0,0)
		[Toggle]_IsBillboard("IsBillboard", Float) = 0
		_WindWaveTintColor("Wind Wave Tint Color", Color) = (1,1,1,0)
		_HealthyDryNoiseTexture("Healthy Dry Noise Texture", 2D) = "white" {}
		[Toggle]_WindWavesOn("Wind Waves On", Float) = 0
		_WindWaveSway("Wind Wave Sway", Range( 0 , 1)) = 0.5
		_WindIdleSway("Wind Idle Sway", Range( 0 , 1)) = 0.6
		[Toggle(BILLBOARD_FACE_CAMERA_POS)] _BillboardFaceCamPos("BillboardFaceCamPos", Float) = 0
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "GPUIFoliage"  "Queue" = "AlphaTest+0" }
		Cull Off
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#pragma multi_compile __ BILLBOARD_FACE_CAMERA_POS
		#include "UnityCG.cginc"
		#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/GPUInstancerSetup.hlsl"
		#include "Packages/com.gurbu.gpui-pro/Runtime/Shaders/Include/FoliageMethods.hlsl"
		#pragma multi_compile_instancing
		#pragma instancing_options procedural:setupGPUI
		#pragma surface surf Lambert keepalpha addshadow fullforwardshadows vertex:vertexDataFunc 
		struct Input
		{
			float3 worldPos;
			float2 uv_texcoord;
			float vertexLocalY;
		};
		
		uniform float _IsBillboard;
		uniform float _WindWavesOn;
		uniform sampler2D _WindWaveNormalTexture;
		uniform float2 _WindVector;
		uniform float _WindWaveSize;
		uniform float _WindIdleSway;
		uniform float _WindWaveSway;
		uniform sampler2D _NormalMap;
		uniform float4 _NormalMap_ST;
		uniform float4 _GradientContrastRatioTint;
		uniform float4 _HealthyColor;
		uniform float4 _DryColor;
		uniform sampler2D _HealthyDryNoiseTexture;
		uniform float _NoiseSpread;
		uniform float4 _WindWaveTintColor;
		uniform sampler2D _MainTex;
		uniform float4 _MainTex_ST;
		uniform float _Cutoff = 0.5;
		uniform sampler2D _ColorTexture;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
			FoliageVertex(worldPos, v.vertex.xyz, _IsBillboard, _WindWavesOn, _WindWaveSize, _WindIdleSway, _WindWaveSway, _WindVector, _WindWaveNormalTexture, v.vertex.xyz);

			v.normal.xyz = float3(0,1,0);
			o.vertexLocalY = mul(unity_WorldToObject, float4(worldPos, 1)).y;
		}

		void surf( Input i , inout SurfaceOutput o )
		{
			float2 uv_MainTex = i.uv_texcoord * _MainTex_ST.xy + _MainTex_ST.zw;
			float4 mainTexSample = tex2D( _MainTex, uv_MainTex );
			clip( mainTexSample.a - _Cutoff );
			o.Alpha = 1;

			FoliageFragment(mainTexSample, i.vertexLocalY, i.worldPos, _NoiseSpread, _HealthyDryNoiseTexture, _HealthyColor, _DryColor, _WindVector, _WindWaveSize, _WindWaveTintColor, _WindWaveNormalTexture, _WindWavesOn, _GradientContrastRatioTint, o.Albedo);
		}

		ENDCG
	}
}	