using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Combat;
using FA.Resources;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN ASSET của tool debug "Setup Resource Test HUD" (xem <see cref="ResourceTestHudSetup"/>).
    /// Tách file riêng (1 chức năng = tạo/tìm asset; §1.2 ≤500 dòng). KHÔNG sửa YAML tay — dùng
    /// AssetDatabase + ScriptableObject.CreateInstance (Unity-native, §1.1).
    ///
    /// Tạo (nếu thiếu) trong <c>Assets/_Project/Data/Debug/</c>:
    ///   • KnockbackConfig (KnockbackConfigSO), ResourceConfig (ResourceConfigSO).
    ///   • 6 FloatEventChannelSO: Dbg_OnPercentChanged_P1/_P2, Dbg_OnManaChanged_P1/_P2, Dbg_OnRageChanged_P1/_P2.
    ///   • 2 IntEventChannelSO: Dbg_OnManaSegments_P1/_P2.
    /// onHitConfirmed/onHitBlocked KHÔNG tự đẻ ở đây mặc định — <see cref="FindOrCreateHitChannel"/> ưu tiên
    /// TÌM asset chung đã có (HitReactionController đang raise) để mana/nộ thật sự tăng.
    ///
    /// Đây là EDITOR-ONLY (asmdef includePlatforms=Editor) — được phép ref nhiều module gameplay vì KHÔNG
    /// có module nào ref ngược lại nó → không phá cô lập §1.3.
    /// </summary>
    internal static class ResourceTestHudAssets
    {
        public const string DebugFolder = "Assets/_Project/Data/Debug";

        /// <summary>Gom mọi asset đã tạo/tìm để các bước sau (wiring, canvas) dùng + log tổng kết.</summary>
        public sealed class Bundle
        {
            public KnockbackConfigSO KnockbackConfig;
            public ResourceConfigSO ResourceConfig;

            public FloatEventChannelSO PercentP1, PercentP2;
            public FloatEventChannelSO ManaP1, ManaP2;
            public FloatEventChannelSO RageP1, RageP2;
            public IntEventChannelSO ManaSegP1, ManaSegP2;

            public HitEventChannelSO HitConfirmed, HitBlocked;
            public bool HitChannelsAreShared; // true = tìm thấy asset chung đã có (tốt). false = phải tự tạo (cảnh báo).

            public readonly List<string> Created = new List<string>();
        }

        /// <summary>Tạo/tìm toàn bộ asset, trả Bundle. Gọi AssetDatabase.SaveAssets ở cuối bước Setup.</summary>
        public static Bundle EnsureAll()
        {
            EnsureFolder(DebugFolder);
            var b = new Bundle();

            b.KnockbackConfig = GetOrCreate<KnockbackConfigSO>($"{DebugFolder}/KnockbackConfig.asset", b);
            b.ResourceConfig = GetOrCreate<ResourceConfigSO>($"{DebugFolder}/ResourceConfig.asset", b);

            b.PercentP1 = GetOrCreate<FloatEventChannelSO>($"{DebugFolder}/Dbg_OnPercentChanged_P1.asset", b);
            b.PercentP2 = GetOrCreate<FloatEventChannelSO>($"{DebugFolder}/Dbg_OnPercentChanged_P2.asset", b);
            b.ManaP1 = GetOrCreate<FloatEventChannelSO>($"{DebugFolder}/Dbg_OnManaChanged_P1.asset", b);
            b.ManaP2 = GetOrCreate<FloatEventChannelSO>($"{DebugFolder}/Dbg_OnManaChanged_P2.asset", b);
            b.RageP1 = GetOrCreate<FloatEventChannelSO>($"{DebugFolder}/Dbg_OnRageChanged_P1.asset", b);
            b.RageP2 = GetOrCreate<FloatEventChannelSO>($"{DebugFolder}/Dbg_OnRageChanged_P2.asset", b);
            b.ManaSegP1 = GetOrCreate<IntEventChannelSO>($"{DebugFolder}/Dbg_OnManaSegments_P1.asset", b);
            b.ManaSegP2 = GetOrCreate<IntEventChannelSO>($"{DebugFolder}/Dbg_OnManaSegments_P2.asset", b);

            FindOrCreateHitChannel(b);
            return b;
        }

        /// <summary>
        /// onHitConfirmed/onHitBlocked: ưu tiên TÌM asset chung đã có (project đã có
        /// OnHitConfirmedChannel/OnHitBlockedChannel — fighter P6 raise vào đó). Nếu tìm được → dùng lại để
        /// mana/nộ THẬT tăng. Nếu KHÔNG có → tự tạo Dbg_* và cảnh báo (HitReactionController phải trỏ cùng asset).
        /// </summary>
        private static void FindOrCreateHitChannel(Bundle b)
        {
            b.HitConfirmed = FindHitChannelByName("OnHitConfirmed");
            b.HitBlocked = FindHitChannelByName("OnHitBlocked");
            b.HitChannelsAreShared = b.HitConfirmed != null;

            if (b.HitConfirmed == null)
            {
                b.HitConfirmed = GetOrCreate<HitEventChannelSO>($"{DebugFolder}/Dbg_OnHitConfirmed.asset", b);
            }
            if (b.HitBlocked == null)
            {
                b.HitBlocked = GetOrCreate<HitEventChannelSO>($"{DebugFolder}/Dbg_OnHitBlocked.asset", b);
            }
        }

        /// <summary>Tìm HitEventChannelSO có TÊN file chứa <paramref name="nameContains"/> (vd "OnHitConfirmed").
        /// Bỏ qua asset trong folder Debug để không tự trỏ vào asset mình vừa đẻ.</summary>
        private static HitEventChannelSO FindHitChannelByName(string nameContains)
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(HitEventChannelSO));
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(DebugFolder)) continue; // bỏ qua asset debug tự tạo.
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                if (file.IndexOf(nameContains, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(path);
            }
            return null;
        }

        private static T GetOrCreate<T>(string path, Bundle b) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            b.Created.Add(path);
            return asset;
        }

        /// <summary>Tạo chuỗi folder lồng nhau qua AssetDatabase (không mkdir tay, để Unity đẻ .meta).</summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
