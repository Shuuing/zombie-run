# FX 툴 — 연출·이펙트 작업 구조 (2026-10-29 ~ 10-30)

한 줄로: **코드는 "때와 자리", 카탈로그는 "모양", 무기 연출 세트는 "어느 무기가 무엇을 얼마나 크게"**.
효과를 바꿀 때 코드를 열지 않는다 — 카탈로그 항목에 프리팹을 꽂고, 무기 연출 에셋에서 ID·배율을 고친다.

메뉴는 전부 **Tools ▸ 좀비 오버런 ▸ FX 툴** 아래. 에셋이 없으면 에디터가 켜질 때 스스로 만든다(`FxAssetsSetup.AutoCreate`).

```
                      ┌─ WeaponFxSet (WFX_*.asset) ─ 무기별 ID·배율 ─┐
                      │                                             │ HitFx
 게임 코드 ──ProtoFx.Play(id, ctx)──▶ FxCatalog ──▶ 프리팹  또는  코드 그림(builtin) ──▶ 화면
      │                                 (id → 모양)
      └─ ActionFxSheet ──ActionFxPlayer──┘
         (동작 → 시각별 이벤트)   시계는 부르는 쪽이 쥔다
```

## 1. 효과 4층 — 예산과 우선순위

| 층 | 예 | 비용 |
|---|---|---|
| ① 정보 | 피격 플래시·넉백·판정 링·예고 원 | 90마리 물량에서도 싸야 한다 |
| ② 타격감 | 총구·착탄·처치·흔들림·히트스톱 | 위와 같음 (동시 개수 상한 있음) |
| ③ 세리머니 | 레벨업·진화·필사기·보스 등장·클리어 | 한 번에 하나 — 비싸도 된다 |
| ④ 분위기 | 비·안개·밤 | 씬 단위 |

## 2. 파일 지도

### 런타임 — `Scripts/Fx/`

| 파일 | 역할 |
|---|---|
| `ProtoFx.cs` | 진입점. `FxContext`(자리·기준·반경·big·quiet·색) · `FxHandle` · `ProtoFx.Play / PlayBlast / Stop / Hitstop` · `ProtoFxRunner`(틱·히트스톱·`BaseTimeScale`) |
| `ProtoFxBuiltins.cs` | 코드 그림 폴백 등록 + ID 상수 `FxIds`. 카탈로그에 프리팹이 없으면 여기 등록된 예전 코드 그림이 그린다 |
| `FxCatalog.cs` | ID → 프리팹 또는 builtin. 항목마다 수명·풀 크기·동시 상한·시계(게임/실시간)·쇼케이스 오프셋·**항상 위에**(`onTop`, 11-05). `upgrade` = 한 번만 적용한 기본값 손질 단계 |
| ~~`../Proto/ProtoFlameSheetFx.cs`~~ (11-05, **11-23 지움**) | 시트 화염(화염방사기 30칸 플립북 쿼드) — 화염방사기가 인페르노 불줄기를 쓰게 된 뒤 아무 데서도 안 써서 지웠다. 파일 · 시트 에셋은 `_Backup/20261123_SujinFolder` |
| `FxOverlay.cs` (11-05) | **항상 위에** — 총구 화염·피격 불똥이 총·적 몸에 가려지지 않게(사용자 "발사 이펙트가 총보다 밑에"). 전용 레이어 `FxOverlay` 를 메인 카메라의 URP 카메라 스택에 쌓은 오버레이 카메라(깊이 지움, 그 레이어만)가 따로 그린다 — 셰이더를 안 건드린다. `onTop` 항목을 `ProtoFx.Play` 가 낼 때 인스턴스 전체를 그 레이어에 놓고(`Apply`), 플레이 중 처음 필요할 때 메인 카메라 자식으로 `FxOverlayCamera` 를 만든다(`Ensure`, 씬에 저장 안 함, 메인이 바뀌면 다시). 메인 카메라는 그 레이어를 뺀다. 기본 켬: `muzzle_flash` · `hit_spark` · `rifle_muzzle` · `rifle_hit`(카탈로그 손질 1단계). 레이어는 `FxAssetsSetup.EnsureOverlayLayer` 가 TagManager 빈 자리(30)에 만든다. 에디터 미리보기(플레이 아님)·코드 그림(builtin)에는 안 먹는다 |
| `HitFx.cs` | 피해 주는 쪽이 `ProtoZombie.TakeDamage(..., HitFx)` 로 넘기는 불똥·처치 ID + 정확한 착탄점 |
| `ActionFxSheet.cs` | 동작 키(`Boss_Final/Slam`) → 이벤트 목록(원샷·스팬·마커, 기준 Root/Socket/Target, 흔들림·소리·히트스톱) |
| `ActionFxPlayer.cs` | 시트 재생기. `Advance(t)` 로 코드가 시계를 넘긴다 — 애니 상태 이름이 필요 없다 |
| `FxShowcase.cs` | 쇼케이스 씬 구동(키로 항목 되풀이·슬로모션·턴테이블·밤). 무기 무대를 세우고 내리는 입구(`BeginWeapon`·`EndWeapon`) |
| `FxWeaponStage.cs` (11-04) | **무기 무대** — 캐릭터 프리팹을 무대 옆에 세우고 무기를 쥐어 더미 좀비를 향해 쏜다. 슈터·투사체·좀비와 같은 연출 경로(세트 → ProtoFx · ProtoGunFx · HitFx · ProtoFlameCone · ProtoBeamFx), 더미는 가짜 체력으로 처치 리듬만. FX 툴 창 [무기] 탭이 조종 |
| `ZombieGibs.cs` (11-05) | **처치 파편** — 좀비 스킨 메시를 본 단위로 조각내(메시마다 한 번 캐시) 죽는 순간 본 자리에 놓고 튀긴다. 코드 그림, 강체 없음, 풀 · 동시 상한. `Burst(body, center, groundY, dir, scale)` → 조각을 냈으면 true. `Clear()` 는 `ProtoFx.Clear` 가 부른다 |
| `ToonExplosionAnchor.cs` (11-12) | **카툰 폭발 바닥 기준** — `FX_ToonExplosion` 루트에 붙어 불덩이 셰이더(`ZombieOverrun/ToonFireball`)에 루트 월드 위치 · 배율을 MaterialPropertyBlock `_Ground` 로 넘긴다. 메시 파티클 정점은 Local 시뮬레이션이어도 **월드 좌표**라 셰이더가 스스로는 바닥을 모른다("위부터 식고 아래부터 사라진다"가 자리에 따라 달라졌다). 켤 때 한 번 + 옮겨졌을 때만 다시 |

### 무기 — `Scripts/Weapons/`

| 파일 | 역할 |
|---|---|
| `WeaponFxSet.cs` (신규) | 무기 하나의 연출 세트. 슬롯 muzzle / hit / kill / explode(= 카탈로그 ID + 배율) + 탄피 크기·배출구 위치. 없으면 `WeaponFxSet.Default` |
| `WeaponStats.cs` | `[연출] fx` 필드 추가 — 세트를 가리킨다 |

### 에디터 — `Editor/Fx/`

| 파일 | 역할 |
|---|---|
| `ActionFxEditorWindow.cs` | **FX 툴 창** — 액션 시트 편집 · 카탈로그 둘러보기 · 쇼케이스 무대 조종을 한 창에서. 탭 [액션 시트 \| 카탈로그], 타임라인 재생·스크럽, 씬 핸들, 창 자체 리그 미리보기, [▶ 무대] 로 쇼케이스를 열고 조종 11-13: 카탈로그 탭에 **크기 (프리팹 루트)** 줄(숫자 · ×0.8 · ×1.25 · ×1.5 · ×2 — 효과 자체, 모든 무기)과 수명 **다시 재기** 버튼 |
| `FxAssetsSetup.cs` | 카탈로그·시트·무기 연출 에셋을 "빠진 것만" 채운다. 예전 코드 상수를 WFX 값으로 옮긴 `Preset` 도 여기 |
| `FxTemplatePrefabs.cs` | 템플릿 파티클 프리팹을 코드로 굽는다. 공통은 `Art/VFX/FX_*.prefab`, 무기별은 `Art/VFX/Weapons/<무기>/FX_<무기>_*.prefab`(이름·폴더·조립 함수 표 `Templates`). 머티리얼 가산 `M_Fx_Glow`·`M_Fx_Ring`·`M_Fx_Star4`, 알파 `M_Fx_Smoke`; 텍스처 점·링·4갈래 별·연기. 머티리얼 규칙 `Harden`(발광 끔·플립북 끔·블렌드·큐)과 "다시 굽기(덮어쓰기)" 메뉴도 여기 |
| `FxShowcaseSceneBuilder.cs` | 쇼케이스 씬을 굽고 연다(`Scenes/FX_Showcase.unity`) |
| `ZombieGibsSetup.cs` (11-05) | 적 모델 3종(워커·개·원거리)의 FBX Read/Write 를 에디터가 뜰 때 켜고(파편이 정점·본 가중치를 읽는다), 디졸브 바탕 머티리얼 `Resources/VFX/M_GibDissolve.mat` 을 만든다. 메뉴 '적 모델 Read-Write 켜기 · 파편 머티리얼 만들기' |
| `ToonExplosionSetup.cs` (11-12) | **카툰 폭발**(사용자 레퍼런스 영상 재현)을 코드로 굽는다 — 머티리얼 9 · 아이코스피어 메시 · 프리팹 `Art/VFX/Explosion/FX_ToonExplosion.prefab`(시스템 16개: 섬광 4 · 가시 불꽃 · 구체 불덩이 3 · 연기 고리 · 흰 파편 · 불똥 2 · 바닥 균열 · 연기 줄) + 카탈로그 `explosion_toon`. 텍스처는 `ArtSource/VFX/bake_toon_explosion.py`, 셰이더 `Art/VFX/Shaders/ToonFireball` · `ToonCrackDecal`. 판(Version)이 오르면 에디터가 뜰 때 다시 굽고, 손으로 고친 프리팹(파일이 표식보다 새것)은 머티리얼만 바꾼다. 메뉴 '카툰 폭발 다시 굽기 (덮어쓰기)' · '카툰 폭발 미리보기 렌더'(`Library/ToonExplosionPreview/video.png · game.png`) |
| ~~`FxToonPackImport.cs`~~ (11-12, **11-20 지움** — 팩 Unique Toon Projectiles 와 복사본 폴더 `Rifle/Toon` 이 이미 없어 쓸 데가 없었다. 파일은 `_Backup/20261120_CodeCleanup`) | **기본 무기 = Toon BallRed** — Unique Toon Projectiles Vol.1 의 `vfx_ToonMuzzle/Hit/Projectile_BallRed` 를 `Art/VFX/Weapons/Rifle/Toon/` 로 복사(데모 스크립트 · 물리 떼고 배율 · 꼬리 시간 배율)해 `rifle_muzzle` · `rifle_hit` · `WS_Rifle_TangTang.projectileModel` 에 건다 — §5-1-d. 메뉴 '소총에 UniqueToonProjectiles (BallRed) 입히기' · '소총 연출 MSFX 로 되돌리기' |
| `FxCatalogEdit.cs` | 프리팹 → 카탈로그 항목 도우미. 프리팹을 놓으면 그 프리팹의 항목을 찾거나(없으면 프리팹 이름으로, 수명은 파티클에서 재서) 만들어 ID 를 돌려준다. ID 드롭다운도 여기 11-13: 새 항목 `onTop` 인자(기본 끔 — 총구 · 피격 슬롯이 켜던 건 같은 날 되돌림) · `SetOnTop` · `SetPrefabRootScale`(루트 배율 고쳐 저장). 수명 재기(`EstimateLifetime`)는 버스트만 쓰는 시스템을 duration 대신 마지막 버스트로, 그리지 않는 빈 껍데기(렌더러 · 방출 꺼짐, 입자 상한 0, 꺼진 오브젝트)는 뺀다 — 팩 프리팹이 6초 · 스팬으로 잘못 잡혔다 |
| `WeaponFxSlotDrawer.cs` | 무기 연출 슬롯 인스펙터(11-13 세 줄) — 1줄 프리팹 칸, 2줄 ID ▾ · **항상 위에**(카탈로그 항목 값, 모든 무기에 먹음), 3줄 **크기** 슬라이더 0.05~4(이 무기의 이 자리 ctx.scale, 툴팁에 실제 = 프리팹 루트 × 크기). 놓아서 새로 생기는 항목은 '항상 위에'가 꺼진 채(11-13 총구 · 피격 칸만 켜던 기본값은 사용자 요청으로 되돌림) |
| `OrbitBladeTrailSetup.cs` (11-18) | **회전 블레이드 톱날 꼬리**를 코드로 굽는다(우리 그림) — 텍스처 2(`T_OrbitTrail_Ribbon` 띠 단면 · `T_OrbitTrail_Spark` 점) · 가산 머티리얼 2(큐 3100 = `_QueueOffset` 100) · 프리팹 `Art/VFX/Weapons/OrbitBlade/FX_OrbitBlade_Trail`(층: `Ribbon_Glow` 하늘색 빛 띠 0.26초 × 0.4m · `Ribbon_Core` 흰 심 0.17초 × 0.12m(11-19 사용자 "띠 너비를 좀 얇게" · "더 얇게" — 0.95 · 0.3 → 0.6 · 0.18 → 지금) — 둘 다 렌더 모드 None + 리본, 시뮬레이션 공간 **Custom** · `Sparks` 테두리 쇳불똥, 월드). 메뉴 '회전 블레이드 꼬리 다시 굽기 (덮어쓰기)'(묻고 한다, GUID 유지). 함정 두 개는 파일 주석: `Mathf.SmoothStep` 은 셰이더 smoothstep 이 아니다(알파 0 텍스처) · `attachRibbonsToTransform` 은 Custom 공간에서 띠 머리를 공간 원점으로 잇는다(톱날 → 플레이어 직선) |
| `FxShaderUnify.cs` (11-20) | **파티클 셰이더 하나로** — `_Game` · `_Reference` 의 파티클 머티리얼을 `ZombieOverrun/FX/Particle`(`Art/VFX/Shaders/FX_Particle.shader`)로 옮긴다. MSFX 복사본(PowDissolve · CommonParticleOneShot), URP Particles/Unlit · Lit, Survivor Ace `SurvivorAce/Effects/Particle · UVFlow`, 탕탕 `Effects/Particle · UVFlow · Turbulence · SheetBase`, 빌트인 `Particles/Standard Unlit`(분홍), 셰이더 그래프를 잃은 `M_LightningRing`. 규칙은 §12. 메뉴 '파티클 셰이더 하나로 합치기' — 새로 가져온 원작 이펙트에 다시 쓴다. 배치 입구 `BatchRun`(전 렌더 → 변환 → 후 렌더 → `Library/FxShaderUnify/compare`) · `BatchSelfCheck`(같은 상태 두 번 — 시간 흐름 UV 가려내기) · `BatchColorTest` |

### 생성되는 에셋

| 경로 | 내용 |
|---|---|
| `Resources/Data/FxCatalog.asset` | 카탈로그 (공통 22항목 + 소총 전용 3항목 = 25) |
| `Resources/Data/ActionFxSheet.asset` | 액션 시트 (`Boss_Final/Slam`, `Boss_Final/JumpSlam`) |
| `Data/Weapons/Fx/WFX_*.asset` | 무기 연출 세트 13개 — `WS_*` 마다 하나, 이미 `WeaponStats.fx` 에 물려 있다 |
| `Art/VFX/Common/FX_MuzzleFlash · FX_HitSpark · FX_KillBurst · FX_GemPickup.prefab` | 공통 템플릿 프리팹(임시 모양 — 실제 아트로 교체 대상) |
| `Art/VFX/Weapons/Rifle/FX_Rifle_Muzzle · FX_Rifle_Hit · FX_Rifle_Kill.prefab` | 루키 소총 전용 템플릿 (2026-11-04). `WFX_Rifle_TangTang` 이 가리킨다 — §5 |
| `Art/VFX/Weapons/Rifle/Toon/FX_Rifle_Muzzle · Hit · Bullet_ToonBallRed.prefab` | 루키 소총(캐릭터 기본 무기)이 지금 쓰는 모양 (2026-11-12) — 팩 BallRed 복사본, 머티리얼 · 텍스처 · 셰이더는 팩 것(GUID) — §5-1-d |
| `Art/Sujin/VFX/Weapon/Rifle/FX_Rifle_TangTang_Muzzle_Flashy.prefab`(11-23 옮김 — 머티리얼은 `Rifle/MSFX` 에 그대로) | 루키 소총 총구가 지금 쓰는 모양 (2026-11-18) — 사용자 MSFX 총구 복사본 + 섬광 · 별 · 양옆 불꽃 · 불똥 층, 카탈로그 `FX_Rifle_Muzzle_Flashy` — §5-1-f |
| ~~`Art/VFX/Explosion/FX_ToonExplosion.prefab`~~ | 카툰 폭발 (2026-11-12) — **11-13 에 폴더째 지워졌다**(11-20 카탈로그 `explosion_toon` 항목도 뺌). 셰이더 `ToonFireball` · `ToonCrackDecal` 과 `ToonExplosionSetup` 은 남아 있다 — 텍스처를 다시 구우면(`ArtSource/VFX/bake_toon_explosion.py`) 메뉴로 되살린다. 텍스처가 없으면 자동 굽기는 쉰다(예전엔 켤 때마다 오류) |
| `Art/VFX/Weapons/OrbitBlade/FX_OrbitBlade_Trail.prefab` + `M_OrbitTrail_*` · `T_OrbitTrail_*` | 회전 블레이드 톱날 꼬리 (2026-11-18) — 카탈로그 `orbit_blade_trail`, `WFX_OrbitBlade.trail` |
| `Scenes/FX_Showcase.unity` | 쇼케이스 씬 (메뉴를 누르면 생긴다 — 아직 안 구움) |

## 3. 메뉴 (Tools ▸ 좀비 오버런 ▸ FX 툴)

| 메뉴 | 하는 일 |
|---|---|
| FX 툴 창 (액션 · 카탈로그 · 무대) | 한 창에서 시트 편집 · 카탈로그 둘러보기 · 쇼케이스 조종. 보통 이것만 연다 |
| 카탈로그·시트 만들기 (빠진 항목 채우기) | 카탈로그·시트·템플릿·무기 세트를 한 번에. 있는 것은 안 건드린다 |
| 무기 연출 에셋 만들기 (빠진 무기만) | `fx` 가 빈 `WS_*` 에만 WFX 를 만들어 물린다 |
| 템플릿 프리팹 만들기 (빠진 것만) | 공통 `Art/VFX/FX_*.prefab` 네 개 + 무기별 `Art/VFX/Weapons/*/` (소총 세 개). 머티리얼 규칙도 다시 건다 |
| 소총 템플릿 다시 굽기 (덮어쓰기) / 공통 템플릿 다시 굽기 | 코드 값으로 **덮어쓴다**(묻고 한다). 같은 경로에 저장해 GUID 가 그대로라 카탈로그·WFX 참조는 안 끊긴다. 코드 값을 고친 뒤 화면에 반영하는 길 — 손으로 고친 프리팹은 사라진다 |
| FX 머티리얼 설정 고치기 (발광 · 플립북 · 블렌드) | `M_Fx_*` 넷에 규칙을 다시 건다. 에디터가 켜질 때도 어긋나 있으면 스스로 고친다 |
| 소총에 MasterStylizedFX (YellowMachineGun) 입히기 | 팩의 총구·착탄·탄 프리팹을 `Art/VFX/Weapons/Rifle/MSFX/` 로 **복사**(머티리얼·텍스처·메시까지, `*_MSFX` 이름)하고 카탈로그 `rifle_muzzle`·`rifle_hit` 와 `WS_Rifle_TangTang.projectileModel` 이 복사본을 가리키게 한다. 원본은 안 건드린다. 처음 한 번은 에디터가 뜰 때 스스로 돈다(§5-1) |
| 소총에 UniqueToonProjectiles (BallRed) 입히기 / 소총 연출 MSFX 로 되돌리기 | `FxToonPackImport` — 팩 BallRed 총구 · 착탄 · 탄을 `Art/VFX/Weapons/Rifle/Toon/` 로 다시 굽고(덮어씀, GUID 유지) 소총에 건다 / 카탈로그 · 탄을 MSFX 복사본으로 되돌린다(Toon 복사본은 남음). 처음 한 번은 에디터가 뜰 때 스스로 돈다(§5-1-d) |
| 카툰 폭발 다시 굽기 (덮어쓰기) / 카툰 폭발 미리보기 렌더 | `ToonExplosionSetup` — 코드 값으로 프리팹을 **덮어쓴다**(묻고 한다, GUID 유지) / 숨은 씬에서 12컷(영상 각도) · 6컷(게임 각도)을 찍어 `Library/ToonExplosionPreview/` 에 둔다(블룸 없음) |
| 쇼케이스 씬 만들기 / 열기 / 열고 플레이 | 쇼케이스 |
| 카탈로그 선택 / 시트 선택 | 인스펙터에 띄운다 |

## 4. 카탈로그 ID (35개)

`ctx` 열은 코드가 넘겨 주는 것 — 프리팹을 만들 때 이 규약에 맞춘다. **scale 1 = 소총·잡좀비**, 덩치·치명타 배율은 코드가 곱한다.

| ID | 층 | 자리 규약 (ctx) | 지금 모양 | 수명 / 풀 / 동시 |
|---|---|---|---|---|
| `boss_windup_dust` | 세리머니 | 보스 발밑, scale | **임시 프리팹 Fx_Matrix_Smoke** (탕탕 추출본, 11-12 — `Assets/_Reference/TangTang/README.md`). 칸을 비우면 코드 그림 | 2 |
| `boss_hammer_flames` | 세리머니 | 스팬, 소켓 HammerHead | 코드 그림 | 스팬 |
| `boss_hammer_trail` | 세리머니 | 스팬, 소켓 HammerHead | **임시 프리팹 FX_JiangShiMa_Trail** (탕탕, 11-12) | 스팬 |
| `boss_impact` | 세리머니 | Target 앵커, radius = 판정, big. 코드 그림은 흔들림·소리 포함 — 프리팹일 땐 시트 충돌 이벤트가 낸다(Slam Heavy · JumpSlam Huge, sfx explosion, scale 2.5 / 3.4) | **임시 프리팹 FX_ZombieKing_WarStomp** (탕탕, 11-12) | 3 |
| `poison_gas_cloud` (11-12) | 정보 | 스팬, 바닥, scale = 판정 반경(m). ProtoPoisonGas — 프리팹이 있으면 코드 퍼프를 숨기고 바닥 원(판정)은 남긴다 | **임시 프리팹 FX_Halo_FuShi** (탕탕) · 폴백 없음 | 스팬 / 4 / 8 |
| `poison_bolt` (11-12) | 정보 | 스팬, 구슬을 따라감(나는 쪽을 봄), scale 1 = 원래 코드 구슬 자리. ProtoEnemyBolt — 프리팹이 있으면 코드 구슬·꼬리를 숨긴다. 맞거나 막히거나 화면 밖으로 나가면 `FxHandle.Kill()`(남은 입자를 기다리지 않음) | **임시 프리팹 Bullet_HUNHUO_Green** (탕탕 혼불탄 초록 복사본, 루트 0.225, 꼬리 0.15초 · 연기 수명 반. 예비: PosionBall) · 폴백 없음 | 스팬 / 8 / 16 |
| `poison_bolt_hit` (11-12) | 정보 | 독 구슬 자리, scale 1. ProtoEnemyBolt.Pop — 프리팹 구슬일 때만 | **임시 프리팹 Bullet_Boss_KongJuZhiChao03_ShuiQiu_Hit** (탕탕) · 폴백 없음 | 0.8 / 8 / 12 |
| `player_poison_hit` (11-12) | 정보 | 플레이어 가슴(로컬 위 1.3m), 따라다님, scale 1. ProtoPlayer.TakeDamage(Poison) — 혼불탄에 맞으면 초록 숫자 · 초록 번쩍임과 같이 | **임시 프리팹 Fx_Drug_Circle** (탕탕, 반복 끔, 루트 0.7) · 폴백 없음 | 파티클 끝 / 2 / 3 |
| `zombie_claw_hit` (11-12) | 타격감 | 플레이어 가슴(로컬 위 1.3m), 따라다님, scale 1, onTop. ProtoPlayer.TakeDamage(Claw) ← 평타 좀비 접촉 공격 — 빨간 숫자 · 빨간 번쩍임과 같이 | **임시 프리팹 Bullet_BeastGirlActive_WolfClawAttack** (탕탕 늑대 발톱, 반복 끔, 루트 0.8) · 폴백 없음 | 0.5 / 4 / 6 |
| `boss_telegraph` (11-12) | 정보 | 스팬, 충돌 지점, scale = 판정 반경(m). ProtoBossFx.ShowTelegraph — 코드 원 위에 얹는다 | **임시 프리팹 WarningIndicator_Circle_Out_OneShot** (탕탕) · 폴백 없음 | 스팬 / 2 / 4 |
| `boss_spawn` (11-12) | 세리머니 | 보스 발밑, scale = 보스 덩치. ProtoSpawnDirector.SpawnBoss | **임시 프리팹 Fx_Boss_Spawn** (탕탕) · 폴백 없음 | 2 / 2 / 2 |
| `hit_blood` (11-12) | 타격감 | hit_spark 와 같은 자리·방향, scale = 덩치(치명타 ×1.5), 큰 한 방 제외. ProtoZombie.DealDamage | **임시 프리팹 HitBlood** (탕탕) · 폴백 없음 | 1.2 / 12 / 20 |
| `muzzle_flash` | 타격감 | 총구, +Z = 총열. 캐논 초당 12발 | 코드 그림 (템플릿 FX_MuzzleFlash 있음) | 0.1 / 8 / 8 |
| `hit_spark` | 타격감 | 맞은 자리, +Z = 날아온 쪽. 치명타 ×1.5·big | **프리팹 FX_HitSpark** | 0.4 / 24 / 20 |
| `kill_burst` | 타격감 | 몸 가운데, scale = 덩치, big = 보스·엘리트 | **프리팹 FX_KillBurst** | 0.8 / 12 / 12 |
| `gem_pickup` | 보상 | 젬 자리, scale = 등급(0.8 / 1 / 1.4) | **프리팹 FX_GemPickup** | 0.6 / 12 / 12 |
| `zombie_gore` (11-05) | 타격감 | 몸 가운데, +Z = 맞은 방향, scale = 덩치. 몸 조각은 코드 그림(ZombieGibs)이 따로 | **프리팹 FX_ZombieGore** (핏방울·핏덩이·안개·바닥 피 3.5초) | 3.8 / 16 / 16 |
| `rifle_muzzle` | 무기·소총 | 총구, +Z = 총열, scale 1 = 소총. 폴백 = 공통 코드 그림 | **프리팹 arrow_white** (11-13, Survivor Ace 원작 — 흰 가시 섬광 · 빛, 루트 0.43, `Assets/_Reference/SurvivorAce/README.md`). 예전: 템플릿 FX_Rifle_Muzzle → MSFX → Toon BallRed(§5-1-d) | 0.47 / 6 / 6 |
| `rifle_hit` | 무기·소총 | 착탄점, +Z = 날아온 쪽, scale = 덩치(치명타 ×1.5) | **프리팹 Hitted_Machine_Small** (11-13, Survivor Ace 원작 — 꺼져 있던 `stg_blast_Hit_01` 갈래를 켬: 흰 → 노랑 가시 별 · 파편, 루트 0.43). 예전: FX_Rifle_Hit → MSFX → Toon BallRed | 0.9 / 24 / 20 |
| `rifle_kill` | 무기·소총 | 몸 가운데, scale = 덩치 | **프리팹 FX_Rifle_Kill** (섬광·링·파편·반짝임) | 0.8 / 8 / 10 |
| `explosion` | 타격감 | 폭심, radius = 판정(링 테두리), big, quiet | 코드 그림 | 1.2 |
| `explosion_toon` (11-12) | 타격감/세리머니 | 폭심(바닥), scale 1 = 불덩이 지름 2.6m · 균열 5m. radius 는 안 쓴다 | **프리팹 FX_ToonExplosion** (섬광 · 가시 불꽃 · 구체 불덩이 → 검은 연기 → 디졸브 · 연기 고리 · 흰 파편 · 불똥 · 바닥 용암 균열) | 3.4 / 4 / 6 |
| `levelup_beam` | 세리머니 | 캐릭터 발 | 코드 그림 | 2 |
| `evolve_beam` | 세리머니 | 캐릭터 발, color = 금색 | 코드 그림(레벨업 빛기둥 금색) | 2 |
| `intro_landing` (11-16) | 세리머니 | 착지 지점(바닥), scale 1 = 캐릭터. ProtoIntro.Land(건너뛰기면 안 냄) — 흔들림 · 찌그러짐 · 소리(land)는 코드 | **임시 프리팹 MFX_Landing_Smoke** (Survivor Ace 보스 낙하 연기 파티클판 — 푸른 흰 회색 · 깊이 검사 끔 · 루트 0.16, `_Reference/SurvivorAce/README_LandingSmoke.md`) · 예비 템플릿 FX_IntroLanding(먼지 고리 · 잔돌 · 충격파) · 비우면 교체 착지 먼지 | 1.4 / 1 / 1 |
| `swap_landing` / `flame_jet` / `burn_body` | 예시 | 교체 착지 / 화염 분사(스팬) / 화상 표시(스팬) | 프리팹 예시 자리 | 2 / 스팬 / 스팬 |
| `van_hit` | 환경 | 맞은 자리, +Z = 위 | 코드 그림 | 0.3 / 8 / 8 |
| `van_blast` | 환경 | 차 중심 바닥, radius, big, scale 2, **항상 위에**. 흔들림·소리는 코드(`ProtoBossFx.ImpactFeel`) | **프리팹 FX_Car_Explosion** (11-13, Survivor Ace 운석 폭발 사용자 복제본 — `Assets/_Reference/SurvivorAce/README_MeteoriteExplosion.md`). 비우면 코드 그림 | 1.5 |
| `van_appear_dust` | 환경 | 차 중심 | 코드 그림 | 1.5 |
| `heli_crash_pop` | 환경 | radius 1.1 / 1.6, quiet | 코드 그림 | 1.2 / 6 / 6 |
| `heli_crash_impact` | 환경 | 충돌 지점 바닥, radius, big, scale 2.2, **항상 위에**. 흔들림(Huge)·소리는 코드(`ProtoBossFx.ImpactFeel`) | **프리팹 FX_Car_Explosion** (11-13). 비우면 코드 그림 | 1.5 |
| `tank_blast` | 탈것 | 탱크 바닥, radius, big, scale 1.7, **항상 위에**(잔해에 가려졌다). 소리·풀은 코드(`ProtoExplosion.Aftermath`) | **프리팹 FX_Car_Explosion** (11-13). 비우면 코드 그림 | 1.5 |
| `tank_debris_pop` | 탈것 | 잔해, radius 1.4 | 코드 그림 | 1.2 / 4 / 4 |
| `tank_cannon_hit` (11-17) | 탈것 | 탱크 포탄이 터진 점(대개 좀비 몸통 0.9m), scale = `ProtoTank.CannonHitFxScale` 1 × 거리 배율(포구에서 거의 안 날아가면 ×0.5 → 5m 부터 ×1, 세트 `explodeNearScale` · `explodeFullDistance`), radius 2.6(코드 그림만 씀), quiet, **항상 위에**(큰 빛 판이 땅에 걸려 잘렸다). ProtoTank 가 런타임 연출 세트 `(탱크 포 연출)` 의 explode 슬롯에 꽂고 `ProtoProjectile.Explode` 가 낸다. 소리·풀은 코드(`ProtoExplosion.Aftermath` — 프리팹 폭발이면 Explode 가 따로 부른다) | **임시 프리팹 FX_Death_XiaoGuai_002** (Survivor Ace 원작 이펙트 표 210008 '일반 폭발', 루트 1 — `_Reference/SurvivorAce/README_TankCannonHit.md`). 비우면 예전 공용 착탄 폭발 코드 그림 | 0.9 / 4 / 6 |
| `ult_combust` | 필사기 | 화상 적마다 radius, 첫 발만 흔들림(quiet) | 코드 그림 | 1.2 / 8 / 8 |
| `orbit_blade_trail` (11-18) | 무기·회전 블레이드 | **스팬**, 톱날마다 피벗(+Z = 궤도 접선, +Y 위, 톱날처럼 돌지 않음). ProtoSupportGear 가 톱날을 만들 때 켜고(`FxHandle.BindSimulationSpace(궤도 루트)` — Custom 층이 플레이어를 따라다니는 `_Gear_` 루트 기준으로 돌아 달려도 원을 그림), 매 프레임 `SnapToAnchor`, 레벨업으로 다시 만들면 Kill, 쓰러지면 Stop. scale = `WFX_OrbitBlade.trail.scale` | **프리팹 FX_OrbitBlade_Trail** (우리 그림 — 하늘색 빛 띠 + 흰 심 + 쇳불똥) | 0.5 / 6 / 12 |

프리팹으로 바꾸는 법: 카탈로그 항목의 `prefab` 에 꽂는다. `builtin` 은 그대로 둬도 된다 — 프리팹이 있으면 프리팹이 이긴다.

**ID 를 손으로 적지 않아도 된다.** 시트 이벤트의 "프리팹" 칸, 무기 연출 슬롯의 프리팹 칸, 카탈로그 탭의 "프리팹 → 항목" 칸에 프리팹을 끌어다 놓으면 그 프리팹의 항목을 찾아 ID 를 넣고, 없으면 프리팹 이름으로 항목을 만든다(수명은 파티클 길이에서, 루프면 스팬). 같은 프리팹은 늘 같은 ID 로 간다. ID 는 풀·동시 상한·쇼케이스 목록의 열쇠라 런타임은 그대로 ID 로 부른다.

## 5. 무기 연출 세트 (`WeaponFxSet`)

| 슬롯 | 뜻 | 코드가 넘기는 것 |
|---|---|---|
| `muzzle` | 쏠 때 총구 | 총구점, +Z 총열, 필사기 연사는 big |
| `casingScale` / `casingPortBack` | 탄피 크기(0 = 없음) / 배출구가 총구에서 뒤로 몇 m | — |
| `muzzleOffset` (11-04) | 총구 화염 자리 보정(m): z = 총열 앞/뒤, y = 위, x = 오른쪽. 팩 프리팹처럼 원점이 불꽃 가운데인 모양을 총구 끝에 맞출 때 | 총구점 + 총열 방향 기준 오프셋(`MuzzlePos`) |
| `hit` | 맞혔을 때 닿은 자리 불똥 | 정확한 착탄점, +Z 날아온 쪽 |
| `kill` | 이 무기로 처치했을 때 | 몸 가운데, scale = 덩치 |
| `explode` | 착탄 폭발(aoe 무기만) | radius = 판정 반경. 세트의 `explodeNearScale` · `explodeFullDistance`(11-17)로 가까이서 터지면 작게 — 탄이 총구에서 날아간 거리 0 에서 ×near, full m 이상 ×1(기본 near 1 = 거리 무관, 탱크 포만 0.5 / 5m) |
| `trail` (11-18) | 몸에서 떨어져 움직이는 부품(회전 블레이드 톱날)마다 붙는 꼬리 — 스팬. 비우면 꼬리 없음 | 부품 피벗(+Z 나아가는 쪽), Custom 공간 층은 궤도 루트에 묶음. 지금은 `WFX_OrbitBlade` 만 씀(`orbit_blade_trail`) |

처음 생성될 때 예전 코드 상수를 발사 방식별로 옮겨 넣었다(`FxAssetsSetup.Preset`). 겉보기는 전과 같다.

| 발사 방식 | muzzle | 탄피 (배출구) | hit | explode |
|---|---|---|---|---|
| Single 소총 / 광선 / 캐논 | 1 / 1.2 / 1.35 | 1 (0.35) / 캐논 1.2 (0.55) | 1 | aoe 있으면 |
| Shotgun | 1.25 | 1.5 | 1 | — |
| Rocket · Cone(화염) · 폭격 · 수류탄 | 없음 | 없음 | 없음 | ✔ |
| Orbit(블레이드) · Companion(드론) | 없음 | 없음 | 0.8 | — |
| HeliCall(헬기 기관포) | 1.8 | 없음 | 0.55 | — |
| 발사체 모델이 있는 무기 | 없음 | 없음 | | |

**다시 눌러도 덮어쓰지 않는다** — 값은 자유롭게 고쳐도 된다. 새 무기는 `WS_*` 를 만들고 메뉴를 누르거나, 기존 WFX 를 복제해 물린다.
인스펙터의 슬롯은 첫 줄이 프리팹 칸이다 — 프리팹을 놓으면 ID 가 저절로 들어간다. 코드 그림을 쓰는 자리는 프리팹 칸이 비어 있고 ▾ 로 고른다. 프리팹 칸을 비우면 그 자리는 꺼진다.

### 무기 전용 세트 — 루키 소총이 1호 (2026-11-04)

공통 ID(`muzzle_flash`·`hit_spark`·`kill_burst`)는 전용 세트가 없는 무기의 기본이다. 무기를 하나씩 손보기 시작하면 **그 무기만의 ID 세 개**(총구·피격·처치)를 두고 WFX 가 그것을 가리키게 한다 — 소총만 고쳐도 다른 무기는 그대로다.

| 무기 | WFX | 총구 | 피격 | 처치 | 템플릿 폴더 |
|---|---|---|---|---|---|
| 루키 탕탕 소총 (캐릭터 기본 무기 — 루키 · 리퍼) | `WFX_Rifle_TangTang` | `rifle_muzzle` — 11-13 부터 Survivor Ace `arrow_white`(흰 가시 섬광) | `rifle_hit` — 11-13 부터 Survivor Ace `Hitted_Machine_Small`(흰 → 노랑 가시 별) · **탄은 11-13 부터 Survivor Ace `Bullet_miniplane1_1`**(금빛, `Assets/_Reference/SurvivorAce/README.md` — 진화 캐논은 `Bullet_miniplane1_2`, 지원 드론도 1_1) | `rifle_kill` — 섬광·바닥 링·파편·반짝임(템플릿 그대로) | `Assets/_Reference/SurvivorAce/` (11-12 Toon 복사본은 `Art/VFX/Weapons/Rifle/Toon/` 에 남아 있다) |
| 인페르노 (11-06, 화염방사기 진화) | `WFX_Inferno` | **`jet` 슬롯** `inferno_jet` — 컨셉 아트를 파티클로(시트 애니 없음, §5-1-c) | — | `kill_burst` 공통 | `Art/Sujin/VFX/Weapon/Inferno/`(11-23 옮김) |
| 화염방사기 (11-05) | `WFX_Flamethrower` | **`jet` 슬롯** `inferno_jet` — 인페르노와 같은 불줄기. 예전 팩 복사본 `flamethrower_jet` · 시트 `flamethrower_sheet` 는 **11-23 지움**(`_Backup/20261123_SujinFolder`) | — (화염은 틱이 잦아 비움) | `kill_burst` 공통 | `Art/Sujin/VFX/Weapon/Inferno/` |

팔레트는 예광탄 노랑(`ProtoGunFx.TracerColor`) 계열 네 색(흰빛 심 → 노랑 → 주황 → 잉걸)으로 통일해 총구·피격·처치가 "같은 총"으로 읽히게 했다. 연기·먼지는 알파 블렌드 `M_Fx_Smoke`(빛나지 않음, 큐 **2850** — 밤 어둠 막 2900 앞에 그려 밤이면 같이 어두워진다), 빛나는 것은 가산 `M_Fx_Glow`·`M_Fx_Star4`·`M_Fx_Ring`(큐 3100, 어둠 뒤라 밤에 빛난다). 탄피는 코드 그림(`ProtoGunFx.EjectCasing`) 그대로, 예광탄은 `WS_Rifle_TangTang` 의 `tracerColor / tracerLength / tracerWidth`.

#### 5-1. 외부 팩 이펙트 입히기 — MasterStylizedFX (2026-11-04)

템플릿 대신 팩 이펙트를 쓰고 싶을 때. 메뉴 **소총에 MasterStylizedFX (YellowMachineGun) 입히기** 가 `Editor/Fx/FxPackImport.cs` 로 다음을 한다.

- 팩 프리팹을 `LoadPrefabContents` 로 열어 **중첩 프리팹(Common/Smoke 등)을 완전히 풀고**, 빠진 스크립트·팩 데모 스크립트·점광원(착탄마다 점광원은 모바일 예산 밖)을 떼고, 머티리얼·텍스처·메시를 `AssetDatabase.CopyAsset` 으로 복사해(이름 `M_MSFX_*` · `T_MSFX_*` · `MSFX_*.fbx`) 참조를 복사본으로 돌린 뒤 `FX_Rifle_Muzzle_MSFX` · `FX_Rifle_Hit_MSFX` · `FX_Rifle_Bullet_MSFX` 로 저장한다. 셰이더만 팩 것을 그대로 쓴다.
- 팩 단위계가 크다(총구 섬광 중심 3.5 유닛). 복사본 루트를 총구 **0.14** · 착탄 **0.36** 배(v3 — v2 의 0.18 은 "너무 안 보인다", v1 의 0.22/0.3 은 크다고 사용자가 봤다)로 줄이고 파티클을 **Hierarchy 스케일**로 바꾼다(팩 기본 Local 은 루트를 줄여도 입자가 안 줄어든다). 탄은 루트 1 — `projectileSize`(0.22)가 스케일한다. 크기는 [무기] 탭의 슬롯 배율로, 총구 자리는 세트의 `muzzleOffset` 으로 더 맞춘다.
- 머티리얼 변환은 원본의 `_Mode`(빌트인 표준 파티클: 페이드/가산/변조…)에서 블렌드를 옮기고, ×2 는 레거시 파티클 셰이더에만 건다(v1 은 표준 파티클에도 ×2 를 걸어 과하게 밝았다). 팩이 머티리얼을 비워 둔 파티클(OneShotParticle 자리)은 끈다 — 팩에서도 안 그려지는 자리다.
- **판(Version)**: 복사 규칙이 바뀌면 `FxPackImport.Version` 을 올린다. 에디터가 뜰 때 표식의 판이 낮으면 복사본을 지우고 다시 만든다. 복사본을 손으로 고친 흔적(표식보다 나중에 저장된 파일)이 있으면 자동으로는 안 하고 메뉴 "복사본 지우고 다시 입히기"에 맡긴다.
- 원샷 길이는 팩의 duration(5초, 의미 없는 값) 대신 **마지막 버스트 시각 + 입자 수명**으로 재서 카탈로그 수명에 넣는다. 탄의 무한 수명은 0 으로 친다.
- **팩은 빌트인 파이프라인 기본값**이다(팩 ReadMe) — 셰이더 14개 중 9개가 `#pragma surface` 서피스 셰이더, 머티리얼 대부분이 빌트인 `Particles/Standard Unlit` 라 URP 에선 팩 프리팹이 전부 분홍이다(셰이더 오류가 아니라 URP 가 안 그리는 것 — 로그에 오류 없음). 사용자 결정(11-04)은 "팩은 그대로, 복사본만 변환"이었으나 **11-05 바뀜**: 팩 프리팹을 제대로 보려고 팩에 든 `MSFURP.unitypackage`(535개 — 머티리얼 240 · 프리팹 149 · 셰이더 14 · 모델 36 · 스크립트 7 · 데모 씬, 전부 팩 폴더 안을 덮어씀)를 **가져오고 복사본도 다시** 만들기로. **v4 자동 흐름**: 에디터가 뜰 때 대표 셰이더(`CommonParticleOneShot.shader`)에 `#pragma surface` 가 남아 있으면 패키지를 조용히 가져오고(`AssetDatabase.ImportPackage`, 시도 표식 `Library/FxPackImport_MSFURP.attempted` — 가져왔는데도 빌트인이면 같은 판에서 다시 시도하지 않는다) → `importPackageCompleted` 의 다음 틀에서(가져오는 중 도메인이 다시 뜨면 `[InitializeOnLoadMethod]` 가 이어서) 복사본을 지우고 다시 만든다. URP 판 머티리얼은 셰이더가 이미 URP 라 변환을 건너뛰고(마스크 등 기능 유지), 팩 자체 셰이더(`CommonParticleOneShot` 등)를 쓰는 머티리얼은 **셰이더 파일도 복사**해 `SH_MSFX_*.shader`, 안의 이름은 `ZombieOverrun/MSFX/원래 이름`(같은 이름이 둘이면 `Shader.Find` 가 헷갈린다; 팩 안 파일을 상대 경로로 `#include` 하거나 복사본이 컴파일에 실패하면 팩 셰이더를 그대로 쓰고 알린다). 패키지 안 `Circle.shader` 는 여전히 서피스 셰이더(팩 작가가 안 바꿈) — 소총과 무관. URP 판 YellowMachineGun 머티리얼: Muzzle · HitCenter · Bullet 은 URP Particles/Unlit, LaserHit · LaserHit2 · FadeRoundParticle 은 팩 `CommonParticleOneShot`(URP, Packages include 만).
- **자동 적용**: 복사본 폴더도 `Library/FxPackImport_YellowMachineGun.applied` 표식도 없으면 에디터가 뜰 때 한 번 스스로 입힌다(메뉴를 누를 필요 없음, 콘솔 `[FX 팩] … 입힘`). 표식이 Library 에 있는 이유: 되돌리려고 복사본 폴더를 지운 뒤 다시 자동으로 붙으면 안 된다. 다른 PC 에서는 복사본 폴더가 이미 있으면 안 돈다.
- 되돌리기: 카탈로그 `rifle_muzzle` · `rifle_hit` 의 프리팹을 `FX_Rifle_*` 템플릿으로 되돌리고 `WS_Rifle_TangTang.projectileModel` 을 비운다. 처치(`rifle_kill`)는 팩에 없어 템플릿 그대로다.

#### 5-1-b. 화염방사기 — RedFlameThrower 그대로 (2026-11-05)

사용자: "RedFlameThrower 프리팹 이펙트를 복사해서 W_Flamethrower 무기 이펙트로". 화염은 원샷이 아니라 **스팬**이고 지금까지 `ProtoFlameCone` 이 `Resources/VFX/FX_FlameJet` 를 코드로(색 · 크기 · 그라디언트 · 콘 각 · 속도) 다시 짜서 썼다 — 팩 모양을 그대로 쓰려면 그 코드를 거치면 안 된다. 그래서:

- 세트에 **`jet` 슬롯**("화염 (콘 무기)")을 뒀다. 카탈로그 ID 를 가리키면 `ProtoShooter` · `FxWeaponStage` 가 `ProtoFlameCone` 대신 **`Scripts/Proto/ProtoFlamePack`** 을 붙인다(옵션 FlameStyle 보다 우선). 비우면 전처럼 `ProtoFlameCone`.
- `ProtoFlamePack` 은 카탈로그 항목의 프리팹을 총구 자식으로 세우고 넷만 한다: ① 루트를 **균일하게** 키워 불끝이 사거리에 닿게 — 프리팹 루트 제트의 "속도 × 수명"(RedFlameThrower 28 × 1.5 = 42 팩 단위)을 재서 `사거리 / (그 값 × visibleFraction 0.75)`(알파가 수명 끝까지 서서히 꺼져 끝 1/4 는 안 보인다) × 세트 `jet.scale`; ② 콘 모양이면 반각을 `판정 전각 × 0.5 × coneFraction(0.7)` 까지 넓히고(원본 5.8° 보다 좁히진 않는다, 넓힌 만큼 방출률 제곱근 배) ③ 모양 스케일 y 를 `flatten`(0.35)으로 눌러 도로를 안 뚫게 ④ 방출만 여닫는다(HoldTime 0.3, 총열 정렬 규칙은 `ProtoFlameCone.AimAlongBarrel` 공용). 색은 손대지 않는다 — 진화 무기의 `flameTint` 는 안 입힌다(팩 머티리얼 색 그대로).
> **2026-11-23 지움** — 아래 MSFX 복사본(`Weapons/Flamethrower/MSFX`) · 시트(`Weapons/Flamethrower/Sheet`) · 카탈로그 `flamethrower_jet` · `flamethrower_sheet` · `FlamethrowerSheetSetup` · `ProtoFlameSheetFx` 는 지웠다(지금 화염방사기는 `inferno_jet`). 에디터가 켜질 때 복사본을 다시 만들던 자동 적용도 껐다(`FxPackImport.FlameAutoApply`, 메뉴는 남음). 기록으로만 남긴다 — 파일은 `_Backup/20261123_SujinFolder`.
- 복사는 `FxPackImport` 의 소총 규칙 그대로(`CopyEffect` 에 폴더 인자, 루트 자세는 원점 · 무회전으로 — 팩 데모 자리 x −10 · y 90° 가 루트에 남아 있었다) → `Art/VFX/Weapons/Flamethrower/MSFX/FX_Flamethrower_Jet_MSFX.prefab`(루트 1, 런타임이 맞춘다) + 머티리얼 `M_MSFX_CommonFire` 등. 적용 = 카탈로그 `flamethrower_jet`(없으면 항목도 만든다, 수명 0) 의 프리팹 + `WFX_Flamethrower.jet`. 에디터가 뜰 때 한 번 자동(표식 `Library/FxPackImport_RedFlameThrower.applied`; 카탈로그 · 세트가 아직 없으면 표식을 안 쓰고 다음 로드에), 메뉴 **화염방사기에 MasterStylizedFX (RedFlameThrower) 입히기**(있으면 지우고 다시). 되돌리기: `WFX_Flamethrower.jet.fxId` 를 비운다.
- 팩 프리팹 구성: 루트 `RedFlameThrower`(콘 5.8°, 속도 16~28, 수명 1.5, 크기 3~4, 늘림 빌보드, 시트 4×4, 방출 20/s) · `Head`(z −1.91, 뒤로 1~7, 수명 0.4 — 총구 쪽 불머리) · `Smoke`(z −1.39, 18, 수명 1, 콘 1°, fudge 20.9). 머티리얼 셋 다 `Fires/CommonFire`(URP 판).
- **v2 손질(사용자 "이런 느낌으로" — 참고 영상 sssss.mp4: 2D 횡스크롤 화염방사기, 총구에서 굵고 밝은 노란-흰 심이 곧게 뻗다 주황 불기둥으로 넓어지고 끝에서 느려지며 부풀어 위로 말린다, 연기 거의 없음)**: `FxPackImport.StyleFlameJet` 이 복사 직후 프리팹을 고친다 — 루트 불기둥 시작색 (1, 0.92, 0.6, α0.75)(팩은 붉은 (1, 0.24, 0, α0.37)), 수명 그라디언트 흰 → 노랑(0.28) → 주황(0.62) → 검붉음(1) · 알파 1 → 0.75(0.8) → 0, 크기 2.6~4.2 · 수명 따라 0.35 → 1 → 1.35(끝에서 부풂), 방출 20 → 40/s, **끌림(limitVelocity.drag 0.9)** 로 끝에서 느려져 모이고(거리 v/d·(1−e^(−d·t)) ≈ 0.5배 — `ProtoFlamePack.Travel` 이 같은 식으로 잰다), 속도-수명 y 곡선(0.45 뒤 위로 6 팩 단위/s)으로 끝에서 위로 말리고, 노이즈(1.2 · 0.35 · 스크롤 0.6 · 2옥타브)로 가장자리가 일렁인다. 늘림 빌보드 길이 1.4 · 속도 0.1. Head(총구 불머리)는 희게 18/s, Smoke 는 (0.05, 0.03, 0.02, α0.22) 6/s. 기본값 `visibleFraction` 0.8 · `coneFraction` 0.6. 판(`FlameVersion` 2, 표식 v2) — 낮으면 복사본을 지우고 다시(손으로 고친 흔적이 있으면 메뉴로), v1 이 소총 폴더에 떨어뜨린 `M_MSFX_CommonFire` · `SH_MSFX_PowDissolve` 도 지운다(복사 헬퍼는 이제 `CurDst` — 세트마다 제 폴더).
- **v3(사용자 "전혀 다르다 — 총구에서 퍼져 나가는 부채꼴이어야 하는데 총구 자리도 안 맞는다")**: 팩의 `Head`(불머리)는 루트 **뒤 1.91 유닛**에서 뒤로(−1~−7) 흐르고 `Smoke` 도 뒤(−1.39)에 있어 불이 총 몸통 · 손에서 나오는 것처럼 보였다 → 셋 다 **원점(총구)** 으로(Head 는 앞으로 0.5~2.5, 1.6~2.6 크기; Smoke 는 0.5 앞, 4/s). 불기둥은 속도 22~28(폭을 좁혀 가장자리 깨끗하게) · 반지름 0.25 · 무작위 방향 0. 부채꼴은 `ProtoFlamePack.coneFraction` **1**(판정 30° 그대로 — 영상의 부채꼴과 같다) · `flatten` 0.3, 넓힌 만큼 방출 ×(각 비율^0.75, 최대 4). 처음 맞출 때 콘솔 `[화염] 팩 제트 세움 — … 루트 ×s, 콘 반각 …°, 총구 이름 (x, y, z)` — 총구 자리가 틀리면 이 좌표와 총 끝을 비교한다.
- **v4(사용자 "총구에서 안 나간다")**: 에디터 로그에 `Particle Velocity curves must all be in the same mode` 가 방출을 켤 때마다(5만 번) 찍혀 있었다 — v2 손질이 속도-수명 모듈의 x · z 를 상수, y 를 곡선으로 넣어 모드가 섞였고 유니티가 모듈을 거부해 불이 제대로 안 나왔다. 셋 다 곡선(상수 0 곡선)으로. **규칙: `velocityOverLifetime` · `limitVelocityOverLifetime`(separateAxes) · `forceOverLifetime` 의 x · y · z 는 같은 모드(상수/곡선/두 상수/두 곡선)여야 한다.** 총구 좌표(로그 `총구 MuzzleAuto (-2.49, 0.83, -1.00)`, 무대 사수 발 (−2.4, 0, −1.6) 앞 0.6m · 높이 0.83)는 맞았다.
- **v5(사용자 스크린샷 "여전히 총구에서 안 나온다" — v3 화면: 불이 캐릭터 몸 · 손에서 시작)**: 늘림 빌보드는 입자 자리를 가운데 두고 앞뒤로 늘어난다 — 태어날 때 크기 0.35 × 0.6~1m 에 길이 1.4 와 속도 늘림(5.5m/s × 0.1)을 더해 약 1m 조각이 총구 뒤 0.5m 까지 걸쳤고, 불머리(0.37~0.6m)도 총구에 가운데를 두어 뒤로 덮었다. → 태어날 때 0.12(영상처럼 총구에서 가늘게 시작해 부채꼴로 커진다), 늘림 1.0 · 속도 0.04, 발사 원점 0.4 팩 단위(≈9cm) 앞, 노이즈 세기를 수명 곡선(0 → 1)으로(갓 태어난 입자가 뒤로 안 흔들리게), 불머리 0.8~1.4 · 0.6 앞, 연기 1.0 앞. **규칙: 총구에서 시작해야 하는 늘림 빌보드는 태어날 때 작아야 한다 — 조각의 뒤쪽 절반이 총구 뒤로 나온다.**
- **v6(사용자 "조금 더 앞으로 · 너무 노란 불꽃 → 붉은 불꽃 · 사거리가 적 앞쪽까지만 → 끝까지")**: `ProtoFlamePack.muzzleForward` 0.2m(불 시작점을 총구 앞으로, Reach 에서 뺀다) · `visibleFraction` 0.8 → 0.62(눈에 보이는 불이 계산 거리보다 짧다 — 알파가 끝에서 꺼지고 끌림으로 느려지므로 더 길게 세운다) · 색 주황-빨강 계열(시작 (1, 0.6, 0.28), 수명 밝은 주황 → 주황-빨강 → 빨강 → 검붉음, 노랑 단계 없음) · 알파 0.65 까지 1(끝이 일찍 안 꺼지게) · 불머리 주황빛. 인스펙터에서 `muzzleForward` · `visibleFraction` 을 바꾸면 바로 다시 맞춘다.
- **사그라들기(사용자 "사라질 때 중간에 뚝 끊긴다 — 서서히 사그라들게")**: 방출만 끊으면 빠른 입자가 총구 쪽부터 비우고 끌림으로 느려진 끝 덩어리만 적 위에 떠 있다가 수명이 다해 뚝 사라졌다(스크린샷). `ProtoFlamePack.TickFade` — 끊는 순간부터 `fadeOut`(0.45초) 동안 남은 입자 전부를 `GetParticles/SetParticles` 로 매 틀 고친다: 속도 × e^(−fadeDrag·dt)(제자리에 멈춤), 크기 × (1 − dt/남은 시간)(끝에 0), 남은 수명도 같은 몫만큼 당긴다(색 곡선이 검붉게 식고 알파가 꺼진다). 끝나면 `Clear`. 다시 쏘면 페이드를 그만두고 남은 입자는 그 상태로 이어 탄다. 100여 개 입자라 비용은 작다.
- **v7 잔불(사용자 "방출될 때 주변으로 불꽃 파편도 자연스럽게 튀게")**: `FxPackImport.AddEmbers` 가 복사본에 `Embers` 자식을 더한다 — 가산 점(`FxTemplatePrefabs.GlowDot()` = M_Fx_Glow, 블룸을 탄다), **콘 부피**(각 18° · 길이 18 팩 단위 ≈ 사거리, 꼭짓점 총구 0.6 앞)에서 태어나 바깥 + 조금 위로(무작위 0.35 · 구면 0.15) 8~26 으로 튀고, 중력 0.5 로 떨어지며 끌림 1.5 로 느려지고 크기 1 → 0.2 · 색 노란빛 → 주황 → 검붉음으로 식는다. 28/s · 수명 0.5~1.1 · 늘림 빌보드. 자식이라 `ProtoFlamePack` 이 방출 · 사그라들기를 불줄기와 같이 다룬다(Fit 는 ConeVolume 각도를 판정 반각 이상으로만 — 18° 는 그대로).
- **v8(사용자 "파편이 너무 멀리 · 옆으로도 · 불꽃 사라지는 게 느리다(총 내리는데 아직 있다)")**: 잔불 속도 4~12 · 수명 0.35~0.8 · 끌림 2.5 · 콘 35° · 무작위 0.7 · 구면 0.3. 꺼지는 시간은 `ProtoFlamePack` 의 HoldTime 0.30 → **0.25**(발사 간격 0.20 보다 길어야 연사 중 안 끊긴다) + `fadeOut` 0.45 → **0.25**(`fadeDrag` 10) — 마지막 틱 뒤 0.5초면 다 꺼진다.
- **v9(사용자 "불꽃보다 파편이 더 길다")**: 잔불이 태어나는 콘 길이가 불줄기 길이(18)라 거기서 또 날아 끝이 22.8 팩 단위 — 눈에 보이는 불끝(23 × 0.62 = 14.3)의 1.6배였다. 콘 길이 8 · 속도 3~7 · 수명 0.25~0.55 · 끌림 3 → 끝 ≈ 10.5(보이는 불꽃의 73%). **규칙: 잔불 끝 = 콘 위치 + 길이 + v/d·(1−e^(−d·t)) 가 보이는 불끝(이동 거리 × visibleFraction)보다 짧아야 한다.**
- **v10(사용자 참고 영상 gemini_generated_video — "검은 연기도 섞이고 불꽃 파편도 튀고 불꽃도 자연스럽게 생겨서 자연스럽게 사라지게")**: 영상에서 읽은 것 — 총구 바로 앞에 짧은 청백색 심, 크림 · 주황 불덩이가 부풀며 위로 말림, 그 위로 검은 연기가 피어오름, 작은 불똥이 위로 떠오름, 멈추면 불줄기가 총구에서 떨어져 나가 떠오르며 연기로 바뀜(연기 · 불똥은 한참 남는다). 손질: `Head` = 청백색 총구 심(시작 (0.72, 0.84, 1), 수명 0.18~0.3, 청백 → 크림 → 주황, 24/s, 0.5 앞) · `Smoke` = **검은 연기 기둥**(월드 공간, ConeVolume 22° · 길이 9 · 2.5 앞에서 태어나 끌림 1.8 로 불 위에 모이고 속도-수명 y 곡선 5 로 떠오름, 크기 3~5 × 0.45 → 1.6, 수명 1~1.7, 색 검회색 (0.07~0.16) · 태어날 땐 불빛에 붉게, 알파 0 → 0.7 → 0.5 → 0 으로 서서히, 노이즈, 빌보드, fudge −1 로 불 위에 섞임, 14/s — 셰이더 PowDissolve 가 정점색 × 연기 시트라 어둡게 칠하면 검은 연기) · `Embers` 중력 −0.15(떠오름) · 크기 0.16~0.38 · 수명 0.3~0.8 · 30/s · 불줄기 끝 크기 1.5 · 노이즈 1.3. 런타임(`ProtoFlamePack`): 쏘기 시작 `rampIn` 0.15초 동안 불 방출 25% → 100%(툭 생기지 않게) · 멈추면 불 입자만 `fadeOut` 0.3초 동안 수명을 당기며 `fadeDrag` 4(떨어져 나아감) · `fadeRise` 10(떠오름), **크기는 줄이지 않는다**(부풀며 흩어진다) · 멈추는 순간 연기 `stopSmoke` 5덩이 · 연기 · 불똥(`_linger`)은 페이드가 손대지 않아 제 수명대로 떠올라 흩어진다(월드 공간 — 총을 내려도 그 자리에).
> **2026-11-23 지움** — 아래 MSFX 복사본(`Weapons/Flamethrower/MSFX`) · 시트(`Weapons/Flamethrower/Sheet`) · 카탈로그 `flamethrower_jet` · `flamethrower_sheet` · `FlamethrowerSheetSetup` · `ProtoFlameSheetFx` 는 지웠다(지금 화염방사기는 `inferno_jet`). 에디터가 켜질 때 복사본을 다시 만들던 자동 적용도 껐다(`FxPackImport.FlameAutoApply`, 메뉴는 남음). 기록으로만 남긴다 — 파일은 `_Backup/20261123_SujinFolder`.
- **시트 화염(사용자 시트 "이런 이펙트 모양으로", 2026-11-05) — 지금 쓰는 판.** 파티클로 흉내 내지 않고 사용자가 준 30칸 시트(점화 0~4 · 지속 5~18 · 소화 19~29, 투명 배경 webp)를 그대로 재생한다. `ArtSource/VFX/bake_flamesheet.py`(원본 `flamethrower_sheet_source.webp`)가 칸 경계(열 빈틈 296 · 606 · 914 · 1216, 행 빈틈 + 붙은 곳은 가장 성긴 줄 488)로 자르고 **프레임마다 불 뿌리(불 화소의 가장 왼쪽 + 그 6px 안 평균 y)를 공통 노즐 자리에 맞춰** 321×183 칸 5×6 아틀라스(1605×1098, 배율 1)로 굽고 `flamesheet_meta.json`(노즐 u 0.1246 · v 0.5519, 지속 불끝 u 0.8805, 가로세로비 1.754)을 쓴다. `Editor/Fx/FlamethrowerSheetSetup`(에디터가 뜰 때 판마다 한 번 + 메뉴 **화염방사기 시트 화염 다시 잇기**)이 임포트(밉맵 끔 · 클램프 · NPOT 그대로) · 머티리얼 `M_Flamethrower_Sheet`(셰이더 `Proto/FlameSprite` — **블렌드를 프로퍼티로 열었다**, 기본은 예전 가산, 이 머티리얼은 알파 블렌드라 그을음 테두리가 남는다 · ZTest Always) · 프리팹 `FX_Flamethrower_Sheet`(루트 `ProtoFlameSheetFx` + 자식 Quad) · 카탈로그 `flamethrower_sheet` · `WFX_Flamethrower.jet = flamethrower_sheet` 를 만든다. 런타임: `ProtoFlamePack` 이 프리팹에 `ProtoFlameSheetFx` 가 있으면 파티클 대신 그것을 세우고(총구 · 조준 · muzzleForward 는 그대로) 매 틀 `Drive(on)` — 점화(×1.6 빠르게) → 지속(14칸 반복, 18 → 5 도 셰이더가 섞음) → 소화(×1.8 빠르게, 끝나면 숨김), 소화 중 다시 쏘면 점화 2칸째부터. 쿼드는 조준축 빌보드(ProtoFlameSprite 와 같은 수법), 길이 = 사거리 / (불끝 u − 노즐 u) — 지속 프레임의 불끝이 사거리 끝, 세로 = 시트 비율 × `thickness` 0.85, 밝기 `intensity` 1.25(노란 심이 블룸). 파티클 판(`flamethrower_jet`, 위의 v1~v10)은 그대로 남아 있다 — 되돌리려면 `WFX_Flamethrower.jet` 을 `flamethrower_jet` 으로(FxPackImport 는 이제 jet 이 비었을 때만 채운다).
- **화면 미확인.** [무기] 탭에서 화염방사기를 들고 쏘면 `_FlamePack/Jet` 아래 시트 쿼드(또는 파티클 판이면 네 파티클)가 돈다. 너무 짧거나 길면 `visibleFraction`, 가늘면 `coneFraction`, 땅을 뚫으면 `flatten`, 전체 크기는 세트 `jet.scale`. 맞는 값은 `ProtoFlamePack` 기본값에 적는다.

#### 5-1-c. 인페르노(화염방사기 진화) — 컨셉 아트를 파티클로 (2026-11-06)

사용자: 컨셉 아트(카툰 화염방사기 — 1 Charge · 2 Start · 3 Sustain · 4 Fade Out, Top View, Additional Effects) "이 불 이펙트로, **Texture Sheet 애니메이션 말고 파티클로**". 그래서 텍스처는 전부 **한 장짜리 모양**이고(플립북 칸 없음) Texture Sheet Animation 모듈은 하나도 안 쓴다. 움직임 · 색은 크기 · 회전 · 수명 색 · 노이즈 · 끌림이 만든다.

- 텍스처: `ArtSource/VFX/bake_inferno_textures.py` → `Art/Sujin/VFX/Weapon/Inferno/T_Inferno_*.png`(11-23 옮김) 7장(RGB = 명암, A = 모양). **v2(11-06, 사용자 "텍스쳐가 너무 2D 그림 — 좀 더 리얼하게")** 부터 원을 겹친 카툰 모양이 아니라 **펄린 노이즈로 깎는다** — fBm + 도메인 워프(소용돌이) + 난류 Σ|노이즈|(뭉게). Puff 512(가운데는 꽉, 가장자리는 노이즈에 갉혀 가닥, 명암 0.18~1 = 뜨거운 소용돌이 · 식은 골) · Tongue 256(위로 가늘어지며 흔들리는 불꽃, 세로 결 · 밝은 심) · Smoke 512(흐린 높이장의 기울기로 위 왼쪽에서 비친 부드러운 명암 — 흐리지 않으면 돌 표면처럼 보였다) · Glow · Dot · GroundFire(불씨 얼룩 + 숯불 금, 고리 아님) · Ring(끊기는 열 고리). 노이즈는 몸통 안쪽에만 걸어야 조각이 떨어져 떠다니지 않고, 잔결을 너무 잘게 하면 작게 그려질 때 지글거린다. 이름이 같아 GUID · 머티리얼 연결은 그대로 — 스크립트를 다시 돌리면 텍스처만 바뀐다(머티리얼 · 파티클 값을 고쳤으면 판도 올린다).
- v2 머티리얼 · 값: 불 뭉치 `_BaseColor` 1.45(어두운 골이 생긴 만큼, 밝은 곳은 1 을 넘겨 블룸), 그을음 · 연기는 보라 → 숯 회색, Licks 는 가산(빛나는 가닥), 렌더러 `flip` 으로 입자 반을 뒤집어(둥근 것은 위아래도, 불꽃 혀는 좌우만) 한 장짜리의 반복을 숨긴다. 본체 · 그늘은 갓 나온 작은 뭉치를 옅게 들여(총구 쪽은 가산 심 줄기가 앞선다 — 작은 뭉치가 팝콘처럼 보였다), 어린 색은 덜 하얗게(반투명 노랑이 회색 바닥 위에서 올리브로 떴다 — HDR 이 꺼진 Performant 품질에서 특히).
- **v3(11-06, 사용자 "너무 실사 느낌 — 좀 더 캐쥬얼하게") = 지금 텍스처.** v1(카툰 그림)과 v2(실사) 사이 — 모양은 동글동글한 혹 덩어리(원들의 **부드러운 합집합**: 혹은 남고 이음매만 둥글게, 느린 노이즈로 윤곽만 살짝 흔듦), 명암은 윤곽에서 가운데로 완만하게 밝아지는 기울기 + 혹마다 부드러운 하이라이트(혹 사이 골만 조금 어둡게), 가장자리는 넓게 번짐. 가닥 · 잔결 · 테두리 선 없음. 불꽃 혀는 매끈한 S 자 물방울 + 밑동 쪽 밝은 심, 바닥 불은 부드러운 얼룩 + 불씨. Puff · Smoke 256.
  - **빛 방향을 굽지 않는다**: 본체 · 그늘 · 연기는 시작 회전 0~360° + 회전 속도 + 위아래 flip 이라, 위 왼쪽 빛을 구우면 입자마다 빛이 엇갈린다(구 합집합 법선 명암을 해 봤다가 버림 — 정면 빛도 가파른 윤곽이 어두워져 테두리 선이 됐다).
  - **가장자리가 안쪽보다 어두우면 안 된다**: 밝은 바닥 위 · 겹친 뭉치마다 테두리 선(v1 그림 느낌)이 된다 → 불 뭉치 명암 0.78~1, 연기 0.68~1. 겹친 불 뭉치 사이에 보이는 비늘 같은 경계는 뒤의 그늘(Shade) 뭉치가 비쳐 보이는 것 — 캐주얼 불의 겹 느낌이라 둔다.
  - 머티리얼만: 불 뭉치 `_BaseColor` 1.1(밝아진 텍스처에 맞춰), 연기 `_BaseColor` (1.5, 1.42, 1.65) = 시작 색(숯 회색) × 밝은 보랏빛 → 캐주얼한 연기. 사용자가 v2 프리팹을 손으로 고쳐 둔 상태(ChargeFlash · ChargeSparks · ChargeRing · GroundFlames 끔, 16:16)라 **손편집 보호 경로로 프리팹은 그대로**, 메인 에디터 로그 `화염 굽기 v2 → v3 (텍스처 · 머티리얼만)`.
- **v4(11-06, 사용자 "불꽃과 연기 텍스쳐에 검은색 테두리 같은 건 빼줘. 경계면은 페더 먹은 것처럼 그라데이션. 불씨 같은 건 서클로") = 지금.**
  - **페더 = 모양 마스크의 가우스 흐림**(포토샵 페더와 같다, `soft_cloud(feather=σ)` 불 0.065 · 연기 0.08 · 바닥 0.09). 윤곽에서 안쪽 거리로 알파를 올리는 방식은 혹 사이 이음매가 얇아 꽃잎처럼 떨어졌다. 혀는 폭의 바깥 55% · 밑동 · 끝이 그라데이션.
  - **명암은 가장자리가 늘 밝다**(`edge_safe` — 안쪽 혹 사이 골만 살짝 어둡게). v3 의 검은 테두리처럼 보이던 것 = 가장자리 명암(0.68~0.78)이 안쪽보다 어두웠던 것 + 좁은 페더(10px)로 뒤의 어두운 그늘 뭉치가 띠로 비친 것.
  - **불씨 = 원**: 새 `T_Inferno_Ember`(반지름 0.48 까지 꽉, 0.88 까지 페더) + `M_Inferno_EmberDot`(가산 1.4) — Embers · Fragments 가 쓴다(v3 까지 물방울 혀), Fragments 크기 0.12~0.26. 불꽃 혀(Licks)는 그대로 혀. 바닥 불 텍스처의 불씨도 서로 안 겹치게 흩은 동그란 원 9개.
  - 손편집 프리팹(사용자가 v4 직전 GroundFire · GroundFlames · ChargeRing 을 꺼 둠)에는 `InfernoFlameSetup.PatchPrefab` 이 **그 두 렌더러 머티리얼만** 바꾼다 — 옛 생성 기본값(M_Inferno_Ember / M_Inferno_Tongue, Fragments 0.3~0.6)일 때만, 프리팹 모드로 열려 있으면 닫힐 때까지 미룬다. 임시 프로젝트에서 v3 모양으로 되돌린 프리팹에 돌려 확인(끈 오브젝트 · Licks 그대로).
- **v5(11-06, 사용자 "불씨가 너무 물방울 모양인데 좀 더 티끌처럼") = 지금 불씨.** 메인 에디터는 프리팹 모드가 열려 있어 v4 원 연결이 미뤄진 채 옛 물방울 혀를 보고 있었다 — **좁은 고침을 미루면 사용자가 바뀐 걸 못 본다.**
  - `T_Inferno_Ember` = 작고 단단한 심(반지름 0.15~0.24, 살짝 울퉁불퉁) + 옅은 번짐 0.3 — 완전한 원 · 넓은 페더는 구슬/물방울처럼 보였다. `M_Inferno_EmberDot` 밝기 2.0(작아서).
  - Embers 0.05~0.12m · 초당 40, Fragments 0.04~0.1m, 멈출 때 파편(`knobs.stopFragments`) 36.
  - `PatchPrefab` 은 이제 **프리팹 모드가 열려 있으면 그 열린 프리팹에 바로** 넣는다(`Undo.RecordObject` — Ctrl+Z, 자동 저장이 켜져 있으면 저장). 닫혀 있으면 파일에서 고쳐 저장. 고치는 건 옛 생성 기본값 그대로인 값만(머티리얼 M_Inferno_Ember/Tongue, 크기 0.15~0.32 · 0.3~0.6 · 0.12~0.26, 초당 24, 파편 16). 임시 프로젝트에서 `PrefabStageUtility.OpenPrefab` 으로 연 프리팹에 돌려 확인.
- 조립: `Editor/Fx/InfernoFlameSetup` → `FX_Inferno_Jet.prefab` + 머티리얼 `M_Inferno_*` 9개(URP Particles/Unlit, **모두 큐 3000** — 앞뒤는 렌더러 fudge, 가산은 `_BaseColor` 1~2.2 로 밝기를 올린다). 값은 **미터**(인페르노 사거리 6m 에서 루트 배율 1.0, 본체 꽉 찬 끝 ≈ 총구 앞 5.4m).

| 시스템 | 컨셉의 어디 | 요점 |
|---|---|---|
| 루트(본체) | 지속 불기둥 | 알파 Puff, 12~15m/s · **속도 배수 1 → 0.15**(끝에서 느려지며 뭉친다 — 끌림 아님, 아래 함정) · 0.2m → 1.5~2m, 노란-흰 → 노랑 → 주황 → 붉은 주황 → 검붉음 → 그을음(v1 보랏빛), 어린 입자가 앞(YoungestInFront) |
| Shade | 아랫면 · 바깥 말림 그늘 | 본체 뒤(fudge 2) 더 크고 어두운 붉은 뭉치 |
| Core · Head | 총구 쪽 흰 심 · 총구 불 | 가산 Glow. Core 는 상자 모양(콘이 아니라 판정 폭으로 안 넓어진다), Head 는 로컬 — 멈춘 뒤 잠깐 남는다 |
| Licks | 가장자리 불꽃 혀 | 본체 뒤에서 위로(±40°), 총구 앞에선 작게, v2 부터 가산 |
| Embers · Sparks | 둘레 불꽃 조각 · 불똥 | 가산 혀(콘 부피의 바깥 껍질에서) · 늘린 점 |
| Smoke | 끝의 연기(v2 숯 회색) | 방출 0 — ProtoFlamePack 이 불 입자가 수명 `tipAge` 0.68 을 넘는 자리에서 낸다, 불 뒤(fudge 4) |
| Fragments | 4 Fade Out 의 조각난 불 | 방출 0 — 멈출 때 몸통에서 |
| ChargeFlash · ChargeSparks · ChargeRing | 1 Charge | 쏘기 시작할 때 한 번(버스트 수를 `ProtoFlameJetTuning` 이 읽어 Emit) |
| GroundGlow · GroundFire · GroundFlames | Additional Effects 바닥 불 | 소유자 발 높이로 내린다, 쏘고 0.25초 뒤부터(불줄기가 닿기 전엔 안 탄다), 불끝 너머까지 · 월드 공간이라 지나간 자리에 1~1.6초 남아 타다 꺼진다 |

- **새 런타임 `Scripts/Proto/ProtoFlameJetTuning`** (프리팹 루트): ① `Knobs` = ProtoFlamePack 맞춤값(visibleFraction 0.53 · coneFraction 0.75 · flatten 0.4 · tipAge 0.68 · 끝 연기 0.45 · 멈출 때 연기 8/파편 16/불똥 30 …) — `ProtoFlamePack.Build` 가 이 컴포넌트를 보면 제 값을 바꾸고, 없는 프리팹으로 돌아가면 처음 값으로 되돌린다(화염방사기 팩 복사본 값은 그대로). ② `Charge…` 점화 버스트 ③ `Ground…` 바닥 자리 · 지연. 이름 규칙이 약속이고, ProtoFlamePack 은 `Charge…` · `Ground…` 를 페이드에서 뺀다(`_linger`).
- 적용: 카탈로그 `inferno_jet`(스팬, 수명 0) · `WFX_Inferno.jet = inferno_jet`(비어 있을 때만). 에디터가 뜰 때 판(`InfernoFlameSetup.Version`)이 오르면 다시 굽고(표식 `Library/InfernoFlame.applied`, 플레이 중이면 끝난 뒤), 메뉴 **인페르노 화염 다시 굽기 (파티클)**. **손편집 보호**: 프리팹 파일이 표식보다 새것이면(에디터에서 고쳐 저장) 판이 올라도 텍스처 · 머티리얼만 바꾸고 프리팹은 둔다(표식 `Library/InfernoFlame.handedited` 가 남아 다음 판에도 — 메뉴 다시 굽기만 지운다). 인스펙터로 열기만 해도 Unity 가 안 쓰는 곡선 모드를 맞추며 저장할 수 있으니, 판이 안 먹으면 콘솔 경고를 보고 메뉴로. 되돌리기: `WFX_Inferno.jet` 을 비우면 예전 `ProtoFlameCone`(파란 flameTint).
- **미리보기 렌더**: 메뉴 **인페르노 화염 미리보기 렌더**(또는 `Library/InfernoPreview/request` 파일) — 숨은 미리보기 씬에서 같은 프리팹을 `ProtoFlamePack.Configure` 로 세우고 ProtoFlamePack 의 흐름(램프 · 끝 연기 · 멈출 때 파편 · 사그라듦)을 흉내 내며 1/60초씩 Simulate 해 `Library/InfernoPreview/inferno_preview.png` 로 찍는다(옆모습 크게 / 점화 · 시작 · 지속 · 사그라듦 / 게임 각도 58° 지속 · 사그라듦). 블룸이 없어 게임보다 심이 덜 빛난다.
- 확인: FX 툴 [무기] 탭 → 무기 **인페르노** → 쏘기. 길이는 `ProtoFlameJetTuning.knobs.visibleFraction`(작을수록 길다), 폭은 `coneFraction`, 전체 크기는 `WFX_Inferno.jet.scale`. 맞는 값은 `InfernoFlameSetup.Knobs()` 에 적고 판을 올린다.
- **함정(11-06, 임시 프로젝트에서 입자 하나로 잼)**: ① Unity 의 끌림(`limitVelocityOverLifetime.drag`)은 **지수 감쇠가 아니다** — 속도 곱을 끄면 등감속(끌림 2 = 매초 2m/s 씩, 15m/s 불이 거의 안 느려진다), 켜면 제곱 감속(거리가 루트 배율을 거의 안 따라간다). `ProtoFlamePack.Travel` 의 v/d·(1−e^(−dt)) 는 둘 다와 다르다(팩 복사본 화염의 visibleFraction 손질이 이 차이를 메운 것). 인페르노 불줄기는 끌림 대신 **속도-수명 speedModifier** 로 늦춘다 — 거리가 배율에 정확히 비례(1 → 1.13 배율에 6.03 → 6.82m). 단 `Particle.velocity` 는 배수 전 값이라 끝 연기 · 멈출 때 파편이 물려받는 속도가 크다 → 연기 · 파편 · 불똥은 제곱 끌림으로 금방 선다. ② 상자 모양의 `randomDirectionAmount` 는 모양 크기를 곱하기 **전에** 섞여, z 크기 0 인 상자에 넣으면 옆으로만 튄다(첫 미리보기에서 심이 총구 위아래로 흩어졌다) — 상자엔 0. ③ `boxThickness` 0 = 겉면, 1 = 부피.

**다음 무기를 붙이는 순서** (파일 넷, 게임 코드 분기 없음):
1. `FxTemplatePrefabs.Templates` 에 `FX_<무기>_Muzzle / Hit / Kill` 세 줄(폴더 `WeaponDir/<무기>`) + 조립 함수 — 소총 것을 복제해 색·크기·개수만 바꾼다.
2. `FxIds` 에 상수 셋, `FxAssetsSetup.RequiredIds` 와 `FillCatalog` 에 항목 셋(수명·풀·동시 상한·쇼케이스 오프셋). 총구에는 `builtin` 폴백을 같이 적는다.
3. `WFX_<무기>.asset` 의 `muzzle.fxId`·`hit.fxId`·`kill.fxId` 를 새 ID 로 — 인스펙터에서 프리팹을 놓아도 같은 결과다.
4. Unity 포커스 → 리컴파일 → `AutoCreate` 가 템플릿·카탈로그를 채운다 → FX 툴 창 [무기] 탭에서 그 무기를 세워 본다.
5. 조립 함수의 값을 고친 뒤에는 **"… 템플릿 다시 굽기 (덮어쓰기)"** 메뉴 — 있는 프리팹은 안 건드리는 게 기본이라 그냥은 안 바뀐다.

#### 5-1-f. 소총 총구 — 화려한 판 (2026-11-18)

사용자: "총 쏠때 총구에 이펙트를 좀더 화려하고 눈에 띄게". 지금 총구(사용자 MSFX 복사본 `FX_Rifle_TangTang_Muzzle`)는 0.05초 작은 별이라 게임 화면에서 30px 남짓이었다.

- 새 프리팹 `Art/Sujin/VFX/Weapon/Rifle/FX_Rifle_TangTang_Muzzle_Flashy.prefab`(11-23 옮김) = 사용자 프리팹 **복사본**(원뿔 2 · 가운데 · OneShot · 연기 그대로) + 루트 배율(0.14)을 상쇄한 `Flashy` 묶음(미터 × 슬롯 크기)에 층 6개:
  흰 중심 섬광 `CoreFlash`(0.55, 0.08초) · 새하얀 심 `HotCore`(0.26) · 노랑 별 `StarBurst`(PistolHitCenter, 1.2, 무작위 회전) · 흰 반짝 별 `Sparkle`(LightningSparkleHit, 0.85) ·
  양옆 불꽃 `SideFlareL/R`(±72°, 늘인 빌보드) · 앞으로 불똥 `Sparks`(9개, 콘 24°, 4~9m/s, 0.1~0.19초). 모두 0.2초 안에 끝나 0.35초 연사와 안 겹친다.
- 머티리얼 `M_MSFX_Flashy_Glow / Star / Sparkle`(URP Particles/Unlit 더하기, 부드러운 입자 끔, 큐 3000).
- 카탈로그 새 항목 `FX_Rifle_Muzzle_Flashy`(수명 1 = 연기, 풀 4, 항상 위에 끔) ← `WFX_Rifle_TangTang.muzzle`(크기 1.5 그대로). 예전 항목 `FX_Rifle_Muzzle` 은 남아 있어 ▾ 로 되돌릴 수 있다.
- 임시 프로젝트에서 굽고(`MuzzleFlashy.cs`) 전/후 검수(`_Review/20261118_RifleMuzzleFlashy/`) 뒤 사용자 "적용해줘". 백업 `_Backup/20261118_RifleMuzzleFlashy/`(WFX · FxCatalog). 게임 플레이로는 아직 미확인.
- 앞으로 뻗는 빛줄기(늘인 빌보드)는 총구와 떨어진 타원이 두 번째 총알처럼 보여 뺐다.

#### 5-1-e. 크기 · 항상 위에 (2026-11-13)

사용자: "FX_Rifle_TangTang_Muzzle 1.5배 · FX 툴에 사이즈 수정 기능 · 이펙트는 총보다, hit 는 좀비보다 위로". 사용자가 MSFX 복사본 이름을 `FX_Rifle_TangTang_*` 로 바꾸고 소총 세트 슬롯에 프리팹을 놓아 새 카탈로그 항목(`FX_Rifle_Muzzle` → FX_Rifle_TangTang_Muzzle · `FX_Rifle_TangTang_Hit_2` → FX_Rifle_TangTang_Hit, 처치 `fx_zombie_death`)이 생겼는데, 놓아서 생긴 항목은 '항상 위에'가 꺼진 채라 총구가 총에, 착탄이 좀비 몸에 가려졌다.

- 데이터: `WFX_Rifle_TangTang.muzzle.scale` 1 → **1.5**(프리팹은 사용자가 손보는 파일이라 안 건드림). 카탈로그 `FX_Rifle_Muzzle` · `FX_Rifle_TangTang_Hit_2` · 후보 `FX_Rifle_Hit` · `FX_Rifle_TangTang_Hit` 의 `onTop` 0 → 1 로 켰다가 **같은 날 사용자 "그냥 되돌려줘"로 0 으로 되돌림**(총구 1.5 · 툴 기능은 남김, 놓을 때 자동으로 켜던 기본값도 뺌). 백업 `_Backup/20261113_RifleFxOnTopSize/`.
- 툴: 위 파일 지도(WeaponFxSlotDrawer · FxCatalogEdit · ActionFxEditorWindow). 크기는 두 층 — 한 무기의 한 자리 = 슬롯 '크기'(ctx.scale), 효과 자체 = 카탈로그 탭 '크기 (프리팹 루트)'.
- 남은 것: 놓아서 생긴 네 항목의 수명은 옛 재기 방식 값(5.2 ~ 6초, 동시 상한 0 = 무제한)이 그대로다 — 카탈로그 탭 '다시 재기'로 고칠 수 있다. '항상 위에'는 플레이 중에만 먹는다(오버레이 카메라).

#### 5-1-d. 캐릭터 기본 무기 — Unique Toon Projectiles BallRed (2026-11-12)

사용자: "캐릭터 기본 무기를 UniqueToonProjectilesVol_1 의 vfx_ToonHit_BallRed 로, 이름이 맞는 Muzzle · Projectiles 도". 기본 무기 = 루키 · 리퍼 프리팹 소켓과 `C_01_Rookie` · `C_05_Reaper` 가 드는 `W_Rifle_TangTang`(→ `WS_Rifle_TangTang` · `WFX_Rifle_TangTang`). `Editor/Fx/FxToonPackImport.cs` 가 MSFX 때(§5-1)와 같은 길로 복사본을 만들어 건다.

| 팩 원본 (`GabrielAguiarProductions/UniqueToonProjectilesVol_1/Prefabs/`) | 복사본 (`Art/VFX/Weapons/Rifle/Toon/`) | 어디에 | 루트 |
|---|---|---|---|
| `Muzzle/vfx_ToonMuzzle_BallRed` | `FX_Rifle_Muzzle_ToonBallRed` | 카탈로그 `rifle_muzzle`(수명 1.3) | 0.14 |
| `Hits/vfx_ToonHit_BallRed` | `FX_Rifle_Hit_ToonBallRed` | 카탈로그 `rifle_hit`(수명 1.7) | 0.2 |
| `Projectiles/vfx_ToonProjectile_BallRed` | `FX_Rifle_Bullet_ToonBallRed` | ~~`WS_Rifle_TangTang.projectileModel`~~ — 11-13 사용자가 소총 탄을 Survivor Ace `Bullet_miniplane1_1` 로 바꿈(복사본은 남아 있다). 이 도구는 이제 소총 탄이 MSFX · Toon 이 아니면 탄은 건드리지 않는다 | 0.636 (× projectileSize 0.22 = 0.14) |

- **복사하는 이유**: 팩 탄 루트에 데모용 `ProjectileMoveScript` · Rigidbody · BoxCollider — 그대로 달면 탄이 스스로 날고 부딪혀 제 총구 · 착탄을 낸다. 복사하며 뗀다(팩 크기 · 색 도구 `ParticleSystemController` 는 런타임 동작이 없어 남긴다). 원본은 GUID 로 찾고, **머티리얼 · 텍스처 · 셰이더는 팩 것을 그대로**(URP 판 셰이더 그래프 `Soo_VFX/GabrielAguiarProductions/Shaders/AB_Unlit`, 투명 큐 3000 — 밤 어둠 막 2900 뒤라 밤에도 빛난다). 팩을 프로젝트 창에서 옮겨도 산다, 지우면 분홍.
- **배율**: 팩 1 유닛 = 0.14m(총구 섬광 Beam 4 ≈ 0.56m — MSFX 총구 0.49m 와 비슷, 탄 구슬 ≈ 0.18m). 착탄만 0.2 — 0.14 면 덩이가 0.35m 남짓으로 11-04 "너무 안 보인다"와 같은 크기라 키웠다(덩이 ≈ 0.5m · 섬광 ≈ 1.4m · 고리 ≈ 0.8m). 파티클은 Hierarchy 스케일 — 월드 공간 입자의 속도도 배율을 따라 줄어든다(원본 대비 거리 정확히 ×0.140 확인).
- **탄 꼬리 시간 배율**: 팩 데모 탄 35 유닛/s 인데 우리 탄은 24m/s ÷ 0.14 ≈ 171 유닛/s(제 크기 대비 k ≈ 4.9 배). 그대로면 꼬리가 2.5m 점선이 된다 → 꼬리 파티클 4개(월드 공간)의 `simulationSpeed` × k — 방출 · 수명 · 불똥 속도가 한꺼번에 맞아 꼬리 ≈ 0.7m(공 지름의 4배, 팩 데모와 같은 비율). `projectileSpeed` 를 바꾸면 메뉴로 다시 굽는다.
- **방향**: 총구 +Z = 총열(팩도 +Z 로 쏜다), 착탄은 팩 그대로(팩 +Y = 위로 튐, +Z = 사수 쪽으로 불똥) — 회전 보정 없음.
- **자동 적용**: 에디터가 뜰 때 표식 `Library/FxToonPackImport_BallRed.applied` 의 판(`Version`)이 낮으면 굽고 건다(플레이 중이면 끝난 뒤). 손으로 고친 복사본(표식보다 새 파일)은 판이 올라도 자동으로 안 덮는다. 되돌리기 메뉴는 MSFX 복사본(`Rifle/MSFX/FX_Rifle_*`)으로 카탈로그 · 탄을 되돌린다. 백업 `_Backup/20261112_RifleToonBallRed/`(카탈로그 · WS · WFX · 팩 스크립트 원본 + 비교 렌더 `compare_msfx_vs_toon.png`).
- **팩 스크립트 컴파일 오류 고침**: 팩의 `Scripts/UniqueProjectiles/ProjectileMoveScript.cs` 가 Unity 6 API `rb.linearDamping` 을 써서 2022.3 에서 **프로젝트 전체 컴파일이 막혀 있었다**(팩 임포트 직후부터, CS1061). `#if UNITY_6000_0_OR_NEWER` 로 감싸 2022 에선 `rb.drag`. 팩을 다시 임포트하면 되살아난다.
- 확인: 임시 URP 프로젝트에서 복사 코드를 그대로 돌려 구성 · 비율 · 한 발 타임라인(총구 → 탄 → 착탄)을 MSFX 와 나란히 렌더. **메인 에디터 플레이 화면은 미확인.** 크기는 FX 툴 [무기] 탭의 `WFX_Rifle_TangTang` muzzle · hit 슬롯 배율로, 탄은 projectileSize 로 더 맞춘다.
- 임시 프로젝트 함정: 프로젝트 경로가 길면(> 약 120자) 셰이더 그래프 패키지 템플릿 경로가 260자를 넘어 임포트가 실패 → 팩 셰이더가 분홍, 머티리얼 큐도 2000 으로 읽힌다(가짜 문제). 짧은 폴더 이름 + 셰이더 그래프 · URP · core 를 짧은 경로의 `file:` 로컬 패키지로 두면 된다.

#### 5-2. 처치 파편 — ZombieGibs (2026-11-05)

사용자: "적이 죽으면 피 터지며 파편이 터지는 이펙트 — 기존 좀비 캐릭터를 터지게". 몸 조각(코드)과 피(프리팹)로 나눴다.

- **몸 조각 = 코드 그림** `Scripts/Fx/ZombieGibs.cs`. 좀비 스킨 메시의 삼각형을 "가장 무거운 본"으로 묶고(정점이 많은 본 `MaxChunks` 8개, 나머지 본은 가장 가까운 조상 조각에), **그 조각을 삼각형 무게중심 기준으로 가장 긴 축에서 반으로 거듭 가른다** — 한 변이 `PieceSize`(0.16m, 본 스케일을 곱해 월드 기준) 아래거나 조각 수가 `MaxPieces`(26)에 닿을 때까지(2판, 사용자 "좀 더 잘게"). 조각은 표면 껍질이라 그대로면 종이처럼 보인다(사용자 "종이짝처럼 — 묵직하게 덩어리감") — **두께**(`Thickness` 6cm): 안쪽 껍질(법선 반대로 들여놓은 사본, 삼각형 뒤집음) + 옆면(조각 안에서 한 삼각형에만 쓰인 경계 변마다 바깥·안쪽 정점을 잇는 사각형, 조각 중심에서 바깥을 보는 평면 법선)을 붙이고 안쪽·옆면은 정점색 살색(`FleshColor`, 셰이더가 알베도에 곱함). 조각 메시는 **메시마다 한 번** 만들어 캐시한다(정점을 바인드 포즈로 본 로컬 좌표에 옮긴 것). 죽는 순간 각 조각을 그 본의 **지금** 월드 자리·회전에 놓으면 죽을 때 자세 그대로 쪼개진다 — 매 죽음마다 메시를 굽지 않는다(물량전). 구성은 처음 만들 때 콘솔 `[파편] 메시: 조각 N개 (… 본 조각 G, 조각 크기 …) — 본×수`. 움직임은 코드(강체 없음): 몸 가운데서 바깥 + 위 + 맞은 방향으로 포물선(세기 5 — 큰 조각은 느리게 0.7배, 작은 조각은 빠르게 1.3배 — 월드 반지름 9cm 기준 질량감), 중력 18, 회전 200°/s(바닥에 닿으면 0.3배 → 멈춤), 바닥에서 한 번 낮게 튀고(0.25) 눕고, 수명(1.8초 ±20%)의 마지막 `DissolveTime`(0.7초) 동안 **디졸브**로 녹아 사라진다(2판 — 줄어드는 게 아니라). 기본 조각 크기 0.2m · 최대 20(3판에서 0.16/26 → 덩어리감 쪽으로). 4판: 3판(세기 4 · 중력 22)은 제자리에 떨어져 "조금은 날아가게" → 세기 5 · 중력 18 · 수명 1.8(날아가는 거리 약 2배). 풀 · 동시 400개 상한. 적 모델 3종(워커 `Zombie Attack.fbx` · 개 `dog_01_set.fbx` · 원거리 `Standing 2H Magic Attack 01.fbx`, 전부 BCM 툰 셰이더)은 `Editor/Fx/ZombieGibsSetup.cs` 가 Read/Write 를 켠다.
- **조각 재질 = 디졸브 셰이더** `Art/VFX/Shaders/GibDissolve.shader`(`ZombieOverrun/GibDissolve`, URP HLSL): 좀비 툰 머티리얼의 `_BaseMap` · `_BaseColor` · `_HColor`(하이라이트) · `_SColor`(그림자)를 받아 두 단계 셀 셰이딩으로 그리고, `_Dissolve`(0→1)에 따라 조각 로컬 좌표의 값 노이즈(텍스처 없음)로 녹는다(가장자리 `_EdgeColor` 검붉은색). 양면(Cull Off) — 안쪽이 뚫려 보이지 않게. 큐는 몸과 같은 2450(밤 어둠 막 앞). 바탕 머티리얼 `Resources/VFX/M_GibDissolve.mat` 은 `ZombieGibsSetup` 이 만들고(Resources 에 두는 이유: 셰이더가 빌드에 들어가게), 런타임은 좀비 머티리얼마다 복제해 조각마다 MPB 로 `_Dissolve` · `_NoiseOffset` 을 준다. 없으면 줄어들며 사라지는 옛 방식.
- **피 = 카탈로그 프리팹** `zombie_gore`(템플릿 `FX_ZombieGore`, 2판 "더 리얼하게 · 바닥에 흩어졌다 사라지게"): 터짐은 팩(MasterStylizedFX)의 피 스프라이트 시트 `CommonBloodSplash`(4×4, 0~11 프레임: 퍼지다 흩어진다)를 **복사해**(`T_MSFX_BloodSplash`, 1024, 원본은 안 건드린다 — 사용자 규칙) 맞은 쪽으로 2~3장 돌리고(`Splash`), 핏방울 줄기 `Jet`(맞은 쪽 콘 16~22) · 사방 `Spray`(14~20) · 긴 가닥 `Strands`(4~7, 늘림 ×5) · 피 안개 `Mist`(2장)를 겹친다. 바닥(`GroundSplats`, -0.78)은 시트의 **흩어진 프레임(5~9)을 고정 그림**으로 8~12장을 반경 0.9m 원 안에 흩뿌리되 버스트를 0.05 · 0.2 · 0.4초로 나눠(떨어져 닿는 시간차) 3.5~4.5초 뒤 알파로 사라지고, 가운데 큰 웅덩이 `Pool`(프레임 1~3) 한 장은 4.2~4.8초. 카탈로그 수명 5.2초(옛 항목은 `FxAssetsSetup.Upgrades` 가 늘린다). 머티리얼 `M_Fx_BloodSheet` · `M_Fx_BloodDrop` · `M_Fx_BloodMist`(알파 블렌드, 밤 어둠 막 앞 큐 2850). 팩이 없으면 얼룩 한 장(`T_Fx_Splat`)으로 떨어진다. **옛 FX_ZombieGore 는 에디터가 뜰 때 `FxTemplatePrefabs.RebuildOutdated`(판 표식 자식 `_v3` 없음)가 대화상자 없이 덮어쓴다 — 조립을 고치면 표식 이름을 `_v4` 로 올리고 조립 함수의 `Child(root, "_v4")` 도 같이.** 함정: 텍스처 시트의 `startFrame` 은 `frameOverTime` 처럼 **0~1 정규화**(× 전체 프레임)다 — 2판에서 프레임 번호(5~9)를 그대로 넣어 마지막 빈 칸에 고정되는 바람에 바닥 피가 안 보였다(3판에서 고침, `Sheet` 헬퍼가 나눈다). 4판: 그래도 "안 보인다" — 디스크의 프리팹·머티리얼·카탈로그는 다 맞아(시트 4×4, 시작 프레임 0.31~0.62, 수명 5.2, 바닥 큐 Geometry) 읽히지 않는 쪽(어두운 바닥 위 어두운 빨강, 시트 칸의 그림이 칸의 반)으로 보고 **핏자국 0.6~1.2m · 밝은 빨강(GroundRed) + 단색 얼룩 `GroundStains` 5~7장 + 웅덩이 단색 1.2~1.5m** 로 키웠다. 무대는 처치 0.6초 뒤 **바닥 피 진단**(핏자국·단색·웅덩이 입자 수, 바닥 y, 인스턴스 활성)을 상태 줄과 콘솔 `[피]` 에 적는다 — 입자 수가 0 이면 데이터, 있는데 안 보이면 그리기 쪽. **5판(사용자 "피 색 더 어둡게 · 적붉은색 · 핏자국 갯수 줄여")**: 피 팔레트를 검붉게(BloodBright 0.62→0.40 · Mid 0.5→0.30 · Dark 0.36→0.20 · GroundRed 0.95→0.50 — 시트가 밝은 빨강이라 입자 색이 곧 최종 밝기의 상한), 바닥 핏자국 8~12 → 4~6, 단색 얼룩 5~7 → 2~3(웅덩이 1장 그대로). 표식 `_v5`. **6판(11-06, 사용자 "좀비 죽을 때 바닥 피가 오랫동안 남는다 — 파편 사라질 때 같이 · 투명도 빼줘, 몹들이 뭉쳐 있을 때 피가 터져 있으니 지저분")**: 바닥 피(핏자국·단색 얼룩·웅덩이) 수명을 몸 조각 수명(`ZombieGibs.Life` 1.8 ±20% = 1.44~2.16초, `GroundLife()`)에 맞춰 조각이 녹는 때 같이 옅어져 사라진다(웅덩이는 상한 쪽 1.8~2.16 — 마지막까지 남는 건 웅덩이 하나). 바닥 피 알파 0.95~1 → `GroundAlpha` 0.6(알파 블렌드라 겹칠수록 진해지니 한 장은 이만큼만). 카탈로그 수명 5.2 → 2.6(`FxAssetsSetup.Upgrades` 2단계가 한 번 내린다, `cat.upgrade = 2`). 표식 `_v6`. 조각 수명(`Life`)을 코드에서 바꾸면 템플릿을 다시 구워야 바닥 피가 따라온다.
- **화상 사망 = 타들어가는 디졸브**(11-10, 사용자 "불타 죽는 좀비는 디졸브로 타들어가는 느낌으로. 경계라인은 add 로 넣어주고 불씨가 튀기고 불꽃도 사그라드는 느낌으로"): 불타 죽은 적은 조각·피 대신 **몸이 머리부터 타 내려가며 사라진다**(그 전엔 0.55초 동안 새까매진 채 쓰러져 2.2초 누워 있었다). 보스는 제외(옛 그을림). `Scripts/Fx/ZombieBurnOut.cs` = 머티리얼과 조절 값(`Duration` 1.15초 · `AnimSpeed` 0.2 · `NoiseScale` 4.5 · `EdgeWidth` 0.08 · `HeightBias` 0.7 — 정적이라 플레이 중 바로 먹는다). `ProtoZombie.Die → BeginBurnOut` 이 렌더러의 머티리얼을 **본체 `M_BurnDissolve`(좀비 머티리얼마다 복제, 큐는 몸과 같게) + 불선 `M_BurnEdge`(가산, 큐 3100)** 로 갈아끼우고 `PushVisual` 이 MPB 로 진행도를 민다, 풀에서 다시 나올 때 `ResetVisual → EndBurnOut` 이 되돌린다. 불선을 두 번째 **머티리얼**로 덧붙인 이유: 밤 어둠 막(2900) 뒤에 그려야 빛나는데 큐는 머티리얼 단위다(서브메시보다 머티리얼이 많으면 Unity 가 마지막 서브메시를 한 번 더 그린다). 셰이더 `Art/VFX/Shaders/BurnDissolve.shader` · `BurnEdge.shader` 는 타는 순서 식을 `BurnDissolveCommon.hlsl` 하나로 같이 쓴다 — 월드 좌표 노이즈(리그마다 로컬 단위가 달라서) + 높이 기울기(머리 먼저, 발밑 마지막). 월드 무늬라 몸이 빨리 움직이면 무늬가 흐르므로 사망 애니를 0.2배속으로 돌린다. **불씨 · 사그라드는 불꽃 = `ProtoBurnFx`**: 불꽃은 기존 몸불 시스템에 불선 아래로만, 갈수록 작고 드물게(`flameEndSize` 0.4 · `flameEndRate` 0.25) 내 바닥에서 꺼지고, 불씨는 코드로 만든 시스템 `Embers`(가산 점 `Resources/VFX/M_BurnEmber`, 늘임 렌더 · 공기 저항 · 음의 중력 · 노이즈)에 불선 높이에서 초당 36알 + 죽는 순간 9알(`EmberBurst`). 옵션 "불타는 몸"을 끄면 불꽃·불씨만 빠지고 디졸브는 남는다. 머티리얼 셋은 `Editor/Fx/ZombieBurnOutSetup.cs` 가 에디터 로드 때 만든다(있으면 안 건드림 — 선 색 `_EdgeColorInner/Outer` · 깜빡임은 `M_BurnEdge` 에서 손본다, 메뉴 "화상 사망 머티리얼 다시 만들기"). 본체가 없으면 옛 연출로 떨어진다. 값은 임시 Unity 프로젝트 렌더로 골랐다: 선 폭 0.16 은 윗몸 전체가 빛나 선이 아니었고 0.06 은 가늘었다, 노이즈를 0~1 로 안 펴면 진행도 70% 에 몸이 다 사라졌다. 게임 화면은 미확인.
- **부르는 곳**: `ProtoZombie.Die` — 보스(자기 연출)와 불타 죽은 적(타들어가며 사라진다 — 바로 위)은 제외. `ZombieGibs.Burst` 가 true 면 몸 렌더러를 숨기고(`HideBody`, 풀에서 다시 나올 때 `ResetVisual` 이 켠다) `zombie_gore` 를 내고 0.4초 뒤 풀로(deathLinger 대신). 처치 슬롯(`kill` / `kill_burst`)은 그대로 위에 얹힌다. `FxWeaponStage.Kill` — 더미(Zombie_Walker)도 같은 길(조각은 코드 그림이라 타임라인 되감기엔 수만 센다, 피는 보인다).
- **착지 피**(5판, 사용자 "조각이 날아가 떨어질 때도 그 조각 주변에 피"): 조각이 처음 바닥에 닿는 자리에 핏자국(조각 반지름 × 2.6~3.8 × `LandingSplatScale`, 0.1~0.7m, 옆에 작은 튄 자국 1~2개)과 위로 튀는 핏방울 3~5개, 튀어서 다른 자리(10cm 넘게)에 누우면 작은 핏자국 하나 더. 핏자국 수명 = 조각 남은 시간 + `LandingBloodLinger`(1.5초 → 6판 0초: 조각이 녹는 동안 같이 옅어져 함께 사라진다, 마지막 1/4 동안 옅어짐). 6판: 알파 0.95 → 0.6(프리팹 바닥 피 `GroundAlpha` 와 같은 값). 5판: 색 검붉게(0.26~0.40), 핏자국은 조각의 `LandingSplatChance`(70%)만 남기고 튄 자국은 열에 넷만 하나, 다른 자리에 누울 때의 작은 핏자국도 20cm 넘게 움직였을 때 35%만 — 핏방울은 늘 튄다. 루트 아래 파티클 시스템 둘(GibSplats 바닥 빌보드 · GibDrops 빌보드+중력 1.3)에 `Emit()` 로만 낸다 — 오브젝트가 안 는다. 머티리얼 `Resources/VFX/M_GibBlood.mat` = `ZombieGibsSetup` 이 템플릿의 `M_Fx_Blood`(얼룩) 를 복제하고 소프트 파티클만 끈 것(바닥 얼룩은 깊이 차 0 — 지금은 깊이 텍스처가 꺼져 있어 URP 가 기본 깊이를 물려 차이가 없지만, 켜는 날 옅어진다). 없으면 경고 한 번, 핏자국 없이 조각만.
- **조절**: [무기] 탭 파편 세 줄(켜기 · 크기 · 최대 · 두께 · 디졸브 / 세기 · 수명 · 중력 · 회전 / 착지 피 · 크기 · 유지, 살아 있는 조각 수, 디졸브·착지 피 머티리얼 유무) — 정적 값이라 플레이 중 바로 먹고 리로드하면 기본값. 맞는 값은 `ZombieGibs` 의 기본값에 적는다. 크기·최대를 바꾸면 캐시를 비워 다음 처치부터 다시 만든다.

## 6. 피해 경로가 넘기는 `HitFx`

| 경로 | 불똥 (hit) | 처치 (kill) |
|---|---|---|
| 투사체 · 광선 · 화염(Cone) · 블레이드 · 헬기 기관포 직격 — 닿은 자리를 안다 | 무기 세트, 착탄점을 **몸 표면으로 당긴 점**(`ProtoZombie.SurfacePoint`, 11-04 — 판정 반경이 넉넉해 총알은 몸 앞에서 맞으므로 그대로 내면 허공에 뜬다) | 무기 세트 |
| 투사체 폭발 · 수류탄 · 폭격 · 기관포 스플래시 — 범위 | 없음 (폭발이 이미 보인다) | 무기 세트 |
| 탱크 깔림·포 · 울트 링/스플래시/연소 · 교체 착지 · 승합차·헬기 추락 폭발 | 없음 | 공통 `kill_burst` |
| 울트 저격 | 공통 `hit_spark` 착탄점 | 공통 |
| `HitFx` 를 안 넘긴 옛 경로 | 공통 `hit_spark` (bigHit 면 없음) | 공통 |

## 7. 액션 시트 (동작 연출)

- 키 하나 = 동작 하나. 이벤트는 **원샷**(그 순간) · **스팬**(time~endTime 켜 둔다) · **마커**(효과 없이 코드에 시각만 알린다 — `impact`, `end`).
- 기준: **Root**(발밑) · **Socket**(본 이름, HammerHead 등) · **Target**(코드가 넘긴 지점, 에디터에선 previewTarget).
- 최종 보스 내려찍기·도약이 첫 이식 예시(`ProtoBossBrain` → `Boss_Final/Slam`, `Boss_Final/JumpSlam`). 피해 판정 시각은 시트의 `impact`·`end` 마커에서 읽는다. 시트에 키가 없으면 예전 코드 `Phases()` 로 돈다.
- FX 툴 창의 [액션 시트] 탭: 액션 +/−, 이벤트 원샷/스팬/마커 추가·복제·삭제·순서, ▶ ■ |◀ 재생과 스크럽, 씬 뷰 핸들. 미리보기는 두 가지 — "미리보기 배치"(지금 씬에 리그를 놓는다, 프리팹 효과만)와 [▶ 무대](쇼케이스 씬을 플레이해 코드 그림까지 전부).
- 이벤트에 효과를 거는 법: 인스펙터의 "프리팹" 칸에 프리팹을 끌어다 놓는다. 끝. ID 는 저절로 채워진다. 코드 그림(폭발 등)을 쓸 때만 "효과 ID" 옆 ▾ 로 고른다.
- [카탈로그] 탭: ID 목록(초록 프리팹 · 회색 코드 그림 · 빨강 비어 있음)을 고르면 바로 재생. "프리팹 → 항목" 칸에 놓으면 항목이 생기고, ID 바꾸기 · + 빈 항목 · − 삭제 · 프리팹·수명·풀·동시 상한·쇼케이스 오프셋을 여기서 고친다.

## 8. 쇼케이스 씬

더미 좀비 턴테이블 위에서 카탈로그·시트 항목을 되풀이 재생하는 **무대**. 시트 동작은 클립을 PlayableGraph(수동)로 돌려 같은 시계로 이벤트를 맞춘다.

**FX 툴 창이 조종한다.** 창의 [▶ 무대] 를 누르면 씬을 열고(없으면 굽고) 플레이하며, 그때부터 창의 액션 선택 · 재생 · 스크럽 · 배속 · 카탈로그 항목 선택이 이 씬으로 간다(`FxShowcase.Instance`). 무대 줄에 배속 · 정지 · 다시 · 반복 · 턴테이블 · 밤 · 큰 한 방 · 반경 · 다시 읽기 · 화면 글자 토글이 있다. Game 뷰의 키도 그대로 먹고 창이 따라온다 — 창 없이(빌드)도 쓸 수 있다.

| 키 | 동작 |
|---|---|
| Space / Enter | 다시 재생 (무기 무대: 한 발) |
| F | (무기 무대) 연사 켜고 끄기 |
| ] / ↓ · [ / ↑ | 다음 · 이전 항목 |
| 1 2 3 4 | 속도 ×1 · ×0.5 · ×0.25 · ×0.1 |
| P / L | 정지 / 반복 |
| T / R | 턴테이블 회전 / 회전 초기화 |
| N / B | 밤 오버레이 / big |
| + / − | 반경 (폭발 판정) |
| H / F5 | UI 숨김 / 카탈로그·시트 다시 읽기 |

## 8-1. 무기 무대 — FX 툴 창 [무기] 탭 (2026-11-04)

무기 연출은 카탈로그 항목 하나씩으로는 판단이 안 된다 — 총구에서 나가고, 예광탄이 날고, 좀비에 맞고, 죽는 **한 줄기**로 봐야 한다. 그래서 캐릭터가 무기를 들고 쏘는 무대를 따로 뒀다.

**쓰는 법**: [무기] 탭 → 캐릭터(Char_01_Rookie …)와 무기(비우면 캐릭터 기본, 진화 무기도 고를 수 있다) → **[▶ 무대에서 쏘기]**(쇼케이스를 열고 플레이한 뒤 자동으로 세운다). 이미 무대가 켜져 있으면 [▶ 무대에 세우기]. 캐릭터·무기를 바꾸면 바로 갈아 끼운다.

| 무대에서 나오는 것 | 경로 |
|---|---|
| 총구 화염 · 탄피 | 세트 `muzzle` · `casingScale` — `ProtoShooter.GunFx` 와 같은 자리(에셋 muzzlePoint, 없으면 총열 끝 추정)·방향(총열). 사격 자세로 총열을 조준 방향에 맞추는 것도 같다 |
| 예광탄 | 같은 `ProtoProjectile`(수치 에셋의 projectileSize·tracer*) — 더미 가슴(0.9m)에 닿으면 착탄 |
| 착탄 불똥 · 흰 플래시 · 탄착음 | 세트 `hit`, 정확한 착탄점, +Z = 날아온 쪽(`HitFx.From`). 치명타는 ×1.5 + 약한 흔들림 |
| 처치 | 더미 가짜 체력(기본 60)이 바닥나면 세트 `kill` + 사망음, 더미가 0.6초 사라졌다 돌아온다 |
| 폭발 | 로켓·수류탄 — 세트 `explode`, radius = aoeRadius, big. 폭발 안의 더미는 공통 불똥 없이 처치만(`HitFx.Blast`) |
| 광선 · 화염 · 폭격 | 슬러그 레일 `ProtoBeamFx`, 화염방사기 `ProtoFlameCone`(틱마다 Pulse, 몸 정면 부채꼴), 융단 폭격 로켓은 airstrikeEvery 마다 `ProtoAirstrike.Strike` |

**조종 줄**: 연사(F) · 한 발(Space) · 공속 · 치명타 확률 · 소리 · 플래시 · 폭격 · 더미 체력. 그 아래 **애니 동기화** 토글과 **발사 프레임** 슬라이더(`ProtoFireSync`, 게임과 같은 규칙 — 끄면 트리거 순간에 내는 전 방식과 비교할 수 있다)와 지난 발의 지연 ms, **지난 발 애니**(발사를 낸 순간 사격 클립 "Shoot Rifle" 의 ms · 프레임 · 정규화 시각 — 어느 동작에서 총이 나가는지 숫자로 본다. 타임라인 안내 줄에도 같은 값). 그 아래 **파편** 세 줄 — ZombieGibs 정적 값(켜기 · 크기 · 최대 · 두께 · 디졸브 / 세기 · 수명 · 중력 · 회전 / 착지 피 · 크기 · 유지)과 살아 있는 조각 수.

**소총 클립 실측(11-05, 클립 YAML 근육 곡선)**: 인게임 사격은 UpperBody 레이어 "Shoot Rifle" = `Firing_Rifle_edit.anim`(Max 수정 자세를 구운 것, 1.167초 · 30fps 35프레임. Mixamo 원본 `Raw/Shoot_Rifle` 이 아니다). 0~267ms(프레임 0~8)는 거의 정지(왼팔이 당겨지고 고개가 숙는 준비), **반동은 267~367ms(프레임 8~11, 정점 300ms = 정규화 0.26)** — 오른 어깨 뒤로 · 오른손 들림 · 방아쇠 손가락 · 고개 숙임, 그 뒤 900ms 까지 천천히 복귀. 오른팔 자체는 안 움직인다(Max 편집 오른팔을 고정으로 구웠다). `FireAt` 0 이면 전환 0.08초가 끝난 클립 90ms(프레임 2~3)에 섬광이 나 **반동보다 ~200ms(6프레임) 앞선다**. 연사 간격 0.35초라 매 발 클립이 처음부터 돌아 프레임 0~10 만 보인다(반동 머리에서 끊김). 맞추는 길 두 가지: ① `FireAt` 0.25(대기 상한 300ms 안의 최대 — 트리거→발사가 0.3초 늦어진다) ② 클립 머리 267ms 를 잘라 반동을 머리로(지연 없음, 연사 때도 반동이 다 보임 — 에셋 수정).

**한 발 타임라인(되감기)**: [● 한 발 녹화] 를 누르면 연사를 끄고 한 발을 쏘며 `recordSeconds`(0.8초) 동안 기록한다 — 포즈는 `Animator.StartRecording`, 몸 틀기는 프레임마다, 프리팹 효과(총구 · 착탄 · 처치 · 폭발)는 시각 · 자리 · 회전 · 배율, 총알은 출발점 · 방향 · 수치. 끝나면 무대가 멈추고(timeScale 0, 애니메이터는 UnscaledTime 으로 `playbackTime` 을 그린다) 슬라이더가 열린다. 슬라이더 · ◀ ▶(1/60초) · [발사] [착탄] 점프 · 눈금 바(트리거 0 · 발사 · 착탄 · 처치 ms). 효과는 기록 시각으로 되감아 `ParticleSystem.Simulate`, 총알은 `출발점 + 방향 × 속도 × 경과`(모델이면 Simulate, 기본 예광탄이면 ProtoProjectile 과 같은 늘림), 더미는 착탄 순간 플래시 · 처치 뒤 사라짐. **코드 그림(탄피 · 광선 · 기본 총구 화염)은 되감지 못해 안 보이고** 그 수를 알려 준다. [■ 나가기] 로 살아 있는 무대로 돌아간다(연사는 꺼진 채). **슬롯 재생**(총구 ▶ · 피격 ▶ · 처치 ▶ · 폭발 ▶)은 세트의 자리 하나만 낸다 — 쏘지 못하는 무기도 이걸로 본다.
그 아래 **연출 세트 인스펙터**(WFX_*, 슬롯 첫 줄 프리팹 칸)와 **예광탄 접이**(WS_* 의 projectileSize·tracerColor·Length·Width)가 있어, 보면서 바로 고친다 — 다음 발부터 반영.

**안 하는 것**: 넉백·화상·데미지 숫자·좀비 AI(더미 프리팹 Zombie_Walker 에는 스크립트가 없다). 손에 들지 않는 보조 장비(회전 블레이드·지원 드론)와 헬기 공습은 무대에서 쏘지 않는다(좀비 목록·보조 장비 런타임이 필요) — 슬롯 재생만. 리퍼·테크니션 프리팹은 지금 소총·로켓을 들고 있다.
에디트 모드에선 세울 수 없다 — 탄피·예광탄·코드 그림이 플레이 중에만 돌고 무기도 소켓이 Awake 에서 물린다.

## 9. 이번에 게임 코드에서 바뀐 곳

| 파일 | 전 | 후 |
|---|---|---|
| `ProtoZombie` | 불똥·처치를 코드 상수로 | `TakeDamage(..., HitFx)` — 넘어온 ID·착탄점·배율로, 없으면 공통 |
| `ProtoZombie` (11-05) | 처치 = 터짐 프리팹 + 쓰러지는 애니 | 보스·불타 죽은 적 빼고 **몸 조각(ZombieGibs) + `zombie_gore`** — 맞은 방향(`_lastHitDir`)으로 튀고, 몸은 숨긴 채 0.4초 뒤 풀로(deathLinger 대신). 풀에서 다시 나오면 `ResetVisual` 이 렌더러를 켠다 |
| `ProtoShooter` | `ProtoGunFx.MuzzleFlash(1.35f …)` 하드코딩 | `GunFx(st, aim)` 이 무기 세트의 muzzle·탄피를 읽는다. 광선·화염은 착탄마다 `HitFx.From` |
| `ProtoShooter` · `FxWeaponStage` (11-05) | 화염 = `ProtoFlameCone`(옵션으로 스프라이트) | 세트 `jet` 슬롯이 있으면 **`ProtoFlamePack`**(팩 프리팹 그대로, 사거리에 자동 스케일) — 슬롯을 비우면 전처럼 |
| `ProtoShooter` (11-04) | 트리거 순간에 총구 화염·탄피·총알·발사음 | **줄 선 발사**(`ProtoFireSync`): 트리거만 걸고, 사격 상태("Shoot Rifle")에 들어와 전환(0.08s)이 끝나고 몸 틀기가 8° 안에 들어온 LateUpdate 에 낸다. 0.3s 안에 못 맞추면 그냥 낸다. DPS 는 그대로(일정한 지연만). `FireAt`(클립 정규화 시각)은 [무기] 탭 슬라이더로 조절해 기본값에 적는다 |
| `ProtoProjectile` | 착탄·폭발을 직접 | `ProjectileSpec.fx` → 착탄 `HitFx.From`, 폭발 `explode` 슬롯. 슬롯 항목에 프리팹이 꽂혀 있으면 폭발음 · 풀 눕힘(`ProtoExplosion.Aftermath`)을 따로 낸다(11-17, 탱크 포 `tank_cannon_hit`) |
| `ProtoSupportGear` | 수류탄 `ProtoExplosion.Play` | `Nade.fx` → `explode` 슬롯 + `HitFx.Blast`. 블레이드 `HitFx.From`. 헬기 호출에 세트 전달 |
| `ProtoHeliGunship` | 총구 1.8 · 불똥 0.55 상수 | `Call(..., fx)` — 세트의 muzzle·hit |
| `ProtoAirstrike` | 폭발 직접 | `Strike(..., fx)` → `Bomb.fx` → `explode` 슬롯 |
| `ProtoTank` · `ProtoUltimate` · `ProtoCharacterSwitcher` · `ProtoDestructibles` · `ProtoHeliCrash` | `ProtoExplosion` / `ProtoBossFx` / `MuzzleFlash` 직접 | 카탈로그 ID + `ProtoFx.PlayBlast`, 피해는 `HitFx.Blast(null)` |
| `ProtoRun` · `ProtoEvolution` | `ProtoLevelUpBeam.Play` 직접 | `levelup_beam` · `evolve_beam`(color 금색) |
| `ProtoGemPool` | 없음 | 흡수 순간 `gem_pickup`, 등급별 크기 |
| `ProtoBossBrain` | 콘티 4컷을 코드 상수 시각으로 | 시트 `Boss_Final/*` 가 있으면 시트가 그린다 |
| `ProtoNightOverlay` | 플레이어 필요 | `target` 필드 — 플레이어 없는 씬(쇼케이스)도 된다 |

이제 `Scripts/` 에서 `ProtoExplosion` · `ProtoBossFx` · `ProtoLevelUpBeam` · `ProtoGunFx.MuzzleFlash` 를 직접 부르는 곳은 `Fx/ProtoFxBuiltins`(폴백)와 `ProtoBossBrain.Phases()`(시트 없을 때)뿐이다.

## 10. Unity 에서 확인하는 순서

1. 에디터에 포커스 → 리컴파일. 공통 에셋은 이미 생성됐다(10-30 13:45: 카탈로그 22항목 · WFX 13개 · 템플릿 4개). 소총 전용 3항목·템플릿 3개·텍스처 2장·머티리얼 2개는 11-04 코드로만 넣어 둔 것 — 리컴파일 뒤 `AutoCreate` 가 만든다(콘솔 `[FX 툴] 카탈로그 25항목` 확인).
2. **FX 툴 ▸ FX 툴 창** 을 열고 **[▶ 무대]** → [카탈로그] 탭에서 `rifle_muzzle` · `rifle_hit` · `rifle_kill`(소총 전용) 과 `hit_spark` · `kill_burst` · `gem_pickup`(공통) 을, [액션 시트] 탭에서 보스 4컷을 본다. 무대 줄의 배속으로 느리게.
3. **[무기] 탭** → Char_01_Rookie · (캐릭터 기본 무기) → [▶ 무대에 세우기]. 캐릭터가 더미 왼쪽 앞에 서서 연사한다 — 총구 연기·탄피·예광탄·착탄 먼지·처치(5발마다)가 한 줄기로 보이면 된 것. 무기를 탕탕 캐논·산탄총·슬러그 레일·화염방사기·로켓으로 바꿔 가며 본다. 배속 0.25 로 느리게.
4. 프로토 씬 플레이 — 루키(1번) 소총이 무대와 같게 나오는지, 캐논·샷건은 공통 그대로인지, 젬 흡수, 수류탄, F3 융단 폭격, F6 헬기 기관포, 탱크, U 필사기.
5. 모양이 마음에 안 들면 [무기] 탭의 연출 세트 인스펙터에서 슬롯의 프리팹을 바꾼다(= `WFX_*`). 공통 모양은 카탈로그 항목의 `prefab`.

6. **파편(11-05)** — [무기] 탭에서 더미를 처치하면 더미(Zombie_Walker)가 잘게(기본 26조각까지) 쪼개져 튀고 피 시트가 터지며 바닥에 핏자국이 흩어졌다 사라진다. 조각은 마지막 0.7초 동안 녹아(디졸브) 없어진다 — 줄어들면 `Resources/VFX/M_GibDissolve.mat` 이 아직 없는 것(포커스 한 번 더). 파편 세 줄(크기 · 최대 · 두께 · 디졸브 / 세기 · 수명 · 중력 · 회전 / 착지 피 · 크기 · 유지)로 맞추고, 조각이 떨어진 자리마다 핏자국·핏방울이 생기는지(`Resources/VFX/M_GibBlood.mat`, 포커스 때 `[파편] 착지 피 머티리얼을 만들었다`) 보고, 콘솔 `[파편] …: 조각 N개 — 본×수` 로 구성을 본다. 프로토 씬에선 잡좀비 · 엘리트 · 개 · 원거리가 죽을 때(보스 · 불타 죽은 적 제외). 포커스 때 `[파편] 적 모델 3개의 Read/Write 를 켰다` · `[FX 툴] 템플릿 FX_ZombieGore 이 옛 모양이라 다시 구웠다` · `[FX 툴] 팩 텍스처 복사 …` 가 찍힌다.

## 11. 주의 · 남은 일

- **항상 위에(11-05)** 는 화면 미확인 — 플레이 중 메인 카메라 아래 `FxOverlayCamera` 가 생기고(Hierarchy), 콘솔에 `[FX 툴] 레이어 30 = FxOverlay 를 만들었다` · `카탈로그 손질 1단계` 가 한 번 찍혀야 한다. 총구 화염이 여전히 총에 가려지면 ① 카탈로그 탭에서 그 ID 의 "항상 위에" 가 켜져 있는지, ② `[FX] 레이어 FxOverlay 가 없다` 경고가 없는지, ③ 메인 카메라 렌더러가 Universal Renderer(포워드)인지 본다. 오버레이는 깊이를 지우고 그리므로 벽 뒤의 총구 화염도 비친다 — 의도(탑다운 가독성). 바닥 효과·연기·피에는 켜지 말 것(바닥에서 떠 보인다).
- 템플릿 프리팹(공통 4개 + 소총 3개)은 **코드로 만든 출발점**. 실제 아트로 다듬는 것이 다음 일 — 소총부터, 다음 무기는 §5 의 순서로.
- **플레이 중 스크립트 리로드**: 툴을 고치는 동안 무대가 플레이 중이면 리로드로 Queue·Dictionary·MaterialPropertyBlock 이 비어 "총알은 날아가는데 안 맞는" 상태가 됐었다(11-04 실제 발생). 이제 `FxWeaponStage` 가 투사체를 직렬화 목록에 두고 리로드 뒤 첫 틀에 회수하며, 플래시 블록은 늦게 만든다. 빗나감 카운터가 3 이상이고 명중 0 이면 상태 줄에 알린다.
- 무기 무대(11-04)도 **화면 미확인** — 캐릭터 키(렌더러 바운즈로 1.70m 맞춤)·총구 자리·사격 자세(총열을 조준 방향으로 트는 것)가 게임과 같은지 먼저 본다. 자리가 어색하면 `FxWeaponStage.shooterOffset`(기본 −2.4, 0, −1.6), 키가 틀리면 `FitHeight`. 더미가 턴테이블 위라 돌면서 맞는다 — 멈추려면 무대 줄의 턴테이블 토글.
- 소총 전용 세트(11-04)는 **화면을 아직 사람이 보지 않았다.** 연기(`M_Fx_Smoke`, 알파 블렌드)가 낮에 너무 진하면 `BuildRifleMuzzle` 의 Smoke 알파(0.3)·색(0.42 회색)부터 본다. 총구 수명 0.55 는 연기 길이 — 연기를 빼면 0.15 로 줄여도 된다.
- **머티리얼 발광 버그(11-04 발견·수정)**: 템플릿 머티리얼은 화염 머티리얼을 복제한 것이라 `_EmissionColor` 주황(4.2 / 1.45 / 0.1)과 플립북 블렌딩이 켜진 채였다 — 지난주 피격·처치·젬 효과까지 전부 주황이 더해져 과하게 밝았을 것. URP 가 임포트 때 값에서 키워드를 다시 만들어 키워드만 꺼선 안 됐다(`M_Fx_Smoke` 가 그랬다). 이제 `Harden` 이 값·GI 플래그·키워드를 같이 고치고, 에디터가 켜질 때 어긋나 있으면 스스로 돌린다(콘솔 `[FX 툴] 머티리얼 … 설정 고침`). 고친 뒤 효과가 전보다 차분해 보이면 그게 맞는 색이다 — 밝기는 입자 색(startColor·colorOverLifetime)으로 올린다.
- `muzzle_flash` 는 아직 코드 그림이 기본. `FX_MuzzleFlash` 프리팹을 카탈로그에 꽂으면 바뀐다.
- `hit_spark` 동시 상한 20 — 물량전에서 넘치면 안 낸다. 프리팹을 무겁게 만들면 상한부터 내릴 것.
- 탱크 포(`ProtoTank._cannon`)는 코드가 만드는 `WeaponStats` 라 연출 에셋이 없다 → 기본 세트. 바꾸려면 거기서 WFX 를 물린다.
- 쇼케이스 씬 파일은 아직 구워지지 않았다 — 메뉴를 처음 누를 때 생긴다.
- 컴파일 검증은 Unity 동봉 Roslyn 으로 했고(런타임·에디터 0건), **화면은 아직 사람이 보지 않았다.**

## 12. 파티클 셰이더 하나 · 폴더 (2026-11-20)

사용자 요청 "Survivor Ace, 탕탕특공대에서 뽑아온 것도 기존 unity 기본으로 쓰는 셰이더도 하나로 통합" — 파티클 이펙트는 이제 **`ZombieOverrun/FX/Particle` 하나**로 그린다(`Art/VFX/Shaders/FX_Particle.shader`, 머티리얼 164개 — 같은 날 저녁 "MSFX 도 합쳐줘"로 MSFX 복사본 6개까지).

| 원래 셰이더 | 옮긴 규칙 |
|---|---|
| URP `Particles/Unlit` · `Lit` | 속성 이름이 같다 — 값 그대로(`_BaseMap` · `_BaseColor` · 블렌드 · 컬 · 플립북 섞기 · 소프트 파티클 · 발광 · 색 모드), 깊이 검사 LEqual, **안개 받음**. Lit 1개(교체 착지 돌 `M_PP_SandParticle`)는 조명 반응이 없어진다 |
| Survivor Ace · 탕탕 `Effects/Particle` | `_MainTex` → `_BaseMap`, `_TintColor` → **`_TintColor`(HDR 칸)**, `_TwoSide` → `_Cull`, `_Ztest` → `_ZTest`, 블렌드 그대로, **안개 안 받음** |
| `UVFlow` · `Turbulence` · `SheetBase` | + 텍스처 흐름 `_MainSpeed` · 회전 `_MainRotation`(탕탕 `_Angele`) · 마스크(`_MASK_ON`) · 일렁임(`_NOISE_ON`, 세기 = `_Noise` × 0.2) · 시트 칸은 타일링으로 |
| MSFX 복사본 `ZombieOverrun/MSFX/PowDissolve`(화염방사기 3) · `CommonParticleOneShot`(소총 복사본 꺼진 층 3) | 셰이더의 **MSFX 모드**(`_Msfx` → `_MSFX_DISSOLVE` · `_MSFX_ONESHOT`)로. 둘 다 입자의 사용자 정점 스트림을 읽는다(UV0.zw · UV1 = Custom1) — 렌더러의 Custom Vertex Streams 를 원본대로 둘 것. 디졸브: `_Base` → `_BaseMap`, `_Noise` → `_MaskTex`(타일링 `_MaskTiling`, 오프셋 (0,1)), 알파 = saturate(a0 + (a0 − 0.1) × Custom1.x), a0 = 알파 × 노이즈R^UV.z, 밝기 × (UV.w + 1), 컬 Back. 원샷: `_Main` + `_Tiling` · `_Offset` → `_BaseMap`, UV.z × `_Scroll` → `_CustomScroll`, `_Mask` 켜면 `_MASK_ON`(경계 = Custom1.z · `_Feather`, 흐름 Custom1.w × `_CustomMaskScroll`), 고정 마스크 `_StaticMask` · `_StaticSmooth`, 밝기 × (1 + Custom1.x), 컬 끔. Stretch · LimitUV 는 없음(쓰던 머티리얼 없음). 둘 다 안개 받음 · 알파 채널 One/1−SrcAlpha |
| 빌트인 `Legacy Shaders/Particles/Additive`(Survivor Ace 꺼진 층 3개) · 셰이더 그래프 잃은 `M_LightningRing` | 레거시는 텍스처 × 색 × 2 그대로. 번개 고리는 팩(NamuFX)과 시트 텍스처까지 지워져 분홍이었다 → 우리 `T_Fx_Ring` · 전기색 가산으로 새로 채움(손으로 쓴 머티리얼, 렌더 미확인) |

- **색이 두 칸인 까닭**: Unity 는 보통 색(`_BaseColor`)은 감마로 저장해 리니어로 바꿔 쓰고, `[HDR]` 색(`_TintColor`)은 리니어로 저장해 그대로 쓴다. 처음엔 `_BaseColor` 하나에 `[HDR]` 을 붙였다가 URP 머티리얼의 중간 밝기 채널이 어두워졌다(같은 색 0.5 → URP 128, 통합 187 — `BatchColorTest`). 그래서 URP 쪽 값은 `_BaseColor`, 원작 쪽 값은 `_TintColor` 에 숫자 그대로 두고 둘을 곱한다. 셋업 코드(`FxTemplatePrefabs.Harden` · `InfernoFlameSetup` · `OrbitBladeTrailSetup` · `ToonExplosionSetup` · `FxPackImport`)는 예전처럼 `_BaseColor` 에 쓰고 `Shader.Find` 만 바꿨다.
- **GPU 인스턴싱은 뺐다** — 원작 셰이더처럼 메시 파티클도 보통 경로. 미리보기에서 인스턴싱 메시 입자가 원점에 그려지던 문제도 같이 없어진다.
- **확인**: 이펙트 프리팹 50개를 전후로 4컷씩 렌더해 비교했다(`Library/FxShaderUnify/report.txt`). 남는 차이는 시간으로 흐르는 UV(불 고리 · 망치 궤적 · 표적 사살 탄) — 같은 상태를 두 번 그려도 같은 만큼 다르다(`selfcheck.txt`).
- **MSFX 확인**: 화염방사기 MSFX 프리팹을 전후 렌더(차이 0, 최대 4/255) + 사용자 스트림 값을 넣은 사각형으로 원본 셰이더 머티리얼과 통합 변환본을 나란히(노이즈 · 마스크를 켠 판까지, `BatchMsfxShaderTest` — 디스크 변환본 6개 모두 최대 ≤ 9/255). 함정: `new Material` 로 만든 메모리 머티리얼에 `CopySerialized` 한 직후엔 키워드 상태가 덜 반영돼 원샷 흐름이 빠진 채 그려진 적이 있다 — 저장본은 정상이라 `ConvertAll` 이 바꾼 파일을 다시 임포트한다.
- **FxPackImport 주의**: MSFX 메뉴('소총/화염방사기에 MasterStylizedFX 입히기')를 다시 돌리면 팩 셰이더 복사본(`SH_MSFX_*`)과 그 셰이더를 쓰는 머티리얼이 다시 생긴다 — 끝난 뒤 '파티클 셰이더 하나로 합치기' 메뉴를 한 번 누른다.
- **지운 것**: `_Reference/*/Shaders` 의 원작 근사 셰이더 6개, MSFX 복사본 셰이더 `SH_MSFX_PowDissolve` · `SH_MSFX_CommonParticleOneShot`(백업 `_Backup/20261120_FxShaderUnify`). 남은 다른 셰이더: 코드 이펙트 전용(`ZombieOverrun/BurnDissolve · BurnEdge · GibDissolve · ToonFireball · ToonCrackDecal`, `Proto/FlameBorder · Explosion · LightBeam · FlameSprite · SpriteFlash · RainSheet · GroundDecal`).
- **11-23 남은 것 마저** (사용자 "이펙트는 ZombieOverrun/FX/Particle 셰이더로 통합으로 써줘", 백업 `_Backup/20261123_UnifyRest`):
  - Unity 기본 파티클 머티리얼(`Default-ParticleSystem`, 빌트인 `Particles/Standard Unlit`)을 쓰던 렌더러 5개 → 새 `Art/VFX/Common/M_Fx_DefaultParticle.mat`(통합 셰이더, 같은 기본 텍스처 · 알파 섞기 · 뒷면 버림 · 안개 받음 — 기본 머티리얼 값 그대로). 보이는 건 소총 총구 · 착탄의 `OneShotParticle` 한 알 층이고, 착지 연기 · 표적 사살 탄 · 히트의 `all` 묶음은 렌더러가 꺼져 있다.
  - 탕탕 추출본 `Shared/TangTang/Materials/BlendMat_Common`(원작 셰이더를 잃고 빌트인 `UI/Default` 로 남아 있던 것, 보스 망치 궤적의 꺼진 `JiaoYin` 만 씀) → 통합 셰이더 알파 섞기 · 양면 · 안개 안 받음.
  - **코드 이펙트도(같은 날 사용자 "그것도 바꿔줘", 백업 `_Backup/20261123_UnifyCodeFx`)**: 새 `Proto/ProtoFxMat.cs` 가 이펙트 머티리얼을 통합 셰이더로 만든다(`ProtoFxMat.New(이름, 텍스처, 큐, 색)` — 기본값 알파 섞기 · 양면 · 깊이 안 씀에 **안개만 끔** = 옛 `Sprites/Default` 와 같은 그림, 셰이더를 못 찾으면 Sprites/Default). 옮긴 곳 12: 예광탄 · 총구 섬광(`ProtoGunFx`), 보스 예고 링 · 채움 · 균열 · 망치 궤적(`ProtoBossFx`), 빔(`ProtoBeamFx`), 필사기 선(`ProtoUltimate`), 적 탄 구슬 · 꼬리(`ProtoEnemyBolt`), 독가스 연기 · 바닥(`ProtoPoisonGas`), 줍기 아이콘 · 보호막/아드레날린/자석 링(`ProtoPickups`), 탱크 탑승 링(`ProtoTank`) · 바퀴 자국(`ProtoTankTracks`), 보스 · 엘리트 발밑 원(`ProtoBossMarker`), 보스 울타리 예비 벽(`ProtoBossArena`), 풀 차기 입자(`ProtoGrassKick`). **MaterialPropertyBlock 색 · 텍스처 이름이 바뀐다**(_Color → _BaseColor, _MainTex → _BaseMap) — `ProtoFxMat.ColorId` · `TexId` 로 고침: 무기별 예광탄 색(`ProtoProjectile`), 적 탄, 독가스, 줍기. 빌드 포함은 `Resources/ShaderRefs/Ref_ZombieOverrun_FX_Particle.mat`(키워드 없는 변형). 다른 코드 입자(`ProtoRainFx` · `ProtoBurnFx` · `ZombieGibs` 피 · `ProtoFlameCone`)는 이미 통합 셰이더 머티리얼을 쓴다.
  - 결과: 게임 이펙트 폴더(`Sujin/S_VFX` · `Resources/VFX` · `Art/VFX`)의 파티클 렌더러는 **`_Unused/Rifle_MSFX_Original`(손보기 전 원본 보관) 말고 전부 통합 셰이더**. 외부 팩(`Soo_VFX` · `VFX_Klaus`) 원본은 팩 셰이더 그대로 둔다 — 게임에 쓸 땐 복사해 와서 합치기 메뉴로.
  - 통합 대상 아님: 파티클이 아닌 특수 셰이더(`Proto/*` 비 · 안개 · 불꽃 테두리 · 폭발 · 빛기둥 · 물고기 젬 등, 좀비 화상 · 조각 디졸브), 이펙트가 아닌 물체 — 체력 바(`ProtoDestructibles` · `ProtoTank`, Sprites/Default), 탄피 · 보스 파편 · 젬 · 무기 무대 수류탄(URP Unlit).

폴더(같은 날 정리 — `_Backup/20261120_FxShaderUnify/moves.json` 에 이동 64개):

```
_Game/Art/VFX/
├─ Common/    공통 템플릿 FX_HitSpark · KillBurst · GemPickup · MuzzleFlash · ZombieGore · IntroLanding + M_Fx_* · T_Fx_*  (FxTemplatePrefabs.Dir)
├─ Legacy/    9월 코드 이펙트 재료(번개 · 비 · 화상 · 화염 · 교체 먼지) + Unity ParticlePack 에서 복사한 T_PP_* · M_PP_* (팩 의존 끊음)
├─ Shaders/   FX_Particle + 코드 이펙트 셰이더(디졸브 · 카툰 폭발 · Proto 불꽃 테두리 · 폭발 · 빛기둥 · 화염 시트 · 번개)
└─ Weapons/   Rifle · OrbitBlade · … (11-23: Flamethrower 지움 · Inferno 는 Sujin 으로)
_Reference/SurvivorAce/Prefabs/  Weapon · Explosion · Ultimate · Appear · _Unused
_Reference/TangTang/Prefabs/     Boss · Enemy · Pickup · _Unused
```

## 13. 사용자 작업 폴더 `Art/Sujin` (2026-11-23)

> **같은 날 오후 바뀜** — 사용자가 에디터에서 `Sujin/VFX` → `Sujin/S_VFX`, `Shaders` → `S_Shaders`, `Art/ToonFantasyCity` → `Sujin/S_City` 로 옮기고 이름을 바꿨고(fx_zombie_death → FX_Zombie_Death, MFX_FireRing → Weapon/Inferno/FX_Inferno_FireRing, 소총 둘 이름 정리), 이어서 "11월 이후 이펙트는 S_VFX 에 종류별로, _Reference 안의 것도 — 추후에 삭제할 거라서" → **11월 이후 이펙트 44개 + 그 재료 전부를 `S_VFX` 로**(Weapon · Explosion · Hit · Enemy · Boss · Pickup · Character · _Unused · Shared, 317건, 백업 `_Backup/20261123_SVfxMove`). 지금 지도는 `Assets/_Game/Art/Sujin/README.md`. 아래 나무 그림은 오전 판 기록이다.

사용자 요청 "Flamethrower 이펙트 작업에서 안 쓰이는 건 삭제, 내가 작업한 이펙트는 따로 폴더 만들어서 정리, 배경 셰이더도 내가 이름 바꾼 걸로 폴더 정리". 사용자가 **직접 손본 이펙트 프리팹**과 이름을 바꾼 배경 셰이더만 모았다. 원작에서 온 머티리얼 · 텍스처는 여러 프리팹이 같이 써서 원래 자리에 둔다(GUID 그대로라 카탈로그 · 무기 세트 연결도 그대로).

```
_Game/Art/Sujin/
├─ VFX/
│  ├─ Weapon/Inferno/     FX_Inferno_Jet + M_Inferno_* · T_Inferno_* · Source~(손질 기록 · patch_inferno*.py)   ← 옛 Art/VFX/Weapons/Inferno
│  ├─ Weapon/Rifle/       FX_Rifle_TangTang_Muzzle_Flashy · FX_Rifle_TangTang_Hit_00                         ← 옛 Art/VFX/Weapons/Rifle/MSFX
│  ├─ Explosion/          FX_Car_Explosion(탈것 폭발) · fx_zombie_death(처치 공통)                              ← 옛 _Reference/SurvivorAce/Prefabs/Explosion
│  └─ Ultimate/           MFX_FireRing(파이로 필사기 불 고리)                                                 ← 옛 _Reference/SurvivorAce/Prefabs/Ultimate
└─ Shaders/Toon/          SujinToon_Outline · SujinToon_Vegetation · SujinToon_Grass · SujinToon.hlsl         ← 옛 Art/ToonFantasyCity/Shaders
```

- 옮김은 Unity 가 했다(`AssetDatabase.MoveAsset`, 한 번 도는 `SujinFolderMove` — 끝나고 스스로 지움, 기록 `Library/SujinFolderMove.log`). 백업 `_Backup/20261123_SujinFolder`.
- `InfernoFlameSetup` 은 이제 자리를 **프리팹 GUID** 로 찾는다(`Dir` · `PrefabPath` 가 속성) — 폴더를 또 옮겨도 "프리팹이 없다"며 새로 굽지 않는다.
- 같이 지운 것: `Art/VFX/Weapons/Flamethrower`(MSFX 복사본 · 시트 — 카탈로그 두 항목 말고는 아무도 안 썼다), `Editor/Fx/FlamethrowerSheetSetup.cs`, `Proto/ProtoFlameSheetFx.cs`, `ArtSource/VFX/bake_flamesheet.py` · `flamesheet_meta.json` · `flamethrower_sheet_source.webp`. 9월 코드 화염(`FX_FlameJet` · `ProtoFlameCone` · `bake_flamejet.py`)은 jet 슬롯을 비웠을 때의 대비책이라 남겼다.
