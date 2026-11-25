# Survivor Ace 추출 이펙트 — 미니 비행기(윙맨) 탄 miniplane1 — 임시 (출시 전 반드시 교체·삭제)

> **2026-11-23 — 이펙트는 모두 `Assets/_Game/Art/Sujin/S_VFX` 로 옮겼다**(프리팹은 종류별 폴더, 머티리얼 · 텍스처 · 메시 · 스프라이트는 `S_VFX/Shared/SurvivorAce/`, GUID 그대로). 사용자가 나중에 `_Reference` 를 지울 예정. **여기 남은 것: `UI/`(게임 HUD · 일시정지 · 결과 · 레벨업 창이 지금 쓴다 — 지우면 UI 가 깨진다) · `Audio/` · 문서 · `Source~`.** 아래 본문의 `Prefabs/` · `Materials/` 경로는 옮기기 전 기록이다. 저작물 교체 의무는 S_VFX 의 복사본에도 그대로 — `Sujin/README.md`.

2026-11-13 "Survivor Ace 프로젝트에 있는 Bullet_miniplane1_1, Bullet_miniplane1_2 이펙트 사용 관련된 부분 가지고 와줘" 요청으로
Survivor Ace 1.2.0 추출본(`D:\SurvivorAce_1.2.0\UnityProject\ExportedProject`)에서 복사했다.
**원작사 저작물이다.** 프로토에서 느낌을 보는 용도로만 쓰고, 빌드·외부 공개 전에 우리 이펙트로 바꾼 뒤 이 폴더를 지운다.

> **2026-11-20 정리** — 프리팹은 용도별 하위 폴더로 옮겼다: `Prefabs/Weapon/`(개틀링 탄 · 미니 비행기 탄 2 · arrow_white · Hitted_Machine_Small) · `Prefabs/Explosion/`(FX_Death_XiaoGuai_002 — 사용자가 손본 fx_zombie_death · FX_Car_Explosion 은 11-23 `_Game/Art/Sujin/VFX/Explosion` 으로) · `Prefabs/Ultimate/`(표적 사살 탄 · 히트 — 불 고리 MFX_FireRing 은 11-23 `_Game/Art/Sujin/VFX/Ultimate` 로) · `Prefabs/Appear/`(교체 등장 · 착지 연기) · `Prefabs/_Unused/`(비륜 1차 · 개틀링 _Super). 머티리얼 · 텍스처 · 메시는 여러 프리팹이 같이 써서 그대로 둔다.
> 셰이더 `SurvivorAce/Effects/Particle` · `UVFlow` 는 **지웠다** — 머티리얼 66개 전부 `ZombieOverrun/FX/Particle`(`_Game/Art/VFX/Shaders/FX_Particle.shader`)로 옮겼다(`FxShaderUnify.cs`). Hitted_Machine_Small 의 꺼진 층에 있던 빌트인 `Legacy Shaders/Particles/Additive` 3개도 같이(텍스처 × 색 × 2 그대로). 옛 셰이더 파일은 `_Backup/20261120_FxShaderUnify` 에. 아래 본문의 셰이더 · 경로 이야기는 그 전 기록이다.
**게임에 나온다(11-13 사용자 "1_1 은 탕탕 소총, 1_2 는 탕탕 캐논, 1_1 지원 드론에도" → "소총 총구 · 착탄도 원작 것으로")** — 아래 '무기 연결'. 발사음 `DragonBullet` 은 아직 연결 안 했다.
이 폴더는 다른 작업과 같이 쓴다: 캐릭터 · 무기 교체 이펙트(`MFX_ChuanSong_Appear_Yellow`, 설명 `README_ChuanSong.md`)와
좀비 처치 폭발(`MFX_Boss_MeteoriteExplosion`, 설명 `README_MeteoriteExplosion.md`)이 같은 폴더에 있고
셰이더 `Effects_Particle` · 머티리얼 `Glow_011_M1` · `Glow_011_M3` · `Glow_011_M1T` · 텍스처 `Glow_011` · `Ring_102` · `Glow_010_T1` · `Glow_011T1` 을 함께 쓴다 —
이 8개는 고치거나 지우지 말고(색을 바꾸려면 새 GUID 복사본), 폴더를 통째로 지울 땐 카탈로그의 그쪽 항목부터 정리한다.
시작 연출 낙하산 착지 연기 `MFX_Landing_Smoke`(보스 낙하 연기 파티클판 복사본, FX 카탈로그 `intro_landing`, 2026-11-16)도 여기 있다 — 설명 `README_LandingSmoke.md`. 머티리얼은 새 GUID 복사본(`*_Land.mat`)이라 위 공유 파일을 고치지 않았다.
`UI/SkillSelect/` 는 레벨업 스킬 3택 팝업(`UI_DankeSkillSelect`) 참고용 사본이다(2026-11-13, 게임과 연결 안 함) — 설명은 `UI/SkillSelect/README.md`. 위 공유 파일과 겹치는 것은 없다.
탱크 대포 착탄 폭발 `FX_Death_XiaoGuai_002`(원작 이펙트 표 210008 '일반 폭발' 원본 복사, FX 카탈로그 `tank_cannon_hit`, 2026-11-17)도 여기 있다 — 설명 `README_TankCannonHit.md`. 공유 파일 중 `Effects_Particle` · `Glow_011_M1` · `Glow_011_M3` · `Glow_011` · `Glow_010_T1` 등을 쓰기만 한다(고치지 않음).
불 고리 `MFX_FireRing`(불덩이 2개가 마주 보며 돌고 반 바퀴 불길을 끌어 한 고리로 이어짐, 11-18 — **파이로 필사기 '불꽃 고리' 그림으로 연결됨**, FX 카탈로그 `ult_fire_ring`)과 1차 불꽃 비륜 `MFX_FeiLun_Fire`(원작 빙결 톱날을 불로 고친 것, 반려, 미연결)도 이 폴더에 있다(2026-11-13) — 설명 `README_FeiLunFire.md`. 폴더를 지울 땐 카탈로그 `ult_fire_ring` 부터 정리한다. 공유 파일 중 `Effects_Particle` · `Glow_011_M1` · `Glow_011` · `Glow_010_T1` 을 쓰기만 한다.
표적 사살 탄 `FX_TargetKill_Bullet_Shock`(원작 충격파 탄 `MFX_ChongJiBo_Bullet` 손질 복사본 — 금빛 혜성탄, 2026-11-18 — **루키 필사기 '표적 사살' 탄으로 연결됨**, FX 카탈로그 `ult_target_kill_bullet`)과 그 히트 `FX_TargetKill_Hit`(원작 `MFX_LuoLei_Hit_Golden` 복사본, `ult_target_kill_hit`)도 이 폴더에 있다 — 설명 `README_TargetKillBullet.md`. 폴더를 지울 땐 카탈로그 `ult_target_kill_bullet` · `ult_target_kill_hit` 부터 정리한다. 공유 파일 중 `Effects_Particle` · `Effects_UVFlow` · `Glow_011_M3` · `Bullet_015_M1` · `Rune_120_M1` 을 쓰기만 한다.

## 들어 있는 것

| 프리팹 | 원작 경로 (`Assets/_Resources/…`) | 원작에서의 역할 |
|---|---|---|
| `Bullet_miniplane1_1` | `Prefab/Bullet/Hero/Bullet_miniplane1_1.prefab` | 미니 비행기 1 의 기본 탄 — 탄 표 200071 |
| `Bullet_miniplane1_2` | `Prefab/Bullet/Hero/Bullet_miniplane1_2.prefab` | 강화 탄(관통) — 탄 표 200072 |
| `arrow_white` | `Effect/BulletStart/arrow_white.prefab` | 두 탄의 발사(총구) 이펙트 — 이펙트 표 100001 |
| `Hitted_Machine_Small` | `Effect/BulletStart/Hitted_Machine_Small.prefab` | 두 탄의 착탄 · 소멸 이펙트 — 이펙트 표 210001 (**원작에서도 꺼져 있다** — 아래) |

함께: 머티리얼 18 · 텍스처 17 · 스프라이트 1(`Bullet_miniplane2_1`, 탄 그림 — 꺼져 있는 자식이 씀) · 셰이더 1 · 발사음 `Audio/DragonBullet.ogg`(소리 표 270016).

## 개틀링 탄 2개 — 가져오기만 함, 미연결 (2026-11-13)

사용자: "지금 거 킵해 두고 MFX_Gatling1_Bullet 이랑 MFX_Gatling1_Bullet_Super 가져와 줘 — 붙이는 건 내가 할게". **원본 그대로(루트 1) 넣었고 어디에도 연결하지 않았다.**

| 프리팹 | 구성 | 새로 넣은 파일(원본 GUID) |
|---|---|---|
| `MFX_Gatling1_Bullet` | 탄 심 `ZiDan_Dark`(크기 1.7 × 세로 3) + 빛 `Glow`(2.5 × 3.5) — 주황 늘어진 예광탄 | 머티리얼 `MFX_DaoDan_008_Bullet_M3` · 텍스처 `MFX_DaoDan_008_Bullet` |
| `MFX_Gatling1_Bullet_Super` | 탄 심 `ZiDan_Dark`(4 × 6) + 빛 `Glow_Dark`(5 × 6) — 흰 머리 노란 불덩이 물방울 | 머티리얼 `MFX_DaoDan_008_Bullet_Super_M3 1`(원본 이름에 " 1") · 텍스처 `MFX_DaoDan_008_Bullet_Super` |

- 이미 이 폴더에 있던 공유 파일을 같이 쓴다: 셰이더 `Effects_Particle` · 머티리얼 `Glow_011_M1` · 텍스처 `Glow_010_T1`. 스크립트 · 꼬리(TrailRenderer) 없음, 파티클은 Hierarchy 배율 · Local 시뮬레이션, 큐 3000.
- **원작에서 쓰는 곳을 못 찾았다**: 데이터 표(`Model_model` 등 `.bytes` 전부)에도, 다른 프리팹 · 에셋의 GUID 참조에도 없다 — 1.2.0 에선 안 쓰는 에셋으로 보인다(그래서 원작 속도 · 크기 · 총구 · 착탄 값도 없다). 비슷한 이름 `MFX_Gatling1_Bullet_Penetrate` 는 안 가져왔다.
- **방향 주의 — `_Super` 는 날아가는 쪽을 안 따라간다**: 일반 탄의 렌더러는 Local 정렬(탄 회전을 따라 눕는다)인데 `_Super` 의 두 렌더러는 **View 정렬**(늘 카메라를 보고 그림이 화면 위쪽을 향함)이다. 원작이 화면 위로 쏘는 세로 슈팅이라 그렇게 만든 것으로 본다. 우리 게임처럼 아무 방향으로 쏘면 물방울 머리가 늘 화면 위를 본다(임시 프로젝트 렌더로 확인). 붙일 때 두 렌더러를 일반 탄처럼 Local 정렬로 바꾸고 회전을 맞춰야 한다.
- 크기 참고: 원작 화면 비율 ×0.43 이면 일반 탄 약 0.7m 줄기, `_Super` 는 빛까지 약 2m 로 크다. 탄 모델로 쓰면 실제 배율 = 프리팹 루트 × `projectileSize`(miniplane 탄은 루트 2.15 × 0.22 = 0.47 로 맞췄다).

## 원작에서 쓰는 방식 (데이터 표 해독)

게임 로직은 `HotFixBattle.dll`, 수치는 `Assets/*.bytes` 표(빅엔디언, 행마다 int32 크기, 문자열은 int16 길이, FP 는 16.16 고정소수점)다.
표 구조는 `LocalModels.Bean.<표>` 클래스의 필드 순서. 프리팹을 GUID 로 무는 곳은 없고 모델 표의 경로 문자열로 불러 쓴다.

**모델 표 `Model_model`** — 1000007 · 11000002 → `Bullet_miniplane1_1` / 1000053 · 210005 → `Bullet_miniplane1_2` / 1400001 → `Prefab/Entity/MiniPlane1.prefab`(쏘는 비행기, 안 가져옴).

**탄 표 `Bullet_Bullet`** — 두 탄은 모델만 다르고 값이 같다.

| 필드 | 200071 (1_1) / 200072 (1_2) | 뜻 |
|---|---|---|
| ModelId | 1000007 / 1000053 | 위 모델 |
| MoveSpeed | 33 | 원작 유닛/초 (아래 크기 참고) |
| LifeTime | 10 | 초 |
| Scale | 1.1 | 탄 루트 배율 |
| MinBulletHurtTime | 0.1 | 같은 적 재타격 간격 |
| hurtAttributes | `Attack=1*100%` | 공격력 100% |
| Shape / ShapeOffset | [0.2, 0.25] / [0, −0.25] | 판정 모양 |
| bulletStartPrefabId | 100001 (`arrow_white`) | 발사 이펙트, 자리 [3, 1], 방향 = 탄 방향(BornEffectDirType 1) |
| hitCharacter / hitOther / bulletDestroy PrefabId | 210001 (`Hitted_Machine_Small`) | 착탄 · 벽 · 소멸, 각도 [180, 0, 0] |
| bulletStartSoundId | 270016 (`DragonBullet`) | 발사음 |
| HitCamp | [2, 32] | 맞히는 진영 |

**스킬 표 `Skillss_Main`**(서바이버 모드 스킬) — 10000801 · 10000802 행이 탄 200071 · 200072 를 직접 가리키고,
**10100803** 이 `WeaponChangeBulletMod {"BulletId":200072}` + `ThroughMod {"ThroughCount":2,"AttackPercent":-0.2}` + `Attack%=-15` —
강화하면 탄이 1_1 → **1_2 로 바뀌고 2번 관통**(관통할 때마다 피해 −20%, 공격력 −15%). 1000002 행(연사 강화: `FireCDReduce%=20`, `WeaponMultiMod {"FireHoldTime":0.06,"FireCount":1,"FireInterval":0.06}`)에도 모델 11000002(1_1)가 들어 있다.

안 가져온 것(이 탄 전용이 아님): 날개 이펙트 170007 `LightWing`(탄 385개 중 361개 공통) · 속성 부여 1802xx `MFX_Common_*_MoreFast`(독 · 불 · 얼음 · 번개 · 사신 · 치명 — 모든 탄 공통) · 쏘는 비행기 `MiniPlane1`(모델).

## 무기 연결 (2026-11-13)

| 무기 수치 (`Data/Weapons/Stats/`) | 탄 (`projectileModel`) | 탄 크기 | 실제 배율 = 루트 2.15 × 크기 | 속도 |
|---|---|---|---|---|
| `WS_Rifle_TangTang` (루키 · 리퍼 기본 무기) | `Bullet_miniplane1_1` | 0.22 | **0.473** (치명타 0.686) — 원작 화면 비율 그대로 | 24m/s · 0.35초 |
| `WS_SupportDrone` (지원 드론, 높이 1.6m 에서 조준) | `Bullet_miniplane1_1` | 0.18 | 0.387 | 22m/s · 0.5초 |
| `WS_TangTangCannon` (소총 진화) | `Bullet_miniplane1_2` | 0.30 | 0.645 | 30m/s · 0.08초 연사 |

- 탄 프리팹 루트 배율 1 → **2.15** = 원작 화면 비율 0.43 × 원작 탄 Scale 1.1 ÷ 소총 크기 0.22. 무기마다 `projectileSize` 비율(드론 < 소총 < 캐논)은 그대로 살렸다. 크기는 `projectileSize` 로 더 맞춘다(탄 모델이 있으면 시각 크기에만 쓰인다 — 판정은 `projectileRadius`).
- 꼬리 TrailRenderer(굵기는 트랜스폼 배율을 안 탄다): 굵기 × 0.43(1_1) · × 0.586(1_2, 캐논 크기 비율까지), 정점 간격 0.1 → 0.043 · 0.059. 길이(시간)는 원작의 "꼬리 길이 ÷ 탄 크기"가 같게 우리 속도로 — 1_1 0.02 → **0.0118초**(소총에서 0.28m), 1_2 0.05 → **0.0322초**(캐논에서 0.97m).
- 앞쪽 = +Z 라 `projectileFacesTravel` 1(셋 다 원래 1). 탄 그림 자식은 원작처럼 꺼진 채.
- 바꾸기 전 원본: `D:\Fork\Newgame\ZombieRun\_Backup\20261113_SAMiniplaneBullets\` (탄 프리팹 2 · 수치 3 · 총구/착탄 프리팹 2 · `FxCatalog.asset`). 소총 탄은 그 전까지 Toon BallRed 구슬(`Art/VFX/Weapons/Rifle/Toon/FX_Rifle_Bullet_ToonBallRed`), 캐논 · 드론은 모델 없이 코드 예광탄(캐논 주황 tracerColor)이었다. 되돌리려면 백업 3개를 덮어쓰거나 `projectileModel` 칸을 고친다.
- `FxToonPackImport`(소총 Toon 입히기)는 이제 소총 탄이 MSFX · Toon 이 아닌 것이면 탄은 건드리지 않는다(총구 · 착탄만).
- (11-13 오후) 사용자가 에디터에서 소총 세트 `WFX_Rifle_TangTang` 의 총구 · 착탄 슬롯을 MSFX 복사본(`FX_Rifle_TangTang_Muzzle` · `FX_Rifle_TangTang_Hit`, 카탈로그 ID `FX_Rifle_Muzzle` · `FX_Rifle_TangTang_Hit_2`)으로 다시 바꿨다 — **아래 `rifle_muzzle` · `rifle_hit`(원작 arrow_white · Hitted) 항목은 지금 소총이 안 쓴다**(항목 · 프리팹은 남아 있다). 탄은 그대로 miniplane1_1. `Scripts/Fx/README.md` §5-1-e.
- **소총 총구 · 착탄도 원작으로**(카탈로그 `Resources/Data/FxCatalog.asset` — 무기 세트 `WFX_Rifle_TangTang` 의 ID 는 그대로):
  - `rifle_muzzle` ← `arrow_white`(흰 가시 섬광 + 빛), 루트 1 → **0.43**(원작 화면 비율), 수명 1.3 → **0.47**(마지막 버스트 0.07 + 입자 0.3 + 0.1).
  - `rifle_hit` ← `Hitted_Machine_Small`, 루트 → **0.43**, 수명 1.7 → **0.9**. 원작은 두 갈래가 다 꺼져 있어 **`stg_blast_Hit_01` 만 켰다**(흰 → 노랑 가시 별 `Ci` + 파편 `LiZi` — 같은 계열 `Small2` 가 원작에서 켜 둔 갈래, 금빛 탄과 색이 맞는다). 그 안의 원래 꺼진 자식(Glow · LiZi_1 · Yuan · Yan · Yuan01 · TuoWei)과 `child` 갈래(불똥 + 붉은 빛)는 그대로 꺼져 있다.
  - 둘 다 `onTop`(오버레이 카메라) · 풀 6/6 · 24/20 그대로. 그 전 모양은 Toon BallRed(빨강, `Art/VFX/Weapons/Rifle/Toon/`).
  - 주의: 메뉴 '소총에 UniqueToonProjectiles (BallRed) 입히기'(또는 `FxToonPackImport` 판 올림)는 이 두 항목을 다시 Toon 으로 바꾼다(탄은 안 건드림).
- 캐논 · 드론의 총구 · 착탄은 그대로: 캐논 = 코드 `muzzle_flash` ×1.35 + `hit_spark` · 드론 = 총구 없음 + `hit_spark` ×0.8 (둘 다 노란 계열이라 금빛 탄과 맞는다).
- 임시 URP 프로젝트에서 게임과 같은 구성(탄 트랜스폼 = projectileSize, 그 아래 프리팹)으로 세 무기를 날려 렌더 확인. 메인 에디터 플레이 화면은 미확인.

## 원작 프리팹 상태 (위 '무기 연결'에서 고친 것: 탄 두 개의 루트 · 꼬리, 총구 · 착탄의 루트 0.43, 착탄 `stg_blast_Hit_01` 켬)

- **탄**: 탄 그림 자식(`Bullet_miniplane1_1` SpriteRenderer, 바닥에 눕힘)은 꺼져 있다 — 원작에서도 금빛 빛 · 심 파티클 + 꼬리 TrailRenderer(0.02초)만 보인다.
  1_1 과 1_2 의 차이는 크기뿐: 심 0.5 → 0.6 · 어두운 심 0.9 → 1.2 · 빛 1.5 → 0.8 (1_2 가 더 길고 머리가 희다). 앞쪽 = +Z(바닥 XZ, 원작도 위에서 내려다보는 직교 카메라).
- **착탄 `Hitted_Machine_Small`**: 두 갈래 `child`(불똥 몇 개 + 붉은 빛 번쩍) · `stg_blast_Hit_01`(흰 → 노란 가시 별 섬광 + 파편)이 **둘 다 꺼져 있어 그대로 놓으면 아무것도 안 보인다.**
  같은 계열 `Hitted_Machine_Small2` 는 stg_blast 만, `Hitted_Machine_Smallest` 는 child 만 켜 두므로 원작에서도 이 탄의 착탄은 안 보이게 해 둔 것으로 본다.
  쓰려면 둘 중 하나를 켠다(임시 프로젝트에서 둘 다 그려지는 것 확인).

## 바꾼 것

- 셰이더 이름 `Effects/Particle` → **`SurvivorAce/Effects/Particle`**(파일 `Shaders/Effects_Particle.shader`) — 탕탕 근사판(`_Reference/TangTang/Shaders/Effects_Particle.shader`)과 이름이 겹치지 않게. 머티리얼은 GUID 로 물려 영향 없다.
  이 셰이더는 11-09 추출본에서 복구한 근사판이다(원본 코드 아님, `ShaderBackup_DummyOriginals/README.txt`). 나머지 4개 머티리얼은 내장 `Sprites/Default` · `Legacy Shaders/Particles/Additive`.
- GUID 는 원본 그대로, 폴더만 Prefabs / Materials / Textures / Sprites / Shaders / Audio 로 폈다(원작은 Assets 바로 아래에 흩어져 있다). 핫픽스 판(`*_0`)은 같은 내용이라 안 가져왔다.
- 머티리얼 렌더 큐는 3000 ~ 3982 그대로 — 밤 어둠 막(2900) 뒤라 밤에도 빛난다.

## 우리 게임에 쓸 때 크기 참고

원작 카메라는 바로 위 직교, 세로 36.2 유닛. 우리 YAZS 카메라는 대상 높이에서 세로 약 15.4m — **원작 화면 비율 그대로면 루트 × 0.43**
(탄은 원작 Scale 1.1 까지 곱해 0.47, 속도 33 → 약 14m/s). 이 배율로 루키 · 좀비와 함께 찍은 한 발 타임라인 렌더로 확인했다(임시 URP 프로젝트, 셰이더 18개 오류 없음). 메인 에디터 화면은 미확인.
파티클 scalingMode 는 Hierarchy(0)라 루트 배율이 먹는다(`arrow_white` 루트 시스템 하나만 Local 인데 루트 자신이라 결과는 같다). TrailRenderer 굵기는 트랜스폼 배율을 안 탄다.
표 해독 스크립트: `D:\SurvivorAce_1.2.0\_scripts\table_decode\`(`rows.py` 행 나누기 · `decode.py` Bean 필드대로 읽기 + Bean 클래스 .cs).
