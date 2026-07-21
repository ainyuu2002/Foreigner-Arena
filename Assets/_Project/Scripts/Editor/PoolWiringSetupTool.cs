using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// WIRE phần Unity cho hệ object pooling (idempotent):
    /// (1) Tạo/ghi đè <see cref="PoolConfigSO"/> tại Data/GameFlow/PoolConfig.asset — điền entries cho
    ///     các prefab NÓNG (spark hit/block, đạn thường, drill/beam, VFX di chuyển) với prewarm/maxSize
    ///     CẦN TEST. Prefab thiếu → bỏ qua + LogWarning (không tạo null entry).
    /// (2) Thêm 1 GameObject "PoolBootstrap" vào Lobby.unity, gán config + onMatchStart qua
    ///     SerializedObject (field private) — chỉ 1 instance (bootstrap tự DontDestroyOnLoad + singleton).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.PoolWiringSetupTool.Run
    /// </summary>
    public static class PoolWiringSetupTool
    {
        private const string ConfigPath = "Assets/_Project/Data/GameFlow/PoolConfig.asset";
        private const string ChannelPath = "Assets/_Project/Data/GameFlow/OnMatchStartChannel.asset";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";

        // (prewarm, maxSize) CẦN TEST theo research — số nằm trong asset PoolConfig, không hardcode game logic.
        private static readonly (string path, int prewarm, int maxSize)[] Prefabs =
        {
            // A) HIT/BLOCK SPARK — churn cao nhất (prewarm 10, maxSize 24)
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/HitSpark_VFX.prefab", 10, 24),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/HitSpark_VFX_Layer2.prefab", 10, 24),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/GuardSpark_VFX.prefab", 10, 24),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/ChainSpark_VFX.prefab", 10, 24),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/ChainSpark_VFX_Layer2.prefab", 10, 24),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/H_ArrowImpact_VFX.prefab", 10, 24),
            // B) ĐẠN THƯỜNG (prewarm 4, maxSize 12)
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/HArrow.prefab", 4, 12),
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/HArrowCharged.prefab", 4, 12),
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/ThrownSword.prefab", 4, 12),
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/ThrownSword_Ender.prefab", 4, 12),
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/BarrageBlade.prefab", 4, 12),
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/BarrageBlade_Ender.prefab", 4, 12),
            // C) ĐẠN DRILL/BEAM Super2/Ulti (prewarm 1, maxSize 2)
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/Super2Arrow.prefab", 1, 2),
            ("Assets/_Project/Prefabs/Projectiles/Emiya_Archer/ExcaliburBeam.prefab", 1, 2),
            // D) VFX DI CHUYỂN/NỔ hay lặp (prewarm 3, maxSize 8)
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/L_StepDust_VFX.prefab", 3, 8),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/L3_JumpDust_VFX.prefab", 3, 8),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/MoveFX_DashAir.prefab", 3, 8),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/MoveFX_JumpAirRing.prefab", 3, 8),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/Super2Explosion_VFX.prefab", 3, 8),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/Super2Explosion_VFX_Layer2.prefab", 3, 8),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/H_Muzzle_VFX.prefab", 3, 8),
            // E) VFX ÍT INSTANCE vòng đời dài (overlay cut-in, nền UBW, gió Super2) — prewarm 1
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/SuperFlashOverlay.prefab", 1, 2),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/UBW_Background.prefab", 1, 2),
            ("Assets/_Project/Prefabs/VFX/Emiya_Archer/Super2Wind_VFX.prefab", 1, 4),
        };

        [MenuItem("FA/Tools/Setup Pool Wiring (config + Lobby bootstrap)")]
        public static void Run()
        {
            PoolConfigSO config = BuildConfig();
            WireLobbyBootstrap(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PoolWiring] DONE.");
        }

        private static PoolConfigSO BuildConfig()
        {
            PoolConfigSO config = AssetDatabase.LoadAssetAtPath<PoolConfigSO>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<PoolConfigSO>();
                AssetDatabase.CreateAsset(config, ConfigPath);
                Debug.Log($"[PoolWiring] Created new PoolConfig at {ConfigPath}");
            }
            else
            {
                Debug.Log($"[PoolWiring] Overwriting existing PoolConfig at {ConfigPath}");
            }

            var entries = new List<PoolConfigSO.Entry>();
            int missing = 0;
            foreach ((string path, int prewarm, int maxSize) in Prefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[PoolWiring] SKIP missing prefab: {path}");
                    missing++;
                    continue;
                }
                entries.Add(new PoolConfigSO.Entry { prefab = prefab, prewarm = prewarm, maxSize = maxSize });
            }

            config.entries = entries.ToArray();
            EditorUtility.SetDirty(config);
            Debug.Log($"[PoolWiring] PoolConfig entries filled: {entries.Count} (skipped {missing} missing) -> {ConfigPath}");
            return config;
        }

        private static void WireLobbyBootstrap(PoolConfigSO config)
        {
            var channel = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(ChannelPath);
            if (channel == null)
                Debug.LogWarning($"[PoolWiring] onMatchStart channel NOT found at {ChannelPath} (will wire null)");

            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            PoolBootstrap bootstrap = FindBootstrap(scene);
            if (bootstrap != null)
            {
                Debug.Log($"[PoolWiring] Reusing existing PoolBootstrap on GameObject '{bootstrap.gameObject.name}'");
            }
            else
            {
                var go = new GameObject("PoolBootstrap");
                bootstrap = go.AddComponent<PoolBootstrap>();
                SceneManager.MoveGameObjectToScene(go, scene);
                Debug.Log("[PoolWiring] Created new GameObject 'PoolBootstrap' with PoolBootstrap component");
            }

            var so = new SerializedObject(bootstrap);
            SerializedProperty configProp = so.FindProperty("config");
            SerializedProperty channelProp = so.FindProperty("onMatchStart");
            if (configProp == null || channelProp == null)
            {
                Debug.LogError("[PoolWiring] SerializedProperty 'config'/'onMatchStart' not found on PoolBootstrap!");
                return;
            }
            configProp.objectReferenceValue = config;
            channelProp.objectReferenceValue = channel;
            so.ApplyModifiedPropertiesWithoutUndo();

            bool ok = configProp.objectReferenceValue != null && channelProp.objectReferenceValue != null;
            Debug.Log($"[PoolWiring] PoolBootstrap wired in Lobby (config != null: {configProp.objectReferenceValue != null}, " +
                      $"onMatchStart != null: {channelProp.objectReferenceValue != null})");
            if (!ok) Debug.LogWarning("[PoolWiring] One or more references are NULL after wiring — check asset paths.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PoolWiring] Saved scene {LobbyScenePath}");
        }

        private static PoolBootstrap FindBootstrap(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                PoolBootstrap found = root.GetComponentInChildren<PoolBootstrap>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
