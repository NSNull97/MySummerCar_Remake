using System;
using System.Collections;
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
    /// Temporary read-only runtime probe for the original floor jack and its
    /// relationship to Satsuma. The payload only writes a text dump into the
    /// host temp directory; it never writes into the donor installation.
    /// </summary>
    public static class RuntimeFloorJackProbeV2
    {
        private const string HarmonyId =
            "msc.remake.donor.runtime-floor-jack-probe.v2";
        private const string OutputFileName =
            "msc-runtime-floor-jack-dump-v2.txt";

        private static int dumpStarted;
        private static readonly List<MethodBase> PatchedMethods =
            new List<MethodBase>();

        public static void Install()
        {
            try
            {
                HarmonyInstance harmony = HarmonyInstance.Create(HarmonyId);
                MethodInfo postfix = typeof(RuntimeFloorJackProbeV2).GetMethod(
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

            var selected = new Dictionary<int, Transform>();
            for (int index = 0; index < transforms.Count; index++)
            {
                Transform candidate = transforms[index];
                string path = BuildPath(candidate);
                if (!IsFloorJackEvidence(path) &&
                    !IsSatsumaJackEvidence(path))
                {
                    continue;
                }

                AddAncestors(candidate, selected);
                if (IsFloorJackRoot(candidate))
                {
                    AddHierarchy(candidate, selected);
                }
                else
                {
                    AddHierarchy(candidate, selected);
                }
            }

            var ordered = new List<Transform>(selected.Values);
            ordered.Sort(delegate(Transform left, Transform right)
            {
                return string.CompareOrdinal(BuildPath(left), BuildPath(right));
            });

            var output = new StringBuilder(1024 * 1024);
            output.AppendLine("MSC_DONOR_RUNTIME_FLOOR_JACK_DUMP_V2");
            output.Append("utc\t").AppendLine(
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            output.Append("unityVersion\t").AppendLine(
                Escape(Application.unityVersion));
            output.Append("scene\t").AppendLine(Escape(GetSceneName()));
            output.Append("allTransforms\t")
                .Append(transforms.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
            output.Append("selectedTransforms\t")
                .Append(ordered.Count.ToString(CultureInfo.InvariantCulture))
                .AppendLine();

            for (int index = 0; index < ordered.Count; index++)
            {
                AppendTransform(output, ordered[index]);
            }

            output.AppendLine("END_MSC_DONOR_RUNTIME_FLOOR_JACK_DUMP_V2");
            File.WriteAllText(OutputPath, output.ToString(), Encoding.UTF8);
        }

        private static void AppendTransform(
            StringBuilder output,
            Transform transform)
        {
            int transformId = transform.GetInstanceID();
            Transform parent = transform.parent;
            string path = BuildPath(transform);

            output.Append("transform\t")
                .Append(transformId.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(transform.gameObject.activeSelf ? "1" : "0")
                .Append('\t').Append(
                    transform.gameObject.activeInHierarchy ? "1" : "0")
                .Append('\t').Append(Escape(path))
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
                .Append('\t').Append(Escape(ComponentList(transform)))
                .AppendLine();

            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer != null)
            {
                MeshFilter filter = transform.GetComponent<MeshFilter>();
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                Mesh mesh = skinned != null
                    ? skinned.sharedMesh
                    : filter != null
                        ? filter.sharedMesh
                        : null;
                output.Append("renderer\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Escape(path))
                    .Append('\t').Append(Vector(renderer.bounds.center))
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
            }

            Rigidbody body = transform.GetComponent<Rigidbody>();
            if (body != null)
            {
                output.Append("rigidbody\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Escape(path))
                    .Append('\t').Append(Vector(body.position))
                    .Append('\t').Append(Rotation(body.rotation))
                    .Append('\t').Append(body.isKinematic ? "1" : "0")
                    .Append('\t').Append(body.useGravity ? "1" : "0")
                    .Append('\t').Append(Format(body.mass))
                    .Append('\t').Append(Vector(body.centerOfMass))
                    .Append('\t').Append(Vector(body.velocity))
                    .Append('\t').Append(Vector(body.angularVelocity))
                    .Append('\t').Append(Escape(body.constraints.ToString()))
                    .Append('\t').Append(Escape(body.interpolation.ToString()))
                    .Append('\t').Append(Escape(
                        body.collisionDetectionMode.ToString()))
                    .AppendLine();
            }

            Collider[] colliders = transform.GetComponents<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                AppendCollider(output, transformId, path, colliders[index]);
            }

            Joint[] joints = transform.GetComponents<Joint>();
            for (int index = 0; index < joints.Length; index++)
            {
                AppendJoint(output, transformId, path, joints[index]);
            }

            Component[] components = transform.GetComponents<Component>();
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null ||
                    !string.Equals(
                        component.GetType().Name,
                        "PlayMakerFSM",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                AppendPlayMakerFsm(output, transformId, path, component);
            }
        }

        private static void AppendCollider(
            StringBuilder output,
            int transformId,
            string path,
            Collider collider)
        {
            output.Append("collider\t")
                .Append(transformId.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(Escape(path))
                .Append('\t').Append(Escape(collider.GetType().FullName))
                .Append('\t').Append(collider.enabled ? "1" : "0")
                .Append('\t').Append(collider.isTrigger ? "1" : "0")
                .Append('\t').Append(Vector(collider.bounds.center))
                .Append('\t').Append(Vector(collider.bounds.extents))
                .Append('\t').Append(Escape(
                    collider.sharedMaterial != null
                        ? collider.sharedMaterial.name
                        : string.Empty));

            BoxCollider box = collider as BoxCollider;
            CapsuleCollider capsule = collider as CapsuleCollider;
            SphereCollider sphere = collider as SphereCollider;
            MeshCollider mesh = collider as MeshCollider;
            if (box != null)
            {
                output.Append("\tbox\t")
                    .Append(Vector(box.center)).Append('\t')
                    .Append(Vector(box.size));
            }
            else if (capsule != null)
            {
                output.Append("\tcapsule\t")
                    .Append(Vector(capsule.center)).Append('\t')
                    .Append(Format(capsule.radius)).Append('\t')
                    .Append(Format(capsule.height)).Append('\t')
                    .Append(capsule.direction.ToString(CultureInfo.InvariantCulture));
            }
            else if (sphere != null)
            {
                output.Append("\tsphere\t")
                    .Append(Vector(sphere.center)).Append('\t')
                    .Append(Format(sphere.radius));
            }
            else if (mesh != null)
            {
                output.Append("\tmesh\t")
                    .Append(mesh.convex ? "1" : "0").Append('\t')
                    .Append(Escape(
                        mesh.sharedMesh != null
                            ? mesh.sharedMesh.name
                            : string.Empty));
            }

            output.AppendLine();
        }

        private static void AppendJoint(
            StringBuilder output,
            int transformId,
            string path,
            Joint joint)
        {
            output.Append("joint\t")
                .Append(transformId.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(Escape(path))
                .Append('\t').Append(Escape(joint.GetType().FullName))
                .Append('\t').Append(Vector(joint.anchor))
                .Append('\t').Append(joint.autoConfigureConnectedAnchor ? "1" : "0")
                .Append('\t').Append(Vector(joint.connectedAnchor))
                .Append('\t').Append(joint.connectedBody != null
                    ? Escape(BuildPath(joint.connectedBody.transform))
                    : string.Empty)
                .Append('\t').Append(joint.enableCollision ? "1" : "0")
                .Append('\t').Append(joint.enablePreprocessing ? "1" : "0")
                .Append('\t').Append(Format(joint.breakForce))
                .Append('\t').Append(Format(joint.breakTorque));

            HingeJoint hinge = joint as HingeJoint;
            if (hinge != null)
            {
                JointLimits limits = hinge.limits;
                JointSpring spring = hinge.spring;
                JointMotor motor = hinge.motor;
                output.Append("\thinge\t")
                    .Append(Vector(hinge.axis)).Append('\t')
                    .Append(hinge.useLimits ? "1" : "0").Append('\t')
                    .Append(Format(limits.min)).Append('\t')
                    .Append(Format(limits.max)).Append('\t')
                    .Append(hinge.useSpring ? "1" : "0").Append('\t')
                    .Append(Format(spring.spring)).Append('\t')
                    .Append(Format(spring.damper)).Append('\t')
                    .Append(Format(spring.targetPosition)).Append('\t')
                    .Append(hinge.useMotor ? "1" : "0").Append('\t')
                    .Append(Format(motor.force)).Append('\t')
                    .Append(Format(motor.targetVelocity));
            }

            output.AppendLine();
        }

        private static void AppendPlayMakerFsm(
            StringBuilder output,
            int transformId,
            string path,
            Component component)
        {
            object fsmName = GetMemberValue(component, "FsmName");
            object activeStateName = GetMemberValue(component, "ActiveStateName");
            output.Append("playmaker\t")
                .Append(transformId.ToString(CultureInfo.InvariantCulture))
                .Append('\t').Append(Escape(path))
                .Append('\t').Append(Escape(FormatValue(fsmName)))
                .Append('\t').Append(Escape(FormatValue(activeStateName)))
                .AppendLine();

            object variables = GetMemberValue(component, "FsmVariables");
            if (variables != null)
            {
                string[] groups =
                {
                    "FloatVariables",
                    "IntVariables",
                    "BoolVariables",
                    "StringVariables",
                    "Vector3Variables",
                    "QuaternionVariables",
                    "GameObjectVariables",
                    "ObjectVariables",
                };
                for (int index = 0; index < groups.Length; index++)
                {
                    AppendVariableGroup(
                        output,
                        transformId,
                        path,
                        fsmName,
                        groups[index],
                        GetMemberValue(variables, groups[index]));
                }
            }

            object states = GetMemberValue(component, "FsmStates");
            IEnumerable stateEnumerable = states as IEnumerable;
            if (stateEnumerable == null)
            {
                return;
            }

            foreach (object state in stateEnumerable)
            {
                if (state == null)
                {
                    continue;
                }

                string stateName = FormatValue(GetMemberValue(state, "Name"));
                output.Append("fsm-state\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Escape(path))
                    .Append('\t').Append(Escape(FormatValue(fsmName)))
                    .Append('\t').Append(Escape(stateName))
                    .AppendLine();

                IEnumerable actions =
                    GetMemberValue(state, "Actions") as IEnumerable;
                if (actions == null)
                {
                    continue;
                }

                int actionIndex = 0;
                foreach (object action in actions)
                {
                    if (action == null)
                    {
                        actionIndex++;
                        continue;
                    }

                    output.Append("fsm-action\t")
                        .Append(transformId.ToString(CultureInfo.InvariantCulture))
                        .Append('\t').Append(Escape(path))
                        .Append('\t').Append(Escape(FormatValue(fsmName)))
                        .Append('\t').Append(Escape(stateName))
                        .Append('\t').Append(
                            actionIndex.ToString(CultureInfo.InvariantCulture))
                        .Append('\t').Append(Escape(action.GetType().FullName))
                        .Append('\t').Append(Escape(SimpleFieldList(action)))
                        .AppendLine();
                    actionIndex++;
                }
            }
        }

        private static void AppendVariableGroup(
            StringBuilder output,
            int transformId,
            string path,
            object fsmName,
            string groupName,
            object group)
        {
            IEnumerable values = group as IEnumerable;
            if (values == null)
            {
                return;
            }

            foreach (object variable in values)
            {
                if (variable == null)
                {
                    continue;
                }

                output.Append("fsm-variable\t")
                    .Append(transformId.ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(Escape(path))
                    .Append('\t').Append(Escape(FormatValue(fsmName)))
                    .Append('\t').Append(Escape(groupName))
                    .Append('\t').Append(Escape(FormatValue(
                        GetMemberValue(variable, "Name"))))
                    .Append('\t').Append(Escape(FormatValue(
                        GetMemberValue(variable, "Value"))))
                    .AppendLine();
            }
        }

        private static object GetMemberValue(object instance, string name)
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                try
                {
                    return property.GetValue(instance, null);
                }
                catch
                {
                }
            }

            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            if (field != null)
            {
                try
                {
                    return field.GetValue(instance);
                }
                catch
                {
                }
            }

            return null;
        }

        private static string SimpleFieldList(object instance)
        {
            FieldInfo[] fields = instance.GetType().GetFields(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            var values = new List<string>();
            for (int index = 0; index < fields.Length; index++)
            {
                FieldInfo field = fields[index];
                if (field.IsStatic)
                {
                    continue;
                }

                object value;
                try
                {
                    value = field.GetValue(instance);
                }
                catch
                {
                    continue;
                }

                string formatted = FormatValue(value);
                if (!string.IsNullOrEmpty(formatted))
                {
                    values.Add(field.Name + "=" + formatted);
                }
            }

            return string.Join(";", values.ToArray());
        }

        private static string FormatValue(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            string text = value as string;
            if (text != null)
            {
                return text;
            }

            if (value is Vector3)
            {
                return Vector((Vector3)value);
            }

            if (value is Quaternion)
            {
                return Rotation((Quaternion)value);
            }

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            if (unityObject != null)
            {
                Component component = unityObject as Component;
                GameObject gameObject = unityObject as GameObject;
                if (component != null)
                {
                    return BuildPath(component.transform);
                }

                if (gameObject != null)
                {
                    return BuildPath(gameObject.transform);
                }

                return unityObject.name;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                IFormattable formattable = value as IFormattable;
                return formattable != null
                    ? formattable.ToString(null, CultureInfo.InvariantCulture)
                    : value.ToString();
            }

            object name = GetMemberValue(value, "Name");
            object nestedValue = GetMemberValue(value, "Value");
            if (name != null || nestedValue != null)
            {
                return FormatValue(name) + ":" + FormatValue(nestedValue);
            }

            return string.Empty;
        }

        private static bool IsFloorJackEvidence(string path)
        {
            return path.IndexOf(
                "floor jack",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(
                    "floor_jack",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSatsumaJackEvidence(string path)
        {
            if (path.IndexOf(
                    "SATSUMA",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            string lower = path.ToLowerInvariant();
            return lower.IndexOf("jack lift", StringComparison.Ordinal) >= 0 ||
                   lower.EndsWith("satsuma(557kg, 248)", StringComparison.Ordinal) ||
                   lower.EndsWith("/satsuma", StringComparison.Ordinal);
        }

        private static bool IsFloorJackRoot(Transform transform)
        {
            return transform != null &&
                   transform.name.IndexOf(
                       "floor jack(itemx)",
                       StringComparison.OrdinalIgnoreCase) >= 0;
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
                    "MSC_DONOR_RUNTIME_FLOOR_JACK_DUMP_ERROR_V2\r\n" + message,
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
