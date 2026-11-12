# Build the preview player (Build\Preview, never the real build) and take screenshots. Use while Build\Main is running.
#   tools\preview-shots.ps1 -OutDir docs\evidence\xxx [-NoBuild] [-Shots street,school] [-Look day]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$OutDir = 'Build\Preview\shots',
    [string[]]$Shots = @('street', 'preview'),
    [string]$Look = 'day',
    [string]$Suffix = '',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe'
if (-not $NoBuild) {
    $log = Join-Path $ProjectPath ("Logs\build_TideBuild_WindowsPreview_{0:yyyyMMdd_HHmmss}.log" -f (Get-Date))
    $p = Start-Process $unity -ArgumentList '-batchmode', '-nographics', '-quit', '-projectPath', $ProjectPath, '-executeMethod', 'TideBuild.WindowsPreview', '-logFile', $log -PassThru
    if (-not $p.WaitForExit(3000000)) { $p.Kill(); throw 'Unity timeout' }
    $text = Get-Content -LiteralPath $log -Raw
    $errs = [regex]::Matches($text, "(?m)^.*(error CS\d+|Shader error in 'TideTown).*$") | ForEach-Object { $_.Value } | Select-Object -Unique -First 8
    if ($errs) { $errs | ForEach-Object { Write-Host $_ }; throw 'Compile or shader errors.' }
    if ($text -notmatch 'TIDEBUILD WINDOWSPREVIEW Succeeded') { throw "Preview build failed. Log: $log" }
    Write-Host 'PREVIEW_BUILD_OK'
}
$exe = Join-Path $ProjectPath 'Build\Preview\TideTown.exe'
$out = Join-Path $ProjectPath $OutDir
New-Item -ItemType Directory -Force $out | Out-Null
foreach ($s in $Shots) {
    $a = @('-shot', $s, '-look', $Look, '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0')
    $p = Start-Process $exe -ArgumentList $a -PassThru
    if (-not $p.WaitForExit(120000)) { $p.Kill(); Write-Host "TIMEOUT $s"; continue }
    Copy-Item (Join-Path $ProjectPath "Build\Preview\shots\$s.png") (Join-Path $out "$s`_$Look$Suffix.png") -Force
    Write-Host "SHOT $s $Look"
}
