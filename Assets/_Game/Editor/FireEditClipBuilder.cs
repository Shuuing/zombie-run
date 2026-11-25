using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace ZombieOverrun.EditorTools
{
    /// <summary>
    /// 3ds Max 에서 고친 사격 자세 → 인게임 상체 사격 클립(.anim). 굽는 방식은 <see cref="PoseClipBaker"/> 참고.
    ///
    /// Blender 의 <c>ArtSource/Anim/dump_max_pose.py</c> 가 'Max 화면 그대로의 자세'(캐릭터 리그 + 원본 액션 + Max 편집 오른팔)를
    /// 본 월드 회전으로 프레임마다 뽑는다(<c>max_edit_pose.txt</c>). 여기서는 상체·손가락 근육만 굽는다(하체는 Base Layer 몫).
    /// 오른손은 <b>총 방향이 Max 와 같도록</b> 돌린다(<see cref="AlignGunToMax"/>) — Max 장면의 손-총 각도가 인게임 그립과 54° 달라
    /// (블렌더 손≠유니티 손이던 시절 예전 자리에 무기를 붙인 탓) 손만 맞추면 총이 24° 들려 나왔다. 그립 값은 그립 툴로 맞춘 것을 유지한다.
    /// (2026-10-01 전체 클립을 원본 자세로 굽고 소총 그립을 Max 관계로 바꿔 봤으나, 2등신 비율이라 원본 자세에서 양팔이 가슴 앞에 몰려 되돌렸다.)
    /// 결과를 sushi_girl_rifle 컨트롤러 UpperBody 레이어의 'Shoot Rifle' 상태에 꽂는다(인게임 사격은 이 상태 —
    /// Base Layer 의 Fire 상태는 전이가 없어 재생되지 않는다).
    /// </summary>
    public static class FireEditClipBuilder
    {
        const string SrcFbx = "Assets/_Game/Art/character/an/anii/Firing Rifle.fbx";
        const string OutClip = "Assets/_Game/Art/character/an/Clips/Firing_Rifle_edit.anim";
        const string PoseFile = "ArtSource/Anim/max_edit_pose.txt";
        const string GripFile = "ArtSource/Anim/weapon_in_hand.txt";    // 인게임 손→총 상대 행렬(예전 0프레임 그립)
        const bool AlignGunToMax = true;     // 소총 그립은 예전 값(그립 툴) 유지 — 손을 돌려 총 방향을 Max 와 맞춘다

        static readonly string[] UpperBody =
        {
            "Spine ", "Chest ", "UpperChest ", "Neck ", "Head ",
            "Left Shoulder", "Left Arm", "Left Forearm", "Left Hand ",
            "Right Shoulder", "Right Arm", "Right Forearm", "Right Hand ",
            "Left Thumb", "Left Index", "Left Middle", "Left Ring", "Left Little",
            "Right Thumb", "Right Index", "Right Middle", "Right Ring", "Right Little",
        };

        [MenuItem("Tools/Zombie Overrun/애니/Max 수정 사격 클립 굽기")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var log = new System.Text.StringBuilder();
            var src = PoseClipBaker.FbxClip(SrcFbx);
            var grip = ReadGrip();
            bool logged = false;
            var clip = PoseClipBaker.Bake(src, PoseClipBaker.ReadPose(PoseFile), OutClip, UpperBody, (f, raw, target) =>
            {
                // 총 방향을 Max 와 같게: 손 회전 = 총 월드 회전 · (손→총)⁻¹. 오른손가락도 손과 함께 돌린다.
                if (!AlignGunToMax || !raw.TryGetValue("weapon", out var wq)) return;
                var fix = wq * Quaternion.Inverse(grip) * Quaternion.Inverse(target["mixamorig:RightHand"]);
                if (!logged) { log.Append("  총 방향 맞춤으로 손 보정 " + Quaternion.Angle(Quaternion.identity, fix).ToString("F1") + "°\n"); logged = true; }
                foreach (var n in target.Keys.ToList()) if (n.StartsWith("mixamorig:RightHand")) target[n] = fix * target[n];
            }, log);

            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(PoseClipBaker.Controller);
            foreach (var l in ac.layers)
                if (l.name == "UpperBody")
                    foreach (var s in l.stateMachine.states)
                        if (s.state.name == "Shoot Rifle") { s.state.motion = clip; EditorUtility.SetDirty(s.state); log.Append("  UpperBody/Shoot Rifle ← " + clip.name + "\n"); }
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static Quaternion ReadGrip()
        {
            Matrix4x4 M(string key)
            {
                var v = File.ReadAllLines(GripFile).First(l => l.StartsWith(key + "=")).Substring(key.Length + 1)
                    .Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                var m = new Matrix4x4();
                for (int i = 0; i < 16; i++) m[i / 4, i % 4] = v[i];
                return m;
            }
            return (M("handW").inverse * M("weaponW")).rotation;
        }
    }
}
