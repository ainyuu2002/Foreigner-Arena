using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FA.Core;
using FA.Effects;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG LẠI SPECIAL 1 "Trace kiếm → lao chém → chém kết" cho Emiya Archer THEO CHUẨN MUGEN
    /// state 1000 (idempotent) — QCF+L, 1 ô mana, bản đất (air 1050 ngoài scope):
    ///  - Anim 1000 (cast ~30t): triệu kiếm Caladbolg — cast flash (tái dùng slot 0 S3), voice S1000,
    ///    kiếm GLOW overlay (sprite 12-553xx bám thân), trace-on S6000 + ring S1005-5, burst tay 789.
    ///    MUGEN có SuperPause 30t + BG darken + hold-charge — port BỎ (special không freeze thế giới).
    ///  - Anim 1001 (dash 20t): LAO x=35px/t giảm dần → MoveImpulse 13 u/s (linearDamping lo decay).
    ///    1 hit (hitbox idx 10, Clsn1 −12..46), dmg 50→1.3%, **fall=1 → causesLaunch văng ngay**, góc 53°.
    ///  - Anim 1002 (chém kết 69t): hitbox idx 11 TO (−38..47 × 62px), dmg 100→2.7%, causesLaunch,
    ///    góc 67° hất lên-trước, hitstop 15. Vệt cung 764 + whoosh S205-4 TÁI DÙNG slot 53 (M4_Swing).
    ///  - 2 hit đều causesLaunch → KHÔNG cần KnockbackPoint; không followUps → không cancel window.
    ///  - Per-hitbox moveOverride (dash/finisher dmg khác nhau — pattern multi-hit override) + wire
    ///    SpecialController.hitboxController (special MELEE đầu tiên — trước giờ toàn projectile).
    /// Cấp phát: StateId 19 (GIỮ — asset/Animator cũ), hitbox idx 10-11, CEP slot 67-76.
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.Special1SetupTool.Run
    /// </summary>
    public static class Special1SetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Special1";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/SampleFighter";
        private const string ControllerPath = AnimDir + "/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataDir = "Assets/_Project/Data/Emiya_Archer/Special_Move";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const float Fps = 60f;
        private const int StateId = 19;       // GIỮ StateId/state "Special_1" cũ.
        private const int CepSlotBase = 67;   // 0-66 đã dùng (H tới 66) — xem memory pipeline.
        private const int HitboxBase = 10;    // 0-9 đã dùng (L/M tới 9).

        // ---- frame data: 3 anim MUGEN nối liền (tick tích lũy, sprite 12-xxx trong Special1/) ----
        // A cast (anim 1000 cắt tại t30 — không hold-charge), B dash (1001, blank flicker → giữ thân
        // hiện), C chém kết (1002).
        private static readonly (int tick, int sprite)[] Frames =
        {
            (0, 550), (4, 551), (10, 552), (14, 553), (18, 554), (22, 555), (27, 556),   // A cast
            (30, 561), (38, 562), (42, 561), (46, 562),                                   // B dash
            (50, 563), (61, 564), (67, 565), (71, 566), (75, 567), (79, 568), (83, 569),  // C finisher
            (93, 570), (97, 571), (107, 572), (111, 573), (115, 574),
        };
        private const int Len = 119;

        private const int DashImpulseTick = 38;  // MUGEN elem4 anim 1001 — velset 35px/t bắt đầu.
        private const int DashHitOn = 36, DashHitOff = 50;      // Clsn1 anim 1001 (elem 3 → hết).
        private const int FinHitOn = 50, FinHitOff = 61;        // Clsn1 anim 1002 (elem 1-4).

        [MenuItem("FA/Tools/Setup Archer Special 1 theo MUGEN (trace + lao chém)")]
        public static void Run()
        {
            ConfigureSpriteImports();
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AnimationClip clip = CreateClip();
            MoveData dashHit = CreateDashHitMoveData();
            MoveData finisher = CreateFinisherMoveData();
            UpdateMainMoveData(clip);
            UpdateAnimatorState(clip);
            WireCharacterPrefab(fx, dashHit, finisher);
            AssetDatabase.SaveAssets();
            Debug.Log("[Special1Setup] DONE — Special 1 trace→lao→chém kết theo MUGEN 1000-1002: " +
                      "QCF+L, dash 1.3% launch + finisher 2.7% launch, hitbox 10-11, CEP 67-76.");
        }

        // ---------------------------------------------------------------- 1. Import settings
        private static void ConfigureSpriteImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteDir, VfxDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                bool isVfx = path.StartsWith(VfxDir);
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

        private static string[] Range(int group, int from, int count) =>
            Enumerable.Range(from, count).Select(i => $"{group}-{i}").ToArray();

        // ---------------------------------------------------------------- 2. VFX prefab + EffectData
        private static Dictionary<string, EffectData> CreateEffectAssets()
        {
            Color white = Color.white;
            // Anim 1007: kiếm glow trên tay (sprite THÂN 12-553xx, pad_bottom — bám root pivot chân).
            GameObject swordGlow = CreateBodyOverlayPrefab("S1_SwordGlow_VFX",
                new[] { "12-55300", "12-55400", "12-55500" }, 15f, 0.3f);
            // Anim 1012: burst năng lượng ở tay (789 scale .4 — nguồn đã có).
            GameObject handBurst = NormalLSetupTool.CreateTrailPrefab("S1_HandBurst_VFX",
                Range(789, 0, 8), 30f, new Vector2(0.4f, 0.4f), 0f, white, 7);
            // Anim 7138: vệt đâm khi lao (770, 1t = 60fps; MUGEN angle 20 → z = 180−20 = 160).
            GameObject dashTrail = NormalLSetupTool.CreateTrailPrefab("S1_DashTrail_VFX",
                Range(770, 0, 6), 60f, new Vector2(0.3f, 0.5f), 160f, white, 7);
            // Anim 8008: speed lines khi lao (711-40..55, 2t = 30fps, scale .55).
            GameObject speedLines = NormalLSetupTool.CreateTrailPrefab("S1_SpeedLines_VFX",
                Range(711, 40, 16), 30f, new Vector2(0.55f, 0.55f), 0f, white, 6);
            // Anim 8000: bụi đất bốc khi chém kết (710-0..6, 2t = 30fps, scale .55).
            GameObject finishDust = NormalLSetupTool.CreateTrailPrefab("S1_FinishDust_VFX",
                Range(710, 0, 7), 30f, new Vector2(0.55f, 0.55f), 0f, white, 6);

            AudioClip Clip(string n) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/{n}.wav");
            var result = new Dictionary<string, EffectData>();
            EffectData Make(string name, GameObject vfx, Vector2 offset, bool attach, string[] clips, float vol)
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
                fx.attachToOwner = attach;
                fx.sfxClips = clips.Select(Clip).Where(c => c != null).ToArray();
                if (fx.sfxClips.Length != clips.Length)
                    Debug.LogError($"[Special1Setup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = 1f;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            Make("S1_Voice", null, Vector2.zero, false, new[] { "S1000_0" }, 0.9f);                  // 67
            Make("S1_SwordGlow", swordGlow, Vector2.zero, true, new string[0], 1f);                  // 68
            Make("S1_TraceOn", null, Vector2.zero, false, new[] { "S6000_0" }, 0.9f);                // 69
            Make("S1_TraceRing", null, Vector2.zero, false, new[] { "S1005_5" }, 0.9f);              // 70
            Make("S1_HandBurst", handBurst, new Vector2(0.0f, 0.45f), true, new string[0], 1f);      // 71
            Make("S1_DashCue", null, Vector2.zero, false, new[] { "S1005_0" }, 1f);                  // 72
            Make("S1_DashTrail", dashTrail, new Vector2(0.30f, 0.20f), true, new string[0], 1f);     // 73
            Make("S1_SpeedLines", speedLines, new Vector2(0.0f, 0.30f), false, new string[0], 1f);   // 74
            Make("S1_FinishDust", finishDust, new Vector2(0.30f, 0.05f), false, new string[0], 1f);  // 75
            Make("S1_Sheathe", null, Vector2.zero, false, new[] { "S1005_10" }, 0.8f);               // 76
            return result;
        }

        /// <summary>Prefab overlay dùng sprite THÂN (pad_bottom trong Special1/ — KHÔNG phải VfxDir):
        /// flipbook loop + tự hủy sau lifeSeconds. Attach vào root (pivot chân trùng) → glow bám tay.</summary>
        private static GameObject CreateBodyOverlayPrefab(string name, string[] frames, float fps,
                                                          float lifeSeconds)
        {
            string path = $"Assets/_Project/Prefabs/VFX/{name}.prefab";
            var root = new GameObject(name);
            try
            {
                Sprite[] sprites = frames.Select(n => Special3SetupTool.LoadSprite(SpriteDir, n))
                                         .Where(s => s != null).ToArray();
                if (sprites.Length != frames.Length)
                    Debug.LogError($"[Special1Setup] {name}: thiếu sprite ({sprites.Length}/{frames.Length})");
                var sr = root.AddComponent<SpriteRenderer>();
                sr.sprite = sprites.Length > 0 ? sprites[0] : null;
                sr.sortingOrder = 7;
                var flip = root.AddComponent<FlipbookOneShot>();
                var so = new SerializedObject(flip);
                SerializedProperty pFrames = so.FindProperty("frames");
                pFrames.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                    pFrames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                so.FindProperty("framesPerSecond").floatValue = fps;
                so.FindProperty("loop").boolValue = true;
                so.FindProperty("lifeSeconds").floatValue = lifeSeconds;
                so.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // ---------------------------------------------------------------- 3. Clip
        private static AnimationEvent Ev(int tick, string fn, int i = 0) =>
            new AnimationEvent { time = tick / Fps, functionName = fn, intParameter = i };

        private static AnimationClip CreateClip()
        {
            var clip = new AnimationClip { frameRate = Fps };
            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, int spriteIdx) in Frames)
            {
                Sprite s = Special3SetupTool.LoadSprite(SpriteDir, $"12-{spriteIdx}");
                if (s == null) { Debug.LogError($"[Special1Setup] Thiếu sprite 12-{spriteIdx}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            keys.Add(new ObjectReferenceKeyframe { time = (Len - 0.5f) / Fps, value = keys[keys.Count - 1].value });
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());

            var events = new[]
            {
                // A — cast (anim 1000): flash + voice + glow kiếm + trace + burst tay.
                Ev(0, "PlayVfx", 0), Ev(0, "PlaySfx", 0),          // cast flash S3 (tái dùng slot 0)
                Ev(10, "PlaySfx", 67),                             // voice S1000-0 (elem 3)
                Ev(14, "PlayVfx", 68),                             // kiếm glow overlay (elem 4)
                Ev(18, "PlaySfx", 69), Ev(18, "PlaySfx", 70),      // trace-on S6000 + ring S1005-5 (elem 5)
                Ev(22, "PlayVfx", 71),                             // burst tay 789 (elem 4+4t)
                // B — dash (anim 1001): impulse lao + whoosh + vệt + speed lines + hitbox 10.
                Ev(30, "PlaySfx", 72),                             // S1005-0 kiếm rít (elem 2)
                Ev(DashImpulseTick, "MoveImpulse", 0),             // 13 u/s (MUGEN 35px/t decel — damping lo)
                Ev(DashImpulseTick, "PlaySfx", 2),                 // whoosh S205-2 (tái dùng slot S3)
                Ev(DashImpulseTick, "PlayVfx", 73),                // vệt đâm 770 z=160
                Ev(DashImpulseTick, "PlayVfx", 42),                // bụi chân (tái dùng slot L)
                Ev(40, "PlayVfx", 74),                             // speed lines 8008
                Ev(DashHitOn, "HitboxOn", 10), Ev(DashHitOff, "HitboxOff", 10),
                // C — chém kết (anim 1002): hitbox 11 to + vệt cung M4 + bụi + sheathe.
                Ev(FinHitOn, "HitboxOn", 11), Ev(FinHitOff, "HitboxOff", 11),
                Ev(52, "PlaySfx", 53), Ev(52, "PlayVfx", 53),      // vệt 764 z40 + S205-4 (tái dùng M4_Swing)
                Ev(52, "PlayVfx", 75),                             // bụi đất 710
                Ev(93, "PlaySfx", 76),                             // hạ kiếm S1005-10 (elem 11)
                Ev(117, "RecoveryEnd"),
            };
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            string path = $"{AnimDir}/Special_1.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------------------------------------------------------------- 4. MoveData
        private static MoveData LoadOrCreate(string path)
        {
            var move = AssetDatabase.LoadAssetAtPath<MoveData>(path);
            if (move == null)
            {
                move = ScriptableObject.CreateInstance<MoveData>();
                AssetDatabase.CreateAsset(move, path);
            }
            return move;
        }

        /// <summary>Hit LAO (HitDef anim 1001): dmg 50→1.3%, fall=1 → causesLaunch, ground.velocity
        /// (-3,-4) → góc ~53° hất lên-sau (rời xa attacker). moveOverride trên hitbox idx 10.</summary>
        private static MoveData CreateDashHitMoveData()
        {
            var move = LoadOrCreate($"{DataDir}/Special_1_DashHit.asset");
            move.moveId = "special1_dash";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.motion = MotionInput.QCF;
            move.button = InputButton.L;
            move.manaCostUnits = 0;
            move.manaGainMultiplier = 0.5f;           // balance: special hệ số 0.5.
            move.baseDamagePercent = 1.3f;
            move.launchAngle = 53f;
            move.baseKnockback = 5f;
            move.knockbackGrowth = 0.1f;
            move.isSpike = false;
            move.causesLaunch = true;
            move.role = MoveRole.Normal;   // melee trực tiếp (không projectile — luật P16 pending áp dụng).
            move.hitsLow = true;                      // guardflag MA.
            move.hitsHigh = true;
            move.hitstunFrames = 25;
            move.hitstopFrames = 6;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            move.chargedVariant = null;
            move.hitEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Medium1.asset");
            move.blockEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Hit CHÉM KẾT (HitDef anim 1002): dmg 100→2.7%, fall=1, ground.velocity (3,-7.1)
        /// → hất LÊN-TRƯỚC góc ~67°, hitstop 15. moveOverride trên hitbox idx 11.</summary>
        private static MoveData CreateFinisherMoveData()
        {
            var move = LoadOrCreate($"{DataDir}/Special_1_Finisher.asset");
            move.moveId = "special1_finisher";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.motion = MotionInput.QCF;
            move.button = InputButton.L;
            move.manaCostUnits = 0;
            move.manaGainMultiplier = 0.5f;
            move.baseDamagePercent = 2.7f;
            move.launchAngle = 67f;
            move.baseKnockback = 8f;
            move.knockbackGrowth = 0.12f;
            move.isSpike = false;
            move.causesLaunch = true;
            move.role = MoveRole.Normal;   // melee trực tiếp (không projectile — luật P16 pending áp dụng).
            move.hitsLow = true;
            move.hitsHigh = true;
            move.hitstunFrames = 28;
            move.hitstopFrames = 15;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            move.chargedVariant = null;
            move.hitEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Heavy2.asset");
            move.blockEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Special_1 chính (asset CŨ — GIỮ guid để MoveSet binding nguyên): dmg 0 (dmg nằm ở
        /// 2 hitbox override), chỉ mang clip + impulse lao + cost 1 ô mana.</summary>
        private static void UpdateMainMoveData(AnimationClip clip)
        {
            var move = AssetDatabase.LoadAssetAtPath<MoveData>($"{DataDir}/Special_1.asset");
            if (move == null) { Debug.LogError("[Special1Setup] Không thấy Special_1.asset"); return; }
            move.moveId = "special1";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.QCF;
            move.button = InputButton.L;
            move.chargeFrames = 0;
            move.chargedVariant = null;
            move.manaCostUnits = 1;
            move.manaGainMultiplier = 0.5f;
            move.baseDamagePercent = 0f;
            move.causesLaunch = false;
            move.role = MoveRole.Normal;   // melee trực tiếp (không projectile — luật P16 pending áp dụng).
            move.hitstunFrames = 0;
            move.hitstopFrames = 0;
            move.motionImpulses = new List<Vector2> { new Vector2(13f, 0f) };   // lao (MUGEN 35px/t decel).
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
        }

        // ---------------------------------------------------------------- 5. Animator
        private static void UpdateAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Special1Setup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Special_1");
            if (st == null) { Debug.LogError("[Special1Setup] Không thấy state Special_1"); return; }
            st.motion = clip;
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- 6. Prefab wiring
        private static void WireCharacterPrefab(Dictionary<string, EffectData> fx,
                                                MoveData dashHit, MoveData finisher)
        {
            var hitChannel = AssetDatabase.LoadAssetAtPath<HitEventChannelSO>(HitLandedChannelPath);
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                FA.Combat.HitboxAuthoring MakeHitbox(string name, Vector2 offset, Vector2 size, MoveData over)
                {
                    Transform t = root.transform.Find(name);
                    GameObject go = t != null ? t.gameObject : new GameObject(name);
                    if (t == null) go.transform.SetParent(root.transform, false);
                    go.layer = LayerMask.NameToLayer(GameLayers.HitboxName);
                    var box = go.GetComponent<BoxCollider2D>();
                    if (box == null) box = go.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.offset = offset;
                    box.size = size;
                    box.enabled = false;
                    var hb = go.GetComponent<FA.Combat.HitboxAuthoring>();
                    if (hb == null) hb = go.AddComponent<FA.Combat.HitboxAuthoring>();
                    var so = new SerializedObject(hb);
                    so.FindProperty("hitChannel").objectReferenceValue = hitChannel;
                    so.FindProperty("box").objectReferenceValue = box;
                    so.FindProperty("moveOverride").objectReferenceValue = over;   // dmg riêng từng hit.
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return hb;
                }
                // Clsn1 union + nới ~0.06u về trước (bài học L). CẦN TEST.
                var hbDash = MakeHitbox("Hitbox_S1Dash", new Vector2(0.20f, 0.235f), new Vector2(0.64f, 0.47f), dashHit);
                var hbFin = MakeHitbox("Hitbox_S1Fin", new Vector2(0.05f, 0.31f), new Vector2(0.85f, 0.62f), finisher);

                var hbCtrl = root.GetComponentInChildren<FA.Combat.HitboxController>();
                if (hbCtrl == null) { Debug.LogError("[Special1Setup] Prefab thiếu HitboxController!"); }
                else
                {
                    var so = new SerializedObject(hbCtrl);
                    SerializedProperty arr = so.FindProperty("hitboxes");
                    if (arr.arraySize < HitboxBase + 2) arr.arraySize = HitboxBase + 2;   // GIỮ 0-9.
                    arr.GetArrayElementAtIndex(HitboxBase + 0).objectReferenceValue = hbDash;
                    arr.GetArrayElementAtIndex(HitboxBase + 1).objectReferenceValue = hbFin;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // SpecialController.hitboxController PHẢI gán cho special MELEE (memory: trống = hit
                // null/stale — trước giờ mọi special là projectile nên có thể chưa gán).
                var spCtrl = root.GetComponent<FA.Specials.SpecialController>();
                if (spCtrl == null) { Debug.LogError("[Special1Setup] Prefab thiếu SpecialController!"); }
                else if (hbCtrl != null)
                {
                    var so = new SerializedObject(spCtrl);
                    so.FindProperty("hitboxController").objectReferenceValue = hbCtrl;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var cep = root.GetComponent<CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[Special1Setup] Prefab thiếu CharacterEffectPlayer!"); }
                else
                {
                    var so = new SerializedObject(cep);
                    SerializedProperty arr = so.FindProperty("effects");
                    string[] slots = { "S1_Voice", "S1_SwordGlow", "S1_TraceOn", "S1_TraceRing",
                                       "S1_HandBurst", "S1_DashCue", "S1_DashTrail", "S1_SpeedLines",
                                       "S1_FinishDust", "S1_Sheathe" };
                    if (arr.arraySize < CepSlotBase + slots.Length) arr.arraySize = CepSlotBase + slots.Length;
                    for (int i = 0; i < slots.Length; i++)
                        arr.GetArrayElementAtIndex(CepSlotBase + i).objectReferenceValue = fx[slots[i]];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
