using System;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Items.Tests.EditMode
{
    public sealed class InactiveItemCompoundOwnershipTests
    {
        [Test]
        public void InactiveAncestorOwnsItsExplicitChildSolidDuringRuntimeRegistration()
        {
            using var fixture = new Fixture();
            BoxCollider source = fixture.CreateChildSolid(fixture.Candidate.transform);
            source.enabled = false; // Save restore can already have disabled the installed solid.
            Assert.That(source.gameObject.activeInHierarchy, Is.False);
            Assert.That(source.GetComponentInParent<PartInstance>(true), Is.SameAs(fixture.Candidate));
            Assert.That(source.GetComponentInParent<Rigidbody>(true), Is.SameAs(fixture.Candidate.Body));
            float mass = fixture.Candidate.Body.mass;
            Vector3 center = fixture.Candidate.Body.centerOfMass;

            bool registered = fixture.Physics.TryRegisterRuntimeBinding(
                new AssemblyCompoundShapeBinding(fixture.Candidate, new Collider[] { source }), out string failure);

            Assert.That(registered, Is.True, failure);
            Assert.That(fixture.Physics.HasRuntimeBinding(fixture.Candidate), Is.True);
            AssemblyCompoundColliderProxy[] proxies = fixture.Proxies();
            Assert.That(proxies, Has.Length.EqualTo(1));
            Assert.That(proxies[0].SourcePart, Is.SameAs(fixture.Candidate));
            Assert.That(proxies[0].SourceShape, Is.SameAs(source));
            Assert.That(proxies[0].GetComponent<BoxCollider>().size, Is.EqualTo(source.size));
            Assert.That(proxies[0].GetComponent<Collider>().enabled, Is.False);
            Assert.That(fixture.Physics.ActiveProxyCount, Is.Zero);
            Assert.That(source.enabled, Is.False);
            Assert.That(fixture.Candidate.Body.mass, Is.EqualTo(mass));
            Assert.That(fixture.Candidate.Body.centerOfMass, Is.EqualTo(center));
        }

        [TestCase(false, TestName = "InactiveChildWithWrongNearestPartIsRejectedWithoutContactOrMassMutation")]
        [TestCase(true, TestName = "InactiveChildWithForeignRigidbodyIsRejectedWithoutContactOrMassMutation")]
        public void InactiveChildWithForeignOwnershipIsRejectedWithoutContactOrMassMutation(bool foreignBody)
        {
            using var fixture = new Fixture();
            BoxCollider existingSource = fixture.CreateChildSolid(fixture.Existing.transform);
            Assert.That(fixture.Physics.TryRegisterRuntimeBinding(
                new AssemblyCompoundShapeBinding(fixture.Existing, new Collider[] { existingSource }), out string initialFailure),
                Is.True, initialFailure);
            BoxCollider invalidSource = fixture.CreateChildSolid(fixture.Candidate.transform);
            if (foreignBody)
            {
                Rigidbody nestedBody = invalidSource.gameObject.AddComponent<Rigidbody>();
                nestedBody.isKinematic = true;
                Assert.That(invalidSource.GetComponentInParent<PartInstance>(true), Is.SameAs(fixture.Candidate));
                Assert.That(invalidSource.GetComponentInParent<Rigidbody>(true), Is.SameAs(nestedBody));
            }
            else
            {
                // A nested wrapper must not borrow its parent's registered ownership merely
                // because both colliders would otherwise resolve to the same Rigidbody.
                PartInstance nestedPart = invalidSource.gameObject.AddComponent<PartInstance>();
                Assert.That(invalidSource.GetComponentInParent<PartInstance>(true), Is.SameAs(nestedPart));
                Assert.That(invalidSource.GetComponentInParent<Rigidbody>(true), Is.SameAs(fixture.Candidate.Body));
            }
            Assert.That(invalidSource.gameObject.activeInHierarchy, Is.False);
            AssemblyCompoundColliderProxy[] contacts = fixture.Proxies();
            Assert.That(contacts, Has.Length.EqualTo(1));
            AssemblyCompoundShapeBinding[] authoredBindings = fixture.Physics.Bindings;
            float existingMass = fixture.Existing.Body.mass;
            float candidateMass = fixture.Candidate.Body.mass;
            Vector3 existingCenter = fixture.Existing.Body.centerOfMass;
            Vector3 candidateCenter = fixture.Candidate.Body.centerOfMass;

            bool accepted = fixture.Physics.TryRegisterRuntimeBinding(
                new AssemblyCompoundShapeBinding(fixture.Candidate, new Collider[] { invalidSource }), out string failure);

            Assert.That(accepted, Is.False);
            Assert.That(failure, Is.Not.Empty);
            Assert.That(fixture.Physics.HasRuntimeBinding(fixture.Candidate), Is.False);
            Assert.That(fixture.Physics.HasRuntimeBinding(fixture.Existing), Is.True);
            Assert.That(fixture.Proxies(), Is.EquivalentTo(contacts));
            Assert.That(contacts[0].SourceShape, Is.SameAs(existingSource));
            Assert.That(contacts[0].GetComponent<Collider>().enabled, Is.False);
            Assert.That(contacts[0].gameObject.activeSelf, Is.False);
            Assert.That(fixture.Physics.ActiveProxyCount, Is.Zero);
            Assert.That(fixture.Physics.Bindings, Is.SameAs(authoredBindings));
            Assert.That(existingSource.enabled && invalidSource.enabled, Is.True);
            Assert.That(fixture.Existing.Body.mass, Is.EqualTo(existingMass));
            Assert.That(fixture.Candidate.Body.mass, Is.EqualTo(candidateMass));
            Assert.That(fixture.Existing.Body.centerOfMass, Is.EqualTo(existingCenter));
            Assert.That(fixture.Candidate.Body.centerOfMass, Is.EqualTo(candidateCenter));
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            private readonly PartDefinition definition;
            public PartInstance Existing { get; }
            public PartInstance Candidate { get; }
            public AssemblyLooseCompoundPhysics Physics { get; }

            public Fixture()
            {
                root = new GameObject("Inactive explicit item ownership fixture");
                root.SetActive(false);
                definition = ScriptableObject.CreateInstance<PartDefinition>();
                definition.Configure("test.inactive-compound-item", "Test item", PartCategory.Engine, 2f, null, null);
                Existing = CreatePart("Existing item");
                Candidate = CreatePart("Candidate item");
                VehicleAssemblyController assembly = root.AddComponent<VehicleAssemblyController>();
                assembly.Configure(new[] { Existing, Candidate }, Array.Empty<MountPointAuthoring>(),
                    Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
                Physics = root.AddComponent<AssemblyLooseCompoundPhysics>();
                Physics.Configure(assembly, Array.Empty<AssemblyCompoundShapeBinding>());
            }

            public BoxCollider CreateChildSolid(Transform owner)
            {
                var child = new GameObject("Explicit child solid");
                child.transform.SetParent(owner, false);
                BoxCollider collider = child.AddComponent<BoxCollider>();
                collider.size = new Vector3(.04f, .08f, .12f);
                return collider;
            }

            public AssemblyCompoundColliderProxy[] Proxies() => root.GetComponentsInChildren<AssemblyCompoundColliderProxy>(true);

            private PartInstance CreatePart(string name)
            {
                var item = new GameObject(name);
                item.transform.SetParent(root.transform, false);
                var identity = item.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = definition.MassKilograms;
                body.centerOfMass = new Vector3(.01f, .02f, .03f);
                PhysicsPickupTarget pickup = item.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, name, 120f);
                PartInstance part = item.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, false, string.Empty);
                return part;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(definition);
            }
        }
    }
}
