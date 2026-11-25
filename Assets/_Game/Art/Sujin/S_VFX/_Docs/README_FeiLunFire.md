# 불 고리 `MFX_FireRing`(지금 판 = 불덩이 머리 2개) · 불꽃 비륜 `MFX_FeiLun_Fire`(1차, 사용자 반려) — 임시 (출시 전 반드시 교체·삭제)

## 지금 판: `MFX_FireRing` — 불덩이 머리 2개 (2026-11-18)

흐름: 1차 비륜(아래) "이런 느낌이 아닌데" → "불 고리가 휘감아 도는 쪽" → 레퍼런스 이미지(마주 보는 불덩이 2개가 들쭉날쭉한 불꽃 가닥을 끌며 돎) → "원형 라인이 안 보이게 불꽃이 크게 타오르게"
→ 불덩이 2개 판에 "이 느낌이 맞는데 꼬리를 길게 해서 하나로 연결된 느낌으로" (+ "헤드가 두 개로 보이면 안 돼" → 11-13 저녁엔 머리 없이 하나로 이어진 고리로 바꿨다 — 아래 '지난 판')
→ **11-18 사용자가 꼬리를 늘린 불덩이 2개 판 그림을 보내며 "요렇게 다시, 헤드 부분을 좀 더 불덩이로"**. 그래서 지금은 **불덩이 2개가 마주 보며 돌고, 각자 반 바퀴 길이 불길을 끌어 두 줄기가 한 고리로 이어진다.** **게임에 연결됨 — 아래 '게임 연결'.**

- 프리팹 `Assets/_Game/Art/Sujin/VFX/Ultimate/MFX_FireRing.prefab`(11-23 옮김, 옛 `Prefabs/MFX_FireRing.prefab` · GUID 그대로 — 카탈로그 · 필사기 연결 그대로) — **캐릭터 발밑(가운데)에 두는 한 덩어리**. 스크립트 없이 파티클만으로 돈다.
  루트 1 = 반지름 2.5m · 높이 0.5m · 42rpm(공전 −4.4 라디안/초, 위에서 볼 때 반시계). 크기는 루트 배율로(불꽃 그림도 같이 커진다).
- 구성(`Ring` 아래). 머리 층은 모두 **원 둘레 균등 분출(BurstSpread) 2개 + 같은 공전**이라 0° · 180° 에 저절로 겹친다(수명 10000초, 최대 2개):
  - `FlameTrail_Outer` 넓은 주황 불꼬리(1.6m · **0.77초 = 반 바퀴 + 조금**, 끝 알파 0.3 — 다음 머리 밑으로 들어가 이어짐) · `FlameTrail_Inner` 금빛 심 꼬리(0.55m · 0.5초) — 낱입자 꼬리, `FireRing_Band_M1`.
  - `Head_Glow` 불덩이 뒤에 깔리는 붉은 주황 빛(2.3, 알파 0.55) + 서브 이미터 4개:
    - **`HeadFire` 불덩이 몸통(11-18)** — 머리에서 `Fire_115` 덩어리가 0.14~0.28초로 짧게 끓어오르듯 피어나 **머리와 같은 공전(1배)** 으로 따라 돈다. 크기 1.0~1.5, 머리 하나당 초당 110, 흰 노랑 → 주황 → 빨강, 노이즈(흔들림 0.25 · 크기 맥동 0.35) · 회전 ±4.
    - **`HeadFlicker` 깜빡이는 심(11-18)** — 0.06~0.14초로 번쩍이는 밝은 빛(초당 40) — 불덩이 속이 이글거린다.
    - `Licks` 피어오르는 큰 불덩이 — 머리보다 훨씬 느리게(공전 0.3배) 돌아 반 바퀴 뒤까지 불길이 남는다(머리가 불을 붙이고 지나가는 느낌). 초당 80, 크기 1.0~1.8, 위로 0.6~1.2m/s.
    - `Wisps` 노이즈 먹은 짧은 불꽃 가닥(공전 0.5배).
  - `Head_Core` 노란빛 심(0.75) — 11-13 의 흰 점(0.55)을 키우고 하얗게 날아가지 않게 노란빛으로.
- 11-13 판(불덩이 2개 · 꼬리 늘림) 대비 바뀐 것은 머리뿐이다: 둥근 빛 구슬(1.6) → 붉은 바탕 빛(2.3) + `HeadFire` · `HeadFlicker` 추가 + 심 0.75.
- 입자 약 머리 층 8 + 불덩이 몸통 45 + 깜빡임 7 + 피어오르는 불 130 + 가닥 40 ≈ 230개.

### 도는 속도 바꾸기 (사용자가 플레이하며 조절 예정)

층마다 **Velocity over Lifetime → Orbital Y**(라디안/초, 음수 = 위에서 볼 때 반시계):

| 층 | Orbital Y | 머리 대비 |
|---|---|---|
| `FlameTrail_Outer` · `FlameTrail_Inner` · `Head_Glow` · `Head_Core` | −4.4 (= 42rpm) | 1 |
| `HeadFire` · `HeadFlicker` (`Head_Glow` 아래) | −4.4 | **1 — 머리와 꼭 같게**(다르면 불덩이가 머리에서 떨어진다) |
| `Wisps` | −2.2 | 0.5 |
| `Licks` | −1.32 | 0.3 |

- 한꺼번에 빠르게 · 느리게: 모든 파티클 층을 골라 **Main → Simulation Speed** 를 같이 바꾸는 게 가장 쉽다(깜빡임 · 피어오름도 같이 빨라진다).
- 회전만 바꿀 땐 위 비율대로 Orbital Y 를 고친다. **머리를 느리게 하면** 꼬리가 반 바퀴를 못 채워 두 줄기 사이에 틈이 생기니 `FlameTrail_Outer` → Trails → Lifetime 도 같은 비율로 늘린다(지금 0.0000774 = 0.774초 ÷ 수명 10000초).

### 지난 판: 하나로 이어진 불길 고리 (11-13 저녁 ~ 11-18, 필사기에 연결돼 있던 판)

머리 없이 고리 전체에서 불이 고르게 타오르는 판(띠 방출점 4개 · `Licks` 초당 440 · `HotCore` · `Sparks` · `SparksSmall`). 사용자 요청 이력: 불꽃 크기 절반 · 불티 · 이글거림 · 바닥 불빛 뺌 · 불티는 고리 둘레에서만.
그 판 프리팹 · 빌더 · GIF 는 `D:\Fork\Newgame\ZombieRun\_Backup\20261118_FireRingFireball\`(`MFX_FireRing_continuous.prefab` 등) — 되돌리려면 프리팹을 덮어쓴다(GUID 같음). 불티가 쓰던 `FeiLunFire_Ember_M1` · `Huoxing` 은 지금 판엔 안 쓰지만 남겨 둔다.

### 파일 · 함정

- 새 파일 4개(11-13): 프리팹 · `Materials/FireRing_Band_M1`(꼬리 — 함정 5) · `Materials/FireRing_Lick_M1`(Fire_115 가산, 큐 3150) · `Textures/Fire_115.png`(원작 GUID). 나머지는 1차 때 넣은 파일과 공유 `Glow_011_M1`. 11-18 은 새 파일 없음.
- `Source~/fire_ring/` — 굽는 스크립트 `FireRingBuild.cs`(지금 판) · 렌더 `OrbitProbe.cs`(`-count 1 -radius 0 -height 0 -spin 0` 로 가운데에 하나, `-fit <반경>` 은 필사기 FitRing 과 같은 계산) · `fire_ring_orbit.gif`(지금 판, 2초 실제 속도) · `fire_ring_v1_two_heads.gif`(11-13 꼬리 늘리기 전 판).
- 함정 5: **낱입자 꼬리(PerParticle)는 U=0 · 색 그라데이션 0 이 머리**다 — 리본용 원작 마스크(`Mask_013`, U=1 쪽이 밝음)를 그대로 쓰면 머리가 투명하고 끝만 밝다. `FireRing_Band_M1` 은 마스크 타일링 −1 · 오프셋 1 로 뒤집었다.
- 함정 6: 공전 속도(orbital)는 **라디안/초**, 양수 = 위에서 볼 때 시계 방향(프레임별 입자 각도로 잼).

### 게임 연결 (2026-11-13 사용자 "화염방사기 게이지가 차면 나가는 불꽃 고리에 쓸 이펙트 — 기존꺼 빼고 새로 만든걸로")

- **파이로 필사기 '불꽃 고리'**(`Data/Ultimates/ULT_FlameRing`, `ProtoUltimate.Pyro`)의 그림을 이 프리팹으로 바꿨다. 판정 · 피해 · 화상 · 갈래 B 연소는 그대로.
- FX 카탈로그(`Resources/Data/FxCatalog.asset`) 새 항목 **`ult_fire_ring`**(`FxIds.UltFireRing`) → 프리팹 `MFX_FireRing`, 스팬 · 게임 시계 · lifetime 1.2 · 동시 1 · onTop 끔. 카탈로그에서 프리팹만 바꿔 끼울 수 있고, 비우면 그림 없이 판정만 남는다.
- `ProtoUltimate`: 발동 때 `ProtoFx.Play(ult_fire_ring, 발밑, span)` → 매 프레임 플레이어 발밑으로 **위치만** 옮긴다(회전은 안 따름 — 조준으로 몸을 돌려도 고리가 안 휘둘림). 끝나면 수명 10000초 층(머리 · 꼬리 · 띠 방출점)만 바로 지우고 나머지는 방출을 멈춰 사그라든다.
- **판정 반경에 맞추기(`FitRing`)**: 프리팹은 반지름 2.5m 인데 필사기는 3.5m(갈래 A 대화염 5m). 트랜스폼 배율로 키우면 불꽃 그림까지 커지므로, **원 둘레 방출 반지름만** ×1.4 / ×2 로 넓히고 둘레가 길어진 만큼 초당 개수 · 상한도 같은 비율로 늘린다(불꽃 크기 · 회전 각속도 그대로). 임시 프로젝트에서 3.5 · 5m 로 렌더해 끊김 없음 확인.
  - 11-13 저녁 판(고리 전체에서 고르게 내는 층)은 입자 수도 같은 비율로 늘었다(3.5m 약 650개, 5m 약 930개).
  - **11-18 불덩이 판**: 둘레에서 고르게 내는 층이 없어(머리는 버스트 2개) 개수는 안 바뀌고 머리 자리만 넓어진다. 불덩이 몸통 · 깜빡임 · 피어오르는 불은 머리의 서브 이미터라 넓어진 머리 자리에서 나오고, 공전이 각속도라 반경이 커져도 머리를 그대로 따라간다. 꼬리도 시간(반 바퀴)이라 반경과 상관없이 두 줄기가 이어진다.
- 뺀 것: 바닥 주황 테두리 선(판정 반경 표시, 밝은 마디 4개) · `FX_BurnBody` 를 한 벌 띄워 코드로 뿜던 잔불 · 불꽃 덩어리 4개. `UltFlameRingDef` 의 연출 칸 5개(`flamePerMeterPerSec` · `flameHeads` · `flameSpin` · `headPerSec` · `headTrailDeg`)와 에셋 값도 지웠다.
- 판정 띠는 반경 −1.4m ~ +0.3m(안쪽으로 넓다)인데 불길은 반경 위에 있다 — 고리 안쪽 1.4m 까지도 맞는다는 것은 그림으로는 안 보인다.
- 바꾸기 전 원본: `D:\Fork\Newgame\ZombieRun\_Backup\20261113_FireRingUlt\`(`ProtoUltimate.cs` · `UltFlameRingDef.cs` · `ULT_FlameRing.asset` · `ProtoFxBuiltins.cs` · `FxCatalog.asset`). 컴파일(런타임 · 에디터) 통과, **메인 에디터 · 플레이 미확인**.
- 확인: 임시 프로젝트 렌더만(11-18 불덩이 판도). 메인 에디터 · 플레이 미확인.

## 1차: 불꽃 비륜 `MFX_FeiLun_Fire` — Survivor Ace 빙결 톱날을 불로 바꾼 것 (사용자 반려, 파일은 남겨 둠)

2026-11-13 사용자: "SurvivorAce 프로젝트에서 내 비행기 주변을 뱅뱅 도는 이펙트 가져와서 불꽃이 빙글빙글 도는것처럼 수정해줘".
**원작사 저작물(을 고친 것)이다.** 프로토에서 느낌을 보는 용도로만 쓰고, 빌드·외부 공개 전에 우리 이펙트로 바꾼다.
**게임에 아직 연결하지 않았다** — 프리팹만 있다. 붙이는 방법은 아래 '게임에 붙일 때'.

## 원작에서 무엇인가

- 스킬 10001601 **빙결 톱날(Frost Sawblade)** — "얼음 톱날 2개가 자신 둘레를 돌며 적을 벤다". 같은 탄을 10101609(톱날 +1) · 10816011~15(다른 모드)도 쓴다.
- 탄 표 `Bullet_Bullet` 200141 · 200145 · 360002 · 360003 → 모델 1000009 = `Assets/_Resources/Prefab/Bullet/Hero/stg_TempRotate_ball_5.prefab`(탄 Scale 3~4, 수명 0 = 계속).
- 추출본에 같은 이름이 4벌 있다. 1.2.0 · 핫픽스 번들 둘 다에 들어 있는 **새 판(`stg_TempRotate_ball_5_0.prefab`, 자식 `MFX_FeiLun_Bullet_Ice`)** 을 원본으로 썼다. 옛 판(`stg_TempRotate_ball_5.prefab`, `SpriteAnimation` 자식)은 안 썼다.
- 궤도 운동은 프리팹이 아니라 게임 코드(HotFixBattle)가 돌린다 — 프리팹은 톱날 하나다.

## 무엇을 바꿨나 (얼음 → 불)

원작 구성(`MFX_FeiLun_Bullet_Ice`)을 그대로 두고 색 · 텍스처 · 방향만 바꿨다. 최상위 `MFX_FeiLun_Fire`(트랜스폼만) 아래 `FireWheel`(×0.5, 회전 없음).

| 층 | 원작 | 불 판 |
|---|---|---|
| `Trail` · `Trail_Dark` | 파란 리본 궤적 2줄(초당 30점, 0.25초, 노이즈 0.2) | **주황 → 붉은 불줄기**. 초당 90점(곡선 매끈) · 0.36초 · 굵기 3.4 · 노이즈 끔 · 방출 위치 보정 (0, 0.5, 0.5) → 0. 새 머티리얼 `FeiLunFire_Trail_M1/M3`(틴트 알파 ×2) |
| `Glow_Dark` · `Glow` · `Swirl_Dark` · `Ring` | 45° 기운 부모 + 로컬 정렬 + 3D 회전(원작 직교 탑뷰에 정면) | **바닥과 나란한 수평 빌보드**, 소용돌이 · 고리는 불 색(빨강 → 주황 → 노랑). `Glow_Dark` 는 복사본 `FeiLunFire_GlowDark_M3`(큐 2930) |
| `FireHaze` (옛 `Smoke_Dark`) | 하늘색 가산 연기 | 주황 가산 아지랑이 |
| `Embers` (옛 `Xuehua` 눈송이) | 눈송이 | **불씨** — 페더 먹은 빛 점(`Huoxing`), 살짝 떠오름 |
| `Flames` (옛 `Bingkuai` 얼음 조각) | 얼음 조각 | **불 혀** — `Fire_128` 4×2 시트, 바깥으로 살짝 튀며 위로 피어오름 |
| `Smoke` (옛 `Smoke_Dark` 2) | 파란 연기(Hovl 셰이더) | 옅은 검은 연기(`Smoke3`, 큐 2920 — 불줄기 아래) |
| `FlameRim` | — (새로) | **테두리를 빙글빙글 도는 불꽃** — 로컬 시뮬, 반경 0.7m 원 둘레, 공전 속도 −7 |
| 지운 것 | 꺼진 `Glow` 하나, 렌더러가 모두 꺼진 `all` 묶음, 꺼진 탄 그림 `BulletModel`, 루트의 `ParticleSequenceBaker` 스크립트 | |

- 크기: 루트 1 이면 원판 지름 약 1.3m(회전 블레이드 판정 지름 1.1m 와 비슷). 렌더로 본 궤도 = 반지름 2.5m · 높이 0.8m · 42rpm · 제자리 회전 4배(`ProtoSupportGear.TickOrbit` 과 같은 식).
- 원작 머티리얼(`Swirl_005_M3` · `Ring_057_M3 1` · `Smoke_020_M1`)은 원본 그대로이고 색은 파티클 색으로만 바꿨다. 공유 파일 `Effects_Particle.shader` · `Glow_011_M1.mat` · `Glow_011.png` · `Glow_010_T1.png` 는 **쓰기만 하고 안 고쳤다.**
- 새 셰이더 `Shaders/Effects_UVFlow.shader` = 원작 `Effects/UVFlow` 근사판(11-09 복구본), 이름만 **`SurvivorAce/Effects/UVFlow`**(GUID a5882cd6… 원본 유지).

### 손질하며 밟은 함정 (다른 원작 이펙트에도 해당)

1. **원작 이펙트는 직교 탑뷰용 45° 기울기가 들어 있다** — 우리 52° 카메라에서 제자리 회전을 주면 원판이 회전문처럼 기운다. 바닥과 나란해야 하는 판은 수평 빌보드로.
2. **방출 모양 위치 보정도 그 기울기 기준이다** — 궤적 방출점이 원판 중심에서 0.25m 비껴 있어 제자리 회전 때 원을 그리며 리본이 접혔다(각진 카드 · 직선 테두리). 원작 얼음 판도 같은 증상.
3. **그리는 순서**: 어두운 바탕 빛(큐 3000) · 검은 연기(3100)가 궤적(2940~2945) 위에 그려져 불줄기를 덮어 탁하게 만들었다 → 둘을 궤적 아래로.
4. **가산 색을 1 넘게(HDR 틴트) 올리면 LDR 렌더에선 섞기 전에 잘려 노랗게(올리브색) 뜬다** — 밝기는 틴트 **알파**로 올린다. 선형 색 공간이라 옅은 주황 가산은 황토색으로 보이니 색을 붉은 쪽으로.

## 들어 있는 파일 (이번에 새로 넣은 19개)

- 프리팹 `Prefabs/MFX_FeiLun_Fire.prefab`
- 새 머티리얼 6: `FeiLunFire_Trail_M1` · `FeiLunFire_Trail_M3` · `FeiLunFire_GlowDark_M3` · `FeiLunFire_Ember_M1` · `FeiLunFire_Flame_M3` · `FeiLunFire_Smoke_M3`
- 원작 머티리얼 3(원본 GUID): `Swirl_005_M3` · `Ring_057_M3 1` · `Smoke_020_M1`
- 텍스처 8(원본 GUID): `Trail_073` · `Mask_013` · `Swirl_005_t` · `Ring_057` · `Smoke_020` · `Smoke3` · `Huoxing`(불씨) · `Fire_128`(불 혀 시트)
- 셰이더 1: `Effects_UVFlow`
- `Source~/feilun_fire/` — 굽는 스크립트 `FireOrbitBuild.cs` · 궤도 렌더 `OrbitProbe.cs`(임시 프로젝트용, 여기선 컴파일 안 됨) · `feilun_fire_orbit.gif`(루키 둘레 2바퀴 남짓, 실제 속도) · `feilun_ice_vs_fire.png`(원작 얼음 ↔ 불 판)

## 게임에 붙일 때 (아직 안 함)

회전 블레이드(`Data/Weapons/W_OrbitBlade.asset` → `WS_OrbitBlade`, `ProtoSupportGear` 가 `model` 을 개수만큼 만들어 2.5m 궤도로 돌림)에 붙이는 게 자연스럽다.
- `model` 을 이 프리팹으로 바꾸면 `weaponScale`(지금 0.0903 — 톱날 메시용)이 곱해지니 **1 로**, `WS_OrbitBlade.modelRotationOffset`(지금 90,0,0 — 메시용)은 **0,0,0** 으로. 판정 · 소리는 그대로.
- 톱날 메시도 같이 보이게 하려면 모델은 그대로 두고 이 프리팹을 자식으로 붙이는 방법도 있다.
- 실제 게임은 HDR(URP-Asset `m_SupportsHDR: 1`)이라 LDR 미리보기보다 가산 층이 조금 더 밝을 수 있다(틴트 알파 2).

## 확인 상태

임시 URP 프로젝트(Unity 2022.3.69f1 batchmode, 셰이더 오류 없음)에서 루키 둘레 궤도 렌더로만 확인했다. **메인 에디터 화면 · 플레이 · 물량전 부하는 미확인**(원판 1개당 입자 약 200개, 4개면 800개).
다시 굽기: 임시 프로젝트에 원작 프리팹(`Assets/SAOrig/stg_TempRotate_ball_5_0.prefab`)과 위 파일을 두고 `-executeMethod FireOrbitBuild.BuildAndRender -fx Assets/_Reference/SurvivorAce/Prefabs/_Unused/MFX_FeiLun_Fire.prefab -out fire -frames 60` — **메인의 프리팹을 손으로 고친 뒤엔 다시 굽지 말 것**(덮어씀).
