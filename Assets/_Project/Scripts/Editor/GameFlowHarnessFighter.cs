using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Combat;
using FA.GameFlow;
using FA.Movement;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN FIGHTER-PREFAB của tool "Build Test_Phase12 (GameFlow harness)" (xem <see cref="GameFlowHarnessSetup"/>).
    /// 1 chức năng: từ prefab fighter có sẵn (Emiya Archer — đã wire Movement/Combat/Resources), tạo 2
    /// PREFAB VARIANT P1/P2 (<c>Harness_FighterP1.prefab</c>, <c>Harness_FighterP2.prefab</c>) trong Prefabs/,
    /// THÊM 4 component P12 per-player (<see cref="LivesSystem"/>/<see cref="RespawnSystem"/>/
    /// <see cref="RespawnInvulnerability"/>/<see cref="ScoreSystem"/>) + wire kênh per-player ĐÚNG slot
    /// + đặt Layer Player1/Player2 + inputScheme Keyboard/Gamepad.
    ///
    /// VÌ SAO variant: per-player channel (Lives/Combo) khác nhau P1 vs P2 → 1 prefab không gói cả 2 (research
    /// §3.7: mỗi nhân vật = prefab variant theo slot). RespawnInvulnerability KHÔNG dùng chung collider với
    /// HurtboxController (soft-flag reviewer) → ta tự tìm collider Layer Hurtbox kéo vào riêng; nếu prefab có
    /// HurtboxController điều khiển CÙNG collider thì ghi rõ NEEDS-MANUAL (xem log).
    ///
    /// EDITOR-ONLY. Dùng PrefabUtility (Unity-native) — KHÔNG sửa YAML tay, KHÔNG sửa code gameplay.
    /// </summary>
    internal static class GameFlowHarnessFighter
    {
        public const string PrefabsFolder = "Assets/_Project/Prefabs";

        /// <summary>Tạo 2 variant, trả qua out. Trả false nếu thiếu prefab gốc.</summary>
        public static bool BuildVariants(GameFlowHarnessAssets.Bundle b,
                                         out GameObject variantP1, out GameObject variantP2,
                                         out string note)
        {
            variantP1 = variantP2 = null;
            note = "";
            if (b.FighterPrefab == null)
            {
                note = "Thiếu fighter prefab gốc (Emiya Archer.prefab).";
                return false;
            }

            variantP1 = BuildOne(b, b.FighterPrefab, PlayerSlot.P1);
            variantP2 = BuildOne(b, b.FighterPrefab, PlayerSlot.P2);
            return variantP1 != null && variantP2 != null;
        }

        private static GameObject BuildOne(GameFlowHarnessAssets.Bundle b, GameObject basePrefab, PlayerSlot slot)
        {
            string name = slot == PlayerSlot.P1 ? "Harness_FighterP1" : "Harness_FighterP2";
            string path = $"{PrefabsFolder}/{name}.prefab";

            // Instance tạm từ prefab gốc → biến thành variant (giữ liên kết override với gốc, Unity-native).
            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            try
            {
                ConfigureSlot(inst, slot);
                AddAndWireGameFlow(inst, b, slot);

                GameObject variant = PrefabUtility.SaveAsPrefabAsset(inst, path);
                return variant;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        // ───────────────────── Layer + inputScheme theo slot ─────────────────────

        private static void ConfigureSlot(GameObject inst, PlayerSlot slot)
        {
            int playerLayer = slot == PlayerSlot.P1 ? GameLayers.Player1 : GameLayers.Player2;
            inst.layer = playerLayer; // CHỈ root đổi sang Player1/2; child Hitbox/Hurtbox giữ layer riêng.

            var motor = inst.GetComponent<CharacterMotor>();
            if (motor != null)
            {
                var so = new SerializedObject(motor);
                var p = so.FindProperty("inputScheme");
                if (p != null)
                    p.enumValueIndex = (int)(slot == PlayerSlot.P1 ? InputScheme.Keyboard : InputScheme.Gamepad);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(motor); // pattern bắt buộc: đánh dấu dirty sau khi set qua SerializedObject.
            }
        }

        // ───────────────────── 4 component P12 + wire ─────────────────────

        private static void AddAndWireGameFlow(GameObject inst, GameFlowHarnessAssets.Bundle b, PlayerSlot slot)
        {
            bool isP1 = slot == PlayerSlot.P1;
            IntEventChannelSO livesCh = isP1 ? b.LivesP1 : b.LivesP2;
            IntEventChannelSO comboCh = isP1 ? b.ComboP1 : b.ComboP2;

            var invuln = AddIfMissing<RespawnInvulnerability>(inst);
            var respawn = AddIfMissing<RespawnSystem>(inst);
            var lives = AddIfMissing<LivesSystem>(inst);
            var score = AddIfMissing<ScoreSystem>(inst);

            // RespawnInvulnerability: kéo collider Layer Hurtbox của prefab vào (tắt khi i-frame).
            Collider2D[] hurtboxes = FindHurtboxColliders(inst);
            var invSo = new SerializedObject(invuln);
            var arr = invSo.FindProperty("hurtboxColliders");
            arr.arraySize = hurtboxes.Length;
            for (int i = 0; i < hurtboxes.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = hurtboxes[i];
            var bodyCol = inst.GetComponent<Collider2D>();
            SetObj(invSo, "bodyCollider", bodyCol);
            invSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(invuln);

            // RespawnSystem: settings + body + invuln (fallbackRespawnPoint để trống — Spawner set runtime).
            var rsSo = new SerializedObject(respawn);
            SetObj(rsSo, "settings", b.MatchSettings);
            SetObj(rsSo, "body", inst.GetComponent<Rigidbody2D>());
            SetObj(rsSo, "invulnerability", invuln);
            rsSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(respawn);

            // LivesSystem: settings + onPlayerDied (chung) + onLivesChanged (per-player) + onOutOfLives (chung)
            //              + respawnSystem + root.
            var lvSo = new SerializedObject(lives);
            SetObj(lvSo, "settings", b.MatchSettings);
            SetObj(lvSo, "onPlayerDied", b.PlayerDied);
            SetObj(lvSo, "onLivesChanged", livesCh);
            SetObj(lvSo, "onOutOfLives", b.OutOfLives);
            SetObj(lvSo, "respawnSystem", respawn);
            SetObj(lvSo, "root", inst.transform);
            lvSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lives);

            // ScoreSystem: onHitConfirmed (chung) + onMyComboChanged (per-player) + onPlayerDied (chung)
            //              + root + livesSystem.
            var scSo = new SerializedObject(score);
            SetObj(scSo, "onHitConfirmed", b.HitConfirmed);
            SetObj(scSo, "onMyComboChanged", comboCh);
            SetObj(scSo, "onPlayerDied", b.PlayerDied);
            SetObj(scSo, "root", inst.transform);
            SetObj(scSo, "livesSystem", lives);
            scSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(score);

            // ComboCounter có sẵn → trỏ onComboChanged về kênh per-player (để ScoreSystem + HUD nghe đúng người).
            var combo = inst.GetComponentInChildren<ComboCounter>();
            if (combo != null)
            {
                var cSo = new SerializedObject(combo);
                SetObj(cSo, "onComboChanged", comboCh); // ghi đè để tách per-player (mục đích harness).
                cSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(combo);
            }
        }

        // ───────────────────── helpers ─────────────────────

        private static Collider2D[] FindHurtboxColliders(GameObject inst)
        {
            var list = new System.Collections.Generic.List<Collider2D>();
            foreach (var col in inst.GetComponentsInChildren<Collider2D>(true))
                if (col.gameObject.layer == GameLayers.Hurtbox)
                    list.Add(col);
            return list.ToArray();
        }

        private static T AddIfMissing<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        private static void SetObj(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = val;
        }
    }
}
