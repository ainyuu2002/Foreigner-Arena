using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// TRẢ TƯỜNG BÁM VỀ CƠ CHẾ P3 (chốt user 2026-07-11): các map dựng bởi tool bản cũ có tường bám
    /// kiểu ClingWall tự chế (khối layer Ground + trigger kẹp vy) — đè lên và vô hiệu cơ chế wall-hang
    /// P3 (map không có collider layer Wall nên WallCheck không bao giờ bật). Tool này quét 3 scene map,
    /// với mỗi ClingWall: xóa ClingZone, chuyển khối sang layer Wall, thêm nắp Ground trên đỉnh
    /// (StageSceneKit.AddWallTopCap) → WallHang/WallJump P3 hoạt động lại. Idempotent. EDITOR-ONLY.
    /// (Ý tưởng "tường băng trượt nhanh hơn": làm sau, chỉnh wallSlideSpeed — chưa nằm ở đây.)
    /// </summary>
    internal static class StageWallHangRestorer
    {
        private static readonly string[] MapScenes =
        {
            StageSceneKit.ScenesFolder + "/Map_Ice.unity",
            StageSceneKit.ScenesFolder + "/Map_Fire.unity",
            StageSceneKit.ScenesFolder + "/Map_Swamp.unity",
        };

        [MenuItem("FA/Stage/Restore wall-hang P3 (3 map)")]
        public static void RestoreAllMaps()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (string path in MapScenes)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int converted = ConvertScene();
                if (converted > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"[WallHangRestorer] {path}: chuyển {converted} tường bám về cơ chế P3.");
            }
        }

        [MenuItem("FA/Stage/Restore wall-hang P3 (scene đang mở)")]
        public static void RestoreCurrentScene()
        {
            int converted = ConvertScene();
            if (converted > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[WallHangRestorer] Scene đang mở: chuyển {converted} tường bám — nhớ Save Scene.");
        }

        private static int ConvertScene()
        {
            var clings = Object.FindObjectsByType<ClingWall>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int converted = 0;
            foreach (var cling in clings)
            {
                // Builder cũ đặt ClingWall trên child "ClingZone" của khối tường.
                var zone = cling.gameObject;
                var wall = zone.transform.parent != null ? zone.transform.parent.gameObject : zone;

                wall.layer = GameLayers.Wall;
                var oldCap = wall.transform.Find("TopCap");
                if (oldCap != null) Object.DestroyImmediate(oldCap.gameObject); // idempotent.
                StageSceneKit.AddWallTopCap(wall);

                if (wall != zone) Object.DestroyImmediate(zone);
                else Object.DestroyImmediate(cling);
                converted++;
            }
            return converted;
        }
    }
}
