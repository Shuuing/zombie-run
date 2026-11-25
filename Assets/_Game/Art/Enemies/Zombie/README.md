# 좀비 애니메이션 셋업

재생성: Unity 메뉴 **Tools ▸ 좀비 오버런 ▸ 좀비 애니메이션 셋업**
스크립트: `Assets/_Game/Editor/ZombieAnimSetup.cs` (임포트 설정까지 코드가 관리 — 손으로 인스펙터를 만지지 말 것)

## 소스 파일 주의

`Assets/monster/zombie1/` 아래에 `zombie_basic01.fbx`가 **두 개** 있다.

| 경로 | 내용 | 용도 |
|---|---|---|
| `zombie1/zombie_basic01.fbx` | 본 0개, 스킨드메시 없음 (정적 메시 1,989 tris) | 사용하지 않음 |
| `zombie1/animation/zombie_basic01.fbx` | 본 46개, 스킨드메시 1개, `mixamorig:` 리그 | **아바타 소스** |

아바타는 `animation/zombie_basic01.fbx`에서 생성한다 (`zombie_basic01Avatar`, valid + human).
나머지 애니메이션 7개는 이 아바타를 **Copy From Other Avatar**로 참조한다.

## 클립

| 클립 | 길이 | 루프 |
|---|---:|---|
| `Zombie_Idle` | 4.23s | O |
| `Zombie_Walk` | 4.03s | O |
| `Zombie_Run` | 0.80s | O |
| `Zombie_Attack` | 4.63s | X |
| `Zombie_Bite` | 6.93s | X |
| `Zombie_StandUp` | 3.10s | X |
| `Zombie_Die` | 3.33s | X |

전부 Humanoid(`humanMotion=true`). 루트 이동은 제거(`lockRootHeightY`, `keepOriginalPositionXZ=false`)했고
`applyRootMotion=false` — **이동은 애니메이션이 아니라 이동 코드가 담당한다.**

## AnimatorController (`AC_Zombie.controller`)

파라미터: `Speed`(float) · `IsDead`(bool) · `Attack`(trigger) · `Bite`(trigger) · `Spawn`(trigger)

- **Locomotion** (기본 상태) — Simple1D 블렌드 트리, `Speed` 기준
  임계값은 GDD §08 이동속도와 일치: `Idle 0` / `Walk 1.6` / `Run 3.4`
- **Attack / Bite / StandUp** — AnyState에서 트리거로 진입, 90% 지점에서 Locomotion 복귀
- **Dead** — `IsDead == true`로 진입, 복귀 없음

`Attack`/`Bite`/`Spawn` 전이에는 `IsDead == false` 조건이 걸려 있다.
**사망 후 공격 애니메이션이 끼어드는 버그를 막기 위한 것이므로 지우지 말 것.**

## 프리팹 (`Zombie_Walker.prefab`)

- 스킨드메시 1,989 tris / 본 46개
- 머티리얼은 프로젝트 원본 `tilemap_zombie1.mat` (`BCM_Toony_shader_03`)을 그대로 사용.
  직접 만들면 `_SColor`(셰도우 컬러)와 `_SColorMap`을 놓쳐 좀비가 어둡게 렌더링된다.
- 모델이 이미 실제 스케일(높이 1.83m) — 스케일 보정 거의 불필요
- `cullingMode = CullCompletely` — 화면 밖 좀비는 애니메이션 연산을 멈춘다

## 검증 결과 (플레이 모드 실측)

- `Speed` 0 / 1.6 / 3.4 → Idle / Walk / Run 각각 weight 1.00로 재생, `normalizedTime` 진행 확인
- `Attack` → `Zombie_Attack`, `Bite` → `Zombie_Bite`, `Spawn` → `Zombie_StandUp` 전이 정상
- `IsDead=true` → `Zombie_Die`, 이 상태에서 `Attack` 트리거는 무시됨 (가드 작동 확인)

## 후속 과제 — 이 셋업만으로는 350마리를 못 돌린다

GDD §16이 못 박은 대로, **Animator 350개는 모바일 CPU에서 즉사한다.** 이 Humanoid 셋업의 역할은:

1. **보스·엘리트용** — 개체 수가 적으므로 이대로 Animator를 쓴다.
2. **일반 좀비용 VAT 베이크의 소스** — 정점 애니메이션 텍스처를 구우려면 먼저 정상 동작하는 클립이
   필요하다. 여기까지가 그 전제 조건이고, 다음 단계는 이 7개 클립을 VAT로 굽는 베이커다.

즉 **일반 몹은 이 프리팹을 그대로 쓰면 안 된다.** §19 체크리스트의 "VAT 베이크" 항목 참조.
