# Project build. Trusts nothing left over from an earlier run.
#   tools\build.ps1 -OnlyWindows                 Windows player
#   tools\build.ps1                              Windows + WebGL
#   tools\build.ps1 -Method IslandBuilder.BuildMain   any -executeMethod, no player check
# Exit 0 only if: Unity really ran and exited 0, a fresh log says success, no compile errors,
# the player's game code was rewritten during this run, and scene files were not changed by a pure build.
param(
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [switch]$OnlyWindows,
    [string]$Method = '',
    [int]$TimeoutSeconds = 1500
)
$ErrorActionPreference = 'Stop'
$unity = 'C:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe'
if (-not (Test-Path -LiteralPath $unity)) {
    $alt = 'H:\Program Files\Unity 2022.3.62f2c1\Editor\Unity.exe'
    if (Test-Path -LiteralPath $alt) { $unity = $alt }
}
if (-not (Test-Path -LiteralPath $unity)) { throw "Unity not found: $unity" }

. (Join-Path $PSScriptRoot 'project-lock.ps1')
$lock = Enter-ProjectLock $ProjectPath
$substDrive = $null
try {
    $unityProjectPath = $ProjectPath
    if ($ProjectPath.Length -gt 48 -or $ProjectPath -match '[^\x00-\x7F]') {
        $parentDir = Split-Path -Parent $ProjectPath
        $leafDir = Split-Path -Leaf $ProjectPath
        foreach ($letter in @('Z','Y','X','W','V','U')) {
            if (-not (Test-Path "${letter}:\")) {
                & subst "${letter}:" "$parentDir"
                if (Test-Path "${letter}:\$leafDir") {
                    $substDrive = "${letter}:"
                    $unityProjectPath = "${letter}:\$leafDir"
                    break
                }
            }
        }
    }

    $isPlayerBuild = -not $Method
    if ($isPlayerBuild) { $Method = if ($OnlyWindows) { 'TideBuild.Windows' } else { 'TideBuild.All' } }
    # A new file per run: an old log can never be mistaken for this run's result.
    $log = Join-Path $unityProjectPath ("Logs\build_{0}_{1:yyyyMMdd_HHmmss}.log" -f ($Method -replace '[^A-Za-z0-9]', '_'), (Get-Date))
    New-Item -ItemType Directory -Force (Split-Path $log) | Out-Null

    function Get-SceneHashes {
        Get-ChildItem -LiteralPath (Join-Path $ProjectPath 'Assets') -Recurse -Filter *.unity |
            Where-Object { $_.FullName -notlike '*\Library\*' } |
            ForEach-Object { '{0} {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash, $_.FullName } | Sort-Object
    }
    $before = Get-SceneHashes
    $started = Get-Date

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $unity
    $psi.Arguments = '-batchmode -nographics -quit -projectPath "{0}" -executeMethod {1} -logFile "{2}"' -f $unityProjectPath, $Method, $log
    $psi.UseShellExecute = $false
    $proc = [System.Diagnostics.Process]::Start($psi)
    if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
        $proc.Kill()
        throw "Unity did not finish in $TimeoutSeconds s and was stopped. Log: $log"
    }
    $code = $proc.ExitCode
    Write-Host "UNITY_EXIT $code  seconds=$([int]((Get-Date) - $started).TotalSeconds)"

    if (-not (Test-Path -LiteralPath $log)) { throw "Unity wrote no log, so it did not really run (exit $code)." }
    # Unity's helper processes can keep the log open for a moment after Unity exits.
    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $stream = New-Object IO.FileStream($log, ([IO.FileMode]::Open), ([IO.FileAccess]::Read), $share)
    $reader = New-Object IO.StreamReader($stream)
    $text = $reader.ReadToEnd()
    $reader.Dispose(); $stream.Dispose()
    $compile = [regex]::Matches($text, '(?m)^.*error CS\d+.*$') | ForEach-Object { $_.Value } | Select-Object -Unique -First 10
    if ($compile) { $compile | ForEach-Object { Write-Host $_ }; throw 'Compile errors.' }
    if ($code -ne 0) { throw "Unity exit code $code. Log: $log" }

    if ($isPlayerBuild) {
        if ($text -notmatch 'TIDEBUILD WINDOWS Succeeded') { throw 'Log does not report a successful Windows build.' }
        # TideTown.exe is only a launcher and Unity skips unchanged files, so compare what matters:
        # the player's code and data must be at least as new as the sources they are built from.
        $data = Join-Path $ProjectPath 'Build\Main\TideTown_Data'
        $dll = Join-Path $data 'Managed\Assembly-CSharp.dll'
        if (-not (Test-Path -LiteralPath $dll)) { throw 'Assembly-CSharp.dll is missing from the build.' }
        $assets = Join-Path $ProjectPath 'Assets'
        $code = Get-ChildItem -LiteralPath $assets -Recurse -Filter *.cs | Where-Object { $_.FullName -notlike '*\Editor\*' } | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $dllTime = (Get-Item -LiteralPath $dll).LastWriteTime
        if ($dllTime -lt $code.LastWriteTime) { throw "Stale player code: dll $dllTime is older than $($code.Name) $($code.LastWriteTime)." }
        $inputs = @(Get-ChildItem -LiteralPath $assets -Recurse -Include *.unity,*.json | Where-Object { $_.FullName -like '*\TideTown\Scenes\*' -or $_.FullName -like '*\Resources\World\*' }) | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        $built = Get-ChildItem -LiteralPath $data -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($built.LastWriteTime -lt $inputs.LastWriteTime) { throw "Stale player data: $($built.Name) $($built.LastWriteTime) is older than $($inputs.Name) $($inputs.LastWriteTime)." }
        Write-Host "PLAYER_FRESH code=$dllTime data=$($built.LastWriteTime)"
        if (-not $OnlyWindows -and $text -notmatch 'TIDEBUILD WEBGL Succeeded') { throw 'Log does not report a successful WebGL build.' }
        if (Compare-Object $before (Get-SceneHashes)) { throw 'A scene file changed during a pure build.' }
    }
    Write-Host "BUILD_OK $Method"
}
finally {
    if ($substDrive) { & subst $substDrive /D | Out-Null }
    Exit-ProjectLock $lock
}
