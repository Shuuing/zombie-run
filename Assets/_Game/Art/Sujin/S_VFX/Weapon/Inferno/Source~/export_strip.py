"""바닥 줄 텍스처(T_Inferno_GroundStrip) + 머티리얼(M_Inferno_GroundStrip, GroundGlow 복사본에 텍스처만 교체) 을 인페르노 폴더에 쓴다."""
import re, json, shutil, uuid, io
PROJ = 'D:/Fork/Newgame/ZombieRun'
DIR = f'{PROJ}/Assets/_Game/Art/Sujin/VFX/Weapon/Inferno'
guids = {}


def write(path, text):
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)


tex_tpl = io.open(f'{DIR}/T_Inferno_Dot.png.meta', encoding='utf-8').read()
mat_tpl = io.open(f'{DIR}/M_Inferno_GroundGlow.mat.meta', encoding='utf-8').read()
shutil.copyfile('sheet/out/T_Inferno_GroundStrip.png', f'{DIR}/T_Inferno_GroundStrip.png')
guids['T_Inferno_GroundStrip'] = uuid.uuid4().hex
write(f'{DIR}/T_Inferno_GroundStrip.png.meta', re.sub(r'^guid: [0-9a-f]{32}', f"guid: {guids['T_Inferno_GroundStrip']}", tex_tpl, flags=re.M))

m = io.open(f'{DIR}/M_Inferno_GroundGlow.mat', encoding='utf-8').read()
assert m.count('00ca1d50c475e0746a817c58ff580b7b') >= 1
m = m.replace('00ca1d50c475e0746a817c58ff580b7b', guids['T_Inferno_GroundStrip'])
m = re.sub(r'^  m_Name: M_Inferno_GroundGlow$', '  m_Name: M_Inferno_GroundStrip', m, count=1, flags=re.M)
assert 'm_Name: M_Inferno_GroundStrip' in m
guids['M_Inferno_GroundStrip'] = uuid.uuid4().hex
write(f'{DIR}/M_Inferno_GroundStrip.mat', m)
write(f'{DIR}/M_Inferno_GroundStrip.mat.meta', re.sub(r'^guid: [0-9a-f]{32}', f"guid: {guids['M_Inferno_GroundStrip']}", mat_tpl, flags=re.M))
json.dump(guids, open('sheet/guids_inferno.json', 'w'), indent=1)
print(json.dumps(guids, indent=1))
