$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$agentFile = Join-Path $repoRoot 'missions/vanilla/agent.yaml'
$cliProject = Join-Path $repoRoot 'src/ForgeMission.Cli/ForgeMission.Cli.csproj'
$electronDir = Join-Path $repoRoot 'src/ForgeMission.ClientRuntime/electron'

# The Client Runtime and Electron inherit these values. The profile-loaded pwsh session carries
# MCL_API_KEY for the vanilla OpenAI mission, avoiding the bash-environment key trap.
$env:MISSION_RUNTIME__BASEURL = 'http://127.0.0.1:8080/'
$env:WORKSPACE__INITIALROOT = $repoRoot

$runtime = Start-Process dotnet -ArgumentList @('run', '--project', $cliProject, '--', 'serve', $agentFile) -PassThru -NoNewWindow

try {
    $ready = $false
    foreach ($attempt in 1..120) {
        try {
            $response = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/' -Method Head
            if ($response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch { }

        if ($runtime.HasExited) {
            throw 'The vanilla Mission Runtime exited before it became ready.'
        }

        Start-Sleep -Milliseconds 250
    }

    if (!$ready) {
        throw 'The vanilla Mission Runtime did not become ready on http://127.0.0.1:8080.'
    }

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
}
finally {
    if (!$runtime.HasExited) {
        Stop-Process -Id $runtime.Id
        $runtime.WaitForExit()
    }
}
