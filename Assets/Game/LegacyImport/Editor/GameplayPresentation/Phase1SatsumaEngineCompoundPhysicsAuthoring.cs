using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEngine;

namespace MSC.LegacyImport.Editor.GameplayPresentation
{
    /// <summary>Explicit physical bindings for the existing engine pickup scope, not all vehicle parts.</summary>
    public static class Phase1SatsumaEngineCompoundPhysicsAuthoring
    {
        // User-approved remake carrying constraint; this is not a donor mass gate.
        public const float EngineCarryLimitKilograms = 120f;
        private const string BlockId = "vehicle.satsuma.part.engine-block";

        public static int Configure(VehicleAssemblyController assembly)
        {
            if (Application.isPlaying || assembly == null)
                throw new InvalidDataException("Engine compound physics requires a non-playing authored assembly.");
            var targets = new List<PartInstance>();
            var expectedIds = new HashSet<string>(StringComparer.Ordinal) { BlockId };
            bool expanded;
            do
            {
                expanded = false;
                foreach (MountPointAuthoring mount in assembly.MountPoints)
                {
                    if (mount?.Definition == null || !expectedIds.Contains(mount.Definition.OwnerPartDefinitionId)) continue;
                    foreach (string accepted in mount.Definition.AcceptedPartDefinitionIds) expanded |= expectedIds.Add(accepted);
                }
            } while (expanded);
            foreach (PartInstance part in assembly.Parts)
            {
                if (part?.Definition == null || !expectedIds.Contains(part.Definition.DefinitionId)) continue;
                AssemblySubassemblyPickupTarget pickup = part.GetComponent<AssemblySubassemblyPickupTarget>();
                if (part.IsAssemblyRoot || part.Body == null || part.PickupTarget == null ||
                    pickup == null || pickup.Controller != assembly || pickup.SurfacePart != part)
                    throw new InvalidDataException("Missing explicit reviewed engine pickup binding: " + part.Definition.DefinitionId);
                targets.Add(part);
            }
            if (targets.Count == 0 || targets.Count(part => part.Definition.DefinitionId == BlockId) != 1 ||
                targets.Distinct().Count() != targets.Count)
                throw new InvalidDataException("Engine compound scope must contain one block and unique registered parts.");

            var bindings = new AssemblyCompoundShapeBinding[targets.Count];
            for (int index = 0; index < targets.Count; index++)
            {
                PartInstance part = targets[index];
                Collider[] shapes = part.GetComponentsInChildren<Collider>(true).Where(shape =>
                    shape.GetComponentInParent<PartInstance>() == part && !shape.isTrigger && shape.enabled &&
                    shape.GetComponent<AssemblyCompoundColliderProxy>() == null && IsActiveBelow(shape.transform, part.transform)).ToArray();
                foreach (Collider shape in shapes)
                {
                    if (shape.attachedRigidbody != part.Body ||
                        !(shape is BoxCollider || shape is SphereCollider || shape is CapsuleCollider ||
                          shape is MeshCollider mesh && mesh.convex && mesh.sharedMesh != null))
                        throw new InvalidDataException("Engine contact must be a supported owned convex shape: " + part.Definition.DefinitionId);
                }
                // A genuinely shape-less gasket may still contribute its authored
                // own mass. Do not replace it with a fabricated bounding box.
                bindings[index] = new AssemblyCompoundShapeBinding(part, shapes);
            }
            AssemblyLooseCompoundPhysics[] existing = assembly.GetComponents<AssemblyLooseCompoundPhysics>();
            if (existing.Length > 1) throw new InvalidDataException("Duplicate compound physics controllers.");
            AssemblyLooseCompoundPhysics physics = existing.FirstOrDefault();
            int changed = 0;
            if (physics == null || physics.Assembly != assembly || !SameBindings(physics.Bindings, bindings))
            {
                if (physics == null) physics = assembly.gameObject.AddComponent<AssemblyLooseCompoundPhysics>();
                physics.Configure(assembly, bindings);
                EditorUtility.SetDirty(physics);
                changed++;
            }
            foreach (PartInstance part in targets)
            {
                PhysicsPickupTarget pickup = part.PickupTarget;
                bool debugOverride = new SerializedObject(pickup)
                    .FindProperty("allowAssemblyMassDebugOverride").boolValue;
                if (Mathf.Approximately(pickup.MaximumCarryMassKilograms, EngineCarryLimitKilograms) && debugOverride) continue;
                pickup.ConfigureAssemblyCarryLimit(EngineCarryLimitKilograms);
                EditorUtility.SetDirty(pickup);
                changed++;
            }
            return changed;
        }

        private static bool SameBindings(AssemblyCompoundShapeBinding[] left, AssemblyCompoundShapeBinding[] right)
        {
            if (left == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] == null || left[index].Part != right[index].Part ||
                    left[index].OwnCenterOfMass != right[index].OwnCenterOfMass ||
                    !left[index].Shapes.SequenceEqual(right[index].Shapes)) return false;
            }
            return true;
        }

        private static bool IsActiveBelow(Transform shape, Transform root)
        {
            for (Transform current = shape; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return shape == root || shape.IsChildOf(root);
        }
    }
}
