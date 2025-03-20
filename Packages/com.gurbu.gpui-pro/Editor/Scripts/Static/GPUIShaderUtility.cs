// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace GPUInstancerPro
{
    public static class GPUIShaderUtility
    {
        private static string INCLUDE_BUILTIN = "#include \"UnityCG.cginc\"\n";
        private static string INCLUDE_URP = "#include \"Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl\"\n";
        private static string INCLUDE_HDRP = "#include \"Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl\"\n#include \"Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl\"\n";

        private static string BUILTIN_START_DEFINES = "";
        private static string URP_START_DEFINES = "";
        private static string HDRP_START_DEFINES = "";

        private static string SEARCH_START_BUILTIN = "CGPROGRAM";
        private static string SEARCH_END_BUILTIN = "ENDCG";
        private static string SEARCH_START_SRP = "HLSLPROGRAM";
        private static string SEARCH_END_SRP = "ENDHLSL";
        private static string PRAGMA_GPUI_SETUP = "#pragma instancing_options procedural:setupGPUI\n#pragma multi_compile_instancing\n";
        private static string FILE_NAME_SUFFIX = "_GPUIPro";

        public static void AutoShaderConverterUpdate()
        {
            GPUIMaterialProvider materialProvider = GPUIRenderingSystem.Instance.MaterialProvider;
            if (materialProvider.checkForShaderModifications)
            {
                materialProvider.checkForShaderModifications = false;
                CheckForShaderModifications();
            }
            if (materialProvider.TryGetShaderToConvert(out GPUIMaterialProvider.GPUIShaderConversionData shaderConversionData))
            {
                if (!SetupShaderForGPUI(shaderConversionData.shader, shaderConversionData.extensionCode, false, true))
                    materialProvider.AddToFailedShaderConversions(shaderConversionData);
            }
            if (materialProvider.TryGetMaterialVariant(out Material material) && GPUIEditorSettings.Instance.isGenerateShaderVariantCollection)
                AddShaderVariantToCollection(material.shader, material.shaderKeywords);
        }

        public static bool SetupShaderForGPUI(Shader shader, string extensionCode, bool logIfExists = true, bool logError = true)
        {
            if (shader == null || shader.name == GPUIConstants.SHADER_UNITY_INTERNAL_ERROR)
            {
                if (logError)
                    Debug.LogError("Can not find shader! Please make sure that the material has a shader assigned.");
                return false;
            }
            GPUIShaderBindings.Instance.ClearEmptyShaderInstances();
            if (!GPUIShaderBindings.Instance.IsShaderSetupForGPUI(shader.name, extensionCode))
            {
                if (IsShaderInstanced(shader, extensionCode))
                {
                    GPUIShaderBindings.Instance.AddShaderInstance(shader.name, shader, extensionCode, true);
                    if (logIfExists)
                        Debug.Log("Shader setup for " + shader.name + " has been successfully completed.", shader);
                    return true;
                }
                else
                {
                    Shader namedShader = Shader.Find(GPUIUtility.ConvertToGPUIShaderName(shader.name, extensionCode));
                    if (namedShader == null && ProcessShaderImports(shader, extensionCode))
                        return true;

                    if (namedShader != null && IsShaderInstanced(namedShader, extensionCode))
                    {
                        GPUIShaderBindings.Instance.AddShaderInstance(shader.name, namedShader, extensionCode, false);
                        return true;
                    }

                    Shader instancedShader = CreateInstancedShader(shader, extensionCode);
                    if (instancedShader != null && !string.IsNullOrEmpty(instancedShader.name))
                    {
                        GPUIShaderBindings.Instance.AddShaderInstance(shader.name, instancedShader, extensionCode);
                        return true;
                    }
                    else
                    {
                        if (logError)
                            LogShaderError(shader);

                        return false;
                    }
                }
            }
            else
            {
                if (logIfExists)
                    Debug.Log(shader.name + " shader has already been setup for GPUI.", shader);
                return true;
            }
        }

        private static readonly string SPEEDTREE8_SHADER_PACKAGE = "Packages/com.gurbu.gpui-pro/Editor/Extras/Shader_SpeedTree8_GPUIPro.unitypackage";
        private static bool SPEEDTREE8_SHADER_IMPORTED = false;
        private static bool ProcessShaderImports(Shader shader, string extensionCode)
        {
            if (shader.name == GPUIConstants.SHADER_UNITY_SPEEDTREE8)
            {
                if (SPEEDTREE8_SHADER_IMPORTED)
                    return true;
                SPEEDTREE8_SHADER_IMPORTED = true;
                Debug.Log("Importing GPUI shader package: " + SPEEDTREE8_SHADER_PACKAGE);
                EditorUtility.DisplayCancelableProgressBar("Importing Package", "Importing SpeedTree8 shader...", 0.11f);
                AssetDatabase.ImportPackage(SPEEDTREE8_SHADER_PACKAGE, false);
                EditorUtility.ClearProgressBar();
                return true;
            }
            return false;
        }

        private static void LogShaderError(Shader shader)
        {
            string originalAssetPath = AssetDatabase.GetAssetPath(shader).ToLower();
            if (originalAssetPath.EndsWith(".shadergraph"))
                Debug.LogError(string.Format(GPUIEditorConstants.ERRORTEXT_shaderGraph, shader.name), shader);
            else if (originalAssetPath.EndsWith(".surfshader"))
                Debug.LogError(string.Format(GPUIEditorConstants.ERRORTEXT_surfshader, shader.name), shader);
            else if (originalAssetPath.EndsWith(".stackedshader"))
                Debug.LogError(string.Format(GPUIEditorConstants.ERRORTEXT_stackedshader, shader.name), shader);
            else
                Debug.LogError(string.Format(GPUIEditorConstants.ERRORTEXT_shaderConversion, shader.name), shader);
        }

        public static bool IsShaderInstanced(Shader shader, string extensionCode)
        {
            if (shader == null || shader.name == GPUIConstants.SHADER_UNITY_INTERNAL_ERROR)
            {
                Debug.LogError("Can not find shader! Please make sure that the material has a shader assigned.");
                return false;
            }
            string originalAssetPath = AssetDatabase.GetAssetPath(shader);
            string originalShaderText;
            try
            {
                originalShaderText = System.IO.File.ReadAllText(originalAssetPath);
            }
            catch (Exception)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(originalShaderText))
            {
                switch (extensionCode)
                {
#if GPUI_CROWD
                    case GPUIConstants.EXTENSION_CODE_CROWD:
                        if (originalAssetPath.ToLower().EndsWith(".shadergraph"))
                            return originalShaderText.Contains("GPUIPro Crowd Setup");
                        else if (originalAssetPath.ToLower().EndsWith(".stackedshader"))
                            return originalShaderText.Contains("08196cc3a5cc8d44698d705c7fde3b68"); // guid for GPUI Stackable for Better Shaders
                        else
                            return originalShaderText.Contains("GPUICrowdSetup.hlsl");
#endif
                    default:
                        if (originalAssetPath.ToLower().EndsWith(".shadergraph"))
                            return originalShaderText.Contains("GPU Instancer Pro Setup") || originalShaderText.Contains("GPUIVariationSetup");
                        else if (originalAssetPath.ToLower().EndsWith(".stackedshader"))
                            return originalShaderText.Contains("08196cc3a5cc8d44698d705c7fde3b68"); // guid for GPUI Stackable for Better Shaders
                        else
                            return originalShaderText.Contains("GPUInstancerSetup.hlsl") || originalShaderText.Contains("GPUICrowdSetup.hlsl");
                }
            }
            return false;
        }

        public static void RegenerateShaders()
        {
            GPUIShaderBindings.Instance.ClearEmptyShaderInstances();
            List<GPUIShaderInstance> shaderInstances = GPUIShaderBindings.Instance.shaderInstances;
            if (shaderInstances != null)
            {
                for (int i = 0; i < shaderInstances.Count; i++)
                {
                    GPUIShaderInstance shaderInstance = shaderInstances[i];
                    Shader originalShader = Shader.Find(shaderInstance.shaderName);
                    if (originalShader == null) continue;
                    if (!IsShaderInstanced(originalShader, shaderInstance.extensionCode))
                    {
                        Shader replacementShader = CreateInstancedShader(originalShader, shaderInstance.extensionCode);
                        if (replacementShader == null)
                        {
                            shaderInstances.RemoveAt(i);
                            i--;
                            continue;
                        }
                        shaderInstance.replacementShaderName = replacementShader.name;
                        shaderInstance.modifiedDate = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else
                        shaderInstance.isUseOriginal = true;
                }
                EditorUtility.SetDirty(GPUIShaderBindings.Instance);
            }
        }

        public static void RegenerateShader(string originalShaderName)
        {
            if (string.IsNullOrEmpty(originalShaderName))
                return;
            List<GPUIShaderInstance> shaderInstances = GPUIShaderBindings.Instance.shaderInstances;
            Shader originalShader = Shader.Find(originalShaderName);
            if (shaderInstances != null && originalShader != null)
            {
                for (int i = 0; i < shaderInstances.Count; i++)
                {
                    GPUIShaderInstance shaderInstance = shaderInstances[i];
                    if (shaderInstance.shaderName == originalShaderName)
                    {
                        if (!IsShaderInstanced(originalShader, shaderInstance.extensionCode))
                        {
                            Shader replacementShader = CreateInstancedShader(originalShader, shaderInstance.extensionCode);
                            if (replacementShader == null)
                            {
                                LogShaderError(originalShader);
                                return;
                            }
                            shaderInstance.replacementShaderName = replacementShader.name;
                            shaderInstance.modifiedDate = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                            shaderInstance.isUseOriginal = true;
                    }
                }
                EditorUtility.SetDirty(GPUIShaderBindings.Instance);
            }
        }

        public static void RemoveShaderInstance(string originalShaderName)
        {
            if (string.IsNullOrEmpty(originalShaderName))
                return;
            List<GPUIShaderInstance> shaderInstances = GPUIShaderBindings.Instance.shaderInstances;
            if (shaderInstances != null)
            {
                for (int i = 0; i < shaderInstances.Count; i++)
                {
                    GPUIShaderInstance shaderInstance = shaderInstances[i];
                    if (shaderInstance.shaderName == originalShaderName)
                    {
                        shaderInstances.RemoveAt(i);
                        break;
                    }
                }
                EditorUtility.SetDirty(GPUIShaderBindings.Instance);
            }
        }

        public static void CheckForShaderModifications()
        {
            List<GPUIShaderInstance> shaderInstances = GPUIShaderBindings.Instance.shaderInstances;
            if (shaderInstances != null)
            {
#if UNITY_EDITOR
                bool modified = false;

                modified |= shaderInstances.RemoveAll(si => si == null || si.replacementShader == null || string.IsNullOrEmpty(si.shaderName)) > 0;

                if (GPUIEditorSettings.Instance.isAutoShaderConversion)
                {
                    for (int i = 0; i < shaderInstances.Count; i++)
                    {
                        GPUIShaderInstance shaderInstance = shaderInstances[i];
                        if (shaderInstance.isUseOriginal)
                            continue;

                        Shader originalShader = Shader.Find(shaderInstance.shaderName);
                        if (originalShader == null)
                        {
                            modified = true;
                            shaderInstances.RemoveAt(i);
                            i--;
                            continue;
                        }
                        string originalAssetPath = AssetDatabase.GetAssetPath(originalShader);
                        DateTime lastWriteTime = System.IO.File.GetLastWriteTime(originalAssetPath);
                        if (lastWriteTime >= DateTime.Now)
                            continue;

                        DateTime instancedTime = DateTime.MinValue;
                        bool isValidDate = false;
                        if (!string.IsNullOrEmpty(shaderInstance.modifiedDate))
                            isValidDate = DateTime.TryParseExact(shaderInstance.modifiedDate, "MM/dd/yyyy HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out instancedTime);
                        if (!isValidDate || lastWriteTime > Convert.ToDateTime(shaderInstance.modifiedDate, System.Globalization.CultureInfo.InvariantCulture))
                        {
                            modified = true;
                            if (!IsShaderInstanced(originalShader, shaderInstance.extensionCode))
                            {
                                shaderInstance.replacementShaderName = CreateInstancedShader(originalShader, shaderInstance.extensionCode).name;
                                shaderInstance.modifiedDate = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture);
                            }
                            else
                                shaderInstance.isUseOriginal = true;
                        }
                    }
                }

                // remove non unique instances
                for (int i = 0; i < shaderInstances.Count; i++)
                {
                    for (int j = 0; j < shaderInstances.Count; j++)
                    {
                        if (i == j)
                            continue;
                        if (shaderInstances[i].shaderName == shaderInstances[j].shaderName && shaderInstances[i].extensionCode == shaderInstances[j].extensionCode)
                        {
                            shaderInstances.RemoveAt(i);
                            i--;
                            modified = true;
                            break;
                        }
                    }
                }

                if (modified)
                    EditorUtility.SetDirty(GPUIShaderBindings.Instance);
#endif
            }
        }

        #region Auto Shader Conversion

        #region Auto Shader Conversion Helper Methods

        private static string GetShaderIncludePath(string originalAssetPath, bool createInDefaultFolder, out string newAssetPath)
        {
            string includePath = GPUIConstants.GetPackagesPath() + "Runtime/Shaders/Include/GPUInstancerSetup.hlsl";
            newAssetPath = originalAssetPath;
            string[] oapSplit = originalAssetPath.Split('/');
            if (createInDefaultFolder)
            {
                string generatedShaderPath = GPUIConstants.GetGeneratedShaderPath();
                if (!System.IO.Directory.Exists(generatedShaderPath))
                    System.IO.Directory.CreateDirectory(generatedShaderPath);

                newAssetPath = generatedShaderPath + oapSplit[oapSplit.Length - 1];
            }

            return includePath;
        }


        private static string AddIncludeAndPragmaDirectives(string includePath, string newShaderText, string setupText, bool doubleEscape)
        {
            int lastIndex = 0;
            string searchStart = GPUIRuntimeSettings.Instance.IsBuiltInRP ? SEARCH_START_BUILTIN : SEARCH_START_SRP;
            string searchEnd = GPUIRuntimeSettings.Instance.IsBuiltInRP ? SEARCH_END_BUILTIN : SEARCH_END_SRP;

            string additionTextEnd = "\n";
            string additionTextStart = "\n";
            switch (GPUIRuntimeSettings.Instance.RenderPipeline)
            {
                case GPUIRenderPipeline.BuiltIn:
                    additionTextStart += BUILTIN_START_DEFINES;
                    additionTextEnd += INCLUDE_BUILTIN;
                    break;
                case GPUIRenderPipeline.URP:
                    additionTextStart += URP_START_DEFINES;
                    additionTextEnd += INCLUDE_URP;
                    break;
                case GPUIRenderPipeline.HDRP:
                    additionTextStart += HDRP_START_DEFINES;
                    additionTextEnd += INCLUDE_HDRP;
                    break;
            }
            //additionTextStart += "#include_with_pragmas \"" + includePath.Replace("Setup.hlsl", "Input.hlsl") + "\"\n";
            additionTextEnd += "#include_with_pragmas \"" + includePath + "\"\n" + setupText + "\n";

            if (doubleEscape)
            {
                additionTextStart = additionTextStart.Replace("\n", "\\n").Replace("\"", "\\\"");
                additionTextEnd = additionTextEnd.Replace("\n", "\\n").Replace("\"", "\\\"");
            }

            while (true)
            {
                int foundIndex = newShaderText.IndexOf(searchStart, lastIndex);
                if (foundIndex == -1)
                    break;
                lastIndex = foundIndex + searchStart.Length + additionTextStart.Length + 1;

                newShaderText = newShaderText.Substring(0, foundIndex + searchStart.Length) + additionTextStart + newShaderText.Substring(foundIndex + searchStart.Length, newShaderText.Length - foundIndex - searchStart.Length);

                foundIndex = newShaderText.IndexOf(searchEnd, lastIndex);
                lastIndex = foundIndex + searchStart.Length + additionTextEnd.Length + 1;
                newShaderText = newShaderText.Substring(0, foundIndex) + additionTextEnd + newShaderText.Substring(foundIndex, newShaderText.Length - foundIndex);
            }

            return newShaderText;
        }

        private static Shader SaveInstancedShader(string newShaderText, string newAssetPath, string newShaderName)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(newShaderText);
            GPUIEditorUtility.VersionControlCheckout(newAssetPath);
            System.IO.FileStream fs = System.IO.File.Create(newAssetPath);
            fs.Write(bytes, 0, bytes.Length);
            fs.Close();
            //System.IO.File.WriteAllText(newAssetPath, newShaderText);
            EditorUtility.DisplayProgressBar("GPU Instancer Shader Conversion", "Importing instanced shader...", 0.8f);
            AssetDatabase.ImportAsset(newAssetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Shader instancedShader = AssetDatabase.LoadAssetAtPath<Shader>(newAssetPath);
            if (instancedShader == null)
                instancedShader = Shader.Find(newShaderName);

            return instancedShader;
        }

        private static bool IsIncludeLine(string line)
        {
            return !line.Contains("#pragma instancing_options")
                    && !line.Contains("#pragma multi_compile_instancing")
                    && !line.Contains("DOTS_INSTANCING_ON")
                    && !line.Contains("GPUInstancerSetup.hlsl")
                    && !line.Contains("DOTS.hlsl")
                    && !line.Contains("GPUInstancerInclude.cginc");
        }

        #endregion Auto Shader Conversion Helper Methods

        public static Shader CreateInstancedShader(Shader originalShader, string extensionCode, string setupText = null, bool useOriginal = false, string shaderNamePrefix = null, string fileNameSuffix = null, string createAtPath = null)
        {
            if (!string.IsNullOrEmpty(extensionCode))
            {
                return null;
            }
            try
            {
                if (originalShader == null || originalShader.name == GPUIConstants.SHADER_UNITY_INTERNAL_ERROR)
                {
                    Debug.LogError("Can not find shader! Please make sure that the material has a shader assigned.");
                    return null;
                }
                string originalShaderName = originalShader.name;
                Shader originalShaderRef = Shader.Find(originalShaderName);
                string originalAssetPath = AssetDatabase.GetAssetPath(originalShaderRef);
                string extension = System.IO.Path.GetExtension(originalAssetPath);

                bool isDoubleEscape = false;
                // can not work with ShaderGraph or other non shader code
                if (extension != ".shader")
                {
                    if (extension.EndsWith("pack"))
                        isDoubleEscape = true;
                    else
                        return null;
                }

                if (string.IsNullOrEmpty(setupText))
                    setupText = PRAGMA_GPUI_SETUP;
                if (string.IsNullOrEmpty(shaderNamePrefix))
                    shaderNamePrefix = GPUIConstants.GetShaderNamePrefix(extensionCode);
                if (string.IsNullOrEmpty(fileNameSuffix))
                    fileNameSuffix = FILE_NAME_SUFFIX;

                EditorUtility.DisplayProgressBar("GPU Instancer Shader Conversion", "Creating instanced shader for " + originalShaderName + ". Please wait...", 0.1f);

                #region Remove Existing procedural setup
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                using (System.IO.StreamReader sr = new System.IO.StreamReader(originalAssetPath))
                {
                    if (isDoubleEscape)
                    {
                        while (!sr.EndOfStream)
                        {
                            string line = sr.ReadLine();
                            string[] lineSplit = line.Split("\\n");
                            for (int i = 0; i < lineSplit.Length; i++)
                            {
                                line = lineSplit[i];
                                if (IsIncludeLine(line))
                                {
                                    sb.Append(line);
                                    if (i + 1 < lineSplit.Length)
                                        sb.Append("\\n");
                                }
                            }
                            if (!sr.EndOfStream)
                                sb.Append("\n");
                        }
                    }
                    else
                    {
                        while (!sr.EndOfStream)
                        {
                            string line = sr.ReadLine();
                            if (IsIncludeLine(line))
                            {
                                sb.Append(line);
                                if (!sr.EndOfStream)
                                    sb.Append("\n");
                            }
                        }
                    }
                }
                string originalShaderText = sb.ToString();
                if (string.IsNullOrEmpty(originalShaderText))
                {
                    EditorUtility.ClearProgressBar();
                    return null;
                }
                #endregion Remove Existing procedural setup

                bool createInDefaultFolder = false;
                // create shader versions for packages inside GPUI folder
                if (originalAssetPath.StartsWith("Packages/") && string.IsNullOrEmpty(createAtPath))
                    createInDefaultFolder = true;

                EditorUtility.DisplayProgressBar("GPU Instancer Shader Conversion", "Creating instanced shader for " + originalShaderName + ".  Please wait...", 0.5f);

                string newShaderName = useOriginal ? originalShaderName : GPUIUtility.ConvertToGPUIShaderName(originalShaderName, null, shaderNamePrefix);
                string newShaderText = originalShaderText.Replace("\r\n", "\n");
                newShaderText = useOriginal ? newShaderText : newShaderText.Replace("\"" + originalShaderName + "\"", "\"" + newShaderName + "\"");
                if (isDoubleEscape && !useOriginal)
                    newShaderText = newShaderText.Replace("\\\"" + originalShaderName + "\\\"", "\\\"" + newShaderName + "\\\"");

                string includePath = GetShaderIncludePath(originalAssetPath, createInDefaultFolder, out string newAssetPath);

                // Include paths fix
                if (createInDefaultFolder)
                {
                    string includeAddition = System.IO.Path.GetDirectoryName(originalAssetPath).Replace("\\", "/") + "/";

                    int lastIndex = 0;
                    string searchStart = "";
                    string searchEnd = "";
                    int foundIndex = -1;

                    lastIndex = 0;
                    searchStart = "#include \"";
                    searchEnd = "\"";
                    string restOfText;

                    foundIndex = -1;
                    while (true)
                    {
                        foundIndex = newShaderText.IndexOf(searchStart, lastIndex);
                        if (foundIndex == -1)
                            break;
                        lastIndex = foundIndex + searchStart.Length + 1;

                        restOfText = newShaderText.Substring(foundIndex + searchStart.Length, newShaderText.Length - foundIndex - searchStart.Length);
                        if (!restOfText.StartsWith("Packages") && !(restOfText.StartsWith("Unity") && GPUIRuntimeSettings.Instance.IsBuiltInRP))
                        {
                            newShaderText = newShaderText.Substring(0, foundIndex + searchStart.Length) + includeAddition + restOfText;
                            lastIndex += includeAddition.Length;
                        }

                        foundIndex = newShaderText.IndexOf(searchEnd, lastIndex);
                        lastIndex = foundIndex;
                    }
                }

                newShaderText = AddIncludeAndPragmaDirectives(includePath, newShaderText, setupText, isDoubleEscape);

                string originalFileName = System.IO.Path.GetFileName(newAssetPath);
                newAssetPath = useOriginal ? newAssetPath : newAssetPath.Replace(originalFileName, originalFileName.Replace(FILE_NAME_SUFFIX, "").Replace(extension, fileNameSuffix + extension));
                if (!string.IsNullOrEmpty(createAtPath))
                    newAssetPath = createAtPath;
                Shader instancedShader = SaveInstancedShader(newShaderText, newAssetPath, newShaderName);

                if (instancedShader != null)
                    Debug.Log("Generated GPUI support enabled version for shader: " + originalShaderName, instancedShader);
                EditorUtility.ClearProgressBar();

                return instancedShader;
            }
            catch (Exception e)
            {
                if (e is System.IO.DirectoryNotFoundException && e.Message.ToLower().Contains("unity_builtin_extra"))
                    Debug.LogError("\"" + originalShader.name + "\" shader is a built-in shader which is not included in GPUI package. Please download the original shader file from Unity Archive to enable auto-conversion for this shader. Check prototype settings on the Manager for instructions.");
                else
                    Debug.LogException(e);
                EditorUtility.ClearProgressBar();
            }
            return null;
        }

        #endregion Auto Shader Conversion

        #region Shader Variant Collection
        public static void AddShaderVariantToCollection(Material material, string extensionCode)
        {
            if (!GPUIEditorSettings.Instance.isGenerateShaderVariantCollection)
                return;

            if (material != null && material.shader != null)
            {
                if (GPUIShaderBindings.Instance.GetInstancedShader(material.shader.name, extensionCode, out Shader instancedShader))
                    AddShaderVariantToCollection(instancedShader, material.shaderKeywords);
            }
        }

        public static void AddShaderVariantToCollection(string shaderName, string extensionCode)
        {
            if (!GPUIEditorSettings.Instance.isGenerateShaderVariantCollection)
                return;

            if (GPUIShaderBindings.Instance.GetInstancedShader(shaderName, extensionCode, out Shader instancedShader))
                AddShaderVariantToCollection(instancedShader);
        }

        public static void AddShaderVariantToCollection(Shader shader, string[] keywords = null)
        {
            if (shader != null)
            {
                ShaderVariantCollection.ShaderVariant shaderVariant = new ShaderVariantCollection.ShaderVariant()
                {
                    shader = shader,
                    keywords = keywords
                };
                GPUIRuntimeSettings.Instance.VariantCollection.Add(shaderVariant);
            }
        }

        public static void AddDefaultShaderVariants()
        {
            if (!GPUIEditorSettings.Instance.isGenerateShaderVariantCollection)
                return;
            AddShaderVariantToCollection(Shader.Find(GPUIConstants.SHADER_GPUI_ERROR));
            AddShaderVariantToCollection(Shader.Find(GPUIConstants.SHADER_GPUI_TREE_PROXY));
        }

        public static void ClearShaderVariantCollection()
        {
            GPUIRuntimeSettings.Instance.VariantCollection.Clear();
            EditorUtility.SetDirty(GPUIRuntimeSettings.Instance.VariantCollection);
            AddDefaultShaderVariants();
        }
        #endregion Shader Variant Collection
    }
}