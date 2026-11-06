# One Unity and one player at a time for this project. Dot-source this file.
# The named mutex is released by Windows even if the holder crashes, so it cannot go stale.
function Enter-ProjectLock([string]$ProjectPath) {
    $mutex = New-Object System.Threading.Mutex($false, 'Global\TideTownProjectBuild')
    try { $got = $mutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $got = $true }
    if (-not $got) { $mutex.Dispose(); throw 'Another build or audit is running for this project. Wait for it to finish.' }
    $mine = $ProjectPath.TrimEnd('\')
    $busy = Get-CimInstance Win32_Process -Filter "Name='Unity.exe' OR Name='TideTown.exe'" | Where-Object {
        $_.Name -eq 'TideTown.exe' -or $_.CommandLine -like "*$mine*"
    }
    if ($busy) {
        $mutex.ReleaseMutex(); $mutex.Dispose()
        throw ('Still running: ' + (($busy | ForEach-Object { "$($_.Name) pid=$($_.ProcessId)" }) -join ', ') + '. Close it first, then retry.')
    }
    return $mutex
}
function Exit-ProjectLock($mutex) {
    if ($mutex) { $mutex.ReleaseMutex(); $mutex.Dispose() }
}
