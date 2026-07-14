using MSC.Editor.Validation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.Validation
{
    public sealed class AssemblyBoundaryTests
    {
        [Test]
        public void RuntimeAssemblies_DoNotReferenceEditorAssemblies()
        {
            var issues = AssemblyDefinitionValidator.ValidateRuntimeToEditorReferences();

            Assert.That(issues, Is.Empty);
        }
    }
}
