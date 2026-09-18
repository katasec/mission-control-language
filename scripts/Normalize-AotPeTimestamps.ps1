[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Image
)

$ErrorActionPreference = 'Stop'
$fixedTimestamp = [uint32]1789750350
$verifier = Join-Path $PSScriptRoot 'Verify-AotPeIdentity.ps1'

function Fail([string]$Message) {
    throw "PE timestamp normalization failed: $Message"
}

if (-not (Test-Path -LiteralPath $verifier -PathType Leaf)) { Fail "missing verifier: $verifier" }
if (-not (Test-Path -LiteralPath $Image -PathType Leaf)) { Fail "not a file: $Image" }

$fullPath = [IO.Path]::GetFullPath($Image)
$identityBeforeJson = & $verifier -Image $fullPath -AsJson
if (-not $?) { Fail 'initial PE identity verification failed.' }
$identityBefore = $identityBeforeJson | ConvertFrom-Json
$timestampOffsets = @($identityBefore.timestamp_offsets | ForEach-Object { [int]$_ })
if ($timestampOffsets.Count -lt 2 -or $timestampOffsets[0] -ne $identityBefore.coff_timestamp_offset) {
    Fail 'verifier returned an invalid timestamp offset list.'
}

$directory = Split-Path -Parent $fullPath
$workPath = Join-Path $directory ('.aot-normalize-' + [guid]::NewGuid().ToString('N'))
try {
    $beforeBytes = [IO.File]::ReadAllBytes($fullPath)
    $afterBytes = [byte[]]$beforeBytes.Clone()
    if ($identityBefore.timestamp -ne $fixedTimestamp) {
        $replacement = [BitConverter]::GetBytes($fixedTimestamp)
        foreach ($offset in $timestampOffsets) {
            [Array]::Copy($replacement, 0, $afterBytes, $offset, 4)
        }
    }

    $allowedOffsets = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($offset in $timestampOffsets) {
        0..3 | ForEach-Object { [void]$allowedOffsets.Add($offset + $_) }
    }
    $changedOffsets = [System.Collections.Generic.List[int]]::new()
    for ($index = 0; $index -lt $beforeBytes.Length; $index++) {
        if ($beforeBytes[$index] -ne $afterBytes[$index]) {
            if (-not $allowedOffsets.Contains($index)) { Fail "normalization changed unexpected byte offset $index." }
            $changedOffsets.Add($index)
        }
    }
    if ($identityBefore.timestamp -ne $fixedTimestamp -and $changedOffsets.Count -eq 0) {
        Fail 'normalization changed no bytes.'
    }

    [IO.File]::WriteAllBytes($workPath, $afterBytes)
    $identityAfterJson = & $verifier -Image $workPath -ExpectedTimestamp $fixedTimestamp -AsJson
    if (-not $?) { Fail 'normalized PE identity verification failed.' }
    $identityAfter = $identityAfterJson | ConvertFrom-Json
    [IO.File]::Move($workPath, $fullPath, $true)
    $workPath = $null

    $identityAfter.image = $fullPath
    $identityAfter | Add-Member -NotePropertyName changed_offsets -NotePropertyValue @($changedOffsets)
    $identityAfter | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $workPath -and (Test-Path -LiteralPath $workPath)) {
        Remove-Item -LiteralPath $workPath -Force
    }
}
