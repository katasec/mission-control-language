$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$electronDir = Join-Path $repoRoot 'src/ForgeMission.ClientRuntime/electron'
$vanillaMissionRefPath = Join-Path $repoRoot 'src/ForgeMission.Cli/BuiltinMissionReferences/vanilla.oci-ref'
$vanillaMissionRef = (Get-Content -Raw $vanillaMissionRefPath).Trim()

# The Client Runtime owns the Docker runner lifecycle and reads provider keys from its own
# Application Support/Forge/provider.env file, so this launcher never relies on pwsh key exports.
$env:MISSIONRUNTIME__MODE = 'docker'
$env:MISSIONRUNTIME__DOCKER__MISSIONREF = $vanillaMissionRef
$env:WORKSPACE__INITIALROOT = $repoRoot

Push-Location $electronDir
try {
    if (!(Test-Path 'node_modules/electron')) {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
    }

    & npm start
    if ($LASTEXITCODE -ne 0) { throw 'Electron exited with an error.' }
}
finally {
    Pop-Location
}
