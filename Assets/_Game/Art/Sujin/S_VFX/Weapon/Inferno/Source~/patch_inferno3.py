"""① 바닥 줄(GroundGlow) 끄기 ② 움직일 때 불이 무기를 따라 끌려가는 양(속도 물려받기 0.5 → 0.2) · 이동 거리당 방출을 줄인다(줄만 교체)."""
import re, os, time, shutil
PROJ = 'D:/Fork/Newgame/ZombieRun'
PATH = f'{PROJ}/Assets/_Game/Art/Sujin/VFX/Weapon/Inferno/FX_Inferno_Jet.prefab'
st = os.stat(PATH)
print('prefab mtime', time.strftime('%m-%d %H:%M:%S', time.localtime(st.st_mtime)))
shutil.copyfile(PATH, 'FX_Inferno_Jet.before_follow.prefab')
raw = open(PATH, 'rb').read()
crlf = b'\r\n' in raw
text = raw.decode('utf-8').replace('\r\n', '\n')
docs = re.split(r'(?m)^(?=--- !u!)', text)
gos = {}
for i, d in enumerate(docs):
    m = re.match(r'--- !u!1 &(\d+)', d)
    if m:
        gos[m.group(1)] = re.search(r'm_Name: (.*)', d).group(1).strip()
CH = []


def doc_index(cls, name):
    for i, d in enumerate(docs):
        if cls == 1 and re.match(r'--- !u!1 &', d) and re.search(r'm_Name: (.*)', d).group(1).strip() == name:
            return i
        if cls != 1 and d.startswith(f'--- !u!{cls} '):
            go = re.search(r'm_GameObject: \{fileID: (\d+)\}', d)
            if go and gos.get(go.group(1)) == name:
                return i
    raise SystemExit(f'doc not found: {cls} {name}')


def patch_block(doc, block_path, line_re, new_line):
    lines = doc.split('\n'); idx = 0; indent = 0
    for key in block_path:
        found = None
        for j in range(idx, len(lines)):
            l = lines[j]
            if l.strip() == '':
                continue
            cur = len(l) - len(l.lstrip(' '))
            if cur < indent and j > idx:
                break
            if cur == indent and l.strip() == key + ':':
                found = j; break
        if found is None:
            raise SystemExit(f'block not found: {block_path} ({key})')
        idx = found + 1; indent += 2
    count = 0
    for j in range(idx, len(lines)):
        l = lines[j]
        if l.strip() == '':
            continue
        cur = len(l) - len(l.lstrip(' '))
        if cur < indent:
            break
        if cur == indent and re.match(line_re, l.strip()):
            if l.strip() != new_line:
                CH.append('/'.join(block_path) + ': ' + l.strip() + '  ->  ' + new_line)
            lines[j] = ' ' * indent + new_line; count += 1
    if count != 1:
        raise SystemExit(f'expected 1 match for {block_path} {line_re}, got {count}')
    return '\n'.join(lines)


PS = 'ParticleSystem'
# ① 바닥 줄 끄기 — 오브젝트 비활성(사용자가 GroundFire · GroundFlames 를 끈 방식과 같게)
i = doc_index(1, 'GroundGlow')
docs[i] = patch_block(docs[i], ['GameObject'], r'm_IsActive:', 'm_IsActive: 0')
CH.append('GroundGlow GameObject inactive')

# ② 무기 움직임을 따라가는 양 — 속도 물려받기 0.5 → 0.2, 이동 거리당 방출 절반 이하
for name, dist in [('FX_Inferno_Jet', 1.2), ('Shade', 0.6), ('Core', 0.4), ('Licks', 0.4)]:
    i = doc_index(198, name)
    docs[i] = patch_block(docs[i], [PS, 'InheritVelocityModule', 'm_Curve'], r'scalar:', 'scalar: 0.2')
    docs[i] = patch_block(docs[i], [PS, 'EmissionModule', 'rateOverDistance'], r'scalar:', f'scalar: {dist:g}')

out = ''.join(docs)
if crlf:
    out = out.replace('\n', '\r\n')
if os.stat(PATH).st_mtime != st.st_mtime:
    raise SystemExit('prefab changed while patching - rerun')
open(PATH, 'wb').write(out.encode('utf-8'))
print('\n'.join(CH))
print(len(CH), 'changes written')
