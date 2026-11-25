# 탕탕특공대 추출 이펙트 — 임시 (출시 전 반드시 교체·삭제)

> **2026-11-23 — 이펙트는 모두 `Assets/_Game/Art/Sujin/S_VFX` 로 옮겼다**(프리팹은 종류별 폴더, 머티리얼 · 텍스처 · 메시 · 스프라이트는 `S_VFX/Shared/TangTang/`, GUID 그대로). 사용자가 나중에 `_Reference` 를 지울 예정. **여기 남은 것: `UI/`(게임 HUD · 일시정지 · 결과 · 레벨업 창이 지금 쓴다 — 지우면 UI 가 깨진다) · 문서 · `Source~`.** 아래 본문의 `Prefabs/` · `Materials/` 경로는 옮기기 전 기록이다. 저작물 교체 의무는 S_VFX 의 복사본에도 그대로 — `Sujin/README.md`.

2026-11-12 "좀비런에 어울리는 이펙트 가져와줘" 요청으로 탕탕특공대 5.3.1 추출본
(`D:\탕탕특공대_5.3.1\UnityProject_full\ExportedProject`)에서 골라 복사했다(지금 프리팹 14개 — 물고기 젬 반짝이 · 꼬리 2개(11-18) 포함, 독 구슬은 보라 물방울 → 초록 점액 구슬 → 초록 혼불탄 → 포이즌볼 → 다시 초록 혼불탄. 포이즌볼은 예비로 남겨 둠).
**Habby 저작물이다.** 프로토에서 느낌을 보는 용도로만 쓰고, 빌드·외부 공개 전에 우리 이펙트로 바꾼 뒤 이 폴더를 지운다.

> **2026-11-20 정리** — 프리팹은 용도별 하위 폴더로 옮겼다: `Prefabs/Boss/`(보스 등장 · 준비 먼지 · 망치 궤적 · 충돌 · 예고 원) · `Prefabs/Enemy/`(독 구슬 · 터짐 · 독가스 · 중독 · 할퀴기 · 피격 피) · `Prefabs/Pickup/`(물고기 젬 반짝이 · 꼬리) · `Prefabs/_Unused/`(포이즌볼). GUID 는 그대로라 카탈로그 참조는 안 끊겼다.
> 셰이더 `Effects/Particle` · `UVFlow` · `Turbulence` · `Custom/SheetBase_tranparent` 는 **지웠다** — 머티리얼 44개 전부 게임 파티클 셰이더 `ZombieOverrun/FX/Particle`(`_Game/Art/VFX/Shaders/FX_Particle.shader`)로 옮겼다(`FxShaderUnify.cs`, 전후 렌더 비교로 같은 그림 확인). 옛 셰이더 파일은 `_Backup/20261120_FxShaderUnify` 에. 아래 본문의 셰이더 이야기는 그 전 기록이다.
카탈로그(`Resources/Data/FxCatalog.asset`)가 참조하는 동안에는 빌드에 같이 들어간다.

## 들어 있는 것

| 프리팹 | 원본 이름 뜻 | 카탈로그 ID | 상태 |
|---|---|---|---|
| `FX_ZombieKing_WarStomp` | 좀비왕 발구르기 | `boss_impact` | **게임에 나옴** — 최종 보스 내려찍기·도약 충돌 |
| `Fx_Matrix_Smoke` | 먼지 구름 | `boss_windup_dust` | **게임에 나옴** — 최종 보스 준비 발밑 먼지 |
| `FX_JiangShiMa_Trail` | 강시마 불 꼬리 | `boss_hammer_trail` | **게임에 나옴** — 최종 보스 망치 휘두르기 궤적 |
| `FX_Halo_FuShi` | 부식 장판(초록) | `poison_gas_cloud` (새 ID, 스팬) | **게임에 나옴** — 러너 독가스 (11-12 코드 연결) |
| `PosionBall` | 포이즌볼(쥐왕 독 방울 탄, 원본 철자 Posion) — 빨간 테두리를 뺀 그림 | (예비 — 카탈로그에서 안 씀) | 11-12 잠깐 독 구슬로 썼다가 혼불탄으로 되돌림. `poison_bolt` 프리팹 칸에 끌어다 놓으면 다시 쓴다 |
| `Bullet_HUNHUO_Green` | 혼불탄(해골 불덩이 + 혜성 꼬리) — **초록으로 색 바꾼 복사본** | `poison_bolt` (새 ID, 스팬) | **게임에 나옴** — 원거리 좀비 독 구슬 |
| `Bullet_Boss_KongJuZhiChao03_ShuiQiu_Hit` | 점액 구슬 터짐(초록) | `poison_bolt_hit` (새 ID) | **게임에 나옴** — 독 구슬이 맞거나 막힐 때 |
| `Fx_Drug_Circle` | 초록 섬광 · 점액 방울 · 연기 터짐 | `player_poison_hit` (새 ID) | **게임에 나옴** — 플레이어가 혼불탄이나 러너 독가스에 맞을 때 가슴께(따라다님) |
| `Bullet_BeastGirlActive_WolfClawAttack` | 늑대 발톱 공격(검붉은 발톱 자국 플립북) | `zombie_claw_hit` (새 ID, 항상 위에) | **게임에 나옴** — 평타 좀비(워커 · 러너 · 엘리트 · 중간 보스)에게 맞을 때 가슴께 |
| `WarningIndicator_Circle_Out_OneShot` | 붉은 예고 원 | `boss_telegraph` (새 ID, 스팬) | **게임에 나옴** — 최종 보스 바닥 예고 원 위에 얹음 |
| `Fx_Boss_Spawn` | 보스 등장 해골 마크 | `boss_spawn` (새 ID) | **게임에 나옴** — 중간·최종 보스 등장 |
| `HitBlood` | 피 튀김 | `hit_blood` (새 ID) | **게임에 나옴** — 모든 적 피격(큰 한 방 제외) |
| `FX_FishSparkle` | 드롭 아이템 연출 `FX_DiaoLuo_Color` 의 **별 반짝임만** 떼어 낸 것(깜빡이며 살짝 솟는 별, 11-18 오후 작게 · 노랑~금) | `gem_sparkle` (새 ID, 스팬) | **게임에 나옴**(11-18) — 좀비가 떨군 물고기 젬 둘레, 자석에 걸리기 전까지 |
| `FX_FishTrail` | 얼음 불사조 화살 `Bullet_WhistleArrowIcePhoenix` 의 **꼬리 띠 + 물방울 점만** 떼어 낸 것(11-18 오후 노란 텍스처 복사본으로 — 금색 물고기에 맞춤, 띠 머리 단면 없앰) | `gem_trail` (새 ID, 스팬) | **게임에 나옴**(11-18) — 물고기 젬이 캐릭터로 빨려 오는 동안 |

망치 불(`boss_hammer_flames`)은 맞는 후보가 없어(탕탕 불꽃은 바닥 불 자국용) 코드 그림 그대로다.

## 원본에서 바꾼 것

- 탕탕은 카메라가 바로 위에서 보는 XZ 바닥 게임이라 회전은 그대로 뒀다.
- 셰이더는 추출본에서 근사 복구한 것(`Effects/Particle` 등, 원본 코드 아님). 라이팅 태그가 없어 URP 가 그대로 그린다.
- `FX_ZombieKing_WarStomp`: 모든 파티클 반복(looping) 끔 — 원샷으로 한 번 터지고 끝나게.
- `Fx_Matrix_Smoke`: 루트 배율 2.5 (보스 발밑 크기).
- `FX_JiangShiMa_Trail`: 루트 배율 0.5, 발자국 불(`JiaoYin`) 끔.
- `FX_Halo_FuShi` · `WarningIndicator_Circle_Out_OneShot` · 혼불탄 · 점액 튐: 탕탕 스크립트 컴포넌트(빈 껍데기)를 뗐다 — Missing Script 경고 방지.
- 포이즌볼(`PosionBall`, 11-12 사용자 "포이즌볼로 다시 교체" · "빨간 테두리는 빼줘"): 파티클이 아니라 그림 한 장(2m 사각판 `Quad_2x2`, 바닥에 눕힘) + 꼬리 TrailRenderer 라 이펙트 목록(_effects_catalog.csv)에 없었다. 원본 루트에 위치 오프셋(-1.253)이 남아 있어 빈 루트(`PosionBall`)를 씌우고 원래 루트는 `Body` 로 이름을 바꿔 0 자리에 뒀다. 배율 0.33(방울 지름 약 0.4m), 꼬리 0.35초·0.35m → 0.25초(약 1.6m)·0.18m. 그림 위쪽(+Z)이 앞이라 회전은 없다(늘어진 방울 끝이 뒤). 그림은 새 GUID 복사본 `Bullet_LaoShuWang_B_NoOutline.png` — 바깥 빨간 테두리(약 7px)를 지우고 빨강·초록이 섞인 경계는 짙은 초록으로 바꿔 안쪽 짙은 초록 선이 새 테두리가 됐다(원래 그림은 지움). 파티클이 없어 맞거나 사라지면 바로 꺼진다(맞을 때 점액 튐은 `poison_bolt_hit`).
- 혼불탄(`Bullet_HUNHUO_Green`, 11-12 사용자 "꼬리 달린 마법 느낌" → "혼불탄을 초록으로"): 원본 `Bullet_HUNHUO` 는 가져오지 않고 새 GUID 복사본만 뒀다. 해골 텍스처 `FX_Bullet_HunHuo_Green.png`(색상 260° → 105°, 채도·명도 그대로)와 그 머티리얼 `FX_Bullet_HunHuo_Green.mat` 도 새 GUID. 파티클·꼬리 그라데이션의 파랑·남색 34개 값도 같은 식으로 초록. 루트 0.45, 꼬리(TrailRenderer 2개) 0.6/0.75초 → **0.3초**(초속 6.5m 에서 약 2m), 굵기 2.5/2.0 → **1.125/0.9m**(TrailRenderer 굵기는 트랜스폼 배율을 안 탄다). 바꾼 스크립트는 스크래치 `recolor_hunhuo.py`. 그 전의 초록 점액 구슬(`Bullet_Boss_KongJuZhiChao03_ShuiQiu`)과 그것만 쓰던 파일 12개는 지웠다(맞을 때 튐 `_Hit` 은 남김).
- 혼불탄 2차 손질(11-12 사용자 "반으로 더 줄여" · "안 맞으면 딱지처럼 남는다 — 화면 밖으로 지나가며 사라지게"): 루트 0.45 → **0.225**(해골 지름 약 0.18m), 꼬리 굵기 1.125/0.9 → **0.5625/0.45m**(길이 0.3초 그대로). "딱지"는 해골 입자 수명(1.5초) 때문 — 구슬이 끝날 때 방출만 멈춰(Stop) 남은 해골이 그 자리에 떠 있었다. 이제 `ProtoEnemyBolt` 는 끝낼 때 `FxHandle.Kill()`(바로 거둠)을 쓰고, 한 번 화면에 보인 구슬은 화면 밖으로 나갈 때까지 날아가(최대 6초) 나간 뒤 꼬리가 빠질 0.35초를 더 날고 거둔다. 맞거나 막히면 바로 거두고 점액 튐만 남는다.
- 혼불탄 3차 손질(11-12 사용자 "해골이 줄었으니 꼬리도 사이즈에 맞게"): 꼬리 TrailRenderer 2개 0.3초 → **0.15초**(초속 6.5m 에서 약 1m), 월드 공간 연기 꼬리 `Smoke_W` 수명 0.15~0.3초 → **0.075~0.15초**. 굵기는 2차의 0.5625/0.45m 그대로.
- `Fx_Drug_Circle`(11-12 사용자 "몸에서 초록색 피격 이펙트"): 2초마다 다시 터지던 반복(8개 시스템)을 끄고 한 번만 — 0.75초면 다 사라진다. 루트 0.7.
- 늑대 발톱(`Bullet_BeastGirlActive_WolfClawAttack`, 11-12 사용자 "손톱으로 할퀴는 이펙트를 평타 좀비에"): 발톱 자국 3×3 플립북(Sheet) 0.3초 + 고리(Quan) + 어두운 번짐(Dark). 빈 루트 시스템 둘이 5초 반복이라 반복을 끄고 카탈로그 수명 0.5초. 루트 0.8. 가슴(몸 안)에 나와 캐릭터 몸에 가려지므로 카탈로그 `onTop`(오버레이 카메라)으로 그린다. 다른 후보: 탕탕 고양이 발톱 `Bullet_MaoZhua`(주황 세 줄, 움직임 없는 그림 한 장) · Survivor Ace `weapon_zhua`.
- 독 구슬 첫 후보 `DrugBall_Trail_Slow`(보라 물방울)는 11-12 사용자 "너무 크고 꼬리가 너무 길다"로 뺐다 — 트레일 2개가 1.4초·굵기 1.5m 라 초속 6.5m 로 날면 9m 짜리 띠가 됐다(정지 렌더에선 안 보였다). 그것만 쓰던 파일 11개도 지웠다.
- 머티리얼 8개의 렌더 큐 2000~2860 → 2950~2993 (순서 유지). 밤 어둠 막(큐 2900)에 묻히지 않게.
- 물고기 젬 반짝이 · 꼬리(11-18 사용자 "떨어지는 물고기 주변에 반짝이, 흡수될 때 꼬리 — 탕탕에서 어울리는 이펙트"): 두 프리팹 모두 **새로 만든 것**(새 GUID, 원본 프리팹은 안 가져옴). 머티리얼 3 · 텍스처 3 은 원본 GUID 그대로 새로 들어왔다(`Star_006_1` · `Star_006` / `HT_ShaoJian_Bing_FengHuang_Trail` · `_Dian`).
  - 고른 이유: 탕탕 아이템 반짝이 후보(`ItemEffect` · `FX_DiaoLuo_Color/Blue`)와 꼬리 후보 6종(얼음 화살 · 얼음 결정탄 · 암야정령 · 펭귄 · 섬광탄 · 콩알탄)을 게임 카메라(각도 3)로 찍어 봤다. 드롭 연출은 빛기둥 · 바닥 빛까지 붙어 물고기 수십 마리에 쓰기엔 과하고 별만 맞았다. 꼬리는 분홍 · 주황 · 얼음 구슬이 물고기와 안 맞고, 푸른 물줄기 + 물방울이 "헤엄쳐 온다"에 맞았다.
  - `FX_FishSparkle`: 원본 `Node`(배율 2) 아래 `Star` 하나만 — 빛기둥 · 기둥 빛 · 바닥 빛 2 · 줄 · 나선은 버렸다. 루트 0.25, 크기 ×2(0.5~0.7 → 1~1.4), 솟는 속도 ×0.35(1.5~5 → 0.525~1.75), 원뿔 반지름 ×0.7(0.5 → 0.35), 초당 3 → 4, 최대 10 → 6. 깜빡이는 크기 곡선 · 파스텔 무지개 색 · 회전은 원본 그대로. 로컬 공간이라 물고기를 따라 움직인다.
  - `FX_FishTrail`: 화살 몸체 메시(MR) · 빈 컨테이너 파티클을 버리고 `Trail` · `LiZi` 만 빈 루트 아래로. 꼬리 1.2초 → **0.3초**(자석 5m/s 에서 약 1.5m), 굵기 곡선 1→0.85→0.15, 최소 점 간격 0.05. 굵기는 코드가 넣는다(물고기 키 × 2.4 — 꼬리 그림의 빛줄기가 가운데 좁은 띠라). 물방울 점 `LiZi` 배율 1.5 → 0.25, 거리당 0.5 → **7**개(지나온 길에 점점이 남는다), 최대 30. 꼬리 머티리얼은 **가산**(DstBlend 10 → 1)으로 바꾸고 큐 2000 → 2960 — 반투명이면 어두운 바닥에서 검푸르게 묻혔다.
  - 노란 계열로(11-18 오후 사용자 "반짝이 사이즈 좀 줄이고, 꼬리는 물고기 색이랑 어울리게 노란색, 반짝이도 노란색 계열로" — 젬이 금색 물고기가 된 뒤):
    - 꼬리 · 물방울 텍스처는 파랑(색상 약 210°)이라 색을 곱해서는 노랗게 안 된다 → 색상만 **47°**(금빛 노랑)로 돌리고 채도 ×0.9 · 밝기 그대로인 **새 GUID 복사본** `HT_ShaoJian_Bing_FengHuang_Trail_Gold.png` · `_Dian_Gold.png` 와 그 머티리얼 `…_Trail_Gold.mat`(가산, 큐 2960) · `…_Dian_Gold.mat`(반투명 → **가산** — 주황 끝색이 회색 바닥에서 갈색으로 탁했다)을 만들어 `FX_FishTrail` 이 쓰게 했다. 물방울 색 변화 흰 → 하늘 → 파랑을 연노랑 → 노랑 → 금 → 주황으로.
    - 파란 원본 머티리얼 2 · 텍스처 2 는 아무도 안 써서 `_Backup/20261118_FishFxYellow/` 로 옮겼다(프리팹 두 개의 바꾸기 전 판도 거기).
    - `FX_FishSparkle`: 별 크기 ×**0.6**(1~1.4 → 0.6~0.84), 시작 색 보라·하늘 두 색 → 흰색(알파 0.9), 색 변화 두 갈래(금 → 분홍 → 보라 → 파랑)를 노랑 → 금 → 주황 두 갈래로.
      별 머티리얼 `Star_006_1` 이 `_TintColor` 2 로 색을 두 배 밝히는 가산이라 1 에 가까운 노랑은 흰색으로 날아갔다 — 색 값을 절반쯤(R 0.6 · G 0.3~0.54 · B 0.05~0.26)으로 적어 두 배 하면 진한 노랑 · 금이 된다.
    - 바꾼 스크립트는 이 세션 임시 프로젝트 `fp` 의 `FishFxRecolor.cs`(텍스처 색 돌리기는 파이썬).
  - 띠 머리의 잘린 단면 없앰(11-18 오후 사용자 스크린샷 두 장 — "꼬리 이펙트에 이미지가 잘린 글로우 텍스처는 빼도" → 띠를 통째로 뺐더니 "요 단면 잘린 텍스처만 지우고 물고기 꼬리에서 나오는 텍스처는 지우지 말아줘").
    원인: 굵기 곡선이 머리(물고기 쪽, 길이 0)에서 가장 넓어(1) 물고기 자리에서 키 × 2.4 높이의 빛무리가 칼로 자른 듯 시작했다.
    고친 것: 띠는 그대로 두고 굵기 곡선을 **머리 0.05 → 길이 0.45 에서 0.75(가장 넓은 곳) → 0.8 에서 0.41 → 끝 0.08** 로, 알파를 **머리 0 → 0.35 에서 1 → 0.75 에서 1 → 끝 0** 으로. 빛줄기가 물고기 몸 안에서 가늘게 나와 뒤에서 퍼진다.
    1차 시도(가장 넓은 곳 0.3 · 폭 1)는 짧은 꼬리(0.15초)일 때 물고기 뒤 0.2m 안에서 폭이 확 벌어져 직선 모서리가 남아 더 뒤 · 덜 넓게 했다.
    물방울은 그 사이 바꾼 그대로(거리당 7 → 10, 크기 ×1.15, 최대 40). 띠를 뺐던 판은 `_Backup/20261118_FishTrailNoGlow/FX_FishTrail_DotsOnly.prefab`. 스크립트 `fp` 의 `FishTrailNoGlow.cs`(FT_KEEP=1).
  - 확인: 임시 프로젝트에서 물고기 그림 3장 + 게임 카메라로 정지 · 이동 렌더(이 세션 스크래치 `fp`, 빌더 `FishFxBuild.cs` · 프로브 `FishProbe.cs`). 메인 에디터 플레이 화면은 미확인.
- GUID 는 원본 그대로. 폴더 구조만 Prefabs / Materials / Textures / Sprites / Meshes / Shaders 로 폈다(`Resources` 폴더 이름은 빌드에 통째로 들어가서 뺐다).

## 같이 바꾼 우리 쪽 파일

- `Assets/_Game/Resources/Data/FxCatalog.asset` — 위 표대로 보스 3개 항목의 `prefab` 칸, 수명(먼지 2초 · 충돌 3초), 새 항목 6개.
- `Assets/_Game/Resources/Data/ActionFxSheet.asset` — 보스 충돌 이벤트. 코드 그림 충돌은 흔들림·소리를 스스로 냈지만 프리팹은 못 내므로 시트로 옮겼다.
  - `Boss_Final/Slam` 충돌: scale 1 → **2.5**, sfx → `explosion`, shake → Heavy
  - `Boss_Final/JumpSlam` 충돌: scale 1 → **3.4**, sfx → `explosion`, shake → Huge
  - 크기 근거: 발구르기 프리팹이 배율 1 에서 눈에 보이는 반경 약 1.3m. 보스 1.3배 × 2.5 ≈ 반경 2.1m(판정 3.4m 의 60%). 판정 범위는 예고 원이 따로 보여준다.
- 바꾸기 전 원본: `D:\Fork\Newgame\ZombieRun\_Backup\20261112_TangTangFx\` (두 파일).

## 코드 연결 (2026-11-12) — 코드가 넘기는 scale 과 프리팹 루트 배율

코드는 뜻이 있는 값만 넘기고(판정 반경 · 덩치), 그 값에 맞는 크기는 **프리팹 루트 배율**이 맞춘다. 배율 1 크기는 바로 위 직교 카메라로 잰 값.
다른 프리팹으로 바꿀 때도 이 규약(scale 의 뜻)에 맞춰 루트 배율만 고치면 코드는 그대로다.

| ID | 부르는 코드 | 코드가 넘기는 scale | 원본 배율 1 크기 | 루트 배율 |
|---|---|---|---|---|
| `poison_gas_cloud` | `ProtoPoisonGas.Begin` (스팬, 구름이 사라질 때 Stop) | 판정 반경(m) — 러너 1.8 | 반경 0.5m | 2.0 |
| `poison_bolt` | `ProtoEnemyBolt.Launch` (스팬, 구슬을 따라가며 나는 쪽을 본다 · 맞으면/막히면/화면 밖으로 나가면 Kill) | 1 = 원래 코드 구슬(0.55m) 자리 | 혼불탄 해골 지름 약 0.8m | 0.225 (+ 꼬리 0.15초 · 0.5625/0.45m, 연기 수명 반) |
| `poison_bolt_hit` | `ProtoEnemyBolt.Pop` (원샷 0.8초, 프리팹 구슬일 때만) | 1 = 독 구슬 기준 | 튐 지름 약 1m | 0.275 |
| `player_poison_hit` | `ProtoPlayer.TakeDamage(…, Poison)` (원샷, 플레이어 가슴 로컬 위 1.3m 에 붙어 따라감) | 1 | 섬광 지름 약 2.5m | 0.7 |
| `zombie_claw_hit` | `ProtoPlayer.TakeDamage(…, Claw)` ← `ProtoZombie` 평타 접촉 공격(개 물기는 Normal) (원샷 0.5초, 가슴 로컬 위 1.3m 따라감, onTop) | 1 | 발톱 자국 약 1.8m | 0.8 |
| `boss_telegraph` | `ProtoBossFx.ShowTelegraph` (스팬, 예고 원이 사라질 때 Stop) | 판정 반경(m) — 내려찍기 3.38 · 도약 4.68 | 반경 1.52m | 0.66 |
| `boss_spawn` | `ProtoSpawnDirector.SpawnBoss` (원샷 2초) | 보스 덩치(테이블 scale) — 중간 1 · 최종 1.3 | 반경 0.8m | 1.0 |
| `hit_blood` | `ProtoZombie.DealDamage` (원샷 1.2초, 동시 20) | 덩치(치명타 ×1.5) | 흩어짐 반경 1~1.4m | 0.3 |
| `gem_sparkle` | `ProtoGemPool` — 떨어질 때 켜고(스팬) 매 프레임 `FxHandle.MoveTo`, 자석에 걸리면 `Stop`(남은 별은 1초 안에 사그라듦), 재시작은 `Kill` (동시 64) | 젬 등급 0.8 · 1 · 1.4 | 별이 물고기 둘레 약 0.4m · 위로 0.5m | 0.25 |
| `gem_trail` | `ProtoGemPool` — 자석에 걸릴 때 켜고(스팬, 꼬리 `Clear` · 굵기 = 물고기 키 × `trailWidthPerFishHeight` 1.6 · 꼬리 시간은 코드 `trailTime` 0.07초가 덮어씀, 11-19), 따라 옮기다 먹히면 방출 끄고 `Stop(detach)` (동시 48) | 젬 등급(물방울 점만 탄다) | 꼬리 길이 = 0.07초 × 속도(8→최대 24m/s 가속) | 1 |

- 프리팹이 있으면 숨기는 코드 그림: 독가스 퍼프 7개(바닥 원 = 판정, 섬광은 남김), 독 구슬 코어·꼬리(손에 모이는 시전 구슬은 초록 코드 그림 그대로).
- 보스 예고 원은 코드 테두리(판정)·차오르는 채움(충돌 시각)을 그대로 두고 프리팹을 위에 얹는다. 탕탕 원은 1초쯤 뒤 사라져서 도약(1.78초) 예고 후반엔 코드 원만 남는다.
- 다섯 ID 모두 코드 그림 폴백이 없다 — 카탈로그 `prefab` 칸을 비우면 원래 화면(코드 그림만)으로 돌아간다.
- 독 구슬 프리팹 안의 TrailRenderer 는 풀에서 다시 나올 때 지난 자리를 기억해 줄을 긋는다 — `ProtoEnemyBolt` 가 켤 때 비우고, 끌 때 방출을 끄고 따라가기를 끊는다(`FxHandle.Stop(detach: true)`). 꼬리 달린 프리팹을 다른 투사체에 쓸 때도 같은 처리가 필요하다.

## 같이 바꾼 코드 (11-12)

`Fx/ProtoFx.cs`(`HasPrefab`) · `Fx/ProtoFxBuiltins.cs`(`FxIds` 6개) · `FxHandle.Stop(detach)` · `Proto/ProtoPoisonGas.cs` · `Proto/ProtoEnemyBolt.cs` · `Proto/ProtoBossFx.cs` · `Proto/ProtoSpawnDirector.cs` · `Proto/ProtoZombie.cs`.
코드는 탕탕과 무관한 일반 연결(ID 로 부르고, 프리팹이 없으면 아무것도 안 하거나 코드 그림)이라 이 폴더를 지워도 그대로 둬도 된다.

## 되돌리기

- 하나만 원래 코드 그림으로: FX 툴 카탈로그에서 그 항목의 `prefab` 칸을 비운다(`builtin` 은 남아 있다).
  `boss_impact` 를 되돌릴 때는 시트 충돌 이벤트의 sfx·shake 도 비울 것 — 안 그러면 코드 그림의 흔들림·소리와 겹친다.
- 전부: `_Backup\20261112_TangTangFx\` 의 두 파일을 원래 자리에 덮어쓰고 이 폴더(`Assets/_Reference/TangTang`)를 지운다.
- 물고기 반짝이 · 꼬리만(11-18): 카탈로그 `gem_sparkle` · `gem_trail` 의 `prefab` 칸을 비우거나, 씬의 `ProtoGemPool` 에서 `sparkleOnGround` · `trailWhileMagnet` 을 끈다. 바꾸기 전 파일은 `_Backup\20261118_FishFx\`(카탈로그 · ProtoGemPool · ProtoFx · ProtoFxBuiltins · FxAssetsSetup · 이 README).

## 전투 UI (11-17)

탕탕특공대 전투 화면 UI 사본은 [`UI/README.md`](UI/README.md) — 전투 HUD · 발밑 체력 · 경고/승리 띠 · 레벨업 3택 · 일시정지 · 설정 · 결과. `Resources/Data/UiTheme.asset` 의 Theme 로 Survivor Ace 판과 바꾼다. 출시 전 이 폴더째 삭제.
