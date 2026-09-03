using System.Collections;
using MSC.LegacyImport;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.WorldBaseline
{
    public sealed class DonorWorldLegacyRendererOverridePlayModeTests
    {
        [UnityTest]
        public IEnumerator RendererOnlyOverride_HidesVisualAndRetainsExactLegacyCollider()
        {
            GameObject legacy = null;
            GameObject registryObject = null;
            try
            {
                legacy = new GameObject("canonical-rock-legacy");
                MeshRenderer renderer = legacy.AddComponent<MeshRenderer>();
                BoxCollider collider = legacy.AddComponent<BoxCollider>();
                DonorWorldBaselineEntityMetadata metadata = legacy
                    .AddComponent<DonorWorldBaselineEntityMetadata>();
                metadata.Configure("canonical-rock-authority", 1L, "",
                    "MAP/MESH/ROCKS", "mesh", "global", "Rock",
                    "23;33;64", "TemporaryDirectImport", "test", true,
                    true, true);

                registryObject = new GameObject("replacement-registry");
                DonorWorldLegacyReplacementRegistry registry = registryObject
                    .AddComponent<DonorWorldLegacyReplacementRegistry>();
                yield return null;

                string key = metadata.ReplacementKey;
                Assert.That(registry.LoadedReplacementCount,
                    Is.GreaterThanOrEqualTo(1));
                Assert.That(registry.SetProductionRendererOverrideActive(key,
                    true), Is.True);
                Assert.That(renderer.enabled, Is.False,
                    "ALP replacement owns only the visible surface.");
                Assert.That(collider.enabled, Is.True,
                    "Exact MAP/MESH/ROCKS collision must remain authoritative.");

                Assert.That(registry.SetProductionOverrideActive(key, true),
                    Is.True);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(collider.enabled, Is.False);
                registry.SetProductionOverrideActive(key, false);
                Assert.That(renderer.enabled, Is.False,
                    "Renderer-only ownership must survive full-override removal.");
                Assert.That(collider.enabled, Is.True);

                registry.SetProductionRendererOverrideActive(key, false);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(collider.enabled, Is.True);
            }
            finally
            {
                if (registryObject != null) Object.Destroy(registryObject);
                if (legacy != null) Object.Destroy(legacy);
            }
            yield return null;
        }
    }
}
