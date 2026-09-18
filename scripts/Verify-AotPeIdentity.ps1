[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Image,
    [uint32]$ExpectedTimestamp,
    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'

function Fail([string]$Message) {
    throw "PE identity verification failed: $Message"
}

function Require-Range([long]$Offset, [long]$Length, [long]$FileLength, [string]$Label) {
    if ($Offset -lt 0 -or $Length -lt 0 -or $Offset -gt $FileLength - $Length) {
        Fail "$Label lies outside the image."
    }
}

function Read-UInt16Le([byte[]]$Bytes, [int]$Offset, [string]$Label) {
    Require-Range $Offset 2 $Bytes.Length $Label
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-UInt32Le([byte[]]$Bytes, [int]$Offset, [string]$Label) {
    Require-Range $Offset 4 $Bytes.Length $Label
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

if (-not (Test-Path -LiteralPath $Image -PathType Leaf)) {
    Fail "not a file: $Image"
}

$fullPath = [IO.Path]::GetFullPath($Image)
$bytes = [IO.File]::ReadAllBytes($fullPath)
Require-Range 0 0x100 $bytes.Length 'PE header'

if ($bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
    Fail 'missing MZ header.'
}

$peOffset = [int](Read-UInt32Le $bytes 0x3c 'PE offset')
Require-Range $peOffset 24 $bytes.Length 'PE/COFF header'
if ([Text.Encoding]::ASCII.GetString($bytes, $peOffset, 4) -ne "PE`0`0") {
    Fail 'missing PE signature.'
}

$sectionCount = Read-UInt16Le $bytes ($peOffset + 6) 'section count'
$coffTimestampOffset = $peOffset + 8
$coffTimestamp = Read-UInt32Le $bytes $coffTimestampOffset 'COFF timestamp'
$optionalHeaderOffset = $peOffset + 24
$optionalHeaderSize = Read-UInt16Le $bytes ($peOffset + 20) 'optional-header size'
if ($sectionCount -eq 0) { Fail 'PE has no sections.' }
Require-Range $optionalHeaderOffset $optionalHeaderSize $bytes.Length 'optional header'

$optionalMagic = Read-UInt16Le $bytes $optionalHeaderOffset 'optional-header magic'
switch ($optionalMagic) {
    0x10b { $dataDirectoryOffset = 96; $numberOfRvaAndSizesOffset = 92 }
    0x20b { $dataDirectoryOffset = 112; $numberOfRvaAndSizesOffset = 108 }
    default { Fail "unsupported optional-header magic $optionalMagic." }
}

if ($optionalHeaderSize -lt $dataDirectoryOffset + 7 * 8) {
    Fail 'optional header does not contain the debug data-directory entry.'
}

$subsystem = Read-UInt16Le $bytes ($optionalHeaderOffset + 68) 'subsystem'
if ($subsystem -ne 2 -and $subsystem -ne 3) {
    Fail "expected GUI or console PE subsystem, got $subsystem."
}

$numberOfRvaAndSizes = Read-UInt32Le $bytes ($optionalHeaderOffset + $numberOfRvaAndSizesOffset) 'number of data directories'
if ($numberOfRvaAndSizes -lt 7) { Fail 'PE has no debug data-directory entry.' }

$debugDirectoryEntryOffset = $optionalHeaderOffset + $dataDirectoryOffset + 6 * 8
$debugDirectoryRva = Read-UInt32Le $bytes $debugDirectoryEntryOffset 'debug data-directory RVA'
$debugDirectorySize = Read-UInt32Le $bytes ($debugDirectoryEntryOffset + 4) 'debug data-directory size'
if ($debugDirectoryRva -eq 0 -or $debugDirectorySize -eq 0 -or $debugDirectorySize % 28 -ne 0) {
    Fail 'debug directory must contain whole IMAGE_DEBUG_DIRECTORY records.'
}

$sectionTableOffset = $optionalHeaderOffset + $optionalHeaderSize
Require-Range $sectionTableOffset ([long]$sectionCount * 40) $bytes.Length 'section table'
$debugDirectoryFileOffset = $null
for ($sectionIndex = 0; $sectionIndex -lt $sectionCount; $sectionIndex++) {
    $sectionOffset = $sectionTableOffset + $sectionIndex * 40
    $sectionRva = Read-UInt32Le $bytes ($sectionOffset + 12) "section $sectionIndex RVA"
    $sectionRawSize = Read-UInt32Le $bytes ($sectionOffset + 16) "section $sectionIndex raw size"
    $sectionRawOffset = Read-UInt32Le $bytes ($sectionOffset + 20) "section $sectionIndex raw offset"
    $relativeOffset = [long]$debugDirectoryRva - [long]$sectionRva
    if ($relativeOffset -ge 0 -and $relativeOffset -le $sectionRawSize -and
        $debugDirectorySize -le $sectionRawSize - $relativeOffset) {
        if ($null -ne $debugDirectoryFileOffset) { Fail 'debug directory maps through multiple sections.' }
        $debugDirectoryFileOffset = [int]($sectionRawOffset + $relativeOffset)
    }
}
if ($null -eq $debugDirectoryFileOffset) { Fail 'debug directory RVA does not map to raw section data.' }
Require-Range $debugDirectoryFileOffset $debugDirectorySize $bytes.Length 'debug directory'

$timestampOffsets = [System.Collections.Generic.List[int]]::new()
$timestampOffsets.Add($coffTimestampOffset)
$recordCount = [int]($debugDirectorySize / 28)
for ($recordIndex = 0; $recordIndex -lt $recordCount; $recordIndex++) {
    $recordOffset = $debugDirectoryFileOffset + $recordIndex * 28
    $characteristics = Read-UInt32Le $bytes $recordOffset "debug record $recordIndex characteristics"
    if ($characteristics -ne 0) { Fail "debug record $recordIndex has nonzero characteristics." }
    $timestampOffset = $recordOffset + 4
    $debugTimestamp = Read-UInt32Le $bytes $timestampOffset "debug record $recordIndex timestamp"
    if ($debugTimestamp -ne $coffTimestamp) {
        Fail "debug record $recordIndex timestamp $debugTimestamp differs from COFF timestamp $coffTimestamp."
    }
    $timestampOffsets.Add($timestampOffset)
}

if ($PSBoundParameters.ContainsKey('ExpectedTimestamp') -and $coffTimestamp -ne $ExpectedTimestamp) {
    Fail "timestamp is $coffTimestamp, expected $ExpectedTimestamp."
}

$result = [ordered]@{
    image = $fullPath
    size = $bytes.Length
    sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    pe_offset = $peOffset
    subsystem = $subsystem
    coff_timestamp_offset = $coffTimestampOffset
    debug_directory_rva = $debugDirectoryRva
    debug_directory_size = $debugDirectorySize
    timestamp_offsets = @($timestampOffsets)
    timestamp = $coffTimestamp
}

if ($AsJson) {
    [pscustomobject]$result | ConvertTo-Json -Compress
    return
}

foreach ($entry in $result.GetEnumerator()) {
    $value = if ($entry.Value -is [array]) { $entry.Value -join ' ' } else { $entry.Value }
    "{0}={1}" -f $entry.Key, $value
}
