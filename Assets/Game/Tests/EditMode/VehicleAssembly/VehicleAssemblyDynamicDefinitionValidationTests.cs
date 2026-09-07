using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class VehicleAssemblyDynamicDefinitionValidationTests
    {
        [Test]
        public void DynamicSocketRequiresExplicitDefinitionAndOldOverloadRetainsItsContract()
        {
            using var f = new Fixture();
            var oldIssues = VehicleAssemblyValidator.Validate(f.Parts, f.Mounts, f.Dependencies, f.Tools);
            Assert.That(oldIssues.Select(issue => issue.Code), Is.EqualTo(new[] { "MOUNT-PART" }));
            Assert.That(f.Validate().Select(Describe), Is.EqualTo(oldIssues.Select(Describe)));
            Assert.That(f.Validate(f.Purchased), Is.Empty);
            Assert.That(f.Purchased.VisualPrefab, Is.Null,
                "The item wrapper supplies the purchased presentation; it is not a missing starting part prefab.");
        }

        [Test]
        public void DynamicDefinitionsRejectNullEmptyAndDuplicateIdentities()
        {
            using var f = new Fixture();
            PartDefinition empty = f.Define(string.Empty);
            Assert.That(f.Validate((PartDefinition)null).Select(issue => issue.Code), Does.Contain("PART-DYNAMIC-NULL"));
            Assert.That(f.Validate(empty).Select(issue => issue.Code), Does.Contain("PART-DYNAMIC-ID"));
            Assert.That(f.Validate(f.Purchased, f.Purchased).Select(issue => issue.Code), Does.Contain("PART-DYNAMIC-DUPLICATE"));
            PartDefinition sameId = f.Define(f.Purchased.DefinitionId);
            Assert.That(f.Validate(f.Purchased, sameId).Select(issue => issue.Code), Does.Contain("PART-DYNAMIC-DUPLICATE"));
        }

        [Test]
        public void ExactSharedStockDefinitionIsAllowedButReplacementAssetIsRejected()
        {
            using var f = new Fixture();
            Assert.That(f.Validate(f.Purchased, f.Stock), Is.Empty,
                "Purchased oil filters may reuse the same stock definition object.");
            PartDefinition replacement = f.Define(f.Stock.DefinitionId);
            Assert.That(f.Validate(f.Purchased, replacement).Select(issue => issue.Code),
                Does.Contain("PART-DYNAMIC-CONFLICT"));
            Assert.That(f.Parts[0].Definition, Is.SameAs(f.Stock));
        }

        [Test]
        public void DeclaredItemCannotHideForeignSocketOrBypassStartingPartVisualValidation()
        {
            using var f = new Fixture();
            f.AddSocket("fixture.foreign-part");
            var errors = f.Validate(f.Purchased);
            Assert.That(errors.Count(issue => issue.Code == "MOUNT-PART"), Is.EqualTo(1));
            Assert.That(errors.Single(issue => issue.Code == "MOUNT-PART").Message,
                Does.Contain("fixture.foreign-part"));
            f.Stock.Configure(f.Stock.DefinitionId, "stock", PartCategory.Engine, 1f, null,
                Array.Empty<PartCompatibilityRule>());
            Assert.That(f.Validate(f.Purchased, f.Stock).Select(issue => issue.Code), Does.Contain("PART-PREFAB"),
                "A dynamic catalog entry must not excuse a missing visual on a real starting PartInstance.");
        }

        private static string Describe(VehicleAssemblyValidationIssue issue) => issue.Severity + ":" + issue.Code + ":" + issue.Message;

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("dynamic definition validation fixture");
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            private readonly List<MountPointAuthoring> mounts = new List<MountPointAuthoring>();
            public readonly PartDefinition Stock;
            public readonly PartDefinition Purchased;
            public readonly PartInstance[] Parts;
            public MountPointAuthoring[] Mounts => mounts.ToArray();
            public AssemblyDependency[] Dependencies => Array.Empty<AssemblyDependency>();
            public ToolDefinition[] Tools => Array.Empty<ToolDefinition>();

            public Fixture()
            {
                root.SetActive(false);
                var visual = new GameObject("starting stock presentation");
                visual.transform.SetParent(root.transform, false);
                Stock = Define("fixture.stock", visual);
                Purchased = Define("fixture.purchased");
                var identity = root.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                PartInstance part = root.AddComponent<PartInstance>();
                part.Configure(Stock, identity, null, null, true, string.Empty);
                Parts = new[] { part };
                AddSocket(Purchased.DefinitionId);
            }

            public PartDefinition Define(string id, GameObject visual = null)
            {
                var definition = ScriptableObject.CreateInstance<PartDefinition>();
                definitions.Add(definition);
                definition.Configure(id, id, PartCategory.Engine, 1f, visual,
                    new[] { PartCompatibilityRule.Create("fixture.item", "fixture.stock") });
                return definition;
            }

            public void AddSocket(string acceptedId)
            {
                string id = "fixture.mount." + mounts.Count;
                var definition = ScriptableObject.CreateInstance<MountPointDefinition>();
                definitions.Add(definition);
                definition.Configure(id, id, "fixture.item", Stock.DefinitionId, new[] { acceptedId },
                    new MountConstraint(.1f, 45f, .1f, 0f), 0f, Array.Empty<FastenerDefinition>());
                var go = new GameObject(id);
                go.transform.SetParent(root.transform, false);
                var mount = go.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, go.transform, 0);
                mounts.Add(mount);
            }

            public IReadOnlyList<VehicleAssemblyValidationIssue> Validate(params PartDefinition[] dynamicDefinitions) =>
                VehicleAssemblyValidator.Validate(Parts, Mounts, Dependencies, Tools, dynamicDefinitions);

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
