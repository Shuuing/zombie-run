# 보스 (boss_001) 애니메이션 셋업

재생성: **Tools ▸ 좀비 오버런 ▸ 보스 애니메이션 셋업** (`Assets/_Game/Editor/BossAnimSetup.cs`, 재실행 가능)

좀비(`ZombieAnimSetup.cs`)와 동일한 절차다. 소스는 `Assets/monster/boss/boss_001/animation/`.

## 리그

| 파일 | 내용 |
|---|---|
| `animation/boss_001.fbx` | **리그** — SkinnedMeshRenderer `tilemap_raid`, 본 46개, rootBone `mixamorig:Hips`. 아바타를 여기서 만든다 |
| `boss_001.fbx` (상위 폴더) | 본이 없다 (MeshRenderer만). 쓰지 않는다 — 좀비와 같은 상황 |

표준 Mixamo 골격(`mixamorig:*`)이라 **블렌더 수정 없이** 임포트 설정만으로 Humanoid 매핑이 된다.
아바타 `boss_001Avatar` = valid, human, 휴머노이드 본 46개 매핑.

임포트 시 클립마다 뜨는 "has rig import warnings"는 Mixamo 말단 본 때문이며 무해하다:
`LeftToe_End` `RightToe_End` `HeadTop_End` `*HandIndex4/Middle4/Ring4/Thumb4` — Humanoid에 대응 슬롯이 없는 끝점.
미매핑 휴머노이드 본은 `Jaw` `LeftEye` `RightEye`뿐이고 전부 옵션이다(좀비도 동일).

## 클립

| 소스 FBX | 클립명 | 길이 | 루프 | 컨트롤러 연결 |
|---|---|---|---|---|
| `Zombie Walk` | `Boss_Walk` | 4.03s | ○ | 블렌드 트리 |
| `Zombie Running` | `Boss_Run` | 0.80s | ○ | 블렌드 트리 |
| `Zombie Attack` | `Boss_Attack` | 4.63s | ✕ | `Attack` 트리거 |
| `Zombie Scream` | `Boss_Scream` | 2.80s | ✕ | `Scream` 트리거 |
| `Zombie Dying` | `Boss_Die` | 3.33s | ✕ | `IsDead` |
| `Rifle Start Run` | `Boss_StartRun` | 1.73s | ✕ | **미연결** |

`Rifle Start Run`만 소총 모션이고 나머지 5개는 좀비 모션이다. 의도가 확인되지 않아 Humanoid로
임포트만 해두고 상태로는 넣지 않았다. 쓰려면 `BossAnimSetup.Clips`에서 `wire`를 `true`로.

**Idle 클립이 보스 폴더에 없다.** Humanoid 클립은 아바타를 넘나들며 리타깃되므로 블렌드 트리의
Speed 0 지점에 좀비의 `Zombie_Idle`(`Assets/monster/zombie1/animation/Zombie Idle.fbx`)을 빌려 쓴다.
보스 전용 Idle을 받으면 `IdleBorrowFbx`를 바꾸면 된다.

전 클립 **Bake Into Pose(XZ) = 꺼짐**. `applyRootMotion=false`인 이 프로젝트에서 이걸 켜면
이동량이 몸통 포즈로 들어가 메시만 앞으로 미끄러졌다 루프마다 되돌아온다(플레이어에서 실제로 발생했던 버그).

## 산출물

- `AC_Boss.controller` — 파라미터 `Speed`(float) `IsDead`(bool) `Attack`(trigger) `Scream`(trigger)
  - 기본 상태 `Locomotion` = Simple1D 블렌드 트리: Idle 0 / Walk 1.6 / Run 3.4 (m/s)
  - 단발 `Attack` `Scream` — AnyState 진입, `IsDead==false` 조건, exitTime 0.9로 Locomotion 복귀
  - `Dead` — AnyState 진입(`IsDead`), 복귀 없음
- `Boss_001.prefab` — `AC_Boss` + `boss_001Avatar`, `applyRootMotion=false`, `cullingMode=CullCompletely`,
  머티리얼 `Assets/monster/boss/boss_001/tilemap_zombie1 1.mat`

## 게임플레이

보스는 별도 클래스가 아니라 `ZombieKind.Boss`로 **`ProtoZombie`에 태웠다.** 사격 타게팅
(`ProtoSpawnDirector.FindNearest`), 투사체 피해(`ProtoProjectile`), 젬 드랍, 킬 카운트, 분리 그리드가
모두 `ProtoZombie`를 기준으로 동작하므로 이 편이 훨씬 적게 건드린다.
`ProtoZombie`가 쓰는 애니 파라미터는 `Speed`/`IsDead`/`Attack`뿐이고 AC_Boss에 전부 있어 그대로 맞는다.

수치는 `ProtoConfig`의 보스 절 (기획서에 없어 프로토에서 임시로 잡은 값):

| | 값 | 근거 |
|---|---|---|
| `BossHP` | 600 (×`EnemyHPScale`) | 150초 시점 870 → 기본 DPS 34.3에서 약 25초 교전 |
| `BossSpeed` | 2.2 | 플레이어 4.2보다 느려 카이팅 가능, 워커 1.6보다는 빠름 |
| `BossContact` | 25 | 워커 8 / 러너 6 대비 확실히 아픈 수준 |
| `BossXP` | 30 | 워커 1 / 러너 2 |
| `BossBodyRadius` | 0.55 | 측정한 다리 스탠스 반경 0.42 + 여유 |
| `BossSpawnTime` | 150s | 5분 런의 중반에 1마리 |

구현상 주의점 세 가지:

- **보스는 잡좀비 풀에 넣으면 안 된다.** 프리팹이 달라서 다음 워커 스폰이 이 인스턴스를 꺼내
  보스 외형의 워커가 생긴다. `ProtoSpawnDirector.Release`가 `IsBoss`면 큐에 넣지 않고 인스턴스만 남긴다.
- **밀어내기.** `separationWeight = 0`이라 잡좀비 물량에 밀려 떠다니지 않는다. `ProtoGrid`가
  최소 거리를 쌍마다 `bodyRadius` 합으로 계산하도록 바뀌었다 — 좀비끼리는
  0.28+0.28+0.06 = 0.62로 기존 `SeparationDist`와 값이 같아 감각 변화가 없다.
- **투사체 판정.** 고정 반경(0.45)만 쓰면 덩치 큰 보스는 어깨로 총알이 통과한다.
  `ProtoProjectile`이 기준 반경 초과분(`bodyRadius - BaseBodyRadius`)만 더한다 — 좀비는 초과분 0.

디버그: 플레이 중 **B 키로 즉시 소환**(`debugBossHotkey`). 150초를 기다리지 않고 확인할 수 있다.
`enableBoss`를 끄면 등장하지 않는다. HUD 상단 중앙에 보스 체력 바가 나온다.

## 미해결

- 블렌드 임계값 1.6 / 3.4은 좀비와 같은 계약(Speed = m/s)을 유지한 값이다. `Boss_Run`의 실제
  보폭 속도는 4.17 m/s인데 `BossSpeed`는 2.2이므로 **발이 미끄러져 보인다.** Locomotion 블렌드
  임계값을 보스 클립 실측치로 다시 잡거나 `BossSpeed`를 올릴 것.
- 보스 전용 연출이 없다. `Boss_Scream`을 등장 시 1회 트리거하는 것 말고는 등장 예고·카메라 연출·
  피격 반응이 없다.
- 보스 메시 크기 (3.45, 1.52, 2.62) — 좀비 (1.43, 0.71, 1.77) 대비 높이 1.48배. 프리팹 스케일은 1.
- 그림자는 좀비와 같은 셰이더 내장 블롭으로 통일했다(`BCM_PLANAR_SHADOW`, Blob Half Extents 1.25).
