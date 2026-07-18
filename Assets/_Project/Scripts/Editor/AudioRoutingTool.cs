using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace FA.EditorTools
{
    /// <summary>
    /// (P12 Audio) ROUTE các AudioSource trong prefab vào group SFX của FA_AudioMixer — để slider SFX
    /// trong Settings điều khiển được tiếng đánh/skill/voice hiện có. CHỈ đổi DATA (outputAudioMixerGroup
    /// qua SerializedObject) — không sửa code gameplay (§1.3). Thay đổi này KHÔNG ảnh hưởng balance nhưng
    /// đụng prefab của phase gameplay → nhắn team một câu khi commit.
    ///
    /// Quy trình: chạy "1. Audit" xem trước sẽ đổi gì → chạy "2. Route" để áp. Nguồn đã route sẵn vào
    /// group KHÁC Master (ai đó cố tình wiring) thì tool BỎ QUA, không giẫm lên.
    /// Cần có sẵn mixer tại <see cref="MixerAssetPath"/> (tạo tay trong Editor — xem hướng dẫn task 3).
    /// </summary>
    public static class AudioRoutingTool
    {
        private const string MixerAssetPath = "Assets/_Project/Audio/Resources/FA_AudioMixer.mixer";
        private const string SfxGroupName = "SFX";
        private const string PrefabRoot = "Assets/_Project/Prefabs";

        // ---- Verify/Fix exposed params dùng API NỘI BỘ editor (UnityEditor.Audio.AudioMixerController) ----
        // Lý do không dùng mixer.SetFloat để verify: SetFloat/GetFloat ở Edit Mode (mixer chưa chạy trong
        // audio engine) có thể trả false dù exposure đúng → báo động giả. Đọc thẳng dữ liệu editor mới chuẩn.

        [MenuItem("FA/Audio/0. Verify FA_AudioMixer (kiểm tra import + exposed params)")]
        public static void VerifyMixer()
        {
            if (!TryGetMixerInternals(out var mixer, out var bgmGroup, out var sfxGroup,
                    out object bgmVolGuid, out object sfxVolGuid, out var exposedProp)) return;

            // exposedParameters: ExposedAudioParameter[] {GUID guid; string name} — so tên + GUID từng cái.
            bool bgmOk = false, sfxOk = false;
            var list = (Array)exposedProp.GetValue(mixer);
            var sb = new StringBuilder();
            foreach (object ep in list)
            {
                var t = ep.GetType();
                string name = (string)t.GetField("name").GetValue(ep);
                object guid = t.GetField("guid").GetValue(ep);
                if (name == GameSettingsParamBgm && guid.Equals(bgmVolGuid)) bgmOk = true;
                if (name == GameSettingsParamSfx && guid.Equals(sfxVolGuid)) sfxOk = true;
                sb.AppendLine($"  exposed: '{name}' → {guid}");
            }

            if (bgmOk && sfxOk)
                Debug.Log($"[FA.Audio] VERIFY OK — group BGM/SFX + exposed {GameSettingsParamBgm}/{GameSettingsParamSfx} trỏ đúng Volume của group.\n{sb}");
            else
                Debug.LogError($"[FA.Audio] VERIFY FAIL — {GameSettingsParamBgm} đúng: {bgmOk}, {GameSettingsParamSfx} đúng: {sfxOk}. Chạy 'FA/Audio/0b. Fix exposed params' rồi verify lại.\n" +
                               $"  Volume GUID chuẩn — BGM: {bgmVolGuid}, SFX: {sfxVolGuid}\n{sb}");
        }

        [MenuItem("FA/Audio/0b. Fix exposed params (BgmVol-SfxVol)")]
        public static void FixExposedParams()
        {
            if (!TryGetMixerInternals(out var mixer, out var bgmGroup, out var sfxGroup,
                    out object bgmVolGuid, out object sfxVolGuid, out var exposedProp)) return;

            // Dựng lại mảng ExposedAudioParameter đúng 2 phần tử, GUID lấy TỪ CHÍNH group đã import →
            // liên kết chắc chắn khớp, Unity tự serialize lại chuẩn khi save.
            var epType = exposedProp.PropertyType.GetElementType();
            var arr = Array.CreateInstance(epType, 2);
            arr.SetValue(MakeExposedParam(epType, bgmVolGuid, GameSettingsParamBgm), 0);
            arr.SetValue(MakeExposedParam(epType, sfxVolGuid, GameSettingsParamSfx), 1);
            exposedProp.SetValue(mixer, arr);

            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            Debug.Log("[FA.Audio] FIX xong — đã expose lại BgmVol/SfxVol từ GUID thật của group. Chạy '0. Verify' để xác nhận.");
        }

        private const string GameSettingsParamBgm = "BgmVol";
        private const string GameSettingsParamSfx = "SfxVol";

        private static object MakeExposedParam(Type epType, object guid, string name)
        {
            object ep = Activator.CreateInstance(epType);
            epType.GetField("guid").SetValue(ep, guid);
            epType.GetField("name").SetValue(ep, name);
            return ep;
        }

        /// <summary>Load mixer + moi các mảnh internal cần cho verify/fix. FALSE = đã log lỗi cụ thể
        /// (kèm danh sách member khi API đổi tên giữa các bản Unity — để biết đường sửa).</summary>
        private static bool TryGetMixerInternals(out AudioMixer mixer, out AudioMixerGroup bgmGroup,
            out AudioMixerGroup sfxGroup, out object bgmVolGuid, out object sfxVolGuid, out PropertyInfo exposedProp)
        {
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            bgmGroup = sfxGroup = null; bgmVolGuid = sfxVolGuid = null; exposedProp = null;
            if (mixer == null)
            {
                Debug.LogError($"[FA.Audio] Không load được mixer tại {MixerAssetPath} (import lỗi?).");
                return false;
            }

            var bgmMatches = mixer.FindMatchingGroups("BGM");
            var sfxMatches = mixer.FindMatchingGroups(SfxGroupName);
            if (bgmMatches.Length == 0 || sfxMatches.Length == 0)
            {
                Debug.LogError($"[FA.Audio] Thiếu group — BGM: {bgmMatches.Length > 0}, SFX: {sfxMatches.Length > 0}.");
                return false;
            }
            bgmGroup = bgmMatches[0];
            sfxGroup = sfxMatches[0];

            var mixerType = mixer.GetType();
            if (mixerType.FullName != "UnityEditor.Audio.AudioMixerController")
            {
                Debug.LogError($"[FA.Audio] Asset load ra type '{mixerType.FullName}' (chờ AudioMixerController) — API editor đã đổi, báo lại để sửa tool.");
                return false;
            }

            var getGuid = bgmGroup.GetType().GetMethod("GetGUIDForVolume",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            exposedProp = mixerType.GetProperty("exposedParameters",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getGuid == null || exposedProp == null)
            {
                var dump = new StringBuilder("[FA.Audio] API nội bộ đổi tên — member hiện có:\n");
                if (getGuid == null)
                    foreach (var m in bgmGroup.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        if (m.Name.Contains("GUID", StringComparison.OrdinalIgnoreCase)) dump.AppendLine($"  method: {m.Name}");
                if (exposedProp == null)
                    foreach (var p in mixerType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        if (p.Name.Contains("xpose", StringComparison.Ordinal)) dump.AppendLine($"  prop: {p.Name}");
                Debug.LogError(dump.ToString());
                return false;
            }

            bgmVolGuid = getGuid.Invoke(bgmGroup, null);
            sfxVolGuid = getGuid.Invoke(sfxGroup, null);
            return true;
        }

        [MenuItem("FA/Audio/1. Audit AudioSource routing (chỉ log, không đổi gì)")]
        public static void Audit() => Run(apply: false);

        [MenuItem("FA/Audio/2. Route AudioSource prefab → group SFX")]
        public static void Route() => Run(apply: true);

        /// <summary>Prefab LỖI SẴN của phase gameplay (parse failure / dangling component) — tool tuyệt đối
        /// không mở để sửa: save lại sẽ ghi đè file và xóa vĩnh viễn phần hỏng → phá dữ liệu của team.
        /// Nhắn phase gameplay tự sửa; sửa xong thì bỏ khỏi danh sách này rồi chạy Route lại.</summary>
        private static readonly string[] BrokenPrefabSkipList =
        {
            "Assets/_Project/Prefabs/Harness_BlackHeartP1.prefab",
        };

        private static void Run(bool apply)
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            if (mixer == null)
            {
                Debug.LogError($"[FA.Audio] Không thấy mixer tại {MixerAssetPath} — tạo Audio Mixer trong Editor trước (xem hướng dẫn task 3).");
                return;
            }

            var groups = mixer.FindMatchingGroups(SfxGroupName);
            if (groups == null || groups.Length == 0)
            {
                Debug.LogError($"[FA.Audio] Mixer chưa có group '{SfxGroupName}' — thêm group con dưới Master rồi chạy lại.");
                return;
            }
            var sfxGroup = groups[0];

            var report = new StringBuilder();
            int changed = 0, skipped = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (Array.IndexOf(BrokenPrefabSkipList, path) >= 0)
                {
                    report.AppendLine($"  BỎ QUA (prefab lỗi sẵn — phase gameplay sửa, tool không đụng): {path}");
                    continue;
                }

                // Lọc TRƯỚC bằng asset đã import (read-only, không parse lại file) — prefab không có
                // AudioSource thì khỏi LoadPrefabContents: nhanh hơn + không khui lỗi của prefab lạ.
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (imported == null || imported.GetComponentsInChildren<AudioSource>(true).Length == 0)
                    continue;

                GameObject root;
                try { root = PrefabUtility.LoadPrefabContents(path); }
                catch (Exception e)
                {
                    report.AppendLine($"  BỎ QUA (không mở được — {e.GetType().Name}): {path}");
                    continue;
                }
                try
                {
                    bool dirty = false;
                    foreach (var src in root.GetComponentsInChildren<AudioSource>(true))
                    {
                        var current = src.outputAudioMixerGroup;
                        // Đã route chủ đích vào group khác (không phải null/Master) → tôn trọng, bỏ qua.
                        if (current != null && current.name != "Master")
                        {
                            skipped++;
                            report.AppendLine($"  BỎ QUA (đã route '{current.name}'): {path} / {src.gameObject.name}");
                            continue;
                        }

                        report.AppendLine($"  {(apply ? "ROUTE" : "SẼ ROUTE")} → SFX: {path} / {src.gameObject.name}");
                        if (apply)
                        {
                            var so = new SerializedObject(src);
                            so.FindProperty("OutputAudioMixerGroup").objectReferenceValue = sfxGroup;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            dirty = true;
                        }
                        changed++;
                    }
                    if (dirty) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (apply) AssetDatabase.SaveAssets();
            Debug.Log($"[FA.Audio] {(apply ? "ROUTE XONG" : "AUDIT")} — {changed} AudioSource {(apply ? "đã" : "sẽ")} vào SFX, {skipped} bỏ qua (đã route chỗ khác).\n{report}");
        }
    }
}
