using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN ASSET của tool debug "Build Test_Phase12 (GameFlow harness)" (xem <see cref="GameFlowHarnessSetup"/>).
    /// 1 chức năng: TÌM (ưu tiên) hoặc TẠO các asset cần cho harness — 14 kênh P12 + MatchSettings (đã có từ
    /// bước implement P12) + CharacterRegistry/StageRegistry (tool tạo, trỏ prefab fighter + scene map harness).
    ///
    /// QUY TẮC: tái dùng tối đa asset có sẵn (tìm theo TÊN trong project). Chỉ tạo mới khi thật sự thiếu.
    /// EDITOR-ONLY (asmdef includePlatforms=Editor) — được ref nhiều module gameplay vì KHÔNG module nào ref
    /// ngược → không phá §1.3.
    /// </summary>
    internal static class GameFlowHarnessAssets
    {
        public const string GameFlowFolder = "Assets/_Project/Data/GameFlow";
        public const string PrefabsFolder = "Assets/_Project/Prefabs";
        public const string EmiyaPrefabPath = "Assets/_Project/Prefabs/Emiya Archer.prefab";

        /// <summary>Gom mọi asset/kênh để các bước scene + canvas + wiring dùng.</summary>
        public sealed class Bundle
        {
            public MatchSettingsSO MatchSettings;
            public CharacterRegistrySO CharacterRegistry;
            public StageRegistrySO StageRegistry;
            public GameObject FighterPrefab;

            // Kênh per-player (Lives/Combo) + chung.
            public IntEventChannelSO LivesP1, LivesP2;
            public IntEventChannelSO ComboP1, ComboP2;
            public IntEventChannelSO CharSelP1, CharSelP2;
            public IntEventChannelSO StageSel;

            public GameObjectEventChannelSO PlayerDied;     // OnPlayerDiedChannel (DeathZone + LivesSystem + debug)
            public GameObjectEventChannelSO OutOfLives;     // OnPlayerOutOfLivesChannel (LivesSystem → WinCondition)
            public HitEventChannelSO HitConfirmed;          // OnHitConfirmedChannel (ScoreSystem + debug)

            public FloatEventChannelSO TimerChanged;        // OnTimerChangedChannel (MatchTimer → TimerView)
            public VoidEventChannelSO TimeUp;               // OnTimeUpChannel (MatchTimer → WinCondition + debug)
            public VoidEventChannelSO MatchEnded;           // OnMatchEndedChannel (WinCondition → MatchManager)
            public VoidEventChannelSO Rematch;              // RematchPressedChannel (EndScreen → MatchManager)
            public VoidEventChannelSO BackToCharSelect;     // BackToCharSelectChannel
            public MatchResultEventChannelSO MatchResult;   // OnMatchResultChannel (WinCondition → EndScreen)

            public string MapSceneName;                     // scene map harness (cho StageRegistry + Build Settings)

            public readonly List<string> Created = new List<string>();
            public readonly List<string> Missing = new List<string>();
        }

        public static Bundle EnsureAll(string mapSceneName)
        {
            var b = new Bundle { MapSceneName = mapSceneName };

            // 1) Asset đã tạo ở bước implement P12 — TÌM theo tên, KHÔNG tạo lại.
            b.MatchSettings = Find<MatchSettingsSO>("MatchSettings", b);

            b.LivesP1 = Find<IntEventChannelSO>("OnLivesChangedChannel_P1", b);
            b.LivesP2 = Find<IntEventChannelSO>("OnLivesChangedChannel_P2", b);
            b.ComboP1 = Find<IntEventChannelSO>("OnComboChangedChannel_P1", b);
            b.ComboP2 = Find<IntEventChannelSO>("OnComboChangedChannel_P2", b);
            b.CharSelP1 = Find<IntEventChannelSO>("CharSelectedChannel_P1", b);
            b.CharSelP2 = Find<IntEventChannelSO>("CharSelectedChannel_P2", b);
            b.StageSel = Find<IntEventChannelSO>("StageSelectedChannel", b);

            b.PlayerDied = Find<GameObjectEventChannelSO>("OnPlayerDiedChannel", b);
            b.OutOfLives = Find<GameObjectEventChannelSO>("OnPlayerOutOfLivesChannel", b);
            b.HitConfirmed = Find<HitEventChannelSO>("OnHitConfirmedChannel", b);

            b.TimerChanged = Find<FloatEventChannelSO>("OnTimerChangedChannel", b);
            b.TimeUp = Find<VoidEventChannelSO>("OnTimeUpChannel", b);
            b.MatchEnded = Find<VoidEventChannelSO>("OnMatchEndedChannel", b);
            b.Rematch = Find<VoidEventChannelSO>("RematchPressedChannel", b);
            b.BackToCharSelect = Find<VoidEventChannelSO>("BackToCharSelectChannel", b);
            b.MatchResult = Find<MatchResultEventChannelSO>("OnMatchResultChannel", b);

            // 2) Fighter prefab (tái dùng Emiya Archer — đã wire đủ Movement/Combat/Resources).
            b.FighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EmiyaPrefabPath);
            if (b.FighterPrefab == null)
            {
                // fallback: prefab đầu tiên có CharacterMotor-like (chỉ cần là 1 fighter prefab).
                b.FighterPrefab = FindFirstFighterPrefab();
                if (b.FighterPrefab == null) b.Missing.Add("Fighter prefab (Emiya Archer.prefab)");
            }

            // 3) Registry — tool TẠO (chưa có asset implement). Stage trỏ scene map harness ngay; Character
            //    để TRỐNG entries ở đây — populate sau khi variant prefab P1/P2 dựng xong (Setup gọi).
            b.CharacterRegistry = EnsureCharacterRegistry(b);
            b.StageRegistry = EnsureStageRegistry(b);

            return b;
        }

        // ───────────────────── Registry ─────────────────────

        private static CharacterRegistrySO EnsureCharacterRegistry(Bundle b)
        {
            string path = $"{GameFlowFolder}/Harness_CharacterRegistry.asset";
            var reg = AssetDatabase.LoadAssetAtPath<CharacterRegistrySO>(path);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<CharacterRegistrySO>();
                AssetDatabase.CreateAsset(reg, path);
                b.Created.Add(path);
            }
            return reg;
        }

        /// <summary>Điền 1 entry trỏ variant P1/P2 đã dựng (Setup gọi sau GameFlowHarnessFighter.BuildVariants).</summary>
        public static void PopulateCharacterRegistry(CharacterRegistrySO reg, GameObject variantP1, GameObject variantP2)
        {
            if (reg == null) return;
            var so = new SerializedObject(reg);
            var entries = so.FindProperty("entries");
            entries.arraySize = 1;
            var e = entries.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("displayName").stringValue = "Harness Fighter";
            e.FindPropertyRelative("prefabP1").objectReferenceValue = variantP1;
            e.FindPropertyRelative("prefabP2").objectReferenceValue = variantP2;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reg);
        }

        private static StageRegistrySO EnsureStageRegistry(Bundle b)
        {
            string path = $"{GameFlowFolder}/Harness_StageRegistry.asset";
            var reg = AssetDatabase.LoadAssetAtPath<StageRegistrySO>(path);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<StageRegistrySO>();
                AssetDatabase.CreateAsset(reg, path);
                b.Created.Add(path);
            }

            var so = new SerializedObject(reg);
            var entries = so.FindProperty("entries");
            entries.arraySize = 1;
            var e = entries.GetArrayElementAtIndex(0);
            e.FindPropertyRelative("displayName").stringValue = "Harness Stage";
            e.FindPropertyRelative("sceneName").stringValue = b.MapSceneName;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(reg);
            return reg;
        }

        // ───────────────────── helpers ─────────────────────

        private static T Find<T>(string nameContains, Bundle b) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name} {nameContains}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file == nameContains) return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            // nới lỏng: lấy cái đầu chứa tên (đề phòng đặt tên hơi khác).
            foreach (string guid in guids)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            b.Missing.Add($"{typeof(T).Name} '{nameContains}'");
            return null;
        }

        private static GameObject FindFirstFighterPrefab()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go != null) return go;
            }
            return null;
        }
    }
}
