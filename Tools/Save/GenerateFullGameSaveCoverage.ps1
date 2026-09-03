param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)

$ErrorActionPreference = "Stop"

$matrixPath = Join-Path $RepositoryRoot "Docs\Phase1\LEGACY_FEATURE_PARITY_MATRIX.csv"
$outputPath = Join-Path $RepositoryRoot "Docs\Save\FULL_GAME_SAVE_COVERAGE.csv"

if (-not (Test-Path -LiteralPath $matrixPath)) {
    throw "Authoritative parity matrix was not found: $matrixPath"
}

function Get-SaveDomainId([pscustomobject]$row) {
    switch ($row.Domain) {
        "Authority"      { return "authority.state" }
        "Communications" { return "communications.state" }
        "Economy"        { return "economy.player" }
        "Home"           { return "home.state" }
        "Items"          { return "items.instances" }
        "Jobs"           { return "jobs.state" }
        "Media"          { return "media.state" }
        "Needs"          { return "player.needs" }
        "NPC"            { return "npc.state" }
        "Player" {
            if ($row.FeatureId -eq "P1.PLAYER.011") { return "interaction.carry" }
            return "player.state"
        }
        "Presentation"   { return "presentation.runtime" }
        "Rally"          { return "rally.state" }
        "Satsuma"        { return "vehicle.satsuma" }
        "Save"           { return "save.system" }
        "Services"       { return "services.state" }
        "Story"          { return "progression.story" }
        "Traffic"        { return "traffic.state" }
        "Vehicles"       { return "vehicles.instances" }
        "World" {
            if ($row.FeatureId -eq "P1.WORLD.009") { return "core.time" }
            if ($row.FeatureId -eq "P1.WORLD.010") { return "weather.environment" }
            return "world.entities"
        }
        default { throw "No save-domain mapping for parity domain '$($row.Domain)' ($($row.FeatureId))." }
    }
}

function Get-SchemaOwner([string]$domainId) {
    switch ($domainId) {
        "save.system"            { return "MSC.Save.Runtime" }
        "player.state"           { return "MSC.Player.Runtime + MSC.Save.Integration" }
        "interaction.carry"      { return "MSC.Interaction.Runtime + MSC.Save.Integration" }
        "world.entities"         { return "MSC.World.Runtime + MSC.Save.Integration" }
        "core.time"              { return "MSC.Core.Runtime + MSC.Save.Integration" }
        "weather.environment"    { return "MSC.Weather.Runtime + MSC.Save.Integration" }
        "vehicle.satsuma"        { return "MSC.Vehicle.Assembly/Simulation + MSC.Save.Integration" }
        "presentation.runtime"   { return "Presentation owner; no gameplay DTO" }
        "items.instances"        { return "MSC.Items.Runtime + MSC.Save.Integration" }
        "player.needs"           { return "MSC.Needs.Runtime + MSC.Save.Integration" }
        "home.state"             { return "MSC.Home.Runtime + MSC.Save.Integration" }
        "lighting.electrical-grid" { return "MSC.Lighting.Runtime + MSC.Save.Integration" }
        "npc.state"              { return "MSC.NPC.Runtime + MSC.Save.Integration" }
        "vehicles.instances"     { return "MSC.Vehicle.Runtime (planned full roster)" }
        "traffic.state"          { return "MSC.Traffic.Runtime + MSC.Save.Integration" }
        "economy.player"         { return "MSC.Economy.Runtime + MSC.Save.Integration" }
        "services.state"         { return "MSC.Services.Runtime + MSC.Save.Integration" }
        "jobs.state"             { return "MSC.Jobs.Runtime (planned)" }
        "communications.state"   { return "MSC.Communications.Runtime (planned)" }
        "progression.story"      { return "MSC.Progression.Runtime (planned)" }
        "authority.state"        { return "MSC.Authority.Runtime (planned)" }
        "rally.state"            { return "MSC.Rally.Runtime (planned)" }
        "media.state"            { return "MSC.Media.Runtime (planned)" }
        default                    { throw "No schema owner for '$domainId'." }
    }
}

function Test-ExplicitNonPersistent([pscustomobject]$row) {
    if ($row.Required -eq "No" -and $row.Status -eq "RejectedNotInLockedDonorVersion") { return $true }
    if ($row.SaveCoverage -match "(?i)non-persistent|not applicable|static world|static baseline") { return $true }
    return $false
}

function Get-UnloadedCellPolicy([string]$domainId, [string]$expectation) {
    if ($expectation -eq "NonPersistent" -or $expectation -eq "PendingDecision") { return "NotApplicable" }
    if ($domainId -in @(
        "world.entities", "interaction.carry", "items.instances", "home.state", "npc.state",
        "vehicles.instances", "traffic.state", "services.state", "jobs.state",
        "communications.state", "progression.story", "authority.state", "rally.state", "media.state")) {
        return "DeferredByStableId"
    }
    return "Immediate"
}

function Get-ReplacementPolicy([string]$domainId, [string]$expectation) {
    if ($expectation -eq "NonPersistent" -or $expectation -eq "PendingDecision") { return "NotApplicable" }
    if ($domainId -in @(
        "world.entities", "interaction.carry", "items.instances", "home.state", "npc.state",
        "vehicle.satsuma", "vehicles.instances", "traffic.state", "services.state", "jobs.state",
        "communications.state", "progression.story", "authority.state", "rally.state", "media.state")) {
        return "StableIdPreserved"
    }
    return "NotApplicable"
}

function Get-CoverageStatus([pscustomobject]$row, [string]$expectation) {
    switch ($row.FeatureId) {
        "P1.SAVE.001" { return "Covered" }
        "P1.SAVE.002" { return "Covered" }
        "P1.SAVE.003" { return "Covered" }
        "P1.SAVE.004" { return "Covered" }
        "P1.SAVE.005" { return "Covered" }
        "P1.SAVE.006" { return "Partial" }
        "P1.SAVE.007" { return "PendingDecision" }
        "P1.WORLD.009" { return "Covered" }
        "P1.WORLD.010" { return "Covered" }
    }

    if ($expectation -eq "NonPersistent") { return "NotRequired" }
    if ($row.SaveCoverage -match "(?i)^\s*Uncovered") { return "Uncovered" }
    if ($row.Status -eq "Unknown") { return "Uncovered" }

    $implementation = $row.CurrentImplementation
    $hasBoundedImplementation =
        $row.Status -eq "PartiallyImplemented" -and
        $implementation -notmatch "(?i)^nothing|evidence only|external frozen evidence only|static evidence only"

    if ($hasBoundedImplementation) { return "Partial" }
    return "Planned"
}

function Get-Evidence([pscustomobject]$row, [string]$expectation, [string]$coverageStatus) {
    if ($row.FeatureId -eq "P1.SAVE.007") {
        return "Optional donor importer is outside the native-save foundation; exact donor schema evidence is still missing and the decision remains pending."
    }
    if ($expectation -eq "NonPersistent") {
        if ($row.Required -eq "No" -and $row.Status -eq "RejectedNotInLockedDonorVersion") {
            return "Not in the locked donor version; no gameplay state may be invented or persisted. Source status: $($row.Status)."
        }
        if ($row.Required -eq "No") {
            return "No independent persistent domain is required: $($row.SaveCoverage). Source status: $($row.Status)."
        }
        return "Explicit non-persistent rationale from parity matrix: $($row.SaveCoverage). Mutable gameplay authority, when any, is owned by another save domain."
    }
    if ($coverageStatus -eq "Covered") {
        return "09A native-save contract covers this implemented domain boundary; source implementation: $($row.CurrentImplementation)."
    }
    if ($coverageStatus -eq "Partial") {
        return "A bounded implementation or DTO exists, but full Phase 1 state/restore coverage is incomplete: $($row.CurrentImplementation); prior save evidence: $($row.SaveCoverage)."
    }
    if ($coverageStatus -eq "Uncovered") {
        return "Donor behavior or persistence semantics remain unknown; owning milestone must resolve evidence before defining a DTO."
    }
    return "Future Phase 1 domain is not implemented. Owning milestone must add stable identity, DTO, participant, migration note and round-trip coverage without fabricating state."
}

$matrixRows = @(Import-Csv -LiteralPath $matrixPath)
$coverageRows = foreach ($row in $matrixRows) {
    $domainId = Get-SaveDomainId $row
    $expectation = if ($row.FeatureId -eq "P1.SAVE.007") {
        "PendingDecision"
    } elseif (Test-ExplicitNonPersistent $row) {
        "NonPersistent"
    } else {
        "Persistent"
    }
    $coverageStatus = Get-CoverageStatus $row $expectation

    $currentImplementation = if ($coverageStatus -eq "Covered") {
        "09A registered native-save coverage; prior matrix state: $($row.CurrentImplementation)"
    } elseif ($row.FeatureId -eq "P1.SAVE.006") {
        "09A deterministic current-domain fixtures; full Phase 1 fresh/mid/late fixtures remain incremental"
    } else {
        $row.CurrentImplementation
    }

    [pscustomobject][ordered]@{
        FeatureId             = $row.FeatureId
        SaveDomainId          = $domainId
        PersistentExpectation = $expectation
        CoverageStatus        = $coverageStatus
        SchemaOwner           = Get-SchemaOwner $domainId
        CurrentImplementation = $currentImplementation
        EvidenceOrReason      = Get-Evidence $row $expectation $coverageStatus
        UnloadedCellPolicy    = Get-UnloadedCellPolicy $domainId $expectation
        ReplacementPolicy     = Get-ReplacementPolicy $domainId $expectation
        OwningMilestone       = $row.OwnerMilestone
        Notes                 = "Generated from authoritative parity row; later owner must update this row in the same commit as runtime implementation."
    }
}

$contractDefinitions = @(
    @("authority.state", "13B", "Planned"),
    @("communications.state", "12A", "Planned"),
    @("core.time", "09A", "Covered"),
    @("economy.player", "12A", "Partial"),
    @("home.state", "09C", "Partial"),
    @("interaction.carry", "09A", "Partial"),
    @("items.instances", "09B", "Partial"),
    @("jobs.state", "12B", "Planned"),
    @("media.state", "13C", "Planned"),
    @("lighting.electrical-grid", "07B", "Partial"),
    @("npc.state", "10B", "Partial"),
    @("player.needs", "09C", "Partial"),
    @("player.state", "09A", "Covered"),
    @("presentation.runtime", "08A", "NotRequired"),
    @("progression.story", "13A", "Planned"),
    @("rally.state", "13B", "Planned"),
    @("save.system", "09A", "Covered"),
    @("services.state", "12A", "Partial"),
    @("traffic.state", "11B", "Partial"),
    @("vehicle.satsuma", "09A", "Partial"),
    @("vehicles.instances", "11A", "Planned"),
    @("weather.environment", "09A", "Covered"),
    @("world.entities", "09A", "Partial")
)

$contractRows = foreach ($definition in $contractDefinitions) {
    $domainId = $definition[0]
    $milestone = $definition[1]
    $status = $definition[2]
    $expectation = if ($domainId -eq "presentation.runtime") { "NonPersistent" } else { "Persistent" }

    [pscustomobject][ordered]@{
        FeatureId             = "CONTRACT.$domainId"
        SaveDomainId          = $domainId
        PersistentExpectation = $expectation
        CoverageStatus        = $status
        SchemaOwner           = Get-SchemaOwner $domainId
        CurrentImplementation = if ($domainId -eq "presentation.runtime") {
            "08A presentation remains separate from gameplay save authority"
        } elseif ($status -eq "Planned") {
            "Domain not implemented yet"
        } elseif ($status -eq "Partial") {
            "Registered native-save domain with bounded runtime coverage; complete Phase 1 state coverage remains incomplete"
        } else {
            "Registered native-save domain or save-framework contract with current bounded coverage"
        }
        EvidenceOrReason      = if ($expectation -eq "NonPersistent") {
            "Presentation is replaceable and is never gameplay authority; settings remain in the separate UI settings store."
        } else {
            "Mandatory registration contract: stable state identity; versioned DTO schema; capture and restore order; unloaded-cell and missing-content behavior; migration impact; deterministic round-trip test; corruption and recovery behavior."
        }
        UnloadedCellPolicy    = Get-UnloadedCellPolicy $domainId $expectation
        ReplacementPolicy     = Get-ReplacementPolicy $domainId $expectation
        OwningMilestone       = $milestone
        Notes                 = "Contract row; not a parity FeatureId. Runtime implementation and this coverage record must change together."
    }
}

$allRows = @($coverageRows) + @($contractRows | Sort-Object SaveDomainId)
$outputDirectory = Split-Path -Parent $outputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$csvLines = @($allRows | ConvertTo-Csv -NoTypeInformation)
[System.IO.File]::WriteAllLines($outputPath, $csvLines, [System.Text.UTF8Encoding]::new($false))

Write-Output "Generated $($coverageRows.Count) parity rows and $($contractRows.Count) domain-contract rows: $outputPath"
