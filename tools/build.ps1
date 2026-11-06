# WP-01 构建脚本：Windows + WebGL，只打包，不生成场景。
# 用法：powershell -ExecutionPolicy Bypass -File tools\build.ps1 [-OnlyWindows]
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [switch]$OnlyWindows
)

$ErrorActionPreference = "Stop"
$unity = "C:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe"
$log = Join-Path $ProjectPath "Logs\build_main.log"
$sceneMain = Join-Path $ProjectPath "Assets\TideTown\Scenes\TideTown_Main.unity"
$sceneFallback = Join-Path $ProjectPath "Assets\TideTown\Scenes\TideTown_Expanded_Stage1.unity"
$scene = if (Test-Path $sceneMain) { $sceneMain } else { $sceneFallback }

if (-not (Test-Path $unity)) { throw "Unity 不存在：$unity" }
if (-not (Test-Path $scene)) { throw "场景不存在：$scene" }

$before = (Get-FileHash $scene -Algorithm SHA256).Hash
Write-Host "SCENE $scene"
Write-Host "HASH_BEFORE $before"

$method = if ($OnlyWindows) { "TideBuild.Windows" } else { "TideBuild.All" }
$args = @("-batchmode", "-nographics", "-quit", "-projectPath", $ProjectPath, "-executeMethod", $method, "-logFile", $log)
$proc = Start-Process -FilePath $unity -ArgumentList $args -PassThru
$proc.WaitForExit()
Write-Host "UNITY_EXIT $($proc.ExitCode)"

$after = (Get-FileHash $scene -Algorithm SHA256).Hash
Write-Host "HASH_AFTER $after"
if ($before -ne $after) { Write-Error "场景文件被构建改动，违反 WP-01（只打包不生成场景）"; exit 2 }

$text = Get-Content $log -Raw
if ($text -match "error CS") { Write-Error "日志里有 error CS"; exit 3 }
if ($text -notmatch "TIDEBUILD WINDOWS Succeeded") { Write-Error "Windows 构建未成功"; exit 4 }
if (-not $OnlyWindows -and $text -notmatch "TIDEBUILD WEBGL Succeeded") { Write-Error "WebGL 构建未成功"; exit 5 }

Write-Host "BUILD OK"
