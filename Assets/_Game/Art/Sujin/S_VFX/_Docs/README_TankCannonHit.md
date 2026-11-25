# 탱크 대포 착탄 폭발 `FX_Death_XiaoGuai_002` — Survivor Ace 원본 복사 (임시, 출시 전 교체)

2026-11-17 요청 "Survivor Ace 의 `FX_Death_XiaoGuai_002` 를 가져와 탱크에 올라타 대포를 쏠 때 포탄이 터지는 자리에 붙여 줘".
원본: `D:\SurvivorAce_1.2.0\UnityProject\ExportedProject\Assets\FX_Death_XiaoGuai_002.prefab`
(원작 경로 `Assets/_Resources/Effect/InGame/FX_Death_XiaoGuai_002.prefab`, 이펙트 표 210008 `爆炸特效-常规` = 일반 폭발).
**Survivor Ace 원작사 저작물이다.** 카탈로그가 참조하는 동안 빌드에 들어간다 — 빌드 · 외부 공개 전에 우리 이펙트로 바꾸고 지운다.
탱크 파괴 · 승합차 · 헬기 큰 폭발(`FX_Car_Explosion`, 사용자 손편집 중)은 건드리지 않았다 — 이건 대포 **착탄**만이다.

## 모양

섬광(흰 원 · 가시 별) → 만화풍 불덩이 플립북(`MFX_BaoZha_004`, 4×3) · 불꽃 줄 · 불티 → 검은 연기로 사그라듦. **0.6초**, 원작 그대로(색 · 모양 안 바꿈).
원작 크기(루트 1)에서 우리 탑승 카메라(52° · FOV 65 · 탑승 줌 1.18)로 보면 섬광 · 불덩이가 탱크 포 판정 반경(2.6m) 원과 거의 같다 —
예전 코드 그림(불덩이 지름 = 반경 × 1.05, 링 = 판정 반경)과 비슷한 크기라 **배율을 바꾸지 않았다**.

| 요소(원본 이름) | 그림 |
|---|---|
| `Shan` · `Shanguang` · `Shan_hei` | 첫 섬광 · 퍼지는 빛 · 가시 별 섬광 |
| `Yan`(MFX_BaoZha_004 M1) · `Yan_Dark`(M3) | 불덩이 플립북(밝은 판 · 어두운 판 겹침) |
| `Huan2` | 순간 고리 |
| `Bao_lizi` · `Lizi` | 늘인 불꽃 줄 · 불티 |
| `Yan`(Smoke_009) | 작은 연기 뭉치 20개 |
| `Glow_Dark` | 바닥에 눕는 큰 갈색 빛(그늘) |

## 게임 연결

| 카탈로그 ID | 부르는 코드 | 자리 · 크기 | 코드가 따로 내는 것 |
|---|---|---|---|
| `tank_cannon_hit` | `ProtoTank.Init` 이 탱크 포(`WeaponStats` 런타임 생성)에 런타임 연출 세트 `(탱크 포 연출)` 를 물리고 그 **explode 슬롯**에 이 ID 를 넣는다 → `ProtoProjectile.Explode` 가 낸다 | 포탄이 터진 점(대개 좀비 몸통 0.9m · 승합차 맞은 자리), scale = `ProtoTank.CannonHitFxScale` **1** | 폭발음 · 풀 눕힘 · 풀 조각(`ProtoExplosion.Aftermath`) — 프리팹 폭발이면 `Explode` 가 따로 부른다. 흔들림은 원래 없음(탱크 포 quiet) |

- **가까이서 터지면 작게**(11-17 사용자 "거리에 따라 이펙트 사이즈를"): 포구(탱크 중심에서 2m 앞)에서 탄이 날아간 거리로 배율을 곱한다 —
  거의 안 날아가고 터지면 ×0.5(`ProtoTank.CannonHitNearScale`), 5m(`CannonHitFullTravel`) 이상이면 ×1, 그 사이는 부드럽게(smoothstep).
  탱크에서 1.8m · 3.5m · 5.5m · 8m 거리면 ×0.5 · ×0.56 · ×0.83 · ×1. 차체에 붙은 좀비를 쏠 때 폭발 셋이 합쳐져 탱크와 캐릭터를 통째로 덮던 것을 막는다
  (`Source~/tank_cannon_hit_distance.png` — 왼쪽 예전 · 가운데 적용 · 오른쪽 더 줄인 대안 ×0.4/6m). **판정 반경 2.6m 는 그대로**라 가까운 폭발은 그림이 판정보다 작다.
  식은 `WeaponFxSet.ExplodeDistanceScale`(필드 `explodeNearScale` · `explodeFullDistance`, 기본 1 = 거리 무관 — 다른 무기는 안 바뀐다), 날아간 거리는 `ProtoProjectile.Traveled`.
- **항상 위에(onTop) 켬** — 깊이 검사 그대로면 큰 빛 판(지름 5~9m)이 땅에 걸려 아래가 가로로 잘렸다(`Source~/tank_cannon_hit_compare.png` 왼쪽). 오버레이 카메라가 그려 좀비 · 탱크 위에도 덮인다.
- 그리는 순서: 머티리얼 큐 전부 3000(셰이더 Transparent) + 루트 SortingGroup 정렬 순서 13 — 밤 어둠 막(2900) 뒤라 어둡게 안 묻힌다. 항상 위에라 메인 카메라 큐와도 상관없다.
- 다른 무기의 착탄 폭발(로켓 · 수류탄 · 폭격 = 공용 `explosion`)은 그대로다. 탱크 포만 바뀐다.
- 카탈로그: 수명 0.9 · 풀 4 · 동시 6 · 게임 시계. 크기는 `CannonHitFxScale` 이나 FX 툴 카탈로그 탭 '크기 (프리팹 루트)'.
- **프리팹 칸을 비우면 예전 그대로**(공용 착탄 폭발 코드 그림 — 폴백 `builtin: tank_cannon_hit` 이 `ProtoExplosion.Play` 를 같은 인자로 부른다).

## 원본에서 바꾼 것 (`Source~/import_xiaoguai.py <Assets> [루트 배율] [--force]`)

- 프리팹은 **원본 GUID(`5b5bdbe0…`) · 원본 이름**. 루트 원작 최적화 스크립트(`OptimizeTool.Baker.ParticleSequenceBaker`, 빈 클래스) 컴포넌트를 뺐다(Missing Script 방지).
- 렌더러가 꺼진 빈 껍데기 둘(최상위 · `MFX_BaoZha_004`) 5초 반복 · 초당 10개 방출 → 반복 끔 · 방출 0. 루트 배율 1(원본 그대로).
- 스크립트는 프리팹이 이미 있으면 멈춘다(손으로 고쳤을 수 있다 — 덮어쓰기는 `--force` 로만).

## 파일

- 이 작업이 넣음(원본 GUID): `Prefabs/FX_Death_XiaoGuai_002.prefab` · `Materials/` Flash_011_M1 · MFX_BaoZha_004_M1 · MFX_BaoZha_004_M3 · Ring_005_M1 · Smoke_009_M3 · `Textures/` Flash_011 · MFX_BaoZha_004 · Ring_005
- 공유(먼저 들어와 있던 원본 GUID 파일 — 고치지 않았다): `Shaders/Effects_Particle.shader` · `Materials/` Glow_011_M1 · Glow_011_M3 · Point_012_M3 · Bullet_015_M1 · `Textures/` Glow_011 · Glow_010_T1 · Point_012 · Bullet_015 · Smoke_009
- 확인 그림: `Source~/tank_cannon_hit.gif`(실제 속도 30fps) · `_slow.gif`(¼ 속도) · `tank_cannon_hit_frames.png`(시각별) · `tank_cannon_hit_compare.png`(깊이 검사 vs 항상 위에)

## 같이 바꾼 코드 · 데이터 (바꾸기 전 파일: `_Backup/20261117_TankCannonHit/`)

- `Scripts/Fx/ProtoFxBuiltins.cs` — `FxIds.TankCannonHit` = `"tank_cannon_hit"` · 폴백 코드 그림 등록(공용 착탄 폭발과 같은 그림)
- `Scripts/Proto/ProtoTank.cs` — `CannonHitFxScale` · `CannonHitNearScale` · `CannonHitFullTravel` · `_cannonFx`(Init 에서 만들고 OnDestroy 에서 지움)
- `Scripts/Proto/ProtoProjectile.cs` (`Explode`) — explode 항목에 프리팹이 있으면 `ProtoExplosion.Aftermath`(폭발음 · 풀), 배율에 `ExplodeDistanceScale(날아간 거리)` 곱함 · `Traveled` 공개
- `Scripts/Weapons/WeaponFxSet.cs` — `explodeNearScale` · `explodeFullDistance` · `ExplodeDistanceScale`(거리 배율 전 파일: `_Backup/20261117_TankCannonHit/WeaponFxSet.cs`)
- `Scripts/Fx/FxWeaponStage.cs` (`Explode`) — FX 툴 [무기] 무대도 같은 거리 배율(지금 세트들은 기본 1 이라 그대로)
- `Editor/Fx/FxAssetsSetup.cs` — 필수 ID 에 추가, 항목이 없으면 이 프리팹으로 채움(폴더를 지웠으면 코드 그림)
- `Resources/Data/FxCatalog.asset` — 항목 1개(맨 끝)

## 되돌리기

1. FX 툴 카탈로그에서 `tank_cannon_hit` 의 `prefab` 칸을 비운다 → 예전 착탄 폭발 그대로(소리 · 풀도 그대로).
2. 그 뒤 프리팹과 "이 작업이 넣음" 파일을 지운다. 공유 파일은 다른 작업이 쓰는 동안 남긴다.

## 확인한 것 / 안 한 것

- 임시 Unity 프로젝트에서 실제 파일로 게임 카메라 렌더(좀비 몸통 높이 · 바닥, 판정 반경 원, 깊이 검사 / 항상 위에). 런타임 · 에디터 오프라인 컴파일 통과.
- **메인 에디터 플레이는 안 봤다** — 실제 밤 화면 · 탱크 탑승 중 연사(공격 속도 버프로 빨라질 때 겹침) · 폭발음이 한 번만 나는지 · 오버레이 카메라에서 SortingGroup 동작.
