using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG ULTIMATE "Unlimited Blade Works" cho Emiya Archer (idempotent) từ MUGEN state 3000:
    ///  PHA 1 — NIỆM CHÚ (~15.5s, khớp voice gốc S3002-0): ChantStart (freeze + tối nhẹ + zoom 1/2)
    ///  + 7 dòng chữ ChantLine + thân anim 3000/3001.
    ///  PHA 2 — FLASH: SuperFlashStart(2) (portrait 9000-12) + voice "…Unlimited Blade Works!"
    ///  + dòng chữ cuối + thân anim 3002 (gồng).
    ///  PHA 3 — INSTALL: UltimateInstall → UBWInstallController đổi bg (sprite nhóm 25) + BGM +
    ///  buff (+dmg gây/−dmg nhận) + nạp đầy nộ rồi drain 12s; hết nộ tự revert. 2 super-UBW làm SAU.
    /// Kích hoạt: P10 — trong Rage Mode bấm nút Rage (DefenseController.ultimateMove).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.UltimateSetupTool.Run
    /// </summary>
    public static class UltimateSetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Ultimate";
        private const string BgDir = "Assets/_Project/Art/Characters/Emiya Archer/UBW_Background";
        private const string TextDir = "Assets/_Project/Art/Characters/Emiya Archer/ChantText";
        private const string PortraitDir = "Assets/_Project/Art/Characters/Emiya Archer/Portrait";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string ClipPath = "Assets/_Project/Animations/SampleFighter/Ultimate_Chant.anim";
        private const string ControllerPath = "Assets/_Project/Animations/SampleFighter/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string UltiDataDir = "Assets/_Project/Data/Emiya_Archer/Ultimate";
        private const string RageClipPath = "Assets/_Project/Animations/SampleFighter/RageMode_Enter.anim";
        private const string DefenseConfigPath = "Assets/_Project/Data/Emiya_Archer/DefenseConfig.asset";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string BgPrefabPath = "Assets/_Project/Prefabs/VFX/UBW_Background.prefab";

        private const int StateId = 38;        // 35=Special_3, 36=Super_1, 37=Super_2.
        private const int PortraitIndex = 2;   // portraits: 0=Super1, 1=Super2, 2=Ultimate (9000-12).
        private const int EffectSlotBase = 16; // CEP: 0-8 S3+Super1, 9-15 Super2 → Ultimate = 16-19.
        private const float Fps = 60f;

        // Timeline theo VOICE THẬT (user tìm ra): 6 dòng chant = voice 800-0..5 (2.32/3.12/2.94/2.56/
        // 3.55/3.46s) phát nối tiếp +10t nghỉ; 2 dòng kết: "So as I pray,"=S3000-0 (line 6, trong flash),
        // "Unlimited"=S3000-5 (line 7), "Blade Works"=S3000-6 (PlaySfx 20). Chữ = SPRITE GỐC group
        // 1500-1507 (flipbook viết dần, voice + chữ cùng ChantLine event). CẦN TEST (kéo event).
        private static readonly int[] LineTicks = { 30, 180, 378, 565, 729, 952 };
        private const int FlashTick = 1170;
        private const int UnlimitedTick = 1281;    // ChantLine(7) — "Unlimited".
        private const int BladeWorksTick = 1345;   // PlaySfx 20 — "Blade Works."
        private const int InstallTick = 1422;
        private const int RecoveryEndTick = 1500;

        // Số frame flipbook mỗi dòng chữ (sprite group 1500..1507 trong ChantText/).
        private static readonly int[] LineFrameCounts = { 32, 39, 38, 38, 41, 41, 24, 30 };

        [MenuItem("FA/Tools/Setup Archer Ultimate (Unlimited Blade Works)")]
        public static void Run()
        {
            EnsureFolders();
            ConfigureSpriteImports();
            AnimationClip clip = CreateClip();
            CreateRageEnterClip();   // asset giữ cho nhân vật KHÁC (không anim đặc biệt) — Emiya không dùng.
            MoveData move = CreateMoveData(clip);
            GameObject bg = UltimateFxBuilder.CreateBackgroundPrefab();
            Dictionary<string, EffectData> fx = UltimateFxBuilder.CreateEffectAssets();
            AddAnimatorState(clip);
            // User chốt: với Archer, ẤN NỘ LẦN 1 (RageModeState → StateId 30) = chạy LUÔN chant→UBW —
            // state 30 trỏ THẲNG clip chant (anim vào Rage Mode của Archer chính là 3 part này).
            AddRageAnimatorState(clip);
            WireCharacterPrefab(move, bg, fx);
            AssetDatabase.SaveAssets();
            Debug.Log("[UltimateSetup] DONE — UBW: chant 15.5s + flash 9000-12 + install (bg nhóm 25, " +
                      "buff 1.3×/0.7×, drain nộ 12s). 2 super-UBW làm sau.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(UltiDataDir))
                AssetDatabase.CreateFolder("Assets/_Project/Data/Emiya_Archer", "Ultimate");
        }

        private static void ConfigureSpriteImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir, BgDir, TextDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)(path.StartsWith(SpriteDir)
                    ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.maxTextureSize = 2048;   // bg 2000px giữ nét.
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        // Thân: anim 3000 (750 hold + 751-755) → 3001 loop niệm chú (756-767 ×8 vòng) →
        // 3002 gồng (768-774 ×2 vòng, trong flash) → 3003 kết (772-778).
        private static (int, string)[] BuildFrames()
        {
            var list = new List<(int, string)>();
            int t = 0;
            void Add(int sprite, int dur) { list.Add((t, $"12-{sprite}")); t += dur; }
            Add(750, 109);
            Add(751, 6); Add(752, 9); Add(753, 8); Add(754, 8); Add(755, 70);      // → 210
            for (int c = 0; c < 10; c++)
                for (int s = 756; s <= 767; s++) Add(s, 8);                        // → 1170 (flash)
            // Anim 3002 ĐÚNG GỐC (user bắt lỗi "tay giơ xong lại hạ"): 768-771 GIƠ TAY 1 LẦN,
            // LoopStart, rồi CHỈ loop 772-778 (tay đã giơ, đuôi áo đung đưa).
            for (int s = 768; s <= 771; s++) Add(s, 8);                            // → 1202 (giơ tay 1 lần)
            for (int c = 0; c < 5; c++)
                for (int s = 772; s <= 778; s++) Add(s, 8);                        // → 1482 (loop giữ tay)
            Add(770, 24);                                                          // → 1506 (hạ tay kết, khớp 3003)
            return list.ToArray();
        }

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, string spriteName) in BuildFrames())
            {
                Sprite s = Special3SetupTool.LoadSprite(SpriteDir, spriteName);
                if (s == null) { Debug.LogError($"[UltimateSetup] Thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());

            AnimationEvent Ev(int tick, string fn, int i = 0) =>
                new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

            var events = new List<AnimationEvent>
            {
                Ev(0, "ChantStart"),                     // freeze + dim nhẹ + zoom 1/2 + NHẠC chuỗi tự start.
                Ev(0, "PlaySfx", 16),                    // announce ultimate (S6100,10)
                Ev(FlashTick, "SuperFlashStart", PortraitIndex),
                Ev(FlashTick + 1, "ChantLine", 6),       // "So as I pray," (sprite 1506 + voice S3000,0)
                                                         // — SAU FlashStart để canvas bật lại.
                Ev(UnlimitedTick, "ChantLine", 7),       // "Unlimited" (sprite 1507 + voice S3000,5)
                Ev(BladeWorksTick, "PlaySfx", 20),       // "Blade Works." (S3000,6)
                Ev(InstallTick - 2, "SuperFlashEnd"),
                Ev(InstallTick, "ChantEnd"),
                Ev(InstallTick, "UltimateInstall"),
                Ev(InstallTick, "PlaySfx", 19),          // install boom (S3105,0)
                Ev(InstallTick, "PlayVfx", 19),          // sóng năng lượng (anim 3600 / sprite 844)
                Ev(RecoveryEndTick, "RecoveryEnd"),
            };
            for (int i = 0; i < LineTicks.Length; i++)
                events.Add(Ev(LineTicks[i], "ChantLine", i));   // chữ + voice dòng i cùng 1 event.
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            AssetDatabase.DeleteAsset(ClipPath);
            AssetDatabase.CreateAsset(clip, ClipPath);
            return clip;
        }

        /// <summary>Clip "bùng nộ" NGẮN cho RageModeState (StateId 30) — prefab Emiya CHƯA có tầng P10
        /// nên phải dựng luôn để đường kích Ultimate (Rage Mode → nút Rage) chạy được. Tái dùng pose
        /// gồng của chant (12-751..755).</summary>
        private static AnimationClip CreateRageEnterClip()
        {
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new List<ObjectReferenceKeyframe>();
            int t = 0;
            foreach ((int s, int d) in new[] { (751, 4), (752, 6), (753, 6), (754, 6), (755, 16) })
            {
                keys.Add(new ObjectReferenceKeyframe
                {
                    time = t / Fps,
                    value = Special3SetupTool.LoadSprite(SpriteDir, $"12-{s}"),
                });
                t += d;
            }
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());
            AnimationUtility.SetAnimationEvents(clip, new[]
            {
                new AnimationEvent { time = 0f, functionName = "PlaySfx", intParameter = 23 },   // lửa bùng (S505,1)
                new AnimationEvent { time = 36f / Fps, functionName = "RecoveryEnd" },
            });
            AssetDatabase.DeleteAsset(RageClipPath);
            AssetDatabase.CreateAsset(clip, RageClipPath);
            return clip;
        }

        private static void AddRageAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) return;
            if (!controller.parameters.Any(p => p.name == "Rage"))
                controller.AddParameter("Rage", AnimatorControllerParameterType.Bool);   // aura layer sau.
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "RageMode_Enter");
            if (state == null) state = sm.AddState("RageMode_Enter");
            state.motion = clip;
            state.writeDefaultValues = true;
            AnimatorStateTransition tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
            if (tr == null) tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;
            tr.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "StateId", threshold = 30 }
            };
            EditorUtility.SetDirty(controller);
        }

        private static MoveData CreateMoveData(AnimationClip clip)
        {
            string path = $"{UltiDataDir}/Ultimate_UBW.asset";
            var move = AssetDatabase.LoadAssetAtPath<MoveData>(path);
            if (move == null)
            {
                move = ScriptableObject.CreateInstance<MoveData>();
                AssetDatabase.CreateAsset(move, path);
            }
            move.moveId = "ultimate_ubw";
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.None;
            move.baseDamagePercent = 0f;      // cinematic install — KHÔNG hitbox (khác ultimate đánh thẳng).
            move.causesLaunch = false;
            move.hitstunFrames = 0;
            move.hitstopFrames = 0;
            move.motionImpulses = new List<Vector2>();
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
            return move;
        }

        private static void AddAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[UltimateSetup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Ultimate_Chant");
            if (state == null) state = sm.AddState("Ultimate_Chant");
            state.motion = clip;
            state.writeDefaultValues = true;
            AnimatorStateTransition tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
            if (tr == null) tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;
            tr.conditions = new[]
            {
                new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = "StateId", threshold = StateId }
            };
            EditorUtility.SetDirty(controller);
        }

        private static void WireCharacterPrefab(MoveData move, GameObject bg, Dictionary<string, EffectData> fx)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                // 1) TẦNG P10 (prefab Emiya CHƯA có — P10 dựng code, chưa wire prefab): BuffAggregator +
                //    RageModeController + DefenseController + DefenseConfig asset. Auto-find lo phần còn
                //    lại (relay/mana/rage qua interface Core).
                var configSo = AssetDatabase.LoadAssetAtPath<FA.Specials.DefenseConfigSO>(DefenseConfigPath);
                if (configSo == null)
                {
                    configSo = ScriptableObject.CreateInstance<FA.Specials.DefenseConfigSO>();
                    AssetDatabase.CreateAsset(configSo, DefenseConfigPath);   // defaults trong class. CẦN TEST.
                }
                if (root.GetComponent<BuffAggregator>() == null) root.AddComponent<BuffAggregator>();

                // rageModeDuration khớp drain UBW (12s) — với Archer, Rage Mode CHÍNH LÀ UBW.
                configSo.rageModeDuration = 12f;
                EditorUtility.SetDirty(configSo);

                var rage = root.GetComponent<FA.Specials.RageModeController>();
                if (rage == null) rage = root.AddComponent<FA.Specials.RageModeController>();
                var soRage = new SerializedObject(rage);
                soRage.FindProperty("config").objectReferenceValue = configSo;
                // Aura KHÔNG bật lúc vào Rage Mode (Archer: anim chant lo phần trình diễn) — chuyển
                // sang UBWInstallController bật khi VÀO UBW (user chốt). Char khác không anim đặc biệt
                // thì wire aura + Rage_Ignite vào đây như thường.
                soRage.FindProperty("auraParticles").objectReferenceValue = null;
                soRage.ApplyModifiedPropertiesWithoutUndo();

                var defense = root.GetComponent<FA.Specials.DefenseController>();
                if (defense == null) defense = root.AddComponent<FA.Specials.DefenseController>();
                var soDef = new SerializedObject(defense);
                soDef.FindProperty("config").objectReferenceValue = configSo;
                soDef.FindProperty("rageMode").objectReferenceValue = rage;
                // ultimateMove ĐỂ TRỐNG (user chốt): với Archer, ẤN NỘ LẦN 1 = chạy luôn chant→UBW
                // (Animator state 30 trỏ clip chant). Ấn nộ lần nữa = move ultimate riêng — LÀM SAU.
                soDef.FindProperty("ultimateMove").objectReferenceValue = null;
                soDef.FindProperty("hitboxController").objectReferenceValue =
                    root.GetComponentInChildren<FA.Combat.HitboxController>();
                soDef.ApplyModifiedPropertiesWithoutUndo();

                // 2) CEP slots 16-19 (giữ 0-15).
                var cep = root.GetComponent<FA.Effects.CharacterEffectPlayer>();
                if (cep != null)
                {
                    var soCep = new SerializedObject(cep);
                    SerializedProperty pEffects = soCep.FindProperty("effects");
                    // 16=announce, 19=install boom, 20="Blade Works.", 23=Rage_Ignite (char khác);
                    // 17/18/21/22 legacy → null (voice dòng qua ChantTextOverlay, nhạc qua MusicSequencePlayer).
                    var extra = new[]
                    {
                        fx["Ulti_Announce"], null, null, fx["Ulti_Install"],
                        fx["Ulti_BladeWorks"], null, null, fx["Rage_Ignite"],
                    };
                    if (pEffects.arraySize < EffectSlotBase + extra.Length)
                        pEffects.arraySize = EffectSlotBase + extra.Length;
                    for (int i = 0; i < extra.Length; i++)
                        pEffects.GetArrayElementAtIndex(EffectSlotBase + i).objectReferenceValue = extra[i];
                    soCep.ApplyModifiedPropertiesWithoutUndo();
                }

                // 3) Portrait slot 2 = 9000-12 (flash "nhắm mắt" — đổi được trong Inspector).
                var flash = root.GetComponent<FA.Effects.SuperFlashController>();
                if (flash != null)
                {
                    var soFl = new SerializedObject(flash);
                    SerializedProperty pP = soFl.FindProperty("portraits");
                    if (pP.arraySize < PortraitIndex + 1) pP.arraySize = PortraitIndex + 1;
                    pP.GetArrayElementAtIndex(PortraitIndex).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/9000-12.png");
                    soFl.ApplyModifiedPropertiesWithoutUndo();
                }

                // 4) ChantController (freeze + tối nhẹ + zoom 1/2).
                if (root.GetComponent<FA.Effects.ChantController>() == null)
                    root.AddComponent<FA.Effects.ChantController>();

                // 5) Canvas chữ niệm chú + ChantTextOverlay.
                WireChantText(root);

                // 6) NHẠC NỀN chuỗi (S3002-0→5→6 liền tù tì từ chant tới thoát UBW — kênh nhạc riêng,
                //    sau này route AudioMixer "Music" khi có menu âm lượng).
                var musicPlayer = root.GetComponent<FA.Specials.MusicSequencePlayer>();
                if (musicPlayer == null) musicPlayer = root.AddComponent<FA.Specials.MusicSequencePlayer>();
                var soM = new SerializedObject(musicPlayer);
                SerializedProperty pTracks = soM.FindProperty("tracks");
                string[] trackNames = { "S3002_0", "S3002_5", "S3002_6" };
                pTracks.arraySize = trackNames.Length;
                for (int i = 0; i < trackNames.Length; i++)
                    pTracks.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{trackNames[i]}.wav");
                soM.ApplyModifiedPropertiesWithoutUndo();

                // 7) UBWInstallController (bg + buff + drain + aura đỏ khi VÀO UBW + stop nhạc khi thoát).
                var ubw = root.GetComponent<FA.Specials.UBWInstallController>();
                if (ubw == null) ubw = root.AddComponent<FA.Specials.UBWInstallController>();
                var soU = new SerializedObject(ubw);
                soU.FindProperty("backgroundPrefab").objectReferenceValue = bg;
                soU.FindProperty("music").objectReferenceValue = musicPlayer;
                soU.FindProperty("auraParticles").objectReferenceValue =
                    UltimateFxBuilder.BuildRageAura(root);
                soU.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void WireChantText(GameObject root)
        {
            Transform old = root.transform.Find("ChantCanvas");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var canvasGo = new GameObject("ChantCanvas");
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;   // dưới cut-in flash (500), trên mọi thứ khác.
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // Image hiển thị SPRITE CHỮ GỐC MUGEN (group 1500-1507 — flipbook "viết dần").
            var imgGo = new GameObject("ChantText", typeof(RectTransform));
            imgGo.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 90f);
            var image = imgGo.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;

            // Voice: dòng 0-5 = Ulti_Line0..5 (group 800); dòng 6 = "So as I pray," (S3000_0);
            // dòng 7 = "Unlimited" (S3000_5). "Blade Works." (S3000_6) phát qua PlaySfx 20 sau đó.
            string[] voices = { "Ulti_Line0", "Ulti_Line1", "Ulti_Line2", "Ulti_Line3",
                                "Ulti_Line4", "Ulti_Line5", "S3000_0", "S3000_5" };

            var overlay = root.GetComponent<FA.Effects.ChantTextOverlay>();
            if (overlay == null) overlay = root.AddComponent<FA.Effects.ChantTextOverlay>();
            var so = new SerializedObject(overlay);
            so.FindProperty("image").objectReferenceValue = image;
            SerializedProperty pLines = so.FindProperty("lines");
            pLines.arraySize = LineFrameCounts.Length;
            for (int i = 0; i < LineFrameCounts.Length; i++)
            {
                SerializedProperty line = pLines.GetArrayElementAtIndex(i);
                SerializedProperty pFrames = line.FindPropertyRelative("frames");
                pFrames.arraySize = LineFrameCounts[i];
                for (int f = 0; f < LineFrameCounts[i]; f++)
                {
                    var s = Special3SetupTool.LoadSprite(TextDir, $"{1500 + i}-{f}");
                    if (s == null) Debug.LogError($"[UltimateSetup] Thiếu chữ {1500 + i}-{f}");
                    pFrames.GetArrayElementAtIndex(f).objectReferenceValue = s;
                }
                line.FindPropertyRelative("voice").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{voices[i]}.wav");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
