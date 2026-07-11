using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// FIX WIRING per-player cho 2 prefab Harness_FighterP1/P2 (P12): bên trong mỗi prefab có Emiya Archer
    /// LỒNG vào, mà prefab Emiya wire CỨNG kênh _P1 (ManaBar/RageBar) + kênh % DÙNG CHUNG → cả 2 người chơi
    /// cùng bắn vào HUD của P1, HUD P2 câm (vụ "một bên đầy một bên không", có stack trace Editor.log làm chứng).
    ///
    /// Tool này KHÔNG sửa code hay prefab Emiya gốc — chỉ đặt OVERRIDE trên instance lồng trong prefab
    /// harness (đúng cơ chế nested-prefab): quét mọi MonoBehaviour con, field nào đang trỏ kênh nguồn thì
    /// trỏ lại kênh đúng slot:
    ///   • OnMana/OnManaSegments/OnRage _P1  → _Pn theo bên.
    ///   • OnPercentChangedChannel (chung)   → OnPercentChangedChannel_Pn (HUD nghe per-player).
    /// Đổi cả field subscribe lẫn raise (quét generic) → nội bộ nhân vật vẫn tự nhất quán. Chạy lại lần 2
    /// an toàn (đã đúng đích thì không đổi gì). Combo KHÔNG đụng — ScoreSystem trên root đã raise _Pn sẵn.
    /// </summary>
    internal static class HarnessChannelFixer
    {
        private const string P1Path = "Assets/_Project/Prefabs/Harness_FighterP1.prefab";
        private const string P2Path = "Assets/_Project/Prefabs/Harness_FighterP2.prefab";

        [MenuItem("FA/UI/Fix kênh per-player cho Harness Fighter (Emiya lồng trong)")]
        public static void Fix()
        {
            var ch = UIChannels.Resolve();
            if (ch.ManaP1 == null || ch.ManaP2 == null || ch.ManaSegP1 == null || ch.ManaSegP2 == null ||
                ch.RageP1 == null || ch.RageP2 == null || ch.PercentP1 == null || ch.PercentP2 == null)
            {
                EditorUtility.DisplayDialog("Fix kênh Harness Fighter",
                    "Thiếu kênh per-player (xem log [UIChannels]) — không fix được. Kiểm tra Assets/_Project/Data.",
                    "OK");
                return;
            }
            var sharedPercent = FindShared<FloatEventChannelSO>("OnPercentChangedChannel");

            int n1 = FixOne(P1Path, isP1: true, ch, sharedPercent);
            int n2 = FixOne(P2Path, isP1: false, ch, sharedPercent);
            AssetDatabase.SaveAssets();

            string msg = $"[HarnessChannelFixer] XONG — P1 đổi {n1} ref, P2 đổi {n2} ref. " +
                         "Play lại: mana/nộ/% của mỗi người đổ về đúng HUD bên mình.";
            Debug.Log(msg);
            EditorUtility.DisplayDialog("Fix kênh Harness Fighter", msg, "OK");
        }

        /// <summary>Tìm asset theo TÊN CHÍNH XÁC (kênh chung không có hậu tố _Pn — tránh khớp nhầm _P1/_P2).</summary>
        private static T FindShared<T>(string assetName) where T : Object
        {
            foreach (string file in UIKit.FindProjectFiles(assetName + ".asset"))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(file);
                if (a != null && a.name == assetName) return a;
            }
            return null;
        }

        private static int FixOne(string path, bool isP1, UIChannels.Bundle ch, FloatEventChannelSO sharedPercent)
        {
            // Bảng nguồn → đích cho bên này. Bên P1: _P1 → _P1 (giữ nguyên), chỉ kênh % chung được tách.
            var map = new Dictionary<Object, Object>();
            AddMap(map, ch.ManaP1, ch.Mana(isP1));
            AddMap(map, ch.ManaSegP1, ch.ManaSeg(isP1));
            AddMap(map, ch.RageP1, ch.Rage(isP1));
            AddMap(map, sharedPercent, ch.Percent(isP1));

            var root = PrefabUtility.LoadPrefabContents(path);
            int swapped = 0;
            try
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue; // missing script — bỏ qua.
                    var so = new SerializedObject(mb);
                    var sp = so.GetIterator();
                    bool dirty = false;
                    while (sp.Next(true))
                    {
                        if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object cur = sp.objectReferenceValue;
                        if (cur == null) continue;
                        if (map.TryGetValue(cur, out Object target) && !ReferenceEquals(cur, target))
                        {
                            sp.objectReferenceValue = target;
                            swapped++;
                            dirty = true;
                        }
                    }
                    if (dirty)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(mb);
                    }
                }
                if (swapped > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[HarnessChannelFixer] {path} ({(isP1 ? "P1" : "P2")}): đổi {swapped} ref kênh.");
            return swapped;
        }

        private static void AddMap(Dictionary<Object, Object> map, Object from, Object to)
        {
            if (from != null && to != null && !map.ContainsKey(from)) map[from] = to;
        }
    }
}
