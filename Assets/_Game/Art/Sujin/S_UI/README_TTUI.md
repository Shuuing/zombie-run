# 탕탕특공대 전투 UI → 좀비 오버런 (참고용 원작 사본, 출시 전 반드시 삭제)

2026-11-17 사용자 "탕탕 특공대 프로젝트 ui를 가지고 와서 적용시켜줘" + "내 캐릭터 하단에 hp바도 넣어주고"
→ 범위 = **전투 화면 전부**(HUD · 발밑 체력 · 보스 경고 · 승리 띠 · 레벨업 3택 · 일시정지 · 설정 · 결과), 자리 = **지금 HUD 자리**(사용자 선택).
원본: `D:\탕탕특공대_5.3.1\UnityProject_full\ExportedProject\Assets\GameResources\UI\InGameUI`(+ `MainUI/SettingPanel`). **원작사(Habby) 저작물**이다.

**11-17 오후 3판 — 우리 그림 + 장르 배치(지금 게임에 들어간 판).** 사용자 "아이콘이나 경험치 바 같은 디자인을 수정해줘" → "탕탕 느낌 유지, 우리 그림으로" · "전부다 수정해주고, 이런 종류의 게임 HUD 레퍼 삼아서 배치도 수정해줘".
- 그림: `Sprites/` 중 46장을 `Source~/art/make_tt_art.py` 가 그린 우리 그림으로 덮었다(파일 이름 · GUID · .meta 그대로, 9-slice 테두리만 새 값). 남색 알약 막대 · 두께 + 광택 + 진한 남색(#231d36) 외곽선 버튼 · 꿰맨 좀비 머리 · '?' 상자 · 조준점 카드 무늬 · 설정 아이콘 6개. 나머지 91장(원작 낱장)은 프리팹이 안 쓴다.
- 배치(탕탕특공대 · Survivor.io 원작 배치): 맨 위 한 줄 = 일시정지(왼쪽) · 시간(가운데) · 처치(오른쪽) / 그 밑 화면 폭 경험치 + 왼쪽 밑 Lv 꼬리표 / 그 밑 가운데 보스 막대 · 버프 · 문구 / Lv 밑 진화 판 / **체력은 발밑에만**(`HeroBar` 없앰) · 연료는 발밑 체력 밑(`Follow/FeetHpBar/FuelBar`) / 오른쪽 아래 필사기 · 하차.
- 원작 그림 판(2판) 백업: 프로젝트 루트 `_Backup/20261117_TtArtOriginal`(Sprites 46장 · Game 프리팹 5개 · 예전 키트 · 미리보기 · 목록). 덮으면 되돌아간다.
- 검수 페이지(전/후 화면 9개 · 부품 46장): https://claude.ai/artifact/V47sqRQjjxKEayK7HfFL67

**11-17 저녁 4판 = 시안 C (지금 게임에 들어간 판).** 경험치 채움 찌그러짐(채움 그림 비율을 막대 비율에 맞춤: 경험치 2048×32 · 보스 1260×60 · 체력 600×60, 그라데이션 그림 압축 끔) · 보스 경고 붉은 빛(Screen_Shine 을 128 9-slice 가장자리 띠로, 경고 `Root/root/Image_BG` · `RedScreen` 을 Sliced) · 설정 슬라이더 손잡이 동그라미 + **위쪽 내 체력(HeroBar)**. 시안 A/B/C 비교 페이지: https://claude.ai/artifact/EwvZDr7WfMKk23fqyHD82A → 사용자 "C로 넣어줘".
- C 배치(지금): 화면 맨 위 가장자리에 얇은 경험치(높이 30) + 왼쪽 Lv 꼬리표 / 그 밑 줄 일시정지(왼쪽) + **바로 옆 내 체력(하트 + 막대 + "72 / 100")** · 시간(가운데) · 처치(오른쪽) / 시간 밑 보스 막대 / 왼쪽 진화 판 / 발밑 체력 + 연료 그대로. 11-17 밤: **필사기 버튼은 왼쪽 아래**(사용자 "필살기 아이콘을 왼쪽으로" — 같은 간격, 하차 버튼은 오른쪽 그대로, 직전 판 `_Backup/20261117_TtUltLeft`). 이어서 **하차 버튼은 오른쪽 아래 구석**으로 내리고(사용자 "표적 사살 아이콘이랑 하차 버튼이랑 높이 맞춰" — 둘 다 가운데가 아래에서 154 = Pad+130, 직전 판 `_Backup/20261117_TtUltLeft/UI_TTHUD_높이다름.prefab`), 버튼 글자는 "하차 (E)" 32 · 외곽선 2.5 · 버튼 앞면 가운데(사용자 "하차 버튼이 좀 지저분" → 키 배지 판을 거쳐 "E 표시는 왼쪽꺼가 더 나은것같아" — 괄호 글자로 두고 크기 · 외곽선만 줄임), 화면 밖 방향 화살표(`ProtoTankIndicator`)는 탱크(노랑) + **보스(빨강, "BOSS n m")** 두 개 — 위는 HUD 띠 밑(260)까지, 아래 두 구석(필사기 300×320 · 하차 300×230)에 걸리면 그 위 옆 가장자리로 올린다(사용자 "하차 버튼을 아래로 … 화살표는 그 위쪽 사이드랑 아래로 … 보스는 빨간색"). (처음 C 는 내 체력이 시간 밑 가운데였다 — 아래 '3판')
- 조립 스크립트 `Source~/scripts/TtZombieFit.cs.txt` 에 시안 A/B/C 가 다 들어 있다(기본 C, 명령줄 `-hudVariant A|B|C`).
- 3판(위 체력 없음) 백업: `_Backup/20261117_TtHud3`(Sprites 46 · Game 5 · 미리보기).

**11-17 저녁 레벨업 카드 2판(지금 게임 판)** — 사용자 "이미지도 해당 효과에 따라 다시 만들어줘" · "텍스트 위치랑 아이콘 이미지 크기 맞춰서 빈공간 없이 다시 사이즈랑 디자인" · "카드 12종 모두 다르게" · "그냥 레벨 표시만 … 중앙 위치":
- 아이콘 12종(우리 그림) = `Assets/_Game/Art/UI/SkillIcons/<스킨 id>.png`(탕탕 참고 폴더 밖 — 출시 뒤에도 쓴다), 만드는 법 `ArtSource/SkillIcons/make_skill_icons.py <출력 폴더> [한눈에]`(SVG → 크롬 2배, .meta 없으면 스프라이트 .meta 를 새 GUID 로). `TtSkillSelectSkin` 의 `showIcons` 1 · 항목 icon 연결.
- 카드 = 334×430: 바탕 `WeaponItemBG_Normal`/`SupplyItemBG_Normal` 을 **회색조 9-slice 판**(200×260, 테두리 34/34/34/84 — 머리 띠만, 물자국 · 별 띠 없음)으로 다시 그리고, 카드마다 색을 곱한다 → 머리 띠 = 그 색, 몸통 = 어두운 톤, 아이콘 뒤 원(`IconGlow`, 알파 0.32) = 그 색. 색은 스킨 항목 `color`(ProtoSkillSelectSkin.Entry, 알파 0 이면 프리팹 기본 — 무기 주황 · 능력치 초록).
- 배치(`TtZombieFit.LayoutCard`): 머리 띠 제목 · 아이콘 164 · 둥근 테 설명 상자(UICommon_ContentBG_Round) 안 가운데 설명 + 아래 진화 안내(금색). 안내가 없으면 코드가 설명 칸을 아래로 넓혀 가운데에 세운다(ProtoTtSkillSelectPopup).
- 제목 띠 = 레벨만("7레벨", ProtoHUD) · 글자는 띠 **앞면**(아래 두께 줄 뺀 밝은 면) 가운데 — `Title()` 글자 y +12(렌더로 잼, 일시정지 제목도 같이).
- 설정 창 설명 = 처음 '옵션' 창(ProtoHUD.BuildOptions) 문구 그대로(사용자 "처음 구성대로"), 설명 칸 넓힘 · 효과음 80% 는 슬라이더 위.
- 다시 고르기(새로고침)는 버튼을 뺀 상태(로직의 rerollsLeft 1 은 남아 있음).
- 백업 `_Backup/20261117_TtSkillCards`(직전 프리팹 3 · 카드 바탕 2 · 스킨).
- **같은 날 밤 — 글자 선명하게 · 카드 키움**(사용자 "카드 사이즈를 좀 키워죠" · "텍스트가 좀 깨지는것같아. 선명하게"):
  - 외곽선: 유니티 Outline 은 대각선 4방향만 찍어 3~4px 두께에서 위아래 · 좌우가 비어 계단처럼 깨졌다 → 8방향(두께 2.2 넘으면 반지름 절반 고리 한 겹 더) `Proto.ProtoTextOutline`(Shadow 상속, 색 · 두께 · 알파 그대로). `TtZombieFit.Txt` 가 이걸 붙이고, `Save` 가 원작 사본의 Outline 도 저장 직전에 바꾼다(프리팹 5개 전부).
  - 레벨업 · 일시정지 창의 가로 화면 1.2배: 판 Transform 을 키우면 글자가 작게 그려진 뒤 늘어나 흐려졌다 → `ProtoSaBattleUi.FitPortraitPanel` 이 CanvasScaler 기준 해상도 ÷ 1.2 로 캔버스 배율을 키운다(판 배율 1).
  - 카드 334×430 → 350×540(아이콘 200 · 뒤 원 224 · 설명 상자 216 · 글자 32), 간격 380, 제목 352 · 남은 기회 -366.
  - 백업 `_Backup/20261117_TtTextCrisp`(직전 UI_TT* 5개).
  - 설명 상자도 카드 색(사용자 "카드 설명하는 칸을 해당 카드 색상이랑 어울리게"): 공용 남색 상자(UICommon_ContentBG_Round — 진화 판 · 설정 줄 · 결과가 같이 씀) 대신 카드 전용 회색조 `Sprites/CardDescBox`(64, 9-slice 24, 새 GUID) — 코드가 카드 색을 곱해 몸통보다 한 단계 어두운 같은 색. 기본(색 없음) = 프리팹 색 남색.
  - "선택 기회가 n회 남았습니다!" 는 우리 기획 문구가 아니라 Survivor Ace 원작 팝업(UI_DankeSkillSelect `text_RemainSkillCount`)에서 온 것 — 경험치가 남아 레벨업이 바로 이어질 때 ProtoHUD 가 남은 횟수를 센다. → 사용자 "문구 빼기" 로 ProtoHUD 가 remain 을 비워 보낸다(글자는 숨김, 연속 레벨업 동작은 그대로).

**11-18 머리 위 체력 · 화면 위 체력 나누기**(사용자 "엘리트랑 중간 보스 나올때는 좀비 머리위에 hp 바 띄워주고 화면 상단에는 최종 보스일때만 상단에 HP로 보여줘"):
- 화면 위 보스 막대(`StateBarBoss`) = **최종 보스만**. 중간 보스 = 머리 위 `Follow/BossHeadBar`(빨강 + 좀비 머리, 막대 위 "중간 보스"), 엘리트 = 머리 위 `Follow/EliteHeadBar`(금색 HpSlider5 + 별, 막대 위 "진화 상자 · n초 뒤 도망" / "엘리트 · 진화 상자"). 머리 높이 = `ProtoBossMarker.HeadOf`(렌더러 바운즈).
- 코드: `ProtoSaBattleUi` 머리 막대를 `HeadBar` 두 개로(`SetBossHead` · `SetEliteHead`, Label 자식이 있으면 글), `HasHeadBars` 면 `ProtoHUD.TickSa` 가 위 규칙, 없으면(Survivor Ace 판) 예전 그대로. 조립 `TtZombieFit.MakeHeadBar`.
- 백업 `_Backup/20261118_TtHeadBars`(직전 UI_TTHUD).

**11-18 일시정지 재정렬**(사용자 "이 팝업 안에 내용 구성한 부분 재 정렬 해줘. 사이즈랑 간격이랑 이쁘게"):
- `UI_TTPause`: 판(`ActiveSkillRoot` · `PassiveSkillRoot`) = 1000 × 322(머리 띠 104 · 위 20 · 칸 한 줄 168 · 아래 30) — 예전 950 × 420 판은 두 줄 자리라 보유가 한 줄이면 아래 절반이 비었다. 칸 142 × 168 · 사이 16 · 한 줄 6칸(가운데 정렬), 아이콘 150(칸 안 좌표, 화면 99) · 별 칸 39 · 판 머리 글자 띠 가운데(-54).
- 쌓기: 제목(900) 440 · 무기 판 위 354 · 강화 판 위 2 · 버튼 줄 -434 — 사이 30, 버튼 앞 44, 보이는 위아래 ±504 로 화면 가운데. 버튼 세 개 높이 140 · ppu 1 같은 모서리, 줄 폭 = 판 폭(다시 시작 266 · 계속 420 · 설정 266, 사이 24), 설정은 톱니 + 글자 묶음 가운데.
- 빈 칸: `ProtoHUD.OpenPause` 가 이번 판에 가질 수 있는 무기 · 강화 종류 수(응급 처치 뺌, 지금 5 · 6)를 세어 `ShowPause(…, weaponSlots, statSlots)` → `ProtoSaBattleUi.Fill` 이 남는 자리를 흐린 빈 칸(`UISkill_Empty`, CanvasGroup 0.4, 아이콘 · 별 끔)으로. 한 줄(6)을 넘으면 `FitGrid` · `ShrinkItem` 이 칸을 줄여 한 줄에 넣는다. 칸 판(GridLayoutGroup)이 없는 Survivor Ace 판은 예전 그대로.
- **같은 날 2판**(사용자 "해당되는 칸만 보여주고 중앙 정렬"): 빈 칸 끔 — `ProtoHUD.OpenPause` 가 slots 를 안 넘긴다(보유한 것만, 줄 가운데). 빈 칸 기능(`ShowPause` 의 weaponSlots · statSlots)은 코드에 남아 있어 숫자만 넘기면 다시 켜진다. 보유가 0 인 판은 가운데 안내 문구(`Image_BG/EmptyText` "아직 얻은 무기가 없어요" · "아직 얻은 강화가 없어요", Fill 이 켜고 끔 — 사용자 "문구 넣어줘", 백업 `_Backup/20261118_TtPauseEmptyText`). 그림 `Source~/preview/pause_owned_only.png`.
- 전/후 그림 `Source~/preview/pause_realign_before_after.png` · `pause_realign_cases.png`(무기 2 · 강화 2 / 가득 / 넘침). 프로브 `TtProbe -states pause,pause2,pause3,pause4` 로 일부만 찍기.
- 백업 `_Backup/20261118_TtPause`(직전 UI_TTPause · ProtoSaBattleUi · ProtoHUD · 스크립트 사본).

**11-17 보스 표시**(사용자 "보스 발 밑에 빨간색 동그라미 표시 해주고 머리 위에 hp 바도 추가해줘"):
- 발밑 빨간 원 = 새 스크립트 `Assets/_Game/Scripts/Proto/ProtoBossMarker.cs`(월드 바닥 사각형 + 코드로 그린 256 텍스처 — 남색 테 · 굵은 빨간 띠 · 바깥 빛 · 안쪽을 가리키는 쐐기 4개, 숨 쉬듯 커졌다 작아지고 쐐기가 천천히 돈다). 반경 = 몸 반경 × 1.8 + 0.25(중간 보스 약 1.2m · 최종 약 1.5m). 큐 3035(밤 어둠 2900 뒤, 깊이 검사 — 보스 다리가 가린다). `ProtoSpawnDirector.Update` 가 매 프레임 `Track(Boss)`. UI 판(탕탕 · Survivor Ace)과 상관없이 나온다.
- 머리 위 체력 = HUD 프리팹 `Follow/BossHeadBar`(190×26, 채움 HpSlider1 · 잔상 HpSlider4 — 21:1 보스 막대 그림은 눌려서 안 씀, 왼쪽 좀비 머리) · `ProtoSaBattleUi.SetBossHead` · `ProtoHUD.TickSa` 가 `ProtoBossMarker.HeadOf`(렌더러 바운즈로 잰 머리 높이 + 0.2m) 점에 띄운다. 경고 띠 동안 숨김. 탕탕 판에만 있음.
- 백업: `_Backup/20261117_TtHud4C`(머리 막대 넣기 전 UI_TTHUD). 미리보기 = TtProbe 보스전 화면(캡처에 보스가 없어 자리만).
- **같은 날 2판**(사용자 "마지막 보스에는 바닥 표시 빼주고, 엘리트랑 중간 보스만 넣어줘, 그리고 엘리트랑 중간보스, 마지막 보스일때만 아웃라인 빨간 테두리 경고 이펙트만 넣어줘. 그리고 빨간테두리도 좀 약하게"):
  - 바닥 원 = 엘리트 · 중간 보스만(`ProtoBossMarker` 칸 두 개 Boss · Elite, `ProtoSpawnDirector` 가 최종 보스는 안 넘김). 머리 위 체력은 중간 · 최종 보스 그대로.
  - 화면 가장자리 빨간 테두리(경고 `Root/root/Image_BG`) = 엘리트 · 중간 · 최종 보스 **출현** 경고에만(`ProtoHUD.ShowWarning(…, redEdge: true)` → `ProtoSaBattleUi.PlayWarning` 이 Image 를 켜고 끔). 몬스터 떼 · 포위 · 엘리트 도망 경고는 띠만.
  - 테두리 약하게: Screen_Shine 가장자리 알파 0.85 → 0.5 · 흐려지는 곡선 1.8 → 2.4, 두께 `GlowSlice` 0.6 → 0.8(약 93 → 70 캔버스 단위). 낮은 체력 붉은 막도 같은 그림이라 같이 옅어짐.
  - 원작 경고 애니메이션은 Image_BG 의 원작 스크립트 값만 건드려(변환 뒤 없는 스크립트) 실제로는 테두리 색 · 켜짐을 안 바꾼다 — 코드가 켜고 끄는 게 안전하다.
- **같은 날 3판**: 낮은 체력 붉은 테두리 끔(사용자 "그것도 빼기" — `TtBattleUiSkin` · `SaBattleUiSkin` 의 `lowHpRatio` 0.3 → 0, 0 = 끔. 다시 켜려면 0.3) · 위쪽 내 체력을 일시정지 버튼 바로 오른쪽으로(사용자 "중간에 내 hp 바는 정지 버튼 옆으로" — 버튼과 세로 가운데 맞춤, 440×40), 비게 된 가운데로 보스 막대를 올림(시간 밑, 위 -142). 직전 판 백업 `_Backup/20261117_TtHud4C/UI_TTHUD_체력가운데.prefab`.

한눈에: [`Source~/preview/00_한눈에.jpg`](Source~/preview/00_한눈에.jpg) — 실제 게임 캡처 위에 가짜 값으로 찍은 9장(임시 프로젝트 렌더, `scripts/make_tt_overview.py`).

리소스 교체 목록(부품별 그림 · 글꼴 · 애니메이션 · 코드 색 · 교체 방법, 상태 칸): https://claude.ai/artifact/HWCjupxPN9uZfcb9wXytek — 원본 `Source~/tt_ui_resource_list.xlsx`(만드는 법: 임시 프로젝트 `TtResourceProbe.Run` → `Source~/scripts/make_tt_resource_xlsx.py`).

## 켜고 끄기

`Assets/_Game/Resources/Data/UiTheme.asset` 의 **Theme** 하나로 바뀐다(다음 실행부터).

| Theme | 전투 HUD · 일시정지 · 결과 · 설정 | 레벨업 3택 |
|---|---|---|
| **TangTang**(지금) | `TtBattleUiSkin` → `Game/UI_TTHUD · UI_TTPause · UI_TTResult · UI_TTSettings` | `TtSkillSelectSkin` → `Game/UI_TTSkillSelect` |
| SurvivorAce | `SaBattleUiSkin` → `_Reference/SurvivorAce/UI/Battle/UI_Zombie*` | `SkillSelectSkin` → SA 팝업 |

`UiTheme.asset` 을 지우거나 탕탕 스킨이 없으면 Survivor Ace 쪽을 쓴다. 각 스킨의 `useThisUi`/`useThisPopup` 을 끄면 예전 코드 화면.

## 폴더

| 폴더 | 내용 |
|---|---|
| `Game/` | 게임이 쓰는 프리팹 5개(아래 표). 에디터에서 손봐도 된다 — **손본 뒤 `TtZombieFit` 재실행 금지**(덮어쓴다) |
| `Original/` | 원작 프리팹 변환본 24개(원작 배치 그대로, 다시 조립할 때 재료) |
| `Sprites/` | 원작 시트에서 낱장으로 자른 PNG(원래 스프라이트 GUID) — 그중 프리팹이 쓰는 46장은 3판에서 우리 그림으로 덮음 · `Animations/` 원작 애니메이터 · 클립 |
| `Source~/` | 변환 · 조립 · 렌더 스크립트, 변환 보고서, 렌더 그림(Unity 가 읽지 않는 폴더) |

## 게임 프리팹 — 무엇을 어디서 가져왔나

| 프리팹 | 부품(이름 = 코드가 찾는 이름) | 원작 그림 |
|---|---|---|
| `UI_TTHUD` | (4판 C: `HeroBar` = 시간 밑 가운데 위 체력, `FuelBar` 는 `Follow/FeetHpBar` 아래) `XpBar/Fill` 맨 위 얇은 경험치 · `LvTag/LvText` · `HeroBar`(Image_Hp · Image_Reduce · Text_HP · Icon_Heart) · `FuelBar` · `Button_Back` 일시정지 · `EvoPanel` · `BuffText` · `TimeRoot/TimeText` · `killCountCtrl/Text` · `ScreenTop/StateBarBoss` · `UIFX_Warning` · `UIFX_ZhanDouShengLi` 승리 띠 · `ShowText/complete` · `UltButton` · `TankExit` · **`Follow/FeetHpBar` 발밑 체력** · `RedScreen` | ExpSlider(ProgressFill_*) · bossNameBg · HpSlider1/5 · Icon_Heart · UICommon_PauseBg · UICommon_ContentBG_Round · KillIcon · ProgressFill_BossHp · WarningRoot(그대로, 애니메이터 포함) · GameOverModuleCommonTitle(VICTORY 띠) · Circle · UICommon_Button_Yellow · HpSliderPlayer(검은 칸 + 빨간 채움) · Screen_Shine |
| `UI_TTPause` | `Root` · `ActiveSkillRoot`/`PassiveSkillRoot`(`skillParent` 칸 판 + 견본 `UISkill`: Icon · Name · SkillStarCtrl/Gray·Light · Level, 빈 칸은 코드가 `UISkill_Empty` 로) · `Continue`/`Back`/`Setting` 의 `ButtonLanguage` | UICommon_Title_Yellow · PauseSkillGroup(11-18: 칸 142×168 · 한 줄 6칸, 칸 안 0.66배) · 노랑/파랑 버튼 · PauseButton_BG |
| `UI_TTResult` | `SuccessBg`/`FailBg`(제목 띠 + `CharAnim`) · `Text_Wave` · `Text_Wave_Desc` · `txt_kill_count` · `UpgradeList` · `CloseBtn` | GameOverTitle1(금색)/2(파랑) · UICommon_Dialog_BG · UICommon_ContentBG_Round · 노란 버튼 |
| `UI_TTSettings` | `Image_Mask` · `Root` · `Button_Close` · `Row_fog/sfx/burn/rain/flame/dmg`(Toggle On/Off · Cycle/Value · Slider/Value) | UICommon_Dialog_BG · SettingPanel_* 아이콘 · Setting_Toggle_On/Off · 노란 버튼 · ProgressFill + HpSlider4 |
| `UI_TTSkillSelect` | `Text_Title` · `Card_1~3`(SkillLearnItem 그대로) · `Text_Remain` — **11-17 보유 칸 줄(`OwnedWeapons`/`OwnedPassives`) · `ButtonRefresh`(+`RefreshCountText`) · `ButtonSkip` 뺌**(사용자 요청, 제목 위 470 · 카드 가운데 -40 · 남은 기회 글자 -540 으로 다시 정렬, 코드는 없는 부품을 건너뜀 · 응급 처치 카드가 늘 있어 건너뛰기 없어도 안 막힘). 이어서 카드의 `NewParent`(NEW! 딱지) · `HorizontalStar`(별 5칸)도 뺌. 예전 판 `_Backup/20261117_TtSkillSelect` · **11-17 저녁 카드 2판**(아래 '레벨업 카드 2판') | SkillLearnItem |

HUD 자리 · 크기 = 4판 시안 C(위 '4판' 참고, 캔버스 1080×1920 · 가로세로 반반). 2판은 Survivor Ace 2판 `UI_ZombieHUD` 와 같은 숫자였다. 일시정지 · 결과 · 설정 · 레벨업은 원작 세로판(1080×1920, Expand).

## 코드(11-17)

- `ProtoUiTheme`(새) · `ProtoSaBattleUiSkin.Load`/`ProtoSkillSelectSkin.Load` 가 그림체를 보고 스킨을 고른다. 스킨에 `feetBarOffset`(발밑 체력, 34) · `style`(SurvivorAce/TangTang) 추가.
- `ProtoSaBattleUi`: `TimeRoot/TimeText` 한 덩어리 시간 · `killCountCtrl/Text` · **`Follow/FeetHpBar` 발밑 체력**(위 체력 칸과 같이) · 필사기 얼굴 색을 프리팹 색 기준으로 · 칸 판(GridLayoutGroup)은 6개 넘어도 안 줄임. 3판: 연료 줄 갱신을 체력 칸(`HeroBar`) 앞으로 옮겨 위 체력 칸이 없어도 연료가 돈다.
- `ProtoTtSkillSelectPopup`(새) — 탕탕 레벨업 팝업. 원작 애니메이터 대신 코드로 등장(0.6→1.08→1, 카드마다 0.09초 늦게) · 누름 · 새 별 깜빡임. `IProtoSkillPopup` 로 Survivor Ace 팝업과 같은 자리에 끼운다(ProtoHUD).

## 다시 만들기 (`Source~/scripts`)

1. `guidmap.py` → `tt_guids.json`(추출본 .meta 전부, 스크립트 GUID → 클래스 이름).
2. `python -P tt_build_ui.py <출력 Assets/TangTang> <임시 프로젝트 Assets> $(cat prefab_list.txt)` — 원작 스크립트 바꿈: DxxImage→Image · DxxText/DxxTextLanguage→Text · OutLineDxx/OutlineEx→Outline · ButtonCtrl→Button · NonDrawingGraphic→투명 Image, 나머지 원작 스크립트 · 파티클 갈래는 뺀다. 원작 글꼴(알리바바)은 우리 것(SurvivorAce/UI/SkillSelect/Font)으로 바꿔 끼운다. `sprite:<이름>` 은 프리팹에 없는 그림만 따로 자르기.
3. 임시 프로젝트(Survivor Ace 때 쓴 ui2)에서 `-executeMethod TtProbe.Run -fit` = `TtZombieFit.Run`(조립) + 렌더 · 클릭 검사. 원작 그림 그대로 보기 = `TtRawProbe.Run`.
4. `tt_tree.py <프리팹>` = 원작 계층 보기, `spr_preview.py` = 그림 후보 한 장에.
5. 우리 그림(3판): `python Source~/art/make_tt_art.py <Sprites 폴더> [미리보기 png]` — SVG 로 46장을 그려 헤드리스 Chrome 으로 2배 렌더 → 줄여 `<이름>.png` 를 덮고 기존 `.meta` 의 `spriteBorder` 만 고친다. 임시 프로젝트 Sprites 에 먼저 돌려 3번으로 렌더 확인 → 메인에 복사. `TtZombieFit.SetBorders` 의 테두리 값도 그림과 같게 맞춰 두었다(ProgressFill_Background 42,36,42,36 · ContentBG_Round 26 · bossNameBg 26,20,40,20).
6. 미리보기 · 키트: `make_tt_overview.py <TTPreview/game> preview/00_한눈에.jpg` → 임시 프로젝트 `TtKitProbe.Run` → `make_tt_kit.py`(키트 게임화면은 UI만 투명 층을 `ArtSource/UI_ConceptKit/01_화면_실제게임/02_UI없는_게임화면.png` 위에 합성).

## 함정

- 탕탕 추출본은 스크립트 본문이 비어 있지만 **UI 컴포넌트 값(스프라이트 · 글자 · 색)은 프리팹에 다 남아 있다**(UnityProject_full 판). 스크립트 GUID → 클래스 = `Scripts/<어셈블리>/<네임스페이스>/<클래스>.cs` 경로.
- 원작 애니메이터는 코드가 `Play` 로 부르던 상태가 따로 있다 — 경고 띠(WarningAnimator)는 기본 상태가 숨김(Enter), 결과 제목 띠(GameOverModuleAnimator_popUp)는 알파 0(Default). 사본은 기본 상태를 `Warning` · `Show` 로 바꿔 켜지면 바로 돈다.
- 원작 막대 그림(HpSlider · ProgressFill_*)은 오른쪽 끝이 꺾여 있어 **폭을 줄이면 끝이 덩어리로 남는다** → 통으로 늘여 두고 Filled(왼쪽부터)로 자른다. 경험치 바탕(ProgressFill_Background) · 둥근 판은 사본 .meta 에 9-slice 테두리를 넣었다.
- 원작 노란 버튼(UICommon_Button_Yellow)은 9-slice 테두리가 커서 작은 버튼은 `pixelsPerUnitMultiplier` 를 키워 얇게(1.4~1.8). 3판 막대 바탕(높이 80 그림, 둥근 끝)은 `80 / 막대 높이` 로 맞춘다(`TtZombieFit.BarPpu`).
- 키트 `TtKitProbe` 의 `_game.png` 는 게임 캡처 배경이 안 실려 검은 판으로 찍혔다(2판 키트도 그랬음) → `make_tt_kit.py` 가 직접 합성.
- **Filled 막대 채움은 그림을 통으로 늘인다 → 그림 비율 = 화면에 보이는 막대 비율**이어야 무늬가 안 눌린다(3판 경험치 줄무늬가 3.6배 납작했던 원인). 막대 크기를 바꾸면 `make_tt_art.py` 의 채움 그림 크기도 같이. 화면 가장자리 빛은 늘이지 말고 9-slice 띠(`TtZombieFit.GlowSlice`).
- 일시정지 스킬 칸 별은 원작이 칸마다(빈 별 + 노란 별) — 코드는 Gray/Light 두 줄을 켜고 끄므로 두 줄로 나눠 왼쪽 정렬로 겹쳤다(가운데 정렬이면 켜진 수가 달라 어긋난다).
- 레벨업 스킨 아이콘은 Survivor Ace 스킨 항목을 그대로 옮겼다(`showIcons` 도 SA 스킨처럼 꺼져 있음 — 켜면 SA 아이콘이 탕탕 카드에 들어간다).
- 검증: 임시 프로젝트 렌더 · 클릭 검사(모든 버튼이 받음) · 메인 rsp 컴파일. **메인 에디터 · 플레이 미확인.**
