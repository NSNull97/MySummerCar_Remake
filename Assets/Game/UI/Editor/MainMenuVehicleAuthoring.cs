using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.UI.Presentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.UI.EditorTools
{
    /// <summary>
    /// Reads the accepted, sanitized vehicle asset without instantiating its
    /// gameplay components. The output owns only static meshes and paint bindings.
    /// </summary>
    public static class MainMenuVehicleAuthoring
    {
        public const string SourcePrefabPath = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/Resources/Phase1Vehicles/Satsuma_Phase1_V1a.prefab";
        public const string OutputDirectory = "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MenuPreview";
        public const string PrefabPath = OutputDirectory + "/SatsumaMenuPreview.prefab";
        private const string ManifestPath = "Assets/Game/LegacyImport/Manifests/Phase1SatsumaV1aManifest.json";
        private const string ReportPath = OutputDirectory + "/SatsumaMenuPreviewReport.json";
        private const string BodyId = "vehicle.satsuma.part.body-shell";
        private const string Prefix = "vehicle.satsuma.part.";
        private const string MountPrefix = "mount.satsuma.";
        private const string HeadlightMeshGuid = "26a82542157805da63f1304bc24dcfde";
        private const string HeadlightMaterialGuid = "4a4307067e1bdcb45af1cf28c216162e";
        private const string RearLampMeshGuid = "c659145015f39214e2515b466a859693";
        private const string RearLampMaterialGuid = "203160a5a0ad7944eafc486732269e66";
        private const string RearLampSectionsPath = OutputDirectory + "/Meshes/RearLampSections.asset";

        [MenuItem("Tools/My Summer Car/UI/Main Menu/Rebuild Satsuma Mesh Preview")]
        public static void Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null) throw new InvalidOperationException("The accepted Satsuma baseline is missing: " + SourcePrefabPath);
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest?.looseParts == null || manifest.transferClassification != "TemporaryDirectImport")
                throw new InvalidDataException("The Satsuma source manifest is missing or has an unexpected classification.");
            VehicleAssemblyController assembly = source.GetComponent<VehicleAssemblyController>();
            VehiclePaintStateController paint = source.GetComponent<VehiclePaintStateController>();
            if (assembly == null || paint == null) throw new InvalidDataException("Satsuma assembly/paint authoring is missing.");
            var report = new Report
            {
                sourcePrefab = SourcePrefabPath,
                sourceSha256 = Sha256(SourcePrefabPath),
                sourceDependencyHash = AssetDatabase.GetAssetDependencyHash(SourcePrefabPath).ToString(),
                sourceManifestSha256 = Sha256(ManifestPath),
                sourceBuilderVersion = manifest.builderVersion,
                classification = "TemporaryDirectImport",
                pose = "Authored installed stock parts; front full droop; static rear authored arm pose; hood open -87 degrees. Mesh display only, no suspension simulation.",
            };
            Scene scene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            var temporaryMeshes = new List<Mesh>();
            try
            {
                root = new GameObject("Satsuma Menu Preview");
                SceneManager.MoveGameObjectToScene(root, scene);
                var context = new BuildContext(source, root, assembly, report, temporaryMeshes);
                context.SelectStockParts(manifest);
                context.ComposeInstalledParts();
                context.ConfigureFrontPose();
                context.OpenHood();
                context.CopyRenderers();
                context.ConfigureRearPresentation();
                context.BakeSkins();
                MainMenuVehiclePaintBinding[] bindings = context.CopyPaintBindings(paint);
                MainMenuVehicleLampBinding[] lamps = context.CopyLampBindings();
                PruneEmptyChildren(root.transform);
                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                MainMenuVehicleModel model = root.AddComponent<MainMenuVehicleModel>();
                model.ConfigureForAuthoring(renderers, bindings);
                model.ConfigureLampsForAuthoring(lamps);
                ValidateOutput(model);
                report.rendererCount = renderers.Length;
                report.paintSurfaceCount = bindings.Length;
                report.lampCount = lamps.Length;
                report.meshCount = renderers.Select(value => value.GetComponent<MeshFilter>().sharedMesh).Distinct().Count();
                report.boundsCenter = model.LocalBounds.center;
                report.boundsSize = model.LocalBounds.size;
                EnsureFolder(OutputDirectory);
                context.PersistBakedMeshes();
                context.PersistRearLampSections();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Could not save the menu vehicle prefab.");
                ValidateOutput(saved.GetComponent<MainMenuVehicleModel>());
                report.status = "Generated; requires visual verification in the menu capture fixture.";
                File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(ReportPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Menu Satsuma: {report.parts.Count} parts, {report.rendererCount} static renderers, {report.paintSurfaceCount} paint surfaces. {PrefabPath}");
            }
            catch (Exception exception)
            {
                report.status = "Failed";
                report.missing.Add(exception.Message);
                Directory.CreateDirectory("Artifacts/MainMenuRedesign");
                File.WriteAllText("Artifacts/MainMenuRedesign/SatsumaMenuPreviewFailure.json",
                    JsonUtility.ToJson(report, true), new UTF8Encoding(false));
                throw;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                foreach (Mesh mesh in temporaryMeshes)
                    if (mesh != null && !AssetDatabase.Contains(mesh)) Object.DestroyImmediate(mesh);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private sealed class BuildContext
        {
            private readonly GameObject source;
            private readonly GameObject root;
            private readonly VehicleAssemblyController assembly;
            private readonly Report report;
            private readonly List<Mesh> temporaryMeshes;
            private readonly Dictionary<string, PartInstance> parts = new Dictionary<string, PartInstance>(StringComparer.Ordinal);
            private readonly Dictionary<string, MountPointAuthoring> assignments = new Dictionary<string, MountPointAuthoring>(StringComparer.Ordinal);
            private readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();
            private readonly Dictionary<Renderer, MeshRenderer> renderers = new Dictionary<Renderer, MeshRenderer>();
            private readonly HashSet<string> installed = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> visiting = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<Renderer> replacedLooseRenderers = new HashSet<Renderer>();
            private readonly HashSet<SkinnedMeshRenderer> installedSkins = new HashSet<SkinnedMeshRenderer>();
            private readonly HashSet<Renderer> requiredInstalledRenderers = new HashSet<Renderer>();
            private readonly HashSet<Transform> installedVisible = new HashSet<Transform>();
            private readonly HashSet<Transform> installedHidden = new HashSet<Transform>();
            private readonly List<KeyValuePair<SkinnedMeshRenderer, SkinnedMeshRenderer>> skins = new List<KeyValuePair<SkinnedMeshRenderer, SkinnedMeshRenderer>>();
            private readonly List<KeyValuePair<MeshFilter, Mesh>> baked = new List<KeyValuePair<MeshFilter, Mesh>>();
            private readonly List<MeshFilter> rearLampFilters = new List<MeshFilter>();
            private Mesh rearLampSections;

            public BuildContext(GameObject source, GameObject root, VehicleAssemblyController assembly, Report report, List<Mesh> temporaryMeshes)
            {
                this.source = source; this.root = root; this.assembly = assembly;
                this.report = report; this.temporaryMeshes = temporaryMeshes;
                transforms.Add(source.transform, root.transform);
            }

            public void SelectStockParts(Manifest manifest)
            {
                var required = new HashSet<string>(manifest.looseParts
                    .Where(value => value.group == "PartsCar" || value.group == "PartsMotor")
                    .Select(value => value.partDefinitionId), StringComparer.Ordinal) { BodyId };
                report.sourceStockPartCount = required.Count;
                // Both panels exist in the new-game inventory, but the authored
                // assembly contract permits only one on the rear parcel shelf.
                const string rearPanel = Prefix + "back-panel";
                const string subwooferPanel = Prefix + "subwoofer-panel";
                MountPointAuthoring panelMount = assembly.MountPoints.SingleOrDefault(value =>
                    value != null && value.MountId == MountPrefix + "panel-back");
                if (!required.Contains(rearPanel) || !required.Contains(subwooferPanel) ||
                    panelMount?.Definition == null || !panelMount.Definition.AcceptsPart(rearPanel) ||
                    !panelMount.Definition.AcceptsPart(subwooferPanel))
                    throw new InvalidDataException("The explicit stock rear-panel alternative contract changed.");
                required.Remove(subwooferPanel);
                report.excludedParts.Add(new ExcludedPartRecord
                {
                    partId = subwooferPanel,
                    selectedInstead = rearPanel,
                    mountId = panelMount.MountId,
                    reason = "Alternative rear parcel shelf: the standard back panel occupies this single mount.",
                });
                foreach (PartManifest alternative in manifest.looseParts.Where(value =>
                    value.group == "PartsGT" || value.group == "PartsExtra"))
                    report.excludedParts.Add(new ExcludedPartRecord
                    {
                        partId = alternative.partDefinitionId,
                        reason = "Optional " + alternative.group + " presentation excluded from the stock display recipe.",
                    });
                foreach (PartInstance part in assembly.Parts)
                    if (part != null && part.Definition != null && required.Contains(part.Definition.DefinitionId))
                        parts.Add(part.Definition.DefinitionId, part);
                foreach (string id in required)
                    if (!parts.ContainsKey(id)) throw new InvalidDataException("Required stock part is missing: " + id);
                if (parts.Count < 100) throw new InvalidDataException("The stock Satsuma roster is unexpectedly incomplete.");
                var occupied = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in parts.Keys.OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (id == BodyId) continue;
                    MountPointAuthoring[] matches = assembly.MountPoints.Where(value => value != null &&
                        value.Definition != null && value.Definition.AcceptsPart(id)).ToArray();
                    string chosen = ExplicitInterchangeableMount(id);
                    if (chosen != null) matches = matches.Where(value => value.MountId == chosen).ToArray();
                    if (matches.Length != 1)
                        throw new InvalidDataException($"Required part {id} has {matches.Length} unresolved mount choices.");
                    if (!occupied.Add(matches[0].MountId))
                        throw new InvalidDataException("Alternative parts would occupy the same preview mount: " + matches[0].MountId);
                    assignments.Add(id, matches[0]);
                }
                foreach (PartInstance part in parts.Values)
                {
                    SatsumaRearSuspensionPartPresentation rearPresentation =
                        part.GetComponent<SatsumaRearSuspensionPartPresentation>();
                    if (rearPresentation != null)
                    {
                        var rearData = new SerializedObject(rearPresentation);
                        if (rearPresentation.IsSpring)
                            requiredInstalledRenderers.Add(Reference<MeshRenderer>(rearData, "springLooseRenderer"));
                        if (rearPresentation.IsShock)
                        {
                            requiredInstalledRenderers.Add(Reference<Transform>(rearData, "shockTopMesh").GetComponent<MeshRenderer>());
                            requiredInstalledRenderers.Add(Reference<Transform>(rearData, "shockBottomMesh").GetComponent<MeshRenderer>());
                        }
                    }
                    foreach (MonoBehaviour component in part.GetComponents<MonoBehaviour>())
                    {
                        string type = component != null ? component.GetType().FullName : string.Empty;
                        if (type == "MSC.Vehicle.NWH.SatsumaFrontStrutPresentation" ||
                            type == "MSC.Vehicle.NWH.SatsumaFrontSteeringRodPresentation")
                        {
                            var serialized = new SerializedObject(component);
                            MeshRenderer loose = Reference<MeshRenderer>(serialized, "looseRenderer");
                            SkinnedMeshRenderer skin = Reference<SkinnedMeshRenderer>(serialized, "installedRenderer");
                            replacedLooseRenderers.Add(loose);
                            installedSkins.Add(skin);
                        }
                    }
                    AssemblyHingedPartInteractionTarget hinge = part.GetComponent<AssemblyHingedPartInteractionTarget>();
                    if (hinge == null) continue;
                    var hingeData = new SerializedObject(hinge);
                    ReadPresentationObjects(hingeData, "installedOnlyPresentationObjects", installedVisible);
                    ReadPresentationObjects(hingeData, "detachedOnlyPresentationObjects", installedHidden);
                }
            }

            public void ComposeInstalledParts()
            {
                installed.Add(BodyId);
                report.parts.Add(new PartRecord { partId = BodyId, mountId = string.Empty, ownerPartId = string.Empty });
                foreach (string id in parts.Keys.OrderBy(value => value, StringComparer.Ordinal)) Install(id);
            }

            private void Install(string id)
            {
                if (installed.Contains(id)) return;
                if (!visiting.Add(id)) throw new InvalidDataException("Cyclic preview mount ownership: " + id);
                MountPointAuthoring mount = assignments[id];
                string owner = mount.Definition.OwnerPartDefinitionId;
                if (!parts.ContainsKey(owner)) throw new InvalidDataException("Required mount owner is missing: " + owner);
                Install(owner);
                Transform clone = CopyTransform(parts[id].transform);
                Transform pose = CopyTransform(mount.Pose);
                if (pose.IsChildOf(clone)) throw new InvalidDataException("Mount pose would create a transform cycle: " + id);
                clone.SetParent(pose, true);
                clone.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visiting.Remove(id);
                installed.Add(id);
                report.parts.Add(new PartRecord { partId = id, mountId = mount.MountId, ownerPartId = owner });
            }

            private Transform CopyTransform(Transform original)
            {
                if (original == null) throw new InvalidDataException("A required presentation transform is missing.");
                if (transforms.TryGetValue(original, out Transform existing)) return existing;
                if (!original.IsChildOf(source.transform)) throw new InvalidDataException("Presentation reference escapes the Satsuma prefab.");
                Transform parent = CopyTransform(original.parent);
                Transform copy = new GameObject(original.name).transform;
                copy.SetParent(parent, false);
                copy.SetLocalPositionAndRotation(original.localPosition, original.localRotation);
                copy.localScale = original.localScale;
                transforms.Add(original, copy);
                return copy;
            }

            public void ConfigureFrontPose()
            {
                MonoBehaviour controller = source.GetComponents<MonoBehaviour>().SingleOrDefault(value =>
                    value != null && value.GetType().FullName == "MSC.Vehicle.NWH.SatsumaFrontSuspensionController");
                if (controller == null) throw new InvalidDataException("The accepted front suspension pose profile is missing.");
                SerializedProperty corners = new SerializedObject(controller).FindProperty("corners");
                if (corners == null || corners.arraySize != 2) throw new InvalidDataException("Expected two authored front corners.");
                for (int index = 0; index < corners.arraySize; index++)
                {
                    SerializedProperty corner = corners.GetArrayElementAtIndex(index);
                    Vector3 hub = Vector(corner, "fullDroopHubLocalPosition");
                    Quaternion rotation = Rotation(corner, "fullDroopHubLocalRotation");
                    Vector3 pivot = Vector(corner, "wishboneBodyPivotLocalPosition");
                    Vector3 direction = hub + rotation * Vector(corner, "hubToWishboneTargetLocalOffset") - pivot;
                    direction.z = 0f;
                    Vector3 outward = corner.FindPropertyRelative("leftSide").boolValue ? Vector3.left : Vector3.right;
                    float angle = direction.sqrMagnitude > 0.0000001f
                        ? Vector3.SignedAngle(outward, direction.normalized, Vector3.forward) : 0f;
                    SetMountPose(corner, "wishboneMount", pivot,
                        Quaternion.AngleAxis(angle, Vector3.forward) * Rotation(corner, "wishboneMeshZeroLocalRotation"));
                    SetMountPose(corner, "spindleMount", hub + rotation * Vector(corner, "hubToSpindleMeshLocalOffset"),
                        rotation * Rotation(corner, "spindleMeshLocalRotation"));
                    SetMountPose(corner, "discBrakeMount", hub + rotation * Vector(corner, "hubToDiscBrakeLocalOffset"),
                        rotation * Rotation(corner, "discBrakeLocalRotation"));
                    SetMountPose(corner, "roadWheelMount", hub + rotation * Vector(corner, "hubToRoadWheelLocalOffset"),
                        rotation * Rotation(corner, "roadWheelLocalRotation"));
                    SetTargetPose(corner, "shockBottomTarget", hub + rotation * Vector(corner, "hubToShockBottomLocalOffset"),
                        rotation * Rotation(corner, "shockBottomLocalRotation"));
                    SetTargetPose(corner, "steeringOuterTarget", hub + rotation * Vector(corner, "hubToSteeringOuterLocalOffset"),
                        rotation * Rotation(corner, "steeringOuterLocalRotation"));
                }
            }

            private void SetMountPose(SerializedProperty corner, string field, Vector3 position, Quaternion rotation)
            {
                var mount = corner.FindPropertyRelative(field).objectReferenceValue as MountPointAuthoring;
                if (mount == null) throw new InvalidDataException("Missing front mount: " + field);
                CopyTransform(mount.transform).SetPositionAndRotation(root.transform.TransformPoint(position), root.transform.rotation * rotation);
            }

            private void SetTargetPose(SerializedProperty corner, string field, Vector3 position, Quaternion rotation)
            {
                var target = corner.FindPropertyRelative(field).objectReferenceValue as Transform;
                CopyTransform(target).SetPositionAndRotation(root.transform.TransformPoint(position), root.transform.rotation * rotation);
            }

            public void OpenHood()
            {
                MountPointAuthoring mount = assignments[Prefix + "hood"];
                AssemblyHingeMountAuthoring hinge = mount.GetComponent<AssemblyHingeMountAuthoring>();
                if (hinge == null || !Mathf.Approximately(hinge.OpenAngleDegrees, -87f))
                    throw new InvalidDataException("The accepted hood hinge/open pose is missing or changed.");
                CopyTransform(parts[Prefix + "hood"].transform).localRotation =
                    Quaternion.AngleAxis(hinge.OpenAngleDegrees, hinge.LocalAxis);
            }

            public void CopyRenderers()
            {
                var representedParts = new HashSet<string>(StringComparer.Ordinal);
                foreach (Renderer original in source.GetComponentsInChildren<Renderer>(true))
                {
                    PartInstance part = original.GetComponentInParent<PartInstance>(true);
                    if (part == null || part.Definition == null || !parts.ContainsKey(part.Definition.DefinitionId)) continue;
                    if (replacedLooseRenderers.Contains(original)) continue;
                    bool installedSkin = original is SkinnedMeshRenderer skin && installedSkins.Contains(skin);
                    bool explicitlyRequired = installedSkin || requiredInstalledRenderers.Contains(original);
                    if ((!original.enabled && !explicitlyRequired) ||
                        (!explicitlyRequired && !VisibleWhenInstalled(original.transform, part.transform))) continue;
                    Transform target = CopyTransform(original.transform);
                    if (original is SkinnedMeshRenderer sourceSkin)
                    {
                        if (!installedSkin) throw new InvalidDataException("Unclassified skinned preview renderer: " + original.name);
                        SkinnedMeshRenderer copy = target.gameObject.AddComponent<SkinnedMeshRenderer>();
                        copy.sharedMesh = sourceSkin.sharedMesh;
                        copy.sharedMaterials = sourceSkin.sharedMaterials;
                        copy.bones = sourceSkin.bones.Select(CopyTransform).ToArray();
                        copy.rootBone = CopyTransform(sourceSkin.rootBone);
                        copy.localBounds = sourceSkin.localBounds;
                        skins.Add(new KeyValuePair<SkinnedMeshRenderer, SkinnedMeshRenderer>(sourceSkin, copy));
                    }
                    else if (original is MeshRenderer meshRenderer)
                    {
                        Mesh mesh = original.GetComponent<MeshFilter>()?.sharedMesh;
                        if (mesh == null) throw new InvalidDataException("Required static mesh is missing: " + original.name);
                        target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        MeshRenderer copy = target.gameObject.AddComponent<MeshRenderer>();
                        CopyRendererSettings(meshRenderer, copy);
                        renderers.Add(original, copy);
                    }
                    else throw new InvalidDataException("Unsupported renderer in preview: " + original.GetType().Name);
                    representedParts.Add(part.Definition.DefinitionId);
                }
                string[] missing = parts.Keys.Where(value => !representedParts.Contains(value)).OrderBy(value => value).ToArray();
                if (missing.Length != 0)
                    throw new InvalidDataException("Selected stock parts have no copied presentation: " + string.Join(", ", missing));
            }

            private bool VisibleWhenInstalled(Transform candidate, Transform part)
            {
                bool active = true;
                for (Transform current = candidate; current != null; current = current.parent)
                {
                    if (installedHidden.Contains(current)) return false;
                    if (installedVisible.Contains(current)) break;
                    // Recipe selection, not the loose inventory's active flag,
                    // decides whether the installed part exists in the preview.
                    if (current == part) break;
                    if (!current.gameObject.activeSelf) active = false;
                }
                return active;
            }

            public void ConfigureRearPresentation()
            {
                SatsumaRearSuspensionController rear = source.GetComponent<SatsumaRearSuspensionController>();
                if (rear == null || rear.Corners.Count != 2) throw new InvalidDataException("Rear suspension corner targets are missing.");
                foreach (SatsumaRearSuspensionCornerBinding corner in rear.Corners)
                {
                    string springId = assignments.Single(value => value.Value.MountId == corner.StockSpringMountId).Key;
                    SatsumaRearSuspensionPartPresentation spring = parts[springId].GetComponent<SatsumaRearSuspensionPartPresentation>();
                    var springData = new SerializedObject(spring);
                    MeshRenderer springSource = Reference<MeshRenderer>(springData, "springLooseRenderer");
                    if (!renderers.TryGetValue(springSource, out MeshRenderer springCopy))
                        throw new InvalidDataException("Required rear spring renderer was excluded: " + springId);
                    FitSpring(springCopy, CopyTransform(corner.SpringTopBone).position,
                        CopyTransform(corner.SpringBottomAnchor).position);
                    string shockId = assignments.Single(value => value.Value.MountId == corner.ShockMountId).Key;
                    var shockData = new SerializedObject(parts[shockId].GetComponent<SatsumaRearSuspensionPartPresentation>());
                    Transform top = CopyTransform(Reference<Transform>(shockData, "shockTopMesh"));
                    Transform bottom = CopyTransform(Reference<Transform>(shockData, "shockBottomMesh"));
                    Vector3 topPosition = CopyTransform(corner.ShockTopTarget).position;
                    Vector3 bottomPosition = CopyTransform(corner.ShockBottomTarget).position;
                    Vector3 axis = topPosition - bottomPosition;
                    if (axis.sqrMagnitude < 0.000001f) throw new InvalidDataException("Rear shock endpoints coincide.");
                    top.SetPositionAndRotation(topPosition, Quaternion.LookRotation(axis.normalized, -root.transform.forward));
                    bottom.SetPositionAndRotation(bottomPosition, Quaternion.LookRotation(axis.normalized, root.transform.forward));
                }
            }

            public void BakeSkins()
            {
                foreach (KeyValuePair<SkinnedMeshRenderer, SkinnedMeshRenderer> pair in skins)
                {
                    var mesh = new Mesh { name = "MenuPreview_" + pair.Key.name };
                    temporaryMeshes.Add(mesh);
                    pair.Value.BakeMesh(mesh);
                    MeshFilter filter = pair.Value.gameObject.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    GameObject owner = pair.Value.gameObject;
                    Object.DestroyImmediate(pair.Value);
                    MeshRenderer renderer = owner.AddComponent<MeshRenderer>();
                    CopyRendererSettings(pair.Key, renderer);
                    renderers.Add(pair.Key, renderer);
                    baked.Add(new KeyValuePair<MeshFilter, Mesh>(filter, mesh));
                }
            }

            public MainMenuVehiclePaintBinding[] CopyPaintBindings(VehiclePaintStateController paint)
            {
                VehiclePaintMaterialProfile regular = paint.PaintMaterialProfiles.SingleOrDefault(value => value.PaintType == VehiclePaintType.Regular);
                if (regular?.Material == null || !regular.AppliesBodyColor)
                    throw new InvalidDataException("The accepted regular paint profile is missing.");
                var bindings = new List<MainMenuVehiclePaintBinding>();
                var surfaces = new HashSet<string>(StringComparer.Ordinal);
                foreach (VehiclePaintSurfaceBinding binding in paint.PaintSurfaceBindings)
                {
                    if (!surfaces.Add(binding.SurfaceId) || !renderers.TryGetValue(binding.Renderer, out MeshRenderer renderer))
                        throw new InvalidDataException("Missing/duplicate preview paint surface: " + binding.SurfaceId);
                    Material[] materials = renderer.sharedMaterials;
                    int[] slots = binding.MaterialIndices.ToArray();
                    if (slots.Length == 0) throw new InvalidDataException("Paint surface has no material slots: " + binding.SurfaceId);
                    foreach (int slot in slots)
                    {
                        if (slot < 0 || slot >= materials.Length) throw new InvalidDataException("Paint slot is out of bounds.");
                        materials[slot] = regular.Material;
                    }
                    renderer.sharedMaterials = materials;
                    bindings.Add(new MainMenuVehiclePaintBinding(binding.SurfaceId, renderer, slots));
                    report.paintBindings.Add(new PaintRecord { surfaceId = binding.SurfaceId, materialSlots = slots });
                }
                if (surfaces.Count != 7 || SatsumaPaintSurfaceIds.All.Any(value => !surfaces.Contains(value)))
                    throw new InvalidDataException("The menu preview must preserve all seven Satsuma paint surfaces.");
                return bindings.ToArray();
            }

            public MainMenuVehicleLampBinding[] CopyLampBindings()
            {
                var result = new List<MainMenuVehicleLampBinding>(4);
                foreach (string slug in new[] { "headlight-left", "headlight-right", "rear-light-left", "rear-light-right" })
                {
                    string partId = Prefix + slug;
                    bool headlight = slug.StartsWith("headlight-", StringComparison.Ordinal);
                    string meshGuid = headlight ? HeadlightMeshGuid : RearLampMeshGuid;
                    string materialGuid = headlight ? HeadlightMaterialGuid : RearLampMaterialGuid;
                    Mesh expectedMesh = RequireAssetGuid<Mesh>(meshGuid);
                    Material expectedMaterial = RequireAssetGuid<Material>(materialGuid);
                    if (!parts.TryGetValue(partId, out PartInstance part))
                        throw new InvalidDataException("Required preview lamp part is missing: " + partId);
                    MeshFilter[] matches = part.GetComponentsInChildren<MeshFilter>(true)
                        .Where(value => value.sharedMesh == expectedMesh &&
                            value.GetComponentInParent<PartInstance>(true) == part).ToArray();
                    MeshRenderer originalLens = matches.Length == 1 ? matches[0].GetComponent<MeshRenderer>() : null;
                    if (originalLens == null || !renderers.TryGetValue(originalLens, out MeshRenderer lens))
                        throw new InvalidDataException("Expected one explicitly mapped lamp lens: " + partId);
                    Material[] materials = lens.sharedMaterials;
                    if (materials.Length != 1 || materials[0] != expectedMaterial ||
                        expectedMaterial.shader == null || expectedMaterial.shader.name != "HDRP/Lit")
                        throw new InvalidDataException("The accepted HDRP lamp lens material changed: " + partId);
                    MeshFilter filter = lens.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh != expectedMesh)
                        throw new InvalidDataException("The copied lamp mesh does not match its source: " + partId);

                    Vector3[] vertices = expectedMesh.vertices;
                    if (!headlight)
                    {
                        if (rearLampSections == null)
                        {
                            SplitRearLampTriangles(expectedMesh, out List<int> red, out List<int> lower);
                            rearLampSections = Object.Instantiate(expectedMesh);
                            temporaryMeshes.Add(rearLampSections);
                            rearLampSections.name = "MenuPreview_RearLampSections";
                            rearLampSections.subMeshCount = 2;
                            rearLampSections.SetTriangles(red, 0, false);
                            rearLampSections.SetTriangles(lower, 1, false);
                            rearLampSections.bounds = expectedMesh.bounds;
                            ValidateRearLampSections(expectedMesh, rearLampSections);
                            string sourcePath = AssetDatabase.GetAssetPath(expectedMesh);
                            report.rearLampDerivation = new MeshDerivationRecord
                            {
                                sourceMeshGuid = meshGuid,
                                sourceMeshPath = sourcePath,
                                sourceMeshSha256 = Sha256(sourcePath),
                                outputMeshPath = RearLampSectionsPath,
                                sourceVertexCount = expectedMesh.vertexCount,
                                redTriangleCount = red.Count / 3,
                                lowerTriangleCount = lower.Count / 3,
                                derivation = "One shared menu-only mesh clone. Original triangles partitioned into submesh 0 (red lens, vertices 0..23) and submesh 1 (lower amber/white lens, vertices 24..43). Vertex buffers, UVs, normals, tangents and bounds unchanged; both slots retain the source material.",
                            };
                        }
                        filter.sharedMesh = rearLampSections;
                        lens.sharedMaterials = new[] { expectedMaterial, expectedMaterial };
                        rearLampFilters.Add(filter);
                    }

                    // Imported mesh forward points upwards. Beam direction is explicitly
                    // authored in model coordinates, where +Z is the front of the car.
                    Bounds bounds = LampBoundsInRoot(root.transform, lens.transform, vertices,
                        headlight ? vertices.Length : 24);
                    Vector3 position = bounds.center;
                    position.z = headlight ? bounds.max.z + 0.02f : bounds.min.z - 0.02f;
                    Vector3 direction = headlight
                        ? Quaternion.Euler(6f, 0f, 0f) * Vector3.forward : Vector3.back;
                    result.Add(new MainMenuVehicleLampBinding(partId, lens, 0, position, direction, headlight));
                    report.lampBindings.Add(new LampRecord
                    {
                        lampId = partId,
                        sourcePartId = partId,
                        sourceMeshGuid = meshGuid,
                        sourceMeshPath = AssetDatabase.GetAssetPath(expectedMesh),
                        sourceMaterialGuid = materialGuid,
                        sourceMaterialPath = AssetDatabase.GetAssetPath(expectedMaterial),
                        materialSlot = 0,
                        localPosition = position,
                        localDirection = direction,
                        headlight = headlight,
                    });
                }
                return result.ToArray();
            }

            public void PersistRearLampSections()
            {
                if (rearLampSections == null || rearLampFilters.Count != 2)
                    throw new InvalidDataException("Both rear lamps must share the derived red-lens mesh.");
                EnsureFolder(OutputDirectory + "/Meshes");
                Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(RearLampSectionsPath);
                if (existing == null) AssetDatabase.CreateAsset(rearLampSections, RearLampSectionsPath);
                else
                {
                    EditorUtility.CopySerialized(rearLampSections, existing);
                    EditorUtility.SetDirty(existing);
                    foreach (MeshFilter filter in rearLampFilters) filter.sharedMesh = existing;
                }
            }

            public void PersistBakedMeshes()
            {
                EnsureFolder(OutputDirectory + "/Meshes");
                for (int index = 0; index < baked.Count; index++)
                {
                    string path = OutputDirectory + "/Meshes/InstalledSuspension" + index + ".asset";
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing == null) AssetDatabase.CreateAsset(baked[index].Value, path);
                    else
                    {
                        EditorUtility.CopySerialized(baked[index].Value, existing);
                        EditorUtility.SetDirty(existing);
                        baked[index].Key.sharedMesh = existing;
                    }
                }
            }
        }

        private static T RequireAssetGuid<T>(string guid) where T : Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T value = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
            if (value == null) throw new InvalidDataException("Required accepted lamp asset is missing: " + guid);
            return value;
        }

        private static Bounds LampBoundsInRoot(Transform root, Transform lens, Vector3[] vertices, int count)
        {
            if (count <= 0 || count > vertices.Length)
                throw new InvalidDataException("Lamp vertices cannot define the requested lens section.");
            Matrix4x4 toRoot = root.worldToLocalMatrix * lens.localToWorldMatrix;
            var bounds = new Bounds(toRoot.MultiplyPoint3x4(vertices[0]), Vector3.zero);
            for (int index = 1; index < count; index++) bounds.Encapsulate(toRoot.MultiplyPoint3x4(vertices[index]));
            return bounds;
        }

        private static void SplitRearLampTriangles(Mesh source, out List<int> red, out List<int> lower)
        {
            if (source == null || !source.isReadable || source.vertexCount != 44 ||
                source.subMeshCount != 1 || source.GetTopology(0) != MeshTopology.Triangles)
                throw new InvalidDataException("The accepted rear-lens mesh must contain 44 vertices and one triangle submesh.");
            int[] triangles = source.GetTriangles(0);
            if (triangles.Length == 0 || triangles.Length % 3 != 0)
                throw new InvalidDataException("The rear-lens triangle buffer is invalid.");
            red = new List<int>();
            lower = new List<int>();
            for (int index = 0; index < triangles.Length; index += 3)
            {
                int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
                if (a < 0 || a >= 44 || b < 0 || b >= 44 || c < 0 || c >= 44 ||
                    (a < 24) != (b < 24) || (a < 24) != (c < 24))
                    throw new InvalidDataException("A rear-lens triangle crosses the explicit red/lower vertex partition.");
                List<int> target = a < 24 ? red : lower;
                target.Add(a); target.Add(b); target.Add(c);
            }
            if (red.Count == 0 || lower.Count == 0)
                throw new InvalidDataException("Both accepted rear-lens sections must contain triangles.");
        }

        private static void ValidateRearLampSections(Mesh source, Mesh derived)
        {
            SplitRearLampTriangles(source, out List<int> red, out List<int> lower);
            if (derived == null || derived == source || derived.vertexCount != source.vertexCount ||
                derived.subMeshCount != 2 || derived.bounds != source.bounds ||
                !derived.vertices.SequenceEqual(source.vertices) || !derived.normals.SequenceEqual(source.normals) ||
                !derived.tangents.SequenceEqual(source.tangents) || !derived.colors32.SequenceEqual(source.colors32) ||
                derived.GetTopology(0) != MeshTopology.Triangles || derived.GetTopology(1) != MeshTopology.Triangles ||
                !derived.GetTriangles(0).SequenceEqual(red) || !derived.GetTriangles(1).SequenceEqual(lower))
                throw new InvalidDataException("The menu-only rear mesh must preserve geometry and the exact source triangle partition.");
            var sourceUv = new List<Vector4>();
            var derivedUv = new List<Vector4>();
            for (int channel = 0; channel < 8; channel++)
            {
                source.GetUVs(channel, sourceUv);
                derived.GetUVs(channel, derivedUv);
                if (!sourceUv.SequenceEqual(derivedUv))
                    throw new InvalidDataException("Rear-lens derivation changed source UV channel " + channel);
            }
        }

        private static string ExplicitInterchangeableMount(string id)
        {
            string slug = id.Substring(Prefix.Length);
            switch (slug)
            {
                case "drum-brake-1": return MountPrefix + "drum-brake-rl";
                case "drum-brake-2": return MountPrefix + "drum-brake-rr";
                case "shock-absorber-1": return MountPrefix + "shock-rl";
                case "shock-absorber-2": return MountPrefix + "shock-rr";
                case "halfshaft-1": return MountPrefix + "halfshaft-fl";
                case "halfshaft-2": return MountPrefix + "halfshaft-fr";
                case "disc-brake-1": return MountPrefix + "discbrake-fl";
                case "disc-brake-2": return MountPrefix + "discbrake-fr";
                case "coil-spring-1": return MountPrefix + "coilspring-rl";
                case "coil-spring-2": return MountPrefix + "coilspring-rr";
                case "hubcap-1": return MountPrefix + "wheel-stock-fl.hubcap";
                case "hubcap-2": return MountPrefix + "wheel-stock-fr.hubcap";
                case "hubcap-3": return MountPrefix + "wheel-stock-rl.hubcap";
                case "hubcap-4": return MountPrefix + "wheel-stock-rr.hubcap";
                case "wheel-stock-fl": return MountPrefix + "wheelfl-new";
                case "wheel-stock-fr": return MountPrefix + "wheelfr-new";
                case "wheel-stock-rl": return MountPrefix + "wheelrl-new";
                case "wheel-stock-rr": return MountPrefix + "wheelrr-new";
                default: return null;
            }
        }

        private static void FitSpring(MeshRenderer renderer, Vector3 top, Vector3 bottom)
        {
            Transform transform = renderer.transform;
            Bounds bounds = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
            Vector3 size = bounds.size;
            int axis = size.y > size.x ? 1 : 0;
            if (size.z > size[axis]) axis = 2;
            Vector3 unit = Vector3.zero; unit[axis] = 1f;
            Vector3 negative = bounds.center - unit * size[axis] * 0.5f;
            Vector3 positive = bounds.center + unit * size[axis] * 0.5f;
            bool negativeTop = (transform.TransformPoint(negative) - top).sqrMagnitude <=
                (transform.TransformPoint(positive) - top).sqrMagnitude;
            Vector3 sourceTop = negativeTop ? negative : positive;
            Vector3 sourceBottom = negativeTop ? positive : negative;
            Vector3 sourceDirection = transform.TransformPoint(sourceBottom) - transform.TransformPoint(sourceTop);
            Vector3 desired = bottom - top;
            if (sourceDirection.magnitude < 0.0001f || desired.magnitude < 0.0001f)
                throw new InvalidDataException("Rear spring endpoints cannot define a presentation pose.");
            Quaternion rotation = Quaternion.FromToRotation(sourceDirection.normalized, desired.normalized) * transform.rotation;
            Vector3 scale = transform.localScale;
            scale[axis] *= desired.magnitude / sourceDirection.magnitude;
            transform.rotation = rotation;
            transform.localScale = scale;
            transform.position += top - transform.TransformPoint(sourceTop);
        }

        private static void ReadPresentationObjects(SerializedObject source, string propertyName, HashSet<Transform> targets)
        {
            SerializedProperty array = source.FindProperty(propertyName);
            if (array == null) throw new InvalidDataException("Hinge presentation schema changed: " + propertyName);
            for (int index = 0; index < array.arraySize; index++)
                if (array.GetArrayElementAtIndex(index).objectReferenceValue is GameObject value) targets.Add(value.transform);
        }

        private static T Reference<T>(SerializedObject source, string property) where T : Object =>
            source.FindProperty(property)?.objectReferenceValue as T ??
                throw new InvalidDataException("Required serialized presentation reference is missing: " + property);

        private static Vector3 Vector(SerializedProperty source, string field) => source.FindPropertyRelative(field).vector3Value;
        private static Quaternion Rotation(SerializedProperty source, string field) => source.FindPropertyRelative(field).quaternionValue;

        private static void CopyRendererSettings(Renderer source, MeshRenderer target)
        {
            target.sharedMaterials = source.sharedMaterials;
            target.shadowCastingMode = source.shadowCastingMode;
            target.receiveShadows = source.receiveShadows;
            target.lightProbeUsage = source.lightProbeUsage;
            target.reflectionProbeUsage = source.reflectionProbeUsage;
            target.renderingLayerMask = source.renderingLayerMask;
            target.enabled = true;
        }

        private static void PruneEmptyChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                PruneEmptyChildren(child);
                if (child.childCount == 0 && child.GetComponent<Renderer>() == null)
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void ValidateOutput(MainMenuVehicleModel model)
        {
            if (model == null || model.RendererCount == 0 || model.PaintSurfaceCount != 7 || model.LampBindings.Count != 4)
                throw new InvalidDataException("Preview component/paint/lamp coverage is incomplete.");
            foreach (Component component in model.GetComponentsInChildren<Component>(true))
                if (!(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer) && component != model)
                    throw new InvalidDataException("Forbidden component leaked into menu preview: " + component.GetType().FullName);
            var required = new HashSet<string>(new[] { Prefix + "headlight-left", Prefix + "headlight-right",
                Prefix + "rear-light-left", Prefix + "rear-light-right" }, StringComparer.Ordinal);
            Mesh rearSections = null;
            foreach (MainMenuVehicleLampBinding lamp in model.LampBindings)
            {
                if (lamp == null || !required.Remove(lamp.LampId) || lamp.LensRenderer == null ||
                    !lamp.LensRenderer.transform.IsChildOf(model.transform) || lamp.MaterialSlot != 0)
                    throw new InvalidDataException("Menu lamps need four unique, explicit model lens bindings.");
                bool headlight = lamp.LampId.StartsWith(Prefix + "headlight-", StringComparison.Ordinal);
                if (lamp.IsHeadlight != headlight || lamp.LocalDirection.sqrMagnitude < 0.99f ||
                    Vector3.Dot(lamp.LocalDirection, headlight ? Vector3.forward : Vector3.back) < 0.98f)
                    throw new InvalidDataException("Menu lamp role or model-local beam direction is invalid.");
                Mesh mesh = lamp.LensRenderer.GetComponent<MeshFilter>()?.sharedMesh;
                Material expected = RequireAssetGuid<Material>(headlight ? HeadlightMaterialGuid : RearLampMaterialGuid);
                Material[] materials = lamp.LensRenderer.sharedMaterials;
                if (materials.Length != (headlight ? 1 : 2) || materials.Any(value => value != expected))
                    throw new InvalidDataException("Lamp lenses must retain the accepted material in every section.");
                if (headlight)
                {
                    if (mesh != RequireAssetGuid<Mesh>(HeadlightMeshGuid))
                        throw new InvalidDataException("The front lens mesh must remain unchanged.");
                }
                else
                {
                    ValidateRearLampSections(RequireAssetGuid<Mesh>(RearLampMeshGuid), mesh);
                    if (rearSections != null && rearSections != mesh)
                        throw new InvalidDataException("Both rear lamps must share one derived menu-only mesh.");
                    rearSections = mesh;
                }
            }
            if (required.Count != 0) throw new InvalidDataException("Required lamp bindings are missing.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Sha256(string path)
        {
            using SHA256 hash = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable] private sealed class Manifest
        {
            public string builderVersion = string.Empty;
            public string transferClassification = string.Empty;
            public PartManifest[] looseParts = Array.Empty<PartManifest>();
        }
        [Serializable] private sealed class PartManifest { public string group = string.Empty; public string partDefinitionId = string.Empty; }
        [Serializable] private sealed class PartRecord { public string partId; public string mountId; public string ownerPartId; }
        [Serializable] private sealed class PaintRecord { public string surfaceId; public int[] materialSlots; }
        [Serializable] private sealed class LampRecord
        {
            public string lampId;
            public string sourcePartId;
            public string sourceMeshGuid;
            public string sourceMeshPath;
            public string sourceMaterialGuid;
            public string sourceMaterialPath;
            public int materialSlot;
            public Vector3 localPosition;
            public Vector3 localDirection;
            public bool headlight;
        }
        [Serializable] private sealed class MeshDerivationRecord
        {
            public string sourceMeshGuid;
            public string sourceMeshPath;
            public string sourceMeshSha256;
            public string outputMeshPath;
            public int sourceVertexCount;
            public int redTriangleCount;
            public int lowerTriangleCount;
            public string derivation;
        }
        [Serializable] private sealed class ExcludedPartRecord
        {
            public string partId;
            public string selectedInstead = string.Empty;
            public string mountId = string.Empty;
            public string reason;
        }
        [Serializable] private sealed class Report
        {
            public string sourcePrefab;
            public string sourceSha256;
            public string sourceDependencyHash;
            public string sourceManifestSha256;
            public string sourceBuilderVersion;
            public string classification;
            public string pose;
            public string status;
            public int rendererCount;
            public int meshCount;
            public int paintSurfaceCount;
            public int lampCount;
            public int sourceStockPartCount;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
            public List<PartRecord> parts = new List<PartRecord>();
            public List<PaintRecord> paintBindings = new List<PaintRecord>();
            public List<LampRecord> lampBindings = new List<LampRecord>();
            public MeshDerivationRecord rearLampDerivation;
            public List<ExcludedPartRecord> excludedParts = new List<ExcludedPartRecord>();
            public List<string> missing = new List<string>();
        }
    }
}
