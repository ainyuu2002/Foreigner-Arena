using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Effects;

namespace FA.EditorTools
{
    /// <summary>
    /// SETUP TỰ ĐỘNG bộ màu ALT cho Emiya Archer (feedback test P12 — phân biệt 2P trùng char):
    ///   1. Đọc PNG sprite (Idle/Walk/Hurt — đọc bytes từ đĩa, không cần texture Read/Write) → histogram
    ///      màu lượng-tử-hóa, lọc các màu ĐỎ chủ đạo (áo choàng/shroud của Archer).
    ///   2. Tạo/ghi đè PaletteSwapProfileSO: mỗi màu đỏ → bản đối xứng kênh R↔B (đỏ → xanh dương,
    ///      giữ độ sáng/shade từng mảng — chuẩn palette-swap MUGEN).
    ///   3. Gắn PaletteSwapper vào prefab gốc "Emiya Archer" + wire profile/shader/target.
    /// Chạy từ menu FA/Effects hoặc CLI: -executeMethod FA.EditorTools.PaletteSwapSetupTool.Run
    /// Màu chọn được log ra console — tinh chỉnh tay trong Inspector nếu lệch (CẦN TEST bằng mắt).
    /// </summary>
    internal static class PaletteSwapSetupTool
    {
        private const string SpriteRoot = "Assets/_Project/Art/Characters/Emiya Archer";
        private const string ProfilePath = "Assets/_Project/Data/Effects/PaletteSwap_Archer_Blue.asset";
        private const string ShaderPath = "Assets/_Project/Art/Shaders/FA_PaletteSwapBake.shader";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private static readonly string[] SampleFolders = { "Idle", "Walk", "Hurt", "Attack_L" };

        [MenuItem("FA/Effects/Setup palette swap Archer (đỏ → xanh)")]
        public static void Run()
        {
            List<Color> reds = ExtractDominantReds();
            if (reds.Count == 0)
            {
                Debug.LogError("[PaletteSwapSetup] Không trích được màu đỏ nào từ sprite — kiểm tra SpriteRoot.");
                return;
            }

            var profile = BuildProfile(reds);
            WirePrefab(profile);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PaletteSwapSetup] XONG: {reds.Count} cặp màu → {ProfilePath}; đã gắn PaletteSwapper vào prefab.");
        }

        /// <summary>Histogram màu (lượng tử /16) trên các folder mẫu, lọc họ ĐỎ (r trội hơn g & b).</summary>
        private static List<Color> ExtractDominantReds()
        {
            var counts = new Dictionary<Vector3Int, (long n, Vector3 sum)>();
            foreach (string folder in SampleFolders)
            {
                string dir = Path.Combine(SpriteRoot, folder);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.GetFiles(dir, "*.png").Take(12))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(file))) continue; // đọc từ đĩa — luôn readable.
                    foreach (Color32 p in tex.GetPixels32())
                    {
                        if (p.a < 230) continue; // bỏ nền trong suốt/biên mờ.
                        var key = new Vector3Int(p.r / 16, p.g / 16, p.b / 16);
                        counts.TryGetValue(key, out var v);
                        counts[key] = (v.n + 1, v.sum + new Vector3(p.r, p.g, p.b) / 255f);
                    }
                    Object.DestroyImmediate(tex);
                }
            }

            return counts
                .Select(kv => (color: kv.Value.sum / kv.Value.n, kv.Value.n))
                .Where(e => e.color.x > 0.14f &&                       // đủ sáng để là mảng bộ đồ.
                            e.color.x > e.color.y * 1.35f &&           // ĐỎ trội hơn lục...
                            e.color.x > e.color.z * 1.35f &&           // ...và trội hơn lam.
                            e.color.y - e.color.z <= 0.07f)            // LOẠI MÀU DA: da ấm có lục > lam
                                                                       // rõ (DB8C67...); áo đỏ đô lục ≈ lam.
                .OrderByDescending(e => e.n)
                .Take(PaletteSwapProfileSO.MaxSwaps)
                .Select(e => new Color(e.color.x, e.color.y, e.color.z, 1f))
                .ToList();
        }

        private static PaletteSwapProfileSO BuildProfile(List<Color> reds)
        {
            string dir = Path.GetDirectoryName(ProfilePath)!.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Effects");

            var profile = AssetDatabase.LoadAssetAtPath<PaletteSwapProfileSO>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PaletteSwapProfileSO>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.swaps.Clear();
            foreach (Color c in reds)
            {
                profile.swaps.Add(new PaletteSwapProfileSO.ColorSwap
                {
                    from = c,
                    to = new Color(c.b, c.g, c.r, 1f), // đảo R↔B: đỏ → xanh dương, GIỮ shade từng mảng.
                });
                Debug.Log($"[PaletteSwapSetup] {ColorUtility.ToHtmlStringRGB(c)} → " +
                          $"{ColorUtility.ToHtmlStringRGB(new Color(c.b, c.g, c.r))}");
            }
            profile.tolerance = 0.08f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void WirePrefab(PaletteSwapProfileSO profile)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var swapper = root.GetComponent<PaletteSwapper>();
                if (swapper == null) swapper = root.AddComponent<PaletteSwapper>();

                var so = new SerializedObject(swapper);
                so.FindProperty("altProfile").objectReferenceValue = profile;
                so.FindProperty("bakeShader").objectReferenceValue = shader;
                so.FindProperty("target").objectReferenceValue = root.GetComponentInChildren<SpriteRenderer>();
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
