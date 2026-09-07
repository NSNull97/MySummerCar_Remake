using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

internal static class VehicleTuningRegression
{
    private const string DomainId = "vehicle.satsuma";
    private const string SimulationPath = "/vehicles/0/simulation";
    private static readonly string[] Axes = ["x", "y", "z", "w"];
    // Phase1SatsumaOperatingAuthoring.HealthParts: purchased items own their condition elsewhere.
    private static readonly string[] MechanicalParts =
        ["piston1", "piston2", "piston3", "piston4", "crankshaft", "head-gasket", "water-pump", "alternator", "rocker-shaft"];

    public static void Register(Action<string, Action> run)
    {
        run("Current tuning fixture has nine mechanical parts, eight valves and typed service caps", CurrentFixtureValid);
        run("Mechanical condition validates percent, broken state and exact part ownership", MechanicalCondition);
        run("Valve settings validate all eight fractional values and their owner", ValveSettings);
        run("Service caps validate angle units, array identity and reservoir ownership", ServiceCaps);
        run("Operating snapshot validates litres, percent, bar, psi and seconds", OperatingRanges);
        run("Purchased dynamic parts cannot acquire fixed-part tuning state", DynamicTuningRejected);
        run("Disabled optional tuning DTOs do not activate or block editing", DisabledOptionalState);
        run("Old version16 and17 saves retain absent tuning state verbatim", OlderSavesRetained);
        run("Enabled unknown tuning schemas remain read-only", UnknownTuningSchema);
        run("Scalar and JSON editors cannot create or remove tuning presence", PresenceProtected);
        run("Service cap identity cannot be changed through scalar or JSON edits", CapIdentityProtected);
        run("Tuning edits round-trip atomically with undo, export and linked fields intact", TuningRoundTrip);
        run("Invalid tuning cannot overwrite the original save", InvalidTuningWrite);
        run("Tuning catalog exposes existing fields with eight valves and read-only snapshots", CatalogFields);
        run("Tuning catalog omits disabled optional state without changing the snapshot", CatalogDisabledState);
        run("Tuning action couples battery charge and broken condition in one reversible batch", TuningActionBatch);
        run("Tuning action does not synthesize absent state or silently repair broken parts", TuningActionAbsentState);
        run("Tuning action rejects unlisted fields and invalid values atomically", TuningActionRejected);
        run("Purchased-part tuning uses its matching item state and preserves assembly ownership", TuningActionPurchasedPart);
    }

    internal static JsonObject Fixture()
    {
        JsonObject root = VehicleRegression.Fixture();
        JsonObject assembly = Assembly(root);
        assembly["schemaVersion"] = 3;
        assembly["dynamicParts"] = new JsonArray();
        foreach (string suffix in MechanicalParts)
        {
            JsonObject part = AddPart(root, suffix);
            part["hasMechanicalCondition"] = true;
            part["mechanicalCondition"] = new JsonObject { ["schemaVersion"] = 1, ["conditionPercent"] = 93.75, ["broken"] = false };
        }
        JsonObject rocker = Part(root, "rocker-shaft");
        rocker["hasValveAdjustment"] = true;
        rocker["valveAdjustment"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["intake"] = new JsonObject { ["x"] = 7.0, ["y"] = 7.3, ["z"] = 7.6, ["w"] = 7.9 },
            ["exhaust"] = new JsonObject { ["x"] = 6.0, ["y"] = 6.3, ["z"] = 6.6, ["w"] = 6.9 },
        };
        foreach ((string suffix, int[] kinds) in new[]
        {
            ("rocker-cover", new[] { 0 }), ("gt-rocker-cover-gt", new[] { 0 }), ("radiator", new[] { 1 }),
            ("brake-master-cylinder", new[] { 2, 3 }), ("clutch-master-cylinder", new[] { 4 }),
        })
        {
            JsonObject part = AddPart(root, suffix);
            part["hasServiceCaps"] = true;
            part["serviceCaps"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["kinds"] = new JsonArray(kinds.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                ["angles"] = new JsonArray(kinds.Select(_ => (JsonNode?)JsonValue.Create(359.0)).ToArray()),
            };
        }
        JsonObject alternator = Part(root, "alternator");
        alternator["hasEngineAdjustment"] = true;
        alternator["engineAdjustment"] = new JsonObject { ["schemaVersion"] = 1, ["kind"] = 1, ["value"] = 4.25 };
        JsonObject simulation = Simulation(root);
        simulation["batteryVoltage"] = 12.6;
        simulation["batteryCharge01"] = 1.0;
        simulation["oilLiters"] = 4.5; // Older native oil snapshots must not be silently capped to the new service capacity of 3 L.
        simulation["coolantLiters"] = 5.0;
        simulation["hasSatsumaOperatingState"] = true;
        simulation["satsumaOperatingState"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["brakeFrontLiters"] = 0.75, ["brakeRearLiters"] = 0.625, ["clutchLiters"] = 0.25,
            ["oilContaminationPercent"] = 12.5, ["oilPressureBar"] = 4.125, ["coolantPressurePsi"] = 13.5,
            ["crankingSeconds"] = 2.25, ["radiatorFanRunning"] = true,
        };
        rocker["unknownPartSnapshot"] = new JsonObject { ["precision"] = 0.1234567890123456, ["counter"] = 9007199254740993L };
        simulation["unknownOperatingSnapshot"] = new JsonArray("preserved", 17, false);
        return root;
    }

    private static JsonObject AddPart(JsonNode root, string suffix)
    {
        JsonArray parts = Assembly(root)["parts"]!.AsArray();
        JsonObject part = new()
        {
            ["stableEntityId"] = (parts.Count + 4096).ToString("x32", CultureInfo.InvariantCulture),
            ["partDefinitionId"] = "vehicle.satsuma.part." + suffix, ["lifecycleState"] = 0, ["installedMountId"] = "",
            ["worldPosition"] = new JsonObject { ["x"] = 2.25, ["y"] = 1.5, ["z"] = -3.75 },
            ["worldRotation"] = new JsonObject { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0, ["w"] = 1.0 },
        };
        parts.Add(part);
        return part;
    }

    private static JsonObject Assembly(JsonNode root) => root["vehicles"]![0]!["assembly"]!.AsObject();
    private static JsonObject Simulation(JsonNode root) => root["vehicles"]![0]!["simulation"]!.AsObject();
    private static JsonObject Operating(JsonNode root) => Simulation(root)["satsumaOperatingState"]!.AsObject();
    private static JsonObject Part(JsonNode root, string suffix) => Assembly(root)["parts"]!.AsArray().OfType<JsonObject>()
        .Single(part => part["partDefinitionId"]!.GetValue<string>() == "vehicle.satsuma.part." + suffix);
    private static string PartPath(JsonNode root, string suffix) => "/vehicles/0/assembly/parts/" +
        Assembly(root)["parts"]!.AsArray().IndexOf(Part(root, suffix));
    private static JsonObject Valves(JsonNode root) => Part(root, "rocker-shaft")["valveAdjustment"]!.AsObject();
    private static JsonObject Caps(JsonNode root, string suffix) => Part(root, suffix)["serviceCaps"]!.AsObject();
    private static JsonNode Payload(SaveSession session) => session.Domains.Single(domain => domain.Id == DomainId).Payload;
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Valid(JsonNode root)
    {
        ValidationIssue[] errors = SaveSchemaValidator.ValidatePayload(DomainId, root).Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        Assert(errors.Length == 0, "Valid tuning rejected: " + string.Join("; ", errors.Select(issue => issue.JsonPointer + ": " + issue.Message)));
    }
    private static void Invalid(JsonNode root, string reason) =>
        Assert(SaveSchemaValidator.ValidatePayload(DomainId, root).Any(issue => issue.Severity == ValidationSeverity.Error), "Expected tuning rejection: " + reason);
    private static void Throws(Action action, string reason)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException) { return; }
        throw new InvalidOperationException("Expected tuning edit rejection: " + reason);
    }

    private static void CurrentFixtureValid()
    {
        JsonObject root = Fixture();
        Valid(root);
        Assert(Assembly(root)["parts"]!.AsArray().Count(part => part?["hasMechanicalCondition"]?.GetValue<bool>() == true) == 9, "Mechanical fixture lost one of its nine authored owners.");
        Assert(Valves(root)["intake"]!.AsObject().Count + Valves(root)["exhaust"]!.AsObject().Count == 8, "Fixture must contain eight independent valve values.");
    }

    private static void MechanicalCondition()
    {
        foreach ((double percent, bool broken) in new[] { (100.0, false), (0.125, false), (0.0, true), (75.5, true) })
        {
            JsonObject root = Fixture();
            Part(root, "piston1")["mechanicalCondition"]!["conditionPercent"] = percent;
            Part(root, "piston1")["mechanicalCondition"]!["broken"] = broken;
            Valid(root);
        }
        foreach (double percent in new[] { -0.001, 100.001, 0.0, 1e100 })
        {
            JsonObject root = Fixture(); Part(root, "piston1")["mechanicalCondition"]!["conditionPercent"] = percent;
            Invalid(root, "mechanical percent=" + percent.ToString("R", CultureInfo.InvariantCulture));
        }
        JsonObject owner = Fixture(); Part(owner, "piston1")["partDefinitionId"] = "vehicle.satsuma.part.battery";
        Invalid(owner, "mechanical state on a non-authored owner");
        JsonObject malformed = Fixture(); Part(malformed, "piston1")["mechanicalCondition"]!["broken"] = "false";
        Invalid(malformed, "mechanical broken must be a boolean");
    }

    private static void ValveSettings()
    {
        foreach (string side in new[] { "intake", "exhaust" })
        foreach (string axis in Axes)
        {
            double minimum = side == "intake" ? 4 : 3, maximum = minimum + 6;
            foreach (double setting in new[] { minimum, minimum + 0.3, maximum })
            {
                JsonObject root = Fixture(); Valves(root)[side]![axis] = setting; Valid(root);
            }
            foreach (double setting in new[] { minimum - 0.01, maximum + 0.01 })
            {
                JsonObject root = Fixture(); Valves(root)[side]![axis] = setting; Invalid(root, side + "/" + axis + " out of range");
            }
            JsonObject missing = Fixture(); Valves(missing)[side]!.AsObject().Remove(axis); Invalid(missing, "missing valve " + side + "/" + axis);
        }
        JsonObject array = Fixture(); Valves(array)["intake"] = new JsonArray(7, 7, 7, 7); Invalid(array, "valves must retain the Vector4 field layout");
        JsonObject owner = Fixture(); Part(owner, "rocker-shaft")["partDefinitionId"] = "vehicle.satsuma.part.crankshaft"; Invalid(owner, "valves on another part");
    }

    private static void ServiceCaps()
    {
        foreach (double angle in new[] { 1.0, 33.5, 359.0 })
        {
            JsonObject root = Fixture(); Caps(root, "brake-master-cylinder")["angles"]![1] = angle; Valid(root);
        }
        foreach (string defect in new[] { "zero", "too-large", "duplicate", "unknown", "fractional-kind", "missing-angle", "too-many", "wrong-owner", "wrong-count" })
        {
            JsonObject root = Fixture(); JsonObject caps = Caps(root, "brake-master-cylinder");
            switch (defect)
            {
                case "zero": caps["angles"]![0] = 0; break;
                case "too-large": caps["angles"]![0] = 360; break;
                case "duplicate": caps["kinds"]![1] = 2; break;
                case "unknown": caps["kinds"]![0] = 5; break;
                case "fractional-kind": caps["kinds"]![0] = 2.5; break;
                case "missing-angle": caps["angles"]!.AsArray().RemoveAt(1); break;
                case "too-many": caps["kinds"]!.AsArray().Add(4); caps["angles"]!.AsArray().Add(359); break;
                case "wrong-owner": caps["kinds"]![0] = 0; break;
                case "wrong-count": caps["kinds"]!.AsArray().RemoveAt(1); caps["angles"]!.AsArray().RemoveAt(1); break;
            }
            Invalid(root, "service caps: " + defect);
        }
    }

    private static void OperatingRanges()
    {
        foreach ((string field, double maximum) in new[]
        {
            ("brakeFrontLiters", 1.0), ("brakeRearLiters", 1.0), ("clutchLiters", 0.5),
            ("oilContaminationPercent", 100.0), ("oilPressureBar", 10.0), ("coolantPressurePsi", 50.0), ("crankingSeconds", 60.0),
        })
        {
            foreach (double value in new[] { 0.0, maximum / 3, maximum })
            {
                JsonObject root = Fixture(); Operating(root)[field] = value; Valid(root);
            }
            foreach (double value in new[] { -0.001, maximum + 0.001 })
            {
                JsonObject root = Fixture(); Operating(root)[field] = value; Invalid(root, field + " out of its DTO unit range");
            }
        }
        JsonObject malformed = Fixture(); Operating(malformed)["radiatorFanRunning"] = "true"; Invalid(malformed, "fan snapshot is boolean");
        // These captured states are independent: no guessed relationship to engine status or cap openness.
        JsonObject snapshots = Fixture(); Simulation(snapshots)["engineStatus"] = 0; Operating(snapshots)["oilPressureBar"] = 10; Operating(snapshots)["radiatorFanRunning"] = true;
        Caps(snapshots, "radiator")["angles"]![0] = 1; Operating(snapshots)["coolantPressurePsi"] = 50; Valid(snapshots);
    }

    private static void DynamicTuningRejected()
    {
        foreach (string extension in new[] { "MechanicalCondition", "ValveAdjustment", "ServiceCaps" })
        {
            JsonObject root = VehicleRegression.DynamicFixture(false);
            JsonNode part = Assembly(root)["dynamicParts"]![0]!["part"]!;
            string field = char.ToLowerInvariant(extension[0]) + extension[1..];
            JsonNode source = extension switch
            {
                "MechanicalCondition" => Part(Fixture(), "piston1")[field]!,
                "ValveAdjustment" => Valves(Fixture()),
                _ => Caps(Fixture(), "rocker-cover"),
            };
            part["has" + extension] = true; part[field] = source.DeepClone();
            Invalid(root, "a purchased dynamic part cannot own " + extension);
        }
    }

    private static void DisableOptionalState(JsonNode root, bool keepPresence)
    {
        foreach (JsonObject part in Assembly(root)["parts"]!.AsArray().OfType<JsonObject>())
        foreach (string field in new[] { "mechanicalCondition", "valveAdjustment", "serviceCaps" })
        {
            if (!part.ContainsKey(field)) continue;
            string presence = "has" + char.ToUpperInvariant(field[0]) + field[1..];
            if (keepPresence) part[presence] = false; else part.Remove(presence);
            // JsonUtility may materialize an unused inline default DTO. Its schema and
            // zero vector values cannot turn optional state into runtime authority.
            part[field] = new JsonObject { ["schemaVersion"] = 99, ["x"] = 0, ["y"] = 0, ["z"] = 0, ["w"] = 0 };
        }
        if (keepPresence) Simulation(root)["hasSatsumaOperatingState"] = false; else Simulation(root).Remove("hasSatsumaOperatingState");
        Simulation(root)["satsumaOperatingState"] = new JsonObject { ["schemaVersion"] = 99, ["brakeFrontLiters"] = -99 };
    }

    private static void DisabledOptionalState()
    {
        foreach (bool keepPresence in new[] { false, true })
        {
            JsonObject root = Fixture(); DisableOptionalState(root, keepPresence); Valid(root);
            WithSession(root, 18, (session, source, export) =>
            {
                byte[] sourceBytes = File.ReadAllBytes(source);
                Assert(session.CanEditDomain(DomainId), "Unused inline defaults blocked the vehicle editor.");
                JsonNode before = Payload(session);
                session.SetValue(DomainId, SimulationPath + "/oilLiters", "4.75");
                JsonNode expected = before.DeepClone(); Simulation(expected)["oilLiters"] = 4.75;
                Assert(JsonNode.DeepEquals(Payload(session), expected), "An unrelated scalar activated or rewrote disabled tuning.");
                session.SaveAs(export);
                Assert(JsonNode.DeepEquals(Payload(SaveSession.Open(export)), expected), "Export activated or rewrote an unused inline tuning DTO.");
                Assert(File.ReadAllBytes(source).SequenceEqual(sourceBytes), "Disabled-state export changed source bytes.");
            });
        }
    }

    private static void OlderSavesRetained()
    {
        foreach (int version in new[] { 16, 17 })
        {
            JsonObject root = VehicleRegression.Fixture();
            Simulation(root)["oilLiters"] = 4.5;
            WithSession(root, version, (session, source, export) =>
            {
                string originalVehicle = RawVehicle(source);
                byte[] originalBytes = File.ReadAllBytes(source);
                session.SetValue("player.state", "/worldPosition/x", "4.125");
                session.SaveAs(export);
                SaveSession reopened = SaveSession.Open(export);
                Assert(reopened.DocumentVersion == version, "Old tuning test silently migrated a document.");
                Assert(RawVehicle(export) == originalVehicle, "Old vehicle gained optional state or was rewritten.");
                Assert(File.ReadAllBytes(source).SequenceEqual(originalBytes), "Old source was overwritten by export.");
                Assert(!reopened.Validate().Any(issue => issue.Severity == ValidationSeverity.Error), "Old vehicle no longer passes validation.");
            });
        }
    }

    private static void UnknownTuningSchema()
    {
        foreach (string extension in new[] { "mechanical", "valves", "caps", "operating" })
        {
            JsonObject root = Fixture();
            JsonNode dto = extension switch
            {
                "mechanical" => Part(root, "piston1")["mechanicalCondition"]!, "valves" => Valves(root),
                "caps" => Caps(root, "rocker-cover"), _ => Operating(root),
            };
            dto["schemaVersion"] = 99;
            WithSession(root, 18, (session, _, _) =>
            {
                Assert(!session.CanEditDomain(DomainId), "Unknown active tuning schema is editable: " + extension);
                Throws(() => session.SetValue(DomainId, SimulationPath + "/oilLiters", "4.75"), "unknown tuning schema");
                Assert(!session.IsDirty, "Unknown tuning schema changed in memory.");
            });
        }
    }

    private static void PresenceProtected()
    {
        JsonObject root = Fixture();
        string[] pointers =
        [
            PartPath(root, "piston1") + "/hasMechanicalCondition", PartPath(root, "rocker-shaft") + "/hasValveAdjustment",
            PartPath(root, "rocker-cover") + "/hasServiceCaps", SimulationPath + "/hasSatsumaOperatingState",
        ];
        WithSession(root, 18, (session, _, _) =>
        {
            foreach (string pointer in pointers) Throws(() => session.SetValue(DomainId, pointer, "false"), "remove active presence " + pointer);
            JsonNode changed = Payload(session); Part(changed, "piston1")["hasMechanicalCondition"] = false;
            Throws(() => session.SetJson(DomainId, "", changed.ToJsonString()), "remove presence through JSON");
            Assert(!session.IsDirty, "Rejected presence changes affected the session.");
        });
        DisableOptionalState(root, true);
        WithSession(root, 17, (session, _, _) =>
        {
            foreach (string pointer in pointers) Throws(() => session.SetValue(DomainId, pointer, "true"), "activate absent state " + pointer);
            Assert(!session.IsDirty, "Rejected presence activation affected the session.");
        });
        WithSession(VehicleRegression.Fixture(), 16, (session, _, _) =>
        {
            JsonNode changed = Payload(session); Simulation(changed)["hasSatsumaOperatingState"] = true; Simulation(changed)["satsumaOperatingState"] = Operating(Fixture()).DeepClone();
            Throws(() => session.SetJson(DomainId, "", changed.ToJsonString()), "create previously absent operating extension");
            Assert(!session.IsDirty, "JSON created tuning data in an old save.");
        });
    }

    private static void CapIdentityProtected()
    {
        JsonObject root = Fixture();
        WithSession(root, 18, (session, _, _) =>
        {
            string path = PartPath(root, "brake-master-cylinder") + "/serviceCaps/kinds";
            Throws(() => session.SetValue(DomainId, path + "/0", "3"), "change a cap kind scalar");
            Throws(() => session.SetJson(DomainId, path, "[3,2]"), "reorder cap identity");
            JsonNode changed = Payload(session); Caps(changed, "brake-master-cylinder")["kinds"] = new JsonArray(3, 2);
            Throws(() => session.SetJson(DomainId, "", changed.ToJsonString()), "reorder cap identity through the full domain");
            Assert(!session.IsDirty, "A rejected cap identity edit entered history.");
        });
    }

    private static void TuningRoundTrip()
    {
        JsonObject root = Fixture();
        WithSession(root, 18, (session, source, export) =>
        {
            byte[] originalBytes = File.ReadAllBytes(source);
            JsonNode before = Payload(session);
            string piston = PartPath(before, "piston1"), rocker = PartPath(before, "rocker-shaft"), brake = PartPath(before, "brake-master-cylinder");
            session.ApplyEdits([
                new(DomainId, piston + "/mechanicalCondition/conditionPercent", "0"), new(DomainId, piston + "/mechanicalCondition/broken", "true"),
                new(DomainId, rocker + "/valveAdjustment/intake/x", "7.3"), new(DomainId, rocker + "/valveAdjustment/exhaust/w", "6.6"),
                new(DomainId, brake + "/serviceCaps/angles/0", "33.5"),
                new(DomainId, SimulationPath + "/satsumaOperatingState/brakeFrontLiters", "0.9"),
                new(DomainId, SimulationPath + "/satsumaOperatingState/clutchLiters", "0.4"),
                new(DomainId, SimulationPath + "/satsumaOperatingState/oilContaminationPercent", "75"),
            ]);
            JsonNode expected = before.DeepClone();
            Part(expected, "piston1")["mechanicalCondition"]!["conditionPercent"] = 0; Part(expected, "piston1")["mechanicalCondition"]!["broken"] = true;
            Valves(expected)["intake"]!["x"] = 7.3; Valves(expected)["exhaust"]!["w"] = 6.6;
            Caps(expected, "brake-master-cylinder")["angles"]![0] = 33.5;
            Operating(expected)["brakeFrontLiters"] = 0.9; Operating(expected)["clutchLiters"] = 0.4; Operating(expected)["oilContaminationPercent"] = 75;
            Assert(JsonNode.DeepEquals(Payload(session), expected), "Tuning batch changed linked or unknown fields.");
            Assert(session.Changes.Count == 8 && session.CanUndo, "Tuning batch did not describe all eight scalar edits.");
            session.Undo(); Assert(!session.IsDirty && JsonNode.DeepEquals(Payload(session), before), "One undo did not restore the complete tuning batch.");
            session.Redo(); Assert(JsonNode.DeepEquals(Payload(session), expected), "Tuning redo lost one of its linked values.");
            session.SaveAs(export);
            SaveSession reopened = SaveSession.Open(export);
            Assert(reopened.DocumentVersion == 18 && JsonNode.DeepEquals(Payload(reopened), expected), "Export lost tuning, existing settings, stable IDs or unknown data.");
            Assert(!reopened.Validate().Any(issue => issue.Severity == ValidationSeverity.Error), "Reopened tuning save is invalid.");
            Assert(File.ReadAllBytes(source).SequenceEqual(originalBytes), "Tuning export modified the source save.");
        });
    }

    private static void InvalidTuningWrite()
    {
        JsonObject root = Fixture();
        WithSession(root, 18, (session, source, _) =>
        {
            byte[] original = File.ReadAllBytes(source);
            Throws(() =>
            {
                session.SetValue(DomainId, PartPath(root, "piston1") + "/mechanicalCondition/conditionPercent", "0");
                session.Save();
            }, "zero condition with broken=false must not reach disk");
            Assert(File.ReadAllBytes(source).SequenceEqual(original), "Invalid tuning write changed original progress.");
        });
    }

    private static void CatalogFields()
    {
        JsonObject root = Fixture();
        JsonNode original = root.DeepClone();
        var fields = VehicleTuningCatalog.Build(root, 0);
        Assert(fields.Select(field => field.DomainId + field.JsonPointer).Distinct(StringComparer.Ordinal).Count() == fields.Count, "Catalog duplicates a tuning field.");
        foreach (var field in fields)
        {
            Assert(field.DomainId == DomainId && Resolve(root, field.JsonPointer) is JsonValue, "Catalog invented a missing or structured field: " + field.JsonPointer);
            Assert(field.Category.Length != 0 && field.Label.Length != 0, "Catalog contains an unnamed control.");
        }
        var valves = fields.Where(field => field.JsonPointer.Contains("/valveAdjustment/", StringComparison.Ordinal)).ToArray();
        Assert(valves.Length == 8 && valves.All(field => field.IsEditable), "Panel must expose all eight independent valve settings.");
        foreach (var field in valves)
        {
            bool intake = field.JsonPointer.Contains("/intake/", StringComparison.Ordinal);
            Assert(field.Min == (intake ? 4 : 3) && field.Max == (intake ? 10 : 9), "Valve control range is not the runtime setting range.");
        }
        Assert(fields.Count(field => field.JsonPointer.Contains("/serviceCaps/angles/", StringComparison.Ordinal)) == 6, "Catalog lost one of the fixture's six stock/GT cap values.");
        foreach (string snapshot in new[] { "/batteryCharge01", "/oilPressureBar", "/coolantPressurePsi", "/crankingSeconds", "/radiatorFanRunning" })
        {
            var field = fields.Single(entry => entry.JsonPointer.EndsWith(snapshot, StringComparison.Ordinal));
            Assert(!field.IsEditable && !field.RecommendedValue.HasValue, "A captured simulation value became a tuning control: " + snapshot);
        }
        Assert(JsonNode.DeepEquals(root, original), "Building the tuning catalog changed source state.");
    }

    private static void CatalogDisabledState()
    {
        foreach (bool keepPresence in new[] { false, true })
        {
            JsonObject root = Fixture(); DisableOptionalState(root, keepPresence);
            JsonNode before = root.DeepClone();
            var fields = VehicleTuningCatalog.Build(root, 0);
            Assert(!fields.Any(field => field.JsonPointer.Contains("/mechanicalCondition/", StringComparison.Ordinal) ||
                field.JsonPointer.Contains("/valveAdjustment/", StringComparison.Ordinal) || field.JsonPointer.Contains("/serviceCaps/", StringComparison.Ordinal) ||
                field.JsonPointer.Contains("/satsumaOperatingState/", StringComparison.Ordinal)), "Catalog exposed an inactive inline DTO.");
            Assert(JsonNode.DeepEquals(root, before), "Catalog synthesized disabled tuning state.");
        }
    }

    private static void TuningActionBatch()
    {
        JsonObject root = Fixture();
        WithSession(root, 18, (session, source, export) =>
        {
            byte[] sourceBytes = File.ReadAllBytes(source);
            JsonNode before = Payload(session);
            session.ApplyVehicleTuning([
                new(DomainId, SimulationPath + "/batteryVoltage", "6,3"),
                new(DomainId, PartPath(root, "piston1") + "/mechanicalCondition/conditionPercent", "0"),
                new(DomainId, PartPath(root, "rocker-shaft") + "/valveAdjustment/intake/x", "7,6"),
                new(DomainId, SimulationPath + "/satsumaOperatingState/brakeRearLiters", "0,9"),
            ]);
            JsonNode expected = before.DeepClone();
            Simulation(expected)["batteryVoltage"] = 6.3; Simulation(expected)["batteryCharge01"] = 0.5;
            Part(expected, "piston1")["mechanicalCondition"]!["conditionPercent"] = 0; Part(expected, "piston1")["mechanicalCondition"]!["broken"] = true;
            Valves(expected)["intake"]!["x"] = 7.6; Operating(expected)["brakeRearLiters"] = 0.9;
            Assert(JsonNode.DeepEquals(Payload(session), expected), "Panel batch lost comma values, dependent charge/broken state, or modified unrelated fields.");
            Assert(session.Changes.Count == 6, "Change review omitted a dependent tuning edit.");
            session.Undo(); Assert(!session.IsDirty && JsonNode.DeepEquals(Payload(session), before), "Tuning action requires more than one undo.");
            session.Redo(); Assert(JsonNode.DeepEquals(Payload(session), expected), "Tuning action redo changed its dependent values.");
            session.SaveAs(export);
            SaveSession reopened = SaveSession.Open(export);
            Assert(reopened.DocumentVersion == 18 && JsonNode.DeepEquals(Payload(reopened), expected), "Tuning action export did not preserve its exact state.");
            Assert(!reopened.Validate().Any(issue => issue.Severity == ValidationSeverity.Error), "Tuning action produced an invalid exported document.");
            Assert(File.ReadAllBytes(source).SequenceEqual(sourceBytes), "Tuning action export changed the source file.");
        });
    }

    private static void TuningActionAbsentState()
    {
        JsonObject legacy = VehicleRegression.Fixture();
        Simulation(legacy)["batteryVoltage"] = 12.6;
        WithSession(legacy, 16, (session, _, _) =>
        {
            JsonNode expected = Payload(session); Simulation(expected)["batteryVoltage"] = 6.3;
            session.ApplyVehicleTuning([new(DomainId, SimulationPath + "/batteryVoltage", "6.3")]);
            Assert(JsonNode.DeepEquals(Payload(session), expected), "Tuning action synthesized charge or another absent extension in a v16 save.");
        });
        JsonObject broken = Fixture();
        Part(broken, "piston1")["mechanicalCondition"]!["conditionPercent"] = 0;
        Part(broken, "piston1")["mechanicalCondition"]!["broken"] = true;
        WithSession(broken, 18, (session, _, _) =>
        {
            session.ApplyVehicleTuning([new(DomainId, PartPath(broken, "piston1") + "/mechanicalCondition/conditionPercent", "25")]);
            JsonNode part = Part(Payload(session), "piston1")["mechanicalCondition"]!;
            Assert(part["conditionPercent"]!.GetValue<double>() == 25 && part["broken"]!.GetValue<bool>(), "Raising condition silently repaired a broken part.");
        });
        WithSession(Fixture(), 18, (session, _, _) =>
        {
            session.ApplyVehicleTuning([new(DomainId, SimulationPath + "/batteryVoltage", "12,6")]);
            Assert(!session.IsDirty && !session.CanUndo, "Equivalent tuning values created a spurious history entry.");
        });
    }

    private static void TuningActionRejected()
    {
        foreach (string defect in new[] { "identity", "snapshot", "out-of-range", "unknown-domain", "invalid-number", "duplicate" })
        {
            JsonObject root = Fixture();
            WithSession(root, 18, (session, source, _) =>
            {
                byte[] originalBytes = File.ReadAllBytes(source);
                JsonNode before = Payload(session);
                SaveEdit rejected = defect switch
                {
                    "identity" => new(DomainId, PartPath(root, "piston1") + "/stableEntityId", "11112222333344445555666677778888"),
                    "snapshot" => new(DomainId, SimulationPath + "/satsumaOperatingState/oilPressureBar", "5"),
                    "out-of-range" => new(DomainId, SimulationPath + "/satsumaOperatingState/clutchLiters", "0.6"),
                    "unknown-domain" => new("probe.unknown", "/value", "1"),
                    "duplicate" => new(DomainId, PartPath(root, "rocker-shaft") + "/valveAdjustment/intake/x", "8"),
                    _ => new(DomainId, SimulationPath + "/batteryVoltage", "not a number"),
                };
                Throws(() => session.ApplyVehicleTuning([
                    new(DomainId, PartPath(root, "rocker-shaft") + "/valveAdjustment/intake/x", "7.6"), rejected,
                ]), "reject the entire tuning batch: " + defect);
                Assert(!session.IsDirty && !session.CanUndo && JsonNode.DeepEquals(Payload(session), before), "Rejected tuning batch partially committed: " + defect);
                Assert(File.ReadAllBytes(source).SequenceEqual(originalBytes), "Rejected tuning batch changed source: " + defect);
            });
        }
        JsonObject hidden = Fixture(); DisableOptionalState(hidden, true);
        WithSession(hidden, 18, (session, _, _) =>
        {
            Throws(() => session.ApplyVehicleTuning([new(DomainId, SimulationPath + "/satsumaOperatingState/brakeFrontLiters", "0.5")]), "inactive optional field is outside the panel catalog");
            Assert(!session.IsDirty, "An action edited an inactive inline DTO.");
        });
    }

    private static void TuningActionPurchasedPart()
    {
        JsonObject vehicle = VehicleRegression.DynamicFixture(false);
        JsonObject items = VehicleRegression.DynamicItems(vehicle);
        JsonObject unrelated = items["instances"]![0]!.DeepClone().AsObject();
        unrelated["state"]!["stableEntityId"] = "22223333444455556666777788889999";
        unrelated["state"]!["isConsumed"] = true;
        unrelated["state"]!["condition"] = 87.5;
        items["instances"]!.AsArray().Insert(0, unrelated);
        var fields = VehicleTuningCatalog.Build(vehicle, 0, items);
        const string itemCondition = "/instances/1/state/condition";
        Assert(fields.Any(field => field.DomainId == "items.instances" && field.JsonPointer == itemCondition && field.IsEditable), "Purchased part did not bind its matching item identity.");
        Assert(!fields.Any(field => field.DomainId == "items.instances" && field.JsonPointer.StartsWith("/instances/0/", StringComparison.Ordinal)), "Catalog exposed a different consumed item's state.");
        WithSession(vehicle, 18, (session, source, export) =>
        {
            byte[] originalBytes = File.ReadAllBytes(source);
            JsonNode beforeVehicle = Payload(session);
            JsonNode beforeItems = session.Domains.Single(domain => domain.Id == "items.instances").Payload;
            session.ApplyVehicleTuning([new("items.instances", itemCondition, "25,5")]);
            JsonNode expectedItems = beforeItems.DeepClone(); expectedItems["instances"]![1]!["state"]!["condition"] = 25.5;
            Assert(JsonNode.DeepEquals(Payload(session), beforeVehicle), "Purchased wear changed assembly-owned state.");
            Assert(JsonNode.DeepEquals(session.Domains.Single(domain => domain.Id == "items.instances").Payload, expectedItems), "Purchased wear changed the wrong item or another field.");
            session.Undo(); Assert(!session.IsDirty, "Purchased-part action did not undo atomically.");
            session.Redo(); session.SaveAs(export);
            SaveSession reopened = SaveSession.Open(export);
            Assert(JsonNode.DeepEquals(Payload(reopened), beforeVehicle) && JsonNode.DeepEquals(reopened.Domains.Single(domain => domain.Id == "items.instances").Payload, expectedItems), "Export broke item/assembly ownership.");
            Assert(!reopened.Validate().Any(issue => issue.Severity == ValidationSeverity.Error), "Purchased-part action export failed ownership validation.");
            Assert(File.ReadAllBytes(source).SequenceEqual(originalBytes), "Purchased-part action export changed source.");
        }, items);
    }

    private static JsonNode? Resolve(JsonNode root, string pointer)
    {
        JsonNode? node = root;
        foreach (string segment in pointer.Split('/').Skip(1))
        {
            string token = segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            node = node is JsonArray array ? array[int.Parse(token, CultureInfo.InvariantCulture)] : node?[token];
        }
        return node;
    }

    private static void WithSession(JsonNode vehicle, int documentVersion, Action<SaveSession, string, string> action, JsonNode? items = null)
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SaveMaster.TuningTests"));
        string directory = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "synthetic.mscsave.json");
            File.WriteAllText(source, SignedDocument(vehicle, documentVersion, items), new UTF8Encoding(false));
            action(SaveSession.Open(source), source, Path.Combine(directory, "edited.mscsave.json"));
        }
        finally
        {
            if (Path.GetFullPath(directory).StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(directory, true);
        }
    }

    private static string SignedDocument(JsonNode vehicle, int documentVersion, JsonNode? items)
    {
        // Preserve the independently Unity-generated envelope; only add this synthetic
        // project DTO and sign with SHA256 directly, never the production codec writer.
        string fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fixture-000.canonical.json");
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(fixture));
        var domains = source.RootElement.GetProperty("Domains").EnumerateArray()
            .Select(domain => (Id: domain.GetProperty("DomainId").GetString()!, Json: domain.GetRawText())).ToList();
        domains.Add((DomainId, JsonSerializer.Serialize(new { DomainId, SchemaVersion = 1, Required = true, PayloadJson = vehicle.ToJsonString() })));
        if (items is not null)
        {
            domains.Add(("items.instances", JsonSerializer.Serialize(new { DomainId = "items.instances", SchemaVersion = 1, Required = true, PayloadJson = items.ToJsonString() })));
            domains.Add(("world.entities", JsonSerializer.Serialize(new { DomainId = "world.entities", SchemaVersion = 2, Required = true, PayloadJson = "{\"schemaVersion\":2,\"entities\":[]}" })));
        }
        string header = source.RootElement.GetProperty("Header").GetRawText().Replace("\"DocumentVersion\":17", "\"DocumentVersion\":" + documentVersion, StringComparison.Ordinal);
        string canonical = "{\"Header\":" + header + ",\"Metadata\":" + source.RootElement.GetProperty("Metadata").GetRawText() +
            ",\"Domains\":[" + string.Join(",", domains.OrderBy(domain => domain.Id, StringComparer.Ordinal).Select(domain => domain.Json)) + "]}";
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return canonical.Replace("\"IntegritySha256\":\"\"", "\"IntegritySha256\":\"" + hash + "\"", StringComparison.Ordinal) + "\n";
    }

    private static string RawVehicle(string path)
    {
        using JsonDocument source = JsonDocument.Parse(File.ReadAllBytes(path));
        return source.RootElement.GetProperty("Domains").EnumerateArray().Single(domain => domain.GetProperty("DomainId").GetString() == DomainId)
            .GetProperty("PayloadJson").GetString()!;
    }
}
