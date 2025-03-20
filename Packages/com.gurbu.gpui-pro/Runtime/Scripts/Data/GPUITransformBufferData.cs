// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUITransformBufferData : IGPUIDisposable
    {
        private GPUIRenderSourceGroup _renderSourceGroup;
        public GPUIRenderSourceGroup RenderSourceGroup {  get { return _renderSourceGroup; } }

        /// <summary>
        /// Contains transform buffers (per camera when CameraBased)
        /// </summary>
        private Dictionary<int, GPUIShaderBuffer> _transformBufferDict;
        /// <summary>
        /// Contains transform buffers (per camera when CameraBased)
        /// </summary>
        public Dictionary<int, GPUIShaderBuffer>.ValueCollection TransformBufferValues => _transformBufferDict == null ? null : _transformBufferDict.Values;

        public GraphicsBuffer PreviousFrameTransformBuffer { get; private set; }
        public bool HasPreviousFrameTransformBuffer { get; private set; }
        private int _previousFrameBufferFrameNo;

        /// <summary>
        /// Contains results of the visibility calculations for LODs and shadows (such as instanceID and crossFadeValue) for each camera
        /// </summary>
        private Dictionary<int, GPUIShaderBuffer> _instanceDataBufferDict;

        public int resetCrossFadeDataFrame;

        public bool IsCameraBasedBuffer
        {
            get
            {
                return _renderSourceGroup.TransformBufferType == GPUITransformBufferType.CameraBased;
            }
        }

        public bool IsDefaultBuffer
        {
            get
            {
                return _renderSourceGroup.TransformBufferType == GPUITransformBufferType.Default;
            }
        }

        public GPUITransformBufferData(GPUIRenderSourceGroup renderSourceGroup)
        {
            _renderSourceGroup = renderSourceGroup;

#if GPUI_HDRP
            if (Application.isPlaying && IsDefaultBuffer && !GPUIRuntimeSettings.Instance.DisableShaderBuffers && GPUIRuntimeSettings.Instance.IsHDRP && renderSourceGroup.LODGroupData != null && renderSourceGroup.Profile != null && renderSourceGroup.Profile.enablePerObjectMotionVectors)
            {
                if (renderSourceGroup.LODGroupData.HasObjectMotion())
                {
                    HasPreviousFrameTransformBuffer = true;
                    _previousFrameBufferFrameNo = -1;
                }
            }
#endif
        }

        public void ReleaseBuffers()
        {
            ReleaseTransformBuffers();
            ReleaseInstanceDataBuffers();
        }

        internal void ReleaseTransformBuffers()
        {
            if (_transformBufferDict != null)
            {
                foreach (var tb in _transformBufferDict.Values)
                {
                    if (tb != null)
                        tb.Dispose();
                }
                _transformBufferDict = null;
            }
            if (PreviousFrameTransformBuffer != null)
                PreviousFrameTransformBuffer.Dispose();
        }

        internal void ReleaseInstanceDataBuffers()
        {
            if (_instanceDataBufferDict != null)
            {
                foreach (GPUIShaderBuffer instanceDataBuffer in _instanceDataBufferDict.Values)
                {
                    if (instanceDataBuffer != null)
                        instanceDataBuffer.Dispose();
                }
                _instanceDataBufferDict = null;
            }
        }

        internal void ReleaseInstanceDataBuffers(GPUICameraData cameraData)
        {
            int key = cameraData.ActiveCamera.GetInstanceID();
            if (_instanceDataBufferDict != null && _instanceDataBufferDict.TryGetValue(key, out GPUIShaderBuffer instanceDataBuffer))
            {
                if (instanceDataBuffer != null)
                    instanceDataBuffer.Dispose();
                _instanceDataBufferDict.Remove(key);
            }
        }

        public void Dispose()
        {
            ReleaseBuffers();
            _transformBufferDict = null;
            _instanceDataBufferDict = null;
        }

        internal void Dispose(GPUICameraData cameraData)
        {
            if (IsCameraBasedBuffer && cameraData != null && cameraData.ActiveCamera != null)
            {
                int key = cameraData.ActiveCamera.GetInstanceID();
                if (_transformBufferDict != null && _transformBufferDict.TryGetValue(key, out var tb))
                {
                    tb.Dispose();
                    _transformBufferDict.Remove(key);
                }
                if (_instanceDataBufferDict != null && _instanceDataBufferDict.TryGetValue(key, out var idb))
                {
                    idb.Dispose();
                    _instanceDataBufferDict.Remove(key);
                }
            }
        }

        internal void ResizeTransformBuffer(bool isCopyPreviousData)
        {
            if (IsCameraBasedBuffer)
            {
                Dispose();
                return;
            }

            if (_transformBufferDict == null)
                _transformBufferDict = new();

            _transformBufferDict.TryGetValue(0, out GPUIShaderBuffer previousTransformBuffer);
            bool hasPreviousBuffer = previousTransformBuffer != null;
            int previousBufferSize = hasPreviousBuffer ? previousTransformBuffer.BufferSize : 0;

            if (!hasPreviousBuffer || previousBufferSize != _renderSourceGroup.BufferSize)
            {
                GPUIShaderBuffer transformBuffer = CreateTransformBuffer();
                _transformBufferDict[0] = transformBuffer;

                if (previousTransformBuffer != null)
                {
                    if (isCopyPreviousData)
                        transformBuffer.Buffer.SetData(previousTransformBuffer.Buffer, 0, 0, Math.Min(_renderSourceGroup.BufferSize, previousBufferSize));
                    previousTransformBuffer.Dispose();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="previousTransformBuffer"></param>
        /// <param name="transformBuffer"></param>
        /// <returns>True if an existing buffer is replaced with a new one</returns>
        internal bool ResizeTransformBuffer(out GPUIShaderBuffer previousTransformBuffer, out GPUIShaderBuffer transformBuffer)
        {
            previousTransformBuffer = null;
            transformBuffer = null;
            if (IsCameraBasedBuffer)
            {
                Dispose();
                return false;
            }

            if (_transformBufferDict == null)
                _transformBufferDict = new();

            _transformBufferDict.TryGetValue(0, out previousTransformBuffer);
            bool hasPreviousBuffer = previousTransformBuffer != null;
            int previousBufferSize = hasPreviousBuffer ? previousTransformBuffer.BufferSize : 0;

            if (!hasPreviousBuffer || previousBufferSize != _renderSourceGroup.BufferSize)
            {
                transformBuffer = CreateTransformBuffer();
                _transformBufferDict[0] = transformBuffer;
                return true;
            }
            return false;
        }

        internal void SetTransformBufferData<T>(NativeArray<T> matrices, int managedBufferStartIndex, int graphicsBufferStartIndex, int count, bool isOverwritePreviousFrameBuffer) where T : struct
        {
            GPUIShaderBuffer transformBuffer = GetTransformBuffer();
            transformBuffer.Buffer.SetData(matrices, managedBufferStartIndex, graphicsBufferStartIndex, count);
            if (isOverwritePreviousFrameBuffer && HasPreviousFrameTransformBuffer && PreviousFrameTransformBuffer != null && graphicsBufferStartIndex < PreviousFrameTransformBuffer.count)
                PreviousFrameTransformBuffer.SetData(matrices, managedBufferStartIndex, graphicsBufferStartIndex, Math.Min(count, PreviousFrameTransformBuffer.count - graphicsBufferStartIndex));
            OnTransformDataModified();
        }

        internal void SetTransformBufferData<T>(T[] matrices, int managedBufferStartIndex, int graphicsBufferStartIndex, int count, bool isOverwritePreviousFrameBuffer) where T : struct
        {
            GPUIShaderBuffer transformBuffer = GetTransformBuffer();
            transformBuffer.Buffer.SetData(matrices, managedBufferStartIndex, graphicsBufferStartIndex, count);
            if (isOverwritePreviousFrameBuffer && HasPreviousFrameTransformBuffer && PreviousFrameTransformBuffer != null && graphicsBufferStartIndex < PreviousFrameTransformBuffer.count)
                PreviousFrameTransformBuffer.SetData(matrices, managedBufferStartIndex, graphicsBufferStartIndex, Math.Min(count, PreviousFrameTransformBuffer.count - graphicsBufferStartIndex));
            OnTransformDataModified();
        }

        internal void SetTransformBufferData<T>(List<T> matrices, int managedBufferStartIndex, int graphicsBufferStartIndex, int count, bool isOverwritePreviousFrameBuffer) where T : struct
        {
            GPUIShaderBuffer transformBuffer = GetTransformBuffer();
            transformBuffer.Buffer.SetData(matrices, managedBufferStartIndex, graphicsBufferStartIndex, count);
            if (isOverwritePreviousFrameBuffer && HasPreviousFrameTransformBuffer && PreviousFrameTransformBuffer != null && graphicsBufferStartIndex < PreviousFrameTransformBuffer.count)
                PreviousFrameTransformBuffer.SetData(matrices, managedBufferStartIndex, graphicsBufferStartIndex, Math.Min(count, PreviousFrameTransformBuffer.count - graphicsBufferStartIndex));
            OnTransformDataModified();
        }

        internal void RemoveIndexes(int startIndex, int count)
        {
            if (IsCameraBasedBuffer)
            {
                Debug.LogError("RemoveIndexes method can not be used with Camera Based transform buffers.");
                return;
            }

            GPUIShaderBuffer transformBuffer = CreateTransformBuffer();

            if (_transformBufferDict.TryGetValue(0, out GPUIShaderBuffer previousTransformBuffer))
            {
                transformBuffer.Buffer.SetData(previousTransformBuffer.Buffer, 0, 0, startIndex);
                transformBuffer.Buffer.SetData(previousTransformBuffer.Buffer, startIndex + count, startIndex, _renderSourceGroup.BufferSize - startIndex);
                previousTransformBuffer.Dispose();
            }

            _transformBufferDict[0] = transformBuffer;
            OnTransformDataModified();
        }

        private GPUIShaderBuffer CreateTransformBuffer()
        {
            //Debug.Log("Creating new buffer with size: " + _renderSourceGroup.bufferSize);
            ReleaseInstanceDataBuffers();
            return new GPUIShaderBuffer(_renderSourceGroup.BufferSize, 4 * 4 * 4);
        }

        private GPUIShaderBuffer CreateInstanceDataBuffer(int instanceDataBufferSize)
        {
            resetCrossFadeDataFrame = Time.frameCount;
            return new GPUIShaderBuffer(instanceDataBufferSize, 4 * 4);
        }

        public GPUIShaderBuffer GetTransformBuffer()
        {
            if (IsCameraBasedBuffer)
            {
                Debug.LogError("GetTransformBuffer method can not be used with Camera Based transform buffers.");
                return null;
            }
            if (_transformBufferDict == null)
                _transformBufferDict = new();
            else if (_transformBufferDict.TryGetValue(0, out GPUIShaderBuffer transformBuffer) && transformBuffer != null)
                return transformBuffer;
            GPUIShaderBuffer result = CreateTransformBuffer();
            _transformBufferDict[0] = result;
            return result;
        }

        public GPUIShaderBuffer GetTransformBuffer(GPUICameraData cameraData)
        {
            if (!IsCameraBasedBuffer || cameraData == null)
                return GetTransformBuffer();
            int key = cameraData.ActiveCamera.GetInstanceID();
            if (_transformBufferDict == null)
                _transformBufferDict = new();
            if (!_transformBufferDict.TryGetValue(key, out var result) || result == null)
            {
                result = CreateTransformBuffer();
                _transformBufferDict[key] = result;
            }
            return result;
        }

        public GPUIShaderBuffer GetInstanceDataBuffer(GPUICameraData cameraData)
        {
            int key = cameraData.ActiveCamera.GetInstanceID();
            if (_instanceDataBufferDict == null)
                _instanceDataBufferDict = new();

            GPUILODGroupData lodGroupData = _renderSourceGroup.LODGroupData;
            Debug.Assert(lodGroupData != null, "Can not find GPUILODGroupData");

            int instanceDataBufferSize = _renderSourceGroup.BufferSize * (lodGroupData.Length
                * (_renderSourceGroup.Profile.isShadowCasting ? 2 : 1)  // multiply with 2 for shadows
                + (_renderSourceGroup.Profile.isLODCrossFade && _renderSourceGroup.Profile.isAnimateCrossFade && !IsCameraBasedBuffer ? 1 : 0) // +1 for keeping crossfading state
                );

            if (!_instanceDataBufferDict.TryGetValue(key, out GPUIShaderBuffer instanceDataBuffer) || instanceDataBuffer == null || instanceDataBuffer.BufferSize != instanceDataBufferSize)
            {
                if (instanceDataBuffer != null)
                    instanceDataBuffer.Dispose();
                instanceDataBuffer = CreateInstanceDataBuffer(instanceDataBufferSize);
                _instanceDataBufferDict[key] = instanceDataBuffer;
            }
            return instanceDataBuffer;
        }

        public void SetMPBBuffers(MaterialPropertyBlock mpb, GPUICameraData cameraData)
        {
            GPUIShaderBuffer transformBuffer = GetTransformBuffer(cameraData);
            GPUIShaderBuffer instanceDataBuffer = GetInstanceDataBuffer(cameraData);
            if (GPUIRuntimeSettings.Instance.DisableShaderBuffers)
            {
                mpb.SetTexture(GPUIConstants.PROP_gpuiTransformBufferTexture, transformBuffer.Texture);
                mpb.SetTexture(GPUIConstants.PROP_gpuiInstanceDataBufferTexture, instanceDataBuffer.Texture);
            }
            else
            {
                mpb.SetBuffer(GPUIConstants.PROP_gpuiTransformBuffer, transformBuffer.Buffer);
                mpb.SetBuffer(GPUIConstants.PROP_gpuiInstanceDataBuffer, instanceDataBuffer.Buffer);

                bool hasMotionVectorBuffer = HasPreviousFrameTransformBuffer && PreviousFrameTransformBuffer != null;
                mpb.SetInt(GPUIConstants.PROP_hasPreviousFrameTransformBuffer, hasMotionVectorBuffer ? 1 : 0);
                if (hasMotionVectorBuffer)
                {
                    int bufferSize = transformBuffer.Buffer.count;
                    if (PreviousFrameTransformBuffer.count < bufferSize)
                    {
                        int previousBufferSize = PreviousFrameTransformBuffer.count;
                        GraphicsBuffer newBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, 4 * 4 * 4);
                        newBuffer.SetData(PreviousFrameTransformBuffer, 0, 0, previousBufferSize);
                        newBuffer.SetData(transformBuffer.Buffer, previousBufferSize, previousBufferSize, bufferSize - previousBufferSize);
                        PreviousFrameTransformBuffer.Release();
                        PreviousFrameTransformBuffer = newBuffer;
                    }
                    mpb.SetBuffer(GPUIConstants.PROP_gpuiPreviousFrameTransformBuffer, PreviousFrameTransformBuffer);
                }
            }
            mpb.SetFloat(GPUIConstants.PROP_transformBufferSize, _renderSourceGroup.BufferSize);
            mpb.SetFloat(GPUIConstants.PROP_instanceDataBufferSize, instanceDataBuffer.BufferSize);
            mpb.SetFloat(GPUIConstants.PROP_maxTextureSize, GPUIConstants.TEXTURE_MAX_SIZE);
        }

        internal void UpdateData(int frameNo)
        {
            if (HasPreviousFrameTransformBuffer)
            {
                if (_renderSourceGroup.BufferSize > 0 && frameNo > _previousFrameBufferFrameNo && _transformBufferDict.TryGetValue(0, out GPUIShaderBuffer transformBuffer) && transformBuffer.Buffer != null)
                {
                    _previousFrameBufferFrameNo = frameNo;
                    int bufferSize = transformBuffer.Buffer.count;
                    if (PreviousFrameTransformBuffer == null)
                        PreviousFrameTransformBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, 4 * 4 * 4);
                    else if (PreviousFrameTransformBuffer.count != bufferSize)
                    {
                        PreviousFrameTransformBuffer.Release();
                        PreviousFrameTransformBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, 4 * 4 * 4);
                    }

                    PreviousFrameTransformBuffer.SetData(transformBuffer.Buffer, 0, 0, bufferSize);
                }
            }
        }

        public void OnTransformDataModified()
        {
            if (_transformBufferDict == null)
                return;
            foreach (var tb in _transformBufferDict.Values)
            {
                if (tb != null)
                    tb.OnDataModified();
            }
            GPUIRenderingSystem.OnBufferDataModified?.Invoke(this);
        }

        public void OnTransformDataModified(GPUIShaderBuffer tb)
        {
            tb.OnDataModified();
            GPUIRenderingSystem.OnBufferDataModified?.Invoke(this);
        }

        public void ResetPreviousFrameBuffer()
        {
            UpdateData(_previousFrameBufferFrameNo + 1);
            _previousFrameBufferFrameNo = -1;
        }
    }

    public enum GPUITransformBufferType
    {
        /// <summary>
        /// One buffer for each prototype
        /// </summary>
        Default = 0,
        /// <summary>
        /// One buffer for each prototype and camera combination
        /// </summary>
        CameraBased = 1,
    }

    [Serializable]
    public struct GPUITransformData : IEquatable<GPUITransformData>
    {
        public Vector3 position;    // Translation (12 bytes)
        public Quaternion rotation; // Quaternion for rotation (16 bytes)
        public Vector3 scale;       // Non-uniform scale (12 bytes)

        public static GPUITransformData CreateFromMatrix(Matrix4x4 matrix)
        {
            return new GPUITransformData()
            {
                position = matrix.GetPosition(),
                rotation = matrix.rotation,
                scale = matrix.lossyScale
            };
        }

        public void SetToTransform(Transform transform)
        {
            transform.SetPositionAndRotation(position, rotation);
            transform.localScale = scale;
        }

        public bool Equals(GPUITransformData other)
        {
            return position == other.position && rotation == other.rotation && scale == other.scale;
        }

        public override bool Equals(object obj)
        {
            if (obj is GPUITransformData crowdTransformData)
                return Equals(crowdTransformData);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return GPUIUtility.GenerateHash(position.GetHashCode(), rotation.GetHashCode(), scale.GetHashCode());
        }

        public static bool operator ==(GPUITransformData left, GPUITransformData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GPUITransformData left, GPUITransformData right)
        {
            return !(left == right);
        }

        private static readonly string TO_STRING_TEXT = "Position: {0}, Rotation: {1}, Scale: {2}";
        public override string ToString()
        {
            return string.Format(TO_STRING_TEXT, position.ToString("F4"), rotation.ToString("F4"), scale.ToString("F4"));
        }
    };
}