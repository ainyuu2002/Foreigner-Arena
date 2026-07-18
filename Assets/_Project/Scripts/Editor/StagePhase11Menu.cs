using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FA.Combat;
using FA.Core;
using FA.GameFlow;
using FA.Movement;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Menu tổng Phase 11: sinh art → dựng 3 map → thả fighter test. Chạy theo số thứ tự 1→2→3,
    /// hoặc chỉ cần "Build ALL". Scene lưu tại Assets/_Project/Scenes, tự thêm Build Settings + StageRegistry.
    /// </summary>
    internal static class StagePhase11Menu
    {
        private const string PrefabP1 = "Assets/_Project/Prefabs/Harness_FighterP1.prefab";
        private const string PrefabP2 = "Assets/_Project/Prefabs/Harness_FighterP2.prefab";

        [MenuItem("FA/Stage/1. Generate Art (Ice + Swamp + Fire)")]
        public static void GenerateArt()
        {
            StageArtIce.Generate();
            StageArtSwamp.Generate();
            StageArtFire.Generate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Phase11] Đã sinh xong art 3 biome tại Assets/_Project/Art/Maps/.");
        }

        [MenuItem("FA/Stage/2. Build Map_Ice (Đỉnh Vĩnh Đông)")]
        public static void BuildIce() { if (Ready()) { StageMapBuilderIce.Build(); Done(); } }

        [MenuItem("FA/Stage/3. Build Map_Swamp (Rừng Sương Độc)")]
        public static void BuildSwamp() { if (Ready()) { StageMapBuilderSwamp.Build(); Done(); } }

        [MenuItem("FA/Stage/4. Build Map_Fire (Lò Dung Nham)")]
        public static void BuildFire() { if (Ready()) { StageMapBuilderFire.Build(); Done(); } }

        [MenuItem("FA/Stage/5. Build ALL (art + 3 map)")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GenerateArt();
            StageMapBuilderSwamp.Build();
            StageMapBuilderFire.Build();
            StageMapBuilderIce.Build(); // dựng Ice cuối → mở sẵn Map_Ice để xem ngay.
            Done();
            Debug.Log("[Phase11] HOÀN TẤT: Map_Ice + Map_Swamp + Map_Fire (đã vào Build Settings + StageRegistry).");
        }

        private static bool Ready()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            // Thiếu art thì tự sinh trước.
            if (AssetDatabase.LoadAssetAtPath<Sprite>(StageArtIce.Sky) == null)
                GenerateArt();
            return true;
        }

        private static void Done()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------
        //  TEST: thả 2 fighter harness vào map đang mở (Test_Phase11 tại chỗ)
        // ------------------------------------------------------------------
        [MenuItem("FA/Stage/Test - Thả 2 fighter vào map đang mở")]
        public static void AddTestFighters()
        {
            var spawns = Object.FindFirstObjectByType<StageSpawnPoints>();
            if (spawns == null)
            {
                Debug.LogError("[Phase11] Scene đang mở không có StageSpawnPoints — mở Map_Ice/Swamp/Fire trước.");
                return;
            }

            var prefab1 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabP1);
            var prefab2 = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabP2);
            if (prefab1 == null || prefab2 == null)
            {
                Debug.LogError("[Phase11] Thiếu Harness_FighterP1/P2.prefab (chạy FA/Debug/Build Test_Phase12 harness trước).");
                return;
            }

            var p1 = (GameObject)PrefabUtility.InstantiatePrefab(prefab1);
            var p2 = (GameObject)PrefabUtility.InstantiatePrefab(prefab2);
            p1.name = "Fighter_P1 (Keyboard)";
            p2.name = "Fighter_P2 (Gamepad)";
            if (spawns.SpawnP1 != null) p1.transform.position = spawns.SpawnP1.position;
            if (spawns.SpawnP2 != null) p2.transform.position = spawns.SpawnP2.position;
            // Phóng to fighter khi test map (nhân vật MUGEN gốc nhỏ so với scale map — chỉnh thật thuộc P2).
            p1.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            p2.transform.localScale = new Vector3(1.5f, 1.5f, 1f);

            // Camera bám cả 2 fighter.
            var group = Object.FindFirstObjectByType<CinemachineTargetGroup>();
            if (group != null)
            {
                group.AddMember(p1.transform, 1f, 1.5f);
                group.AddMember(p2.transform, 1f, 1.5f);
                EditorUtility.SetDirty(group);

                // Bảo hiểm: chắc chắn CinemachineCamera đang Follow đúng TargetGroup.
                var cm = Object.FindFirstObjectByType<CinemachineCamera>();
                if (cm != null)
                {
                    cm.Follow = group.transform;
                    EditorUtility.SetDirty(cm);
                }
            }

            // Auto-face chéo (P6).
            var f1 = p1.GetComponentInChildren<FacingController>();
            var f2 = p2.GetComponentInChildren<FacingController>();
            if (f1 != null) StageSceneKit.SetObj(f1, "opponent", p2.transform);
            if (f2 != null) StageSceneKit.SetObj(f2, "opponent", p1.transform);

            // Drop-through one-way platform: CharacterMotor tự gắn PlatformDropThrough (Awake) — KHÔNG
            // cần wire tay ở đây nữa (hệ thống dùng chung, áp dụng mọi nhân vật).

            // Điểm hồi sinh dự phòng cho LivesSystem/RespawnSystem (P12 có sẵn trên fighter):
            // thiếu cái này thì khi test không qua luồng P12, chết là rơi tự do giữa không trung.
            WireRespawnPoint(p1, spawns.RespawnPoint);
            WireRespawnPoint(p2, spawns.RespawnPoint);

            // Bật debug self-respawn cho mọi DeathZone (test không cần P12).
            ToggleDebugRespawn(true, spawns.RespawnPoint);

            EditorSceneManager.MarkSceneDirty(p1.scene);
            Debug.Log("[Phase11] Đã thả 2 fighter (P1 Keyboard / P2 Gamepad) + bật debug self-respawn. Bấm Play để test.");
        }

        private static void WireRespawnPoint(GameObject fighter, Transform respawn)
        {
            if (respawn == null) return;
            var rs = fighter.GetComponentInChildren<RespawnSystem>();
            if (rs != null) StageSceneKit.SetObj(rs, "fallbackRespawnPoint", respawn);
        }

        [MenuItem("FA/Stage/Test - Tắt debug self-respawn (chuẩn bị ghép P12)")]
        public static void DisableDebugRespawn()
        {
            ToggleDebugRespawn(false, null);
            Debug.Log("[Phase11] Đã tắt debug self-respawn trên mọi DeathZone của scene đang mở.");
        }

        private static void ToggleDebugRespawn(bool on, Transform spawn)
        {
            var zones = Object.FindObjectsByType<DeathZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var dz in zones)
            {
                StageSceneKit.SetBool(dz, "debugSelfRespawn", on);
                if (on && spawn != null) StageSceneKit.SetObj(dz, "debugSpawnPoint", spawn);
            }
            if (zones.Length > 0) EditorSceneManager.MarkSceneDirty(zones[0].gameObject.scene);
        }
    }
}
