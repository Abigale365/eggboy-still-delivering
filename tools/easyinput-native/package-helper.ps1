$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..\..')
try {
    python -m PyInstaller --noconfirm --onefile --name EasyInputRelay --distpath Assets/StreamingAssets/EasyInputNative --workpath tools/easyinput-native/package-work --specpath tools/easyinput-native --collect-all winrt.windows.devices.bluetooth --collect-all winrt.windows.devices.bluetooth.genericattributeprofile --collect-all winrt.windows.storage.streams tools/easyinput-native/listener.py
    if ($LASTEXITCODE -ne 0) { throw 'EasyInput helper packaging failed' }
}
finally { Pop-Location }
