using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// (Item Chặng 2) DỰNG ANIM ĐÁP cho hệ item hợp nhất — Unity-native (AnimationClip/Animator), KHÔNG đếm
    /// frame trong code (CLAUDE §3.2). Idempotent (LoadOrCreate + xoá clip cũ trước khi tạo).
    ///
    /// Item_Land.controller + Item_Land.anim: clip "đáp" 1 nhịp CHỈ key Transform (rotation Z lật + squash
    /// localScale) trên child Visual (path ""), Write Defaults OFF, KHÔNG key m_Sprite/m_Color (để
    /// Item.ApplyVisual giữ icon/màu của từng bình). Gán cho Animator Visual của Item.prefab.
    ///
    /// Mystery_Block_66.controller GIỮ NGUYÊN bản gốc của dev (lật đa-mặt bằng sprite, New Animation.anim)
    /// — tool này KHÔNG đụng tới. Portal visual của spawner cũng do dev tự làm — tool này KHÔNG tạo.
    /// CLI: -executeMethod FA.EditorTools.ItemAnimSetupTool.Run
    /// </summary>
    public static class ItemAnimSetupTool
    {
        private const string AnimDir = "Assets/_Project/Animations/ItemAnime";
        private const string LandClipPath = AnimDir + "/Item_Land.anim";
        private const string LandControllerPath = AnimDir + "/Item_Land.controller";

        [MenuItem("FA/Tools/Items/1. Setup Item Anim (land)")]
        public static void Run()
        {
            EnsureFolder();
            AnimationClip land = CreateLandClip();
            CreateLandController(land);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ItemAnimSetup] DONE — Item_Land (Transform-only, WD OFF).");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(AnimDir))
                AssetDatabase.CreateFolder("Assets/_Project/Animations", "ItemAnime");
        }

        private static AnimationCurve Curve(params (float t, float v)[] keys)
        {
            var c = new AnimationCurve(keys.Select(k => new Keyframe(k.t, k.v)).ToArray());
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            return c;
        }

        private static void SetTransform(AnimationClip clip, string prop, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), prop), curve);
        }

        /// <summary>Clip đáp 1 nhịp (~0.4s): squash-and-settle + wobble Z, kết ở neutral (scale 1, rot 0).</summary>
        private static AnimationClip CreateLandClip()
        {
            AssetDatabase.DeleteAsset(LandClipPath);
            var clip = new AnimationClip { frameRate = 60f };

            SetTransform(clip, "m_LocalScale.x", Curve((0f, 1f), (0.08f, 1.28f), (0.2f, 0.92f), (0.38f, 1f)));
            SetTransform(clip, "m_LocalScale.y", Curve((0f, 1f), (0.08f, 0.68f), (0.2f, 1.12f), (0.38f, 1f)));
            SetTransform(clip, "m_LocalScale.z", Curve((0f, 1f), (0.38f, 1f)));
            SetTransform(clip, "localEulerAnglesRaw.z", Curve((0f, 0f), (0.1f, -10f), (0.24f, 6f), (0.38f, 0f)));

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            AssetDatabase.CreateAsset(clip, LandClipPath);
            return clip;
        }

        private static void CreateLandController(AnimationClip land)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LandControllerPath)
                             ?? AnimatorController.CreateAnimatorControllerAtPath(LandControllerPath);
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Land");
            if (state == null) state = sm.AddState("Land");
            state.motion = land;
            state.writeDefaultValues = false;   // WD OFF — chỉ key Transform, giữ icon/màu do ApplyVisual set.
            sm.defaultState = state;
            EditorUtility.SetDirty(controller);
        }
    }
}
