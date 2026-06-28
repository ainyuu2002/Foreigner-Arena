using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Combat;
using FA.Resources;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN WIRING của tool debug "Setup Resource Test HUD" (xem <see cref="ResourceTestHudSetup"/>).
    /// 1 chức năng: tìm 2 fighter trong scene đang mở + AddComponent NẾU THIẾU + gán SerializedProperty
    /// (config, channel per-player, onHitConfirmed/onHitBlocked chung, attackerCombo CHÉO của đối thủ).
    ///
    /// QUY TẮC GHI (theo yêu cầu tool):
    ///   • Field NGUỒN (config, onHitConfirmed/onHitBlocked, attackerCombo): chỉ điền nếu đang TRỐNG —
    ///     KHÔNG đè reference người dùng/prefab đã gắn (prefab Emiya Archer đã trỏ kênh hit chung).
    ///   • Field HIỂN THỊ per-player (onPercentChanged / onManaChanged / onManaSegmentsChanged /
    ///     onRageChanged): LUÔN set về kênh Dbg_*_Px tương ứng — vì cả 2 fighter dùng CHUNG 1 prefab nên
    ///     mặc định trỏ cùng 1 kênh hiển thị → 2 panel sẽ trùng số. Tách per-player là MỤC ĐÍCH của tool,
    ///     nên đây là ghi đè CHỦ Ý (log rõ ở bước Setup).
    /// </summary>
    internal static class ResourceTestHudWiring
    {
        /// <summary>Một bên đấu sĩ đã nhận diện trong scene (P1 hoặc P2) + tham chiếu component để canvas dùng.</summary>
        public sealed class Fighter
        {
            public GameObject Root;
            public bool IsP1;
            public KnockbackPercent Percent;
            public ManaBar Mana;
            public RageBar Rage;
        }

        /// <summary>
        /// Tìm tối đa 2 fighter qua CombatController (cách dự án nhận diện đấu sĩ — mọi đấu sĩ đều có
        /// CombatController). P1/P2 phân biệt theo LAYER (Player1/Player2) nếu đặt đúng; nếu cả 2 cùng layer
        /// hoặc layer không phải Player1/2 thì fallback theo thứ tự X (trái = P1, phải = P2). Trả null nếu &lt;2.
        /// </summary>
        public static List<Fighter> FindFighters(out string note)
        {
            note = "";
            var combats = Object.FindObjectsByType<CombatController>(FindObjectsInactive.Include);
            var roots = new List<GameObject>();
            foreach (var c in combats)
            {
                GameObject r = c.transform.root.gameObject;
                if (!roots.Contains(r)) roots.Add(r);
            }

            if (roots.Count < 2)
            {
                note = $"Chỉ tìm thấy {roots.Count} fighter (cần 2) qua CombatController trong scene đang mở.";
                return null;
            }
            if (roots.Count > 2)
                note = $"Tìm thấy {roots.Count} fighter — lấy 2 cái đầu (theo layer P1/P2, nếu không thì theo X).";

            // Ưu tiên layer Player1/Player2.
            GameObject p1 = roots.Find(g => g.layer == GameLayers.Player1);
            GameObject p2 = roots.Find(g => g.layer == GameLayers.Player2);

            if (p1 == null || p2 == null || p1 == p2)
            {
                // Fallback theo vị trí X (trái = P1).
                roots.Sort((a, bb) => a.transform.position.x.CompareTo(bb.transform.position.x));
                p1 = roots[0];
                p2 = roots[1];
                note += " (Phân P1/P2 theo vị trí X vì layer Player1/Player2 không khớp.)";
            }

            return new List<Fighter>
            {
                new Fighter { Root = p1, IsP1 = true },
                new Fighter { Root = p2, IsP1 = false },
            };
        }

        /// <summary>Add (nếu thiếu) + wire KnockbackPercent/ManaBar/RageBar cho 1 fighter. opponent = bên kia
        /// (để gán attackerCombo chéo). Điền tham chiếu component vào <paramref name="f"/> cho canvas.</summary>
        public static void WireFighter(Fighter f, Fighter opponent, ResourceTestHudAssets.Bundle b)
        {
            ComboCounter oppCombo = opponent.Root.GetComponentInChildren<ComboCounter>();

            f.Percent = AddIfMissing<KnockbackPercent>(f.Root);
            f.Mana = AddIfMissing<ManaBar>(f.Root);
            f.Rage = AddIfMissing<RageBar>(f.Root);

            FloatEventChannelSO percentCh = f.IsP1 ? b.PercentP1 : b.PercentP2;
            FloatEventChannelSO manaCh = f.IsP1 ? b.ManaP1 : b.ManaP2;
            FloatEventChannelSO rageCh = f.IsP1 ? b.RageP1 : b.RageP2;
            IntEventChannelSO segCh = f.IsP1 ? b.ManaSegP1 : b.ManaSegP2;

            WirePercent(f.Percent, b, percentCh, oppCombo);
            WireMana(f.Mana, b, manaCh, segCh);
            WireRage(f.Rage, b, rageCh);
        }

        private static void WirePercent(KnockbackPercent c, ResourceTestHudAssets.Bundle b,
                                        FloatEventChannelSO percentCh, ComboCounter oppCombo)
        {
            var so = new SerializedObject(c);
            FillIfEmpty(so, "onHitConfirmed", b.HitConfirmed);
            FillIfEmpty(so, "config", b.KnockbackConfig);
            FillIfEmpty(so, "attackerCombo", oppCombo);
            ForceSet(so, "onPercentChanged", percentCh); // per-player: ghi đè chủ ý.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireMana(ManaBar c, ResourceTestHudAssets.Bundle b,
                                     FloatEventChannelSO manaCh, IntEventChannelSO segCh)
        {
            var so = new SerializedObject(c);
            FillIfEmpty(so, "onHitConfirmed", b.HitConfirmed);
            FillIfEmpty(so, "onHitBlocked", b.HitBlocked);
            FillIfEmpty(so, "config", b.ResourceConfig);
            ForceSet(so, "onManaChanged", manaCh);            // per-player: ghi đè chủ ý.
            ForceSet(so, "onManaSegmentsChanged", segCh);     // per-player: ghi đè chủ ý.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireRage(RageBar c, ResourceTestHudAssets.Bundle b, FloatEventChannelSO rageCh)
        {
            var so = new SerializedObject(c);
            FillIfEmpty(so, "onHitConfirmed", b.HitConfirmed);
            FillIfEmpty(so, "onHitBlocked", b.HitBlocked);
            FillIfEmpty(so, "config", b.ResourceConfig);
            ForceSet(so, "onRageChanged", rageCh);            // per-player: ghi đè chủ ý.
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T AddIfMissing<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return Undo.AddComponent<T>(go);
        }

        /// <summary>Chỉ gán nếu property đang null (không đè reference đã có).</summary>
        private static void FillIfEmpty(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null || value == null) return;
            if (p.objectReferenceValue == null)
                p.objectReferenceValue = value;
        }

        /// <summary>Gán bất chấp giá trị cũ (cho field hiển thị per-player — mục đích của tool).</summary>
        private static void ForceSet(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null || value == null) return;
            p.objectReferenceValue = value;
        }
    }
}
