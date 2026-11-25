# 무기 모델 (2026-09-08)

캐릭터가 손에 드는 무기 FBX. **이 폴더를 건드리기 전에 이 문서를 먼저 읽을 것.**

## 지금 있는 것

| FBX | 기획서 §05 자리 | tri | FBX 루트 스케일 |
|---|---|---|---|
| `shotgun_01.fbx` | 산탄총 — 브레이커 | 1,306 | 0.8588 |
| `Flame_thrower_01.fbx` | 화염방사기 — 파이로 | 1,400 | 1.3677 |
| `rocket_launcher_01.fbx` | 로켓 발사기 — 데몰리셔 | 1,544 | 1.1056 |
| `Balkan_gun_01.fbx` | **탕탕 캐논** — 루키 *진화형* | 2,886 | 1.0 |
| `orbit_blade_01.fbx` | 회전 블레이드 — **보조 장비** | 228 | 1.0 |
| `support_drone_01.fbx` | 지원 드론 — **보조 장비** | 508 | 1.0 |
| `rocket_projectile_01.fbx` | 로켓 **발사체** | 138 | 1.0 |

### 발사체는 단위 규약이 다르다 — 길이 1

손에 드는 무기는 약 5 단위로 만들어 배율 `0.090277` 을 곱하지만, **발사체는
`WeaponStats.projectileSize` 가 스케일로 그대로 들어간다.** 그래서 길이 **1 단위**로 만든다 —
그러면 인게임 길이가 곧 `projectileSize` 다(로켓 0.34 → 0.35m). 이 규약을 섞으면 발사체가
5배 크게 날아간다.

**코는 +Z 로 만든다.** `Quaternion.LookRotation` 이 +Z 를 겨누므로 보정 회전이 필요 없다
(`projectileFacesTravel`). 블레이드·드론처럼 `modelRotationOffset` 을 따로 잡을 일이 없다.

모델이 비어 있는 무기는 예전처럼 노란 구슬이 날아간다 — 로켓만 모델이 물려 있다.
풀에서 재사용될 때 **지금 어떤 모델을 달고 있는지 기억해서 같으면 건드리지 않는다**
(`ProtoProjectile.ApplyVisual`). 매 발사마다 Instantiate/Destroy 하면 풀링이 무의미해진다.

소총(`탕탕 소총`, Lv1)은 여기가 아니라 `../fbx/weapon_celebration_01.fbx` 다.

**아래 둘은 임시 모델**이다 — `ArtSource/blender/make_placeholder_weapons.py` 가 만든다(결정론, 다시 돌리면 같은 결과).
아트 모델이 오면 FBX 만 갈아끼우면 되고 `WeaponModelDef` 는 그대로 쓴다.

### 손에 쥐지 않는 무기 — `WeaponMount`

블레이드는 몸 주위를 돌고 드론은 떠다닌다. 그래서 `WeaponModelDef.mount` 가 `Hand` 가 아니고,
`WeaponSocket.Equip()` 이 **손에 물리는 것을 건너뛴다**(위치는 런타임이 정한다).

> **legacy 함정**: `mount != Hand` 일 때 조기 반환하지 않으면 `Equip` 이 legacy `weaponPrefab` 으로
> fallback 해서 **리퍼가 소총을 든다.** 그래서 조기 반환 + `CharacterSetup` 이 legacy 필드까지 비운다.

그립 좌표(`gripPoint`/`weaponEuler`/`calibrated`)는 이 둘에 대해 **의미가 없다.**
그립 툴 목록에서 `—` 로 표시되고 [씬에 셋업] 이 잠긴다.

머티리얼은 `M_Blade_PH.mat` / `M_Drone_PH.mat` (`weapon_01.mat` 을 복제해 텍스처만 뺀 단색).
복제로 만든 이유는 툰 램프·외곽선 설정을 손으로 다시 맞추면 반드시 하나가 어긋나기 때문이다
(외곽선 CONST_SIZE 를 실제로 한 번 놓쳤다 — 아래 참조).

**발칸포를 진화 무기로 둔 이유**: 캐릭터 6종·무기 6종 구조를 깨지 않으면서 발칸이 들어갈
유일한 자리다. 탕탕 캐논은 발사 간격 0.08s 연사라 개틀링 그림이 그대로 맞고,
§05 가 "진화 시 주무기는 완전히 새 무기로 교체된다"고 이미 규정하고 있어 기획서를 고칠 게 없다.
캐릭터를 7종으로 늘리는 안은 버렸다 — "7종 이상은 무기를 새로 만들지 말고 같은 무기 + 다른 패시브"가
캐릭터 확장 방어선 중 하나다.

## 머티리얼

4종이 **`weapon_01.mat` 한 장을 공유**한다(`weapon_01_d` + `weapon_01_normal`, `BCM/BCM_Toony_shader`).

- 연결은 FBX 임포터의 **머티리얼 리맵**으로 걸려 있다(`.meta` 에 저장). **FBX 를 다시 뽑아도 살아남는다.**
  단 임베디드 머티리얼 이름이 바뀌면 리맵 키가 안 맞아 조용히 풀린다 — 그때는 다시 걸어야 한다.
  현재 키는 `13 - Default`(발칸·화염·샷건) / `Material`(로켓)이다.
- `rocket_launcher_01` 만 다른 배치에서 뽑혀 임베디드 머티리얼이 필드 프롭 텍스처
  (`texture_pbr_20250901`) 이름을 참조한다. **UV 는 `weapon_01_d` 아틀라스와 맞다** — 렌더로 확인했다.
- 임포터 설정에서 `materialLocation` 을 **External 로 바꾸지 말 것.** 리임포트 때 텍스처 이름으로
  머티리얼을 새로 뽑아 `Materials/` 폴더를 만들고, 그 과정에서 머티리얼 이름이 바뀌어 리맵 키가 깨진다.
- **`weapon_01_metallic.png` / `weapon_01_roughness.png` 는 아무도 안 쓴다.** 툰 셰이더가
  PBR 메탈릭/러프니스 워크플로를 받지 않는다. 참조가 없으니 빌드에는 안 들어가지만 소스로만 남아 있다.

### 외곽선 — 한 번 깨졌던 곳

`weapon_01.mat` 은 처음에 `_OutlineWidth = 1`, **CONST_SIZE 키워드 없음** 상태였고,
그러면 셰이더가 `_OutlineWidth / 100` 을 **클립 공간에 그대로 더한다**(`BCM_Toony_Include.cginc:2091`).
거리와 무관한 화면 비율이라, 손에 든 총처럼 화면에서 작은 물체는 통째로 검은 덩어리가 된다.

캐릭터 바디(0.27)·기존 소총(0.38)은 전부 `TCP2_OUTLINE_CONST_SIZE`(= 월드 두께 고정) 경로다.
**무기 머티리얼을 새로 만들면 `_OutlinePixelSizeType = 1` + 키워드 `TCP2_OUTLINE_CONST_SIZE` 를
반드시 같이 켤 것.** float 만 바꾸면 안 켜진다.

## 손에 물리기

값은 무기별 에셋 `Assets/_Game/Data/Weapons/W_*.asset`(`WeaponModelDef`)에 있다.
캐릭터 프리팹의 `WeaponSocket` 에는 더 이상 그립 값을 넣지 않는다 — 무기 5종 × 캐릭터 6종이면
같은 숫자를 30곳에 복사하게 된다.

**맞추는 법**: `Tools ▸ 좀비 오버런 ▸ 무기 그립 맞추기` → 무기 선택 → [씬에 셋업] →
씬 뷰에서 **무기 오브젝트만** 이동·회전(크기는 창의 슬라이더로) → [에셋에 저장] → [정리].
창의 `● / ○` 가 눈으로 맞춘 값인지 아직 기본값인지를 표시한다.

> 그립은 **계산으로 안 나온다.** 지오메트리로 손잡이를 추정했다가 아래로 뻗은 세 덩어리 중
> 엉뚱한 것을 짚은 적이 있고, 손목이 꺾인 채 쥐는 각도는 축 정렬로도 안 나온다.
> 그래서 툴이 하는 일은 "사람이 맞춘 배치를 숫자로 역산해 에셋에 굽는 것" 하나뿐이다.

### 함정 2가지

1. **무기 루트의 자체 스케일을 1 로 덮지 말 것.** FBX 루트에 아티스트가 잡아둔 비율이 구워져 있다
   (화염방사기 1.3677, 로켓 1.1056, 샷건 0.8588). 예전 `WeaponSocket` 은 무기 스케일을 1 로
   덮어썼는데, 소총은 원래 1 이라 아무 문제가 없었고 새 무기 4종에서만 터진다.
   그래서 배율은 **전 무기 공통 `0.090277` 하나**면 되고, 아티스트가 잡은 상대 크기가 그대로 나온다
   (실측 월드 길이: 샷건 0.43 → 소총 0.46 → 화염 0.46 → 캐논 0.50 → 로켓 0.53 m).
2. **`gripPoint` 는 소켓 공간 좌표**다 — 모델 루트 스케일이 이미 곱해진 뒤의 값이다.
   메시 로컬 좌표로 착각하면 스케일이 1 이 아닌 무기에서만 어긋난다.

## 블렌더로 다시 뽑을 때

**Blender 는 `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe`** 다
(예전 메모의 `E:\...\Blender 4.4` 는 exe 가 없는 껍데기 폴더만 남아 있다).

```
"C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup \
    --python ArtSource/blender/make_placeholder_weapons.py
```

**FBX 익스포트에서 `apply_scale_options='FBX_SCALE_ALL'` 을 반드시 줄 것.**
기본값(`FBX_SCALE_NONE`, "All Local")은 cm 환산을 **오브젝트 로컬 스케일에 얹어서**
유니티에서 `메시 0.05 단위 + 루트 스케일 100` 으로 들어온다. 월드 크기는 맞으니 눈으로는
안 보이지만 "메시는 약 5 단위" 규약이 깨진다. `apply_unit_scale=True` 로 바꿔도 결과는 같다 —
고치는 손잡이는 `apply_scale_options` 쪽이다.

**인게임에서 더 크게 보여야 하면 배율이 아니라 모델을 키운다.** 블레이드를 처음 5.1 단위(=0.46m)로
뽑았더니 반경 2.5m 궤도에서 점처럼 보였고, 배율을 무기별로 다르게 두는 대신 스크립트의 `K` 를
1.83 으로 올려 9.4 단위(=0.85m)로 만들었다. 배율 상수는 전 무기 공통 `0.090277` 하나로 남는다.

## 확인 렌더

- `Assets/Screenshots/weapons_check.png` — 4종 머티리얼 확인 (측면)
- `Assets/Screenshots/weapons_in_hand.png` — 손에 물린 상태 (좌→우: 소총·샷건·화염·로켓·캐논).
  소총 외 4종은 아직 그립 미보정이라 손에서 떨어져 있는 것이 정상이다.
- `Assets/Screenshots/weapons_placeholder.png` — 임시 무기 2종을 인게임 카메라 각도(피치 58°)로 본 것.
  좌 = 리퍼 + 블레이드 3개(반경 2.5m), 우 = 테크니션 + 드론.
  **§05 의 궤도 반경 2.5m 는 키 1.70m 캐릭터 기준으로 상당히 넓다**(반경이 키의 1.5배).
  블레이드를 0.85m 로 키워도 링이 커서 플레이어가 그 안의 점처럼 보인다 — 수치 재검토 대상.

## v2 — Higgsfield + Tripo 로 전부 다시 만듦 (2026-09-30)

`v2/` 폴더. **옛 FBX·텍스처·머티리얼은 하나도 지우지 않았다.** 무기 에셋은 이렇게 바꿨다:

- `W_*.asset` 의 **model 만** 새 FBX 로 교체(그립·배율 값은 그대로). 옛 값 전체는 `W_*_v1.asset` 에 백업.
  되돌리려면 `W_*.asset` 의 model 을 `_v1` 의 model 로 되돌리면 끝(참조하는 프리팹·씬은 손댈 게 없다).
- 로켓 발사체는 `Stats/WS_RocketLauncher.asset` 의 `projectileModel` 을 교체, 백업 `WS_RocketLauncher_v1.asset`.

| 새 FBX | 무기 에셋 | tri | 기준 |
|---|---|---|---|
| `rifle_02` | W_Rifle_TangTang | 1,402 | 옛 weapon_celebration_01 |
| `shotgun_02` | W_Shotgun | 1,436 | shotgun_01 |
| `Flame_thrower_02` | W_Flamethrower | 1,486 | Flame_thrower_01 |
| `rocket_launcher_02` | W_RocketLauncher | 1,384 | rocket_launcher_01 |
| `Balkan_gun_02` | W_TangTangCannon | 1,699 | Balkan_gun_01 |
| `support_drone_02` | W_SupportDrone | 992 | support_drone_01 |
| `orbit_blade_02` | W_OrbitBlade | 972 | orbit_blade_01 |
| `rocket_projectile_02` | WS_RocketLauncher.projectileModel | 497 | rocket_projectile_01 |

**핵심: 새 모델을 옛 모델의 바운즈에 정확히 맞춰 뽑았다** — 길이(긴 축)·바운즈 중심이 옛 모델(로컬 바운즈 × 옛 루트 스케일)과
소수 둘째 자리까지 같다. 그래서 같은 그립 값을 쓰면 손 기준으로 거의 같은 자리에 물린다(`Screenshots/weapons_v2_grip.png`:
위 = 옛, 아래 = 새, 분홍 점 = 손). 새 FBX 루트 스케일은 1(옛 1.37·1.11·0.86 은 바운즈 계산에 접어 넣음).
캐논·소총은 옛 모델로 보정(`calibrated`)된 값이라 **쥐는 자리가 모델마다 조금 달라 플레이 모드에서 그립 툴로 한 번 다듬을 것.**

제작: 원본·스크립트 `ArtSource/Weapons_v2/` — 컨셉(GPT Image 2, 옆모습·총구 오른쪽·두꺼운 형태) → Tripo(face_limit **최소 1000**)
→ `prep_weapon.py`. **Tripo 축 함정 2개**: (1) 이미지 가로가 블렌더 **Y** 로 나온다 → Z 90° (2) 그러고 나면 총구가 **-X** → flipx.
또 **FBX 임포트가 X 를 뒤집어** 유니티에서 잰 중심 X 는 블렌더에 넣을 때 부호를 바꿔야 한다(처음에 반대로 넣어 중심이 거울상이 됐다).
원판·드론은 3/4 뷰 컨셉이라 PCA 로 가장 얇은 축을 Z 로 눕힌다(`flat`). 발사체는 `nose_z`(코 +Z).
작은 모델을 너무 줄이면(972→597) 면이 조각나 보였다 — 블레이드 972 유지, 발사체 500.
머티리얼 `v2/M_*.mat` = `weapon_01.mat` 복제(외곽선 CONST_SIZE 유지) + 새 텍스처, 옛 노멀맵은 옛 UV 용이라 뺐다. 텍스처 1024(블레이드·발사체 512).
전체 모습 `Screenshots/weapons_v2_all.png`.

## 진화 무기 모델 (2026-10-01)

기획서 §05 진화 4종 중 모델이 필요한 것. 탕탕 캐논은 이미 `Balkan_gun_02`. **진화 시스템이 아직 없어 아무도 참조하지 않는다.**

| 모델 | 무기 에셋 | tri | 규칙 |
|---|---|---|---|
| `v2/slug_rail_01` | `W_SlugRail` (산탄총 진화) | 964 | 산탄총과 길이·중심 동일 → 그립 값 복사 |
| `v2/inferno_01` | `W_Inferno` (화염방사기 진화) | 947 | 화염방사기와 동일 → 그립 값 복사 |
| `v2/airstrike_radio_01` | `W_AirstrikeRadio` (로켓 → **융단 폭격**) | 796 | 손에 드는 건 무전기. 세운 자세(안테나 +Z), **중심을 소총의 손 위치(블렌더 -0.69,-0.54,-0.21)에** — 소총 그립 값 복사 |
| `v2/airstrike_bomb_01` | (없음 — 폭격 시스템 때 연결) | 496 | 하늘에서 떨어지는 미사일. 로켓 발사체 규약(길이 1.04, 코 +Z) |

융단 폭격은 "화면 랜덤 지점 폭격"이라 사용자 결정으로 **손에는 무전기, 화면에는 미사일**. 그립은 셋 다 `calibrated=false` — 그립 툴로 다듬을 것.
블레이드 스톰·드론 편대(보조 장비 Lv5)는 선택 사항이라 아직 안 만들었다. 확인 `Screenshots/weapons_evo_grip.png`(위 기반 / 아래 진화).
