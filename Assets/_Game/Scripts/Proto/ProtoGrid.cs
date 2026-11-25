using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// 균일 공간 해시 그리드 (GDD §16 — Unity Physics 대신 쓸 자체 충돌 구조).
    ///
    /// 좀비 상호 밀어내기를 O(n²) 없이 처리한다. 90마리면 전수 비교가 4,005쌍이지만
    /// 그리드로는 각 좀비가 주변 3×3 셀만 본다. 이 구조는 나중에 VAT + Job 구조로 갈 때
    /// 그대로 재사용할 수 있게 배열 기반 카운팅 소트로 짰다 — 매 프레임 할당이 없다.
    public class ProtoGrid
    {
        readonly float _cell;
        readonly float _invCell;
        readonly float _origin;     // 월드 -origin 이 셀 (0,0)
        readonly int   _side;       // 한 변 셀 개수

        int[] _cellCount;
        int[] _cellStart;
        int[] _items;               // 셀 순서로 정렬된 인덱스
        Vector2[] _push;            // 프레임당 누적 밀어내기
        Vector3[] _pos;             // 1번에서 한 번 읽은 위치 — 2~5번은 이것을 쓴다(2026-11-20: 예전엔 쌍마다 transform.position)
        float _cx, _cz;             // 격자 가운데(월드) — 플레이어를 따라간다

        public int LastPairChecks { get; private set; }

        public ProtoGrid(float worldHalf, float cellSize)
        {
            _cell    = Mathf.Max(0.25f, cellSize);
            _invCell = 1f / _cell;
            _origin  = worldHalf;
            _side    = Mathf.Max(1, Mathf.CeilToInt(worldHalf * 2f * _invCell));

            int cells = _side * _side;
            _cellCount = new int[cells];
            _cellStart = new int[cells + 1];
        }

        int CellIndex(float x, float z)
        {
            int cx = Mathf.Clamp((int)((x - _cx + _origin) * _invCell), 0, _side - 1);
            int cz = Mathf.Clamp((int)((z - _cz + _origin) * _invCell), 0, _side - 1);
            return cz * _side + cx;
        }

        void EnsureCapacity(int n)
        {
            if (_items == null || _items.Length < n) _items = new int[Mathf.NextPowerOfTwo(Mathf.Max(64, n))];
            if (_push  == null || _push.Length  < n) _push  = new Vector2[Mathf.NextPowerOfTwo(Mathf.Max(64, n))];
            if (_pos   == null || _pos.Length   < n) _pos   = new Vector3[Mathf.NextPowerOfTwo(Mathf.Max(64, n))];
        }

        /// 좀비 상호 밀어내기. 겹친 만큼 절반씩 서로 밀어낸다.
        /// minDist 는 두 좀비 중심이 이보다 가까우면 겹친 것으로 본다.
        public void Separate(IReadOnlyList<ProtoZombie> items, float minDist, float strength, float dt)
            => Separate(items, minDist, strength, dt, Vector3.zero);

        /// center = 격자 가운데(보통 플레이어). 2026-11-20 최적화: 격자가 월드 원점(±22m)에 고정돼 있어 도시 맵(±84m)에서
        /// 플레이어가 원점에서 멀어지면 좀비가 전부 가장자리 칸 하나로 몰려 사실상 전수 비교(90마리 = 4,005쌍)가 됐다.
        /// 가운데를 칸 단위로 맞춰 따라가게 했다 — 겹친 쌍은 예전과 똑같이 다 찾는다(격자 밖 좀비도 가장자리 칸에서 서로 본다).
        public void Separate(IReadOnlyList<ProtoZombie> items, float minDist, float strength, float dt, Vector3 center)
        {
            int n = items.Count;
            if (n < 2) return;
            EnsureCapacity(n);
            LastPairChecks = 0;
            _cx = Mathf.Floor(center.x * _invCell) * _cell;
            _cz = Mathf.Floor(center.z * _invCell) * _cell;

            // ── 1) 셀별 개수 ────────────────────────────────────────────
            System.Array.Clear(_cellCount, 0, _cellCount.Length);
            for (int i = 0; i < n; i++)
            {
                var z = items[i];
                if (z == null || !z.Alive) continue;
                var p = z.transform.position;
                _pos[i] = p;
                _cellCount[CellIndex(p.x, p.z)]++;
            }

            // ── 2) 접두사 합으로 시작 오프셋 ────────────────────────────
            int run = 0;
            for (int c = 0; c < _cellCount.Length; c++)
            {
                _cellStart[c] = run;
                run += _cellCount[c];
            }
            _cellStart[_cellCount.Length] = run;

            // ── 3) 인덱스 배치 (cursor 로 채운다) ───────────────────────
            //     _cellCount 를 커서로 재사용한다
            System.Array.Copy(_cellStart, _cellCount, _cellCount.Length);
            for (int i = 0; i < n; i++)
            {
                var z = items[i];
                if (z == null || !z.Alive) continue;
                var p = _pos[i];
                _items[_cellCount[CellIndex(p.x, p.z)]++] = i;
            }

            for (int i = 0; i < n; i++) _push[i] = Vector2.zero;

            // ── 4) 3×3 이웃 셀만 비교 ──────────────────────────────────
            //     최소 거리는 쌍마다 몸 반경으로 계산한다 (아래). minDist 는 하한.
            for (int cz = 0; cz < _side; cz++)
            {
                for (int cx = 0; cx < _side; cx++)
                {
                    int cell = cz * _side + cx;
                    int s = _cellStart[cell], e = _cellStart[cell + 1];
                    if (s == e) continue;

                    for (int nz = cz; nz <= cz + 1; nz++)
                    {
                        if (nz >= _side) continue;
                        int xFrom = (nz == cz) ? cx : cx - 1;
                        for (int nx = xFrom; nx <= cx + 1; nx++)
                        {
                            if (nx < 0 || nx >= _side) continue;
                            int other = nz * _side + nx;
                            if (other < cell) continue;              // 같은 쌍을 두 번 보지 않는다
                            int s2 = _cellStart[other], e2 = _cellStart[other + 1];
                            if (s2 == e2) continue;

                            for (int a = s; a < e; a++)
                            {
                                int ia = _items[a];
                                var za = items[ia];
                                if (za == null || !za.Alive) continue;
                                Vector3 pa = _pos[ia];

                                int bFrom = (other == cell) ? a + 1 : s2;
                                for (int b = bFrom; b < e2; b++)
                                {
                                    int ib = _items[b];
                                    var zb = items[ib];
                                    if (zb == null || !zb.Alive) continue;

                                    Vector3 pb = _pos[ib];
                                    float dx = pb.x - pa.x, dz = pb.z - pa.z;
                                    float sq = dx * dx + dz * dz;
                                    LastPairChecks++;

                                    // 최소 거리는 두 몸 반경의 합. 보스처럼 덩치가 다른 개체가
                                    // 섞여도 맞게 나온다. minDist 는 하한 — 좀비끼리는
                                    // 0.28+0.28+0.06 = 0.62 로 기존 값과 동일하다.
                                    float pairMin = Mathf.Max(minDist,
                                        za.bodyRadius + zb.bodyRadius + ProtoConfig.SeparationPad);
                                    if (sq >= pairMin * pairMin) continue;

                                    // 밀릴 비중. 합이 0이면 둘 다 부동(보스끼리) → 건너뛴다.
                                    float wa = za.separationWeight, wb = zb.separationWeight;
                                    float wsum = wa + wb;
                                    if (wsum < 0.0001f) continue;

                                    float dist = Mathf.Sqrt(sq);
                                    // 완전히 겹쳐 방향이 없으면 인덱스로 결정론적 방향을 만든다
                                    if (dist < 0.0001f)
                                    {
                                        float ang = (ia * 2.399963f);
                                        dx = Mathf.Cos(ang); dz = Mathf.Sin(ang); dist = 0.0001f;
                                    }
                                    // 침투량을 무게 반대로 나눠 가진다. 둘 다 1이면 기존처럼 반반.
                                    float total = pairMin - dist;
                                    float ux = dx / dist, uz = dz / dist;
                                    float fa = total * (wa / wsum), fb = total * (wb / wsum);
                                    _push[ia].x -= ux * fa; _push[ia].y -= uz * fa;
                                    _push[ib].x += ux * fb; _push[ib].y += uz * fb;
                                }
                            }
                        }
                    }
                }
            }

            // ── 5) 적용 ────────────────────────────────────────────────
            float maxStep = 6f * dt;   // 한 프레임에 과하게 튀지 않게 제한
            for (int i = 0; i < n; i++)
            {
                var z = items[i];
                if (z == null || !z.Alive) continue;
                Vector2 d = _push[i] * strength;
                float mag = d.magnitude;
                if (mag < 0.0001f) continue;
                if (mag > maxStep) d *= maxStep / mag;

                var p = _pos[i];   // 1번 뒤로 이 좀비를 옮긴 곳이 없다 — 같은 값
                Vector3 next = new Vector3(p.x + d.x, p.y, p.z + d.y);

                // 밀려나서 프롭/아레나 밖으로 나가지 않게
                float lim = ProtoConfig.ArenaHalf;
                next.x = Mathf.Clamp(next.x, -lim, lim);
                next.z = Mathf.Clamp(next.z, -lim, lim);
                var obs = ProtoObstacles.I;
                if (obs != null && obs.blockEnemies) next = obs.ResolveMove(next, z.bodyRadius, 1);

                z.transform.position = next;
            }
        }
    }
}
