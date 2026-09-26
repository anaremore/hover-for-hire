param(
    [int[]]$Fps = @(30, 60, 144),
    [switch]$Art,
    [int]$Width = 1600,
    [int]$Height = 900,
    [string]$Label = 'smoke',
    [string]$Player = ''
)
# Runs the opt-in RuntimeSmoke flight in the development Windows player (Tools\Unity.ps1 WindowsDev) at each frame cap
# and summarizes the reports. Release players do not contain the smoke flight.
# Writes Artifacts/<Label>-<fps>/runtime-smoke.json (+ screenshots) and Artifacts/<Label>-<fps>.log.
# Note: the player saves its normal PlayerPrefs (controls/assists/volume) like any play session.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Player) { $Player = Join-Path $projectRoot 'Builds\Windows-Development\Hover for Hire.exe' }
if (-not (Test-Path -LiteralPath $Player)) { throw "Player not found: $Player (run Tools\Unity.ps1 WindowsDev first)." }
$artifacts = Join-Path $projectRoot 'Artifacts'
$failed = 0
foreach ($rate in $Fps) {
    $output = Join-Path $artifacts "$Label-$rate"
    if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    $log = Join-Path $artifacts "$Label-$rate.log"
    $arguments = @('-hover-smoke', ('"' + $output + '"'), '-hover-fps', $rate, '-screen-fullscreen', '0',
        '-screen-width', $Width, '-screen-height', $Height, '-logFile', ('"' + $log + '"'))
    if ($Art) { $arguments += '-hover-art' }
    $process = Start-Process -FilePath $Player -ArgumentList $arguments -PassThru
    if (-not $process.WaitForExit(300000)) { $process.Kill(); Write-Host "$rate fps: TIMED OUT"; $failed++; continue }
    $report = Join-Path $output 'runtime-smoke.json'
    if (-not (Test-Path -LiteralPath $report)) { Write-Host "$rate fps: no report (exit $($process.ExitCode)); see $log"; $failed++; continue }
    $r = Get-Content -Raw -LiteralPath $report | ConvertFrom-Json
    '{0,4} fps cap: exit {1}  passed={2}  errors={3}  avg {4:0.0} fps  peak AGL {5:0.00} m  peak ground speed {6:0.00} m/s  crashed={7}' -f `
        $rate, $process.ExitCode, $r.Passed, $r.Errors, $r.AverageFps, $r.PeakAltitude, $r.PeakSpeed, $r.EverCrashed | Write-Host
    $effects = Join-Path $output 'effects-smoke.json'
    if (Test-Path -LiteralPath $effects) { Write-Host ('      effects: ' + ((Get-Content -Raw -LiteralPath $effects | ConvertFrom-Json) | ConvertTo-Json -Compress)) }
    if ($process.ExitCode -ne 0) { $failed++ }
}
if ($failed -gt 0) { throw "$failed smoke run(s) failed." }
Write-Host 'All smoke runs passed.'
