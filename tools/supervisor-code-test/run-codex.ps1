# Run the shared prompt through a native Codex supervisor and sequential reviewers.
$ErrorActionPreference = 'Stop'
& python3 (Join-Path $PSScriptRoot 'harness.py') codex
exit $LASTEXITCODE
