using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 투사체 1발의 성질. 무기마다 다르므로 인자로 넘긴다 — 예전에는 전부 ProtoConfig 상수였다.
    public struct ProjectileSpec
    {
        public float speed, radius, life, size, aoeRadius, knockbackMult;
        public int pierce;
        public GameObject model;      // null 이면 기본 구슬
        public bool facesTravel;
        public bool noShake;          // 착탄 폭발이 화면을 흔들지 않는다 (탱크 포)
        public Color tracerColor;     // 기본 예광탄 색. 알파 0 = 기본 노랑(머티리얼 색 그대로)
        public float tracerLength;    // 기본 예광탄 꼬리 길이 배율. 0 이하면 1
        public float tracerWidth;     // 기본 예광탄 굵기 배율. 0 이하면 1
        public WeaponFxSet fx;        // 무기 연출 세트(피격 불똥 · 처치 · 폭발). null 이면 기본 세트

    }

    /// 투사체. 물리 엔진을 쓰지 않고 거리 판정으로 명중을 처리한다 (GDD §16 방향).
    /// 풀링되므로 Update를 갖지 않고 Shooter가 Tick으로 굴린다 — MonoBehaviour Update 수백 개를 피한다.
    public class ProtoProjectile : MonoBehaviour
    {
        Vector3 _dir;
        float _damage;
        float _dieAt;
        bool  _crit;
        ProjectileSpec _spec;
        int _pierceLeft;

        // 풀에서 재사용되므로 지금 어떤 모델을 붙여놨는지 기억해야 한다. 같은 모델이면
        // 그대로 쓰고, 다른 무기의 발사체로 재사용될 때만 갈아끼운다.
        GameObject _visual;
        GameObject _visualOf;
        /// 총구에서 지금까지 날아간 거리(m). 착탄 폭발 크기를 거리로 줄이는 데 쓴다(WeaponFxSet.ExplodeDistanceScale).
        public float Traveled => _traveled;
        MeshRenderer _ball;      // 기본 모델 = 예광탄(ProtoGunFx). 이름은 구슬 시절 그대로
        float _baseScale, _traveled;
        // 예광탄 색은 MPB 로 칠한다 — 머티리얼은 풀 전체가 공유한다. 바뀔 때만 다시 쓴다.
        static MaterialPropertyBlock _mpb;
        static int ColorID => ProtoFxMat.ColorId;   // 예광탄 머티리얼(ProtoGunFx) = 이펙트 셰이더의 _BaseColor(2026-11-23 — 옛 Sprites/Default 의 _Color)
        Color _tintNow = new Color(1f, 1f, 1f, 0f);

        // 관통할 때 같은 적을 여러 번 때리지 않게 기억한다. 풀에서 재사용되므로 발사마다 비운다.
        readonly List<ProtoZombie> _hit = new List<ProtoZombie>(4);

        public void Launch(Vector3 origin, Vector3 dir, float damage, bool crit, ProjectileSpec spec)
        {
            _dir = dir; _damage = damage; _crit = crit; _spec = spec;
            _pierceLeft = spec.pierce;
            _hit.Clear();
            _dieAt = Time.time + spec.life;
            transform.SetParent(null, true);
            transform.position = origin;
            // 크리는 조금 크게 보여준다. 무기별 기본 크기(로켓은 크고 펠릿은 작다) 위에 곱한다.
            _baseScale = spec.size * (crit ? 1.45f : 1f);
            _traveled = 0f;
            transform.localScale = Vector3.one * _baseScale;
            ApplyVisual(spec.model);
            // 모델은 코가 +Z 라 진행 방향으로 그냥 돌리면 된다. 기본 예광탄도 +Z 가 탄두라 같이 돌린다.
            bool orient = dir.sqrMagnitude > 0.0001f && (spec.model == null || spec.facesTravel);
            transform.rotation = orient ? Quaternion.LookRotation(dir) : Quaternion.identity;
            if (spec.model == null) { TintTracer(spec.tracerColor); StretchTracer(); }
            gameObject.SetActive(true);
        }

        /// 무기별 예광탄 색(진화 무기 구분). 알파 0 이면 MPB 를 비워 머티리얼 기본색으로 돌린다.
        void TintTracer(Color c)
        {
            if (_ball == null || c == _tintNow) return;
            _tintNow = c;
            if (c.a <= 0f) { _ball.SetPropertyBlock(null); return; }
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.Clear();
            _mpb.SetColor(ColorID, c);
            _ball.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 모델을 붙이거나 기본 구슬로 되돌린다. 풀에서 나온 개체가 이미 같은 모델을 달고 있으면
        /// 아무것도 하지 않는다 — 매 발사마다 Instantiate/Destroy 하면 풀링의 의미가 없어진다.
        /// </summary>
        void ApplyVisual(GameObject model)
        {
            if (_ball == null) _ball = GetComponent<MeshRenderer>();

            if (_visualOf == model) return;      // 첫 발사(둘 다 null)도 여기서 걸린다
            _visualOf = model;

            if (_visual != null) { Destroy(_visual); _visual = null; }

            if (model == null)
            {
                if (_ball != null) _ball.enabled = true;   // 기본 구슬로 복귀
                return;
            }

            if (_ball != null) _ball.enabled = false;      // 구슬은 감춘다
            _visual = Instantiate(model, transform);
            _visual.transform.localPosition = Vector3.zero;
            _visual.transform.localRotation = Quaternion.identity;
            // 모델 루트에 구워진 스케일은 유지한다 — 1 로 덮으면 아티스트가 잡은 비율이 사라진다.
        }

        /// 살아 있으면 true, 소멸해야 하면 false
        public bool Tick(float dt, ProtoSpawnDirector dir)
        {
            if (Time.time >= _dieAt) return false;

            float step = _spec.speed * dt;
            transform.position += _dir * step;
            _traveled += step;
            if (_spec.model == null) StretchTracer();

            if (dir == null) return true;

            // 이동 구간을 고려해 판정 반경을 살짝 키운다 (빠른 투사체가 적을 통과하는 문제 방지)
            float rBase = _spec.radius + step * 0.5f;
            var list = dir.Active;
            Vector3 me = transform.position - Vector3.up * 0.9f;   // 루프 밖에서 한 번(2026-11-20)
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                // 덩치가 기준(잡좀비)보다 큰 적은 그 초과분만큼 판정을 넓힌다. 보스는 몸 반경이
                // 0.55라 고정 반경만 쓰면 어깨 쪽으로 총알이 그냥 통과한다. 좀비는 초과분 0 → 기존과 동일.
                float r = rBase + Mathf.Max(0f, z.bodyRadius - ProtoConfig.BaseBodyRadius);
                float rSq = r * r;
                Vector3 d = z.transform.position - me;
                if (d.sqrMagnitude > rSq) continue;
                if (_hit.Contains(z)) continue;   // 이미 맞힌 적 — 거리 검사 뒤로(예전엔 모든 좀비마다 목록을 먼저 훑었다)

                if (_spec.aoeRadius > 0.01f)
                {
                    // 착탄 폭발 — 직격한 적도 포함해 반경 안 전체에 같은 피해를 준다(§05 '착탄 반경 폭발').
                    Explode(dir, transform.position);
                    return false;
                }

                // 진행 방향을 넘겨 좀비가 총알이 온 쪽으로 밀려나게 한다.
                // 크리 여부도 같이 넘긴다 — 탄착음이 갈라져야 크리가 귀로도 읽힌다.
                // 연출은 무기 세트대로, 자리는 총알 위치를 몸 표면으로 당긴 점(판정 반경이 넉넉해 총알은 몸 앞에서 맞는다 — 그대로 내면 불똥이 허공에 뜬다).
                z.TakeDamage(_damage, _dir * _spec.knockbackMult, _crit, false, HitFx.From(_spec.fx, z.SurfacePoint(transform.position)));
                _hit.Add(z);
                if (_pierceLeft <= 0) return false;
                _pierceLeft--;
            }

            // 버려진 승합차(ProtoDestructibles) — 맞으면 거기서 멈춘다. 로켓은 차에서 터진다.
            if (ProtoDestructibles.ProjectileHit(transform.position, rBase, _spec.aoeRadius > 0.01f ? 0f : _damage))
            {
                if (_spec.aoeRadius > 0.01f) Explode(dir, transform.position);
                return false;
            }
            return true;
        }

        /// 예광탄 꼬리는 날아간 거리만큼만 자란다 — 처음부터 다 펴면 꼬리가 총 뒤로, 캐릭터 몸을 뚫고 나온다.
        void StretchTracer()
        {
            float len = _spec.tracerLength > 0f ? _spec.tracerLength : 1f;   // 슬러그 레일은 길게 — 관통하는 한 줄로 보이게
            float full = _baseScale * len * 1.25f / 0.22f;   // ProtoGunFx 의 예광탄 길이(크기 1 기준)와 같은 값
            float k = Mathf.Clamp(_traveled / Mathf.Max(0.01f, full), 0.08f, 1f);
            float wid = _baseScale * (_spec.tracerWidth > 0f ? _spec.tracerWidth : 1f);   // 산탄총은 굵게(2026-10-21)
            transform.localScale = new Vector3(wid, wid, _baseScale * len * k);
        }

        void Explode(ProtoSpawnDirector dir, Vector3 center)
        {
            // 연출을 먼저 띄운다 — 어느 효과인지는 무기 세트(explode 자리), 기본은 코드 그림 explosion.
            // 반경을 그대로 넘기는 이유: 충격파 링의 테두리가 곧 판정 반경이라
            // 무엇이 맞았는지가 화면에서 읽힌다 — 데미지 숫자를 안 쓰는 게임(§15)에서 유일한 단서다.
            var set = _spec.fx != null ? _spec.fx : WeaponFxSet.Default;
            if (set.explode != null && set.explode.On)
            {
                var c = FxContext.At(center);
                c.radius = _spec.aoeRadius;
                c.scale = set.explode.scale * set.ExplodeDistanceScale(_traveled);   // 가까이서 터지면 작게(세트가 정한 무기만 — 탱크 포)
                c.big = true;
                c.quiet = _spec.noShake;   // 탱크 포는 흔들지 않는다
                ProtoFx.Play(set.explode.fxId, c);
                // 카탈로그에 프리팹이 꽂힌 폭발(탱크 포 tank_cannon_hit — 2026-11-17)은 코드 그림이 안 돌아 그 안의 폭발음 · 풀 눕힘이
                // 같이 빠진다 → 따로 낸다(탈것 폭발 VehicleBlastFx 와 같은 함정). 흔들림은 원래 quiet 면 없고, 아니면 프리팹 쪽에 없다.
                if (ProtoFx.HasPrefab(set.explode.fxId)) ProtoExplosion.Aftermath(center, _spec.aoeRadius);
            }
            ProtoDestructibles.DamageInRadius(center, _spec.aoeRadius, _damage);   // 반경 안 승합차도

            float rSq = _spec.aoeRadius * _spec.aoeRadius;
            var list = dir.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var z = list[i];
                if (z == null || !z.Alive) continue;
                Vector3 d = z.transform.position - center;
                d.y = 0f;
                if (d.sqrMagnitude > rSq) continue;
                // 그을음을 **피해보다 먼저** 붙인다. 이 폭발로 죽는 좀비도 검은 채로 쓰러져야
                // 한다 — TakeDamage 안에서 죽으면 그 뒤에 붙여도 사망 연출을 못 잡는다.
                z.ApplyBlastSoot();
                // 폭심에서 밖으로 밀어낸다. 정확히 폭심이면 투사체 진행 방향을 쓴다.
                Vector3 push = d.sqrMagnitude > 0.0001f ? d.normalized : _dir;
                z.TakeDamage(_damage, push * _spec.knockbackMult, _crit, true, HitFx.Blast(set));   // 큰 한 방 — 데미지 숫자. 자리 불똥 없음
            }
        }
    }
}
