using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 자폭 러너(빨간 좀비)가 터진 자리에 남는 독가스 — 프로토 (2026-10-14 사용자 요청).
    ///
    /// <para>터지는 순간 반경 안이면 한 방, 그 뒤 구름이 남은 동안 안에 있으면 틱 피해. 수치는 터뜨린 적의
    /// 테이블 줄(<see cref="EnemyDef.GasBlock"/>)에서 온다. <b>바닥 원 테두리가
    /// 곧 판정 반경</b>이다 — 피해 숫자를 안 쓰는 게임이라(§15) "어디까지 위험한가"는 원으로만 읽힌다.</para>
    ///
    /// <para>씬 배선 없음 — 처음 부를 때 풀 루트를 만든다(ProtoEnemyBolt 와 같은 방식).
    /// 밤 어둠 막(큐 2900)보다 위(3100)에 그린다. 물리 미사용, 평면 거리 판정.</para>
    /// </summary>
    public class ProtoPoisonGas : MonoBehaviour
    {
        const int   PuffCount = 7;
        const float BurstTime = 0.22f;   // 퍼프가 중심에서 바깥으로 터져 나가는 시간
        const float FadeTime  = 0.8f;    // 마지막 이 시간 동안 옅어진다

        static readonly Color GasCol   = new Color(0.45f, 0.95f, 0.25f, 1f);
        static readonly Color GasDark  = new Color(0.22f, 0.55f, 0.12f, 1f);
        static readonly Color FlashCol = new Color(0.85f, 1f, 0.6f, 1f);

        static readonly List<ProtoPoisonGas> _all = new List<ProtoPoisonGas>();
        static readonly Stack<ProtoPoisonGas> _free = new Stack<ProtoPoisonGas>();
        static Material _puffMat, _discMat;
        static Mesh _quad;
        static Transform _root;

        Transform _disc, _flash;
        MeshRenderer _discR, _flashR;
        readonly Transform[] _puff = new Transform[PuffCount];
        readonly MeshRenderer[] _puffR = new MeshRenderer[PuffCount];
        readonly Vector3[] _puffDir = new Vector3[PuffCount];
        readonly float[] _puffSize = new float[PuffCount], _puffPhase = new float[PuffCount], _puffDark = new float[PuffCount];
        MaterialPropertyBlock _mpb;
        float _born, _radius, _nextTick;
        float _life, _tickInterval, _tickDamage;   // 터뜨린 적의 가스 줄에서 복사 — 구름마다 다를 수 있다
        FxHandle _art;   // 카탈로그 poison_gas_cloud 프리팹(있으면 코드 퍼프 대신). 바닥 원(판정)·섬광은 코드가 계속 그린다
        bool _useArt;

        // ── 공개 ──────────────────────────────────────────────────────
        public static void Burst(Vector3 pos, EnemyDef.GasBlock gas)
        {
            if (gas == null) gas = new EnemyDef.GasBlock();
            var g = _free.Count > 0 ? _free.Pop() : Create();
            g.Begin(pos, gas);
        }

        public static void ClearAll()
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && _all[i].gameObject.activeSelf) _all[i].Recycle();
        }

        // ── 생성 ──────────────────────────────────────────────────────
        static ProtoPoisonGas Create()
        {
            EnsureAssets();
            if (_root == null) _root = new GameObject("_PoisonGas").transform;
            var go = new GameObject("Gas");
            go.transform.SetParent(_root, false);
            var g = go.AddComponent<ProtoPoisonGas>();
            g._mpb = new MaterialPropertyBlock();

            g._disc  = g.MakeQuad("Disc", _discMat, out g._discR);
            g._disc.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 바닥에 눕힌다
            g._disc.localPosition = new Vector3(0f, 0.05f, 0f);
            g._flash = g.MakeQuad("Flash", _puffMat, out g._flashR);
            for (int i = 0; i < PuffCount; i++) g._puff[i] = g.MakeQuad("Puff" + i, _puffMat, out g._puffR[i]);
            _all.Add(g);
            return g;
        }

        Transform MakeQuad(string name, Material m, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        static void EnsureAssets()
        {
            if (_puffMat != null) return;
            // 이펙트 셰이더 (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            _puffMat = ProtoFxMat.New("GasPuff", BuildPuff(96), 3101);
            _discMat = ProtoFxMat.New("GasDisc", BuildDisc(128), 3100);
            _quad = new Mesh { name = "GasQuad" };
            _quad.vertices  = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            _quad.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();
        }

        /// 울퉁불퉁한 구름 덩어리 — 원형 감쇠에 펄린 노이즈로 가장자리를 뜯는다.
        static Texture2D BuildPuff(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float noise = Mathf.PerlinNoise(x * 0.09f + 3.1f, y * 0.09f + 7.7f);
                    float a = Mathf.Clamp01((1f - r - (noise - 0.5f) * 0.55f) * 2.2f);
                    float shade = Mathf.Lerp(0.72f, 1f, Mathf.Clamp01(0.5f + v * 1.2f + (noise - 0.5f)));   // 위쪽이 밝다
                    byte c = (byte)(shade * 255);
                    px[y * n + x] = new Color32(c, c, c, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        /// 바닥 원: 속은 옅게, 테두리는 진하게 — 테두리 = 판정 반경.
        static Texture2D BuildDisc(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(u * u + v * v) * 2f;
                    float fill = r < 1f ? 0.35f : 0f;
                    float rim  = Mathf.Clamp01(1f - Mathf.Abs(r - 0.95f) / 0.05f);
                    float a = Mathf.Max(fill, rim);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        // ── 수명 ──────────────────────────────────────────────────────
        void Begin(Vector3 pos, EnemyDef.GasBlock gas)
        {
            pos.y = 0f;
            transform.position = pos;
            _born = Time.time;
            _radius = gas.radius;
            _life = gas.life;
            _tickInterval = Mathf.Max(0.05f, gas.tickInterval);
            _tickDamage = gas.tickDamage;
            _nextTick = Time.time + _tickInterval;
            for (int i = 0; i < PuffCount; i++)
            {
                float a = (i / (float)PuffCount) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                float d = i == 0 ? 0f : Random.Range(0.35f, 0.7f);
                _puffDir[i]   = new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                _puffSize[i]  = Random.Range(0.9f, 1.25f);
                _puffPhase[i] = Random.Range(0f, 10f);
                _puffDark[i]  = Random.Range(0f, 0.6f);
            }
            gameObject.SetActive(true);

            // 구름 모양: 카탈로그에 프리팹이 있으면 그것(scale = 판정 반경), 없으면 코드 퍼프 7개
            StopArt();
            _useArt = ProtoFx.HasPrefab(FxIds.PoisonGasCloud);
            for (int i = 0; i < PuffCount; i++) _puff[i].gameObject.SetActive(!_useArt);
            if (_useArt)
            {
                var c = FxContext.At(pos);
                c.scale = _radius;
                _art = ProtoFx.Play(FxIds.PoisonGasCloud, c, span: true);
            }
            Tick();   // 첫 프레임부터 제자리에 보이게

            // 터지는 순간 한 방
            HurtPlayerIfInside(gas.burstDamage);
            ProtoAudio.Play("explosion", pos, 0.55f);
            ProtoCameraFollow.Shake(ProtoCameraFollow.Impact.Light);
        }

        void Recycle()
        {
            StopArt();
            gameObject.SetActive(false);
            _free.Push(this);
        }

        /// 프리팹 구름은 방출만 멈춘다 — 남은 입자는 스스로 사그라든다(ProtoFx.Stop).
        void StopArt()
        {
            if (_art != null) { _art.Stop(); _art = null; }
        }

        void Update()
        {
            if (ProtoConfig.Delta <= 0f) return;   // 레벨업 정지 중엔 그대로 멈춰 있는다
            float age = Time.time - _born;
            if (age >= _life) { Recycle(); return; }
            Tick();

            if (Time.time >= _nextTick)
            {
                _nextTick = Time.time + _tickInterval;
                HurtPlayerIfInside(_tickDamage);
            }
        }

        void HurtPlayerIfInside(float dmg)
        {
            var run = ProtoRun.I;
            var p = run != null ? run.player : null;
            if (p == null || !run.IsRunning) return;
            Vector3 d = p.transform.position - transform.position; d.y = 0f;
            float reach = _radius + (ProtoTank.PlayerRiding ? ProtoTank.HitRadius : 0.3f);   // 몸 반경만큼 봐준다
            if (d.sqrMagnitude <= reach * reach) p.TakeDamage(dmg, ProtoPlayer.DamageKind.Poison);   // 초록 숫자 · 초록 번쩍임 · 몸 피격 이펙트(혼불탄과 같다)
        }

        void Tick()
        {
            float age  = Time.time - _born;
            float life = _life;
            float burst = Mathf.Clamp01(age / BurstTime);
            float grow  = 1f - (1f - burst) * (1f - burst) * (1f - burst);            // ease-out
            float fade  = Mathf.Clamp01((life - age) / FadeTime);
            var cam = Camera.main;
            Quaternion bill = cam != null ? cam.transform.rotation : Quaternion.identity;

            // 섬광 — 터지는 첫 0.15초
            float fk = Mathf.Clamp01(age / 0.15f);
            _flash.gameObject.SetActive(fk < 1f);
            if (fk < 1f)
            {
                _flash.position = transform.position + Vector3.up * 0.8f;
                _flash.rotation = bill;
                _flash.localScale = Vector3.one * _radius * Mathf.Lerp(1.2f, 2.6f, fk);
                var c = FlashCol; c.a = 1f - fk;
                SetCol(_flashR, c);
            }

            // 바닥 원 — 판정 반경 그대로
            _disc.localScale = Vector3.one * (_radius * 2f * Mathf.Lerp(0.3f, 1f, grow));
            var dc = GasCol; dc.a = 0.55f * fade * (0.85f + 0.15f * Mathf.Sin(Time.time * 6f));
            SetCol(_discR, dc);

            // 구름 퍼프 — 바깥으로 터져 나간 뒤 천천히 일렁이며 부풀고, 마지막에 옅어지며 떠오른다 (프리팹 구름이면 건너뛴다)
            if (_useArt) return;
            for (int i = 0; i < PuffCount; i++)
            {
                float ph = _puffPhase[i];
                Vector3 wob = new Vector3(Mathf.Sin(Time.time * 0.9f + ph), 0f, Mathf.Cos(Time.time * 0.7f + ph)) * 0.12f;
                float rise = 0.55f + 0.25f * Mathf.Sin(Time.time * 1.3f + ph) + (1f - fade) * 0.6f;
                _puff[i].position = transform.position + _puffDir[i] * _radius * grow + wob + Vector3.up * rise;
                _puff[i].rotation = bill * Quaternion.Euler(0f, 0f, ph * 36f + Time.time * 12f * (i % 2 == 0 ? 1f : -1f));
                float s = _radius * 1.15f * _puffSize[i] * Mathf.Lerp(0.25f, 1f, grow) * (1f + 0.06f * Mathf.Sin(Time.time * 2.1f + ph)) * (1f + (1f - fade) * 0.3f);
                _puff[i].localScale = Vector3.one * s;
                var pc = Color.Lerp(GasCol, GasDark, _puffDark[i]);
                pc.a = 0.62f * fade;
                SetCol(_puffR[i], pc);
            }
        }

        void SetCol(Renderer r, Color c)
        {
            _mpb.SetColor(ProtoFxMat.ColorId, c);   // 이펙트 셰이더의 _BaseColor
            r.SetPropertyBlock(_mpb);
        }
    }
}
