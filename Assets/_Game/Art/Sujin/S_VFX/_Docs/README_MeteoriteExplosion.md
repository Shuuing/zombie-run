# 좀비 처치 폭발 — Survivor Ace `MFX_Boss_MeteoriteExplosion` (임시 · 출시 전 반드시 교체)

2026-11-13 사용자 요청 "좀비 죽을 때 MFX_Boss_MeteoriteExplosion 이펙트 가져와서 붙여줘".
원본: `D:\SurvivorAce_1.2.0\UnityProject\ExportedProject\Assets\MFX_Boss_MeteoriteExplosion.prefab` (보스 운석 폭발. 머티리얼만 다른 `_0` 변형은 안 가져옴).
**Survivor Ace 원작사 저작물이다.** 빌드·외부 공개 전에 우리 이펙트로 바꾼 뒤 지운다. 카탈로그가 참조하는 동안 빌드에 들어간다.

## 게임에 나오는 곳

| 카탈로그 ID | 부르는 코드 | 자리 · 크기 | 같이 나는 것 |
|---|---|---|---|
| `fx_zombie_death` | `ProtoZombie.PlayDeathFx` ← `ProtoZombie.Die`(**모든 적** — 잡좀비 · 개 · 원거리 · 엘리트 · 중간/최종 보스 · 불타 죽은 적) · `FxWeaponStage.Kill`(FX 툴 [무기] 탭 더미) | **몸 가운데**, scale = **몸 키 / 1.72m**(워커 키) | 무기 처치 효과(세트 kill 슬롯 — 루키 `rifle_kill` 등 · 공통 `kill_burst`) · 몸 조각 · 피 터짐(`zombie_gore`) |

- 11-13 사용자 "피 터지는 것도 같이 터지게" · "전 이펙트도 살려줘" — 잠깐 숨겼던 무기 처치 효과를 다시 켜서 **대신하지 않고 더한다**.
  피(`Art/VFX/Common/M_Fx_Blood*.mat` 4개)는 같은 자리의 밝은 폭발 밑에 깔려 안 보였다 → 그리기 순서를 3000 → **3010**(`_QueueOffset` 10)으로, 폭발(3000) 위에 그린다.
  URP 파티클 머티리얼은 큐 숫자만 바꾸면 임포트 때 3000 + `_QueueOffset` 으로 되돌아간다. 생성 규칙(`FxTemplatePrefabs.Harden` — `BloodQueue` 3010)도 같이 바꿨다. 바꾸기 전 머티리얼: `_Backup/20261113_BloodQueue/`.

- 11-13 사용자 "fx_zombie_death 네이밍으로 수정, 좀비 죽을 때 공통 이펙트로, 몸 중간에, 좀비 사이즈에 맞춰서" — 프리팹 이름을 사용자가 `fx_zombie_death` 로 바꿨고(GUID 그대로), 카탈로그 ID 도 `zombie_death_blast` → `fx_zombie_death` 로 맞췄다.
- 몸 키 = 죽는 순간 몸 렌더러(스킨 · 메시) 바운즈 윗면 − 발 높이. 적마다 모델 크기가 달라서(개 · 보스) 덩치 반경 대신 실제 키로 잰다. 렌더러가 없으면 덩치 배율(bodyRadius / 0.28)로.
  임시 프로젝트 실측: 개 1.19m → ×0.69 · 워커 1.65m → ×0.96 · 엘리트(워커 ×1.6) 2.44m → ×1.42 · 최종 보스(×1.3) 3.40m → ×1.98.
- 몸 조각(ZombieGibs) · 피 터짐(`zombie_gore`) · 사망 소리는 그대로 같이 난다. 키는 몸 조각을 내기 전(몸을 숨기기 전)에 잰다.
- 수명 1.5초, 동시 12개 — 넘으면 가장 오래된 것부터 거둔다(물량전 보호). 게임 시계(레벨업 카드 중엔 같이 멈춤).
- FX 툴 [무기] 탭 "처치" 슬롯 재생 버튼은 여전히 무기 세트의 kill 효과를 낸다(슬롯 확인용). 더미를 쏴서 죽이면 이 폭발이 나온다.

> 2026-11-23: `fx_zombie_death` · `FX_Car_Explosion` 프리팹은 사용자 작업 폴더 `Assets/_Game/Art/Sujin/VFX/Explosion/` 으로 옮겼다(GUID 그대로 — 카탈로그 연결 그대로). 머티리얼 · 텍스처는 이 폴더에 그대로.

## 원본에서 바꾼 것 (`Prefabs/fx_zombie_death.prefab` → 11-23 `_Game/Art/Sujin/VFX/Explosion/`, 원본 GUID `1c110920…` 그대로)

11-13 오후부터 **사용자가 에디터에서 직접 고치는 프리팹**이다(이름 변경 · 파티클 손질 — 바닥 고리 · 붉은 빛 없이 불 덩어리 위주로 바뀜). 아래는 처음 가져올 때 바꾼 것.
같은 폴더의 `MFX_Boss_MeteoriteExplosion 1.prefab`(GUID `5d656d0d…`)은 사용자가 만든 복제본 — 사용자가 `FX_Car_Explosion` 으로 이름을 바꾸고 손질해(불 · 그을림 · 불꽃 줄을 키움) 탈것 폭발에 쓴다(아래 "탈것 폭발").

## 탈것 폭발 — `FX_Car_Explosion` (11-13, 사용자 복제본 · 손편집 중이라 덮어쓰지 말 것)

사용자 요청 "탱크 터지거나 차량 터질때 헬기가 바닥으로 떨어져서 터질때 이펙트를 FX_Car_Explosion 로". 작은 2차 폭발(`tank_debris_pop` · `heli_crash_pop`)은 그대로 코드 그림.

| 카탈로그 ID | 부르는 코드 | 자리 · 크기 | 코드가 따로 내는 것 |
|---|---|---|---|
| `tank_blast` | `ProtoTank.Destroyed` | 탱크 바닥(높이 0), scale **1.7** (`ProtoTank.BlastFxScale`) | 폭발음 · 풀 눕힘(`ProtoExplosion.Aftermath`), 흔들림 Heavy(원래부터 코드) |
| `van_blast` | `ProtoDestructibles.Explode` | 차 중심 바닥, scale **2** (`ProtoDestructibles.BlastFxScale`) | 흔들림 Huge · 폭발음 · 쿵 · 풀(`ProtoBossFx.ImpactFeel`) |
| `heli_crash_impact` | `ProtoHeliCrash.Impact` | 충돌 지점 바닥, scale **2.2** (`ProtoHeliCrash.BlastFxScale`) | 흔들림 Huge · 폭발음 · 쿵 · 풀(`ProtoBossFx.ImpactFeel`) |

- 셋 다 `VehicleBlastFx.Play`(`Scripts/Fx/ProtoFxBuiltins.cs`)로 부른다 — 카탈로그에 프리팹이 있으면 프리팹 + 위 "따로 내는 것", 프리팹 칸을 비우면 예전 코드 그림(흔들림 · 소리 포함) 그대로.
- **항상 위에(onTop) 켬** — 11-13 처음 연결 뒤 사용자 "이펙트가 안보여". 임시 프로젝트 렌더로 보니 바닥(높이 0)에 놓인 폭발이 그 자리에 남는 탱크 잔해(헬기 잔해도 같은 자리)와 땅에 가려져 거의 안 보였다. 오버레이 카메라(깊이 지움)로 그리게 해 아무것에도 안 가려진다. 끄면 다시 가려진다.
- 크기: scale 1 에서 그을림 지름 약 3.8m · 고리 약 2.8m(최상위 0.8 × `Root` 0.16, 파티클 크기 × 0.128). 처음 배율(0.85 · 1 · 1.1)은 좀비 처치 폭발만 해서 판정 반경(3.6 · 5 · 5.5m) 안에 점처럼 보였다 → 같은 비율로 ×2(1.7 · 2 · 2.2) — 불덩이가 판정 반경의 절반쯤. 조절은 각 `BlastFxScale` 이나 FX 툴 카탈로그 '크기(프리팹 루트)'(세 항목 공유).
- 수명 1.5초(탱크는 1.2 → 1.5). 바꾸기 전 파일: `_Backup/20261113_CarExplosion/`(처음 연결 전) · `FxCatalog_before_ontop.asset`(항상 위에 켜기 전).
- 확인: 임시 프로젝트(게임 카메라 각도 · 42°)에서 빈 바닥 / 탱크 잔해 크기 상자 / 판정 반경 고리와 함께 시각별 렌더. 컴파일 통과. **메인 에디터 플레이 화면은 아직 미확인.**

- **크기**: 최상위 배율 1 → **0.8** (안쪽 `Root` 0.16 은 원본 그대로). 충격파 고리 지름 약 1.8m, 붉은 빛 약 2m — 좀비(키 1.72m)보다 조금 큰 폭발.
- **띄움**: `Root` 를 위로 0.0625(월드 약 5cm). 바닥에 눕는 고리 · 그을림 판이 땅(높이 0)과 겹쳐 반쪽이 가로로 잘려 보였다(z-fighting).
- **반복 끔**: 렌더러가 꺼진 빈 껍데기 둘(최상위 · `Root`)이 5초 반복 · 초당 10개 방출 → 반복 끔 · 방출 0.
- 색 · 모양은 원본 그대로(주황 불 · 붉은 빛 · 흰 섬광).
- 모양 요소(원본 이름): `Glow_Dark` 어두운 그을림 · `Smoke` 연기 · `Quan` 고리 · `Wave` 충격파 · `Mesh_Wind` 반구 바람(메시) · `Ci` 가시 섬광 · `Shitou` 돌 · `Line` 불꽃 줄 · `Lizi` 불티 · `Huo` 불 · `Glow` 빛 · `Yan_liang` 밝은 연기 · `Mesh` 고리(메시) · `Shan` 섬광.

## 파일

- 공유(먼저 들어와 있던 원본 GUID 파일 — 고치지 말 것): `Shaders/Effects_Particle.shader` · `Materials/Glow_011_M3.mat` · `Glow_011_M1T.mat` · `Ring_102_M1.mat` · `Textures/Glow_011.png` · `Glow_011T1.png` · `Ring_102.png`
- 이 작업이 넣음(원본 GUID): `Materials/` Fire_126_M3 · Flash_040_M1_0 · Smoke_009_M1 · Object_127_M3 · Bullet_015_M1 · Color_White_M1_0 · Smoke_115_M3 · Smoke_025_M3 · Point_024_M1 · Ray_017_M1 · Wave_004_M3, `Textures/` 같은 이름 png 11개(Flash_040_0 · Color 포함), `Meshes/FXMesh_Ring_01.asset` · `FXMesh_BanQiu_02.asset`
- 머티리얼 렌더 큐는 전부 3000(투명) — 밤 어둠 막(2900) 위.
- 만든 스크립트: `Source~/import_meteor.py` — 처음 가져올 때 쓴 것. 지금 프리팹은 사용자가 손으로 고친 것이라 **다시 돌리지 않는다**(돌려도 `fx_zombie_death.prefab` 이 있으면 멈추게 해 뒀다).
- 확인용 GIF: `Source~/zombie_death_blast*.gif`(처음 발밑 버전) · `Source~/fx_zombie_death_sizes*.gif`(몸 가운데 · 종류별 크기, 사용자 수정본) · `Source~/fx_zombie_death_combo*.gif`(폭발 + 소총 처치 효과 + 피 — 지금 게임 순서, 몸 조각 빠짐).

## 같이 바꾼 코드 · 데이터

- `Scripts/Fx/ProtoFxBuiltins.cs` — `FxIds.ZombieDeath` = `"fx_zombie_death"`
- `Scripts/Proto/ProtoZombie.cs` — `PlayDeathFx`(몸 키 재기 · 자리 · 크기, 상수 `DeathFxRefHeight` 1.72) · `Die` 가 부름
- `Scripts/Fx/FxWeaponStage.cs` (`Kill`) — 더미도 `ProtoZombie.PlayDeathFx`
- `Resources/Data/FxCatalog.asset` — 항목 1개(맨 끝)

## 되돌리기

1. FX 툴 카탈로그에서 `fx_zombie_death` 의 `prefab` 칸을 비운다 → 이 폭발만 빠지고 무기 처치 효과 · 몸 조각 · 피는 그대로.
2. 그 뒤 프리팹과 "이 작업이 넣음" 파일을 지운다. 공유 파일은 다른 작업이 쓰는 동안 남긴다.

## 확인한 것 / 안 한 것

- 임시 Unity 프로젝트에서 YAZS 카메라로 좀비 발밑 시간별 렌더와 좀비 셋이 차례로 죽는 GIF, 수정한 카탈로그가 이 프리팹을 가리키는 것 확인. 런타임 · 에디터 컴파일 통과.
- 메인 에디터 플레이(실제 물량전에서 많이 겹칠 때의 밝기 · 부하)는 아직 안 봤다.
