using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FA.Combat;
using FA.Resources;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DEBUG (KHÔNG phải gameplay deliverable) — dựng nhanh HUD 3 thanh (Knockback % / Mana / Rage)
    /// cho 2 fighter NGAY trong scene đang mở (vd Test_Phase05), người dùng chỉ bấm 1 menu item.
    ///
    /// EDITOR-ONLY: nằm trong asmdef FA.EditorTools.Editor (includePlatforms=Editor), được phép reference
    /// nhiều module gameplay (FA.Core/Combat/Resources/UI) vì KHÔNG có module gameplay nào reference NGƯỢC
    /// lại nó → không phá cô lập module §1.3 (ranh giới asmdef vẫn 1 chiều). KHÔNG sửa code gameplay; chỉ
    /// add/wire component + tạo asset + dựng UI bằng Unity API (AssetDatabase / SerializedObject / Undo).
    ///
    /// Việc thật chia 3 file con: <see cref="ResourceTestHudAssets"/> (tạo/tìm asset),
    /// <see cref="ResourceTestHudWiring"/> (tìm + wire fighter), <see cref="ResourceTestHudCanvas"/> (dựng UI).
    /// File này chỉ ĐIỀU PHỐI + log + 2 MenuItem.
    /// </summary>
    internal static class ResourceTestHudSetup
    {
        [MenuItem("FA/Debug/Setup Resource Test HUD (current scene)")]
        public static void Setup()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Resource Test HUD", "Không có scene hợp lệ đang mở.", "OK");
                return;
            }

            // 1) Tìm 2 fighter TRƯỚC (nếu thiếu → dừng sớm, không đẻ asset thừa).
            List<ResourceTestHudWiring.Fighter> fighters = ResourceTestHudWiring.FindFighters(out string findNote);
            if (fighters == null)
            {
                EditorUtility.DisplayDialog("Resource Test HUD",
                    "KHÔNG đủ 2 fighter trong scene đang mở.\n\n" + findNote +
                    "\n\nMở Test_Phase05 (có 2 prefab Emiya Archer) rồi chạy lại.", "OK");
                Debug.LogWarning("[ResourceTestHUD] " + findNote);
                return;
            }

            // 2) Tạo/tìm asset (config + 8 channel per-player + tìm kênh hit chung).
            ResourceTestHudAssets.Bundle b = ResourceTestHudAssets.EnsureAll();

            // 3) Add (nếu thiếu) + wire component cho từng fighter (attackerCombo CHÉO).
            ResourceTestHudWiring.WireFighter(fighters[0], fighters[1], b);
            ResourceTestHudWiring.WireFighter(fighters[1], fighters[0], b);

            // 4) Dọn HUD cũ (nếu chạy lại) rồi dựng mới.
            RemoveExistingHud();
            int maxSegments = b.ResourceConfig != null ? Mathf.Max(1, b.ResourceConfig.MaxSegments) : 3;
            float percentCap = b.KnockbackConfig != null ? Mathf.Max(1f, b.KnockbackConfig.percentCap) : 250f;
            GameObject hud = ResourceTestHudCanvas.Build(b, maxSegments, percentCap);

            // 5) Debug-reset (T = mana/nộ, R = %) gắn lên HUD, trỏ tới 2 fighter.
            WireDebugReset(hud, fighters);

            // 6) Dirty + save asset + log.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorUtility.SetDirty(hud);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogSummary(b, fighters, findNote);
        }

        [MenuItem("FA/Debug/Remove Resource Test HUD (current scene)")]
        public static void Remove()
        {
            if (RemoveExistingHud())
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log($"[ResourceTestHUD] Đã xóa '{ResourceTestHudCanvas.HudName}'.");
            }
            else
            {
                Debug.Log($"[ResourceTestHUD] Không tìm thấy '{ResourceTestHudCanvas.HudName}' để xóa.");
            }
        }

        private static bool RemoveExistingHud()
        {
            bool removed = false;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go != null && go.name == ResourceTestHudCanvas.HudName && go.transform.parent == null)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed = true;
                }
            }
            return removed;
        }

        private static void WireDebugReset(GameObject hud, List<ResourceTestHudWiring.Fighter> fighters)
        {
            var manaBars = new List<ManaBar>();
            var rageBars = new List<RageBar>();
            var percents = new List<KnockbackPercent>();
            foreach (var f in fighters)
            {
                if (f.Mana != null) manaBars.Add(f.Mana);
                if (f.Rage != null) rageBars.Add(f.Rage);
                if (f.Percent != null) percents.Add(f.Percent);
            }

            var resReset = hud.GetComponent<ResourceBarsDebugReset>();
            if (resReset == null) resReset = Undo.AddComponent<ResourceBarsDebugReset>(hud);
            var rso = new SerializedObject(resReset);
            AssignArray(rso, "manaBars", manaBars.ToArray());
            AssignArray(rso, "rageBars", rageBars.ToArray());
            rso.ApplyModifiedPropertiesWithoutUndo();

            var pctReset = hud.GetComponent<KnockbackPercentDebugReset>();
            if (pctReset == null) pctReset = Undo.AddComponent<KnockbackPercentDebugReset>(hud);
            var pso = new SerializedObject(pctReset);
            AssignArray(pso, "targets", percents.ToArray());
            pso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignArray(SerializedObject so, string prop, Object[] values)
        {
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void LogSummary(ResourceTestHudAssets.Bundle b,
                                       List<ResourceTestHudWiring.Fighter> fighters, string findNote)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[ResourceTestHUD] HOÀN TẤT dựng HUD debug 3 thanh.");
            if (!string.IsNullOrEmpty(findNote)) sb.AppendLine("• Fighter: " + findNote);
            sb.AppendLine($"• P1 = '{fighters[0].Root.name}'  |  P2 = '{fighters[1].Root.name}'");

            if (b.Created.Count > 0)
            {
                sb.AppendLine($"• Asset tạo mới ({b.Created.Count}):");
                foreach (var p in b.Created) sb.AppendLine("    - " + p);
            }
            else sb.AppendLine("• Asset: tái dùng asset Debug đã có (không tạo mới).");

            sb.AppendLine("• Component trên mỗi fighter: KnockbackPercent + ManaBar + RageBar " +
                          "(chỉ ADD nếu thiếu; field NGUỒN chỉ điền khi trống; field HIỂN THỊ per-player set về Dbg_*_Px).");

            if (b.HitChannelsAreShared)
            {
                sb.AppendLine("• onHitConfirmed/onHitBlocked: TÁI DÙNG asset chung tìm thấy trong project " +
                              "(cùng asset HitReactionController raise) → mana/nộ sẽ tăng khi đánh trúng.");
            }
            else
            {
                sb.AppendLine("⚠ onHitConfirmed/onHitBlocked: KHÔNG tìm thấy asset chung → đã TẠO Dbg_OnHitConfirmed/" +
                              "Dbg_OnHitBlocked. NHƯNG HitReactionController trên fighter PHẢI trỏ CÙNG asset này, " +
                              "nếu không mana/nộ sẽ KHÔNG tăng. Kiểm: chọn fighter → HitReactionController → " +
                              "so onHitConfirmed/onHitBlocked với 2 asset Dbg vừa tạo.");
            }
            sb.AppendLine("• Phím khi Play: T = reset Mana+Rage, R = reset %.");
            sb.AppendLine("• Dọn nhanh: menu FA/Debug/Remove Resource Test HUD (current scene).");
            Debug.Log(sb.ToString());
        }
    }
}
