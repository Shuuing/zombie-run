"""FX_Inferno_Jet.prefab 을 지우지 않고 줄만 바꾼다: ① 시작점(knobs.muzzleForward 0.15 → 0) ② GroundGlow 원형 점점점 → 바닥에 누운 줄 하나(Quad 메시 · 로컬 정렬 · 3D 크기)."""
import re, os, json, time, shutil
PROJ = 'D:/Fork/Newgame/ZombieRun'
PATH = f'{PROJ}/Assets/_Game/Art/Sujin/VFX/Weapon/Inferno/FX_Inferno_Jet.prefab'
G = json.load(open('sheet/guids_inferno.json'))
st = os.stat(PATH)
print('prefab mtime', time.strftime('%m-%d %H:%M:%S', time.localtime(st.st_mtime)))
shutil.copyfile(PATH, 'FX_Inferno_Jet.before_strip.prefab')
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


def doc_index(cls, name=None, contains=None):
    for i, d in enumerate(docs):
        if not d.startswith(f'--- !u!{cls} '):
            continue
        if contains is not None and contains in d:
            return i
        if name is not None:
            go = re.search(r'm_GameObject: \{fileID: (\d+)\}', d)
            if go and gos.get(go.group(1)) == name:
                return i
    raise SystemExit(f'doc not found: {cls} {name or contains}')


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
# ① 시작점을 총구로 — 맞춤값 muzzleForward 0.15 → 0
i = doc_index(114, contains='knobs:')
docs[i] = patch_block(docs[i], ['MonoBehaviour', 'knobs'], r'muzzleForward:', 'muzzleForward: 0')

# ② GroundGlow — 렌더러: 메시(Quad) · 로컬 정렬 · 줄 머티리얼
i = doc_index(199, 'GroundGlow')
docs[i] = patch_block(docs[i], ['ParticleSystemRenderer'], r'm_RenderMode:', 'm_RenderMode: 4')
docs[i] = patch_block(docs[i], ['ParticleSystemRenderer'], r'm_RenderAlignment:', 'm_RenderAlignment: 2')
docs[i] = patch_block(docs[i], ['ParticleSystemRenderer'], r'm_Mesh:', 'm_Mesh: {fileID: 10210, guid: 0000000000000000e000000000000000, type: 0}')
new, n = re.subn(r'(m_Materials:\n\s*- \{fileID: 2100000, guid: )[0-9a-f]{32}', lambda m: m.group(1) + G['M_Inferno_GroundStrip'], docs[i])
if n != 1:
    raise SystemExit('GroundGlow material line not found')
docs[i] = new; CH.append('GroundGlow renderer material -> M_Inferno_GroundStrip')

# ② GroundGlow — 시스템: 로컬 공간 · 3D 크기(폭 1.6~1.9, 길이 4.6~5.2) · X 90° 눕힘 · 모양 거의 점 · 방출 12/s
i = doc_index(198, 'GroundGlow')
docs[i] = patch_block(docs[i], [PS], r'moveWithTransform:', 'moveWithTransform: 0')
docs[i] = patch_block(docs[i], [PS, 'InitialModule'], r'size3D:', 'size3D: 1')
docs[i] = patch_block(docs[i], [PS, 'InitialModule'], r'rotation3D:', 'rotation3D: 1')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSize'], r'scalar:', 'scalar: 1.9')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSize'], r'minScalar:', 'minScalar: 1.6')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSizeY'], r'minMaxState:', 'minMaxState: 3')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSizeY'], r'scalar:', 'scalar: 5.2')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSizeY'], r'minScalar:', 'minScalar: 4.6')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSizeZ'], r'minMaxState:', 'minMaxState: 0')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startSizeZ'], r'scalar:', 'scalar: 1')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startRotationX'], r'minMaxState:', 'minMaxState: 0')
docs[i] = patch_block(docs[i], [PS, 'InitialModule', 'startRotationX'], r'scalar:', 'scalar: 1.5707964')
docs[i] = patch_block(docs[i], [PS, 'ShapeModule'], r'm_Scale:', 'm_Scale: {x: 0.25, y: 0, z: 0.5}')
docs[i] = patch_block(docs[i], [PS, 'EmissionModule', 'rateOverTime'], r'scalar:', 'scalar: 12')

out = ''.join(docs)
if crlf:
    out = out.replace('\n', '\r\n')
if os.stat(PATH).st_mtime != st.st_mtime:
    raise SystemExit('prefab changed while patching - rerun')
open(PATH, 'wb').write(out.encode('utf-8'))
print('\n'.join(CH))
print(len(CH), 'changes written')
