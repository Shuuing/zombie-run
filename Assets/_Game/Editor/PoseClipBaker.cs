using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieOverrun.EditorTools
{
    /// <summary>
    /// 블렌더에서 뽑은 '본 월드 회전' 자세 파일 → 캐릭터 아바타 기준 휴머노이드 클립(.anim).
    ///
    /// <para><b>왜 필요한가.</b> Mixamo 애니 FBX 는 파일마다 자기 첫 자세로 아바타를 만든다. 그 기준 자세가 캐릭터
    /// (04_unity_sushi_girl)와 크게 달라(왼손 123°, 엄지 57°, 왼팔 13°) Unity 리타깃 결과가 원본과 딴판이 된다 —
    /// Rifle Idle 은 원본에서 왼팔이 가슴 앞인데 인게임은 허리 옆, 손가락 근육은 ±11(정상 ±1)로 손이 찌그러졌다.
    /// 애니 FBX 의 아바타 기준 자세만 캐릭터 것으로 바꿔 봤지만 결과가 거의 안 변했다.</para>
    ///
    /// <para><b>어떻게.</b> 블렌더(<c>ArtSource/Anim/dump_raw_pose.py</c> / <c>dump_max_pose.py</c>)가 캐릭터 리그에 원본 액션을
    /// 그대로 얹은 자세를 Unity 좌표 월드 회전으로 프레임마다 뽑는다. 여기서는 실제 캐릭터 프리팹에 그 회전을 세운 뒤
    /// <see cref="HumanPoseHandler"/> 로 '이 캐릭터 아바타의' 근육 값을 읽어 곡선에 굽는다. 같은 아바타로 재생하므로
    /// 관절 위치가 원본과 2mm 안으로 맞는다(사격 클립에서 확인). 본 축 차이 K 는 블렌더 바인드 자세 ↔ 스킨 bindpose 로 구한다.</para>
    ///
    /// <para>자세 파일 형식: 첫 줄 머리말, <c>rest 본이름 3x3</c>(바인드 자세), <c>프레임 본이름 3x3</c>(그 프레임의 월드 회전),
    /// 그 밖의 키(<c>프레임 weapon 3x3</c> 등)는 호출한 쪽이 <c>adjust</c> 에서 쓴다.</para>
    /// </summary>
    public static class PoseClipBaker
    {
        public const string Prefab = "Assets/_Game/Art/character/an/Prefabs/SushiGirl_Rifle.prefab";
        public const string Controller = "Assets/_Game/Art/character/an/Controllers/sushi_girl_rifle.controller";

        public class Pose
        {
            public List<string> order = new List<string>();                       // 부모 → 자식 순
            public Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
            public List<Dictionary<string, Quaternion>> frames = new List<Dictionary<string, Quaternion>>();
        }

        /// <summary>
        /// <paramref name="src"/> 의 곡선을 모두 복사하고, 근육(<paramref name="musclePrefixes"/> 로 거름, null 이면 전부)과
        /// 몸 회전(RootQ)을 자세 파일에서 구운 값으로 바꿔 <paramref name="outPath"/> 에 저장한다.
        /// <paramref name="adjust"/>(프레임, 그 프레임 원본 줄, 목표 월드 회전) 로 본을 세우기 전에 목표를 고칠 수 있다.
        /// </summary>
        public static AnimationClip Bake(AnimationClip src, Pose pose, string outPath, string[] musclePrefixes,
                                         System.Action<int, Dictionary<string, Quaternion>, Dictionary<string, Quaternion>> adjust,
                                         System.Text.StringBuilder log)
        {
            float fps = src.frameRate;
            int frameCount = pose.frames.Count;
            int srcFrames = Mathf.RoundToInt(src.length * fps) + 1;
            if (srcFrames != frameCount) log.Append($"  주의: 원본 클립 {srcFrames}프레임 ≠ 자세 파일 {frameCount}프레임\n");

            var muscleIdx = Enumerable.Range(0, HumanTrait.MuscleCount)
                .Where(i => musclePrefixes == null || musclePrefixes.Any(p => HumanTrait.MuscleName[i].StartsWith(p))).ToList();
            var keys = muscleIdx.ToDictionary(i => i, i => new List<Keyframe>());
            bool bakeRoot = musclePrefixes == null;
            var rootKeys = new List<Keyframe>[4] { new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>(), new List<Keyframe>() };

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            float maxPosErr = 0f; string maxPosBone = "";
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(2000, 0, 2000), Quaternion.identity);
                var anim = go.GetComponentInChildren<Animator>();
                var bones = pose.order.ToDictionary(n => n, n => FindDeep(anim.transform, n));
                var K = AxisFix(go, pose, bones, log);

                var handler = new HumanPoseHandler(anim.avatar, anim.transform);
                var hp = new HumanPose();
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Quaternion prevRoot = Quaternion.identity;
                AnimationMode.StartAnimationMode();
                try
                {
                    for (int f = 0; f < frameCount; f++)
                    {
                        float t = f / fps;
                        AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(anim.gameObject, src, Mathf.Min(t, src.length)); AnimationMode.EndSampling();
                        var target = pose.order.ToDictionary(n => n, n => pose.frames[f][n] * K[n]);
                        adjust?.Invoke(f, pose.frames[f], target);
                        foreach (var n in pose.order) bones[n].rotation = target[n];
                        var want = pose.order.ToDictionary(n => n, n => hips.InverseTransformPoint(bones[n].position));
                        handler.GetHumanPose(ref hp);
                        foreach (var i in muscleIdx) keys[i].Add(new Keyframe(t, hp.muscles[i]));
                        if (bakeRoot)
                        {
                            var q = hp.bodyRotation;
                            if (f > 0 && Quaternion.Dot(q, prevRoot) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // 부호 연속
                            prevRoot = q;
                            rootKeys[0].Add(new Keyframe(t, q.x)); rootKeys[1].Add(new Keyframe(t, q.y));
                            rootKeys[2].Add(new Keyframe(t, q.z)); rootKeys[3].Add(new Keyframe(t, q.w));
                        }
                        // 근육 → 자세로 되돌려 재현 오차(엉덩이 기준 관절 위치)
                        handler.SetHumanPose(ref hp);
                        foreach (var n in pose.order)
                        {
                            float e = (hips.InverseTransformPoint(bones[n].position) - want[n]).magnitude * hips.lossyScale.x;
                            if (e > maxPosErr) { maxPosErr = e; maxPosBone = n + " f" + f; }
                        }
                    }
                }
                finally { AnimationMode.StopAnimationMode(); }
                handler.Dispose();
            }
            finally { Object.DestroyImmediate(go); }

            var byCurve = muscleIdx.ToDictionary(i => CurveName(HumanTrait.MuscleName[i]), i => i);
            string[] rootNames = { "RootQ.x", "RootQ.y", "RootQ.z", "RootQ.w" };
            var clip = new AnimationClip { frameRate = fps };
            int replaced = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(src))
            {
                var curve = AnimationUtility.GetEditorCurve(src, b);
                List<Keyframe> k = null;
                if (b.type == typeof(Animator) && byCurve.TryGetValue(b.propertyName, out int mi)) { k = keys[mi]; replaced++; }
                else if (bakeRoot && b.type == typeof(Animator) && System.Array.IndexOf(rootNames, b.propertyName) >= 0) k = rootKeys[System.Array.IndexOf(rootNames, b.propertyName)];
                if (k != null) curve = Smooth(new AnimationCurve(k.ToArray()));
                AnimationUtility.SetEditorCurve(clip, b, curve);
            }
            AnimationUtility.SetAnimationClipSettings(clip, AnimationUtility.GetAnimationClipSettings(src));

            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; EditorUtility.SetDirty(existing); }
            else AssetDatabase.CreateAsset(clip, outPath);
            clip.name = Path.GetFileNameWithoutExtension(outPath);

            log.Append($"  {clip.name}: {frameCount}프레임, 근육 {replaced}/{muscleIdx.Count}" + (bakeRoot ? " + 몸 회전" : "") +
                       $", 재현 최대 위치 오차 {maxPosErr * 100f:F1}cm ({maxPosBone})\n");
            if (replaced != muscleIdx.Count) log.Append("  경고: 일부 근육 이름이 클립 곡선과 안 맞음\n");
            return clip;
        }

        /// <summary>본 축 차이 K_b = (블렌더 바인드 회전)⁻¹ · 유니티 바인드 회전. 본끼리 같아야 정상(0°).</summary>
        static Dictionary<string, Quaternion> AxisFix(GameObject go, Pose pose, Dictionary<string, Transform> bones, System.Text.StringBuilder log)
        {
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(s => s.bones.Length).First();
            var K = new Dictionary<string, Quaternion>();
            foreach (var n in pose.order)
            {
                int bi = System.Array.IndexOf(smr.bones, bones[n]);
                if (bi < 0) { log.Append("  경고: 스킨에 없는 본 " + n + "\n"); K[n] = Quaternion.identity; continue; }
                var bindWorld = (smr.transform.localToWorldMatrix * smr.sharedMesh.bindposes[bi].inverse).rotation;
                K[n] = Quaternion.Inverse(pose.rest[n]) * bindWorld;
            }
            var k0 = K[pose.order[0]];
            float spread = pose.order.Max(n => Quaternion.Angle(K[n], k0));
            if (spread > 1f) log.Append($"  경고: 본 축 차이 K 가 본마다 다름(최대 {spread:F1}°) — 좌표 변환을 의심할 것\n");
            return K;
        }

        static AnimationCurve Smooth(AnimationCurve c)
        {
            for (int k = 0; k < c.length; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, k, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, k, AnimationUtility.TangentMode.ClampedAuto);
            }
            return c;
        }

        /// <summary>근육 이름 → 클립 곡선 속성 이름. 몸통은 같고, 손가락은 "Left Thumb 1 Stretched" → "LeftHand.Thumb.1 Stretched".</summary>
        public static string CurveName(string muscle)
        {
            foreach (var side in new[] { "Left", "Right" })
                foreach (var fn in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                {
                    var pre = side + " " + fn + " ";
                    if (!muscle.StartsWith(pre)) continue;
                    var rest = muscle.Substring(pre.Length);              // "1 Stretched" | "Spread"
                    return rest == "Spread" ? $"{side}Hand.{fn}.Spread" : $"{side}Hand.{fn}.{rest[0]} {rest.Substring(2)}";
                }
            return muscle;
        }

        public static Pose ReadPose(string path)
        {
            var p = new Pose();
            foreach (var line in File.ReadAllLines(path).Skip(1))
            {
                var s = line.Split(' ');
                if (s.Length < 3) continue;
                if (s[0] == "rest") { p.rest[s[1]] = Q(s[2]); p.order.Add(s[1]); continue; }
                if (!int.TryParse(s[0], out int f)) continue;      // weaponpos 같은 부가 줄은 호출한 쪽이 직접 읽는다
                while (p.frames.Count <= f) p.frames.Add(new Dictionary<string, Quaternion>());
                p.frames[f][s[1]] = Q(s[2]);
            }
            return p;
        }

        /// <summary>컨트롤러에서 motion 이 <paramref name="from"/> 인 상태를 전부 <paramref name="to"/> 로 바꾼다.</summary>
        public static int ReplaceMotion(AnimationClip from, AnimationClip to, System.Text.StringBuilder log)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
            int n = 0;
            foreach (var l in ac.layers)
                foreach (var s in l.stateMachine.states)
                    if (s.state.motion == from) { s.state.motion = to; EditorUtility.SetDirty(s.state); n++; log.Append($"  [{l.name}] {s.state.name} ← {to.name}\n"); }
            EditorUtility.SetDirty(ac);
            return n;
        }

        public static AnimationClip FbxClip(string path) =>
            AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));

        static Quaternion Q(string csv)
        {
            var v = csv.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var m = Matrix4x4.identity;
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) m[r, c] = v[r * 3 + c];
            return m.rotation;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }
    }

    /// <summary>
    /// 컨트롤러가 쓰는 Mixamo 클립 전부를 원본 자세로 다시 굽는다(왼팔·손가락이 원본과 딴판이던 문제).
    /// 먼저 블렌더에서: <c>blender -b ArtSource/Anim/Firing_Rifle_edit.blend --python ArtSource/Anim/dump_raw_pose.py -- ArtSource/Anim/raw_pose "Rifle Idle" ...</c>
    /// 결과는 <c>an/Clips/Raw/*.anim</c>, 원본 FBX 는 그대로 두고 컨트롤러 상태만 새 클립으로 바꾼다.
    /// </summary>
    public static class RawPoseClipBaker
    {
        const string AnimDir = "Assets/_Game/Art/character/an/anii/";
        const string OutDir = "Assets/_Game/Art/character/an/Clips/Raw/";
        const string PoseDir = "ArtSource/Anim/raw_pose/";

        public static readonly string[] Clips =
        {
            "Rifle Idle", "Swagger Walk", "Run Forward", "Shoot Rifle", "Crouch Rapid Fire", "Walking", "Running",
            "Waving", "Standing Greeting", "Climbing Ladder", "Jumping Up", "Jumping Down",
        };

        /// <summary>손가락 근육만. 손가락 근육은 손 기준 상대값이라 팔·몸이 원본과 달라도 그대로 옮길 수 있다.</summary>
        public static readonly string[] Fingers =
        {
            "Left Thumb", "Left Index", "Left Middle", "Left Ring", "Left Little",
            "Right Thumb", "Right Index", "Right Middle", "Right Ring", "Right Little",
        };

        // 기본은 손가락만 고친다. 몸 전체를 원본으로 굽으면 오른손 각도가 바뀌어, 예전 리타깃 손에 맞춰 둔
        // 모든 무기 그립(W_*.asset gripPoint/weaponEuler)을 다시 맞춰야 한다(2026-10-01 확인: 총이 엉뚱한 방향).
        [MenuItem("Tools/Zombie Overrun/애니/원본 자세로 전체 클립 다시 굽기 (손가락만)")]
        public static void BakeFingersMenu() => Debug.Log(BakeAll(true));

        [MenuItem("Tools/Zombie Overrun/애니/원본 자세로 전체 클립 다시 굽기 (몸 전체 — 무기 그립 재보정 필요)")]
        public static void BakeFullMenu() => Debug.Log(BakeAll(false));

        public static string BakeAll(bool fingersOnly)
        {
            var log = new System.Text.StringBuilder();
            foreach (var name in Clips)
            {
                var posePath = PoseDir + name.Replace(" ", "_") + ".txt";
                if (!File.Exists(posePath)) { log.Append("자세 파일 없음: " + posePath + "\n"); continue; }
                var src = PoseClipBaker.FbxClip(AnimDir + name + ".fbx");
                var clip = PoseClipBaker.Bake(src, PoseClipBaker.ReadPose(posePath), OutDir + name.Replace(" ", "_") + ".anim", fingersOnly ? Fingers : null, null, log);
                PoseClipBaker.ReplaceMotion(src, clip, log);
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }
    }
}
