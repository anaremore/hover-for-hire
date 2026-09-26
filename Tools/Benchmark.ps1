param(
    [int]$Width = 1920,
    [int]$Height = 1080,
    [string]$Label = 'benchmark',
    [string]$Without = '',
    [switch]$Development,
    [string]$Player = ''
)
# Runs the opt-in uncapped benchmark (RuntimeBenchmark) in the release Windows player and prints its table.
# -Development uses the development player instead, which also reports managed garbage per frame.
# Writes Artifacts/<Label>/benchmark.json and benchmark.txt, and Artifacts/<Label>.log.
# -Without hud,shadows,vegetation,postfx,msaa removes those parts first, to measure what each one costs.
# Close other GPU-heavy applications first; do not run it alongside a smoke run.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Player) { $Player = Join-Path $projectRoot ('Builds\' + $(if ($Development) { 'Windows-Development' } else { 'Windows' }) + '\Hover for Hire.exe') }
if (-not (Test-Path -LiteralPath $Player)) { throw "Player not found: $Player (run Tools\Unity.ps1 Windows first)." }
$output = Join-Path $projectRoot "Artifacts\$Label"
if (Test-Path -LiteralPath $output) { Remove-Item -LiteralPath $output -Recurse -Force }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$log = Join-Path $projectRoot "Artifacts\$Label.log"
$arguments = @('-hover-benchmark', ('"' + $output + '"'), '-screen-fullscreen', '0',
    '-screen-width', $Width, '-screen-height', $Height, '-logFile', ('"' + $log + '"'))
if ($Without) { $arguments += @('-hover-bench-without', $Without) }
$process = Start-Process -FilePath $Player -ArgumentList $arguments -PassThru
if (-not $process.WaitForExit(300000)) { $process.Kill(); throw "Benchmark timed out; see $log" }
$table = Join-Path $output 'benchmark.txt'
if (-not (Test-Path -LiteralPath $table)) { throw "No benchmark report (exit $($process.ExitCode)); see $log" }
Get-Content -LiteralPath $table
