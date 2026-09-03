using System.Linq;
using System.Reflection;
using MSC.Interaction.Capabilities;
using MSC.Items;
using MSC.Presentation.Fire;
using MSC.Presentation.Fluid;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Presentation.Fluid.Tests.EditMode
{
    public sealed class ProceduralFluidStreamPresenterTests
    {
        private const string DefinitionCatalogPath =
            "Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset";

        [TestCase(FluidStreamProfile.Shower, 0.32f)]
        [TestCase(FluidStreamProfile.Faucet, 0.48f)]
        [TestCase(FluidStreamProfile.Urine, 0.44f)]
        public void ConfigureCreatesValidatedHdrpTransparentShortStreaks(
            FluidStreamProfile profile,
            float expectedLengthScale)
        {
            var root = new GameObject($"Fluid Test {profile}");
            try
            {
                ProceduralFluidStreamPresenter presenter =
                    root.AddComponent<ProceduralFluidStreamPresenter>();
                presenter.Configure(
                    profile,
                    Vector3.zero,
                    Vector3.forward,
                    0);

                ParticleSystemRenderer renderer =
                    root.GetComponent<ParticleSystemRenderer>();
                Material material = renderer.sharedMaterial;

                Assert.That(renderer.renderMode,
                    Is.EqualTo(ParticleSystemRenderMode.Stretch));
                Assert.That(renderer.lengthScale,
                    Is.EqualTo(expectedLengthScale).Within(0.001f));
                Assert.That(renderer.lengthScale, Is.LessThan(0.65f));
                Assert.That(renderer.velocityScale, Is.LessThan(0.03f));
                Assert.That(renderer.sortMode,
                    Is.EqualTo(ParticleSystemSortMode.Distance));

                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo("HDRP/Unlit"));
                Assert.That(material.GetFloat("_SurfaceType"),
                    Is.EqualTo(1f));
                Assert.That(material.GetInt("_ZWrite"), Is.Zero);
                Assert.That(material.GetInt("_SrcBlend"),
                    Is.EqualTo((int)BlendMode.One));
                Assert.That(material.GetInt("_DstBlend"),
                    Is.EqualTo((int)BlendMode.OneMinusSrcAlpha));
                Assert.That(material.renderQueue,
                    Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PuddlePresenterBuildsRainPoolAndPlacesLocalWaterSpill()
        {
            var root = new GameObject("Puddle Test");
            var follow = new GameObject("Puddle Follow Target");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var ignoredContainer =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Puddle Ground";
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.transform.localScale = new Vector3(80f, 0.2f, 80f);
            ignoredContainer.name = "Held spill container";
            ignoredContainer.transform.position = new Vector3(0f, 0.9f, 0f);
            ignoredContainer.transform.localScale =
                new Vector3(0.5f, 0.5f, 0.5f);
            try
            {
                Physics.SyncTransforms();
                ProceduralPuddlePresenter presenter =
                    root.AddComponent<ProceduralPuddlePresenter>();
                presenter.Configure(follow.transform, ~0);
                presenter.SetRainAmount(0.75f);
                InvokePrivate(presenter, "RefreshRainPuddles");

                Assert.That(presenter.RainAmount01, Is.EqualTo(0.75f));
                Assert.That(root.transform.childCount, Is.EqualTo(26));
                Transform rainPuddle = root.transform.Find("Rain puddle 1");
                Assert.That(rainPuddle.gameObject.activeSelf, Is.True);
                Vector3 anchoredRainPosition = rainPuddle.position;
                Vector3 initialRainDelta =
                    anchoredRainPosition - follow.transform.position;
                initialRainDelta.y = 0f;
                Assert.That(
                    initialRainDelta.magnitude,
                    Is.InRange(29.9f, 55.1f),
                    "Rain puddles must be prepared outside the player's immediate view.");
                var initialProperties = new MaterialPropertyBlock();
                rainPuddle.GetComponent<MeshRenderer>().GetPropertyBlock(
                    initialProperties);
                Assert.That(
                    initialProperties.GetColor("_BaseColor").a,
                    Is.Zero.Within(0.0001f),
                    "A newly anchored rain puddle must fade in instead of popping.");
                follow.transform.position = Vector3.MoveTowards(
                    follow.transform.position,
                    anchoredRainPosition,
                    3f);
                Physics.SyncTransforms();
                InvokePrivate(presenter, "RefreshRainPuddles");
                Assert.That(
                    rainPuddle.position,
                    Is.EqualTo(anchoredRainPosition),
                    "Approaching a rain puddle must not move it away.");
                Assert.That(
                    presenter.AddLocalPuddle(
                        ignoredContainer.transform.position,
                        0.5f,
                        ignoredContainer.transform),
                    Is.True);

                Transform localPuddle = root.transform.Find(
                    "Local spill puddle 1");
                Assert.That(localPuddle, Is.Not.Null);
                Assert.That(localPuddle.gameObject.activeSelf, Is.True);
                Assert.That(localPuddle.position.y, Is.LessThan(0.1f));
                Material material =
                    localPuddle.GetComponent<MeshRenderer>().sharedMaterial;
                Assert.That(material.shader.name, Is.EqualTo("HDRP/Lit"));
                Assert.That(material.GetFloat("_Smoothness"), Is.GreaterThan(0.9f));
                Assert.That(material.GetTexture("_BaseColorMap"), Is.Not.Null);
                var properties = new MaterialPropertyBlock();
                rainPuddle.GetComponent<MeshRenderer>().GetPropertyBlock(
                    properties);
                Color puddleColor = properties.GetColor("_BaseColor");
                Assert.That(
                    Mathf.Max(
                        Mathf.Abs(puddleColor.r - puddleColor.g),
                        Mathf.Abs(puddleColor.g - puddleColor.b)),
                    Is.LessThan(0.001f),
                    "Puddle absorption must be neutral rather than blue-tinted.");
                Assert.That(
                    material.GetColor("_BaseColor").r,
                    Is.EqualTo(material.GetColor("_BaseColor").b)
                        .Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(ignoredContainer);
                Object.DestroyImmediate(floor);
                Object.DestroyImmediate(follow);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BucketSurfaceReplacesStaticInsertAndTracksWaterLitres()
        {
            var root = new GameObject("Dynamic bucket water test");
            var visualRoot = new GameObject("Reviewed bucket presentation");
            visualRoot.transform.SetParent(root.transform, false);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(visualRoot.transform, false);
            body.transform.localScale = new Vector3(0.25f, 0.14f, 0.25f);
            GameObject staticInsert =
                GameObject.CreatePrimitive(PrimitiveType.Quad);
            staticInsert.transform.SetParent(visualRoot.transform, false);
            try
            {
                ItemDefinitionRecord definition =
                    LoadDefinition(
                        LiquidContainerSurfacePresenter.SaunaBucketDefinitionId);
                var state = new ItemInstanceState
                {
                    definitionId = definition.DefinitionId,
                    content = 0f,
                    liquidId = string.Empty,
                };
                WorldItemInstance item =
                    root.AddComponent<WorldItemInstance>();
                SetPrivateField(item, "definition", definition);
                SetPrivateField(item, "state", state);

                LiquidContainerSurfacePresenter presenter =
                    root.AddComponent<LiquidContainerSurfacePresenter>();
                presenter.Configure(item, visualRoot);

                Assert.That(
                    staticInsert.GetComponent<MeshRenderer>().enabled,
                    Is.False,
                    "The donor Water insert must not remain statically visible.");
                Assert.That(presenter.IsSurfaceVisible, Is.False);

                state.content = 5f;
                state.liquidId = LiquidTypeIds.Water;
                presenter.RefreshPresentation();
                Assert.That(presenter.IsSurfaceVisible, Is.True);
                Assert.That(presenter.SurfaceMaterial.shader.name,
                    Is.EqualTo("HDRP/Lit"));
                Assert.That(presenter.SurfaceMaterial.shader.isSupported,
                    Is.True);
                Assert.That(
                    Vector3.Dot(
                        presenter.SurfaceTransform.localPosition -
                        staticInsert.transform.localPosition,
                        presenter.LocalOpenAxis),
                    Is.LessThanOrEqualTo(0.0001f),
                    "The dynamic level must remain inside the mouth plane.");
                Assert.That(
                    presenter.SurfaceTransform.localScale.z,
                    Is.GreaterThan(0.004f),
                    "Container water must render as a volume, not a detached quad.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GarbageBarrelFireUsesSupportedHdrpMaterial()
        {
            var root = new GameObject("HDRP barrel fire test");
            GameObject visualRoot =
                GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualRoot.transform.SetParent(root.transform, false);
            try
            {
                ItemDefinitionRecord definition =
                    LoadDefinition("item.garbage-barrel");
                var state = new ItemInstanceState
                {
                    definitionId = definition.DefinitionId,
                    isEnabled = true,
                };
                WorldItemInstance item =
                    root.AddComponent<WorldItemInstance>();
                SetPrivateField(item, "definition", definition);
                SetPrivateField(item, "state", state);

                GarbageBarrelFirePresenter presenter =
                    root.AddComponent<GarbageBarrelFirePresenter>();
                presenter.Configure(item, visualRoot);

                Material material = presenter.FireRenderer.sharedMaterial;
                Assert.That(presenter.IsFireVisible, Is.True);
                Assert.That(material, Is.Not.Null);
                Assert.That(material.shader.name, Is.EqualTo("HDRP/Unlit"));
                Assert.That(material.shader.isSupported, Is.True);
                Assert.That(material.GetTexture("_UnlitColorMap"), Is.Not.Null);
                Assert.That(
                    material.GetTexture("_UnlitColorMap").name,
                    Is.EqualTo("FireSeq1"));
                Assert.That(presenter.UsesImportedPreset, Is.True);
                Assert.That(presenter.ParticleSystemCount, Is.EqualTo(6));
                ParticleSystemRenderer[] renderers =
                    root.GetComponentsInChildren<
                        ParticleSystemRenderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(6));
                for (int index = 0; index < renderers.Length; index++)
                {
                    Assert.That(renderers[index].sharedMaterial, Is.Not.Null);
                    Assert.That(
                        renderers[index].sharedMaterial.shader.name,
                        Is.EqualTo("HDRP/Unlit"));
                }

                ParticleSystem[] systems =
                    root.GetComponentsInChildren<ParticleSystem>(true);
                float totalEmissionRate = 0f;
                for (int index = 0; index < systems.Length; index++)
                {
                    ParticleSystem.MainModule main = systems[index].main;
                    ParticleSystem.EmissionModule emission =
                        systems[index].emission;
                    Assert.That(main.loop, Is.True, systems[index].name);
                    Assert.That(emission.enabled, Is.True, systems[index].name);
                    Assert.That(
                        emission.burstCount,
                        Is.Zero,
                        systems[index].name +
                        " must not repeat a finite ignition burst.");
                    Assert.That(
                        emission.rateOverTime.constant,
                        Is.GreaterThan(0f),
                        systems[index].name +
                        " must emit continuously while the barrel is lit.");
                    totalEmissionRate +=
                        emission.rateOverTime.constant;
                }
                Assert.That(
                    totalEmissionRate,
                    Is.EqualTo(410f).Within(0.001f),
                    "Continuous rates must preserve the source preset's dense burn.");

                Assert.That(
                    presenter.FireRenderer.transform.localPosition.y,
                    Is.InRange(0.15f, 0.35f),
                    "Flames must originate deep enough inside the barrel cavity.");
                Assert.That(
                    presenter.FireRenderer.transform.localScale.x,
                    Is.EqualTo(0.72f).Within(0.0001f),
                    "The source preset scale must be preserved for this barrel size.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PortableGrillFireStaysInsideBowlAndUsesLowFlameProfile()
        {
            var root = new GameObject("Portable grill fire test");
            var visualRoot = new GameObject("Reviewed grill presentation");
            visualRoot.transform.SetParent(root.transform, false);
            try
            {
                ItemDefinitionRecord definition =
                    LoadDefinition("item.portable-grill");
                var state = new ItemInstanceState
                {
                    definitionId = definition.DefinitionId,
                    content = 20f,
                    isEnabled = true,
                    scalarStates = new[]
                    {
                        new ItemScalarState
                        {
                            stateId = definition.Combustion.BurnTimeStateId,
                            value = 120f,
                        },
                    },
                };
                WorldItemInstance item =
                    root.AddComponent<WorldItemInstance>();
                SetPrivateField(item, "definition", definition);
                SetPrivateField(item, "state", state);

                GarbageBarrelFirePresenter presenter =
                    root.AddComponent<GarbageBarrelFirePresenter>();
                presenter.Configure(item, visualRoot);

                Assert.That(presenter.IsFireVisible, Is.True);
                Assert.That(
                    presenter.FireTransform.localPosition,
                    Is.EqualTo(new Vector3(-0.017f, 0f, 0.111f)));
                Assert.That(
                    Vector3.Angle(
                        presenter.FireTransform.localRotation * Vector3.up,
                        Vector3.forward),
                    Is.LessThan(0.001f));
                Assert.That(
                    presenter.FireTransform.localScale.x,
                    Is.EqualTo(0.1728f).Within(0.0001f));
                Assert.That(
                    presenter.FireLight.intensity,
                    Is.EqualTo(145f).Within(0.001f));
                Assert.That(
                    presenter.FireLight.range,
                    Is.EqualTo(1.65f).Within(0.001f));

                float totalEmission = root
                    .GetComponentsInChildren<ParticleSystem>(true)
                    .Sum(system => system.emission.rateOverTime.constant);
                Assert.That(
                    totalEmission,
                    Is.EqualTo(65.6f).Within(0.001f),
                    "The grill must use a restrained flame instead of the " +
                    "full garbage-barrel preset density.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ItemDefinitionRecord LoadDefinition(string definitionId)
        {
            ItemDefinitionCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalog>(
                    DefinitionCatalogPath);
            Assert.That(catalog, Is.Not.Null, DefinitionCatalogPath);
            Assert.That(catalog.TryGet(definitionId, out ItemDefinitionRecord value),
                Is.True,
                definitionId);
            return value;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivate(
            object target,
            string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }
    }
}
