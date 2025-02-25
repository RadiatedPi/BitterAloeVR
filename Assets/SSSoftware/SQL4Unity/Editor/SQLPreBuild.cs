/*
 * This File and its contents are Copyright SteveSmith.Software 2020. All rights reserverd
 * 
 * No part may be copied, modified or stored in a public repository
 * 
 * This script adds the Resources directory structure to all databases so they can be used in a runtime build
 * 
 */
using UnityEditor;
using UnityEditor.Build;
#if (UNITY_2018_1_OR_NEWER)
using UnityEditor.Build.Reporting;
#endif
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace SQL4Unity
{
#if (UNITY_2017)
    class SQLPreBuild : IPreprocessBuild
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildTarget target, string path)
        {
            Debug.Log("SQL4Unity PreprocessBuild for target " + target + " at path " + path);
#elif (UNITY_2018_1_OR_NEWER)
    class SQLPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("SQL4Unity.OnPreprocessBuild for target " + report.summary.platform + " at path " + report.summary.outputPath);
#endif
            // Execute Pre Build Database updates
            SQLEditorUtility.PreBuild();

			string target = report.summary.platform.ToString();
			if (target.ToLower().StartsWith("standalone"))
			{
				string[] files = Directory.GetFiles(Application.persistentDataPath, "*.s4u");
				if (files.Length > 0)
				{
					Debug.Log("SQL4Unity Clean up PersistentData");

					foreach (string file in files)
					{
						File.Delete(file);
						Debug.Log("SQL4Unity Old Database removed " + file);
					}
				}
			}

            Debug.Log("SQL4Unity PreprocessBuild complete");
        }
    }
}