using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CompositeCurves.Editor
{
    internal static class CompositeCurveTestUtility
    {
        internal const string TempAssetFolder = "Assets/CompositeCurves/TestsTemp";
        internal const string GeneratedAssetPath = "Assets/CompositeCurves/Generated/CompositeCurveGenerated.Evaluator.g.cs";

        internal static string GeneratedAbsolutePath
        {
            get
            {
                return Path.Combine(Directory.GetCurrentDirectory(), GeneratedAssetPath);
            }
        }

        internal static void EnsureTempFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CompositeCurves"))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(TempAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/CompositeCurves", "TestsTemp");
            }
        }

        internal static string CreateUniqueAssetPath(string prefix)
        {
            EnsureTempFolder();
            return TempAssetFolder + "/" + prefix + "_" + Guid.NewGuid().ToString("N") + ".asset";
        }

        internal static CompositeCurveDefinition CreateCurveAsset(string prefix)
        {
            var curve = ScriptableObject.CreateInstance<CompositeCurveDefinition>();
            var path = CreateUniqueAssetPath(prefix);
            AssetDatabase.CreateAsset(curve, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return curve;
        }

        internal static string BackupGeneratedSource()
        {
            return File.Exists(GeneratedAbsolutePath)
                ? File.ReadAllText(GeneratedAbsolutePath)
                : string.Empty;
        }

        internal static void RestoreGeneratedSource(string content)
        {
            File.WriteAllText(GeneratedAbsolutePath, content);
            AssetDatabase.ImportAsset(GeneratedAssetPath, ImportAssetOptions.ForceUpdate);
        }

        internal static string ReadGeneratedSource()
        {
            return File.Exists(GeneratedAbsolutePath)
                ? File.ReadAllText(GeneratedAbsolutePath)
                : string.Empty;
        }

        internal static void CleanupTempAssets()
        {
            if (AssetDatabase.IsValidFolder(TempAssetFolder))
            {
                AssetDatabase.DeleteAsset(TempAssetFolder);
            }

            AssetDatabase.Refresh();
        }
    }
}
