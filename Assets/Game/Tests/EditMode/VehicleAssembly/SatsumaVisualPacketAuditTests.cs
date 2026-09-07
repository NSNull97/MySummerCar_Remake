using System;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaVisualPacketAuditTests
    {
        [Test]
        public void RecordInstalledPresentationAndZeroPedalRpmWithoutChangingAssetsOrSaves()
        {
            using var f = new Fixture();
            var report = new StringBuilder();
            foreach (PartInstance part in f.Assembly.Parts)
            {
                string id = part.Definition.DefinitionId;
                if (!(id.Contains("radiator") || id.Contains("fuel-pump") || id.Contains("water-pump") ||
                    id.Contains("dashboard") || id.Contains("gauge") || id.Contains("exhaust"))) continue;
                report.AppendLine("PART " + id + " installed=" + part.IsInstalled + " pos=" +
                    f.Root.transform.InverseTransformPoint(part.transform.position).ToString("F6"));
                foreach (Transform t in part.GetComponentsInChildren<Transform>(true))
                {
                    var renderer = t.GetComponent<Renderer>();
                    var mesh = t.GetComponent<MeshFilter>();
                    report.AppendLine("  " + t.name + " parent=" + t.parent?.name + " local=" + t.localPosition.ToString("F6") +
                        " rot=" + t.localEulerAngles.ToString("F3") + " scale=" + t.localScale.ToString("F5") +
                        " active=" + t.gameObject.activeSelf + " mesh=" + (mesh == null ? "" : AssetDatabase.GetAssetPath(mesh.sharedMesh)) +
                        " materials=" + (renderer == null ? "" : string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath))));
                }
            }
            Directory.CreateDirectory("Logs/visual-packet-20260907");
            File.WriteAllText("Logs/visual-packet-20260907/installed-presentation.txt", report.ToString());
            report.Clear().AppendLine("seconds,rpm,throttle,status,afr,temperature");
            var input = new VehicleInputState(0f, 1f, 0f, 0f, true, true, true, 0);
            for (int i = 0; i < 1000 && f.Host.State.EngineStatus != VehicleEngineStatus.Running; i++) f.Host.Root.Tick(.02f, input);
            Assert.That(f.Host.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running), f.Describe());
            float min = float.MaxValue, max = 0;
            for (int i = 0; i < 3000; i++)
            {
                f.Host.Root.Tick(.02f, VehicleInputState.Neutral(true));
                float rpm = f.Host.State.EngineRpm;
                if (i > 500) { min = Mathf.Min(min, rpm); max = Mathf.Max(max, rpm); }
                if (i % 5 == 0) report.AppendLine(FormattableString.Invariant($"{i * .02f:F2},{rpm:F3},0,{f.Host.State.EngineStatus},{f.Host.Root.SatsumaOperatingModel.LastPoint.AirFuelRatio:F3},{f.Host.State.EngineTemperatureCelsius:F3}"));
            }
            File.WriteAllText("Logs/visual-packet-20260907/zero-pedal-rpm.csv", report.ToString());
            Debug.Log("SATSUMA_VISUAL_PACKET_BASELINE idleMin=" + min + " idleMax=" + max);
        }
    }
}
