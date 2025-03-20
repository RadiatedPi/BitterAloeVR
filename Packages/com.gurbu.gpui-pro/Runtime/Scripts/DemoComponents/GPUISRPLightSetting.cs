// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancerPro
{
    [RequireComponent(typeof(Light))]
    [DefaultExecutionOrder(-1000)]
    [ExecuteInEditMode]
    public class GPUISRPLightSetting : MonoBehaviour
    {
        [Header("URP")]
        public float uRPIntensity = 1f;
        [Header("HDRP")]
        public float hDRPIntensity = 100000f;
        [Range(0, 3)]
        public int hDRPShadowResolutionLevel = 2;

        private void OnEnable()
        {
            switch (GPUIRuntimeSettings.Instance.RenderPipeline)
            {
#if GPUI_HDRP
                case GPUIRenderPipeline.HDRP:
                    var hdLight = gameObject.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                    if (hdLight != null)
                    {
#if UNITY_6000_0_OR_NEWER
                        Light lightHDRP = GetComponent<Light>();
                        lightHDRP.intensity = hDRPIntensity;
#else
                        hdLight.intensity = hDRPIntensity;
#endif
                        hdLight.SetShadowResolutionOverride(false);
                        hdLight.SetShadowResolutionLevel(hDRPShadowResolutionLevel);
                    }
                    break;
#endif
#if GPUI_URP
                case GPUIRenderPipeline.URP:
                    Light lightURP = GetComponent<Light>();
                    lightURP.intensity = uRPIntensity;
                    break;
#endif
                default:
                    break;
            }
        }
    }
}
