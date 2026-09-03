[CmdletBinding()]
param(
    [string]$WaapiUri = 'http://127.0.0.1:8090/waapi',
    [string]$RepoRoot = '',
    [string]$WwiseProjectPath = '',
    [string]$DonorAudioRoot = '',
    [string]$ConvertedAudioRoot = '',
    [string]$FfmpegPath = '',
    [string]$LogPath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

if ([string]::IsNullOrWhiteSpace($DonorAudioRoot) -or
    [string]::IsNullOrWhiteSpace($ConvertedAudioRoot)) {
    $donorPathsConfig = Join-Path $RepoRoot 'Config\DonorPaths.local.json'
    if (-not (Test-Path -LiteralPath $donorPathsConfig -PathType Leaf)) {
        throw 'Pass -DonorAudioRoot/-ConvertedAudioRoot or configure the ignored Config/DonorPaths.local.json.'
    }

    $donorPaths = Get-Content -LiteralPath $donorPathsConfig -Raw |
        ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($donorPaths.DonorStagingDirectory)) {
        throw 'Config/DonorPaths.local.json does not define DonorStagingDirectory.'
    }

    if ([string]::IsNullOrWhiteSpace($DonorAudioRoot)) {
        $DonorAudioRoot = Join-Path $donorPaths.DonorStagingDirectory `
            'raw\world\milestone-04a1\assetripper-unity-project\ExportedProject\Assets\AudioClip'
    }

    if ([string]::IsNullOrWhiteSpace($ConvertedAudioRoot)) {
        $ConvertedAudioRoot = Join-Path $donorPaths.DonorStagingDirectory `
            'converted\audio\m08'
    }
}

if ([string]::IsNullOrWhiteSpace($WwiseProjectPath)) {
    $WwiseProjectPath = Join-Path $RepoRoot 'MySummerCar_Remake_WwiseProject\MySummerCar_Remake_WwiseProject.wproj'
}

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $RepoRoot 'Logs\M08_WwiseAuthoring.log'
}

$logDirectory = Split-Path -Parent $LogPath
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
Set-Content -LiteralPath $LogPath -Value '' -Encoding UTF8

function Write-M08Log {
    param([Parameter(Mandatory)][string]$Message)

    $line = '{0:yyyy-MM-dd HH:mm:ss.fff} {1}' -f (Get-Date), $Message
    Write-Host $line
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
}

function Invoke-Waapi {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [hashtable]$Arguments = @{},
        [hashtable]$Options = @{}
    )

    $body = @{
        uri = $Uri
        args = $Arguments
        options = $Options
    } | ConvertTo-Json -Depth 64 -Compress

    try {
        return Invoke-RestMethod -Uri $WaapiUri -Method Post -ContentType 'application/json' -Body $body
    }
    catch {
        $details = $_.Exception.Message
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) {
            $details = '{0}: {1}' -f $details, $_.ErrorDetails.Message
        }

        throw "WAAPI call '$Uri' failed. $details"
    }
}

function Get-WwiseObjectByPath {
    param([Parameter(Mandatory)][string]$Path)

    try {
        $result = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{
            from = @{ path = @($Path) }
        } -Options @{
            return = @('id', 'name', 'type', 'path', 'parent', '@ActionType', '@Target', '@OutputBus', '@RTPC', '@SwitchGroupOrStateGroup')
        }
    }
    catch {
        if ($_.Exception.Message -match 'ak\.wwise\.query\.unknown_object|from path cannot be resolved') {
            return $null
        }
        throw
    }

    return @($result.return) | Select-Object -First 1
}

function Get-WwiseChildren {
    param([Parameter(Mandatory)][string]$Path)

    $result = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{
        from = @{ path = @($Path) }
        transform = @(@{ select = @('children') })
    } -Options @{
        return = @('id', 'name', 'type', 'path', 'parent', '@ActionType', '@Target', '@OutputBus', '@RTPC', '@SwitchGroupOrStateGroup')
    }

    return @($result.return)
}

function Ensure-WwiseObject {
    param(
        [Parameter(Mandatory)][string]$Parent,
        [Parameter(Mandatory)][string]$Type,
        [Parameter(Mandatory)][string]$Name,
        [string]$Notes = ''
    )

    $arguments = @{
        parent = $Parent
        type = $Type
        name = $Name
        onNameConflict = 'merge'
        autoAddToSourceControl = $false
    }
    if (-not [string]::IsNullOrWhiteSpace($Notes)) {
        $arguments.notes = $Notes
    }

    Invoke-Waapi -Uri 'ak.wwise.core.object.create' -Arguments $arguments | Out-Null
    $path = "$Parent\$Name"
    $object = Get-WwiseObjectByPath -Path $path
    if (-not $object) {
        throw "Wwise object was not created or resolved: $path"
    }

    return $object
}

function Set-WwiseProperty {
    param(
        [Parameter(Mandatory)][string]$Object,
        [Parameter(Mandatory)][string]$Property,
        [Parameter(Mandatory)]$Value
    )

    Invoke-Waapi -Uri 'ak.wwise.core.object.setProperty' -Arguments @{
        object = $Object
        property = $Property
        value = $Value
    } | Out-Null
}

function Set-WwiseReference {
    param(
        [Parameter(Mandatory)][string]$Object,
        [Parameter(Mandatory)][string]$Reference,
        [Parameter(Mandatory)][string]$Value
    )

    Invoke-Waapi -Uri 'ak.wwise.core.object.setReference' -Arguments @{
        object = $Object
        reference = $Reference
        value = $Value
    } | Out-Null
}

function Set-SwitchContainerAssignments {
    param(
        [Parameter(Mandatory)][string]$ContainerPath,
        [Parameter(Mandatory)][string]$SwitchGroupPath,
        [Parameter(Mandatory)][object[]]$Assignments
    )

    Set-WwiseReference -Object $ContainerPath -Reference 'SwitchGroupOrStateGroup' -Value $SwitchGroupPath

    $existingAssignments = Invoke-Waapi -Uri 'ak.wwise.core.switchContainer.getAssignments' -Arguments @{
        id = $ContainerPath
    }
    foreach ($assignment in @($existingAssignments.return)) {
        Invoke-Waapi -Uri 'ak.wwise.core.switchContainer.removeAssignment' -Arguments @{
            child = $assignment.child
            stateOrSwitch = $assignment.stateOrSwitch
        } | Out-Null
    }

    foreach ($assignment in $Assignments) {
        Invoke-Waapi -Uri 'ak.wwise.core.switchContainer.addAssignment' -Arguments @{
            child = "$ContainerPath\$($assignment.Child)"
            stateOrSwitch = "$SwitchGroupPath\$($assignment.Switch)"
        } | Out-Null
    }
}

function Ensure-GameParameter {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][double]$Minimum,
        [Parameter(Mandatory)][double]$Maximum,
        [Parameter(Mandatory)][double]$DefaultValue,
        [Parameter(Mandatory)][string]$StableId
    )

    $object = Ensure-WwiseObject -Parent '\Game Parameters\Default Work Unit' -Type 'GameParameter' -Name $Name -Notes "Project-owned mapping: $StableId"
    Invoke-Waapi -Uri 'ak.wwise.core.gameParameter.setRange' -Arguments @{
        object = $object.id
        min = $Minimum
        max = $Maximum
        onCurveUpdate = 'preserveX'
    } | Out-Null
    Set-WwiseProperty -Object $object.id -Property 'InitialValue' -Value $DefaultValue
    Set-WwiseProperty -Object $object.id -Property 'SimulationValue' -Value $DefaultValue
}

function Ensure-GameSyncGroup {
    param(
        [Parameter(Mandatory)][ValidateSet('SwitchGroup', 'StateGroup')][string]$Type,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string[]]$Values,
        [Parameter(Mandatory)][string]$StableId
    )

    $root = if ($Type -eq 'SwitchGroup') { '\Switches\Default Work Unit' } else { '\States\Default Work Unit' }
    $valueType = if ($Type -eq 'SwitchGroup') { 'Switch' } else { 'State' }
    $group = Ensure-WwiseObject -Parent $root -Type $Type -Name $Name -Notes "Project-owned mapping: $StableId"
    foreach ($existingValue in (Get-WwiseChildren -Path $group.path | Where-Object { $_.type -eq $valueType -and $_.name -notin $Values })) {
        Invoke-Waapi -Uri 'ak.wwise.core.object.delete' -Arguments @{ object = $existingValue.id } | Out-Null
    }
    foreach ($value in $Values) {
        Ensure-WwiseObject -Parent $group.path -Type $valueType -Name $value -Notes "Project-owned value for $StableId" | Out-Null
    }
}

function Import-PrototypeSound {
    param(
        [Parameter(Mandatory)][string]$ParentPath,
        [Parameter(Mandatory)][string]$SoundName,
        [Parameter(Mandatory)][string]$SourceFileName,
        [Parameter(Mandatory)][string]$Domain,
        [Parameter(Mandatory)][string]$OutputBusPath,
        [bool]$Loop = $false,
        [bool]$Spatialized = $true
    )

    $sourceAudioFile = Join-Path $DonorAudioRoot $SourceFileName
    if (-not (Test-Path -LiteralPath $sourceAudioFile -PathType Leaf)) {
        throw "Required donor prototype audio is missing: $sourceAudioFile"
    }

    $hash = (Get-FileHash -LiteralPath $sourceAudioFile -Algorithm SHA256).Hash
    $convertedName = '{0}-{1}.wav' -f ([IO.Path]::GetFileNameWithoutExtension($SourceFileName)), $hash.Substring(0, 12)
    $audioFile = Join-Path $ConvertedAudioRoot $convertedName
    & $script:ResolvedFfmpegPath -hide_banner -loglevel error -y -i $sourceAudioFile -map_metadata -1 -c:a pcm_s16le -ar 48000 $audioFile
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $audioFile -PathType Leaf)) {
        throw "FFmpeg conversion failed for $sourceAudioFile"
    }
    $convertedHash = (Get-FileHash -LiteralPath $audioFile -Algorithm SHA256).Hash
    $soundPath = "$ParentPath\$SoundName"
    $notes = "TemporaryDirectImport; private prototype only; source=$SourceFileName; SHA256=$hash"
    $sound = Get-WwiseObjectByPath -Path $soundPath
    $existingAudioSources = @(if ($sound) { Get-WwiseChildren -Path $soundPath | Where-Object { $_.type -eq 'AudioFileSource' } })
    if ($existingAudioSources.Count -eq 0) {
        $result = Invoke-Waapi -Uri 'ak.wwise.core.audio.import' -Arguments @{
            importOperation = 'useExisting'
            autoAddToSourceControl = $false
            autoCheckOutToSourceControl = $false
            imports = @(@{
                audioFile = $audioFile
                originalsSubFolder = "TempDonorPrototype\$Domain"
                objectPath = "$ParentPath\<Sound SFX>$SoundName"
                notes = $notes
                audioSourceNotes = $notes
            })
        } -Options @{
            return = @('id', 'name', 'type', 'path')
        }

        foreach ($entry in @($result.log)) {
            Write-M08Log ("Wwise import log [{0}]: {1}" -f $entry.severity, $entry.message)
        }
    }

    $sound = Get-WwiseObjectByPath -Path $soundPath
    if (-not $sound) {
        throw "Imported Wwise Sound was not resolved: $soundPath"
    }

    if ($Spatialized) {
        Set-WwiseProperty -Object $sound.id -Property 'OverridePositioning' -Value $true
        Set-WwiseProperty -Object $sound.id -Property '3DSpatialization' -Value 1
    }

    Set-WwiseProperty -Object $sound.id -Property 'IsLoopingEnabled' -Value $Loop
    Set-WwiseReference -Object $sound.id -Reference 'OutputBus' -Value $OutputBusPath
    $audioSources = @(Get-WwiseChildren -Path $soundPath | Where-Object { $_.type -eq 'AudioFileSource' })
    foreach ($duplicateSource in @($audioSources | Select-Object -Skip 1)) {
        Invoke-Waapi -Uri 'ak.wwise.core.object.delete' -Arguments @{ object = $duplicateSource.id } | Out-Null
    }

    Write-M08Log ("Imported {0} -> {1}; sourceSHA256={2}; convertedSHA256={3}; converter=FFmpeg; loop={4}; spatialized={5}" -f $SourceFileName, $soundPath, $hash, $convertedHash, $Loop, $Spatialized)
    return $soundPath
}

function Ensure-EventAction {
    param(
        [Parameter(Mandatory)][string]$EventPath,
        [Parameter(Mandatory)][ValidateSet(1, 2, 3)][int]$ActionType,
        [string]$TargetPath = ''
    )

    foreach ($child in (Get-WwiseChildren -Path $EventPath | Where-Object { $_.type -eq 'Action' })) {
        Invoke-Waapi -Uri 'ak.wwise.core.object.delete' -Arguments @{ object = $child.id } | Out-Null
    }

    $arguments = @{
        parent = $EventPath
        type = 'Action'
        name = ''
        onNameConflict = 'merge'
        autoAddToSourceControl = $false
        '@ActionType' = $ActionType
    }
    if (-not [string]::IsNullOrWhiteSpace($TargetPath)) {
        $arguments['@Target'] = $TargetPath
    }
    Invoke-Waapi -Uri 'ak.wwise.core.object.create' -Arguments $arguments | Out-Null
}

function Set-ObjectRtpcCurves {
    param(
        [Parameter(Mandatory)][string]$ObjectPath,
        [Parameter(Mandatory)][object[]]$Curves
    )

    $definitions = foreach ($curve in $Curves) {
        @{
            type = 'RTPC'
            name = ''
            notes = 'Milestone 08 prototype tuning; project-owned curve.'
            '@PropertyName' = $curve.Property
            '@ControlInput' = "\Game Parameters\Default Work Unit\$($curve.Parameter)"
            '@Curve' = @{
                type = 'Curve'
                points = @($curve.Points)
            }
        }
    }

    Invoke-Waapi -Uri 'ak.wwise.core.object.set' -Arguments @{
        listMode = 'replaceAll'
        onNameConflict = 'merge'
        autoAddToSourceControl = $false
        objects = @(@{
            object = $ObjectPath
            '@RTPC' = @($definitions)
        })
    } -Options @{
        return = @('id', 'name', 'type', '@RTPC')
    } | Out-Null
}

function New-Curve {
    param(
        [Parameter(Mandatory)][string]$Property,
        [Parameter(Mandatory)][string]$Parameter,
        [Parameter(Mandatory)][object[]]$Points
    )

    return [pscustomobject]@{
        Property = $Property
        Parameter = $Parameter
        Points = @($Points)
    }
}

function New-Point {
    param([double]$X, [double]$Y)
    return @{ x = $X; y = $Y; shape = 'Linear' }
}

if (-not (Test-Path -LiteralPath $WwiseProjectPath -PathType Leaf)) {
    throw "Wwise project not found: $WwiseProjectPath"
}
if (-not (Test-Path -LiteralPath $DonorAudioRoot -PathType Container)) {
    throw "Donor audio root not found: $DonorAudioRoot"
}

if ([string]::IsNullOrWhiteSpace($FfmpegPath)) {
    $ffmpegCommand = Get-Command ffmpeg -ErrorAction SilentlyContinue
    if ($ffmpegCommand) {
        $FfmpegPath = $ffmpegCommand.Source
    }
    else {
        $wingetPackageRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe'
        $FfmpegPath = (Get-ChildItem -LiteralPath $wingetPackageRoot -Recurse -File -Filter 'ffmpeg.exe' -ErrorAction SilentlyContinue | Select-Object -First 1).FullName
    }
}
if ([string]::IsNullOrWhiteSpace($FfmpegPath) -or -not (Test-Path -LiteralPath $FfmpegPath -PathType Leaf)) {
    throw 'FFmpeg was not found. Install winget package Gyan.FFmpeg or pass -FfmpegPath.'
}
$script:ResolvedFfmpegPath = (Resolve-Path -LiteralPath $FfmpegPath).Path
New-Item -ItemType Directory -Force -Path $ConvertedAudioRoot | Out-Null
$ffmpegVersion = (& $script:ResolvedFfmpegPath -version | Select-Object -First 1)

$info = Invoke-Waapi -Uri 'ak.wwise.core.getInfo'
Write-M08Log ("Connected to {0} {1}; process {2}; WAAPI={3}" -f $info.displayName, $info.version.displayName, $info.processId, $WaapiUri)
Write-M08Log "Wwise project: $WwiseProjectPath"
Write-M08Log "Donor audio root (read-only): $DonorAudioRoot"
Write-M08Log "Converter: $ffmpegVersion; executable=$script:ResolvedFfmpegPath"
Write-M08Log "Converted WAV staging (outside Git): $ConvertedAudioRoot"

$parameters = @(
    @{ Id='audio.parameter.mixer.master'; Name='MSC_Mixer_Master'; Min=0; Max=1; Default=1 },
    @{ Id='audio.parameter.mixer.vehicle'; Name='MSC_Mixer_Vehicle'; Min=0; Max=1; Default=1 },
    @{ Id='audio.parameter.mixer.effects'; Name='MSC_Mixer_Effects'; Min=0; Max=1; Default=1 },
    @{ Id='audio.parameter.mixer.ambience'; Name='MSC_Mixer_Ambience'; Min=0; Max=1; Default=1 },
    @{ Id='audio.parameter.mixer.music'; Name='MSC_Mixer_Music'; Min=0; Max=1; Default=1 },
    @{ Id='audio.parameter.mixer.ui'; Name='MSC_Mixer_UI'; Min=0; Max=1; Default=1 },
    @{ Id='audio.parameter.vehicle.rpm'; Name='MSC_Vehicle_RPM'; Min=0; Max=9000; Default=0 },
    @{ Id='audio.parameter.vehicle.rpm.normalized'; Name='MSC_Vehicle_RPM_Normalized'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.engine_load'; Name='MSC_Vehicle_EngineLoad'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.throttle'; Name='MSC_Vehicle_Throttle'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.gear'; Name='MSC_Vehicle_Gear'; Min=-1; Max=6; Default=0 },
    @{ Id='audio.parameter.vehicle.clutch_slip'; Name='MSC_Vehicle_ClutchSlipRPM'; Min=0; Max=9000; Default=0 },
    @{ Id='audio.parameter.vehicle.speed'; Name='MSC_Vehicle_Speed'; Min=0; Max=80; Default=0 },
    @{ Id='audio.parameter.vehicle.wheel_speed_radps'; Name='MSC_Vehicle_WheelSpeedRadPS'; Min=0; Max=250; Default=0 },
    @{ Id='audio.parameter.vehicle.wheel_slip'; Name='MSC_Vehicle_WheelSlip'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.brake'; Name='MSC_Vehicle_Brake'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.suspension_impact'; Name='MSC_Vehicle_SuspensionImpact'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.battery_voltage'; Name='MSC_Vehicle_BatteryVoltage'; Min=0; Max=18; Default=12.6 },
    @{ Id='audio.parameter.vehicle.engine_temperature'; Name='MSC_Vehicle_EngineTemperature'; Min=-50; Max=180; Default=20 },
    @{ Id='audio.parameter.vehicle.damage'; Name='MSC_Vehicle_Damage'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.interior_blend'; Name='MSC_Vehicle_InteriorBlend'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.door_openness'; Name='MSC_Vehicle_DoorOpenness'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.vehicle.window_openness'; Name='MSC_Vehicle_WindowOpenness'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.weather.precipitation'; Name='MSC_Weather_Precipitation'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.weather.wind'; Name='MSC_Weather_Wind'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.weather.thunder_risk'; Name='MSC_Weather_ThunderRisk'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.environment.shelter'; Name='MSC_Environment_Shelter'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.environment.time_of_day'; Name='MSC_Environment_TimeOfDay'; Min=0; Max=1; Default=0.5 },
    @{ Id='audio.parameter.lightning.intensity'; Name='MSC_Lightning_Intensity'; Min=0; Max=1; Default=0 },
    @{ Id='audio.parameter.lightning.distance'; Name='MSC_Lightning_DistanceMeters'; Min=0; Max=5000; Default=0 },
    @{ Id='audio.parameter.lightning.delay'; Name='MSC_Lightning_DelaySeconds'; Min=0; Max=20; Default=0 },
    @{ Id='audio.parameter.interaction.impact_intensity'; Name='MSC_Interaction_ImpactIntensity'; Min=0; Max=1; Default=0 }
)

$existingParameters = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ ofType = @('GameParameter') } } -Options @{ return = @('id','name','path') }
$desiredParameterNames = @($parameters | ForEach-Object { $_.Name })
foreach ($obsoleteParameter in @($existingParameters.return | Where-Object { $_.name -like 'MSC_*' -and $_.name -notin $desiredParameterNames })) {
    Invoke-Waapi -Uri 'ak.wwise.core.object.delete' -Arguments @{ object = $obsoleteParameter.id } | Out-Null
    Write-M08Log "Removed obsolete Milestone 08 Game Parameter: $($obsoleteParameter.name)"
}

foreach ($parameter in $parameters) {
    Ensure-GameParameter -Name $parameter.Name -Minimum $parameter.Min -Maximum $parameter.Max -DefaultValue $parameter.Default -StableId $parameter.Id
}
Write-M08Log "Configured $($parameters.Count) Game Parameters."

$mainAudioBus = '\Busses\Default Work Unit\Main Audio Bus'
$masterBus = (Ensure-WwiseObject -Parent $mainAudioBus -Type 'Bus' -Name 'MSC_Master' -Notes 'Project-owned master audio bus.').path
$vehicleBus = (Ensure-WwiseObject -Parent $masterBus -Type 'Bus' -Name 'Vehicle' -Notes 'Vehicle category bus.').path
$effectsBus = (Ensure-WwiseObject -Parent $masterBus -Type 'Bus' -Name 'Effects' -Notes 'Interaction and gameplay effects category bus.').path
$ambienceBus = (Ensure-WwiseObject -Parent $masterBus -Type 'Bus' -Name 'Ambience' -Notes 'Weather and world ambience category bus.').path
$musicBus = (Ensure-WwiseObject -Parent $masterBus -Type 'Bus' -Name 'Music' -Notes 'Reserved project music category bus.').path
$uiBus = (Ensure-WwiseObject -Parent $masterBus -Type 'Bus' -Name 'UI' -Notes 'Project UI category bus; Milestone 08 has no UI media.').path

$mixerBusBindings = @(
    @{ Bus=$masterBus; Parameter='MSC_Mixer_Master'; MaximumDb=4 },
    @{ Bus=$vehicleBus; Parameter='MSC_Mixer_Vehicle'; MaximumDb=0 },
    @{ Bus=$effectsBus; Parameter='MSC_Mixer_Effects'; MaximumDb=0 },
    @{ Bus=$ambienceBus; Parameter='MSC_Mixer_Ambience'; MaximumDb=0 },
    @{ Bus=$musicBus; Parameter='MSC_Mixer_Music'; MaximumDb=0 },
    @{ Bus=$uiBus; Parameter='MSC_Mixer_UI'; MaximumDb=0 }
)
foreach ($binding in $mixerBusBindings) {
    Set-ObjectRtpcCurves -ObjectPath $binding.Bus -Curves @(
        (New-Curve -Property 'Volume' -Parameter $binding.Parameter -Points @((New-Point 0 -96), (New-Point 1 $binding.MaximumDb)))
    )
}
Write-M08Log 'Configured MSC_Master/{Vehicle,Effects,Ambience,Music,UI} and six mixer RTPC-to-bus Volume curves (master ceiling +4 dB).'

Ensure-GameSyncGroup -Type SwitchGroup -Name 'MSC_Vehicle_Surface' -Values @('Unknown','Paved','Gravel','Dirt','Grass','MudWet') -StableId 'audio.switch.vehicle.surface'
Ensure-GameSyncGroup -Type SwitchGroup -Name 'MSC_Weather_PrecipitationType' -Values @('None_01','Drizzle','Rain','Snow') -StableId 'audio.switch.weather.precipitation_type (Wwise reserves None, so stable None maps to authoring value None_01)'
Ensure-GameSyncGroup -Type SwitchGroup -Name 'MSC_Interaction_Material' -Values @('Unknown','Metal','Wood','Plastic','Glass','Concrete','Fabric') -StableId 'audio.switch.interaction.material'
Ensure-GameSyncGroup -Type SwitchGroup -Name 'MSC_Player_FootstepSurface' -Values @('Unknown','Paved','Gravel','Dirt','Grass','Wood','Concrete','Metal','Wet') -StableId 'audio.switch.footstep.surface'
Ensure-GameSyncGroup -Type StateGroup -Name 'MSC_Environment' -Values @('Exterior','Sheltered','Interior','VehicleInterior') -StableId 'audio.state.environment'
Ensure-GameSyncGroup -Type StateGroup -Name 'MSC_Time_DayPhase' -Values @('Dawn','Day','Evening','Night') -StableId 'audio.state.day_phase'
Ensure-GameSyncGroup -Type StateGroup -Name 'MSC_Vehicle_EngineState' -Values @('Off','Cranking','Running','Stalled') -StableId 'audio.state.vehicle.engine'
Write-M08Log 'Configured four Switch Groups and three State Groups.'

$actorRoot = (Ensure-WwiseObject -Parent '\Actor-Mixer Hierarchy\Default Work Unit' -Type 'ActorMixer' -Name 'MSC_Prototype' -Notes 'Milestone 08 private prototype. Donor media is TemporaryDirectImport and excluded from Git.').path
$vehicleRoot = (Ensure-WwiseObject -Parent $actorRoot -Type 'ActorMixer' -Name 'Vehicle' -Notes 'Vehicle prototype layers.').path
$weatherRoot = (Ensure-WwiseObject -Parent $actorRoot -Type 'ActorMixer' -Name 'Weather' -Notes 'Weather prototype layers.').path
$worldRoot = (Ensure-WwiseObject -Parent $actorRoot -Type 'ActorMixer' -Name 'World' -Notes 'World ambience prototype layers.').path
$interactionRoot = (Ensure-WwiseObject -Parent $actorRoot -Type 'ActorMixer' -Name 'Interaction' -Notes 'Interaction prototype one-shots.').path
$musicRoot = (Ensure-WwiseObject -Parent $actorRoot -Type 'ActorMixer' -Name 'Music' -Notes 'Reserved project music routing root; no Milestone 08 media.').path
$uiRoot = (Ensure-WwiseObject -Parent $actorRoot -Type 'ActorMixer' -Name 'UI' -Notes 'Reserved project UI routing root; no donor gameplay media.').path

Set-WwiseReference -Object $actorRoot -Reference 'OutputBus' -Value $masterBus
Set-WwiseReference -Object $vehicleRoot -Reference 'OutputBus' -Value $vehicleBus
Set-WwiseReference -Object $weatherRoot -Reference 'OutputBus' -Value $ambienceBus
Set-WwiseReference -Object $worldRoot -Reference 'OutputBus' -Value $ambienceBus
Set-WwiseReference -Object $interactionRoot -Reference 'OutputBus' -Value $effectsBus
Set-WwiseReference -Object $musicRoot -Reference 'OutputBus' -Value $musicBus
Set-WwiseReference -Object $uiRoot -Reference 'OutputBus' -Value $uiBus
Write-M08Log 'Routed Vehicle->Vehicle, Weather/World->Ambience, Interaction->Effects, Music->Music and UI->UI buses.'

$engineLayers = (Ensure-WwiseObject -Parent $vehicleRoot -Type 'ActorMixer' -Name 'Engine_Layers' -Notes 'Layered engine prototype target.').path
$engineStart = (Ensure-WwiseObject -Parent $vehicleRoot -Type 'RandomSequenceContainer' -Name 'Engine_Start' -Notes 'Temporary donor start variations.').path
Set-WwiseProperty -Object $engineStart -Property 'RandomOrSequence' -Value 1
$thunder = (Ensure-WwiseObject -Parent $weatherRoot -Type 'RandomSequenceContainer' -Name 'Thunder' -Notes 'Temporary donor lightning variations.').path
Set-WwiseProperty -Object $thunder -Property 'RandomOrSequence' -Value 1
$mountHandoff = (Ensure-WwiseObject -Parent $interactionRoot -Type 'RandomSequenceContainer' -Name 'Mount_Handoff' -Notes 'Temporary proxy using door movement clips.').path
Set-WwiseProperty -Object $mountHandoff -Property 'RandomOrSequence' -Value 1
$footsteps = (Ensure-WwiseObject -Parent $interactionRoot -Type 'SwitchContainer' -Name 'MSC_Player_Footsteps' -Notes 'Temporary donor footstep prototype selected by project-owned surface metadata.').path
$footstepPairs = @{}
foreach ($surfaceName in @('Concrete','Forest','Gravel','Wet','Wood')) {
    $footstepPairs[$surfaceName] = (Ensure-WwiseObject -Parent $footsteps -Type 'RandomSequenceContainer' -Name $surfaceName -Notes "Temporary donor $surfaceName footstep variations.").path
    Set-WwiseProperty -Object $footstepPairs[$surfaceName] -Property 'RandomOrSequence' -Value 1
}

Set-WwiseReference -Object $engineLayers -Reference 'OutputBus' -Value $vehicleBus
Set-WwiseReference -Object $engineStart -Reference 'OutputBus' -Value $vehicleBus
Set-WwiseReference -Object $thunder -Reference 'OutputBus' -Value $ambienceBus
Set-WwiseReference -Object $mountHandoff -Reference 'OutputBus' -Value $effectsBus
Set-WwiseReference -Object $footsteps -Reference 'OutputBus' -Value $effectsBus
foreach ($pairPath in $footstepPairs.Values) {
    Set-WwiseReference -Object $pairPath -Reference 'OutputBus' -Value $effectsBus
}

$footstepSwitchGroup = '\Switches\Default Work Unit\MSC_Player_FootstepSurface'
$footstepAssignments = @(
    @{ Child='Concrete'; Switch='Unknown' },
    @{ Child='Concrete'; Switch='Paved' },
    @{ Child='Concrete'; Switch='Concrete' },
    @{ Child='Concrete'; Switch='Metal' },
    @{ Child='Forest'; Switch='Dirt' },
    @{ Child='Forest'; Switch='Grass' },
    @{ Child='Gravel'; Switch='Gravel' },
    @{ Child='Wet'; Switch='Wet' },
    @{ Child='Wood'; Switch='Wood' }
)
Set-SwitchContainerAssignments -ContainerPath $footsteps -SwitchGroupPath $footstepSwitchGroup -Assignments $footstepAssignments

$sounds = @{}
$sounds.VehicleStarter = Import-PrototypeSound -ParentPath $vehicleRoot -SoundName 'Starter_Whine' -SourceFileName 'starter_whine.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $true -Spatialized $true
$sounds.VehicleStart1 = Import-PrototypeSound -ParentPath $engineStart -SoundName 'Motor_Start_1' -SourceFileName 'motor_start_1.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $false -Spatialized $true
$sounds.VehicleStart2 = Import-PrototypeSound -ParentPath $engineStart -SoundName 'Motor_Start_2' -SourceFileName 'motor_start_2.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $false -Spatialized $true
$sounds.VehicleStart3 = Import-PrototypeSound -ParentPath $engineStart -SoundName 'Motor_Start_3' -SourceFileName 'motor_start_3.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $false -Spatialized $true
$sounds.VehicleStall = Import-PrototypeSound -ParentPath $vehicleRoot -SoundName 'Engine_Stall_Proxy' -SourceFileName 'motor_start_3.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $false -Spatialized $true
$sounds.VehicleIntake = Import-PrototypeSound -ParentPath $engineLayers -SoundName 'Engine_Intake' -SourceFileName '850_mid3.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $true -Spatialized $true
$sounds.VehicleExhaust = Import-PrototypeSound -ParentPath $engineLayers -SoundName 'Engine_Exhaust' -SourceFileName '850_mid13.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $true -Spatialized $true
$sounds.VehicleMechanical = Import-PrototypeSound -ParentPath $engineLayers -SoundName 'Engine_Mechanical' -SourceFileName '850_idle5.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $true -Spatialized $true
$sounds.VehicleTireRoll = Import-PrototypeSound -ParentPath $vehicleRoot -SoundName 'Tire_Roll' -SourceFileName 'gravel3.ogg' -Domain 'Vehicle' -OutputBusPath $vehicleBus -Loop $true -Spatialized $true

$sounds.RainExterior = Import-PrototypeSound -ParentPath $weatherRoot -SoundName 'Rain_Exterior' -SourceFileName 'rain_3.ogg' -Domain 'Weather' -OutputBusPath $ambienceBus -Loop $true -Spatialized $false
$sounds.RainSheltered = Import-PrototypeSound -ParentPath $weatherRoot -SoundName 'Rain_Sheltered' -SourceFileName 'rain_3.ogg' -Domain 'Weather' -OutputBusPath $ambienceBus -Loop $true -Spatialized $false
$sounds.RainInterior = Import-PrototypeSound -ParentPath $weatherRoot -SoundName 'Rain_Interior' -SourceFileName 'rain_3.ogg' -Domain 'Weather' -OutputBusPath $ambienceBus -Loop $true -Spatialized $false
$sounds.Wind = Import-PrototypeSound -ParentPath $weatherRoot -SoundName 'Wind' -SourceFileName 'wind.ogg' -Domain 'Weather' -OutputBusPath $ambienceBus -Loop $true -Spatialized $false
foreach ($index in 1..4) {
    $sounds["Thunder$index"] = Import-PrototypeSound -ParentPath $thunder -SoundName "Lightning_Strike_$index" -SourceFileName "lightning_strike_$index.ogg" -Domain 'Weather' -OutputBusPath $ambienceBus -Loop $false -Spatialized $true
}

$sounds.Forest = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Forest_Ambience' -SourceFileName 'birds_day.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.BirdsMorning = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Birds_Morning' -SourceFileName 'birds_morning.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.BirdsDay = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Birds_Day' -SourceFileName 'birds_day.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.BirdsEvening = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Birds_Evening' -SourceFileName 'birds_evening.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.BirdsNight = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Birds_Night' -SourceFileName 'birds_night.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.BirdsSwamp = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Birds_Swamp' -SourceFileName 'birds_swamp.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Meadow = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Meadow' -SourceFileName 'meadow.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Dog = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Distant_Dog' -SourceFileName 'dog.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Lake = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Lake_Ambience' -SourceFileName 'lake_water.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Chainsaw = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Distant_Chainsaw' -SourceFileName 'chainsaw.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $false -Spatialized $true
$sounds.WindChime = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Wind_Chime' -SourceFileName 'wind_chime.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Mosquito = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Mosquito' -SourceFileName 'mosquito.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Fly = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Fly' -SourceFileName 'fly.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.FlyVariant = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Fly_Variant' -SourceFileName 'fly2.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Wasp = Import-PrototypeSound -ParentPath $worldRoot -SoundName 'Wasp' -SourceFileName 'wasp_fly2.ogg' -Domain 'World' -OutputBusPath $ambienceBus -Loop $true -Spatialized $true
$sounds.Pickup = Import-PrototypeSound -ParentPath $interactionRoot -SoundName 'Pickup' -SourceFileName 'wood_hit1.ogg' -Domain 'Interaction' -OutputBusPath $effectsBus -Loop $false -Spatialized $true
$sounds.Drop = Import-PrototypeSound -ParentPath $interactionRoot -SoundName 'Drop' -SourceFileName 'player_impact.ogg' -Domain 'Interaction' -OutputBusPath $effectsBus -Loop $false -Spatialized $true
$sounds.Throw = Import-PrototypeSound -ParentPath $interactionRoot -SoundName 'Throw' -SourceFileName 'player_impact.ogg' -Domain 'Interaction' -OutputBusPath $effectsBus -Loop $false -Spatialized $true
$sounds.Place = Import-PrototypeSound -ParentPath $interactionRoot -SoundName 'Place' -SourceFileName 'wood_hit1.ogg' -Domain 'Interaction' -OutputBusPath $effectsBus -Loop $false -Spatialized $true
$sounds.DoorOpen = Import-PrototypeSound -ParentPath $mountHandoff -SoundName 'Door_Open_Proxy' -SourceFileName 'door_open.ogg' -Domain 'Interaction' -OutputBusPath $effectsBus -Loop $false -Spatialized $true
$sounds.DoorClose = Import-PrototypeSound -ParentPath $mountHandoff -SoundName 'Door_Close_Proxy' -SourceFileName 'door_close.ogg' -Domain 'Interaction' -OutputBusPath $effectsBus -Loop $false -Spatialized $true

$footstepSounds = @{}
foreach ($footstepDefinition in @(
    @{ Key='Concrete1'; Pair='Concrete'; Sound='Footstep_Concrete_1'; Source='footstep_concrete1.wav' },
    @{ Key='Concrete2'; Pair='Concrete'; Sound='Footstep_Concrete_2'; Source='footstep_concrete2.wav' },
    @{ Key='Forest1'; Pair='Forest'; Sound='Footstep_Forest_1'; Source='footstep_forest1.wav' },
    @{ Key='Forest2'; Pair='Forest'; Sound='Footstep_Forest_2'; Source='footstep_forest2.wav' },
    @{ Key='Gravel1'; Pair='Gravel'; Sound='Footstep_Gravel_1'; Source='footstep_gravel1.wav' },
    @{ Key='Gravel2'; Pair='Gravel'; Sound='Footstep_Gravel_2'; Source='footstep_gravel2.wav' },
    @{ Key='Wet1'; Pair='Wet'; Sound='Footstep_Wet_1'; Source='footstep_wet1.wav' },
    @{ Key='Wet2'; Pair='Wet'; Sound='Footstep_Wet_2'; Source='footstep_wet2.wav' },
    @{ Key='Wood1'; Pair='Wood'; Sound='Footstep_Wood_1'; Source='footstep_wood1.wav' },
    @{ Key='Wood2'; Pair='Wood'; Sound='Footstep_Wood_2'; Source='footstep_wood2.wav' }
)) {
    $footstepSounds[$footstepDefinition.Key] = Import-PrototypeSound -ParentPath $footstepPairs[$footstepDefinition.Pair] -SoundName $footstepDefinition.Sound -SourceFileName $footstepDefinition.Source -Domain 'Interaction\Footsteps' -OutputBusPath $effectsBus -Loop $false -Spatialized $true
}

$routingExpectations = @(
    @{ Path=$actorRoot; Bus=$masterBus },
    @{ Path=$vehicleRoot; Bus=$vehicleBus },
    @{ Path=$weatherRoot; Bus=$ambienceBus },
    @{ Path=$worldRoot; Bus=$ambienceBus },
    @{ Path=$interactionRoot; Bus=$effectsBus },
    @{ Path=$musicRoot; Bus=$musicBus },
    @{ Path=$uiRoot; Bus=$uiBus }
)
foreach ($objectPath in @($engineLayers, $engineStart, $sounds.VehicleStarter, $sounds.VehicleStart1, $sounds.VehicleStart2, $sounds.VehicleStart3, $sounds.VehicleStall, $sounds.VehicleIntake, $sounds.VehicleExhaust, $sounds.VehicleMechanical, $sounds.VehicleTireRoll)) {
    $routingExpectations += @{ Path=$objectPath; Bus=$vehicleBus }
}
foreach ($objectPath in @($thunder, $sounds.RainExterior, $sounds.RainSheltered, $sounds.RainInterior, $sounds.Wind, $sounds.Thunder1, $sounds.Thunder2, $sounds.Thunder3, $sounds.Thunder4, $sounds.Forest, $sounds.BirdsMorning, $sounds.BirdsDay, $sounds.BirdsEvening, $sounds.BirdsNight, $sounds.BirdsSwamp, $sounds.Meadow, $sounds.Dog, $sounds.Lake, $sounds.Chainsaw, $sounds.WindChime, $sounds.Mosquito, $sounds.Fly, $sounds.FlyVariant, $sounds.Wasp)) {
    $routingExpectations += @{ Path=$objectPath; Bus=$ambienceBus }
}
foreach ($objectPath in @($mountHandoff, $footsteps, $sounds.Pickup, $sounds.Drop, $sounds.Throw, $sounds.Place, $sounds.DoorOpen, $sounds.DoorClose) + @($footstepPairs.Values) + @($footstepSounds.Values)) {
    $routingExpectations += @{ Path=$objectPath; Bus=$effectsBus }
}

Set-ObjectRtpcCurves -ObjectPath $sounds.VehicleIntake -Curves @(
    (New-Curve -Property 'Volume' -Parameter 'MSC_Vehicle_RPM_Normalized' -Points @((New-Point 0 -14), (New-Point 0.3 -4), (New-Point 1 0))),
    (New-Curve -Property 'Pitch' -Parameter 'MSC_Vehicle_RPM_Normalized' -Points @((New-Point 0 -300), (New-Point 1 500)))
)
Set-ObjectRtpcCurves -ObjectPath $sounds.VehicleExhaust -Curves @(
    (New-Curve -Property 'Volume' -Parameter 'MSC_Vehicle_RPM_Normalized' -Points @((New-Point 0 -18), (New-Point 0.35 -5), (New-Point 1 2))),
    (New-Curve -Property 'Pitch' -Parameter 'MSC_Vehicle_RPM_Normalized' -Points @((New-Point 0 -250), (New-Point 1 450)))
)
Set-ObjectRtpcCurves -ObjectPath $sounds.VehicleMechanical -Curves @(
    (New-Curve -Property 'Volume' -Parameter 'MSC_Vehicle_RPM_Normalized' -Points @((New-Point 0 0), (New-Point 1 -8))),
    (New-Curve -Property 'Pitch' -Parameter 'MSC_Vehicle_RPM_Normalized' -Points @((New-Point 0 -100), (New-Point 1 250)))
)
Set-ObjectRtpcCurves -ObjectPath $sounds.VehicleTireRoll -Curves @(
    (New-Curve -Property 'Volume' -Parameter 'MSC_Vehicle_Speed' -Points @((New-Point 0 -96), (New-Point 2 -18), (New-Point 40 0), (New-Point 80 2))),
    (New-Curve -Property 'Pitch' -Parameter 'MSC_Vehicle_Speed' -Points @((New-Point 0 -350), (New-Point 80 350)))
)
foreach ($rainSound in @($sounds.RainExterior, $sounds.RainSheltered, $sounds.RainInterior)) {
    Set-ObjectRtpcCurves -ObjectPath $rainSound -Curves @(
        (New-Curve -Property 'Volume' -Parameter 'MSC_Weather_Precipitation' -Points @((New-Point 0 -96), (New-Point 0.1 -16), (New-Point 1 0)))
    )
}
Set-WwiseProperty -Object $sounds.RainSheltered -Property 'Volume' -Value -8
Set-WwiseProperty -Object $sounds.RainInterior -Property 'Volume' -Value -16
Set-WwiseProperty -Object $sounds.BirdsMorning -Property 'Volume' -Value -3
Set-WwiseProperty -Object $sounds.BirdsDay -Property 'Volume' -Value -3
Set-WwiseProperty -Object $sounds.BirdsEvening -Property 'Volume' -Value -3
Set-WwiseProperty -Object $sounds.BirdsNight -Property 'Volume' -Value -2
Set-WwiseProperty -Object $sounds.BirdsSwamp -Property 'Volume' -Value -5
Set-WwiseProperty -Object $sounds.Meadow -Property 'Volume' -Value -2
Set-WwiseProperty -Object $sounds.Dog -Property 'Volume' -Value -8
Set-WwiseProperty -Object $sounds.Lake -Property 'Volume' -Value -12
Set-WwiseProperty -Object $sounds.WindChime -Property 'Volume' -Value -4
Set-WwiseProperty -Object $sounds.Mosquito -Property 'Volume' -Value -5
Set-WwiseProperty -Object $sounds.Fly -Property 'Volume' -Value -6
Set-WwiseProperty -Object $sounds.FlyVariant -Property 'Volume' -Value -6
Set-WwiseProperty -Object $sounds.Wasp -Property 'Volume' -Value -6
Set-ObjectRtpcCurves -ObjectPath $sounds.Wind -Curves @(
    (New-Curve -Property 'Volume' -Parameter 'MSC_Weather_Wind' -Points @((New-Point 0 -96), (New-Point 0.1 -20), (New-Point 1 0))),
    (New-Curve -Property 'Volume' -Parameter 'MSC_Environment_Shelter' -Points @((New-Point 0 0), (New-Point 1 -24)))
)
Set-ObjectRtpcCurves -ObjectPath $thunder -Curves @(
    (New-Curve -Property 'Volume' -Parameter 'MSC_Lightning_Intensity' -Points @((New-Point 0 -24), (New-Point 1 0)))
)
foreach ($thunderSound in @($sounds.Thunder1, $sounds.Thunder2, $sounds.Thunder3, $sounds.Thunder4)) {
    Set-ObjectRtpcCurves -ObjectPath $thunderSound -Curves @(
        (New-Curve -Property 'Volume' -Parameter 'MSC_Environment_Shelter' -Points @((New-Point 0 0), (New-Point 1 -14)))
    )
}
Write-M08Log 'Configured prototype RTPC curves for engine layers, tire roll, rain, wind plus -24 dB shelter attenuation, lightning intensity and thunder shelter attenuation.'

$eventDefinitions = @(
    @{ Id='audio.event.vehicle.starter.engaged'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Starter_Engage'; Bank='MSC_Vehicle'; Action=1; Target=$sounds.VehicleStarter },
    @{ Id='audio.event.vehicle.starter.disengaged'; Domain='MSC_Vehicle'; Name='Stop_MSC_Vehicle_Starter'; Bank='MSC_Vehicle'; Action=2; Target=$sounds.VehicleStarter },
    @{ Id='audio.event.vehicle.engine.started'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Engine_Start'; Bank='MSC_Vehicle'; Action=1; Target=$engineStart },
    @{ Id='audio.event.vehicle.engine.stopped'; Domain='MSC_Vehicle'; Name='Stop_MSC_Vehicle_Engine'; Bank='MSC_Vehicle'; Action=2; Target=$engineLayers },
    @{ Id='audio.event.vehicle.engine.stalled'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Engine_Stall'; Bank='MSC_Vehicle'; Action=1; Target=$sounds.VehicleStall },
    @{ Id='audio.event.vehicle.reset'; Domain='MSC_Vehicle'; Name='Stop_MSC_Vehicle_All'; Bank='MSC_Vehicle'; Action=3; Target='' },
    @{ Id='audio.event.vehicle.engine.intake'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Engine_Intake'; Bank='MSC_Vehicle'; Action=1; Target=$sounds.VehicleIntake },
    @{ Id='audio.event.vehicle.engine.exhaust'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Engine_Exhaust'; Bank='MSC_Vehicle'; Action=1; Target=$sounds.VehicleExhaust },
    @{ Id='audio.event.vehicle.engine.mechanical'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Engine_Mechanical'; Bank='MSC_Vehicle'; Action=1; Target=$sounds.VehicleMechanical },
    @{ Id='audio.event.vehicle.transmission.loop'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Transmission'; Bank='MSC_Vehicle'; Action=0; Target='' },
    @{ Id='audio.event.vehicle.body.rattle'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Body_Rattle'; Bank='MSC_Vehicle'; Action=0; Target='' },
    @{ Id='audio.event.vehicle.suspension.impact'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Suspension_Impact'; Bank='MSC_Vehicle'; Action=0; Target='' },
    @{ Id='audio.event.vehicle.tire.roll'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Tire_Roll'; Bank='MSC_Vehicle'; Action=1; Target=$sounds.VehicleTireRoll },
    @{ Id='audio.event.vehicle.tire.skid'; Domain='MSC_Vehicle'; Name='Play_MSC_Vehicle_Tire_Skid'; Bank='MSC_Vehicle'; Action=0; Target='' },
    @{ Id='audio.event.weather.rain.exterior'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Rain_Exterior'; Bank='MSC_Weather'; Action=1; Target=$sounds.RainExterior },
    @{ Id='audio.event.weather.rain.sheltered'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Rain_Sheltered'; Bank='MSC_Weather'; Action=1; Target=$sounds.RainSheltered },
    @{ Id='audio.event.weather.rain.interior'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Rain_Interior'; Bank='MSC_Weather'; Action=1; Target=$sounds.RainInterior },
    @{ Id='audio.event.weather.wind'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Wind'; Bank='MSC_Weather'; Action=1; Target=$sounds.Wind },
    @{ Id='audio.event.weather.thunder'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Thunder'; Bank='MSC_Weather'; Action=1; Target=$thunder },
    @{ Id='audio.event.weather.thunder.distant'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Thunder_Distant'; Bank='MSC_Weather'; Action=1; Target=$thunder },
    @{ Id='audio.event.weather.thunder.strike'; Domain='MSC_Weather'; Name='Play_MSC_Weather_Thunder_Strike'; Bank='MSC_Weather'; Action=1; Target=$thunder },
    @{ Id='audio.event.world.forest.ambience'; Domain='MSC_World'; Name='Play_MSC_World_Forest_Ambience'; Bank='MSC_World'; Action=1; Target=$sounds.Forest },
    @{ Id='audio.event.world.lake.ambience'; Domain='MSC_World'; Name='Play_MSC_World_Lake_Ambience'; Bank='MSC_World'; Action=1; Target=$sounds.Lake },
    @{ Id='audio.event.world.garage.roomtone'; Domain='MSC_World'; Name='Play_MSC_World_Garage_RoomTone'; Bank='MSC_World'; Action=0; Target='' },
    @{ Id='audio.event.world.interior.roomtone'; Domain='MSC_World'; Name='Play_MSC_World_Interior_RoomTone'; Bank='MSC_World'; Action=0; Target='' },
    @{ Id='audio.event.world.distant_traffic'; Domain='MSC_World'; Name='Play_MSC_World_Distant_Traffic'; Bank='MSC_World'; Action=0; Target='' },
    @{ Id='audio.event.world.birds'; Domain='MSC_World'; Name='Play_MSC_World_Birds'; Bank='MSC_World'; Action=1; Target=$sounds.Forest },
    @{ Id='audio.event.world.birds.morning'; Domain='MSC_World'; Name='Play_MSC_World_Birds_Morning'; Bank='MSC_World'; Action=1; Target=$sounds.BirdsMorning },
    @{ Id='audio.event.world.birds.day'; Domain='MSC_World'; Name='Play_MSC_World_Birds_Day'; Bank='MSC_World'; Action=1; Target=$sounds.BirdsDay },
    @{ Id='audio.event.world.birds.evening'; Domain='MSC_World'; Name='Play_MSC_World_Birds_Evening'; Bank='MSC_World'; Action=1; Target=$sounds.BirdsEvening },
    @{ Id='audio.event.world.birds.night'; Domain='MSC_World'; Name='Play_MSC_World_Birds_Night'; Bank='MSC_World'; Action=1; Target=$sounds.BirdsNight },
    @{ Id='audio.event.world.birds.swamp'; Domain='MSC_World'; Name='Play_MSC_World_Birds_Swamp'; Bank='MSC_World'; Action=1; Target=$sounds.BirdsSwamp },
    @{ Id='audio.event.world.meadow'; Domain='MSC_World'; Name='Play_MSC_World_Meadow'; Bank='MSC_World'; Action=1; Target=$sounds.Meadow },
    @{ Id='audio.event.world.dog'; Domain='MSC_World'; Name='Play_MSC_World_Dog'; Bank='MSC_World'; Action=1; Target=$sounds.Dog },
    @{ Id='audio.event.world.chainsaw'; Domain='MSC_World'; Name='Play_MSC_World_Chainsaw'; Bank='MSC_World'; Action=1; Target=$sounds.Chainsaw },
    @{ Id='audio.event.world.insects'; Domain='MSC_World'; Name='Play_MSC_World_Insects'; Bank='MSC_World'; Action=1; Target=$sounds.Mosquito },
    @{ Id='audio.event.world.wind_chime'; Domain='MSC_World'; Name='Play_MSC_World_Wind_Chime'; Bank='MSC_World'; Action=1; Target=$sounds.WindChime },
    @{ Id='audio.event.world.mosquito'; Domain='MSC_World'; Name='Play_MSC_World_Mosquito'; Bank='MSC_World'; Action=1; Target=$sounds.Mosquito },
    @{ Id='audio.event.world.fly'; Domain='MSC_World'; Name='Play_MSC_World_Fly'; Bank='MSC_World'; Action=1; Target=$sounds.Fly },
    @{ Id='audio.event.world.fly.variant'; Domain='MSC_World'; Name='Play_MSC_World_Fly_Variant'; Bank='MSC_World'; Action=1; Target=$sounds.FlyVariant },
    @{ Id='audio.event.world.wasp'; Domain='MSC_World'; Name='Play_MSC_World_Wasp'; Bank='MSC_World'; Action=1; Target=$sounds.Wasp },
    @{ Id='audio.event.interaction.pickup'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Pickup'; Bank='MSC_Interaction'; Action=1; Target=$sounds.Pickup },
    @{ Id='audio.event.interaction.drop'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Drop'; Bank='MSC_Interaction'; Action=1; Target=$sounds.Drop },
    @{ Id='audio.event.interaction.throw'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Throw'; Bank='MSC_Interaction'; Action=1; Target=$sounds.Throw },
    @{ Id='audio.event.interaction.place'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Place'; Bank='MSC_Interaction'; Action=1; Target=$sounds.Place },
    @{ Id='audio.event.interaction.mount_handoff'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Mount_Handoff'; Bank='MSC_Interaction'; Action=1; Target=$mountHandoff },
    @{ Id='audio.event.interaction.impact'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Impact'; Bank='MSC_Interaction'; Action=1; Target=$sounds.Drop },
    @{ Id='audio.event.interaction.tool.use'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Tool_Use'; Bank='MSC_Interaction'; Action=0; Target='' },
    @{ Id='audio.event.interaction.fastener.insert'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Fastener_Insert'; Bank='MSC_Interaction'; Action=0; Target='' },
    @{ Id='audio.event.interaction.fastener.tighten'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Fastener_Tighten'; Bank='MSC_Interaction'; Action=0; Target='' },
    @{ Id='audio.event.interaction.fastener.loosen'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Fastener_Loosen'; Bank='MSC_Interaction'; Action=0; Target='' },
    @{ Id='audio.event.interaction.part.install'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Part_Install'; Bank='MSC_Interaction'; Action=0; Target='' },
    @{ Id='audio.event.interaction.part.remove'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Part_Remove'; Bank='MSC_Interaction'; Action=0; Target='' },
    @{ Id='audio.event.interaction.door.open'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Door_Open'; Bank='MSC_Interaction'; Action=1; Target=$sounds.DoorOpen },
    @{ Id='audio.event.interaction.door.close'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Door_Close'; Bank='MSC_Interaction'; Action=1; Target=$sounds.DoorClose },
    @{ Id='audio.event.interaction.gate.open'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Gate_Open'; Bank='MSC_Interaction'; Action=1; Target=$sounds.DoorOpen },
    @{ Id='audio.event.interaction.gate.close'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Gate_Close'; Bank='MSC_Interaction'; Action=1; Target=$sounds.DoorClose },
    @{ Id='audio.event.interaction.window.open'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Window_Open'; Bank='MSC_Interaction'; Action=1; Target=$sounds.DoorOpen },
    @{ Id='audio.event.interaction.window.close'; Domain='MSC_Interaction'; Name='Play_MSC_Interaction_Window_Close'; Bank='MSC_Interaction'; Action=1; Target=$sounds.DoorClose },
    @{ Id='audio.event.player.footstep'; Domain='MSC_Interaction'; Name='Play_MSC_Player_Footstep'; Bank='MSC_Interaction'; Action=1; Target=$footsteps },
    @{ Id='audio.event.ui.navigate'; Domain='MSC_UI'; Name='Play_MSC_UI_Navigate'; Bank='MSC_UI'; Action=0; Target='' },
    @{ Id='audio.event.ui.confirm'; Domain='MSC_UI'; Name='Play_MSC_UI_Confirm'; Bank='MSC_UI'; Action=0; Target='' },
    @{ Id='audio.event.ui.cancel'; Domain='MSC_UI'; Name='Play_MSC_UI_Cancel'; Bank='MSC_UI'; Action=0; Target='' },
    @{ Id='audio.event.ui.save.feedback'; Domain='MSC_UI'; Name='Play_MSC_UI_Save_Feedback'; Bank='MSC_UI'; Action=0; Target='' },
    @{ Id='audio.event.ui.load.feedback'; Domain='MSC_UI'; Name='Play_MSC_UI_Load_Feedback'; Bank='MSC_UI'; Action=0; Target='' }
)

$eventRoot = '\Events\Default Work Unit'
foreach ($domain in @('MSC_Vehicle','MSC_Weather','MSC_World','MSC_Interaction','MSC_UI')) {
    Ensure-WwiseObject -Parent $eventRoot -Type 'Folder' -Name $domain -Notes 'Project-owned Milestone 08 event namespace.' | Out-Null
}

foreach ($eventDefinition in $eventDefinitions) {
    $domainPath = "$eventRoot\$($eventDefinition.Domain)"
    $event = Ensure-WwiseObject -Parent $domainPath -Type 'Event' -Name $eventDefinition.Name -Notes "Project-owned mapping: $($eventDefinition.Id); bank=$($eventDefinition.Bank)"
    if ($eventDefinition.Action -gt 0) {
        Ensure-EventAction -EventPath $event.path -ActionType $eventDefinition.Action -TargetPath $eventDefinition.Target
    }
    else {
        foreach ($child in (Get-WwiseChildren -Path $event.path | Where-Object { $_.type -eq 'Action' })) {
            Invoke-Waapi -Uri 'ak.wwise.core.object.delete' -Arguments @{ object = $child.id } | Out-Null
        }
    }
}
Write-M08Log "Configured $($eventDefinitions.Count) distinct runtime events. Runtime aliases are not duplicated."

$bankNames = @('MSC_Vehicle','MSC_Weather','MSC_World','MSC_Interaction','MSC_UI')
foreach ($bankName in $bankNames) {
    $bank = Ensure-WwiseObject -Parent '\SoundBanks\Default Work Unit' -Type 'SoundBank' -Name $bankName -Notes 'Milestone 08 project-owned bank definition.'
    $eventsForBank = @($eventDefinitions | Where-Object { $_.Bank -eq $bankName })
    $inclusions = foreach ($eventDefinition in $eventsForBank) {
        @{
            object = "$eventRoot\$($eventDefinition.Domain)\$($eventDefinition.Name)"
            filter = @('events','structures','media')
        }
    }

    Invoke-Waapi -Uri 'ak.wwise.core.soundbank.setInclusions' -Arguments @{
        soundbank = $bank.id
        operation = 'replace'
        inclusions = @($inclusions)
    } | Out-Null
}

Invoke-Waapi -Uri 'ak.wwise.core.project.save' | Out-Null
Write-M08Log 'Saved Wwise project and configured bank inclusions.'

$generation = Invoke-Waapi -Uri 'ak.wwise.core.soundbank.generate' -Arguments @{
    soundbanks = @($bankNames | ForEach-Object { @{ name = $_; rebuild = $true } })
    platforms = @('Windows')
    skipLanguages = $true
    rebuildSoundBanks = $true
    rebuildInitBank = $true
    clearAudioFileCache = $true
    writeToDisk = $true
}
foreach ($entry in @($generation.logs)) {
    Write-M08Log ("SoundBank generation [{0}]: {1}" -f $entry.severity, $entry.message)
}
if ($generation.PSObject.Properties.Name -contains 'error' -and -not [string]::IsNullOrWhiteSpace([string]$generation.error)) {
    throw "SoundBank generation failed: $($generation.error)"
}

$events = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ ofType = @('Event') } } -Options @{ return = @('id','name','path') }
$gameParameters = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ ofType = @('GameParameter') } } -Options @{ return = @('id','name','path','@Min','@Max','@InitialValue') }
$switchGroups = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ ofType = @('SwitchGroup') } } -Options @{ return = @('id','name','path') }
$stateGroups = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ ofType = @('StateGroup') } } -Options @{ return = @('id','name','path') }
$soundBanks = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ ofType = @('SoundBank') } } -Options @{ return = @('id','name','path') }
$mixerBuses = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ path = @($masterBus,$vehicleBus,$effectsBus,$ambienceBus,$musicBus,$uiBus) } } -Options @{ return = @('id','name','path','@RTPC') }
$routedRoots = Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ path = @($actorRoot,$vehicleRoot,$weatherRoot,$worldRoot,$interactionRoot,$musicRoot,$uiRoot) } } -Options @{ return = @('id','name','path','@OutputBus') }

$mscEventCount = @($events.return | Where-Object { $_.path -like '\Events\Default Work Unit\MSC_*\*' }).Count
$mscParameterCount = @($gameParameters.return | Where-Object { $_.name -like 'MSC_*' }).Count
$mscSwitchCount = @($switchGroups.return | Where-Object { $_.name -like 'MSC_*' }).Count
$mscStateCount = @($stateGroups.return | Where-Object { $_.name -like 'MSC_*' }).Count
$mscBankCount = @($soundBanks.return | Where-Object { $_.name -in $bankNames }).Count
$mscBusCount = @($mixerBuses.return).Count
$mixerBusRtpcCount = @($mixerBuses.return | Where-Object { @($_.'@RTPC').Count -eq 1 }).Count
$routedRootCount = @($routedRoots.return | Where-Object { $_.'@OutputBus' }).Count

$routingFailures = @()
foreach ($expectation in $routingExpectations) {
    $routedObject = Get-WwiseObjectByPath -Path $expectation.Path
    $expectedBus = Get-WwiseObjectByPath -Path $expectation.Bus
    if (-not $routedObject) {
        $routingFailures += "missing object: $($expectation.Path)"
        continue
    }
    if (-not $expectedBus) {
        $routingFailures += "missing bus: $($expectation.Bus)"
        continue
    }

    $actualBus = $routedObject.'@OutputBus'
    if (-not $actualBus -or $actualBus.id -ne $expectedBus.id) {
        $actualBusName = if ($actualBus) { $actualBus.name } else { '<none>' }
        $routingFailures += "$($expectation.Path): expected=$($expectedBus.name), actual=$actualBusName"
    }
}
$explicitlyRoutedObjectCount = $routingExpectations.Count - $routingFailures.Count

$footstepContainer = Get-WwiseObjectByPath -Path $footsteps
$footstepSwitchGroupObject = Get-WwiseObjectByPath -Path $footstepSwitchGroup
$footstepSwitchReferenceValid = $footstepContainer -and $footstepSwitchGroupObject -and $footstepContainer.'@SwitchGroupOrStateGroup' -and $footstepContainer.'@SwitchGroupOrStateGroup'.id -eq $footstepSwitchGroupObject.id
$footstepPairSoundCount = 0
$footstepPairCardinalityValid = $true
foreach ($pairPath in $footstepPairs.Values) {
    $pairSoundCount = @(Get-WwiseChildren -Path $pairPath | Where-Object { $_.type -eq 'Sound' }).Count
    $footstepPairSoundCount += $pairSoundCount
    if ($pairSoundCount -ne 2) {
        $footstepPairCardinalityValid = $false
    }
}

$expectedFootstepAssignmentKeys = foreach ($assignment in $footstepAssignments) {
    $childObject = Get-WwiseObjectByPath -Path "$footsteps\$($assignment.Child)"
    $switchObject = Get-WwiseObjectByPath -Path "$footstepSwitchGroup\$($assignment.Switch)"
    if (-not $childObject -or -not $switchObject) {
        throw "Footstep assignment endpoint is missing: child=$($assignment.Child); switch=$($assignment.Switch)."
    }
    '{0}|{1}' -f $childObject.id, $switchObject.id
}
$actualFootstepAssignments = Invoke-Waapi -Uri 'ak.wwise.core.switchContainer.getAssignments' -Arguments @{ id = $footsteps }
$actualFootstepAssignmentKeys = @($actualFootstepAssignments.return | ForEach-Object { '{0}|{1}' -f $_.child, $_.stateOrSwitch })
$footstepAssignmentDifferences = @(Compare-Object -ReferenceObject @($expectedFootstepAssignmentKeys | Sort-Object -Unique) -DifferenceObject @($actualFootstepAssignmentKeys | Sort-Object -Unique))
$footstepAssignmentsValid = $footstepAssignmentDifferences.Count -eq 0 -and $actualFootstepAssignmentKeys.Count -eq $footstepAssignments.Count

$footstepEventPath = "$eventRoot\MSC_Interaction\Play_MSC_Player_Footstep"
$footstepActions = @(Get-WwiseChildren -Path $footstepEventPath | Where-Object { $_.type -eq 'Action' })
$footstepEventValid = $footstepActions.Count -eq 1 -and [int]$footstepActions[0].'@ActionType' -eq 1 -and $footstepActions[0].'@Target' -and $footstepActions[0].'@Target'.id -eq $footstepContainer.id

$windObject = Get-WwiseObjectByPath -Path $sounds.Wind
$windRtpcIds = @($windObject.'@RTPC' | ForEach-Object { $_.id })
$windRtpcs = if ($windRtpcIds.Count -gt 0) {
    Invoke-Waapi -Uri 'ak.wwise.core.object.get' -Arguments @{ from = @{ id = $windRtpcIds } } -Options @{ return = @('id','type','path','@PropertyName','@ControlInput','@Curve') }
}
else {
    [pscustomobject]@{ return = @() }
}
$windShelterRtpcs = @($windRtpcs.return | Where-Object { $_.'@PropertyName' -eq 'Volume' -and $_.'@ControlInput' -and $_.'@ControlInput'.name -eq 'MSC_Environment_Shelter' })
$windShelterCurveValid = $windShelterRtpcs.Count -eq 1 -and @($windShelterRtpcs[0].'@Curve'.points | Where-Object { [Math]::Abs([double]$_.x - 1.0) -lt 0.0001 -and [double]$_.y -le -23.9 }).Count -eq 1

$generatedRoot = Join-Path (Split-Path -Parent $WwiseProjectPath) 'GeneratedSoundBanks'
$projectInfo = Get-ChildItem -LiteralPath $generatedRoot -Recurse -File -Filter 'ProjectInfo.json' -ErrorAction SilentlyContinue
$bankFiles = Get-ChildItem -LiteralPath $generatedRoot -Recurse -File -Filter '*.bnk' -ErrorAction SilentlyContinue
$mediaFiles = Get-ChildItem -LiteralPath $generatedRoot -Recurse -File -Filter '*.wem' -ErrorAction SilentlyContinue
$embeddedMediaCount = 0
foreach ($bankInfoFile in (Get-ChildItem -LiteralPath $generatedRoot -Recurse -File -Filter 'MSC_*.json' -ErrorAction SilentlyContinue)) {
    $bankInfo = Get-Content -LiteralPath $bankInfoFile.FullName -Raw | ConvertFrom-Json
    foreach ($bankInfoEntry in @($bankInfo.SoundBanksInfo.SoundBanks)) {
        if ($bankInfoEntry.PSObject.Properties.Name -contains 'Media') {
            $embeddedMediaCount += @($bankInfoEntry.Media).Count
        }
    }
}

Write-M08Log "WAAPI verification: events=$mscEventCount; parameters=$mscParameterCount; switchGroups=$mscSwitchCount; stateGroups=$mscStateCount; banks=$mscBankCount; mixerBuses=$mscBusCount; mixerBusRtpcs=$mixerBusRtpcCount; routedRoots=$routedRootCount; exactChildRoutes=$explicitlyRoutedObjectCount/$($routingExpectations.Count)."
Write-M08Log "Footstep verification: sounds=$footstepPairSoundCount; randomPairs=$($footstepPairs.Count); assignments=$($actualFootstepAssignmentKeys.Count)/$($footstepAssignments.Count); switchReference=$footstepSwitchReferenceValid; eventPlayAction=$footstepEventValid; windShelterCurve=$windShelterCurveValid."
Write-M08Log "Disk verification: ProjectInfo.json=$(@($projectInfo).Count); bnk=$(@($bankFiles).Count); looseWem=$(@($mediaFiles).Count); embeddedMedia=$embeddedMediaCount; root=$generatedRoot"

if ($mscEventCount -ne $eventDefinitions.Count) { throw "Expected $($eventDefinitions.Count) MSC events, found $mscEventCount." }
if ($mscParameterCount -ne $parameters.Count) { throw "Expected $($parameters.Count) MSC Game Parameters, found $mscParameterCount." }
if ($mscSwitchCount -lt 4) { throw "Expected at least four MSC Switch Groups, found $mscSwitchCount." }
if ($mscStateCount -lt 3) { throw "Expected at least three MSC State Groups, found $mscStateCount." }
if ($mscBankCount -ne $bankNames.Count) { throw "Expected $($bankNames.Count) MSC banks, found $mscBankCount." }
if ($mscBusCount -ne 6) { throw "Expected six mixer buses, found $mscBusCount." }
if ($mixerBusRtpcCount -ne 6) { throw "Expected one mixer Volume RTPC on each of six buses, found $mixerBusRtpcCount configured buses." }
if ($routedRootCount -ne 7) { throw "Expected seven routed actor roots, found $routedRootCount." }
if ($routingFailures.Count -gt 0) { throw "Expected exact OutputBus routing on every imported Sound/container. Failures: $($routingFailures -join '; ')" }
if (-not $footstepSwitchReferenceValid) { throw 'MSC_Player_Footsteps does not reference MSC_Player_FootstepSurface.' }
if (-not $footstepPairCardinalityValid -or $footstepPairSoundCount -ne 10) { throw "Expected five two-sound footstep random containers, found $footstepPairSoundCount sounds." }
if (-not $footstepAssignmentsValid) { throw "Footstep switch assignments do not match the nine project mappings. Differences: $($footstepAssignmentDifferences | ConvertTo-Json -Compress)" }
if (-not $footstepEventValid) { throw 'Play_MSC_Player_Footstep must contain exactly one Play Action targeting MSC_Player_Footsteps.' }
if (-not $windShelterCurveValid) { throw 'Wind must have a MSC_Environment_Shelter Volume RTPC reaching approximately -24 dB at full shelter.' }
if (@($projectInfo).Count -lt 1) { throw 'Generated ProjectInfo.json was not found.' }
if (@($bankFiles).Count -lt 6) { throw "Expected Init plus five user banks, found $(@($bankFiles).Count) .bnk files." }
if (@($mediaFiles).Count -lt 1 -and $embeddedMediaCount -lt 1) { throw 'No loose or embedded generated media were found.' }

Write-M08Log 'Milestone 08 Wwise authoring configuration completed successfully.'
