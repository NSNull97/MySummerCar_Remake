<#
.SYNOPSIS
Read action parameter evidence from one externally staged, plaintext PlayMaker YAML record.
.DESCRIPTION
Read-only inspection, not an FSM translator or runtime importer. Unknown types
remain explicit hexadecimal evidence. No donor component is instantiated.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourcePath,
    [ValidateRange(0, [long]::MaxValue)][long]$ObjectId = 0
)

$ErrorActionPreference = 'Stop'
$fsmText = if ($ObjectId -gt 0) {
    (& (Join-Path $PSScriptRoot 'Read-UnityYamlObject.ps1') -SourcePath $SourcePath -ObjectId @($ObjectId)).Body
} else {
    [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourcePath).ProviderPath)
}
function Convert-FsmHex([string]$value) {
    if ($value.Length % 2 -ne 0) { throw 'Odd hexadecimal evidence length.' }
    $bytes = [byte[]]::new($value.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) { $bytes[$i] = [Convert]::ToByte($value.Substring($i * 2, 2), 16) }
    return ,$bytes
}
function Read-FsmScalar([string]$block, [string]$key) {
    $match = [regex]::Match($block, '(?m)^        ' + [regex]::Escape($key) + ': *([^\r\n]*)')
    if (-not $match.Success) { throw "Missing evidence field $key" }
    return $match.Groups[1].Value
}
function Read-FsmIntArray([string]$value) {
    $bytes = Convert-FsmHex $value
    if ($bytes.Length % 4 -ne 0) { throw 'Malformed 32-bit parameter array.' }
    $ints = [int[]]::new($bytes.Length / 4)
    for ($i = 0; $i -lt $ints.Length; $i++) { $ints[$i] = [BitConverter]::ToInt32($bytes, $i * 4) }
    return ,$ints
}

$fsmStates = [regex]::Matches($fsmText, '(?ms)^    - name: (?<name>[^\r\n]*)\r?\n(?<body>.*?)(?=^    - name: |^    events:|\z)')
foreach ($fsmState in $fsmStates) {
    $fsmBody = $fsmState.Groups['body'].Value
    if ($fsmBody -notmatch '(?m)^      actionData:') { continue }
    $fsmActions = [regex]::Matches(([regex]::Match($fsmBody, '(?ms)^        actionNames:\r?\n(.*?)^        customNames:')).Groups[1].Value, '(?m)^        - ([^\r\n]+)')
    $fsmNames = [regex]::Matches(([regex]::Match($fsmBody, '(?ms)^        paramName:\r?\n(.*?)^        paramDataPos:')).Groups[1].Value, '(?m)^        - ([^\r\n]*)')
    $fsmStarts = Read-FsmIntArray (Read-FsmScalar $fsmBody 'actionStartIndex')
    $fsmEnabled = Convert-FsmHex (Read-FsmScalar $fsmBody 'actionEnabled')
    $fsmOffsets = Read-FsmIntArray (Read-FsmScalar $fsmBody 'paramDataPos')
    $fsmSizes = Read-FsmIntArray (Read-FsmScalar $fsmBody 'paramByteDataSize')
    $fsmTypes = Read-FsmIntArray (Read-FsmScalar $fsmBody 'paramDataType')
    $fsmBytes = Convert-FsmHex (Read-FsmScalar $fsmBody 'byteData')
    if ($fsmActions.Count -ne $fsmEnabled.Length -or $fsmActions.Count -ne $fsmStarts.Length -or $fsmNames.Count -ne $fsmOffsets.Length -or
        $fsmNames.Count -ne $fsmSizes.Length -or $fsmNames.Count -ne $fsmTypes.Length) {
        throw "Parameter evidence arrays disagree in $($fsmState.Groups['name'].Value): actions=$($fsmActions.Count) starts=$($fsmStarts.Length) names=$($fsmNames.Count) offsets=$($fsmOffsets.Length) sizes=$($fsmSizes.Length) types=$($fsmTypes.Length)."
    }
    for ($fsmAction = 0; $fsmAction -lt $fsmActions.Count; $fsmAction++) {
        $fsmEnd = if ($fsmAction + 1 -lt $fsmStarts.Length) { $fsmStarts[$fsmAction + 1] } else { $fsmNames.Count }
        for ($fsmParam = $fsmStarts[$fsmAction]; $fsmParam -lt $fsmEnd; $fsmParam++) {
            $fsmOffset = $fsmOffsets[$fsmParam]; $fsmSize = $fsmSizes[$fsmParam]; $fsmType = $fsmTypes[$fsmParam]
            if ($fsmSize -lt 0 -or $fsmOffset -lt 0 -or ($fsmSize -gt 0 -and $fsmOffset + $fsmSize -gt $fsmBytes.Length)) { throw 'Parameter exceeds byte evidence.' }
            $fsmHex = if ($fsmSize -gt 0) { [BitConverter]::ToString($fsmBytes, $fsmOffset, $fsmSize).Replace('-', '') } else { '' }
            $fsmValue = "external-array-index:$fsmOffset"
            if ($fsmType -eq 15 -and $fsmSize -ge 5) {
                $fsmValue = if ($fsmBytes[$fsmOffset + 4] -eq 0) { [BitConverter]::ToSingle($fsmBytes, $fsmOffset).ToString('R', [Globalization.CultureInfo]::InvariantCulture) }
                    else { 'variable:' + [Text.Encoding]::UTF8.GetString($fsmBytes, $fsmOffset + 5, $fsmSize - 5) }
            } elseif ($fsmType -eq 16 -and $fsmSize -ge 5) {
                $fsmValue = if ($fsmBytes[$fsmOffset + 4] -eq 0) { [BitConverter]::ToInt32($fsmBytes, $fsmOffset) }
                    else { 'variable:' + [Text.Encoding]::UTF8.GetString($fsmBytes, $fsmOffset + 5, $fsmSize - 5) }
            } elseif ($fsmType -eq 17 -and $fsmSize -ge 2) {
                $fsmValue = if ($fsmBytes[$fsmOffset + 1] -eq 0) { $fsmBytes[$fsmOffset] -ne 0 }
                    else { 'variable:' + [Text.Encoding]::UTF8.GetString($fsmBytes, $fsmOffset + 2, $fsmSize - 2) }
            } elseif ($fsmType -eq 7 -and $fsmSize -eq 4) { $fsmValue = [BitConverter]::ToInt32($fsmBytes, $fsmOffset) }
            elseif ($fsmType -eq 1 -and $fsmSize -eq 1) { $fsmValue = $fsmBytes[$fsmOffset] -ne 0 }
            elseif ($fsmSize -gt 0) { $fsmValue = [regex]::Replace([Text.Encoding]::UTF8.GetString($fsmBytes, $fsmOffset, $fsmSize), '[^\x20-\x7E]', '.') }
            [pscustomobject]@{ State = $fsmState.Groups['name'].Value; ActionIndex = $fsmAction; Enabled = $fsmEnabled[$fsmAction] -ne 0; Action = $fsmActions[$fsmAction].Groups[1].Value.Replace('HutongGames.PlayMaker.Actions.', ''); Parameter = $fsmNames[$fsmParam].Groups[1].Value; Type = $fsmType; Value = $fsmValue; RawHex = $fsmHex }
        }
    }
}
