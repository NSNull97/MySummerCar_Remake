using MSC.LegacyImport.Editor.Ledger;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class PortingLedgerUpdaterTests
    {
        [Test]
        public void Upsert_IsIdempotentAndEscapesCsvFields()
        {
            var entry = new PortingLedgerEntry(
                "mysummercar_Data/sharedassets0.assets",
                "Object, with comma",
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "Mesh",
                "ReferenceOnly",
                "Assets/Game/LegacyImport/ReferenceOnly/Proof/object.fbx",
                "ImportedReference",
                "sharedassets0.resource",
                "SyntheticTestImporter 1.0.0",
                "Reference only",
                "Quoted \"note\"");

            PortingLedgerUpdatePlan first = PortingLedgerUpdater.CreatePlan(string.Empty, new[] { entry });
            PortingLedgerUpdatePlan second = PortingLedgerUpdater.CreatePlan(first.UpdatedCsv, new[] { entry });

            Assert.That(first.AddedCount, Is.EqualTo(1));
            Assert.That(first.UpdatedCount, Is.Zero);
            Assert.That(first.UpdatedCsv, Does.Contain("\"Object, with comma\""));
            Assert.That(first.UpdatedCsv, Does.Contain("\"Quoted \"\"note\"\"\""));
            Assert.That(second.HasChanges, Is.False);
            Assert.That(second.UpdatedCsv, Is.EqualTo(first.UpdatedCsv));
        }
    }
}
