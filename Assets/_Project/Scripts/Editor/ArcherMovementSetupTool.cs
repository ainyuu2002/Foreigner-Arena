using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FA.Core;
using FA.Effects;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG LẠI MOVEMENT cho Emiya Archer (idempotent — chạy lại an toàn) từ MUGEN:
    ///   1. Copy sprite thân + VFX cần thiết từ dump D:\Materials\Archer\Sprite (nếu thiếu) + import
    ///      (PPU 100; thân=BottomCenter, VFX=Center).
    ///   2. Trích SFX di chuyển (jump/land/footstep) từ Archer.snd (nếu chưa có) + import.
    ///   3. Ghi đè IN-PLACE 5 clip (Idle/Walk/JumpUp/Fall/Dash) — GIỮ GUID để không đứt motion trong
    ///      SampleFighter.controller. Frame data theo .air (60fps): Idle=act0, Walk=act20, JumpUp=act41,
    ///      Fall=act44, Dash=act105 (dash-nhảy tiến). KHÔNG gắn Animation Event (VFX movement do CODE
    ///      MovementEffectController phân biệt đất/khí — clip Jump/Fall/Dash dùng chung đất+không).
    ///   4. Dựng 4 VFX flipbook (711 bụi-nhảy-đất, 713 cột-khí-nhảy, 745 bụi-đáp/dash-đất, 730 vòng-khí-
    ///      dash-không) — scale NHỎ (user chốt "làm bé"). Blend additive: project chưa có material riêng
    ///      → để mặc định (DEFER, xem CẦN TEST).
    ///   5. Tạo 6 EffectData (jumpGround/jumpAir/land/dashGround/dashAir/walkStep).
    ///   6. Wire prefab: AudioSource + MovementEffectController (gán 6 slot).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.ArcherMovementSetupTool.Run
    /// </summary>
    public static class ArcherMovementSetupTool
    {
        // ---- Nguồn ngoài project (dump MUGEN) ----
        private const string DumpDir = @"D:\Materials\Archer\Sprite";
        private const string SndPath = @"D:\Materials\Archer\Archer.snd";

        // ---- Trong project ----
        private const string CharArt = "Assets/_Project/Art/Characters/Emiya Archer";
        private const string VfxArt = CharArt + "/MoveVFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/Emiya Archer";
        private const string VfxPrefabDir = "Assets/_Project/Prefabs/VFX";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const float Fps = 60f;

        // (folder tương đối trong CharArt, danh sách sprite) — nguồn copy từ dump nếu thiếu.
        private static readonly (string dir, string[] sprites)[] BodySprites =
        {
            ("Idle", new[] { "0-0", "0-1", "0-2", "0-3", "0-4", "0-5", "0-6", "0-11" }),
            ("Walk", new[] { "12-20", "12-21", "12-22", "12-23", "12-24", "12-25", "12-26", "12-27", "12-28", "12-29" }),
            ("JumpUp", new[] { "12-40", "12-41", "12-42", "12-43", "12-44" }),
            ("Fall", new[] { "12-46", "12-47", "12-48" }),
            ("Dash", new[] { "12-42", "12-62", "12-63", "12-64", "12-65" }),
            ("Crouch", new[] { "12-0", "12-1", "12-2", "12-3", "12-4" }),   // A2: ngồi (act10)
        };

        // VFX group,frame → tên file dump "<group>-<frame>". (User 2026-07-15: chỉ CÒN ring-khí 730 —
        // dùng cho jump-air (xoay NGANG) + dash-air (như cũ). Bỏ bụi-nhảy 711, cột-khí 713, bụi-đáp 745.)
        private static readonly string[] VfxDashAir = { "730-0", "730-1", "730-2", "730-3", "730-4", "730-5", "730-7", "730-8", "730-9", "730-10", "730-6" };

        // SFX cần trích từ .snd: (group, sample). Theo PlaySnd .cns: jump S40,0 · land S5,10/11 ·
        // run/dash S100,0/1 · bước chân walk S5,2/5,6 (self-contained: trích cả 2 clip footstep để
        // clean-rebuild với folder audio trống vẫn có tiếng bước chân).
        private static readonly (uint g, uint s)[] WantSnd =
        {
            (40, 0), (5, 10), (5, 11), (100, 0), (100, 1), (5, 2), (5, 6),
        };

        [MenuItem("FA/Tools/Setup Archer Movement (re-port + VFX/SFX)")]
        public static void Run()
        {
            EnsureFolders();
            CopySpritesFromDump();
            ExtractSnd();
            AssetDatabase.Refresh();
            ConfigureSpriteImports();

            BuildClip("Idle", true, new (int, string)[] {
                (0, "0-0"), (5, "0-1"), (10, "0-2"), (15, "0-3"), (20, "0-4"), (25, "0-5"),
                (30, "0-6"), (35, "0-5"), (40, "0-4"), (45, "0-3"), (50, "0-2"), (55, "0-11") }, "Idle", 60);
            BuildClip("Walk", true, new (int, string)[] {
                (0, "12-20"), (6, "12-21"), (12, "12-22"), (18, "12-23"), (24, "12-24"), (30, "12-25"),
                (36, "12-26"), (42, "12-27"), (48, "12-28"), (54, "12-29") }, "Walk", 60);
            BuildClip("JumpUp", false, new (int, string)[] {
                (0, "12-40"), (5, "12-41"), (8, "12-42"), (11, "12-43"), (14, "12-44") }, "JumpUp", 17);
            BuildClip("Fall", true, new (int, string)[] {
                (0, "12-46"), (5, "12-47"), (10, "12-48"), (15, "12-47") }, "Fall", 20);
            BuildClip("Dash", false, new (int, string)[] {
                (0, "12-42"), (4, "12-62"), (9, "12-63"), (14, "12-64"), (15, "12-65") }, "Dash", 16);
            // A2: Ngồi (act10) — chúi xuống 12-0..12-4 rồi GIỮ 12-4 (loop=false). Clip MỚI → cần thêm Animator state.
            AnimationClip crouchClip = BuildClip("Crouch", false, new (int, string)[] {
                (0, "12-0"), (2, "12-1"), (4, "12-2"), (6, "12-3"), (8, "12-4") }, "Crouch", 10);
            AddCrouchAnimatorState(crouchClip);

            // User (2026-07-15): CHỈ còn ring-khí 730 cho jump-air (xoay NGANG 90° Z) + dash-air (như cũ), KHÔNG SFX.
            // Bỏ hết walk/jump-đất/dash-đất/land.
            GameObject dashAirVfx = CreateFlipbookPrefab("MoveFX_DashAir", VfxDashAir, 20f, 0.22f, 0f);
            GameObject jumpAirVfx = CreateFlipbookPrefab("MoveFX_JumpAirRing", VfxDashAir, 20f, 0.22f, 90f);

            // Dọn asset không còn dùng (idempotent).
            foreach (string p in new[] { "MoveFX_JumpGround", "MoveFX_JumpAir", "MoveFX_Land" })
                AssetDatabase.DeleteAsset($"{VfxPrefabDir}/{p}.prefab");
            foreach (string p in new[] { "Move_JumpGround", "Move_DashGround", "Move_WalkStep", "Move_Land" })
                AssetDatabase.DeleteAsset($"{EffectsDataDir}/{p}.asset");

            var fx = new Dictionary<string, EffectData>();
            // Ring khí 730 — KHÔNG SFX. jump-air = bản XOAY NGANG; dash-air = bản gốc.
            fx["jumpAir"] = MakeFx("Move_JumpAir", jumpAirVfx, new Vector2(0f, 0.25f), new string[0], 0f, 1f);
            fx["dashAir"] = MakeFx("Move_DashAir", dashAirVfx, new Vector2(0f, 0.25f), new string[0], 0f, 1f);

            WirePrefab(fx);

            AssetDatabase.SaveAssets();
            Debug.Log("[ArcherMovement] DONE — re-port 5 clip (in-place) + ring-khí jump-air(ngang)/dash-air, KHÔNG SFX/land + MovementEffectController.");
        }

        // ---------------------------------------------------------------- 0. folders
        private static void EnsureFolders()
        {
            foreach (string dir in new[] { VfxArt, VfxPrefabDir, EffectsDataDir })
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                string parent = dir.Substring(0, dir.LastIndexOf('/'));
                if (!AssetDatabase.IsValidFolder(parent)) Directory.CreateDirectory(parent);
                AssetDatabase.CreateFolder(parent, dir.Substring(dir.LastIndexOf('/') + 1));
            }
        }

        // ---------------------------------------------------------------- 1. copy sprite từ dump
        private static void CopySpritesFromDump()
        {
            foreach ((string dir, string[] sprites) in BodySprites)
                CopyGroup($"{CharArt}/{dir}", sprites);

            CopyGroup(VfxArt, new HashSet<string>(VfxDashAir).ToArray());
        }

        private static void CopyGroup(string projDir, string[] sprites)
        {
            string absDir = Path.GetFullPath(projDir);
            Directory.CreateDirectory(absDir);
            foreach (string s in sprites)
            {
                string dest = Path.Combine(absDir, s + ".png");
                if (File.Exists(dest)) continue;                     // idempotent — không đè bản đã import.
                string src = Path.Combine(DumpDir, s + ".png");
                if (!File.Exists(src)) { Debug.LogError($"[ArcherMovement] Thiếu sprite dump: {src}"); continue; }
                File.Copy(src, dest);
            }
        }

        // ---------------------------------------------------------------- 2. trích SFX từ .snd
        private static void ExtractSnd()
        {
            if (!File.Exists(SndPath)) { Debug.LogError($"[ArcherMovement] Không thấy {SndPath}"); return; }
            byte[] all = File.ReadAllBytes(SndPath);
            if (all.Length < 0x18) { Debug.LogError("[ArcherMovement] .snd hỏng (quá ngắn)"); return; }

            var want = new HashSet<(uint, uint)>(WantSnd);
            string absAudio = Path.GetFullPath(AudioDir);
            Directory.CreateDirectory(absAudio);

            long off = BitConverter.ToUInt32(all, 0x14);             // subheader đầu tiên.
            int guard = 0;
            while (off != 0 && off + 16 <= all.Length && guard++ < 100000)
            {
                uint next = BitConverter.ToUInt32(all, (int)off);
                uint len = BitConverter.ToUInt32(all, (int)off + 4);
                uint group = BitConverter.ToUInt32(all, (int)off + 8);
                uint sample = BitConverter.ToUInt32(all, (int)off + 12);
                long dataStart = off + 16;
                if (want.Contains((group, sample)) && len > 0 && dataStart + len <= all.Length)
                {
                    string dest = Path.Combine(absAudio, $"S{group}_{sample}.wav");
                    if (!File.Exists(dest))
                    {
                        var wav = new byte[len];
                        Array.Copy(all, dataStart, wav, 0, (int)len);
                        File.WriteAllBytes(dest, wav);
                    }
                }
                off = next;
            }
        }

        // ---------------------------------------------------------------- 3b. import settings
        private static void ConfigureSpriteImports()
        {
            var dirs = BodySprites.Select(b => $"{CharArt}/{b.dir}").Concat(new[] { VfxArt })
                                  .Where(AssetDatabase.IsValidFolder).ToArray();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", dirs))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                bool isVfx = path.StartsWith(VfxArt);
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)(isVfx ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static Sprite LoadSprite(string dir, string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{dir}/{name}.png");

        // ---------------------------------------------------------------- 3. clip IN-PLACE (giữ GUID)
        private static AnimationClip BuildClip(string action, bool loop, (int tick, string sprite)[] frames,
                                               string spriteFolder, int endTick)
        {
            string path = $"{AnimDir}/{action}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            clip.frameRate = Fps;

            string dir = $"{CharArt}/{spriteFolder}";
            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, string spriteName) in frames)
            {
                Sprite s = LoadSprite(dir, spriteName);
                if (s == null) { Debug.LogError($"[ArcherMovement] {action}: thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

            AnimationClipSettings cs = AnimationUtility.GetAnimationClipSettings(clip);
            cs.loopTime = loop;
            cs.stopTime = endTick / Fps;                              // độ dài clip khớp .air (giữ frame cuối).
            AnimationUtility.SetAnimationClipSettings(clip, cs);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ---- Animator state Crouch (A2) — clip MỚI nên phải thêm state + transition Any State → Crouch (StateId 41).
        private static void AddCrouchAnimatorState(AnimationClip clip)
        {
            string controllerPath = $"{AnimDir}/SampleFighter.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(controllerPath);
            if (controller == null) { Debug.LogError($"[ArcherMovement] Không thấy controller {controllerPath}"); return; }

            const int crouchStateId = 41;   // = CharacterStateId.Crouch (literal — Editor asmdef không ref FA.Movement).
            var sm = controller.layers[0].stateMachine;
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Crouch");
            if (state == null) state = sm.AddState("Crouch");
            state.motion = clip;
            state.writeDefaultValues = true;

            // Transition Any State → Crouch khi StateId==41. BỎ Can-Transition-To-Self (gotcha: bật thì
            // mỗi frame StateId==41 re-enter → kẹt frame 0). Duration 0, không exit-time.
            var tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
            if (tr == null) tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;
            tr.conditions = new[]
            {
                new UnityEditor.Animations.AnimatorCondition
                {
                    mode = UnityEditor.Animations.AnimatorConditionMode.Equals,
                    parameter = "StateId",
                    threshold = crouchStateId
                }
            };
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- 4. VFX flipbook
        private static GameObject CreateFlipbookPrefab(string name, string[] spriteNames, float fps, float scale,
                                                       float zRot = 0f)
        {
            string path = $"{VfxPrefabDir}/{name}.prefab";
            var go = new GameObject(name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite[] sprites = spriteNames.Select(n => LoadSprite(VfxArt, n)).Where(s => s != null).ToArray();
                if (sprites.Length != spriteNames.Length)
                    Debug.LogError($"[ArcherMovement] {name}: thiếu sprite VFX ({sprites.Length}/{spriteNames.Length})");
                sr.sprite = sprites.Length > 0 ? sprites[0] : null;
                sr.sortingOrder = 5;

                var flip = go.AddComponent<FlipbookOneShot>();
                var so = new SerializedObject(flip);
                SerializedProperty pFrames = so.FindProperty("frames");
                pFrames.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                so.FindProperty("framesPerSecond").floatValue = fps;
                so.ApplyModifiedPropertiesWithoutUndo();

                go.transform.localScale = new Vector3(scale, scale, 1f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, zRot);   // zRot=90 → ring nằm NGANG (MovementEffectController giữ rotation prefab).
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ---------------------------------------------------------------- 5. EffectData
        private static EffectData MakeFx(string name, GameObject vfx, Vector2 offset, string[] clipNames,
                                         float vol, float scale)
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
            fx.attachToOwner = false;                                // bụi world-space: ở lại chỗ khi nhân vật đi tiếp.
            fx.sfxClips = clipNames
                .Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav"))
                .Where(c => c != null).ToArray();
            if (clipNames.Length > 0 && fx.sfxClips.Length == 0)
                Debug.LogWarning($"[ArcherMovement] {name}: yêu cầu SFX nhưng KHÔNG resolve được clip nào (kiểm tra trích .snd).");
            fx.volume = vol;
            fx.scale = scale;
            EditorUtility.SetDirty(fx);
            return fx;
        }

        // ---------------------------------------------------------------- 6. wire prefab
        private static void WirePrefab(Dictionary<string, EffectData> fx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var audio = root.GetComponent<AudioSource>();
                if (audio == null) audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;

                var mec = root.GetComponent<MovementEffectController>();
                if (mec == null) mec = root.AddComponent<MovementEffectController>();
                var so = new SerializedObject(mec);
                so.FindProperty("animator").objectReferenceValue = root.GetComponent<Animator>();
                so.FindProperty("audioSource").objectReferenceValue = audio;
                // User 2026-07-15: chỉ jump-air + dash-air (ring 730) và land có hiệu ứng; còn lại null (im+vô hình).
                so.FindProperty("jumpGround").objectReferenceValue = null;
                so.FindProperty("jumpAir").objectReferenceValue = fx["jumpAir"];
                so.FindProperty("land").objectReferenceValue = null;
                so.FindProperty("dashGround").objectReferenceValue = null;
                so.FindProperty("dashAir").objectReferenceValue = fx["dashAir"];
                so.FindProperty("walkStep").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
