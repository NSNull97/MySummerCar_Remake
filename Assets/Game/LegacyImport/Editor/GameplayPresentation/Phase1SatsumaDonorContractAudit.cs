using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Read-only diagnostic for the small donor hierarchies that own Satsuma
    /// assembly tools. It emits evidence to the Editor log and never creates
    /// donor-derived runtime content.
    /// </summary>
    public static class Phase1SatsumaDonorContractAudit
    {
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string SceneRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/" +
            "ExportedProject/Assets/_Scenes/GAME.unity";

        private static readonly string[] AuditedPaths =
        {
            "ITEMS/car jack(itemx)",
            "ITEMS/floor jack(itemx)",
            "ITEMS/spanner set(itemx)",
            "SATSUMA(557kg, 248)",
        };

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Audit donor physical contracts")]
        public static void Run()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            DonorPathConfiguration paths = DonorPathConfiguration.LoadFromFile(
                Path.Combine(projectRoot, ConfigurationPath));
            string scenePath = Path.Combine(
                paths.DonorStagingDirectory,
                SceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
            DonorUnitySceneModel scene = DonorUnitySceneModel.Parse(scenePath);

            foreach (string path in AuditedPaths)
            {
                AuditHierarchy(scene, path);
            }

            Debug.Log("PHASE1_SATSUMA_DONOR_CONTRACT_AUDIT_OK");
        }

        private static void AuditHierarchy(
            DonorUnitySceneModel scene,
            string hierarchyPath)
        {
            DonorTransformRecord root = scene.GetUniqueTransformByPath(
                hierarchyPath.Split('/'));
            DonorTransformRecord[] descendants = scene
                .GetDescendants(root.TransformId, includeRoot: true)
                .ToArray();
            Debug.Log(
                $"DONOR_CONTRACT_ROOT path={hierarchyPath} " +
                $"transform={root.TransformId} descendants={descendants.Length}");

            foreach (DonorTransformRecord transform in descendants)
            {
                string path = scene.GetHierarchyPath(transform.TransformId);
                bool wroteTransform = false;
                if (scene.TryGetRigidbody(
                        transform.TransformId,
                        out DonorRigidbodyRecord body))
                {
                    Debug.Log(
                        $"DONOR_RIGIDBODY path={path} component={body.ComponentId} " +
                        $"mass={Format(body.MassKilograms)} " +
                        $"drag={Format(body.LinearDamping)} " +
                        $"angularDrag={Format(body.AngularDamping)} " +
                        $"gravity={body.UseGravity} kinematic={body.IsKinematic}");
                    wroteTransform = true;
                }

                DonorColliderRecord[] colliders = scene
                    .GetCollidersForGameObject(transform.GameObjectId)
                    .ToArray();
                foreach (DonorColliderRecord collider in colliders)
                {
                    Debug.Log(
                        $"DONOR_COLLIDER path={path} component={collider.ComponentId} " +
                        $"kind={collider.Kind} enabled={collider.Enabled} " +
                        $"trigger={collider.IsTrigger} convex={collider.Convex} " +
                        $"meshGuid={collider.MeshGuid} " +
                        $"center={Format(collider.Center)} size={Format(collider.Size)} " +
                        $"radius={Format(collider.Radius)} " +
                        $"height={Format(collider.Height)}");
                    wroteTransform = true;
                }

                DonorMonoBehaviourRecord[] behaviours = scene
                    .GetMonoBehaviours(transform.GameObjectId)
                    .ToArray();
                foreach (DonorMonoBehaviourRecord behaviour in behaviours)
                {
                    string fsmName = Capture(
                        behaviour.SerializedBody,
                        @"(?ms)^  fsm:\s*\r?\n.*?^    name:\s*(?<value>[^\r\n]*)");
                    if (string.IsNullOrEmpty(fsmName))
                    {
                        continue;
                    }

                    string states = string.Join(
                        ";",
                        Regex.Matches(
                                behaviour.SerializedBody,
                                @"(?m)^    - name:\s*(?<value>[^\r\n]*)")
                            .Cast<Match>()
                            .Select(match => match.Groups["value"].Value.Trim())
                            .Where(value => !string.IsNullOrEmpty(value))
                            .Distinct(StringComparer.Ordinal));
                    string actions = string.Join(
                        ";",
                        Regex.Matches(
                                behaviour.SerializedBody,
                                @"(?m)^\s*-\s+(?<value>[^\r\n]*Actions\.[^\r\n]+)$")
                            .Cast<Match>()
                            .Select(match => match.Groups["value"].Value.Trim())
                            .Distinct(StringComparer.Ordinal));
                    string variables = ExtractVariables(
                        behaviour.SerializedBody);
                    Debug.Log(
                        $"DONOR_FSM path={path} component={behaviour.ComponentId} " +
                        $"name={fsmName} states={states} actions={actions} " +
                        $"variables={variables}");
                    wroteTransform = true;
                }

                if (wroteTransform)
                {
                    Debug.Log(
                        $"DONOR_TRANSFORM path={path} transform={transform.TransformId} " +
                        $"localPosition={Format(transform.LocalPosition)} " +
                        $"localRotation={Format(transform.LocalRotation)} " +
                        $"localScale={Format(transform.LocalScale)}");
                }
            }
        }

        private static string ExtractVariables(string body)
        {
            return string.Join(
                ";",
                Regex.Matches(
                        body ?? string.Empty,
                        @"(?ms)^      - useVariable:.*?^        name:\s*(?<name>[^\r\n]*)" +
                        @"(?:.*?^        value:\s*(?<value>[^\r\n]*))?")
                    .Cast<Match>()
                    .Select(match =>
                        match.Groups["name"].Value.Trim() + "=" +
                        match.Groups["value"].Value.Trim())
                    .Where(value => !value.StartsWith("=", StringComparison.Ordinal))
                    .Distinct(StringComparer.Ordinal));
        }

        private static string Capture(string value, string pattern) =>
            Regex.Match(value ?? string.Empty, pattern)
                .Groups["value"].Value.Trim();

        private static string Format(float value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        private static string Format(Vector3 value) =>
            $"({Format(value.x)},{Format(value.y)},{Format(value.z)})";

        private static string Format(Quaternion value) =>
            $"({Format(value.x)},{Format(value.y)},{Format(value.z)},{Format(value.w)})";
    }
}
