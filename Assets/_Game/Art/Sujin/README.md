# Sujin — 사용자 작업 폴더 (2026-11-23)

사용자가 11월부터 작업한 이펙트 · 배경을 모은 곳. 폴더 이름(`S_VFX` · `S_City` · `S_Shaders`)과 일부 프리팹 이름은 사용자가 에디터에서 정했다.
**손으로 고친 프리팹이 많다 — 도구로 다시 굽거나 덮어쓰지 말 것.** 모두 GUID 를 그대로 둔 채 옮겼으므로 카탈로그 · 무기 연출 세트 · 머티리얼 연결은 그대로다.

```
Sujin/
├─ S_VFX/        11월 이후 이펙트 — 종류별 (아래 표)
├─ S_UI/         전투 HUD · 레벨업 창 · 일시정지 등 UI — 종류별(Prefabs · Sprites · Animations …) 한 곳(옛 _Reference/*/UI 중 게임이 쓰는 것, 11-25)
├─ S_City/       도시 맵 '마지막' 판 배경 재료(옛 Art/ToonFantasyCity) — 맵 버전 메뉴가 이 경로를 쓴다(ToonFantasyCitySwap.Root)
├─ S_Shaders/    Sujin 의 셰이더 전부 — Toon/(배경 Sujin/Toon/Outline · Vegetation · Grass + SujinToon.hlsl) · VFX/(MSFX CommonParticleOneShot)
└─ S_Textures/   Sujin 의 텍스처 맵 전부(11-25) — VFX/(이펙트 114) · City/(배경 12) · MatCap/(162, 옛 Art/MatCap — 캐릭터 머티리얼이 쓴다)
```

- **같은 종류는 한 곳에(11-25 사용자 규칙)**: 셰이더는 `S_Shaders`, 텍스처 맵은 `S_Textures` 에만 둔다. 출처(SurvivorAce · TangTang · 팩 이름) 폴더는 만들지 않는다. UI 그림(`S_UI/Sprites` · `AppIcon` · `Resources/PickFx`)은 텍스처 맵이 아니라 S_UI 에 둔다.
- 이름이 겹친 이펙트 텍스처 4장(탕탕 쪽 `Color` · `Glow_011` · `Mask_013` · `Ring_005`)은 옮기며 `_02` 를 붙였다(GUID 그대로라 연결은 같음). 기록 `_Backup/20261125_SujinTypeMerge`.
- 텍스처를 새로 굽는 도구도 `S_Textures/VFX` 에 쓴다: `OrbitBladeTrailSetup`(T_OrbitTrail_*) · `InfernoFlameSetup` + `ArtSource/VFX/bake_inferno_textures.py`(T_Inferno_*) · `FxPackImport`(소총 T_MSFX_*).

## 이름 규칙 (2026-11-25, 사용자)

- 이펙트 프리팹은 모두 `FX_` 로 시작, 3D 메시(.fbx · 메시 .asset)는 `FXM_`.
- 무기 이펙트 `FX_무기_스킬`(FX_Rifle_Muzzle · FX_Inferno_Jet · FX_OrbitBlade_Trail · FX_Drone_Bullet · FX_Cannon_Bullet · FX_Tank_CannonHit), 필사기 `FX_Ult_스킬_부분`(S_VFX/Ult 폴더), 그 밖은 `FX_폴더_이름`(FX_Explosion_* · FX_Hit_* · FX_Enemy_* · FX_Boss_* · FX_Pickup_* · FX_Char_* · FX_Shared_*).
- 안 쓰는 것(_Unused)은 꼬리 `_Old` · `_Original` · `_Template`. 메시: FXM_HalfSphere_01/02 · FXM_Ring_01 · FXM_Cone_01 · FXM_Rifle_MuzzleQuad.
- 이름만 바꿨고 GUID 는 그대로라 카탈로그 ID(boss_spawn 등) · 무기 연출 세트 연결은 같다. 템플릿을 굽는 코드(FxTemplatePrefabs) · 경로 상수(FxAssetsSetup)도 새 이름. 바뀐 표 `_Backup/20261125_FxRename/moves.tsv`.

## GitHub 에 올리는 것 (2026-11-25)

- **`Assets/Soo_VFX`(외부 팩 창고)와 `Assets/_Reference`(추출본 창고)는 GitHub(Shuuing/zombie-run)에 올리지 않는다**(.gitignore). 내 컴퓨터에는 그대로 있다.
- 그래서 **게임이 쓰는 것은 전부 Sujin 안에 있어야 한다.** 11-25 에 두 창고에서 게임이 쓰는 484개를 GUID 그대로 Sujin 으로 옮겼고(S_UI · S_VFX/Shared/MasterStylizedFX), Sujin 안에서 안 쓰던 추출본 25개는 `_Reference` 로 내보냈다. 옮긴 뒤 남는 쪽에서 두 창고를 가리키는 참조 0 확인.
- 앞으로 팩 · 추출본에서 무엇을 가져다 쓸 때는 **Sujin 안으로 복사하거나 옮긴 뒤 연결**할 것. 창고 안의 것을 바로 연결하면 GitHub 에서 받은 프로젝트에서 빈칸이 된다.
- 기록: `_Backup/20261125_SujinReorg/`(moves.tsv 517건 · log.txt · 분석 스크립트).

## S_VFX — 종류별 (2026-11-23)

| 폴더 | 들어 있는 것 | 카탈로그 ID · 쓰는 곳 |
|---|---|---|
| `Weapon/Rifle/` | 총구 `FX_Rifle_Muzzle` · 착탄 `FX_Rifle_Hit`(사용자 손편집) · 소총 탄 `FX_Rifle_Bullet`★ · `MSFX/`(복사본 머티리얼 · 메시 `FXM_Rifle_MuzzleQuad` — 텍스처 T_MSFX_* 는 S_Textures/VFX) | `FX_Rifle_Muzzle_Flashy` · `FX_Rifle_Hit` · 소총 무기 수치 탄 |
| `Weapon/Inferno/` | 불줄기 `FX_Inferno_Jet` + 머티리얼 · `Source~`(텍스처 T_Inferno_* 는 S_Textures/VFX) | `inferno_jet` |
| `Weapon/OrbitBlade/` | 톱날 꼬리 `FX_OrbitBlade_Trail` + 머티리얼(텍스처 T_OrbitTrail_* 는 S_Textures/VFX) | `orbit_blade_trail` |
| `Weapon/Drone/` · `Weapon/Cannon/` · `Weapon/Tank/` | 드론 탄 `FX_Drone_Bullet`★ · 캐논 탄 `FX_Cannon_Bullet`★ · 탱크 대포 착탄 `FX_Tank_CannonHit`★ | 지원 드론 · 캐논 무기 수치 탄 · `tank_cannon_hit` |
| `Ult/` | 루키 필사기 탄 `FX_Ult_TargetKill_Bullet`★ · 히트 `FX_Ult_TargetKill_Hit`★ · 파이로 필사기 불 고리 `FX_Ult_FireRing`★ | `ult_target_kill_bullet` · `ult_target_kill_hit` · `ult_fire_ring` |
| `Explosion/` | 탈것 폭발 `FX_Explosion_Vehicle`★ · 좀비 처치 `FX_Explosion_ZombieDeath`★ | `van_blast` · `tank_blast` · `heli_crash_impact` · `fx_zombie_death` |
| `Hit/` | 타격 불똥 `FX_Hit_Spark` · 처치 터짐 `FX_Hit_KillBurst` · 피 터짐 `FX_Hit_Gore`(템플릿) · 피 튀김 `FX_Hit_Blood`☆ | `hit_spark` · `kill_burst` · `zombie_gore` · `hit_blood` |
| `Enemy/` | 좀비 할퀴기 `FX_Enemy_ClawHit`☆ · 독 구슬 `FX_Enemy_PoisonBolt`☆ · 독 착탄 `FX_Enemy_PoisonBoltHit`☆ · 독가스 `FX_Enemy_PoisonGas`☆ · 독 맞음 `FX_Enemy_PoisonHitPlayer`☆ | `zombie_claw_hit` · `poison_bolt(_hit)` · `poison_gas_cloud` · `player_poison_hit` |
| `Boss/` | 등장 `FX_Boss_Spawn`☆ · 내려찍기 `FX_Boss_Impact`☆ · 망치 궤적 `FX_Boss_HammerTrail`☆ · 기모으기 먼지 `FX_Boss_WindupDust`☆ · 예고 원 `FX_Boss_Telegraph`☆ | `boss_spawn` · `boss_impact` · `boss_hammer_trail` · `boss_windup_dust` · `boss_telegraph` |
| `Pickup/` | 젬 줍기 `FX_Pickup_Gem`(템플릿) · 물고기 반짝이 `FX_Pickup_FishSparkle`☆ · 흡수 꼬리 `FX_Pickup_FishTrail`☆ | `gem_pickup` · `gem_sparkle` · `gem_trail` |
| `Character/` | 교체 등장 노랑 `FX_Char_SwapAppear`★ · 낙하산 착지 연기 `FX_Char_IntroLanding`★ | `char_swap_appear` · `weapon_change_appear` · `intro_landing` |
| `_Unused/` | 게임에 안 연결된 것 — 착지 예비 템플릿 `FX_Char_IntroLanding_Template` · 옛 소총 템플릿 3(`Rifle_Template/FX_Rifle_*_Template`) · 소총 손보기 전 원본 3(`Rifle_MSFX_Original/FX_Rifle_*_Original`) · `FX_Rifle_Muzzle_Old`★ · `FX_Rifle_Hit_Old`★ (11-25: 아무도 안 쓰던 비륜 1차★ · 개틀링 Super★ · 독 공☆ 은 `_Reference/*/Prefabs/_Unused` 로 내보냄) | (카탈로그 `rifle_*` · `FX_Rifle_Hit_2` · `FX_Rifle_Muzzle` 항목이 아직 가리키는 것도 있다) |
| `Shared/` | 위 이펙트들이 같이 쓰는 재료 — 출처 구분 없이 종류별: `Materials/`(105) · `Meshes/`(FXM_HalfSphere_01 · 02 · FXM_Ring_01 · FXM_Cone_01) · `Sprites/`(37) · `Prefabs/`(`FX_Shared_Light` — 착탄 `FX_Rifle_Hit` 이 씀). 텍스처는 `S_Textures/VFX`, 셰이더는 `S_Shaders/VFX`. 11-25 부터 쓰는 것만(안 쓰던 22개는 `_Reference` 로) | — |
| `_Docs/` | ★ · ☆ 이펙트를 가져오고 손본 설명서 — `README_SurvivorAce.md`(미니 비행기 탄 등) · `README_ChuanSong` · `README_FeiLunFire` · `README_LandingSmoke` · `README_MeteoriteExplosion` · `README_TankCannonHit` · `README_TargetKillBullet` · `README_TangTang.md`(옛 `_Reference/*/README*.md`, 본문 경로는 옛 자리 기준) | — |

> ⚠ **★ = Survivor Ace 추출본(을 손본 것), ☆ = 탕탕특공대(Habby) 추출본** — 남의 게임 저작물이다. 프로토에서 느낌을 보는 용도로만 쓰고, **빌드 · 외부 공개 전에 우리 이펙트로 바꾼다.**
> `_Reference` 를 지워도 이 복사본들은 여기 남으므로 교체 의무도 같이 따라온다. `Shared/` 를 지우면 ★ · ☆ 이펙트가 전부 분홍/빈칸이 된다.

## S_UI — UI (2026-11-25, 출처별로 나누지 않고 종류별 한 곳)

| 폴더 | 들어 있는 것 | 쓰는 곳 |
|---|---|---|
| `Prefabs/` | 탕탕식 전투 UI `UI_TTHUD` · `UI_TTPause` · `UI_TTResult` · `UI_TTSettings` · `UI_TTSkillSelect` | `Resources/Data/TtBattleUiSkin` · `TtSkillSelectSkin` |
| `Sprites/` (44) · `Animations/` (8) · `Fonts/`(Alibaba PuHuiTi Heavy) | 위 프리팹이 쓰는 그림 — 42장은 11-17 3판에서 우리 그림으로 그린 것, 2장은 레벨업 카드 바탕(원작) | 프리팹 |
| `Resources/PickFx/` | 레벨업 카드 선택 빛(FangSheGuang · Ray_016_Hard) — **코드가 `Resources.Load("PickFx/…")` 로 이름으로 부른다**, 폴더 이름 `Resources` · `PickFx` 를 바꾸지 말 것 | `ProtoTtSkillSelectPopup` |
| `README_TTUI.md` | 탕탕식 UI 를 가져오고 고친 기록(옛 탕탕 UI 폴더의 README) | — |
| `AppIcon/` | 앱 아이콘 네 장(Background · Foreground · Round · Legacy) — 스시걸 렌더로 만든 C 민트 시안(옛 Art/UI/AppIcon, 11-25). 원본 · 다른 시안은 `ArtSource/AppIcon` | 안드로이드 Player 설정 아이콘 · `AppIconSetup`(메뉴 · 비었을 때 자동) |

- **지금 그림체(`Resources/Data/UiTheme` = TangTang)에 나오는 것만 여기 있다.** Survivor Ace 그림체 묶음(`UI_Zombie*` · `UI_DankeSkillSelect_Sample` 과 그 스프라이트 274 · 텍스처 7 · 애니메이션 120 · 재료, 스킨 `SaBattleUiSkin` · `SkillSelectSkin`)은 11-25 에 원래 자리 `_Reference/SurvivorAce/UI/{Battle,SkillSelect}` 로 돌려보냈다(GitHub 에 없음). 스킨은 `_Reference/SurvivorAce/UI/Resources/Data` 에 있어 **이 컴퓨터에선 UiTheme 을 SurvivorAce 로 바꿔도 그대로 뜬다** — GitHub 에서 받은 프로젝트에서는 스킨이 없어 탕탕 판 또는 예전 코드 화면이 뜬다. 기록 `_Backup/20261125_SaThemeOut`.

> ⚠ 카드 바탕 2장 · 글꼴과 탕탕식 원작 애니메이션은 추출본(을 손본 것)이다 — 출시 전에 우리 것으로 바꾼다. 안 쓰는 원본 프리팹 · 스프라이트와 이 UI 를 만든 도구 `Source~`(우리 그림을 그리는 `make_tt_art.py` 포함)는 `_Reference/*/UI` 에 있다(GitHub 에 없음). README 본문의 경로는 옮기기 전 자리(`_Reference/…`) 기준이다.

## 그 밖

- 옮긴 기록: `_Backup/20261123_SVfxMove/moves.json`(317건) · `SVfxMove.log`. 옮기기 전 원본 전부 같은 백업 폴더.
- 코드가 이 자리를 안다: `FxTemplatePrefabs.SVfx`(템플릿 프리팹 저장 자리) · `FxAssetsSetup`(물고기 반짝이 · 꼬리 · 착지 연기 · 탱크 착탄) · `FxPackImport.DstDir`(소총 MSFX 복사본) · `OrbitBladeTrailSetup.Dir` · `InfernoFlameSetup`(프리팹 GUID 로 찾음).
- 9~10월 코드 이펙트(`Resources/VFX` — FX_FlameJet · FX_BurnBody · FX_SwapLanding 등)와 공통 머티리얼 · 텍스처(`Art/VFX/Common` 의 M_Fx_* · T_Fx_*), 파티클 셰이더(`Art/VFX/Shaders`)는 그대로다.
- 배경 셰이더 경고 정리(중복 키워드 줄 · 바람 pow, 11-23)는 `S_City/README.md` 셰이더 절 — Amplify 로 다시 저장하면 돌아온다.
- 같은 날 지운 것(안 쓰던 화염방사기 MSFX 복사본 · 시트와 그 도구)은 `Assets/_Game/Scripts/Fx/README.md` §13.
