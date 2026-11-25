# 표적 사살 탄 `FX_TargetKill_Bullet_Shock` — Survivor Ace `MFX_ChongJiBo_Bullet` 손질 복사본 (임시, 출시 전 교체)

2026-11-18 요청 "표적 사살 … 표적 되는 적에게 총알 날라가는 이펙트 좀 더 화려한걸로 찾아서 넣어줘" → 후보 비교 뒤 "A로 적용".
원본: `D:\SurvivorAce_1.2.0\UnityProject\ExportedProject\Assets\MFX_ChongJiBo_Bullet.prefab`(충격파 탄).
**Survivor Ace 원작사 저작물이다.** 카탈로그가 참조하는 동안 빌드에 들어간다 — 빌드 · 외부 공개 전에 우리 이펙트로 바꾸고 지운다.

## 쓰는 곳

- FX 카탈로그 `ult_target_kill_bullet`(스팬, 수명 0.4 · 풀 8 · 항상 위에 끔) ← `ProtoUltimate.Fly`(루키 필사기 표적 사살). 탄 속도 · 관통 거리는 `ULT_TargetKill` 의 `bulletSpeed` 32 · `bulletPierce` **0**(처음 2m → 11-18 사용자 "너무 통과하는 것 같다" → 몸 표면에서 히트와 함께 사라짐).
- 프리팹 칸을 비우면 예전 즉발 금색 선(`ProtoBeamFx`)으로 돌아간다.
- **끝낼 때(11-18 고침)**: 머리 파티클(`Tou` · `Ci` · `Glow` · `Mesh_UVFlow` 두 겹 — 로컬 시뮬레이션, 수명 0.2~1초)은 탄에 붙어 있어서 방출만 멈추면
  멈춘 자리에 떠 있다가(불덩이 메시는 1초 동안 흐려지지도 않는다) 회수 때 뚝 사라졌다(사용자 "히트되면 이펙트가 안 사라지고 멈춰 있다가 사라져").
  지금은 `ProtoUltimate.Fly` 가 끝에서 **로컬 시뮬레이션 파티클을 바로 지우고**(StopEmittingAndClear) 월드 꼬리(`Trail_liang`) · 불티(`LiZi_Trail`)만 사그라들게 한다.
  카탈로그 lifetime 1 → **0.4**(남은 꼬리 · 불티 수명 0.3초 안). 다른 곳에서 이 프리팹을 날려 쓰면 같은 처리가 필요하다.

## 모양 · 원본에서 바꾼 것

금빛 줄기 꼬리(`Trail_liang`) + 주황 불꽃 머리(메시 `Mesh_UVFlow` 두 겹 · `Tou` · `Glow`) + 뒤로 뻗는 가시(`Ci`) + 불티(`LiZi_Trail`). +Z = 나는 쪽.

- 프리팹 GUID 새로(`b1d51764…`), 루트 이름 `FX_TargetKill_Bullet_Shock`, **루트 배율 0.6**(게임 카메라에서 불꽃 머리 약 1m).
- 끔: `Mesh_Quan`(회색 연기 고리 메시 — 우리 카메라에선 회색 돌판처럼 보였다), `LiZi_KeJi`(룬 글자 입자 — 회색 글자처럼 떠다녔다).
- `Ci`(가시): 원작은 View 정렬이라 늘 화면 아래를 향했다(세로 슈팅용) → Local 정렬 + 트랜스폼 X 90° 로 눕혀 날아가는 쪽 뒤로 뻗게.
- 빠진 스크립트 컴포넌트 제거. 나머지 값 · 색은 원작 그대로.

## 함께 넣은 파일 (원본 GUID, 이미 있던 공유 파일은 건너뜀)

머티리얼 6 `Trail_024_M1` · `Ray_030_M1` · `Mask_031_M3_UVFlow` · `Noise_033_M1_UVFlow` · `Fire_118_Golden_M1` · `Smoke_011_M1`,
텍스처 7 `Trail_024` · `Ray_030` · `Mask_025` · `Mask_031` · `Noise_033` · `Fire_118_Golden` · `Smoke_011`, 메시 `FXMesh_BanQiu_008`.
공유(고치지 않음): 셰이더 `Effects_Particle` · `Effects_UVFlow`(근사판), `Glow_011_M3` · `Bullet_015_M1` · `Rune_120_M1` 등.

## 확인

- 임시 URP 프로젝트에서 플레이 모드로 루키 + 좀비 5체에 0.07초 간격으로 날려 찍음(지금 금색 선 · 레이저 볼트 · 다이아 탄과 비교) — `_Review/20261118_TargetKillBullet/`.
- 메인 에디터 · 게임 플레이로는 아직 미확인. 굽는 곳: 임시 프로젝트 `Assets/Editor/TkCandBuild.cs`(Build) · 비교 `TkPlaySetup` + `PlayProbe/TkPlayDriver.cs`.

## 맞는 순간 히트 `FX_TargetKill_Hit` (11-18)

사용자 "적에 맞으면 히트 연출 나오면서 사라지게". 원작에서 충격파 탄은 데이터 표(모델 · 이펙트)에 연결된 곳이 없어(1.2.0 미사용) 짝 히트가 없다 —
금빛 · 불꽃 계열 히트 14개(원작 10 · MSFX 2 · Vefects 1 · 지금 소총 히트)를 같은 흐름으로 찍어 고름(`_Review/20261118_TargetKillBullet/hit_candidates_*.png`).

- 원본 `MFX_LuoLei_Hit_Golden`(금빛 낙뢰 히트 — 섬광 · 금빛 빛무리 · 번개 · 가시 · 불티, 약 1초). 복사본 GUID `a2c930ae…`, **루트 0.6**.
- 바꾼 것: 모든 자식 시작 지연 **0.05초 당김**(0 아래는 0) — 원본은 섬광이 0.05초 늦게 떠서 탄이 사라진 뒤 3프레임 빈틈이 보였다. 빠진 스크립트 제거. 나머지 그대로.
- 카탈로그 `ult_target_kill_hit`(원샷, 수명 1.1 · 풀 8 · 항상 위에 끔). 자리 = 표적 몸 표면(쏜 쪽, 가슴 높이 — 탄이 `bodyRadius` 앞에서 멈춘다), +Z = 쏜 사람 쪽. 비우면 기본 불똥만.
- 함께 넣은 파일: 머티리얼 7 `Ray_105_M1` · `Flash_017_M1` · `Light_121_M1` · `Light_168_M1` · `Ray_033_Golden_M1` · `Glow_011_M3T` · `Glow_010_Red_M1`, 텍스처 6. 나머지는 이미 있던 공유 파일.
- 반려 후보: DaoDan · ZiDan · Lightning 계열(작거나 몸에 가림), BaoZha_001_Super(화면을 덮음), MSFX 두 개(빨강), Vefects Fireball_Impact(회색 네모 데칼).
- 처치되면 이와 별개로 모든 적 공통 `fx_zombie_death` 가 같이 터진다(대상당 주무기 ×25 라 대부분 한 방에 죽는다).
