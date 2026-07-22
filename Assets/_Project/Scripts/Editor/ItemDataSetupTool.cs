using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Items;

namespace FA.EditorTools
{
    /// <summary>Tạo hoặc cập nhật data, icon và spawn config của hệ item.</summary>
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

        // Kích thước hiển thị theo tier.
        private const float ScaleT1 = 1.6f;
        private const float ScaleT2 = 1.8f;
        private const float ScaleT3 = 2.0f;
        private const float MysteryScale = 0.19f;

        // Icon mặc định cho từng loại bình.
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
            data.tintColor = Color.white;
            data.visualScale = MysteryScale;
            data.icon = LoadMysteryBox();
            data.spawnWeight = 2f;
            data.mysteryOutcomeConfig = AssetDatabase.LoadAssetAtPath<MysteryBlockOutcomeConfigSO>(OutcomeConfigPath);
            data.prefabOverride = AssetDatabase.LoadAssetAtPath<GameObject>(MysteryPrefabPath);
            var serialized = new SerializedObject(data);
            serialized.FindProperty("displayName").stringValue = "MYSTERY BLOCK";
            SerializedProperty effect = serialized.FindProperty("effect");
            effect.FindPropertyRelative("type").enumValueIndex = (int)ItemEffectType.ResistanceRecovery;
            effect.FindPropertyRelative("value").floatValue = 0f;
            effect.FindPropertyRelative("duration").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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

        /// <summary>Đặt kích thước item theo hậu tố tier của asset.</summary>
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
            cfg.spawnInterval = 10f;
            cfg.maxConcurrent = 3;
            cfg.itemLifetime = 13f;
            cfg.mysterySpawnIntervalRange = new Vector2(30f, 40f);
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
