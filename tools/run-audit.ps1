# WP-01 路线审计脚本：跑 Windows 包，收集 route-audit.json。
# 用法：powershell -ExecutionPolicy Bypass -File tools\run-audit.ps1 [-OutDir docs\evidence\WP-01]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$OutDir = "docs\evidence\WP-01"
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $ProjectPath "Build\Main\TideTown.exe"
if (-not (Test-Path $exe)) { $exe = Join-Path $ProjectPath "Build\Expanded\TideTown.exe" }
if (-not (Test-Path $exe)) { throw "找不到 TideTown.exe，请先跑 tools\build.ps1" }

$json = Join-Path (Split-Path -Parent $exe) "route-audit.json"
Remove-Item $json -Force -ErrorAction SilentlyContinue

$proc = Start-Process -FilePath $exe -ArgumentList @(
    "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0", "-routeaudit"
) -WorkingDirectory (Split-Path -Parent $exe) -PassThru
$proc.WaitForExit()
Write-Host "EXIT $($proc.ExitCode)"

if (-not (Test-Path $json)) { throw "没有生成 route-audit.json" }
$content = Get-Content $json -Raw
Write-Host $content

$dest = Join-Path $ProjectPath $OutDir
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item $json (Join-Path $dest "route-audit.json") -Force
Write-Host "SAVED $dest\route-audit.json"

if ($content -notmatch '"status":"passed"') { Write-Error "路线审计未通过"; exit 2 }
