using System.Text.Json.Nodes;
using MySummerRemake.SaveMaster.Core;

internal static class VehicleRegression
{
    public static void Register(Action<string, Action> run)
    {
        run("Vehicle fixture respects current assembly invariants", FixtureValid);
        run("Assembly schema3 accepts loose and installed dynamic consumables", DynamicValid);
        run("Assembly schema3 fastener actions preserve dynamic state and identity", DynamicActions);
        run("Assembly schema3 rejects contradictory dynamic occupancy and group latch", DynamicOccupancy);
        run("Assembly schema3 rejects duplicate dynamic and base identities", DynamicDuplicate);
        run("Assembly schema3 requires valid dynamic descriptors and mappings", DynamicDescriptor);
        run("Assembly schema3 validates dynamic optional consumable state", DynamicExtensions);
        run("Selected gear matches audited reverse, neutral and five forward ratios", SelectedGear);
        run("Bolt maximum comes from its definition, not a universal stage", BoltMaximum);
        run("Tightening synchronizes inserted/seated/stage and group latch", Tighten);
        run("Individual bolt stage updates its group latch in both directions", IndividualStage);
        run("Individual bolt refuses absent, unknown and out-of-range fasteners", IndividualInvalid);
        run("Loosening resets group latch without detaching a part", Loosen);
        run("Mount and installed part contradictions are rejected", Occupancy);
        run("Bolt insertion and seating contradictions are rejected", Insertion);
        run("Fastener group hysteresis contradicting stages is rejected", GroupLatch);
        run("Unknown bolt definitions are never bulk guessed", UnknownBolt);
        run("Wiring selections synchronize terminal prerequisites", Wiring);
        run("Wiring disconnect removes dependent terminal tightening", Disconnect);
        run("Unknown and duplicate wire IDs are rejected", InvalidWire);
    }

    internal static JsonObject Fixture()
    {
        MountCatalogEntry mount = SchemaVehicleDefinitions.Mounts["mount.battery"];
        var bolts = new JsonArray();
        foreach (string id in mount.Fasteners)
            bolts.Add(new JsonObject { ["mountId"] = mount.Id, ["fastenerDefinitionId"] = id, ["inserted"] = true, ["seated"] = true, ["stage"] = 0 });
        JsonObject result = new()
        {
            ["schemaVersion"] = 1, ["configurationId"] = "vehicles.native.v1",
            ["vehicles"] = new JsonArray(new JsonObject
            {
                ["schemaVersion"] = 1, ["stableVehicleId"] = "abcdef0123456789abcdef0123456789", ["configurationId"] = "test.synthetic.vehicle", ["tuningSchemaVersion"] = 1,
                ["assembly"] = new JsonObject
                {
                    ["schemaVersion"] = 2,
                    ["parts"] = new JsonArray(new JsonObject
                    {
                        ["stableEntityId"] = "00112233445566778899aabbccddeeff00", ["partDefinitionId"] = mount.AcceptedParts[0], ["lifecycleState"] = 1,
                        ["installedMountId"] = mount.Id, ["worldPosition"] = Vector(), ["worldRotation"] = Rotation(),
                    }),
                    ["mounts"] = new JsonArray(new JsonObject { ["mountId"] = mount.Id, ["installedPartStableEntityId"] = "00112233445566778899aabbccddeeff00" }),
                    ["fasteners"] = bolts,
                    ["fastenerGroups"] = new JsonArray(new JsonObject { ["mountId"] = mount.Id, ["isBolted"] = false }),
                },
                ["simulation"] = new JsonObject { ["schemaVersion"] = 1, ["wheels"] = new JsonArray() },
                ["physics"] = new JsonObject { ["worldPosition"] = Vector(), ["worldRotation"] = Rotation(), ["linearVelocity"] = Vector(), ["angularVelocity"] = Vector() },
                ["electrical"] = new JsonObject
                {
                    ["schemaVersion"] = 2, ["installedConnectionIds"] = new JsonArray(), ["batteryPlusInstalled"] = false, ["batteryMinusInstalled"] = false,
                    ["batteryPlusStage"] = 0, ["batteryMinusStage"] = 0, ["starterCableStage"] = 0,
                },
            }),
        };
        return result;
    }

    internal static JsonObject DynamicFixture(bool installed)
    {
        JsonObject result = Fixture();
        JsonObject assembly = Assembly(result);
        assembly["schemaVersion"] = 3;
        const string id = "11112222333344445555666677778888";
        const string mountId = "mount.satsuma.cylinder-head.spark-plug-1";
        assembly["dynamicParts"] = new JsonArray(new JsonObject
        {
            ["itemDefinitionId"] = "item.spark-plug",
            ["part"] = new JsonObject
            {
                ["stableEntityId"] = id, ["partDefinitionId"] = "vehicle.satsuma.part.spark-plug",
                ["lifecycleState"] = installed ? 1 : 0, ["installedMountId"] = installed ? mountId : "",
                ["worldPosition"] = new JsonObject { ["x"] = 2.25, ["y"] = 1.5, ["z"] = -3.75 }, ["worldRotation"] = Rotation(),
                ["futurePartState"] = new JsonObject { ["text"] = "Preserve \\ and \"quoted\" state", ["precision"] = 0.1234567890123456 },
            },
            ["linearVelocity"] = Vector(), ["angularVelocity"] = Vector(), ["sleeping"] = true,
            ["futureDynamicState"] = new JsonArray("new field", 17, false),
        });
        assembly["futureAssemblyState"] = new JsonObject { ["reviewed"] = false, ["counter"] = 9007199254740993L };
        if (installed)
        {
            MountCatalogEntry mount = SchemaVehicleDefinitions.Mounts[mountId];
            assembly["mounts"]!.AsArray().Add(new JsonObject { ["mountId"] = mountId, ["installedPartStableEntityId"] = id });
            foreach (string boltId in mount.Fasteners)
                assembly["fasteners"]!.AsArray().Add(new JsonObject { ["mountId"] = mountId, ["fastenerDefinitionId"] = boltId, ["inserted"] = true, ["seated"] = true, ["stage"] = 0 });
            assembly["fastenerGroups"]!.AsArray().Add(new JsonObject { ["mountId"] = mountId, ["isBolted"] = false });
        }
        return result;
    }

    internal static JsonObject DynamicItems(JsonNode vehicle)
    {
        var instances = new JsonArray();
        foreach (JsonObject descriptor in Assembly(vehicle)["dynamicParts"]!.AsArray().OfType<JsonObject>())
            instances.Add(new JsonObject
            {
                ["state"] = new JsonObject
                {
                    ["schemaVersion"] = 1, ["stableEntityId"] = descriptor["part"]!["stableEntityId"]!.DeepClone(),
                    ["definitionId"] = descriptor["itemDefinitionId"]!.DeepClone(), ["isConsumed"] = false,
                    ["condition"] = 100, ["content"] = 0, ["variantIndex"] = 0, ["containedStableIds"] = new JsonArray(), ["scalarStates"] = new JsonArray(),
                },
                ["isCanonicalPlacement"] = false, ["sourceCellId"] = "test-cell", ["materializationPosition"] = Vector(), ["materializationRotation"] = Rotation(),
            });
        return new JsonObject { ["schemaVersion"] = 1, ["configurationId"] = "items.instances.native.v1", ["instances"] = instances };
    }

    private static JsonObject Vector() => new() { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0 };
    private static JsonObject Rotation() => new() { ["x"] = 0.0, ["y"] = 0.0, ["z"] = 0.0, ["w"] = 1.0 };
    private static JsonObject Assembly(JsonNode root) => root["vehicles"]![0]!["assembly"]!.AsObject();
    private static JsonObject Electrical(JsonNode root) => root["vehicles"]![0]!["electrical"]!.AsObject();
    private static JsonObject Bolt(JsonNode root) => Assembly(root)["fasteners"]![0]!.AsObject();
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Valid(JsonNode value)
    {
        ValidationIssue[] errors = SaveSchemaValidator.ValidatePayload("vehicle.satsuma", value).Where(i => i.Severity == ValidationSeverity.Error).ToArray();
        Assert(errors.Length == 0, "Synthetic vehicle invalid: " + string.Join("; ", errors.Select(i => i.JsonPointer + ": " + i.Message)));
    }
    private static void Invalid(JsonNode value, string reason) => Assert(SaveSchemaValidator.ValidatePayload("vehicle.satsuma", value).Any(i => i.Severity == ValidationSeverity.Error), "Expected vehicle validation rejection: " + reason);
    private static void FixtureValid() => Valid(Fixture());
    private static JsonObject Dynamic(JsonNode value) => Assembly(value)["dynamicParts"]![0]!.AsObject();
    private static void DynamicValid()
    {
        Valid(DynamicFixture(false)); Valid(DynamicFixture(true));
        JsonObject empty = Fixture(); Assembly(empty)["schemaVersion"] = 3; Assembly(empty)["dynamicParts"] = new JsonArray(); Valid(empty);
    }
    private static void DynamicActions()
    {
        JsonObject original = DynamicFixture(true);
        JsonNode tightened = SchemaActions.SetFasteners(original, 0, true);
        Valid(tightened);
        Assert(JsonNode.DeepEquals(Dynamic(tightened), Dynamic(original)), "Fastener action changed dynamic identity or physical state.");
        Assert(Assembly(tightened)["fastenerGroups"]![1]!["isBolted"]!.GetValue<bool>(), "Dynamic mount group did not latch.");
        JsonNode bolt = Assembly(tightened)["fasteners"]!.AsArray().Last()!;
        JsonNode loosened = SchemaActions.SetFastenerStage(tightened, 0, bolt["mountId"]!.GetValue<string>(), bolt["fastenerDefinitionId"]!.GetValue<string>(), 0);
        Valid(loosened);
        Assert(!Assembly(loosened)["fastenerGroups"]![1]!["isBolted"]!.GetValue<bool>(), "Dynamic group latch did not reset after its last turn.");
        Assert(Dynamic(loosened)["part"]!["lifecycleState"]!.GetValue<int>() == 1, "Fastener action detached the dynamic part.");
        Assert(JsonNode.DeepEquals(Dynamic(SchemaActions.SetWiring(tightened, 0, ["Starter"], true)), Dynamic(original)), "Wiring action changed dynamic state.");
        Assert(Bolt(original)["stage"]!.GetValue<int>() == 0, "Dynamic action mutated its source snapshot.");
    }
    private static void DynamicOccupancy()
    {
        JsonObject mismatch = DynamicFixture(true); Assembly(mismatch)["mounts"]![1]!["installedPartStableEntityId"] = ""; Invalid(mismatch, "dynamic mount lost occupancy");
        JsonObject loose = DynamicFixture(true); Dynamic(loose)["part"]!["lifecycleState"] = 0; Invalid(loose, "loose dynamic still occupies mount");
        JsonObject group = DynamicFixture(true); Assembly(group)["fastenerGroups"]![1]!["isBolted"] = true; Invalid(group, "dynamic zero stages with latched group");
        JsonObject missing = DynamicFixture(true); Assembly(missing)["fastenerGroups"]!.AsArray().RemoveAt(1); Invalid(missing, "schema3 missing dynamic group");
        JsonObject noGroups = DynamicFixture(false); Assembly(noGroups).Remove("fastenerGroups"); Invalid(noGroups, "schema3 missing all groups");
    }
    private static void DynamicDuplicate()
    {
        JsonObject baseCollision = DynamicFixture(false); Dynamic(baseCollision)["part"]!["stableEntityId"] = Assembly(baseCollision)["parts"]![0]!["stableEntityId"]!.DeepClone(); Invalid(baseCollision, "dynamic identity collides with base");
        JsonObject duplicate = DynamicFixture(false); Assembly(duplicate)["dynamicParts"]!.AsArray().Add(Dynamic(duplicate).DeepClone()); Invalid(duplicate, "two dynamic descriptors share identity");
    }
    private static void DynamicDescriptor()
    {
        foreach (string defect in new[] { "missing-array", "missing-part", "missing-id", "bad-id", "missing-item", "mapping", "root", "missing-velocity", "wrong-sleeping" })
        {
            JsonObject value = DynamicFixture(false);
            switch (defect)
            {
                case "missing-array": Assembly(value).Remove("dynamicParts"); break;
                case "missing-part": Dynamic(value).Remove("part"); break;
                case "missing-id": Dynamic(value)["part"]!["stableEntityId"] = ""; break;
                case "bad-id": Dynamic(value)["part"]!["stableEntityId"] = "DEADBEEF111122223333444455556666"; break;
                case "missing-item": Dynamic(value).Remove("itemDefinitionId"); break;
                case "mapping": Dynamic(value)["itemDefinitionId"] = "item.oil-filter"; break;
                case "root": Dynamic(value)["part"]!["lifecycleState"] = 2; break;
                case "missing-velocity": Dynamic(value).Remove("linearVelocity"); break;
                case "wrong-sleeping": Dynamic(value)["sleeping"] = "true"; break;
            }
            Invalid(value, defect);
        }
    }
    private static void DynamicExtensions()
    {
        foreach (string extension in new[] { "hasSteeringAlignment", "hasCamshaftTiming", "hasEngineDocking", "hasEngineAdjustment" })
        {
            JsonObject value = DynamicFixture(false); Dynamic(value)["part"]![extension] = true; Invalid(value, "unsupported dynamic extension " + extension);
        }
        JsonObject oil = DynamicFixture(false);
        Dynamic(oil)["itemDefinitionId"] = "item.oil-filter";
        JsonNode part = Dynamic(oil)["part"]!;
        part["partDefinitionId"] = "vehicle.satsuma.part.oilfilter0";
        part["hasEngineAdjustment"] = true;
        part["engineAdjustment"] = new JsonObject { ["schemaVersion"] = 1, ["kind"] = 4, ["value"] = 0 };
        Valid(oil);
        part["engineAdjustment"]!["value"] = 1;
        Invalid(oil, "loose dynamic oil filter retained tightening");
    }
    private static void SelectedGear()
    {
        foreach (int gear in new[] { -1, 0, 1, 2, 3, 4, 5 })
        {
            JsonObject value = Fixture();
            value["vehicles"]![0]!["simulation"]!["selectedGear"] = gear;
            Valid(value);
        }
        foreach (double gear in new[] { -2d, 6d, 999d, 1.5d })
        {
            JsonObject value = Fixture();
            value["vehicles"]![0]!["simulation"]!["selectedGear"] = gear;
            Invalid(value, "unsupported selected gear " + gear);
        }
    }
    private static void BoltMaximum()
    {
        var value = Fixture();
        Bolt(value)["stage"] = 4; // M05 battery bolt is max 3, unlike eight-stage Satsuma bolts.
        Assembly(value)["fastenerGroups"]![0]!["isBolted"] = true;
        Invalid(value, "battery fastener beyond three stages");
    }
    private static void Tighten()
    {
        var original = Fixture();
        JsonNode changed = SchemaActions.SetFasteners(original, 0, true);
        Valid(changed);
        Assert(Bolt(changed)["stage"]!.GetValue<int>() == 3, "Wrong max stage for battery bolt.");
        Assert(Bolt(changed)["inserted"]!.GetValue<bool>() && Bolt(changed)["seated"]!.GetValue<bool>(), "Bolt seating not synchronized.");
        Assert(Assembly(changed)["fastenerGroups"]![0]!["isBolted"]!.GetValue<bool>(), "Group failed to latch.");
        Assert(Bolt(original)["stage"]!.GetValue<int>() == 0, "Bulk action mutated source snapshot.");
    }
    private static void IndividualStage()
    {
        JsonNode original = Fixture();
        JsonNode firstTurn = SchemaActions.SetFastenerStage(original, 0, "mount.battery", "fastener.battery", 1);
        Valid(firstTurn);
        Assert(Bolt(firstTurn)["stage"]!.GetValue<int>() == 1 && Assembly(firstTurn)["fastenerGroups"]![0]!["isBolted"]!.GetValue<bool>(), "First turn did not latch the battery group.");
        JsonNode unturned = SchemaActions.SetFastenerStage(firstTurn, 0, "mount.battery", "fastener.battery", 0);
        Valid(unturned);
        Assert(!Assembly(unturned)["fastenerGroups"]![0]!["isBolted"]!.GetValue<bool>(), "Zero stage did not unlatch group.");
        Assert(Bolt(original)["stage"]!.GetValue<int>() == 0 && !Assembly(original)["fastenerGroups"]![0]!["isBolted"]!.GetValue<bool>(), "Individual stage mutated the source snapshot.");
    }
    private static void IndividualInvalid()
    {
        foreach ((string boltId, int stage) in new[] { ("fastener.battery", -1), ("fastener.battery", 4), ("unknown.future.bolt", 1) })
        {
            bool rejected = false;
            try { SchemaActions.SetFastenerStage(Fixture(), 0, "mount.battery", boltId, stage); }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or NotSupportedException) { rejected = true; }
            Assert(rejected, "Invalid individual stage was accepted: " + boltId + "=" + stage);
        }
        JsonNode absent = Fixture(); Bolt(absent)["inserted"] = false; Bolt(absent)["seated"] = false;
        try { SchemaActions.SetFastenerStage(absent, 0, "mount.battery", "fastener.battery", 1); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Individual action silently inserted a missing bolt.");
    }
    private static void Loosen()
    {
        JsonNode tightened = SchemaActions.SetFasteners(Fixture(), 0, true);
        JsonNode loose = SchemaActions.SetFasteners(tightened, 0, false);
        Valid(loose);
        Assert(Bolt(loose)["stage"]!.GetValue<int>() == 0, "Bolt stayed tight.");
        Assert(!Assembly(loose)["fastenerGroups"]![0]!["isBolted"]!.GetValue<bool>(), "Group latch stayed set.");
        Assert(Assembly(loose)["parts"]![0]!["lifecycleState"]!.GetValue<int>() == 1, "Loosening silently detached a part.");
    }
    private static void Occupancy()
    {
        var value = Fixture(); Assembly(value)["mounts"]![0]!["installedPartStableEntityId"] = "ffeeddccbbaa99887766554433221100ff";
        Invalid(value, "part and mount disagree");
    }
    private static void Insertion()
    {
        var value = Fixture(); Bolt(value)["inserted"] = false; Bolt(value)["stage"] = 1;
        Invalid(value, "uninserted tightened bolt");
    }
    private static void GroupLatch()
    {
        var value = Fixture(); Assembly(value)["fastenerGroups"]![0]!["isBolted"] = true;
        Invalid(value, "zero tightening with latched group");
    }
    private static void UnknownBolt()
    {
        var value = Fixture(); Bolt(value)["fastenerDefinitionId"] = "unknown.future.bolt";
        try { SchemaActions.SetFasteners(value, 0, true); }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or NotSupportedException) { return; }
        throw new InvalidOperationException("Unknown bolt was assigned a guessed maximum.");
    }
    private static void Wiring()
    {
        JsonNode result = SchemaActions.SetWiring(Fixture(), 0, ["BatteryHarness", "GroundBattery", "Starter"], true);
        Valid(result);
        foreach (string terminal in new[] { "batteryPlusStage", "batteryMinusStage", "starterCableStage" })
            Assert(Electrical(result)[terminal]!.GetValue<int>() == 8, "Terminal did not tighten: " + terminal);
    }
    private static void Disconnect()
    {
        JsonNode wired = SchemaActions.SetWiring(Fixture(), 0, ["BatteryHarness", "GroundBattery", "Starter"], true);
        JsonNode disconnected = SchemaActions.SetWiring(wired, 0, [], false);
        Valid(disconnected);
        foreach (string terminal in new[] { "batteryPlusStage", "batteryMinusStage", "starterCableStage" })
            Assert(Electrical(disconnected)[terminal]!.GetValue<int>() == 0, "Removed cable left terminal tight: " + terminal);
    }
    private static void InvalidWire()
    {
        foreach (string[] selected in new[] { new[] { "MadeUpWire" }, new[] { "Starter", "Starter" } })
        {
            var value = Fixture(); Electrical(value)["installedConnectionIds"] = new JsonArray(selected.Select(s => (JsonNode?)JsonValue.Create(s)).ToArray());
            Invalid(value, "unknown/duplicate connection ID");
        }
        var missing = Fixture(); Electrical(missing)["starterCableStage"] = 8;
        Invalid(missing, "tight starter without starter wire");
    }
}
