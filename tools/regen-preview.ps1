# Regenerate the main scene without the "player is running" check in build.ps1 (scene generation does not touch the player build).
# During shared work use tools/build.ps1 -Method IslandBuilder.BuildMain to acquire the project lock.
# After layout or tier changes, recapture mapbase and update the map image and bounds after joined visual acceptance (PL-11).
# Under the shared lock, use the joined Build/Preview player with -resetsave -look day -shot mapbase -shot_aa.
# Import shots/mapbase.png as UI/town_preview.png together with shots/map_bounds.json. Keep mapbase_points.json for alignment checks.
param([string]$ProjectPath = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe'
$log = Join-Path $ProjectPath ("Logs\build_IslandBuilder_BuildMain_{0:yyyyMMdd_HHmmss}.log" -f (Get-Date))
$p = Start-Process $unity -ArgumentList '-batchmode', '-nographics', '-quit', '-projectPath', $ProjectPath, '-executeMethod', 'IslandBuilder.BuildMain', '-logFile', $log -PassThru
if (-not $p.WaitForExit(1500000)) { $p.Kill(); throw 'Unity timeout' }
$text = Get-Content -LiteralPath $log -Raw
[regex]::Matches($text, "(?m)^.*(error CS\d+|Shader error in 'TideTown|^GRASS_BUILT|^NATURE_BUILT|^ISLAND_BUILT|^CONTOUR removed).*$") | ForEach-Object { Write-Host ($_.Value.Substring(0, [Math]::Min(220, $_.Value.Length))) }
if ($p.ExitCode -ne 0) { throw "Unity exit $($p.ExitCode). Log: $log" }
