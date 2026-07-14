using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.LegacyImport.Editor.Proof
{
    public static class DonorComparisonSceneBuilder
    {
        [MenuItem("Tools/My Summer Car/Legacy Import/Build Controlled Proof Comparison Scene")]
        public static void Build()
        {
            string[] proofGuids = AssetDatabase.FindAssets(
                "t:ReauthoredAssetProvenance",
                new[] { "Assets/Game/LegacyImport/Manifests" });
            if (proofGuids.Length == 0)
            {
                throw new InvalidOperationException(
                    "Create reauthored provenance assets before building the comparison scene.");
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AddNeutralLighting();

                int proofIndex = 0;
                foreach (string proofGuid in proofGuids)
                {
                    string proofPath = AssetDatabase.GUIDToAssetPath(proofGuid);
                    ReauthoredAssetProvenance proof =
                        AssetDatabase.LoadAssetAtPath<ReauthoredAssetProvenance>(proofPath);
                    if (proof == null || proof.Registry == null ||
                        !proof.Registry.TryGetRecord(proof.DonorRecordId, out DonorAssetRecord record))
                    {
                        continue;
                    }

                    GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(record.ReferenceAssetPath);
                    if (referencePrefab == null || proof.ProductionPrefab == null)
                    {
                        continue;
                    }

                    float rowZ = proofIndex * 5f;
                    InstantiateReference(referencePrefab, record, new Vector3(-2.5f, 0f, rowZ));
                    InstantiateProduction(proof.ProductionPrefab, proof.Role, new Vector3(2.5f, 0f, rowZ));
                    proofIndex++;
                }

                if (proofIndex == 0)
                {
                    throw new InvalidOperationException(
                        "No complete donor-reference/production proof pair could be instantiated.");
                }

                string sceneFile = Path.GetFullPath(ControlledProofValidator.ComparisonScenePath);
                Directory.CreateDirectory(Path.GetDirectoryName(sceneFile));
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ControlledProofValidator.ComparisonScenePath))
                {
                    throw new InvalidOperationException("Could not save controlled proof comparison scene.");
                }
            }
            finally
            {
                if (previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log(
                "Controlled proof comparison scene created at " +
                ControlledProofValidator.ComparisonScenePath);
        }

        private static void AddNeutralLighting()
        {
            var lightObject = new GameObject("Neutral Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 100000f;
            light.lightUnit = UnityEngine.Rendering.LightUnit.Lux;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var cameraObject = new GameObject("Comparison Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(0f, 3f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        private static void InstantiateReference(
            GameObject prefab,
            DonorAssetRecord record,
            Vector3 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "REFERENCE__" + record.RecordId;
            instance.transform.position = position;
            LegacyAssetReference marker = instance.AddComponent<LegacyAssetReference>();
            var serializedMarker = new SerializedObject(marker);
            serializedMarker.FindProperty("recordId").stringValue = record.RecordId;
            serializedMarker.FindProperty("donorObjectName").stringValue = record.SourceObjectName;
            serializedMarker.FindProperty("sourceSha256").stringValue = record.StagedFileSha256;
            serializedMarker.FindProperty("classification").enumValueIndex =
                (int)record.Classification;
            serializedMarker.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstantiateProduction(
            GameObject prefab,
            DonorProofRole role,
            Vector3 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "PRODUCTION__" + role;
            instance.transform.position = position;
        }
    }
}
