<#
  Runs the end-to-end tests against a real Manager on a THROWAWAY data root, so nothing in your own
  XRUIOS data (calendars, albums, accounts, baselines) is read or changed.

  What it does:
    1. builds the solution (skip with -NoBuild)
    2. points XRUIOS_ROOT at a fresh temp folder and creates a test account in it
    3. installs the two test apps (e2e.harness, e2e.other) and approves every worker and app
    4. starts the Manager with --dev-creds and waits for the workers
    5. runs XRUIOS.E2E, stops the Manager, deletes the throwaway root

  Usage:   .\run-e2e.ps1 [-NoBuild] [-Only clock,calendars] [-Keep]
  Needs:   no other XRUIOS.Manager running (there is one per device). Windows PowerShell 5.1 or PowerShell 7.
#>
param(
    [switch]$NoBuild,
    [string]$Only = "",
    [switch]$Keep,
    [switch]$Memory,
    [switch]$NoTests
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$src = Join-Path $repo "src"
$mgr = Join-Path $src "XRUIOS.Manager\bin\Debug\net10.0\XRUIOS.Manager.exe"
$e2e = Join-Path $src "XRUIOS.E2E\bin\Debug\net10.0\XRUIOS.E2E.exe"

if (-not $NoBuild) {
    Write-Host "Building..." -ForegroundColor Cyan
    # The solution also holds the browser (WebAssembly) heads of the UI apps, which fail with NETSDK1082
    # ("no runtime pack for browser-wasm") on most machines and make the exit code non-zero. That is a
    # known problem unrelated to these tests, so only OTHER build errors stop the run.
    $buildOutput = dotnet build (Join-Path $repo "XRUIOS.slnx") -nologo -v q -m:2 2>&1 | Out-String
    $realErrors = $buildOutput -split "`r?`n" | Where-Object { $_ -match " error " -and $_ -notmatch "NETSDK1082" } | Select-Object -Unique
    if ($realErrors) { $realErrors | Select-Object -First 20 | ForEach-Object { Write-Host $_ }; throw "Build failed." }
}
foreach ($exe in @($mgr, $e2e)) { if (-not (Test-Path $exe)) { throw "Not built: $exe" } }

& $mgr status | Out-Null
if ($LASTEXITCODE -eq 0) { throw "An XRUIOS.Manager is already running. Stop it first (XRUIOS.Manager stop); there is one per device." }

$root = Join-Path ([System.IO.Path]::GetTempPath()) "xruios-e2e-root"
if (Test-Path $root) { Remove-Item -Recurse -Force $root }
New-Item -ItemType Directory -Path $root | Out-Null

$env:XRUIOS_ROOT = $root
$env:XRUIOS_WORKERS_ROOT = Join-Path $src "workers"
$env:XRUIOS_APPS_ROOT = Join-Path $src "apps"


# Per-process memory. "Private WS" is the RAM only that process uses (what Task Manager shows as
# "Memory"); "Working set" also counts DLL pages shared with other processes; "Committed" is what the
# process has asked the OS for.
function Show-FleetMemory([string]$title) {
    Write-Host ""
    Write-Host "Memory: $title" -ForegroundColor Cyan
    $rows = Get-CimInstance Win32_PerfFormattedData_PerfProc_Process |
        Where-Object { $_.Name -like "XRUIOS.Worker.*" -or $_.Name -like "XRUIOS.Manager*" } |
        ForEach-Object {
            [pscustomobject]@{
                Process     = ($_.Name -replace "^XRUIOS\.", "")
                PrivateWS   = [math]::Round($_.WorkingSetPrivate / 1MB, 1)
                WorkingSet  = [math]::Round($_.WorkingSet / 1MB, 1)
                Committed   = [math]::Round($_.PrivateBytes / 1MB, 1)
                Threads     = $_.ThreadCount
            }
        } | Sort-Object PrivateWS -Descending
    $rows | Format-Table Process, @{n="Private WS (MB)";e={$_.PrivateWS}}, @{n="Working set (MB)";e={$_.WorkingSet}}, @{n="Committed (MB)";e={$_.Committed}}, Threads -AutoSize | Out-String | Write-Host
    $w = $rows | Where-Object { $_.Process -like "Worker.*" }
    $sum = { param($c) [math]::Round(($c | Measure-Object -Sum).Sum, 0) }
    Write-Host ("Workers: {0}   total private WS {1} MB   average {2} MB   total working set {3} MB   total committed {4} MB" -f `
        $w.Count, (& $sum ($w | ForEach-Object PrivateWS)), [math]::Round(($w | Measure-Object PrivateWS -Average).Average, 1), `
        (& $sum ($w | ForEach-Object WorkingSet)), (& $sum ($w | ForEach-Object Committed))) -ForegroundColor Yellow
}

$managerProcess = $null
$exitCode = 0
try {
    Write-Host "Creating a test account in $root" -ForegroundColor Cyan
    & $mgr login e2e "e2e-test-password" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create the test account." }

    # The harness is an installed app whose exe is this build of XRUIOS.E2E. e2e.other is registered to
    # a different exe, so enrolling as it from the harness must be refused (path mismatch).
    $manifests = Join-Path $root "manifests"
    New-Item -ItemType Directory -Path $manifests | Out-Null
    $harness = Get-Content (Join-Path $PSScriptRoot "e2e.harness.manifest.json") -Raw | ConvertFrom-Json
    $harness.exe = $e2e
    $harness | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $manifests "e2e.harness.json") -Encoding UTF8
    @{ name = "e2e.other"; exe = $mgr; capabilities = @(); events = @() } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $manifests "e2e.other.json") -Encoding UTF8

    foreach ($m in "e2e.harness", "e2e.other") {
        & $mgr install-app (Join-Path $manifests "$m.json") | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "install-app failed for $m." }
    }

    Write-Host "Approving workers and apps (this hashes every folder)..." -ForegroundColor Cyan
    & $mgr baseline | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "baseline failed." }

    # The certificate vault is a Manager command, so it can be checked before the Manager starts: seal a file,
    # see it verify, damage the sealed copy, see that `cert verify` notices, then remove it.
    function Test-Step([bool]$ok, [string]$what) {
        if ($ok) { Write-Host "  PASS $what" } else { Write-Host "  FAIL $what" -ForegroundColor Red; $script:certFailures++ }
    }
    $script:certFailures = 0
    if (-not $NoTests -and (-not $Only -or $Only -match "cert")) {
        # Some of these commands are EXPECTED to fail and say why on stderr. Windows PowerShell 5.1 turns that
        # into a terminating error under "Stop", so the checks run under "Continue".
        $ErrorActionPreference = "Continue"
        Write-Host "[certificates]" -ForegroundColor Cyan
        $certFile = Join-Path $root "e2e-cert.pem"
        Set-Content -Path $certFile -Value "-----BEGIN CERTIFICATE-----`ne2e test certificate`n-----END CERTIFICATE-----" -Encoding ASCII
        & $mgr cert add $certFile | Out-Null;           Test-Step ($LASTEXITCODE -eq 0) "cert add seals and verifies a file"
        & $mgr cert add $certFile | Out-Null;           Test-Step ($LASTEXITCODE -ne 0) "adding the same name again is refused"
        $listing = & $mgr cert list;                    Test-Step (($LASTEXITCODE -eq 0) -and ($listing -match "OK\s+e2e-cert")) "cert list shows it as OK"
        $sealed = Get-ChildItem -Path (Join-Path $root "Users") -Recurse -Filter "e2e-cert.sealed" | Select-Object -First 1
        $text = [System.IO.File]::ReadAllText($sealed.FullName)
        [System.IO.File]::WriteAllText($sealed.FullName, $text.Substring(0, $text.Length - 4) + "AAAA")
        $listing = & $mgr cert verify;                  Test-Step (($LASTEXITCODE -eq 3) -and ($listing -match "INVALID\s+e2e-cert")) "cert verify catches a damaged certificate (exit code 3)"
        & $mgr cert remove e2e-cert | Out-Null;         Test-Step ($LASTEXITCODE -eq 0) "cert remove forgets it"
        & $mgr cert verify | Out-Null;                  Test-Step ($LASTEXITCODE -eq 0) "the vault is clean again"
        if ($script:certFailures -gt 0) { $exitCode = 1 }
        $ErrorActionPreference = "Stop"
    }

    Write-Host "Starting the Manager..." -ForegroundColor Cyan
    $log = Join-Path $root "manager.log"
    $managerProcess = Start-Process -FilePath $mgr -ArgumentList "run", "--dev-creds" -PassThru -WindowStyle Hidden `
        -RedirectStandardOutput $log -RedirectStandardError (Join-Path $root "manager.err.log")
    $deadline = (Get-Date).AddSeconds(240)
    while (-not (Select-String -Path $log -Pattern "workers up" -Quiet -ErrorAction SilentlyContinue)) {
        if ($managerProcess.HasExited) { throw "The Manager exited early. See $log" }
        if ((Get-Date) -gt $deadline) { throw "Timed out waiting for the workers. See $log" }
        Start-Sleep -Seconds 1
    }
    Select-String -Path $log -Pattern "workers up" | ForEach-Object { Write-Host $_.Line }

    if ($Memory) { Start-Sleep -Seconds 20; Show-FleetMemory "fleet idle, 20 s after start" }

    if (-not $NoTests) {
        $testArgs = @()
        if ($Only) { $testArgs += @("--only", $Only) }
        & $e2e @testArgs
        $exitCode = $LASTEXITCODE
        if ($Memory) { Show-FleetMemory "after the tests" }
    }
}
finally {
    Write-Host "Stopping the Manager..." -ForegroundColor Cyan
    & $mgr stop | Out-Null
    if ($managerProcess) { $managerProcess.WaitForExit(30000) | Out-Null }
    Get-Process | Where-Object { $_.Name -like "XRUIOS.Worker.*" } | Stop-Process -Force -ErrorAction SilentlyContinue
    if ($Keep) { Write-Host "Kept $root" } else { Remove-Item -Recurse -Force $root -ErrorAction SilentlyContinue }
}
exit $exitCode
