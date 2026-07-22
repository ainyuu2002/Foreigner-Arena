using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Effects;

namespace FA.EditorTools
{
    /// <summary>
    /// (MỤC D — NHÓM 2, tinh chỉnh playtest #2) SFX/VFX cho Intro/Win/Lose/Draw Emiya Archer — ĐÚNG dữ
    /// liệu MUGEN (Archer.air/.cns), KHÔNG bịa thêm:
    ///   • Trích sound (group,sample) từ Archer.snd — COPY logic ArcherMovementSetupTool.ExtractSnd.
    ///   • Import dãy sprite VFX từ dump: 13-23 (Intro aura, 1901) / 786-0..5 (Win aura, 1810) /
    ///     13-25,13-26 + 13-27,13-28 (VFX chuyển trang phục cuối Intro, anim 1910/1911, blend ADDITIVE).
    ///     Lose/Draw KHÔNG VFX.
    ///   • Dựng flipbook prefab (FlipbookOneShot) — mẫu ArcherMovementSetupTool.CreateFlipbookPrefab.
    ///   • Tạo EffectData (introFx/introTransitionFx/winFx/loseFx/drawFx) + wire component
    ///     PoseEffectController (FA.Effects, mirror MovementEffectController) lên prefab Emiya Archer.
    /// Idempotent — chạy lại an toàn (không ghi đè file audio/sprite đã trích/import).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.PoseFxSetupTool.Run
    /// </summary>
    public static class PoseFxSetupTool
    {
        private const string DumpDir = @"D:\Materials\Archer\Sprite";
        private const string SndPath = @"D:\Materials\Archer\Archer.snd";

        private const string CharArt = "Assets/_Project/Art/Characters/Emiya Archer";
        private const string VfxArt = CharArt + "/PoseVFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";

        // (playtest #2, điểm 2) Aura Intro sống hết dwell clip Intro (3 vòng múa, IntroWinLoseSetupTool
        // ~7.3s tổng, ~5.8s tới RecoveryEnd) — CẦN TEST.
        private const float IntroDwellSeconds = 7.3f;

        // Sprite VFX aura ĐÚNG MUGEN (KHÔNG bịa): anim 1901=13-23 (Intro), anim 1810=786-0..5 (Win).
        private static readonly string[] IntroVfxSprites = { "13-23" };
        private static readonly string[] WinVfxSprites = { "786-0", "786-1", "786-2", "786-3", "786-4", "786-5" };
        // (playtest #2, điểm 5) VFX chuyển trang phục cuối Intro (Archer.air: Begin Action 1910/1911).
        private static readonly string[] IntroTransition1910Sprites = { "13-25", "13-26" };
        private static readonly string[] IntroTransition1911Sprites = { "13-27", "13-28" };

        // SFX ĐÚNG PlaySnd MUGEN (KHÔNG bịa): Intro S191,0/1 (random) · Win S181,0+random%4 (S181,0..3) ·
        // Lose S170,0 · Draw S171,0 (playtest #2, điểm 3/4 — S2005,7 SAI cho Win, S190,0 bỏ khỏi Lose).
        private static readonly (uint g, uint s)[] WantSnd =
        {
            (191, 0), (191, 1), (181, 0), (181, 1), (181, 2), (181, 3), (170, 0), (171, 0),
        };

        [MenuItem("FA/Tools/Setup Pose Fx (MỤC D - Intro Win Lose SFX+VFX)")]
        public static void Run()
        {
            EnsureFolders();
            CopySpritesFromDump();
            ExtractSnd();
            AssetDatabase.Refresh();
            ConfigureSpriteImports();

            // (playtest #2, điểm 2) Aura Intro = "mỏm đá" Archer ĐỨNG lên — đặt ở CHÂN (offset y≈0, xem
            // MakeFx bên dưới), lifeSeconds ≈ tổng dwell clip Intro (IntroDwellSeconds) để sống hết lượt
            // diễn thay vì tự hủy tức thì (flipbook 1 frame không loop sẽ Destroy ngay khung đầu).
            GameObject introVfx = CreateFlipbookPrefab("PoseFX_Intro", IntroVfxSprites, fps: 10f, scale: 0.5f,
                                                        loop: true, lifeSeconds: IntroDwellSeconds);

            // (playtest #2, điểm 5) VFX chuyển trang phục (1910+1911, additive) — 1 prefab composite 2
            // layer, fire ở RecoveryEnd trong lúc StateId=Intro (PoseEffectController.OnRecoveryEnd).
            Material additiveMat = CreateAdditiveMaterial();
            GameObject introTransitionVfx = CreateTransitionVfxPrefab(additiveMat);

            // (playtest #2, điểm 3) Aura Win: 6 sprite (786-0..5) — THU NHỎ (0.18, dải 0.15-0.2 yêu cầu),
            // màu TRẮNG (ConfigureFlipbookGameObject luôn set sr.color=white). loop=true + lifeSeconds
            // rộng rãi hơn outroHold mặc định (2.5s, xem MatchManager) để aura không tắt giữa chừng lúc
            // giữ pose. CẦN TEST.
            GameObject winVfx = CreateFlipbookPrefab("PoseFX_Win", WinVfxSprites, fps: 20f, scale: 0.18f,
                                                      loop: true, lifeSeconds: 3f);

            // volume voice ~0.8 CẦN TEST. Offset: Intro=CHÂN (mỏm đá tại mặt đất); Win=MŨI KIẾM đưa lên
            // (cao + hơi ra trước, CẦN TEST vị trí — điểm 3); IntroTransition=THÂN (Vector2.zero).
            EffectData introFx = MakeFx("Pose_IntroFx", introVfx, new Vector2(0f, 0f),
                                        new[] { "S191_0", "S191_1" }, volume: 0.8f, scale: 1f);
            EffectData introTransitionFx = MakeFx("Pose_IntroTransitionFx", introTransitionVfx, Vector2.zero,
                                                  Array.Empty<string>(), volume: 0f, scale: 1f);
            EffectData winFx = MakeFx("Pose_WinFx", winVfx, new Vector2(0.25f, 1.35f),
                                      new[] { "S181_0", "S181_1", "S181_2", "S181_3" }, volume: 0.8f, scale: 1f);
            EffectData loseFx = MakeFx("Pose_LoseFx", null, Vector2.zero,
                                       new[] { "S170_0" }, volume: 0.8f, scale: 1f);
            EffectData drawFx = MakeFx("Pose_DrawFx", null, Vector2.zero,
                                       new[] { "S171_0" }, volume: 0.8f, scale: 1f);

            WirePrefab(introFx, introTransitionFx, winFx, loseFx, drawFx);

            AssetDatabase.SaveAssets();
            Debug.Log("[PoseFx] DONE — 4 flipbook VFX (Intro 13-23 / IntroTransition 1910+1911 / Win " +
                      "786-0..5) + 5 EffectData (introFx/introTransitionFx/winFx/loseFx/drawFx) + " +
                      "PoseEffectController trên prefab Emiya Archer.");
        }

        // ---------------------------------------------------------------- folders
        private static void EnsureFolders()
        {
            foreach (string dir in new[] { VfxArt, VfxPrefabDir, EffectsDataDir, AudioDir })
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                string parent = dir.Substring(0, dir.LastIndexOf('/'));
                if (!AssetDatabase.IsValidFolder(parent)) Directory.CreateDirectory(Path.GetFullPath(parent));
                AssetDatabase.CreateFolder(parent, dir.Substring(dir.LastIndexOf('/') + 1));
            }
        }

        // ---------------------------------------------------------------- sprite VFX
        private static void CopySpritesFromDump()
        {
            string absDir = Path.GetFullPath(VfxArt);
            Directory.CreateDirectory(absDir);
            foreach (string s in IntroVfxSprites.Concat(WinVfxSprites)
                                                 .Concat(IntroTransition1910Sprites)
                                                 .Concat(IntroTransition1911Sprites))
            {
                string dest = Path.Combine(absDir, s + ".png");
                if (File.Exists(dest)) continue;   // idempotent.
                string src = Path.Combine(DumpDir, s + ".png");
                if (!File.Exists(src)) { Debug.LogWarning($"[PoseFx] Thiếu sprite dump: {src}"); continue; }
                File.Copy(src, dest);
            }
        }

        private static void ConfigureSpriteImports()
        {
            if (!AssetDatabase.IsValidFolder(VfxArt)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { VfxArt }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static Sprite LoadSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{VfxArt}/{name}.png");

        // ---------------------------------------------------------------- SFX .snd (COPY logic ArcherMovementSetupTool.ExtractSnd — header ElecbyteSnd, subheader next(4)+len(4)+group(4)+sample(4)+RIFF)
        private static void ExtractSnd()
        {
            if (!File.Exists(SndPath)) { Debug.LogWarning($"[PoseFx] Không thấy {SndPath} — bỏ qua trích SFX."); return; }
            byte[] all = File.ReadAllBytes(SndPath);
            if (all.Length < 0x18) { Debug.LogWarning("[PoseFx] .snd hỏng (quá ngắn)"); return; }

            var want = new HashSet<(uint, uint)>(WantSnd);
            string absAudio = Path.GetFullPath(AudioDir);
            Directory.CreateDirectory(absAudio);

            long off = BitConverter.ToUInt32(all, 0x14);   // subheader đầu tiên.
            int guard = 0;
            var foundSet = new HashSet<(uint, uint)>();
            while (off != 0 && off + 16 <= all.Length && guard++ < 100000)
            {
                uint next = BitConverter.ToUInt32(all, (int)off);
                uint len = BitConverter.ToUInt32(all, (int)off + 4);
                uint group = BitConverter.ToUInt32(all, (int)off + 8);
                uint sample = BitConverter.ToUInt32(all, (int)off + 12);
                long dataStart = off + 16;
                if (want.Contains((group, sample)) && len > 0 && dataStart + len <= all.Length)
                {
                    foundSet.Add((group, sample));
                    string dest = Path.Combine(absAudio, $"S{group}_{sample}.wav");
                    if (!File.Exists(dest))   // idempotent — không đè bản đã trích.
                    {
                        var wav = new byte[len];
                        Array.Copy(all, dataStart, wav, 0, (int)len);
                        File.WriteAllBytes(dest, wav);
                    }
                }
                off = next;
            }
            foreach ((uint g, uint s) in WantSnd)
                if (!foundSet.Contains((g, s)) && !File.Exists(Path.Combine(absAudio, $"S{g}_{s}.wav")))
                    Debug.LogWarning($"[PoseFx] Thiếu sound (group={g}, sample={s}) trong Archer.snd — bỏ qua, không crash.");
        }

        // ---------------------------------------------------------------- flipbook VFX (mẫu ArcherMovementSetupTool.CreateFlipbookPrefab)
        /// <summary>Gắn SpriteRenderer (màu TRẮNG mặc định — điểm 3) + FlipbookOneShot lên 1 GameObject có
        /// sẵn — dùng chung cho prefab đơn (<see cref="CreateFlipbookPrefab"/>) lẫn layer con của prefab
        /// composite (<see cref="CreateTransitionVfxPrefab"/>).</summary>
        private static void ConfigureFlipbookGameObject(GameObject go, string[] spriteNames, float fps, bool loop,
                                                          float lifeSeconds, Material material, string logName)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            Sprite[] sprites = spriteNames.Select(LoadSprite).Where(s => s != null).ToArray();
            if (sprites.Length != spriteNames.Length)
                Debug.LogWarning($"[PoseFx] {logName}: thiếu sprite VFX ({sprites.Length}/{spriteNames.Length})");
            sr.sprite = sprites.Length > 0 ? sprites[0] : null;
            sr.color = Color.white;   // (điểm 3) đảm bảo KHÔNG tint — giữ màu gốc sprite MUGEN.
            sr.sortingOrder = 5;
            if (material != null) sr.material = material;

            var flip = go.AddComponent<FlipbookOneShot>();
            var so = new SerializedObject(flip);
            SerializedProperty pFrames = so.FindProperty("frames");
            pFrames.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
            so.FindProperty("framesPerSecond").floatValue = fps;
            so.FindProperty("loop").boolValue = loop;
            so.FindProperty("lifeSeconds").floatValue = lifeSeconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateFlipbookPrefab(string name, string[] spriteNames, float fps, float scale,
                                                        bool loop, float lifeSeconds, Material material = null)
        {
            string path = $"{VfxPrefabDir}/{name}.prefab";
            var go = new GameObject(name);
            try
            {
                ConfigureFlipbookGameObject(go, spriteNames, fps, loop, lifeSeconds, material, name);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        /// <summary>(playtest #2, điểm 5) VFX chuyển trang phục cuối Intro — 1 prefab composite 2 layer
        /// (Layer1910: 13-25/26, Layer1911: 13-27/28), CÙNG material additive, one-shot (KHÔNG loop).
        /// fps=2/lifeSeconds=1 → 2 sprite/layer đổi chậm ~1s (mờ dần vào/ra qua additive, KHÔNG bịa thêm
        /// hệ fade riêng — CẦN TEST nếu playtest thấy đổi quá đột ngột).</summary>
        private static GameObject CreateTransitionVfxPrefab(Material additiveMat)
        {
            string path = $"{VfxPrefabDir}/PoseFX_IntroTransition.prefab";
            var root = new GameObject("PoseFX_IntroTransition");
            try
            {
                var layer1910 = new GameObject("Layer1910");
                layer1910.transform.SetParent(root.transform, false);
                ConfigureFlipbookGameObject(layer1910, IntroTransition1910Sprites, fps: 2f, loop: false,
                                             lifeSeconds: 1f, material: additiveMat,
                                             logName: "PoseFX_IntroTransition(1910)");

                var layer1911 = new GameObject("Layer1911");
                layer1911.transform.SetParent(root.transform, false);
                ConfigureFlipbookGameObject(layer1911, IntroTransition1911Sprites, fps: 2f, loop: false,
                                             lifeSeconds: 1f, material: additiveMat,
                                             logName: "PoseFX_IntroTransition(1911)");

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// <summary>Material additive URP cho VFX chuyển trang phục — mirror FA.UI.MenuFXAssets.AdditiveMaterial
        /// (internal, khác assembly nên không ref được — Editor tool tự dựng bản riêng, lưu asset persist
        /// để prefab tham chiếu bền giữa các lần chạy CLI).</summary>
        private static Material CreateAdditiveMaterial()
        {
            string path = $"{VfxPrefabDir}/PoseFX_Additive.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                mat = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (mat.HasProperty("_Surface"))   // URP Particles/Unlit — cấu hình Transparent + Blend Additive.
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------- EffectData
        private static EffectData MakeFx(string name, GameObject vfx, Vector2 offset, string[] clipNames,
                                         float volume, float scale)
        {
            string path = $"{EffectsDataDir}/{name}.asset";
            var fx = AssetDatabase.LoadAssetAtPath<EffectData>(path);
            if (fx == null)
            {
                fx = ScriptableObject.CreateInstance<EffectData>();
                AssetDatabase.CreateAsset(fx, path);
            }
            fx.vfxPrefab = vfx;
            fx.offset = offset;
            fx.attachToOwner = false;
            fx.sfxClips = clipNames
                .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav"))
                .Where(c => c != null).ToArray();
            if (clipNames.Length > 0 && fx.sfxClips.Length == 0)
                Debug.LogWarning($"[PoseFx] {name}: yêu cầu SFX nhưng KHÔNG resolve được clip nào (kiểm tra trích .snd).");
            fx.volume = volume;
            fx.scale = scale;
            EditorUtility.SetDirty(fx);
            return fx;
        }

        // ---------------------------------------------------------------- wire prefab
        private static void WirePrefab(EffectData introFx, EffectData introTransitionFx, EffectData winFx,
                                       EffectData loseFx, EffectData drawFx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var audio = root.GetComponent<AudioSource>();
                if (audio == null) audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;

                var pfx = root.GetComponent<PoseEffectController>();
                if (pfx == null) pfx = root.AddComponent<PoseEffectController>();
                var so = new SerializedObject(pfx);
                so.FindProperty("animator").objectReferenceValue = root.GetComponent<Animator>();
                so.FindProperty("audioSource").objectReferenceValue = audio;
                so.FindProperty("relay").objectReferenceValue = root.GetComponent<AnimationEventRelay>();
                so.FindProperty("bodyRenderer").objectReferenceValue = root.GetComponent<SpriteRenderer>();
                so.FindProperty("introFx").objectReferenceValue = introFx;
                so.FindProperty("introTransitionFx").objectReferenceValue = introTransitionFx;
                so.FindProperty("winFx").objectReferenceValue = winFx;
                so.FindProperty("loseFx").objectReferenceValue = loseFx;
                so.FindProperty("drawFx").objectReferenceValue = drawFx;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
