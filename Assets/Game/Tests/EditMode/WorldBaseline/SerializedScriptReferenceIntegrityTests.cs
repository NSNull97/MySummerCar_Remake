using MSC.Editor.WorldBaseline;
using MSC.LegacyImport;
using NUnit.Framework;
using UnityEditor;

namespace MSC.Tests.EditMode.WorldBaseline
{
    [Category("LocalDonorBaseline")]
    public sealed class SerializedScriptReferenceIntegrityTests
    {
        [Test]
        public void SupplementalMetadata_HasMatchingPermanentMonoScript()
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                ProjectSerializedScriptReferenceBuildGuard
                    .SupplementalScriptPath);

            Assert.That(script, Is.Not.Null);
            Assert.That(
                script.GetClass(),
                Is.EqualTo(typeof(DonorWorldSupplementalEntityMetadata)));
            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    ProjectSerializedScriptReferenceBuildGuard
                        .SupplementalScriptPath),
                Is.Not.Empty);
        }

        [Test]
        public void ProjectAuthoredYaml_UsesPermanentScriptReferences()
        {
            Assert.DoesNotThrow(
                ProjectSerializedScriptReferenceBuildGuard
                    .ValidateProjectAuthoredReferences);
        }

        [Test]
        public void ScriptReferenceGate_RejectsMissingLocalAndUnknownScripts()
        {
            string validGuid = AssetDatabase.AssetPathToGUID(
                ProjectSerializedScriptReferenceBuildGuard
                    .SupplementalScriptPath);

            Assert.That(
                ProjectSerializedScriptReferenceBuildGuard
                    .IsResolvablePermanentScriptReference(
                        "m_Script: {fileID: 11500000, guid: " +
                        validGuid + ", type: 3}"),
                Is.True);
            Assert.That(
                ProjectSerializedScriptReferenceBuildGuard
                    .IsResolvablePermanentScriptReference(
                        "m_Script: {fileID: 0}"),
                Is.False);
            Assert.That(
                ProjectSerializedScriptReferenceBuildGuard
                    .IsResolvablePermanentScriptReference(
                        "m_Script: {fileID: 172497845}"),
                Is.False);
            Assert.That(
                ProjectSerializedScriptReferenceBuildGuard
                    .IsResolvablePermanentScriptReference(
                        "m_Script: {fileID: 11500000, guid: " +
                        "00000000000000000000000000000000, type: 3}"),
                Is.False);
        }
    }
}
