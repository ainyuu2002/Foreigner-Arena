using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Core;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DEBUG (KHÔNG phải gameplay deliverable) — dựng sẵn HARNESS test trọn luồng GameFlow (P12) chỉ bằng
    /// 1 menu item, để người dùng Play ngay KHÔNG cần animation/combat thật: CharSelect → StageSelect → Match
    /// → (Debug panel giả lập chết/đánh/combo/hết-giờ) → End → Rematch.
    ///
    /// Dựng 2 scene:
    ///   • <c>Test_Phase12_Map.unity</c> (map nhỏ: sàn + 4 DeathZone + StageSpawnPoints) — MatchManager load
    ///     ADDITIVE khi vào Match (đúng thiết kế P12). <see cref="GameFlowHarnessMapScene"/>.
    ///   • <c>Test_Phase12.unity</c> (scene MENU): MatchManager/PlayerSpawner/MatchTimer/WinCondition + Canvas
    ///     (HUD/CharSelect/StageSelect/EndScreen/Debug). Cả 2 thêm vào Build Settings (LoadSceneAsync cần).
    ///
    /// EDITOR-ONLY (asmdef includePlatforms=Editor) — được ref module gameplay vì 1 chiều (§1.3 không vỡ).
    /// KHÔNG sửa code gameplay; chỉ add/wire component + tạo asset/scene/prefab-variant qua Unity API.
    /// Việc chia 4 file con: Assets (asset/registry), MapScene, Fighter (variant prefab), Canvas (UI).
    /// </summary>
    internal static class GameFlowHarnessSetup
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string MenuSceneName = "Test_Phase12";
        public static string MenuScenePath => $"{ScenesFolder}/{MenuSceneName}.unity";

        [MenuItem("FA/Debug/Build Test_Phase12 (GameFlow harness)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return; // người dùng hủy lưu scene đang mở → dừng.

            var created = new List<string>();
            var notes = new List<string>();

            // 1) Asset/kênh/registry (tái dùng asset P12 có sẵn; tạo registry + populate sau).
            GameFlowHarnessAssets.Bundle b = GameFlowHarnessAssets.EnsureAll(GameFlowHarnessMapScene.MapSceneName);
            created.AddRange(b.Created);

            // 2) Variant prefab P1/P2 (thêm 4 component P12 + wire per-player) → populate CharacterRegistry.
            if (GameFlowHarnessFighter.BuildVariants(b, out var vP1, out var vP2, out string fighterNote))
            {
                GameFlowHarnessAssets.PopulateCharacterRegistry(b.CharacterRegistry, vP1, vP2);
                created.Add("Assets/_Project/Prefabs/Harness_FighterP1.prefab");
                created.Add("Assets/_Project/Prefabs/Harness_FighterP2.prefab");
            }
            else notes.Add("Variant fighter KHÔNG dựng được: " + fighterNote);

            // 3) Map scene (NewScene Single → đóng scene hiện tại; đã prompt save ở trên). Lưu + Build Settings.
            string mapPath = GameFlowHarnessMapScene.BuildMapScene(b);
            created.Add(mapPath);

            // 4) Menu scene mới (Single) — dựng systems + Canvas, wire MatchManager.
            Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameFlowHarnessCanvas.Refs ui = GameFlowHarnessCanvas.Build(b);
            GameFlowHarnessSystems.Refs sys = GameFlowHarnessSystems.Build(b, ui);
            WireDebugPanelChannels(ui.DebugPanel, sys, b);

            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu, MenuScenePath);
            created.Add(MenuScenePath);

            // 5) Build Settings: thêm cả 2 scene (menu enabled, map enabled — LoadSceneAsync theo tên cần có).
            AddScenesToBuildSettings(mapPath, MenuScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogSummary(b, created, notes, fighterNote);
            EditorUtility.DisplayDialog("GameFlow Harness",
                "Đã dựng Test_Phase12 (menu) + Test_Phase12_Map.\n\nBấm Play scene Test_Phase12, chọn nhân vật " +
                "(P1+P2) → chọn stage → dùng Debug panel (góc dưới phải) giả lập chết/đánh/combo/hết-giờ.\n\n" +
                "Chi tiết + NEEDS-MANUAL: xem Console.", "OK");
        }

        [MenuItem("FA/Debug/Remove Test_Phase12 harness")]
        public static void Remove()
        {
            // Xóa UI + systems trong scene đang mở (nếu là menu scene). KHÔNG xóa asset/scene file (giữ để chạy lại).
            bool removed = false;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go == null || go.transform.parent != null) continue;
                if (go.name == GameFlowHarnessCanvas.UiName ||
                    go.name == GameFlowHarnessSystems.RootName ||
                    go.name == "EventSystem" ||
                    go.name == "MenuCamera")
                {
                    Undo.DestroyObjectImmediate(go);
                    removed = true;
                }
            }
            if (removed)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[GameFlowHarness] Đã xóa UI + systems harness khỏi scene đang mở. " +
                          "(Scene/asset file vẫn giữ — chạy lại menu Build để dựng lại.)");
            }
            else Debug.Log("[GameFlowHarness] Không thấy object harness trong scene đang mở để xóa.");
        }

        // ───────────────────── wire debug panel ─────────────────────

        private static void WireDebugPanelChannels(GameFlowDebugPanel panel, GameFlowHarnessSystems.Refs sys,
                                                   GameFlowHarnessAssets.Bundle b)
        {
            if (panel == null) return;
            var so = new SerializedObject(panel);
            SetObj(so, "playerSpawner", sys.PlayerSpawner);
            SetObj(so, "onPlayerDied", b.PlayerDied);
            SetObj(so, "onHitConfirmed", b.HitConfirmed);
            SetObj(so, "onComboChangedP1", b.ComboP1);
            SetObj(so, "onComboChangedP2", b.ComboP2);
            SetObj(so, "onTimeUp", b.TimeUp);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(panel); // flush ref kênh/spawner vào scene khi SaveScene (nếu không → Debug panel vô hiệu lúc Play).
        }

        // ───────────────────── build settings ─────────────────────

        private static void AddScenesToBuildSettings(params string[] paths)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string p in paths)
            {
                bool exists = list.Exists(s => s.path == p);
                if (!exists) list.Add(new EditorBuildSettingsScene(p, true));
                else list.ForEach(s => { if (s.path == p) s.enabled = true; });
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ───────────────────── log ─────────────────────

        private static void LogSummary(GameFlowHarnessAssets.Bundle b, List<string> created,
                                       List<string> notes, string fighterNote)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[GameFlowHarness] HOÀN TẤT dựng harness P12.");
            sb.AppendLine($"• Scene menu: {MenuScenePath}");
            sb.AppendLine($"• Scene map (additive): {GameFlowHarnessMapScene.MapScenePath}");

            if (b.Missing.Count > 0)
            {
                sb.AppendLine("⚠ Asset KHÔNG tìm thấy (kiểm tra bước implement P12):");
                foreach (var m in b.Missing) sb.AppendLine("    - " + m);
            }
            if (created.Count > 0)
            {
                sb.AppendLine($"• Tạo mới ({created.Count}):");
                foreach (var c in created) sb.AppendLine("    - " + c);
            }
            foreach (var n in notes) sb.AppendLine("⚠ " + n);

            sb.AppendLine("• Variant fighter P1/P2 = Emiya Archer + 4 component P12 (Lives/Respawn/Invuln/Score), " +
                          "Layer Player1/2, inputScheme Keyboard/Gamepad.");
            sb.AppendLine("⚠ NEEDS-MANUAL (nếu có): RespawnInvulnerability đã kéo collider Layer Hurtbox của " +
                          "prefab vào. Nếu prefab CÓ HurtboxController điều khiển CÙNG collider đó → khi i-frame " +
                          "tranh .enabled. Hiện harness KHÔNG cần né nên chấp nhận; nếu thấy hurtbox không tắt " +
                          "đúng, gỡ HurtboxController khỏi variant hoặc dùng collider hurtbox riêng.");
            sb.AppendLine("⚠ inputScheme P2=Gamepad: cần CẮM gamepad để điều khiển P2 di chuyển. Không có gamepad " +
                          "vẫn test được toàn bộ luồng qua Debug panel (giả lập chết/đánh/combo/hết-giờ).");
            sb.AppendLine("• Phím/nút: chọn nhân vật #0 cho P1 và P2 → chọn stage #0 → Match. Debug panel góc " +
                          "dưới-phải: Kill/Damage/Combo/Force Time-Up.");
            Debug.Log(sb.ToString());
        }

        private static void SetObj(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = val;
        }
    }
}
