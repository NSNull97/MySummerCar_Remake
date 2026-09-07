using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

internal static class Program
{
    private static readonly string Scratch = Path.Combine(Path.GetTempPath(), "SaveMaster.Tests", Guid.NewGuid().ToString("N"));
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures");
    private static int passed;
    private static int failed;

    private static int Main(string[] args)
    {
        Directory.CreateDirectory(Scratch);
        try
        {
            if (args.Length == 3 && args[0] == "--roundtrip-unity") return RoundTripUnity(args[1], args[2]);
            if (args.Length == 3 && args[0] == "--roundtrip-native") return RoundTripNative(args[1], args[2]);
            if (args.Length == 2 && args[0] == "--inspect-directory") return InspectDirectory(args[1]);
            if (args.Length > 0) throw new ArgumentException("Usage: no arguments, --roundtrip-unity input output, --roundtrip-native input output, or --inspect-directory directory.");
            Run("Unity-generated integrity fixtures are independently signed", FixturesHaveExpectedHashes);
            Run("All committed Unity fixtures open with intact data", FixturesOpen);
            Run("Payload tampering is rejected before editing", TamperRejected);
            Run("Malformed envelope and trailing data are rejected", MalformedRejected);
            Run("Duplicate envelope properties are rejected", DuplicateEnvelopeRejected);
            Run("Duplicate payload properties are rejected even with valid signature", DuplicatePayloadRejected);
            Run("Duplicate domain IDs are rejected", DuplicateDomainRejected);
            Run("Unknown envelope fields are refused without silent loss", UnknownEnvelopeRejected);
            Run("Invalid UTF-8 is rejected", InvalidUtf8Rejected);
            Run("Payload and document size limits are enforced", OversizedRejected);
            Run("Unsupported future and old document versions stay read-only", UnsupportedVersionReadOnly);
            Run("Document version 16 preserves its version without migration", Version16Retained);
            Run("Document version 18 edits and exports without changing its version", Version18Retained);
            Run("Version 18 preserves dynamic assembly and unknown data through player edits", Version18DynamicPreserved);
            Run("Version 18 edits dynamic assembly with undo, export and reopen", Version18DynamicEdit);
            Run("Version 18 refuses conflicting dynamic item ownership without writes", Version18DynamicOwnership);
            Run("Newer domain schemas are protected and preserved", UnsupportedDomainReadOnly);
            Run("No-op and undo restore original data without writes", NoOpAndUndo);
            Run("Redo restores edits and a new edit clears redo", Redo);
            Run("Detached domain snapshots cannot silently mutate a session", SnapshotIsolation);
            Run("Typed scalar edits reject malformed values and structural fields", InvalidScalarRejected);
            Run("Floating point fields serialized as zero accept fractional edits", ZeroFloatEdit);
            Run("Finite doubles that overflow Unity float coordinates are rejected", UnityFloatOverflow);
            Run("Unknown nested schema is read-only before mutation", NestedSchemaReadOnly);
            Run("Out-of-range needs, motor and look cannot be saved", SemanticValidation);
            Run("Zero quaternion cannot be saved", QuaternionValidation);
            Run("Advanced JSON cannot remove fields or change scalar types", InvalidStructureRejected);
            Run("Unknown fields inside edited payload and unknown domains survive", UnknownDataRetained);
            Run("An existing unknown double field survives without float coercion", UnknownDoublePreserved);
            Run("Save makes a byte-exact backup and a readable new checksum", AtomicSaveBackup);
            Run("Save refuses a concurrently changed source without overwriting it", ExternalModificationConflict);
            Run("Save refuses a source replaced while editor was open", ExternalReplacementConflict);
            Run("Save refuses a deleted source without recreating it", DeletedSourceConflict);
            Run("Save-as never overwrites an existing destination", SaveAsCollision);
            Run("Save-as creates an independent readable copy", SaveAsCopy);
            Run("Failed validation leaves source byte-identical", FailedSaveDoesNotTouchSource);
            Run("Balance action appends a coherent transaction with undo and save", BalanceAction);
            Run("No-op balance action does not append a transaction", BalanceNoOp);
            Run("Money ledger rejects inconsistent debit, order and balance", MoneyLedgerValidation);
            Run("Money ledger rejects unknown transaction kind enums", MoneyKindValidation);
            Run("Int64 money overflow is rejected without double rounding", MoneyInt64Overflow);
            Run("Maximum Int64 money remains exact through debit, undo and save", MoneyInt64Maximum);
            Run("Time action preserves epoch across a leap day with undo", TimeAction);
            Run("Time cannot be moved before the beginning of a world", TimeBeforeWorld);
            Run("Raw date or tick changes that shift the world epoch are blocked", InconsistentTime);
            Run("Needs reset clears delayed effects but retains body weight", NeedsReset);
            Run("Added wires appear individually in change review and undo", WireChangeReview);
            VehicleRegression.Register(Run);
            VehicleTuningRegression.Register(Run);
            Console.WriteLine($"RESULT passed={passed} failed={failed}");
            return failed == 0 ? 0 : 1;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
        finally
        {
            string expectedPrefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SaveMaster.Tests")) + Path.DirectorySeparatorChar;
            if (Path.GetFullPath(Scratch).StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Scratch))
                Directory.Delete(Scratch, true);
        }
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception exception) { failed++; Console.WriteLine("FAIL " + name + "\n" + exception); }
    }

    private static string CopyFixture(string? json = null)
    {
        string path = Path.Combine(Scratch, Guid.NewGuid().ToString("N") + ".mscsave.json");
        if (json == null) File.Copy(Path.Combine(Fixtures, "fixture-000.mscsave.json"), path);
        else File.WriteAllText(path, json, new UTF8Encoding(false));
        return path;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws(Action action, string reason)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or IOException or ArgumentException or NotSupportedException) { return; }
        throw new InvalidOperationException("Expected rejection: " + reason);
    }

    private static string CanonicalFixture() => File.ReadAllText(Path.Combine(Fixtures, "fixture-000.canonical.json"));

    private static string Sign(string canonical)
    {
        const string marker = "\"IntegritySha256\":\"\"";
        Assert(canonical.Contains(marker), "Fixture signing requires an empty integrity field.");
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return canonical.Replace(marker, "\"IntegritySha256\":\"" + hash + "\"", StringComparison.Ordinal) + "\n";
    }

    private static string MutatePayload(string canonical, string domainId, Func<string, string> mutation)
    {
        using JsonDocument parsed = JsonDocument.Parse(canonical);
        JsonElement domain = parsed.RootElement.GetProperty("Domains").EnumerateArray().Single(d => d.GetProperty("DomainId").GetString() == domainId);
        JsonElement payload = domain.GetProperty("PayloadJson");
        // All test mutations here target ASCII-only project DTOs. JSON serialization
        // is independent of the production Save Master writer; Unity created the envelope.
        string changed = JsonSerializer.Serialize(mutation(payload.GetString()!));
        return canonical.Replace(payload.GetRawText(), changed, StringComparison.Ordinal);
    }

    private static string AddDomain(string domainId, int schemaVersion, JsonNode payload, int documentVersion = 17) =>
        AddDomains(documentVersion, (domainId, schemaVersion, payload));

    private static string AddDomains(int documentVersion, params (string Id, int Schema, JsonNode Payload)[] additions)
    {
        using JsonDocument source = JsonDocument.Parse(CanonicalFixture());
        var domains = source.RootElement.GetProperty("Domains").EnumerateArray()
            .Select(d => (Id: d.GetProperty("DomainId").GetString()!, Json: d.GetRawText())).ToList();
        foreach (var addition in additions)
            domains.Add((addition.Id, JsonSerializer.Serialize(new { DomainId = addition.Id, SchemaVersion = addition.Schema, Required = true, PayloadJson = addition.Payload.ToJsonString() })));
        string header = source.RootElement.GetProperty("Header").GetRawText().Replace("\"DocumentVersion\":17", "\"DocumentVersion\":" + documentVersion, StringComparison.Ordinal);
        string canonical = "{\"Header\":" + header + ",\"Metadata\":" + source.RootElement.GetProperty("Metadata").GetRawText() +
            ",\"Domains\":[" + string.Join(",", domains.OrderBy(d => d.Id, StringComparer.Ordinal).Select(d => d.Json)) + "]}";
        return Sign(canonical);
    }

    private static JsonNode Payload(SaveSession session, string id) => session.Domains.Single(d => d.Id == id).Payload;
    private static double X(SaveSession session) => Payload(session, "player.state")["worldPosition"]!["x"]!.GetValue<double>();
    private static void EditX(SaveSession session, string value = "5.5") => session.SetValue("player.state", "/worldPosition/x", value);
    private static string RawPayload(string path, string id)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        return document.RootElement.GetProperty("Domains").EnumerateArray().Single(d => d.GetProperty("DomainId").GetString() == id).GetProperty("PayloadJson").GetString()!;
    }

    private static void FixturesHaveExpectedHashes()
    {
        foreach (string fixture in Directory.GetFiles(Fixtures, "*.mscsave.json"))
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(fixture));
            string canonical = File.ReadAllText(fixture.Replace(".mscsave.json", ".canonical.json", StringComparison.Ordinal));
            string actual = doc.RootElement.GetProperty("Header").GetProperty("IntegritySha256").GetString()!;
            Assert(actual == Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))), "Fixture hash mismatch: " + fixture);
        }
    }

    private static void FixturesOpen()
    {
        foreach (string path in Directory.GetFiles(Fixtures, "*.mscsave.json"))
        {
            SaveSession session = SaveSession.Open(path);
            Assert(session.CanEdit && !session.IsDirty && session.DocumentVersion == 17, "Current Unity fixture not editable: " + path);
            Assert(X(session) == 1.25, "Player position changed while reading.");
            Assert(!session.Validate().Any(i => i.Severity == ValidationSeverity.Error), "Valid synthetic DTO rejected.");
        }
    }

    private static void TamperRejected() => Throws(() => SaveSession.Open(CopyFixture(Sign(CanonicalFixture()).Replace("Monday 12:00", "Monday 13:00", StringComparison.Ordinal))), "SHA tampering");
    private static void MalformedRejected()
    {
        foreach (string json in new[] { "", "{", "[]", "null", Sign(CanonicalFixture()) + "{}", "{\"Header\":NaN}" })
            Throws(() => SaveSession.Open(CopyFixture(json)), "malformed JSON");
    }
    private static void DuplicateEnvelopeRejected() => Throws(() => SaveSession.Open(CopyFixture(Sign(CanonicalFixture()).Replace("\"DocumentVersion\":17", "\"DocumentVersion\":17,\"DocumentVersion\":17", StringComparison.Ordinal))), "duplicate header field");
    private static void DuplicatePayloadRejected() => Throws(() => SaveSession.Open(CopyFixture(Sign(MutatePayload(CanonicalFixture(), "player.needs", p => p.Replace("\"thirst\":10", "\"thirst\":10,\"thirst\":11", StringComparison.Ordinal))))), "duplicate payload field");
    private static void DuplicateDomainRejected()
    {
        string canonical = CanonicalFixture();
        using var parsed = JsonDocument.Parse(canonical);
        string domain = parsed.RootElement.GetProperty("Domains")[0].GetRawText();
        canonical = canonical.Insert(canonical.LastIndexOf(']'), "," + domain);
        Throws(() => SaveSession.Open(CopyFixture(Sign(canonical))), "duplicate domain");
    }
    private static void UnknownEnvelopeRejected() => Throws(() => SaveSession.Open(CopyFixture(Sign(CanonicalFixture()).TrimEnd().Insert(1, "\"FutureEnvelope\":123,"))), "unknown envelope field");
    private static void InvalidUtf8Rejected()
    {
        string path = CopyFixture();
        File.WriteAllBytes(path, [0x7b, 0x22, 0xc3, 0x28, 0x22, 0x7d]);
        Throws(() => SaveSession.Open(path), "invalid UTF-8");
    }
    private static void OversizedRejected()
    {
        string path = CopyFixture();
        File.WriteAllBytes(path, new byte[SaveMasterLimits.MaximumDocumentBytes + 1]);
        Throws(() => SaveSession.Open(path), "document exceeds limit");
        string big = "{\"value\":\"" + new string('x', SaveMasterLimits.MaximumDomainPayloadBytes) + "\"}";
        Throws(() => SaveSession.Open(CopyFixture(Sign(MutatePayload(CanonicalFixture(), "player.needs", _ => big)))), "payload exceeds limit");
    }
    private static void UnsupportedVersionReadOnly()
    {
        foreach (int version in new[] { 1, 15, 19, 99 })
        {
            var session = SaveSession.Open(CopyFixture(Sign(CanonicalFixture().Replace("\"DocumentVersion\":17", "\"DocumentVersion\":" + version, StringComparison.Ordinal))));
            Assert(!session.CanEdit, "Unsupported version editable: " + version);
            Throws(() => EditX(session), "read-only edit");
            Throws(() => session.SaveAs(Path.Combine(Scratch, "unsupported-" + version + ".mscsave.json")), "read-only write");
        }
    }
    private static void Version16Retained()
    {
        string path = CopyFixture(Sign(CanonicalFixture().Replace("\"DocumentVersion\":17", "\"DocumentVersion\":16", StringComparison.Ordinal)));
        var session = SaveSession.Open(path); Assert(session.CanEdit, "Supported v16 is not editable.");
        EditX(session); session.Save();
        Assert(SaveSession.Open(path).DocumentVersion == 16, "Editor silently migrated a v16 document.");
    }
    private static void Version18Retained()
    {
        string path = CopyFixture(Sign(CanonicalFixture().Replace("\"DocumentVersion\":17", "\"DocumentVersion\":18", StringComparison.Ordinal)));
        byte[] original = File.ReadAllBytes(path);
        var session = SaveSession.Open(path);
        Assert(session.CanEdit && session.DocumentVersion == 18 && session.CanEditDomain("player.state"), "Supported v18 is not editable.");
        EditX(session); session.Undo();
        Assert(!session.IsDirty && File.ReadAllBytes(path).SequenceEqual(original), "V18 undo modified disk or retained an edit.");
        session.Redo();
        string export = Path.Combine(Scratch, "version18-export.mscsave.json");
        session.SaveAs(export);
        Assert(File.ReadAllBytes(path).SequenceEqual(original), "V18 export modified source.");
        SaveSession reopened = SaveSession.Open(export);
        Assert(reopened.DocumentVersion == 18 && X(reopened) == 5.5, "V18 export lost version or edit.");
        EditX(reopened, "8.25"); reopened.Save();
        Assert(SaveSession.Open(export).DocumentVersion == 18 && X(SaveSession.Open(export)) == 8.25, "V18 in-place save lost version or edit.");
    }
    private static string DynamicDocument(JsonNode vehicle, JsonNode? items = null, JsonNode? world = null) =>
        AddDomains(18, ("vehicle.satsuma", 1, vehicle), ("items.instances", 1, items ?? VehicleRegression.DynamicItems(vehicle)),
            ("world.entities", 2, world ?? new JsonObject { ["schemaVersion"] = 2, ["entities"] = new JsonArray() }));

    private static void Version18DynamicPreserved()
    {
        foreach (bool installed in new[] { false, true })
        {
            JsonObject vehicle = VehicleRegression.DynamicFixture(installed);
            string path = CopyFixture(DynamicDocument(vehicle));
            string before = RawPayload(path, "vehicle.satsuma"), unknown = RawPayload(path, "probe.unknown");
            var session = SaveSession.Open(path);
            Assert(session.CanEditDomain("vehicle.satsuma"), "Assembly schema3 is still read-only.");
            ValidationIssue[] errors = session.Validate().Where(i => i.Severity == ValidationSeverity.Error).ToArray();
            Assert(errors.Length == 0, "Valid dynamic owner document rejected: " + string.Join("; ", errors.Select(i => i.DomainId + i.JsonPointer + ": " + i.Message)));
            EditX(session); session.Save();
            Assert(RawPayload(path, "vehicle.satsuma") == before, "Unedited dynamic assembly was rewritten.");
            Assert(RawPayload(path, "probe.unknown") == unknown, "Unknown v18 domain was rewritten.");
            Assert(SaveSession.Open(path).DocumentVersion == 18, "Schema3 save altered document version.");
        }
    }
    private static void Version18DynamicEdit()
    {
        string path = CopyFixture(DynamicDocument(VehicleRegression.DynamicFixture(true)));
        byte[] original = File.ReadAllBytes(path);
        var session = SaveSession.Open(path);
        JsonNode before = Payload(session, "vehicle.satsuma");
        JsonNode changed = SchemaActions.SetFasteners(before, 0, true);
        session.SetJson("vehicle.satsuma", "", changed.ToJsonString());
        session.Undo(); Assert(!session.IsDirty, "V18 assembly undo failed.");
        session.Redo();
        const string velocity = "/vehicles/0/assembly/dynamicParts/0/linearVelocity/x";
        session.SetValue("vehicle.satsuma", velocity, "1.5");
        string target = Path.Combine(Scratch, "dynamic18-export.mscsave.json");
        session.SaveAs(target);
        Assert(File.ReadAllBytes(path).SequenceEqual(original), "Dynamic export changed the source.");
        var reopened = SaveSession.Open(target);
        JsonNode actual = Payload(reopened, "vehicle.satsuma");
        JsonNode expected = changed.DeepClone();
        expected["vehicles"]![0]!["assembly"]!["dynamicParts"]![0]!["linearVelocity"]!["x"] = 1.5;
        Assert(JsonNode.DeepEquals(actual, expected), "Dynamic pose, definition, sleeping, unknown fields or action result did not round-trip.");
        Assert(reopened.DocumentVersion == 18 && !reopened.Validate().Any(i => i.Severity == ValidationSeverity.Error), "Reopened dynamic export is invalid.");
    }
    private static void Version18DynamicOwnership()
    {
        foreach (string defect in new[] { "missing-item", "consumed", "canonical", "mismatch", "world-owner", "missing-descriptor" })
        {
            JsonObject vehicle = VehicleRegression.DynamicFixture(false);
            JsonObject items = VehicleRegression.DynamicItems(vehicle);
            JsonObject world = new() { ["schemaVersion"] = 2, ["entities"] = new JsonArray() };
            JsonNode state = items["instances"]![0]!["state"]!;
            switch (defect)
            {
                case "missing-item": items["instances"]!.AsArray().Clear(); break;
                case "consumed": state["isConsumed"] = true; break;
                case "canonical": items["instances"]![0]!["isCanonicalPlacement"] = true; break;
                case "mismatch": state["definitionId"] = "item.oil-filter"; break;
                case "world-owner": world["entities"]!.AsArray().Add(new JsonObject { ["stableEntityId"] = state["stableEntityId"]!.DeepClone() }); break;
                case "missing-descriptor": vehicle["vehicles"]![0]!["assembly"]!["dynamicParts"]!.AsArray().Clear(); break;
            }
            string path = CopyFixture(DynamicDocument(vehicle, items, world));
            byte[] original = File.ReadAllBytes(path);
            var session = SaveSession.Open(path);
            Assert(session.Validate().Any(i => i.Severity == ValidationSeverity.Error), "Invalid cross-domain ownership accepted: " + defect);
            EditX(session); Throws(() => session.Save(), "dynamic owner " + defect);
            Assert(File.ReadAllBytes(path).SequenceEqual(original), "Ownership rejection changed source: " + defect);
        }
    }
    private static void UnsupportedDomainReadOnly()
    {
        string canonical = CanonicalFixture().Replace("\"DomainId\":\"player.needs\",\"SchemaVersion\":3", "\"DomainId\":\"player.needs\",\"SchemaVersion\":99", StringComparison.Ordinal);
        string path = CopyFixture(Sign(canonical));
        var session = SaveSession.Open(path);
        Throws(() => session.SetValue("player.needs", "/thirst", "20"), "unknown domain schema edit");
        Assert(!session.IsDirty, "Unknown schema edit changed the session.");
        string original = RawPayload(path, "player.needs");
        EditX(session); session.Save();
        Assert(RawPayload(path, "player.needs") == original, "Unknown schema payload was not retained verbatim.");
    }
    private static void NoOpAndUndo()
    {
        string path = CopyFixture(); byte[] original = File.ReadAllBytes(path);
        var session = SaveSession.Open(path);
        EditX(session, "1.25");
        Assert(!session.IsDirty && !session.CanUndo, "Equivalent scalar created a change.");
        EditX(session); Assert(session.IsDirty && session.CanUndo, "Edit did not enter history.");
        session.Undo();
        Assert(!session.IsDirty && X(session) == 1.25 && session.CanRedo, "Undo did not restore original.");
        try { session.Save(); } catch (InvalidOperationException) { }
        Assert(File.ReadAllBytes(path).SequenceEqual(original), "No-op save rewrote the original.");
    }
    private static void Redo()
    {
        var session = SaveSession.Open(CopyFixture());
        EditX(session); session.Undo(); session.Redo();
        Assert(X(session) == 5.5 && session.IsDirty, "Redo lost the edit.");
        session.Undo(); EditX(session, "8.25");
        Assert(!session.CanRedo && X(session) == 8.25, "A new branch retained stale redo.");
    }
    private static void SnapshotIsolation()
    {
        var session = SaveSession.Open(CopyFixture());
        Payload(session, "player.state")["worldPosition"]!["x"] = 999;
        Assert(!session.IsDirty && X(session) == 1.25, "Public payload snapshot mutated the session.");
    }
    private static void InvalidScalarRejected()
    {
        var session = SaveSession.Open(CopyFixture());
        foreach (string value in new[] { "NaN", "Infinity", "abc", "1e9999", "{}", "true" })
            Throws(() => EditX(session, value), "invalid floating-point value " + value);
        Throws(() => session.SetValue("player.state", "/motor/crouching", "perhaps"), "invalid boolean");
        Throws(() => session.SetValue("player.state", "/schemaVersion", "2"), "protected schema");
        Throws(() => session.SetValue("player.state", "/does-not-exist", "1"), "new scalar field");
        Assert(!session.IsDirty, "Rejected edits changed session state.");
    }
    private static void ZeroFloatEdit()
    {
        var session = SaveSession.Open(CopyFixture());
        session.SetValue("player.needs", "/stress", "0.5");
        session.SetValue("player.state", "/look/pitchDegrees", "0.5");
        Assert(!session.Validate().Any(i => i.Severity == ValidationSeverity.Error), "Valid fractional value rejected because original was zero.");
        session.Save();
        Assert(Payload(SaveSession.Open(session.FilePath), "player.needs")["stress"]!.GetValue<double>() == 0.5, "Fractional need lost on save.");
    }
    private static void UnityFloatOverflow()
    {
        string file = CopyFixture(); byte[] before = File.ReadAllBytes(file);
        var session = SaveSession.Open(file);
        foreach (string text in new[] { "1e300", "-1e300", "3.5e38" })
        {
            Throws(() => EditX(session, text), "coordinate cannot fit native Unity float: " + text);
            Assert(!session.IsDirty, "Overflowed coordinate changed session.");
        }
        Throws(() => session.SetJson("player.state", "/worldPosition", "{\"x\":1e300,\"y\":2.5,\"z\":-3.75}"), "JSON editor bypass of Unity float range");
        Assert(File.ReadAllBytes(file).SequenceEqual(before), "Coordinate overflow changed original bytes.");
    }
    private static void NestedSchemaReadOnly()
    {
        string canonical = MutatePayload(CanonicalFixture(), "player.state", p => p.Replace("\"look\":{\"schemaVersion\":1", "\"look\":{\"schemaVersion\":99", StringComparison.Ordinal));
        var session = SaveSession.Open(CopyFixture(Sign(canonical)));
        Throws(() => EditX(session), "unknown nested look schema");
        Assert(!session.IsDirty, "Unsupported nested schema edit dirtied session.");
    }
    private static void RejectUnsafeEdit(string domain, string pointer, string value)
    {
        string path = CopyFixture(); byte[] original = File.ReadAllBytes(path);
        var session = SaveSession.Open(path);
        bool rejected = false;
        try { session.SetValue(domain, pointer, value); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or ArgumentException) { rejected = true; }
        if (!rejected)
        {
            Assert(session.Validate().Any(i => i.Severity == ValidationSeverity.Error), "No validation error for " + pointer);
            Throws(() => session.Save(), "invalid values must never reach disk");
        }
        Assert(File.ReadAllBytes(path).SequenceEqual(original), "Rejected edit changed original bytes.");
    }
    private static void SemanticValidation()
    {
        RejectUnsafeEdit("player.needs", "/thirst", "101");
        RejectUnsafeEdit("player.needs", "/hunger", "-1");
        RejectUnsafeEdit("player.needs", "/weightKilograms", "0");
        RejectUnsafeEdit("player.needs", "/pendingIntoxicationEffect", "1001");
        RejectUnsafeEdit("player.state", "/look/pitchDegrees", "90");
        RejectUnsafeEdit("player.state", "/motor/verticalSpeedMetersPerSecond", "201");
    }
    private static void QuaternionValidation() => RejectUnsafeEdit("player.state", "/worldRotation/w", "0");
    private static void InvalidStructureRejected()
    {
        var session = SaveSession.Open(CopyFixture());
        Throws(() => session.SetJson("player.state", "", "{}"), "dropping payload fields");
        Throws(() => session.SetJson("player.state", "/worldPosition", "{\"x\":\"oops\",\"y\":2.5,\"z\":-3.75}"), "numeric type replacement");
    }
    private static void UnknownDataRetained()
    {
        string canonical = MutatePayload(CanonicalFixture(), "player.state", p => p.Insert(1, "\"futureField\":{\"valuable\":[1,true,null,\"keep me\"]},"));
        string path = CopyFixture(Sign(canonical));
        string unknownBefore = RawPayload(path, "probe.unknown");
        var session = SaveSession.Open(path); JsonNode originalFuture = Payload(session, "player.state")["futureField"]!.DeepClone();
        EditX(session); session.Save();
        var restored = SaveSession.Open(path);
        Assert(JsonNode.DeepEquals(originalFuture, Payload(restored, "player.state")["futureField"]), "Unknown nested field changed.");
        Assert(RawPayload(path, "probe.unknown") == unknownBefore, "Untouched unknown domain bytes changed.");
    }
    private static void UnknownDoublePreserved()
    {
        string canonical = MutatePayload(CanonicalFixture(), "player.state", p => p.Insert(1, "\"futureUnknownDouble\":1e300,"));
        var session = SaveSession.Open(CopyFixture(Sign(canonical)));
        EditX(session); session.Save();
        JsonNode value = Payload(SaveSession.Open(session.FilePath), "player.state")["futureUnknownDouble"]!;
        Assert(value.GetValue<double>() == 1e300, "Untouched unknown double was narrowed to float or rejected.");
    }
    private static void AtomicSaveBackup()
    {
        string path = CopyFixture(); byte[] original = File.ReadAllBytes(path);
        var session = SaveSession.Open(path); EditX(session); SaveWriteResult result = session.Save();
        Assert(result.BackupPath != null && File.Exists(result.BackupPath), "Original backup is missing.");
        Assert(File.ReadAllBytes(result.BackupPath!).SequenceEqual(original), "Backup differs from original bytes.");
        Assert(X(SaveSession.Open(path)) == 5.5, "Edited save did not reopen.");
        Assert(!session.IsDirty, "Successful save left dirty state.");
    }
    private static void ExternalModificationConflict()
    {
        string path = CopyFixture(); var session = SaveSession.Open(path); EditX(session);
        byte[] external = Encoding.UTF8.GetBytes(Sign(CanonicalFixture()) + " "); File.WriteAllBytes(path, external);
        Throws(() => session.Save(), "stale source");
        Assert(File.ReadAllBytes(path).SequenceEqual(external), "Concurrent writer's data overwritten.");
    }
    private static void ExternalReplacementConflict()
    {
        string path = CopyFixture(); var session = SaveSession.Open(path); EditX(session);
        string replacement = path + ".replacement"; byte[] external = Encoding.UTF8.GetBytes(Sign(CanonicalFixture()) + "  ");
        File.WriteAllBytes(replacement, external); File.Move(replacement, path, true);
        Throws(() => session.Save(), "replaced source");
        Assert(File.ReadAllBytes(path).SequenceEqual(external), "Replacement file overwritten.");
    }
    private static void DeletedSourceConflict()
    {
        string path = CopyFixture(); var session = SaveSession.Open(path); EditX(session); File.Delete(path);
        Throws(() => session.Save(), "deleted source"); Assert(!File.Exists(path), "Deleted source unexpectedly recreated.");
    }
    private static void SaveAsCollision()
    {
        var session = SaveSession.Open(CopyFixture()); EditX(session);
        string target = CopyFixture(); byte[] before = File.ReadAllBytes(target);
        Throws(() => session.SaveAs(target), "existing destination");
        Assert(File.ReadAllBytes(target).SequenceEqual(before), "Save-as overwrote destination.");
    }
    private static void SaveAsCopy()
    {
        string originalPath = CopyFixture(); byte[] before = File.ReadAllBytes(originalPath);
        var session = SaveSession.Open(originalPath); EditX(session);
        string target = Path.Combine(Scratch, "copy-" + Guid.NewGuid().ToString("N") + ".mscsave.json");
        session.SaveAs(target);
        Assert(File.ReadAllBytes(originalPath).SequenceEqual(before), "Save-as changed original.");
        Assert(X(SaveSession.Open(target)) == 5.5, "Save-as did not persist the edit.");
    }
    private static void FailedSaveDoesNotTouchSource() => RejectUnsafeEdit("player.needs", "/fatigue", "-1000");

    private static JsonNode Economy() => JsonNode.Parse("""
        {"schemaVersion":1,"configurationId":"economy.player.native.v1","balanceMinorUnits":10000,"nextSequence":1,"ledger":[]}
        """)!;
    private static void BalanceAction()
    {
        string path = CopyFixture(AddDomain("economy.player", 1, Economy()));
        var session = SaveSession.Open(path); session.SetBalance(25000);
        JsonNode money = Payload(session, "economy.player");
        Assert(money["balanceMinorUnits"]!.GetValue<long>() == 25000 && money["nextSequence"]!.GetValue<long>() == 2, "Balance/sequence not updated.");
        JsonNode transaction = money["ledger"]![0]!;
        Assert(transaction["amountMinorUnits"]!.GetValue<long>() == 15000 && transaction["direction"]!.GetValue<int>() == 1 && transaction["balanceBeforeMinorUnits"]!.GetValue<long>() == 10000, "Credit adjustment is inconsistent.");
        Throws(() => session.SetValue("economy.player", "/ledger/0/transactionId", "rewritten.identity"), "new adjustment identity is protected");
        session.Undo();
        Assert(!session.IsDirty && Payload(session, "economy.player")["ledger"]!.AsArray().Count == 0, "Undo did not remove adjustment transaction.");
        session.Redo(); session.SetBalance(5000);
        Assert(Payload(session, "economy.player")["ledger"]![1]!["direction"]!.GetValue<int>() == 0, "Lower balance did not create a debit.");
        session.Save();
        var restored = SaveSession.Open(path);
        Assert(Payload(restored, "economy.player")["balanceMinorUnits"]!.GetValue<long>() == 5000, "Balance was not persisted.");
        Assert(!restored.Validate().Any(i => i.Severity == ValidationSeverity.Error), "Saved adjustment chain invalid.");
    }
    private static void BalanceNoOp()
    {
        var session = SaveSession.Open(CopyFixture(AddDomain("economy.player", 1, Economy())));
        session.SetBalance(10000);
        Assert(!session.IsDirty && !session.CanUndo && Payload(session, "economy.player")["ledger"]!.AsArray().Count == 0, "No-op balance created a transaction.");
        Throws(() => session.SetBalance(-1), "negative balance");
    }
    private static void MoneyLedgerValidation()
    {
        JsonNode adjusted = SchemaActions.SetBalance(Economy(), 25000);
        foreach (string field in new[] { "direction", "amountMinorUnits", "sequence", "balanceAfterMinorUnits" })
        {
            JsonNode broken = adjusted.DeepClone(); broken["ledger"]![0]![field] = 0;
            Assert(SaveSchemaValidator.ValidatePayload("economy.player", broken).Any(i => i.Severity == ValidationSeverity.Error), "Money ledger accepted invalid " + field);
        }
    }
    private static void MoneyKindValidation()
    {
        foreach (int kind in new[] { -1, 7, 99 })
        {
            JsonNode adjusted = SchemaActions.SetBalance(Economy(), 25000); adjusted["ledger"]![0]!["kind"] = kind;
            Assert(SaveSchemaValidator.ValidatePayload("economy.player", adjusted).Any(i => i.Severity == ValidationSeverity.Error), "Unknown transaction enum accepted: " + kind);
        }
    }
    private static void MoneyInt64Overflow()
    {
        foreach (string number in new[] { "9223372036854775808", "-9223372036854775809", "9223372036854775807.5" })
        {
            JsonNode overflow = JsonNode.Parse("{\"schemaVersion\":1,\"balanceMinorUnits\":" + number + ",\"nextSequence\":1,\"ledger\":[]}")!;
            Assert(SaveSchemaValidator.ValidatePayload("economy.player", overflow).Any(i => i.Severity == ValidationSeverity.Error), "Int64 overflow was rounded into a valid value: " + number);
        }
        string file = CopyFixture(AddDomain("economy.player", 1, Economy())); byte[] original = File.ReadAllBytes(file);
        var session = SaveSession.Open(file); session.SetValue("economy.player", "/balanceMinorUnits", "9223372036854775808");
        Throws(() => session.Save(), "Int64 overflow in edited balance");
        Assert(File.ReadAllBytes(file).SequenceEqual(original), "Int64 overflow touched the source.");
    }
    private static void MoneyInt64Maximum()
    {
        var session = SaveSession.Open(CopyFixture(AddDomain("economy.player", 1, Economy())));
        session.SetBalance(long.MaxValue);
        Assert(Payload(session, "economy.player")["balanceMinorUnits"]!.GetValue<long>() == long.MaxValue, "Maximum money lost precision.");
        session.SetBalance(long.MaxValue - 1);
        Assert(Payload(session, "economy.player")["ledger"]![1]!["amountMinorUnits"]!.GetValue<long>() == 1L, "One penny at Int64 maximum was rounded away.");
        session.Undo(); Assert(Payload(session, "economy.player")["balanceMinorUnits"]!.GetValue<long>() == long.MaxValue, "Undo lost Int64 precision.");
        session.Redo(); session.Save();
        Assert(Payload(SaveSession.Open(session.FilePath), "economy.player")["balanceMinorUnits"]!.GetValue<long>() == long.MaxValue - 1, "Saved Int64 value lost precision.");
    }
    private static JsonNode TimeState() => JsonNode.Parse("""
        {"schemaVersion":1,"configId":"time.synthetic.v1","elapsedGameTicks":172800000000,"fractionalGameTickRemainder":0,"timeScale":1,"isPaused":false,"dayIndex":2,"year":2024,"month":2,"day":28,"timeOfDayTicks":0}
        """)!;
    private static void TimeAction()
    {
        string path = CopyFixture(AddDomain("core.time", 1, TimeState()));
        var session = SaveSession.Open(path); session.SetTime(new DateTime(2024, 3, 1, 12, 30, 0));
        JsonNode state = Payload(session, "core.time");
        Assert(state["dayIndex"]!.GetValue<long>() == 4, "Leap day was skipped.");
        Assert(state["timeOfDayTicks"]!.GetValue<long>() == 45_000_000_000L, "Clock uses wrong tick unit.");
        Assert(state["elapsedGameTicks"]!.GetValue<long>() == 390_600_000_000L, "Game epoch changed.");
        session.Undo(); Assert(!session.IsDirty, "Time action undo did not restore original.");
        session.Redo(); session.Save();
        Assert(Payload(SaveSession.Open(path), "core.time")["day"]!.GetValue<int>() == 1, "Changed date not saved.");
    }
    private static void TimeBeforeWorld()
    {
        var session = SaveSession.Open(CopyFixture(AddDomain("core.time", 1, TimeState())));
        Throws(() => session.SetTime(new DateTime(2020, 1, 1)), "time before world epoch");
        Assert(!session.IsDirty, "Rejected time action altered session.");
    }
    private static void InconsistentTime()
    {
        foreach ((string path, string value) in new[] { ("/day", "29"), ("/elapsedGameTicks", "172800000001"), ("/timeOfDayTicks", "1") })
        {
            string file = CopyFixture(AddDomain("core.time", 1, TimeState())); byte[] original = File.ReadAllBytes(file);
            var session = SaveSession.Open(file); session.SetValue("core.time", path, value);
            Assert(session.Validate().Any(i => i.Severity == ValidationSeverity.Error), "Inconsistent calendar edit was not flagged: " + path);
            Throws(() => session.Save(), "calendar epoch mismatch");
            Assert(File.ReadAllBytes(file).SequenceEqual(original), "Invalid calendar changed original bytes.");
        }
    }
    private static void NeedsReset()
    {
        var session = SaveSession.Open(CopyFixture());
        session.SetValue("player.needs", "/pendingIntoxicationEffect", "5.25");
        JsonNode original = Payload(session, "player.needs");
        JsonNode reset = SchemaActions.ResetNeeds(original);
        Assert(reset["thirst"]!.GetValue<float>() == 0 && reset["pendingIntoxicationEffect"]!.GetValue<float>() == 0, "Needs reset left a delayed effect.");
        Assert(reset["weightKilograms"]!.GetValue<double>() == 83, "Reset changed body weight.");
        Assert(original["pendingIntoxicationEffect"]!.GetValue<double>() == 5.25, "Reset changed source snapshot.");
    }
    private static void WireChangeReview()
    {
        var session = SaveSession.Open(CopyFixture(AddDomain("vehicle.satsuma", 1, VehicleRegression.Fixture())));
        JsonNode changed = SchemaActions.SetWiring(Payload(session, "vehicle.satsuma"), 0, ["BatteryHarness", "GroundBattery", "Starter"], true);
        session.SetJson("vehicle.satsuma", "", changed.ToJsonString());
        SaveChange[] additions = session.Changes.Where(c => c.JsonPointer.Contains("/electrical/installedConnectionIds/", StringComparison.Ordinal)).ToArray();
        Assert(additions.Length == 3 && additions.Any(c => c.After.Contains("Starter", StringComparison.Ordinal)), "Wire review did not show added connections individually.");
        session.Undo(); Assert(!session.IsDirty, "Wire undo left added connections or terminals.");
        session.Redo(); session.Save();
        Assert(Payload(SaveSession.Open(session.FilePath), "vehicle.satsuma")["vehicles"]![0]!["electrical"]!["installedConnectionIds"]!.AsArray().Count == 3, "Wire change failed save round-trip.");
    }

    private static int RoundTripUnity(string input, string output)
    {
        Directory.CreateDirectory(output);
        string[] sources = Directory.GetFiles(input, "*.mscsave.json");
        Assert(sources.Length > 0, "No Unity fixtures were provided.");
        foreach (string source in sources)
        {
            Run("Unity round-trip " + Path.GetFileName(source), () =>
            {
                string target = Path.Combine(output, Path.GetFileNameWithoutExtension(source) + "-" + Guid.NewGuid().ToString("N") + ".mscsave.json");
                byte[] original = File.ReadAllBytes(source);
                var session = SaveSession.Open(source);
                EditX(session, "4.125");
                session.SaveAs(target);
                Assert(X(SaveSession.Open(target)) == 4.125, "Round-trip altered player position.");
                Assert(File.ReadAllBytes(source).SequenceEqual(original), "Round-trip modified original Unity fixture.");
            });
        }
        Console.WriteLine($"RESULT passed={passed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }

    private static int InspectDirectory(string directory)
    {
        string[] files = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".save.json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mscsave.json", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert(files.Length > 0, "No native save files found.");
        foreach (string path in files)
        {
            Run("Read-only inspection " + Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path), () =>
            {
                byte[] before = File.ReadAllBytes(path);
                var session = SaveSession.Open(path);
                Console.WriteLine($"  version={session.DocumentVersion}; domains={session.Domains.Count}; editable={session.CanEdit}; errors={session.Validate().Count(i => i.Severity == ValidationSeverity.Error)}");
                Assert(File.ReadAllBytes(path).SequenceEqual(before), "Read-only inspection changed source bytes.");
            });
        }
        Console.WriteLine($"RESULT passed={passed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }

    private static int RoundTripNative(string input, string output)
    {
        Directory.CreateDirectory(output);
        string[] files = Directory.GetFiles(input, "*.save.json", SearchOption.AllDirectories);
        Assert(files.Length > 0, "No native save files found.");
        foreach (string path in files)
        {
            Run("Native save copy " + Path.GetFileName(Path.GetDirectoryName(path)), () =>
            {
                byte[] before = File.ReadAllBytes(path);
                var session = SaveSession.Open(path);
                double newX = X(session) + 0.125;
                EditX(session, newX.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                string target = Path.Combine(output, Path.GetFileName(Path.GetDirectoryName(path)) + "-" + Guid.NewGuid().ToString("N") + ".mscsave.json");
                session.SaveAs(target);
                Assert(X(SaveSession.Open(target)) == newX, "Edited copy did not retain new coordinate.");
                Assert(File.ReadAllBytes(path).SequenceEqual(before), "Original progress was changed.");
            });
        }
        Console.WriteLine($"RESULT passed={passed} failed={failed}");
        return failed == 0 ? 0 : 1;
    }
}
