# Regenerate the main scene without the "player is running" check in build.ps1 (scene generation does not touch the player build).
param([string]$ProjectPath = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe'
$log = Join-Path $ProjectPath ("Logs\build_IslandBuilder_BuildMain_{0:yyyyMMdd_HHmmss}.log" -f (Get-Date))
$p = Start-Process $unity -ArgumentList '-batchmode', '-nographics', '-quit', '-projectPath', $ProjectPath, '-executeMethod', 'IslandBuilder.BuildMain', '-logFile', $log -PassThru
if (-not $p.WaitForExit(1500000)) { $p.Kill(); throw 'Unity timeout' }
$text = Get-Content -LiteralPath $log -Raw
[regex]::Matches($text, "(?m)^.*(error CS\d+|Shader error in 'TideTown|^GRASS_BUILT|^NATURE_BUILT|^ISLAND_BUILT|^CONTOUR removed).*$") | ForEach-Object { Write-Host ($_.Value.Substring(0, [Math]::Min(220, $_.Value.Length))) }
if ($p.ExitCode -ne 0) { throw "Unity exit $($p.ExitCode). Log: $log" }
