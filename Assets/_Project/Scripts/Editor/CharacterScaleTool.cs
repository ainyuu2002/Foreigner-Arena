using UnityEditor;
using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// ONE-SHOT scale Emiya Archer ×2.5 (user chốt 2026-07-11 — nhân vật ~0.6u quá nhỏ so với camera
    /// ortho 3–10). Nguyên tắc vật lý: VẬN TỐC ×k và GIA TỐC ×k → mọi quỹ đạo (nhảy/dash/knockback/
    /// lao đòn) PHÓNG TO k lần nhưng TIMING GIỮ NGUYÊN — game feel y hệt cũ, chỉ to lên:
    ///   • Prefab root localScale ×k (thân + hitbox/hurtbox + VFX attach tự theo; SetFacing giữ magnitude).
    ///   • CharacterData: walk/jump/dash/wall speeds ×k, gravityScale ×k.
    ///   • KnockbackConfig: các NGƯỠNG VẬN TỐC (splat/bounce/tumble-end) ×k.
    ///   • DefenseConfig: rcDash/dodge/burst-push speeds ×k.
    ///   • Mọi MoveData Emiya: baseKnockback + knockbackGrowth (vận tốc văng) ×k, driftSpeed +
    ///     motionImpulses (P14 move-motion) ×k.
    /// VFX world-space / hit spark / projectile ĐÃ scale RUNTIME theo lossyScale owner (CharacterEffect-
    /// Player, HitEffectPlayer, ProjectileSpawner) — KHÔNG đụng asset ở đây.
    /// CHỐNG CHẠY LẶP: root scale ≠ 1 là DỪNG (2.5 × 2.5 = thảm họa). EDITOR-ONLY, chạy được headless.
    /// </summary>
    internal static class CharacterScaleTool
    {
        private const float K = 2.5f;
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataFolder = "Assets/_Project/Data/Emiya_Archer";

        [MenuItem("FA/Characters/Scale Emiya Archer x2.5 (ONE-SHOT)")]
        public static void ScaleEmiya()
        {
            // 1. Prefab root — guard one-shot.
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                float cur = Mathf.Abs(root.transform.localScale.x);
                if (cur > 1.01f)
                {
                    Debug.LogError($"[CharacterScaleTool] Root scale hiện ×{cur} (≠1) — tool ONE-SHOT, " +
                                   "KHÔNG chạy lần 2. Không đổi gì.");
                    return;
                }
                root.transform.localScale = new Vector3(K, K, 1f);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 2. CharacterData — v ×k, a ×k (giữ timing).
            Mul($"{DataFolder}/Emiya_Archer_Data.asset",
                "walkSpeed", "jumpSpeed", "dashSpeed", "wallSlideSpeed", "wallStickSpeed",
                "wallJumpSpeedX", "wallJumpSpeedY", "gravityScale");

            // 3. Knockback thresholds (đơn vị vận tốc).
            Mul($"{DataFolder}/EmiyaArcher_KnockbackConfig.asset",
                "wallSplatSpeedThreshold", "groundBounceSpeedThreshold", "tumbleEndSpeed");

            // 4. Defense speeds/forces.
            Mul($"{DataFolder}/DefenseConfig.asset",
                "rcDashSpeed", "groundDodgeBackSpeed", "burstPushbackForce", "burstPushbackUp");

            // 5. Mọi MoveData Emiya — quét System.IO (AssetDatabase.FindAssets trượt .asset máy này).
            int moves = 0;
            foreach (string path in UIKit.FindProjectFiles("*.asset"))
            {
                if (!path.Replace('\\', '/').Contains("/Data/Emiya_Archer/")) continue;
                var move = AssetDatabase.LoadAssetAtPath<FA.Core.MoveData>(path);
                if (move == null) continue;

                var so = new SerializedObject(move);
                bool dirty = MulProps(so, "baseKnockback", "knockbackGrowth", "driftSpeed");
                var imp = so.FindProperty("motionImpulses");
                if (imp != null && imp.isArray)
                    for (int i = 0; i < imp.arraySize; i++)
                    {
                        var el = imp.GetArrayElementAtIndex(i);
                        if (el.propertyType != SerializedPropertyType.Vector2) continue;
                        el.vector2Value *= K;
                        dirty = true;
                    }
                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(move);
                    moves++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[CharacterScaleTool] XONG ×{K}: prefab root + CharacterData + KnockbackConfig + " +
                      $"DefenseConfig + {moves} MoveData. Runtime (VFX/spark/projectile) tự theo lossyScale.");
        }

        private static void Mul(string path, params string[] props)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null)
            {
                Debug.LogWarning($"[CharacterScaleTool] Không thấy {path} — bỏ qua.");
                return;
            }
            var so = new SerializedObject(obj);
            if (MulProps(so, props))
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(obj);
            }
        }

        /// <summary>Nhân K các property float ≠ 0 (0 giữ 0 — vd groundDodgeBackSpeed). Trả true nếu có đổi.</summary>
        private static bool MulProps(SerializedObject so, params string[] props)
        {
            bool dirty = false;
            foreach (string name in props)
            {
                var p = so.FindProperty(name);
                if (p == null || p.propertyType != SerializedPropertyType.Float) continue;
                if (Mathf.Approximately(p.floatValue, 0f)) continue;
                p.floatValue *= K;
                dirty = true;
            }
            return dirty;
        }
    }
}
