# Run the Windows player for one check, then always close it.
#   tools\run-audit.ps1 -Mode route -OutDir docs\evidence\WP-06
#   tools\run-audit.ps1 -Mode shot -Shot preview -IdPass -OutDir docs\evidence\WP-06\preview
# Old result files are deleted first, so a result can only come from this run.
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [ValidateSet('route','shot','water','scan','story')][string]$Mode = 'route',
    [string]$Shot = 'preview',
    [switch]$IdPass,
    [string]$OutDir = '',
    [int]$TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'
$exe = Join-Path $ProjectPath 'Build\Main\TideTown.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Build\Main\TideTown.exe not found. Run tools\build.ps1 first.' }
$dir = Split-Path -Parent $exe

. (Join-Path $PSScriptRoot 'project-lock.ps1')
$lock = Enter-ProjectLock $ProjectPath
$proc = $null
try {
    $argv = '-screen-width 1280 -screen-height 720 -screen-fullscreen 0'
    if ($Mode -eq 'route') {
        $argv += ' -routeaudit2'
        $files = @('route-audit.json')
    } elseif ($Mode -eq 'water') {
        $argv += ' -wateraudit'
        $files = @('water-audit.json', 'shots\water_L0.png', 'shots\water_L1.png', 'shots\water_L2.png', 'shots\water_L3.png', 'shots\water_L4.png')
    } elseif ($Mode -eq 'scan') {
        $argv += ' -scanaudit'
        $files = @('scan-audit.json')
    } elseif ($Mode -eq 'story') {
        $argv += ' -storyaudit -resetsave'
        $files = @('story-audit.json', 'shots\story_boot.png', 'shots\story_wake.png', 'shots\story_pickup.png',
                   'shots\story_bakery_letter.png', 'shots\story_oven.png')
    } else {
        $argv += " -shot $Shot"
        $files = @("shots\$Shot.png")
        if ($IdPass) { $argv += ' -idpass'; $files += @("shots\${Shot}_id.png", "shots\${Shot}_id.json") }
    }
    # 有些机器上 Remove-Item 会被拒（文件本身没锁），改用改名，效果一样：旧结果不能冒充这次的结果
    foreach ($f in $files) {
        $full = Join-Path $dir $f
        if (-not (Test-Path -LiteralPath $full)) { continue }
        try { Remove-Item -LiteralPath $full -Force }
        catch { Move-Item -LiteralPath $full -Destination ($full + '.prev') -Force }
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = $argv
    $psi.WorkingDirectory = $dir
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)
    # Unity sometimes hangs while shutting down, after the results are already written.
    # So wait for the final result, give it a short grace to exit, then close it ourselves.
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $graceEnd = $null
    while (-not $proc.WaitForExit(1000)) {
        if ((Get-Date) -gt $deadline) {
            $proc.Kill(); $proc.WaitForExit()
            throw "Player gave no final result in $TimeoutSeconds s and was stopped. Partial results, if any, are in $dir."
        }
        $ready = -not ($files | Where-Object { -not (Test-Path -LiteralPath (Join-Path $dir $_)) })
        if ($ready -and $Mode -eq 'route') {
            $now = [IO.File]::ReadAllText((Join-Path $dir 'route-audit.json'))
            $ready = $now -notmatch '"status":"running"'
        }
        if ($ready -and -not $graceEnd) { $graceEnd = (Get-Date).AddSeconds(15) }
        if ($graceEnd -and (Get-Date) -gt $graceEnd) {
            Write-Host 'PLAYER_SHUTDOWN_HANG results are complete; closing the player'
            $proc.Kill(); $proc.WaitForExit()
            break
        }
    }
    Write-Host "PLAYER_EXIT $($proc.ExitCode)"

    foreach ($f in $files) { if (-not (Test-Path -LiteralPath (Join-Path $dir $f))) { throw "This run produced no $f." } }
    if ($OutDir) {
        $dest = if ([IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $ProjectPath $OutDir }
        New-Item -ItemType Directory -Force $dest | Out-Null
        foreach ($f in $files) { Copy-Item -LiteralPath (Join-Path $dir $f) -Destination $dest -Force }
        Write-Host "SAVED $dest"
    }
    if ($Mode -eq 'route') {
        $content = [IO.File]::ReadAllText((Join-Path $dir 'route-audit.json'))
        Write-Host $content
        if ($content -notmatch '"status":"passed"') { exit 2 }
    }
    if ($Mode -eq 'water') {
        $content = [IO.File]::ReadAllText((Join-Path $dir 'water-audit.json'))
        Write-Host $content
        if ($content -notmatch '"status":"passed"') { exit 2 }
    }
    if ($Mode -eq 'scan') {
        $content = [IO.File]::ReadAllText((Join-Path $dir 'scan-audit.json'))
        Write-Host $content
        if ($content -notmatch '"status"\s*:\s*"passed"') { exit 2 }
    }
    if ($Mode -eq 'story') {
        $content = [IO.File]::ReadAllText((Join-Path $dir 'story-audit.json'))
        Write-Host $content
        if ($content -notmatch '"status"\s*:\s*"passed"') { exit 2 }
    }
}
finally { Exit-ProjectLock $lock }
