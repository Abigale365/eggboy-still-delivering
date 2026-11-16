param([Parameter(Mandatory=$true)][string]$Exe,[Parameter(Mandatory=$true)][ValidateSet('story','innov')][string]$Mode,[Parameter(Mandatory=$true)][string]$Log)
$ErrorActionPreference='Stop'
$arguments='-{0}audit -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -resetsave -logFile "{1}"' -f $Mode,$Log
if($Mode -eq 'innov'){$arguments+=' -lux3d mock -lux3ddelay 3'}
# Interactive game validation needs a visible rendering surface; ShellExecute avoids inherited hidden-console startup flags.
$process=Start-Process -FilePath $Exe -ArgumentList $arguments -WorkingDirectory (Split-Path -LiteralPath $Exe) -WindowStyle Normal -PassThru
Write-Output "AUDIT_PID $($process.Id)"
if(-not $process.WaitForExit(600000)){Stop-Process -Id $process.Id;throw 'Audit exceeded 600 seconds'}
Write-Output "AUDIT_EXIT $($process.ExitCode)"
