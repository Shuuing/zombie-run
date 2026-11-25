using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 코드 그림 효과를 카탈로그 이름으로 등록한다 — 프리팹이 아직 없는 항목의 폴백.
    /// 기존 ProtoBossFx · ProtoExplosion · ProtoLevelUpBeam 은 손대지 않고 이름만 붙인다.
    /// 새 코드 그림을 만들면 여기에 한 줄 추가하고, 프리팹이 생기면 카탈로그에서 프리팹만 채우면 된다.
    /// </summary>
    public static class ProtoFxBuiltins
    {
        static bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register()
        {
            if (_done) return;
            _done = true;

            // ── 최종 보스 콘티 4컷 (ProtoBossFx) ──
            // 1컷 준비 — 발밑 먼지. 자리 = 루트(발), 크기 = 덩치
            ProtoFx.RegisterBuiltin("boss_windup_dust", c => ProtoBossFx.WindupDust(c.WorldPosition, c.scale));
            // 1컷 준비 — 망치에 불. 스팬: 매 프레임 소켓(HammerHead)에서 뿜는다
            ProtoFx.RegisterBuiltin("boss_hammer_flames", null, (c, dt) => ProtoBossFx.HammerFlames(c.anchor, dt));
            // 2컷 휘두르기 — 궤적. 스팬: 켜고 끈다
            ProtoFx.RegisterBuiltin("boss_hammer_trail",
                c => ProtoBossFx.HammerTrail(c.anchor, true), null, c => ProtoBossFx.HammerTrail(c.anchor, false));
            // 3·4컷 충돌 — 섬광·화구·링·파편·균열·연기 + 흔들림 + 소리 (전부 ProtoBossFx.Impact 안에 있다 —
            // 그래서 시트 이벤트의 shake / sfx 는 비워 둔다. 둘 다 적으면 두 번 난다)
            ProtoFx.RegisterBuiltin("boss_impact", c => ProtoBossFx.Impact(c.WorldPosition, c.radius, c.big));

            // ── 공용 ──
            // 착탄 폭발. radius = 판정 반경(링 테두리). quiet 면 흔들지 않는다(탱크 포)
            ProtoFx.RegisterBuiltin(FxIds.Explosion, c => ProtoExplosion.Play(c.WorldPosition, c.radius > 0f ? c.radius : 2.4f, shake: !c.quiet));
            ProtoFx.RegisterBuiltin(FxIds.LevelUpBeam, c => ProtoLevelUpBeam.Play(c.WorldPosition, c.color.a > 0f ? c.color : (Color?)null));
            // 진화 — 같은 빛기둥을 금색으로. 따로 이름을 둔 이유: 아트가 진화만 다른 연출로 바꿀 수 있게
            ProtoFx.RegisterBuiltin(FxIds.EvolveBeam, c => ProtoLevelUpBeam.Play(c.WorldPosition, c.color.a > 0f ? c.color : (Color?)null));

            // ── 환경·탈것·필사기 (2026-10-30 프리팹 교체 2차) — 전부 기존 코드 그림에 이름만 붙였다 ──
            // 승합차: 피격 불똥(총구 섬광 메시를 작게 재사용) · 폭발(보스 충돌 연출 재사용) · 재생성 먼지
            ProtoFx.RegisterBuiltin(FxIds.VanHit, c => ProtoGunFx.MuzzleFlash(c.WorldPosition, c.WorldRotation * Vector3.forward, 0.45f * c.scale));
            ProtoFx.RegisterBuiltin(FxIds.VanBlast, c => ProtoBossFx.Impact(c.WorldPosition, c.radius, c.big));
            ProtoFx.RegisterBuiltin(FxIds.VanAppearDust, c => ProtoBossFx.WindupDust(c.WorldPosition, 1.6f * c.scale));
            // 헬기 추락: 엔진·잔해의 작은 폭발(흔들림 없음) · 추락 충돌
            ProtoFx.RegisterBuiltin(FxIds.HeliCrashPop, c => ProtoExplosion.Play(c.WorldPosition, c.radius > 0f ? c.radius : 1.1f, shake: !c.quiet));
            ProtoFx.RegisterBuiltin(FxIds.HeliCrashImpact, c => ProtoBossFx.Impact(c.WorldPosition, c.radius, c.big));
            // 탱크: 파괴 폭발 · 잔해 작은 폭발
            ProtoFx.RegisterBuiltin(FxIds.TankBlast, c => ProtoExplosion.Play(c.WorldPosition, c.radius > 0f ? c.radius : 3f, shake: !c.quiet));
            ProtoFx.RegisterBuiltin(FxIds.TankDebrisPop, c => ProtoExplosion.Play(c.WorldPosition, c.radius > 0f ? c.radius : 1.4f, shake: !c.quiet));
            // 탱크 대포 착탄(2026-11-17) — 폴백은 공용 착탄 폭발과 똑같은 코드 그림(프리팹 FX_Death_XiaoGuai_002 를 비우면 예전 그대로)
            ProtoFx.RegisterBuiltin(FxIds.TankCannonHit, c => ProtoExplosion.Play(c.WorldPosition, c.radius > 0f ? c.radius : 2.6f, shake: !c.quiet));
            // 파이로 필사기 연소 — 화상 적이 터진다(첫 발만 흔들림)
            ProtoFx.RegisterBuiltin(FxIds.UltCombust, c => ProtoExplosion.Play(c.WorldPosition, c.radius > 0f ? c.radius : 1.5f, false, shake: !c.quiet));

            // ── 빈도 높은 타격감 (2026-10-30 프리팹 교체 1차) ──
            // 총구 화염 — 기존 코드 그림(ProtoGunFx, 별 + 앞으로 뻗는 불꽃). +Z = 총열 방향, scale 1 = 소총
            ProtoFx.RegisterBuiltin(FxIds.MuzzleFlash, c => ProtoGunFx.MuzzleFlash(c.WorldPosition, c.WorldRotation * Vector3.forward, c.scale));
            // 피격 불똥 · 처치 터짐 · 젬 흡수는 코드 그림이 없다 — 카탈로그의 프리팹(템플릿 FX_HitSpark 등)만 쓴다.
            // 프리팹 슬롯을 비우면 아무것도 안 나온다(피격 플래시·소리·사망 애니는 코드가 그대로 한다).

            // ── 무기별 (2026-11-04) — WFX 에셋이 가리키는 ID. 총구 폴백은 공통 코드 그림과 같다(프리팹 칸을 비워도 쏘는 게 보이게) ──
            ProtoFx.RegisterBuiltin(FxIds.RifleMuzzle, c => ProtoGunFx.MuzzleFlash(c.WorldPosition, c.WorldRotation * Vector3.forward, c.scale));
            // rifle_hit · rifle_kill 은 공통 hit_spark · kill_burst 처럼 프리팹만 — 비우면 안 나온다
        }
    }

    /// <summary>
    /// 탈것 큰 폭발 — 탱크 파괴 · 승합차 폭발 · 헬기 추락 충돌(2026-11-13 사용자 "FX_Car_Explosion 으로").
    /// 카탈로그 항목에 프리팹이 꽂혀 있으면 프리팹을 바닥(y 0 — 바닥 고리 · 충격파 판이 땅에 깔린다)에 <c>prefabScale</c> 배로 놓고,
    /// 코드 그림이 안에서 같이 내던 풀 눕힘 · 흔들림 · 소리는 <c>feel</c> 로 따로 낸다(프리팹은 그걸 못 낸다).
    /// 프리팹 칸을 비우면 예전 코드 그림이 <c>builtinPos</c> 에서 예전 그대로 난다(feel 은 안 부른다 — 코드 그림이 낸다).
    /// <para>세 항목은 카탈로그 '항상 위에'(onTop)를 켜 둔다 — 바닥에 놓인 폭발이 그 자리에 남는 탱크 · 헬기 잔해와 땅에
    /// 가려져 거의 안 보였다(11-13 "이펙트가 안보여", 임시 프로젝트 렌더로 확인). 끄면 다시 가려진다.</para>
    /// </summary>
    public static class VehicleBlastFx
    {
        public static void Play(string id, Vector3 builtinPos, float radius, float prefabScale, System.Action feel)
        {
            if (!ProtoFx.HasPrefab(id)) { ProtoFx.PlayBlast(id, builtinPos, radius, big: true); return; }
            ProtoFx.PlayBlast(id, new Vector3(builtinPos.x, 0f, builtinPos.z), radius, big: true, scale: prefabScale);
            feel?.Invoke();
        }
    }

    /// <summary>
    /// 코드가 부르는 효과 ID. 카탈로그의 id 와 같아야 한다 — 문자열을 여기저기 적지 않으려고 모아 둔다.
    /// 보스 콘티 4컷은 시트 이벤트가 ID 를 들고 있어 여기 없다.
    /// </summary>
    public static class FxIds
    {
        /// 총구 화염 — ProtoShooter (매 발)
        public const string MuzzleFlash = "muzzle_flash";
        /// 피격 불똥 — ProtoZombie.DealDamage (플래시가 나는 피해마다. 화상 틱 제외)
        public const string HitSpark = "hit_spark";
        /// 처치 터짐 — ProtoZombie.Die
        public const string KillBurst = "kill_burst";
        /// 젬 흡수 — ProtoGemPool (젬이 캐릭터에 닿는 순간)
        public const string GemPickup = "gem_pickup";
        /// 떨어진 물고기 젬 둘레 반짝이(스팬) — ProtoGemPool. 떨어질 때 켜고 자석에 걸리면 Stop. 자리 = 물고기(매 프레임 MoveTo), scale = 젬 등급(0.8 · 1 · 1.4).
        /// 프리팹 = 탕탕 드롭 아이템 연출(FX_DiaoLuo_Color)의 별 반짝임만 떼어 낸 Sujin/S_VFX/Pickup/FX_FishSparkle (2026-11-18, 임시 — 출시 전 교체). 비우면 안 나온다
        public const string GemSparkle = "gem_sparkle";
        /// 자석에 끌려오는 물고기 젬 꼬리(스팬) — ProtoGemPool. 자석에 걸릴 때 켜고 흡수되면 Stop(detach). 자리 = 물고기(매 프레임 MoveTo), scale = 젬 등급,
        /// 꼬리 굵기는 코드가 물고기 키 × trailWidthPerFishHeight 로 넣는다(길이도 trailTime 으로 덮어쓴다)(TrailRenderer 굵기는 배율을 안 탄다).
        /// 프리팹 = 탕탕 얼음 불사조 화살(Bullet_WhistleArrowIcePhoenix)의 꼬리 띠 + 물방울 점 — 금색 물고기에 맞춰 노란 텍스처 복사본(_Gold)으로. 띠 머리(물고기 쪽)는 가늘고 투명하게 시작한다 — 물고기 자리에서 잘린 단면이 보였다(같은 날) 떼어 낸 FX_FishTrail (2026-11-18, 임시). 비우면 안 나온다
        public const string GemTrail = "gem_trail";

        // ── 무기별 — 코드가 직접 부르지 않는다. WFX_* 에셋의 슬롯이 가리킨다. 여기 두는 이유는
        //    카탈로그 채우기(FxAssetsSetup)와 폴백 등록(위)이 같은 이름을 쓰게 하려고. 무기 하나 = 총구 · 피격 · 처치 세 ID ──
        /// 루키 탕탕 소총 — 총구 (WFX_Rifle_TangTang.muzzle). 템플릿 Art/VFX/Weapons/Rifle/FX_Rifle_Muzzle
        public const string RifleMuzzle = "rifle_muzzle";
        /// 루키 탕탕 소총 — 피격 불똥 + 먼지 (WFX_Rifle_TangTang.hit)
        public const string RifleHit = "rifle_hit";
        /// 루키 탕탕 소총 — 처치 (WFX_Rifle_TangTang.kill)
        public const string RifleKill = "rifle_kill";
        /// 화염방사기 화염 줄기(스팬) — 세트 jet 슬롯이 가리키고 ProtoFlamePack 이 프리팹을 세운다. 모양 = MasterStylizedFX RedFlameThrower 복사본(FxPackImport, 2026-11-05)
        public const string FlamethrowerJet = "flamethrower_jet";
        /// 인페르노(화염방사기 진화) 화염 줄기(스팬) — WFX_Inferno.jet 이 가리키고 ProtoFlamePack 이 세운다. 모양 = 컨셉 아트를 파티클로 짠 불(InfernoFlameSetup, 2026-11-06)
        public const string InfernoJet = "inferno_jet";

        // ── 공용 코드 그림 ──
        /// 착탄 폭발 — 무기 연출 세트의 explode 기본값. radius = 판정 반경
        public const string Explosion = "explosion";
        /// 레벨업 빛기둥 — ProtoRun (카드 적용 순간)
        public const string LevelUpBeam = "levelup_beam";
        /// 진화 빛기둥(금색) — ProtoEvolution
        public const string EvolveBeam = "evolve_beam";

        // ── 교체 연출 — 프리팹이 있을 때만 그걸로 바뀐다(2026-11-13). 비우면 예전 그림(번개 · 진화 빛기둥)으로 돌아간다 ──
        /// 캐릭터 교체 등장 — ProtoCharacterSwitcher. 자리 = 플레이어 발밑, 따라다님, unscaled, scale 1.
        /// 프리팹이 있으면 하늘 번개(ProtoLightningStrike)를 숨기고 이것만 낸다(교체 시각 · 피해 · 착지 먼지는 그대로)
        public const string CharSwapAppear = "char_swap_appear";
        /// 무기 교체(진화) 등장 — ProtoEvolution.Evolve. 자리 = 플레이어 발밑, 따라다님, unscaled, scale 1.
        /// 프리팹이 있으면 진화 빛기둥(evolve_beam)을 숨기고 이것만 낸다(흔들림 · 소리 · 문구는 그대로)
        public const string WeaponChangeAppear = "weapon_change_appear";
        /// 시작 연출 낙하산 착지 — ProtoIntro.Land (건너뛰기면 안 낸다, 2026-11-16). 자리 = 착지 지점(바닥 y 0), scale 1 = 캐릭터.
        /// 프리팹 = Survivor Ace 보스 낙하 연기 복사본 Sujin/S_VFX/Character/MFX_Landing_Smoke(같은 날 사용자 "Survivor Ace 에서 가져와서", 임시 — 출시 전 교체).
        /// 우리 템플릿 Art/VFX/Common/FX_IntroLanding(먼지 고리 · 피어오르는 먼지 · 잔돌 · 먼지 충격파)은 예비. 흔들림 · 찌그러짐 · 소리(land)는 ProtoIntro.
        /// 프리팹이 비어 있으면 예전처럼 캐릭터 교체 착지 먼지(ProtoCharacterSwitcher.PlayLandingFx)를 낸다
        public const string IntroLanding = "intro_landing";
        /// 좀비 처치 공통 폭발 — ProtoZombie.PlayDeathFx ← ProtoZombie.Die(모든 적) · FxWeaponStage.Kill (2026-11-13, 프리팹 fx_zombie_death).
        /// 자리 = 몸 가운데, scale = 몸 높이 / 워커 키 1.72m(개 · 엘리트 · 보스도 자기 덩치대로).
        /// 무기 처치 효과(세트 kill 슬롯 · kill_burst) · 몸 조각 · 피(zombie_gore)와 같이 난다 — 대신하지 않는다(사용자 "전 이펙트도 살려줘")
        public const string ZombieDeath = "fx_zombie_death";

        // ── 환경 · 탈것 · 필사기 ──
        /// 승합차 피격 불똥 — ProtoDestructibles (맞은 자리, +Z = 위)
        public const string VanHit = "van_hit";
        /// 승합차 폭발 — ProtoDestructibles (radius = 판정 반경, big). 프리팹 FX_Car_Explosion(2026-11-13) — VehicleBlastFx 로 부른다
        public const string VanBlast = "van_blast";
        /// 승합차 재생성 먼지 — ProtoDestructibles
        public const string VanAppearDust = "van_appear_dust";
        /// 추락 헬기의 작은 폭발(엔진 피격 · 잔해) — ProtoHeliCrash (quiet)
        public const string HeliCrashPop = "heli_crash_pop";
        /// 헬기 추락 충돌 — ProtoHeliCrash (radius = 판정 반경, big). 프리팹 FX_Car_Explosion(2026-11-13) — VehicleBlastFx 로 부른다
        public const string HeliCrashImpact = "heli_crash_impact";
        /// 탱크 파괴 폭발 — ProtoTank (radius = 판정 반경). 프리팹 FX_Car_Explosion(2026-11-13) — VehicleBlastFx 로 부른다
        public const string TankBlast = "tank_blast";
        /// 탱크 잔해 작은 폭발 — ProtoTank
        public const string TankDebrisPop = "tank_debris_pop";
        /// 탱크 대포 착탄 폭발 — ProtoTank 가 탱크 포 연출 세트의 explode 슬롯에 꽂고, ProtoProjectile.Explode 가 낸다(2026-11-17).
        /// 자리 = 포탄이 터진 점(대개 좀비 몸통 높이), radius = 판정 반경 2.6m, quiet(흔들림 없음). 프리팹 = Survivor Ace FX_Death_XiaoGuai_002
        /// (원작 이펙트 표 210008 '일반 폭발', 임시 — 출시 전 교체). 폭발음 · 풀 눕힘은 코드(ProtoExplosion.Aftermath).
        /// 프리팹 칸을 비우면 예전 그대로 공용 착탄 폭발 코드 그림(explosion)이 난다
        public const string TankCannonHit = "tank_cannon_hit";
        /// 파이로 필사기 연소 — ProtoUltimate (화상 적마다, 첫 발만 흔들림)
        public const string UltCombust = "ult_combust";
        /// 파이로 필사기 불꽃 고리(스팬) — ProtoUltimate.Pyro. 자리 = 플레이어 발밑(매 프레임 옮김, 회전 없음), 필사기 동안 켜 둔다.
        /// 프리팹은 반지름 2.5m 로 만들어져 있고 ProtoUltimate 가 판정 반경(3.5 / 갈래 A 5m)에 맞춰 원 둘레만 넓힌다(불꽃 그림 크기는 그대로).
        /// 프리팹 MFX_FireRing(2026-11-13, Survivor Ace 재료로 만든 임시 — 출시 전 교체). 비우면 그림 없이 판정만 남는다
        public const string UltFireRing = "ult_fire_ring";
        /// 루키 필사기 표적 사살 탄(스팬) — ProtoUltimate.Fly. 자리 = 몸 가슴 높이에서 출발해 코드가 매 프레임 옮기고 돌린다(+Z = 나는 쪽, 표적을 따라 꺾임).
        /// 몸 표면(쏜 쪽)에 닿으면 피해 + 히트(UltTargetKillHit)와 함께 끝난다(bulletPierce 0, 11-18). 끝날 때 머리(로컬 파티클)는 바로 지우고 꼬리만 사그라든다.
        /// 프리팹 FX_TargetKill_Bullet_Shock(2026-11-18, Survivor Ace MFX_ChongJiBo_Bullet 손질 복사본 — 임시, 출시 전 교체).
        /// 비우면 예전 그대로 금색 선(ProtoBeamFx) + 즉발 피해
        public const string UltTargetKillBullet = "ult_target_kill_bullet";
        /// 루키 필사기 표적 사살 탄이 맞는 순간(원샷) — ProtoUltimate.Fly. 자리 = 표적 몸 표면(쏜 쪽, 가슴 높이), +Z = 쏜 사람 쪽.
        /// 프리팹 FX_TargetKill_Hit(2026-11-18, Survivor Ace MFX_LuoLei_Hit_Golden 금빛 낙뢰 히트 복사본, 루트 0.6 · 시작 지연 0.05 당김 — 임시, 출시 전 교체).
        /// 비우면 기본 불똥(HitFx)만
        public const string UltTargetKillHit = "ult_target_kill_hit";
        /// 처치 피 터짐 + 바닥 피 — ProtoZombie.Die · FxWeaponStage.Kill (몸 조각은 코드 그림 ZombieGibs 가 따로). 자리 = 몸 가운데, +Z = 맞은 방향, scale = 덩치
        public const string ZombieGore = "zombie_gore";

        // ── 적 · 보스 — 프리팹이 있을 때만 나온다(코드 그림 폴백 없음, 2026-11-12) ──
        /// 러너 독가스 구름(스팬) — ProtoPoisonGas. 자리 = 바닥, scale = 판정 반경(m). 프리팹이 있으면 코드 퍼프를 숨기고 바닥 원(판정)은 남긴다
        public const string PoisonGasCloud = "poison_gas_cloud";
        /// 원거리 좀비 독 구슬(스팬, 구슬을 따라감) — ProtoEnemyBolt. scale 1 = 구슬 지름 0.55m. 프리팹이 있으면 코드 구슬·꼬리를 숨긴다
        public const string PoisonBolt = "poison_bolt";
        /// 독 구슬이 맞거나 막힐 때 터짐 — ProtoEnemyBolt.Pop. 자리 = 구슬 위치, scale 1 = 독 구슬 기준
        public const string PoisonBoltHit = "poison_bolt_hit";
        /// 플레이어 독 피격 — ProtoPlayer.TakeDamage(Poison). 자리 = 플레이어 가슴(로컬 위 1.3m), 따라다님, scale 1
        public const string PlayerPoisonHit = "player_poison_hit";
        /// 좀비 평타(할퀴기)에 맞음 — ProtoPlayer.TakeDamage(Claw). 자리 = 플레이어 가슴(로컬 위 1.3m), 따라다님, scale 1. 몸에 가리지 않게 onTop
        public const string ZombieClawHit = "zombie_claw_hit";
        /// 보스 바닥 예고 원 강조 — ProtoBossFx.ShowTelegraph. 자리 = 충돌 지점, scale = 판정 반경(m). 코드 원(테두리 · 차오르는 채움)은 그대로 둔다
        public const string BossTelegraph = "boss_telegraph";
        /// 보스 등장 — ProtoSpawnDirector.SpawnBoss (중간 · 최종). 자리 = 보스 발밑, scale = 보스 덩치(테이블 scale)
        public const string BossSpawn = "boss_spawn";
        /// 피격 피 튀김 — ProtoZombie.DealDamage (hit_spark 와 같은 자리 · 방향, 큰 한 방 제외). scale = 덩치(치명타 ×1.5)
        public const string HitBlood = "hit_blood";
        /// 회전 블레이드 꼬리(2026-11-18, 스팬) — ProtoSupportGear 가 톱날마다 켠다(WFX_OrbitBlade.trail). 자리 = 톱날 가운데, +Z = 궤도 접선(나아가는 쪽).
        /// 프리팹 FX_OrbitBlade_Trail(OrbitBladeTrailSetup 이 굽는 우리 그림) — 하늘색 빛 띠 두 겹 + 쇳불똥. 띠 층(Custom 공간)은 궤도 루트에 묶여 플레이어가 달려도 원을 그린다
        public const string OrbitBladeTrail = "orbit_blade_trail";
    }
}
