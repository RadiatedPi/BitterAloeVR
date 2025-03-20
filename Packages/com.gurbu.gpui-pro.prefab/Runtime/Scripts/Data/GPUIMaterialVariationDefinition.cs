// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GPUInstancerPro.PrefabModule
{
    [CreateAssetMenu(menuName = "Rendering/GPU Instancer Pro/Material Variation Definition", order = 811)]
    [HelpURL("https://wiki.gurbu.com/index.php?title=GPU_Instancer_Pro:BestPractices#Prefab_Manager_Material_Variations")]
    public class GPUIMaterialVariationDefinition : ScriptableObject
    {
        [SerializeField]
        public Material material;
        [SerializeField]
        public Shader replacementShader;
        [SerializeField]
        public string bufferName = "variationBuffer";
        [SerializeField]
        public GPUIMVDefinitionItem[] items;
#if UNITY_EDITOR
        [SerializeField]
        public ShaderInclude shaderIncludeFile;
#endif

        public void AddVariation(int renderKey, int bufferIndex, Vector4 value)
        {
            GPUIMaterialVariationData variationData = GPUIMaterialVariationDataProvider.GetMaterialVariationData(this, renderKey);

            variationData.AddVariation(renderKey, bufferIndex, value);
        }
    }

    [Serializable]
    public struct GPUIMVDefinitionItem
    {
        public string propertyName;
        public GPUIMaterialVariationType variationType;
        public Vector4 defaultValue;
    }

    public enum GPUIMaterialVariationType
    {
        Vector4 = 0,
        Color = 1,
        Integer = 2,
        Float = 3,
    }
}
