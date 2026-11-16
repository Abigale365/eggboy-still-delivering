param(
    [ValidateSet('auto','ble','usb')][string]$Transport = 'auto',
    [string]$Address = 'D4:C1:9F:39:80:C6',
    [int]$Seconds = 0
)
$relay = Join-Path $PSScriptRoot '..\..\Assets\StreamingAssets\EasyInputNative\EasyInputRelay.exe'
& $relay --transport $Transport --address $Address --seconds $Seconds
exit $LASTEXITCODE
