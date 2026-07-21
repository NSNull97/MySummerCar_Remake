using System;
using UnityEditor;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    public static class UIReferenceBatchCommands
    {
        [MenuItem("Tools/My Summer Car/UI/Generate Milestone 08A Review Set")]
        public static void GenerateAllReviewSets()
        {
            string projectRoot = UIReferencePaths.GetProjectRoot();
            UIReferenceCaptureResult result =
                UIReferenceCaptureUtility.GenerateAllReviewSets(projectRoot);
            if (result.Errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", result.Errors));
            }

            AssetDatabase.Refresh();
            Debug.Log(
                $"M08A review capture set generated: {result.OutputPaths.Count} files under " +
                UIReferencePaths.GetReviewDirectory(projectRoot));
        }

        public static void GenerateAllReviewSetsBatch()
        {
            try
            {
                GenerateAllReviewSets();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(2);
                }

                throw;
            }
        }
    }
}
