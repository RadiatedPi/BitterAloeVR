// GPU Instancer Pro
// Copyright (c) GurBu Technologies

#ifndef GPU_INSTANCER_PRO_INPUT_INCLUDED
#define GPU_INSTANCER_PRO_INPUT_INCLUDED

#include "GPUIShaderSettings.hlsl"

#ifdef __INTELLISENSE__
#define UNITY_SUPPORT_INSTANCING
#define PROCEDURAL_INSTANCING_ON
#define UNITY_PROCEDURAL_INSTANCING_ENABLED
#undef GPUI_NO_BUFFER
#define GPUI_ENABLE_OBJECT_MOTION_VECTOR
#define LOD_FADE_CROSSFADE
//#define LIGHTMAP_ON
//#define DYNAMICLIGHTMAP_ON
static uint unity_InstanceID;
static float4x4 unity_ObjectToWorld;
static float4x4 unity_WorldToObject;
static float4x4 unity_MatrixPreviousM;
static float4x4 unity_MatrixPreviousMI;
static float4 unity_LODFade;
static float4 unity_LightmapST;
static float4 unity_DynamicLightmapST;
#endif // __INTELLISENSE__

#if defined(UNITY_PREV_MATRIX_M) && !defined(BUILTIN_TARGET_API) && !defined(GPUI_NO_BUFFER)
    #define GPUI_ENABLE_OBJECT_MOTION_VECTOR 1
#endif

uniform uint gpui_InstanceID;
uniform float4x4 gpuiTransformOffset;
uniform uint instanceDataBufferShift;
uniform float maxTextureSize;
uniform float instanceDataBufferSize;
uniform float transformBufferSize;

#if (defined(UNITY_SUPPORT_INSTANCING) && defined(PROCEDURAL_INSTANCING_ON)) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
#ifdef GPUI_NO_BUFFER
    uniform sampler2D_float gpuiTransformBufferTexture;
    uniform sampler2D_float gpuiInstanceDataBufferTexture;
#else // GPUI_NO_BUFFER
    uniform StructuredBuffer<float4x4> gpuiTransformBuffer;
    uniform StructuredBuffer<float4> gpuiInstanceDataBuffer;
#endif // GPUI_NO_BUFFER
#ifdef GPUI_ENABLE_OBJECT_MOTION_VECTOR
    uniform uint hasPreviousFrameTransformBuffer;
    uniform StructuredBuffer<float4x4> gpuiPreviousFrameTransformBuffer;
#endif // GPUI_ENABLE_OBJECT_MOTION_VECTOR
#endif //UNITY_PROCEDURAL_INSTANCING_ENABLED

#endif // GPU_INSTANCER_PRO_INPUT_INCLUDED