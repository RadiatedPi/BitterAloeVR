// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GPUInstancerPro.PrefabModule
{
    /// <summary>
    /// This component is automatically attached to prefabs that are used as GPUI prototypes to identify them
    /// </summary>
    [DefaultExecutionOrder(200)]
    [HelpURL("https://wiki.gurbu.com/index.php?title=GPU_Instancer_Pro:GettingStarted#GPUI_Prefab")]
    public class GPUIPrefab : GPUIPrefabBase
    {
        /// <summary>
        /// Unique identifier to find instances of a prefab
        /// </summary>
        [SerializeField]
        private int _prefabID;

        [SerializeField]
        private bool _isRenderersDisabled;
        /// <summary>
        /// True when Mesh Renderer components are disabled
        /// </summary>
        public bool IsRenderersDisabled => _isRenderersDisabled;

        /// <summary>
        /// Prefab Manager that is currently rendering this instance
        /// </summary>
        public GPUIPrefabManager registeredManager { get; internal set; }

        /// <summary>
        /// Render key on the registered manager
        /// </summary>
        public int renderKey { get; internal set; }

        /// <summary>
        /// Buffer index of the instance
        /// </summary>
        public int bufferIndex { get; private set; }

        [NonSerialized]
        public UnityEvent OnInstancingStatusModified;

        public bool IsInstanced => renderKey != 0/* && registeredManager != null && registeredManager.IsInitialized*/;

        private Transform _cachedTransform;
        public Transform CachedTransform
        {
            get
            {
                if (_cachedTransform == null)
                    _cachedTransform = transform;
                return _cachedTransform;
            }
        }

        internal bool _isBeingAddedToThePrefabManager;

        private static List<Renderer> _rendererList;

        internal void SetInstancingData(GPUIPrefabManager registeredManager, int prefabID, int renderKey, int bufferIndex)
        {
            //Debug.Assert(_prefabID == 0 || _prefabID == prefabID, "Prefab ID mismatch. Current ID: " + _prefabID + " Given ID: " + prefabID, gameObject);
            this.registeredManager = registeredManager;
            this.renderKey = renderKey;
            this.bufferIndex = bufferIndex;
            if (_prefabID == 0)
                _prefabID = prefabID;
            registeredManager.SetPrefabInstanceRenderersEnabled(this, false);
            _isBeingAddedToThePrefabManager = false;
            OnInstancingStatusModified?.Invoke();
        }

        internal void ClearInstancingData(bool enableRenderers)
        {
            if (enableRenderers && registeredManager != null)
                registeredManager.SetPrefabInstanceRenderersEnabled(this, true);
            registeredManager = null;
            renderKey = 0;
            bufferIndex = -1;
            _isBeingAddedToThePrefabManager = false;
            OnInstancingStatusModified?.Invoke();
        }

        public void RemovePrefabInstance()
        {
            if (!IsInstanced) return;
            if (!registeredManager.RemovePrefabInstance(this))
                Debug.LogError("Can not remove prefab instance with prefab ID: " + GetPrefabID(), this);
        }

        internal void UpdateTransformData()
        {
            if (!IsInstanced) return;
            if (CachedTransform.hasChanged)
                registeredManager.UpdateTransformData(this);
        }

        public void SetRenderersEnabled(bool enabled)
        {
            if (_isRenderersDisabled != enabled)
                return;
            Profiler.BeginSample("GPUIPrefabManager.SetRenderersEnabled");

            _rendererList ??= new List<Renderer>();
            GetComponentsInChildren(enabled, _rendererList);
            foreach (Renderer renderer in _rendererList)
            {
                if (renderer is MeshRenderer || renderer is BillboardRenderer || renderer is SkinnedMeshRenderer)
                    renderer.enabled = enabled;
            }

            if (TryGetComponent(out LODGroup lodGroup))
                lodGroup.enabled = enabled;
            _isRenderersDisabled = !enabled;
            Profiler.EndSample();
        }

        internal void SetBufferIndex(int bufferIndex)
        {
            this.bufferIndex = bufferIndex;
            OnInstancingStatusModified?.Invoke();
        }

        public int GetPrefabID()
        {
#if UNITY_EDITOR
            if (_prefabID == 0 && !Application.isPlaying)
            {
                if (GPUIPrefabUtility.IsPrefabAsset(gameObject, out GameObject prefabObject, false))
                {
                    if (gameObject == prefabObject)
                    {
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefabObject.GetInstanceID(), out string guid, out long localId))
                        {
                            Undo.RecordObject(this, "Set Prefab ID");
                            _prefabID = guid.GetHashCode();
#if GPUIPRO_DEVMODE
                            Debug.Log(name + " prefab ID set to: " + _prefabID, gameObject);
#endif
                            EditorUtility.SetDirty(gameObject);
                            GPUIPrefabUtility.MergeAllPrefabInstances(gameObject);
                        }
                    }
                    else
                        PrefabUtility.RevertPrefabInstance(gameObject, InteractionMode.AutomatedAction);
                }
            }
#endif
            return _prefabID;
        }

#if UNITY_EDITOR
        public void Reset()
        {
            Undo.RecordObject(this, "Reset Prefab ID");
#if GPUIPRO_DEVMODE
            Debug.Log(name + " prefab ID reset.");
#endif
            _prefabID = 0;
            GetPrefabID();
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }
}