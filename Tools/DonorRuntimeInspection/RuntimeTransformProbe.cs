using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using Harmony;
using UnityEngine;

namespace MSC.DonorRuntimeInspection
{
    /// <summary>
    /// Temporary read-only donor probe. Install() is invoked from an attached
    /// Mono thread, then Harmony moves the actual Unity object inspection onto
    /// MSCLoader's next main-thread Update.
    /// </summary>
    public static class RuntimeTransformProbe
    {
        private static readonly string HarmonyId =
            "msc.remake.donor.runtime-transform-probe.v3." +
            typeof(RuntimeTransformProbe).Assembly.GetName().Name;
        private const string OutputFileName =
            "msc-runtime-suspension-dump-v3.txt";

        private static int dumpStarted;
        private static readonly List<MethodBase> PatchedMethods =
            new List<MethodBase>();

        public static void Install()
        {
            try
            {
                HarmonyInstance harmony = HarmonyInstance.Create(HarmonyId);
                MethodInfo postfix = typeof(RuntimeTransformProbe).GetMethod(
                    "OnMainThreadUpdate",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (postfix == null)
                {
                    WriteFailure("Probe postfix method was not found.");
                    return;
                }

                string[] candidates =
                {
                    "MSCLoader.A_ModUpdate",
                    "MSCLoader.BC_ModUpdate",
                };

                for (int index = 0; index < candidates.Length; index++)
                {
                    Type type = FindLoadedType(candidates[index]);
                    if (type == null)
                    {
                        continue;
                    }

                    MethodInfo update = type.GetMethod(
                        "Update",
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);
                    if (update == null)
                    {
                        continue;
                    }

                    harmony.Patch(
                        update,
                        null,
                        new HarmonyMethod(postfix),
                        null);
                    PatchedMethods.Add(update);
                }

                if (PatchedMethods.Count == 0)
                {
                    WriteFailure(
                        "No live MSCLoader Update method could be patched.");
                }
            }
            catch (Exception exception)
            {
                WriteFailure("Install failed: " + exception);
            }
        }

        private static void OnMainThreadUpdate()
        {
            if (Interlocked.Exchange(ref dumpStarted, 1) != 0)
            {
                return;
            }

            try
            {
                WriteDump();
            }
            catch (Exception exception)
            {
                WriteFailure("Main-thread dump failed: " + exception);
            }
        }

        private static void WriteDump()
        {
            UnityEngine.Object[] rawTransforms =
                Resources.FindObjectsOfTypeAll(typeof(Transform));
            var transforms = new List<Transform>(rawTransforms.Length);
            for (int index = 0; index < rawTransforms.Length; index++)
            {
                Transform transform = rawTransforms[index] as Transform;
                if (transform != null)
                {
                    transforms.Add(transform);
                }
            }

            var satsumaRoots = new List<Transform>();
            for (int index = 0; index < transforms.Count; index++)
            {
                Transform candidate = transforms[index];
                if (candidate.name.IndexOf(
                        "SATSUMA",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    satsumaRoots.Add(candidate);
                }
            }

            var selected = new Dictionary<int, Transform>();
            for (int index = 0; index < satsumaRoots.Count; index++)
            {
                AddHierarchy(satsumaRoots[index], selected);
            }

            for (int index = 0; index < transforms.Count; index++)
            {
                Transform candidate = transforms[index];
                string path = BuildPath(candidate);
                if (IsSuspensionEvidence(path))
                {
                    AddAncestors(candidate, selected);
                    AddHierarchy(candidate, selected);
                }
            }

            var ordered = new List<Transform>(selected.Values);
            ordered.Sort(delegate(Transform left, Transform right)
            {
                return string.CompareOrdinal(BuildPath(left), BuildPath(right));
            });

            var output = new StringBuilder(1024 * 1024);
            output.AppendLine("MSC_DONOR_RUNTIME_TRANSFORM_DUMP_V3");
            output.Append("utc\t").AppendLine(
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            output.Append("unityVersion\t").AppendLine(
                Escape(Application.unityVersion));
            output.Append("scene\t").AppendLine(Escape(GetSceneName()));
            output.Append("allTransforms\t")
                .Append(transforms.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
            output.Append("satsumaRoots\t")
                .Append(satsumaRoots.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
            output.Append("selectedTransforms\t")
                .Append(ordered.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
            output.AppendLine(
                "kind\tid\tactiveSelf\tactiveInHierarchy\tpath\tparentId" +
                "\tlocalPosition\tlocalRotation\tlocalEuler\tlocalScale" +
                "\tworldPosition\tworldRotation\tworldEuler\tlossyScale" +
                "\tlocalToWorld\tcomponents");

            for (int index = 0; index < ordered.Count; index++)
            {
                AppendTransform(output, ordered[index]);
            }

            output.AppendLine("END_MSC_DONOR_RUNTIME_TRANSFORM_DUMP_V3");
            File.WriteAllText(OutputPath, output.ToString(), Encoding.UTF8);
        }

        private static void AppendTransform(
            StringBuilder output,
            Transform transform)
        {
            int transformId = transform.GetInstanceID();
            Transform parent = transform.parent;
            Transform satsumaRoot = FindSatsumaAncestor(transform);
            output.Append("transform\t")
                .Append(transformId.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(transform.gameObject.activeSelf ? "1" : "0")
                .Append('\t').Append(
                    transform.gameObject.activeInHierarchy ? "1" : "0")
                .Append('\t').Append(Escape(BuildPath(transform)))
                .Append('\t').Append(parent != null
                    ? parent.GetInstanceID().ToString(CultureInfo.InvariantCulture)
                    : string.Empty)
                .Append('\t').Append(Vector(transform.localPosition))
                .Append('\t').Append(Rotation(transform.localRotation))
                .Append('\t').Append(Vector(transform.localEulerAngles))
                .Append('\t').Append(Vector(transform.localScale))
                .Append('\t').Append(Vector(transform.position))
                .Append('\t').Append(Rotation(transform.rotation))
                .Append('\t').Append(Vector(transform.eulerAngles))
                .Append('\t').Append(Vector(transform.lossyScale))
                .Append('\t').Append(Matrix(transform.localToWorldMatrix))
                .Append('\t').Append(Escape(ComponentList(transform)))
                .AppendLine();

            if (satsumaRoot != null && satsumaRoot != transform)
            {
                Quaternion satsumaLocalRotation =
                    Quaternion.Inverse(satsumaRoot.rotation) *
                    transform.rotation;
                output.Append("satsuma-local\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append("\t\t\t")
                    .Append(Escape(BuildPath(transform)))
                    .Append("\t\t")
                    .Append(Vector(satsumaRoot.InverseTransformPoint(
                        transform.position)))
                    .Append('\t').Append(Rotation(satsumaLocalRotation))
                    .Append('\t').Append(Vector(
                        satsumaLocalRotation.eulerAngles))
                    .AppendLine();
            }

            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer != null)
            {
                MeshFilter filter = transform.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinned =
                    renderer as SkinnedMeshRenderer;
                Mesh mesh = skinned != null
                    ? skinned.sharedMesh
                    : filter != null
                        ? filter.sharedMesh
                        : null;
                output.Append("renderer\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append("\t\t\t")
                    .Append(Escape(BuildPath(transform)))
                    .Append("\t\t")
                    .Append(Vector(renderer.bounds.center))
                    .Append('\t').Append(Vector(renderer.bounds.extents))
                    .Append('\t').Append(renderer.enabled ? "1" : "0")
                    .Append('\t').Append(Escape(mesh != null ? mesh.name : ""))
                    .Append('\t').Append(mesh != null
                        ? Vector(mesh.bounds.center)
                        : string.Empty)
                    .Append('\t').Append(mesh != null
                        ? Vector(mesh.bounds.extents)
                        : string.Empty)
                    .AppendLine();

                if (skinned != null)
                {
                    output.Append("skinned\t")
                        .Append(transformId.ToString(CultureInfo.InvariantCulture))
                        .Append("\t\t\t")
                        .Append(Escape(BuildPath(transform)))
                        .Append("\t\t")
                        .Append(Escape(mesh != null ? mesh.name : string.Empty))
                        .Append('\t').Append(Vector(skinned.localBounds.center))
                        .Append('\t').Append(Vector(skinned.localBounds.extents))
                        .Append('\t').Append(skinned.rootBone != null
                            ? Escape(BuildPath(skinned.rootBone))
                            : string.Empty)
                        .Append('\t').Append(Escape(BoneList(skinned.bones)))
                        .AppendLine();
                }
            }

            Rigidbody body = transform.GetComponent<Rigidbody>();
            if (body != null)
            {
                output.Append("rigidbody\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append("\t\t\t")
                    .Append(Escape(BuildPath(transform)))
                    .Append("\t\t")
                    .Append(Vector(body.position))
                    .Append('\t').Append(Rotation(body.rotation))
                    .Append('\t').Append(body.isKinematic ? "1" : "0")
                    .Append('\t').Append(body.useGravity ? "1" : "0")
                    .Append('\t').Append(Format(body.mass))
                    .AppendLine();
            }
        }

        private static void AddHierarchy(
            Transform root,
            Dictionary<int, Transform> selected)
        {
            if (root == null)
            {
                return;
            }

            int id = root.GetInstanceID();
            if (!selected.ContainsKey(id))
            {
                selected.Add(id, root);
            }

            for (int index = 0; index < root.childCount; index++)
            {
                AddHierarchy(root.GetChild(index), selected);
            }
        }

        private static void AddAncestors(
            Transform transform,
            Dictionary<int, Transform> selected)
        {
            Transform current = transform;
            while (current != null)
            {
                int id = current.GetInstanceID();
                if (!selected.ContainsKey(id))
                {
                    selected.Add(id, current);
                }

                current = current.parent;
            }
        }

        private static bool IsSuspensionEvidence(string path)
        {
            string lower = path.ToLowerInvariant();
            return lower.IndexOf("trail arm", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("trailarm", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("drumbrake", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("drum brake", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("coil spring", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("shock absorber", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("wheelrl", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("wheelrr", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("tirerl", StringComparison.Ordinal) >= 0 ||
                   lower.IndexOf("tirerr", StringComparison.Ordinal) >= 0;
        }

        private static string BuildPath(Transform transform)
        {
            var names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static Transform FindSatsumaAncestor(Transform transform)
        {
            Transform current = transform;
            Transform result = null;
            while (current != null)
            {
                if (current.name.IndexOf(
                        "SATSUMA",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result = current;
                }

                current = current.parent;
            }

            return result;
        }

        private static string ComponentList(Transform transform)
        {
            Component[] components = transform.GetComponents<Component>();
            var names = new List<string>(components.Length);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                names.Add(component == null
                    ? "<missing>"
                    : component.GetType().FullName);
            }

            return string.Join(";", names.ToArray());
        }

        private static string BoneList(Transform[] bones)
        {
            if (bones == null || bones.Length == 0)
            {
                return string.Empty;
            }

            var paths = new List<string>(bones.Length);
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = bones[index];
                paths.Add(bone != null ? BuildPath(bone) : "<missing>");
            }

            return string.Join(";", paths.ToArray());
        }

        private static Type FindLoadedType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string GetSceneName()
        {
            try
            {
                return Application.loadedLevelName;
            }
            catch
            {
                return "<unavailable>";
            }
        }

        private static string Vector(Vector3 value)
        {
            return Format(value.x) + "," +
                   Format(value.y) + "," +
                   Format(value.z);
        }

        private static string Rotation(Quaternion value)
        {
            return Format(value.x) + "," +
                   Format(value.y) + "," +
                   Format(value.z) + "," +
                   Format(value.w);
        }

        private static string Matrix(Matrix4x4 value)
        {
            var text = new StringBuilder(256);
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    if (row != 0 || column != 0)
                    {
                        text.Append(',');
                    }

                    text.Append(Format(value[row, column]));
                }
            }

            return text.ToString();
        }

        private static string Format(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static void WriteFailure(string message)
        {
            try
            {
                File.WriteAllText(
                    OutputPath,
                    "MSC_DONOR_RUNTIME_TRANSFORM_DUMP_ERROR\r\n" + message,
                    Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string OutputPath
        {
            get { return Path.Combine(Path.GetTempPath(), OutputFileName); }
        }
    }
}
