// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace GPUInstancerPro
{
    public class GPUIDataBuffer<T> : IGPUIDisposable where T : struct
    {
        public GraphicsBuffer Buffer { get; private set; }
        public int Length { get; private set; }

        private readonly string _name;
        private readonly GraphicsBuffer.Target _target;
        private readonly int _stride;

        protected NativeArray<T> _data;
        protected bool _requireUpdate;

        private bool _isDataRequested;
        private bool _isRequestedDataInvalid;
        private AsyncGPUReadbackRequest _readbackRequest;
        private readonly System.Action<AsyncGPUReadbackRequest> _requestCallbackInternal;
        private List<System.Action<GPUIDataBuffer<T>>> _requestCallbacksExternal;
        private NativeArray<T> _requestedData;
        private bool _writeToDataAfterReadback;

        public GPUIDataBuffer(string name, int length = 0, GraphicsBuffer.Target target = GraphicsBuffer.Target.Structured)
        {
            if (length < 0)
                length = 0;
            this.Length = length;
            _name = name;
            _target = target;
            if (length > 0)
            {
                _data = new NativeArray<T>(length, Allocator.Persistent);
                _requireUpdate = true;
            }
            _stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
            _requestCallbackInternal = OnDataRequestCompleted;
            _requestCallbacksExternal = new List<Action<GPUIDataBuffer<T>>>();
        }

        public T this[int index]
        {
            get => _data[index];
            set
            {
                if (_isDataRequested && _writeToDataAfterReadback)
                    _readbackRequest.WaitForCompletion();
                _data[index] = value;
                _requireUpdate = true;
            }
        }

        public void Add(T element)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            int index = Length;
            Length++;
            _data.ResizeNativeArray(Length, Allocator.Persistent);
            _data[index] = element;
            _requireUpdate = true;
        }

        public void Add(params T[] elements)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            int index = Length;
            Length += elements.Length;
            _data.ResizeNativeArray(Length, Allocator.Persistent);
            for (int i = 0; i < elements.Length; i++)
            {
                _data[index + i] = elements[i];
            }
            _requireUpdate = true;
        }

        public void Add(List<T> elements)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            int index = Length;
            Length += elements.Count;
            _data.ResizeNativeArray(Length, Allocator.Persistent);
            for (int i = 0; i < elements.Count; i++)
            {
                _data[index + i] = elements[i];
            }
            _requireUpdate = true;
        }

        public void AddOrSet(int index, T element)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            if (!_data.IsCreated)
            {
                Length = index + 1;
                _data = new NativeArray<T>(Length, Allocator.Persistent);
            }
            else if (index >= Length)
            {
                Length = index + 1;
                _data.ResizeNativeArray(Length, Allocator.Persistent);
            }
            _data[index] = element;
            _requireUpdate = true;
        }

        public void SetData(int bufferStartIndex, int arrayStartIndex, int count, T[] array)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            NativeArray<T>.Copy(array, arrayStartIndex, _data, bufferStartIndex, count);
            _requireUpdate = true;
        }

        public void SetData(int bufferStartIndex, int count, T newValue)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            int end = count + bufferStartIndex;
            for (int i = bufferStartIndex; i < end; i++)
                _data[i] = newValue;
            _requireUpdate = true;
        }

        public void Resize(int newSize)
        {
            if (_isDataRequested && _writeToDataAfterReadback)
                _readbackRequest.WaitForCompletion();
            Length = newSize;
            if (newSize <= 0)
            {
                ReleaseBuffers();
                return;
            }
            _data.ResizeNativeArray(Length, Allocator.Persistent);
            _requireUpdate = true;
        }

        public bool UpdateBufferData(bool forceUpdate = false)
        {
            if (forceUpdate || _requireUpdate)
            {
                SetBufferData();
                return true;
            }
            return false;
        }

        public void SetBufferData()
        {
            if (_isDataRequested)
                _readbackRequest.WaitForCompletion();
            _requireUpdate = false;
            if (Length == 0 || !_data.IsCreated)
            {
                ReleaseBuffers();
                return;
            }
            if (Buffer != null && Buffer.count != Length)
            {
                Buffer.Release();
                Buffer = null;
            }
            if (Buffer == null)
                Buffer = new(_target, Length, _stride);
            SetBufferDataInternal();
        }

        protected virtual void SetBufferDataInternal()
        {
            Buffer.SetData(_data);
        }

        public virtual void ReleaseBuffers()
        {
            if (_isDataRequested)
            {
                _isRequestedDataInvalid = true;
                _readbackRequest.WaitForCompletion();
            }
            if (Buffer != null)
            {
                Buffer.Release();
                Buffer = null;
            }
            if (_data.IsCreated)
                _data.Dispose();
            Length = 0;
            if (!_isDataRequested && _requestedData.IsCreated)
                _requestedData.Dispose();
        }

        public void Dispose()
        {
            ReleaseBuffers();
        }

        public T[] GetBufferData()
        {
            UpdateBufferData();
            T[] data = new T[Length];
            if (Buffer != null)
                Buffer.GetData(data);
            _data.CopyFrom(data);
            return data;
        }

        public NativeArray<T> GetNativeData(bool isReadFromGPU = false)
        {
            if (isReadFromGPU)
            {
                AsyncDataRequest(null, true);
                if (_isDataRequested)
                    _readbackRequest.WaitForCompletion();
            }
            return _data;
        }

        public bool AsyncDataRequest(Action<GPUIDataBuffer<T>> callback, bool writeToDataAfterReadback)
        {
            if (Buffer == null)
                return false;
            
            if (!_isDataRequested)
            {
                _writeToDataAfterReadback = writeToDataAfterReadback;
                if (!_requestedData.IsCreated || _requestedData.Length != Length)
                {
                    if (_requestedData.IsCreated)
                        _requestedData.Dispose();
                    _requestedData = new NativeArray<T>(Length, Allocator.Persistent);
                }
                _readbackRequest = AsyncGPUReadback.RequestIntoNativeArray(ref _requestedData, Buffer, _requestCallbackInternal);
                _isDataRequested = true;
            }
            else if (writeToDataAfterReadback != _writeToDataAfterReadback)
            {
                Debug.LogWarning("There is another AsyncDataRequest pending results.");
                return false;
            }
            _requestCallbacksExternal.Add(callback);
            return true;
        }

        private void OnDataRequestCompleted(AsyncGPUReadbackRequest obj)
        {
            _isDataRequested = false;
            if (obj.hasError)
            {
                Debug.LogError("Async data request has encountered an error. Data buffer name: " + _name);
                return;
            }
            if (_isRequestedDataInvalid)
            {
                _isRequestedDataInvalid = false;
                if (_requestedData.IsCreated)
                {
                    //Debug.Log("Disposing invalid requested data...");
                    _requestedData.Dispose();
                }
                return;
            }
            if (_writeToDataAfterReadback)
            {
                if (!_data.IsCreated)
                    return;
                if (_data.Length != _requestedData.Length)
                {
                    if (Application.isPlaying)
                        Debug.LogError("GPUI async data request size mismatch. Data length: " + _data.Length + " requested length: " + _requestedData.Length + " name: " + _name);
                    return;
                }
                _data.CopyFrom(_requestedData);
            }
            foreach (var callback in _requestCallbacksExternal)
                callback?.Invoke(this);
            _requestCallbacksExternal.Clear();
        }

        public bool IsDataRequested()
        {
            return _isDataRequested;
        }

        public NativeArray<T> GetRequestedData()
        {
            if (_isDataRequested)
                return default;
            return _requestedData;
        }

        public void WaitForReadbackCompletion()
        {
            if (_isDataRequested)
                _readbackRequest.WaitForCompletion();
        }

        public void RequireUpdate()
        {
            _requireUpdate = true;
        }
    }

    public struct GPUICounterData
    {
        public uint count;
        public uint dummy1; // We need padding because GPU loads data in 16 bytes and we use counters with InterlockedAdd
        public uint dummy2;
        public uint dummy3;
    }
}