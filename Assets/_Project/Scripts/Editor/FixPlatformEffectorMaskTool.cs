using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FA.EditorTools
{
    /// <summary>
    /// FIX (idempotent): thu hẹp PlatformEffector2D.colliderMask trên mọi map để đạn/projectile
    /// + hitbox XUYÊN QUA one-way platform + nấm nhún thay vì bị "chạm" rồi despawn.
    ///
    /// Root cause: mọi PlatformEffector2D đang useColliderMask=1 + colliderMask=Everything
    /// (m_Bits 4294967295) → effector ĐÈ Collision Matrix toàn cục, cho platform va với MỌI layer
    /// (kể cả Hitbox/ProjectileP1/ProjectileP2). Child Hitbox của đạn "chạm" platform → HitboxAuthoring
    /// raise HitData → Projectile.despawnOnHit → đạn biến mất.
    ///
    /// Fix: colliderMask = CHỈ Player1 | Player2 | Item (thứ THỰC SỰ đứng/đáp trên platform).
    /// Giữ useColliderMask=true. Player vẫn one-way (matrix OneWayPlatform↔Player còn ON), nấm vẫn nảy.
    ///
    /// CLI: Unity.exe -batchmode -quit -nographics -projectPath D:/Foreigner-Arena
    ///      -executeMethod FA.EditorTools.FixPlatformEffectorMaskTool.Run -logFile -
    /// </summary>
    public static class FixPlatformEffectorMaskTool
    {
        private const string ScenesFolder = "Assets/_Project/Scenes";

        [MenuItem("FA/Tools/Fix Platform Effector Masks (projectiles pass through)")]
        public static void Run()
        {
            int player1 = LayerMask.NameToLayer("Player1");
            int player2 = LayerMask.NameToLayer("Player2");
            int item = LayerMask.NameToLayer("Item");
            if (player1 < 0 || player2 < 0 || item < 0)
            {
                Debug.LogError($"[FixEffector] Layer chưa tồn tại: Player1={player1} Player2={player2} Item={item}. HỦY.");
                return;
            }

            int mask = (1 << player1) | (1 << player2) | (1 << item);
            Debug.Log($"[FixEffector] Target colliderMask = Player1|Player2|Item = {mask} (0x{mask:X}) " +
                      $"[Player1={player1}, Player2={player2}, Item={item}]");

            List<string> scenePaths = FindMapScenes();
            if (scenePaths.Count == 0)
            {
                Debug.LogWarning($"[FixEffector] Không tìm thấy scene Map_*.unity trong {ScenesFolder}.");
                return;
            }

            int totalFixed = 0;
            foreach (string scenePath in scenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                PlatformEffector2D[] effectors = Object.FindObjectsByType<PlatformEffector2D>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                int fixedCount = 0;
                foreach (PlatformEffector2D effector in effectors)
                {
                    effector.useColliderMask = true;
                    effector.colliderMask = mask;
                    EditorUtility.SetDirty(effector);
                    fixedCount++;
                }

                totalFixed += fixedCount;
                Debug.Log($"[FixEffector] {scene.name}: sửa {fixedCount} PlatformEffector2D " +
                          $"-> colliderMask = {mask} (0x{mask:X}) [Player1|Player2|Item]");

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[FixEffector] DONE. Tổng {totalFixed} effector đã sửa trên {scenePaths.Count} scene.");
        }

        private static List<string> FindMapScenes()
        {
            var result = new List<string>();
            string absFolder = Path.GetFullPath(ScenesFolder);
            if (!Directory.Exists(absFolder)) return result;

            foreach (string full in Directory.GetFiles(absFolder, "Map_*.unity", SearchOption.TopDirectoryOnly))
            {
                // Chuyển về đường dẫn tương đối "Assets/..." cho OpenScene.
                string rel = ScenesFolder + "/" + Path.GetFileName(full);
                result.Add(rel);
            }
            result.Sort();
            return result;
        }
    }
}
