using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FA.UI;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỌN (an toàn) — GỠ hàng nút "CỬ CHỈ TAY CẦM" (Row_GamepadGesture) đã thêm ở bản trước, giờ
    /// KHÔNG còn dùng: cử chỉ lắc/thổi tự chạy khi panel Settings mở (<see cref="SettingsPanelView.AnyOpen"/>),
    /// không cần nút bật/tắt. Nếu bạn đã lỡ chạy tool "thêm nút", chạy cái này để xoá nút thừa mà KHÔNG
    /// rebuild UI. Không có nút thừa thì tool báo 0 (vô hại).
    ///
    /// Scene: dùng Undo.DestroyObjectImmediate (Ctrl+Z được) + MarkDirty, KHÔNG tự lưu (bạn tự Ctrl+S).
    /// Prefab MainMenu/PauseMenu: xoá row rồi SaveAsPrefabAsset.
    /// </summary>
    internal static class GamepadGesturePatchTool
    {
        private const string RowName = "Row_GamepadGesture";

        [MenuItem("FA/UI/Cleanup — Gỡ nút Cử Chỉ Tay Cầm (không còn dùng)")]
        public static void Cleanup()
        {
            int sceneRemoved = CleanupActiveScene();
            int prefabRemoved = CleanupPrefabs();

            var sb = new StringBuilder();
            sb.AppendLine("[GamepadGestureCleanup] Gỡ nút CỬ CHỈ TAY CẦM (không rebuild):");
            sb.AppendLine($"• Scene đang mở: gỡ {sceneRemoved} nút.");
            sb.AppendLine($"• Prefab (MainMenu/PauseMenu): gỡ {prefabRemoved} nút.");
            if (sceneRemoved > 0)
                sb.AppendLine("→ Scene đã MarkDirty. Nhấn Ctrl+S để LƯU (hoặc Ctrl+Z để hoàn tác).");
            if (sceneRemoved == 0 && prefabRemoved == 0)
                sb.AppendLine("→ Không thấy nút nào để gỡ (chưa từng thêm, hoặc scene Lobby chưa mở).");
            Debug.Log(sb.ToString());

            EditorUtility.DisplayDialog("Cleanup cử chỉ tay cầm",
                $"Scene: gỡ {sceneRemoved}.\nPrefab: gỡ {prefabRemoved}.\n\n" +
                (sceneRemoved > 0 ? "Nhấn Ctrl+S để lưu scene.\n\n" : "") + "Chi tiết ở Console.", "OK");
        }

        private static int CleanupActiveScene()
        {
            int removed = 0;
            var views = Object.FindObjectsByType<SettingsPanelView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var view in views)
            {
                var row = FindRow(view);
                if (row == null) continue;
                Undo.DestroyObjectImmediate(row.gameObject);
                removed++;
            }
            if (removed > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return removed;
        }

        private static int CleanupPrefabs()
        {
            int removed = 0;
            string[] paths = { UIProductionMenu.MainMenuPrefabPath, UIProductionMenu.PauseMenuPrefabPath };
            foreach (var path in paths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = false;
                    foreach (var view in root.GetComponentsInChildren<SettingsPanelView>(true))
                    {
                        var row = FindRow(view);
                        if (row == null) continue;
                        Object.DestroyImmediate(row.gameObject);
                        changed = true;
                        removed++;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            if (removed > 0) AssetDatabase.SaveAssets();
            return removed;
        }

        /// <summary>Tìm Row_GamepadGesture trong panel của 1 SettingsPanelView (con "Panel" hoặc bất kỳ đâu dưới view).</summary>
        private static Transform FindRow(SettingsPanelView view)
        {
            var panel = view.transform.Find("Panel");
            var row = panel != null ? panel.Find(RowName) : null;
            if (row != null) return row;
            // Fallback: quét sâu (phòng khi cấu trúc khác).
            foreach (var t in view.GetComponentsInChildren<Transform>(true))
                if (t.name == RowName) return t;
            return null;
        }
    }
}
