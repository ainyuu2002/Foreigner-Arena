using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FA.EditorTools
{
    /// <summary>
    /// ENTRY-POINT bộ tool dựng UI PRODUCTION (P8 HUD + P12 màn hình) — gom 4 file: <see cref="UIKit"/>
    /// (primitives), <see cref="UIChannels"/> (kênh), <see cref="UIHudBuilder"/> (HUD), <see cref="UIScreensBuilder"/>
    /// (CharSelect/StageSelect/End). Menu chính tạo SCENE RIÊNG <c>UI_Build.unity</c> để dựng/tinh chỉnh UI
    /// tách khỏi scene gameplay (hạn chế conflict) + lưu mỗi Canvas thành PREFAB ở <c>Prefabs/UI/</c> → người
    /// dùng ghép prefab vào main scene sau.
    ///
    /// EDITOR-ONLY (asmdef includePlatforms=Editor). KHÔNG sửa code gameplay; chỉ dựng GameObject + wire qua
    /// SerializedObject + tạo scene/prefab bằng Unity API. Quy trình khớp yêu cầu người dùng:
    ///   1) Chạy menu → dựng UI vào scene riêng + tạo prefab.
    ///   2) Tinh chỉnh hình ảnh trong scene UI_Build.
    ///   3) Apply override về prefab → kéo prefab vào main/match scene.
    /// </summary>
    internal static class UIProductionMenu
    {
        public const string ScenesFolder = "Assets/_Project/Scenes";
        public const string PrefabsFolder = "Assets/_Project/Prefabs/UI";
        public const string BuildSceneName = "UI_Build";
        public const string LobbySceneName = "Lobby";
        public static string BuildScenePath => $"{ScenesFolder}/{BuildSceneName}.unity";
        public static string LobbyScenePath => $"{ScenesFolder}/{LobbySceneName}.unity";
        public static string HudPrefabPath => $"{PrefabsFolder}/{UIHudBuilder.HudName}.prefab";
        public static string MenuPrefabPath => $"{PrefabsFolder}/{UIScreensBuilder.MenuName}.prefab";
        public static string MainMenuPrefabPath => $"{PrefabsFolder}/{UILobbyBuilder.MainMenuName}.prefab";
        public static string PauseMenuPrefabPath => $"{PrefabsFolder}/{UISettingsBuilder.PauseMenuName}.prefab";

        // ───────────────────── Menu chính: scene riêng + prefab ─────────────────────

        [MenuItem("FA/UI/Build Production UI (new scene + prefabs)")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return; // người dùng hủy lưu scene đang mở → dừng.

            // Scene mới (Single) TRƯỚC — rồi mới Resolve (v9.1: NewScene unload asset load trước đó → fake null).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            UIChannels.Bundle ch = UIChannels.Resolve();
            UIKit.EnsureCamera();
            UIKit.EnsureEventSystem();

            GameObject hud = UIHudBuilder.Build(ch);
            GameObject menu = UIScreensBuilder.Build(ch);

            // Tạo prefab bằng SaveAsPrefabAsset (KHÔNG ...AndConnect) — giống GameFlowHarnessFighter (proven):
            // AndConnect làm RỚT giá trị wiring (channel) lúc trích prefab → ra null. SaveAsPrefabAsset serialize
            // đúng state hiện tại của object (đã gán channel qua SerializedObject) → prefab wired đúng.
            EnsurePrefabFolder();
            var created = new List<string>();
            SavePrefab(hud, HudPrefabPath, created);
            SavePrefab(menu, MenuPrefabPath, created);

            // Lưu scene để bạn mở xem/tinh chỉnh (object trong scene là bản wired độc lập, không liên kết prefab).
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, BuildScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            LogSummary(ch, created, true);
            EditorUtility.DisplayDialog("Build Production UI",
                "Đã dựng UI vào scene riêng:\n" + BuildScenePath +
                "\n\nPrefab lưu ở " + PrefabsFolder +
                "/ (HUD_InMatch, Menu_Screens).\n\nMở UI_Build → tinh chỉnh hình ảnh → Apply override về prefab " +
                "→ kéo prefab vào main/match scene.\n\nChi tiết + cảnh báo wire: xem Console.", "OK");
        }

        // ───────────────────── Menu Lobby: Main Menu + FULL FLOW vào trận (tuần 2) ─────────────────────

        [MenuItem("FA/UI/Build Lobby (Main Menu + full flow, new scene)")]
        public static void BuildLobby()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // v9.1: NewScene TRƯỚC — Resolve SAU. NewScene(Single) unload asset đã load trước đó (thành
            // "fake null": blocker pass nhưng ref gán vào scene serialize {fileID: 0}). Sprite không dính
            // bug này vì FindSprite luôn chạy sau NewScene — manh mối phá án.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            UIChannels.Bundle ch = UIChannels.Resolve();

            // CHỐT CHẶN (v9): thiếu kênh/registry sống còn → KHÔNG build. Trước đây tool vẫn build với ref
            // null → scene "chết im lặng" (nút bấm được nhưng flow đứng — vụ "bấm TIẾP TỤC không ăn").
            string blockers = LobbyBlockers(ch);
            if (blockers != null)
            {
                Debug.LogError("[UIProduction] DỪNG build Lobby — " + blockers);
                EditorUtility.DisplayDialog("Build Lobby — THIẾU ASSET", blockers, "OK");
                return;
            }

            UIKit.EnsureCamera();
            UIKit.EnsureEventSystem();

            // Các tầng (sorting): HUD 100 (tắt sẵn) < Menu_Screens 200 < MenuBG 250 < FX world 255-256
            // < MainMenu 300. TẤT CẢ canvas chuyển Screen Space CAMERA — Overlay luôn đè renderer world
            // → sét/tia lửa sẽ vô hình nếu giữ Overlay.
            GameObject hud = UIHudBuilder.Build(ch);
            GameObject menu = UIScreensBuilder.Build(ch);
            GameObject mainMenu = UILobbyBuilder.BuildMainMenu(); // tự tạo MenuBG + MenuFX_* (camera-space).
            GameObject pauseMenu = UISettingsBuilder.BuildPauseMenu(ch, hud); // ESC trong trận (canvas 400).
            UILobbyBuilder.ToCameraSpace(hud);
            UILobbyBuilder.ToCameraSpace(menu);
            UILobbyBuilder.BuildSystems(ch, menu, hud);
            hud.SetActive(false); // MatchManager bật khi vào Match (matchHud).

            EnsurePrefabFolder();
            var created = new List<string>();
            SavePrefab(mainMenu, MainMenuPrefabPath, created); // HUD/Menu prefab đã có từ Build Production UI.
            SavePrefab(pauseMenu, PauseMenuPrefabPath, created);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Cảnh báo scene map thiếu trong Build Settings (LoadSceneAsync sẽ fail).
            var missing = UILobbyBuilder.MissingBuildScenes(ch.StageRegistry);
            var sb = new StringBuilder();
            sb.AppendLine("[UIProduction] HOÀN TẤT dựng LOBBY v9.1 (FIX 2 tầng ref null: scan đường dẫn TUYỆT ĐỐI + Resolve SAU NewScene — NewScene unload asset load trước; SETTINGS + PAUSE MENU + LUẬT TRẬN + CharSelect/StageSelect bố cục mock).");
            sb.AppendLine($"• Scene: {LobbyScenePath} — Play thử: START → chọn 2 nhân vật → chọn map (chỉnh luật) → vào trận → ESC.");
            sb.AppendLine("• Art slot (thả PNG vào Assets/_Project/Art/UI/ rồi rebuild):");
            foreach (var s in UILobbyBuilder.ArtSlotStatus()) sb.AppendLine("    - " + s);
            foreach (var p in created) sb.AppendLine("• Prefab: " + p);
            if (ch.Missing.Count > 0)
            {
                sb.AppendLine("⚠ Kênh thiếu:");
                foreach (var m in ch.Missing) sb.AppendLine("    - " + m);
            }
            if (missing.Count > 0)
            {
                sb.AppendLine("⚠ Scene map CHƯA có trong Build Settings (chọn map sẽ KHÔNG load — nhờ bạn/P11 thêm " +
                              "File → Build Settings → Add Open Scenes):");
                foreach (var m in missing) sb.AppendLine("    - " + m);
            }
            Debug.Log(sb.ToString());

            EditorUtility.DisplayDialog("Build Lobby",
                "Đã dựng Lobby:\n" + LobbyScenePath +
                "\n\nPlay thử: START → chọn nhân vật (P1+P2) → chọn map → vào trận." +
                (missing.Count > 0 ? "\n\n⚠ Có scene map chưa vào Build Settings — xem Console." : ""), "OK");
        }

        // ───────────────────── Menu phụ: dựng vào scene đang mở ─────────────────────

        [MenuItem("FA/UI/Build In-Match HUD (current scene)")]
        public static void BuildHudCurrent()
        {
            UIChannels.Bundle ch = UIChannels.Resolve();
            UIKit.EnsureCamera();
            UIKit.EnsureEventSystem();
            UIHudBuilder.Build(ch);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            LogSummary(ch, new List<string>(), false);
            Debug.Log("[UIProduction] Đã dựng HUD_InMatch vào scene đang mở. Wire kênh per-player: xem Console.");
        }

        [MenuItem("FA/UI/Build Menu Screens (current scene)")]
        public static void BuildMenuCurrent()
        {
            UIChannels.Bundle ch = UIChannels.Resolve();
            UIKit.EnsureCamera();
            UIKit.EnsureEventSystem();
            UIScreensBuilder.Build(ch);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            LogSummary(ch, new List<string>(), false);
            Debug.Log("[UIProduction] Đã dựng Menu_Screens vào scene đang mở (CharSelect hiện, Stage/End ẩn).");
        }

        [MenuItem("FA/UI/Remove Production UI (current scene)")]
        public static void RemoveCurrent()
        {
            bool removed = false;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go == null || go.transform.parent != null) continue;
                if (go.name == UIHudBuilder.HudName || go.name == UIScreensBuilder.MenuName)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed = true;
                }
            }
            if (removed)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[UIProduction] Đã xóa HUD_InMatch / Menu_Screens khỏi scene đang mở (giữ prefab + scene file).");
            }
            else Debug.Log("[UIProduction] Không thấy HUD_InMatch / Menu_Screens trong scene đang mở để xóa.");
        }

        // ───────────────────── helpers ─────────────────────

        /// <summary>
        /// Kênh/asset SỐNG CÒN của flow lobby — thiếu cái nào là flow đứng không lời báo (chọn xong không
        /// chuyển màn, không spawn...). Trả message liệt kê để dừng build; null = đủ.
        /// </summary>
        private static string LobbyBlockers(UIChannels.Bundle ch)
        {
            var miss = new List<string>();
            if (ch.CharSelP1 == null) miss.Add("CharSelectedChannel_P1");
            if (ch.CharSelP2 == null) miss.Add("CharSelectedChannel_P2");
            if (ch.StageSel == null) miss.Add("StageSelectedChannel");
            if (ch.Rematch == null) miss.Add("RematchPressedChannel");
            if (ch.BackToCharSelect == null) miss.Add("BackToCharSelectChannel");
            if (ch.MatchEnded == null) miss.Add("OnMatchEndedChannel");
            if (ch.MatchSettings == null) miss.Add("MatchSettings (MatchSettingsSO)");
            if (ch.CharacterRegistry == null) miss.Add("CharacterRegistry (CharacterRegistrySO)");
            if (ch.StageRegistry == null) miss.Add("StageRegistry (StageRegistrySO)");
            if (miss.Count == 0) return null;

            return $"Không tìm thấy {miss.Count} asset bắt buộc:\n  • " + string.Join("\n  • ", miss) +
                   "\n\nAsset lẽ ra nằm ở Assets/_Project/Data/GameFlow/." +
                   "\nCWD hiện tại của Unity: " + System.IO.Directory.GetCurrentDirectory() +
                   "\n\nXem Console log [UIChannels] để biết kênh nào NULL rồi báo P5.";
        }

        private static void EnsurePrefabFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabsFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
        }

        private static void SavePrefab(GameObject instance, string path, List<string> created)
        {
            // SaveAsPrefabAsset (KHÔNG AndConnect) — pattern proven của GameFlowHarnessFighter: serialize đúng
            // state object (đã wire channel) → prefab giữ đủ reference. AndConnect làm rớt wiring → tránh.
            var saved = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool success);
            if (success && saved != null) created.Add(path);
            else Debug.LogWarning($"[UIProduction] Lưu prefab thất bại: {path}");
        }

        private static void LogSummary(UIChannels.Bundle ch, List<string> createdPrefabs, bool wholeScene)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[UIProduction] HOÀN TẤT dựng UI production v11 (SF6 PARALLELOGRAM ĐỐI XỨNG: P1 nghiêng vào giữa (+), P2 nghiêng ngược (−) — mirror).");
            if (wholeScene) sb.AppendLine($"• Scene: {BuildScenePath}");

            if (createdPrefabs.Count > 0)
            {
                sb.AppendLine($"• Prefab tạo ({createdPrefabs.Count}):");
                foreach (var p in createdPrefabs) sb.AppendLine("    - " + p);
            }
            if (ch.Created.Count > 0)
            {
                sb.AppendLine($"• Asset kênh tạo mới ({ch.Created.Count}):");
                foreach (var c in ch.Created) sb.AppendLine("    - " + c);
            }
            if (ch.Missing.Count > 0)
            {
                sb.AppendLine("⚠ Kênh KHÔNG tìm thấy (widget tương ứng sẽ KHÔNG cập nhật lúc Play — kiểm tên asset):");
                foreach (var m in ch.Missing) sb.AppendLine("    - " + m);
            }

            sb.AppendLine("• HUD_InMatch: P1 (trên-trái) + P2 (trên-phải) — Lives/% / Mana ô / Nộ; Timer (trên-giữa).");
            sb.AppendLine("• Menu_Screens: CharSelect (hiện) + StageSelect + EndScreen (ẩn — bật để tinh chỉnh).");
            sb.AppendLine("⚠ % per-player: tool tạo OnPercentChangedChannel_P1/_P2 (P7 chỉ có 1 kênh % chung). " +
                          "Để thanh % chạy: trỏ KnockbackPercent.onPercentChanged của fighter P1→_P1, P2→_P2 " +
                          "(hoặc trỏ lại ResourceBarView của HUD về kênh % mà fighter đang raise).");
            sb.AppendLine("• Ghép vào trận: kéo prefab HUD_InMatch + Menu_Screens vào main/match scene; MatchManager " +
                          "trỏ charSelectPanel/stageSelectPanel/endScreenPanel tới 3 panel con của Menu_Screens.");
            Debug.Log(sb.ToString());
        }
    }
}
