using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.GameFlow;
using FA.Items;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// (Item Chặng 2) WIRE VÒNG ĐỜI TRẬN + đặt ItemSpawner vào 3 map. Idempotent (reuse object theo tên).
    ///
    /// 1) Lobby: set MatchManager.onMatchStart = OnMatchStartChannel (kênh Void chung — MatchManager raise
    ///    cuối LoadMapAndStart; ItemSpawner ở map nghe qua CÙNG asset này, Observer/Event Channel §5).
    /// 2) Mỗi Map_Ice/Fire/Swamp: GameObject "ItemSpawner" + component ItemSpawner (config biome, itemPrefab
    ///    = Item.prefab, autoStart=false, onMatchStart + onMatchEnded). Đặt tại tâm fly-bounds map.
    /// ItemSpawner KHÔNG có visual (vô hình) — dev tự làm hoạt ảnh triệu hồi sau. KHÔNG tạo child portal ở đây.
    /// CLI: -executeMethod FA.EditorTools.ItemSceneSetupTool.Run
    /// </summary>
    public static class ItemSceneSetupTool
    {
        private const string LobbyPath = "Assets/_Project/Scenes/Lobby.unity";
        private const string ItemsDataDir = "Assets/_Project/Data/Items";
        private const string GameFlowDataDir = "Assets/_Project/Data/GameFlow";
        private const string ItemPrefabPath = "Assets/_Project/Prefabs/Items/Item.prefab";
        private const string ItemSpawnerPrefabPath = "Assets/_Project/Prefabs/Items/ItemSpawner.prefab";
        private const string OnMatchStartPath = GameFlowDataDir + "/OnMatchStartChannel.asset";
        private const string OnMatchEndedPath = GameFlowDataDir + "/OnMatchEndedChannel.asset";

        [MenuItem("FA/Tools/Items/4. Setup Item Scenes (wire lobby + spawners)")]
        public static void Run()
        {
            WireLobby();
            PackItemSpawnersIntoPrefab();
            Debug.Log("[ItemSceneSetup] DONE — lobby onMatchStart wired + ItemSpawner added to 3 maps.");
        }

        [MenuItem("FA/Tools/Items/6. Pack Item Spawners Into Prefab %&p")]
        public static void PackItemSpawnersIntoPrefab()
        {
            GameObject spawnerPrefab = CreateItemSpawnerPrefab();
            if (spawnerPrefab == null) return;

            PackMapSpawner("Ice", spawnerPrefab);
            PackMapSpawner("Fire", spawnerPrefab);
            PackMapSpawner("Swamp", spawnerPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("[ItemSceneSetup] ItemSpawner của 3 map đã dùng chung prefab.");
        }

        /// <summary>(fa-fix) Gỡ child "PortalVisual" cũ khỏi ItemSpawner trong CẢ 3 map (idempotent — no-op
        /// nếu đã gỡ). Dev tự làm visual triệu hồi sau; spawner trả về vô hình.</summary>
        [MenuItem("FA/Tools/Items/5. Remove Portal Visual From Scenes")]
        public static void RemovePortalVisual()
        {
            foreach (string biome in new[] { "Ice", "Fire", "Swamp" })
            {
                string scenePath = $"Assets/_Project/Scenes/Map_{biome}.unity";
                Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                GameObject go = GameObject.Find("ItemSpawner");
                if (go == null) { Debug.LogWarning($"[ItemSceneSetup] {biome}: không thấy ItemSpawner."); continue; }

                Transform portal = go.transform.Find("PortalVisual");
                if (portal != null)
                {
                    Object.DestroyImmediate(portal.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[ItemSceneSetup] {biome}: đã gỡ PortalVisual.");
                }
                else
                {
                    Debug.Log($"[ItemSceneSetup] {biome}: không có PortalVisual (đã sạch).");
                }
            }
        }

        private static void WireLobby()
        {
            // MỞ scene TRƯỚC — OpenScene(Single) hủy native của asset nạp trước đó (fake-null). Nạp asset SAU.
            Scene scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
            var channel = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(OnMatchStartPath);
            if (channel == null) { Debug.LogError("[ItemSceneSetup] Thiếu OnMatchStartChannel (chạy tool 3 trước)."); return; }

            var mm = Object.FindAnyObjectByType<MatchManager>(FindObjectsInactive.Include);
            if (mm == null) { Debug.LogError("[ItemSceneSetup] Không thấy MatchManager trong Lobby."); return; }

            var so = new SerializedObject(mm);
            so.FindProperty("onMatchStart").objectReferenceValue = channel;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mm);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Smoke-test batchmode: mở từng map, báo missing-script + kiểm ItemSpawner đã wire đủ ref.</summary>
        [MenuItem("FA/Tools/Items/9. Validate Item Scenes")]
        public static void Validate()
        {
            foreach (string biome in new[] { "Ice", "Fire", "Swamp" })
            {
                Scene scene = EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Map_{biome}.unity", OpenSceneMode.Single);
                int missing = 0;
                foreach (GameObject go in scene.GetRootGameObjects())
                    foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                        missing += CountMissing(t.gameObject);

                var spawner = Object.FindAnyObjectByType<ItemSpawner>(FindObjectsInactive.Include);
                if (spawner == null) { Debug.LogError($"[Validate] {biome}: KHÔNG có ItemSpawner!"); continue; }
                var so = new SerializedObject(spawner);
                bool cfg = so.FindProperty("config").objectReferenceValue != null;
                bool pf = so.FindProperty("itemPrefab").objectReferenceValue != null;
                bool st = so.FindProperty("onMatchStart").objectReferenceValue != null;
                bool en = so.FindProperty("onMatchEnded").objectReferenceValue != null;

                Debug.Log($"[Validate] {biome}: missingScripts={missing} config={cfg} itemPrefab={pf} onMatchStart={st} onMatchEnded={en}");
            }
        }

        private static int CountMissing(GameObject go)
        {
            int n = 0;
            foreach (Component c in go.GetComponents<Component>()) if (c == null) n++;
            if (n > 0) Debug.LogError($"[Validate] MISSING SCRIPT trên '{go.name}' ({n}).");
            return n;
        }

        private static GameObject CreateItemSpawnerPrefab()
        {
            var itemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath);
            var onStart = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(OnMatchStartPath);
            var onEnded = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(OnMatchEndedPath);
            if (itemPrefab == null || onStart == null || onEnded == null)
            {
                Debug.LogError("[ItemSceneSetup] Thiếu Item prefab hoặc event channel.");
                return null;
            }

            var root = new GameObject("ItemSpawner");
            try
            {
                var spawner = root.AddComponent<ItemSpawner>();
                var so = new SerializedObject(spawner);
                so.FindProperty("itemPrefab").objectReferenceValue = itemPrefab.GetComponent<Item>();
                so.FindProperty("autoStart").boolValue = false;
                so.FindProperty("onMatchStart").objectReferenceValue = onStart;
                so.FindProperty("onMatchEnded").objectReferenceValue = onEnded;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, ItemSpawnerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void PackMapSpawner(string biome, GameObject spawnerPrefab)
        {
            string scenePath = $"Assets/_Project/Scenes/Map_{biome}.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            GameObject existing = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "ItemSpawner") continue;
                existing = root;
                break;
            }

            Vector3 position = existing != null ? existing.transform.position : Vector3.zero;
            GameObject source = existing != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(existing)
                : null;
            if (existing == null || source != spawnerPrefab)
            {
                if (existing != null) Object.DestroyImmediate(existing);
                existing = PrefabUtility.InstantiatePrefab(spawnerPrefab, scene) as GameObject;
            }

            var config = AssetDatabase.LoadAssetAtPath<ItemSpawnConfigSO>(
                $"{ItemsDataDir}/ItemSpawnConfig_{biome}.asset");
            if (existing == null || config == null)
            {
                Debug.LogError($"[ItemSceneSetup] {biome}: không thể tạo spawner prefab.");
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            existing.transform.position = position;
            var spawner = existing.GetComponent<ItemSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(existing.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
