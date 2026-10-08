import hashlib,json,os,subprocess,time
from pathlib import Path
from datetime import datetime,timezone
root=Path('/tmp/forge-responses-default-path.txt').read_text().strip()
root=Path(root)
workspace=root/'workspace'
forge=Path('/Users/ameerdeen/.local/bin/forge')
evidence=Path('/Users/ameerdeen/.codex/artifacts/responses-provider/default-acceptance')
evidence.mkdir(exist_ok=True)
record={'started_at':datetime.now(timezone.utc).isoformat(),'artifact':str(forge),'version':subprocess.check_output([forge,'--version'],text=True).strip(),'sha256':hashlib.sha256(forge.read_bytes()).hexdigest(),'cwd':str(workspace),'provider':'openai','model':'gpt-6.1-sol','endpoint_override':False,'observations':[]}
assert '4976129a15a5961421134079b520f15f3d6a2cd5' in record['version']
for name in ['files','plain','cancel']:
    result=subprocess.run([forge,'init',root/name/'mission.mcl'],cwd=workspace,text=True,capture_output=True,timeout=30)
    (evidence/f'{name}-init.log').write_text(result.stdout+result.stderr)
    assert result.returncode==0,(name,result.stderr)
for name,target in [('files','probe.txt'),('outside',str(root/'outside.txt')),('symlink','escape/outside.txt'),('plain','')]:
    if name=='symlink': (workspace/'escape').symlink_to(root,target_is_directory=True)
    mission=root/('plain' if name=='plain' else 'files')/'mission.mcl'
    command=[forge,'run',mission,'--steps']
    if target: command+=['--var',f'targetPath={target}']
    start=time.monotonic()
    result=subprocess.run(command,cwd=workspace,text=True,capture_output=True,timeout=180)
    (evidence/f'{name}-stdout.txt').write_text(result.stdout)
    (evidence/f'{name}-stderr.txt').write_text(result.stderr)
    assert result.returncode==0,(name,result.stderr)
    if name=='files':
        assert (workspace/'probe.txt').read_bytes()==b'hands-default-ok'
        assert result.stdout.strip()=='hands-default-ok',result.stdout
    elif name=='plain': assert result.stdout.strip()=='tool-free-ok',result.stdout
    else:
        assert 'ERROR [Failed]' in result.stdout,result.stdout
        assert (root/'outside.txt').read_text()=='outside-sentinel'
    record['observations'].append({'case':name,'exit_code':result.returncode,'elapsed_seconds':round(time.monotonic()-start,3),'outcome':'PASS','stdout':result.stdout.strip()})
    (evidence/'report.json').write_text(json.dumps(record,indent=2)+'\n')
    print(name,'PASS',flush=True)
record['finished_at']=datetime.now(timezone.utc).isoformat()
(evidence/'report.json').write_text(json.dumps(record,indent=2)+'\n')
