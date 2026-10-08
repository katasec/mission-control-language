import json,subprocess,signal,time
from pathlib import Path
from datetime import datetime,timezone
root=Path(Path('/tmp/forge-responses-default-path.txt').read_text().strip())
out=Path('/Users/ameerdeen/.codex/artifacts/responses-provider/default-acceptance')
started=datetime.now(timezone.utc).isoformat()
p=subprocess.Popen(['/Users/ameerdeen/.local/bin/forge','run',str(root/'cancel/mission.mcl'),'--steps'],cwd=root/'workspace',stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True)
lines=[]
try:
 while True:
  line=p.stderr.readline()
  if not line: raise RuntimeError('No command admission observed before exit')
  lines.append(line)
  if '→ Probe' in line: break
 p.send_signal(signal.SIGINT)
 stdout,stderr=p.communicate(timeout=30)
 stderr=''.join(lines)+stderr
 (out/'cancel-stdout.txt').write_text(stdout)
 (out/'cancel-stderr.txt').write_text(stderr)
 assert p.returncode!=0,(p.returncode,stdout,stderr)
 assert 'cancel' in stderr.lower(),stderr
 assert not stdout.strip(),stdout
 record={'case':'cancellation','started_at':started,'finished_at':datetime.now(timezone.utc).isoformat(),'exit_code':p.returncode,'outcome':'PASS','signal':'SIGINT after observed Probe admission','stderr':stderr,'stdout':stdout}
 (out/'cancellation.json').write_text(json.dumps(record,indent=2)+'\n')
 print(json.dumps(record))
finally:
 if p.poll() is None: p.kill();p.wait()

