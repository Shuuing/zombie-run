"""불꽃 모양 되살리기: 불줄기 · 그늘 · 심은 둥근 빌보드로 되돌리고(끝 크기 축소는 유지), 혀(Licks)에 안 쓰던 혀 텍스처를 가산으로 입혀 속도 방향으로 세운다."""
import re, os, time, shutil, uuid, io
PROJ = 'D:/Fork/Newgame/ZombieRun'
DIR = f'{PROJ}/Assets/_Game/Art/Sujin/VFX/Weapon/Inferno'
PATH = f'{DIR}/FX_Inferno_Jet.prefab'
TONGUE_TEX = '4d01eda249ff76f44a018f28709bb504'
EMBER_TEX = '392719ce3da9f9b4e9eacc34615e3877'

# ── 가산 혀 머티리얼: 잔불(가산) 머티리얼 복사본에 혀 텍스처만 ──
mat = io.open(f'{DIR}/M_Inferno_Ember.mat', encoding='utf-8').read()
assert mat.count(EMBER_TEX) >= 1
mat = mat.replace(EMBER_TEX, TONGUE_TEX)
mat = re.sub(r'^  m_Name: M_Inferno_Ember$', '  m_Name: M_Inferno_TongueAdd', mat, count=1, flags=re.M)
assert 'm_Name: M_Inferno_TongueAdd' in mat
mat_guid = uuid.uuid4().hex
io.open(f'{DIR}/M_Inferno_TongueAdd.mat', 'w', encoding='utf-8', newline='\n').write(mat)
meta = io.open(f'{DIR}/M_Inferno_Ember.mat.meta', encoding='utf-8').read()
io.open(f'{DIR}/M_Inferno_TongueAdd.mat.meta', 'w', encoding='utf-8', newline='\n').write(re.sub(r'^guid: [0-9a-f]{32}', f'guid: {mat_guid}', meta, flags=re.M))
print('M_Inferno_TongueAdd', mat_guid)

st = os.stat(PATH)
print('prefab mtime', time.strftime('%m-%d %H:%M:%S', time.localtime(st.st_mtime)))
shutil.copyfile(PATH, 'FX_Inferno_Jet.before_shape.prefab')
raw = open(PATH, 'rb').read()
crlf = b'\r\n' in raw
text = raw.decode('utf-8').replace('\r\n', '\n')
docs = re.split(r'(?m)^(?=--- !u!)', text)
gos = {}
for d in docs:
    m = re.match(r'--- !u!1 &(\d+)', d)
    if m:
        gos[m.group(1)] = re.search(r'm_Name: (.*)', d).group(1).strip()
CH = []


def doc_index(cls, name):
    for i, d in enumerate(docs):
        if d.startswith(f'--- !u!{cls} '):
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


PS, PSR = 'ParticleSystem', 'ParticleSystemRenderer'
# ① 불줄기 · 그늘 · 심 — 둥근 빌보드로(퍼프 윤곽이 곧 불꽃 모양). 끝 크기 축소 곡선은 그대로
for name in ['FX_Inferno_Jet', 'Shade', 'Core']:
    i = doc_index(199, name)
    docs[i] = patch_block(docs[i], [PSR], r'm_RenderMode:', 'm_RenderMode: 0')
    docs[i] = patch_block(docs[i], [PSR], r'm_LengthScale:', 'm_LengthScale: 2')
    docs[i] = patch_block(docs[i], [PSR], r'm_VelocityScale:', 'm_VelocityScale: 0')

# ② 혀(Licks) — 혀 텍스처(가산) · 속도 방향 정렬 · 조금 크게 · 더 많이 · 기울기 ±20°
i = doc_index(199, 'Licks')
new, n = re.subn(r'(m_Materials:\n\s*- \{fileID: 2100000, guid: )[0-9a-f]{32}', lambda m: m.group(1) + mat_guid, docs[i])
if n != 1:
    raise SystemExit('Licks material line not found')
docs[i] = new; CH.append('Licks renderer material -> M_Inferno_TongueAdd')
docs[i] = patch_block(docs[i], [PSR], r'm_RenderAlignment:', 'm_RenderAlignment: 4')
i = doc_index(198, 'Licks')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSize'], r'scalar:', 'scalar: 0.85')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSize'], r'minScalar:', 'minScalar: 0.5')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startRotation'], r'scalar:', 'scalar: 0.35')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startRotation'], r'minScalar:', 'minScalar: -0.35')
docs[i] = patch_block(docs[i], [PS, 'EmissionModule', 'rateOverTime'], r'scalar:', 'scalar: 32')

out = ''.join(docs)
if crlf:
    out = out.replace('\n', '\r\n')
if os.stat(PATH).st_mtime != st.st_mtime:
    raise SystemExit('prefab changed while patching - rerun')
open(PATH, 'wb').write(out.encode('utf-8'))
print('\n'.join(CH))
print(len(CH), 'changes written')
