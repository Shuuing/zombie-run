# 교체 등장 이펙트 — Survivor Ace `MFX_ChuanSong_Appear` 노란 복사본 (임시 · 출시 전 반드시 교체)

2026-11-13 사용자 요청 "캐릭터 바뀌거나 무기 바뀔 때 이펙트 — MFX_ChuanSong_Appear 가져와서 붙이고 노란색 계열로".
원본: `D:\SurvivorAce_1.2.0\UnityProject\ExportedProject\Assets\MFX_ChuanSong_Appear.prefab` (传送 = 텔레포트 등장, 초록).
**Survivor Ace 원작사 저작물이다.** 프로토에서 느낌을 보는 용도로만 쓰고, 빌드·외부 공개 전에 우리 이펙트로 바꾼 뒤 지운다.
카탈로그(`Resources/Data/FxCatalog.asset`)가 참조하는 동안에는 빌드에 같이 들어간다.

같은 폴더의 탄·총구·착탄(`README.md`)은 다른 작업이다. 아래 "공유" 파일은 두 작업이 같이 쓴다.

## 게임에 나오는 곳

| 카탈로그 ID | 부르는 코드 | 자리 | 대신 숨기는 것 |
|---|---|---|---|
| `char_swap_appear` | `ProtoCharacterSwitcher.SwapRoutine` — 교체 시작 순간(옛 몸이 줄어들기 시작할 때) | 플레이어 발밑, 따라다님, scale 1 | 하늘 번개 `ProtoLightningStrike`(줄기 + 바닥 링) |
| `weapon_change_appear` | `ProtoEvolution.Evolve` — 무기 진화(진화권 + 메인 무기 Lv5, 디버그 F5) | 플레이어 발밑, 따라다님, scale 1 | 진화 빛기둥 `evolve_beam` |

- 둘 다 같은 프리팹을 가리킨다. 한쪽만 다른 모양으로 바꾸려면 카탈로그 그 항목의 `prefab` 칸만 바꾸면 된다.
- 교체 시각(0.10초 뒤 몸 교체 + 주변 피해) · 떨어져 내려오는 몸 · 착지 먼지(`FX_SwapLanding`) · 진화 흔들림·소리·문구는 그대로다.
- 카탈로그 `clock` = Unscaled, 프리팹 파티클 9개 모두 `useUnscaledTime` — 레벨업 카드가 시간을 멈춘 동안 교체해도 얼지 않는다.
- 수명 1.2초(입자는 0.9초 안에 다 사라진다), 동시 2개.

## 원본에서 바꾼 것 (`Prefabs/MFX_ChuanSong_Appear_Yellow.prefab`, 새 GUID `5c1e7a0d…`)

- **색**: 초록(색상 113~131°) 14개 값 → 노랑(색상 약 48~53°). 채도·명도는 그대로, 원래 값끼리의 차이는 0.3배로 좁혀 남겼다.
  텍스처는 전부 흰색이라 색은 파티클 시작색·수명 색 그라데이션에만 있다(머티리얼 · 텍스처는 원본 그대로).
  예: 짙은 초록 (0.09, 1, 0) → (1, 0.81, 0), 옅은 초록 (0.53, 1, 0.56) → (1, 0.93, 0.53).
- **기울기**: 자식 `ChuanSong` 의 X 45° → **0°**. 원작은 바로 위에서 보는 직교 카메라라 고리를 45° 세워 "비스듬한 바닥" 처럼 보이게 했다.
  우리 카메라(YAZS 52°)에서는 45° 면 고리가 납작한 선이 되고, 0°(바닥에 눕힘)면 발밑 타원 고리 + 몸을 타고 오르는 고리로 원작 느낌이 난다.
- **크기**: 루트 배율 1 → **0.6**. 원작 화면 비율(직교 세로 36 유닛 ↔ 우리 화면 약 15m, 0.43배)보다 조금 크게 — 퍼지는 바닥 고리 최대 반경 약 0.8m, 빛기둥 폭 약 1m, 루키(키 1.70m) 기준.
  교체 피해 반경 1.86m 보다는 작다 — 번개 바닥 링처럼 "테두리 = 판정선" 규칙은 이 이펙트에는 없다. 키우려면 루트 배율만 바꾼다.
- **반복 끔**: 렌더러가 꺼진 빈 껍데기 둘(루트 · `ChuanSong`)이 5초 반복·초당 10개 방출이었다 → 반복 끔 · 방출 0.
- 모양 요소(원본 이름): `Shanguang` 섬광 · `Glow_an`/`Glow_liang` 바닥 빛 · `Kuo_quan` 퍼지는 고리 · `Quan_sheng` 오르는 고리 · `Guangxian` 빛기둥 · `Lizi` 네모·세모 조각(룬 시트 2×2).

## 파일

- 공유(다른 작업이 먼저 넣음, 원본 GUID — 고치지 말 것): `Shaders/Effects_Particle.shader`(셰이더 이름 `SurvivorAce/Effects/Particle`, 추출본 근사 복구판) · `Materials/Glow_011_M1.mat` · `Glow_011_M3.mat` · `Textures/Glow_011.png` · `Ring_102.png` · `Glow_010_T1.png`
- 이 작업이 넣음(원본 GUID): `Materials/Glow_029_M3.mat` · `Rune_120_M1.mat` · `Ring_102_M1.mat` · `Mask_016_M1.mat`, `Textures/Glow_029.png` · `Rune_120.png` · `Mask_016.png`
- 만든 스크립트: `Source~/make_yellow.py` (뒤에 `~` 붙은 폴더는 Unity 가 무시). `python -P make_yellow.py "<Assets 경로>" 0.6 0` 으로 다시 만든다 — **프리팹을 에디터에서 고친 뒤에 다시 돌리면 덮어쓴다.**
- 머티리얼 렌더 큐는 3000(투명) — 밤 어둠 막(2900) 위에 그려진다.

## 같이 바꾼 코드 · 데이터

- `Scripts/Fx/ProtoFxBuiltins.cs` — `FxIds.CharSwapAppear` · `FxIds.WeaponChangeAppear`
- `Scripts/Proto/ProtoCharacterSwitcher.cs` — `ProtoFx.HasPrefab(char_swap_appear)` 면 번개 대신 이 이펙트
- `Scripts/Proto/ProtoEvolution.cs` — `ProtoFx.HasPrefab(weapon_change_appear)` 면 진화 빛기둥 대신 이 이펙트
- `Resources/Data/FxCatalog.asset` — 항목 2개(맨 끝)

코드는 일반 연결(ID 로 부르고 프리팹이 없으면 예전 그림)이라 이 폴더를 지워도 컴파일·게임은 그대로 돈다.

## 되돌리기

1. FX 툴 카탈로그에서 `char_swap_appear` · `weapon_change_appear` 의 `prefab` 칸을 비운다 → 번개 · 진화 빛기둥으로 돌아간다(항목째 지워도 된다).
2. 그 뒤 `Prefabs/MFX_ChuanSong_Appear_Yellow.prefab` 과 "이 작업이 넣음" 파일을 지운다. 공유 파일은 다른 작업(`README.md`)이 쓰는 동안 남긴다.

## 확인한 것 / 안 한 것

- 임시 Unity 프로젝트에서 YAZS 카메라로 시각별 렌더(루키 옆, 점선 = 피해 반경), 수정한 카탈로그가 읽히고 두 항목이 이 프리팹을 가리키는 것 확인. 런타임 · 에디터 스크립트 컴파일 통과.
- 메인 에디터 플레이 화면(실제 교체 · 진화)은 아직 안 봤다.
