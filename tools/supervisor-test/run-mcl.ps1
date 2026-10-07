# Run the same prompt through the MCL mission.
$ErrorActionPreference = 'Stop'
& python3 (Join-Path $PSScriptRoot 'harness.py') mcl
exit $LASTEXITCODE
