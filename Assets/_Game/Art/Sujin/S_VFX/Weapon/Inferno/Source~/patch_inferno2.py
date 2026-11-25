"""인페르노 불줄기 앞부분 동글동글 → 줄기: 불줄기 · 그늘 · 심 세 시스템을 늘임 빌보드로, 수명 끝 크기 성장을 줄인다(줄만 교체)."""
import re, os, time, shutil
PROJ = 'D:/Fork/Newgame/ZombieRun'
PATH = f'{PROJ}/Assets/_Game/Art/Sujin/VFX/Weapon/Inferno/FX_Inferno_Jet.prefab'
st = os.stat(PATH)
print('prefab mtime', time.strftime('%m-%d %H:%M:%S', time.localtime(st.st_mtime)))
shutil.copyfile(PATH, 'FX_Inferno_Jet.before_stretch.prefab')
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


def set_line(i, key, value):
    """문서 최상위(들여쓰기 2) 한 줄."""
    pat = re.compile(rf'^  {re.escape(key)}: (.*)$', re.M)
    m = pat.search(docs[i])
    if not m:
        raise SystemExit(f'line not found: {key}')
    if m.group(1) != str(value):
        CH.append(f'{key}: {m.group(1)} -> {value}')
    docs[i] = pat.sub(f'  {key}: {value}', docs[i], count=1)


def key_text(keys, indent=8):
    """AnimationCurve 키 목록 — 구간별 직선 기울기(예측 가능한 꺾은선)."""
    out = []
    n = len(keys)
    for k, (t, v) in enumerate(keys):
        sl_in = (v - keys[k-1][1]) / (t - keys[k-1][0]) if k > 0 else 0.0
        sl_out = (keys[k+1][1] - v) / (keys[k+1][0] - t) if k < n - 1 else 0.0
        if k == 0: sl_in = sl_out
        if k == n - 1: sl_out = sl_in
        sp = ' ' * indent
        out.append(f'{sp}- serializedVersion: 3\n{sp}  time: {t:g}\n{sp}  value: {v:g}\n{sp}  inSlope: {sl_in:.7g}\n{sp}  outSlope: {sl_out:.7g}\n'
                   f'{sp}  tangentMode: 0\n{sp}  weightedMode: 0\n{sp}  inWeight: {0 if k == 0 else 0.33333334}\n{sp}  outWeight: {0 if k == n-1 else 0.33333334}')
    return '\n'.join(out)


def set_size_curve(name, keys):
    i = doc_index(198, name)
    d = docs[i]
    a = d.find('\n  SizeModule:')
    if a < 0:
        raise SystemExit('SizeModule not found ' + name)
    b = d.find('\n  ', a + 5)                      # 다음 모듈 헤더
    while b >= 0 and d[b+3] == ' ':                 # 더 깊은 들여쓰기면 건너뜀
        b = d.find('\n  ', b + 1)
    blk = d[a:b]
    m = re.search(r'(      maxCurve:\n        serializedVersion: 2\n        m_Curve:\n)(.*?)(\n        m_PreInfinity:)', blk, re.S)
    if not m:
        raise SystemExit('size maxCurve not found ' + name)
    old = re.findall(r'time: ([-\d.e]+)\n\s+value: ([-\d.e]+)', m.group(2))
    new_txt = key_text(keys)
    blk2 = blk[:m.start(2)] + new_txt + blk[m.end(2):]
    docs[i] = d[:a] + blk2 + d[b:]
    CH.append(f'{name} SizeOverLifetime: {[(float(t), float(v)) for t, v in old]} -> {keys}')


for name, length in [('FX_Inferno_Jet', 2.4), ('Shade', 2.4), ('Core', 3)]:
    i = doc_index(199, name)
    set_line(i, 'm_RenderMode', 1)
    set_line(i, 'm_LengthScale', length)
    set_line(i, 'm_VelocityScale', 0.02)

set_size_curve('FX_Inferno_Jet', [(0, 0.14), (0.12, 0.36), (0.4, 0.76), (0.75, 0.9), (1, 0.7)])
set_size_curve('Shade', [(0, 0.2), (0.15, 0.45), (0.45, 0.82), (0.8, 0.95), (1, 0.8)])
set_size_curve('Core', [(0, 0.25), (0.35, 0.8), (1, 0.9)])

out = ''.join(docs)
if crlf:
    out = out.replace('\n', '\r\n')
if os.stat(PATH).st_mtime != st.st_mtime:
    raise SystemExit('prefab changed while patching - rerun')
open(PATH, 'wb').write(out.encode('utf-8'))
print('\n'.join(CH))
print(len(CH), 'changes written')
