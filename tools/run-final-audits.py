"""终版审计：先归档已有结果，不删除任何旧文件。"""
from pathlib import Path
import subprocess, time, json, shutil, datetime
import argparse

parser=argparse.ArgumentParser()
parser.add_argument('--only',choices=['story','innov'])
options=parser.parse_args()
modes=[options.only] if options.only else ['story','innov']

PROJECT=Path(__file__).resolve().parents[1]
BUILD=PROJECT/'Build'/'Main'
STAMP=datetime.datetime.now().strftime('%Y%m%d_%H%M%S')
EVIDENCE=PROJECT/'docs'/'evidence'/('final-trailer-'+STAMP)
EVIDENCE.mkdir(parents=True)
BACKUP=EVIDENCE/'审计前旧文件'
BACKUP.mkdir()
for name in [mode+'-audit.json' for mode in modes]+['shots']:
    p=BUILD/name
    if p.exists():
        assert p.resolve().is_relative_to(PROJECT.resolve())
        shutil.move(str(p),str(BACKUP/name))
results={}
for mode in modes:
    argv=[str(BUILD/'TideTown.exe'),'-'+mode+'audit','-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-resetsave','-logFile',str(EVIDENCE/(mode+'-Player.log'))]
    if mode=='innov':argv+=['-lux3d','mock','-lux3ddelay','3']
    start=time.time()
    launcher=PROJECT/'tools'/'run-visible-audit.ps1'
    process=subprocess.Popen(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File',str(launcher),'-Exe',str(BUILD/'TideTown.exe'),'-Mode',mode,'-Log',str(EVIDENCE/(mode+'-Player.log'))],cwd=BUILD)
    target=BUILD/(mode+'-audit.json');finished=None
    while process.poll() is None:
        if target.exists():
            try:
                data=json.loads(target.read_text(encoding='utf-8-sig'))
                if data.get('status') not in ['running',None] and finished is None:finished=time.time()
            except (ValueError,OSError):pass
        if finished and time.time()-finished>15:process.terminate();break
        if time.time()-start>720:process.terminate();raise RuntimeError(mode+' 审计超时')
        time.sleep(1)
    process.wait(timeout=20)
    assert target.exists(),mode+' 未输出结果'
    shutil.copy2(target,EVIDENCE/target.name)
    data=json.loads(target.read_text(encoding='utf-8-sig'));results[mode]=data
    print(mode,'status=',data.get('status'),'elapsed=',round(time.time()-start),flush=True)
    assert data.get('status')=='passed',data
    assert 'TRAILER_SCENE' not in (EVIDENCE/(mode+'-Player.log')).read_text(encoding='utf-8',errors='replace')
    assert 'FILMING_STAGE' not in (EVIDENCE/(mode+'-Player.log')).read_text(encoding='utf-8',errors='replace')
if (BUILD/'shots').exists():shutil.copytree(BUILD/'shots',EVIDENCE/'shots')
(EVIDENCE/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
print('EVIDENCE',EVIDENCE,flush=True)
