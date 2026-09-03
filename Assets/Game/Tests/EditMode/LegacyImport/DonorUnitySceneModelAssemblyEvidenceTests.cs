using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class DonorUnitySceneModelAssemblyEvidenceTests
    {
        [Test]
        public void ParserKeepsAssemblyFsmBodyWithoutInstantiatingDonorCode()
        {
            const string yaml = @"%YAML 1.1
--- !u!1 &1
GameObject:
  m_Name: Root
  m_IsActive: 1
--- !u!4 &10
Transform:
  m_GameObject: {fileID: 1}
  m_Father: {fileID: 0}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalScale: {x: 1, y: 1, z: 1}
--- !u!114 &20
MonoBehaviour:
  m_GameObject: {fileID: 1}
  m_Script: {fileID: 1, guid: dddddddddddddddddddddddddddddddd, type: 3}
  fsm:
    name: Assembly
    variables:
      stringVariables:
      - name: HandChild
        value: drum brake(Clone)
";
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, yaml);
                DonorUnitySceneModel model = DonorUnitySceneModel.Parse(path);
                DonorMonoBehaviourRecord record = model.MonoBehaviours.Single();

                Assert.That(record.ComponentId, Is.EqualTo(20));
                Assert.That(record.GameObjectId, Is.EqualTo(1));
                Assert.That(
                    record.ScriptGuid,
                    Is.EqualTo("dddddddddddddddddddddddddddddddd"));
                Assert.That(record.SerializedBody, Does.Contain("name: Assembly"));
                Assert.That(model.GetMonoBehaviours(1).Count, Is.EqualTo(1));
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
