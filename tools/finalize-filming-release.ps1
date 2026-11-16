$ErrorActionPreference='Stop'
$project=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$root=(Resolve-Path -LiteralPath (Join-Path $project '../..')).Path
$build=Join-Path $project 'Build/Main'
$launch=Join-Path $root '08_最终提交材料/TideTown-Launch-Test-46a506'
$delivery=Join-Path $root '08_最终提交材料'
foreach($target in @($build,$launch,$delivery)){
    $absolute=[IO.Path]::GetFullPath($target)
    if(-not $absolute.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "路径越界：$absolute"}
}
if(-not(Test-Path -LiteralPath (Join-Path $build 'TideTown.exe'))){throw '缺少已构建的 TideTown.exe'}
$stamp=Get-Date -Format 'yyyyMMdd_HHmmss'
$backup=Join-Path $launch '_旧版备份_20261116'
New-Item -ItemType Directory -Force -Path $backup | Out-Null
$old=@(Get-ChildItem -LiteralPath $launch -Force | Where-Object {$_.Name -ne '_旧版备份_20261116'})
if(Get-ChildItem -LiteralPath $backup -Force | Select-Object -First 1){$backup=Join-Path $backup ("再次更新前_$stamp");New-Item -ItemType Directory -Path $backup | Out-Null}
$inventory=@($old | ForEach-Object { [PSCustomObject]@{name=$_.Name;directory=$_.PSIsContainer;length=$_.Length} })
foreach($item in $old){
    $absolute=[IO.Path]::GetFullPath($item.FullName)
    if(-not $absolute.StartsWith([IO.Path]::GetFullPath($launch)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "移动源越界：$absolute"}
    Move-Item -LiteralPath $item.FullName -Destination $backup
}
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $backup '本次移入清单.json') -Encoding UTF8
$ship=@(Get-ChildItem -LiteralPath $build -Force | Where-Object {$_.Name -in @('TideTown.exe','TideTown_Data','MonoBleedingEdge','UnityCrashHandler64.exe','UnityPlayer.dll')})
foreach($item in $ship){Copy-Item -LiteralPath $item.FullName -Destination $launch -Recurse -Force}
$notice=Join-Path $project 'docs/F5-F9实拍与开发板快捷键说明.md'
Copy-Item -LiteralPath $notice -Destination $launch
$zip=Join-Path $delivery '2026-11-16_TideTown-Windows-终版.zip'
if(Test-Path -LiteralPath $zip){Move-Item -LiteralPath $zip -Destination (Join-Path $delivery "2026-11-16_TideTown-Windows-终版_更新前_$stamp.zip")}
$env:TIDETOWN_RELEASE_ROOT=$launch
$env:TIDETOWN_RELEASE_ZIP=$zip
@'
from pathlib import Path
import os,zipfile
root=Path(os.environ['TIDETOWN_RELEASE_ROOT'])
target=Path(os.environ['TIDETOWN_RELEASE_ZIP'])
with zipfile.ZipFile(target,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for path in root.rglob('*'):
        relative=path.relative_to(root)
        if relative.parts[0].startswith('_'):continue
        if path.is_file():archive.write(path,str(relative))
with zipfile.ZipFile(target) as archive:
    bad=archive.testzip()
    assert bad is None,bad
    assert 'TideTown.exe' in archive.namelist()
    assert not any(p.startswith('_') for p in archive.namelist())
print('ZIP_VALID',target)
'@ | python -
if($LASTEXITCODE -ne 0){throw 'ZIP 验证失败'}
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
[PSCustomObject]@{zip=$zip;sha256=$hash;size=(Get-Item -LiteralPath $zip).Length;launch=$launch;backup=$backup;generated=(Get-Date -Format o)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $delivery '2026-11-16_TideTown-Windows-终版_SHA256.json') -Encoding UTF8
Write-Output "RELEASE_OK SHA256=$hash"
