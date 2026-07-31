$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$electronDir = Join-Path $repoRoot 'src/ForgeMission.ClientRuntime/electron'

# The Client Runtime owns the Docker runner lifecycle and reads provider keys from its own
# Application Support/Forge/provider.env file, so this launcher never relies on pwsh key exports.
$env:MISSIONRUNTIME__MODE = 'docker'
$env:MISSIONRUNTIME__DOCKER__MISSIONREF = 'ghcr.io/katasec/forge-mission-vanilla@sha256:9663e05847676da28191f09459ce45671d624221d2d9b329ff0770cb9621dc46'
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
