using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>
    /// Produces a read-only inventory of the donor Satsuma wiring hierarchy.
    /// The report contains transforms and inert PlayMaker references only;
    /// donor behaviours are never instantiated in the remake project.
    /// </summary>
    public static class Phase1SatsumaElectricalDonorAudit
    {
        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string SceneRelativePath =
            "raw/world/milestone-04a1/assetripper-unity-project/" +
            "ExportedProject/Assets/_Scenes/GAME.unity";
        private const string HierarchyAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaElectricalHierarchyAudit.csv";
        private const string FsmAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaElectricalFsmAudit.csv";
        private const string StateAuditPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1SatsumaElectricalStateAudit.csv";

        [MenuItem("Tools/MSC Remake/Phase 1/Satsuma/Audit donor electrics")]
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
            DonorTransformRecord wiringRoot = scene.GetUniqueTransformByPath(
                "SATSUMA(557kg, 248)", "Wiring");
            DonorTransformRecord itemRoot = scene.GetUniqueTransformByPath(
                "ITEMS", "wiring mess(itemx)");

            DonorTransformRecord[] wiringTransforms = scene
                .GetDescendants(wiringRoot.TransformId, includeRoot: true)
                .ToArray();
            DonorTransformRecord[] itemTransforms = scene
                .GetDescendants(itemRoot.TransformId, includeRoot: true)
                .ToArray();
            WriteHierarchyAudit(scene, wiringRoot, wiringTransforms);
            WriteFsmAudit(
                scene,
                wiringTransforms.Concat(itemTransforms).ToArray());
            WriteStateAudit(
                scene,
                wiringTransforms.Concat(itemTransforms).ToArray());
            AssetDatabase.ImportAsset(
                HierarchyAuditPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                FsmAuditPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(
                StateAuditPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "PHASE1_SATSUMA_ELECTRICAL_DONOR_AUDIT_OK " +
                $"wiringTransforms={wiringTransforms.Length} " +
                $"itemTransforms={itemTransforms.Length}");
        }

        private static void WriteHierarchyAudit(
            DonorUnitySceneModel scene,
            DonorTransformRecord wiringRoot,
            IReadOnlyList<DonorTransformRecord> transforms)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "TransformId,GameObjectId,Name,HierarchyPath,FatherTransformId," +
                "ActiveSelf,LocalPosition,LocalRotation,WorldPosition," +
                "MeshGuids,ColliderIds,FsmComponentIds");
            foreach (DonorTransformRecord transform in transforms)
            {
                string[] meshGuids = scene
                    .GetStaticRenderersBelowIncludingInactive(transform.TransformId)
                    .Where(value => value.GameObjectId == transform.GameObjectId)
                    .Select(value => value.MeshGuid)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                string[] colliderIds = scene
                    .GetCollidersForGameObject(transform.GameObjectId)
                    .Select(value => value.ComponentId.ToString(
                        CultureInfo.InvariantCulture))
                    .ToArray();
                string[] fsmIds = scene
                    .GetMonoBehaviours(transform.GameObjectId)
                    .Where(value => HasFsm(value.SerializedBody))
                    .Select(value => value.ComponentId.ToString(
                        CultureInfo.InvariantCulture))
                    .ToArray();

                builder.Append(transform.TransformId).Append(',')
                    .Append(transform.GameObjectId).Append(',')
                    .Append(Csv(scene.GetGameObjectName(transform.GameObjectId)))
                    .Append(',')
                    .Append(Csv(scene.GetHierarchyPath(transform.TransformId)))
                    .Append(',')
                    .Append(transform.FatherTransformId).Append(',')
                    .Append(scene.IsGameObjectActiveSelf(transform.TransformId)
                        ? "true"
                        : "false")
                    .Append(',')
                    .Append(Csv(Format(transform.LocalPosition))).Append(',')
                    .Append(Csv(Format(transform.LocalRotation))).Append(',')
                    .Append(Csv(Format(scene.GetWorldPosition(
                        transform.TransformId) -
                        scene.GetWorldPosition(wiringRoot.TransformId))))
                    .Append(',')
                    .Append(Csv(string.Join(";", meshGuids))).Append(',')
                    .Append(Csv(string.Join(";", colliderIds))).Append(',')
                    .Append(Csv(string.Join(";", fsmIds)))
                    .AppendLine();
            }

            File.WriteAllText(
                ToFileSystemPath(HierarchyAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static void WriteFsmAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorTransformRecord> transforms)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "ComponentId,GameObjectId,TransformId,HierarchyPath,FsmName," +
                "StartState,States,Actions,GameObjectReferences,NamedValues");
            foreach (DonorTransformRecord transform in transforms)
            {
                foreach (DonorMonoBehaviourRecord behaviour in scene
                             .GetMonoBehaviours(transform.GameObjectId)
                             .Where(value => HasFsm(value.SerializedBody)))
                {
                    string body = behaviour.SerializedBody;
                    builder.Append(behaviour.ComponentId).Append(',')
                        .Append(behaviour.GameObjectId).Append(',')
                        .Append(transform.TransformId).Append(',')
                        .Append(Csv(scene.GetHierarchyPath(transform.TransformId)))
                        .Append(',')
                        .Append(Csv(Capture(
                            body,
                            @"(?ms)^  fsm:\s*\r?\n.*?^    name:\s*(?<value>[^\r\n]*)")))
                        .Append(',')
                        .Append(Csv(Capture(
                            body,
                            @"(?m)^    startState:\s*(?<value>[^\r\n]*)")))
                        .Append(',')
                        .Append(Csv(ExtractStates(body))).Append(',')
                        .Append(Csv(ExtractActions(body))).Append(',')
                        .Append(Csv(ExtractGameObjectReferences(scene, body)))
                        .Append(',')
                        .Append(Csv(ExtractNamedValues(body)))
                        .AppendLine();
                }
            }

            File.WriteAllText(
                ToFileSystemPath(FsmAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static void WriteStateAudit(
            DonorUnitySceneModel scene,
            IReadOnlyList<DonorTransformRecord> transforms)
        {
            var builder = new StringBuilder();
            builder.AppendLine(
                "ComponentId,HierarchyPath,FsmName,StateName,Transitions," +
                "ActionSequence,GameObjectReferences,NamedValues," +
                "StringParameters,ParameterNames,ByteDataAscii");
            foreach (DonorTransformRecord transform in transforms)
            {
                foreach (DonorMonoBehaviourRecord behaviour in scene
                             .GetMonoBehaviours(transform.GameObjectId)
                             .Where(value => HasFsm(value.SerializedBody)))
                {
                    string fsmName = Capture(
                        behaviour.SerializedBody,
                        @"(?ms)^  fsm:\s*\r?\n.*?^    name:\s*(?<value>[^\r\n]*)");
                    MatchCollection states = Regex.Matches(
                        behaviour.SerializedBody,
                        @"(?ms)^    - name:\s*(?<name>[^\r\n]*).*?" +
                        @"(?=^    - name:|^    events:)");
                    foreach (Match state in states)
                    {
                        string body = state.Value;
                        string transitions = string.Join(
                            ";",
                            Regex.Matches(
                                    body,
                                    @"(?ms)^      - fsmEvent:\s*\r?\n" +
                                    @"\s*name:\s*(?<event>[^\r\n]*).*?" +
                                    @"^        toState:\s*(?<state>[^\r\n]*)")
                                .Cast<Match>()
                                .Select(value =>
                                    value.Groups["event"].Value.Trim() + "->" +
                                    value.Groups["state"].Value.Trim()));
                        string actions = string.Join(
                            ";",
                            Regex.Matches(
                                    body,
                                    @"(?m)^        - HutongGames\.PlayMaker\.Actions\." +
                                    @"(?<value>[^\r\n]*)")
                                .Cast<Match>()
                                .Select(value =>
                                    value.Groups["value"].Value.Trim()));
                        builder.Append(behaviour.ComponentId).Append(',')
                            .Append(Csv(scene.GetHierarchyPath(
                                transform.TransformId)))
                            .Append(',').Append(Csv(fsmName))
                            .Append(',').Append(Csv(
                                state.Groups["name"].Value.Trim()))
                            .Append(',').Append(Csv(transitions))
                            .Append(',').Append(Csv(actions))
                            .Append(',').Append(Csv(
                                ExtractGameObjectReferences(scene, body)))
                            .Append(',').Append(Csv(ExtractNamedValues(body)))
                            .Append(',').Append(Csv(ExtractStringParameters(body)))
                            .Append(',').Append(Csv(ExtractParameterNames(body)))
                            .Append(',').Append(Csv(ExtractByteDataAscii(body)))
                            .AppendLine();
                    }
                }
            }

            File.WriteAllText(
                ToFileSystemPath(StateAuditPath),
                builder.ToString(),
                new UTF8Encoding(false));
        }

        private static string ExtractStates(string body) => string.Join(
            ";",
            Regex.Matches(body, @"(?m)^    - name:\s*(?<value>[^\r\n]*)")
                .Cast<Match>()
                .Select(value => value.Groups["value"].Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal));

        private static string ExtractActions(string body) => string.Join(
            ";",
            Regex.Matches(
                    body,
                    @"(?m)^\s*-\s+HutongGames\.PlayMaker\.Actions\.(?<value>[^\r\n]*)")
                .Cast<Match>()
                .Select(value => value.Groups["value"].Value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal));

        private static string ExtractGameObjectReferences(
            DonorUnitySceneModel scene,
            string body)
        {
            return string.Join(
                ";",
                ExtractNamedValuesWithFileIds(body)
                    .Select(value =>
                    {
                        string path = value.GameObjectId != 0L &&
                                      scene.TryGetTransformIdForGameObject(
                                          value.GameObjectId,
                                          out long transformId)
                            ? scene.GetHierarchyPath(transformId)
                            : string.Empty;
                        return value.Name + "=" + value.GameObjectId + "[" +
                               path + "]";
                    })
                    .Distinct(StringComparer.Ordinal));
        }

        private static IEnumerable<NamedGameObjectReference>
            ExtractNamedValuesWithFileIds(string body)
        {
            string[] lines = body.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int index = 0; index < lines.Length; index++)
            {
                string nameLine = lines[index].Trim();
                if (!nameLine.StartsWith("name:", StringComparison.Ordinal))
                {
                    continue;
                }

                string name = nameLine.Substring("name:".Length).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                int limit = Math.Min(lines.Length, index + 10);
                for (int scan = index + 1; scan < limit; scan++)
                {
                    string valueLine = lines[scan].Trim();
                    if (!valueLine.StartsWith("value:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Match match = Regex.Match(
                        valueLine,
                        @"\{fileID:\s*(?<id>-?\d+)\}",
                        RegexOptions.CultureInvariant);
                    if (match.Success)
                    {
                        yield return new NamedGameObjectReference(
                            name,
                            long.Parse(
                                match.Groups["id"].Value,
                                CultureInfo.InvariantCulture));
                    }

                    break;
                }
            }
        }

        private static string ExtractNamedValues(string body)
        {
            string[] lines = body.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            var values = new List<string>();
            for (int index = 0; index < lines.Length; index++)
            {
                string nameLine = lines[index].Trim();
                if (!nameLine.StartsWith("name:", StringComparison.Ordinal))
                {
                    continue;
                }

                string name = nameLine.Substring("name:".Length).Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                int limit = Math.Min(lines.Length, index + 10);
                for (int scan = index + 1; scan < limit; scan++)
                {
                    string valueLine = lines[scan].Trim();
                    if (!valueLine.StartsWith("value:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string value = valueLine.Substring("value:".Length).Trim();
                    if (!string.IsNullOrWhiteSpace(value) &&
                        !value.StartsWith("{fileID:", StringComparison.Ordinal))
                    {
                        values.Add(name + "=" + value);
                    }

                    break;
                }
            }

            return string.Join(
                ";",
                values.Distinct(StringComparer.Ordinal));
        }

        private static string ExtractByteDataAscii(string body)
        {
            Match match = Regex.Match(
                body ?? string.Empty,
                @"(?m)^        byteData:\s*(?<value>[0-9a-fA-F]*)\s*$");
            string hex = match.Groups["value"].Value;
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0)
            {
                return string.Empty;
            }

            var bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = byte.Parse(
                    hex.Substring(index * 2, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }

            char[] text = Encoding.ASCII.GetString(bytes)
                .Select(value => value >= 32 && value <= 126 ? value : '|')
                .ToArray();
            return Regex.Replace(new string(text), @"\|+", "|").Trim('|');
        }

        private static string ExtractStringParameters(string body)
        {
            Match section = Regex.Match(
                body ?? string.Empty,
                @"(?ms)^        fsmStringParams:\s*(?<value>.*?)" +
                @"(?=^        fsmObjectParams:)");
            if (!section.Success)
            {
                return string.Empty;
            }

            return string.Join(
                ";",
                Regex.Matches(
                        section.Groups["value"].Value,
                        @"(?m)^          value:\s*(?<value>[^\r\n]*)")
                    .Cast<Match>()
                    .Select(value => value.Groups["value"].Value.Trim()));
        }

        private static string ExtractParameterNames(string body)
        {
            Match section = Regex.Match(
                body ?? string.Empty,
                @"(?ms)^        paramName:\s*(?<value>.*?)" +
                @"(?=^        paramDataPos:)");
            if (!section.Success)
            {
                return string.Empty;
            }

            return string.Join(
                ";",
                Regex.Matches(
                        section.Groups["value"].Value,
                        @"(?m)^        -\s*(?<value>[^\r\n]*)")
                    .Cast<Match>()
                    .Select(value => value.Groups["value"].Value.Trim()));
        }

        private static bool HasFsm(string body) => Regex.IsMatch(
            body ?? string.Empty,
            @"(?m)^  fsm:\s*$",
            RegexOptions.CultureInvariant);

        private static string Capture(string value, string pattern) =>
            Regex.Match(value ?? string.Empty, pattern)
                .Groups["value"].Value.Trim();

        private static string ToFileSystemPath(string assetPath) =>
            Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                assetPath.Replace('/', Path.DirectorySeparatorChar)));

        private static string Format(Vector3 value) => string.Format(
            CultureInfo.InvariantCulture,
            "{0:R};{1:R};{2:R}",
            value.x,
            value.y,
            value.z);

        private static string Format(Quaternion value) => string.Format(
            CultureInfo.InvariantCulture,
            "{0:R};{1:R};{2:R};{3:R}",
            value.x,
            value.y,
            value.z,
            value.w);

        private static string Csv(string value) =>
            "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private readonly struct NamedGameObjectReference
        {
            public NamedGameObjectReference(string name, long gameObjectId)
            {
                Name = name;
                GameObjectId = gameObjectId;
            }

            public string Name { get; }
            public long GameObjectId { get; }
        }
    }
}
