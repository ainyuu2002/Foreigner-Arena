using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Items;

namespace FA.EditorTools
{
    /// <summary>
    /// (Item Chặng 2) DỰNG DATA (SO) cho hệ item hợp nhất — mọi số balance ở SO (CLAUDE §3.4), CẦN TEST.
    /// Idempotent (LoadOrCreate). Gồm:
    /// 1) Item_MysteryBlock.asset — ItemData type=MysteryBlock, prefabOverride=Mystery_Block_66, outcomeConfig,
    ///    visualScale=0.19 (box native 3.22u @ scale1 → world-height ≈0.61u, ~39% chiều cao nhân vật ~1.55u).
    /// 2) Gán icon TẠM cho 9 bình (3 hình phân biệt 3 LOẠI; tier phân biệt bằng visualScale) + mystery.
    /// 2b) (fa-fix ISSUE 1) Set lại visualScale 9 bình theo tier T1/T2/T3=1.6/1.8/2.0 — world-height ≈0.42–0.60u
    ///    (~27–39% chiều cao nhân vật Emiya Archer ~1.55u = root scale 2.5 × sprite 62px/100PPU). CẦN TEST.
    /// 3) OnMatchStartChannel.asset (VoidEventChannelSO) ở Data/GameFlow.
    /// 4) ItemSpawnConfig_Ice/Fire/Swamp.asset — pool 9 bình + mystery, fly-bounds/safe-rect theo geometry mỗi map.
    /// CLI: -executeMethod FA.EditorTools.ItemDataSetupTool.Run
    /// </summary>
    public static class ItemDataSetupTool
    {
        private const string ItemsDataDir = "Assets/_Project/Data/Items";
        private const string GameFlowDataDir = "Assets/_Project/Data/GameFlow";
        private const string MysteryDataPath = ItemsDataDir + "/Item_MysteryBlock.asset";
        private const string OutcomeConfigPath = ItemsDataDir + "/MysteryBlockOutcomeConfig.asset";
        private const string MysteryPrefabPath = "Assets/_Project/Prefabs/Mystery_Block_66.prefab";
        private const string OnMatchStartPath = GameFlowDataDir + "/OnMatchStartChannel.asset";
        private const string BuffSheet = "Assets/_Project/Art/Items/Buff_Debuff Items.png";
        private const string MysteryBoxSheet = "Assets/_Project/Art/Items/Mystery Box.png";
        private const long MysteryBoxLocalId = -7953269085874672423;

        // (fa-fix ISSUE 1) visualScale theo tier — item cao ~30-40% nhân vật (~1.55u). CẦN TEST.
        private const float ScaleT1 = 1.6f;
        private const float ScaleT2 = 1.8f;
        private const float ScaleT3 = 2.0f;
        private const float MysteryScale = 0.19f;

        // 3 hình TẠM phân biệt 3 LOẠI (dev đổi qua ItemData.icon).
        private const string KnockbackIcon = "Mobile - Idle Skilling - Apothecary - Potions_0";
        private const string ManaIcon = "Mobile - Idle Skilling - Apothecary - Potions_10";
        private const string DamageIcon = "Mobile - Idle Skilling - Apothecary - Potions_20";

        private static readonly string[] PotionAssets =
        {
            "Item_KnockbackPotion_T1", "Item_KnockbackPotion_T2", "Item_KnockbackPotion_T3",
            "Item_ManaPotion_T1", "Item_ManaPotion_T2", "Item_ManaPotion_T3",
            "Item_DamageBuff_T1", "Item_DamageBuff_T2", "Item_DamageBuff_T3",
        };

        [MenuItem("FA/Tools/Items/3. Setup Item Data (mystery + icons + channel + configs)")]
        public static void Run()
        {
            ItemData mystery = CreateMysteryData();
            AssignPotionIcons();
            SetPotionVisualScales();
            CreateOnMatchStartChannel();
            List<ItemData> pool = BuildPool(mystery);
            CreateSpawnConfig("Ice", pool, new Vector2(0f, 4f), new Vector2(14f, 5f), new Vector2(0f, 1f), new Vector2(18f, 10f));
            CreateSpawnConfig("Fire", pool, new Vector2(0f, 3.5f), new Vector2(14f, 5f), new Vector2(0f, 1f), new Vector2(18f, 10f));
            CreateSpawnConfig("Swamp", pool, new Vector2(0f, 4.5f), new Vector2(12f, 3f), new Vector2(0f, 2.5f), new Vector2(14f, 9f));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ItemDataSetup] DONE — mystery data + icons + visualScale tiers + OnMatchStartChannel + 3 spawn configs.");
        }

        private static Sprite LoadSlice(string name) =>
            AssetDatabase.LoadAllAssetsAtPath(BuffSheet).OfType<Sprite>().FirstOrDefault(s => s.name == name);

        private static Sprite LoadMysteryBox()
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(MysteryBoxSheet))
                if (o is Sprite s &&
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out _, out long id) && id == MysteryBoxLocalId)
                    return s;
            return AssetDatabase.LoadAllAssetsAtPath(MysteryBoxSheet).OfType<Sprite>().FirstOrDefault();
        }

        private static ItemData CreateMysteryData()
        {
            var data = AssetDatabase.LoadAssetAtPath<ItemData>(MysteryDataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ItemData>();
                AssetDatabase.CreateAsset(data, MysteryDataPath);
            }
            data.type = ItemType.MysteryBlock;
            data.tintColor = Color.white;                 // white — không tint box.
            data.visualScale = MysteryScale;              // box native 3.22u → world-height ≈0.61u. CẦN TEST.
            data.icon = LoadMysteryBox();
            data.spawnWeight = 2f;                        // CẦN TEST.
            data.mysteryOutcomeConfig = AssetDatabase.LoadAssetAtPath<MysteryBlockOutcomeConfigSO>(OutcomeConfigPath);
            data.prefabOverride = AssetDatabase.LoadAssetAtPath<GameObject>(MysteryPrefabPath);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static void AssignPotionIcons()
        {
            Sprite kb = LoadSlice(KnockbackIcon), mana = LoadSlice(ManaIcon), dmg = LoadSlice(DamageIcon);
            foreach (string name in PotionAssets)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsDataDir}/{name}.asset");
                if (data == null) { Debug.LogError($"[ItemDataSetup] Thiếu {name}.asset"); continue; }
                data.icon = data.type == ItemType.KnockbackPotion ? kb
                          : data.type == ItemType.ManaPotion ? mana : dmg;
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>(fa-fix ISSUE 1) Item quá bé — set visualScale theo tier (suffix _T1/_T2/_T3 tên asset)
        /// để item cao ~30-40% nhân vật, giữ chênh lệch tier (T1&lt;T2&lt;T3). CẦN TEST.</summary>
        private static void SetPotionVisualScales()
        {
            foreach (string name in PotionAssets)
            {
                var data = AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsDataDir}/{name}.asset");
                if (data == null) continue;
                float scale = name.EndsWith("_T1") ? ScaleT1 : name.EndsWith("_T2") ? ScaleT2 : ScaleT3;
                data.visualScale = scale;
                EditorUtility.SetDirty(data);
            }
        }

        private static void CreateOnMatchStartChannel()
        {
            if (AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(OnMatchStartPath) != null) return;
            var ch = ScriptableObject.CreateInstance<VoidEventChannelSO>();
            AssetDatabase.CreateAsset(ch, OnMatchStartPath);
        }

        private static List<ItemData> BuildPool(ItemData mystery)
        {
            var pool = PotionAssets
                .Select(n => AssetDatabase.LoadAssetAtPath<ItemData>($"{ItemsDataDir}/{n}.asset"))
                .Where(d => d != null).ToList();
            if (mystery != null) pool.Add(mystery);
            return pool;
        }

        private static void CreateSpawnConfig(string biome, List<ItemData> pool,
            Vector2 flyCenter, Vector2 flySize, Vector2 safeCenter, Vector2 safeSize)
        {
            string path = $"{ItemsDataDir}/ItemSpawnConfig_{biome}.asset";
            var cfg = AssetDatabase.LoadAssetAtPath<ItemSpawnConfigSO>(path);
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<ItemSpawnConfigSO>();
                AssetDatabase.CreateAsset(cfg, path);
            }
            cfg.spawnInterval = 10f;       // CẦN TEST.
            cfg.maxConcurrent = 3;         // CẦN TEST.
            cfg.itemLifetime = 13f;        // CẦN TEST.
            cfg.keepItemsInRect = true;
            cfg.flyBoundsCenter = flyCenter;
            cfg.flyBoundsSize = flySize;
            cfg.safeRectCenter = safeCenter;
            cfg.safeRectSize = safeSize;
            cfg.flySpeedRange = new Vector2(1.5f, 3f);
            cfg.directionChangeIntervalRange = new Vector2(1.5f, 3.5f);
            cfg.itemPool = new List<ItemData>(pool);
            EditorUtility.SetDirty(cfg);
        }
    }
}
