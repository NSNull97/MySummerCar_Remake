using System;
using System.Collections.Generic;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaRemovalPolicyGeneratedTests
    {
        [TestCase("fl")]
        [TestCase("fr")]
        public void GeneratedFrontRemovalRulesKeepDonorPredicatesSeparateFromInstallation(
            string corner)
        {
            VehicleAssemblyController assembly = GeneratedAssembly();
            MountPointDefinition strut = Mount(assembly, "strut-" + corner);
            MountPointDefinition halfshaft = Mount(assembly, "halfshaft-" + corner);
            MountPointDefinition disc = Mount(assembly, "discbrake-" + corner);
            MountPointDefinition spindle = Mount(assembly, "spindle-" + corner);
            MountPointDefinition wheel = Mount(assembly, "wheel" + corner + "-new");

            Assert.That(strut.RemovalBlockedWhileOccupiedMountIds,
                Is.EqualTo(new[] { Id("steering-rod-" + corner) }));
            Assert.That(strut.RemovalBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(strut.RemovalIgnoredDependentMountIds, Is.Empty);

            Assert.That(halfshaft.RemovalBlockedWhileOccupiedMountIds, Is.Empty);
            Assert.That(halfshaft.RemovalBlockedWhileBoltedMountIds,
                Is.EqualTo(new[] { Id("discbrake-" + corner) }));
            Assert.That(halfshaft.RemovalIgnoredDependentMountIds, Is.Empty);
            Assert.That(halfshaft.InstallationBlockedWhileBoltedMountIds,
                Is.EqualTo(new[] { Id("discbrake-" + corner) }),
                "Package A's inverse installation predicate must remain intact.");

            Assert.That(disc.RemovalBlockedWhileOccupiedMountIds, Is.Empty);
            Assert.That(disc.RemovalBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(disc.RemovalIgnoredDependentMountIds,
                Is.EqualTo(new[] { Id("halfshaft-" + corner) }));
            Assert.That(wheel.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { Id("discbrake-" + corner) }),
                "Ignoring the halfshaft must not suppress the installed-wheel blocker.");

            Assert.That(spindle.RemovalBlockedWhileOccupiedMountIds, Is.Empty);
            Assert.That(spindle.RemovalBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(spindle.RemovalIgnoredDependentMountIds,
                Is.EqualTo(new[] { Id("discbrake-" + corner) }));
            Assert.That(assembly.Dependencies.Any(value =>
                    value.Kind == AssemblyDependencyKind.RemovalBlockedWhileInstalled &&
                    value.DependentPartDefinitionId == "vehicle.satsuma.part.spindle-" + corner &&
                    value.RelatedPartDefinitionId == "vehicle.satsuma.part.strut-" + corner),
                Is.True, "The explicit strut blocker must survive the narrow ignore rule.");
        }

        [TestCase("rl")]
        [TestCase("rr")]
        public void GeneratedRearRulesPreserveSpringAndArmPredicates(string corner)
        {
            VehicleAssemblyController assembly = GeneratedAssembly();
            MountPointDefinition arm = Mount(assembly, "trail-arm-" + corner);
            MountPointDefinition shock = Mount(assembly, "shock-" + corner);
            MountPointDefinition spring = Mount(assembly, "coilspring-" + corner);
            MountPointDefinition longSpring = Mount(assembly, "long-coilspring-" + corner);
            MountPointDefinition drum = Mount(assembly, "drum-brake-" + corner);

            Assert.That(arm.RequiredOccupiedMountIds, Is.Empty);
            Assert.That(arm.RequiredAnyOccupiedMountIds, Is.Empty);
            Assert.That(arm.BlockedWhileOccupiedMountIds,
                Is.EqualTo(new[] { Id("shock-" + corner) }),
                "An installed same-corner stock shock blocks rear-arm installation.");

            AssertSpringPolicy(spring, corner, "long-coilspring-");
            AssertSpringPolicy(longSpring, corner, "coilspring-");

            Assert.That(shock.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { Id("trail-arm-" + corner) }));
            Assert.That(drum.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { Id("trail-arm-" + corner) }));
            Assert.That(spring.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { Id("trail-arm-" + corner) }));
            Assert.That(longSpring.RequiredOccupiedMountIds,
                Is.EqualTo(new[] { Id("trail-arm-" + corner) }));
            Assert.That(arm.RemovalIgnoredDependentMountIds, Is.Empty,
                "The unresolved manual arm-with-spring outcome must not be changed in package C.");
        }

        [Test]
        public void ScopedRemovalRefreshIsIdempotentAndPreservesFrontPackageA()
        {
            Assert.That(Phase1SatsumaBaselineBuilder.BuilderVersion,
                Is.EqualTo("11A-V1d.66"));
            GameObject contents = PrefabUtility.LoadPrefabContents(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                VehicleAssemblyController assembly = contents
                    .GetComponent<VehicleAssemblyController>();
                Dictionary<string, string> before = assembly.MountPoints.ToDictionary(
                    value => value.MountId,
                    value => EditorJsonUtility.ToJson(value.Definition),
                    StringComparer.Ordinal);

                MountPointDefinition[] changed =
                    Phase1SatsumaBaselineBuilder.RefreshSuspensionRemovalRules(contents);
                Assert.That(changed, Is.Empty,
                    "The already refreshed V66 mount assets must be a fixed point.");

                MountPointDefinition[] frontChanged =
                    Phase1SatsumaBaselineBuilder.RefreshFrontInstallationRules(
                        contents, out int removedDependencies);
                Assert.That(frontChanged, Is.Empty,
                    "Reapplying package A must preserve the V66 removal fields.");
                Assert.That(removedDependencies, Is.Zero);

                foreach (MountPointAuthoring mount in assembly.MountPoints)
                {
                    Assert.That(EditorJsonUtility.ToJson(mount.Definition),
                        Is.EqualTo(before[mount.MountId]), mount.MountId);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void AssertSpringPolicy(
            MountPointDefinition definition,
            string corner,
            string oppositePrefix)
        {
            Assert.That(definition.RemovalBlockedWhileOccupiedMountIds,
                Is.EqualTo(new[] { Id("shock-" + corner) }));
            Assert.That(definition.RemovalBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(definition.RemovalIgnoredDependentMountIds, Is.Empty);
            Assert.That(definition.BlockedWhileOccupiedMountIds,
                Is.EqualTo(new[]
                {
                    Id(oppositePrefix + corner),
                    Id("shock-" + corner),
                }),
                "Spring installation must retain opposite-spring exclusion and add the shock gate.");
        }

        private static VehicleAssemblyController GeneratedAssembly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null,
                "Run the scoped V66 removal-rule refresh before this generated contract suite.");
            VehicleAssemblyController assembly = prefab
                .GetComponent<VehicleAssemblyController>();
            Assert.That(assembly, Is.Not.Null);
            return assembly;
        }

        private static MountPointDefinition Mount(
            VehicleAssemblyController assembly,
            string suffix)
        {
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == Id(suffix));
            Assert.That(mount.Definition, Is.Not.Null);
            return mount.Definition;
        }

        private static string Id(string suffix) => "mount.satsuma." + suffix;
    }
}
