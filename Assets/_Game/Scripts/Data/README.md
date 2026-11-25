# 데이터 테이블 — 적 · 캐릭터 · 필사기 (2026-11-05)

**코드에는 행동 방식만, 숫자와 연결은 전부 에셋.** 무기(`WeaponStats`·`WeaponModelDef`·`WeaponFxSet`)가 먼저 이 형태였고,
2026-11-05 에 코드에 박혀 있던 나머지 셋 — 적 수치(`ProtoConfig` 상수), 캐릭터 ↔ 무기 ↔ 필사기 연결(에디터 표 + 프리팹 배열 + 이름 검사),
필사기 수치(`ProtoUltimate` 상수) — 를 같은 형태로 옮겼다.

## 어디에 무엇이 있나

| 테이블 | 에셋 | 클래스 | 읽는 코드 |
|---|---|---|---|
| 적 7종 | `Assets/_Game/Data/Enemies/E_*.asset` | `EnemyDef` | `ProtoZombie.Spawn / MakeElite` |
| 적 칸 + 일정 | `Assets/_Game/Resources/Data/EnemyTable.asset` | `EnemyTable` | `ProtoSpawnDirector`, HUD(엘리트 도망 표시), 젬 등급, 헬기 추락 경험치 |
| 캐릭터 6명 | `Assets/_Game/Data/Characters/C_*.asset` | `CharacterDef` | `ProtoCharacterSwitcher`(명단·프리팹·무기 표시), `ProtoUltimate.DefOf`, `CharacterSetup`(프리팹 굽기) |
| 캐릭터 명단 | `Assets/_Game/Resources/Data/CharacterTable.asset` | `CharacterTable` | 위와 같음. 순서 = 숫자키 |
| 필사기 3종 | `Assets/_Game/Data/Ultimates/ULT_*.asset` | `UltimateDef` 자식 (`UltTargetKillDef` · `UltFlameRingDef` · `UltBarrageDef`) | `ProtoUltimate` |
| 무기 (기존) | `Assets/_Game/Data/Weapons/…` | `WeaponModelDef` · `WeaponStats` · `WeaponFxSet` | `WeaponSocket`, `ProtoShooter` 등 |
| 진화표 (기존) | `Assets/_Game/Resources/Data/EvolutionTable.asset` | `ProtoEvolutionTable` | `ProtoEvolution` |

`Resources/Data` 아래 두 테이블은 런타임이 `Resources.Load` 로 읽는다(`EnemyTable.I`, `CharacterTable.I`). 없으면 에러 로그 한 번 + 그 기능이 멈춘다(적이 안 나온다 / 패널이 빈다).

## 규칙

- **행동은 코드가 고른다.** `EnemyDef.behaviour`(걷기·자폭·원거리·물기·보스)와 `UltimateDef` 의 자식 타입이 "어떻게 움직이나"를 정하고, 나머지 칸은 그 움직임의 숫자다.
  같은 움직임에 다른 숫자인 적(엘리트 = 워커 + 큰 덩치)은 에셋 복제로 끝난다. 새 움직임이 필요할 때만 `ProtoZombie` / `ProtoUltimate` 를 건드린다.
- **숫자와 자산 참조를 한 에셋에 두되 칸은 가른다.** `CharacterDef` 는 프리팹·머티리얼(아트)과 무기·필사기(기획)를 같이 들지만, 수치는 들지 않는다 — 수치는 무기·필사기 에셋 쪽이다.
- **트리 선택(강화 I·II, 갈래 A/B)은 `UltTree`(ProtoRun) 가 고르고, 선택마다 쓸 숫자가 에셋에 나란히 있다** (`damageMul` / `damageMulI`, `radius` / `radiusA`).
- **검증은 플레이 들어갈 때 자동.** `DataTableSetup` 이 `ExitingEditMode` 에서 빈 칸·이상한 값을 경고로 띄운다. 수동: `Tools ▸ 좀비 오버런 ▸ 데이터 테이블 ▸ 검증`.

## 새 것을 붙이는 순서

- **적 하나 더**: `좀비 오버런 ▸ 적` 으로 `E_*.asset` 생성 → 움직임·숫자 → `EnemyTable` 의 칸에 꽂는다.
  칸이 7개로 고정이라 "8번째 종류"는 칸을 하나 더 파야 한다(`EnemyTable` + `ProtoSpawnDirector.SpawnOne`). 종류가 10개를 넘으면 목록 + 가중치 표로 바꿀 때다.
  프리팹은 아직 스폰 디렉터 인스펙터(`zombiePrefab / rangedPrefab / dogPrefab / bossPrefab / finalBossPrefab`)에 있다 — 풀이 프리팹 단위라 같이 옮기려면 풀 구조를 바꿔야 한다.
- **캐릭터 하나 더**: `좀비 오버런 ▸ 캐릭터` 로 `C_07_*.asset` 생성 → 머티리얼·무기·필사기 → `CharacterTable.roster` 끝에 추가 →
  `Tools ▸ 좀비 오버런 ▸ 데이터 테이블 ▸ 캐릭터 프리팹 만들기·갱신` 이 베이스에서 배리언트를 만들어 `prefab` 칸을 채운다. 코드 수정 없음.
- **필사기 하나 더**: 기존 틀(표적 사살 · 불꽃 고리 · 광란 난사)과 같으면 `좀비 오버런 ▸ 필사기 ▸ …` 로 에셋 복제 후 캐릭터에 꽂는다.
  새 틀이면 `UltimateDef` 자식 클래스 하나 + `ProtoUltimate` 에 코루틴 하나 + `KindOf` 분기 + `ProtoRun` 에 트리 칸.

## 아직 코드에 남은 것 (다음 후보)

- 최종 보스 패턴 수치(내려찍기·도약의 사거리·반경·피해·쿨) — `ProtoBossBrain`. `EnemyDef` 에 "보스 패턴" 칸을 더하면 된다.
- 플레이어 공통 수치(체력 100·이동 4.2·치명타) — `ProtoConfig`. 캐릭터마다 달라지는 순간 `CharacterDef` 로.
- 상태 효과(화상·감속·보호막·아드레날린) — 각 스크립트에 흩어짐. 종류가 늘면 `StatusDef`.
- 버프 아이템 4종 수치·가중치 — `ProtoConfig`. 종류가 늘면 `PickupDef`.
- 적 프리팹 참조 — 스폰 디렉터 인스펙터 (위 참조).
