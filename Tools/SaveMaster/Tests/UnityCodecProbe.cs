// Compiled only inside the isolated Unity codec verification project.
// This uses the remake's original, project-owned codec without modifying game Assets.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MSC.Save;
using UnityEditor;
using UnityEngine;

public static class SaveMasterUnityCodecProbe
{
    public static void Generate()
    {
        try
        {
            string output = Argument("-saveMasterOutput");
            Directory.CreateDirectory(output);
            var codec = new SaveDocumentCodec();
            var values = new List<double> { 0d, 42d, 0.1d, 0.000001d, 100000000000000000000d,
                123456.78901234567d, 0.0000000000000001d, double.Epsilon };
            var random = new System.Random(0x5A7E);
            for (int i = 0; i < 256; i++) values.Add(random.Next(0, 1000000) + random.NextDouble());
            for (int i = 0; i < values.Count; i++)
            {
                var document = Create(values[i]);
                string json = codec.Serialize(document, i % 2 == 0);
                codec.Deserialize(json, true);
                string prefix = "fixture-" + i.ToString("D3", CultureInfo.InvariantCulture);
                File.WriteAllText(Path.Combine(output, prefix + ".mscsave.json"), json, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(output, prefix + ".canonical.json"),
                    JsonUtility.ToJson(codec.Normalize(codec.Deserialize(json)).WithoutIntegrity(), false), new UTF8Encoding(false));
            }
            File.WriteAllText(Path.Combine(output, "generated.txt"),
                "Unity=" + Application.unityVersion + "\nCount=" + values.Count + "\n", new UTF8Encoding(false));
            Debug.Log("SAVE_MASTER_PROBE_GENERATED " + values.Count);
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void Verify()
    {
        try
        {
            string input = Argument("-saveMasterInput");
            string output = Argument("-saveMasterOutput");
            Directory.CreateDirectory(output);
            var codec = new SaveDocumentCodec();
            var report = new StringBuilder();
            int passed = 0, failed = 0;
            foreach (string path in Directory.GetFiles(input, "*.mscsave.json"))
            {
                try
                {
                    // Version 16 is also an explicitly supported Save Master editing
                    // target. Verification must preserve it rather than migrate it.
                    SaveDocument document = codec.Deserialize(File.ReadAllText(path), false);
                    codec.Deserialize(codec.Serialize(document), false);
                    report.AppendLine("PASS " + Path.GetFileName(path));
                    passed++;
                }
                catch (Exception exception)
                {
                    report.AppendLine("FAIL " + Path.GetFileName(path) + " " + exception);
                    failed++;
                }
            }
            report.AppendLine("Unity=" + Application.unityVersion + "; passed=" + passed + "; failed=" + failed);
            File.WriteAllText(Path.Combine(output, "unity-verification.txt"), report.ToString(), new UTF8Encoding(false));
            Debug.Log("SAVE_MASTER_PROBE_VERIFIED passed=" + passed + " failed=" + failed);
            EditorApplication.Exit(failed == 0 && passed > 0 ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    private static SaveDocument WithoutIntegrity(this SaveDocument document)
    {
        document.Header.IntegritySha256 = string.Empty;
        return document;
    }

    private static SaveDocument Create(double playTime)
    {
        return new SaveDocument
        {
            Header = new SaveHeader
            {
                SaveId = "save-master-synthetic-001", SlotId = "slot-probe", BuildId = "save-master-codec-probe",
                CreatedUtc = "2026-09-05T00:00:00.0000000+00:00", UpdatedUtc = "2026-09-05T00:00:00.0000000+00:00",
            },
            Metadata = new SaveMetadata
            {
                DisplayName = "Катя / Łódź \"quotes\" \\ slash\n\r\t\b\f \u0001 \u001f <>& 😀 \u2028 \u2029",
                PlayTimeSeconds = playTime, GameTimestamp = "Monday 12:00", LocationStableId = "location.home",
            },
            Domains = new[]
            {
                new SaveDomainEnvelope { DomainId = "probe.unknown", Required = false, SchemaVersion = 1,
                    PayloadJson = "{\"description\":\"Катя / Łódź <>& 😀 \\u2028 \\u2029\",\"escaped\":\"\\\\ \\\" \\n \\r \\t \\b \\f \\u0001\",\"value\":12,\"extra\":{\"untouched\":[1,true,null]}}" },
                new SaveDomainEnvelope { DomainId = "player.state", SchemaVersion = 1,
                    PayloadJson = "{\"schemaVersion\":1,\"configurationId\":\"player.first-person.v1\",\"worldPosition\":{\"x\":1.25,\"y\":2.5,\"z\":-3.75},\"worldRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1},\"motor\":{\"schemaVersion\":1,\"crouching\":false,\"posture\":0,\"verticalSpeedMetersPerSecond\":0},\"look\":{\"schemaVersion\":1,\"pitchDegrees\":0}}" },
                new SaveDomainEnvelope { DomainId = "player.needs", SchemaVersion = 3,
                    PayloadJson = "{\"schemaVersion\":3,\"thirst\":10,\"hunger\":20,\"stress\":0,\"urine\":0,\"fatigue\":0,\"dirtiness\":0,\"weightKilograms\":83,\"intoxication\":0,\"hangover\":0,\"pendingHungerEffect\":0,\"pendingThirstEffect\":0,\"pendingWeightEffect\":0,\"pendingIntoxicationEffect\":0,\"pendingUrineEffect\":0,\"pendingFatigueEffect\":0,\"pendingDirtinessEffect\":0}" },
            },
        };
    }

    private static string Argument(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
        throw new ArgumentException("Missing argument " + name);
    }
}
