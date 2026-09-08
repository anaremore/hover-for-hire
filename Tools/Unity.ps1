param(
    [ValidateSet('Prepare','EditMode','PlayMode','Windows','macOS','Linux')]
    [string]$Task = 'Prepare',
    [string]$Editor = $env:UNITY_EDITOR
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Editor) {
    $localEditor = Join-Path $projectRoot '.tools\Unity6000.3.22f1\Editor\Unity.exe'
    $hubEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe'
    if (Test-Path -LiteralPath $localEditor) { $Editor = $localEditor }
    elseif (Test-Path -LiteralPath $hubEditor) { $Editor = $hubEditor }
    else { throw 'Pass -Editor with the Unity 6000.3.22f1 executable path, or set UNITY_EDITOR.' }
}
$artifacts = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$arguments = @('-batchmode', '-projectPath', ('"' + $projectRoot + '"'), '-logFile', ('"' + (Join-Path $artifacts ($Task + '.log')) + '"'))
if ($Task -in @('EditMode','PlayMode')) {
    $arguments += @('-nographics','-runTests','-testPlatform',$Task,'-testResults',('"' + (Join-Path $artifacts ($Task + '.xml')) + '"'))
} else {
    $method = @{Prepare='Prepare';Windows='BuildWindows';macOS='BuildMac';Linux='BuildLinux'}[$Task]
    $arguments += @('-executeMethod', ('HoverForHire.Editor.ProjectSetup.' + $method), '-quit')
    if ($Task -eq 'Prepare') { $arguments += '-nographics' }
}
$process = Start-Process -FilePath $Editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
# Wait for the editor itself; shader/compiler service descendants can outlive a failed import.
$process.WaitForExit()
if ($process.ExitCode -ne 0) { throw "Unity exited with $($process.ExitCode). See Artifacts/$Task.log." }
Write-Host "$Task completed. See $artifacts."
