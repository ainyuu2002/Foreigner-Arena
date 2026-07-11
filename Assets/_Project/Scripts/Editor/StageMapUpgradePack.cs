using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// UPDATE PACK feedback test P11 vòng 2 — áp cho scene ĐÃ DỰNG (không rebuild map, không mất
    /// chỉnh tay):
    ///  • Map_Swamp: nâng nấm nhún lên v2 "mền nhảy" — mũ trigger → collider ĐẶC one-way +
    ///    PhysicsMaterial2D bounciness (MushroomBouncer mới đọc bộ mushroom* trong StageConfigSO).
    ///  • Map_Fire: icon cảnh báo lava ghim theo màn hình (CameraEdgeAnchor) thay vì world-space đáy map.
    ///  • Map_Ice: gai băng → IceSpikeHazard chạm-là-đau (+% 1 phát + hất nhẹ + flash), vùng thu sát gai.
    /// Camera zoom KHÔNG nằm ở đây: đã nới hằng (7,18) trong StageCameraRigBuilder → chạy lại
    /// "FA/Stage/Build match camera rig (current scene)" cho từng map. Idempotent. EDITOR-ONLY.
    /// </summary>
    internal static class StageMapUpgradePack
    {
        [MenuItem("FA/Stage/Update pack v2 — nấm + cảnh báo lava + gai băng (3 map)")]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            UpgradeSwampMushrooms();
            UpgradeFireWarning();
            ConvertIceSpikes();
        }

        /// <summary>
        /// Gai băng v2 (feedback test): DoT LavaFloorZone vô hình → IceSpikeHazard CHẠM-LÀ-ĐAU
        /// (+% một phát + hất nhẹ + flash màu). Đổi component tại chỗ, giữ collider (đã thu 1.7×1.2
        /// — phải giẫm lên gai thật mới dính). Idempotent: chạy lại chỉ đổi những gai còn là LavaFloorZone.
        /// </summary>
        private static void ConvertIceSpikes()
        {
            var scene = EditorSceneManager.OpenScene(
                StageSceneKit.ScenesFolder + "/Map_Ice.unity", OpenSceneMode.Single);
            int n = 0;
            foreach (var zone in Object.FindObjectsByType<LavaFloorZone>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = zone.gameObject;
                var cfg = new SerializedObject(zone).FindProperty("config")
                    .objectReferenceValue as StageConfigSO;
                var col = go.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    col.size = new Vector2(1.7f, 1.2f);
                    col.offset = new Vector2(0f, -0.1f);
                }
                Object.DestroyImmediate(zone);

                var spike = go.AddComponent<IceSpikeHazard>();
                StageSceneKit.SetObj(spike, "config", cfg);
                StageSceneKit.SetObj(spike, "spikeArea", col);
                StageSceneKit.SetMask(spike, "playerLayers",
                    FA.Core.GameLayers.Player1Mask | FA.Core.GameLayers.Player2Mask);
                EditorUtility.SetDirty(go);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UpdatePack] Map_Ice: đổi {n} gai băng sang IceSpikeHazard (chạm-là-đau).");
        }

        private static void UpgradeSwampMushrooms()
        {
            var scene = EditorSceneManager.OpenScene(
                StageSceneKit.ScenesFolder + "/Map_Swamp.unity", OpenSceneMode.Single);
            int n = 0;
            foreach (var b in Object.FindObjectsByType<MushroomBouncer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = b.gameObject;
                var cfg = new SerializedObject(b).FindProperty("config")
                    .objectReferenceValue as StageConfigSO;

                var cap = go.GetComponent<BoxCollider2D>();
                if (cap != null)
                {
                    cap.isTrigger = false;
                    cap.usedByEffector = true;
                    cap.sharedMaterial = StageSceneKit.EnsureMushroomMaterial(cfg);
                }
                if (go.GetComponent<PlatformEffector2D>() == null)
                {
                    var eff = go.AddComponent<PlatformEffector2D>();
                    eff.useOneWay = true;
                    eff.surfaceArc = 180f;
                }
                go.layer = GameLayers.OneWayPlatform;
                EditorUtility.SetDirty(go);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UpdatePack] Map_Swamp: nâng cấp {n} nấm nhún lên v2 (mền nhảy).");
        }

        private static void UpgradeFireWarning()
        {
            var scene = EditorSceneManager.OpenScene(
                StageSceneKit.ScenesFolder + "/Map_Fire.unity", OpenSceneMode.Single);
            int n = 0;
            foreach (var lava in Object.FindObjectsByType<RisingLava>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var iconProp = new SerializedObject(lava).FindProperty("warningIcon");
                var icon = iconProp != null ? iconProp.objectReferenceValue as SpriteRenderer : null;
                if (icon == null) continue;
                if (icon.GetComponent<CameraEdgeAnchor>() == null)
                {
                    icon.gameObject.AddComponent<CameraEdgeAnchor>();
                    EditorUtility.SetDirty(icon.gameObject);
                    n++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UpdatePack] Map_Fire: ghim {n} icon cảnh báo lava theo màn hình.");
        }
    }
}
