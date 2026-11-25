using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// 버프 아이템 — 승합차(ProtoDestructibles)가 터지면 떨군다. 프로토 (2026-10-14 사용자 요청).
    ///
    /// <para>4종: <b>HP</b>(최대 HP 20% 회복) · <b>자석</b>(캐릭터 반경 6m 젬 흡수) ·
    /// <b>아드레날린</b>(8초 공속 ×1.5·이속 ×1.2, 개에게 물린 감속 해제) · <b>보호막</b>(4초 무적).
    /// 확률·수치는 전부 ProtoConfig 의 Pickup* / Adrenaline* / Shield*.</para>
    ///
    /// <para>아이템은 직접 밟아야 먹는다(젬 자석 범위와 무관) — "차를 터뜨리고 가서 줍는다"가 한 동작이 되게.
    /// 아이콘·바닥 빛은 코드로 그린다(아트 없음). 밤 어둠 막(큐 2900)보다 위(3100)에 그린다.
    /// 씬 배선 없음 — 처음 떨굴 때 관리자가 자기 GameObject 를 만든다. 버프 중 플레이어 주변 표시(보호막 원·
    /// 아드레날린 바닥 링)도 여기서 그린다.</para>
    /// </summary>
    public class ProtoPickups : MonoBehaviour
    {
        /// Chest = 무기 진화 상자(엘리트·중간 보스, 2026-10-21). 승합차에서는 안 나온다(PickKind 가중치에 없음).
        /// 사라지지 않고, 주우면 진화권(ProtoEvolution.OnChestOpened).
        public enum Kind { HP, Magnet, Adrenaline, Shield, Chest }

        const float FlyTime = 0.5f, FlyHeight = 2.0f;   // 차에서 튀어나와 떨어지는 포물선
        const float IconSize = 0.8f, IconLift = 0.75f;
        const float BlinkTime = 4f;
        const float TakeTime = 0.22f;                    // 먹을 때 커지며 사라지는 시간

        static readonly Color[] KindCol =
        {
            new Color(0.95f, 0.25f, 0.28f, 1f),   // HP — 빨강
            new Color(0.25f, 0.55f, 1.00f, 1f),   // 자석 — 파랑
            new Color(1.00f, 0.60f, 0.10f, 1f),   // 아드레날린 — 주황
            new Color(0.30f, 0.90f, 1.00f, 1f),   // 보호막 — 하늘
            new Color(1.00f, 0.79f, 0.29f, 1f),   // 진화 상자 — 금색(ProtoEvolution.Gold)
        };
        // 이펙트 셰이더의 _BaseMap · _BaseColor(2026-11-23 — 옛 Sprites/Default 의 _MainTex · _Color). ID 는 미리 구해 둔 것
        static int MainTexID => ProtoFxMat.TexId;
        static int ColorID => ProtoFxMat.ColorId;
        const float ChestIconScale = 1.4f;   // 상자는 크게 — 멀리서도 "저기 보상이 있다"가 보여야 한다

        class Item
        {
            public Kind kind;
            public Transform root, icon, glow;
            public Renderer iconR, glowR;
            public Vector3 from, to;
            public float born, takenAt = -1f;
            public bool active;
        }

        static ProtoPickups _inst;
        static Material _iconMat, _glowMat;
        static Mesh _quad;
        static Texture2D[] _icons;
        static Texture2D _glowTex, _ringTex;

        readonly List<Item> _items = new List<Item>();
        MaterialPropertyBlock _mpb;
        Transform _shieldFx, _adrenFx, _magnetFx;
        Renderer _shieldR, _adrenR, _magnetR;
        float _magnetPulseAt = -99f;
        const float MagnetPulseTime = 0.45f;

        // ── 공개 ──────────────────────────────────────────────────────
        /// 승합차가 터진 자리에서 부른다. 확률(PickupDropChance)·종류는 여기서 정한다.
        public static void DropFromVan(Vector3 center)
        {
            if (Random.value > ProtoConfig.PickupDropChance) return;
            Drop(center, PickKind());
        }

        public static void Drop(Vector3 center, Kind kind)
        {
            Ensure();
            // 플레이어 쪽으로 조금 치우쳐 떨어진다 — 차 반대편에 떨어지면 줍다가 좀비 떼에 들어간다
            Vector3 dir = Random.insideUnitSphere; dir.y = 0f;
            var p = ProtoRun.I != null ? ProtoRun.I.player : null;
            if (p != null) { Vector3 tp = p.transform.position - center; tp.y = 0f; if (tp.sqrMagnitude > 0.01f) dir = tp.normalized + dir * 0.6f; }
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            Vector3 to = center + dir.normalized * Random.Range(2.2f, 3.0f); to.y = 0f;
            var obs = ProtoObstacles.I;
            if (obs != null) to = obs.Resolve(to, 0.4f, 4);   // 다른 프롭 안에 떨어져 못 줍는 일이 없게
            float lim = ProtoConfig.ArenaHalf;
            to.x = Mathf.Clamp(to.x, -lim, lim); to.z = Mathf.Clamp(to.z, -lim, lim);
            _inst.Spawn(kind, new Vector3(center.x, 0.8f, center.z), to);
        }

        public static void ClearAll()
        {
            if (_inst == null) return;
            foreach (var it in _inst._items) { it.active = false; it.root.gameObject.SetActive(false); }
        }

        static Kind PickKind()
        {
            float wHP = ProtoConfig.PickupWeightHP;
            var p = ProtoRun.I != null ? ProtoRun.I.player : null;
            if (p != null && p.maxHP > 0f && p.hp / p.maxHP <= ProtoConfig.PickupLowHP) wHP *= ProtoConfig.PickupLowHPBoost;
            float[] w = { wHP, ProtoConfig.PickupWeightMagnet, ProtoConfig.PickupWeightAdrenaline, ProtoConfig.PickupWeightShield };
            float sum = 0f; foreach (var x in w) sum += x;
            float r = Random.value * sum;
            for (int i = 0; i < w.Length; i++) { r -= w[i]; if (r <= 0f) return (Kind)i; }
            return Kind.HP;
        }

        // ── 생성 ──────────────────────────────────────────────────────
        static void Ensure()
        {
            if (_inst != null) return;
            var go = new GameObject("_Pickups");
            _inst = go.AddComponent<ProtoPickups>();
            _inst._mpb = new MaterialPropertyBlock();
            EnsureAssets();
            _inst._shieldFx = _inst.MakeQuad(go.transform, "ShieldFx", _iconMat, out _inst._shieldR);
            _inst._adrenFx  = _inst.MakeQuad(go.transform, "AdrenalineFx", _glowMat, out _inst._adrenR);
            _inst._shieldR.material.mainTexture = _ringTex;   // 인스턴스 머티리얼 — 이 둘만 링 텍스처를 쓴다
            _inst._adrenR.material.mainTexture  = _ringTex;
            _inst._magnetFx = _inst.MakeQuad(go.transform, "MagnetFx", _glowMat, out _inst._magnetR);
            _inst._magnetFx.gameObject.SetActive(false);
            _inst._shieldFx.gameObject.SetActive(false);
            _inst._adrenFx.gameObject.SetActive(false);
        }

        void OnDestroy() { if (_inst == this) _inst = null; }

        Transform MakeQuad(Transform parent, string name, Material m, out Renderer r)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            r = mr;
            return go.transform;
        }

        void Spawn(Kind kind, Vector3 from, Vector3 to)
        {
            Item it = null;
            foreach (var x in _items) if (!x.active) { it = x; break; }
            if (it == null)
            {
                it = new Item();
                var go = new GameObject("Pickup");
                go.transform.SetParent(transform, false);
                it.root = go.transform;
                it.glow = MakeQuad(it.root, "Glow", _glowMat, out it.glowR);
                it.glow.localRotation = Quaternion.Euler(90f, 0f, 0f);
                it.glow.localPosition = new Vector3(0f, 0.04f, 0f);
                it.icon = MakeQuad(it.root, "Icon", _iconMat, out it.iconR);
                _items.Add(it);
            }
            it.kind = kind; it.from = from; it.to = to;
            it.born = Time.time; it.takenAt = -1f; it.active = true;
            it.root.position = from;
            it.root.gameObject.SetActive(true);
            _mpb.Clear();
            _mpb.SetTexture(MainTexID, _icons[(int)kind]);
            _mpb.SetColor(ColorID, Color.white);
            it.iconR.SetPropertyBlock(_mpb);
        }

        // ── 매 프레임 ─────────────────────────────────────────────────
        void Update()
        {
            var run = ProtoRun.I;
            var player = run != null ? run.player : null;
            var cam = Camera.main;
            Quaternion bill = cam != null ? cam.transform.rotation : Quaternion.identity;
            bool paused = ProtoConfig.Delta <= 0f;

            foreach (var it in _items)
            {
                if (!it.active) continue;
                float age = Time.time - it.born;

                if (it.takenAt >= 0f)
                {
                    float k = Mathf.Clamp01((Time.time - it.takenAt) / TakeTime);
                    if (player != null) it.root.position = Vector3.Lerp(it.root.position, player.transform.position, k);
                    it.icon.localScale = Vector3.one * IconSize * (1f + k * 0.8f);
                    it.icon.rotation = bill;
                    SetCol(it.iconR, new Color(1f, 1f, 1f, 1f - k), _icons[(int)it.kind]);
                    it.glow.gameObject.SetActive(false);
                    if (k >= 1f) { it.active = false; it.root.gameObject.SetActive(false); }
                    continue;
                }

                bool chest = it.kind == Kind.Chest;   // 진화 상자는 사라지지 않는다 — 놓치면 진화를 통째로 잃는다
                if (!chest && !paused && age >= ProtoConfig.PickupLife) { it.active = false; it.root.gameObject.SetActive(false); continue; }
                float size = IconSize * (chest ? ChestIconScale : 1f);

                // 포물선 → 착지 뒤 둥실
                float f = Mathf.Clamp01(age / FlyTime);
                Vector3 pos = Vector3.Lerp(it.from, it.to, f);
                pos.y = Mathf.Lerp(it.from.y, 0f, f) + FlyHeight * 4f * f * (1f - f);
                it.root.position = new Vector3(pos.x, 0f, pos.z);
                float bob = f >= 1f ? 0.12f * Mathf.Sin(Time.time * 3.2f) : 0f;
                it.icon.localPosition = new Vector3(0f, pos.y + IconLift + bob, 0f);
                it.icon.rotation = bill;
                it.icon.localScale = Vector3.one * size * (f >= 1f ? 1f + 0.05f * Mathf.Sin(Time.time * 6f) : Mathf.Lerp(0.5f, 1f, f));

                // 사라지기 전 깜빡임 — 점점 빨라진다
                float left = ProtoConfig.PickupLife - age;
                bool vis = chest || left > BlinkTime || Mathf.Sin(Time.time * Mathf.Lerp(30f, 10f, left / BlinkTime)) > -0.2f;
                it.icon.gameObject.SetActive(vis);
                it.glow.gameObject.SetActive(vis && f >= 1f);

                var c = KindCol[(int)it.kind];
                it.glow.localScale = Vector3.one * (chest ? 2.6f : 1.5f) * (1f + 0.1f * Mathf.Sin(Time.time * 4f));
                SetCol(it.glowR, new Color(c.r, c.g, c.b, 0.65f), _glowTex);

                // 줍기 — 착지한 뒤에만
                if (f >= 1f && !paused && player != null && player.IsAlive && run.IsRunning)
                {
                    Vector3 d = player.transform.position - it.root.position; d.y = 0f;
                    float reach = ProtoConfig.ItemPickupRadius + (ProtoTank.PlayerRiding ? ProtoTank.HitRadius : 0f);
                    if (d.sqrMagnitude <= reach * reach) { it.takenAt = Time.time; Apply(it.kind, player); }
                }
            }

            TickAuras(player, bill);
        }

        void Apply(Kind kind, ProtoPlayer p)
        {
            // 진화 상자 — 문구·소리는 ProtoEvolution 이 상황(바로 진화 / 진화권 보관)에 맞춰 낸다
            if (kind == Kind.Chest) { ProtoEvolution.OnChestOpened(p); return; }
            switch (kind)
            {
                case Kind.HP:         p.Heal(p.maxHP * ProtoConfig.PickupHealPct); break;
                case Kind.Magnet:
                    ProtoGemPool.I?.MagnetRadius(p.transform.position, ProtoConfig.MagnetItemRadius);
                    _magnetPulseAt = Time.time;   // 흡수 범위를 파란 원으로 보여준다
                    break;
                case Kind.Adrenaline: p.GrantAdrenaline(ProtoConfig.AdrenalineTime); break;
                case Kind.Shield:     p.GrantShield(ProtoConfig.ShieldTime); break;
            }
            string msg = kind == Kind.HP ? $"HP +{Mathf.RoundToInt(ProtoConfig.PickupHealPct * 100f)}%"
                       : kind == Kind.Magnet ? "자석!"
                       : kind == Kind.Adrenaline ? $"아드레날린 {ProtoConfig.AdrenalineTime:0}초"
                       : $"보호막 {ProtoConfig.ShieldTime:0}초";
            ProtoHUD.ShowPickup(msg, KindCol[(int)kind]);
            ProtoAudio.Play("levelup", p.transform.position, 0.6f);
        }

        /// 버프 중 플레이어 주변 표시. 보호막 = 몸을 감싸는 하늘색 원, 아드레날린 = 발밑에서 도는 주황 링.
        void TickAuras(ProtoPlayer p, Quaternion bill)
        {
            bool shield = p != null && p.IsShielded && p.IsAlive;
            if (_shieldFx.gameObject.activeSelf != shield) _shieldFx.gameObject.SetActive(shield);
            if (shield)
            {
                _shieldFx.position = p.transform.position + Vector3.up * 0.9f;
                _shieldFx.rotation = bill;
                _shieldFx.localScale = Vector3.one * (2.0f + 0.06f * Mathf.Sin(Time.time * 5f));
                // 끝나기 1초 전부터 깜빡여 "곧 풀린다"를 알린다
                float a = p.ShieldLeft > 1f ? 0.8f : (Mathf.Sin(Time.time * 30f) > 0f ? 0.8f : 0.2f);
                var c = KindCol[(int)Kind.Shield];
                SetCol(_shieldR, new Color(c.r, c.g, c.b, a), _ringTex);
            }

            // 자석 — 발밑에서 흡수 반경까지 파란 원이 퍼지며 옅어진다. 테두리가 끝나는 곳 = 흡수 범위
            float mk = (Time.time - _magnetPulseAt) / MagnetPulseTime;
            bool magnet = p != null && mk >= 0f && mk < 1f;
            if (_magnetFx.gameObject.activeSelf != magnet) _magnetFx.gameObject.SetActive(magnet);
            if (magnet)
            {
                float e = 1f - (1f - mk) * (1f - mk);
                _magnetFx.position = p.transform.position + Vector3.up * 0.06f;
                _magnetFx.rotation = Quaternion.Euler(90f, 0f, 0f);
                _magnetFx.localScale = Vector3.one * (ProtoConfig.MagnetItemRadius * 2f / 0.9f * Mathf.Lerp(0.15f, 1f, e));   // 링 텍스처 테두리가 0.9 지점
                var c = KindCol[(int)Kind.Magnet];
                SetCol(_magnetR, new Color(c.r, c.g, c.b, 0.9f * (1f - mk * mk)), _ringTex);
            }

            bool adren = p != null && p.IsAdrenaline && p.IsAlive;
            if (_adrenFx.gameObject.activeSelf != adren) _adrenFx.gameObject.SetActive(adren);
            if (adren)
            {
                _adrenFx.position = p.transform.position + Vector3.up * 0.05f;
                _adrenFx.rotation = Quaternion.Euler(90f, Time.time * 240f, 0f);
                _adrenFx.localScale = Vector3.one * (2.2f + 0.2f * Mathf.Sin(Time.time * 10f));   // 발밑 초록 링(지름 ~1.4)보다 확실히 크게
                float a = p.AdrenalineLeft > 1f ? 0.9f : (Mathf.Sin(Time.time * 30f) > 0f ? 0.9f : 0.2f);
                var c = KindCol[(int)Kind.Adrenaline];
                SetCol(_adrenR, new Color(c.r, c.g, c.b, a), _ringTex);
            }
        }

        void SetCol(Renderer r, Color c, Texture tex)
        {
            _mpb.Clear();
            _mpb.SetTexture(MainTexID, tex);
            _mpb.SetColor(ColorID, c);
            r.SetPropertyBlock(_mpb);
        }

        // ── 아이콘·텍스처 (코드로 그린다) ──────────────────────────────
        static void EnsureAssets()
        {
            if (_iconMat != null) return;
            // 이펙트 셰이더 (ProtoFxMat — 게임 이펙트 셰이더 하나, 2026-11-23)
            _iconMat = ProtoFxMat.New("PickupIcon", null, 3101);
            _glowMat = ProtoFxMat.New("PickupGlow", null, 3100);
            _quad = new Mesh { name = "PickupQuad" };
            _quad.vertices  = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
            _quad.uv        = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            _quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _quad.RecalculateBounds();

            _icons = new Texture2D[KindCol.Length];
            for (int i = 0; i < _icons.Length; i++) _icons[i] = BuildIcon((Kind)i, 128);
            _glowTex = BuildSoft(64, false);
            _ringTex = BuildSoft(128, true);
        }

        /// 둥근 배지(종류 색) + 흰 그림 + 어두운 테두리. 4×4 슈퍼샘플로 가장자리를 부드럽게.
        static Texture2D BuildIcon(Kind kind, int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            Color bg = KindCol[(int)kind], rim = new Color(0.08f, 0.08f, 0.1f, 1f), glyph = Color.white;
            const int ss = 4;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color acc = Color.clear;
                    for (int sy = 0; sy < ss; sy++)
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / ss) / n) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / ss) / n) * 2f - 1f;
                            float r = Mathf.Sqrt(u * u + v * v);
                            Color c;
                            if (r > 0.98f) c = Color.clear;
                            else if (r > 0.86f) c = rim;
                            else if (Glyph(kind, u / 0.86f, v / 0.86f, out bool accent)) c = accent ? new Color(1f, 0.3f, 0.3f, 1f) : glyph;
                            else c = Color.Lerp(bg, bg * 0.7f, (0.5f - v * 0.5f) * 0.6f);   // 아래쪽이 조금 어둡다
                            c.a = r > 0.98f ? 0f : 1f;
                            acc += c;
                        }
                    px[y * n + x] = acc / (ss * ss);
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static bool Glyph(Kind kind, float u, float v, out bool accent)
        {
            accent = false;
            switch (kind)
            {
                case Kind.HP:   // 십자
                    return (Mathf.Abs(u) < 0.2f && Mathf.Abs(v) < 0.6f) || (Mathf.Abs(v) < 0.2f && Mathf.Abs(u) < 0.6f);
                case Kind.Magnet:   // 말굽 자석 — 아래가 둥글고 위로 두 다리, 끝은 빨강
                {
                    float cy = 0.0f, r = Mathf.Sqrt(u * u + (v - cy) * (v - cy));
                    bool arc  = v <= cy && r > 0.25f && r < 0.58f;
                    bool legs = v > cy && v < 0.6f && Mathf.Abs(u) > 0.25f && Mathf.Abs(u) < 0.58f;
                    if (legs && v > 0.36f) accent = true;
                    return arc || legs;
                }
                case Kind.Adrenaline:   // 번개
                    return InPoly(u, v, new[] { new Vector2(0.18f, 0.68f), new Vector2(-0.38f, -0.06f), new Vector2(-0.02f, -0.06f),
                                                new Vector2(-0.18f, -0.68f), new Vector2(0.38f, 0.10f), new Vector2(0.02f, 0.10f) });
                case Kind.Chest:   // 보물 상자 — 둥근 뚜껑 + 몸통, 뚜껑 이음새와 자물쇠 구멍은 비운다
                {
                    if (Mathf.Abs(u) > 0.62f) return false;
                    bool lid  = v > 0.06f && v < 0.5f && (v < 0.26f || u * u / (0.62f * 0.62f) + (v - 0.26f) * (v - 0.26f) / (0.24f * 0.24f) < 1f);
                    bool body = v < 0.0f && v > -0.5f;
                    bool lockHole = Mathf.Abs(u) < 0.09f && v > -0.2f && v < 0.12f;
                    return (lid || body) && !lockHole;
                }
                default:   // 보호막 — 방패
                {
                    if (v > 0.55f || v < -0.68f) return false;
                    if (v >= 0f) return Mathf.Abs(u) < 0.5f;
                    float half = 0.5f * Mathf.Pow(Mathf.Clamp01(1f + v / 0.68f), 0.6f);   // 아래로 갈수록 좁아져 끝이 뾰족 — 둥글면 자석 U 와 헷갈린다
                    return Mathf.Abs(u) < half;
                }
            }
        }

        static bool InPoly(float x, float y, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y) && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        /// ring=false: 부드러운 원(바닥 빛), ring=true: 테두리가 밝은 고리(보호막·아드레날린).
        static Texture2D BuildSoft(int n, bool ring)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = ring
                        ? Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(r - 0.9f) / 0.08f), r < 0.9f ? 0.32f * r * r : 0f)
                        : Mathf.Clamp01(1f - r) * Mathf.Clamp01(1f - r);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            return t;
        }
    }
}
