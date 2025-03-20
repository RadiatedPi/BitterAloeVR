// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIShaderBuffer : IGPUIDisposable
    {
        public GraphicsBuffer Buffer { get; private set; }
        public RenderTexture Texture { get; private set; }
        public int BufferSize { get; private set; }

        private bool _isBufferToTextureFloat4;

        public GPUIShaderBuffer(int bufferSize, int stride)
        {
            if (bufferSize > GPUIConstants.MAX_BUFFER_SIZE)
            {
                Debug.LogError(bufferSize.ToString("#,0") + " exceeds maximum allowed buffer size (" + GPUIConstants.MAX_BUFFER_SIZE.ToString("#,0") + ").");
                return;
            }
            if (bufferSize > 0)
                Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferSize, stride);
            BufferSize = bufferSize;
            if (GPUIRuntimeSettings.Instance.DisableShaderBuffers)
            {
                int rowCount = Mathf.CeilToInt(bufferSize / (float)GPUIConstants.TEXTURE_MAX_SIZE);
                Texture = new RenderTexture(rowCount == 1 ? bufferSize : GPUIConstants.TEXTURE_MAX_SIZE, rowCount * Mathf.CeilToInt(stride / 16.0f), 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                {
                    isPowerOfTwo = false,
                    enableRandomWrite = true,
                    filterMode = FilterMode.Point,
                    useMipMap = false,
                    autoGenerateMips = false,
                    useDynamicScale = false,
                    wrapMode = TextureWrapMode.Clamp
                };
                Texture.Create();

                _isBufferToTextureFloat4 = stride > 16 ? false : true;
            }
        }

        public void ReleaseBuffers()
        {
            if (Buffer != null)
                Buffer.Release();
            Buffer = null;
            GPUITextureUtility.DestroyRenderTexture(Texture);
            Texture = null;
            BufferSize = 0;
        }

        public void Dispose()
        {
            ReleaseBuffers();
        }

        public void OnDataModified()
        {
            if (Buffer == null)
                return;
            if (GPUIRuntimeSettings.Instance.DisableShaderBuffers)
            {
                ComputeShader cs = GPUIConstants.CS_BufferToTexture;
                if (_isBufferToTextureFloat4)
                    cs.EnableKeyword(GPUIConstants.Kw_GPUI_FLOAT4_BUFFER);
                else
                    cs.DisableKeyword(GPUIConstants.Kw_GPUI_FLOAT4_BUFFER);

                cs.SetBuffer(0, GPUIConstants.PROP_sourceBuffer, Buffer);
                cs.SetTexture(0, GPUIConstants.PROP_targetTexture, Texture);
                cs.SetInt(GPUIConstants.PROP_count, BufferSize);
                cs.SetInt(GPUIConstants.PROP_maxTextureSize, GPUIConstants.TEXTURE_MAX_SIZE);
                cs.DispatchX(0, Buffer.count);

                Texture.IncrementUpdateCount();
            }
        }

        public void SetBuffer(ComputeShader cs, int kernelIndex, int nameID)
        {
            if (Buffer != null)
                cs.SetBuffer(kernelIndex, nameID, Buffer);
            else if (Texture != null)
                cs.SetTexture(kernelIndex, nameID, Texture);
        }
    }
}