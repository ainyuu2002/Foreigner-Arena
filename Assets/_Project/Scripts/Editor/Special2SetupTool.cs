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
    /// TOOL DỰNG LẠI SPECIAL 2 "Xoay kiếm bay lên" cho Emiya Archer THEO CHUẨN MUGEN state 1100
    /// (idempotent) — QCF+M, 1 ô mana, BẢN ĐẤT (air anim 1102 ngoài scope):
    ///  - Anim 1100 (windup 34t): voice S1100 + nhích tới (Veladd 1.4 → drift 2.5) + bụi, cue phóng
    ///    S1105 + bụi bung 8077. MUGEN có SuperPause 30t — BỎ (như Special 1), giữ cast flash slot 0.
    ///  - Anim 1101 (xoáy 33t): NHẢY XOÁY — MoveImpulse (1.2, 6.0) (MUGEN vel y=-7 + gravity yếu hơn
    ///    → bù 6 u/s, CẦN TEST độ cao ~1u). HitDef persistent=4 → port 4 HIT CARRY (hitbox idx 12,
    ///    4 cặp On/Off, mỗi hit causesLaunch góc 85° hất NHẸ lên = địch bay theo — thay TargetBind
    ///    MUGEN không có trong engine) + 1 HIT KẾT (hitbox idx 13, moveOverride Special_2_Ender —
    ///    GIỮ guid asset cũ — góc 60° văng xa). Trail xoay = sprite 794 (anim 1120 = 0..4 lúc bắt
    ///    đầu, 1121 = 5..13 lúc kết — user xác nhận), glow overlay 12-603xx bám thân.
    ///  - Hết clip RecoveryEnd trên KHÔNG (apex) → về Idle, Motor tự rơi (MUGEN ChangeState 50 ctrl=1).
    /// Cấp phát: StateId 20 (GIỮ), hitbox idx 12-13, CEP slot 77-84.
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.Special2SetupTool.Run
    /// </summary>
    public static class Special2SetupTool
    {
        private const string SpriteDir = "Assets/_Project/Art/Characters/Emiya Archer/Special2";
        private const string VfxDir = "Assets/_Project/Art/Characters/Emiya Archer/VFX";
        private const string AudioDir = "Assets/_Project/Audio/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/SampleFighter";
        private const string ControllerPath = AnimDir + "/SampleFighter.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string DataDir = "Assets/_Project/Data/Emiya_Archer/Special_Move";
        private const string EffectsDataDir = "Assets/_Project/Data/Emiya_Archer/Effects";
        private const string HitLandedChannelPath = "Assets/_Project/Data/OnHitLandedChannel.asset";

        private const float Fps = 60f;
        private const int StateId = 20;       // GIỮ StateId/state "Special_2" cũ.
        private const int CepSlotBase = 77;   // 0-76 đã dùng (S1 tới 76).
        private const int HitboxBase = 12;    // 0-11 đã dùng (S1 tới 11).

        // ---- frame data: anim 1100 (windup) + 1101 (xoáy, sprite pad +30 theo offset .air) ----
        private static readonly (int tick, int sprite)[] Frames =
        {
            (0, 237), (4, 238), (9, 239), (13, 240), (16, 241), (19, 242), (22, 243),   // windup
            (25, 600), (28, 601), (31, 602),
            (34, 603), (37, 604), (40, 605), (43, 606), (46, 607), (49, 608),           // xoáy ×10
            (52, 609), (55, 610), (58, 603), (61, 604),
            (64, 65),                                                                    // kết xoáy
        };
        private const int Len = 67;

        private const int SpinStart = 34;     // MoveImpulse nhảy xoáy + trail + voice.

        [MenuItem("FA/Tools/Setup Archer Special 2 theo MUGEN (xoay kiếm bay lên)")]
        public static void Run()
        {
            ConfigureSpriteImports();
            Dictionary<string, EffectData> fx = CreateEffectAssets();
            AnimationClip clip = CreateClip();
            MoveData spinHit = CreateSpinHitMoveData();
            MoveData ender = UpdateEnderMoveData();
            UpdateMainMoveData(clip);
            UpdateAnimatorState(clip);
            WireCharacterPrefab(fx, spinHit, ender);
            AssetDatabase.SaveAssets();
            Debug.Log("[Special2Setup] DONE — Special 2 xoay kiếm bay lên theo MUGEN 1100/1101: " +
                      "QCF+M, 4 hit carry 0.5% + ender 0.8% văng, hitbox 12-13, CEP 77-84.");
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
            // Anim 8077: bụi bung khi phóng (711-20..25, ~2.5t = 25fps, scale .6,.8).
            GameObject launchDust = NormalLSetupTool.CreateTrailPrefab("S2_LaunchDust_VFX",
                new[] { "711-20", "711-21", "711-22", "711-23", "711-24", "711-25" }, 25f,
                new Vector2(0.6f, 0.8f), 0f, white, 6);
            // Anim 1120: vòng xoay kiếm ĐẦU (794-0..4, 3t = 20fps; MUGEN angle -10 → z=+10 y-flip. CẦN TEST).
            GameObject spinTrail = NormalLSetupTool.CreateTrailPrefab("S2_SpinTrail_VFX",
                Range(794, 0, 5), 20f, new Vector2(0.65f, 0.3f), 10f, white, 7);
            // Anim 1121: vòng xoay kiếm KẾT (794-5..13, 2-3t ≈ 25fps).
            GameObject endTrail = NormalLSetupTool.CreateTrailPrefab("S2_EndTrail_VFX",
                Range(794, 5, 9), 25f, new Vector2(0.65f, 0.3f), 10f, white, 7);
            // Anim 1105: glow overlay bám thân khi xoáy (sprite THÂN 12-603xx pad +30 — cùng pivot chân).
            GameObject spinGlow = Special1SetupToolOverlay("S2_SpinGlow_VFX",
                Enumerable.Range(0, 8).Select(i => $"12-{60300 + i * 100}").ToArray(), 20f, 0.5f);

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
                    Debug.LogError($"[Special2Setup] {name}: thiếu AudioClip ({fx.sfxClips.Length}/{clips.Length})");
                fx.volume = vol;
                fx.scale = 1f;
                EditorUtility.SetDirty(fx);
                result[name] = fx;
                return fx;
            }

            Make("S2_Voice", null, Vector2.zero, false, new[] { "S1100_0" }, 0.9f);                  // 77
            Make("S2_LaunchCue", null, Vector2.zero, false, new[] { "S1105_0" }, 1f);                // 78
            Make("S2_LaunchDust", launchDust, new Vector2(0.10f, 0.05f), false, new string[0], 1f);  // 79
            Make("S2_SpinVoice", null, Vector2.zero, false, new[] { "S200_7" }, 0.9f);               // 80
            Make("S2_SpinTrail", spinTrail, new Vector2(0.05f, 0.45f), true, new string[0], 1f);     // 81
            Make("S2_SpinGlow", spinGlow, Vector2.zero, true, new string[0], 1f);                    // 82
            Make("S2_SpinWhoosh", null, Vector2.zero, false, new[] { "S1205_1" }, 0.8f);             // 83
            Make("S2_EndTrail", endTrail, new Vector2(0.05f, 0.45f), true, new string[0], 1f);       // 84
            return result;
        }

        /// <summary>Overlay flipbook từ sprite THÂN trong Special2/ (pattern CreateBodyOverlayPrefab
        /// của Special1SetupTool — copy vì SpriteDir khác).</summary>
        private static GameObject Special1SetupToolOverlay(string name, string[] frames, float fps,
                                                           float lifeSeconds)
        {
            string path = $"Assets/_Project/Prefabs/VFX/{name}.prefab";
            var root = new GameObject(name);
            try
            {
                Sprite[] sprites = frames.Select(n => Special3SetupTool.LoadSprite(SpriteDir, n))
                                         .Where(s => s != null).ToArray();
                if (sprites.Length != frames.Length)
                    Debug.LogError($"[Special2Setup] {name}: thiếu sprite ({sprites.Length}/{frames.Length})");
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
                if (s == null) { Debug.LogError($"[Special2Setup] Thiếu sprite 12-{spriteIdx}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            keys.Add(new ObjectReferenceKeyframe { time = (Len - 0.5f) / Fps, value = keys[keys.Count - 1].value });
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys.ToArray());

            var events = new[]
            {
                // Windup (anim 1100): flash + voice; nhích tới (Veladd 1.4 → drift) + bụi; cue phóng.
                Ev(0, "PlayVfx", 0), Ev(0, "PlaySfx", 0),          // cast flash S3 (tái dùng slot 0)
                Ev(0, "PlaySfx", 77),                              // voice S1100-0
                Ev(22, "MoveDriftOn"), Ev(22, "PlayVfx", 42),      // nhích tới (elem 7) + bụi chân
                Ev(31, "PlaySfx", 78), Ev(31, "PlayVfx", 79),      // cue S1105-0 + bụi bung 8077 (elem 10)
                Ev(33, "MoveDriftOff"),
                // Xoáy (anim 1101): nhảy xoáy + trail + glow + 4 hit carry + ender.
                Ev(SpinStart, "MoveImpulse", 0),                   // (1.2, 6.0) — bay lên (MUGEN y=-7)
                Ev(SpinStart, "PlaySfx", 80),                      // kiai S200-7
                Ev(SpinStart, "PlayVfx", 81),                      // vòng xoay kiếm 794-0..4 (anim 1120)
                Ev(SpinStart, "PlayVfx", 82),                      // glow overlay 603xx (anim 1105)
                Ev(SpinStart, "PlaySfx", 83),                      // whoosh S1205-1 (lặp mỗi ~7t)
                Ev(41, "PlaySfx", 83), Ev(48, "PlaySfx", 83), Ev(55, "PlaySfx", 83),
                Ev(34, "HitboxOn", 12), Ev(38, "HitboxOff", 12),   // 4 hit carry (persistent=4 MUGEN,
                Ev(40, "HitboxOn", 12), Ev(44, "HitboxOff", 12),   //  mỗi On reset ignore → re-hit)
                Ev(46, "HitboxOn", 12), Ev(50, "HitboxOff", 12),
                Ev(52, "HitboxOn", 12), Ev(56, "HitboxOff", 12),
                Ev(58, "HitboxOn", 13), Ev(63, "HitboxOff", 13),   // ENDER (fall=1 từ elem 9 MUGEN)
                Ev(64, "PlayVfx", 84),                             // vòng kiếm kết 794-5..13 (anim 1121)
                Ev(66, "RecoveryEnd"),                             // trên KHÔNG — Motor tự rơi về đất.
            };
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());

            string path = $"{AnimDir}/Special_2.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---------------------------------------------------------------- 4. MoveData
        /// <summary>Hit XOÁY carry (MUGEN dmg 20, persistent=4, vel (-1,-7) hất LÊN): causesLaunch góc 85°
        /// lực NHỎ — mỗi hit pop địch lên bay theo attacker (thay TargetBind). moveOverride hitbox 12.</summary>
        private static MoveData CreateSpinHitMoveData()
        {
            var move = LoadOrCreate($"{DataDir}/Special_2_SpinHit.asset");
            move.moveId = "special2_spin";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.motion = MotionInput.QCF;
            move.button = InputButton.M;
            move.manaCostUnits = 0;
            move.manaGainMultiplier = 0.5f;
            move.baseDamagePercent = 0.5f;
            move.launchAngle = 85f;
            move.baseKnockback = 3f;
            move.knockbackGrowth = 0.02f;
            move.isSpike = false;
            move.causesLaunch = true;
            move.role = MoveRole.Normal;
            move.hitsLow = true;
            move.hitsHigh = true;
            move.hitstunFrames = 20;
            move.hitstopFrames = 4;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            move.chargedVariant = null;
            move.hitEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Medium1.asset");
            move.blockEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            EditorUtility.SetDirty(move);
            return move;
        }

        /// <summary>Hit KẾT (GIỮ guid Special_2_Ender.asset cũ): văng xa góc 60° kết juggle.</summary>
        private static MoveData UpdateEnderMoveData()
        {
            var move = LoadOrCreate($"{DataDir}/Special_2_Ender.asset");
            move.moveId = "special2_ender";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.motion = MotionInput.QCF;
            move.button = InputButton.M;
            move.manaCostUnits = 0;
            move.manaGainMultiplier = 0.5f;
            move.baseDamagePercent = 0.8f;
            move.launchAngle = 60f;
            move.baseKnockback = 8f;
            move.knockbackGrowth = 0.12f;
            move.isSpike = false;
            move.causesLaunch = true;
            move.role = MoveRole.Normal;
            move.hitsLow = true;
            move.hitsHigh = true;
            move.hitstunFrames = 25;
            move.hitstopFrames = 8;
            move.motionImpulses = new List<Vector2>();
            move.driftSpeed = 0f;
            move.followUps = new List<FollowUp>();
            move.chargedVariant = null;
            move.hitEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Hit_Heavy2.asset");
            move.blockEffect = AssetDatabase.LoadAssetAtPath<EffectData>($"{EffectsDataDir}/Block_Guard.asset");
            EditorUtility.SetDirty(move);
            return move;
        }

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

        /// <summary>Special_2 chính (GIỮ guid — MoveSet binding nguyên): dmg 0, mang clip + drift windup
        /// + impulse nhảy xoáy (x=1.2, y=6.0) + cost 1 ô mana.</summary>
        private static void UpdateMainMoveData(AnimationClip clip)
        {
            var move = AssetDatabase.LoadAssetAtPath<MoveData>($"{DataDir}/Special_2.asset");
            if (move == null) { Debug.LogError("[Special2Setup] Không thấy Special_2.asset"); return; }
            move.moveId = "special2";
            move.category = MoveCategory.Special;
            move.animatorStateId = StateId;
            move.clip = clip;
            move.motion = MotionInput.QCF;
            move.button = InputButton.M;
            move.chargeFrames = 0;
            move.chargedVariant = null;
            move.manaCostUnits = 1;
            move.manaGainMultiplier = 0.5f;
            move.baseDamagePercent = 0f;
            move.causesLaunch = false;
            move.role = MoveRole.Normal;
            move.hitstunFrames = 0;
            move.hitstopFrames = 0;
            move.motionImpulses = new List<Vector2> { new Vector2(1.2f, 6.0f) };   // nhảy xoáy. CẦN TEST độ cao.
            move.driftSpeed = 2.5f;                                                // windup nhích tới (Veladd 1.4).
            move.followUps = new List<FollowUp>();
            EditorUtility.SetDirty(move);
        }

        // ---------------------------------------------------------------- 5. Animator
        private static void UpdateAnimatorState(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError("[Special2Setup] Không thấy controller"); return; }
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Special_2");
            if (st == null) { Debug.LogError("[Special2Setup] Không thấy state Special_2"); return; }
            st.motion = clip;
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- 6. Prefab wiring
        private static void WireCharacterPrefab(Dictionary<string, EffectData> fx,
                                                MoveData spinHit, MoveData ender)
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
                    so.FindProperty("moveOverride").objectReferenceValue = over;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return hb;
                }
                // Clsn1Default anim 1101 union (−37..39 × −61..−17) — vòng xoáy bao quanh người.
                var hbSpin = MakeHitbox("Hitbox_S2Spin", new Vector2(0.01f, 0.39f), new Vector2(0.76f, 0.44f), spinHit);
                var hbEnd = MakeHitbox("Hitbox_S2End", new Vector2(0.01f, 0.39f), new Vector2(0.76f, 0.44f), ender);

                var hbCtrl = root.GetComponentInChildren<FA.Combat.HitboxController>();
                if (hbCtrl == null) { Debug.LogError("[Special2Setup] Prefab thiếu HitboxController!"); }
                else
                {
                    var so = new SerializedObject(hbCtrl);
                    SerializedProperty arr = so.FindProperty("hitboxes");
                    if (arr.arraySize < HitboxBase + 2) arr.arraySize = HitboxBase + 2;   // GIỮ 0-11.
                    arr.GetArrayElementAtIndex(HitboxBase + 0).objectReferenceValue = hbSpin;
                    arr.GetArrayElementAtIndex(HitboxBase + 1).objectReferenceValue = hbEnd;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var cep = root.GetComponent<CharacterEffectPlayer>();
                if (cep == null) { Debug.LogError("[Special2Setup] Prefab thiếu CharacterEffectPlayer!"); }
                else
                {
                    var so = new SerializedObject(cep);
                    SerializedProperty arr = so.FindProperty("effects");
                    string[] slots = { "S2_Voice", "S2_LaunchCue", "S2_LaunchDust", "S2_SpinVoice",
                                       "S2_SpinTrail", "S2_SpinGlow", "S2_SpinWhoosh", "S2_EndTrail" };
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
