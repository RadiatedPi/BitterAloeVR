// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace GPUInstancerPro.PrefabModule
{
    [Serializable]
    public class GPUIPrefabPrototypeData : GPUIPrototypeData
    {
        [SerializeField]
        public bool isAutoUpdateTransformData;
        [SerializeField]
        public GPUIPrefabInstances registeredInstances;

        #region Runtime Properties
        /// <summary>
        /// Matrix4x4 data for each instance
        /// </summary>
        [NonSerialized]
        public NativeArray<Matrix4x4> matrixArray;
        /// <summary>
        /// Transform for each instance
        /// </summary>
        [NonSerialized]
        public Transform[] instanceTransforms;
        /// <summary>
        /// TransformAccess for each instance
        /// </summary>
        [NonSerialized]
        public TransformAccessArray transformAccessArray;
        /// <summary>
        /// True when instanceTransforms array is modified, false after changes are applied to the transformAccessArray
        /// </summary>
        [NonSerialized]
        public bool isTransformReferencesModified;
        /// <summary>
        /// Unique prefab ID
        /// </summary>
        [NonSerialized]
        public int prefabID;
        /// <summary>
        /// List of instances to add to buffers
        /// </summary>
        [NonSerialized]
        public List<GPUIPrefab> instancesToAdd;
        /// <summary>
        /// List of instance indexes to remove from buffers
        /// </summary>
        [NonSerialized]
        public SortedSet<int> indexesToRemove;
        /// <summary>
        /// True when a change has been made to transform data
        /// </summary>
        [NonSerialized]
        public bool isMatrixArrayModified;
        /// <summary>
        /// Number of registered instances for this prototype
        /// </summary>
        [NonSerialized]
        internal int instanceCount;
        #endregion Runtime Properties

        #region Editor Properties
#if UNITY_EDITOR
        [NonSerialized]
        internal int editor_editModeRenderKey;
#endif
        #endregion Editor Properties

        public override bool Initialize(GPUIPrototype prototype)
        {
            if (base.Initialize(prototype))
            {
                instancesToAdd ??= new();
                indexesToRemove ??= new(new IntInverseComparer());
                prefabID = GPUIPrefabManager.GetPrefabID(prototype);
                matrixArray = new(0, Allocator.Persistent);

                return true;
            }

            return false;
        }

        public override void Dispose()
        {
            base.Dispose();
            instanceCount = 0;
        }

        public override void ReleaseBuffers()
        {
            base.ReleaseBuffers();
            if (matrixArray.IsCreated)
                matrixArray.Dispose();
            if (transformAccessArray.isCreated)
                transformAccessArray.Dispose();
        }

        public int GetRegisteredInstanceCount()
        {
            if (registeredInstances != null && registeredInstances.prefabInstances != null)
                return registeredInstances.prefabInstances.Length;
            return 0;
        }

        [Serializable]
        public class GPUIPrefabInstances
        {
            public GameObject[] prefabInstances;
        }

        private class IntInverseComparer : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                return y.CompareTo(x);
            }
        }
    }
}