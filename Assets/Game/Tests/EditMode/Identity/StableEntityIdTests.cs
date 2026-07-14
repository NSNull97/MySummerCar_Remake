using System.Collections.Generic;
using MSC.Core.Identity;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.Identity
{
    public sealed class StableEntityIdTests
    {
        [Test]
        public void NewId_UsesCanonicalLowerCaseGuidFormat()
        {
            StableEntityId entityId = StableEntityId.New();

            Assert.That(entityId.Value, Has.Length.EqualTo(StableEntityId.SerializedLength));
            Assert.That(entityId.Value, Is.EqualTo(entityId.Value.ToLowerInvariant()));
            Assert.That(StableEntityId.TryParse(entityId.Value, out StableEntityId parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(entityId));
        }

        [Test]
        public void TryParse_RejectsUpperCaseAndSeparatedGuids()
        {
            string lowerCaseId = StableEntityId.New().Value;

            Assert.That(StableEntityId.TryParse(lowerCaseId.ToUpperInvariant(), out _), Is.False);
            Assert.That(StableEntityId.TryParse(System.Guid.NewGuid().ToString("D"), out _), Is.False);
        }

        [Test]
        public void NewId_IsUniqueAcrossRepresentativeAuthoringBatch()
        {
            var ids = new HashSet<StableEntityId>();

            for (int index = 0; index < 512; index++)
            {
                Assert.That(ids.Add(StableEntityId.New()), Is.True);
            }
        }

        [Test]
        public void AuthoringComponent_DoesNotGenerateIdImplicitly()
        {
            var gameObject = new GameObject("Stable ID test object");

            try
            {
                StableEntityIdAuthoring authoring = gameObject.AddComponent<StableEntityIdAuthoring>();
                Assert.That(authoring.SerializedId, Is.Empty);
                Assert.That(authoring.TryGetStableId(out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
